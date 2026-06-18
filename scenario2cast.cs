#:sdk Microsoft.NET.Sdk
#:property TargetFramework=net10.0
#:property Version=0.3.0
#:property Nullable=enable
#:property ImplicitUsings=enable
#:package VYaml@1.3.0
#:include SvgRender.cs
#:include CastReader.cs

using System.Diagnostics;
using System.Globalization;
using System.Buffers;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using VYaml.Annotations;
using VYaml.Serialization;

const string DefaultPrompt    = "$ ";
const double DefaultSpeed     = 0.05;
const double DefaultJitter    = 0.015;
const double DefaultPreDelay  = 0.2;
const double DefaultPostDelay = 0.5;
const double DefaultExecutionDuration = 0.05;
const string DefaultStderrColorSpec = "red";
const string AppVersion = "0.3.0";
const string SgrReset = "\u001b[0m";

if (args.Length < 1)
{
    PrintUsage();
    return 1;
}

if (args[0] is "init")
{
    if (args.Length == 2 && args[1] is "-h" or "--help")
    {
        PrintInitUsage();
        return 0;
    }

    if (!TryParseInitArgs(args, out var initPathArg, out var initError))
    {
        Console.Error.WriteLine($"Error: {initError}");
        PrintInitUsage();
        return 1;
    }

    var initPath = initPathArg is not null
        ? Path.GetFullPath(initPathArg)
        : Path.GetFullPath("scenario.yaml");

    if (File.Exists(initPath))
    {
        Console.Error.WriteLine($"Error: {initPath} already exists");
        return 1;
    }

    Directory.CreateDirectory(Path.GetDirectoryName(initPath) ?? ".");
    File.WriteAllText(initPath, CreateInitialScenarioYaml(), Encoding.UTF8);
    Console.Error.WriteLine($"Created: {initPath}");
    return 0;
}

if (args[0] is "svg")
{
    if (args.Length == 2 && args[1] is "-h" or "--help")
    {
        PrintSvgUsage();
        return 0;
    }

    if (!TryParseSvgArgs(args, out var castArg, out var svgOutputArg, out var svgFontSize, out var svgThemePreset, out var svgError))
    {
        Console.Error.WriteLine($"Error: {svgError}");
        PrintSvgUsage();
        return 1;
    }

    var castPath = Path.GetFullPath(castArg);
    if (!File.Exists(castPath))
    {
        Console.Error.WriteLine($"Error: {castPath} not found");
        return 1;
    }

    var svgOutputPath = RenderSettingsResolver.ResolveCastSvgOutputPath(castPath, svgOutputArg);
    Console.Error.WriteLine($"Loading: {castPath}");

    try
    {
        var recording = CastReader.Read(castPath);
        var svgRenderSettings = RenderSettingsResolver.ApplySvgOverrides(
            recording.RenderSettings,
            svgFontSize,
            svgThemePreset,
            out var svgOverrideError);
        if (svgOverrideError.Length != 0)
        {
            Console.Error.WriteLine($"Error: {svgOverrideError}");
            return 1;
        }

        SvgRender.WriteSvg(recording.Events, recording.Width, recording.Height, svgRenderSettings, svgOutputPath);
        var svgDuration = recording.Events.Count > 0 ? recording.Events[^1].Time : 0.0;
        Console.Error.WriteLine($"Written: {svgOutputPath}  ({recording.Events.Count} events, {svgDuration:F1}s)");
        Console.Error.WriteLine($"Done: {svgOutputPath}");
        return 0;
    }
    catch (CastReadException ex)
    {
        if (File.Exists(svgOutputPath))
            File.Delete(svgOutputPath);
        Console.Error.WriteLine($"Error: {ex.Message}");
        return 1;
    }
    catch (Exception ex)
    {
        if (File.Exists(svgOutputPath))
            File.Delete(svgOutputPath);
        Console.Error.WriteLine($"Error: SVG render failed: {ex.Message}");
        return 1;
    }
}

if (args[0] is "--version")
{
    PrintVersion();
    return 0;
}

if (args[0] is "-h" or "--help")
{
    PrintUsage();
    return 0;
}

if (!TryParseRunArgs(args, out var scenarioArg, out var outputArg, out var outputFormat, out var verbose, out var runFontSize, out var runThemePreset, out var runError))
{
    Console.Error.WriteLine($"Error: {runError}");
    PrintUsage();
    return 1;
}

var scenarioPath = Path.GetFullPath(scenarioArg);
if (!File.Exists(scenarioPath))
{
    Console.Error.WriteLine($"Error: {scenarioPath} not found");
    return 1;
}

var outputStem = RenderSettingsResolver.ResolveOutputStem(scenarioPath, outputArg);
var outputPath = outputStem + ".cast";
var svgPath = outputFormat == OutputFormat.Svg ? outputStem + ".svg" : null;

Console.Error.WriteLine($"Loading: {scenarioPath}");
var yaml = File.ReadAllText(scenarioPath, Encoding.UTF8);
// Register VYaml formatters explicitly for NativeAOT (source generator cannot call __Register via reflection)
Scenario.__RegisterVYamlFormatter();
ScenarioSettings.__RegisterVYamlFormatter();
ScenarioRender.__RegisterVYamlFormatter();
ScenarioTheme.__RegisterVYamlFormatter();
var scenario = ParseScenario(yaml);
if (!RenderSettingsResolver.TryResolve(scenario, runThemePreset, out var renderSettings, out var themeError))
{
    Console.Error.WriteLine($"Error: {themeError}");
    PrintUsage();
    return 1;
}

if (runFontSize is int runFontSizeValue)
    renderSettings = renderSettings with { FontSize = runFontSizeValue };
var deterministicSeed = ComputeDeterministicSeed(yaml);
var deterministicTimestamp = ComputeDeterministicTimestamp(deterministicSeed);

var shell = ResolveShell(scenario);
Console.Error.WriteLine($"Using shell: {shell.DisplayName}");

var preExitCode = RunScenarioCommands(scenario.Pre, "pre", scenario.Cwd, shell, verbose);
if (preExitCode != 0)
    return preExitCode;

if (verbose)
    PrintPhase("steps");

Console.Error.WriteLine("Generating cast...");
var events = Generate(scenario, shell, deterministicSeed);

WriteCast(scenario, events, outputPath, shell, deterministicTimestamp, renderSettings);

var duration = events.Count > 0 ? events[^1].Time : 0.0;
Console.Error.WriteLine($"Written: {outputPath}  ({events.Count} events, {duration:F1}s)");

var exitCode = 0;
if (outputFormat == OutputFormat.Svg)
{
    var width = scenario.Width ?? 120;
    var height = scenario.Height ?? 24;
    try
    {
        SvgRender.WriteSvg(events, width, height, renderSettings, svgPath!);
        Console.Error.WriteLine($"Written: {svgPath}");
    }
    catch (Exception ex)
    {
        if (File.Exists(svgPath!))
            File.Delete(svgPath!);
        Console.Error.WriteLine($"Error: SVG render failed: {ex.Message}");
        exitCode = 1;
    }
}

var postExitCode = RunScenarioCommands(scenario.Post, "post", scenario.Cwd, shell, verbose);
if (postExitCode != 0)
    return postExitCode;

if (exitCode != 0)
    return exitCode;

if (outputFormat == OutputFormat.Svg)
    Console.Error.WriteLine($"Done: {outputPath}, {svgPath}");
else
    Console.Error.WriteLine($"Done: {outputPath}");
return 0;

static bool TryParseInitArgs(string[] args, out string? path, out string error)
{
    path = null;
    error = "";
    for (var i = 1; i < args.Length; i++)
    {
        var arg = args[i];
        if (arg.StartsWith('-'))
        {
            error = $"unknown option: {arg}";
            return false;
        }

        if (path is not null)
        {
            error = $"unexpected argument: {arg}";
            return false;
        }

        path = arg;
    }

    return true;
}

static bool TryParseSvgArgs(
    string[] args,
    out string castPath,
    out string? outputPath,
    out int? fontSizeOverride,
    out string? themePresetOverride,
    out string error)
{
    castPath = "";
    outputPath = null;
    fontSizeOverride = null;
    themePresetOverride = null;
    error = "";

    for (var i = 1; i < args.Length; i++)
    {
        var arg = args[i];
        if (TryConsumeFontSizeArg(args, ref i, ref fontSizeOverride, out error))
        {
            if (error.Length != 0)
                return false;
            continue;
        }

        if (TryConsumeThemeArg(args, ref i, ref themePresetOverride, out error))
        {
            if (error.Length != 0)
                return false;
            continue;
        }

        if (arg.StartsWith('-'))
        {
            error = $"unknown option: {arg}";
            return false;
        }

        if (castPath.Length == 0)
        {
            castPath = arg;
            continue;
        }

        if (outputPath is null)
        {
            outputPath = arg;
            continue;
        }

        error = $"unexpected argument: {arg}";
        return false;
    }

    if (castPath.Length != 0)
        return true;

    error = "cast path is required";
    return false;
}

static bool TryParseRunArgs(
    string[] args,
    out string scenarioPath,
    out string? outputPath,
    out OutputFormat outputFormat,
    out bool verbose,
    out int? fontSizeOverride,
    out string? themePresetOverride,
    out string error)
{
    scenarioPath = "";
    outputPath = null;
    outputFormat = OutputFormat.Cast;
    verbose = false;
    fontSizeOverride = null;
    themePresetOverride = null;
    error = "";

    for (var i = 0; i < args.Length; i++)
    {
        var arg = args[i];
        if (arg == "--verbose")
        {
            verbose = true;
            continue;
        }

        if (TryConsumeFontSizeArg(args, ref i, ref fontSizeOverride, out error))
        {
            if (error.Length != 0)
                return false;
            continue;
        }

        if (TryConsumeThemeArg(args, ref i, ref themePresetOverride, out error))
        {
            if (error.Length != 0)
                return false;
            continue;
        }

        if (arg == "--format")
        {
            if (i + 1 >= args.Length)
            {
                error = "--format requires a value";
                return false;
            }

            if (!TryParseOutputFormat(args[++i], out outputFormat, out error))
                return false;

            continue;
        }

        if (arg.StartsWith("--format=", StringComparison.Ordinal))
        {
            if (!TryParseOutputFormat(arg["--format=".Length..], out outputFormat, out error))
                return false;

            continue;
        }

        if (arg.StartsWith('-'))
        {
            error = $"unknown option: {arg}";
            return false;
        }

        if (scenarioPath.Length == 0)
        {
            scenarioPath = arg;
            continue;
        }

        if (outputPath is null)
        {
            outputPath = arg;
            continue;
        }

        error = $"unexpected argument: {arg}";
        return false;
    }

    if (scenarioPath.Length != 0)
        return true;

    error = "scenario path is required";
    return false;
}

static bool TryConsumeFontSizeArg(string[] args, ref int i, ref int? fontSizeOverride, out string error)
{
    error = "";
    var arg = args[i];
    string? value;

    if (arg == "--font-size")
    {
        if (i + 1 >= args.Length)
        {
            error = "--font-size requires a value";
            return true;
        }

        value = args[++i];
    }
    else if (arg.StartsWith("--font-size=", StringComparison.Ordinal))
    {
        value = arg["--font-size=".Length..];
        if (value.Length == 0)
        {
            error = "--font-size requires a value";
            return true;
        }
    }
    else
    {
        return false;
    }

    if (fontSizeOverride.HasValue)
    {
        error = "duplicate option: --font-size";
        return true;
    }

    if (!RenderSettingsResolver.TryParseFontSize(value, out var parsed, out error))
        return true;

    fontSizeOverride = parsed;
    return true;
}

static bool TryConsumeThemeArg(string[] args, ref int i, ref string? themePresetOverride, out string error)
{
    error = "";
    var arg = args[i];
    string? value;

    if (arg == "--theme")
    {
        if (i + 1 >= args.Length)
        {
            error = "--theme requires a value";
            return true;
        }

        value = args[++i];
    }
    else if (arg.StartsWith("--theme=", StringComparison.Ordinal))
    {
        value = arg["--theme=".Length..];
        if (value.Length == 0)
        {
            error = "--theme requires a value";
            return true;
        }
    }
    else
    {
        return false;
    }

    if (themePresetOverride is not null)
    {
        error = "duplicate option: --theme";
        return true;
    }

    if (!ThemePresets.TryParse(value, out var parsed, out error))
        return true;

    themePresetOverride = parsed;
    return true;
}

static bool TryParseOutputFormat(string value, out OutputFormat format, out string error)
{
    format = OutputFormat.Cast;
    error = "";
    switch (value.ToLowerInvariant())
    {
        case "cast":
            format = OutputFormat.Cast;
            return true;
        case "svg":
            format = OutputFormat.Svg;
            return true;
        default:
            error = $"unknown format: {value}";
            return false;
    }
}

static Scenario ParseScenario(string yaml)
{
    var bytes = Encoding.UTF8.GetBytes(yaml);
    return YamlSerializer.Deserialize<Scenario>(bytes) ?? new Scenario();
}

static List<CastEvent> Generate(Scenario scenario, ShellLaunch shell, int deterministicSeed)
{
    var settings = scenario.Settings ?? new ScenarioSettings();
    var prompt    = settings.Prompt ?? DefaultPrompt;
    var speed     = settings.TypingSpeed ?? DefaultSpeed;
    var jitter    = settings.TypingJitter ?? DefaultJitter;
    var preDelay  = settings.PreDelay ?? DefaultPreDelay;
    var postDelay = settings.PostDelay ?? DefaultPostDelay;
    var defaultExecutionDuration = settings.ExecutionDuration ?? DefaultExecutionDuration;
    var defaultStderrStyle = ParseStyleOrFallback(settings.StderrColor, SgrNamed(DefaultStderrColorSpec), "settings.stderr-color");
    var events = new List<CastEvent>();
    var rng    = new Random(deterministicSeed);
    double t   = 0.5;

    var steps = scenario.Steps ?? [];
    for (var i = 0; i < steps.Count; i++)
    {
        var command = ParseCommand(steps[i]);
        if (string.IsNullOrWhiteSpace(command.Cmd)) continue;

        var cmdSpeed  = GetDouble(command.Extra, speed, "typing-speed");
        var cmdJitter = GetDouble(command.Extra, jitter, "typing-jitter");
        var cmdPre    = GetDouble(command.Extra, preDelay, "pre-delay");
        var cmdPost   = GetDouble(command.Extra, postDelay, "post-delay");
        var cmdExecutionDuration = GetDouble(command.Extra, defaultExecutionDuration, "execution-duration");
        var cmdStderrStyle = GetOverrideStyle(command.Extra, "stderr-color", defaultStderrStyle, "stderr-color", command.Cmd);
        var hasRunHighlight = TryGetStepStyle(command.Extra, "run-highlight", "run-highlight", command.Cmd, out var runHighlightStyle);

        if (events.Count == 0)
            t += preDelay;

        if (TryFormatNameComment(command.Name, command.Cmd, out var nameLine, out var nameDisplayText))
        {
            var prefix = NameCommentPrefix(events.Count > 0 ? events[^1].Data : null);
            events.Add(CastEvent.Marker(Math.Round(t, 6), nameDisplayText));
            events.Add(CastEvent.Output(Math.Round(t, 6), prefix + nameLine));
            t += 0.05;
        }

        events.Add(CastEvent.Output(Math.Round(t, 6), prompt));
        t += 0.05;

        if (hasRunHighlight && !string.IsNullOrEmpty(runHighlightStyle))
            events.Add(CastEvent.Output(Math.Round(t, 6), runHighlightStyle));

        foreach (var ch in command.Cmd)
        {
            events.Add(CastEvent.Output(Math.Round(t, 6), ch.ToString()));
            var delay = cmdSpeed + rng.NextDouble() * 2 * cmdJitter - cmdJitter;
            t += Math.Max(delay, 0.005);
        }

        if (hasRunHighlight && !string.IsNullOrEmpty(runHighlightStyle))
            events.Add(CastEvent.Output(Math.Round(t, 6), SgrReset));

        events.Add(CastEvent.Output(Math.Round(t, 6), "\r\n"));
        t += 0.15;

        Console.Error.WriteLine($"  running: {command.Cmd}");
        var execution = RunCommand(command.Cmd, scenario.Cwd, shell);
        t += cmdExecutionDuration;
        var mergedOutput = MergeCommandOutput(execution, cmdStderrStyle);

        if (!string.IsNullOrEmpty(mergedOutput))
        {
            var output = GetHighlights(command.Extra, command.Cmd) is { } highlights
                ? ApplyHighlights(mergedOutput, highlights, command.Cmd)
                : mergedOutput;
            events.Add(CastEvent.Output(Math.Round(t, 6), NormalizeNewlines(output)));
        }

        t += cmdPost;
        t += cmdPre;
    }

    if (events.Count > 0)
        events.Add(CastEvent.Output(Math.Round(t, 6), prompt));

    return events;
}

static string NameCommentPrefix(string? precedingOutput)
{
    if (string.IsNullOrEmpty(precedingOutput)) return "";
    return precedingOutput.EndsWith("\r\n", StringComparison.Ordinal) || precedingOutput.EndsWith('\n')
        ? ""
        : "\r\n";
}

static CommandEntry ParseCommand(object? item)
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

static CommandOutput RunCommand(string cmd, string? cwd, ShellLaunch shell)
{
    var psi = new ProcessStartInfo(shell.FileName)
    {
        RedirectStandardOutput = true,
        RedirectStandardError  = true,
        UseShellExecute        = false,
        CreateNoWindow         = true,
    };
    foreach (var arg in shell.Arguments)
        psi.ArgumentList.Add(arg);
    psi.ArgumentList.Add(cmd);
    if (!string.IsNullOrWhiteSpace(cwd)) psi.WorkingDirectory = cwd;

    using var proc = new Process { StartInfo = psi };
    proc.Start();
    var stdout = proc.StandardOutput.ReadToEnd();
    var stderr = proc.StandardError.ReadToEnd();
    proc.WaitForExit();
    return new CommandOutput(stdout, stderr, proc.ExitCode);
}

static int RunScenarioCommands(List<string>? commands, string phase, string? cwd, ShellLaunch shell, bool verbose)
{
    if (commands is null || commands.Count == 0)
        return 0;

    var phasePrinted = false;
    foreach (var cmd in commands)
    {
        if (string.IsNullOrWhiteSpace(cmd))
            continue;

        if (verbose)
        {
            if (!phasePrinted)
            {
                PrintPhase(phase);
                phasePrinted = true;
            }

            Console.Error.WriteLine($"Running {phase}:");
            Console.Error.WriteLine(cmd);
        }

        CommandOutput execution;
        try
        {
            execution = RunCommand(cmd, cwd, shell);
        }
        catch (Exception ex)
        {
            PrintScenarioCommandFailure(phase, cmd, 1);
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        if (!string.IsNullOrEmpty(execution.Stdout))
            Console.Out.Write(execution.Stdout);
        if (!string.IsNullOrEmpty(execution.Stderr))
            Console.Error.Write(execution.Stderr);

        if (execution.ExitCode == 0)
            continue;

        PrintScenarioCommandFailure(phase, cmd, execution.ExitCode);
        return execution.ExitCode;
    }

    return 0;
}

static void PrintPhase(string name) => Console.Error.WriteLine($"== {name} ==");

static void PrintScenarioCommandFailure(string phase, string cmd, int exitCode)
{
    Console.Error.WriteLine($"{phase} failed (exit code {exitCode}):");
    Console.Error.WriteLine(cmd);
}

static string MergeCommandOutput(CommandOutput output, string stderrStyle)
{
    if (string.IsNullOrEmpty(output.Stdout))
    {
        if (string.IsNullOrEmpty(output.Stderr) || string.IsNullOrEmpty(stderrStyle) || ContainsAnsiSgr(output.Stderr))
            return output.Stderr;

        return WrapWithStylePreserveTrailingNewlines(output.Stderr, stderrStyle);
    }

    if (string.IsNullOrEmpty(output.Stderr))
        return output.Stdout;

    if (string.IsNullOrEmpty(stderrStyle) || ContainsAnsiSgr(output.Stderr))
        return string.Concat(output.Stdout, output.Stderr);

    return string.Concat(output.Stdout, WrapWithStylePreserveTrailingNewlines(output.Stderr, stderrStyle));
}

static string WrapWithStylePreserveTrailingNewlines(string text, string styleOpen)
{
    if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(styleOpen))
        return text;

    var splitAt = text.Length;
    while (splitAt > 0)
    {
        var ch = text[splitAt - 1];
        if (ch is '\n' or '\r')
            splitAt--;
        else
            break;
    }

    if (splitAt == text.Length)
        return string.Concat(styleOpen, text, SgrReset);

    var body = text[..splitAt];
    var trailingNewlines = text[splitAt..];
    return string.Concat(styleOpen, body, SgrReset, trailingNewlines);
}

static bool ContainsAnsiSgr(string text)
{
    for (var i = 0; i + 2 < text.Length; i++)
    {
        if (text[i] != '\u001b' || text[i + 1] != '[')
            continue;

        for (var j = i + 2; j < text.Length; j++)
        {
            var ch = text[j];
            if ((uint)(ch - '0') <= 9 || ch == ';')
                continue;

            if (ch == 'm')
                return true;

            break;
        }
    }

    return false;
}

static string NormalizeNewlines(string s)
    => s.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "\r\n");

static bool TryFormatNameComment(string? raw, string cmd, out string coloredLine, out string displayText)
{
    coloredLine = "";
    displayText = "";
    if (string.IsNullOrWhiteSpace(raw))
        return false;

    var value = raw.Trim();
    var colorOpen = SgrNamed("cyan");
    if (value.StartsWith('['))
    {
        var close = value.IndexOf(']');
        if (close > 1)
        {
            var colorName = value[1..close];
            displayText = value[(close + 1)..].TrimStart();
            if (!TryParseStyle(colorName, out var parsedStyle))
            {
                Warn("name", cmd, $"unknown color/style '{colorName}'");
                colorOpen = SgrNamed("cyan");
            }
            else
            {
                colorOpen = parsedStyle;
            }
        }
        else
        {
            displayText = value;
        }
    }
    else
    {
        displayText = value;
    }

    if (string.IsNullOrWhiteSpace(displayText))
    {
        Warn("name", cmd, "empty name text after color prefix");
        return false;
    }

    coloredLine = string.IsNullOrEmpty(colorOpen)
        ? $"# {displayText}\r\n"
        : $"{colorOpen}# {displayText}{SgrReset}\r\n";
    return true;
}

static string GetOverrideStyle(Dictionary<string, object?> extra, string key, string fallbackStyle, string scope, string cmd)
    => extra.TryGetValue(key, out var raw)
        ? ParseStyleOrFallback(raw?.ToString(), fallbackStyle, scope, cmd)
        : fallbackStyle;

static bool TryGetStepStyle(Dictionary<string, object?> extra, string key, string scope, string cmd, out string style)
{
    style = "";
    if (!extra.TryGetValue(key, out var raw))
        return false;

    var value = raw?.ToString();
    if (TryParseStyle(value, out style))
        return true;

    Warn(scope, cmd, $"unknown color/style '{value}'");
    return false;
}

static string SgrOpen(string codes) => $"\u001b[{codes}m";

static string SgrNamed(string name)
{
    return TryNamedForegroundCode(name, out var code)
        ? SgrOpen(code.ToString(CultureInfo.InvariantCulture))
        : "";
}

static List<HighlightSpec>? GetHighlights(Dictionary<string, object?> extra, string cmd)
{
    if (!extra.TryGetValue("highlight", out var raw) || raw is not List<object?> list || list.Count == 0)
        return null;

    var specs = new List<HighlightSpec>(list.Count);
    foreach (var item in list)
    {
        if (item is not Dictionary<object, object?> map) continue;
        var colorRaw = map.GetValueOrDefault("color")?.ToString();
        if (!TryParseStyle(colorRaw, out var colorOpen))
        {
            Warn("highlight", cmd, $"unknown color/style '{colorRaw}'");
            continue;
        }

        if (string.IsNullOrEmpty(colorOpen))
            continue;

        if (!map.TryGetValue("at", out var atRaw) || atRaw is null)
        {
            Warn("highlight", cmd, "missing 'at'");
            continue;
        }

        var ats = new List<string>();
        if (atRaw is List<object?> atList)
        {
            foreach (var a in atList)
            {
                var s = a?.ToString();
                if (!string.IsNullOrWhiteSpace(s)) ats.Add(s);
            }
        }
        else
        {
            var s = atRaw.ToString();
            if (!string.IsNullOrWhiteSpace(s)) ats.Add(s);
        }

        if (ats.Count == 0)
        {
            Warn("highlight", cmd, "missing 'at'");
            continue;
        }

        specs.Add(new HighlightSpec(colorOpen, ats));
    }

    return specs.Count > 0 ? specs : null;
}

static bool TryNamedForegroundCode(string? name, out int code)
{
    code = name?.Trim().ToLowerInvariant() switch
    {
        "black" => 30,
        "red" => 31,
        "green" => 32,
        "yellow" => 33,
        "blue" => 34,
        "magenta" => 35,
        "cyan" => 36,
        "white" => 37,
        "bright-black" or "gray" or "grey" => 90,
        "bright-red" => 91,
        "bright-green" => 92,
        "bright-yellow" => 93,
        "bright-blue" => 94,
        "bright-magenta" => 95,
        "bright-cyan" => 96,
        "bright-white" => 97,
        _ => 0,
    };

    return code != 0;
}

static bool TryParseStyle(string? raw, out string styleOpen)
{
    styleOpen = "";
    if (string.IsNullOrWhiteSpace(raw))
        return false;

    var text = raw.Trim();
    if (text.Equals("none", StringComparison.OrdinalIgnoreCase) ||
        text.Equals("off", StringComparison.OrdinalIgnoreCase) ||
        text.Equals("default", StringComparison.OrdinalIgnoreCase) ||
        text.Equals("reset", StringComparison.OrdinalIgnoreCase))
    {
        styleOpen = "";
        return true;
    }

    if (TryNamedForegroundCode(text, out var namedCode))
    {
        styleOpen = SgrOpen(namedCode.ToString(CultureInfo.InvariantCulture));
        return true;
    }

    if (TryParseSgrLiteral(text, out var sgrCodes))
    {
        styleOpen = SgrOpen(sgrCodes);
        return true;
    }

    if (TryParseStyleWords(text, out var wordCodes))
    {
        styleOpen = SgrOpen(wordCodes);
        return true;
    }

    return false;
}

static bool TryParseSgrLiteral(string raw, out string codes)
{
    codes = "";
    var text = raw.Trim();

    if (text.StartsWith("\\e[", StringComparison.OrdinalIgnoreCase))
        text = text[3..];
    else if (text.StartsWith("\\x1b[", StringComparison.OrdinalIgnoreCase))
        text = text[5..];
    else if (text.StartsWith("\\u001b[", StringComparison.OrdinalIgnoreCase))
        text = text[7..];
    else if (text.Length >= 2 && text[0] == '\u001b' && text[1] == '[')
        text = text[2..];
    else if (text.StartsWith("[", StringComparison.Ordinal))
        text = text[1..];

    if (text.EndsWith("m", StringComparison.OrdinalIgnoreCase))
        text = text[..^1];

    if (text.Length == 0)
        return false;

    if (text[0] == ';' || text[^1] == ';')
        return false;

    var parts = text.Split(';', StringSplitOptions.TrimEntries);
    if (parts.Length == 0)
        return false;

    var normalized = new List<string>(parts.Length);
    for (var i = 0; i < parts.Length; i++)
    {
        var part = parts[i];
        if (!int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out var value))
            return false;

        if (value is 38 or 48 && i + 1 < parts.Length && parts[i + 1] == "5")
        {
            if (i + 2 >= parts.Length)
                return false;

            if (!int.TryParse(parts[i + 2], NumberStyles.None, CultureInfo.InvariantCulture, out var colorIndex))
                return false;

            if (colorIndex is < 0 or > 255)
                return false;

            normalized.Add(value.ToString(CultureInfo.InvariantCulture));
            normalized.Add("5");
            normalized.Add(colorIndex.ToString(CultureInfo.InvariantCulture));
            i += 2;
            continue;
        }

        if (value is 38 or 48 && i + 1 < parts.Length && parts[i + 1] == "2")
        {
            if (i + 4 >= parts.Length)
                return false;

            if (!int.TryParse(parts[i + 2], NumberStyles.None, CultureInfo.InvariantCulture, out var r) ||
                !int.TryParse(parts[i + 3], NumberStyles.None, CultureInfo.InvariantCulture, out var g) ||
                !int.TryParse(parts[i + 4], NumberStyles.None, CultureInfo.InvariantCulture, out var b))
            {
                return false;
            }

            if (r is < 0 or > 255 || g is < 0 or > 255 || b is < 0 or > 255)
                return false;

            normalized.Add(value.ToString(CultureInfo.InvariantCulture));
            normalized.Add("2");
            normalized.Add(r.ToString(CultureInfo.InvariantCulture));
            normalized.Add(g.ToString(CultureInfo.InvariantCulture));
            normalized.Add(b.ToString(CultureInfo.InvariantCulture));
            i += 4;
            continue;
        }

        if (value is < 0 or > 107)
            return false;

        normalized.Add(value.ToString(CultureInfo.InvariantCulture));
    }

    codes = string.Join(';', normalized);
    return true;
}

static bool TryParseStyleWords(string raw, out string codes)
{
    codes = "";
    var tokens = raw
        .Split([' ', ',', '+'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(static t => t.Trim().ToLowerInvariant())
        .ToArray();

    if (tokens.Length == 0)
        return false;

    var bold = false;
    var underline = false;
    var intensityRequested = false;
    string? fgCodes = null;
    string? bgCodes = null;
    int simpleFgCode = 0;

    foreach (var token in tokens)
    {
        if (token is "bold")
        {
            bold = true;
            continue;
        }

        if (token is "underline")
        {
            underline = true;
            continue;
        }

        if (token is "bright")
        {
            intensityRequested = true;
            continue;
        }

        if (TryParseFgBgToken(token, out var isBackground, out var colorName))
        {
            if (isBackground)
            {
                if (!TryParseStyleColor(colorName, isBackground: true, out bgCodes, out _))
                    return false;
            }
            else
            {
                if (!TryParseStyleColor(colorName, isBackground: false, out fgCodes, out simpleFgCode))
                    return false;
            }

            continue;
        }

        if (TryNamedForegroundCode(token, out var directFg))
        {
            simpleFgCode = directFg;
            fgCodes = directFg.ToString(CultureInfo.InvariantCulture);
            continue;
        }

        return false;
    }

    if (intensityRequested)
    {
        if (simpleFgCode is >= 30 and <= 37)
            fgCodes = (simpleFgCode + 60).ToString(CultureInfo.InvariantCulture);
        else if (fgCodes is null)
            bold = true;
    }

    var list = new List<string>(4);
    if (bold) list.Add("1");
    if (underline) list.Add("4");
    if (fgCodes is not null) list.Add(fgCodes);
    if (bgCodes is not null) list.Add(bgCodes);
    if (list.Count == 0) return false;

    codes = string.Join(';', list);
    return true;
}

static bool TryParseStyleColor(string raw, bool isBackground, out string codes, out int simpleForegroundCode)
{
    codes = "";
    simpleForegroundCode = 0;

    if (TryNamedForegroundCode(raw, out var fgCode))
    {
        simpleForegroundCode = isBackground ? 0 : fgCode;
        codes = (isBackground ? fgCode + 10 : fgCode).ToString(CultureInfo.InvariantCulture);
        return true;
    }

    if (TryParseTrueColorValue(raw, out var r, out var g, out var b))
    {
        codes = isBackground
            ? string.Create(CultureInfo.InvariantCulture, $"48;2;{r};{g};{b}")
            : string.Create(CultureInfo.InvariantCulture, $"38;2;{r};{g};{b}");
        return true;
    }

    if (!int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var colorIndex))
        return false;

    if (colorIndex is < 0 or > 255)
        return false;

    codes = isBackground
        ? $"48;5;{colorIndex.ToString(CultureInfo.InvariantCulture)}"
        : $"38;5;{colorIndex.ToString(CultureInfo.InvariantCulture)}";
    return true;
}

static bool TryParseTrueColorValue(string raw, out int r, out int g, out int b)
{
    r = g = b = 0;
    var text = raw.Trim();
    if (text.Length == 0)
        return false;

    if (text[0] == '#')
    {
        var hex = text[1..];
        if (hex.Length == 3)
        {
            if (!TryParseHexComponent(hex[0], out var rNibble) ||
                !TryParseHexComponent(hex[1], out var gNibble) ||
                !TryParseHexComponent(hex[2], out var bNibble))
            {
                return false;
            }

            r = rNibble * 17;
            g = gNibble * 17;
            b = bNibble * 17;
            return true;
        }

        if (hex.Length != 6)
            return false;

        if (!TryParseHexPair(hex[0], hex[1], out r) ||
            !TryParseHexPair(hex[2], hex[3], out g) ||
            !TryParseHexPair(hex[4], hex[5], out b))
        {
            return false;
        }

        return true;
    }

    var parts = text.Split(',', StringSplitOptions.TrimEntries);
    if (parts.Length != 3)
        return false;

    if (!int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out r) ||
        !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out g) ||
        !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out b))
    {
        return false;
    }

    return r is >= 0 and <= 255 &&
           g is >= 0 and <= 255 &&
           b is >= 0 and <= 255;
}

static bool TryParseHexPair(char hi, char lo, out int value)
{
    value = 0;
    if (!TryParseHexComponent(hi, out var high) || !TryParseHexComponent(lo, out var low))
        return false;

    value = high * 16 + low;
    return true;
}

static bool TryParseHexComponent(char c, out int value)
{
    if (c is >= '0' and <= '9')
    {
        value = c - '0';
        return true;
    }

    if (c is >= 'a' and <= 'f')
    {
        value = c - 'a' + 10;
        return true;
    }

    if (c is >= 'A' and <= 'F')
    {
        value = c - 'A' + 10;
        return true;
    }

    value = 0;
    return false;
}

static bool TryParseFgBgToken(string token, out bool isBackground, out string colorName)
{
    isBackground = false;
    colorName = "";
    if (token.StartsWith("fg:", StringComparison.Ordinal))
    {
        var value = token[3..].Trim();
        if (value.Length == 0)
            return false;

        isBackground = false;
        colorName = value;
        return true;
    }

    if (token.StartsWith("bg:", StringComparison.Ordinal))
    {
        var value = token[3..].Trim();
        if (value.Length == 0)
            return false;

        isBackground = true;
        colorName = value;
        return true;
    }

    return false;
}

static string ApplyHighlights(string output, List<HighlightSpec> specs, string cmd)
{
    var text = NormalizeToLf(output);
    var lineCount = CountLines(text.AsSpan());
    var lineRangesArray = ArrayPool<int>.Shared.Rent(lineCount * 2);

    try
    {
        var lineRanges = lineRangesArray.AsSpan(0, lineCount * 2);
        FillLineRanges(text.AsSpan(), lineRanges);

        var paint = new ushort[lineCount][];
        var styleIds = new Dictionary<string, ushort>(StringComparer.Ordinal);
        var stylesList = new List<string> { "" };

        foreach (var spec in specs)
        {
            var styleId = GetOrAddStyleId(styleIds, stylesList, spec.ColorOpen);
            foreach (var at in spec.At)
                ApplyAt(lineRanges, paint, at, styleId, cmd);
        }

        var styles = CollectionsMarshal.AsSpan(stylesList);
        var sb = new StringBuilder(text.Length + specs.Count * 16);
        for (var i = 0; i < lineCount; i++)
        {
            if (i > 0) sb.Append('\n');
            var start = lineRanges[i * 2];
            var length = lineRanges[i * 2 + 1];
            AppendPaintedLine(sb, text.AsSpan(start, length), paint[i], styles);
        }

        return sb.ToString();
    }
    finally
    {
        ArrayPool<int>.Shared.Return(lineRangesArray, clearArray: false);
    }
}

static string NormalizeToLf(string text)
{
    if (text.IndexOf('\r') < 0)
        return text;

    var sb = new StringBuilder(text.Length);
    var span = text.AsSpan();
    for (var i = 0; i < span.Length; i++)
    {
        var ch = span[i];
        if (ch == '\r')
        {
            sb.Append('\n');
            if (i + 1 < span.Length && span[i + 1] == '\n')
                i++;
            continue;
        }

        sb.Append(ch);
    }

    return sb.ToString();
}

static int CountLines(ReadOnlySpan<char> text)
{
    var count = 1;
    for (var i = 0; i < text.Length; i++)
    {
        if (text[i] == '\n')
            count++;
    }

    return count;
}

static void FillLineRanges(ReadOnlySpan<char> text, Span<int> lineRanges)
{
    var start = 0;
    var line = 0;
    for (var i = 0; i < text.Length; i++)
    {
        if (text[i] != '\n')
            continue;

        lineRanges[line * 2] = start;
        lineRanges[line * 2 + 1] = i - start;
        line++;
        start = i + 1;
    }

    lineRanges[line * 2] = start;
    lineRanges[line * 2 + 1] = text.Length - start;
}

static void ApplyAt(ReadOnlySpan<int> lines, ushort[][] paint, string at, ushort styleId, string cmd)
{
    if (!TryParseAt(at, out var lineLo, out var lineHi, out var openLineEnd, out var colLo, out var colHi, out var openColEnd, out var hasCol))
    {
        Warn("highlight", cmd, $"invalid at '{at}'");
        return;
    }

    var lineCount = lines.Length / 2;
    if (lineLo > lineCount)
    {
        Warn("highlight", cmd, $"at '{at}' starts after output");
        return;
    }

    if (openLineEnd)
    {
        lineHi = lineCount;
    }
    else if (lineHi > lineCount)
    {
        Warn("highlight", cmd, $"at '{at}': lines {lineCount + 1}-{lineHi} are missing");
        lineHi = lineCount;
    }

    for (var line = lineLo; line <= lineHi; line++)
    {
        var li = line - 1;
        var contentLength = lines[li * 2 + 1];
        int start, end;
        if (hasCol)
        {
            start = colLo - 1;
            end = openColEnd ? contentLength : colHi;
            if (start >= contentLength)
            {
                if (lineLo == lineHi)
                    Warn("highlight", cmd, $"at '{at}' starts after output");
                continue;
            }
            if (end > contentLength) end = contentLength;
        }
        else
        {
            start = 0;
            end = contentLength;
        }

        if (start >= end) continue;
        var row = paint[li] ??= new ushort[contentLength];
        for (var c = start; c < end; c++)
            row[c] = styleId;
    }
}

static void AppendPaintedLine(StringBuilder sb, ReadOnlySpan<char> line, ushort[]? row, ReadOnlySpan<string> styles)
{
    if (row is null)
    {
        sb.Append(line);
        return;
    }

    ushort cur = 0;
    for (var i = 0; i < line.Length; i++)
    {
        var next = row[i];
        if (next != cur)
        {
            if (cur != 0) sb.Append(SgrReset);
            if (next != 0) sb.Append(styles[next]);
            cur = next;
        }
        sb.Append(line[i]);
    }

    if (cur != 0) sb.Append(SgrReset);
}

static ushort GetOrAddStyleId(Dictionary<string, ushort> styleIds, List<string> styles, string style)
{
    if (styleIds.TryGetValue(style, out var id))
        return id;

    if (styles.Count >= ushort.MaxValue)
        throw new InvalidOperationException("Too many unique highlight styles in one output.");

    id = (ushort)styles.Count;
    styles.Add(style);
    styleIds[style] = id;
    return id;
}

static bool TryParseAt(string at, out int lineLo, out int lineHi, out bool openLineEnd, out int colLo, out int colHi, out bool openColEnd, out bool hasCol)
{
    lineLo = lineHi = colLo = colHi = 0;
    openLineEnd = openColEnd = hasCol = false;
    var span = at.AsSpan().Trim();
    if (span.IsEmpty) return false;

    var colon = span.IndexOf(':');
    if (!TryParseSpan(span[..(colon >= 0 ? colon : span.Length)], allowOpenEnd: true, out lineLo, out lineHi, out openLineEnd))
        return false;

    if (colon < 0) return true;
    hasCol = true;
    return TryParseColSpan(span[(colon + 1)..], out colLo, out colHi, out openColEnd);
}

static bool TryParseSpan(ReadOnlySpan<char> span, bool allowOpenEnd, out int lo, out int hi, out bool openEnd)
{
    lo = hi = 0;
    openEnd = false;
    var dash = span.IndexOf('-');
    if (dash < 0)
        return TryPosInt(span, out lo) && (hi = lo) > 0;

    if (allowOpenEnd && dash == span.Length - 1)
    {
        if (!TryPosInt(span[..dash], out lo) || lo < 1)
            return false;

        hi = lo;
        openEnd = true;
        return true;
    }

    if (!TryPosInt(span[..dash], out lo) || !TryPosInt(span[(dash + 1)..], out hi) || lo < 1 || hi < 1)
        return false;

    if (lo > hi) (lo, hi) = (hi, lo);
    return true;
}

static bool TryParseColSpan(ReadOnlySpan<char> span, out int lo, out int hi, out bool openEnd)
{
    lo = hi = 0;
    openEnd = false;
    if (span.EndsWith("-"))
    {
        openEnd = true;
        span = span[..^1];
        return TryPosInt(span, out lo) && lo > 0;
    }

    return TryParseSpan(span, allowOpenEnd: false, out lo, out hi, out _);
}

static bool TryPosInt(ReadOnlySpan<char> span, out int value)
{
    value = 0;
    if (span.IsEmpty) return false;
    foreach (var ch in span)
    {
        if (ch is < '0' or > '9') return false;
        value = value * 10 + (ch - '0');
    }
    return value > 0;
}

static string ParseStyleOrFallback(string? raw, string fallbackStyle, string scope, string? cmd = null)
{
    if (string.IsNullOrWhiteSpace(raw))
        return fallbackStyle;

    if (TryParseStyle(raw, out var style))
        return style;

    Warn(scope, cmd, $"unknown color/style '{raw}'");
    return fallbackStyle;
}

static void Warn(string scope, string? cmd, string detail)
{
    if (string.IsNullOrWhiteSpace(cmd))
        Console.Error.WriteLine($"Warning: {scope}: {detail}");
    else
        Console.Error.WriteLine($"Warning: {scope} ({cmd}): {detail}");
}

static void WriteCast(
    Scenario scenario,
    List<CastEvent> events,
    string outputPath,
    ShellLaunch shell,
    long timestamp,
    ResolvedRenderSettings renderSettings)
{
    using var writer = new StreamWriter(outputPath, append: false, encoding: new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    writer.NewLine = "\n";

    var width = scenario.Width ?? 120;
    var height = scenario.Height ?? 24;
    var title = scenario.Title ?? "";
    var fontSize = renderSettings.FontSize.ToString(CultureInfo.InvariantCulture);
    writer.WriteLine(
        $"{{\"version\":3,\"term\":{{\"cols\":{width},\"rows\":{height}" +
        $",\"type\":\"xterm-256color\"" +
        $",\"theme\":{{\"fg\":{JsonString(renderSettings.Theme.Fg)},\"bg\":{JsonString(renderSettings.Theme.Bg)},\"palette\":{JsonString(renderSettings.Theme.Palette)}}}}}" +
        $",\"timestamp\":{timestamp},\"title\":{JsonString(title)}" +
        $",\"env\":{{\"SHELL\":{JsonString(shell.EnvValue)}}}" +
        $",\"tags\":[\"s2c:font-size={fontSize}\"]}}");

    var intervalError = 0.0;
    var previousAbs = 0.0;
    for (var i = 0; i < events.Count; i++)
    {
        var ev = events[i];
        var exact = i == 0 ? ev.Time : ev.Time - previousAbs;
        previousAbs = ev.Time;
        var code = ev.Kind switch
        {
            CastEventKind.Resize => "r",
            CastEventKind.Marker => "m",
            _ => "o",
        };
        writer.WriteLine($"[{FormatCastInterval(exact, ref intervalError)},\"{code}\",{JsonString(ev.Data)}]");
    }

    writer.WriteLine($"[{FormatCastInterval(events.Count > 0 ? 0.05 : 0, ref intervalError)},\"x\",\"0\"]");
}

static string FormatCastInterval(double exact, ref double error)
{
    const double scale = 1000.0;
    var scaled = exact * scale + error;
    var quantized = Math.Round(scaled, MidpointRounding.AwayFromZero);
    error = scaled - quantized;
    return (quantized / scale).ToString("0.000", CultureInfo.InvariantCulture);
}

static string JsonString(string s)
{
    var sb = new StringBuilder(s.Length + 2);
    sb.Append('"');
    foreach (var c in s)
    {
        switch (c)
        {
            case '"':  sb.Append("\\\""); break;
            case '\\': sb.Append("\\\\"); break;
            case '\b': sb.Append("\\b");  break;
            case '\f': sb.Append("\\f");  break;
            case '\n': sb.Append("\\n");  break;
            case '\r': sb.Append("\\r");  break;
            case '\t': sb.Append("\\t");  break;
            default:
                if (c < 0x20)
                    sb.Append($"\\u{(int)c:x4}");
                else
                    sb.Append(c);
                break;
        }
    }
    sb.Append('"');
    return sb.ToString();
}

static double GetDouble(Dictionary<string, object?> d, double def, params string[] keys)
{
    foreach (var key in keys)
    {
        if (d.TryGetValue(key, out var value) &&
            double.TryParse(value?.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }
    }

    return def;
}

static ShellLaunch ResolveShell(Scenario scenario)
{
    var requested = scenario.Shell;

    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        return ResolveWindowsShell(requested);

    if (string.IsNullOrWhiteSpace(requested))
        requested = Environment.GetEnvironmentVariable("SHELL");

    if (string.IsNullOrWhiteSpace(requested))
        requested = "bash";

    return new ShellLaunch(requested, ["-lc"], requested, requested);
}

static ShellLaunch ResolveWindowsShell(string? requested)
{
    if (string.IsNullOrWhiteSpace(requested))
    {
        if (TryResolveExecutableOnWindows("pwsh", out var pwsh))
            return new ShellLaunch(pwsh, ["-NoLogo", "-NoProfile", "-Command"], pwsh, "pwsh");

        if (TryResolveExecutableOnWindows("powershell", out var powershell))
            return new ShellLaunch(powershell, ["-NoLogo", "-NoProfile", "-Command"], powershell, "powershell");

        throw new InvalidOperationException("No supported shell was found on Windows. Install pwsh or powershell, or set settings.shell to a Git Bash or MSYS bash.exe path.");
    }

    if (IsPowerShellName(requested))
    {
        var exeName = NormalizeWindowsShellName(requested);
        if (TryResolveExecutableOnWindows(exeName, out var resolved))
            return new ShellLaunch(resolved, ["-NoLogo", "-NoProfile", "-Command"], resolved, exeName);

        throw new InvalidOperationException($"Shell '{requested}' was requested, but '{exeName}' could not be found on Windows.");
    }

    if (string.Equals(NormalizeWindowsShellName(requested), "bash", StringComparison.OrdinalIgnoreCase))
    {
        if (TryResolveWindowsBash(out var resolved))
            return new ShellLaunch(resolved, ["-lc"], resolved, "bash");

        throw new InvalidOperationException("Shell 'bash' was requested, but a Git Bash or MSYS bash.exe could not be found. WSL bash is intentionally not used.");
    }

    if (Path.IsPathRooted(requested) || requested.Contains(Path.DirectorySeparatorChar) || requested.Contains(Path.AltDirectorySeparatorChar))
    {
        if (File.Exists(requested))
            return new ShellLaunch(requested, ["-c"], requested, requested);

        throw new InvalidOperationException($"Shell '{requested}' was requested, but the path does not exist.");
    }

    if (TryResolveExecutableOnWindows(requested, out var resolvedExecutable))
        return new ShellLaunch(resolvedExecutable, ["-c"], resolvedExecutable, requested);

    throw new InvalidOperationException($"Shell '{requested}' could not be resolved on Windows.");
}

static bool IsPowerShellName(string shell)
{
    var name = NormalizeWindowsShellName(shell);
    return name is "pwsh" or "powershell";
}

static string NormalizeWindowsShellName(string shell)
    => Path.GetFileNameWithoutExtension(shell.Trim()).ToLowerInvariant();

static bool TryResolveWindowsBash(out string resolved)
{
    var candidateRoots = new[]
    {
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
        Environment.GetEnvironmentVariable("ProgramW6432") ?? string.Empty,
        Environment.GetEnvironmentVariable("ProgramFiles") ?? string.Empty,
        Environment.GetEnvironmentVariable("ProgramFiles(x86)") ?? string.Empty,
        Environment.GetEnvironmentVariable("LOCALAPPDATA") ?? string.Empty,
        Environment.GetEnvironmentVariable("ProgramData") ?? string.Empty,
    };

    var candidatePaths = new[]
    {
        @"Git\bin\bash.exe",
        @"Git\usr\bin\bash.exe",
        @"msys64\usr\bin\bash.exe",
        @"GitHub\Desktop\bin\bash.exe",
    };

    foreach (var root in candidateRoots.Where(static r => !string.IsNullOrWhiteSpace(r)))
    {
        foreach (var suffix in candidatePaths)
        {
            var candidate = Path.Combine(root, suffix);
            if (File.Exists(candidate))
            {
                resolved = candidate;
                return true;
            }
        }
    }

    if (TryResolveExecutableOnWindows("bash.exe", out resolved) && !IsWslBashPath(resolved))
        return true;

    resolved = string.Empty;
    return false;
}

static bool IsWslBashPath(string path)
    => path.Contains(@"\Windows\System32\bash.exe", StringComparison.OrdinalIgnoreCase)
       || path.Contains(@"\WindowsApps\", StringComparison.OrdinalIgnoreCase);

static bool TryResolveExecutableOnWindows(string executableName, out string resolved)
{
    if (File.Exists(executableName))
    {
        resolved = Path.GetFullPath(executableName);
        return true;
    }

    var pathEnv = Environment.GetEnvironmentVariable("PATH");
    if (!string.IsNullOrWhiteSpace(pathEnv))
    {
        var paths = pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var pathext = (Environment.GetEnvironmentVariable("PATHEXT") ?? ".EXE;.CMD;.BAT;.COM")
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var directory in paths)
        {
            if (Path.HasExtension(executableName))
            {
                var candidate = Path.Combine(directory, executableName);
                if (File.Exists(candidate))
                {
                    resolved = candidate;
                    return true;
                }
                continue;
            }

            foreach (var extension in pathext)
            {
                var candidate = Path.Combine(directory, executableName + extension);
                if (File.Exists(candidate))
                {
                    resolved = candidate;
                    return true;
                }
            }
        }
    }

    resolved = string.Empty;
    return false;
}

static int ComputeDeterministicSeed(string yaml)
{
    var normalized = yaml.Replace("\r\n", "\n").Replace("\r", "\n");
    var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
    return BitConverter.ToInt32(bytes, 0);
}

static long ComputeDeterministicTimestamp(int seed)
{
    const long baseUnixTime = 1700000000;
    const long spanSeconds = 365L * 24 * 60 * 60;
    return baseUnixTime + (uint)seed % spanSeconds;
}

static string CreateInitialScenarioYaml()
{
    return $"""
    # scenario2cast starter scenario. Edit the values below and add commands under steps.
    title: "My App"            # Optional cast title
    # width: 120                 # Default: 120
    # height: 24                 # Default: 24
    # cwd: /your/path            # Optional working directory for all steps
    # shell: bash                # Optional shell override: bash, pwsh, powershell, or a path

    # Optional SVG rendering metadata. Written to the cast header (v3 tags / term.theme) and used by --format svg.
    # render:
    #   font-size: 16
    #   theme:
    #     preset: dark
    #     fg: "#d0d0d0"
    #     bg: "#282c34"

    # Default settings for all steps. Can be overridden per step by using a mapping with "run" and timing keys.
    settings:
      prompt: "{DefaultPrompt}"             # Default prompt shown before each command.
      typing-speed: {DefaultSpeed}       # Seconds per character on average.
      typing-jitter: {DefaultJitter}     # Random typing variance (+/- seconds).
      pre-delay: {DefaultPreDelay}           # Pause before typing each step.
      post-delay: {DefaultPostDelay}          # Pause after output before the next step.
      execution-duration: {DefaultExecutionDuration} # Optional cast wait after command execution.
      stderr-color: {DefaultStderrColorSpec}       # Applied only when stderr has no ANSI SGR.

    # Optional setup commands. They run before steps, but are not recorded in the cast.
    # pre:
    #   - echo "run before steps"

    # Add one command per step. Use a mapping when you want to override per-command timing.
    steps:
      - echo "Hello, World!"
      - run: pwd
        post-delay: 1.5
      - name: "[cyan]Styled command typing + output highlight"
        run: printf 'line1 alpha\nline2 beta gamma\nline3\nline4 delta\n'
        run-highlight: "bold fg:bright-cyan"
        highlight:
          - at: "1"
            color: "fg:bright-green"
          - at: "2:7-10"
            color: "underline fg:yellow"
          - at: "2:12-"
            color: "bold fg:magenta"
          - at: "3"
            color: "fg:white bg:212"
          - at: "4"
            color: "fg:black bg:114"

      - name: "[bright-yellow]stderr color override"
        run: echo "stderr sample" 1>&2
        stderr-color: "bold fg:red"

    # Optional teardown commands. They run after the cast file is written, but are not recorded in the cast.
    # post:
    #   - echo "run after steps"
    """;
}

static void PrintUsage()
{
    Console.Error.WriteLine("Usage: scenario2cast [--verbose] [--format cast|svg] [--font-size N] [--theme dark|light] <scenario.yaml> [output]");
    Console.Error.WriteLine("       scenario2cast svg [--font-size N] [--theme dark|light] <input.cast> [output.svg]");
    Console.Error.WriteLine("       scenario2cast init [scenario.yaml]");
    Console.Error.WriteLine("       scenario2cast --help");
}

static void PrintInitUsage()
{
    Console.Error.WriteLine("Usage: scenario2cast init [scenario.yaml]");
    Console.Error.WriteLine("Creates a commented starter YAML scenario file.");
}

static void PrintSvgUsage()
{
    Console.Error.WriteLine("Usage: scenario2cast svg [--font-size N] [--theme dark|light] <input.cast> [output.svg]");
    Console.Error.WriteLine("Converts an existing asciinema v2/v3 cast file to animated SVG.");
}

static void PrintVersion()
{
    Console.WriteLine(AppVersion);
}

enum CastEventKind
{
    Output,
    Resize,
    Marker,
}

readonly record struct CastEvent(
    double Time,
    CastEventKind Kind,
    string Data,
    int ResizeWidth = 0,
    int ResizeHeight = 0)
{
    public static CastEvent Output(double time, string data) => new(time, CastEventKind.Output, data);

    public static CastEvent Resize(double time, int width, int height) =>
        new(time, CastEventKind.Resize, $"{width}x{height}", width, height);

    public static CastEvent Marker(double time, string label) => new(time, CastEventKind.Marker, label);
}

enum OutputFormat
{
    Cast,
    Svg,
}

readonly record struct CommandOutput(string Stdout, string Stderr, int ExitCode);

record HighlightSpec(string ColorOpen, List<string> At);

record ShellLaunch(string FileName, string[] Arguments, string EnvValue, string DisplayName);

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
    public string? StderrColor { get; set; }
}

[YamlObject(NamingConvention.KebabCase)]
public partial class ScenarioRender
{
    public int? FontSize { get; set; }
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
