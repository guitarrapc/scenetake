using System.Globalization;
using System.Text;

internal static class SvgRender
{
    internal static void WriteSvg(
        IReadOnlyList<CastEvent> events,
        int width,
        int height,
        ResolvedRenderSettings render,
        string outputPath)
    {
        var (canvasWidth, canvasHeight) = TerminalReplay.ResolveCanvasSize(width, height, events);
        var theme = TerminalTheme.FromResolved(render.Theme);
        var frames = TerminalReplay.BuildFrames(events, width, height, canvasWidth, canvasHeight, theme);
        var svg = SvgFrameRenderer.Render(frames, render, canvasWidth, canvasHeight);
        File.WriteAllText(outputPath, svg, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }
}

internal readonly record struct ResolvedRenderSettings(int FontSize, string FontFamily, ResolvedTheme Theme)
{
    public string DefaultFg => Theme.Fg;
}

internal readonly record struct ResolvedTheme(string Fg, string Bg, string Palette);

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
    internal const int MaxFontFamilyLength = 256;
    internal const int MaxFontFamilyCount = 10;
    internal const string FontSizeTagPrefix = "s2c:font-size=";
    internal const string FontFamilyTagPrefix = "s2c:font-family=";
    internal const string DefaultFontFamily =
        "ui-monospace, \"Cascadia Mono\", \"Cascadia Code\", \"JetBrains Mono\", \"Noto Sans Mono\", SFMono-Regular, Menlo, Monaco, Consolas, \"Liberation Mono\", monospace";
    internal static string DefaultFg => ThemePresets.Dark.Fg;
    internal static string DefaultBg => ThemePresets.Dark.Bg;
    internal static string DefaultPalette => ThemePresets.Dark.Palette;

    internal static bool TryParseFontFamily(string text, out string fontFamily, out string error)
    {
        fontFamily = "";
        error = "";
        if (string.IsNullOrWhiteSpace(text))
        {
            error = "font-family must not be empty";
            return false;
        }

        text = text.Trim();
        if (text.Length > MaxFontFamilyLength)
        {
            error = $"font-family must be at most {MaxFontFamilyLength} characters";
            return false;
        }

        foreach (var c in text)
        {
            if (char.IsControl(c) || c is ';' or '{' or '}' or '<' or '>')
            {
                error = $"font-family: invalid character '{c}'";
                return false;
            }
        }

        var familyCount = 1;
        foreach (var c in text)
        {
            if (c == ',')
                familyCount++;
        }

        var hasMono = text.Contains("monospace", StringComparison.OrdinalIgnoreCase)
            || text.Contains("ui-monospace", StringComparison.OrdinalIgnoreCase);
        var maxAllowed = hasMono ? MaxFontFamilyCount : MaxFontFamilyCount - 1;
        if (familyCount > maxAllowed)
        {
            error = $"font-family must have at most {maxAllowed} families";
            return false;
        }

        fontFamily = hasMono ? text : $"{text}, monospace";
        if (fontFamily.Length > MaxFontFamilyLength)
        {
            error = $"font-family must be at most {MaxFontFamilyLength} characters";
            return false;
        }

        return true;
    }

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

        var fontFamily = DefaultFontFamily;
        if (!string.IsNullOrWhiteSpace(render?.FontFamily))
        {
            if (!TryParseFontFamily(render!.FontFamily!, out fontFamily, out error))
            {
                error = $"invalid render.font-family: {error}";
                return false;
            }
        }

        settings = new ResolvedRenderSettings(fontSize, fontFamily, theme);
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
        string? fontFamilyOverride,
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

        if (fontFamilyOverride is string fontFamily)
            settings = settings with { FontFamily = fontFamily };

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
    private const double DefaultMaxFps = 12d;
    private const string Space = " ";

    [ThreadStatic] private static StringBuilder? t_runText;

    internal static string Render(
        IReadOnlyList<ReplayFrame> frames,
        ResolvedRenderSettings render,
        int canvasWidth,
        int canvasHeight)
    {
        if (frames.Count == 0)
            return BuildEmptySvg(render, canvasWidth, canvasHeight);

        frames = OptimizeFrames(frames);
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

    private static bool RowEquals(ScreenBuffer left, ScreenBuffer right, int row) =>
        left.RowEquals(right, row);

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

            if (cell.Text == Space)
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
            var runText = (t_runText ??= new StringBuilder(64));
            runText.Clear();
            runText.Append(cell.Text);
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

            var visible = runText.ToString().TrimEnd();
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
            var styleAttr = runItalic && runUnderline ? " style=\"font-style:italic;text-decoration:underline;\""
                : runItalic ? " style=\"font-style:italic;\""
                : runUnderline ? " style=\"text-decoration:underline;\""
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

    private static string EscapeXml(string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] is '&' or '<' or '>' or '"')
            {
                var sb = new StringBuilder(text.Length + 8);
                sb.Append(text, 0, i);
                for (; i < text.Length; i++)
                {
                    switch (text[i])
                    {
                        case '&': sb.Append("&amp;"); break;
                        case '<': sb.Append("&lt;"); break;
                        case '>': sb.Append("&gt;"); break;
                        case '"': sb.Append("&quot;"); break;
                        default: sb.Append(text[i]); break;
                    }
                }

                return sb.ToString();
            }
        }

        return text;
    }

    private static List<ReplayFrame> OptimizeFrames(IReadOnlyList<ReplayFrame> frames, double maxFps = DefaultMaxFps)
    {
        if (frames.Count <= 1)
            return frames.ToList();

        var normalized = NormalizeTiming(frames, maxFps);
        var reduced = ReduceFrames(normalized, maxFps);
        return SpreadCollapsedFrameTimes(reduced, maxFps);
    }

    private static List<ReplayFrame> NormalizeTiming(IReadOnlyList<ReplayFrame> frames, double maxFps)
    {
        if (frames.Count == 0 || maxFps <= 0)
            return frames.ToList();

        var interval = 1d / maxFps;
        var normalized = new List<ReplayFrame>(frames.Count);
        var lastTime = 0d;
        for (var i = 0; i < frames.Count; i++)
        {
            var rawTime = Math.Max(0d, frames[i].Time);
            var quantizedTime = Math.Round(rawTime / interval, MidpointRounding.AwayFromZero) * interval;
            if (i > 0 && quantizedTime < lastTime)
                quantizedTime = lastTime;
            normalized.Add(CloneAtTime(frames[i], quantizedTime));
            lastTime = quantizedTime;
        }

        return normalized;
    }

    private static List<ReplayFrame> ReduceFrames(IReadOnlyList<ReplayFrame> frames, double maxFps)
    {
        if (frames.Count <= 2 || maxFps <= 0)
            return frames.ToList();

        var minimumInterval = 1d / maxFps;
        var reduced = new List<ReplayFrame>(frames.Count) { frames[0] };
        var lastKeptTime = frames[0].Time;
        var lastKeptSignature = frames[0].Signature;
        ReplayFrame? pending = null;
        ulong pendingSignature = 0;

        for (var i = 1; i < frames.Count - 1; i++)
        {
            var frame = frames[i];
            var signature = frame.Signature;
            var visualChanged = signature != lastKeptSignature;
            var elapsed = frame.Time - lastKeptTime;
            if (elapsed < minimumInterval)
            {
                if (visualChanged)
                {
                    pending = frame;
                    pendingSignature = signature;
                }

                continue;
            }

            if (pending is not null)
            {
                reduced.Add(pending);
                lastKeptTime = pending.Time;
                lastKeptSignature = pendingSignature;
                pending = null;
                signature = frame.Signature;
                visualChanged = signature != lastKeptSignature;
                elapsed = frame.Time - lastKeptTime;
                if (elapsed < minimumInterval)
                {
                    if (visualChanged)
                    {
                        pending = frame;
                        pendingSignature = signature;
                    }

                    continue;
                }

                if (!visualChanged)
                    continue;
            }

            reduced.Add(frame);
            lastKeptTime = frame.Time;
            lastKeptSignature = signature;
        }

        if (pending is not null && !ReferenceEquals(reduced[^1], pending))
            reduced.Add(pending);

        var last = frames[^1];
        if (reduced[^1].Signature != last.Signature || Math.Abs(reduced[^1].Time - last.Time) > 1e-9)
            reduced.Add(last);

        return reduced;
    }

    private static List<ReplayFrame> SpreadCollapsedFrameTimes(IReadOnlyList<ReplayFrame> frames, double maxFps)
    {
        if (frames.Count <= 1)
            return frames.ToList();

        List<ReplayFrame>? adjusted = null;
        for (var runStart = 0; runStart < frames.Count;)
        {
            var runEnd = runStart;
            while (runEnd + 1 < frames.Count && HaveSameTime(frames[runEnd + 1].Time, frames[runStart].Time))
                runEnd++;

            if (runEnd == runStart)
            {
                adjusted?.Add(frames[runStart]);
                runStart++;
                continue;
            }

            adjusted ??= new List<ReplayFrame>(frames.Count);
            if (adjusted.Count == 0)
            {
                for (var copyIndex = 0; copyIndex < runStart; copyIndex++)
                    adjusted.Add(frames[copyIndex]);
            }

            var runCount = runEnd - runStart + 1;
            if (runStart == 0 && frames[runStart].Time <= 0d)
            {
                var upperBound = runEnd + 1 < frames.Count ? frames[runEnd + 1].Time : 1d / maxFps;
                if (upperBound <= 0d)
                    upperBound = 1d / maxFps;
                var step = upperBound / runCount;
                for (var offset = 0; offset < runCount; offset++)
                    adjusted.Add(CloneAtTime(frames[runStart + offset], step * offset));
            }
            else
            {
                var lowerBound = adjusted.Count > 0 ? adjusted[^1].Time : 0d;
                var upperBound = frames[runStart].Time;
                var step = (upperBound - lowerBound) / runCount;
                if (step <= 0d)
                    step = (1d / maxFps) / runCount;
                for (var offset = 0; offset < runCount; offset++)
                    adjusted.Add(CloneAtTime(frames[runStart + offset], lowerBound + (step * (offset + 1))));
            }

            runStart = runEnd + 1;
        }

        return adjusted ?? frames.ToList();
    }

    private static ReplayFrame CloneAtTime(ReplayFrame frame, double time) =>
        new(time, frame.Buffer, frame.ViewportWidth, frame.ViewportHeight);

    private static bool HaveSameTime(double left, double right) =>
        Math.Abs(left - right) <= 1e-9;

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

