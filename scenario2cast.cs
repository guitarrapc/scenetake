#:sdk Microsoft.NET.Sdk
#:property TargetFramework=net10.0
#:property Nullable=enable
#:property ImplicitUsings=enable

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

// ────────────────────────────────────────────────────────────────── defaults ──

const string DefaultPrompt    = "$ ";
const double DefaultSpeed     = 0.05;
const double DefaultJitter    = 0.015;
const double DefaultPreDelay  = 0.8;
const double DefaultPostDelay = 1.5;

// ──────────────────────────────────────────────────────────────────────  main ──

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
var scenario = ParseScenario(File.ReadAllText(scenarioPath, Encoding.UTF8));

Console.Error.WriteLine("Generating cast...");
var (header, events) = Generate(scenario);

WriteCast(header, events, outputPath);

var duration = events.Count > 0 ? events[^1].Time : 0.0;
Console.Error.WriteLine($"Done: {outputPath}  ({events.Count} events, {duration:F1}s)");
return 0;

// ─────────────────────────────────────────────────────────────── YAML parser ──

static Scenario ParseScenario(string text)
{
    var scenario = new Scenario();
    var lines = text.Replace("\r\n", "\n").Split('\n').ToList();
    int i = 0;
    while (i < lines.Count)
    {
        var line = lines[i];
        if (IsEmptyOrComment(line)) { i++; continue; }
        if (GetIndent(line) > 0)   { i++; continue; }

        var (key, rest) = SplitKeyValue(line);
        switch (key)
        {
            case "title":    scenario.Title  = Unquote(rest); i++; break;
            case "width":    if (int.TryParse(rest, out var w)) scenario.Width  = w; i++; break;
            case "height":   if (int.TryParse(rest, out var h)) scenario.Height = h; i++; break;
            case "cwd":      scenario.Cwd = string.IsNullOrEmpty(rest) ? null : rest; i++; break;
            case "settings": i++; i = ParseSettings(lines, i, scenario.Settings); break;
            case "commands": i++; i = ParseCommands(lines, i, scenario.Commands); break;
            default:         i++; break;
        }
    }
    return scenario;
}

static int ParseSettings(List<string> lines, int start, Dictionary<string, string> settings)
{
    int i = start;
    while (i < lines.Count)
    {
        var line = lines[i];
        if (IsEmptyOrComment(line)) { i++; continue; }
        if (GetIndent(line) == 0) break;
        var (key, val) = SplitKeyValue(line);
        if (!string.IsNullOrEmpty(key)) settings[key] = val;
        i++;
    }
    return i;
}

static int ParseCommands(List<string> lines, int start, List<CommandEntry> commands)
{
    int i = start;
    while (i < lines.Count)
    {
        var line = lines[i];
        if (IsEmptyOrComment(line)) { i++; continue; }
        if (GetIndent(line) == 0) break;

        var trimmed = line.TrimStart();
        if (!trimmed.StartsWith("- ")) { i++; continue; }

        var itemIndent = GetIndent(line);
        var afterDash  = trimmed[2..].Trim();

        if (afterDash.StartsWith("cmd:"))
        {
            // mapping item starting with cmd:
            var entry = new CommandEntry { Cmd = Unquote(afterDash[4..].Trim()) };
            i++;
            i = ParseCommandMapping(lines, i, itemIndent, entry);
            if (!string.IsNullOrEmpty(entry.Cmd)) commands.Add(entry);
        }
        else
        {
            // plain string command
            var cmd = Unquote(afterDash);
            if (!string.IsNullOrEmpty(cmd)) commands.Add(new CommandEntry { Cmd = cmd });
            i++;
        }
    }
    return i;
}

static int ParseCommandMapping(List<string> lines, int start, int itemIndent, CommandEntry entry)
{
    int i = start;
    while (i < lines.Count)
    {
        var line = lines[i];
        if (IsEmptyOrComment(line)) { i++; continue; }
        if (GetIndent(line) <= itemIndent) break;
        var (key, val) = SplitKeyValue(line);
        if (!string.IsNullOrEmpty(key)) entry.Extra[key] = val;
        i++;
    }
    return i;
}

// ─────────────────────────────────────────────────────────────── YAML utils ──

static bool IsEmptyOrComment(string line)
    => string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith('#');

static int GetIndent(string line)
    => line.Length - line.TrimStart().Length;

static (string key, string value) SplitKeyValue(string line)
{
    var idx = line.IndexOf(':');
    if (idx < 0) return (line.Trim(), "");
    // strip inline comment from value
    var val = line[(idx + 1)..];
    var commentIdx = val.IndexOf(" #");
    if (commentIdx >= 0) val = val[..commentIdx];
    return (line[..idx].Trim(), val.Trim());
}

static string Unquote(string s)
{
    s = s.Trim();
    if (s.Length >= 2 &&
        ((s[0] == '"'  && s[^1] == '"') ||
         (s[0] == '\'' && s[^1] == '\'')))
        return s[1..^1];
    return s;
}

// ──────────────────────────────────────────────────────────────────── generate ──

static (CastHeader header, List<CastEvent> events) Generate(Scenario scenario)
{
    var s = scenario.Settings;
    var prompt    = GetStrSetting(s, "prompt",            DefaultPrompt);
    var speed     = GetDbl(s, "typing_speed",             DefaultSpeed);
    var jitter    = GetDbl(s, "typing_jitter",            DefaultJitter);
    var preDelay  = GetDbl(s, "pre_command_delay",        DefaultPreDelay);
    var postDelay = GetDbl(s, "post_command_delay",       DefaultPostDelay);

    var header = new CastHeader
    {
        Version   = 2,
        Width     = scenario.Width,
        Height    = scenario.Height,
        Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        Title     = scenario.Title,
        Env       = new CastEnv { Shell = DefaultShell() },
    };

    var events = new List<CastEvent>();
    var rng    = new Random();
    double t   = 0.5;

    // initial prompt
    events.Add(new CastEvent(t, prompt));
    t += preDelay;

    foreach (var item in scenario.Commands)
    {
        var cmdSpeed   = GetDbl(item.Extra, "typing_speed",  speed);
        var cmdJitter  = GetDbl(item.Extra, "typing_jitter", jitter);
        var cmdPre     = GetDbl(item.Extra, "pre_delay",     preDelay);
        var cmdPost    = GetDbl(item.Extra, "post_delay",    postDelay);

        // simulate typing
        foreach (var ch in item.Cmd)
        {
            events.Add(new CastEvent(Math.Round(t, 6), ch.ToString()));
            var delay = cmdSpeed + rng.NextDouble() * 2 * cmdJitter - cmdJitter;
            t += Math.Max(delay, 0.005);
        }

        // Enter key
        events.Add(new CastEvent(Math.Round(t, 6), "\r\n"));
        t += 0.15;

        // run command and capture output
        Console.Error.WriteLine($"  running: {item.Cmd}");
        var output = RunCommand(item.Cmd, scenario.Cwd);
        if (!string.IsNullOrEmpty(output))
        {
            events.Add(new CastEvent(Math.Round(t, 6), NormalizeNewlines(output)));
            t += Math.Min(0.004 * output.Length, 2.0);
        }

        t += cmdPost;

        // next prompt
        events.Add(new CastEvent(Math.Round(t, 6), prompt));
        t += cmdPre;
    }

    return (header, events);
}

// ───────────────────────────────────────────────────────── command execution ──

static string RunCommand(string cmd, string? cwd)
{
    var (shell, flag) = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
        ? (Environment.GetEnvironmentVariable("COMSPEC") ?? "cmd.exe", "/c")
        : (Environment.GetEnvironmentVariable("SHELL")   ?? "/bin/bash", "-c");

    using var proc = new Process
    {
        StartInfo = new ProcessStartInfo
        {
            FileName               = shell,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
            CreateNoWindow         = true,
            WorkingDirectory       = cwd ?? "",
        }
    };
    proc.StartInfo.ArgumentList.Add(flag);
    proc.StartInfo.ArgumentList.Add(cmd);

    var sb = new StringBuilder();
    proc.OutputDataReceived += (_, e) => { if (e.Data is not null) sb.AppendLine(e.Data); };
    proc.ErrorDataReceived  += (_, e) => { if (e.Data is not null) sb.AppendLine(e.Data); };
    proc.Start();
    proc.BeginOutputReadLine();
    proc.BeginErrorReadLine();
    proc.WaitForExit();

    return sb.ToString();
}

static string NormalizeNewlines(string s)
    => s.Replace("\r\n", "\n").Replace("\n", "\r\n");

static string DefaultShell()
    => RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
        ? Environment.GetEnvironmentVariable("COMSPEC") ?? "cmd.exe"
        : Environment.GetEnvironmentVariable("SHELL")   ?? "/bin/bash";

// ──────────────────────────────────────────────────────────────────── output ──

static void WriteCast(CastHeader header, List<CastEvent> events, string outputPath)
{
    using var writer = new StreamWriter(outputPath, append: false, encoding: new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    writer.NewLine = "\n";

    // Header JSON built manually — no System.Text.Json reflection needed.
    writer.WriteLine(
        $"{{\"version\":{header.Version},\"width\":{header.Width},\"height\":{header.Height}" +
        $",\"timestamp\":{header.Timestamp},\"title\":{JsonString(header.Title)}" +
        $",\"env\":{{\"SHELL\":{JsonString(header.Env.Shell)},\"TERM\":\"xterm-256color\"}}}}");

    foreach (var ev in events)
        writer.WriteLine($"[{ev.Time.ToString("0.######", CultureInfo.InvariantCulture)},\"o\",{JsonString(ev.Data)}]");
}

// Minimal JSON string serializer: escapes control characters and special JSON characters.
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

// ──────────────────────────────────────────────────────────── setting helpers ──

static string GetStrSetting(Dictionary<string, string> d, string key, string def)
    => d.TryGetValue(key, out var v) && !string.IsNullOrEmpty(v) ? Unquote(v) : def;

static double GetDbl(Dictionary<string, string> d, string key, double def)
    => d.TryGetValue(key, out var v) &&
       double.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out var r) ? r : def;

// ───────────────────────────────────────────────────────────────────── types ──

record CastHeader
{
    public int     Version   { get; init; }
    public int     Width     { get; init; }
    public int     Height    { get; init; }
    public long    Timestamp { get; init; }
    public string  Title     { get; init; } = "";
    public CastEnv Env       { get; init; } = new();
}

record CastEnv
{
    public string Shell { get; init; } = "";
    public string Term  { get; init; } = "xterm-256color";
}

record CastEvent(double Time, string Data);

class Scenario
{
    public string                    Title    { get; set; } = "";
    public int                       Width    { get; set; } = 120;
    public int                       Height   { get; set; } = 24;
    public string?                   Cwd      { get; set; }
    public Dictionary<string, string> Settings { get; } = new();
    public List<CommandEntry>         Commands { get; } = new();
}

class CommandEntry
{
    public string                    Cmd   { get; set; } = "";
    public Dictionary<string, string> Extra { get; } = new();
}
