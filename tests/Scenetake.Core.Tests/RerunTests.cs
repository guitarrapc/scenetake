using static Scenetake.Core.ScenarioCast;
using static Scenetake.Core.Tests.RerecordCases;

namespace Scenetake.Core.Tests;

// rerun: false (.github/docs/spec_scenario.md → Recording again): a step whose output the earlier cast has shows it
// without its command running. It runs when the earlier cast does not have it, when masking would hide part of the
// earlier output now, and with --rerun.

public sealed class RerunTests
{
    [Test]
    public async Task NotRunShowsTheEarlierOutput() => await Assert.That(RerunCases.NotRunShowsTheEarlierOutput()).IsTrue();

    [Test]
    public async Task NotRunKeepsPtyTiming() => await Assert.That(RerunCases.NotRunKeepsPtyTiming()).IsTrue();

    [Test]
    public async Task RunsWithoutAnEarlierOutput() => await Assert.That(RerunCases.RunsWithoutAnEarlierOutput()).IsTrue();

    [Test]
    public async Task RerunAllRunsIt() => await Assert.That(RerunCases.RerunAllRunsIt()).IsTrue();

    [Test]
    public async Task RunsWhenMaskingHidesTheEarlierOutput() => await Assert.That(RerunCases.RunsWhenMaskingHidesTheEarlierOutput()).IsTrue();

    [Test]
    public async Task HiddenStepTakesNoRerun() => await Assert.That(RerunCases.HiddenStepTakesNoRerun()).IsTrue();

    [Test]
    public async Task InteractiveStepShownWithoutATerminal() => await Assert.That(RerunCases.InteractiveStepShownWithoutATerminal()).IsTrue();
}

internal static class RerunCases
{
    private const string Scenario = """
        settings:
          typing-speed: 0.01
        steps:
          - echo one
          - run: slow
            rerun: false
          - echo three
        """;

    internal static bool NotRunShowsTheEarlierOutput()
    {
        // The second and third recordings do not run the step, and give the first recording's cast byte for byte.
        var first = Record(Scenario, step => Pipe(step.Command.Cmd == "slow" ? "first\n" : "ok\n"));
        var runs = 0;
        CommandExecution Again(StepStart step)
        {
            if (step.Command.Cmd == "slow")
                runs++;
            return Pipe(step.Command.Cmd == "slow" ? "second\n" : "ok\n");
        }

        var second = Record(Scenario, Again, first.Cast);
        var third = Record(Scenario, Again, second.Cast);
        var lines = Rerecording.Describe(second.Result, null, "demo.cast");
        return Expect(runs == 0, $"the step ran {runs} times")
            && Expect(second.Cast == first.Cast, $"the cast changed: {FirstDifference(first.Cast, second.Cast)}")
            && Expect(third.Cast == first.Cast, $"the third cast changed: {FirstDifference(first.Cast, third.Cast)}")
            && Expect(second.Result.NotRun.Count == 1 && second.Result.NotRun[0].Number == 2, $"not run: {second.Result.NotRun.Count}")
            && Expect(lines.Count > 0 && lines[0] == "Compared with the earlier cast: 2 of 3 steps printed the same, 1 not run (rerun: false)", string.Join(" | ", lines));
    }

    internal static bool NotRunKeepsPtyTiming()
    {
        // A PTY step's output keeps the times the clock gave it, so the cast stays byte for byte the first one.
        var yaml = Scenario.Replace("    rerun: false\n", "    pty: true\n    rerun: false\n", StringComparison.Ordinal);
        var first = Record(yaml, step => step.UsePty ? Pty((0.1234, "building\r\n"), (0.4567, "done\r\n")) : Pipe("ok\n"));
        var runs = 0;
        var second = Record(yaml, step =>
        {
            if (step.UsePty)
                runs++;
            return step.UsePty ? Pty((0.3, "other\r\n")) : Pipe("ok\n");
        }, first.Cast);
        return Expect(runs == 0, $"the step ran {runs} times")
            && Expect(second.Cast == first.Cast, $"the cast changed: {FirstDifference(first.Cast, second.Cast)}");
    }

    internal static bool RunsWithoutAnEarlierOutput()
    {
        // A first recording, and a step whose command changed, run it, and say why.
        var runs = 0;
        CommandExecution Run(StepStart step)
        {
            if (step.Command.Cmd.StartsWith("slow", StringComparison.Ordinal))
                runs++;
            return Pipe("out\n");
        }

        var first = Record(Scenario, Run);
        var firstLines = Rerecording.Describe(first.Result, null, "demo.cast");
        var changed = Record(Scenario.Replace("run: slow", "run: slow --flag", StringComparison.Ordinal), Run, first.Cast);
        var changedLines = Rerecording.Describe(changed.Result, null, "demo.cast");
        return Expect(runs == 2, $"the step ran {runs} times, not 2")
            && Expect(firstLines.Contains("Note: rerun: false: there is no earlier cast at demo.cast yet, so every step ran."), string.Join(" | ", firstLines))
            && Expect(changedLines.Contains("Note: step 2 sets rerun: false, but the earlier cast has no output of it, so it ran: slow --flag"), string.Join(" | ", changedLines));
    }

    internal static bool RerunAllRunsIt()
    {
        // --rerun runs the step and shows what it printed now. The comparison goes on as for any step.
        var first = Record(Scenario, step => Pipe(step.Command.Cmd == "slow" ? "first\n" : "ok\n"));
        var (cast, result) = Record(Scenario, step => Pipe(step.Command.Cmd == "slow" ? "second\n" : "ok\n"), first.Cast, rerunAll: true);
        return Expect(cast.Contains("second", StringComparison.Ordinal) && !cast.Contains("first", StringComparison.Ordinal), "the cast does not show the new output")
            && Expect(result.NotRun.Count == 0 && result.Changed.Count == 1, $"not run {result.NotRun.Count}, changed {result.Changed.Count}");
    }

    internal static bool RunsWhenMaskingHidesTheEarlierOutput()
    {
        // A mask added after the earlier recording must hide the earlier output too, so the step runs and shows its
        // output masked, as keep-output does.
        var first = Record(Scenario, step => Pipe(step.Command.Cmd == "slow" ? "token hunter22\n" : "ok\n"));
        var masked = "settings:\n  mask:\n    - hunter22\n" + Scenario.Replace("settings:\n", "", StringComparison.Ordinal);
        var runs = 0;
        var (cast, result) = Record(masked, step =>
        {
            if (step.Command.Cmd == "slow")
                runs++;
            return Pipe(step.Command.Cmd == "slow" ? "token hunter22\n" : "ok\n");
        }, first.Cast);
        var lines = Rerecording.Describe(result, null, "demo.cast");
        return Expect(runs == 1, $"the step ran {runs} times")
            && Expect(!cast.Contains("hunter22", StringComparison.Ordinal), "the cast shows the masked text")
            && Expect(lines.Contains("Note: step 2 sets rerun: false, but its earlier output holds text that is masked now, so it ran: slow"), string.Join(" | ", lines));
    }

    internal static bool HiddenStepTakesNoRerun()
    {
        // A hidden step's output is not in the cast, so it always runs, and rerun on it is an error before anything runs.
        var error = FindStepKeyError(ParseScenario("steps:\n  - run: setup\n    show: none\n    rerun: false\n  - echo hi\n"));
        return Expect(error?.Contains("'rerun' has no effect with 'show: none'", StringComparison.Ordinal) == true, error ?? "no error");
    }

    internal static bool InteractiveStepShownWithoutATerminal()
    {
        // An interactive step recorded once by hand, with rerun: false, does not need a terminal when the earlier cast
        // has it: CI records the rest of the scenario.
        const string yaml = "steps:\n  - echo before\n  - run: vim notes.md\n    pty: true\n    interactive: true\n    rerun: false\n";
        var first = Record(yaml, step => step.UseInteractive ? Pty((0.1, "edited\r\n")) : Pipe("ok\n"));
        var scenario = ParseScenario(yaml);
        var previous = PreviousCast.Parse(first.Cast.Split('\n'), 80, 24, out var problem);
        var masker = SecretMasker.ForScenario(scenario, environment: null);
        return Expect(previous is not null, $"the cast cannot be read: {problem}")
            && Expect(!RunsInteractiveStep(scenario, previous, masker, rerunAll: false), "the step needs a terminal although the cast has it")
            && Expect(RunsInteractiveStep(scenario, previous, masker, rerunAll: true), "--rerun should run it")
            && Expect(RunsInteractiveStep(scenario, null, masker, rerunAll: false), "a first recording should run it")
            && Expect(RunsInteractiveStep(ParseScenario(yaml.Replace("    rerun: false\n", "", StringComparison.Ordinal)), previous, masker, rerunAll: false), "a step without rerun: false should run")
            && Expect(RunsInteractiveStep(ParseScenario(yaml + "  - run: vim notes.md\n    pty: true\n    interactive: true\n    rerun: false\n"), previous, masker, rerunAll: false), "a second such step has no earlier output and should run");
    }

    private static bool Expect(bool condition, string message)
    {
        if (!condition)
            Console.Error.WriteLine($"  {message}");
        return condition;
    }
}
