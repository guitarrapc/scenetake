internal sealed class ReplayFrame
{
    internal ReplayFrame(double time, ScreenBuffer buffer, int viewportWidth, int viewportHeight)
    {
        Time = time;
        Buffer = buffer;
        ViewportWidth = viewportWidth;
        ViewportHeight = viewportHeight;
    }

    internal double Time { get; }
    internal ScreenBuffer Buffer { get; }
    internal int ViewportWidth { get; }
    internal int ViewportHeight { get; }
}

internal static class TerminalReplay
{
    internal static List<ReplayFrame> BuildFrames(
        IReadOnlyList<CastEvent> events,
        int initialWidth,
        int initialHeight,
        int canvasWidth,
        int canvasHeight,
        TerminalTheme theme)
    {
        var buffer = new ScreenBuffer(initialWidth, initialHeight, theme);
        var parser = new AnsiParser(buffer, theme);
        var frames = new List<ReplayFrame>();
        ulong? lastSignature = null;

        void CaptureIfChanged(double time)
        {
            var padded = PadToCanvas(buffer, canvasWidth, canvasHeight, theme);
            var signature = BuildVisualSignature(padded);
            if (lastSignature is ulong previous && previous == signature)
                return;

            frames.Add(new ReplayFrame(time, padded, buffer.Width, buffer.Height));
            lastSignature = signature;
        }

        if (!IsBlankBuffer(buffer))
            CaptureIfChanged(0);

        foreach (var ev in events)
        {
            if (ev.Kind == CastEventKind.Marker)
                continue;

            if (ev.Kind == CastEventKind.Resize)
            {
                buffer.Resize(ev.ResizeWidth, ev.ResizeHeight);
                CaptureIfChanged(ev.Time);
                continue;
            }

            parser.Process(ev.Data);
            CaptureIfChanged(ev.Time);
        }

        if (frames.Count == 0)
            frames.Add(new ReplayFrame(0, PadToCanvas(buffer, canvasWidth, canvasHeight, theme), buffer.Width, buffer.Height));

        return TrimTrailingBlankRestore(frames, events);
    }

    internal static (int width, int height) ResolveCanvasSize(
        int initialWidth,
        int initialHeight,
        IReadOnlyList<CastEvent> events)
    {
        var maxWidth = initialWidth;
        var maxHeight = initialHeight;
        foreach (var ev in events)
        {
            if (ev.Kind != CastEventKind.Resize)
                continue;

            maxWidth = Math.Max(maxWidth, ev.ResizeWidth);
            maxHeight = Math.Max(maxHeight, ev.ResizeHeight);
        }

        return (maxWidth, maxHeight);
    }

    private static ScreenBuffer PadToCanvas(ScreenBuffer source, int canvasWidth, int canvasHeight, TerminalTheme theme) =>
        CopyToCanvas(source, canvasWidth, canvasHeight, theme);

    private static ScreenBuffer CopyToCanvas(ScreenBuffer source, int canvasWidth, int canvasHeight, TerminalTheme theme)
    {
        var copy = source.Clone();
        if (copy.Width == canvasWidth && copy.Height == canvasHeight)
            return copy;

        copy.Resize(canvasWidth, canvasHeight);
        return copy;
    }

    private static List<ReplayFrame> TrimTrailingBlankRestore(
        IReadOnlyList<ReplayFrame> frames,
        IReadOnlyList<CastEvent> events)
    {
        if (frames.Count <= 1)
            return frames.ToList();

        var lastNonBlank = -1;
        for (var i = frames.Count - 1; i >= 0; i--)
        {
            if (!IsBlankFrame(frames[i].Buffer))
            {
                lastNonBlank = i;
                break;
            }
        }

        if (lastNonBlank < 0 || lastNonBlank == frames.Count - 1)
            return frames.ToList();

        if (!HasTrailingBlankIndicators(events, lastNonBlank + 1))
            return frames.ToList();

        return frames.Take(lastNonBlank + 1).ToList();
    }

    private static bool IsBlankBuffer(ScreenBuffer buffer)
    {
        for (var row = 0; row < buffer.Height; row++)
        {
            for (var col = 0; col < buffer.Width; col++)
            {
                var cell = buffer.GetCell(row, col);
                if (cell.Text != " " || cell.IsWide || cell.IsWideContinuation)
                    return false;

                if (!string.Equals(cell.Foreground, buffer.DefaultStyle.Foreground, StringComparison.Ordinal)
                    || !string.Equals(cell.Background, buffer.DefaultStyle.Background, StringComparison.Ordinal)
                    || cell.Bold || cell.Italic || cell.Underline || cell.Reversed || cell.Faint)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool IsBlankFrame(ScreenBuffer buffer) =>
        IsBlankBuffer(buffer);

    private static bool HasTrailingBlankIndicators(IReadOnlyList<CastEvent> events, int startIndex)
    {
        for (var i = startIndex; i < events.Count; i++)
        {
            if (events[i].Kind != CastEventKind.Output)
                continue;

            var data = events[i].Data;
            if (data.Contains("\u001b[?1049l", StringComparison.Ordinal)
                || data.Contains("\u001b[?47l", StringComparison.Ordinal)
                || data.Contains("\u001b[?1047l", StringComparison.Ordinal)
                || data.Contains("\u001b[2J", StringComparison.Ordinal)
                || data.Contains("\u001b[J", StringComparison.Ordinal)
                || data.Contains("\u001b[H", StringComparison.Ordinal)
                || data.Contains("\u001b[;H", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    internal static ulong BuildVisualSignature(ScreenBuffer buffer)
    {
        const ulong fnvOffset = 1469598103934665603UL;
        const ulong fnvPrime = 1099511628211UL;

        var signature = fnvOffset;
        signature = HashInt(signature, buffer.CursorRow, fnvPrime);
        signature = HashInt(signature, buffer.CursorCol, fnvPrime);

        for (var row = 0; row < buffer.Height; row++)
        {
            for (var col = 0; col < buffer.Width; col++)
            {
                var cell = buffer.GetCell(row, col);
                signature = HashString(signature, cell.Text, fnvPrime);
                signature = HashString(signature, cell.Foreground, fnvPrime);
                signature = HashString(signature, cell.Background, fnvPrime);
                signature = HashBool(signature, cell.Bold, fnvPrime);
                signature = HashBool(signature, cell.Italic, fnvPrime);
                signature = HashBool(signature, cell.Underline, fnvPrime);
                signature = HashBool(signature, cell.Reversed, fnvPrime);
                signature = HashBool(signature, cell.Faint, fnvPrime);
                signature = HashBool(signature, cell.IsWide, fnvPrime);
                signature = HashBool(signature, cell.IsWideContinuation, fnvPrime);
            }
        }

        return signature;
    }

    private static ulong HashString(ulong signature, string value, ulong fnvPrime)
    {
        foreach (var ch in value)
        {
            signature ^= ch;
            signature *= fnvPrime;
        }

        signature ^= 0xFF;
        signature *= fnvPrime;
        return signature;
    }

    private static ulong HashBool(ulong signature, bool value, ulong fnvPrime)
    {
        signature ^= value ? (byte)1 : (byte)0;
        signature *= fnvPrime;
        return signature;
    }

    private static ulong HashInt(ulong signature, int value, ulong fnvPrime)
    {
        unchecked
        {
            signature ^= (byte)value;
            signature *= fnvPrime;
            signature ^= (byte)(value >> 8);
            signature *= fnvPrime;
            signature ^= (byte)(value >> 16);
            signature *= fnvPrime;
            signature ^= (byte)(value >> 24);
            signature *= fnvPrime;
        }

        return signature;
    }
}
