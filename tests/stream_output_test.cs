#:sdk Microsoft.NET.Sdk
#:property TargetFramework=net10.0
#:property Nullable=enable
#:property ImplicitUsings=enable

using System.Diagnostics;
using System.Text.Json;

var repoRoot = FindRepoRoot(Directory.GetCurrentDirectory());
var failures = 0;

failures += Run("IntegrationStreamStagger", () => IntegrationStreamStagger(repoRoot));
failures += Run("IntegrationInstantDefault", () => IntegrationInstantDefault(repoRoot));
failures += Run("IntegrationInstantMergeOrder", () => IntegrationInstantMergeOrder(repoRoot));
failures += Run("IntegrationInstantProgressBar", () => IntegrationInstantProgressBar(repoRoot));
failures += Run("IntegrationDockerBuildStream", () => IntegrationDockerBuildStream(repoRoot));
failures += Run("IntegrationTypingKeepsGraphemes", () => IntegrationTypingKeepsGraphemes(repoRoot));
if (OperatingSystem.IsWindows())
    failures += Run("IntegrationPipeOutputIsUtf8OnWindows", () => IntegrationPipeOutputIsUtf8OnWindows(repoRoot));

return failures == 0 ? 0 : 1;

static bool IntegrationPipeOutputIsUtf8OnWindows(string repoRoot)
{
    // Pipe output used to be decoded in the code page of the terminal that started scenetake, while each
    // child writes either UTF-8 (Git Bash, git) or the code page of its own console (pwsh, cmd). Under code
    // page 932 Git Bash's output was garbled; under 65001 pwsh's was, and pwsh turned an emoji into "?" under
    // both. scenetake starts here in a console of its own, as its children do, so the terminal code page is
    // the OEM code page (437 or 932, not UTF-8). The recording must hold the text exactly.
    if (!IntegrationTestsEnabled())
        return true;

    var ok = true;
    foreach (var (fixture, expected) in new[]
    {
        ("pipe-encoding-pwsh.yaml", new[] { "SCENETAKE_PWSH 絵文字 🎉\r\n", "SCENETAKE_CMD 漢字 ✓\r\n" }),
        // The cast writes Git Bash's "\n" as "\r\n".
        ("pipe-encoding-bash.yaml", new[] { "SCENETAKE_BASH 絵文字 🎉\r\n" }),
    })
    {
        var outputStem = Path.Combine(Path.GetTempPath(), $"scenetake-encoding-{Guid.NewGuid():N}");
        try
        {
            var psi = new ProcessStartInfo(Environment.GetEnvironmentVariable("SCENETAKE_BIN")!, [Path.Combine(repoRoot, "tests", "fixtures", fixture), outputStem])
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var process = Process.Start(psi)!;
            process.WaitForExit();
            var output = process.ExitCode == 0 ? string.Concat(ReadCastTimedOutputEvents(outputStem + ".cast").Select(ev => ev.Text)) : "";
            var missing = expected.Where(text => !output.Contains(text, StringComparison.Ordinal)).ToList();
            if (process.ExitCode != 0 || missing.Count > 0)
            {
                Console.Error.WriteLine($"  {fixture}: exit {process.ExitCode}, missing [{string.Join(" | ", missing)}] in: {output.Replace("\r", "\\r", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal)}");
                ok = false;
            }
        }
        finally
        {
            Cleanup(outputStem);
        }
    }

    return ok;
}

static int Run(string name, Func<bool> test)
{
    try
    {
        if (test())
        {
            Console.Error.WriteLine($"ok {name}");
            return 0;
        }

        Console.Error.WriteLine($"FAIL {name}");
        return 1;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"FAIL {name}: {ex.Message}");
        return 1;
    }
}

static bool IntegrationTestsEnabled()
{
    var env = Environment.GetEnvironmentVariable("SCENETAKE_BIN");
    if (!string.IsNullOrWhiteSpace(env) && File.Exists(env))
        return true;

    Console.Error.WriteLine("skip stream output integration tests: set SCENETAKE_BIN to a published scenetake binary");
    return false;
}

static bool IntegrationStreamStagger(string repoRoot)
{
    if (!IntegrationTestsEnabled())
        return true;

    var fixturePath = Path.Combine(repoRoot, "tests", "fixtures", "stream-output-stagger.yaml");
    var outputStem = Path.Combine(Path.GetTempPath(), $"scenetake-stream-{Guid.NewGuid():N}");
    try
    {
        if (!RunScenetake(fixturePath, outputStem, out var exitCode) || exitCode != 0)
            return false;

        var events = ReadCastTimedOutputEvents(outputStem + ".cast");
        var first = events.Where(ev => ev.Text.Contains("SCENETAKE_STREAM_FIRST", StringComparison.Ordinal)).ToList();
        var second = events.Where(ev => ev.Text.Contains("SCENETAKE_STREAM_SECOND", StringComparison.Ordinal)).ToList();
        if (first.Count == 0 || second.Count == 0)
            return false;

        var gap = second.Min(ev => ev.Time) - first.Max(ev => ev.Time);
        return gap >= 0.3;
    }
    finally
    {
        Cleanup(outputStem);
    }
}

static bool IntegrationInstantDefault(string repoRoot)
{
    if (!IntegrationTestsEnabled())
        return true;

    var fixturePath = Path.Combine(repoRoot, "tests", "fixtures", "stream-output-stagger.yaml");
    var outputStem = Path.Combine(Path.GetTempPath(), $"scenetake-stream-{Guid.NewGuid():N}");
    try
    {
        if (!RunScenetake(fixturePath, outputStem, out var exitCode) || exitCode != 0)
            return false;

        var events = ReadCastTimedOutputEvents(outputStem + ".cast");
        var instantOutputs = events
            .Where(ev => ev.Text.Contains("SCENETAKE_INSTANT_MARKER", StringComparison.Ordinal))
            .Where(ev => ev.Text.Length > "SCENETAKE_INSTANT_MARKER".Length)
            .ToList();
        return instantOutputs.Count == 1;
    }
    finally
    {
        Cleanup(outputStem);
    }
}

static bool IntegrationInstantMergeOrder(string repoRoot)
{
    if (!IntegrationTestsEnabled())
        return true;

    var fixturePath = Path.Combine(repoRoot, "tests", "fixtures", "pipe-instant-merge-order.yaml");
    var outputStem = Path.Combine(Path.GetTempPath(), $"scenetake-merge-{Guid.NewGuid():N}");
    try
    {
        if (!RunScenetake(fixturePath, outputStem, out var exitCode) || exitCode != 0)
            return false;

        var events = ReadCastTimedOutputEvents(outputStem + ".cast");
        var outputEvents = events
            .Where(ev => ev.Text.Contains("SCENETAKE_STDOUT", StringComparison.Ordinal)
                || ev.Text.Contains("SCENETAKE_STDERR_A", StringComparison.Ordinal))
            .ToList();
        if (outputEvents.Count != 1)
            return false;

        var text = outputEvents[0].Text;
        var stdoutIndex = text.IndexOf("SCENETAKE_STDOUT", StringComparison.Ordinal);
        var stderrIndex = text.IndexOf("SCENETAKE_STDERR_A", StringComparison.Ordinal);
        return stdoutIndex >= 0 && stderrIndex >= 0 && stdoutIndex < stderrIndex;
    }
    finally
    {
        Cleanup(outputStem);
    }
}

static bool IntegrationInstantProgressBar(string repoRoot)
{
    if (!IntegrationTestsEnabled())
        return true;

    var fixturePath = Path.Combine(repoRoot, "tests", "fixtures", "pipe-instant-progress.yaml");
    var outputStem = Path.Combine(Path.GetTempPath(), $"scenetake-progress-{Guid.NewGuid():N}");
    try
    {
        if (!RunScenetake(fixturePath, outputStem, out var exitCode) || exitCode != 0)
            return false;

        var events = ReadCastTimedOutputEvents(outputStem + ".cast");
        var progressOutputs = events
            .Where(ev => ev.Text.Contains("SCENETAKE_PROGRESS", StringComparison.Ordinal))
            .ToList();
        return progressOutputs.Count == 1 && progressOutputs[0].Text.Contains("30/30", StringComparison.Ordinal);
    }
    finally
    {
        Cleanup(outputStem);
    }
}

static bool IntegrationDockerBuildStream(string repoRoot)
{
    if (!IntegrationTestsEnabled())
        return true;

    if (!DockerEnabled())
    {
        Console.Error.WriteLine("skip IntegrationDockerBuildStream: docker not available");
        return true;
    }

    const double maxDuration = 12.0;
    var fixturePath = Path.Combine(repoRoot, "tests", "fixtures", "stream-output-docker.yaml");
    var outputStem = Path.Combine(Path.GetTempPath(), $"scenetake-stream-docker-{Guid.NewGuid():N}");
    try
    {
        if (!RunScenetake(fixturePath, outputStem, out var exitCode) || exitCode != 0)
            return false;

        var events = ReadCastTimedOutputEvents(outputStem + ".cast");
        var combined = string.Concat(events.Select(static ev => ev.Text));
        if (!combined.Contains("scenetake-layer-1", StringComparison.Ordinal)
            || !combined.Contains("scenetake-layer-2", StringComparison.Ordinal))
        {
            return false;
        }

        var layerEvents = events
            .Where(ev => ev.Text.Contains("scenetake-layer", StringComparison.Ordinal))
            .ToList();
        if (layerEvents.Count < 2)
            return false;

        var span = layerEvents[^1].Time - layerEvents[0].Time;
        return span > 0.1 && span <= maxDuration + 0.5;
    }
    finally
    {
        Cleanup(outputStem);
    }
}

static bool DockerEnabled()
{
    try
    {
        var psi = new ProcessStartInfo("docker", ["version", "--format", "{{.Server.Version}}"])
        {
            RedirectStandardOutput = false,
            RedirectStandardError = false,
            UseShellExecute = false,
        };
        using var process = Process.Start(psi);
        if (process is null)
            return false;

        process.WaitForExit();
        return process.ExitCode == 0;
    }
    catch
    {
        return false;
    }
}

static bool IntegrationTypingKeepsGraphemes(string repoRoot)
{
    // Typing one UTF-16 code unit at a time split an emoji's surrogate pair into two events, which the
    // cast stores as two U+FFFD. Each user-perceived character must be one typing event.
    if (!IntegrationTestsEnabled())
        return true;

    var fixturePath = Path.Combine(repoRoot, "tests", "fixtures", "typing-graphemes.yaml");
    var outputStem = Path.Combine(Path.GetTempPath(), $"scenetake-graphemes-{Guid.NewGuid():N}");
    try
    {
        if (!RunScenetake(fixturePath, outputStem, out var exitCode) || exitCode != 0)
            return false;

        // The typed command ends at the first "\r\n" event after the prompt; the command's own output
        // comes later and is not checked here.
        var typed = ReadCastTimedOutputEvents(outputStem + ".cast")
            .Select(ev => ev.Text)
            .SkipWhile(text => text != "$ ")
            .Skip(1)
            .TakeWhile(text => text != "\r\n")
            .ToList();
        // The whole command comes back unchanged (no U+FFFD), and each character is a single event.
        string[] expected = ["🎉", "👍🏽", "👨‍👩‍👧", "か゚"];
        var ok = string.Concat(typed) == "echo \"🎉 👍🏽 👨‍👩‍👧 か゚\"" && expected.All(typed.Contains);
        if (!ok)
            Console.Error.WriteLine($"  typed events: {string.Join(" | ", typed)}");
        return ok;
    }
    finally
    {
        Cleanup(outputStem);
    }
}

static bool RunScenetake(string fixturePath, string outputStem, out int exitCode)
{
    exitCode = -1;
    var scenetakePath = Environment.GetEnvironmentVariable("SCENETAKE_BIN");
    if (string.IsNullOrWhiteSpace(scenetakePath) || !File.Exists(scenetakePath))
        return false;

    var psi = new ProcessStartInfo(scenetakePath, [fixturePath, outputStem])
    {
        RedirectStandardOutput = false,
        RedirectStandardError = false,
        UseShellExecute = false,
    };

    using var process = Process.Start(psi);
    if (process is null)
        return false;

    process.WaitForExit();
    exitCode = process.ExitCode;
    return true;
}

static List<(double Time, string Text)> ReadCastTimedOutputEvents(string castPath)
{
    var results = new List<(double Time, string Text)>();
    var time = 0.0;
    foreach (var line in File.ReadAllLines(castPath))
    {
        if (line.Length == 0 || line[0] == '#')
            continue;
        if (line[0] == '{')
            continue;

        using var doc = JsonDocument.Parse(line);
        var root = doc.RootElement;
        if (root.GetArrayLength() < 3)
            continue;

        time += root[0].GetDouble();
        if (root[1].GetString() != "o")
            continue;

        results.Add((time, root[2].GetString() ?? ""));
    }

    return results;
}

static void Cleanup(string outputStem)
{
    foreach (var path in new[] { outputStem + ".cast", outputStem + ".svg" })
    {
        if (File.Exists(path))
            File.Delete(path);
    }
}

static string FindRepoRoot(string startDir)
{
    var dir = Path.GetFullPath(startDir);
    while (true)
    {
        if (File.Exists(Path.Combine(dir, "scenetake.cs")))
            return dir;

        var parent = Directory.GetParent(dir);
        if (parent is null)
            break;

        dir = parent.FullName;
    }

    Console.Error.WriteLine("Error: could not find scenetake.cs; run from the repository root");
    Environment.Exit(1);
    return "";
}
