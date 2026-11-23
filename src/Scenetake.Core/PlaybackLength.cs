using System.Globalization;
using System.Text;

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

    /// <summary>A moment before a command is typed: kept on top of the reading time, and the least a prompt stays before typing.</summary>
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
            return new Result(events, loopDuration, loopDuration + fadeOut, loopDuration + fadeOut, loopDuration + fadeOut) { Original = events, GapShortest = [0], Actions = [] };

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

        // Typing and output first, then the still screens between them, which win where a one-character prompt
        // looked typed. Each step acts at its first typed character, or at its first output when it types nothing.
        var stepCount = steps.Length / 2;
        var prompt = OneCharacterPrompt(events);
        var actions = new int[stepCount];
        for (var s = 0; s < stepCount; s++)
            actions[s] = MarkStep(steps[s * 2], steps[s * 2 + 1]);

        // Before the first step: a moment to see the window, and a moment before the first command.
        if (stepCount > 0 && actions[0] >= 0)
        {
            var openingEnd = StillEnd(0, actions[0]);
            MarkStill(0, openingEnd, openingEnd == actions[0] ? PauseFloor + BeforeTyping : PauseFloor);
        }

        for (var s = 0; s < stepCount; s++)
            MarkReading(steps[s * 2], steps[s * 2 + 1], s + 1 < stepCount ? actions[s + 1] : count - 1);

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
        // Each gap at its shortest, which the warning of a length that cannot be kept also splits by step.
        var shortest = fixedTail;
        var gapShortest = new double[count + 1];
        for (var i = 0; i <= count; i++)
        {
            gapShortest[i] = kinds[i] == Fixed ? gaps[i] : Math.Min(gaps[i], floors[i]);
            shortest += gapShortest[i];
        }

        if (natural <= length)
            return new Result(events, loopDuration, natural, natural, shortest) { Original = events, GapShortest = gapShortest, Actions = actions };

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

        return new Result(fitted, time + gaps[count], natural, Sum(gaps) + fixedTail, shortest) { Original = events, GapShortest = gapShortest, Actions = actions };

        // A gap's kind and floor. A floor above the gap leaves the gap as it is.
        void Mark(int index, int kind, double floor)
        {
            if (index > count)
                return;
            kinds[index] = kind;
            floors[index] = floor;
        }

        // A step's typing and its output. Returns where the step acts: its first typed character, or its first output
        // event when it types nothing. -1 for a range that is not in the cast.
        int MarkStep(int first, int outputs)
        {
            if (first < 0 || first > count || outputs < 0 || first + outputs > count)
                return -1;

            // The typed command: the characters before Enter, back to the prompt. The gap to the first character holds
            // the pause before typing (pre-delay), which keeps a moment.
            var action = first;
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

                // A prompt of one character looks typed: the closing prompt tells it, and the command starts after it.
                if (earliest >= 0 && prompt is not null && events[earliest].Data == prompt)
                {
                    var promptIndex = earliest;
                    Mark(promptIndex, gaps[promptIndex] > StructuralGap ? Pause : Fixed, PauseFloor);
                    for (earliest = promptIndex + 1; earliest < first - 1 && !IsTypedCharacter(events[earliest]); earliest++)
                    {
                    }

                    if (earliest >= first - 1)
                        earliest = -1;
                }

                if (earliest >= 0)
                {
                    Mark(earliest, gaps[earliest] > BeforeTyping ? Pause : Fixed, BeforeTyping);
                    action = earliest;
                }
            }

            // The output: a long gap with nothing drawn is a wait, a short one is motion.
            for (var i = first; i < first + outputs; i++)
                Mark(i, gaps[i] > WaitThreshold ? Wait : Fixed, WaitFloor);
            return action;
        }

        // The still screen after a step, from its last output that draws to where the next step acts: the time to read
        // what it printed, and a moment before the next command. Its post-delay, the next step's comment and prompt, and
        // that step's pre-delay are all in it, and shrink together. A step at the end keeps it up to the last event.
        void MarkReading(int first, int outputs, int next)
        {
            if (first < 0 || first > count || outputs < 0 || first + outputs > count)
                return;

            // A program's last escape codes, such as showing the cursor again, draw nothing, and the screen is already
            // still before them.
            var last = first + outputs - 1;
            while (last >= first && IsSilent(events[last]))
                last--;

            var lines = 0;
            for (var i = first; i <= last; i++)
            {
                if (events[i].Kind == CastEventKind.Output)
                    lines += CountLines(events[i].Data);
            }

            var reading = Math.Min(ReadingMax, ReadingBase + ReadingPerLine * lines) + BeforeTyping;
            // Without output that draws, the still screen starts at Enter, and the gap after Enter is in it.
            var start = last >= first ? last + 1 : first;
            if (last < first)
                reading += StructuralGap;
            if (first + outputs >= count)
            {
                // The step's output ends the cast: the default hold on the final screen is part of its still screen.
                MarkStill(start, count, reading);
                return;
            }

            MarkStill(start, StillEnd(start, Math.Max(next, first + outputs)), reading);
        }

        // The last gap of a still screen from start to end: it stops at the gap to a cleared screen, a card or a clear
        // step, which shows something else.
        int StillEnd(int start, int end)
        {
            end = Math.Min(end, count - 1);
            for (var i = start; i < end; i++)
            {
                if (events[i].Kind == CastEventKind.Output && events[i].Data.Contains(ScenarioCast.ClearScreen, StringComparison.Ordinal))
                    return i;
            }

            return end;
        }

        // The gaps from start to end as one still screen of at least floor seconds. The short ones, the comment line
        // and the prompt, stay. The others share the rest of the floor by their length, so one factor shrinks them
        // together, as one gap would.
        void MarkStill(int start, int end, double floor)
        {
            var still = 0.0;
            var structural = 0.0;
            for (var i = start; i <= end; i++)
            {
                if (gaps[i] > StructuralGap)
                    still += gaps[i];
                else
                    structural += gaps[i];
            }

            if (still <= 0)
                return;

            var share = Math.Min(1, Math.Max(0, floor - structural) / still);
            for (var i = start; i <= end; i++)
            {
                if (gaps[i] > StructuralGap)
                    Mark(i, Pause, gaps[i] * share);
                else
                    Mark(i, Fixed, 0);
            }
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

    /// <summary>
    /// The prompt when it is one character, which looks typed: the closing prompt, the last event that draws. Null for
    /// a longer prompt, or a cast that ends with a card.
    /// </summary>
    private static string? OneCharacterPrompt(IReadOnlyList<CastEvent> events)
    {
        for (var i = events.Count - 1; i >= 0; i--)
        {
            if (events[i].Kind != CastEventKind.Output || IsSilent(events[i]))
                continue;
            return IsTypedCharacter(events[i]) ? events[i].Data : null;
        }

        return null;
    }

    /// <summary>
    /// An event of escape codes that draw nothing: styles, modes such as showing the cursor, and erasing the rest of the
    /// line, which a program writes as it exits, after its cursor is on an empty line. Switching to or from the
    /// alternate screen draws.
    /// </summary>
    internal static bool IsSilent(CastEvent ev)
    {
        if (ev.Kind != CastEventKind.Output || ev.HasUtf8Output || ev.Data.Length == 0)
            return false;

        var data = ev.Data.AsSpan();
        var i = 0;
        while (i < data.Length)
        {
            if (data[i] != '\u001b' || i + 1 >= data.Length)
                return false;
            if (data[i + 1] is '=' or '>')
            {
                // The keypad modes.
                i += 2;
                continue;
            }

            if (data[i + 1] != '[')
                return false;
            var end = i + 2;
            while (end < data.Length && data[end] is >= '0' and <= '?')
                end++;
            while (end < data.Length && data[end] is >= ' ' and <= '/')
                end++;
            if (end >= data.Length)
                return false;

            var parameters = data[(i + 2)..end];
            switch (data[end])
            {
                case 'm' or 'K' or 'q':
                    break;
                case 'h' or 'l' when !IsAlternateScreen(parameters):
                    break;
                default:
                    return false;
            }

            i = end + 1;
        }

        return true;

        static bool IsAlternateScreen(ReadOnlySpan<char> parameters)
        {
            if (parameters.IsEmpty || parameters[0] != '?')
                return false;
            foreach (var range in parameters[1..].Split(';'))
            {
                if (parameters[1..][range] is "47" or "1047" or "1049")
                    return true;
            }

            return false;
        }
    }

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

    /// <summary>
    /// Each step's part of the shortest the recording can play, for the warning of a length it cannot keep, as the
    /// time per step splits a recording (<see cref="StepTimes"/>): a step runs from where it begins to where the next
    /// one begins, so its post-delay is its own, the first holds the moment before it, and the last ends with its
    /// output, as the final screen is no step's. A gap across where two steps meet is shared by its seconds in each.
    /// <paramref name="names"/> gives the steps as the scenario's run knew them. Without it, as for a cast alone, the
    /// steps are those of <paramref name="steps"/>, the <c>st:steps</c> tag's: each begins with the event after the
    /// output of the step before it, a card's time on the screen is no step's, and each is named by the command it
    /// types in quotes, or by when it acts in the cast.
    /// </summary>
    internal static StepShortest[] StepsAtShortest(int[] steps, Result fitted, StepNames? names)
    {
        var events = fitted.Original;
        var count = events.Count;
        var gapShortest = fitted.GapShortest;
        double[] starts;
        double end;
        string[] labels;
        if (names is not null)
        {
            starts = names.Starts;
            end = names.End;
            labels = names.Labels;
        }
        else
        {
            var stepCount = Math.Min(steps.Length / 2, fitted.Actions.Length);
            starts = new double[stepCount];
            labels = new string[stepCount];
            end = 0;
            gapShortest = (double[])gapShortest.Clone();
            for (var i = 1; i <= count; i++)
            {
                if (events[i - 1].Kind == CastEventKind.Output && events[i - 1].Data.StartsWith(CardStart, StringComparison.Ordinal))
                    gapShortest[i] = 0;
            }

            for (var s = 0; s < stepCount; s++)
            {
                var first = steps[s * 2];
                var action = fitted.Actions[s];
                var after = s == 0 ? 0 : steps[s * 2 - 2] + steps[s * 2 - 1];
                starts[s] = s == 0 ? 0 : after < count ? events[after].Time : starts[s - 1];
                if (action < 0)
                {
                    labels[s] = "";
                    continue;
                }

                // The command as it was typed, from where the step acts to Enter.
                var typed = new StringBuilder();
                for (var i = action; i < first && i < count; i++)
                {
                    if (IsTypedCharacter(events[i]))
                        typed.Append(events[i].Data);
                }

                labels[s] = typed.Length > 0
                    ? JsonEscape.JsonString(Rerecording.Shorten(typed.ToString()))
                    : string.Create(CultureInfo.InvariantCulture, $"the step at {events[Math.Min(action, count - 1)].Time:F1}s");
                end = events[Math.Clamp(first + steps[s * 2 + 1] - 1, 0, count - 1)].Time;
            }
        }

        var seconds = new double[starts.Length];
        var previous = 0.0;
        var step = 0;
        for (var i = 0; i < count && starts.Length > 0; i++)
        {
            var from = previous;
            var to = events[i].Time;
            previous = to;
            if (to <= from || gapShortest[i] <= 0)
                continue;
            while (step + 1 < starts.Length && starts[step + 1] <= from)
                step++;
            for (var s = step; s < starts.Length && starts[s] < to; s++)
            {
                var overlap = Math.Min(to, s + 1 < starts.Length ? starts[s + 1] : end) - Math.Max(from, starts[s]);
                if (overlap > 0)
                    seconds[s] += gapShortest[i] * overlap / (to - from);
            }
        }

        var named = 0;
        foreach (var label in labels)
            named += label.Length > 0 ? 1 : 0;
        var result = new StepShortest[named];
        named = 0;
        for (var s = 0; s < starts.Length; s++)
        {
            if (labels[s].Length > 0)
                result[named++] = new StepShortest(labels[s], seconds[s]);
        }

        return result;
    }

    /// <summary>A step named for messages, and its seconds at the shortest (<see cref="StepsAtShortest"/>).</summary>
    internal sealed class StepShortest(string label, double seconds)
    {
        internal string Label { get; } = label;
        internal double Seconds { get; } = seconds;
    }

    /// <summary>A fitted recording, with how long it played before and now, and the shortest it can play.</summary>
    internal sealed class Result(IReadOnlyList<CastEvent> events, double loopDuration, double natural, double played, double shortest)
    {
        /// <summary>The events as they were given, before fitting.</summary>
        internal required IReadOnlyList<CastEvent> Original { get; init; }

        /// <summary>What each gap of <see cref="Original"/> gives at the shortest, the default hold last.</summary>
        internal required double[] GapShortest { get; init; }

        /// <summary>Where each step of the <c>st:steps</c> tag acts: its first typed character, or its first output. -1 for one not in the cast.</summary>
        internal required int[] Actions { get; init; }

        internal IReadOnlyList<CastEvent> Events { get; } = events;

        /// <summary>The loop with the default hold, without an explicit end hold or the fade-out.</summary>
        internal double LoopDuration { get; } = loopDuration;

        internal double Natural { get; } = natural;
        internal double Played { get; } = played;
        internal double Shortest { get; } = shortest;
    }
}
