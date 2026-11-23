using Scenetake.TestSupport;

namespace Scenetake.Tests;

// --at with a step's id through a published binary (SCENETAKE_BIN, spec_scenario.md → Step ids): the scenario path, an
// outputs entry, the svg command on the cast, and the errors before anything runs. The scenarios run in bash.

public sealed class StepIdTests
{
    [Test]
    public async Task AtNamesAStep() => await Assert.That(StepIdCliCases.AtNamesAStep(TestEnv.Binary)).IsTrue();

    [Test]
    public async Task IdErrorsStopBeforeRecording() => await Assert.That(StepIdCliCases.IdErrorsStopBeforeRecording(TestEnv.Binary)).IsTrue();
}

internal static class StepIdCliCases
{
    private const string Scenario = """
        shell: bash
        settings:
          typing-speed: 0.01
          typing-jitter: 0
        steps:
          - card: ["Title card"]
            id: title
          - id: compare
            run: printf 'kiwi\nmango\n'
          - echo papaya
        outputs:
          - format: svg
            at: title
            file: poster
        """;

    internal static bool AtNamesAStep(string scenetake) => InFolder(folder =>
    {
        File.WriteAllText(Path.Combine(folder, "demo.yaml"), Scenario);
        // --format replaces the outputs list. The screen right after compare's output: its lines, and not the next step's.
        var (exit, stderr) = Mp4FfmpegCases.Exec(scenetake, folder, ["--format", "svg", "--at", "compare", "demo.yaml"]);
        var still = exit == 0 ? File.ReadAllText(Path.Combine(folder, "demo.svg")) : "";
        var cast = exit == 0 ? File.ReadAllText(Path.Combine(folder, "demo.cast")) : "";

        // The svg command on the cast alone gives the same file as the scenario path. Then the outputs entry, which
        // records the same cast again.
        var (svgExit, svgErr) = Mp4FfmpegCases.Exec(scenetake, folder, ["svg", "demo.cast", "again.svg", "--at", "compare"]);
        var again = svgExit == 0 ? File.ReadAllText(Path.Combine(folder, "again.svg")) : "";
        var (entryExit, entryErr) = Mp4FfmpegCases.Exec(scenetake, folder, ["demo.yaml"]);
        var poster = entryExit == 0 ? File.ReadAllText(Path.Combine(folder, "poster.svg")) : "";
        var (missingExit, missingErr) = Mp4FfmpegCases.Exec(scenetake, folder, ["svg", "demo.cast", "missing.svg", "--at", "nope"]);
        return Check(exit == 0 && still.Contains("kiwi", StringComparison.Ordinal) && still.Contains("mango", StringComparison.Ordinal)
                && !still.Contains("papaya", StringComparison.Ordinal) && !still.Contains("animation", StringComparison.Ordinal)
                && stderr.Contains("still, at compare (", StringComparison.Ordinal) && cast.Contains("st:ids=title:", StringComparison.Ordinal),
                $"--at compare: exit {exit}{Environment.NewLine}{stderr}")
            && Check(entryExit == 0 && poster.Contains("Title", StringComparison.Ordinal) && !poster.Contains("kiwi", StringComparison.Ordinal),
                $"outputs entry: exit {entryExit}{Environment.NewLine}{entryErr}")
            && Check(svgExit == 0 && again == still,
                $"svg command: exit {svgExit}{Environment.NewLine}{svgErr}")
            && Check(missingExit == 1 && missingErr.Contains("Error: --at nope: no step in the cast has this id (it has title, compare)", StringComparison.Ordinal),
                $"an id the cast does not have: exit {missingExit}{Environment.NewLine}{missingErr}");
    });

    internal static bool IdErrorsStopBeforeRecording(string scenetake) => InFolder(folder =>
    {
        File.WriteAllText(Path.Combine(folder, "demo.yaml"), Scenario);
        File.WriteAllText(Path.Combine(folder, "entry.yaml"), Scenario.Replace("at: title", "at: titel", StringComparison.Ordinal));
        File.WriteAllText(Path.Combine(folder, "twice.yaml"), Scenario.Replace("id: title", "id: compare", StringComparison.Ordinal));
        var cases = new (string[] Args, string Expected)[]
        {
            (["--format", "svg", "--at", "nope", "demo.yaml"], "Error: --at nope: no step has this id. Give the step 'id: nope'."),
            (["entry.yaml"], "Error: outputs entry 1: --at titel: no step has this id. Give the step 'id: titel'."),
            (["twice.yaml"], "Steps 1 and 2 have the same id 'compare': each id names one step for --at."),
        };

        var ok = true;
        foreach (var (args, expected) in cases)
        {
            var (exit, stderr) = Mp4FfmpegCases.Exec(scenetake, folder, args);
            ok &= Check(exit == 1 && stderr.Contains(expected, StringComparison.Ordinal) && !stderr.Contains("running:", StringComparison.Ordinal),
                $"{string.Join(' ', args)}: exit {exit}{Environment.NewLine}{stderr}");
        }

        return ok && Check(!File.Exists(Path.Combine(folder, "demo.cast")), "a cast was written");
    });

    private static bool InFolder(Func<string, bool> test)
    {
        var folder = Directory.CreateTempSubdirectory("scenetake-stepid-").FullName;
        try
        {
            return test(folder);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    private static bool Check(bool passed, string message)
    {
        if (!passed)
            Console.Error.WriteLine($"  {message}");
        return passed;
    }
}
