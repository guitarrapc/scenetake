using static Scenetake.Core.Tests.RerecordCases;

namespace Scenetake.Core.Tests;

// The pauses around a step (.github/docs/spec_scenario.md → settings): a step's comment and prompt show as soon as the
// step before has paused for its post-delay, and its own pre-delay passes with the prompt on the screen, before the
// typing. The final screen holds the closing prompt. The recordings come from the composer and the cast writer, and
// are read back as the renderers read them.

public sealed class StepPauseTests
{
    [Test]
    public async Task PreDelayPassesWithThePrompt() => await Assert.That(StepPauseCases.PreDelayPassesWithThePrompt()).IsTrue();

    [Test]
    public async Task FinalScreenHoldsTheClosingPrompt() => await Assert.That(StepPauseCases.FinalScreenHoldsTheClosingPrompt()).IsTrue();

    [Test]
    public async Task EndHoldCountsFromTheClosingPrompt() => await Assert.That(StepPauseCases.EndHoldCountsFromTheClosingPrompt()).IsTrue();

    [Test]
    public async Task OutputStepPausesBeforeItsOutput() => await Assert.That(StepPauseCases.OutputStepPausesBeforeItsOutput()).IsTrue();

    [Test]
    public async Task CardShowsAfterItsPreDelay() => await Assert.That(StepPauseCases.CardShowsAfterItsPreDelay()).IsTrue();
}

internal static class StepPauseCases
{
    // Step two sets a pre-delay of its own.
    private const string TwoSteps = """
        settings:
          typing-speed: 0.1
          typing-jitter: 0
          pre-delay: 0.4
          post-delay: 0.7
        steps:
          - name: one
            run: echo one
          - name: two
            run: echo two
            pre-delay: 1.5
        """;

    // The cast stores each interval in whole milliseconds.
    private const double Tolerance = 0.0015;

    private static CommandExecution Echo(StepStart step) => Pipe(step.Command.Cmd["echo ".Length..] + "\n");

    internal static bool PreDelayPassesWithThePrompt()
    {
        var events = Read(TwoSteps).Events;
        var commentOne = Index(events, "\u001b[36m# one\u001b[0m\r\n");
        var outputOne = Index(events, "one\r\n");
        var commentTwo = Index(events, "\u001b[36m# two\u001b[0m\r\n");
        // The comment, the prompt 0.05 s later, then pre-delay and 0.05 s before the first character.
        return Expect(Near(events[commentOne].Time, 0.5), $"the first comment is at {events[commentOne].Time}, not 0.5")
            && Expect(events[commentOne + 1].Data == "$ " && Near(Gap(events, commentOne + 1), 0.05), "the first prompt does not follow its comment")
            && Expect(Near(Gap(events, commentOne + 2), 0.45), $"step one types {Gap(events, commentOne + 2):F3} s after its prompt, not after its pre-delay 0.4")
            // Step two's comment comes after step one's post-delay, and its own pre-delay passes after its prompt.
            && Expect(Near(events[commentTwo].Time - events[outputOne].Time, 0.7), $"the next comment is {events[commentTwo].Time - events[outputOne].Time:F3} s after the output, not post-delay 0.7")
            && Expect(events[commentTwo + 1].Data == "$ " && Near(Gap(events, commentTwo + 1), 0.05), "the next prompt does not follow its comment")
            && Expect(Near(Gap(events, commentTwo + 2), 1.55), $"step two types {Gap(events, commentTwo + 2):F3} s after its prompt, not after its own pre-delay 1.5");
    }

    internal static bool FinalScreenHoldsTheClosingPrompt()
    {
        // The closing prompt shows after the last step's post-delay. The final screen stays 2 x (post-delay + pre-delay)
        // after the last output: an event that draws nothing ends the recording, and the render holds it as long again.
        var recording = Read(TwoSteps);
        var events = recording.Events;
        var outputTwo = Index(events, "two\r\n");
        return Expect(events.Count == outputTwo + 3, $"{events.Count - outputTwo - 1} events after the last output, expected the prompt and the end")
            && Expect(events[outputTwo + 1].Data == "$ " && Near(Gap(events, outputTwo + 1), 0.7), $"the closing prompt is {Gap(events, outputTwo + 1):F3} s after the output, not post-delay 0.7")
            && Expect(PlaybackLength.IsSilent(events[^1]), $"the last event draws: {events[^1].Data}")
            && Expect(Near(recording.LoopDuration - events[outputTwo].Time, 2 * (0.7 + 1.5)), $"the final screen stays {recording.LoopDuration - events[outputTwo].Time:F3} s, expected 4.4");
    }

    internal static bool EndHoldCountsFromTheClosingPrompt()
    {
        // An end-hold replaces the default hold: it starts when the closing prompt shows, without the event after it,
        // so end-hold 0 starts again at once. At 2x it is still seconds of the output.
        var recording = Read(TwoSteps);
        var closing = recording.Events[^2].Time;
        double Loop(double? endHold, double speed = 1)
        {
            var settings = recording.RenderSettings with { EndHold = endHold, Speed = speed };
            double? loop = recording.LoopDuration;
            var events = RenderSettingsResolver.ApplySpeed(recording.Events, settings, ref loop);
            return RenderSettingsResolver.ResolveLoopDuration(loop ?? TerminalReplay.ComputeLoopDuration(events), events, settings);
        }

        return Expect(recording.Events[^2].Data == "$ ", "the event before the last is not the closing prompt")
            && Expect(Near(Loop(null), recording.LoopDuration), $"the default hold moved: {Loop(null):F3}, recorded {recording.LoopDuration:F3}")
            && Expect(Near(Loop(0), closing), $"end-hold 0 loops at {Loop(0):F3}, expected the closing prompt at {closing:F3}")
            && Expect(Near(Loop(3), closing + 3), $"end-hold 3 loops at {Loop(3):F3}, expected {closing + 3:F3}")
            && Expect(Near(Loop(3, 2), closing / 2 + 3), $"end-hold 3 at 2x loops at {Loop(3, 2):F3}, expected {closing / 2 + 3:F3}");
    }

    internal static bool OutputStepPausesBeforeItsOutput()
    {
        // show: output types nothing: pre-delay passes after its comment, before the output.
        const string yaml = """
            settings:
              pre-delay: 0.6
              execution-duration: 0.05
            steps:
              - name: logs
                run: echo ready
                show: output
            """;
        var events = Read(yaml).Events;
        var comment = Index(events, "\u001b[36m# logs\u001b[0m\r\n");
        var output = Index(events, "ready\r\n");
        return Expect(output == comment + 1, "something is drawn between the comment and the output")
            && Expect(Near(Gap(events, output), 0.05 + 0.6 + 0.05), $"the output is {Gap(events, output):F3} s after the comment, expected 0.7");
    }

    internal static bool CardShowsAfterItsPreDelay()
    {
        // A card shows after its pre-delay and stays for its post-delay, until the next step clears it.
        const string yaml = """
            settings:
              post-delay: 0.7
            steps:
              - echo one
              - card: Hello
                pre-delay: 0.6
                post-delay: 2
              - echo two
            """;
        var events = Read(yaml).Events;
        var output = Index(events, "one\r\n");
        var card = -1;
        for (var i = 0; i < events.Count && card < 0; i++)
        {
            if (events[i].Data.StartsWith("\u001b[?25l" + ScenarioCast.ClearScreen, StringComparison.Ordinal))
                card = i;
        }

        return Expect(card == output + 1, "the card does not follow the output")
            && Expect(Near(Gap(events, card), 0.7 + 0.6), $"the card shows {Gap(events, card):F3} s after the output, expected post-delay 0.7 and its pre-delay 0.6")
            && Expect(events[card + 1].Data.StartsWith(ScenarioCast.ClearScreen, StringComparison.Ordinal) && Near(Gap(events, card + 1), 2), $"the card stays {Gap(events, card + 1):F3} s, expected its post-delay 2");
    }

    private static CastRecording Read(string yaml) => CastReader.ReadText(Record(yaml, Echo).Cast);

    private static double Gap(IReadOnlyList<CastEvent> events, int index) => events[index].Time - (index > 0 ? events[index - 1].Time : 0);

    private static bool Near(double actual, double expected) => Math.Abs(actual - expected) < Tolerance;

    private static int Index(IReadOnlyList<CastEvent> events, string data)
    {
        for (var i = 0; i < events.Count; i++)
        {
            if (events[i].Data == data)
                return i;
        }

        throw new InvalidOperationException($"no event {data}");
    }
}
