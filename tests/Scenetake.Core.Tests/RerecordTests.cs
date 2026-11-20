using System.Diagnostics;
using System.Text;
using static Scenetake.Core.ScenarioCast;
using Scenetake.TestSupport;

namespace Scenetake.Core.Tests;

// Recording again to an earlier cast (.github/docs/spec_scenario.md → Recording again): a step that prints the same
// keeps its earlier events and timing, keep-output keeps them whatever the step prints, and only changed steps differ.
// The integration tests need SCENETAKE_BIN and bash.

[Category("Smoke")]
public sealed class RerecordTests
{
    [Test]
    public async Task SameOutputGivesSameCast() => await Assert.That(RerecordCases.SameOutputGivesSameCast()).IsTrue();

    [Test]
    public async Task NoStepsSaysNothing() => await Assert.That(RerecordCases.NoStepsSaysNothing()).IsTrue();

    [Test]
    public async Task ChunkSplitIsTheSameOutput() => await Assert.That(RerecordCases.ChunkSplitIsTheSameOutput()).IsTrue();

    [Test]
    public async Task OnlyTheChangedStepDiffers() => await Assert.That(RerecordCases.OnlyTheChangedStepDiffers()).IsTrue();

    [Test]
    public async Task KeepOutputKeepsTheEarlierOutput() => await Assert.That(RerecordCases.KeepOutputKeepsTheEarlierOutput()).IsTrue();

    [Test]
    public async Task KeepOutputOfInstantOutput() => await Assert.That(RerecordCases.KeepOutputOfInstantOutput()).IsTrue();

    [Test]
    public async Task NewTimingKeysTakeEffect() => await Assert.That(RerecordCases.NewTimingKeysTakeEffect()).IsTrue();

    [Test]
    public async Task TypingChangesKeepOutputTiming() => await Assert.That(RerecordCases.TypingChangesKeepOutputTiming()).IsTrue();

    [Test]
    public async Task StepsAreFoundByRunText() => await Assert.That(RerecordCases.StepsAreFoundByRunText()).IsTrue();

    [Test]
    public async Task RepeatedCommandsMatchInOrder() => await Assert.That(RerecordCases.RepeatedCommandsMatchInOrder()).IsTrue();

    [Test]
    public async Task FilteredEventsKeepTheirSteps() => await Assert.That(RerecordCases.FilteredEventsKeepTheirSteps()).IsTrue();

    [Test]
    public async Task NoEarlierCastChangesNothing() => await Assert.That(RerecordCases.NoEarlierCastChangesNothing()).IsTrue();

    [Test]
    public async Task TagRoundTrips() => await Assert.That(RerecordCases.TagRoundTrips()).IsTrue();

    [Test]
    public async Task EarlierCastProblems() => await Assert.That(RerecordCases.EarlierCastProblems()).IsTrue();

    [Test]
    public async Task KeepOutputKeyRules() => await Assert.That(RerecordCases.KeepOutputKeyRules()).IsTrue();

    [Test]
    public async Task DescribeSaysWhatChanged() => await Assert.That(RerecordCases.DescribeSaysWhatChanged()).IsTrue();

    [Test]
    public async Task KeepOutputDoesNotBringBackMaskedText() => await Assert.That(RerecordCases.KeepOutputDoesNotBringBackMaskedText()).IsTrue();

    [Test]
    public async Task CommentFollowsTheKeptOutput() => await Assert.That(RerecordCases.CommentFollowsTheKeptOutput()).IsTrue();

    [Test]
    public async Task NextPtyStepStartsAfterTheKeptOutput() => await Assert.That(RerecordCases.NextPtyStepStartsAfterTheKeptOutput()).IsTrue();

    [Test]
    public async Task KeptPtyOutputNeedsItsPlaceOnTheScreen() => await Assert.That(RerecordCases.KeptPtyOutputNeedsItsPlaceOnTheScreen()).IsTrue();

    [Test]
    public async Task OtherTerminalSizeIsNotCompared() => await Assert.That(RerecordCases.OtherTerminalSizeIsNotCompared()).IsTrue();

    [Test]
    public async Task TagDescribesTheEventsTheCastHas() => await Assert.That(RerecordCases.TagDescribesTheEventsTheCastHas()).IsTrue();

    [Test]
    public async Task SplitCharactersAreTheSameOutput() => await Assert.That(RerecordCases.SplitCharactersAreTheSameOutput()).IsTrue();

    [Test]
    public async Task InvalidBytesKeepTheCast() => await Assert.That(RerecordCases.InvalidBytesKeepTheCast()).IsTrue();

    [Test]
    public async Task LineEndingsDoNotChangeTheStep() => await Assert.That(RerecordCases.LineEndingsDoNotChangeTheStep()).IsTrue();

    [Test]
    public async Task HalfMillisecondSettingsStayTheSame() => await Assert.That(RerecordCases.HalfMillisecondSettingsStayTheSame()).IsTrue();

    [Test]
    public async Task NamedStepsAreCompared() => await Assert.That(RerecordCases.NamedStepsAreCompared()).IsTrue();

    [Test]
    public async Task KeepOutputWithoutAnEarlierStepIsSaid() => await Assert.That(RerecordCases.KeepOutputWithoutAnEarlierStepIsSaid()).IsTrue();

    [Test]
    public async Task StepIdentityAndTimingKeys() => await Assert.That(RerecordCases.StepIdentityAndTimingKeys()).IsTrue();

    [Test]
    public async Task MaskedRunTextIsNotHashed() => await Assert.That(RerecordCases.MaskedRunTextIsNotHashed()).IsTrue();

    [Test]
    public async Task EditedCastIsNotCompared() => await Assert.That(RerecordCases.EditedCastIsNotCompared()).IsTrue();

    [Test]
    public async Task IntervalsDoNotDependOnThePreviousTime() => await Assert.That(RerecordCases.IntervalsDoNotDependOnThePreviousTime()).IsTrue();

    [Test]
    public async Task OutputThatIsOnlyARemovedTitleKeepsTheLayout() => await Assert.That(RerecordCases.OutputThatIsOnlyARemovedTitleKeepsTheLayout()).IsTrue();

    [Test]
    public async Task StderrReadsDoNotChangeTheStep() => await Assert.That(RerecordCases.StderrReadsDoNotChangeTheStep()).IsTrue();

    [Test]
    public async Task CarriageReturnSplitFromItsLineFeed() => await Assert.That(RerecordCases.CarriageReturnSplitFromItsLineFeed()).IsTrue();

    [Test]
    public async Task LaterStepsDoNotDependOnWhatTheFirstPrinted() => await Assert.That(RerecordCases.LaterStepsDoNotDependOnWhatTheFirstPrinted()).IsTrue();

    [Test]
    public async Task StreamedHighlightIgnoresHowSequencesFell() => await Assert.That(RerecordCases.StreamedHighlightIgnoresHowSequencesFell()).IsTrue();

    [Test]
    public async Task ReplayMasksTheRecordedRunAgain() => await Assert.That(RerecordCases.ReplayMasksTheRecordedRunAgain()).IsTrue();

    [Test]
    public async Task ColorsCountForPtyOutput() => await Assert.That(RerecordCases.ColorsCountForPtyOutput()).IsTrue();

    [Test]
    public async Task CarriageReturnPairsOnlyWithItsOwnStream() => await Assert.That(RerecordCases.CarriageReturnPairsOnlyWithItsOwnStream()).IsTrue();

    [Test]
    public async Task NoIntervalGoesBack() => await Assert.That(RerecordCases.NoIntervalGoesBack()).IsTrue();

    [Test]
    public async Task OpenSequencesEndWithTheirStep() => await Assert.That(RerecordCases.OpenSequencesEndWithTheirStep()).IsTrue();

    [Test]
    public async Task LongSequenceBeforeACommentKeepsTheLine() => await Assert.That(RerecordCases.LongSequenceBeforeACommentKeepsTheLine()).IsTrue();

    [Test]
    public async Task KeptEmptyFirstStepKeepsTheOthersInPlace() => await Assert.That(RerecordCases.KeptEmptyFirstStepKeepsTheOthersInPlace()).IsTrue();

    [Test]
    public async Task OutputsFileKeepsTheMaskedRun() => await Assert.That(RerecordCases.OutputsFileKeepsTheMaskedRun()).IsTrue();

    [Test]
    public async Task SplitCharactersMoveWhole() => await Assert.That(RerecordCases.SplitCharactersMoveWhole()).IsTrue();

    [Test]
    public async Task UncountedMaskingLeavesTheCounts() => await Assert.That(RerecordCases.UncountedMaskingLeavesTheCounts()).IsTrue();

    [Test]
    public async Task IntegrationRecordingAgainGivesSameFiles() => await Assert.That(RerecordCases.IntegrationRecordingAgainGivesSameFiles(TestEnv.RepoRoot)).IsTrue();

    [Test]
    public async Task IntegrationChangedAndKeptSteps() => await Assert.That(RerecordCases.IntegrationChangedAndKeptSteps(TestEnv.RepoRoot)).IsTrue();

    [Test]
    public async Task IntegrationOutputsFileReplaysToTheCast() => await Assert.That(RerecordCases.IntegrationOutputsFileReplaysToTheCast(TestEnv.RepoRoot)).IsTrue();

    [Test]
    public async Task IntegrationReplayFindsStepsWithExportedSecrets() => await Assert.That(RerecordCases.IntegrationReplayFindsStepsWithExportedSecrets(TestEnv.RepoRoot)).IsTrue();
}

internal static class RerecordCases
{

    internal static bool Expect(bool condition, string message)
    {
        if (!condition)
            Console.Error.WriteLine($"  {message}");
        return condition;
    }


    // A PTY step's output: each part at its time in seconds since the command started.
    internal static CommandExecution Pty(params (double Time, string Text)[] parts) =>
        new(0, true, "", "", [.. parts.Select(static part => new TimedBytes(TimeSpan.FromSeconds(part.Time), Encoding.UTF8.GetBytes(part.Text)))], null, null);

    internal static CommandExecution Pipe(string text) =>
        CommandExecution.FromPipeStream(text, "", 0, [new PipeStreamChunk(TimeSpan.FromMilliseconds(5), false, text)]);

    // Records the scenario with the outputs that run gives each step, compared with an earlier cast when there is one,
    // and returns the cast as scenetake writes it.
    internal static (string Cast, Rerecorder Result) Record(string yaml, Func<StepStart, CommandExecution> run, string? earlier = null, bool trackCursor = false)
    {
        var scenario = ParseScenario(yaml);
        PreviousCast? previous = null;
        if (earlier is not null)
        {
            previous = PreviousCast.Parse(earlier.Split('\n'), Cols(scenario), Rows(scenario), out var problem);
            if (previous is null)
                throw new InvalidOperationException($"the earlier cast cannot be used: {problem}");
        }

        var masker = SecretMasker.ForScenario(scenario, environment: null);
        var result = new Rerecorder(previous, masker);
        var options = new GenerateOptions { LogRunning = false, Masker = masker, Rerecorder = result, TrackPtyCursor = trackCursor };
        var (events, failure) = GenerateAsync(scenario, 7, options, step => Task.FromResult(run(step))).GetAwaiter().GetResult();
        if (failure is not null)
            throw new InvalidOperationException($"step {failure.Number} failed");

        var map = new int[events.Count + 1];
        events = CastOscFilter.RemovePrivateSequences(events, map);
        using var writer = new StringWriter();
        WriteCast(writer, scenario, events, "bash", 1700000000, RenderSettingsResolver.Resolve(scenario), Rerecording.MapIndices(result.Slots, map));
        return (writer.ToString(), result);
    }

    internal static int Cols(Scenario scenario) => scenario.Width ?? RenderSettingsResolver.DefaultTerminalCols;

    internal static int Rows(Scenario scenario) => scenario.Height ?? RenderSettingsResolver.DefaultTerminalRows;

    // An earlier cast with no steps, so a Rerecorder has something to compare with.
    internal static PreviousCast EmptyEarlierCast() =>
        PreviousCast.Parse(["{\"version\":3,\"term\":{\"cols\":80,\"rows\":24},\"tags\":[\"st:steps=0:0\"]}"], 80, 24, out _)!;

    // The event lines of a cast, without the header, whose st:steps tag moves with the events.
    internal static string[] EventLines(string cast) => cast.Split('\n')[1..];

    internal static string FirstDifference(string a, string b)
    {
        var left = a.Split('\n');
        var right = b.Split('\n');
        for (var i = 0; i < Math.Max(left.Length, right.Length); i++)
        {
            var l = i < left.Length ? left[i] : "(none)";
            var r = i < right.Length ? right[i] : "(none)";
            if (l != r)
                return $"line {i + 1}: {l} | {r}";
        }

        return "(same)";
    }

    // The output lines of the three steps: in a step that keeps its earlier events, they keep their intervals.
    internal static List<string> ThreeStepsLines(string cast) => EventLines(cast)
        .Where(static line => line.Contains("hello\\r\\n", StringComparison.Ordinal) || line.Contains("world\\r\\n", StringComparison.Ordinal)
            || line.Contains("\"1\\r\\n\"", StringComparison.Ordinal) || line.Contains("\"2\\r\\n\"", StringComparison.Ordinal)
            || line.Contains("\"3\\r\\n\"", StringComparison.Ordinal) || line.Contains("three\\r\\n", StringComparison.Ordinal))
        .ToList();

    internal static CommandExecution ThreeStepsOutput(StepStart step, double jitter) => step.Command.Cmd switch
    {
        "hello" => Pty((0.1 + jitter, "hello\r\n"), (0.4 + 2 * jitter, "world\r\n")),
        "count" => Pty((0.05 + jitter, "1\r\n"), (0.25 + 3 * jitter, "2\r\n"), (0.6 - jitter, "3\r\n")),
        _ => Pipe("three\n"),
    };

    // A recording of cards alone writes no step ranges, so the next one finds none. Its note would blame another tool
    // and promise a comparison next time, on every run.
    internal static bool NoStepsSaysNothing()
    {
        var lines = Rerecording.Describe(new Rerecorder(previous: null, masker: null), "it has no step ranges: an earlier scenetake or another tool wrote it", "cards.cast");
        return Expect(lines.Count == 0, string.Join(" | ", lines));
    }

    internal static bool SameOutputGivesSameCast()
    {
        var first = Record(ThreeSteps, step => ThreeStepsOutput(step, 0));
        // The same output, read a few milliseconds earlier or later, as each recording reads it.
        var fresh = Record(ThreeSteps, step => ThreeStepsOutput(step, 0.0037));
        var again = Record(ThreeSteps, step => ThreeStepsOutput(step, 0.0037), first.Cast);
        return Expect(fresh.Cast != first.Cast, "the test's timing differences change a cast that is not compared")
            & Expect(again.Cast == first.Cast, $"the cast differs: {FirstDifference(first.Cast, again.Cast)}")
            & Expect(again.Result.Same.Count == 3 && again.Result.Changed.Count == 0, $"same {again.Result.Same.Count}, changed {again.Result.Changed.Count}")
            & Expect(first.Cast.Contains("\"st:steps=", StringComparison.Ordinal), "the cast has no st:steps tag");
    }

    internal static bool ChunkSplitIsTheSameOutput()
    {
        const string yaml = "steps:\n  - run: two lines\n    pty: true\n";
        var first = Record(yaml, _ => Pty((0.1, "one\r\n"), (0.3, "two\r\n")));
        var again = Record(yaml, _ => Pty((0.1, "one\r"), (0.12, "\ntw"), (0.31, "o\r\n")), first.Cast);
        return Expect(again.Cast == first.Cast, $"the cast differs: {FirstDifference(first.Cast, again.Cast)}")
            & Expect(again.Result.Same.Count == 1, "the step was not the same");
    }

    internal static bool OnlyTheChangedStepDiffers()
    {
        var first = Record(ThreeSteps, step => ThreeStepsOutput(step, 0));
        var again = Record(ThreeSteps, step => step.Command.Cmd == "count"
            ? Pty((0.05, "1\r\n"), (0.2, "2\r\n"), (0.5, "3\r\n"), (0.9, "4\r\n"))
            : ThreeStepsOutput(step, 0.0051), first.Cast);

        // The first step's events and the third step's output keep their lines, timing included.
        var before = EventLines(first.Cast);
        var after = EventLines(again.Cast);
        var keptLines = before.Where(static line => line.Contains("hello", StringComparison.Ordinal) || line.Contains("world", StringComparison.Ordinal) || line.Contains("three\\r\\n", StringComparison.Ordinal)).ToList();
        return Expect(again.Result.Changed is [{ Number: 2 }], $"changed: {string.Join(", ", again.Result.Changed.Select(static s => s.Number))}")
            & Expect(again.Result.Same.Count == 2, $"same {again.Result.Same.Count}")
            & Expect(keptLines.Count == 3 && keptLines.All(line => after.Contains(line)), $"lost a line of an unchanged step: {string.Join(" ", keptLines)}")
            & Expect(after.Any(static line => line.Contains("4\\r\\n", StringComparison.Ordinal)), "the changed step's new output is missing");
    }

    internal static bool KeepOutputKeepsTheEarlierOutput()
    {
        const string yaml = "steps:\n  - run: date\n    pty: true\n    keep-output: true\n  - echo after\n";
        var first = Record(yaml, step => step.Command.Cmd == "date" ? Pty((0.1, "Mon 10:00:01\r\n")) : Pipe("after\n"));
        var again = Record(yaml, step => step.Command.Cmd == "date" ? Pty((0.13, "Tue 11:22:33\r\n")) : Pipe("after\n"), first.Cast);
        return Expect(again.Cast == first.Cast, $"the cast differs: {FirstDifference(first.Cast, again.Cast)}")
            & Expect(again.Result.Kept is [{ Number: 1 }], "the step was not kept");
    }

    internal static bool KeepOutputOfInstantOutput()
    {
        // A pipe step's output appears at once, at the step's execution-duration: the earlier text takes this run's time.
        const string yaml = "steps:\n  - run: uuidgen\n    keep-output: true\n  - echo after\n";
        var first = Record(yaml, step => step.Command.Cmd == "uuidgen" ? Pipe("1111-aaaa\n") : Pipe("after\n"));
        var again = Record(yaml, step => step.Command.Cmd == "uuidgen" ? Pipe("2222-bbbb\n") : Pipe("after\n"), first.Cast);
        var empty = Record(yaml, step => step.Command.Cmd == "uuidgen" ? Pipe("") : Pipe("after\n"), first.Cast);
        // A step that printed nothing before keeps showing nothing, without an empty event in its place.
        var silent = Record(yaml, step => step.Command.Cmd == "uuidgen" ? Pipe("") : Pipe("after\n"));
        var stillSilent = Record(yaml, step => step.Command.Cmd == "uuidgen" ? Pipe("now\n") : Pipe("after\n"), silent.Cast);
        return Expect(again.Cast == first.Cast, $"the cast differs: {FirstDifference(first.Cast, again.Cast)}")
            & Expect(again.Result.Kept.Count == 1, "the step was not kept")
            & Expect(empty.Cast == first.Cast, $"a step that printed nothing this time: {FirstDifference(first.Cast, empty.Cast)}")
            & Expect(stillSilent.Cast == silent.Cast, $"a step that printed nothing before: {FirstDifference(silent.Cast, stillSilent.Cast)}");
    }

    internal static bool NewTimingKeysTakeEffect()
    {
        // The same output, but max-duration now shortens it: the earlier timing would leave the new key without effect.
        const string yaml = "steps:\n  - run: slow\n    pty: true\n";
        var first = Record(yaml, _ => Pty((0.1, "a\r\n"), (1.5, "b\r\n")));
        var again = Record(yaml + "    max-duration: 0.5\n", _ => Pty((0.1, "a\r\n"), (1.5, "b\r\n")), first.Cast);
        var bLine = static (string cast) => EventLines(cast).First(static line => line.Contains("b\\r\\n", StringComparison.Ordinal));
        return Expect(again.Result.Same.Count == 1, "the output is the same")
            & Expect(bLine(again.Cast) != bLine(first.Cast), $"the earlier timing was kept: {bLine(again.Cast)}");
    }

    internal static bool TypingChangesKeepOutputTiming()
    {
        // Typing is timed by the YAML, so a new typing speed applies, while the PTY output keeps its earlier timing.
        var first = Record(ThreeSteps, step => ThreeStepsOutput(step, 0));
        var faster = "settings:\n  typing-speed: 0.02\n" + ThreeSteps;
        var again = Record(faster, step => ThreeStepsOutput(step, 0.0029), first.Cast);
        var typed = static (string cast) => EventLines(cast).Where(static line => line.Contains("\"o\",\"e\"]", StringComparison.Ordinal)).ToList();
        return Expect(again.Result.Same.Count == 3, $"same {again.Result.Same.Count}")
            & Expect(ThreeStepsLines(again.Cast).SequenceEqual(ThreeStepsLines(first.Cast)), $"the output lost its timing: {string.Join(" ", ThreeStepsLines(again.Cast))}")
            & Expect(!typed(again.Cast).SequenceEqual(typed(first.Cast)), "the new typing speed has no effect");
    }

    internal static bool StepsAreFoundByRunText()
    {
        // The steps move after a new first step, and keep their earlier output timing.
        var first = Record(ThreeSteps, step => ThreeStepsOutput(step, 0));
        var withNewStep = "steps:\n  - echo first\n" + ThreeSteps["steps:\n".Length..];
        var again = Record(withNewStep, step => step.Command.Cmd == "echo first" ? Pipe("first\n") : ThreeStepsOutput(step, 0.0043), first.Cast);
        return Expect(again.Result.Added is [{ Number: 1 }], $"added: {string.Join(", ", again.Result.Added.Select(static s => s.Number))}")
            & Expect(again.Result.Same.Count == 3, $"same {again.Result.Same.Count}")
            & Expect(ThreeStepsLines(again.Cast).SequenceEqual(ThreeStepsLines(first.Cast)), $"the moved steps lost their timing: {string.Join(" ", ThreeStepsLines(again.Cast))}");
    }

    internal static bool RepeatedCommandsMatchInOrder()
    {
        const string yaml = "steps:\n  - run: next\n    pty: true\n  - run: next\n    pty: true\n";
        var count = 0;
        CommandExecution Next(double jitter, bool swap) => (count++ % 2 == 0) != swap ? Pty((0.1 + jitter, "A\r\n")) : Pty((0.2 + jitter, "B\r\n"));
        var first = Record(yaml, _ => Next(0, false));
        var again = Record(yaml, _ => Next(0.0061, false), first.Cast);
        var swapped = Record(yaml, _ => Next(0, true), first.Cast);
        return Expect(again.Cast == first.Cast, $"the cast differs: {FirstDifference(first.Cast, again.Cast)}")
            & Expect(swapped.Result.Changed.Count == 2, $"swapped outputs: changed {swapped.Result.Changed.Count}");
    }

    internal static bool FilteredEventsKeepTheirSteps()
    {
        // ConPTY's title holds the shell's path. An event that holds only the title in the middle of a step is dropped, so
        // the next step's events sit one place earlier in the cast. A title at the start of a step would not prove this:
        // it is removed with the PTY's leading output, before the filter.
        const string yaml = "steps:\n  - run: titled\n    pty: true\n  - run: after\n    pty: true\n";
        CommandExecution Output(StepStart step, double jitter, string firstLine) => step.Command.Cmd == "titled"
            ? Pty((0.05 + jitter, "titled\r\n"), (0.1 + jitter, "\u001b]0;C:\\Users\\someone\\bash.exe\u0007"), (0.2 + jitter, "done\r\n"))
            : Pty((0.1 + 2 * jitter, firstLine), (0.3 + jitter, "end\r\n"));
        var first = Record(yaml, step => Output(step, 0, "after\r\n"));
        var again = Record(yaml, step => Output(step, 0.0047, "after\r\n"), first.Cast);
        var changed = Record(yaml, step => Output(step, 0.0047, "other\r\n"), first.Cast);
        return Expect(!first.Cast.Contains("someone", StringComparison.Ordinal), "the title was not removed")
            & Expect(again.Cast == first.Cast, $"the cast differs: {FirstDifference(first.Cast, again.Cast)}")
            & Expect(again.Result.Same.Count == 2, $"same {again.Result.Same.Count}")
            // With the second step's range one event off, its first line would fall outside what is compared.
            & Expect(changed.Result.Same is [{ Number: 1 }] && changed.Result.Changed is [{ Number: 2 }], $"same {changed.Result.Same.Count}, changed {changed.Result.Changed.Count}");
    }

    internal static bool NoEarlierCastChangesNothing()
    {
        var plain = new List<CastEvent>();
        var scenario = ParseScenario(ThreeSteps);
        (plain, _) = GenerateAsync(scenario, 7, new GenerateOptions { LogRunning = false }, step => Task.FromResult(ThreeStepsOutput(step, 0))).GetAwaiter().GetResult();
        var result = new Rerecorder(previous: null, masker: null);
        var (events, _) = GenerateAsync(scenario, 7, new GenerateOptions { LogRunning = false, Rerecorder = result }, step => Task.FromResult(ThreeStepsOutput(step, 0))).GetAwaiter().GetResult();
        return Expect(events.Count == plain.Count && events.Zip(plain).All(static pair => pair.First.Time == pair.Second.Time), "the events changed")
            & Expect(result.Added.Count == 3 && result.KeepMissing.Count == 0, $"added {result.Added.Count}");
    }

    internal static bool TagRoundTrips()
    {
        StepSlot[] slots =
        [
            new(1, "a", 0x0123456789abcdefUL, 0xdeadbeefU, true, false, 4, 3, 1.2345, 2.5, 1.3),
        new(2, "b", 42UL, 7U, false, true, 9, 0, 3.0004, 3.05, 3.05),
    ];
        var tag = Rerecording.FormatTag(slots, 9, 3100);
        var ok = Expect(tag == "st:steps=9:3100 0123456789abcdef:deadbeef:c:4:3:1235:2500 000000000000002a:00000007:d:9:0:3000:3050", tag)
            & Expect(Rerecording.TryParseTag(tag[Rerecording.TagPrefix.Length..], 9, 3100, out var steps) && steps.Count == 2, "parse")
            && Expect(Describe(steps[0]) == "81985529216486895 3735928559 True 4 3 1235 2500", Describe(steps[0]))
            & Expect(Describe(steps[1]) == "42 7 False 9 0 3000 3050", Describe(steps[1]));
        // An entry that names events the cast does not have, or that is malformed, makes the tag unusable.
        const string id = "0000000000000000:00000000";
        foreach (var bad in new[] { $"{id}:c:5:5:0:1", $"{id}:x:0:1:0:1", $"{id}:c:0:1:0", "zz00000000000000:00000000:c:0:1:0:1", "0:0:c:0:1:0:1", $"{id}:c:-1:1:0:1", "0000000000000000:0000000A:c:0:1:0:1" })
            ok &= Expect(!Rerecording.TryParseTag($"9:3100 {bad}", 9, 3100, out _), $"accepted {bad}");
        // The cast's own event count and end must be what the tag says.
        ok &= Expect(!Rerecording.TryParseTag("9:3100", 8, 3100, out _), "another event count");
        ok &= Expect(!Rerecording.TryParseTag("9:3100", 9, 3101, out _), "another end");
        ok &= Expect(!Rerecording.TryParseTag("", 0, 0, out _), "an empty tag");
        return ok;

        static string Describe(PreviousStep step) => $"{step.Id} {step.Timing} {step.Clock} {step.First} {step.Count} {step.StartMs} {step.EndMs}";
    }

    internal static bool KeepOutputDoesNotBringBackMaskedText()
    {
        const string steps = "steps:\n  - run: pipe step\n    keep-output: true\n  - run: pty step\n    pty: true\n    keep-output: true\n";
        CommandExecution Output(StepStart step, string word) => step.Command.Cmd == "pipe step" ? Pipe($"build {word}\n") : Pty((0.1, $"pty {word}\r\n"));
        var first = Record(steps, step => Output(step, "hunter2"));
        var masked = Record("settings:\n  mask: [hunter2]\n" + steps, step => Output(step, "hunter2"), first.Cast);
        var lines = Rerecording.Describe(masked.Result, null, "demo.cast");
        // Output that was masked when it was recorded holds only stars, so it stays kept.
        var kept = Record("settings:\n  mask: [hunter2]\n" + steps, step => Output(step, "hunter2 again"), masked.Cast);
        return Expect(first.Cast.Contains("hunter2", StringComparison.Ordinal), "the first cast should hold the text")
            & Expect(!masked.Cast.Contains("hunter2", StringComparison.Ordinal), "the masked text is still in the cast")
            & Expect(masked.Result.KeepMasked.Count == 2 && masked.Result.Changed.Count == 2 && masked.Result.Kept.Count == 0, $"masked {masked.Result.KeepMasked.Count}, changed {masked.Result.Changed.Count}, kept {masked.Result.Kept.Count}")
            & Expect(lines.Any(static line => line.StartsWith("Note: step 1 sets keep-output, but its earlier output holds text that is masked now", StringComparison.Ordinal)), string.Join("\n", lines))
            & Expect(kept.Result.Kept.Count == 2 && kept.Result.KeepMasked.Count == 0, $"kept {kept.Result.Kept.Count}, masked {kept.Result.KeepMasked.Count}");
    }

    internal static bool CommentFollowsTheKeptOutput()
    {
        // The line break before a name comment depends on how the output above it ends, and that output is the kept one.
        const string yaml = "steps:\n  - run: cat t.txt\n    keep-output: true\n  - run: echo two\n    name: second\n";
        CommandExecution Output(StepStart step, string text) => step.Command.Cmd == "cat t.txt" ? Pipe(text) : Pipe("two\n");
        var noBreak = Record(yaml, step => Output(step, "text"));
        var gainedBreak = Record(yaml, step => Output(step, "text\n"), noBreak.Cast);
        var withBreak = Record(yaml, step => Output(step, "text\n"));
        var lostBreak = Record(yaml, step => Output(step, "text"), withBreak.Cast);
        return Expect(gainedBreak.Cast == noBreak.Cast, $"gained a line break: {FirstDifference(noBreak.Cast, gainedBreak.Cast)}")
            & Expect(lostBreak.Cast == withBreak.Cast, $"lost a line break: {FirstDifference(withBreak.Cast, lostBreak.Cast)}");
    }

    internal static bool NextPtyStepStartsAfterTheKeptOutput()
    {
        // The fake ConPTY writes the row it was told, as ConPTY does: a start cursor from the discarded output changes it.
        const string yaml = "width: 40\nheight: 10\nsteps:\n  - run: lines\n    pty: true\n    keep-output: true\n  - run: draw\n    pty: true\n";
        CommandExecution Output(StepStart step, int lines) => step.Command.Cmd == "lines"
            ? Pty((0.1, string.Concat(Enumerable.Repeat("line\r\n", lines))))
            : Pty((0.1, $"\u001b[{step.PtyStartCursor?.Row};1HX\r\n"));
        var first = Record(yaml, step => Output(step, 1), trackCursor: true);
        var again = Record(yaml, step => Output(step, 6), first.Cast, trackCursor: true);
        return Expect(again.Cast == first.Cast, $"the cast differs: {FirstDifference(first.Cast, again.Cast)}")
            & Expect(again.Result.Kept is [{ Number: 1 }] && again.Result.Same is [{ Number: 2 }], $"kept {again.Result.Kept.Count}, same {again.Result.Same.Count}, changed {again.Result.Changed.Count}");
    }

    internal static bool KeptPtyOutputNeedsItsPlaceOnTheScreen()
    {
        // The step above now prints more lines, so rows that ConPTY wrote for the old place would land on other text.
        const string yaml = "width: 40\nheight: 20\nsteps:\n  - run: lines\n  - run: draw\n    pty: true\n    keep-output: true\n";
        CommandExecution Output(StepStart step, int lines, string text) => step.Command.Cmd == "lines"
            ? Pipe(string.Concat(Enumerable.Repeat("line\n", lines)))
            : Pty((0.1, $"\u001b[{step.PtyStartCursor?.Row};1H{text}\r\n"));
        var first = Record(yaml, step => Output(step, 1, "old"), trackCursor: true);
        var moved = Record(yaml, step => Output(step, 4, "new"), first.Cast, trackCursor: true);
        var stayed = Record(yaml, step => Output(step, 1, "new"), first.Cast, trackCursor: true);
        var unix = Record(yaml, step => Output(step, 4, "new"), first.Cast, trackCursor: false);
        return Expect(moved.Result.KeepMoved is [{ Number: 2 }] && moved.Result.Kept.Count == 0, $"moved {moved.Result.KeepMoved.Count}, kept {moved.Result.Kept.Count}")
            & Expect(moved.Cast.Contains("new\\r\\n", StringComparison.Ordinal), "the step should show what it printed now")
            & Expect(stayed.Result.Kept is [{ Number: 2 }] && stayed.Cast.Contains("old\\r\\n", StringComparison.Ordinal), "the same place keeps the output")
            // A Unix PTY passes the program's bytes through and is not told where it starts.
            & Expect(unix.Result.Kept is [{ Number: 2 }], $"without a start cursor: kept {unix.Result.Kept.Count}");
    }

    internal static bool OtherTerminalSizeIsNotCompared()
    {
        const string yaml = "width: 100\nheight: 30\nsteps:\n  - run: wide\n    pty: true\n    keep-output: true\n";
        var first = Record(yaml, _ => Pty((0.1, "text\r\n")));
        var lines = first.Cast.Split('\n');
        var same = PreviousCast.Parse(lines, 100, 30, out var sameProblem);
        var narrow = PreviousCast.Parse(lines, 30, 30, out var narrowProblem);
        var shorter = PreviousCast.Parse(lines, 100, 10, out var shorterProblem);
        return Expect(same is not null, $"the same size: {sameProblem}")
            & Expect(narrow is null && narrowProblem == "it was recorded in a 100x30 terminal, and this one is 30x30", $"another width: {narrowProblem}")
            & Expect(shorter is null && shorterProblem == "it was recorded in a 100x30 terminal, and this one is 100x10", $"another height: {shorterProblem}");
    }

    internal static bool TagDescribesTheEventsTheCastHas()
    {
        // Timing keys change while keep-output holds the earlier events, and then keep-output goes: the step must take the
        // new timing, because the events in the cast were never timed with those keys.
        const string steps = "steps:\n  - run: slow\n    pty: true\n";
        CommandExecution Output(StepStart _) => Pty((0.1, "a\r\n"), (1.5, "b\r\n"));
        var first = Record(steps + "    keep-output: true\n", Output);
        var kept = Record(steps + "    keep-output: true\n    max-duration: 0.5\n", Output, first.Cast);
        var released = Record(steps + "    max-duration: 0.5\n", Output, kept.Cast);
        var fresh = Record(steps + "    max-duration: 0.5\n", Output);
        var bLine = static (string cast) => EventLines(cast).First(static line => line.Contains("b\\r\\n", StringComparison.Ordinal));
        return Expect(bLine(kept.Cast) == bLine(first.Cast), "keep-output should hold the earlier timing")
            & Expect(bLine(released.Cast) == bLine(fresh.Cast), $"the new timing was lost: {bLine(released.Cast)} | {bLine(fresh.Cast)}")
            & Expect(released.Result.Same.Count == 1, "the output is the same");
    }

    internal static bool SplitCharactersAreTheSameOutput()
    {
        const string yaml = "steps:\n  - run: japanese\n    pty: true\n";
        var text = Encoding.UTF8.GetBytes("日本語\r\n");
        CommandExecution Split(int at) => new(0, true, "", "", [new TimedBytes(TimeSpan.FromSeconds(0.1), text[..at]), new TimedBytes(TimeSpan.FromSeconds(0.2), text[at..])], null, null);
        var first = Record(yaml, _ => Pty((0.1, "日本語\r\n")));
        var again = Record(yaml, _ => Split(1), first.Cast);
        var splitFirst = Record(yaml, _ => Split(4));
        var splitAgain = Record(yaml, _ => Split(1), splitFirst.Cast);
        return Expect(again.Result.Same.Count == 1 && again.Cast == first.Cast, $"split inside a character: {FirstDifference(first.Cast, again.Cast)}")
            & Expect(!splitFirst.Cast.Contains("uFFFD", StringComparison.Ordinal), "a split character became U+FFFD")
            & Expect(splitFirst.Cast.Contains("日", StringComparison.Ordinal) && splitFirst.Cast.Contains("本", StringComparison.Ordinal), "the characters are missing")
            & Expect(splitAgain.Result.Same.Count == 1, "the same text split elsewhere");
    }

    internal static bool InvalidBytesKeepTheCast()
    {
        // Kept events come back from the file as text, so all three sources of U+FFFD must be written alike.
        const string yaml = "steps:\n  - run: bytes\n    pty: true\n  - run: encoded\n    pty: true\n  - run: text\n    stream-output: true\n";
        CommandExecution Output(StepStart step, double time) => step.Command.Cmd switch
        {
            "bytes" => new(0, true, "", "", [new TimedBytes(TimeSpan.FromSeconds(time), new byte[] { (byte)'a', 0xFF, (byte)'b', (byte)'\r', (byte)'\n' })], null, null),
            "encoded" => Pty((time, "c\uFFFDd\r\n")),
            _ => Pipe("replacement \uFFFD char\n"),
        };
        var first = Record(yaml, step => Output(step, 0.1));
        var again = Record(yaml, step => Output(step, 0.1037), first.Cast);
        return Expect(first.Cast.Contains("a\\uFFFDb", StringComparison.Ordinal), "the byte should be the escape")
            & Expect(first.Cast.Contains("c\\uFFFDd", StringComparison.Ordinal), "an encoded U+FFFD should use the same escape")
            & Expect(first.Cast.Contains("replacement \\uFFFD char", StringComparison.Ordinal), "text should use the same escape")
            & Expect(again.Result.Same.Count == 3, $"same {again.Result.Same.Count}")
            & Expect(again.Cast == first.Cast, $"the cast differs: {FirstDifference(first.Cast, again.Cast)}");
    }

    internal static bool LineEndingsDoNotChangeTheStep()
    {
        const string lf = "steps:\n  - run: |\n      first\n      second\n    pty: true\n    keep-output: true\n";
        var crlf = lf.Replace("\n", "\r\n", StringComparison.Ordinal);
        var first = Record(lf, _ => Pty((0.1, "old\r\n")));
        var again = Record(crlf, _ => Pty((0.1, "new\r\n")), first.Cast);
        return Expect(again.Result.Kept.Count == 1 && again.Result.KeepMissing.Count == 0, $"kept {again.Result.Kept.Count}, missing {again.Result.KeepMissing.Count}")
            & Expect(EventLines(again.Cast).Any(static line => line.Contains("old\\r\\n", StringComparison.Ordinal)), "the earlier output was not kept");
    }

    internal static bool HalfMillisecondSettingsStayTheSame()
    {
        // These settings put events exactly on half a millisecond. The first recording's reads vary, because the time of
        // the event before decides what floating-point noise a half gets.
        const string yaml = "settings:\n  typing-speed: 0.0125\n  typing-jitter: 0\n  pre-delay: 0.1235\n  post-delay: 0.5\n  execution-duration: 0.05\n  stream-output: true\nsteps:\n  - run: abc\n    pty: true\n  - run: def\n    pty: true\n";
        var ok = true;
        for (var i = 0; i < 40 && ok; i++)
        {
            var d1 = i * 0.000257;
            var d2 = i * 0.000113;
            var first = Record(yaml, step => Pty((0.1 + d1, step.Command.Cmd + "\r\n"), (0.2 + d2, "x\r\n")));
            var again = Record(yaml, step => Pty((0.1037, step.Command.Cmd + "\r\n"), (0.2091, "x\r\n")), first.Cast);
            ok &= Expect(again.Cast == first.Cast, $"reads moved by {d1} and {d2}: {FirstDifference(first.Cast, again.Cast)}");
        }

        return ok;
    }

    internal static bool IntervalsDoNotDependOnThePreviousTime()
    {
        static string Written(params double[] times)
        {
            using var writer = new StringWriter();
            var error = 0.0;
            var previous = 0.0;
            foreach (var time in times)
            {
                WriteCastInterval(writer, time - previous, ref error);
                writer.Write(' ');
                previous = time;
            }

            return writer.ToString();
        }

        // 12.5 ms stays on the same millisecond whether the event before sits on a fraction or on a whole millisecond.
        return Expect(Written(0.000437, 0.0125) == "0.000 0.013 ", Written(0.000437, 0.0125))
            & Expect(Written(0.001, 0.0125) == "0.001 0.012 ", Written(0.001, 0.0125))
            // Two events at the same half millisecond: the second adds nothing, and never goes back.
            & Expect(Written(0.0125, 0.0125, 0.013) == "0.013 0.000 0.000 ", Written(0.0125, 0.0125, 0.013));
    }

    internal static bool OutputThatIsOnlyARemovedTitleKeepsTheLayout()
    {
        // The filter removes this step's only event after composing. The steps after it are laid out with the event still
        // in the list, in the first recording and in this one, or the second recording would start them elsewhere.
        const string title = "\u001b]0;my title\u0007";
        foreach (var yaml in new[]
        {
        "steps:\n  - run: title\n    show: output\n    stream-output: true\n",
        "steps:\n  - run: title\n    show: output\n    stream-output: true\n  - echo after\n",
        "steps:\n  - run: title\n    show: output\n    stream-output: true\n  - run: echo after\n    name: second\n",
    })
        {
            CommandExecution Output(StepStart step) => step.Command.Cmd == "title" ? Pipe(title) : Pipe("after\n");
            var first = Record(yaml, Output);
            var again = Record(yaml, Output, first.Cast);
            if (!Expect(again.Cast == first.Cast, $"the cast differs: {FirstDifference(first.Cast, again.Cast)}"))
                return false;
        }

        return true;
    }

    internal static bool StderrReadsDoNotChangeTheStep()
    {
        // Each read of a streamed pipe step is colored by itself, so the same lines read otherwise have other color codes.
        static CommandExecution Reads(params (double Time, string Text)[] reads) => CommandExecution.FromPipeStream("", string.Concat(reads.Select(static r => r.Text)), 0,
            [.. reads.Select(static r => new PipeStreamChunk(TimeSpan.FromSeconds(r.Time), true, r.Text))]);
        const string yaml = "steps:\n  - run: build\n    stream-output: true\n";
        var first = Record(yaml, _ => Reads((0.1, "one\n"), (0.2, "two\nthree\n")));
        var again = Record(yaml, _ => Reads((0.1, "one\ntwo\n"), (0.2, "three\n")), first.Cast);
        var otherText = Record(yaml, _ => Reads((0.1, "one\ntwo\n"), (0.2, "four\n")), first.Cast);
        // The colors no longer tell a change apart, so a new color key must still take effect.
        var recolored = Record(yaml + "    stderr-color: green\n", _ => Reads((0.1, "one\n"), (0.2, "two\nthree\n")), first.Cast);
        return Expect(again.Result.Same.Count == 1 && again.Cast == first.Cast, $"other reads: {FirstDifference(first.Cast, again.Cast)}")
            & Expect(otherText.Result.Changed.Count == 1, "other text is a change")
            & Expect(recolored.Result.Same.Count == 1 && recolored.Cast != first.Cast, "a new stderr-color had no effect");
    }

    internal static bool CarriageReturnSplitFromItsLineFeed()
    {
        const string yaml = "steps:\n  - run: tool\n    stream-output: true\n";
        static CommandExecution Reads(params (double Time, string Text)[] reads) => CommandExecution.FromPipeStream(string.Concat(reads.Select(static r => r.Text)), "", 0,
            [.. reads.Select(static r => new PipeStreamChunk(TimeSpan.FromSeconds(r.Time), false, r.Text))]);
        static CommandExecution ErrorReads(params (double Time, string Text)[] reads) => CommandExecution.FromPipeStream("", string.Concat(reads.Select(static r => r.Text)), 0,
            [.. reads.Select(static r => new PipeStreamChunk(TimeSpan.FromSeconds(r.Time), true, r.Text))]);
        var split = Record(yaml, _ => Reads((0.1, "one\r"), (0.2, "\ntwo\r\n")));
        var text = string.Concat(EventLines(split.Cast).Where(static line => line.Contains("one", StringComparison.Ordinal) || line.Contains("two", StringComparison.Ordinal)));
        // On stderr each read gets its color first, and the two halves must still make one line break.
        var whole = Record(yaml, _ => ErrorReads((0.1, "one\r\ntwo\r\n")));
        var errorSplit = Record(yaml, _ => ErrorReads((0.1, "one\r"), (0.2, "\ntwo\r\n")), whole.Cast);
        // One line break between the two lines, in the event that has the line feed.
        return Expect(text.Contains("\"one\"]", StringComparison.Ordinal) && text.Contains("\"\\r\\ntwo\\r\\n\"]", StringComparison.Ordinal), text)
            & Expect(errorSplit.Result.Same.Count == 1 && errorSplit.Cast == whole.Cast, $"stderr: {FirstDifference(whole.Cast, errorSplit.Cast)}");
    }

    internal static bool LaterStepsDoNotDependOnWhatTheFirstPrinted()
    {
        // Sums the intervals up to the first event that holds the text.
        static double TimeOf(string cast, string text)
        {
            var total = 0m;
            foreach (var line in EventLines(cast))
            {
                if (line.Length == 0)
                    continue;
                total += decimal.Parse(line[1..line.IndexOf(',')], System.Globalization.CultureInfo.InvariantCulture);
                if (line.Contains(text, StringComparison.Ordinal))
                    return (double)total;
            }

            throw new InvalidOperationException($"no event holds {text}");
        }

        const string yaml = "steps:\n  - run: first\n    show: output\n  - echo second\n";
        CommandExecution Output(StepStart step, string text) => step.Command.Cmd == "first" ? Pipe(text) : Pipe("second\n");
        var title = Record(yaml, step => Output(step, "\u001b]0;My demo\u0007")).Cast;
        var printed = Record(yaml, step => Output(step, "text\n")).Cast;
        var silent = Record(yaml, step => Output(step, "")).Cast;
        var alone = Record("steps:\n  - run: first\n    show: output\n", _ => Pipe("\u001b]0;My demo\u0007")).Cast;
        return Expect(TimeOf(title, "\"$ \"") == TimeOf(printed, "\"$ \""), $"a title only: {TimeOf(title, "\"$ \"")} against {TimeOf(printed, "\"$ \"")}")
            & Expect(TimeOf(silent, "\"$ \"") == TimeOf(printed, "\"$ \""), $"no output: {TimeOf(silent, "\"$ \"")} against {TimeOf(printed, "\"$ \"")}")
            // The recording still ends at a prompt, though the step left nothing in the cast.
            & Expect(alone.Contains("\"$ \"]", StringComparison.Ordinal), alone);
    }

    internal static bool StreamedHighlightIgnoresHowSequencesFell()
    {
        const string yaml = "steps:\n  - run: link\n    stream-output: true\n    highlight:\n      - color: green\n        at: \"1\"\n";
        static CommandExecution Reads(params (double Time, string Text)[] reads) => CommandExecution.FromPipeStream(string.Concat(reads.Select(static r => r.Text)), "", 0,
            [.. reads.Select(static r => new PipeStreamChunk(TimeSpan.FromSeconds(r.Time), false, r.Text))]);
        const string open = "\u001b]8;;http://x\u001b\\";
        const string close = "\u001b]8;;\u001b\\";
        var first = Record(yaml, _ => Reads((0.1, $"see {open}"), (0.4, $"link text{close} done\n")));
        var again = Record(yaml, _ => Reads((0.1, $"see {open}link "), (0.4, $"text{close} done\n")), first.Cast);
        return Expect(!first.Cast.Contains("\\u001b\\\\", StringComparison.Ordinal), $"a stray string terminator: {first.Cast}")
            & Expect(again.Result.Same.Count == 1, $"same {again.Result.Same.Count}, changed {again.Result.Changed.Count}");
    }

    internal static bool ReplayMasksTheRecordedRunAgain()
    {
        // The recording masked one secret. The other one is masked only since then, and must not reach the hash.
        const string yaml = "settings:\n  mask: [aaaasecret, bbbbsecret]\nsteps:\n  - run: echo aaaasecret bbbbsecret\n";
        var replayed = Record(yaml, _ => Pipe("ok\n") with { MaskedRun = "echo ********** bbbbsecret" });
        var recorded = Record(yaml, _ => Pipe("ok\n"));
        var ptyYaml = yaml + "    pty: true\n";
        var replayedPty = Record(ptyYaml, _ => Pty((0.1, "ok\r\n")) with { MaskedRun = "echo ********** bbbbsecret" });
        var recordedPty = Record(ptyYaml, _ => Pty((0.1, "ok\r\n")));
        return Expect(replayed.Result.Slots[0].Run == "echo ********** **********", replayed.Result.Slots[0].Run)
            & Expect(replayed.Result.Slots[0].Id == recorded.Result.Slots[0].Id, "the replay's step id differs from a recording's")
            & Expect(replayedPty.Result.Slots[0].Id == recordedPty.Result.Slots[0].Id, "the replayed PTY step's id differs from a recording's");
    }

    internal static bool ColorsCountForPtyOutput()
    {
        // Only streamed pipe output is colored by scenetake one read at a time. A PTY's colors come from the program.
        const string yaml = "steps:\n  - run: ls\n    pty: true\n";
        var first = Record(yaml, _ => Pty((0.1, "\u001b[32mok\u001b[0m\r\n")));
        var again = Record(yaml, _ => Pty((0.1, "\u001b[31mok\u001b[0m\r\n")), first.Cast);
        return Expect(again.Result.Changed.Count == 1, $"changed {again.Result.Changed.Count}, same {again.Result.Same.Count}");
    }

    internal static bool CarriageReturnPairsOnlyWithItsOwnStream()
    {
        // A line feed on the other stream is not the rest of this stream's CRLF, so the carriage return stays a line break.
        const string yaml = "steps:\n  - run: tool\n    stream-output: true\n";
        PipeStreamChunk[] chunks =
        [
            new(TimeSpan.FromSeconds(0.1), false, "one\r"),
        new(TimeSpan.FromSeconds(0.2), true, "\nerr\n"),
        new(TimeSpan.FromSeconds(0.3), false, "two\n"),
    ];
        var cast = Record(yaml, _ => CommandExecution.FromPipeStream("one\rtwo\n", "\nerr\n", 0, chunks)).Cast;
        return Expect(cast.Contains("\"one\\r\\n\"]", StringComparison.Ordinal), string.Join("\n", EventLines(cast).Where(static line => line.Contains("one", StringComparison.Ordinal))));
    }

    internal static bool NoIntervalGoesBack()
    {
        // The command starts just under half a millisecond, and the output before it is written just over.
        const string settings = "  typing-jitter: 0\n  pre-delay: 0\n  post-delay: 0\n  execution-duration: 0\nsteps:\n  - echo A\n  - run: b\n    pty: true\n    show: output\n";
        CommandExecution Output(StepStart step) => step.Command.Cmd == "b" ? Pty((0, "B\r\n")) : Pipe("A\n");
        var first = Record($"settings:\n  typing-speed: 0.05\n{settings}", Output);
        var again = Record($"settings:\n  typing-speed: 0.00508326\n{settings}", Output, first.Cast);
        return Expect(again.Result.Same.Count == 2, $"same {again.Result.Same.Count}")
            & Expect(!EventLines(again.Cast).Any(static line => line.StartsWith("[-", StringComparison.Ordinal)), string.Join("\n", EventLines(again.Cast).TakeLast(4)));
    }

    internal static bool OpenSequencesEndWithTheirStep()
    {
        // An escape sequence that a step leaves open must not reach the next step's text, in either recording.
        var ok = true;
        foreach (var (unfinished, kind) in new[] { ("x\u001b", "\n    stream-output: true"), ("x\u001b]0;title", "\n    stream-output: true"), ("x\u001b]0;open", ""), ("x\u001b\u001b]0;t\u0007", "") })
        {
            var yaml = $"steps:\n  - run: unfinished{kind}\n  - run: echo after\n  - run: done\n";
            CommandExecution Output(StepStart step) => step.Command.Cmd switch
            {
                "unfinished" => Pipe(unfinished),
                "done" => Pipe("done\u0007\n"),
                _ => Pipe("after\n"),
            };
            var first = Record(yaml, Output);
            var again = Record(yaml, Output, first.Cast);
            ok &= Expect(again.Cast == first.Cast, $"{unfinished}: {FirstDifference(first.Cast, again.Cast)}");
            ok &= Expect(again.Result.Same.Count == 3, $"{unfinished}: same {again.Result.Same.Count}, changed {again.Result.Changed.Count}");
            ok &= Expect(first.Cast.Contains("after\\r\\n", StringComparison.Ordinal), $"{unfinished}: the next step's output is gone");
        }

        return ok;
    }

    internal static bool LongSequenceBeforeACommentKeepsTheLine()
    {
        // The comment's line break looks back a limited distance, and must not stop inside text the cast does not hold.
        var sequence = "hi\n\u001b]52;c;" + new string('A', 5000) + "\u0007";
        const string yaml = "steps:\n  - run: clip\n    stream-output: true\n  - run: echo next\n    name: note\n";
        CommandExecution Output(StepStart step) => step.Command.Cmd == "clip" ? Pipe(sequence) : Pipe("next\n");
        var first = Record(yaml, Output);
        var again = Record(yaml, Output, first.Cast);
        return Expect(again.Cast == first.Cast, $"the cast differs: {FirstDifference(first.Cast, again.Cast)}");
    }

    internal static bool KeptEmptyFirstStepKeepsTheOthersInPlace()
    {
        const string yaml = "steps:\n  - run: first\n    show: output\n    keep-output: true\n    stream-output: true\n  - echo second\n";
        CommandExecution Output(StepStart step, string text) => step.Command.Cmd == "first" ? Pipe(text) : Pipe("second\n");
        var title = Record(yaml, step => Output(step, "\u001b]0;t\u0007"));
        var kept = Record(yaml, step => Output(step, "new\n"), title.Cast);
        var empty = Record(yaml, step => Output(step, ""));
        var titleNow = Record(yaml, step => Output(step, "\u001b]0;t\u0007"), empty.Cast);
        return Expect(kept.Result.Kept.Count == 1 && kept.Cast == title.Cast, $"earlier title only: {FirstDifference(title.Cast, kept.Cast)}")
            & Expect(titleNow.Cast == empty.Cast, $"earlier nothing: {FirstDifference(empty.Cast, titleNow.Cast)}");
    }

    internal static bool OutputsFileKeepsTheMaskedRun()
    {
        var step = RecordedStep.Pipe("login hunter2", 0, [new PipeStreamChunk(TimeSpan.FromMilliseconds(5), false, "ok\n")], "login *******");
        var plain = RecordedStep.Pipe("ls", 0, [new PipeStreamChunk(TimeSpan.FromMilliseconds(5), false, "ok\n")], "ls");
        using var stream = new MemoryStream();
        new StepOutputs { Scenetake = "t", Os = "linux", Shell = "bash", Width = 80, Height = 24, Steps = [step, plain] }.Write(stream);
        var json = Encoding.UTF8.GetString(stream.ToArray());
        var read = StepOutputs.Parse(json);
        return Expect(read.Steps[0].MaskedRun == "login *******", $"read {read.Steps[0].MaskedRun}")
            // A run that masking left alone adds nothing to the file, so existing outputs files stay the same.
            & Expect(read.Steps[1].MaskedRun is null && json.Split("maskedRun").Length == 2, json);
    }

    internal static bool SplitCharactersMoveWhole()
    {
        static List<(double Time, ReadOnlyMemory<byte> Data)> Chunks(params byte[][] parts) =>
            [.. parts.Select(static (part, index) => ((double)index, (ReadOnlyMemory<byte>)part))];
        static string Hex(List<(double Time, byte[] Data)> chunks) => string.Join(" ", chunks.Select(static chunk => $"{chunk.Time}:{Convert.ToHexString(chunk.Data)}"));

        // U+1F600 is four bytes, so three of them can wait for the last. A chunk of nothing but a part goes away, and the
        // parts of two such chunks in a row stay in order.
        var emoji = CastTimingPipeline.CoalesceTimedUtf8(Chunks([0x41, 0xF0, 0x9F, 0x98], [0x80, 0x42]));
        var shortChunks = CastTimingPipeline.CoalesceTimedUtf8(Chunks([0xE6], [0x97], [0xA5, 0x43]));
        // Bytes that can never become a character stay where they are, and the last chunk has nothing to wait for.
        var invalid = CastTimingPipeline.CoalesceTimedUtf8(Chunks([0x41, 0xFF], [0x42]));
        var tail = CastTimingPipeline.CoalesceTimedUtf8(Chunks([0x41], [0x42, 0xE6]));
        var continuation = CastTimingPipeline.CoalesceTimedUtf8(Chunks([0x41, 0x80], [0x42]));
        return Expect(Hex(emoji) == "0:41 1:F09F988042", Hex(emoji))
            & Expect(Hex(shortChunks) == "2:E697A543", Hex(shortChunks))
            & Expect(Hex(invalid) == "0:41FF 1:42", Hex(invalid))
            & Expect(Hex(tail) == "0:41 1:42E6", Hex(tail))
            & Expect(Hex(continuation) == "0:4180 1:42", Hex(continuation));
    }

    internal static bool UncountedMaskingLeavesTheCounts()
    {
        var masker = SecretMasker.ForScenario(ParseScenario("settings:\n  mask: [hunter2]\nsteps:\n  - echo hi\n"), environment: null)!;
        var masked = masker.MaskUncounted("say hunter2");
        var hides = masker.Hides("say hunter2");
        var hidesStars = masker.Hides(masked);
        var timing = new CastTimingSettings(0.1, 1, null, false);
        uint Hash(string waitText) => Rerecording.TimingHash(0.05, timing, false, null, true, 0.05, 0.01,
            new List<object?> { new Dictionary<object, object?> { ["wait"] = waitText } }, masker);
        return Expect(masked == "say *******", masked)
            & Expect(hides && !hidesStars, $"hides {hides}, hides its own stars {hidesStars}")
            & Expect(masker.Counts.Count == 0, "what no recording shows was counted")
            // A secret in the keys reaches the hash only as stars, like every other key.
            & Expect(Hash("hunter2") == Hash("*******"), "a masked text in the keys is in the hash");
    }

    internal static bool NamedStepsAreCompared()
    {
        // A name comment adds a marker event: the earlier cast's event count holds only when markers are read too.
        const string yaml = "steps:\n  - run: hello\n    name: greeting\n    pty: true\n  - run: world\n    pty: true\n";
        var first = Record(yaml, step => Pty((0.1, step.Command.Cmd + "\r\n")));
        var again = Record(yaml, step => Pty((0.1041, step.Command.Cmd + "\r\n")), first.Cast);
        return Expect(first.Cast.Contains("\"m\",", StringComparison.Ordinal), "the cast has no marker")
            & Expect(again.Result.Same.Count == 2, $"same {again.Result.Same.Count}")
            & Expect(again.Cast == first.Cast, $"the cast differs: {FirstDifference(first.Cast, again.Cast)}");
    }

    internal static bool KeepOutputWithoutAnEarlierStepIsSaid()
    {
        const string yaml = "steps:\n  - run: old\n";
        var first = Record(yaml, _ => Pipe("old\n"));
        var again = Record("steps:\n  - run: fresh\n    keep-output: true\n  - run: other\n", _ => Pipe("now\n"), first.Cast);
        return Expect(again.Result.KeepMissing is [{ Number: 1 }], $"missing {again.Result.KeepMissing.Count}")
            & Expect(again.Result.Added is [{ Number: 2 }], $"added {again.Result.Added.Count}");
    }

    internal static bool StepIdentityAndTimingKeys()
    {
        var ok = true;
        var ids = new[]
        {
        Rerecording.StepId(false, false, StepShow.All, "run"),
        Rerecording.StepId(true, false, StepShow.All, "run"),
        Rerecording.StepId(true, true, StepShow.All, "run"),
        Rerecording.StepId(false, false, StepShow.Output, "run"),
        Rerecording.StepId(false, false, StepShow.All, "other"),
    };
        ok &= Expect(ids.Distinct().Count() == ids.Length, "a mode, a show value, or a run text does not change the id");
        ok &= Expect(Rerecording.StepId(false, false, StepShow.All, "a\nb") == Rerecording.StepId(false, false, StepShow.All, "a\r\nb"), "line endings change the id");

        var timing = new CastTimingSettings(0.1, 1, null, false);
        uint Hash(double duration = 0.05, CastTimingSettings? settings = null, bool normalize = false, double? timeout = null, bool types = false, double speed = 0.05, double jitter = 0.01, object? keys = null) =>
            Rerecording.TimingHash(duration, settings ?? timing, normalize, timeout, types, speed, jitter, keys, masker: null);
        static List<object?> Keys(string key, object value) => [new Dictionary<object, object?> { [key] = value }];
        var plain = Hash();
        foreach (var (name, other) in new (string, uint)[]
        {
        ("execution-duration", Hash(duration: 0.2)),
        ("silence-threshold", Hash(settings: timing with { SilenceThreshold = 0.2 })),
        ("max-idle", Hash(settings: timing with { MaxIdle = 2 })),
        ("max-duration", Hash(settings: timing with { MaxDuration = 2 })),
        ("stream-output", Hash(settings: timing with { StreamOutput = true })),
        ("normalize-typing", Hash(normalize: true)),
        ("timeout", Hash(timeout: 3)),
        })
        {
            ok &= Expect(other != plain, $"{name} does not change the timing hash");
        }

        // Typing keys time the output only in a step that types them.
        ok &= Expect(Hash(speed: 0.2) == plain && Hash(jitter: 0.2) == plain && Hash(keys: Keys("key", "enter")) == plain, "typing counts in a step that types nothing");
        var typing = Hash(types: true, keys: Keys("type", "hunter2"));
        ok &= Expect(Hash(types: true, speed: 0.2, keys: Keys("type", "hunter2")) != typing, "typing-speed");
        ok &= Expect(Hash(types: true, jitter: 0.2, keys: Keys("type", "hunter2")) != typing, "typing-jitter");
        ok &= Expect(Hash(types: true, keys: Keys("key", "enter")) != typing, "another key");
        ok &= Expect(Hash(types: true, keys: Keys("type", "longer text")) != typing, "another length of typed text");
        // The published hash says nothing about what was typed, only how long it was.
        ok &= Expect(Hash(types: true, keys: Keys("type", "hunter3")) == typing, "the typed text itself is in the hash");
        return ok;
    }

    internal static bool MaskedRunTextIsNotHashed()
    {
        // The cast is published without the secret, so its tag must not let anyone test a guess of it.
        const string steps = "steps:\n  - run: login --password hunter2\n";
        var first = Record(steps, _ => Pipe("ok\n"));
        var masked = Record("settings:\n  mask: [hunter2]\n" + steps, _ => Pipe("ok\n"));
        var other = Record("settings:\n  mask: [letmein]\nsteps:\n  - run: login --password letmein\n", _ => Pipe("ok\n"));
        var tag = static (string cast) => cast[cast.IndexOf("st:steps=", StringComparison.Ordinal)..].Split('"')[0].Split(' ')[1].Split(':')[0];
        var expected = Rerecording.StepId(false, false, StepShow.All, "login --password *******");
        return Expect(tag(masked.Cast) != tag(first.Cast), "the id is the hash of the unmasked text")
            & Expect(tag(masked.Cast) == expected.ToString("x16", System.Globalization.CultureInfo.InvariantCulture), $"id {tag(masked.Cast)}")
            & Expect(masked.Result.Slots[0].Run == "login --password *******", masked.Result.Slots[0].Run)
            // Both secrets are seven characters: the two casts cannot be told apart by this id.
            & Expect(tag(other.Cast) == tag(masked.Cast), "the id depends on the secret");
    }

    internal static bool EditedCastIsNotCompared()
    {
        const string yaml = "steps:\n  - run: one\n    pty: true\n  - run: two\n    pty: true\n    keep-output: true\n";
        var cast = Record(yaml, step => Pty((0.1, step.Command.Cmd + "\r\n"))).Cast;
        var lines = cast.Split('\n');
        var scenario = ParseScenario(yaml);
        string? Problem(string[] edited) => PreviousCast.Parse(edited, Cols(scenario), Rows(scenario), out var problem) is null ? problem : "(read)";

        var outputLine = Array.FindIndex(lines, static line => line.Contains("one\\r\\n", StringComparison.Ordinal));
        var slower = (string[])lines.Clone();
        slower[outputLine] = "[9.000,\"o\",\"one\\r\\n\"]";
        var shorter = lines.Where((_, index) => index != outputLine).ToArray();
        var retyped = (string[])lines.Clone();
        retyped[outputLine] = retyped[outputLine].Replace("one", "ONE", StringComparison.Ordinal);
        const string expected = "its step ranges do not match its events";
        return Expect(Problem(lines) == "(read)", $"the cast as written: {Problem(lines)}")
            & Expect(Problem(slower) == expected, $"a changed interval: {Problem(slower)}")
            & Expect(Problem(shorter) == expected, $"a removed event: {Problem(shorter)}")
            // A corrected word moves nothing, so the ranges still fit.
            & Expect(Problem(retyped) == "(read)", $"changed text: {Problem(retyped)}");
    }

    internal static bool EarlierCastProblems()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"scenetake-rerecord-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            string? Problem(string text)
            {
                var path = Path.Combine(directory, "earlier.cast");
                File.WriteAllText(path, text);
                return PreviousCast.TryRead(path, RenderSettingsResolver.DefaultTerminalCols, RenderSettingsResolver.DefaultTerminalRows, out var problem) is null ? problem : "(read)";
            }

            var valid = Record(ThreeSteps, step => ThreeStepsOutput(step, 0)).Cast;
            var halfPair = valid.Replace("[0.050,\"o\",\"h\"]", "[0.050,\"o\",\"\\ud83d\"]", StringComparison.Ordinal);
            return Expect(PreviousCast.TryRead(Path.Combine(directory, "missing.cast"), 80, 24, out var missing) is null && missing is null, $"a missing file: {missing}")
                & Expect(Problem(valid) == "(read)", $"a valid cast: {Problem(valid)}")
                // Valid JSON whose string has no text: the recording goes on, and says so.
                & Expect(halfPair != valid && Problem(halfPair) is { } surrogate && surrogate.Contains("not a cast", StringComparison.Ordinal), $"half a surrogate pair: {Problem(halfPair)}")
                & Expect(Problem("{\"version\":3,\"term\":{\"cols\":80,\"rows\":24}}\n[0.5,\"o\",\"$ \"]\n") is { } noTag && noTag.Contains("no step ranges", StringComparison.Ordinal), "a cast without the tag")
                & Expect(Problem("{\"version\":2,\"width\":80,\"height\":24}\n") is { } v2 && v2.Contains("v3", StringComparison.Ordinal), "a v2 cast")
                & Expect(Problem("not json\n") is { } garbage && garbage.Contains("not a cast", StringComparison.Ordinal), "a file that is not JSON")
                & Expect(Problem(valid.Replace("[0.050,\"o\",\"h\"]", "[0.050,\"i\",\"h\"]", StringComparison.Ordinal)) is { } input && input.Contains("does not write", StringComparison.Ordinal), $"an input event: {Problem(valid.Replace("[0.050,\"o\",\"h\"]", "[0.050,\"i\",\"h\"]", StringComparison.Ordinal))}");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    internal static bool KeepOutputKeyRules()
    {
        string? Error(string steps) => FindStepKeyError(ParseScenario("steps:\n" + steps));
        var unknown = FindUnknownScenarioKeys("steps:\n  - run: date\n    keep-output: true\n");
        return Expect(unknown.Count == 0, $"unknown keys: {string.Join("; ", unknown)}")
            & Expect(Error("  - run: date\n    keep-output: true\n") is null, "a shown step")
            & Expect(Error("  - run: date\n    show: none\n    keep-output: true\n") is { } none && none.Contains("'keep-output' has no effect with 'show: none'", StringComparison.Ordinal), $"show: none: {Error("  - run: date\n    show: none\n    keep-output: true\n")}")
            & Expect(Error("  - run: serve\n    background: true\n    keep-output: true\n") is { } background && background.Contains("'keep-output' does not apply to a 'background: true' step", StringComparison.Ordinal), "background")
            & Expect(Error("  - clear: true\n    keep-output: true\n") is not null, "clear");
    }

    internal static bool DescribeSaysWhatChanged()
    {
        StepSlot Slot(int number, string run, bool keep = false) => new(number, run, (ulong)number, 0, true, keep, 0, 0, 0, 0, 0);
        Rerecorder Result(bool hadPrevious, params StepSlot[] slots)
        {
            var result = new Rerecorder(hadPrevious ? EmptyEarlierCast() : null, masker: null);
            result.Slots.AddRange(slots);
            return result;
        }

        var result = Result(true, Slot(1, "ls"), Slot(2, "date", keep: true), Slot(3, "npm test"), Slot(4, "uuidgen", keep: true), Slot(5, "new one"), Slot(6, "token", keep: true), Slot(7, "draw", keep: true));
        result.Same.Add(Slot(1, "ls"));
        result.Kept.Add(Slot(2, "date", keep: true));
        result.Changed.AddRange([Slot(3, "npm test"), Slot(6, "token", keep: true), Slot(7, "draw", keep: true)]);
        result.Added.Add(Slot(5, "new one"));
        result.KeepMissing.Add(Slot(4, "uuidgen", keep: true));
        result.KeepMasked.Add(Slot(6, "token", keep: true));
        result.KeepMoved.Add(Slot(7, "draw", keep: true));
        var lines = Rerecording.Describe(result, problem: null, "demo.cast");
        var same = Result(true, Slot(1, "ls"));
        same.Same.Add(Slot(1, "ls"));
        var allSame = Rerecording.Describe(same, null, "demo.cast");
        var first = Rerecording.Describe(Result(false, Slot(1, "date", keep: true)), null, "demo.cast");
        var quiet = Rerecording.Describe(Result(false, Slot(1, "ls")), null, "demo.cast");
        var problem = Rerecording.Describe(Result(false, Slot(1, "ls")), "it has no step ranges: an earlier scenetake or another tool wrote it", "demo.cast");
        return Expect(lines is [
                "Compared with the earlier cast: 1 of 7 steps printed the same, 1 kept the earlier output (keep-output), 3 changed, 2 are new",
                "  changed: step 3: npm test",
                "  changed: step 6: token",
                "  changed: step 7: draw",
                "  new: step 5: new one",
                "Note: step 4 sets keep-output, but the earlier cast has no output of it, so it shows what it printed now: uuidgen",
                "Note: step 6 sets keep-output, but its earlier output holds text that is masked now, so it shows what it printed now: token",
                "Note: step 7 sets keep-output, but it starts at another place on the screen than its earlier output did, so it shows what it printed now: draw"],
                string.Join("\n", lines))
            & Expect(allSame is ["Compared with the earlier cast: all 1 step printed the same"], string.Join("\n", allSame))
            & Expect(first is ["Note: keep-output: there is no earlier cast at demo.cast yet, so every step shows what it printed now."], string.Join("\n", first))
            & Expect(quiet.Count == 0, string.Join("\n", quiet))
            & Expect(problem is ["Note: the earlier demo.cast was not compared with this recording: it has no step ranges: an earlier scenetake or another tool wrote it. The next recording compares with this one."], string.Join("\n", problem));
    }

    internal static bool TryGetBinary(out string bin)
    {
        bin = TestEnv.Binary;
        return true;
    }

    internal static (int ExitCode, string Stderr) RunScenetake(string bin, string directory, params string[] args)
    {
        var psi = new ProcessStartInfo(bin)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = directory,
        };
        foreach (var arg in args)
            psi.ArgumentList.Add(arg);

        using var process = Process.Start(psi)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(TimeSpan.FromSeconds(120)))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("scenetake ran for over 120 s");
        }

        return (process.ExitCode, stdout.Result + stderr.Result);
    }

    // Runs the test in a new folder with demo.yaml, and removes the folder after.
    internal static bool WithScenario(string yaml, Func<string, string, bool> test)
    {
        if (!TryGetBinary(out var bin))
            return true;

        var directory = Path.Combine(Path.GetTempPath(), $"scenetake-rerecord-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, "demo.yaml"), yaml);
            return test(bin, directory);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    internal static bool IntegrationRecordingAgainGivesSameFiles(string repoRoot) => WithScenario("""
    shell: bash
    steps:
      - run: printf 'hello\n'; sleep 0.3; printf 'world\n'
        pty: true
      - run: echo "random $RANDOM$RANDOM"
        pty: true
        keep-output: true
      - echo plain
    """, (bin, directory) =>
    {
        var first = RunScenetake(bin, directory, "--format", "svg", "demo.yaml", "demo");
        var cast = File.ReadAllText(Path.Combine(directory, "demo.cast"));
        var svg = File.ReadAllText(Path.Combine(directory, "demo.svg"));
        var again = RunScenetake(bin, directory, "--format", "svg", "demo.yaml", "demo");
        var castAgain = File.ReadAllText(Path.Combine(directory, "demo.cast"));
        var svgAgain = File.ReadAllText(Path.Combine(directory, "demo.svg"));
        return Expect(first.ExitCode == 0 && again.ExitCode == 0, $"exit {first.ExitCode}, {again.ExitCode}\n{first.Stderr}\n{again.Stderr}")
            && Expect(first.Stderr.Contains("Note: keep-output: there is no earlier cast at demo.cast yet", StringComparison.Ordinal), first.Stderr)
            & Expect(again.Stderr.Contains("Compared with the earlier cast: 2 of 3 steps printed the same, 1 kept the earlier output (keep-output)", StringComparison.Ordinal), again.Stderr)
            & Expect(castAgain == cast, $"the cast differs: {FirstDifference(cast, castAgain)}")
            & Expect(svgAgain == svg, "the SVG differs");
    });

    internal static bool IntegrationChangedAndKeptSteps(string repoRoot) => WithScenario("""
    shell: bash
    steps:
      - run: printf 'same\n'
        pty: true
      - echo "changes $RANDOM$RANDOM$RANDOM"
    """, (bin, directory) =>
    {
        var first = RunScenetake(bin, directory, "demo.yaml", "demo");
        var cast = File.ReadAllText(Path.Combine(directory, "demo.cast"));
        var again = RunScenetake(bin, directory, "demo.yaml", "demo");
        var castAgain = File.ReadAllText(Path.Combine(directory, "demo.cast"));
        var changedLines = EventLines(cast).Zip(EventLines(castAgain)).Where(static pair => pair.First != pair.Second).ToList();
        return Expect(first.ExitCode == 0 && again.ExitCode == 0, $"exit {first.ExitCode}, {again.ExitCode}\n{first.Stderr}\n{again.Stderr}")
            && Expect(!first.Stderr.Contains("Compared with", StringComparison.Ordinal), first.Stderr)
            & Expect(again.Stderr.Contains("Compared with the earlier cast: 1 of 2 steps printed the same, 1 changed", StringComparison.Ordinal)
                && again.Stderr.Contains("  changed: step 2: echo \"changes $RANDOM$RANDOM$RANDOM\"", StringComparison.Ordinal), again.Stderr)
            // Only the changed output: the PTY step before it keeps its timing.
            & Expect(changedLines.Count == 1 && changedLines[0].First.Contains("changes ", StringComparison.Ordinal), $"changed lines: {string.Join(" | ", changedLines)}");
    });

    internal static bool IntegrationOutputsFileReplaysToTheCast(string repoRoot) => WithScenario("""
    shell: bash
    steps:
      - run: printf 'one\n'; sleep 0.2; printf 'two\n'
        pty: true
      - run: echo "random $RANDOM$RANDOM"
        pty: true
        keep-output: true
      - echo plain
    """, (bin, directory) =>
    {
        var plain = RunScenetake(bin, directory, "demo.yaml", "demo");
        var earlier = File.ReadAllText(Path.Combine(directory, "demo.cast"));
        var withOutputs = RunScenetake(bin, directory, "--save-outputs", "demo.outputs.json", "demo.yaml", "demo");
        var cast = File.ReadAllText(Path.Combine(directory, "demo.cast"));
        // The file goes to a new path, so nothing is compared: the outputs alone must give the cast.
        var replay = RunScenetake(bin, directory, "--replay", "demo.outputs.json", "demo.yaml", "replayed");
        var replayed = File.ReadAllText(Path.Combine(directory, "replayed.cast"));
        var randomLine = static (string text) => EventLines(text).First(static line => line.Contains("random ", StringComparison.Ordinal) && !line.Contains("$RANDOM", StringComparison.Ordinal));
        return Expect(plain.ExitCode == 0 && withOutputs.ExitCode == 0 && replay.ExitCode == 0, $"exit {plain.ExitCode}, {withOutputs.ExitCode}, {replay.ExitCode}\n{withOutputs.Stderr}\n{replay.Stderr}")
            && Expect(withOutputs.Stderr.Contains("Note: the earlier demo.cast was not compared with this recording: an outputs file keeps what each step prints now, so the cast does too.", StringComparison.Ordinal), withOutputs.Stderr)
            & Expect(!withOutputs.Stderr.Contains("The next recording compares", StringComparison.Ordinal), withOutputs.Stderr)
            // keep-output has nothing to hold on to: the step shows this run's random number.
            & Expect(randomLine(cast) != randomLine(earlier), "the step kept the earlier output")
            & Expect(replayed == cast, $"the replay differs: {FirstDifference(cast, replayed)}");
    });

    internal static bool IntegrationReplayFindsStepsWithExportedSecrets(string repoRoot) => WithScenario("""
    shell: bash
    pre:
      - export DEMO_API_TOKEN=exportedsecret9
    steps:
      - run: echo "uses exportedsecret9" > /dev/null; echo fetched
        show: output
      - echo plain
      - echo exportedsecret9 > /dev/null
    """, (bin, directory) =>
    {
        // Only the recording knows the secret, which pre exported, so a replay must take the masked text from the file:
        // to find the step, and to type the last one as the recording did.
        var recorded = RunScenetake(bin, directory, "--save-outputs", "demo.outputs.json", "demo.yaml", "demo");
        var cast = File.ReadAllText(Path.Combine(directory, "demo.cast"));
        var replay = RunScenetake(bin, directory, "--replay", "demo.outputs.json", "demo.yaml", "other");
        var onto = RunScenetake(bin, directory, "--replay", "demo.outputs.json", "demo.yaml", "demo");
        // The cast types one character per event, so its output events are joined before looking for the secret.
        var shown = string.Concat(cast.Split('\n').Skip(1).Where(static line => line.StartsWith('['))
            .Select(static line => System.Text.Json.JsonDocument.Parse(line).RootElement)
            .Where(static ev => ev[1].GetString() == "o").Select(static ev => ev[2].GetString()));
        return Expect(recorded.ExitCode == 0 && replay.ExitCode == 0 && onto.ExitCode == 0, $"exit {recorded.ExitCode}, {replay.ExitCode}, {onto.ExitCode}\n{recorded.Stderr}\n{replay.Stderr}")
            && Expect(!shown.Contains("exportedsecret9", StringComparison.Ordinal) && shown.Contains("echo *************** > /dev/null", StringComparison.Ordinal), $"the recording types the secret: {shown}")
            & Expect(File.ReadAllText(Path.Combine(directory, "other.cast")) == cast, $"the replay differs from the recording: {FirstDifference(cast, File.ReadAllText(Path.Combine(directory, "other.cast")))}")
            & Expect(onto.Stderr.Contains("all 3 steps printed the same", StringComparison.Ordinal), onto.Stderr)
            & Expect(!onto.Stderr.Contains("new: step", StringComparison.Ordinal), onto.Stderr);
    });

    private const string ThreeSteps = """
    steps:
      - run: hello
        pty: true
      - run: count
        pty: true
      - echo three
    """;
}
