using System.Globalization;
using System.Text;
using System.Text.Json;

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

    /// <summary>The same step with its events elsewhere, or moved in time.</summary>
    internal StepSlot With(int first, int count, double start, double end) =>
        new(Number, Run, Id, Timing, Clock, KeepOutput, first, count, start, end, InstantAt + start - Start);
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
    /// The cast at <paramref name="path"/>, or null: when there is no file, and when the file cannot be used, which
    /// <paramref name="problem"/> then says, such as a cast without step ranges that an earlier scenetake wrote.
    /// </summary>
    internal static PreviousCast? TryRead(string path, out string? problem)
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
            return Parse(lines, out problem);
        }
        catch (JsonException)
        {
            problem = "it is not a cast that scenetake can read";
            return null;
        }
    }

    /// <summary>A cast held in memory, split into lines. Null, with <paramref name="problem"/>, when it cannot be used.</summary>
    internal static PreviousCast? Parse(string[] lines, out string? problem)
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
                case "r" when TryParseSize(data, out var width, out var height):
                    events.Add(CastEvent.Resize(time, width, height));
                    break;
                case "x":
                    continue;
                default:
                    problem = $"line {line + 1} has an event scenetake does not write";
                    return null;
            }

            ms.Add(now);
        }

        if (!Rerecording.TryParseTag(tag, events.Count, out var steps))
        {
            problem = "its step ranges cannot be read";
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
}

/// <summary>What recording again to an earlier cast did to each step. The lists hold the steps of <see cref="Slots"/>.</summary>
internal sealed class RerecordResult(
    List<CastEvent> events,
    List<StepSlot> slots,
    List<StepSlot> same,
    List<StepSlot> kept,
    List<StepSlot> changed,
    List<StepSlot> added,
    List<StepSlot> keepMissing)
{
    /// <summary>The cast's events, with the steps that kept their earlier events.</summary>
    internal List<CastEvent> Events { get; } = events;

    /// <summary>Every step, where its events are in <see cref="Events"/>: the st:steps tag.</summary>
    internal List<StepSlot> Slots { get; } = slots;

    /// <summary>The steps that printed the same as in the earlier cast.</summary>
    internal List<StepSlot> Same { get; } = same;

    /// <summary>The steps that printed something else and kept the earlier output (keep-output).</summary>
    internal List<StepSlot> Kept { get; } = kept;

    /// <summary>The steps that printed something else.</summary>
    internal List<StepSlot> Changed { get; } = changed;

    /// <summary>The steps that the earlier cast does not have, without keep-output.</summary>
    internal List<StepSlot> Added { get; } = added;

    /// <summary>The steps with keep-output that the earlier cast does not have.</summary>
    internal List<StepSlot> KeepMissing { get; } = keepMissing;
}

/// <summary>
/// Recording again to the file an earlier recording wrote. A step that prints what it printed then keeps the earlier
/// events, with their timing, and a step with <c>keep-output: true</c> keeps them even when its output changed. So a
/// scenario recorded again with nothing changed gives the same cast, and the same SVG, though PTY output is timed
/// by the wall clock. Only the steps whose output changed differ.
/// </summary>
internal static class Rerecording
{
    internal const string TagPrefix = "st:steps=";

    /// <summary>What finds a step in the earlier cast: its run text, how it runs, and what it shows.</summary>
    internal static ulong StepId(bool pty, bool interactive, StepShow show, string run) =>
        Fnv64($"{(interactive ? "interactive" : pty ? "pty" : "pipe")}\n{(show == StepShow.Output ? "output" : "all")}\n{run}");

    /// <summary>
    /// The keys that time a step's output, hashed. A step whose output is the same keeps its earlier timing only when
    /// these are the same too: otherwise a new execution-duration or max-duration would have no effect.
    /// </summary>
    internal static uint TimingHash(
        double executionDuration,
        CastTimingSettings timing,
        bool normalizeTyping,
        double? timeout,
        bool typesKeys,
        double typingSpeed,
        double typingJitter,
        object? keys)
    {
        var text = new StringBuilder();
        text.Append(CultureInfo.InvariantCulture, $"{executionDuration:R}|{timing.SilenceThreshold:R}|{timing.MaxIdle:R}|{timing.MaxDuration:R}|{timing.StreamOutput}|{normalizeTyping}|{timeout:R}");
        // The keys a step types, and its typing speed, time its output only when it types keys.
        if (typesKeys)
        {
            text.Append(CultureInfo.InvariantCulture, $"|{typingSpeed:R}|{typingJitter:R}|");
            Describe(text, keys);
        }

        var hash = Fnv64(text.ToString());
        return (uint)(hash ^ (hash >> 32));
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

    private static ulong Fnv64(string text)
    {
        var hash = 14695981039346656037UL;
        foreach (var c in text)
        {
            hash ^= c;
            hash *= 1099511628211UL;
        }

        return hash;
    }

    /// <summary>The <c>st:steps</c> tag: for each step, its id, timing hash, c or d (clock), first event, event count, and start and end in milliseconds.</summary>
    internal static string FormatTag(IReadOnlyList<StepSlot> slots)
    {
        var text = new StringBuilder(TagPrefix);
        for (var i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            if (i > 0)
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

    /// <summary>Reads the tag's value after <see cref="TagPrefix"/>. False when an entry is malformed or names events the cast does not have.</summary>
    internal static bool TryParseTag(string value, int eventCount, out List<PreviousStep> steps)
    {
        steps = [];
        foreach (var entry in value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = entry.Split(':');
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

    private static long ToMs(double seconds) => (long)Math.Round(seconds * 1000, MidpointRounding.AwayFromZero);

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
            mapped.Add(slot.With(first, map[slot.First + slot.Count] - first, slot.Start, slot.End));
        }

        return mapped;
    }

    /// <summary>
    /// The cast's events with each step compared to the earlier cast. A step that prints the same as before, with the
    /// same timing keys, keeps the earlier events, and the steps after it move with where it ends. A step with
    /// keep-output keeps the earlier events whatever it printed. <paramref name="previous"/> null keeps every step as
    /// recorded.
    /// </summary>
    internal static RerecordResult Apply(List<CastEvent> events, List<StepSlot> slots, PreviousCast? previous)
    {
        var result = new RerecordResult(new List<CastEvent>(events.Count), new List<StepSlot>(slots.Count), [], [], [], [], []);
        var output = result.Events;
        // How far the events after a kept step move: a whole number of milliseconds.
        var shift = 0.0;
        var next = 0;

        foreach (var slot in slots)
        {
            for (; next < slot.First; next++)
                output.Add(Moved(events[next], shift));

            var start = slot.Start + shift;
            var end = slot.End + shift;
            var first = output.Count;
            var earlier = previous?.Take(slot.Id);
            if (earlier is { } step)
            {
                var same = string.Equals(Text(events, slot.First, slot.Count), previous!.Text(step), StringComparison.Ordinal);
                if (slot.Clock && (slot.KeepOutput || (same && slot.Timing == step.Timing)))
                {
                    // The earlier events, at their earlier offsets from the command's start, in whole milliseconds as
                    // the file has them. With nothing changed before the step, they land where they were.
                    var startMs = ToMs(start);
                    for (var i = step.First; i < step.First + step.Count; i++)
                        output.Add(previous.EventAt(i) with { Time = (startMs + previous.MsAt(i) - step.StartMs) / 1000.0 });
                    var keptEnd = (startMs + step.EndMs - step.StartMs) / 1000.0;
                    shift = keptEnd - slot.End;
                    end = keptEnd;
                }
                else if (!slot.Clock && slot.KeepOutput && !same)
                {
                    // Output that the clock does not time appears at once, so the earlier text takes this run's time.
                    var text = previous.Text(step);
                    if (text.Length > 0)
                        output.Add(CastEvent.Output(slot.Count > 0 ? Moved(events[slot.First], shift).Time : Math.Round(slot.InstantAt + shift, 6), text));
                }
                else
                {
                    for (var i = slot.First; i < slot.First + slot.Count; i++)
                        output.Add(Moved(events[i], shift));
                }

                (same ? result.Same : slot.KeepOutput ? result.Kept : result.Changed).Add(slot);
            }
            else
            {
                for (var i = slot.First; i < slot.First + slot.Count; i++)
                    output.Add(Moved(events[i], shift));
                (slot.KeepOutput && previous is not null ? result.KeepMissing : result.Added).Add(slot);
            }

            next = slot.First + slot.Count;
            result.Slots.Add(slot.With(first, output.Count - first, start, end));
        }

        for (; next < events.Count; next++)
            output.Add(Moved(events[next], shift));

        return result;
    }

    // Rounding again to the microsecond, as the recording rounds, gives the time the earlier recording had.
    private static CastEvent Moved(CastEvent ev, double shift) =>
        shift == 0 ? ev : ev with { Time = Math.Round(ev.Time + shift, 6) };

    /// <summary>The text of output events, joined, as the cast file holds it: a byte that is not UTF-8 is U+FFFD, as <see cref="JsonEscape.WriteJsonUtf8String"/> writes it.</summary>
    private static string Text(List<CastEvent> events, int first, int count)
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
    internal static List<string> Describe(RerecordResult result, bool hadPrevious, string? problem, string castPath)
    {
        var lines = new List<string>();
        var keeps = result.Slots.Exists(static slot => slot.KeepOutput);
        if (!hadPrevious)
        {
            if (problem is not null)
                lines.Add($"Note: the earlier {castPath} was not compared with this recording: {problem}. The next recording compares with this one.");
            else if (keeps)
                lines.Add($"Note: keep-output: there is no earlier cast at {castPath} yet, so every step shows what it printed now.");
            return lines;
        }

        var total = result.Slots.Count;
        var summary = new StringBuilder(result.Same.Count == total
            ? $"Compared with the earlier cast: all {total} step{(total == 1 ? "" : "s")} printed the same"
            : $"Compared with the earlier cast: {result.Same.Count} of {total} steps printed the same");
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
        return lines;
    }

    private static string Shorten(string run)
    {
        var line = run.ReplaceLineEndings("\n");
        var newline = line.IndexOf('\n');
        if (newline >= 0)
            line = line[..newline] + " …";
        return line.Length > 60 ? line[..59] + "…" : line;
    }
}
