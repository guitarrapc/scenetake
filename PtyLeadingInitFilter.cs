using System.Buffers;

internal sealed class PtyLeadingInitFilter
{
    private const int LeadingByteLimit = 4096;
    private const int CleanupByteLimit = 4096;
    private const byte Esc = 0x1B;
    private const byte Bel = 0x07;

    private readonly List<byte> pending = [];
    private FilterMode mode = FilterMode.LeadingInit;
    private int leadingBytesSeen;
    private int cleanupBytesSeen;

    internal int StrippedByteCount { get; private set; }

    internal ReadOnlyMemory<byte> Process(ReadOnlyMemory<byte> input)
    {
        if (input.IsEmpty)
            return ReadOnlyMemory<byte>.Empty;

        if (pending.Count == 0 && TryProcessInputWithoutBuffering(input, out var directOutput))
            return directOutput;

        AppendPending(input.Span);

        var output = new List<byte>(pending.Count);
        while (pending.Count > 0)
        {
            var progressed = mode switch
            {
                FilterMode.LeadingInit => ProcessLeadingInit(output, out _),
                FilterMode.Passthrough => ProcessPassthrough(output, out _),
                FilterMode.AfterAlternateScreenExit => ProcessAfterAlternateScreenExit(output, out _),
                _ => false,
            };

            if (!progressed)
                break;
        }

        return output.Count == 0 ? ReadOnlyMemory<byte>.Empty : output.ToArray();
    }

    private bool TryProcessInputWithoutBuffering(ReadOnlyMemory<byte> input, out ReadOnlyMemory<byte> output)
    {
        output = ReadOnlyMemory<byte>.Empty;
        var span = input.Span;
        if (span.IndexOf(Esc) >= 0)
            return false;

        if (mode == FilterMode.Passthrough)
        {
            output = input.ToArray();
            return true;
        }

        if (mode == FilterMode.LeadingInit && IsPrintableUtf8Start(span[0]))
        {
            mode = FilterMode.Passthrough;
            output = input.ToArray();
            return true;
        }

        return false;
    }

    internal ReadOnlyMemory<byte> Complete()
    {
        if (pending.Count == 0)
            return ReadOnlyMemory<byte>.Empty;

        var output = pending.ToArray();
        pending.Clear();
        mode = FilterMode.Passthrough;
        return output;
    }

    private bool ProcessLeadingInit(List<byte> output, out bool incomplete)
    {
        incomplete = false;
        var first = pending[0];
        if (IsPrintableUtf8Start(first))
        {
            mode = FilterMode.Passthrough;
            AppendAndClear(output, pending);
            return true;
        }

        if (first == Esc)
        {
            if (!TryConsumeEscape(output, EscapeContext.LeadingInit, out incomplete))
            {
                if (incomplete && leadingBytesSeen + pending.Count > LeadingByteLimit)
                {
                    mode = FilterMode.Passthrough;
                    AppendAndClear(output, pending);
                    return true;
                }

                return false;
            }
        }
        else if (first is (byte)'\r' or (byte)'\n' or (byte)'\t')
        {
            StripPending(1);
        }
        else
        {
            output.Add(first);
            pending.RemoveAt(0);
        }

        leadingBytesSeen++;
        if (mode == FilterMode.LeadingInit && leadingBytesSeen > LeadingByteLimit)
        {
            mode = FilterMode.Passthrough;
            AppendAndClear(output, pending);
        }

        return true;
    }

    private bool ProcessPassthrough(List<byte> output, out bool incomplete)
    {
        incomplete = false;

        var escIndex = pending.IndexOf(Esc);
        if (escIndex < 0)
        {
            AppendAndClear(output, pending);
            return true;
        }

        if (escIndex > 0)
        {
            AppendRange(output, pending, 0, escIndex);
            pending.RemoveRange(0, escIndex);
            return true;
        }

        return TryConsumeEscape(output, EscapeContext.Passthrough, out incomplete);
    }

    private bool ProcessAfterAlternateScreenExit(List<byte> output, out bool incomplete)
    {
        incomplete = false;
        var first = pending[0];
        if (IsPrintableUtf8Start(first))
        {
            if (TryFindCursorPositionCsiEnd(0, out var cursorCsiEnd))
            {
                StripPending(cursorCsiEnd);
                cleanupBytesSeen += cursorCsiEnd;
                return true;
            }

            if (PendingEndsWithIncompleteCsi()
                || (pending.IndexOf(Esc) >= 0 && cleanupBytesSeen + pending.Count <= CleanupByteLimit))
            {
                incomplete = true;
                return false;
            }

            mode = FilterMode.Passthrough;
            AppendAndClear(output, pending);
            return true;
        }

        if (first == Esc)
        {
            if (!TryConsumeEscape(output, EscapeContext.AfterAlternateScreenExit, out incomplete))
            {
                if (incomplete && cleanupBytesSeen + pending.Count > CleanupByteLimit)
                {
                    mode = FilterMode.Passthrough;
                    AppendAndClear(output, pending);
                    return true;
                }

                return false;
            }
        }
        else if (first is (byte)'\r' or (byte)'\n' or (byte)'\t')
        {
            StripPending(1);
        }
        else
        {
            StripPending(1);
        }

        cleanupBytesSeen++;
        if (mode == FilterMode.AfterAlternateScreenExit && cleanupBytesSeen > CleanupByteLimit)
        {
            mode = FilterMode.Passthrough;
            AppendAndClear(output, pending);
        }

        return true;
    }

    private void AppendPending(ReadOnlySpan<byte> input)
    {
        pending.EnsureCapacity(pending.Count + input.Length);
        foreach (var value in input)
            pending.Add(value);
    }

    private bool TryConsumeEscape(List<byte> output, EscapeContext context, out bool incomplete)
    {
        incomplete = false;
        if (pending.Count < 2)
        {
            incomplete = true;
            return false;
        }

        return pending[1] switch
        {
            (byte)'[' => TryConsumeCsi(output, context, out incomplete),
            (byte)']' => TryConsumeOsc(output, context, out incomplete),
            _ => ConsumeUnknownEscape(output),
        };
    }

    private bool TryConsumeCsi(List<byte> output, EscapeContext context, out bool incomplete)
    {
        incomplete = false;
        var finalIndex = -1;
        for (var i = 2; i < pending.Count; i++)
        {
            var value = pending[i];
            if (value is >= 0x40 and <= 0x7E)
            {
                finalIndex = i;
                break;
            }
        }

        if (finalIndex < 0)
        {
            incomplete = true;
            return false;
        }

        var finalByte = pending[finalIndex];
        var length = finalIndex + 1;
        var parameterCount = CountCsiParameterBytes(finalIndex);
        byte[]? rentedParameters = null;
        Span<byte> parameterBuffer = parameterCount <= 128
            ? stackalloc byte[parameterCount]
            : rentedParameters = ArrayPool<byte>.Shared.Rent(parameterCount);
        var parameters = parameterBuffer[..parameterCount];
        CopyCsiParameters(finalIndex, parameters);

        try
        {
            if (IsAlternateScreenMode(finalByte, parameters, out var alternateEnabled))
            {
                AppendRange(output, pending, 0, length);
                pending.RemoveRange(0, length);
                if (alternateEnabled)
                {
                    mode = FilterMode.Passthrough;
                }
                else
                {
                    mode = FilterMode.AfterAlternateScreenExit;
                    cleanupBytesSeen = 0;
                }
                return true;
            }

            if (ShouldStripCsi(finalByte, parameters, context))
            {
                StrippedByteCount += length;
                pending.RemoveRange(0, length);
                return true;
            }

            AppendRange(output, pending, 0, length);
            pending.RemoveRange(0, length);
            return true;
        }
        finally
        {
            if (rentedParameters is not null)
                ArrayPool<byte>.Shared.Return(rentedParameters);
        }
    }

    private bool TryConsumeOsc(List<byte> output, EscapeContext context, out bool incomplete)
    {
        incomplete = false;
        for (var i = 2; i < pending.Count; i++)
        {
            if (pending[i] == Bel)
            {
                if (context == EscapeContext.Passthrough)
                {
                    AppendRange(output, pending, 0, i + 1);
                    pending.RemoveRange(0, i + 1);
                    return true;
                }

                StripPending(i + 1);
                return true;
            }

            if (pending[i] == Esc && i + 1 < pending.Count && pending[i + 1] == (byte)'\\')
            {
                if (context == EscapeContext.Passthrough)
                {
                    AppendRange(output, pending, 0, i + 2);
                    pending.RemoveRange(0, i + 2);
                    return true;
                }

                StripPending(i + 2);
                return true;
            }
        }

        incomplete = true;
        return false;
    }

    private bool ConsumeUnknownEscape(List<byte> output)
    {
        if (mode == FilterMode.AfterAlternateScreenExit)
        {
            StripPending(2);
            return true;
        }

        output.Add(pending[0]);
        output.Add(pending[1]);
        pending.RemoveRange(0, 2);
        return true;
    }

    private int CountCsiParameterBytes(int finalIndex)
    {
        var count = 0;
        for (var i = 2; i < finalIndex; i++)
        {
            if (pending[i] is >= 0x30 and <= 0x3F)
                count++;
        }

        return count;
    }

    private void CopyCsiParameters(int finalIndex, Span<byte> destination)
    {
        var offset = 0;
        for (var i = 2; i < finalIndex; i++)
        {
            if (pending[i] is >= 0x30 and <= 0x3F)
                destination[offset++] = pending[i];
        }
    }

    private static bool ShouldStripCsi(byte finalByte, ReadOnlySpan<byte> parameters, EscapeContext context)
    {
        if (context == EscapeContext.Passthrough)
            return false;

        if (finalByte == (byte)'J' && IsOneOf(parameters, ""u8, "0"u8, "1"u8, "2"u8))
            return true;

        if (context == EscapeContext.AfterAlternateScreenExit && finalByte == (byte)'K')
            return true;

        if ((finalByte == (byte)'H' || finalByte == (byte)'f') && !parameters.StartsWith("?"u8))
            return true;

        if (finalByte == (byte)'m' && IsOneOf(parameters, ""u8, "0"u8))
            return true;

        return IsStripPrivateMode(finalByte, parameters);
    }

    private static bool IsStripPrivateMode(byte finalByte, ReadOnlySpan<byte> parameters)
    {
        if (finalByte is not ((byte)'h' or (byte)'l') || !parameters.StartsWith("?"u8))
            return false;

        var anyMode = false;
        foreach (var mode in SplitModes(parameters[1..]))
        {
            anyMode = true;
            if (!IsOneOf(mode, "12"u8, "25"u8, "9001"u8, "1004"u8, "2004"u8))
                return false;
        }

        return anyMode;
    }

    private static bool IsAlternateScreenMode(byte finalByte, ReadOnlySpan<byte> parameters, out bool enabled)
    {
        enabled = finalByte == (byte)'h';
        if (finalByte is not ((byte)'h' or (byte)'l') || !parameters.StartsWith("?"u8))
            return false;

        foreach (var mode in SplitModes(parameters[1..]))
        {
            if (IsOneOf(mode, "1049"u8, "47"u8, "1047"u8))
                return true;
        }

        return false;
    }

    private static bool IsOneOf(ReadOnlySpan<byte> value, ReadOnlySpan<byte> first, ReadOnlySpan<byte> second) =>
        value.SequenceEqual(first) || value.SequenceEqual(second);

    private static bool IsOneOf(ReadOnlySpan<byte> value, ReadOnlySpan<byte> first, ReadOnlySpan<byte> second, ReadOnlySpan<byte> third) =>
        value.SequenceEqual(first) || value.SequenceEqual(second) || value.SequenceEqual(third);

    private static bool IsOneOf(ReadOnlySpan<byte> value, ReadOnlySpan<byte> first, ReadOnlySpan<byte> second, ReadOnlySpan<byte> third, ReadOnlySpan<byte> fourth) =>
        value.SequenceEqual(first) || value.SequenceEqual(second) || value.SequenceEqual(third) || value.SequenceEqual(fourth);

    private static bool IsOneOf(ReadOnlySpan<byte> value, ReadOnlySpan<byte> first, ReadOnlySpan<byte> second, ReadOnlySpan<byte> third, ReadOnlySpan<byte> fourth, ReadOnlySpan<byte> fifth) =>
        value.SequenceEqual(first) || value.SequenceEqual(second) || value.SequenceEqual(third) || value.SequenceEqual(fourth) || value.SequenceEqual(fifth);

    private static ModeSplitEnumerable SplitModes(ReadOnlySpan<byte> modes) => new(modes);

    private readonly ref struct ModeSplitEnumerable
    {
        private readonly ReadOnlySpan<byte> modes;

        public ModeSplitEnumerable(ReadOnlySpan<byte> modes)
        {
            this.modes = modes;
        }

        public ModeSplitEnumerator GetEnumerator() => new(modes);
    }

    private ref struct ModeSplitEnumerator
    {
        private ReadOnlySpan<byte> remaining;

        public ModeSplitEnumerator(ReadOnlySpan<byte> modes)
        {
            remaining = modes;
            Current = ReadOnlySpan<byte>.Empty;
        }

        public ReadOnlySpan<byte> Current { get; private set; }

        public bool MoveNext()
        {
            while (true)
            {
                if (remaining.IsEmpty)
                    return false;

                var separator = remaining.IndexOf((byte)';');
                var mode = separator < 0 ? remaining : remaining[..separator];
                remaining = separator < 0 ? ReadOnlySpan<byte>.Empty : remaining[(separator + 1)..];
                mode = TrimAsciiWhitespace(mode);
                if (mode.IsEmpty)
                    continue;

                Current = mode;
                return true;
            }
        }
    }

    private static ReadOnlySpan<byte> TrimAsciiWhitespace(ReadOnlySpan<byte> value)
    {
        var start = 0;
        var end = value.Length - 1;
        while (start <= end && value[start] is (byte)' ' or (byte)'\t')
            start++;
        while (end >= start && value[end] is (byte)' ' or (byte)'\t')
            end--;

        return value[start..(end + 1)];
    }

    private void StripPending(int length)
    {
        StrippedByteCount += length;
        pending.RemoveRange(0, length);
    }

    private static bool IsPrintableUtf8Start(byte value) =>
        value != Esc && value != 0x7F && (value >= 0x20 || value >= 0x80);

    private bool TryFindCursorPositionCsiEnd(int startIndex, out int endIndex)
    {
        endIndex = 0;
        for (var i = startIndex; i < pending.Count; i++)
        {
            if (pending[i] != Esc || i + 1 >= pending.Count || pending[i + 1] != (byte)'[')
                continue;

            var finalIndex = -1;
            for (var j = i + 2; j < pending.Count; j++)
            {
                var value = pending[j];
                if (value is >= 0x40 and <= 0x7E)
                {
                    finalIndex = j;
                    break;
                }
            }

            if (finalIndex < 0)
                return false;

            if (pending[finalIndex] is (byte)'H' or (byte)'f' && !CsiParametersStartWithPrivate(i + 2, finalIndex))
            {
                endIndex = finalIndex + 1;
                return true;
            }

            i = finalIndex;
        }

        return false;
    }

    private bool CsiParametersStartWithPrivate(int start, int finalIndex)
    {
        for (var i = start; i < finalIndex; i++)
        {
            if (pending[i] == (byte)'?')
                return true;
        }

        return false;
    }

    private bool PendingEndsWithIncompleteCsi()
    {
        var escIndex = pending.LastIndexOf(Esc);
        if (escIndex < 0 || escIndex + 1 >= pending.Count || pending[escIndex + 1] != (byte)'[')
            return false;

        for (var i = escIndex + 2; i < pending.Count; i++)
        {
            if (pending[i] is >= 0x40 and <= 0x7E)
                return false;
        }

        return true;
    }

    private static void AppendAndClear(List<byte> output, List<byte> source)
    {
        AppendRange(output, source, 0, source.Count);
        source.Clear();
    }

    private static void AppendRange(List<byte> output, List<byte> source, int start, int length)
    {
        output.EnsureCapacity(output.Count + length);
        for (var i = 0; i < length; i++)
            output.Add(source[start + i]);
    }

    private enum FilterMode
    {
        LeadingInit,
        Passthrough,
        AfterAlternateScreenExit,
    }

    private enum EscapeContext
    {
        LeadingInit,
        Passthrough,
        AfterAlternateScreenExit,
    }
}
