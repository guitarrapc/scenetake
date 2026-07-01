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

return failures == 0 ? 0 : 1;

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

static bool RunScenetake(string fixturePath, string outputStem, out int exitCode)
{
    exitCode = -1;
    var scenetakePath = Environment.GetEnvironmentVariable("SCENETAKE_BIN");
    if (string.IsNullOrWhiteSpace(scenetakePath) || !File.Exists(scenetakePath))
        return false;

    var psi = new ProcessStartInfo(scenetakePath, [fixturePath, outputStem])
    {
        RedirectStandardOutput = true,
        RedirectStandardError = true,
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
