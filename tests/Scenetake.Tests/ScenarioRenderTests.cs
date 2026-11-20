using System.Diagnostics;
using Scenetake.TestSupport;

namespace Scenetake.Tests;

// The scenario path renders SVG and mp4 from the cast it just wrote, so its output is the same
// as running `scenetake svg|mp4` on that cast. The in-memory events differ from the cast: their
// times are not rounded to milliseconds, and they still hold markers.

[Category("Ffmpeg")]
[Category("Smoke")]
public sealed class ScenarioRenderTests
{
    // A folder for the files the tests write, removed after the last test of the class.
    private static readonly string Work = Directory.CreateTempSubdirectory("scenetake-scenario-render-").FullName;

    [After(Class)]
    public static void RemoveWork() => Directory.Delete(Work, recursive: true);

    [Test]
    public async Task ScenarioSvgMatchesSvgSubcommand() => await Assert.That(ScenarioRenderCases.SvgMatches(TestEnv.Binary, TestEnv.RepoRoot, Work, "plain", [], [])).IsTrue();

    [Test]
    public async Task ScenarioSvgMatchesWithMaxFps() => await Assert.That(ScenarioRenderCases.SvgMatches(TestEnv.Binary, TestEnv.RepoRoot, Work, "maxfps", ["--max-fps", "12"], ["--max-fps", "12"])).IsTrue();

    [Test]
    public async Task ScenarioSvgMatchesWithRenderOverrides() => await Assert.That(ScenarioRenderCases.SvgMatches(TestEnv.Binary, TestEnv.RepoRoot, Work, "overrides", ["--theme", "light", "--font-size", "20", "--window", "macos"], [])).IsTrue();

    [Test]
    public async Task ScenarioMp4MatchesMp4Subcommand()
    {
        TestEnv.RequireOnPath("ffmpeg");
        await Assert.That(ScenarioRenderCases.Mp4Matches(TestEnv.Binary, TestEnv.RepoRoot, Work)).IsTrue();
    }
}

internal static class ScenarioRenderCases
{

internal static bool SvgMatches(string scenetake, string repoRoot, string work, string name, string[] scenarioOptions, string[] subcommandOptions)
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

internal static bool Mp4Matches(string scenetake, string repoRoot, string work)
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
internal static string WriteScenario(string work, string name)
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

internal static (int ExitCode, string Stderr) Exec(string file, string workingDirectory, string[] arguments)
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

}
