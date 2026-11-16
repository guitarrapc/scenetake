using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using VYaml.Annotations;
using VYaml.Serialization;

/// <summary>
/// Scenario parsing, step keys, and the deterministic seed. Shared by the CLI and the playground, which
/// compile the same source; scenetake.cs reaches these members through <c>using static</c>.
/// </summary>
internal static partial class ScenarioCast
{
    internal const string DefaultPrompt = "$ ";
    internal const double DefaultSpeed = 0.05;
    internal const double DefaultJitter = 0.015;
    internal const double DefaultPreDelay = 0.2;
    internal const double DefaultPostDelay = 0.5;
    internal const double DefaultExecutionDuration = 0.05;
    internal const double DefaultSilenceThreshold = 2.0;
    internal const double DefaultMaxIdle = 0.2;
    internal const double DefaultBurstThreshold = 0.05;
    internal const string DefaultStderrColorSpec = "red";
    internal const string SgrReset = "\u001b[0m";

    // The generated formatters compare each YAML key with the member's kebab-case name. Under any other naming
    // convention in the options, VYaml first converts the YAML key to kebab case, so typing_speed, typingSpeed
    // and TypingSpeed took effect while the unknown-key check warned that scenetake does not read them.
    private static readonly YamlSerializerOptions ScenarioYamlOptions = new()
    {
        Resolver = StandardResolver.Instance,
        NamingConvention = NamingConvention.KebabCase,
    };

    internal static Scenario ParseScenario(string yaml)
    {
        var bytes = Encoding.UTF8.GetBytes(yaml);
        try
        {
            return YamlSerializer.Deserialize<Scenario>(bytes, ScenarioYamlOptions) ?? new Scenario();
        }
        catch (Exception ex) when (FindValueTypeError(yaml) is { } message)
        {
            // VYaml's message, such as "Cannot detect a scalar value as double : Scalar 50ms at Line: 9", names
            // neither the key nor its type, and its position is the token after the value.
            throw new FormatException(message, ex);
        }
    }

    /// <summary>
    /// The error for the first value that cannot be read as its key's type, such as <c>typing-speed: 50ms</c>
    /// or <c>width: 100px</c>, or null when every value fits. Only keys that scenetake reads are checked.
    /// </summary>
    internal static string? FindValueTypeError(string yaml)
    {
        try
        {
            return YamlSerializer.Deserialize<object>(Encoding.UTF8.GetBytes(yaml)) is Dictionary<object, object?> root
                ? FindValueTypeError<Scenario>(root, "")
                : null;
        }
        catch (Exception)
        {
            // A YAML syntax error: the library's own message names its line.
            return null;
        }
    }

    private static string? FindValueTypeError<T>(Dictionary<object, object?> map, string prefix)
    {
        if (GeneratedResolver.Instance.GetFormatter<T>() is not IYamlSchemaInfoProvider provider)
            return null;

        foreach (var member in provider.GetSchemaInfo().Members)
        {
            if (!map.TryGetValue(member.KeyName, out var value) || value is null)
                continue;

            var key = prefix + member.KeyName;
            var type = Nullable.GetUnderlyingType(member.Type) ?? member.Type;
            string? error;
            if (type == typeof(double))
                error = IsNumber(value, wholeOnly: false) ? null : $"'{key}' must be a number{(prefix == "settings." ? " of seconds, such as 0.5" : "")}, not {DescribeValue(value)}.";
            else if (type == typeof(int))
                error = IsNumber(value, wholeOnly: true) ? null : $"'{key}' must be a whole number, not {DescribeValue(value)}.";
            else if (type == typeof(bool))
                error = value is bool || (value is string text && bool.TryParse(text, out _)) ? null : $"'{key}' must be true or false, not {DescribeValue(value)}.";
            else if (type == typeof(string))
                error = value is Dictionary<object, object?> or System.Collections.IList ? $"'{key}' must be text, not {DescribeValue(value)}." : null;
            else if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
                error = value is System.Collections.IList ? null : $"'{key}' must be a list, not {DescribeValue(value)}.";
            else if (value is not Dictionary<object, object?> section)
                error = $"'{key}' must be a mapping of keys, not {DescribeValue(value)}.";
            else if (type == typeof(ScenarioSettings))
                error = FindValueTypeError<ScenarioSettings>(section, key + ".");
            else if (type == typeof(ScenarioRender))
                error = FindValueTypeError<ScenarioRender>(section, key + ".");
            else if (type == typeof(ScenarioTheme))
                error = FindValueTypeError<ScenarioTheme>(section, key + ".");
            else
                error = null;

            if (error is not null)
                return error;
        }

        return null;

        static bool IsNumber(object value, bool wholeOnly) => value switch
        {
            int or long => true,
            double or float or decimal => !wholeOnly,
            string text => wholeOnly
                ? int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)
                : double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out _),
            _ => false,
        };
    }

    private static string DescribeValue(object? value) => value switch
    {
        Dictionary<object, object?> => "a mapping",
        System.Collections.IList => "a list",
        bool flag => flag ? "'true'" : "'false'",
        IFormattable formattable => $"'{formattable.ToString(null, CultureInfo.InvariantCulture)}'",
        _ => $"'{value}'",
    };

    /// <summary>Keys the scenario sets that scenetake does not read, one message each (see <see cref="ScenarioKeys"/>).</summary>
    internal static List<string> FindUnknownScenarioKeys(string yaml)
    {
        // Maps are read as object: Native AOT has VYaml's formatter for object, which makes each map a
        // Dictionary<object, object?>, but none for Dictionary<object, object?> itself (JIT builds that one
        // by reflection, so only the published binary showed it).
        ScenarioKeys.Known known;
        Dictionary<object, object?> root;
        try
        {
            if (YamlSerializer.Deserialize<object>(Encoding.UTF8.GetBytes(yaml)) is not Dictionary<object, object?> map)
                return [];
            root = map;

            // The section keys come from the classes that read them: VYaml's source generator lists each member's
            // key in the schema of the formatter it generates, so a key added to a class is known here without a
            // second list. Serializing an empty instance gives the same keys, but no other code serializes, and
            // that call alone kept VYaml's serializer and emitter in the Native AOT binary (about 330 KB of 5 MB).
            known = new(KeysOf<Scenario>(), KeysOf<ScenarioSettings>(), KeysOf<ScenarioRender>(), KeysOf<ScenarioTheme>());
        }
        catch (Exception)
        {
            // The typed parse already accepted the file. A failure here only loses the warnings; it must never
            // report every key as unknown.
            return [];
        }

        return ScenarioKeys.FindUnknown(root, known);

        static string[] KeysOf<T>() =>
            GeneratedResolver.Instance.GetFormatter<T>() is IYamlSchemaInfoProvider provider
                && provider.GetSchemaInfo().Members is { Length: > 0 } members
                ? [.. members.Select(member => member.KeyName)]
                : throw new InvalidOperationException($"no keys for {typeof(T).Name}");
    }
    /// <summary>
    /// The error for the first step that is a list, has no run, has a run that is a list or a mapping, or has
    /// keys that contradict each other, or for a scenario with no command to record; null when there is none.
    /// </summary>
    internal static string? FindStepKeyError(Scenario scenario)
    {
        var steps = scenario.Steps ?? [];
        var recorded = 0;
        for (var index = 0; index < steps.Count; index++)
        {
            var step = steps[index];
            if (step is Dictionary<object, object?> map)
            {
                // YAML reads an unquoted command that holds ": ", such as `- echo key: value`, as a mapping.
                // Without run it used to be skipped with no message, so the recording lost that command.
                if (!map.TryGetValue("run", out var run))
                    return $"Step {index + 1} has no 'run' key (keys: {string.Join(", ", map.Keys)}). Quote a command that contains \": \", or write it under 'run:'.";

                // A list or a mapping under run would otherwise be run as its type name.
                if (run is System.Collections.IEnumerable and not string)
                    return "Step key 'run' must be a command string, not a list or a mapping.";
            }
            else if (step is System.Collections.IEnumerable and not string)
            {
                // `- [echo, hi]` is a YAML list, which used to be skipped with no message.
                return $"Step {index + 1} must be a command string or a mapping with 'run', not a list.";
            }

            var command = ParseCommand(step);
            if (string.IsNullOrWhiteSpace(command.Cmd))
                continue;
            recorded++;

            // GetBool warns about a value it cannot read, and the recording reads these keys again.
            var usePty = IsStepFlagSet(command.Extra, "pty");
            var useInteractive = IsStepFlagSet(command.Extra, "interactive");
            if (useInteractive && !usePty)
                return "Step key 'interactive: true' requires 'pty: true'.";
            if (IsStepFlagSet(command.Extra, "normalize-typing") && !useInteractive)
                return "Step key 'normalize-typing: true' requires 'interactive: true'.";
        }

        // A missing or misspelled steps key (`step:`) used to write an empty cast and exit 0.
        if (recorded == 0)
            return "The scenario has no steps to record. Add commands under 'steps:'.";

        return null;
    }

    /// <summary>Whether a step that will be recorded sets <c>interactive: true</c>.</summary>
    internal static bool HasInteractiveStep(Scenario scenario) =>
        (scenario.Steps ?? []).Select(ParseCommand).Any(command => !string.IsNullOrWhiteSpace(command.Cmd) && IsStepFlagSet(command.Extra, "interactive"));

    internal static bool IsStepFlagSet(Dictionary<string, object?> extra, string key) =>
        extra.TryGetValue(key, out var value) && (value is true || (bool.TryParse(value?.ToString(), out var parsed) && parsed));

    internal static CommandEntry ParseCommand(object? item)
    {
        if (item is string s) return new CommandEntry { Cmd = s };
        if (item is Dictionary<object, object?> map)
        {
            var extra = new Dictionary<string, object?>(map.Count);
            var cmd = "";
            string? name = null;
            foreach (var (rawKey, value) in map)
            {
                var key = rawKey?.ToString() ?? "";
                switch (key)
                {
                    case "run":
                        cmd = value?.ToString() ?? "";
                        break;
                    case "name":
                        name = value?.ToString();
                        break;
                    default:
                        extra[key] = value;
                        break;
                }
            }

            return new CommandEntry { Cmd = cmd, Name = name, Extra = extra };
        }
        return new CommandEntry();
    }

    internal static double GetDouble(Dictionary<string, object?> d, double def, params string[] keys) =>
        GetOptionalDouble(d, def, keys) ?? def;

    internal static double? GetOptionalDouble(Dictionary<string, object?> d, double? def, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!d.TryGetValue(key, out var value) || value is null)
                continue;

            if (double.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
                return parsed;

            // A step value such as `typing-speed: 50ms` used to fall back to the default with no message. The same
            // value under settings stops the run (ParseScenario), but a step key is read here, so it warns, as
            // GetBool does.
            var fallback = def is { } number ? $"using {number.ToString(CultureInfo.InvariantCulture)}" : "ignoring it";
            Warn(key, null, $"{DescribeValue(value)} is not a number of seconds, such as 0.5; {fallback}");
            return def;
        }

        return def;
    }

    internal static bool GetBool(Dictionary<string, object?> d, bool def, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!d.TryGetValue(key, out var value))
                continue;

            if (value is bool b)
                return b;

            if (bool.TryParse(value?.ToString(), out var parsed))
                return parsed;

            Warn(key, null, $"invalid boolean value '{value}'; using {def.ToString().ToLowerInvariant()}");
            return def;
        }

        return def;
    }

    internal static int ComputeDeterministicSeed(string yaml)
    {
        var normalized = yaml.Replace("\r\n", "\n").Replace("\r", "\n");
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return BitConverter.ToInt32(bytes, 0);
    }

    internal static long ComputeDeterministicTimestamp(int seed)
    {
        const long baseUnixTime = 1700000000;
        const long spanSeconds = 365L * 24 * 60 * 60;
        return baseUnixTime + (uint)seed % spanSeconds;
    }
}

record HighlightSpec(string ColorOpen, List<string> At);

[YamlObject(NamingConvention.KebabCase)]
public partial class Scenario
{
    public string? Title { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public string? Cwd { get; set; }
    public string? Shell { get; set; }
    public ScenarioSettings? Settings { get; set; }
    public ScenarioRender? Render { get; set; }
    public List<string>? Pre { get; set; }
    public List<object?>? Steps { get; set; }
    public List<string>? Post { get; set; }
}

[YamlObject(NamingConvention.KebabCase)]
public partial class ScenarioSettings
{
    public string? Prompt { get; set; }
    public double? TypingSpeed { get; set; }
    public double? TypingJitter { get; set; }
    public double? PreDelay { get; set; }
    public double? PostDelay { get; set; }
    public double? ExecutionDuration { get; set; }
    public double? SilenceThreshold { get; set; }
    public double? MaxIdle { get; set; }
    public double? MaxDuration { get; set; }
    public bool? StreamOutput { get; set; }
    public string? StderrColor { get; set; }
}

[YamlObject(NamingConvention.KebabCase)]
public partial class ScenarioRender
{
    public int? FontSize { get; set; }
    public string? FontFamily { get; set; }
    public string? Window { get; set; }
    public ScenarioTheme? Theme { get; set; }
}

[YamlObject(NamingConvention.KebabCase)]
public partial class ScenarioTheme
{
    public string? Preset { get; set; }
    public string? Fg { get; set; }
    public string? Bg { get; set; }
    public string? Palette { get; set; }
}

public class CommandEntry
{
    public string Cmd { get; set; } = "";
    public string? Name { get; set; }
    public Dictionary<string, object?> Extra { get; set; } = new();
}
