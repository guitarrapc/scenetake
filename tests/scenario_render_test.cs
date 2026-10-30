#:sdk Microsoft.NET.Sdk
#:property TargetFramework=net10.0
#:property Nullable=enable
#:property ImplicitUsings=enable

// The scenario path renders SVG and mp4 from the cast it just wrote, so its output is the same
// as running `scenetake svg|mp4` on that cast. The in-memory events differ from the cast: their
// times are not rounded to milliseconds, and they still hold markers.
//
// Usage (from repository root):
//   SCENETAKE_BIN=/path/to/scenetake dotnet run tests/scenario_render_test.cs

using System.Diagnostics;

var repoRoot = FindRepoRoot(Directory.GetCurrentDirectory());
var scenetake = Environment.GetEnvironmentVariable("SCENETAKE_BIN");
if (string.IsNullOrWhiteSpace(scenetake) || !File.Exists(scenetake))
{
    Console.Error.WriteLine("skip scenario render tests: set SCENETAKE_BIN to a published scenetake binary");
    return 0;
}

var work = Directory.CreateTempSubdirectory("scenetake-scenario-render-").FullName;
var failures = 0;
try
{
    failures += Run("ScenarioSvgMatchesSvgSubcommand", () => SvgMatches(scenetake, repoRoot, work, "plain", [], []));
    failures += Run("ScenarioSvgMatchesWithMaxFps", () => SvgMatches(scenetake, repoRoot, work, "maxfps", ["--max-fps", "12"], ["--max-fps", "12"]));
    // Render overrides on the scenario path are written to the cast header, so the subcommand needs none.
    failures += Run("ScenarioSvgMatchesWithRenderOverrides", () => SvgMatches(scenetake, repoRoot, work, "overrides", ["--theme", "light", "--font-size", "20", "--window", "macos"], []));

    if (FindOnPath("ffmpeg") is null)
        Console.Error.WriteLine("skip ScenarioMp4MatchesMp4Subcommand: ffmpeg not found on PATH");
    else
        failures += Run("ScenarioMp4MatchesMp4Subcommand", () => Mp4Matches(scenetake, repoRoot, work));
}
finally
{
    Directory.Delete(work, recursive: true);
}

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
        Console.Error.WriteLine($"FAIL {name}: {ex}");
        return 1;
    }
}

static bool SvgMatches(string scenetake, string repoRoot, string work, string name, string[] scenarioOptions, string[] subcommandOptions)
{
    var stem = Path.Combine(work, name);
    var fromCast = Path.Combine(work, name + "-from-cast.svg");
    var (runExit, runStderr) = Exec(scenetake, repoRoot, ["--format", "svg", .. scenarioOptions, WriteScenario(work, name + ".yaml"), stem]);
    var (svgExit, svgStderr) = Exec(scenetake, repoRoot, ["svg", .. subcommandOptions, stem + ".cast", fromCast]);
    if (runExit != 0 || svgExit != 0)
    {
        Console.Error.WriteLine(runStderr + svgStderr);
        return false;
    }

    return File.ReadAllBytes(stem + ".svg").AsSpan().SequenceEqual(File.ReadAllBytes(fromCast));
}

static bool Mp4Matches(string scenetake, string repoRoot, string work)
{
    var stem = Path.Combine(work, "video");
    var fromCast = Path.Combine(work, "video-from-cast.mp4");
    // At 60fps a frame edge falls at 0.700s. The scenario's first output, the prompt, is at 0.7003s in
    // memory and 0.700s in the cast, on opposite sides of that edge. Rendering from the in-memory
    // events would show it one frame later, so the two files would differ.
    var (runExit, runStderr) = Exec(scenetake, repoRoot, ["--format", "mp4", "--fps", "60", WriteScenario(work, "video.yaml"), stem]);
    var (mp4Exit, mp4Stderr) = Exec(scenetake, repoRoot, ["mp4", "--fps", "60", stem + ".cast", fromCast]);
    if (runExit != 0 || mp4Exit != 0)
    {
        Console.Error.WriteLine(runStderr + mp4Stderr);
        return false;
    }

    return File.ReadAllBytes(stem + ".mp4").AsSpan().SequenceEqual(File.ReadAllBytes(fromCast));
}

// Typing jitter gives event times that are not whole milliseconds, which the cast then rounds.
// pre-delay 0.2003 puts the first prompt 0.3ms past a 60fps frame edge (see Mp4Matches).
static string WriteScenario(string work, string name)
{
    var path = Path.Combine(work, name);
    File.WriteAllText(path, """
        title: "scenario render"
        width: 40
        height: 6
        settings:
          pre-delay: 0.2003
        steps:
          - echo first line
          - name: "second step"
            run: echo second line
        """);
    return path;
}

static (int ExitCode, string Stderr) Exec(string file, string workingDirectory, string[] arguments)
{
    var startInfo = new ProcessStartInfo(file)
    {
        WorkingDirectory = workingDirectory,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
    };
    foreach (var argument in arguments)
        startInfo.ArgumentList.Add(argument);

    using var process = Process.Start(startInfo)!;
    var stdout = process.StandardOutput.ReadToEndAsync();
    var stderr = process.StandardError.ReadToEnd();
    process.WaitForExit();
    _ = stdout.Result;
    return (process.ExitCode, stderr);
}

static string? FindOnPath(string name)
{
    var fileName = OperatingSystem.IsWindows() ? name + ".exe" : name;
    foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
    {
        var candidate = Path.Combine(directory.Trim().Trim('"'), fileName);
        if (File.Exists(candidate))
            return candidate;
    }

    return null;
}

static string FindRepoRoot(string start)
{
    var current = new DirectoryInfo(start);
    while (current is not null)
    {
        if (File.Exists(Path.Combine(current.FullName, "scenetake.cs")))
            return current.FullName;
        current = current.Parent;
    }

    throw new InvalidOperationException("repository root not found");
}
