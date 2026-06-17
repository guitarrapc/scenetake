using System.Globalization;
using System.Text;

internal static class SvgRender
{
    private const double LineHeightFactor = 1.25;
    private const double CharWidthFactor = 0.62;
    private const double Padding = 8.0;

    internal static void WriteSvg(
        IReadOnlyList<CastEvent> events,
        int width,
        int height,
        ResolvedRenderSettings render,
        string outputPath)
    {
        var frames = BuildFrames(events, width, height, render);
        if (frames.Count == 0)
            frames.Add(new TerminalFrame(0, CreateEmptyScreen(width, height, render)));

        var totalDuration = events.Count > 0 ? events[^1].Time : 0.0;
        if (totalDuration <= 0)
            totalDuration = 0.001;

        var svg = BuildSvgDocument(frames, width, height, render, totalDuration);
        File.WriteAllText(outputPath, svg, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static List<TerminalFrame> BuildFrames(
        IReadOnlyList<CastEvent> events,
        int width,
        int height,
        ResolvedRenderSettings render)
    {
        var terminal = new TerminalEmulator(width, height, render);
        var frames = new List<TerminalFrame>();
        TerminalScreen? lastScreen = null;

        foreach (var ev in events)
        {
            terminal.Write(ev.Data);
            var screen = terminal.CaptureScreen();
            if (lastScreen is null || !screen.ContentEquals(lastScreen))
            {
                frames.Add(new TerminalFrame(ev.Time, screen));
                lastScreen = screen;
            }
        }

        return frames;
    }

    private static TerminalScreen CreateEmptyScreen(int width, int height, ResolvedRenderSettings render)
    {
        var terminal = new TerminalEmulator(width, height, render);
        return terminal.CaptureScreen();
    }

    private static string BuildSvgDocument(
        IReadOnlyList<TerminalFrame> frames,
        int width,
        int height,
        ResolvedRenderSettings render,
        double totalDuration)
    {
        var fontSize = render.FontSize;
        var lineHeight = fontSize * LineHeightFactor;
        var charWidth = fontSize * CharWidthFactor;
        var svgWidth = width * charWidth + Padding * 2;
        var svgHeight = height * lineHeight + Padding * 2;

        var sb = new StringBuilder(64 * 1024);
        sb.AppendLine(CultureInfo.InvariantCulture, $"<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{svgWidth:0.##}\" height=\"{svgHeight:0.##}\" viewBox=\"0 0 {svgWidth:0.##} {svgHeight:0.##}\">");
        sb.AppendLine("<style>");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"text {{ font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, \"Liberation Mono\", \"Courier New\", monospace; font-size: {fontSize}px; white-space: pre; }}");
        sb.AppendLine(CultureInfo.InvariantCulture, $".bg {{ fill: {render.Theme.Bg}; }}");

        for (var i = 0; i < frames.Count; i++)
            AppendFrameKeyframes(sb, i, frames, totalDuration);

        sb.AppendLine("</style>");

        sb.AppendLine(CultureInfo.InvariantCulture,
            $"<rect class=\"bg\" x=\"0\" y=\"0\" width=\"{svgWidth:0.##}\" height=\"{svgHeight:0.##}\"/>");

        for (var i = 0; i < frames.Count; i++)
        {
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"<g class=\"frame frame-{i}\" style=\"animation: frame-{i} {totalDuration.ToString("0.######", CultureInfo.InvariantCulture)}s linear forwards\">");
            AppendFrameContent(sb, frames[i].Screen, width, height, render, charWidth, lineHeight);
            sb.AppendLine("</g>");
        }

        sb.AppendLine("</svg>");
        return sb.ToString();
    }

    private static void AppendFrameKeyframes(
        StringBuilder sb,
        int index,
        IReadOnlyList<TerminalFrame> frames,
        double totalDuration)
    {
        var start = frames[index].Time;
        var end = index + 1 < frames.Count ? frames[index + 1].Time : totalDuration;
        var startPct = (start / totalDuration * 100).ToString("0.######", CultureInfo.InvariantCulture);
        var endPct = (end / totalDuration * 100).ToString("0.######", CultureInfo.InvariantCulture);

        sb.AppendLine(CultureInfo.InvariantCulture, $"@keyframes frame-{index} {{");
        sb.AppendLine("  0%, 100% { opacity: 0; }");
        if (start > 0)
            sb.AppendLine(CultureInfo.InvariantCulture, $"  {startPct}% {{ opacity: 0; }}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"  {startPct}%, {endPct}% {{ opacity: 1; }}");
        if (end < totalDuration)
            sb.AppendLine(CultureInfo.InvariantCulture, $"  {endPct}%, 100% {{ opacity: 0; }}");
        sb.AppendLine("}");
        sb.AppendLine(CultureInfo.InvariantCulture, $".frame-{index} {{ opacity: 0; }}");
    }

    private static void AppendFrameContent(
        StringBuilder sb,
        TerminalScreen screen,
        int width,
        int height,
        ResolvedRenderSettings render,
        double charWidth,
        double lineHeight)
    {
        for (var row = 0; row < height; row++)
        {
            AppendRow(sb, screen, row, width, charWidth, lineHeight);
        }
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
            if (text.ToString().TrimEnd(' ').Length == 0 && runBg is null)
                continue;

            var x = Padding + runStart * charWidth;
            if (runBg is not null)
            {
                sb.AppendLine(CultureInfo.InvariantCulture,
                    $"<rect x=\"{x:0.##}\" y=\"{Padding + row * lineHeight:0.##}\" width=\"{runLen * charWidth:0.##}\" height=\"{lineHeight:0.##}\" fill=\"{runBg}\"/>");
            }

            var visible = text.ToString().TrimEnd(' ');
            if (visible.Length == 0)
                continue;

            var weight = runBold ? "bold" : "normal";
            if (runUnderline)
            {
                sb.AppendLine(CultureInfo.InvariantCulture,
                    $"<text x=\"{x:0.##}\" y=\"{y:0.##}\" fill=\"{runFg}\" font-weight=\"{weight}\" text-decoration=\"underline\">{EscapeXml(visible)}</text>");
            }
            else
            {
                sb.AppendLine(CultureInfo.InvariantCulture,
                    $"<text x=\"{x:0.##}\" y=\"{y:0.##}\" fill=\"{runFg}\" font-weight=\"{weight}\">{EscapeXml(visible)}</text>");
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

internal readonly record struct TerminalFrame(double Time, TerminalScreen Screen);

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
    private readonly int _width;
    private readonly int _height;
    private readonly ResolvedRenderSettings _render;
    private readonly string[] _palette;
    private readonly TerminalCell[,] _cells;
    private int _cursorX;
    private int _cursorY;
    private AnsiStyle _style;
    private AnsiStyle _defaultStyle;
    private bool _warned256Color;

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

    public TerminalScreen CaptureScreen()
    {
        var screen = new TerminalScreen(_width, _height);
        for (var row = 0; row < _height; row++)
        {
            for (var col = 0; col < _width; col++)
                screen[row, col] = _cells[row, col];
        }

        return screen;
    }

    private int ParseCsi(string data, int index)
    {
        var start = index;
        while (index < data.Length && data[index] != 'm' && data[index] != 'J' && data[index] != 'H' && data[index] != 'K')
            index++;

        if (index >= data.Length)
            return data.Length - 1;

        var end = index;
        var command = data[end];
        var body = data[start..end];
        var parts = body.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var numbers = new List<int>();
        foreach (var part in parts)
        {
            if (int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n))
                numbers.Add(n);
        }

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
                    if (TryConsume256Color(codes, ref i, code == 48, out var applied))
                    {
                        if (applied)
                            break;
                    }
                    Warn256Color();
                    if (code == 38)
                        _style = _style with { Foreground = _defaultStyle.Foreground };
                    else
                        _style = _style with { Background = null };
                    break;
                default:
                    break;
            }
        }
    }

    private static bool TryConsume256Color(List<int> codes, ref int index, bool isBackground, out bool applied)
    {
        applied = false;
        if (index + 1 >= codes.Count)
            return true;

        if (codes[index + 1] != 5)
            return true;

        if (index + 2 >= codes.Count)
            return true;

        index += 2;
        applied = false;
        return true;
    }

    private void Warn256Color()
    {
        if (_warned256Color)
            return;

        _warned256Color = true;
        Console.Error.WriteLine("Warning: svg: 256-color SGR is not supported in v1; using default colors");
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

internal static class RenderSettingsResolver
{
    internal const int DefaultFontSize = 16;
    internal const string DefaultFg = "#d0d0d0";
    internal const string DefaultBg = "#282c34";
    internal const string DefaultPalette =
        "#151515:#ac4142:#7e8e50:#e5b567:#6c99bb:#9f4e85:#7dd6cf:#d0d0d0:#505050:#ac4142:#7e8e50:#e5b567:#6c99bb:#9f4e85:#7dd6cf:#f5f5f5";

    internal static ResolvedRenderSettings Resolve(Scenario scenario)
    {
        var render = scenario.Render;
        var fontSize = render?.FontSize ?? DefaultFontSize;
        if (fontSize <= 0)
            fontSize = DefaultFontSize;

        var theme = render?.Theme;
        return new ResolvedRenderSettings(
            fontSize,
            new ResolvedTheme(
                string.IsNullOrWhiteSpace(theme?.Fg) ? DefaultFg : theme!.Fg!,
                string.IsNullOrWhiteSpace(theme?.Bg) ? DefaultBg : theme!.Bg!,
                string.IsNullOrWhiteSpace(theme?.Palette) ? DefaultPalette : theme!.Palette!));
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
