using System.Globalization;
using System.Text;

internal static class SvgFrameRenderer
{
    private const double LineHeightFactor = 1.25;
    private const double CharWidthFactor = 0.62;
    private const double Padding = 8.0;
    private const double InnerPaddingHorizontalMin = 4.0;
    private const double InnerPaddingHorizontalMax = 16.0;
    private const double InnerPaddingVerticalMin = 2.0;
    private const double InnerPaddingVerticalMax = 8.0;
    private const double MinimumFrameInterval = 0.05;

    internal static string Render(
        IReadOnlyList<ReplayFrame> frames,
        ResolvedRenderSettings render,
        int canvasWidth,
        int canvasHeight)
    {
        if (frames.Count == 0)
            return BuildEmptySvg(render, canvasWidth, canvasHeight);

        frames = CollapseDuplicateFrames(frames);
        var theme = TerminalTheme.FromResolved(render.Theme);
        var fontSize = render.FontSize;
        var metrics = CreateMetrics(fontSize, canvasWidth, canvasHeight);
        var lastTime = Math.Max(MinimumFrameInterval, frames[^1].Time);
        var totalDuration = lastTime + MinimumFrameInterval;

        var hashToDefsIndex = new Dictionary<ulong, int>();
        var uniqueIndices = new List<int>();
        var frameToDefs = new int[frames.Count];

        for (var i = 0; i < frames.Count; i++)
        {
            var hash = TerminalReplay.BuildVisualSignature(frames[i].Buffer);
            if (!hashToDefsIndex.TryGetValue(hash, out var defsIndex))
            {
                defsIndex = uniqueIndices.Count;
                hashToDefsIndex[hash] = defsIndex;
                uniqueIndices.Add(i);
            }

            frameToDefs[i] = defsIndex;
        }

        var sb = new StringBuilder(128 * 1024);
        sb.AppendLine(CultureInfo.InvariantCulture, $"<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"<svg xmlns=\"http://www.w3.org/2000/svg\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" width=\"{metrics.SvgWidth:0.##}\" height=\"{metrics.SvgHeight:0.##}\" viewBox=\"0 0 {metrics.SvgWidth:0.##} {metrics.SvgHeight:0.##}\" preserveAspectRatio=\"xMidYMid meet\" role=\"img\" aria-label=\"scenario2cast output\">");

        sb.AppendLine("<style>");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $".term {{ font-family: {render.FontFamily}; font-size: {fontSize}px; white-space: pre; }}");
        sb.AppendLine("text { dominant-baseline: alphabetic; }");
        sb.AppendLine(".bg { shape-rendering: crispEdges; }");
        sb.AppendLine(".frame { opacity: 0; }");
        AppendAnimationCss(sb, frames, totalDuration);
        sb.AppendLine("</style>");

        sb.AppendLine("<defs>");
        foreach (var frameIndex in uniqueIndices)
            AppendFrameContent(sb, frames[frameIndex], metrics, theme, $"fd-{frameIndex}");
        sb.AppendLine("</defs>");

        var bgRect = metrics.BackgroundRect();
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"<rect x=\"{bgRect.X:0.##}\" y=\"{bgRect.Y:0.##}\" width=\"{bgRect.Width:0.##}\" height=\"{bgRect.Height:0.##}\" fill=\"{render.Theme.Bg}\"/>");

        for (var i = 0; i < frames.Count; i++)
        {
            var defsFrameIndex = uniqueIndices[frameToDefs[i]];
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"<use href=\"#fd-{defsFrameIndex}\" class=\"frame frame-{i}\"/>");
        }

        sb.AppendLine("</svg>");
        return sb.ToString();
    }

    private static string BuildEmptySvg(ResolvedRenderSettings render, int width, int height)
    {
        var metrics = CreateMetrics(render.FontSize, width, height);
        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{metrics.SvgWidth:0.##}\" height=\"{metrics.SvgHeight:0.##}\" viewBox=\"0 0 {metrics.SvgWidth:0.##} {metrics.SvgHeight:0.##}\">");
        var bgRect = metrics.BackgroundRect();
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"<rect x=\"{bgRect.X:0.##}\" y=\"{bgRect.Y:0.##}\" width=\"{bgRect.Width:0.##}\" height=\"{bgRect.Height:0.##}\" fill=\"{render.Theme.Bg}\"/>");
        sb.AppendLine("</svg>");
        return sb.ToString();
    }

    private static IReadOnlyList<ReplayFrame> CollapseDuplicateFrames(IReadOnlyList<ReplayFrame> frames)
    {
        if (frames.Count <= 1)
            return frames;

        var collapsed = new List<ReplayFrame> { frames[0] };
        for (var i = 1; i < frames.Count; i++)
        {
            if (TerminalReplay.BuildVisualSignature(frames[i].Buffer)
                == TerminalReplay.BuildVisualSignature(collapsed[^1].Buffer))
            {
                continue;
            }

            collapsed.Add(frames[i]);
        }

        if (collapsed.Count == 0 || collapsed[^1].Time != frames[^1].Time
            || TerminalReplay.BuildVisualSignature(collapsed[^1].Buffer) != TerminalReplay.BuildVisualSignature(frames[^1].Buffer))
        {
            if (collapsed.Count == 0 || !ReferenceEquals(collapsed[^1], frames[^1]))
                collapsed.Add(frames[^1]);
        }

        return collapsed;
    }

    private static void AppendAnimationCss(StringBuilder sb, IReadOnlyList<ReplayFrame> frames, double totalDuration)
    {
        for (var i = 0; i < frames.Count; i++)
        {
            var isLast = i == frames.Count - 1;
            var start = Percentage(frames[i].Time, totalDuration);
            var end = isLast
                ? Percentage(totalDuration - MinimumFrameInterval, totalDuration)
                : Math.Max(start, Percentage(frames[i + 1].Time, totalDuration));
            var fadeIn = Math.Max(0d, start - 0.001d);
            var fadeOut = Math.Min(100d, end + 0.001d);

            sb.AppendLine(CultureInfo.InvariantCulture, $"@keyframes k{i} {{");
            sb.AppendLine(CultureInfo.InvariantCulture, $"  0%, {fadeIn:0.###}% {{ opacity: 0; }}");
            if (isLast)
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"  {start:0.###}%, 100% {{ opacity: 1; }}");
            }
            else
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"  {start:0.###}%, {end:0.###}% {{ opacity: 1; }}");
                sb.AppendLine(CultureInfo.InvariantCulture, $"  {fadeOut:0.###}%, 100% {{ opacity: 0; }}");
            }

            sb.AppendLine("}");
            sb.AppendLine(CultureInfo.InvariantCulture,
                $".frame-{i} {{ animation: k{i} {totalDuration:0.######}s linear forwards; }}");
        }
    }

    private static void AppendFrameContent(
        StringBuilder sb,
        ReplayFrame frame,
        SvgMetrics metrics,
        TerminalTheme theme,
        string id)
    {
        sb.AppendLine(CultureInfo.InvariantCulture, $"<g id=\"{id}\">");
        var clipWidth = metrics.ViewportPixelWidth(frame.ViewportWidth);
        var clipHeight = metrics.ViewportPixelHeight(frame.ViewportHeight);
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"<clipPath id=\"{id}-clip\"><rect x=\"{metrics.ContentOriginX:0.##}\" y=\"{metrics.ContentOriginY:0.##}\" width=\"{clipWidth:0.##}\" height=\"{clipHeight:0.##}\"/></clipPath>");
        sb.AppendLine(CultureInfo.InvariantCulture, $"<g clip-path=\"url(#{id}-clip)\">");

        var origin = metrics.ContentOrigin;
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"<rect x=\"{origin.X:0.##}\" y=\"{origin.Y:0.##}\" width=\"{metrics.ContentWidth:0.##}\" height=\"{metrics.ContentHeight:0.##}\" fill=\"{theme.Background}\"/>");

        var buffer = frame.Buffer;
        for (var row = 0; row < buffer.Height; row++)
        {
            var y = origin.Y + row * metrics.LineHeight;
            AppendRow(sb, buffer, row, origin.X, y, metrics, theme);
        }

        if (frame.Buffer.CursorVisible)
        {
            var cursorX = origin.X + frame.Buffer.CursorCol * metrics.CharWidth;
            var cursorY = origin.Y + frame.Buffer.CursorRow * metrics.LineHeight;
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"<rect x=\"{cursorX:0.##}\" y=\"{cursorY:0.##}\" width=\"{metrics.CharWidth:0.##}\" height=\"{metrics.LineHeight:0.##}\" fill=\"{theme.Foreground}\" fill-opacity=\"0.5\"/>");
        }

        sb.AppendLine("</g>");
        sb.AppendLine("</g>");
    }

    private static void AppendRow(
        StringBuilder sb,
        ScreenBuffer buffer,
        int row,
        double originX,
        double rowY,
        SvgMetrics metrics,
        TerminalTheme theme)
    {
        var textY = rowY + metrics.BaselineOffset;
        var col = 0;
        while (col < buffer.Width)
        {
            var cell = buffer.GetCell(row, col);
            if (cell.IsWideContinuation)
            {
                col++;
                continue;
            }

            if (cell.Text == " ")
            {
                col++;
                continue;
            }

            var runStart = col;
            var runFg = ResolveForeground(cell, theme);
            var runBg = ResolveBackground(cell, theme);
            var runBold = cell.Bold;
            var runItalic = cell.Italic;
            var runUnderline = cell.Underline;
            var runText = new StringBuilder(cell.Text);
            var runWidth = cell.IsWide ? 2 : 1;
            col++;

            while (col < buffer.Width)
            {
                var next = buffer.GetCell(row, col);
                if (next.IsWideContinuation || next.Text == " ")
                    break;

                var nextFg = ResolveForeground(next, theme);
                var nextBg = ResolveBackground(next, theme);
                if (nextFg != runFg || nextBg != runBg || next.Bold != runBold
                    || next.Italic != runItalic || next.Underline != runUnderline || next.IsWide)
                {
                    break;
                }

                runText.Append(next.Text);
                runWidth += next.IsWide ? 2 : 1;
                col++;
            }

            var x = originX + runStart * metrics.CharWidth;
            if (runBg is not null)
            {
                sb.AppendLine(CultureInfo.InvariantCulture,
                    $"<rect class=\"bg\" x=\"{x:0.##}\" y=\"{rowY:0.##}\" width=\"{runWidth * metrics.CharWidth:0.##}\" height=\"{metrics.LineHeight:0.##}\" fill=\"{runBg}\"/>");
            }

            if (IsBlockElement(cell.Text))
            {
                RenderBlockElement(sb, cell.Text, x, rowY, runWidth * metrics.CharWidth, metrics.LineHeight, runFg);
                continue;
            }

            var drawWidth = runWidth * metrics.CharWidth;
            var weight = runBold ? "bold" : "normal";
            var styleAttr = runItalic || runUnderline
                ? BuildTextStyle(runItalic, runUnderline)
                : "";
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"<text class=\"term\" x=\"{x:0.##}\" y=\"{textY:0.##}\" fill=\"{runFg}\" font-weight=\"{weight}\" textLength=\"{drawWidth:0.##}\" lengthAdjust=\"spacingAndGlyphs\"{styleAttr}>{EscapeXml(runText.ToString())}</text>");
        }
    }

    private static string ResolveForeground(ScreenCell cell, TerminalTheme theme) =>
        ApplyIntensity(cell.Reversed ? cell.Background : cell.Foreground, cell.Bold, cell.Faint);

    private static string? ResolveBackground(ScreenCell cell, TerminalTheme theme)
    {
        var bg = cell.Reversed ? cell.Foreground : cell.Background;
        return string.Equals(bg, theme.Background, StringComparison.OrdinalIgnoreCase) ? null : bg;
    }

    private static string ApplyIntensity(string color, bool bold, bool faint)
    {
        var factor = 1d;
        if (bold) factor *= 1.2d;
        if (faint) factor *= 0.75d;
        if (Math.Abs(factor - 1d) < 0.0001d)
            return color;

        if (!TryParseHexColor(color, out var r, out var g, out var b))
            return color;

        return string.Create(
            CultureInfo.InvariantCulture,
            $"#{(int)Math.Clamp(Math.Round(r * factor), 0, 255):x2}{(int)Math.Clamp(Math.Round(g * factor), 0, 255):x2}{(int)Math.Clamp(Math.Round(b * factor), 0, 255):x2}");
    }

    private static bool TryParseHexColor(string color, out int r, out int g, out int b)
    {
        r = g = b = 0;
        if (color.Length != 7 || color[0] != '#')
            return false;

        if (!TryParseHexByte(color[1], color[2], out r)
            || !TryParseHexByte(color[3], color[4], out g)
            || !TryParseHexByte(color[5], color[6], out b))
        {
            return false;
        }

        return true;
    }

    private static bool TryParseHexByte(char high, char low, out int value)
    {
        value = 0;
        if (!TryParseHexNibble(high, out var hi) || !TryParseHexNibble(low, out var lo))
            return false;

        value = (hi << 4) | lo;
        return true;
    }

    private static bool TryParseHexNibble(char c, out int value)
    {
        value = c switch
        {
            >= '0' and <= '9' => c - '0',
            >= 'A' and <= 'F' => c - 'A' + 10,
            >= 'a' and <= 'f' => c - 'a' + 10,
            _ => -1
        };
        return value >= 0;
    }

    private static string BuildTextStyle(bool italic, bool underline)
    {
        var parts = new List<string>(2);
        if (italic) parts.Add("font-style:italic");
        if (underline) parts.Add("text-decoration:underline");
        return $" style=\"{string.Join(';', parts)};\"";
    }

    private static bool IsBlockElement(string text)
    {
        if (string.IsNullOrEmpty(text))
            return false;

        var cp = text.Length == 1 ? text[0] : char.ConvertToUtf32(text[0], text[1]);
        return cp is >= 0x2580 and <= 0x259F and not (0x2591 or 0x2592 or 0x2593);
    }

    private static void RenderBlockElement(
        StringBuilder sb,
        string text,
        double x,
        double y,
        double cellRectWidth,
        double cellRectHeight,
        string fill)
    {
        var cp = text.Length == 1 ? text[0] : char.ConvertToUtf32(text[0], text[1]);
        var w = cellRectWidth;
        var h = cellRectHeight;
        var hh = h / 2d;
        var hw = w / 2d;

        void R(double rx, double ry, double rw, double rh)
        {
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"<rect class=\"bg\" x=\"{rx:0.##}\" y=\"{ry:0.##}\" width=\"{rw:0.##}\" height=\"{rh:0.##}\" fill=\"{fill}\"/>");
        }

        switch (cp)
        {
            case 0x2580: R(x, y, w, hh); break;
            case 0x2584: R(x, y + hh, w, hh); break;
            case 0x2588: R(x, y, w, h); break;
            case 0x258C: R(x, y, hw, h); break;
            case 0x2590: R(x + hw, y, hw, h); break;
            default: R(x, y, w, h); break;
        }
    }

    private static SvgMetrics CreateMetrics(int fontSize, int cols, int rows)
    {
        var lineHeight = fontSize * LineHeightFactor;
        var charWidth = fontSize * CharWidthFactor;
        var (innerPaddingH, innerPaddingV) = ResolveInnerPadding(fontSize);
        var contentOriginX = Padding + innerPaddingH;
        var contentOriginY = Padding + innerPaddingV;
        var contentWidth = cols * charWidth + innerPaddingH * 2;
        var contentHeight = rows * lineHeight + innerPaddingV * 2;
        var svgWidth = contentWidth + Padding * 2;
        var svgHeight = contentHeight + Padding * 2;

        return new SvgMetrics(
            charWidth,
            lineHeight,
            fontSize * 0.8,
            innerPaddingH,
            innerPaddingV,
            contentOriginX,
            contentOriginY,
            contentWidth,
            contentHeight,
            svgWidth,
            svgHeight);
    }

    private static (double horizontal, double vertical) ResolveInnerPadding(int fontSize)
    {
        var horizontal = Math.Clamp(fontSize * 10.0 / 16.0, InnerPaddingHorizontalMin, InnerPaddingHorizontalMax);
        var vertical = Math.Clamp(fontSize * 4.0 / 16.0, InnerPaddingVerticalMin, InnerPaddingVerticalMax);
        return (horizontal, vertical);
    }

    private static double Percentage(double value, double total) =>
        total <= 0 ? 100 : Math.Max(0, Math.Min(100, value / total * 100));

    private static string EscapeXml(string text) =>
        text
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal);

    private readonly struct SvgMetrics
    {
        internal SvgMetrics(
            double charWidth,
            double lineHeight,
            double baselineOffset,
            double innerPaddingH,
            double innerPaddingV,
            double contentOriginX,
            double contentOriginY,
            double contentWidth,
            double contentHeight,
            double svgWidth,
            double svgHeight)
        {
            CharWidth = charWidth;
            LineHeight = lineHeight;
            BaselineOffset = baselineOffset;
            InnerPaddingH = innerPaddingH;
            InnerPaddingV = innerPaddingV;
            ContentOriginX = contentOriginX;
            ContentOriginY = contentOriginY;
            ContentWidth = contentWidth;
            ContentHeight = contentHeight;
            SvgWidth = svgWidth;
            SvgHeight = svgHeight;
        }

        internal double CharWidth { get; }
        internal double LineHeight { get; }
        internal double BaselineOffset { get; }
        private double InnerPaddingH { get; }
        private double InnerPaddingV { get; }
        internal double ContentOriginX { get; }
        internal double ContentOriginY { get; }
        internal double ContentWidth { get; }
        internal double ContentHeight { get; }
        internal double SvgWidth { get; }
        internal double SvgHeight { get; }
        internal (double X, double Y) ContentOrigin => (ContentOriginX, ContentOriginY);

        internal double ViewportPixelWidth(int cols) => cols * CharWidth + InnerPaddingH * 2;
        internal double ViewportPixelHeight(int rows) => rows * LineHeight + InnerPaddingV * 2;

        internal (double X, double Y, double Width, double Height) BackgroundRect() =>
            (Padding, Padding, SvgWidth - Padding * 2, SvgHeight - Padding * 2);
    }
}
