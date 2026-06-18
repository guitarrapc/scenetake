using System.Globalization;
using System.Text;

internal static class SvgRender
{
    private const double LineHeightFactor = 1.25;
    private const double CharWidthFactor = 0.62;
    private const double Padding = 8.0;
    private const double LayerFadeSeconds = 0.001;
    private const double CursorBlockOpacity = 0.5;

    internal static void WriteSvg(
        IReadOnlyList<CastEvent> events,
        int width,
        int height,
        ResolvedRenderSettings render,
        string outputPath)
    {
        var (canvasWidth, canvasHeight) = ResolveCanvasSize(width, height, events);
        var frames = BuildFrames(events, width, height, canvasWidth, canvasHeight, render);
        if (frames.Count == 0)
            frames.Add(new TerminalFrame(0, CreateEmptyScreen(canvasWidth, canvasHeight, render), 0, 0, true, width, height));

        var rowLayers = BuildRowLayers(frames, canvasWidth, canvasHeight, render);
        var cursorLayers = BuildCursorLayers(frames);
        var viewportLayers = BuildViewportLayers(frames);
        var svg = BuildSvgDocument(rowLayers, cursorLayers, viewportLayers, canvasWidth, canvasHeight, render);
        File.WriteAllText(outputPath, svg, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static (int width, int height) ResolveCanvasSize(
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

    private static List<TerminalFrame> BuildFrames(
        IReadOnlyList<CastEvent> events,
        int terminalWidth,
        int terminalHeight,
        int canvasWidth,
        int canvasHeight,
        ResolvedRenderSettings render)
    {
        var terminal = new TerminalEmulator(terminalWidth, terminalHeight, render);
        var frames = new List<TerminalFrame>();
        TerminalScreen? lastScreen = null;
        var lastCursorX = -1;
        var lastCursorY = -1;
        var lastCursorVisible = false;
        var hasCursorState = false;

        foreach (var ev in events)
        {
            if (ev.Kind == CastEventKind.Marker)
                continue;

            var forceFrame = false;
            if (ev.Kind == CastEventKind.Resize)
            {
                terminal.Resize(ev.ResizeWidth, ev.ResizeHeight);
                forceFrame = true;
            }
            else
            {
                terminal.Write(ev.Data);
            }

            var screen = terminal.CaptureScreen(canvasWidth, canvasHeight);
            var cursorX = Math.Clamp(terminal.CursorX, 0, canvasWidth - 1);
            var cursorY = Math.Clamp(terminal.CursorY, 0, canvasHeight - 1);
            var cursorVisible = terminal.CursorVisible;
            if (forceFrame ||
                lastScreen is null ||
                !screen.ContentEquals(lastScreen) ||
                !hasCursorState ||
                cursorX != lastCursorX ||
                cursorY != lastCursorY ||
                cursorVisible != lastCursorVisible)
            {
                frames.Add(new TerminalFrame(
                    ev.Time,
                    screen,
                    cursorX,
                    cursorY,
                    cursorVisible,
                    terminal.Width,
                    terminal.Height));
                lastScreen = screen;
                lastCursorX = cursorX;
                lastCursorY = cursorY;
                lastCursorVisible = cursorVisible;
                hasCursorState = true;
            }
        }

        return frames;
    }

    private static List<CursorLayer> BuildCursorLayers(IReadOnlyList<TerminalFrame> frames)
    {
        var layers = new List<CursorLayer>();
        CursorLayer? active = null;

        foreach (var frame in frames)
        {
            if (!frame.CursorVisible)
            {
                if (active is not null)
                {
                    active.HideTime = frame.Time;
                    active = null;
                }

                continue;
            }

            if (active is not null &&
                active.Row == frame.CursorY &&
                active.Col == frame.CursorX)
            {
                continue;
            }

            if (active is not null)
                active.HideTime = frame.Time;

            active = new CursorLayer(frame.CursorY, frame.CursorX, frame.Time);
            layers.Add(active);
        }

        return layers;
    }

    private static List<ViewportLayer> BuildViewportLayers(IReadOnlyList<TerminalFrame> frames)
    {
        var layers = new List<ViewportLayer>();
        ViewportLayer? active = null;

        foreach (var frame in frames)
        {
            if (active is not null &&
                active.Width == frame.ViewportWidth &&
                active.Height == frame.ViewportHeight)
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

    private static List<RowLayer> BuildRowLayers(
        IReadOnlyList<TerminalFrame> frames,
        int width,
        int height,
        ResolvedRenderSettings render)
    {
        var layers = new List<RowLayer>();
        var activeByRow = new RowLayer?[height];
        var previous = CreateEmptyScreen(width, height, render);

        foreach (var frame in frames)
        {
            for (var row = 0; row < height; row++)
            {
                if (RowEquals(previous, frame.Screen, row))
                    continue;

                if (activeByRow[row] is { } active)
                    active.HideTime = frame.Time;

                var layer = new RowLayer(row, frame.Time, frame.Screen);
                layers.Add(layer);
                activeByRow[row] = layer;
            }

            previous = frame.Screen;
        }

        return layers;
    }

    private static bool RowEquals(TerminalScreen left, TerminalScreen right, int row)
    {
        for (var col = 0; col < left.Width; col++)
        {
            if (!left[row, col].Equals(right[row, col]))
                return false;
        }

        return true;
    }

    private static TerminalScreen CreateEmptyScreen(int width, int height, ResolvedRenderSettings render)
    {
        var terminal = new TerminalEmulator(width, height, render);
        return terminal.CaptureScreen(width, height);
    }

    private static string BuildSvgDocument(
        IReadOnlyList<RowLayer> layers,
        IReadOnlyList<CursorLayer> cursorLayers,
        IReadOnlyList<ViewportLayer> viewportLayers,
        int width,
        int height,
        ResolvedRenderSettings render)
    {
        var fontSize = render.FontSize;
        var lineHeight = fontSize * LineHeightFactor;
        var charWidth = fontSize * CharWidthFactor;
        var svgWidth = width * charWidth + Padding * 2;
        var svgHeight = height * lineHeight + Padding * 2;
        var fadeText = LayerFadeSeconds.ToString("0.######", CultureInfo.InvariantCulture);
        var cursorOpacityText = CursorBlockOpacity.ToString("0.######", CultureInfo.InvariantCulture);

        if (viewportLayers.Count == 0)
            viewportLayers = [new ViewportLayer(width, height, 0)];

        var sb = new StringBuilder(64 * 1024);
        sb.AppendLine(CultureInfo.InvariantCulture, $"<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{svgWidth:0.##}\" height=\"{svgHeight:0.##}\" viewBox=\"0 0 {svgWidth:0.##} {svgHeight:0.##}\" preserveAspectRatio=\"xMidYMid meet\">");
        sb.AppendLine("<style>");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"text {{ font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, \"Liberation Mono\", \"Courier New\", monospace; font-size: {fontSize}px; white-space: pre; }}");
        sb.AppendLine(CultureInfo.InvariantCulture, $".bg {{ fill: {render.Theme.Bg}; }}");
        sb.AppendLine(CultureInfo.InvariantCulture, $".layer {{ opacity: 0; animation-duration: {fadeText}s; animation-timing-function: linear; animation-fill-mode: forwards; }}");
        sb.AppendLine("@keyframes layer-in { from { opacity: 0; } to { opacity: 1; } }");
        sb.AppendLine("@keyframes layer-out { from { opacity: 1; } to { opacity: 0; } }");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $".cursor-block {{ fill: {render.Theme.Fg}; fill-opacity: {cursorOpacityText}; }}");

        for (var i = 0; i < layers.Count; i++)
            AppendLayerStyle(sb, i, layers[i], fadeText);

        for (var i = 0; i < cursorLayers.Count; i++)
            AppendCursorLayerStyle(sb, i, cursorLayers[i], fadeText);

        for (var i = 0; i < viewportLayers.Count; i++)
            AppendViewportLayerStyle(sb, i, viewportLayers[i], fadeText);

        sb.AppendLine("</style>");

        sb.AppendLine("<defs>");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"<mask id=\"viewport-mask\" x=\"0\" y=\"0\" width=\"{svgWidth:0.##}\" height=\"{svgHeight:0.##}\" maskUnits=\"userSpaceOnUse\">");
        for (var i = 0; i < viewportLayers.Count; i++)
        {
            var viewport = viewportLayers[i];
            var viewportWidth = ViewportPixelWidth(viewport.Width, charWidth);
            var viewportHeight = ViewportPixelHeight(viewport.Height, lineHeight);
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"<rect class=\"layer viewport-mask-{i}\" x=\"{Padding:0.##}\" y=\"{Padding:0.##}\" width=\"{viewportWidth:0.##}\" height=\"{viewportHeight:0.##}\" fill=\"white\"/>");
        }

        sb.AppendLine("</mask>");
        sb.AppendLine("</defs>");

        sb.AppendLine("<g mask=\"url(#viewport-mask)\">");
        for (var i = 0; i < viewportLayers.Count; i++)
        {
            var viewport = viewportLayers[i];
            var viewportWidth = ViewportPixelWidth(viewport.Width, charWidth);
            var viewportHeight = ViewportPixelHeight(viewport.Height, lineHeight);
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"<rect class=\"bg layer viewport-bg-{i}\" x=\"{Padding:0.##}\" y=\"{Padding:0.##}\" width=\"{viewportWidth:0.##}\" height=\"{viewportHeight:0.##}\"/>");
        }

        for (var i = 0; i < layers.Count; i++)
        {
            var layer = layers[i];
            sb.AppendLine(CultureInfo.InvariantCulture, $"<g class=\"layer layer-{i}\">");
            AppendRow(sb, layer.Screen, layer.Row, width, charWidth, lineHeight);
            sb.AppendLine("</g>");
        }

        for (var i = 0; i < cursorLayers.Count; i++)
        {
            var layer = cursorLayers[i];
            sb.AppendLine(CultureInfo.InvariantCulture, $"<g class=\"layer cursor-layer-{i}\">");
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"<rect class=\"cursor-block\" x=\"{Padding + layer.Col * charWidth:0.##}\" y=\"{Padding + layer.Row * lineHeight:0.##}\" width=\"{charWidth:0.##}\" height=\"{lineHeight:0.##}\"/>");
            sb.AppendLine("</g>");
        }

        sb.AppendLine("</g>");
        sb.AppendLine("</svg>");
        return sb.ToString();
    }

    private static double ViewportPixelWidth(int cols, double charWidth) => cols * charWidth;

    private static double ViewportPixelHeight(int rows, double lineHeight) => rows * lineHeight;

    private static void AppendViewportLayerStyle(
        StringBuilder sb,
        int index,
        ViewportLayer layer,
        string fadeText)
    {
        var showDelay = layer.ShowTime.ToString("0.######", CultureInfo.InvariantCulture);
        var selector = $".viewport-mask-{index}, .viewport-bg-{index}";

        if (layer.HideTime is double hideTime)
        {
            var hideDelay = hideTime.ToString("0.######", CultureInfo.InvariantCulture);
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"{selector} {{ animation-name: layer-in, layer-out; animation-delay: {showDelay}s, {hideDelay}s; }}");
            return;
        }

        sb.AppendLine(CultureInfo.InvariantCulture,
            $"{selector} {{ animation-name: layer-in; animation-delay: {showDelay}s; }}");
    }

    private static void AppendCursorLayerStyle(
        StringBuilder sb,
        int index,
        CursorLayer layer,
        string fadeText)
    {
        var showDelay = layer.ShowTime.ToString("0.######", CultureInfo.InvariantCulture);

        if (layer.HideTime is double hideTime)
        {
            var hideDelay = hideTime.ToString("0.######", CultureInfo.InvariantCulture);
            sb.AppendLine(CultureInfo.InvariantCulture,
                $".cursor-layer-{index} {{ animation-name: layer-in, layer-out; animation-delay: {showDelay}s, {hideDelay}s; }}");
            return;
        }

        sb.AppendLine(CultureInfo.InvariantCulture,
            $".cursor-layer-{index} {{ animation-name: layer-in; animation-delay: {showDelay}s; }}");
    }

    private static void AppendLayerStyle(
        StringBuilder sb,
        int index,
        RowLayer layer,
        string fadeText)
    {
        var showDelay = layer.ShowTime.ToString("0.######", CultureInfo.InvariantCulture);

        if (layer.HideTime is double hideTime)
        {
            var hideDelay = hideTime.ToString("0.######", CultureInfo.InvariantCulture);
            sb.AppendLine(CultureInfo.InvariantCulture,
                $".layer-{index} {{ animation-name: layer-in, layer-out; animation-delay: {showDelay}s, {hideDelay}s; }}");
            return;
        }

        sb.AppendLine(CultureInfo.InvariantCulture,
            $".layer-{index} {{ animation-name: layer-in; animation-delay: {showDelay}s; }}");
    }

    private static void AppendRow(
        StringBuilder sb,
        TerminalScreen screen,
        int row,
        int width,
        double charWidth,
        double lineHeight)
    {
        var y = Padding + (row + 1) * lineHeight - lineHeight * 0.2;
        var col = 0;

        while (col < width)
        {
            var cell = screen[row, col];
            if (cell.Character == ' ' && cell.Background is null)
            {
                col++;
                continue;
            }

            var runStart = col;
            var runFg = cell.Foreground;
            var runBg = cell.Background;
            var runBold = cell.Bold;
            var runUnderline = cell.Underline;
            var text = new StringBuilder();
            text.Append(cell.Character);

            col++;
            while (col < width)
            {
                var next = screen[row, col];
                if (next.Foreground != runFg || next.Background != runBg ||
                    next.Bold != runBold || next.Underline != runUnderline)
                {
                    break;
                }

                text.Append(next.Character);
                col++;
            }

            var runLen = col - runStart;
            var runText = text.ToString();
            var visible = runText.TrimEnd(' ');
            var visibleLen = visible.Length;

            if (visibleLen == 0 && runBg is null)
                continue;

            var drawLen = visibleLen > 0 ? visibleLen : runLen;
            var drawWidth = drawLen * charWidth;
            var x = Padding + runStart * charWidth;
            if (runBg is not null)
            {
                sb.AppendLine(CultureInfo.InvariantCulture,
                    $"<rect x=\"{x:0.##}\" y=\"{Padding + row * lineHeight:0.##}\" width=\"{runLen * charWidth:0.##}\" height=\"{lineHeight:0.##}\" fill=\"{runBg}\"/>");
            }

            if (visibleLen == 0)
                continue;

            var weight = runBold ? "bold" : "normal";
            var textLength = string.Create(CultureInfo.InvariantCulture, $" textLength=\"{drawWidth:0.##}\" lengthAdjust=\"spacingAndGlyphs\"");
            if (runUnderline)
            {
                sb.AppendLine(CultureInfo.InvariantCulture,
                    $"<text x=\"{x:0.##}\" y=\"{y:0.##}\" fill=\"{runFg}\" font-weight=\"{weight}\" text-decoration=\"underline\"{textLength}>{EscapeXml(visible)}</text>");
            }
            else
            {
                sb.AppendLine(CultureInfo.InvariantCulture,
                    $"<text x=\"{x:0.##}\" y=\"{y:0.##}\" fill=\"{runFg}\" font-weight=\"{weight}\"{textLength}>{EscapeXml(visible)}</text>");
            }
        }
    }

    private static string EscapeXml(string text)
    {
        return text
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal);
    }
}

internal readonly record struct ResolvedRenderSettings(int FontSize, ResolvedTheme Theme)
{
    public string DefaultFg => Theme.Fg;
}

internal readonly record struct ResolvedTheme(string Fg, string Bg, string Palette);

internal readonly record struct TerminalFrame(
    double Time,
    TerminalScreen Screen,
    int CursorX,
    int CursorY,
    bool CursorVisible,
    int ViewportWidth,
    int ViewportHeight);

internal sealed class ViewportLayer
{
    public ViewportLayer(int width, int height, double showTime)
    {
        Width = width;
        Height = height;
        ShowTime = showTime;
    }

    public int Width { get; }
    public int Height { get; }
    public double ShowTime { get; }
    public double? HideTime { get; set; }
}

internal sealed class CursorLayer
{
    public CursorLayer(int row, int col, double showTime)
    {
        Row = row;
        Col = col;
        ShowTime = showTime;
    }

    public int Row { get; }
    public int Col { get; }
    public double ShowTime { get; }
    public double? HideTime { get; set; }
}

internal sealed class RowLayer
{
    public RowLayer(int row, double showTime, TerminalScreen screen)
    {
        Row = row;
        ShowTime = showTime;
        Screen = screen;
    }

    public int Row { get; }
    public double ShowTime { get; }
    public double? HideTime { get; set; }
    public TerminalScreen Screen { get; }
}

internal sealed class TerminalScreen
{
    private readonly TerminalCell[,] _cells;

    public TerminalScreen(int width, int height)
    {
        Width = width;
        Height = height;
        _cells = new TerminalCell[height, width];
    }

    public int Width { get; }
    public int Height { get; }

    public ref TerminalCell this[int row, int col] => ref _cells[row, col];

    public bool ContentEquals(TerminalScreen other)
    {
        if (Width != other.Width || Height != other.Height)
            return false;

        for (var row = 0; row < Height; row++)
        {
            for (var col = 0; col < Width; col++)
            {
                if (!_cells[row, col].Equals(other._cells[row, col]))
                    return false;
            }
        }

        return true;
    }
}

internal readonly struct TerminalCell : IEquatable<TerminalCell>
{
    public TerminalCell(char character, string foreground, string? background, bool bold, bool underline)
    {
        Character = character;
        Foreground = foreground;
        Background = background;
        Bold = bold;
        Underline = underline;
    }

    public char Character { get; }
    public string Foreground { get; }
    public string? Background { get; }
    public bool Bold { get; }
    public bool Underline { get; }

    public bool Equals(TerminalCell other) =>
        Character == other.Character &&
        Foreground == other.Foreground &&
        Background == other.Background &&
        Bold == other.Bold &&
        Underline == other.Underline;

    public override bool Equals(object? obj) => obj is TerminalCell other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Character, Foreground, Background, Bold, Underline);
}

internal sealed class TerminalEmulator
{
    private int _width;
    private int _height;
    private readonly ResolvedRenderSettings _render;
    private readonly string[] _palette;
    private TerminalCell[,] _cells;
    private int _cursorX;
    private int _cursorY;
    private bool _cursorVisible = true;
    private AnsiStyle _style;
    private AnsiStyle _defaultStyle;
    private readonly HashSet<string> _warnedKeys = new(StringComparer.Ordinal);

    public TerminalEmulator(int width, int height, ResolvedRenderSettings render)
    {
        _width = width;
        _height = height;
        _render = render;
        _palette = ParsePalette(render.Theme.Palette);
        _cells = new TerminalCell[height, width];
        _defaultStyle = AnsiStyle.Default(render.Theme.Fg);
        _style = _defaultStyle;
        ClearAll();
    }

    public int Width => _width;
    public int Height => _height;
    public int CursorX => _cursorX;
    public int CursorY => _cursorY;
    public bool CursorVisible => _cursorVisible;

    public void Resize(int newWidth, int newHeight)
    {
        var resized = new TerminalCell[newHeight, newWidth];
        for (var row = 0; row < newHeight; row++)
        {
            for (var col = 0; col < newWidth; col++)
            {
                resized[row, col] = row < _height && col < _width
                    ? _cells[row, col]
                    : EmptyCell();
            }
        }

        _cells = resized;
        _width = newWidth;
        _height = newHeight;
        _cursorX = Math.Clamp(_cursorX, 0, Math.Max(0, _width - 1));
        _cursorY = Math.Clamp(_cursorY, 0, Math.Max(0, _height - 1));
    }

    public void Write(string data)
    {
        for (var i = 0; i < data.Length; i++)
        {
            var ch = data[i];
            if (ch == '\u001b' || ch == '\x1b')
            {
                if (i + 1 < data.Length && data[i + 1] == '[')
                {
                    i = ParseCsi(data, i + 2);
                    continue;
                }
            }

            switch (ch)
            {
                case '\r':
                    _cursorX = 0;
                    break;
                case '\n':
                    _cursorX = 0;
                    _cursorY++;
                    if (_cursorY >= _height)
                        ScrollUp();
                    break;
                case '\b':
                    if (_cursorX > 0)
                        _cursorX--;
                    break;
                case '\t':
                    _cursorX = Math.Min(_width - 1, (_cursorX + 8) & ~7);
                    break;
                case '\a':
                    break;
                default:
                    if (ch >= 32 || ch == ' ')
                        PutChar(ch);
                    break;
            }
        }
    }

    public TerminalScreen CaptureScreen(int canvasWidth, int canvasHeight)
    {
        var screen = new TerminalScreen(canvasWidth, canvasHeight);
        for (var row = 0; row < canvasHeight; row++)
        {
            for (var col = 0; col < canvasWidth; col++)
            {
                screen[row, col] = row < _height && col < _width
                    ? _cells[row, col]
                    : EmptyCell();
            }
        }

        return screen;
    }

    private int ParseCsi(string data, int index)
    {
        var start = index;
        while (index < data.Length && !IsCsiFinalByte(data[index]))
            index++;

        if (index >= data.Length)
            return data.Length - 1;

        var end = index;
        var command = data[end];
        var body = data[start..end];

        if (body.StartsWith('?'))
        {
            ApplyPrivateMode(body[1..], command);
            return end;
        }

        var numbers = ParseCsiNumericParameters(body, command == 'm');

        switch (command)
        {
            case 'm':
                ApplySgr(numbers);
                break;
            case 'J':
                if (numbers.Count == 0 || numbers[0] == 2)
                    ClearAll();
                break;
            case 'H':
                _cursorX = 0;
                _cursorY = 0;
                if (numbers.Count >= 2)
                {
                    _cursorY = Math.Max(0, numbers[0] - 1);
                    _cursorX = Math.Max(0, numbers[1] - 1);
                }
                else if (numbers.Count == 1)
                {
                    _cursorY = Math.Max(0, numbers[0] - 1);
                    _cursorX = 0;
                }
                _cursorY = Math.Min(_cursorY, _height - 1);
                _cursorX = Math.Min(_cursorX, _width - 1);
                break;
            case 'K':
                ClearLineFromCursor(numbers.Count == 0 ? 0 : numbers[0]);
                break;
        }

        return end;
    }

    private static bool IsCsiFinalByte(char c) => c is >= (char)0x40 and <= (char)0x7e;

    private void ApplyPrivateMode(string parameters, char command)
    {
        if (command is not ('h' or 'l'))
            return;

        foreach (var part in parameters.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (part == "25")
                _cursorVisible = command == 'h';
        }
    }

    private static List<int> ParseCsiNumericParameters(string body, bool normalizeColons)
    {
        if (normalizeColons)
            body = body.Replace(':', ';');

        var parts = body.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var numbers = new List<int>(parts.Length);
        foreach (var part in parts)
        {
            if (int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n))
                numbers.Add(n);
        }

        return numbers;
    }

    private void ApplySgr(List<int> codes)
    {
        if (codes.Count == 0)
            codes.Add(0);

        for (var i = 0; i < codes.Count; i++)
        {
            var code = codes[i];
            switch (code)
            {
                case 0:
                    _style = _defaultStyle;
                    break;
                case 1:
                    _style = _style with { Bold = true };
                    break;
                case 4:
                    _style = _style with { Underline = true };
                    break;
                case 22:
                    _style = _style with { Bold = false };
                    break;
                case 24:
                    _style = _style with { Underline = false };
                    break;
                case >= 30 and <= 37:
                    _style = _style with { Foreground = PaletteColor(code - 30, false), ForegroundIsBright = false };
                    break;
                case >= 90 and <= 97:
                    _style = _style with { Foreground = PaletteColor(code - 90, true), ForegroundIsBright = true };
                    break;
                case 39:
                    _style = _style with { Foreground = _defaultStyle.Foreground };
                    break;
                case >= 40 and <= 47:
                    _style = _style with { Background = PaletteColor(code - 40, false) };
                    break;
                case >= 100 and <= 107:
                    _style = _style with { Background = PaletteColor(code - 100, true) };
                    break;
                case 49:
                    _style = _style with { Background = null };
                    break;
                case 38:
                case 48:
                    if (!TryApplyExtendedColor(codes, ref i, code == 48))
                    {
                        if (code == 38)
                            _style = _style with { Foreground = _defaultStyle.Foreground };
                        else
                            _style = _style with { Background = null };
                    }
                    break;
                default:
                    break;
            }
        }
    }

    private bool TryApplyExtendedColor(List<int> codes, ref int index, bool isBackground)
    {
        if (index + 1 >= codes.Count)
        {
            WarnOnce(
                "invalid-extended-color",
                "Warning: svg: extended color SGR is missing a mode; using default colors");
            return false;
        }

        var mode = codes[index + 1];
        if (mode == 5)
        {
            if (index + 2 >= codes.Count)
            {
                index += 1;
                WarnOnce(
                    "invalid-256-index",
                    "Warning: svg: 256-color SGR is missing an index; using default colors");
                return false;
            }

            var colorIndex = codes[index + 2];
            index += 2;
            if (colorIndex is < 0 or > 255)
            {
                WarnOnce(
                    "invalid-256-index",
                    "Warning: svg: 256-color index is out of range; using default colors");
                return false;
            }

            var hex = Ansi256.ToHex(_palette, colorIndex);
            if (isBackground)
            {
                _style = _style with { Background = hex };
            }
            else
            {
                _style = _style with
                {
                    Foreground = hex,
                    ForegroundIsBright = colorIndex is >= 8 and <= 15,
                };
            }

            return true;
        }

        if (mode == 2)
        {
            if (index + 4 >= codes.Count)
            {
                index += Math.Min(4, codes.Count - index - 1);
                WarnOnce(
                    "invalid-true-color",
                    "Warning: svg: true-color SGR is missing RGB components; using default colors");
                return false;
            }

            var r = codes[index + 2];
            var g = codes[index + 3];
            var b = codes[index + 4];
            index += 4;
            if (r is < 0 or > 255 || g is < 0 or > 255 || b is < 0 or > 255)
            {
                WarnOnce(
                    "invalid-true-color",
                    "Warning: svg: true-color RGB value is out of range; using default colors");
                return false;
            }

            var hex = AnsiTrueColor.ToHex(r, g, b);
            if (isBackground)
            {
                _style = _style with { Background = hex };
            }
            else
            {
                _style = _style with
                {
                    Foreground = hex,
                    ForegroundIsBright = true,
                };
            }

            return true;
        }

        index += 1;
        WarnOnce(
            "unsupported-extended-color",
            $"Warning: svg: unsupported extended color mode {mode}; using default colors");
        return false;
    }

    private void WarnOnce(string key, string message)
    {
        if (!_warnedKeys.Add(key))
            return;

        Console.Error.WriteLine(message);
    }

    private string PaletteColor(int index, bool bright)
    {
        var paletteIndex = bright ? index + 8 : index;
        if (paletteIndex >= 0 && paletteIndex < _palette.Length)
            return _palette[paletteIndex];
        return _render.Theme.Fg;
    }

    private void PutChar(char ch)
    {
        if (_cursorY >= _height)
            ScrollUp();
        if (_cursorY < 0)
            _cursorY = 0;
        if (_cursorX >= _width)
        {
            _cursorX = 0;
            _cursorY++;
            if (_cursorY >= _height)
                ScrollUp();
        }

        var fg = _style.Foreground;
        if (_style.Bold && !_style.ForegroundIsBright)
            fg = BrightenIfPossible(fg);

        _cells[_cursorY, _cursorX] = new TerminalCell(
            ch,
            fg,
            _style.Background,
            _style.Bold,
            _style.Underline);
        _cursorX++;
    }

    private string BrightenIfPossible(string fg)
    {
        for (var i = 0; i < 8 && i < _palette.Length; i++)
        {
            if (string.Equals(_palette[i], fg, StringComparison.OrdinalIgnoreCase) && i + 8 < _palette.Length)
                return _palette[i + 8];
        }

        return fg;
    }

    private void ScrollUp()
    {
        for (var row = 1; row < _height; row++)
        {
            for (var col = 0; col < _width; col++)
                _cells[row - 1, col] = _cells[row, col];
        }

        for (var col = 0; col < _width; col++)
            _cells[_height - 1, col] = EmptyCell();

        _cursorY = _height - 1;
    }

    private void ClearAll()
    {
        for (var row = 0; row < _height; row++)
        {
            for (var col = 0; col < _width; col++)
                _cells[row, col] = EmptyCell();
        }

        _cursorX = 0;
        _cursorY = 0;
    }

    private void ClearLineFromCursor(int mode)
    {
        if (_cursorY < 0 || _cursorY >= _height)
            return;

        if (mode == 2)
        {
            for (var col = 0; col < _width; col++)
                _cells[_cursorY, col] = EmptyCell();
            return;
        }

        for (var col = _cursorX; col < _width; col++)
            _cells[_cursorY, col] = EmptyCell();
    }

    private TerminalCell EmptyCell() =>
        new(' ', _defaultStyle.Foreground, null, false, false);

    private static string[] ParsePalette(string palette)
    {
        var parts = palette.Split(':', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 16)
            return parts[..16];

        var defaults = DefaultPaletteColors();
        for (var i = 0; i < parts.Length && i < defaults.Length; i++)
            defaults[i] = parts[i];
        return defaults;
    }

    internal static string[] DefaultPaletteColors() =>
    [
        "#151515", "#ac4142", "#7e8e50", "#e5b567", "#6c99bb", "#9f4e85", "#7dd6cf", "#d0d0d0",
        "#505050", "#ac4142", "#7e8e50", "#e5b567", "#6c99bb", "#9f4e85", "#7dd6cf", "#f5f5f5"
    ];
}

internal readonly record struct AnsiStyle(string Foreground, string? Background, bool Bold, bool Underline, bool ForegroundIsBright)
{
    public static AnsiStyle Default(string fg) => new(fg, null, false, false, false);
}

internal static class AnsiTrueColor
{
    internal static string ToHex(int r, int g, int b) =>
        string.Create(CultureInfo.InvariantCulture, $"#{r:x2}{g:x2}{b:x2}");
}

internal static class Ansi256
{
    internal static string ToHex(string[] palette16, int index)
    {
        if (index is >= 0 and <= 15)
        {
            if ((uint)index < palette16.Length)
                return palette16[index];
            return RenderSettingsResolver.DefaultFg;
        }

        if (index is >= 16 and <= 231)
        {
            var n = index - 16;
            var r = (n / 36) % 6 * 40;
            var g = (n / 6) % 6 * 40;
            var b = (n % 6) * 40;
            if (r > 0) r += 55;
            if (g > 0) g += 55;
            if (b > 0) b += 55;
            return ToHex(r, g, b);
        }

        var gray = 8 + 10 * (index - 232);
        return ToHex(gray, gray, gray);
    }

    private static string ToHex(int r, int g, int b) =>
        string.Create(CultureInfo.InvariantCulture, $"#{r:x2}{g:x2}{b:x2}");
}

internal static class ThemePresets
{
    internal const string DarkName = "dark";
    internal const string LightName = "light";
    internal static string ExpectedPresetNames => $"{DarkName}|{LightName}";

    private static readonly ResolvedTheme DarkTheme = new(
        "#d0d0d0",
        "#282c34",
        "#151515:#ac4142:#7e8e50:#e5b567:#6c99bb:#9f4e85:#7dd6cf:#d0d0d0:#505050:#ac4142:#7e8e50:#e5b567:#6c99bb:#9f4e85:#7dd6cf:#f5f5f5");

    private static readonly ResolvedTheme LightTheme = new(
        "#383838",
        "#fafafa",
        "#383838:#c82828:#548b2e:#a88800:#2871aa:#9a4a96:#008787:#585858:#686868:#e74c3c:#69a845:#d4a017:#3498db:#c678dd:#20b2aa:#fafafa");

    internal static ResolvedTheme Dark => DarkTheme;
    internal static ResolvedTheme Light => LightTheme;

    internal static bool TryGet(string name, out ResolvedTheme theme)
    {
        switch (name.Trim().ToLowerInvariant())
        {
            case DarkName:
                theme = DarkTheme;
                return true;
            case LightName:
                theme = LightTheme;
                return true;
            default:
                theme = default;
                return false;
        }
    }

    internal static bool TryParse(string text, out string presetName, out string error)
    {
        presetName = text.Trim();
        if (presetName.Length == 0)
        {
            error = "--theme requires a value";
            return false;
        }

        if (!TryGet(presetName, out _))
        {
            error = $"unknown theme preset: {presetName} (expected: {ExpectedPresetNames})";
            return false;
        }

        error = "";
        return true;
    }
}

internal static class RenderSettingsResolver
{
    internal const int MinFontSize = 1;
    internal const int MaxFontSize = 128;
    internal const int MinTerminalCols = 1;
    internal const int MaxTerminalCols = 512;
    internal const int MinTerminalRows = 1;
    internal const int MaxTerminalRows = 512;

    internal static bool IsValidTerminalSize(int cols, int rows) =>
        cols is >= MinTerminalCols and <= MaxTerminalCols &&
        rows is >= MinTerminalRows and <= MaxTerminalRows;

    internal const int DefaultFontSize = 16;
    internal static string DefaultFg => ThemePresets.Dark.Fg;
    internal static string DefaultBg => ThemePresets.Dark.Bg;
    internal static string DefaultPalette => ThemePresets.Dark.Palette;

    internal static bool TryParseFontSize(string text, out int fontSize, out string error)
    {
        fontSize = 0;
        error = "";
        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out fontSize))
        {
            error = $"invalid --font-size value: {text}";
            return false;
        }

        if (fontSize is < MinFontSize or > MaxFontSize)
        {
            error = $"--font-size must be between {MinFontSize} and {MaxFontSize}: {fontSize}";
            return false;
        }

        return true;
    }

    internal static bool TryResolve(
        Scenario scenario,
        string? cliThemePreset,
        out ResolvedRenderSettings settings,
        out string error)
    {
        settings = default;
        error = "";
        var render = scenario.Render;
        var fontSize = render?.FontSize ?? DefaultFontSize;
        if (fontSize is < MinFontSize or > MaxFontSize)
            fontSize = DefaultFontSize;

        if (!TryResolveTheme(render?.Theme, cliThemePreset, out var theme, out error))
            return false;

        settings = new ResolvedRenderSettings(fontSize, theme);
        return true;
    }

    internal static bool TryResolveTheme(
        ScenarioTheme? theme,
        string? cliThemePreset,
        out ResolvedTheme resolved,
        out string error)
    {
        resolved = default;
        error = "";

        var presetName = cliThemePreset ?? theme?.Preset;
        if (string.IsNullOrWhiteSpace(presetName))
            presetName = ThemePresets.DarkName;

        if (!ThemePresets.TryGet(presetName, out var baseTheme))
        {
            error = $"unknown theme preset: {presetName} (expected: {ThemePresets.ExpectedPresetNames})";
            return false;
        }

        resolved = MergeTheme(baseTheme, theme);
        return true;
    }

    internal static ResolvedRenderSettings ApplySvgOverrides(
        ResolvedRenderSettings settings,
        int? fontSizeOverride,
        string? themePresetOverride,
        out string error)
    {
        error = "";
        if (themePresetOverride is not null)
        {
            if (!ThemePresets.TryGet(themePresetOverride, out var theme))
            {
                error = $"unknown theme preset: {themePresetOverride} (expected: {ThemePresets.ExpectedPresetNames})";
                return settings;
            }

            settings = settings with { Theme = theme };
        }

        if (fontSizeOverride is int fontSize)
            settings = settings with { FontSize = fontSize };

        return settings;
    }

    private static ResolvedTheme MergeTheme(ResolvedTheme baseTheme, ScenarioTheme? overrides)
    {
        if (overrides is null)
            return baseTheme;

        return new ResolvedTheme(
            string.IsNullOrWhiteSpace(overrides.Fg) ? baseTheme.Fg : overrides.Fg!,
            string.IsNullOrWhiteSpace(overrides.Bg) ? baseTheme.Bg : overrides.Bg!,
            string.IsNullOrWhiteSpace(overrides.Palette) ? baseTheme.Palette : overrides.Palette!);
    }

    internal static ResolvedRenderSettings Resolve(Scenario scenario)
    {
        if (!TryResolve(scenario, cliThemePreset: null, out var settings, out var error))
            throw new InvalidOperationException(error);

        return settings;
    }

    internal static string ResolveCastSvgOutputPath(string castPath, string? outputArg)
    {
        if (outputArg is not null)
        {
            var full = Path.GetFullPath(outputArg);
            return Path.Combine(
                Path.GetDirectoryName(full) ?? ".",
                Path.GetFileNameWithoutExtension(full) + ".svg");
        }

        return Path.ChangeExtension(Path.GetFullPath(castPath), ".svg")!;
    }

    internal static string ResolveOutputStem(string scenarioPath, string? outputArg)
    {
        if (outputArg is not null)
        {
            var full = Path.GetFullPath(outputArg);
            return Path.Combine(Path.GetDirectoryName(full) ?? ".", Path.GetFileNameWithoutExtension(full));
        }

        return Path.ChangeExtension(Path.GetFullPath(scenarioPath), null)!;
    }
}
