#:sdk Microsoft.NET.Sdk
#:property TargetFramework=net10.0
#:property Nullable=enable
#:property ImplicitUsings=enable
#:package YamlDotNet@18.0.0
#:package Vecc.YamlDotNet.Analyzers.StaticGenerator@18.0.0

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
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using Scenario2Cast;

const string DefaultPrompt    = "$ ";
const double DefaultSpeed     = 0.05;
const double DefaultJitter    = 0.015;
const double DefaultPreDelay  = 0.8;
const double DefaultPostDelay = 1.5;

if (args.Length < 1)
{
    Console.Error.WriteLine("Usage: scenario2cast <scenario.yaml> [output.cast]");
    return 1;
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
    var d = new StaticDeserializerBuilder(new Scenario2Cast.StaticContext())
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();
    return d.Deserialize<Scenario>(yaml) ?? new Scenario();
}

static List<CastEvent> Generate(Scenario scenario, ShellLaunch shell, int deterministicSeed)
{
    var s = scenario.Settings ?? new();
    var prompt    = AsString(s, "prompt", DefaultPrompt);
    var speed     = AsDouble(s, "typing-speed", DefaultSpeed);
    var jitter    = AsDouble(s, "typing-jitter", DefaultJitter);
    var preDelay  = AsDouble(s, "pre-command-delay", DefaultPreDelay);
    var postDelay = AsDouble(s, "post-command-delay", DefaultPostDelay);
    var events = new List<CastEvent>();
    var rng    = new Random(deterministicSeed);
    double t   = 0.5;

    events.Add(new CastEvent(t, prompt));
    t += preDelay;

    foreach (var item in scenario.Steps ?? new())
    {
        var command = ParseCommand(item);
        if (string.IsNullOrWhiteSpace(command.Cmd)) continue;

        var cmdSpeed  = AsDouble(command.Extra, "typing-speed", speed);
        var cmdJitter = AsDouble(command.Extra, "typing-jitter", jitter);
        var cmdPre    = AsDouble(command.Extra, "pre-delay", preDelay);
        var cmdPost   = AsDouble(command.Extra, "post-delay", postDelay);

        foreach (var ch in command.Cmd)
        {
            events.Add(new CastEvent(Math.Round(t, 6), ch.ToString()));
            var delay = cmdSpeed + rng.NextDouble() * 2 * cmdJitter - cmdJitter;
            t += Math.Max(delay, 0.005);
        }

        events.Add(new CastEvent(Math.Round(t, 6), "\r\n"));
        t += 0.15;

        Console.Error.WriteLine($"  running: {command.Cmd}");
        var output = RunCommand(command.Cmd, scenario.Cwd, shell);
        if (!string.IsNullOrEmpty(output))
        {
            events.Add(new CastEvent(Math.Round(t, 6), NormalizeNewlines(output)));
            t += Math.Min(0.004 * output.Length, 2.0);
        }

        events.Add(new CastEvent(Math.Round(t, 6), prompt));
        t += cmdPost;
        t += cmdPre;
    }

    return events;
}

static CommandEntry ParseCommand(object item)
{
    if (item is string s) return new CommandEntry { Cmd = s };
    if (item is IDictionary<object, object> map)
    {
        var extra = map.ToDictionary(kv => kv.Key.ToString() ?? "", kv => kv.Value);
        var cmd = extra.TryGetValue("run", out var runValue) ? runValue?.ToString() ?? "" : "";

        extra.Remove("run");
        return new CommandEntry { Cmd = cmd, Extra = extra };
    }
    return new CommandEntry();
}

static string RunCommand(string cmd, string? cwd, ShellLaunch shell)
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
    return stdout + stderr;
}

static string NormalizeNewlines(string s)
    => s.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "\r\n");

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

static string AsString(Dictionary<string, object> d, string key, string def)
    => d.TryGetValue(key, out var v) && v is not null ? v.ToString() ?? def : def;

static double AsDouble(Dictionary<string, object> d, string key, double def)
{
    if (d.TryGetValue(key, out var value) &&
        double.TryParse(value?.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
    {
        return parsed;
    }

    return def;
}

static ShellLaunch ResolveShell(Scenario scenario)
{
    var requested = scenario.Shell;
    if (string.IsNullOrWhiteSpace(requested) && scenario.Settings is not null)
        requested = AsString(scenario.Settings, "shell", "");

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

record CastEvent(double Time, string Data);

record ShellLaunch(string FileName, string[] Arguments, string EnvValue, string DisplayName);

namespace Scenario2Cast
{
    [YamlSerializable]
    public class Scenario
    {
        public string? Title { get; set; }
        public int? Width { get; set; }
        public int? Height { get; set; }
        public string? Cwd { get; set; }
        public string? Shell { get; set; }
        public Dictionary<string, object>? Settings { get; set; }
        public List<object>? Steps { get; set; }
    }

    [YamlSerializable]
    public class CommandEntry
    {
        public string Cmd { get; set; } = "";
        public Dictionary<string, object> Extra { get; set; } = new();
    }

    [YamlStaticContext]
    [YamlSerializable(typeof(Scenario))]
    public partial class StaticContext : YamlDotNet.Serialization.StaticContext
    {
    }
}
