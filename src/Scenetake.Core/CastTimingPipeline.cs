using System.Buffers;
using System.Text;
using System.Runtime.InteropServices;

namespace Scenetake.Core;

internal readonly record struct CastTimingSettings(
    double SilenceThreshold,
    double MaxIdle,
    double? MaxDuration,
    bool StreamOutput);

internal static class CastTimingSettingsExtensions
{
    internal static bool UsesStreamEmission(this CastTimingSettings settings) =>
        settings.StreamOutput || (settings.MaxDuration is double maxDuration && maxDuration > 0);
}

internal static class CastTimingPipeline
{
    internal static double[] AdjustTimeline(ReadOnlySpan<double> relativeSeconds, CastTimingSettings settings)
    {
        if (relativeSeconds.Length == 0)
            return [];

        var times = relativeSeconds.ToArray();
        AdjustTimelineInPlace(times, settings);
        return times;
    }

    internal static void AdjustTimelineInPlace(Span<double> times, CastTimingSettings settings)
    {
        if (times.Length == 0)
            return;

        CollapseIdleGaps(times, settings.SilenceThreshold, settings.MaxIdle);
        if (settings.MaxDuration is double maxDuration && maxDuration > 0)
            LinearCompress(times, maxDuration);
    }

    internal static void CollapseIdleGaps(Span<double> times, double silenceThreshold, double maxIdle)
    {
        if (times.Length <= 1 || silenceThreshold < 0 || maxIdle < 0)
            return;

        var previousOriginal = times[0];
        var cursor = previousOriginal;
        times[0] = cursor;
        for (var i = 1; i < times.Length; i++)
        {
            var currentOriginal = times[i];
            var gap = currentOriginal - previousOriginal;
            if (gap > silenceThreshold)
                gap = maxIdle;
            cursor += gap;
            times[i] = cursor;
            previousOriginal = currentOriginal;
        }
    }

    /// <summary>
    /// Collapse long gaps measured on <paramref name="originalTimes"/> while writing adjusted values to <paramref name="emitTimes"/>.
    /// </summary>
    internal static void CollapseIdleGapsWithOriginal(
        Span<double> emitTimes,
        ReadOnlySpan<double> originalTimes,
        double silenceThreshold,
        double maxIdle)
    {
        if (emitTimes.Length <= 1 || emitTimes.Length != originalTimes.Length || silenceThreshold < 0 || maxIdle < 0)
            return;

        var emitGaps = new double[emitTimes.Length - 1];
        for (var i = 1; i < emitTimes.Length; i++)
            emitGaps[i - 1] = emitTimes[i] - emitTimes[i - 1];

        var previousOriginal = originalTimes[0];
        for (var i = 1; i < emitTimes.Length; i++)
        {
            var originalGap = originalTimes[i] - previousOriginal;
            emitTimes[i] = originalGap > silenceThreshold
                ? emitTimes[i - 1] + maxIdle
                : emitTimes[i - 1] + emitGaps[i - 1];
            previousOriginal = originalTimes[i];
        }
    }

    internal static void LinearCompress(Span<double> times, double maxDuration)
    {
        if (times.Length == 0)
            return;

        var total = times[^1];
        if (total <= maxDuration || total <= 0)
            return;

        var scale = maxDuration / total;
        for (var i = 0; i < times.Length; i++)
            times[i] *= scale;
    }

    internal static List<(double Time, string Text)> CoalesceTimedText(IReadOnlyList<(double Time, string Text)> chunks)
    {
        if (chunks.Count == 0)
            return [];

        var result = new List<(double Time, string Text)>(chunks.Count);
        var currentTime = chunks[0].Time;
        var currentText = chunks[0].Text;
        StringBuilder? currentBuilder = null;
        for (var i = 1; i < chunks.Count; i++)
        {
            var (time, text) = chunks[i];
            if (HaveSameTime(time, currentTime))
            {
                currentBuilder ??= new StringBuilder(currentText.Length + text.Length).Append(currentText);
                currentBuilder.Append(text);
                continue;
            }

            result.Add((currentTime, currentBuilder?.ToString() ?? currentText));
            currentTime = time;
            currentText = text;
            currentBuilder = null;
        }

        result.Add((currentTime, currentBuilder?.ToString() ?? currentText));
        return result;
    }

    internal static List<(double Time, byte[] Data)> CoalesceTimedUtf8(IReadOnlyList<(double Time, ReadOnlyMemory<byte> Data)> chunks)
    {
        if (chunks.Count == 0)
            return [];

        static byte[] GetBytes(ReadOnlyMemory<byte> data)
        {
            return MemoryMarshal.TryGetArray(data, out ArraySegment<byte> segment)
                   && segment.Array is not null
                   && segment.Offset == 0
                   && segment.Count == segment.Array.Length
                ? segment.Array
                : data.ToArray();
        }

        var result = new List<(double Time, byte[] Data)>(chunks.Count);
        var currentTime = chunks[0].Time;
        var currentLength = chunks[0].Data.Length;
        var buffers = new List<byte[]>(4) { GetBytes(chunks[0].Data) };
        for (var i = 1; i < chunks.Count; i++)
        {
            var (time, data) = chunks[i];
            if (HaveSameTime(time, currentTime))
            {
                buffers.Add(GetBytes(data));
                currentLength += data.Length;
                continue;
            }

            result.Add((currentTime, ConcatUtf8(buffers, currentLength)));
            currentTime = time;
            buffers = [GetBytes(data)];
            currentLength = data.Length;
        }

        result.Add((currentTime, ConcatUtf8(buffers, currentLength)));
        MoveSplitCharactersForward(result);
        return result;
    }

    // A read can end inside a character. Its first bytes go to the next chunk, because each event is written as text
    // of its own: the two halves would each become U+FFFD, and the same output read otherwise would look different.
    private static void MoveSplitCharactersForward(List<(double Time, byte[] Data)> chunks)
    {
        for (var i = 0; i < chunks.Count - 1; i++)
        {
            var (time, data) = chunks[i];
            var split = IncompleteTailLength(data);
            if (split == 0)
                continue;

            var (nextTime, nextData) = chunks[i + 1];
            var joined = new byte[split + nextData.Length];
            data.AsSpan(data.Length - split).CopyTo(joined);
            nextData.CopyTo(joined, split);
            chunks[i + 1] = (nextTime, joined);
            if (split == data.Length)
                chunks.RemoveAt(i--);
            else
                chunks[i] = (time, data[..^split]);
        }
    }

    private static int IncompleteTailLength(ReadOnlySpan<byte> data)
    {
        for (var length = 1; length <= Math.Min(3, data.Length); length++)
        {
            var value = data[^length];
            if (value < 0x80)
                return 0;
            if (value >= 0xC0)
                return Rune.DecodeFromUtf8(data[^length..], out _, out _) == OperationStatus.NeedMoreData ? length : 0;
        }

        return 0;
    }

    internal static void MergeBurstGaps(Span<double> times, ReadOnlySpan<double> originalSeconds, double burstThreshold)
    {
        if (times.Length <= 1 || burstThreshold <= 0)
            return;

        for (var i = 1; i < times.Length; i++)
        {
            if (originalSeconds[i] - originalSeconds[i - 1] <= burstThreshold)
                times[i] = times[i - 1];
        }
    }

    internal static bool HaveSameTime(double left, double right) =>
        Math.Abs(left - right) <= 1e-9;

    private static byte[] ConcatUtf8(List<byte[]> buffers, int totalLength)
    {
        if (buffers.Count == 1)
            return buffers[0];

        var merged = new byte[totalLength];
        var offset = 0;
        foreach (var buffer in buffers)
        {
            buffer.CopyTo(merged.AsSpan(offset));
            offset += buffer.Length;
        }

        return merged;
    }
}

internal static class HighlightSplitter
{
    /// <summary>
    /// The highlighted text cut back into the reads it came from, by visible characters. A sequence counts as no
    /// characters, by the same rules as the highlighting (<see cref="EscapeSequences"/>), so a cut falls only right
    /// after a visible character, never inside a sequence where the color that a part closes would end it. Each read
    /// is counted within the joined output, since one can end inside a sequence. The last part runs to the end of the
    /// text, with the sequences after the last visible character, such as one that shows the cursor again.
    /// </summary>
    internal static List<string> SplitByRawSegments(string highlighted, IReadOnlyList<string> rawSegments)
    {
        var raw = string.Concat(rawSegments);
        var rawInside = EscapeSequences.Inside(raw);
        var results = new List<string>(rawSegments.Count);
        // One walk over the highlighted text. Each part goes on from where the one before it stopped, with the colors
        // set so far, which is where and how a walk from the start would arrive. Walking from the start for every part
        // took time in reads × length, seconds for a long streamed output.
        var index = 0;
        var visible = 0;
        var activeSgr = new System.Text.StringBuilder();
        var part = new System.Text.StringBuilder();
        var visibleStart = 0;
        var offset = 0;
        for (var k = 0; k < rawSegments.Count; k++)
        {
            var visibleEnd = k == rawSegments.Count - 1
                ? int.MaxValue
                : visibleStart + VisibleLength(raw, rawInside, offset, rawSegments[k].Length);
            results.Add(ExtractVisibleRange(highlighted, ref index, ref visible, activeSgr, part, visibleStart, visibleEnd));
            visibleStart = visibleEnd;
            offset += rawSegments[k].Length;
        }

        return results;
    }

    // The visible characters of raw[start..start+length], as the highlighting counts them after it turned each CR and
    // CRLF into one LF. A CR that ends a sequence leaves its place to the LF after it, which then ends the sequence.
    private static int VisibleLength(string raw, bool[] inside, int start, int length)
    {
        var visible = 0;
        for (var i = start; i < start + length; i++)
        {
            var c = raw[i];
            if (c == '\u001b' || inside[i]
                || (c == '\r' && i + 1 < raw.Length && raw[i + 1] == '\n')
                || (c == '\n' && i > 0 && raw[i - 1] == '\r' && inside[i - 1]))
                continue;
            visible++;
        }

        return visible;
    }

    // The visible characters visibleStart..visibleEnd of text, with the sequences around them. The walk is at index,
    // after visible characters, with the colors in activeSgr set: the previous part stopped there, at visibleStart, or
    // the text ended before it. A part never walks back, so an empty part leaves the walk where it is.
    private static string ExtractVisibleRange(
        string text, ref int index, ref int visible, System.Text.StringBuilder activeSgr, System.Text.StringBuilder sb, int visibleStart, int visibleEnd)
    {
        if (visibleEnd <= visibleStart)
            return "";

        sb.Clear();
        var prefixedActiveSgr = false;
        for (; index < text.Length && visible < visibleEnd; index++)
        {
            if (text[index] == '\u001b')
            {
                var end = EscapeSequences.End(text, index);
                if (IsSgrSequence(text, index, end))
                {
                    // `0;32` resets and then sets green, so the part after a cut needs it again.
                    ReadSgr(text, index, end, out var resets, out var sets);
                    if (resets)
                        activeSgr.Clear();
                    if (sets)
                        activeSgr.Append(text, index, end - index);
                }

                if (visible >= visibleStart)
                {
                    if (!prefixedActiveSgr)
                    {
                        sb.Append(activeSgr);
                        prefixedActiveSgr = true;
                    }
                    sb.Append(text, index, end - index);
                }
                index = end - 1;
                continue;
            }

            if (visible >= visibleStart)
            {
                if (!prefixedActiveSgr)
                {
                    sb.Append(activeSgr);
                    prefixedActiveSgr = true;
                }
                sb.Append(text[index]);
            }
            visible++;
        }

        if (prefixedActiveSgr && activeSgr.Length > 0 && !EndsWithReset(sb))
            sb.Append("\u001b[0m");

        return sb.ToString();
    }

    // ESC [ params m, where params are digits, semicolons, and colons. A private marker such as `>` makes it another
    // sequence that also ends in m.
    internal static bool IsSgrSequence(string text, int start, int end)
    {
        if (end <= start + 2 || text[start] != '\u001b' || text[start + 1] != '[' || text[end - 1] != 'm')
            return false;
        for (var i = start + 2; i < end - 1; i++)
        {
            if (text[i] is not ((>= '0' and <= '9') or ';' or ':'))
                return false;
        }

        return true;
    }

    // Whether an SGR resets (a parameter that is empty or 0) and whether it sets anything. The values after 38, 48, or
    // 58 belong to that color, so a 0 among them, as in `38;2;255;0;0`, is no reset. A colon starts sub-parameters of
    // the one before it, such as the underline style in `4:3`.
    internal static void ReadSgr(string text, int start, int end, out bool resets, out bool sets)
    {
        resets = false;
        sets = false;
        var colorSelector = false; // after 38, 48, or 58: the 5 or 2 that says how many color values follow
        var colorValues = 0;
        var i = start + 2;
        while (true)
        {
            var value = 0;
            var hasValue = false;
            var hasSubParameters = false;
            for (; i < end - 1 && text[i] != ';'; i++)
            {
                if (text[i] == ':')
                {
                    hasSubParameters = true;
                }
                else if (!hasSubParameters)
                {
                    hasValue = true;
                    value = Math.Min(value * 10 + (text[i] - '0'), 1000);
                }
            }

            if (colorValues > 0)
            {
                colorValues--;
            }
            else if (colorSelector)
            {
                colorValues = value == 5 ? 1 : value == 2 ? 3 : 0;
                colorSelector = false;
            }
            else if (!hasValue || value == 0)
            {
                resets = true;
            }
            else
            {
                sets = true;
                colorSelector = value is 38 or 48 or 58 && !hasSubParameters;
            }

            if (i >= end - 1)
                return;
            i++;
        }
    }

    private static bool EndsWithReset(StringBuilder sb)
    {
        const string reset = "\u001b[0m";
        if (sb.Length < reset.Length)
            return false;

        for (var i = 0; i < reset.Length; i++)
        {
            if (sb[sb.Length - reset.Length + i] != reset[i])
                return false;
        }

        return true;
    }
}
