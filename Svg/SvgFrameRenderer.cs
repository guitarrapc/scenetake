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
    private const double LayerFadeSeconds = 0.001;
    private const double CursorBlockOpacity = 0.5;

    internal static string Render(
        IReadOnlyList<ReplayFrame> frames,
        ResolvedRenderSettings render,
        int canvasWidth,
        int canvasHeight)
    {
        if (frames.Count == 0)
            return BuildEmptySvg(render, canvasWidth, canvasHeight);

        frames = SvgFrameOptimizer.Optimize(frames);
        var theme = TerminalTheme.FromResolved(render.Theme);
        var metrics = CreateMetrics(render.FontSize, canvasWidth, canvasHeight);

        var rowLayers = BuildRowLayers(frames, canvasWidth, canvasHeight, theme);
        var cursorLayers = BuildCursorLayers(frames);
        var viewportLayers = BuildViewportLayers(frames);

        return BuildLayeredSvg(
            rowLayers,
            cursorLayers,
            viewportLayers,
            metrics,
            render,
            theme,
            canvasWidth,
            canvasHeight);
    }

    private static string BuildLayeredSvg(
        IReadOnlyList<RowLayer> rowLayers,
        IReadOnlyList<CursorLayer> cursorLayers,
        IReadOnlyList<ViewportLayer> viewportLayers,
        SvgMetrics metrics,
        ResolvedRenderSettings render,
        TerminalTheme theme,
        int canvasWidth,
        int canvasHeight)
    {
        var fadeText = LayerFadeSeconds.ToString("0.######", CultureInfo.InvariantCulture);
        var cursorOpacityText = CursorBlockOpacity.ToString("0.######", CultureInfo.InvariantCulture);

        if (viewportLayers.Count == 0)
            viewportLayers = [new ViewportLayer(canvasWidth, canvasHeight, 0)];

        var sb = new StringBuilder(64 * 1024);
        sb.AppendLine(CultureInfo.InvariantCulture, $"<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{metrics.SvgWidth:0.##}\" height=\"{metrics.SvgHeight:0.##}\" viewBox=\"0 0 {metrics.SvgWidth:0.##} {metrics.SvgHeight:0.##}\" preserveAspectRatio=\"xMidYMid meet\" role=\"img\" aria-label=\"scenario2cast output\">");

        sb.AppendLine("<style>");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"text {{ font-family: {render.FontFamily}; font-size: {render.FontSize}px; white-space: pre; }}");
        sb.AppendLine("text { dominant-baseline: alphabetic; }");
        sb.AppendLine(".bg { shape-rendering: crispEdges; }");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $".layer {{ opacity: 0; animation-duration: {fadeText}s; animation-timing-function: linear; animation-fill-mode: forwards; }}");
        sb.AppendLine("@keyframes layer-in { from { opacity: 0; } to { opacity: 1; } }");
        sb.AppendLine("@keyframes layer-out { from { opacity: 1; } to { opacity: 0; } }");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $".cursor-block {{ fill: {theme.Foreground}; fill-opacity: {cursorOpacityText}; }}");

        for (var i = 0; i < rowLayers.Count; i++)
            AppendLayerStyle(sb, $".layer-{i}", rowLayers[i].ShowTime, rowLayers[i].HideTime);

        for (var i = 0; i < cursorLayers.Count; i++)
            AppendLayerStyle(sb, $".cursor-layer-{i}", cursorLayers[i].ShowTime, cursorLayers[i].HideTime);

        for (var i = 0; i < viewportLayers.Count; i++)
        {
            var viewport = viewportLayers[i];
            AppendLayerStyle(sb, $".viewport-mask-{i}, .viewport-bg-{i}", viewport.ShowTime, viewport.HideTime);
        }

        sb.AppendLine("</style>");

        sb.AppendLine("<defs>");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"<mask id=\"viewport-mask\" x=\"0\" y=\"0\" width=\"{metrics.SvgWidth:0.##}\" height=\"{metrics.SvgHeight:0.##}\" maskUnits=\"userSpaceOnUse\">");
        for (var i = 0; i < viewportLayers.Count; i++)
        {
            var viewport = viewportLayers[i];
            var viewportWidth = metrics.ViewportPixelWidth(viewport.Width);
            var viewportHeight = metrics.ViewportPixelHeight(viewport.Height);
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"<rect class=\"layer viewport-mask-{i}\" x=\"{Padding:0.##}\" y=\"{Padding:0.##}\" width=\"{viewportWidth:0.##}\" height=\"{viewportHeight:0.##}\" fill=\"white\"/>");
        }

        sb.AppendLine("</mask>");
        sb.AppendLine("</defs>");

        sb.AppendLine("<g mask=\"url(#viewport-mask)\">");
        for (var i = 0; i < viewportLayers.Count; i++)
        {
            var viewport = viewportLayers[i];
            var viewportWidth = metrics.ViewportPixelWidth(viewport.Width);
            var viewportHeight = metrics.ViewportPixelHeight(viewport.Height);
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"<rect class=\"bg layer viewport-bg-{i}\" x=\"{Padding:0.##}\" y=\"{Padding:0.##}\" width=\"{viewportWidth:0.##}\" height=\"{viewportHeight:0.##}\" fill=\"{render.Theme.Bg}\"/>");
        }

        var origin = metrics.ContentOrigin;
        for (var i = 0; i < rowLayers.Count; i++)
        {
            var layer = rowLayers[i];
            sb.AppendLine(CultureInfo.InvariantCulture, $"<g class=\"layer layer-{i}\">");
            AppendRow(sb, layer.Buffer, layer.Row, origin.X, origin.Y, metrics, theme);
            sb.AppendLine("</g>");
        }

        for (var i = 0; i < cursorLayers.Count; i++)
        {
            var layer = cursorLayers[i];
            sb.AppendLine(CultureInfo.InvariantCulture, $"<g class=\"layer cursor-layer-{i}\">");
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"<rect class=\"cursor-block\" x=\"{origin.X + layer.Col * metrics.CharWidth:0.##}\" y=\"{origin.Y + layer.Row * metrics.LineHeight:0.##}\" width=\"{metrics.CharWidth:0.##}\" height=\"{metrics.LineHeight:0.##}\"/>");
            sb.AppendLine("</g>");
        }

        sb.AppendLine("</g>");
        sb.AppendLine("</svg>");
        return sb.ToString();
    }

    private static void AppendLayerStyle(
        StringBuilder sb,
        string selector,
        double showTime,
        double? hideTime)
    {
        var showDelay = showTime.ToString("0.######", CultureInfo.InvariantCulture);
        if (hideTime is double hide)
        {
            var hideDelay = hide.ToString("0.######", CultureInfo.InvariantCulture);
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"{selector} {{ animation-name: layer-in, layer-out; animation-delay: {showDelay}s, {hideDelay}s; }}");
            return;
        }

        sb.AppendLine(CultureInfo.InvariantCulture,
            $"{selector} {{ animation-name: layer-in; animation-delay: {showDelay}s; }}");
    }

    private static List<RowLayer> BuildRowLayers(
        IReadOnlyList<ReplayFrame> frames,
        int canvasWidth,
        int canvasHeight,
        TerminalTheme theme)
    {
        var layers = new List<RowLayer>();
        var activeByRow = new RowLayer?[canvasHeight];
        var previous = new ScreenBuffer(canvasWidth, canvasHeight, theme);

        foreach (var frame in frames)
        {
            var buffer = frame.Buffer;
            for (var row = 0; row < canvasHeight; row++)
            {
                if (RowEquals(previous, buffer, row))
                    continue;

                if (activeByRow[row] is { } active)
                    active.HideTime = frame.Time;

                var layer = new RowLayer(row, frame.Time, buffer);
                layers.Add(layer);
                activeByRow[row] = layer;
            }

            previous = buffer;
        }

        return layers;
    }

    private static List<CursorLayer> BuildCursorLayers(IReadOnlyList<ReplayFrame> frames)
    {
        var layers = new List<CursorLayer>();
        CursorLayer? active = null;

        foreach (var frame in frames)
        {
            var buffer = frame.Buffer;
            if (!buffer.CursorVisible)
            {
                if (active is not null)
                {
                    active.HideTime = frame.Time;
                    active = null;
                }

                continue;
            }

            if (active is not null && active.Row == buffer.CursorRow && active.Col == buffer.CursorCol)
                continue;

            if (active is not null)
                active.HideTime = frame.Time;

            active = new CursorLayer(buffer.CursorRow, buffer.CursorCol, frame.Time);
            layers.Add(active);
        }

        return layers;
    }

    private static List<ViewportLayer> BuildViewportLayers(IReadOnlyList<ReplayFrame> frames)
    {
        var layers = new List<ViewportLayer>();
        ViewportLayer? active = null;

        foreach (var frame in frames)
        {
            if (active is not null
                && active.Width == frame.ViewportWidth
                && active.Height == frame.ViewportHeight)
            {
                continue;
            }

            if (active is not null)
                active.HideTime = frame.Time;

            active = new ViewportLayer(frame.ViewportWidth, frame.ViewportHeight, frame.Time);
            layers.Add(active);
        }

        return layers;
    }

    private static bool RowEquals(ScreenBuffer left, ScreenBuffer right, int row)
    {
        if (left.Width != right.Width)
            return false;

        for (var col = 0; col < left.Width; col++)
        {
            var a = left.GetCell(row, col);
            var b = right.GetCell(row, col);
            if (a.Text != b.Text
                || a.Foreground != b.Foreground
                || a.Background != b.Background
                || a.Bold != b.Bold
                || a.Italic != b.Italic
                || a.Underline != b.Underline
                || a.Reversed != b.Reversed
                || a.Faint != b.Faint
                || a.IsWide != b.IsWide
                || a.IsWideContinuation != b.IsWideContinuation)
            {
                return false;
            }
        }

        return true;
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

    private static void AppendRow(
        StringBuilder sb,
        ScreenBuffer buffer,
        int row,
        double originX,
        double originY,
        SvgMetrics metrics,
        TerminalTheme theme)
    {
        var rowY = originY + row * metrics.LineHeight;
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
                if (next.IsWideContinuation)
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

            var visible = runText.ToString().TrimEnd(' ');
            if (visible.Length == 0 && runBg is null)
                continue;

            var drawText = visible.Length > 0 ? visible : runText.ToString();
            var drawWidth = (visible.Length > 0 ? visible.Length : runWidth) * metrics.CharWidth;
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

            var weight = runBold ? "bold" : "normal";
            var styleAttr = runItalic || runUnderline
                ? BuildTextStyle(runItalic, runUnderline)
                : "";
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"<text x=\"{x:0.##}\" y=\"{textY:0.##}\" fill=\"{runFg}\" font-weight=\"{weight}\" textLength=\"{drawWidth:0.##}\" lengthAdjust=\"spacing\"{styleAttr}>{EscapeXml(drawText)}</text>");
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

    private sealed class RowLayer
    {
        internal RowLayer(int row, double showTime, ScreenBuffer buffer)
        {
            Row = row;
            ShowTime = showTime;
            Buffer = buffer;
        }

        internal int Row { get; }
        internal double ShowTime { get; }
        internal double? HideTime { get; set; }
        internal ScreenBuffer Buffer { get; }
    }

    private sealed class CursorLayer
    {
        internal CursorLayer(int row, int col, double showTime)
        {
            Row = row;
            Col = col;
            ShowTime = showTime;
        }

        internal int Row { get; }
        internal int Col { get; }
        internal double ShowTime { get; }
        internal double? HideTime { get; set; }
    }

    private sealed class ViewportLayer
    {
        internal ViewportLayer(int width, int height, double showTime)
        {
            Width = width;
            Height = height;
            ShowTime = showTime;
        }

        internal int Width { get; }
        internal int Height { get; }
        internal double ShowTime { get; }
        internal double? HideTime { get; set; }
    }
}
