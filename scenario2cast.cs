#:sdk Microsoft.NET.Sdk
#:property TargetFramework=net10.0
#:property Version=0.1.0
#:property Nullable=enable
#:property ImplicitUsings=enable
#:package VYaml@1.3.0

// scenario2cast - Generate asciinema v2 cast files from YAML scenario files.
//
// Usage:
//   dotnet run scenario2cast.cs <scenario.yaml> [output.cast]
//
// If output.cast is omitted, writes to <scenario>.cast in the same directory.

using System.Diagnostics;
using System.Globalization;
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
const string AppVersion = "0.1.0";
const string SgrReset = "\u001b[0m";

if (args.Length < 1)
{
    PrintUsage();
    return 1;
}

if (args[0] is "init")
{
    if (args.Length >= 2 && args[1] is "-h" or "--help")
    {
        PrintInitUsage();
        return 0;
    }

    var initPath = args.Length >= 2 && !args[1].StartsWith('-')
        ? Path.GetFullPath(args[1])
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

var scenarioPath = Path.GetFullPath(args[0]);
if (!File.Exists(scenarioPath))
{
    Console.Error.WriteLine($"Error: {scenarioPath} not found");
    return 1;
}

var outputPath = args.Length >= 2
    ? Path.GetFullPath(args[1])
    : Path.ChangeExtension(scenarioPath, ".cast");

Console.Error.WriteLine($"Loading: {scenarioPath}");
var yaml = File.ReadAllText(scenarioPath, Encoding.UTF8);
// Register VYaml formatters explicitly for NativeAOT (source generator cannot call __Register via reflection)
Scenario.__RegisterVYamlFormatter();
ScenarioSettings.__RegisterVYamlFormatter();
var scenario = ParseScenario(yaml);
var deterministicSeed = ComputeDeterministicSeed(yaml);
var deterministicTimestamp = ComputeDeterministicTimestamp(deterministicSeed);

var shell = ResolveShell(scenario);
Console.Error.WriteLine($"Using shell: {shell.DisplayName}");

Console.Error.WriteLine("Generating cast...");
var events = Generate(scenario, shell, deterministicSeed);

WriteCast(scenario, events, outputPath, shell, deterministicTimestamp);

var duration = events.Count > 0 ? events[^1].Time : 0.0;
Console.Error.WriteLine($"Done: {outputPath}  ({events.Count} events, {duration:F1}s)");
return 0;

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
    var defaultStderrColorIndex = ParseSettingsStderrColor(settings.StderrColor);
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
        var cmdStderrColorIndex = ResolveStderrColor(command.Extra, defaultStderrColorIndex, command.Cmd);
        var hasRunHighlight = TryGetRunHighlight(command.Extra, command.Cmd, out var runHighlightColor);

        if (events.Count == 0)
            t += preDelay;

        if (TryFormatNameComment(command.Name, command.Cmd, out var nameLine))
        {
            var prefix = NameCommentPrefix(events.Count > 0 ? events[^1].Data : null);
            events.Add(new CastEvent(Math.Round(t, 6), prefix + nameLine));
            t += 0.05;
        }

        events.Add(new CastEvent(Math.Round(t, 6), prompt));
        t += 0.05;

        if (hasRunHighlight)
            events.Add(new CastEvent(Math.Round(t, 6), SgrOpen(runHighlightColor)));

        foreach (var ch in command.Cmd)
        {
            events.Add(new CastEvent(Math.Round(t, 6), ch.ToString()));
            var delay = cmdSpeed + rng.NextDouble() * 2 * cmdJitter - cmdJitter;
            t += Math.Max(delay, 0.005);
        }

        if (hasRunHighlight)
            events.Add(new CastEvent(Math.Round(t, 6), SgrReset));

        events.Add(new CastEvent(Math.Round(t, 6), "\r\n"));
        t += 0.15;

        Console.Error.WriteLine($"  running: {command.Cmd}");
        var execution = RunCommand(command.Cmd, scenario.Cwd, shell);
        t += cmdExecutionDuration;
        var mergedOutput = MergeCommandOutput(execution, cmdStderrColorIndex);

        if (!string.IsNullOrEmpty(mergedOutput))
        {
            var output = GetHighlights(command.Extra, command.Cmd) is { } highlights
                ? ApplyHighlights(mergedOutput, highlights, command.Cmd)
                : mergedOutput;
            events.Add(new CastEvent(Math.Round(t, 6), NormalizeNewlines(output)));
        }

        t += cmdPost;
        t += cmdPre;
    }

    if (events.Count > 0)
        events.Add(new CastEvent(Math.Round(t, 6), prompt));

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
        var extra = map.ToDictionary(kv => kv.Key.ToString() ?? "", kv => kv.Value);
        var cmd = extra.TryGetValue("run", out var runValue) ? runValue?.ToString() ?? "" : "";
        var name = extra.TryGetValue("name", out var nameValue) ? nameValue?.ToString() : null;

        extra.Remove("run");
        extra.Remove("name");
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
    return new CommandOutput(stdout, stderr);
}

static string MergeCommandOutput(CommandOutput output, byte stderrColorIndex)
{
    if (string.IsNullOrEmpty(output.Stdout))
        return MaybeColorizeStderr(output.Stderr, stderrColorIndex);

    if (string.IsNullOrEmpty(output.Stderr))
        return output.Stdout;

    if (stderrColorIndex == 0 || ContainsAnsiSgr(output.Stderr))
        return string.Concat(output.Stdout, output.Stderr);

    return string.Concat(output.Stdout, SgrOpen(stderrColorIndex), output.Stderr, SgrReset);
}

static string MaybeColorizeStderr(string stderr, byte stderrColorIndex)
{
    if (string.IsNullOrEmpty(stderr) || stderrColorIndex == 0 || ContainsAnsiSgr(stderr))
        return stderr;

    return string.Concat(SgrOpen(stderrColorIndex), stderr, SgrReset);
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

// Step name comment

static bool TryFormatNameComment(string? raw, string cmd, out string coloredLine)
{
    coloredLine = "";
    if (string.IsNullOrWhiteSpace(raw))
        return false;

    var value = raw.Trim();
    var colorIndex = (byte)7; // cyan default
    string displayText;

    if (value.StartsWith('['))
    {
        var close = value.IndexOf(']');
        if (close > 1)
        {
            var colorName = value[1..close];
            displayText = value[(close + 1)..].TrimStart();
            if (!TryColorIndex(colorName, out colorIndex))
            {
                WarnName(cmd, $"unknown color '{colorName}'");
                colorIndex = 7;
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
        WarnName(cmd, "empty name text after color prefix");
        return false;
    }

    coloredLine = $"{SgrOpen(colorIndex)}# {displayText}{SgrReset}\r\n";
    return true;
}

static void WarnName(string cmd, string detail)
    => Console.Error.WriteLine($"Warning: name ({cmd}): {detail}");

static byte ParseSettingsStderrColor(string? raw)
{
    if (string.IsNullOrWhiteSpace(raw))
        return 0;

    if (TryColorIndex(raw, out var colorIndex))
        return colorIndex;

    Console.Error.WriteLine($"Warning: settings.stderr-color: unknown color '{raw}'");
    return 0;
}

static byte ResolveStderrColor(Dictionary<string, object?> extra, byte fallbackColorIndex, string cmd)
{
    if (!extra.TryGetValue("stderr-color", out var raw))
        return fallbackColorIndex;

    var value = raw?.ToString();
    if (string.IsNullOrWhiteSpace(value))
        return 0;

    if (TryColorIndex(value, out var colorIndex))
        return colorIndex;

    WarnStderrColor(cmd, $"unknown color '{value}'");
    return fallbackColorIndex;
}

static void WarnStderrColor(string cmd, string detail)
    => Console.Error.WriteLine($"Warning: stderr-color ({cmd}): {detail}");

static bool TryGetRunHighlight(Dictionary<string, object?> extra, string cmd, out byte colorIndex)
{
    colorIndex = 0;
    if (!extra.TryGetValue("run-highlight", out var raw))
        return false;

    var value = raw?.ToString();
    if (!TryColorIndex(value, out colorIndex))
    {
        WarnRunHighlight(cmd, $"unknown color '{value}'");
        return false;
    }

    return true;
}

static void WarnRunHighlight(string cmd, string detail)
    => Console.Error.WriteLine($"Warning: run-highlight ({cmd}): {detail}");

// Highlight parsing and application

static string SgrOpen(byte index) => index switch
{
    1 => "\u001b[30m",  2 => "\u001b[31m",  3 => "\u001b[32m",  4 => "\u001b[33m",
    5 => "\u001b[34m",  6 => "\u001b[35m",  7 => "\u001b[36m",  8 => "\u001b[37m",
    9 => "\u001b[90m", 10 => "\u001b[91m", 11 => "\u001b[92m", 12 => "\u001b[93m",
   13 => "\u001b[94m", 14 => "\u001b[95m", 15 => "\u001b[96m", 16 => "\u001b[97m",
    _ => "",
};

static List<HighlightSpec>? GetHighlights(Dictionary<string, object?> extra, string cmd)
{
    if (!extra.TryGetValue("highlight", out var raw) || raw is not List<object?> list || list.Count == 0)
        return null;

    var specs = new List<HighlightSpec>(list.Count);
    foreach (var item in list)
    {
        if (item is not Dictionary<object, object?> map) continue;
        var colorName = map.GetValueOrDefault("color")?.ToString();
        if (!TryColorIndex(colorName, out var colorIndex))
        {
            WarnHighlight(cmd, $"unknown color '{colorName}'");
            continue;
        }

        if (!map.TryGetValue("at", out var atRaw) || atRaw is null)
        {
            WarnHighlight(cmd, "missing 'at'");
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
            WarnHighlight(cmd, "missing 'at'");
            continue;
        }

        specs.Add(new HighlightSpec(colorIndex, ats));
    }

    return specs.Count > 0 ? specs : null;
}

static bool TryColorIndex(string? name, out byte index)
{
    index = name?.Trim().ToLowerInvariant() switch
    {
        "black" => 1,
        "red" => 2,
        "green" => 3,
        "yellow" => 4,
        "blue" => 5,
        "magenta" => 6,
        "cyan" => 7,
        "white" => 8,
        "bright-black" or "gray" or "grey" => 9,
        "bright-red" => 10,
        "bright-green" => 11,
        "bright-yellow" => 12,
        "bright-blue" => 13,
        "bright-magenta" => 14,
        "bright-cyan" => 15,
        "bright-white" => 16,
        _ => (byte)0,
    };
    return index != 0;
}

static string ApplyHighlights(string output, List<HighlightSpec> specs, string cmd)
{
    var text = output.Replace("\r\n", "\n").Replace("\r", "\n");
    var lines = text.Split('\n');
    var paint = new byte[lines.Length][];

    foreach (var spec in specs)
    {
        foreach (var at in spec.At)
            ApplyAt(lines, paint, at, spec.ColorIndex, cmd);
    }

    var sb = new StringBuilder(text.Length + specs.Count * 16);
    for (var i = 0; i < lines.Length; i++)
    {
        if (i > 0) sb.Append('\n');
        AppendPaintedLine(sb, lines[i], paint[i]);
    }
    return sb.ToString();
}

static void ApplyAt(string[] lines, byte[][] paint, string at, byte colorIndex, string cmd)
{
    if (!TryParseAt(at, out var lineLo, out var lineHi, out var colLo, out var colHi, out var openColEnd, out var hasCol))
    {
        WarnHighlight(cmd, $"invalid at '{at}'");
        return;
    }

    var lineCount = lines.Length;
    if (lineLo > lineCount)
    {
        WarnHighlight(cmd, $"at '{at}' starts after output");
        return;
    }

    if (lineHi > lineCount)
    {
        WarnHighlight(cmd, $"at '{at}': lines {lineCount + 1}-{lineHi} are missing");
        lineHi = lineCount;
    }

    for (var line = lineLo; line <= lineHi; line++)
    {
        var li = line - 1;
        var content = lines[li];
        int start, end;
        if (hasCol)
        {
            start = colLo - 1;
            end = openColEnd ? content.Length : colHi;
            if (start >= content.Length)
            {
                if (lineLo == lineHi)
                    WarnHighlight(cmd, $"at '{at}' starts after output");
                continue;
            }
            if (end > content.Length) end = content.Length;
        }
        else
        {
            start = 0;
            end = content.Length;
        }

        if (start >= end) continue;
        var row = paint[li] ??= new byte[content.Length];
        for (var c = start; c < end; c++)
            row[c] = colorIndex;
    }
}

static void AppendPaintedLine(StringBuilder sb, string line, byte[]? row)
{
    if (row is null)
    {
        sb.Append(line);
        return;
    }

    byte cur = 0;
    for (var i = 0; i < line.Length; i++)
    {
        var next = row[i];
        if (next != cur)
        {
            if (cur != 0) sb.Append(SgrReset);
            if (next != 0) sb.Append(SgrOpen(next));
            cur = next;
        }
        sb.Append(line[i]);
    }

    if (cur != 0) sb.Append(SgrReset);
}

static bool TryParseAt(string at, out int lineLo, out int lineHi, out int colLo, out int colHi, out bool openColEnd, out bool hasCol)
{
    lineLo = lineHi = colLo = colHi = 0;
    openColEnd = hasCol = false;
    var span = at.AsSpan().Trim();
    if (span.IsEmpty) return false;

    var colon = span.IndexOf(':');
    if (!TryParseSpan(span[..(colon >= 0 ? colon : span.Length)], out lineLo, out lineHi))
        return false;

    if (colon < 0) return true;
    hasCol = true;
    return TryParseColSpan(span[(colon + 1)..], out colLo, out colHi, out openColEnd);
}

static bool TryParseSpan(ReadOnlySpan<char> span, out int lo, out int hi)
{
    lo = hi = 0;
    var dash = span.IndexOf('-');
    if (dash < 0)
        return TryPosInt(span, out lo) && (hi = lo) > 0;

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

    return TryParseSpan(span, out lo, out hi);
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

static void WarnHighlight(string cmd, string detail)
    => Console.Error.WriteLine($"Warning: highlight ({cmd}): {detail}");

// Cast writing

static void WriteCast(Scenario scenario, List<CastEvent> events, string outputPath, ShellLaunch shell, long timestamp)
{
    using var writer = new StreamWriter(outputPath, append: false, encoding: new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    writer.NewLine = "\n";

    var width = scenario.Width ?? 120;
    var height = scenario.Height ?? 24;
    var title = scenario.Title ?? "";
    writer.WriteLine(
        $"{{\"version\":2,\"width\":{width},\"height\":{height}" +
        $",\"timestamp\":{timestamp},\"title\":{JsonString(title)}" +
        $",\"env\":{{\"SHELL\":{JsonString(shell.EnvValue)},\"TERM\":\"xterm-256color\"}}}}");

    foreach (var ev in events)
        writer.WriteLine($"[{ev.Time.ToString("0.######", CultureInfo.InvariantCulture)},\"o\",{JsonString(ev.Data)}]");
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
        if (TryResolveWindowsPowerShell("pwsh", out var pwsh))
            return new ShellLaunch(pwsh, ["-NoLogo", "-NoProfile", "-Command"], pwsh, "pwsh");

        if (TryResolveWindowsPowerShell("powershell", out var powershell))
            return new ShellLaunch(powershell, ["-NoLogo", "-NoProfile", "-Command"], powershell, "powershell");

        throw new InvalidOperationException("No supported shell was found on Windows. Install pwsh or powershell, or set settings.shell to a Git Bash or MSYS bash.exe path.");
    }

    if (IsPowerShellName(requested))
    {
        var exeName = NormalizeWindowsShellName(requested);
        if (TryResolveWindowsPowerShell(exeName, out var resolved))
            return new ShellLaunch(resolved, ["-NoLogo", "-NoProfile", "-Command"], resolved, exeName);

        throw new InvalidOperationException($"Shell '{requested}' was requested, but '{exeName}' could not be found on Windows.");
    }

    if (IsBashName(requested))
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

static bool IsBashName(string shell)
    => string.Equals(NormalizeWindowsShellName(shell), "bash", StringComparison.OrdinalIgnoreCase);

static string NormalizeWindowsShellName(string shell)
    => Path.GetFileNameWithoutExtension(shell.Trim()).ToLowerInvariant();

static bool TryResolveWindowsPowerShell(string executableName, out string resolved)
    => TryResolveExecutableOnWindows(executableName, out resolved);

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
    # width: 80                  # Default: 80
    # height: 24                 # Default: 24
    # cwd: /your/path            # Optional working directory for all steps
    # shell: bash                # Optional shell override: bash, pwsh, powershell, or a path

    # Default settings for all steps. Can be overridden per step by using a mapping with "run" and timing keys.
    # settings:
    #   prompt: "{DefaultPrompt}"             # Default prompt shown before each command. Default: "{DefaultPrompt}"
    #   typing-speed: {DefaultSpeed}       # Seconds per character on average. Default: {DefaultSpeed}
    #   typing-jitter: {DefaultJitter}     # Random typing variance (+/- seconds). Default: {DefaultJitter}
    #   pre-delay: {DefaultPreDelay}           # Pause before typing each step. Default: {DefaultPreDelay}
    #   post-delay: {DefaultPostDelay}          # Pause after output before the next step. Default: {DefaultPostDelay}
    #   execution-duration: {DefaultExecutionDuration} # Optional cast wait after command execution. Default: {DefaultExecutionDuration}
    #   stderr-color: red             # Optional default stderr color when stderr has no ANSI SGR. Default: off

    # Add one command per step. Use a mapping when you want to override per-command timing.
    steps:
      - echo "Hello, World!"
      - run: pwd
        post-delay: 1.5
    """;
}

static void PrintUsage()
{
    Console.Error.WriteLine("Usage: scenario2cast <scenario.yaml> [output.cast]");
    Console.Error.WriteLine("       scenario2cast init [scenario.yaml]");
    Console.Error.WriteLine("       scenario2cast --help");
}

static void PrintInitUsage()
{
    Console.Error.WriteLine("Usage: scenario2cast init [scenario.yaml]");
    Console.Error.WriteLine("Creates a commented starter YAML scenario file.");
}

static void PrintVersion()
{
    Console.WriteLine(AppVersion);
}

record CastEvent(double Time, string Data);

readonly record struct CommandOutput(string Stdout, string Stderr);

record HighlightSpec(byte ColorIndex, List<string> At);

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
    public List<object?>? Steps { get; set; }
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

public class CommandEntry
{
    public string Cmd { get; set; } = "";
    public string? Name { get; set; }
    public Dictionary<string, object?> Extra { get; set; } = new();
}
