using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Scenetake.Core;

// The types here are classes, not records or structs in collections: under Native AOT each record's generated members,
// and each collection of a struct, add code to the binary, while collections of classes share theirs.

/// <summary>
/// One recorded step in a cast: which events hold its output, and when the step started and ended. A cast keeps
/// these in its <c>st:steps</c> tag (<see cref="Rerecording.FormatTag"/>), so the next recording to the same file
/// finds each step's earlier output (spec_scenario.md → Recording again).
/// </summary>
internal sealed class StepSlot(
    int number,
    string run,
    ulong id,
    uint timing,
    bool clock,
    bool keepOutput,
    int first,
    int count,
    double start,
    double end,
    double instantAt)
{
    /// <summary>The step's 1-based position in <c>steps</c>.</summary>
    internal int Number { get; } = number;

    /// <summary>The step's run text, for messages.</summary>
    internal string Run { get; } = run;

    /// <summary>The run text, pipe, pty, or interactive, and show, hashed: what finds the step in the earlier cast.</summary>
    internal ulong Id { get; } = id;

    /// <summary>The keys that time the step's output, hashed (<see cref="Rerecording.TimingHash"/>).</summary>
    internal uint Timing { get; } = timing;

    /// <summary>Whether the wall clock times its output: a PTY step, an interactive step, or stream emit.</summary>
    internal bool Clock { get; } = clock;

    /// <summary>Whether the step sets <c>keep-output: true</c>.</summary>
    internal bool KeepOutput { get; } = keepOutput;

    /// <summary>The index of its first output event.</summary>
    internal int First { get; } = first;

    /// <summary>How many output events it has.</summary>
    internal int Count { get; } = count;

    /// <summary>When its command started, in seconds. Its output is timed from here.</summary>
    internal double Start { get; } = start;

    /// <summary>When the step ended, before its post-delay. A whole millisecond for a <see cref="Clock"/> step.</summary>
    internal double End { get; } = end;

    /// <summary>Where output that the clock does not time appears: <see cref="Start"/> plus execution-duration.</summary>
    internal double InstantAt { get; } = instantAt;

    /// <summary>The same step with its events elsewhere: where the OSC filter left them.</summary>
    internal StepSlot At(int first, int count) =>
        new(Number, Run, Id, Timing, Clock, KeepOutput, first, count, Start, End, InstantAt);

    /// <summary>The same step showing the earlier recording's output, with the timing keys of the events it now has.</summary>
    internal StepSlot Reused(int count, double end, uint timing) =>
        new(Number, Run, Id, timing, Clock, KeepOutput, First, count, Start, end, InstantAt);
}

/// <summary>A step as the earlier cast's <c>st:steps</c> tag has it. Times are whole milliseconds from the cast's start.</summary>
internal sealed class PreviousStep(ulong id, uint timing, bool clock, int first, int count, long startMs, long endMs)
{
    internal ulong Id { get; } = id;
    internal uint Timing { get; } = timing;
    internal bool Clock { get; } = clock;
    internal int First { get; } = first;
    internal int Count { get; } = count;
    internal long StartMs { get; } = startMs;
    internal long EndMs { get; } = endMs;

    /// <summary>Whether a step of this recording has found it already.</summary>
    internal bool Taken { get; set; }
}

/// <summary>
/// The cast that an earlier recording wrote where this one writes: its events, each at a whole millisecond as the
/// file has them, and its steps, from the <c>st:steps</c> tag.
/// </summary>
internal sealed class PreviousCast
{
    private readonly List<CastEvent> _events;
    private readonly long[] _ms;
    private readonly List<PreviousStep> _steps;

    private PreviousCast(List<CastEvent> events, long[] ms, List<PreviousStep> steps)
    {
        _events = events;
        _ms = ms;
        _steps = steps;
    }

    /// <summary>
    /// The cast at <paramref name="path"/>, or null: when there is no file, and when the file cannot be used for a
    /// recording in a <paramref name="width"/> by <paramref name="height"/> terminal, which <paramref name="problem"/>
    /// then says, such as a cast without step ranges that an earlier scenetake wrote.
    /// </summary>
    internal static PreviousCast? TryRead(string path, int width, int height, out string? problem)
    {
        problem = null;
        if (!File.Exists(path))
            return null;

        string[] lines;
        try
        {
            lines = File.ReadAllLines(path, Encoding.UTF8);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            problem = $"it cannot be read: {ex.Message}";
            return null;
        }

        try
        {
            return Parse(lines, width, height, out problem);
        }
        // InvalidOperationException: a JSON string that holds half of a surrogate pair is valid JSON, but has no text.
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            problem = "it is not a cast that scenetake can read";
            return null;
        }
    }

    /// <summary>A cast held in memory, split into lines. Null, with <paramref name="problem"/>, when it cannot be used.</summary>
    internal static PreviousCast? Parse(string[] lines, int width, int height, out string? problem)
    {
        problem = null;
        var line = 0;
        while (line < lines.Length && (lines[line].Length == 0 || lines[line].TrimStart('﻿').Length == 0))
            line++;
        if (line == lines.Length)
        {
            problem = "it is empty";
            return null;
        }

        string? tag = null;
        var cols = width;
        var rows = height;
        using (var header = JsonDocument.Parse(lines[line].TrimStart('﻿')))
        {
            if (header.RootElement.ValueKind != JsonValueKind.Object
                || !header.RootElement.TryGetProperty("version", out var version)
                || version.ValueKind != JsonValueKind.Number
                || !version.TryGetInt32(out var number)
                || number != 3)
            {
                problem = "it is not an asciicast v3 file";
                return null;
            }

            if (header.RootElement.TryGetProperty("term", out var term) && term.ValueKind == JsonValueKind.Object)
            {
                if (term.TryGetProperty("cols", out var c) && c.ValueKind == JsonValueKind.Number && c.TryGetInt32(out var readCols))
                    cols = readCols;
                if (term.TryGetProperty("rows", out var r) && r.ValueKind == JsonValueKind.Number && r.TryGetInt32(out var readRows))
                    rows = readRows;
            }

            if (header.RootElement.TryGetProperty("tags", out var tags) && tags.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in tags.EnumerateArray())
                {
                    if (element.ValueKind == JsonValueKind.String && element.GetString() is { } value
                        && value.StartsWith(Rerecording.TagPrefix, StringComparison.Ordinal))
                    {
                        tag = value[Rerecording.TagPrefix.Length..];
                        break;
                    }
                }
            }
        }

        if (tag is null)
        {
            problem = "it has no step ranges: an earlier scenetake or another tool wrote it";
            return null;
        }

        // PTY output is laid out for its terminal, so what fits one size draws over other text in another.
        if (cols != width || rows != height)
        {
            problem = $"it was recorded in a {cols}x{rows} terminal, and this one is {width}x{height}";
            return null;
        }

        // Every event the writer wrote, the closing x event aside, in order: the tag counts them that way.
        var events = new List<CastEvent>();
        var ms = new List<long>();
        long now = 0;
        for (line++; line < lines.Length; line++)
        {
            if (lines[line].Length == 0)
                continue;

            using var document = JsonDocument.Parse(lines[line]);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() < 3
                || root[0].ValueKind != JsonValueKind.Number || root[1].ValueKind != JsonValueKind.String || root[2].ValueKind != JsonValueKind.String)
            {
                problem = $"line {line + 1} is not a cast event";
                return null;
            }

            now += (long)Math.Round(root[0].GetDouble() * 1000, MidpointRounding.AwayFromZero);
            var code = root[1].GetString();
            var data = root[2].GetString() ?? "";
            var time = now / 1000.0;
            switch (code)
            {
                case "o":
                    events.Add(CastEvent.Output(time, data));
                    break;
                case "m":
                    events.Add(CastEvent.Marker(time, data));
                    break;
                case "r" when TryParseSize(data, out var resizeWidth, out var resizeHeight):
                    events.Add(CastEvent.Resize(time, resizeWidth, resizeHeight));
                    break;
                case "x":
                    continue;
                default:
                    problem = $"line {line + 1} has an event scenetake does not write";
                    return null;
            }

            ms.Add(now);
        }

        if (!Rerecording.TryParseTag(tag, events.Count, ms.Count > 0 ? ms[^1] : 0, out var steps))
        {
            problem = "its step ranges do not match its events";
            return null;
        }

        return new PreviousCast(events, [.. ms], steps);
    }

    private static bool TryParseSize(string text, out int width, out int height)
    {
        width = height = 0;
        var x = text.IndexOf('x');
        return x > 0
            && int.TryParse(text.AsSpan(0, x), NumberStyles.None, CultureInfo.InvariantCulture, out width)
            && int.TryParse(text.AsSpan(x + 1), NumberStyles.None, CultureInfo.InvariantCulture, out height);
    }

    /// <summary>The next earlier step with <paramref name="id"/>, in order: the second step that runs the same command finds the second one.</summary>
    internal PreviousStep? Take(ulong id)
    {
        // A scenario has tens of steps, so a scan is as quick as a lookup, and adds no code to the binary.
        foreach (var step in _steps)
        {
            if (!step.Taken && step.Id == id)
            {
                step.Taken = true;
                return step;
            }
        }

        return null;
    }

    /// <summary>The step that <see cref="Take"/> would give next for <paramref name="id"/>, without taking it.</summary>
    internal PreviousStep? Peek(ulong id)
    {
        foreach (var step in _steps)
        {
            if (!step.Taken && step.Id == id)
                return step;
        }

        return null;
    }

    /// <summary>How many earlier steps have <paramref name="id"/>: the n-th step with it, from 0, finds one when n is below this.</summary>
    internal int CountOf(ulong id)
    {
        var count = 0;
        foreach (var step in _steps)
        {
            if (step.Id == id)
                count++;
        }

        return count;
    }

    /// <summary>The earlier step with <paramref name="id"/> that the <paramref name="n"/>-th step with it, from 0, finds, or null.</summary>
    internal PreviousStep? Nth(ulong id, int n)
    {
        foreach (var step in _steps)
        {
            if (step.Id == id && n-- == 0)
                return step;
        }

        return null;
    }

    /// <summary>The text of the step's output events, joined.</summary>
    internal string Text(PreviousStep step)
    {
        var text = new StringBuilder();
        for (var i = step.First; i < step.First + step.Count; i++)
        {
            if (_events[i].Kind == CastEventKind.Output)
                text.Append(_events[i].Data);
        }

        return text.ToString();
    }

    /// <summary>The event at <paramref name="index"/>.</summary>
    internal CastEvent EventAt(int index) => _events[index];

    /// <summary>The time of the event at <paramref name="index"/>, in whole milliseconds from the cast's start.</summary>
    internal long MsAt(int index) => _ms[index];

    /// <summary>Where the cursor was when the event at <paramref name="index"/> came: where that step started.</summary>
    internal (int Row, int Column) CursorAt(int index, int width, int height) =>
        new CastCursorTracker(width, height).Advance(_events, index);
}

/// <summary>
/// Compares each step of a recording with the earlier cast, as the step is composed. The steps after it are then laid
/// out on what the cast shows: a <c>name</c> comment starts on the right line, and ConPTY gets the right cursor.
/// A step that prints what it printed then keeps the earlier events, with their timing, and a step with
/// <c>keep-output: true</c> keeps them even when its output changed. So a scenario recorded again with nothing changed
/// gives the same cast, and the same SVG, though PTY output is timed by the wall clock.
/// </summary>
internal sealed class Rerecorder(PreviousCast? previous, SecretMasker? masker)
{
    /// <summary>Whether there is an earlier cast to compare with.</summary>
    internal bool HasPrevious => previous is not null;

    /// <summary>Every step, where its events are: the st:steps tag, after <see cref="Rerecording.MapIndices"/>.</summary>
    internal List<StepSlot> Slots { get; } = [];

    /// <summary>The steps that printed the same as in the earlier cast.</summary>
    internal List<StepSlot> Same { get; } = [];

    /// <summary>The steps that printed something else and kept the earlier output (keep-output).</summary>
    internal List<StepSlot> Kept { get; } = [];

    /// <summary>The steps that printed something else and show it.</summary>
    internal List<StepSlot> Changed { get; } = [];

    /// <summary>The steps that the earlier cast does not have, without keep-output.</summary>
    internal List<StepSlot> Added { get; } = [];

    /// <summary>The steps with keep-output that the earlier cast does not have.</summary>
    internal List<StepSlot> KeepMissing { get; } = [];

    /// <summary>The steps with keep-output whose earlier output holds text that is masked now. They are in <see cref="Changed"/> too.</summary>
    internal List<StepSlot> KeepMasked { get; } = [];

    /// <summary>The steps with keep-output that start elsewhere on the screen than their earlier output did. They are in <see cref="Changed"/> too.</summary>
    internal List<StepSlot> KeepMoved { get; } = [];

    /// <summary>The steps with <c>rerun: false</c> that show the earlier output, without running.</summary>
    internal List<StepSlot> NotRun { get; } = [];

    /// <summary>Why a step with <c>rerun: false</c> ran after all, one line each, for <see cref="Rerecording.Describe"/>.</summary>
    internal List<string> RerunNotes { get; } = [];

    /// <summary>Whether a step set <c>rerun: false</c>. Without an earlier cast, each such step ran.</summary>
    internal bool RerunAsked { get; private set; }

    /// <summary>
    /// For a step with <c>rerun: false</c>, before its command would run: when the earlier cast has the step, adds its
    /// earlier events at their earlier offsets from <paramref name="start"/>, with their timing, and returns when the
    /// step ends. Null when the command must run: the earlier cast does not have the step, its earlier output holds
    /// text that is masked now, or ConPTY would start it elsewhere on the screen (<paramref name="startCursor"/>), as
    /// for keep-output.
    /// </summary>
    internal double? Reuse(
        List<CastEvent> events,
        int number,
        string maskedRun,
        ulong id,
        bool keepOutput,
        double start,
        double instantAt,
        (int Row, int Column)? startCursor,
        int width,
        int height)
    {
        RerunAsked = true;
        if (previous is null)
            return null;

        var earlier = previous.Peek(id);
        string? why = null;
        if (earlier is null)
            why = "the earlier cast has no output of it";
        else if (masker?.Hides(previous.Text(earlier)) == true)
            why = "its earlier output holds text that is masked now";
        else if (startCursor is { } cursor && previous.CursorAt(earlier.First, width, height) != cursor)
            why = "it starts at another place on the screen than its earlier output did";
        if (earlier is null || why is not null)
        {
            RerunNotes.Add($"Note: step {number} sets rerun: false, but {why}, so it ran: {Rerecording.Shorten(maskedRun)}");
            return null;
        }

        previous.Take(id);
        var first = events.Count;
        double end;
        if (earlier.Clock)
        {
            // Output that the clock timed keeps its earlier offsets from the command's start, in whole milliseconds as
            // the file has them, as a step that prints the same keeps them.
            var startMs = Rerecording.ToMs(start);
            for (var i = earlier.First; i < earlier.First + earlier.Count; i++)
                events.Add(previous.EventAt(i) with { Time = (startMs + previous.MsAt(i) - earlier.StartMs) / 1000.0 });
            end = (startMs + earlier.EndMs - earlier.StartMs) / 1000.0;
        }
        else
        {
            // Output that appears at once does so at this run's time, as a step that runs prints it.
            var text = previous.Text(earlier);
            if (text.Length > 0)
                events.Add(CastEvent.Output(Math.Round(instantAt, 6), text));
            end = instantAt;
        }

        // The earlier timing keys stay with the earlier events, so a later recording that runs the step compares as usual.
        var slot = new StepSlot(number, maskedRun, id, earlier.Timing, earlier.Clock, keepOutput, first, events.Count - first, start, end, instantAt);
        NotRun.Add(slot);
        Slots.Add(slot);
        return end;
    }

    /// <summary>
    /// Takes a composed step, whose events are at the end of <paramref name="events"/>, and returns when it ends.
    /// <paramref name="startCursor"/> is where ConPTY started, for a step that it ran. With
    /// <paramref name="colorsFollowReads"/>, colors and carriage returns do not count as output.
    /// </summary>
    internal double Finish(List<CastEvent> events, StepSlot slot, (int Row, int Column)? startCursor, int width, int height, bool colorsFollowReads)
    {
        var earlier = previous?.Take(slot.Id);
        if (earlier is null)
        {
            (slot.KeepOutput && previous is not null ? KeepMissing : Added).Add(slot);
            Slots.Add(slot);
            return slot.End;
        }

        var before = previous!.Text(earlier);
        var printed = Rerecording.PrintedText(events, slot.First, slot.Count);
        var same = colorsFollowReads
            ? string.Equals(Rerecording.WithoutColors(printed), Rerecording.WithoutColors(before), StringComparison.Ordinal)
            : string.Equals(printed, before, StringComparison.Ordinal);
        var keep = slot.KeepOutput && !same && MayKeep(slot, earlier, before, startCursor, width, height);
        (same ? Same : keep ? Kept : Changed).Add(slot);

        if (slot.Clock && (keep || (same && (slot.KeepOutput || slot.Timing == earlier.Timing))))
        {
            // The earlier events, at their earlier offsets from the command's start, in whole milliseconds as the
            // file has them. With nothing changed before the step, they land where they were.
            events.RemoveRange(slot.First, slot.Count);
            var startMs = Rerecording.ToMs(slot.Start);
            for (var i = earlier.First; i < earlier.First + earlier.Count; i++)
                events.Add(previous.EventAt(i) with { Time = (startMs + previous.MsAt(i) - earlier.StartMs) / 1000.0 });
            var end = (startMs + earlier.EndMs - earlier.StartMs) / 1000.0;
            // The timing keys of these events: a later recording without keep-output then sees what changed.
            Slots.Add(slot.Reused(earlier.Count, end, earlier.Timing));
            return end;
        }

        if (keep)
        {
            // Output that the clock does not time appears at once, so the earlier text takes this run's time.
            var at = slot.Count > 0 ? events[slot.First].Time : Math.Round(slot.InstantAt, 6);
            events.RemoveRange(slot.First, slot.Count);
            if (before.Length > 0)
                events.Add(CastEvent.Output(at, before));
            Slots.Add(slot.Reused(events.Count - slot.First, slot.End, slot.Timing));
            return slot.End;
        }

        Slots.Add(slot);
        return slot.End;
    }

    private bool MayKeep(StepSlot slot, PreviousStep earlier, string before, (int Row, int Column)? startCursor, int width, int height)
    {
        // A mask added after the earlier recording must hide this text too, so the step shows its new, masked output.
        if (masker?.Hides(before) == true)
        {
            KeepMasked.Add(slot);
            return false;
        }

        // ConPTY writes rows counted from where it started, so the earlier output fits only the same place.
        if (startCursor is { } cursor && previous!.CursorAt(earlier.First, width, height) != cursor)
        {
            KeepMoved.Add(slot);
            return false;
        }

        return true;
    }
}

/// <summary>The <c>st:steps</c> tag and what a recording says about comparing with the earlier cast (<see cref="Rerecorder"/>).</summary>
internal static class Rerecording
{
    internal const string TagPrefix = "st:steps=";

    /// <summary>
    /// Why a recording that writes an outputs file is not compared: a replay of the file must give the cast, so both
    /// come from this run.
    /// </summary>
    internal const string OutputsFileRecordsThisRun = "an outputs file keeps what each step prints now, so the cast does too";

    /// <summary>
    /// What finds a step in the earlier cast: its run text, how it runs, and what it shows. The cast is published, so
    /// <paramref name="maskedRun"/> is the text after masking: a hash of a secret would let anyone test guesses of it.
    /// </summary>
    internal static ulong StepId(bool pty, bool interactive, StepShow show, string maskedRun) =>
        Fnv64($"{(interactive ? "interactive" : pty ? "pty" : "pipe")}\n{(show == StepShow.Output ? "output" : "all")}\n{maskedRun}");

    /// <summary>
    /// The keys that time a step's output, hashed. A step whose output is the same keeps its earlier timing only when
    /// these are the same too: otherwise a new execution-duration or max-duration would have no effect. Typed text
    /// counts by its length, which is what times the output, so the cast holds no hash of a typed password.
    /// </summary>
    internal static uint TimingHash(
        double executionDuration,
        CastTimingSettings timing,
        bool normalizeTyping,
        double? timeout,
        bool typesKeys,
        double typingSpeed,
        double typingJitter,
        object? keys,
        SecretMasker? masker,
        string? colors = null)
    {
        var text = new StringBuilder();
        text.Append(CultureInfo.InvariantCulture, $"{executionDuration:R}|{timing.SilenceThreshold:R}|{timing.MaxIdle:R}|{timing.MaxDuration:R}|{timing.StreamOutput}|{normalizeTyping}|{timeout:R}");
        // The keys a step types, and its typing speed, time its output only when it types keys.
        if (typesKeys)
        {
            text.Append(CultureInfo.InvariantCulture, $"|{typingSpeed:R}|{typingJitter:R}|");
            Describe(text, keys);
        }

        // Output compared without its colors (WithoutColors) would otherwise keep them after a new color key.
        if (colors is not null)
            text.Append('|').Append(colors);

        var described = text.ToString();
        var hash = Fnv64(masker?.MaskUncounted(described) ?? described);
        return (uint)(hash ^ (hash >> 32));
    }

    /// <summary>The keys that color a pipe step's output, as text for <see cref="TimingHash"/>.</summary>
    internal static string DescribeColors(string stderrStyle, List<HighlightSpec>? highlights)
    {
        var text = new StringBuilder(stderrStyle);
        foreach (var highlight in highlights ?? [])
        {
            text.Append('|').Append(highlight.ColorOpen);
            foreach (var at in highlight.At)
                text.Append(',').Append(at);
        }

        return text.ToString();
    }

    /// <summary>
    /// <paramref name="text"/> without color sequences and carriage returns. Streamed pipe output has them where the
    /// reads fell, so two recordings of the same output can differ in nothing else.
    /// </summary>
    internal static string WithoutColors(string text)
    {
        var result = new StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '\r')
                continue;

            if (c == '\u001b' && i + 1 < text.Length && text[i + 1] == '[')
            {
                var end = i + 2;
                while (end < text.Length && text[end] is >= '0' and <= '?')
                    end++;
                if (end < text.Length && text[end] == 'm')
                {
                    i = end;
                    continue;
                }
            }

            result.Append(c);
        }

        return result.ToString();
    }

    private static void Describe(StringBuilder text, object? value)
    {
        switch (value)
        {
            case null:
                text.Append('~');
                break;
            case string s:
                text.Append('"').Append(s.Replace("\"", "\"\"", StringComparison.Ordinal)).Append('"');
                break;
            case IDictionary<object, object?> map:
                text.Append('{');
                foreach (var (key, item) in map)
                {
                    Describe(text, key);
                    text.Append(':');
                    if (key is "type")
                        text.Append(CultureInfo.InvariantCulture, $"{Convert.ToString(item, CultureInfo.InvariantCulture)?.Length}");
                    else
                        Describe(text, item);
                    text.Append(',');
                }

                text.Append('}');
                break;
            case System.Collections.IEnumerable list:
                text.Append('[');
                foreach (var item in list)
                {
                    Describe(text, item);
                    text.Append(',');
                }

                text.Append(']');
                break;
            case IFormattable formattable:
                text.Append(formattable.ToString(null, CultureInfo.InvariantCulture));
                break;
            default:
                text.Append(value);
                break;
        }
    }

    // Line endings do not count: a checkout with CRLF has the same steps as one with LF.
    private static ulong Fnv64(string text)
    {
        var hash = 14695981039346656037UL;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '\r')
            {
                if (i + 1 < text.Length && text[i + 1] == '\n')
                    i++;
                c = '\n';
            }

            hash ^= c;
            hash *= 1099511628211UL;
        }

        return hash;
    }

    /// <summary>
    /// The <c>st:steps</c> tag: how many events the cast has and where it ends in milliseconds, which tell whether the
    /// cast was edited since, then for each step its id, timing hash, c or d (clock), first event, event count, and
    /// start and end in milliseconds.
    /// </summary>
    internal static string FormatTag(IReadOnlyList<StepSlot> slots, int eventCount, long castMs)
    {
        var text = new StringBuilder(TagPrefix);
        text.Append(eventCount.ToString(CultureInfo.InvariantCulture)).Append(':').Append(castMs.ToString(CultureInfo.InvariantCulture));
        for (var i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            text.Append(' ');
            AppendHex(text, slot.Id, 16);
            text.Append(':');
            AppendHex(text, slot.Timing, 8);
            text.Append(':').Append(slot.Clock ? 'c' : 'd')
                .Append(':').Append(slot.First.ToString(CultureInfo.InvariantCulture))
                .Append(':').Append(slot.Count.ToString(CultureInfo.InvariantCulture))
                .Append(':').Append(ToMs(slot.Start).ToString(CultureInfo.InvariantCulture))
                .Append(':').Append(ToMs(slot.End).ToString(CultureInfo.InvariantCulture));
        }

        return text.ToString();
    }

    // Hex by hand: the framework's hex parsing and formatting for ulong and uint would add their own code to the binary.
    private static void AppendHex(StringBuilder text, ulong value, int digits)
    {
        for (var shift = (digits - 1) * 4; shift >= 0; shift -= 4)
            text.Append("0123456789abcdef"[(int)((value >> shift) & 0xF)]);
    }

    private static bool TryParseHex(string text, int digits, out ulong value)
    {
        value = 0;
        if (text.Length != digits)
            return false;
        foreach (var c in text)
        {
            var digit = c is >= '0' and <= '9' ? c - '0' : c is >= 'a' and <= 'f' ? c - 'a' + 10 : -1;
            if (digit < 0)
                return false;
            value = (value << 4) | (uint)digit;
        }

        return true;
    }

    /// <summary>
    /// Reads the tag's value after <see cref="TagPrefix"/>. False when it is malformed, or does not fit a cast of
    /// <paramref name="eventCount"/> events that ends at <paramref name="castMs"/>: an edit moved the events it names.
    /// </summary>
    internal static bool TryParseTag(string value, int eventCount, long castMs, out List<PreviousStep> steps)
    {
        steps = [];
        var entries = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (entries.Length == 0)
            return false;

        var cast = entries[0].Split(':');
        if (cast.Length != 2
            || !int.TryParse(cast[0], NumberStyles.None, CultureInfo.InvariantCulture, out var writtenCount)
            || !long.TryParse(cast[1], NumberStyles.None, CultureInfo.InvariantCulture, out var writtenMs)
            || writtenCount != eventCount
            || writtenMs != castMs)
        {
            return false;
        }

        for (var e = 1; e < entries.Length; e++)
        {
            var parts = entries[e].Split(':');
            if (parts.Length != 7
                || !TryParseHex(parts[0], 16, out var id)
                || !TryParseHex(parts[1], 8, out var timing)
                || parts[2] is not ("c" or "d")
                || !int.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out var first)
                || !int.TryParse(parts[4], NumberStyles.None, CultureInfo.InvariantCulture, out var count)
                || !long.TryParse(parts[5], NumberStyles.None, CultureInfo.InvariantCulture, out var startMs)
                || !long.TryParse(parts[6], NumberStyles.None, CultureInfo.InvariantCulture, out var endMs)
                || first > eventCount - count)
            {
                return false;
            }

            steps.Add(new PreviousStep(id, (uint)timing, parts[2] == "c", first, count, startMs, endMs));
        }

        return true;
    }

    // As the cast writer rounds an event's time, so a step's start is never after the event that comes next.
    internal static long ToMs(double seconds) => (long)Math.Floor(Math.Round(Math.Round(seconds, 6) * 1000, 6) + 0.5);

    /// <summary>
    /// The steps with their events where <see cref="CastOscFilter.RemovePrivateSequences(List{CastEvent}, int[])"/>
    /// left them: <paramref name="map"/> gives, for each event before the filter, the index it went to.
    /// </summary>
    internal static List<StepSlot> MapIndices(List<StepSlot> slots, int[] map)
    {
        var mapped = new List<StepSlot>(slots.Count);
        foreach (var slot in slots)
        {
            var first = map[slot.First];
            mapped.Add(slot.At(first, map[slot.First + slot.Count] - first));
        }

        return mapped;
    }

    /// <summary>
    /// The text of a step's output events, joined, as the cast file will hold it: with U+FFFD for a byte that is not
    /// UTF-8, as <see cref="JsonEscape.WriteJsonUtf8String"/> writes it.
    /// </summary>
    internal static string PrintedText(List<CastEvent> events, int first, int count)
    {
        var text = new StringBuilder();
        Span<char> chars = stackalloc char[2];
        for (var i = first; i < first + count; i++)
        {
            var ev = events[i];
            if (ev.Kind != CastEventKind.Output)
                continue;
            if (!ev.HasUtf8Output)
            {
                text.Append(ev.Data);
                continue;
            }

            var bytes = ev.Utf8Output.Span;
            while (!bytes.IsEmpty)
            {
                if (Rune.DecodeFromUtf8(bytes, out var rune, out var consumed) == System.Buffers.OperationStatus.Done)
                {
                    text.Append(chars[..rune.EncodeToUtf16(chars)]);
                    bytes = bytes[consumed..];
                }
                else
                {
                    text.Append('�');
                    bytes = bytes[1..];
                }
            }
        }

        return text.ToString();
    }

    /// <summary>
    /// What recording again did, for stderr: how many steps printed the same, kept their output, or changed, and the
    /// steps that changed. Empty for a first recording without keep-output.
    /// </summary>
    internal static List<string> Describe(Rerecorder result, string? problem, string castPath)
    {
        var lines = new List<string>();
        // Cards and hidden steps alone print nothing to compare, and the cast has no step ranges for the next run.
        if (result.Slots.Count == 0)
            return lines;

        var keeps = result.Slots.Exists(static slot => slot.KeepOutput);
        if (!result.HasPrevious)
        {
            if (problem == OutputsFileRecordsThisRun)
                lines.Add($"Note: the earlier {castPath} was not compared with this recording: {problem}.");
            else if (problem is not null)
                lines.Add($"Note: the earlier {castPath} was not compared with this recording: {problem}. The next recording compares with this one.");
            else if (keeps)
                lines.Add($"Note: keep-output: there is no earlier cast at {castPath} yet, so every step shows what it printed now.");
            if (result.RerunAsked)
                lines.Add(problem is null
                    ? $"Note: rerun: false: there is no earlier cast at {castPath} yet, so every step ran."
                    : "Note: rerun: false: the earlier cast was not used, so every step ran.");
            return lines;
        }

        var total = result.Slots.Count;
        var summary = new StringBuilder(result.Same.Count == total
            ? $"Compared with the earlier cast: all {total} step{(total == 1 ? "" : "s")} printed the same"
            : $"Compared with the earlier cast: {result.Same.Count} of {total} steps printed the same");
        if (result.NotRun.Count > 0)
            summary.Append(CultureInfo.InvariantCulture, $", {result.NotRun.Count} not run (rerun: false)");
        if (result.Kept.Count > 0)
            summary.Append(CultureInfo.InvariantCulture, $", {result.Kept.Count} kept the earlier output (keep-output)");
        if (result.Changed.Count > 0)
            summary.Append(CultureInfo.InvariantCulture, $", {result.Changed.Count} changed");
        var added = result.Added.Count + result.KeepMissing.Count;
        if (added > 0)
            summary.Append(CultureInfo.InvariantCulture, $", {added} {(added == 1 ? "is" : "are")} new");
        lines.Add(summary.ToString());

        foreach (var slot in result.Changed)
            lines.Add($"  changed: step {slot.Number}: {Shorten(slot.Run)}");
        foreach (var slot in result.Added)
            lines.Add($"  new: step {slot.Number}: {Shorten(slot.Run)}");
        foreach (var slot in result.KeepMissing)
            lines.Add($"Note: step {slot.Number} sets keep-output, but the earlier cast has no output of it, so it shows what it printed now: {Shorten(slot.Run)}");
        foreach (var slot in result.KeepMasked)
            lines.Add($"Note: step {slot.Number} sets keep-output, but its earlier output holds text that is masked now, so it shows what it printed now: {Shorten(slot.Run)}");
        foreach (var slot in result.KeepMoved)
            lines.Add($"Note: step {slot.Number} sets keep-output, but it starts at another place on the screen than its earlier output did, so it shows what it printed now: {Shorten(slot.Run)}");
        lines.AddRange(result.RerunNotes);
        return lines;
    }

    internal static string Shorten(string run)
    {
        var line = run.ReplaceLineEndings("\n");
        var newline = line.IndexOf('\n');
        if (newline >= 0)
            line = line[..newline] + " …";
        return line.Length > 60 ? line[..59] + "…" : line;
    }
}
