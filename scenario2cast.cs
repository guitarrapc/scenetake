#:sdk Microsoft.NET.Sdk
#:property TargetFramework=net10.0
#:property Nullable=enable
#:property ImplicitUsings=enable
#:package YamlDotNet@16.3.0

// scenario2cast - Generate asciinema v2 cast files from YAML scenario files.
//
// Usage:
//   dotnet run scenario2cast.cs <scenario.yaml> [output.cast]
//
// If output.cast is omitted, writes to <scenario>.cast in the same directory.

using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

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

Console.Error.WriteLine("Generating cast...");
var events = Generate(scenario);

WriteCast(scenario, events, outputPath);

var duration = events.Count > 0 ? events[^1].Time : 0.0;
Console.Error.WriteLine($"Done: {outputPath}  ({events.Count} events, {duration:F1}s)");
return 0;

static Scenario ParseScenario(string yaml)
{
    var d = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();
    return d.Deserialize<Scenario>(yaml) ?? new Scenario();
}

static List<CastEvent> Generate(Scenario scenario)
{
    var s = scenario.Settings ?? new();
    var prompt    = AsString(s, "prompt", DefaultPrompt);
    var speed     = AsDouble(s, "typing_speed", DefaultSpeed);
    var jitter    = AsDouble(s, "typing_jitter", DefaultJitter);
    var preDelay  = AsDouble(s, "pre_command_delay", DefaultPreDelay);
    var postDelay = AsDouble(s, "post_command_delay", DefaultPostDelay);
    var events = new List<CastEvent>();
    var rng    = new Random();
    double t   = 0.5;

    events.Add(new CastEvent(t, prompt));
    t += preDelay;

    foreach (var item in scenario.Commands ?? new())
    {
        var command = ParseCommand(item);
        if (string.IsNullOrWhiteSpace(command.Cmd)) continue;

        var cmdSpeed  = AsDouble(command.Extra, "typing_speed", speed);
        var cmdJitter = AsDouble(command.Extra, "typing_jitter", jitter);
        var cmdPre    = AsDouble(command.Extra, "pre_delay", preDelay);
        var cmdPost   = AsDouble(command.Extra, "post_delay", postDelay);

        foreach (var ch in command.Cmd)
        {
            events.Add(new CastEvent(Math.Round(t, 6), ch.ToString()));
            var delay = cmdSpeed + rng.NextDouble() * 2 * cmdJitter - cmdJitter;
            t += Math.Max(delay, 0.005);
        }

        events.Add(new CastEvent(Math.Round(t, 6), "\r\n"));
        t += 0.15;

        Console.Error.WriteLine($"  running: {command.Cmd}");
        var output = RunCommand(command.Cmd, scenario.Cwd);
        if (!string.IsNullOrEmpty(output))
        {
            events.Add(new CastEvent(Math.Round(t, 6), NormalizeNewlines(output)));
            t += Math.Min(0.004 * output.Length, 2.0);
        }

        t += cmdPost;
        events.Add(new CastEvent(Math.Round(t, 6), prompt));
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
        var cmd = extra.TryGetValue("cmd", out var v) ? v?.ToString() ?? "" : "";
        extra.Remove("cmd");
        return new CommandEntry { Cmd = cmd, Extra = extra };
    }
    return new CommandEntry();
}

static string RunCommand(string cmd, string? cwd)
{
    var (shell, flag) = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
        ? (Environment.GetEnvironmentVariable("COMSPEC") ?? "cmd.exe", "/c")
        : (Environment.GetEnvironmentVariable("SHELL")   ?? "/bin/bash", "-c");

    var psi = new ProcessStartInfo(shell)
    {
        RedirectStandardOutput = true,
        RedirectStandardError  = true,
        UseShellExecute        = false,
        CreateNoWindow         = true,
    };
    psi.ArgumentList.Add(flag);
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
    => s.Replace("\r\n", "\n").Replace("\n", "\r\n");

static string DefaultShell()
    => RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
        ? Environment.GetEnvironmentVariable("COMSPEC") ?? "cmd.exe"
        : Environment.GetEnvironmentVariable("SHELL")   ?? "/bin/bash";

static void WriteCast(Scenario scenario, List<CastEvent> events, string outputPath)
{
    using var writer = new StreamWriter(outputPath, append: false, encoding: new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    writer.NewLine = "\n";

    var width = scenario.Width ?? 120;
    var height = scenario.Height ?? 24;
    var title = scenario.Title ?? "";
    var shell = DefaultShell();
    writer.WriteLine(
        $"{{\"version\":2,\"width\":{width},\"height\":{height}" +
        $",\"timestamp\":{DateTimeOffset.UtcNow.ToUnixTimeSeconds()},\"title\":{JsonString(title)}" +
        $",\"env\":{{\"SHELL\":{JsonString(shell)},\"TERM\":\"xterm-256color\"}}}}");

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
    => d.TryGetValue(key, out var v) &&
       double.TryParse(v?.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var r) ? r : def;

record CastEvent(double Time, string Data);

class Scenario
{
    public string? Title { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public string? Cwd { get; set; }
    public Dictionary<string, object>? Settings { get; set; }
    public List<object>? Commands { get; set; }
}

class CommandEntry
{
    public string Cmd { get; set; } = "";
    public Dictionary<string, object> Extra { get; set; } = new();
}
