using System.Globalization;

namespace Scenetake.Core;

/// <summary>
/// <c>render.length</c>: plays a recording in at most that many seconds, as naturally as it can (spec_svg.md → Length).
/// Each gap between two events of the cast is one kind of time, and the kinds shrink in turn, each down to a floor:
/// waits inside a command's output first, then pauses, then typing. Motion inside output (a progress bar, a TUI
/// redrawing) and the short structural gaps (the prompt, Enter) never shrink. A recording that is shorter than the
/// length stays as it is, and one that cannot fit stops at its floors.
/// </summary>
/// <remarks>
/// It works on the cast's events at render time, as <c>speed</c> does, so the cast keeps the recorded times, and one
/// recording plays at another length in each output. The steps come from the cast's <c>st:steps</c> tag: where each
/// step's output is. The same cast always gives the same timing.
/// </remarks>
internal static class PlaybackLength
{
    /// <summary>A gap inside a command's output longer than this, with nothing drawn in it, is a wait.</summary>
    internal const double WaitThreshold = 0.5;

    /// <summary>What a wait shrinks to at most: long enough to see that the command took a while.</summary>
    internal const double WaitFloor = 0.3;

    /// <summary>A gap this short is structural (the prompt, Enter, a comment line) or motion, and never shrinks.</summary>
    internal const double StructuralGap = 0.2;

    /// <summary>What another pause, such as the one before the first step, shrinks to at most.</summary>
    internal const double PauseFloor = 0.3;

    /// <summary>The time to read a step's output: this, and <see cref="ReadingPerLine"/> per line, up to <see cref="ReadingMax"/>.</summary>
    internal const double ReadingBase = 0.6;

    internal const double ReadingPerLine = 0.08;
    internal const double ReadingMax = 3.0;

    /// <summary>The pause before the next command is typed, kept on top of the reading time.</summary>
    internal const double BeforeTyping = 0.15;

    /// <summary>The time to read a card: this, and <see cref="CardPerLine"/> per line.</summary>
    internal const double CardBase = 1.0;

    internal const double CardPerLine = 0.25;

    /// <summary>What the final screen's default hold shrinks to at most. An explicit end-hold never shrinks.</summary>
    internal const double EndHoldFloor = 1.0;

    /// <summary>The shortest gap between two typed characters: fast typing, still not a paste.</summary>
    internal const double TypingFloor = 0.025;

    // The kinds of time, in the order they shrink.
    private const int Fixed = 0;
    private const int Wait = 1;
    private const int Pause = 2;
    private const int Typing = 3;

    private const string CardStart = "\u001b[?25l" + ScenarioCast.ClearScreen;

    /// <summary>
    /// The recording fitted to <paramref name="length"/> seconds: the events at their new times, the loop with the
    /// default hold, how long it played before (<see cref="Result.Natural"/>), how long it plays now, and the shortest
    /// it can play (<see cref="Result.Shortest"/>). <paramref name="steps"/> holds each step's first output event and
    /// its count, in pairs. The length counts the explicit end hold and the fade-out, which never shrink.
    /// </summary>
    internal static Result Fit(IReadOnlyList<CastEvent> events, int[] steps, double loopDuration, double? endHold, double fadeOut, double length)
    {
        var count = events.Count;
        if (count == 0)
            return new Result(events, loopDuration, loopDuration + fadeOut, loopDuration + fadeOut, loopDuration + fadeOut);

        // gaps[i] leads to event i, and gaps[count] is the default hold on the final screen.
        var gaps = new double[count + 1];
        var previous = 0.0;
        for (var i = 0; i < count; i++)
        {
            gaps[i] = Math.Max(0, events[i].Time - previous);
            previous = events[i].Time;
        }

        gaps[count] = endHold is null ? Math.Max(0, loopDuration - events[^1].Time) : 0;
        var kinds = new int[count + 1];
        var floors = new double[count + 1];
        for (var i = 0; i <= count; i++)
            Mark(i, gaps[i] > StructuralGap ? Pause : Fixed, PauseFloor);

        // Typing and output first, then the pauses after each step, which win where a one-character prompt looked typed.
        for (var s = 0; s + 1 < steps.Length; s += 2)
            MarkStep(steps[s], steps[s + 1]);
        for (var s = 0; s + 1 < steps.Length; s += 2)
            MarkReading(steps[s], steps[s + 1]);

        for (var i = 0; i < count; i++)
        {
            if (events[i].Kind == CastEventKind.Output && events[i].Data.StartsWith(CardStart, StringComparison.Ordinal))
                Mark(i + 1, Pause, CardBase + CardPerLine * CardLines(events[i].Data));
        }

        // The final screen's default hold is read like any pause, and a closing card's reading time stays in it.
        if (endHold is null)
            Mark(count, Pause, Math.Max(floors[count], EndHoldFloor));

        var fixedTail = (endHold ?? 0) + fadeOut;
        var natural = Sum(gaps) + fixedTail;
        var shortest = fixedTail;
        for (var i = 0; i <= count; i++)
            shortest += kinds[i] == Fixed ? gaps[i] : Math.Min(gaps[i], floors[i]);

        if (natural <= length)
            return new Result(events, loopDuration, natural, natural, shortest);

        // Each kind in turn gives up time down to its floors, all of its gaps by the same factor, until the recording fits.
        var excess = natural - length;
        foreach (var kind in (ReadOnlySpan<int>)[Wait, Pause, Typing])
        {
            if (excess <= 0)
                break;
            excess -= Shrink(gaps, kinds, floors, kind, excess);
        }

        var fitted = new CastEvent[count];
        var time = 0.0;
        for (var i = 0; i < count; i++)
        {
            time += gaps[i];
            fitted[i] = events[i] with { Time = Math.Round(time, 6) };
        }

        return new Result(fitted, time + gaps[count], natural, Sum(gaps) + fixedTail, shortest);

        // A gap's kind and floor. A floor above the gap leaves the gap as it is.
        void Mark(int index, int kind, double floor)
        {
            if (index > count)
                return;
            kinds[index] = kind;
            floors[index] = floor;
        }

        // A step's typing, its output, and the pause after it.
        void MarkStep(int first, int outputs)
        {
            if (first < 0 || first > count || outputs < 0 || first + outputs > count)
                return;

            // The typed command: the characters before Enter, back to the prompt. The gap to the first character is
            // the prompt's, and stays.
            var j = first - 1;
            if (j >= 0 && events[j].Data == "\r\n")
            {
                var earliest = -1;
                Mark(j, gaps[j] > 0 ? Typing : Fixed, TypingFloor);
                for (j--; j >= 0 && (IsTypedCharacter(events[j]) || IsStyleOnly(events[j])); j--)
                {
                    Mark(j, gaps[j] > 0 ? Typing : Fixed, TypingFloor);
                    if (IsTypedCharacter(events[j]))
                        earliest = j;
                }

                if (earliest >= 0)
                    Mark(earliest, Fixed, 0);
            }

            // The output: a long gap with nothing drawn is a wait, a short one is motion.
            for (var i = first; i < first + outputs; i++)
                Mark(i, gaps[i] > WaitThreshold ? Wait : Fixed, WaitFloor);
        }

        // The pause after a step: the time to read what it printed, and a moment before the next command.
        void MarkReading(int first, int outputs)
        {
            if (first < 0 || first > count || outputs < 0 || first + outputs > count)
                return;

            var lines = 0;
            for (var i = first; i < first + outputs; i++)
            {
                if (events[i].Kind == CastEventKind.Output)
                    lines += CountLines(events[i].Data);
            }

            var reading = Math.Min(ReadingMax, ReadingBase + ReadingPerLine * lines) + BeforeTyping;
            Mark(first + outputs, Pause, outputs == 0 ? reading + StructuralGap : reading);
        }
    }

    /// <summary>
    /// Takes up to <paramref name="excess"/> seconds from the gaps of one kind, all by the same factor, none below its
    /// floor, and returns what it took.
    /// </summary>
    private static double Shrink(double[] gaps, int[] kinds, double[] floors, int kind, double excess)
    {
        var room = 0.0;
        for (var i = 0; i < gaps.Length; i++)
        {
            if (kinds[i] == kind && gaps[i] > floors[i])
                room += gaps[i] - floors[i];
        }

        if (room <= 0)
            return 0;

        if (room <= excess)
        {
            for (var i = 0; i < gaps.Length; i++)
            {
                if (kinds[i] == kind && gaps[i] > floors[i])
                    gaps[i] = floors[i];
            }

            return room;
        }

        // The factor that takes exactly the excess: each gap becomes max(floor, gap x factor).
        double low = 0, high = 1;
        for (var step = 0; step < 60; step++)
        {
            var factor = (low + high) / 2;
            var taken = 0.0;
            for (var i = 0; i < gaps.Length; i++)
            {
                if (kinds[i] == kind && gaps[i] > floors[i])
                    taken += gaps[i] - Math.Max(floors[i], gaps[i] * factor);
            }

            if (taken > excess)
                low = factor;
            else
                high = factor;
        }

        var taken2 = 0.0;
        for (var i = 0; i < gaps.Length; i++)
        {
            if (kinds[i] == kind && gaps[i] > floors[i])
            {
                var shrunk = Math.Max(floors[i], gaps[i] * high);
                taken2 += gaps[i] - shrunk;
                gaps[i] = shrunk;
            }
        }

        return taken2;
    }

    private static double Sum(double[] values)
    {
        var sum = 0.0;
        foreach (var value in values)
            sum += value;
        return sum;
    }

    /// <summary>A typed character: one grapheme that draws, as the composer types each one as an event of its own.</summary>
    private static bool IsTypedCharacter(CastEvent ev) =>
        ev.Kind == CastEventKind.Output && !ev.HasUtf8Output && ev.Data.Length > 0 && !char.IsControl(ev.Data[0])
        && StringInfo.GetNextTextElementLength(ev.Data) == ev.Data.Length;

    /// <summary>An event that only sets a style, as run-highlight writes around the typed command.</summary>
    private static bool IsStyleOnly(CastEvent ev) =>
        ev.Kind == CastEventKind.Output && ev.Data.Length >= 3 && ev.Data[0] == '\u001b' && ev.Data[1] == '[' && ev.Data[^1] == 'm'
        && ev.Data.IndexOf('\u001b', 1) < 0;

    private static int CountLines(string data)
    {
        var lines = 0;
        foreach (var c in data)
        {
            if (c == '\n')
                lines++;
        }

        return lines;
    }

    /// <summary>A card's lines: one cursor move each, less the one that keeps its last row (CastComposer.ComposeCard).</summary>
    private static int CardLines(string data)
    {
        var moves = 0;
        for (var i = 0; i + 1 < data.Length; i++)
        {
            if (data[i] != '\u001b' || data[i + 1] != '[')
                continue;
            var end = i + 2;
            while (end < data.Length && (char.IsAsciiDigit(data[end]) || data[end] == ';'))
                end++;
            if (end < data.Length && data[end] == 'H' && end > i + 2)
                moves++;
        }

        return Math.Max(1, moves - 1);
    }

    /// <summary>A fitted recording, with how long it played before and now, and the shortest it can play.</summary>
    internal sealed class Result(IReadOnlyList<CastEvent> events, double loopDuration, double natural, double played, double shortest)
    {
        internal IReadOnlyList<CastEvent> Events { get; } = events;

        /// <summary>The loop with the default hold, without an explicit end hold or the fade-out.</summary>
        internal double LoopDuration { get; } = loopDuration;

        internal double Natural { get; } = natural;
        internal double Played { get; } = played;
        internal double Shortest { get; } = shortest;
    }
}
