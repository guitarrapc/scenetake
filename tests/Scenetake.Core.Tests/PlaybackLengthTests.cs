using static Scenetake.Core.Tests.RerecordCases;

namespace Scenetake.Core.Tests;

// render.length (.github/docs/spec_svg.md → Length): a recording plays in at most that many seconds. Waits in a
// command's output shrink first, then pauses, then typing, each down to a floor. Motion and structural gaps stay, a
// shorter recording stays as it is, and one that cannot fit stops at its floors. The recordings here come from the
// composer and the cast writer, as the CLI makes them, and are read back as the renderers read them.

public sealed class PlaybackLengthTests
{
    [Test]
    public async Task ShorterRecordingStaysAsItIs() => await Assert.That(PlaybackLengthCases.ShorterRecordingStaysAsItIs()).IsTrue();

    [Test]
    public async Task WaitsShrinkFirst() => await Assert.That(PlaybackLengthCases.WaitsShrinkFirst()).IsTrue();

    [Test]
    public async Task PausesShrinkBeforeTyping() => await Assert.That(PlaybackLengthCases.PausesShrinkBeforeTyping()).IsTrue();

    [Test]
    public async Task StopsAtTheFloors() => await Assert.That(PlaybackLengthCases.StopsAtTheFloors()).IsTrue();

    [Test]
    public async Task ExplicitEndHoldAndFadeStay() => await Assert.That(PlaybackLengthCases.ExplicitEndHoldAndFadeStay()).IsTrue();

    [Test]
    public async Task CastWithoutStepRangesPlaysAsRecorded() => await Assert.That(PlaybackLengthCases.CastWithoutStepRangesPlaysAsRecorded()).IsTrue();

    [Test]
    public async Task StepRangesSkipMarkers() => await Assert.That(PlaybackLengthCases.StepRangesSkipMarkers()).IsTrue();
}

internal static class PlaybackLengthCases
{
    // A step that waits 3 s with nothing drawn, between two lines of a progress bar 0.1 s apart (motion), and a step
    // that types slowly and pauses long after its output.
    private const string Scenario = """
        settings:
          typing-speed: 0.1
          typing-jitter: 0
          pre-delay: 0.5
          post-delay: 2.5
        steps:
          - name: build
            run: build
            pty: true
          - name: show
            run: echo hello
        """;

    private static CommandExecution Run(StepStart step) =>
        step.UsePty ? Pty((0.1, "[#    ]\r"), (0.2, "[##   ]\r"), (3.2, "[#####] done\r\n")) : Pipe("hello\n");

    internal static bool ShorterRecordingStaysAsItIs()
    {
        var (recording, settings) = Read(Scenario, Run);
        var natural = Played(recording, settings);
        var fitted = RenderSettingsResolver.FitLength(recording.Events, settings with { Length = natural + 5 }, recording.LoopDuration)!;
        return Expect(Same(recording.Events, fitted.Events), "the events moved")
            && Expect(Math.Abs(fitted.Played - natural) < 1e-9, $"played {fitted.Played}, natural {natural}");
    }

    internal static bool WaitsShrinkFirst()
    {
        // Two seconds less than recorded: the 3 s wait gives them, and nothing else moves.
        var (recording, settings) = Read(Scenario, Run);
        var natural = Played(recording, settings);
        var fitted = RenderSettingsResolver.FitLength(recording.Events, settings with { Length = natural - 2 }, recording.LoopDuration)!;
        var before = Gaps(recording.Events);
        var after = Gaps(fitted.Events);
        var wait = Index(recording.Events, "[#####] done\r\n");
        var changed = Enumerable.Range(0, before.Length).Where(i => Math.Abs(before[i] - after[i]) > 1e-6).ToList();
        return Expect(Math.Abs(fitted.Played - (natural - 2)) < 0.01, $"played {fitted.Played:F3}, expected {natural - 2:F3}")
            && Expect(changed.SequenceEqual([wait]), $"changed gaps at [{string.Join(", ", changed)}], expected only the wait at {wait}")
            && Expect(after[wait] >= PlaybackLength.WaitFloor - 1e-9, $"the wait went below its floor: {after[wait]}")
            && Expect(Math.Abs(after[Index(recording.Events, "[##   ]\r")] - 0.1) < 1e-6, "the motion inside the progress bar changed");
    }

    internal static bool PausesShrinkBeforeTyping()
    {
        // More than the wait can give: the wait goes to its floor, the pauses shrink to theirs, and typing gives the rest.
        var (recording, settings) = Read(Scenario, Run);
        var natural = Played(recording, settings);
        var shortest = RenderSettingsResolver.FitLength(recording.Events, settings with { Length = 1 }, recording.LoopDuration)!.Shortest;
        var typingRoom = TypingGaps(recording.Events).Sum(i => Gaps(recording.Events)[i] - PlaybackLength.TypingFloor);
        var length = shortest + typingRoom / 2;
        var fitted = RenderSettingsResolver.FitLength(recording.Events, settings with { Length = length }, recording.LoopDuration)!;
        var after = Gaps(fitted.Events);
        var wait = Index(recording.Events, "[#####] done\r\n");
        var pause = Index(recording.Events, "\u001b[36m# show\u001b[0m\r\n");
        var typing = TypingGaps(recording.Events);
        return Expect(Math.Abs(fitted.Played - length) < 0.01, $"played {fitted.Played:F3}, expected {length:F3} (natural {natural:F3})")
            && Expect(Math.Abs(after[wait] - PlaybackLength.WaitFloor) < 1e-6, $"the wait is {after[wait]}, not at its floor")
            && Expect(after[pause] < Gaps(recording.Events)[pause] && after[pause] >= ReadingFloor(1) - 1e-6, $"the pause after build is {after[pause]}")
            && Expect(typing.All(i => after[i] > PlaybackLength.TypingFloor + 1e-6 && after[i] < 0.1 - 1e-6), "typing should be between its floor and as typed");
    }

    internal static bool StopsAtTheFloors()
    {
        // A length the recording cannot keep: every kind at its floor, and a warning that names the shortest.
        var (recording, settings) = Read(Scenario, Run);
        var fitted = RenderSettingsResolver.FitLength(recording.Events, settings with { Length = 1 }, recording.LoopDuration)!;
        var after = Gaps(fitted.Events);
        var note = RenderSettingsResolver.LengthNote(recording.Events, settings with { Length = 1 }, recording.LoopDuration);
        return Expect(Math.Abs(fitted.Played - fitted.Shortest) < 1e-6, $"played {fitted.Played}, shortest {fitted.Shortest}")
            && Expect(TypingGaps(recording.Events).All(i => Math.Abs(after[i] - PlaybackLength.TypingFloor) < 1e-6), "typing is not at its floor")
            && Expect(note?.Contains($"{fitted.Played:F1}s with every wait, pause, and typed character at its shortest", StringComparison.Ordinal) == true, note ?? "no warning");
    }

    internal static bool ExplicitEndHoldAndFadeStay()
    {
        // An end-hold and a fade-out the author set are seconds of the output: the length counts them and keeps them.
        var (recording, settings) = Read(Scenario, Run);
        var held = settings with { EndHold = 2, FadeOut = 1 };
        var natural = Played(recording, held);
        var fitted = RenderSettingsResolver.FitLength(recording.Events, held with { Length = natural - 2 }, recording.LoopDuration)!;
        var loop = RenderSettingsResolver.ResolveLoopDuration(fitted.LoopDuration, fitted.Events, held);
        return Expect(Math.Abs(loop - fitted.Events[^1].Time - 3) < 1e-6, $"the hold and fade became {loop - fitted.Events[^1].Time}")
            && Expect(Math.Abs(loop - (natural - 2)) < 0.01, $"the loop is {loop:F3}, expected {natural - 2:F3}");
    }

    internal static bool CastWithoutStepRangesPlaysAsRecorded()
    {
        // A cast that another tool wrote has no st:steps: nothing tells typing from output, so it plays as recorded.
        var (recording, settings) = Read(Scenario, Run);
        var bare = settings with { StepRanges = null, Length = 3 };
        double? loop = recording.LoopDuration;
        var events = RenderSettingsResolver.ApplySpeed(recording.Events, bare, ref loop);
        var note = RenderSettingsResolver.LengthNote(recording.Events, bare, recording.LoopDuration);
        return Expect(Same(recording.Events, events), "the events moved")
            && Expect(note?.Contains("has no effect: the cast has no step ranges", StringComparison.Ordinal) == true, note ?? "no warning");
    }

    internal static bool StepRangesSkipMarkers()
    {
        // The st:steps tag counts the markers that name comments write, which the reader does not keep: each range
        // still starts at the step's output.
        var (recording, settings) = Read(Scenario, Run);
        var ranges = settings.StepRanges!;
        return Expect(ranges.Length == 4, $"ranges: [{string.Join(", ", ranges)}]")
            && Expect(recording.Events[ranges[0]].Data == "[#    ]\r" && ranges[1] == 3, $"build: {ranges[0]}+{ranges[1]}")
            && Expect(recording.Events[ranges[2]].Data == "hello\r\n" && ranges[3] == 1, $"show: {ranges[2]}+{ranges[3]}");
    }

    /// <summary>A recording as the renderers read it: composed, written as a cast with its st:steps tag, and read back.</summary>
    private static (CastRecording Recording, ResolvedRenderSettings Settings) Read(string yaml, Func<StepStart, CommandExecution> run)
    {
        var recording = CastReader.ReadText(Record(yaml, run).Cast);
        return (recording, recording.RenderSettings);
    }

    private static double Played(CastRecording recording, ResolvedRenderSettings settings) =>
        RenderSettingsResolver.ResolveLoopDuration(recording.LoopDuration, recording.Events, settings);

    private static double ReadingFloor(int lines) =>
        Math.Min(PlaybackLength.ReadingMax, PlaybackLength.ReadingBase + PlaybackLength.ReadingPerLine * lines) + PlaybackLength.BeforeTyping;

    /// <summary>The gap leading to each event.</summary>
    private static double[] Gaps(IReadOnlyList<CastEvent> events)
    {
        var gaps = new double[events.Count];
        var previous = 0.0;
        for (var i = 0; i < events.Count; i++)
        {
            gaps[i] = events[i].Time - previous;
            previous = events[i].Time;
        }

        return gaps;
    }

    /// <summary>The gaps between typed characters, and from the last one to Enter: not the prompt's gap to the first.</summary>
    private static List<int> TypingGaps(IReadOnlyList<CastEvent> events)
    {
        var typing = new List<int>();
        for (var i = 1; i < events.Count; i++)
        {
            var typed = events[i].Data.Length == 1 && !char.IsControl(events[i].Data[0]);
            var previousTyped = events[i - 1].Data.Length == 1 && !char.IsControl(events[i - 1].Data[0]);
            if ((typed || events[i].Data == "\r\n") && previousTyped)
                typing.Add(i);
        }

        return typing;
    }

    private static int Index(IReadOnlyList<CastEvent> events, string data)
    {
        for (var i = 0; i < events.Count; i++)
        {
            if (events[i].Data == data)
                return i;
        }

        throw new InvalidOperationException($"no event {data}");
    }

    private static bool Same(IReadOnlyList<CastEvent> a, IReadOnlyList<CastEvent> b) =>
        a.Count == b.Count && Enumerable.Range(0, a.Count).All(i => a[i].Time == b[i].Time && a[i].Data == b[i].Data);

    private static bool Expect(bool condition, string message)
    {
        if (!condition)
            Console.Error.WriteLine($"  {message}");
        return condition;
    }
}
