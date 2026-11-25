using System.Globalization;
using System.Text;
using VYaml.Annotations;
using VYaml.Serialization;

namespace Scenetake.Core;

/// <summary>
/// Scenario parsing, step keys, and the cast's fixed timestamp, shared by the CLI and the playground. The CLI reaches
/// these members through <c>using static</c>.
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
            // A key that takes more than one form, such as settings.prompt, checks its value itself (FindStepKeyError).
            else if (type == typeof(object))
                error = null;
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

    /// <summary>
    /// Keys the scenario sets that scenetake does not read, and colors that YAML read as comments, one message each
    /// (see <see cref="ScenarioKeys"/>). Both are warnings: the scenario still runs.
    /// </summary>
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
            known = KnownSectionKeys();
        }
        catch (Exception)
        {
            // The typed parse already accepted the file. A failure here only loses the warnings; it must never
            // report every key as unknown.
            return [];
        }

        return [.. ScenarioKeys.FindUnknown(root, known), .. ScenarioKeys.FindUnquotedColors(yaml, root)];
    }

    /// <summary>
    /// The keys of the top level, <c>settings</c>, <c>render</c>, and <c>render.theme</c>, from the classes that read
    /// them. The editor schema (schema/scenario.schema.json) is checked against them.
    /// </summary>
    /// <remarks>
    /// VYaml's source generator lists each member's key in the schema of the formatter it generates, so a key added to
    /// a class is known here without a second list. Serializing an empty instance gives the same keys, but no other code
    /// serializes, and that call alone kept VYaml's serializer and emitter in the Native AOT binary (about 330 KB of 5 MB).
    /// </remarks>
    internal static ScenarioKeys.Known KnownSectionKeys()
    {
        return new(KeysOf<Scenario>(), KeysOf<ScenarioSettings>(), KeysOf<ScenarioRender>(), KeysOf<ScenarioTheme>());

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
        if (FindPromptError(scenario.Settings?.Prompt) is { } promptError)
            return promptError;

        if (FindCwdAsError(scenario.CwdAs) is { } cwdAsError)
            return cwdAsError;

        var steps = scenario.Steps ?? [];
        var recorded = 0;
        for (var index = 0; index < steps.Count; index++)
        {
            var step = steps[index];
            if (step is Dictionary<object, object?> map)
            {
                if (map.ContainsKey("card"))
                {
                    var width = scenario.Width ?? RenderSettingsResolver.DefaultTerminalCols;
                    var height = scenario.Height ?? RenderSettingsResolver.DefaultTerminalRows;
                    if (FindCardStepError(map, index + 1, width, height) is { } cardError)
                        return cardError;
                    // A card is shown, so a scenario of cards alone records them.
                    recorded++;
                    continue;
                }

                if (map.ContainsKey("clear"))
                {
                    if (FindClearStepError(map, index + 1) is { } clearError)
                        return clearError;
                    continue;
                }

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

            if (FindBackgroundError(command, index + 1, out var background) is { } backgroundError)
                return backgroundError;
            // A background step runs without being recorded, and takes none of the keys checked below.
            if (background)
                continue;

            if (FindShowError(command, index + 1, out var show) is { } showError)
                return showError;
            // A step that shows nothing is a step to run, not to record.
            if (show != StepShow.None)
                recorded++;

            // GetBool warns about a value it cannot read, and the recording reads these keys again.
            var usePty = IsStepFlagSet(command.Extra, "pty");
            var useInteractive = IsStepFlagSet(command.Extra, "interactive");
            if (useInteractive && !usePty)
                return "Step key 'interactive: true' requires 'pty: true'.";
            if (IsStepFlagSet(command.Extra, "normalize-typing") && !useInteractive)
                return "Step key 'normalize-typing: true' requires 'interactive: true'.";
            if (FindStepInputError(command.Extra, index + 1, usePty, useInteractive) is { } inputError)
                return inputError;
        }

        // A missing or misspelled steps key (`step:`) used to write an empty cast and exit 0.
        if (recorded == 0)
            return "The scenario has no steps to record. Add commands under 'steps:'.";

        return FindStepIdError(steps);
    }

    /// <summary>
    /// Whether <paramref name="text"/> can be a step's <c>id</c>: a letter or <c>_</c>, then letters, digits, <c>-</c>,
    /// and <c>_</c>, as GitHub Actions takes a step's id. <c>--at</c> then reads it neither as seconds nor as
    /// <c>end</c>, and a shell needs no quotes around it.
    /// </summary>
    internal static bool IsStepId(string text)
    {
        if (text.Length == 0 || !(char.IsAsciiLetter(text[0]) || text[0] == '_') || text.Equals("end", StringComparison.OrdinalIgnoreCase))
            return false;
        foreach (var c in text)
        {
            if (!(char.IsAsciiLetterOrDigit(c) || c is '-' or '_'))
                return false;
        }

        return true;
    }

    /// <summary>The error for a step <c>id</c> that <c>--at</c> could not name, or that two steps share. Null when the ids are fine.</summary>
    /// <summary>The error for a <c>cwd-as</c> that is blank or holds a control character, such as a line break, or null.</summary>
    internal static string? FindCwdAsError(string? value)
    {
        if (value is null)
            return null;
        if (string.IsNullOrWhiteSpace(value))
            return "cwd-as cannot be empty: give the path the recording shows for cwd, such as ~/my-app.";
        foreach (var c in value)
        {
            if (char.IsControl(c))
                return "cwd-as cannot hold a line break or another control character: give one path, such as ~/my-app.";
        }

        return null;
    }

    private static string? FindStepIdError(List<object?> steps)
    {
        var seen = new List<string>();
        var numbers = new List<int>();
        for (var index = 0; index < steps.Count; index++)
        {
            if (steps[index] is not Dictionary<object, object?> map || !map.TryGetValue("id", out var value))
                continue;
            if (value is not string id || !IsStepId(id))
            {
                return value is string text && text.Equals("end", StringComparison.OrdinalIgnoreCase)
                    ? $"Step {index + 1} key 'id' cannot be '{text}', which --at reads as the final screen."
                    : $"Step {index + 1} key 'id' must start with a letter or '_' and hold only letters, digits, '-', and '_', such as compare or build-1, not {DescribeValue(value)}.";
            }

            var earlier = seen.IndexOf(id);
            if (earlier >= 0)
                return $"Steps {numbers[earlier]} and {index + 1} have the same id '{id}': each id names one step for --at.";
            seen.Add(id);
            numbers.Add(index + 1);
        }

        return null;
    }

    // Keys about what a step shows in the recording, which a step that shows nothing does not have. rerun: false shows
    // the earlier cast's output, and a hidden step has none there, so it always runs.
    internal static readonly string[] ShownOnlyKeys =
    [
        "name", "id", "run-highlight", "highlight", "stderr-color", "pre-delay", "post-delay", "execution-duration",
        "stream-output", "max-duration", "silence-threshold", "max-idle", "keep-output", "rerun",
    ];

    // The keys a clear step reads besides clear: it shows a comment line and takes time, but runs nothing.
    internal static readonly string[] ClearStepKeys = ["clear", "name", "id", "pre-delay", "post-delay"];

    // The keys a card step reads besides card: it shows its lines and takes time, but runs nothing.
    internal static readonly string[] CardStepKeys = ["card", "id", "pre-delay", "post-delay"];

    /// <summary>
    /// Seconds a card stays without <c>post-delay</c>: long enough to read a title and an install line, which a
    /// command's post-delay (half a second) is not.
    /// </summary>
    internal const double CardPostDelay = 2.0;

    /// <summary>
    /// What a step shows: <c>show: all</c> (the default) types the command and shows its output; <c>show: output</c>
    /// shows the output without the prompt and typing; <c>show: none</c> runs the command without recording it.
    /// The error for a value that is not one of those, or for a key that the value leaves without effect; null when
    /// the step is fine. <paramref name="number"/> is the step's 1-based position.
    /// </summary>
    internal static string? FindShowError(CommandEntry command, int number, out StepShow show)
    {
        show = StepShow.All;
        var extra = command.Extra;
        if (!extra.TryGetValue("show", out var value) || value is null)
            return null;

        if (value is not string text || !TryParseShow(text, out show))
            return $"Step {number} key 'show' must be all, output, or none, not {DescribeValue(value)}.";

        if (show == StepShow.None)
        {
            foreach (var key in ShownOnlyKeys)
            {
                if (key == "name" ? command.Name is not null : extra.ContainsKey(key))
                    return $"Step {number} key '{key}' has no effect with 'show: none': the step runs without appearing in the recording, and takes no time in it.";
            }
        }
        else if (show == StepShow.Output && extra.ContainsKey("run-highlight"))
        {
            return $"Step {number} key 'run-highlight' styles the typed command, which 'show: output' leaves out.";
        }

        return null;
    }

    /// <summary>
    /// Seconds a background step waits for its <c>wait</c> text without <c>wait-timeout</c>. A server or a build
    /// watcher takes longer to start than a prompt takes to appear, so this is longer than <see cref="KeyWaitLimit"/>.
    /// </summary>
    internal const double BackgroundWaitLimit = 30.0;

    // The keys a background step reads besides run.
    internal static readonly string[] BackgroundStepKeys = ["background", "wait", "wait-timeout", "continue-on-error"];

    /// <summary>
    /// Whether a step sets <c>background: true</c>, and the error for its keys: <c>wait</c> and <c>wait-timeout</c>
    /// apply to a background step only, and a background step, which is never recorded, takes no other key. Null when
    /// the step is fine. <paramref name="number"/> is the step's 1-based position.
    /// </summary>
    internal static string? FindBackgroundError(CommandEntry command, int number, out bool background)
    {
        background = false;
        var extra = command.Extra;
        if (extra.TryGetValue("background", out var value) && value is not null)
        {
            if (value is bool flag)
                background = flag;
            else if (!(value is string text && bool.TryParse(text, out background)))
                return $"Step {number} key 'background' must be true or false, not {DescribeValue(value)}.";
        }

        var hasWait = extra.TryGetValue("wait", out var wait) && wait is not null;
        var hasWaitTimeout = extra.TryGetValue("wait-timeout", out var waitTimeout) && waitTimeout is not null;
        if (!background)
        {
            if (hasWait)
                return $"Step {number} key 'wait' applies to a 'background: true' step. To wait for text in a PTY step, use a keys entry: keys: [{{wait: \"...\"}}].";
            if (hasWaitTimeout)
                return $"Step {number} key 'wait-timeout' applies to a 'background: true' step with 'wait'.";
            return null;
        }

        if (command.Name is not null)
            return $"Step {number} key 'name' does not apply to a 'background: true' step, which runs without appearing in the recording.";
        foreach (var key in extra.Keys)
        {
            if (Array.IndexOf(BackgroundStepKeys, key) < 0 && Array.IndexOf(ScenarioKeys.Step, key) >= 0)
                return $"Step {number} key '{key}' does not apply to a 'background: true' step, which runs without appearing in the recording and takes only 'wait', 'wait-timeout', and 'continue-on-error'.";
        }

        if (hasWait && (TextToType(wait) is not { } waitText || !HasPrintable(waitText)))
            return $"Step {number} key 'wait' must be text the command prints when it is ready, such as \"Listening on\", not {DescribeValue(wait)}.";
        if (hasWaitTimeout)
        {
            if (!hasWait)
                return $"Step {number} key 'wait-timeout' needs 'wait': without a text to wait for, the step goes on as soon as the command starts.";
            if (!TryReadSeconds(waitTimeout, out var seconds) || seconds <= 0)
                return $"Step {number} key 'wait-timeout' must be a number of seconds above 0, such as 60, not {DescribeValue(waitTimeout)}.";
        }

        return null;
    }

    /// <summary>The text a background step waits for, or null, and how long it waits. <see cref="FindBackgroundError"/> checks both.</summary>
    internal static (string? Text, double Limit) GetBackgroundWait(Dictionary<string, object?> extra)
    {
        var text = extra.TryGetValue("wait", out var wait) && TextToType(wait) is { Length: > 0 } waitText ? waitText : null;
        var limit = extra.TryGetValue("wait-timeout", out var waitTimeout) && TryReadSeconds(waitTimeout, out var seconds) && seconds > 0
            ? seconds
            : BackgroundWaitLimit;
        return (text, limit);
    }

    /// <summary>What a step shows (<see cref="FindShowError"/> checks the value first): <see cref="StepShow.All"/> when it sets nothing.</summary>
    internal static StepShow GetStepShow(Dictionary<string, object?> extra) =>
        extra.TryGetValue("show", out var value) && value is string text && TryParseShow(text, out var show) ? show : StepShow.All;

    private static bool TryParseShow(string text, out StepShow show)
    {
        show = text.Trim().ToLowerInvariant() switch
        {
            "all" => StepShow.All,
            "output" => StepShow.Output,
            "none" => StepShow.None,
            _ => (StepShow)(-1),
        };
        return show >= StepShow.All;
    }

    /// <summary>The error for a <c>clear</c> step: one that sets <c>clear: true</c>, no <c>run</c>, and only the keys it reads.</summary>
    private static string? FindClearStepError(Dictionary<object, object?> map, int number)
    {
        if (map.ContainsKey("run"))
            return $"Step {number} has both 'run' and 'clear'. A 'clear: true' step clears the screen and runs nothing: put the command in a step of its own.";

        var value = map["clear"];
        if (value is not true && !(value is string text && bool.TryParse(text, out var parsed) && parsed))
            return $"Step {number} key 'clear' must be true, not {DescribeValue(value)}. Leave the step out to keep the screen.";

        foreach (var key in map.Keys)
        {
            var name = key?.ToString() ?? "";
            if (Array.IndexOf(ClearStepKeys, name) < 0 && Array.IndexOf(ScenarioKeys.Step, name) >= 0)
                return $"Step {number} key '{name}' does not apply to a 'clear: true' step, which takes only 'name', 'id', 'pre-delay', and 'post-delay'.";
        }

        return null;
    }

    /// <summary>
    /// The error for a <c>card</c> step: one that has no <c>run</c> or <c>clear</c>, only the keys it reads, and at
    /// least one line of text that fits the terminal, each line and all of them. Null when the step is fine.
    /// </summary>
    private static string? FindCardStepError(Dictionary<object, object?> map, int number, int width, int height)
    {
        if (map.ContainsKey("run"))
            return $"Step {number} has both 'run' and 'card'. A card shows its lines and runs nothing: put the command in a step of its own.";
        if (map.ContainsKey("clear"))
            return $"Step {number} has both 'card' and 'clear'. A card clears the screen by itself: leave 'clear' out.";

        foreach (var key in map.Keys)
        {
            var name = key?.ToString() ?? "";
            if (Array.IndexOf(CardStepKeys, name) < 0 && Array.IndexOf(ScenarioKeys.Step, name) >= 0)
                return $"Step {number} key '{name}' does not apply to a 'card' step, which takes only 'id', 'pre-delay', and 'post-delay'.";
        }

        if (CardLines(map["card"]) is not { } lines)
        {
            // An unquoted line with ": " in it, such as an install line, is a mapping to YAML.
            if (map["card"] is List<object?> entries && entries.FindIndex(static entry => entry is Dictionary<object, object?>) is var mapping and >= 0)
                return $"Step {number} card line {mapping + 1} is read as a mapping, because YAML takes \": \" in it as a key. Quote the line, such as \"Install: npm i -g my-app\".";
            return $"Step {number} key 'card' must be a line of text or a list of lines, not {DescribeValue(map["card"])}.";
        }
        if (!lines.Any(static line => HasPrintable(SplitCardStyle(line).Text)))
            return $"Step {number} key 'card' has no text to show. Give it a line, such as card: [\"my-app\", \"npm i -g my-app\"].";
        if (lines.Count > height)
            return $"Step {number} card has {lines.Count} lines, more than the terminal's {height} rows. Leave lines out, or raise 'height'.";

        for (var i = 0; i < lines.Count; i++)
        {
            var text = SplitCardStyle(lines[i]).Text;
            if (text.AsSpan().IndexOfAnyInRange('\0', '\u001f') >= 0 || text.AsSpan().IndexOfAnyInRange('\u007f', '\u009f') >= 0)
                return $"Step {number} card line {i + 1} holds a control character. Color a line with a [style] prefix, such as \"[bold fg:cyan]my-app\".";
            if (DisplayWidth(text) is var columns && columns > width)
                return $"Step {number} card line {i + 1} is {columns} columns wide, wider than the terminal's {width}. Shorten it, or raise 'width'.";
        }

        return null;
    }

    /// <summary>
    /// The error for a <c>settings.prompt</c> that is neither text nor a list of parts, each part text or a mapping with
    /// <c>text</c> and an optional <c>color</c>. Null when the prompt is fine or not set.
    /// </summary>
    internal static string? FindPromptError(object? value)
    {
        const string Example = "such as [{text: \"demo\", color: green}, \" {dir} $ \"]";
        if (value is null || TextToType(value) is not null)
            return null;
        if (value is not List<object?> parts)
            return $"'settings.prompt' must be text or a list of parts, {Example}, not {DescribeValue(value)}.";

        for (var i = 0; i < parts.Count; i++)
        {
            if (parts[i] is null || TextToType(parts[i]) is not null)
                continue;
            if (parts[i] is not Dictionary<object, object?> part)
                return $"settings.prompt part {i + 1} must be text or a mapping with 'text' and 'color', not {DescribeValue(parts[i])}.";
            if (!part.TryGetValue("text", out var text) || TextToType(text) is null)
                return $"settings.prompt part {i + 1} needs 'text', the text it shows, {Example}.";
            if (part.TryGetValue("color", out var color) && color is not null and not string)
                return $"settings.prompt part {i + 1} key 'color' must be a style, such as \"bold fg:green\", not {DescribeValue(color)}.";
        }

        return null;
    }

    /// <summary>
    /// The lines of a <c>card</c> value: a text, split at its line breaks, as a YAML block (<c>card: |</c>) writes
    /// one, or a list of texts, each split the same way. A number is text as YAML read it, and an empty entry is an
    /// empty line.
    /// Null for a value that is neither.
    /// </summary>
    internal static List<string>? CardLines(object? value)
    {
        var lines = new List<string>();
        if (TextToType(value) is { } text)
        {
            AddLines(text);
            return lines;
        }

        if (value is not List<object?> list)
            return null;
        foreach (var item in list)
        {
            if (item is null)
                lines.Add("");
            else if (TextToType(item) is { } itemText)
                AddLines(itemText);
            else
                return null;
        }

        return lines;

        // A block ends with a line break, which is not a line of its own.
        void AddLines(string part) =>
            lines.AddRange(part.Replace("\r\n", "\n").TrimEnd('\n').Split('\n'));
    }

    /// <summary>
    /// A card line's optional <c>[style]</c> prefix, as a <c>name</c> comment reads one, and its text without the
    /// spaces around it: a card centers each line, so they would only move it. <c>"[bold]my-app"</c> gives
    /// <c>("bold", "my-app")</c>, and a line without a prefix gives a null style.
    /// </summary>
    internal static (string? Style, string Text) SplitCardStyle(string line)
    {
        var value = line.Trim();
        if (value.StartsWith('[') && value.IndexOf(']') is var close and > 1)
            return (value[1..close], value[(close + 1)..].Trim());
        return (null, value);
    }

    /// <summary>
    /// The columns a text takes in the terminal: two for a wide character, none for a combining mark or a character
    /// the terminal draws nothing for, such as a zero-width joiner.
    /// </summary>
    internal static int DisplayWidth(string text)
    {
        var width = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            width += Rune.GetUnicodeCategory(rune) is UnicodeCategory.NonSpacingMark or UnicodeCategory.EnclosingMark or UnicodeCategory.SpacingCombiningMark
                || (rune.IsBmp && AnsiParser.IsZeroWidthChar((char)rune.Value))
                ? 0
                : ScreenBuffer.IsWideCodePoint(rune.Value) ? 2 : 1;
        }

        return width;
    }

    /// <summary>Whether a step that will be recorded sets <c>interactive: true</c>.</summary>
    internal static bool HasInteractiveStep(Scenario scenario) =>
        (scenario.Steps ?? []).Select(ParseCommand).Any(command => !string.IsNullOrWhiteSpace(command.Cmd) && IsStepFlagSet(command.Extra, "interactive"));

    /// <summary>
    /// Whether an interactive step will run, so that the recording needs a terminal. A step with <c>rerun: false</c> does
    /// not run when <paramref name="previous"/> has its output, unless <paramref name="rerunAll"/>: so an interactive step
    /// recorded once by hand records again in CI. Run text is masked by <paramref name="masker"/>, which knows no secret
    /// that pre or a step exports yet, so a step whose run text holds one counts as one that runs.
    /// </summary>
    internal static bool RunsInteractiveStep(Scenario scenario, PreviousCast? previous, SecretMasker? masker, bool rerunAll)
    {
        // The n-th step with an id finds the n-th earlier step with it, as the recording takes them (Rerecorder).
        var seen = new Dictionary<ulong, int>();
        foreach (var item in scenario.Steps ?? [])
        {
            var command = ParseCommand(item);
            if (command.Card is not null || command.Clear || string.IsNullOrWhiteSpace(command.Cmd) || IsStepFlagSet(command.Extra, "background"))
                continue;

            var interactive = IsStepFlagSet(command.Extra, "interactive");
            var show = GetStepShow(command.Extra);
            if (show == StepShow.None)
            {
                if (interactive)
                    return true;
                continue;
            }

            var maskedRun = masker?.MaskUncounted(command.Cmd) ?? command.Cmd;
            var id = Rerecording.StepId(IsStepFlagSet(command.Extra, "pty"), interactive, show, maskedRun);
            var occurrence = seen.GetValueOrDefault(id);
            seen[id] = occurrence + 1;
            if (!interactive)
                continue;

            var shown = !rerunAll && previous is not null && IsStepFlagCleared(command.Extra, "rerun") && occurrence < previous.CountOf(id);
            if (!shown)
                return true;
        }

        return false;
    }

    /// <summary>Whether a step sets a flag to <c>false</c>, without a warning for another value, which the recording gives.</summary>
    internal static bool IsStepFlagCleared(Dictionary<string, object?> extra, string key) =>
        extra.TryGetValue(key, out var value) && (value is false || (value is string text && bool.TryParse(text, out var parsed) && !parsed));

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
            var clear = false;
            List<string>? card = null;
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
                    case "clear" when !map.ContainsKey("run") && !map.ContainsKey("card"):
                        clear = value is true || (value is string text && bool.TryParse(text, out var parsed) && parsed);
                        break;
                    case "card" when !map.ContainsKey("run"):
                        card = CardLines(value);
                        break;
                    default:
                        extra[key] = value;
                        break;
                }
            }

            return new CommandEntry { Cmd = cmd, Name = name, Extra = extra, Clear = clear, Card = card };
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

            // As settings read their numbers (FindValueTypeError) and sleep and timeout read theirs: "0,05" is not 5,
            // "(1)" is not -1, and infinity is not a time.
            if (TryReadSeconds(value, out var parsed))
                return parsed;

            // A step value such as `typing-speed: 50ms` used to fall back to the default with no message. The same
            // value under settings stops the run (ParseScenario), but a step key is read here, so it warns, as
            // GetBool does.
            var fallback = def is { } number ? $"using {number.ToString(CultureInfo.InvariantCulture)}" : "ignoring it";
            Warn(key, null, $"{DescribeValue(value)} is not a number of seconds (such as 0.5), {fallback}");
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

            Warn(key, null, $"invalid boolean value '{value}', using {def.ToString().ToLowerInvariant()}");
            return def;
        }

        return def;
    }

    // ---------------------------------------------------------------- step input: keys and timeout

    /// <summary>Seconds a step stopped by its timeout gets to exit after Ctrl+C, before scenetake ends it.</summary>
    internal const double TimeoutInterruptGrace = 2.0;

    /// <summary>
    /// What a terminal sends for each named key, as xterm sends it in normal cursor mode. Programs in application
    /// cursor mode, such as vim, also read the normal-mode arrows.
    /// </summary>
    private static readonly Dictionary<string, string> NamedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        ["enter"] = "\r", ["return"] = "\r", ["tab"] = "\t", ["space"] = " ", ["backspace"] = "\u007f",
        ["escape"] = "\u001b", ["esc"] = "\u001b",
        ["up"] = "\u001b[A", ["down"] = "\u001b[B", ["right"] = "\u001b[C", ["left"] = "\u001b[D",
        ["home"] = "\u001b[H", ["end"] = "\u001b[F",
        ["page-up"] = "\u001b[5~", ["pageup"] = "\u001b[5~", ["page-down"] = "\u001b[6~", ["pagedown"] = "\u001b[6~",
        ["insert"] = "\u001b[2~", ["delete"] = "\u001b[3~",
        ["f1"] = "\u001bOP", ["f2"] = "\u001bOQ", ["f3"] = "\u001bOR", ["f4"] = "\u001bOS",
        ["f5"] = "\u001b[15~", ["f6"] = "\u001b[17~", ["f7"] = "\u001b[18~", ["f8"] = "\u001b[19~",
        ["f9"] = "\u001b[20~", ["f10"] = "\u001b[21~", ["f11"] = "\u001b[23~", ["f12"] = "\u001b[24~",
    };

    /// <summary>The names in the table above, which the editor schema (schema/scenario.schema.json) is checked against.</summary>
    internal static IEnumerable<string> KeyNames => NamedKeys.Keys;

    /// <summary>
    /// Seconds a <c>wait</c> entry waits for its text in a step without <c>timeout</c>. Then it gives up with a
    /// warning and goes on, so a text the program never prints does not stop the recording.
    /// </summary>
    internal const double KeyWaitLimit = 10.0;

    /// <summary>
    /// A step's input for its PTY: wait <paramref name="Delay"/> seconds after the previous action (or the
    /// command's start), then write <paramref name="Text"/>. With <paramref name="Wait"/> set it writes nothing:
    /// it waits until the output shows that text. <paramref name="Entry"/> is the 1-based <c>keys</c> entry, for messages.
    /// </summary>
    internal readonly record struct KeyAction(double Delay, string Text, string? Wait = null, int Entry = 0);

    /// <summary>
    /// The bytes a key name stands for: a name from the table above, <c>ctrl+</c> and a letter, or <c>alt+</c>
    /// and one printable ASCII character. Null for any other name. Names ignore case.
    /// </summary>
    internal static string? KeyText(string name)
    {
        var key = name.Trim();
        if (NamedKeys.TryGetValue(key, out var text))
            return text;

        if (key.Length == 6 && key.StartsWith("ctrl+", StringComparison.OrdinalIgnoreCase) && char.IsAsciiLetter(key[5]))
            return ((char)(char.ToLowerInvariant(key[5]) - 'a' + 1)).ToString();

        if (key.Length == 5 && key.StartsWith("alt+", StringComparison.OrdinalIgnoreCase) && key[4] is > ' ' and < '\u007f')
            return "\u001b" + key[4];

        return null;
    }

    /// <summary>The step's <c>timeout</c> in seconds, or null when it sets none. <see cref="FindStepInputError"/> checks the value.</summary>
    internal static double? GetStepTimeout(Dictionary<string, object?> extra) =>
        extra.TryGetValue("timeout", out var value) && TryReadSeconds(value, out var seconds) && seconds > 0 ? seconds : null;

    /// <summary>
    /// The error for a step's <c>keys</c> or <c>timeout</c>, or null. <paramref name="number"/> is the step's
    /// 1-based position. Both are found before <c>pre</c> runs, so a typo does not cost a recording.
    /// </summary>
    internal static string? FindStepInputError(Dictionary<string, object?> extra, int number, bool usePty, bool useInteractive)
    {
        if (extra.TryGetValue("timeout", out var timeout) && timeout is not null)
        {
            if (!TryReadSeconds(timeout, out var seconds) || seconds <= 0)
                return $"Step {number} key 'timeout' must be a number of seconds above 0, such as 5, not {DescribeValue(timeout)}.";
            if (useInteractive)
                return $"Step {number} key 'timeout' does not apply to 'interactive: true': the person at the keyboard ends that step.";
        }

        if (!extra.TryGetValue("keys", out var keys) || keys is null)
            return null;
        if (useInteractive)
            return $"Step {number} key 'keys' does not apply to 'interactive: true': the person at the keyboard types in that step.";
        if (!usePty)
            return $"Step {number} key 'keys' requires 'pty: true': a step without a terminal has nothing to type into.";
        if (keys is not System.Collections.IList list)
            return $"Step {number} key 'keys' must be a list, such as [{{sleep: 2, key: q}}], not {DescribeValue(keys)}.";

        for (var i = 0; i < list.Count; i++)
        {
            var where = $"Step {number} keys entry {i + 1}";
            if (list[i] is not Dictionary<object, object?> entry)
                return $"{where} must be a mapping with 'sleep', 'wait', 'type', or 'key', such as {{key: enter}}, not {DescribeValue(list[i])}.";

            var hasSleep = entry.TryGetValue("sleep", out var sleep);
            var hasWait = entry.TryGetValue("wait", out var waitFor);
            var hasType = entry.TryGetValue("type", out var type);
            var hasKey = entry.TryGetValue("key", out var key);
            if (!hasSleep && !hasWait && !hasType && !hasKey)
                return $"{where} needs 'sleep', 'wait', 'type', or 'key'.";
            if (hasType && hasKey)
                return $"{where} has both 'type' and 'key'. Put each in an entry of its own.";
            if (hasSleep && (!TryReadSeconds(sleep, out var wait) || wait < 0))
                return $"{where} 'sleep' must be a number of seconds, such as 0.5, not {DescribeValue(sleep)}.";
            // The output is matched without control characters and spaces at the ends, so text made only of them matches nothing.
            if (hasWait && (TextToType(waitFor) is not { } waitText || !HasPrintable(waitText)))
                return $"{where} 'wait' must be text the program prints, such as \"[y/N]\", not {DescribeValue(waitFor)}.";
            if (hasType && string.IsNullOrEmpty(TextToType(type)))
                return $"{where} 'type' must be text to type, not {DescribeValue(type)}.";
            if (hasKey && (key is not string name || KeyText(name) is null))
                return $"{where} has an unknown key {DescribeValue(key)}. Use a name such as enter, escape, up, page-down, f1, ctrl+c, or alt+x.";
        }

        return null;
    }

    /// <summary>
    /// A step's <c>keys</c> as timed writes. <c>sleep</c> adds to the wait before the next write. <c>wait</c> comes
    /// after it and is an action of its own, which writes nothing. <c>type</c> writes one character at a time, each
    /// after a typing delay drawn as for a typed command, and a line break in it is Enter. Assumes
    /// <see cref="FindStepInputError"/> accepted the step. The playground draws the same delays, so the typing of
    /// later steps stays the same as on the CLI. A wait draws none.
    /// </summary>
    internal static IReadOnlyList<KeyAction> BuildKeyActions(Dictionary<string, object?> extra, double speed, double jitter, Random rng)
    {
        if (!extra.TryGetValue("keys", out var keys) || keys is not System.Collections.IList list)
            return [];

        var actions = new List<KeyAction>();
        var wait = 0.0;
        for (var index = 0; index < list.Count; index++)
        {
            if (list[index] is not Dictionary<object, object?> entry)
                continue;

            var number = index + 1;
            if (entry.TryGetValue("sleep", out var sleep) && TryReadSeconds(sleep, out var seconds) && seconds > 0)
                wait += seconds;

            if (entry.TryGetValue("wait", out var waitFor) && TextToType(waitFor) is { Length: > 0 } waitText)
            {
                actions.Add(new KeyAction(wait, "", waitText, number));
                wait = 0;
            }

            if (entry.TryGetValue("key", out var key) && key is string name && KeyText(name) is { } keyText)
            {
                actions.Add(new KeyAction(wait, keyText, Entry: number));
                wait = 0;
            }
            else if (entry.TryGetValue("type", out var type) && TextToType(type) is { Length: > 0 } text)
            {
                var normalized = text.Replace("\r\n", "\n");
                for (var i = 0; i < normalized.Length;)
                {
                    var length = StringInfo.GetNextTextElementLength(normalized.AsSpan(i));
                    var element = normalized.Substring(i, length);
                    actions.Add(new KeyAction(wait, element == "\n" ? "\r" : element, Entry: number));
                    wait = Math.Max(speed + rng.NextDouble() * 2 * jitter - jitter, 0.005);
                    i += length;
                }
            }
        }

        return actions;
    }

    private static bool HasPrintable(string text)
    {
        foreach (var c in text)
        {
            if (!char.IsWhiteSpace(c) && !char.IsControl(c))
                return true;
        }

        return false;
    }

    /// <summary>The text of a <c>type</c> value: YAML reads <c>type: 42</c> as a number, which is still text to type.</summary>
    private static string? TextToType(object? value) => value switch
    {
        string text => text,
        int or long or double or float or decimal => Convert.ToString(value, CultureInfo.InvariantCulture),
        _ => null,
    };

    private static bool TryReadSeconds(object? value, out double seconds)
    {
        seconds = 0;
        return value is not (null or bool or System.Collections.IEnumerable and not string)
            && double.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out seconds)
            && double.IsFinite(seconds);
    }

    /// <summary>
    /// The cast header's <c>timestamp</c>, the same for every recording (2023-11-14 UTC). It came from a hash of the YAML
    /// text, so a new comment changed the header of a recording whose steps were the same.
    /// </summary>
    internal const long CastTimestamp = 1700000000;
}

record HighlightSpec(string ColorOpen, List<string> At);

[YamlObject(NamingConvention.KebabCase)]
public partial class Scenario
{
    public string? Title { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public string? Cwd { get; set; }
    public string? CwdAs { get; set; }
    public string? Shell { get; set; }
    public ScenarioSettings? Settings { get; set; }
    public ScenarioRender? Render { get; set; }
    public List<string>? Pre { get; set; }
    public List<object?>? Steps { get; set; }
    public List<string>? Post { get; set; }

    /// <summary>The files a recording writes besides the cast, each a mapping with <c>format</c> (the CLI reads them).</summary>
    public List<object?>? Outputs { get; set; }
}

[YamlObject(NamingConvention.KebabCase)]
public partial class ScenarioSettings
{
    /// <summary>Text, or a list of parts with a color each (<see cref="ScenarioCast.ParsePrompt"/>).</summary>
    public object? Prompt { get; set; }
    public double? TypingSpeed { get; set; }
    public double? TypingJitter { get; set; }
    public double? PreDelay { get; set; }
    public double? PostDelay { get; set; }

    /// <summary>Whether the recording ends with the closing prompt. Without it, it ends on the last output.</summary>
    public bool? EndPrompt { get; set; }

    public double? ExecutionDuration { get; set; }
    public double? SilenceThreshold { get; set; }
    public double? MaxIdle { get; set; }
    public double? MaxDuration { get; set; }
    public bool? StreamOutput { get; set; }
    public string? StderrColor { get; set; }
    public List<string>? Mask { get; set; }
    public bool? MaskAuto { get; set; }
    public bool? MaskPersonal { get; set; }
}

[YamlObject(NamingConvention.KebabCase)]
public partial class ScenarioRender
{
    public int? FontSize { get; set; }
    public string? FontFamily { get; set; }
    public string? Window { get; set; }
    public string? WindowTitle { get; set; }
    public string? Background { get; set; }
    public int? Margin { get; set; }
    public int? Padding { get; set; }
    public double? Opacity { get; set; }
    public int? Blur { get; set; }
    public bool? Loop { get; set; }
    public double? EndHold { get; set; }
    public double? FadeOut { get; set; }
    public double? Speed { get; set; }
    public double? Length { get; set; }
    public string? Fit { get; set; }
    public int? CropTop { get; set; }
    public int? CropBottom { get; set; }
    public int? CropLeft { get; set; }
    public int? CropRight { get; set; }
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

    /// <summary>A <c>clear: true</c> step: it clears the screen and runs nothing.</summary>
    public bool Clear { get; set; }

    /// <summary>A <c>card</c> step's lines: it shows them centered on a cleared screen and runs nothing.</summary>
    public List<string>? Card { get; set; }
}

/// <summary>What a step shows in the recording (<c>show</c>).</summary>
internal enum StepShow
{
    /// <summary>The prompt, the typed command, and its output: what a step always showed.</summary>
    All,

    /// <summary>The output only, without the prompt and typing: a banner, a title, or a screen that is already open.</summary>
    Output,

    /// <summary>Nothing: the command runs, as pre does, between the steps around it, and takes no time in the recording.</summary>
    None,
}
