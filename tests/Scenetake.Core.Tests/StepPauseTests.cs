using static Scenetake.Core.Tests.RerecordCases;

namespace Scenetake.Core.Tests;

// The pauses around a step (.github/docs/spec_scenario.md → settings): a step's comment and prompt show as soon as the
// step before has paused for its post-delay, and its own pre-delay passes with the prompt on the screen, before the
// typing. The final screen holds the closing prompt. The recordings come from the composer and the cast writer, and
// are read back as the renderers read them.

public sealed class StepPauseTests
{
    [Test]
    public async Task StepTimingValuesReadAsSettingsDo() => await Assert.That(StepPauseCases.StepTimingValuesReadAsSettingsDo()).IsTrue();

    [Test]
    public async Task PreDelayPassesWithThePrompt() => await Assert.That(StepPauseCases.PreDelayPassesWithThePrompt()).IsTrue();

    [Test]
    public async Task FinalScreenIsTheLastOutput() => await Assert.That(StepPauseCases.FinalScreenIsTheLastOutput()).IsTrue();

    [Test]
    public async Task EndPromptDrawsTheClosingPrompt() => await Assert.That(StepPauseCases.EndPromptDrawsTheClosingPrompt()).IsTrue();

    [Test]
    public async Task EndHoldCountsFromTheFinalScreen() => await Assert.That(StepPauseCases.EndHoldCountsFromTheFinalScreen(endPrompt: false)).IsTrue();

    [Test]
    public async Task EndHoldCountsFromTheClosingPrompt() => await Assert.That(StepPauseCases.EndHoldCountsFromTheFinalScreen(endPrompt: true)).IsTrue();

    [Test]
    public async Task OutputStepPausesBeforeItsOutput() => await Assert.That(StepPauseCases.OutputStepPausesBeforeItsOutput()).IsTrue();

    [Test]
    public async Task CardShowsAfterItsPreDelay() => await Assert.That(StepPauseCases.CardShowsAfterItsPreDelay()).IsTrue();
}

internal static class StepPauseCases
{
    internal static bool StepTimingValuesReadAsSettingsDo()
    {
        // A step's timing keys read a number as settings and sleep do: a decimal comma, parentheses, or a currency
        // sign used to give 5, -1, or 1, and infinity passed as a time. Each warns and keeps the default instead.
        var original = Console.Error;
        using var captured = new StringWriter();
#pragma warning disable TUnit0055
        Console.SetError(captured);
        try
        {
            static double Read(object? value) =>
                ScenarioCast.GetDouble(new Dictionary<string, object?> { ["typing-speed"] = value }, 0.05, "typing-speed");

            var refused = new object?[] { "0,05", "(1)", "$1", double.PositiveInfinity, double.NaN, "Infinity", true };
            var refusedOk = true;
            foreach (var value in refused)
                refusedOk &= Expect(Read(value) == 0.05, $"{value} was read as a number");

            var warnings = captured.ToString();
            return refusedOk
                & Expect(warnings.Contains("Warning: typing-speed: '0,05' is not a number of seconds (such as 0.5), using 0.05", StringComparison.Ordinal), warnings)
                & Expect(Read("0.5") == 0.5 && Read(0.25) == 0.25 && Read(1) == 1 && Read("1e-2") == 0.01 && Read(" 0.3 ") == 0.3, "a plain number is read")
                & Expect(ScenarioCast.GetOptionalDouble(new Dictionary<string, object?> { ["max-duration"] = "1,5" }, null, "max-duration") is null, "an optional key keeps null");
        }
        finally
        {
            Console.SetError(original);
        }
#pragma warning restore TUnit0055
    }

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

    internal static bool FinalScreenIsTheLastOutput()
    {
        // By default the recording ends on the last output, without the closing prompt: the cursor goes as soon as the
        // output ends, and the final screen stays 2 x (post-delay + pre-delay). An event that draws nothing ends the
        // recording, and the render holds it as long again.
        var recording = Read(TwoSteps);
        var events = recording.Events;
        var outputTwo = Index(events, "two\r\n");
        return Expect(events.Count == outputTwo + 3, $"{events.Count - outputTwo - 1} events after the last output, expected hiding the cursor and the end")
            && Expect(events[outputTwo + 1].Data == "\u001b[?25l" && Near(Gap(events, outputTwo + 1), 0), $"the cursor is hidden {Gap(events, outputTwo + 1):F3} s after the output, with {events[outputTwo + 1].Data}")
            && Expect(PlaybackLength.IsSilent(events[^1]), $"the last event draws: {events[^1].Data}")
            && Expect(Near(recording.LoopDuration - events[outputTwo].Time, 2 * (0.7 + 1.5)), $"the final screen stays {recording.LoopDuration - events[outputTwo].Time:F3} s, expected 4.4");
    }

    internal static bool EndPromptDrawsTheClosingPrompt()
    {
        // end-prompt: the closing prompt shows after the last step's post-delay, and the final screen stays as long.
        var recording = Read(EndPrompt(TwoSteps));
        var events = recording.Events;
        var outputTwo = Index(events, "two\r\n");
        return Expect(events.Count == outputTwo + 3, $"{events.Count - outputTwo - 1} events after the last output, expected the prompt and the end")
            && Expect(events[outputTwo + 1].Data == "$ " && Near(Gap(events, outputTwo + 1), 0.7), $"the closing prompt is {Gap(events, outputTwo + 1):F3} s after the output, not post-delay 0.7")
            && Expect(PlaybackLength.IsSilent(events[^1]), $"the last event draws: {events[^1].Data}")
            && Expect(Near(recording.LoopDuration - events[outputTwo].Time, 2 * (0.7 + 1.5)), $"the final screen stays {recording.LoopDuration - events[outputTwo].Time:F3} s, expected 4.4");
    }

    internal static bool EndHoldCountsFromTheFinalScreen(bool endPrompt)
    {
        // An end-hold replaces the default hold: it starts when the final screen shows, the last output or the closing
        // prompt, without the event after it, so end-hold 0 starts again at once. At 2x it is still seconds of the output.
        var recording = Read(endPrompt ? EndPrompt(TwoSteps) : TwoSteps);
        var final = recording.Events[^2];
        double Loop(double? endHold, double speed = 1)
        {
            var settings = recording.RenderSettings with { EndHold = endHold, Speed = speed };
            double? loop = recording.LoopDuration;
            var events = RenderSettingsResolver.ApplySpeed(recording.Events, settings, ref loop);
            return RenderSettingsResolver.ResolveLoopDuration(loop ?? TerminalReplay.ComputeLoopDuration(events), events, settings);
        }

        var expected = endPrompt ? "$ " : "\u001b[?25l";
        return Expect(final.Data == expected, $"the event before the last is {final.Data}, not {expected}")
            && Expect(Near(Loop(null), recording.LoopDuration), $"the default hold moved: {Loop(null):F3}, recorded {recording.LoopDuration:F3}")
            && Expect(Near(Loop(0), final.Time), $"end-hold 0 loops at {Loop(0):F3}, expected the final screen at {final.Time:F3}")
            && Expect(Near(Loop(3), final.Time + 3), $"end-hold 3 loops at {Loop(3):F3}, expected {final.Time + 3:F3}")
            && Expect(Near(Loop(3, 2), final.Time / 2 + 3), $"end-hold 3 at 2x loops at {Loop(3, 2):F3}, expected {final.Time / 2 + 3:F3}");
    }

    private static string EndPrompt(string yaml) => yaml.Replace("settings:", "settings:\n  end-prompt: true", StringComparison.Ordinal);

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
