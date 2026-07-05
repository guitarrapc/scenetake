using System.Text;
using System.Runtime.InteropServices;

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
        return result;
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
    internal static List<string> SplitByRawSegments(string highlighted, IReadOnlyList<string> rawSegments)
    {
        var results = new List<string>(rawSegments.Count);
        var visibleStart = 0;
        foreach (var raw in rawSegments)
        {
            var length = VisibleLength(NormalizeToLf(raw));
            var visibleEnd = visibleStart + length;
            results.Add(ExtractVisibleRange(highlighted, visibleStart, visibleEnd));
            visibleStart = visibleEnd;
        }

        return results;
    }

    private static int VisibleLength(string text)
    {
        var visible = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\u001b')
            {
                i = SkipAnsiSequence(text, i) - 1;
                continue;
            }

            visible++;
        }

        return visible;
    }

    private static int SkipAnsiSequence(string text, int index)
    {
        var end = index + 1;
        if (end < text.Length && text[end] == '[')
        {
            end++;
            while (end < text.Length)
            {
                var ch = text[end];
                if ((uint)(ch - '0') <= 9 || ch == ';')
                {
                    end++;
                    continue;
                }

                if (ch is >= 'A' and <= 'Z' or >= 'a' and <= 'z')
                {
                    end++;
                    break;
                }

                end++;
                break;
            }

            return end;
        }

        while (end < text.Length)
        {
            var ch = text[end];
            if (ch is >= 'A' and <= 'Z' or >= 'a' and <= 'z')
            {
                end++;
                break;
            }

            end++;
        }

        return end;
    }

    private static string ExtractVisibleRange(string text, int visibleStart, int visibleEnd)
    {
        if (visibleEnd <= visibleStart)
            return "";

        var sb = new System.Text.StringBuilder();
        var activeSgr = new System.Text.StringBuilder();
        var visible = 0;
        var prefixedActiveSgr = false;
        for (var index = 0; index < text.Length && visible < visibleEnd; index++)
        {
            if (text[index] == '\u001b')
            {
                var end = SkipAnsiSequence(text, index);
                if (IsSgrSequence(text, index, end))
                {
                    if (ContainsReset(text, index, end))
                    {
                        activeSgr.Clear();
                    }
                    else
                    {
                        activeSgr.Append(text, index, end - index);
                    }
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

    private static bool IsSgrSequence(string text, int start, int end) =>
        end > start + 2 && text[start] == '\u001b' && text[start + 1] == '[' && text[end - 1] == 'm';

    private static bool ContainsReset(string text, int start, int end)
    {
        var span = text.AsSpan(start, end - start);
        var i = 2; // skip ESC[
        var hasDigits = false;
        var hasCurrentValue = false;
        var value = 0;
        while (i < span.Length - 1) // final 'm'
        {
            var ch = span[i++];
            if ((uint)(ch - '0') <= 9)
            {
                hasDigits = true;
                hasCurrentValue = true;
                value = value * 10 + (ch - '0');
                continue;
            }

            if (ch == ';')
            {
                if (!hasCurrentValue || value == 0)
                    return true;
                value = 0;
                hasCurrentValue = false;
                continue;
            }

            return true;
        }

        return !hasDigits || !hasCurrentValue || value == 0;
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

    private static string NormalizeToLf(string text)
    {
        if (text.IndexOf('\r') < 0)
            return text;

        var sb = new System.Text.StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (ch == '\r')
            {
                sb.Append('\n');
                if (i + 1 < text.Length && text[i + 1] == '\n')
                    i++;
                continue;
            }

            sb.Append(ch);
        }

        return sb.ToString();
    }
}
