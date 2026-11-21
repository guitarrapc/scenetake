using System.Text;
using Scenetake.TestSupport;
using static Scenetake.Core.ScenarioCast;

namespace Scenetake.Tests;

// rerun: false and --rerun through a published binary (SCENETAKE_BIN, spec_scenario.md → Recording again): a step that
// the earlier cast has does not run again, --rerun runs it, and an interactive step recorded once records without a
// terminal, as in CI. The counter scenario runs in bash.

public sealed class RerunCliTests
{
    [Test]
    public async Task StepRunsOnceUntilRerun() => await Assert.That(RerunCliCases.StepRunsOnceUntilRerun(TestEnv.Binary)).IsTrue();

    [Test]
    public async Task RerunWithReplayOrCaptureIsRefused() => await Assert.That(RerunCliCases.RerunWithReplayOrCaptureIsRefused(TestEnv.Binary)).IsTrue();

    [Test]
    public async Task InteractiveStepRecordedOnceNeedsNoTerminal() => await Assert.That(RerunCliCases.InteractiveStepRecordedOnceNeedsNoTerminal(TestEnv.Binary)).IsTrue();
}

internal static class RerunCliCases
{
    internal static bool StepRunsOnceUntilRerun(string scenetake)
    {
        // The step counts its runs in a file. The second recording shows the first one's output and leaves the file as
        // it was, and --rerun runs it again.
        var folder = Directory.CreateTempSubdirectory("scenetake-rerun-").FullName;
        try
        {
            File.WriteAllText(Path.Combine(folder, "demo.yaml"), """
                shell: bash
                settings:
                  typing-speed: 0.001
                  pre-delay: 0
                  post-delay: 0
                steps:
                  - run: echo run >> runs.txt; echo "runs=$(grep -c run runs.txt)"
                    rerun: false
                  - echo after
                """);
            var (firstExit, firstErr) = Mp4FfmpegCases.Exec(scenetake, folder, ["demo.yaml"]);
            var firstCast = ReadCast(folder);
            var (secondExit, secondErr) = Mp4FfmpegCases.Exec(scenetake, folder, ["demo.yaml"]);
            var secondCast = ReadCast(folder);
            var runsAfterSecond = RunCount(folder);
            var (rerunExit, rerunErr) = Mp4FfmpegCases.Exec(scenetake, folder, ["--rerun", "demo.yaml"]);
            var rerunCast = ReadCast(folder);
            return Check(firstExit == 0 && firstCast.Contains("runs=1", StringComparison.Ordinal) && firstErr.Contains("Note: rerun: false: there is no earlier cast", StringComparison.Ordinal),
                    $"first: exit {firstExit}{Environment.NewLine}{firstErr}")
                && Check(secondExit == 0 && runsAfterSecond == 1 && secondCast == firstCast
                    && secondErr.Contains("not run (rerun: false)", StringComparison.Ordinal) && secondErr.Contains("1 not run (rerun: false)", StringComparison.Ordinal),
                    $"second: exit {secondExit}, runs {runsAfterSecond}, same cast {secondCast == firstCast}{Environment.NewLine}{secondErr}")
                && Check(rerunExit == 0 && RunCount(folder) == 2 && rerunCast.Contains("runs=2", StringComparison.Ordinal),
                    $"--rerun: exit {rerunExit}, runs {RunCount(folder)}{Environment.NewLine}{rerunErr}");
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    internal static bool RerunWithReplayOrCaptureIsRefused(string scenetake)
    {
        // A replay runs no step, so --rerun has nothing to do there. capture's one step sets no rerun.
        var folder = Directory.CreateTempSubdirectory("scenetake-rerun-").FullName;
        try
        {
            File.WriteAllText(Path.Combine(folder, "demo.yaml"), "steps:\n  - echo hi\n");
            var (replayExit, replayErr) = Mp4FfmpegCases.Exec(scenetake, folder, ["--rerun", "--replay", "demo.outputs.json", "demo.yaml"]);
            var (captureExit, captureErr) = Mp4FfmpegCases.Exec(scenetake, folder, ["capture", "--rerun", "--", "echo", "hi"]);
            return Check(replayExit == 1 && replayErr.Contains("--rerun runs every step, and --replay runs none", StringComparison.Ordinal), $"replay: exit {replayExit}{Environment.NewLine}{replayErr}")
                && Check(captureExit == 1 && captureErr.Contains("unknown option: --rerun", StringComparison.Ordinal), $"capture: exit {captureExit}{Environment.NewLine}{captureErr}");
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    internal static bool InteractiveStepRecordedOnceNeedsNoTerminal(string scenetake)
    {
        // The earlier cast is made here as a recording by hand would make it. The binary runs with its output
        // redirected, as in CI: the interactive step shows that output, and the step before it runs.
        const string yaml = "settings:\n  typing-speed: 0.001\n  pre-delay: 0\n  post-delay: 0\nsteps:\n  - echo before\n  - run: vim notes.md\n    pty: true\n    interactive: true\n    rerun: false\n";
        var folder = Directory.CreateTempSubdirectory("scenetake-rerun-").FullName;
        try
        {
            File.WriteAllText(Path.Combine(folder, "demo.yaml"), yaml);
            File.WriteAllText(Path.Combine(folder, "demo.cast"), EarlierCast(yaml), new UTF8Encoding(false));
            var (exit, stderr) = Mp4FfmpegCases.Exec(scenetake, folder, ["demo.yaml"]);
            var cast = ReadCast(folder);

            File.WriteAllText(Path.Combine(folder, "plain.yaml"), yaml.Replace("    rerun: false\n", "", StringComparison.Ordinal));
            File.WriteAllText(Path.Combine(folder, "plain.cast"), EarlierCast(yaml.Replace("    rerun: false\n", "", StringComparison.Ordinal)), new UTF8Encoding(false));
            var (plainExit, plainErr) = Mp4FfmpegCases.Exec(scenetake, folder, ["plain.yaml"]);
            var (rerunExit, rerunErr) = Mp4FfmpegCases.Exec(scenetake, folder, ["--rerun", "demo.yaml"]);
            return Check(exit == 0 && cast.Contains("edited by hand", StringComparison.Ordinal) && stderr.Contains("not run (rerun: false): vim notes.md", StringComparison.Ordinal),
                    $"rerun: false: exit {exit}{Environment.NewLine}{stderr}")
                && Check(plainExit == 1 && plainErr.Contains("require a real terminal", StringComparison.Ordinal), $"without rerun: false: exit {plainExit}{Environment.NewLine}{plainErr}")
                && Check(rerunExit == 1 && rerunErr.Contains("require a real terminal", StringComparison.Ordinal), $"--rerun: exit {rerunExit}{Environment.NewLine}{rerunErr}");
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>The cast a recording of <paramref name="yaml"/> writes, with the interactive step's output given here.</summary>
    private static string EarlierCast(string yaml)
    {
        var scenario = ParseScenario(yaml);
        var masker = SecretMasker.ForScenario(scenario, environment: null);
        var rerecorder = new Rerecorder(null, masker);
        var options = new GenerateOptions { LogRunning = false, Masker = masker, Rerecorder = rerecorder };
        var (events, _) = GenerateAsync(scenario, options, step => Task.FromResult(step.UseInteractive
            ? new CommandExecution(0, true, "", "", [new TimedBytes(TimeSpan.FromSeconds(0.2), Encoding.UTF8.GetBytes("edited by hand\r\n"))], null, null)
            : CommandExecution.FromPipeStream("before\n", "", 0, [new PipeStreamChunk(TimeSpan.FromMilliseconds(5), false, "before\n")]))).GetAwaiter().GetResult();
        var map = new int[events.Count + 1];
        events = CastOscFilter.RemovePrivateSequences(events, map);
        using var writer = new StringWriter();
        WriteCast(writer, scenario, events, "bash", CastTimestamp, RenderSettingsResolver.Resolve(scenario), Rerecording.MapIndices(rerecorder.Slots, map));
        return writer.ToString();
    }

    private static string ReadCast(string folder) =>
        File.Exists(Path.Combine(folder, "demo.cast")) ? File.ReadAllText(Path.Combine(folder, "demo.cast")) : "";

    private static int RunCount(string folder) =>
        File.Exists(Path.Combine(folder, "runs.txt")) ? File.ReadAllLines(Path.Combine(folder, "runs.txt")).Length : 0;

    private static bool Check(bool passed, string message)
    {
        if (!passed)
            Console.Error.WriteLine($"  {message}");
        return passed;
    }
}
