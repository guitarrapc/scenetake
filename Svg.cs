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

internal enum WindowStyle
{
    None,
    Macos,
    Windows,
}

internal readonly record struct ResolvedRenderSettings(
    int FontSize,
    string FontFamily,
    ResolvedTheme Theme,
    WindowStyle Window = WindowStyle.None,
    int MaxFps = RenderSettingsResolver.DefaultMaxFps)
{
    public string DefaultFg => Theme.Fg;
}

internal readonly record struct ResolvedTheme(string Fg, string Bg, string Palette);

internal static class RenderSettingsResolver
{
    internal const int MinFontSize = 1, MaxFontSize = 128;
    internal const int MinTerminalCols = 1, MaxTerminalCols = 512;
    internal const int MinTerminalRows = 1, MaxTerminalRows = 512;
    internal const int DefaultFontSize = 16;
    internal const int DefaultMaxFps = 0;
    internal const int MinMaxFps = 0, MaxMaxFps = 120;
    internal const int MaxFontFamilyLength = 256, MaxFontFamilyCount = 10;
    internal const string FontSizeTagPrefix = "s2c:font-size=";
    internal const string FontFamilyTagPrefix = "s2c:font-family=";
    internal const string WindowTagPrefix = "s2c:window=";
    internal const string DarkName = "dark", LightName = "light";
    internal const string MacosWindowName = "macos", WindowsWindowName = "windows", NoneWindowName = "none";
    internal static string ExpectedPresetNames => $"{DarkName}|{LightName}";
    internal static string ExpectedWindowNames => $"{NoneWindowName}|{MacosWindowName}|{WindowsWindowName}";
    internal const string DefaultFontFamily =
        "ui-monospace, \"Cascadia Mono\", \"Cascadia Code\", \"JetBrains Mono\", \"Noto Sans Mono\", SFMono-Regular, Menlo, Monaco, Consolas, \"Liberation Mono\", monospace";

    private static readonly ResolvedTheme DarkTheme = new(
        "#d0d0d0", "#282c34",
        "#151515:#ac4142:#7e8e50:#e5b567:#6c99bb:#9f4e85:#7dd6cf:#d0d0d0:#505050:#ac4142:#7e8e50:#e5b567:#6c99bb:#9f4e85:#7dd6cf:#f5f5f5");
    private static readonly ResolvedTheme LightTheme = new(
        "#383838", "#fafafa",
        "#383838:#c82828:#548b2e:#a88800:#2871aa:#9a4a96:#008787:#585858:#686868:#e74c3c:#69a845:#d4a017:#3498db:#c678dd:#20b2aa:#fafafa");

    internal static string DefaultFg => DarkTheme.Fg;
    internal static string DefaultBg => DarkTheme.Bg;
    internal static string DefaultPalette => DarkTheme.Palette;

    internal static bool IsValidTerminalSize(int cols, int rows) =>
        cols is >= MinTerminalCols and <= MaxTerminalCols &&
        rows is >= MinTerminalRows and <= MaxTerminalRows;

    internal static bool TryGetPreset(string name, out ResolvedTheme theme)
    {
        switch (name.Trim().ToLowerInvariant())
        {
            case DarkName: theme = DarkTheme; return true;
            case LightName: theme = LightTheme; return true;
            default: theme = default; return false;
        }
    }

    internal static bool TryParsePreset(string text, out string presetName, out string error)
    {
        presetName = text.Trim();
        if (presetName.Length == 0) { error = "--theme requires a value"; return false; }
        if (!TryGetPreset(presetName, out _)) { error = $"unknown theme preset: {presetName} (expected: {ExpectedPresetNames})"; return false; }
        error = "";
        return true;
    }

    internal static bool TryParseWindow(string text, out WindowStyle window, out string error)
    {
        window = WindowStyle.None;
        error = "";
        switch (text.Trim().ToLowerInvariant())
        {
            case "":
            case NoneWindowName:
                return true;
            case MacosWindowName:
                window = WindowStyle.Macos;
                return true;
            case WindowsWindowName:
                window = WindowStyle.Windows;
                return true;
            default:
                error = $"unknown window style: {text} (expected: {ExpectedWindowNames})";
                return false;
        }
    }

    internal static string WindowToTagValue(WindowStyle window) => window switch
    {
        WindowStyle.Macos => MacosWindowName,
        WindowStyle.Windows => WindowsWindowName,
        _ => NoneWindowName,
    };

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

    internal static bool TryParseMaxFps(string text, out int maxFps, out string error)
    {
        maxFps = 0;
        error = "";
        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out maxFps))
        {
            error = $"invalid --max-fps value: {text}";
            return false;
        }

        if (maxFps is < MinMaxFps or > MaxMaxFps)
        {
            error = $"--max-fps must be between {MinMaxFps} and {MaxMaxFps} ({MinMaxFps} disables sampling): {maxFps}";
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

        if (!TryParseWindow(render?.Window ?? NoneWindowName, out var window, out error))
        {
            error = $"invalid render.window: {error}";
            return false;
        }

        settings = new ResolvedRenderSettings(fontSize, fontFamily, theme, window);
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
            presetName = RenderSettingsResolver.DarkName;

        if (!RenderSettingsResolver.TryGetPreset(presetName, out var baseTheme))
        {
            error = $"unknown theme preset: {presetName} (expected: {RenderSettingsResolver.ExpectedPresetNames})";
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
        WindowStyle? windowOverride,
        int? maxFpsOverride,
        out string error)
    {
        error = "";
        if (themePresetOverride is not null)
        {
            if (!RenderSettingsResolver.TryGetPreset(themePresetOverride, out var theme))
            {
                error = $"unknown theme preset: {themePresetOverride} (expected: {RenderSettingsResolver.ExpectedPresetNames})";
                return settings;
            }

            settings = settings with { Theme = theme };
        }

        if (fontSizeOverride is int fontSize)
            settings = settings with { FontSize = fontSize };

        if (fontFamilyOverride is string fontFamily)
            settings = settings with { FontFamily = fontFamily };

        if (windowOverride is WindowStyle window)
            settings = settings with { Window = window };

        if (maxFpsOverride is int maxFps)
            settings = settings with { MaxFps = maxFps };

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

    internal static string ResolveCastSvgOutputPath(string castPath, string? outputArg) =>
        ResolveOutputPath(castPath, outputArg, ".svg");

    internal static string ResolveOutputStem(string scenarioPath, string? outputArg) =>
        ResolveOutputPath(scenarioPath, outputArg, null);

    private static string ResolveOutputPath(string inputPath, string? outputArg, string? extension)
    {
        if (outputArg is not null)
        {
            var full = Path.GetFullPath(outputArg);
            var dir = Path.GetDirectoryName(full) ?? ".";
            var stem = Path.GetFileNameWithoutExtension(full);
            return extension is null ? Path.Combine(dir, stem) : Path.Combine(dir, stem + extension);
        }

        var resolved = Path.GetFullPath(inputPath);
        return extension is null ? Path.ChangeExtension(resolved, null)! : Path.ChangeExtension(resolved, extension)!;
    }
}

internal readonly record struct WindowChromePalette(
    string TitleBarBg,
    string Border,
    string MacClose,
    string MacMinimize,
    string MacMaximize,
    string WinButton);

internal static class WindowChromeTheme
{
    internal static WindowChromePalette For(WindowStyle window, bool light) =>
        (window, light) switch
        {
            (WindowStyle.Macos, false) => new WindowChromePalette(
                "#323232", "#1a1a1a", "#ff5f57", "#febc2e", "#28c840", "#8a8a8a"),
            (WindowStyle.Macos, true) => new WindowChromePalette(
                "#dcdcdc", "#b0b0b0", "#ff5f57", "#febc2e", "#28c840", "#8a8a8a"),
            (WindowStyle.Windows, false) => new WindowChromePalette(
                "#2d2d2d", "#404040", "#e81123", "#8a8a8a", "#8a8a8a", "#8a8a8a"),
            (WindowStyle.Windows, true) => new WindowChromePalette(
                "#f3f3f3", "#d0d0d0", "#e81123", "#8a8a8a", "#8a8a8a", "#8a8a8a"),
            _ => new WindowChromePalette(
                "#323232", "#1a1a1a", "#ff5f57", "#febc2e", "#28c840", "#8a8a8a"),
        };

    internal static bool IsLightBackground(string bg)
    {
        if (bg.Length != 7 || bg[0] != '#')
            return false;

        if (!TryHexByte(bg, 1, out var r) || !TryHexByte(bg, 3, out var g) || !TryHexByte(bg, 5, out var b))
            return false;

        var luminance = (0.299 * r) + (0.587 * g) + (0.114 * b);
        return luminance > 140;
    }

    private static bool TryHexByte(string color, int offset, out int value)
    {
        value = 0;
        if (!TryHexNibble(color[offset], out var hi) || !TryHexNibble(color[offset + 1], out var lo))
            return false;

        value = (hi << 4) | lo;
        return true;
    }

    private static bool TryHexNibble(char c, out int value)
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
    private const string Space = " ";

    [ThreadStatic] private static StringBuilder? t_runText;
    [ThreadStatic] private static StringBuilder? t_escape;

    internal static string Render(
        IReadOnlyList<ReplayFrame> frames,
        ResolvedRenderSettings render,
        int canvasWidth,
        int canvasHeight)
    {
        if (frames.Count == 0)
            return BuildEmptySvg(render, canvasWidth, canvasHeight);

        frames = OptimizeFrames(frames, render.MaxFps);
        var theme = TerminalTheme.FromResolved(render.Theme);
        var metrics = CreateMetrics(render.FontSize, canvasWidth, canvasHeight, render.Window, render.Theme.Bg);

        var layers = BuildLayers(frames, canvasWidth, canvasHeight);

        return BuildLayeredSvg(layers, metrics, render, theme, canvasWidth, canvasHeight);
    }

    private static LayerSet BuildLayers(
        IReadOnlyList<ReplayFrame> frames,
        int canvasWidth,
        int canvasHeight)
    {
        var rows = new List<AnimLayer>();
        var cursors = new List<AnimLayer>();
        var viewports = new List<AnimLayer>();
        var activeByRow = new AnimLayer?[canvasHeight];
        ScreenBuffer? previous = null;
        AnimLayer? cursorActive = null;
        AnimLayer? viewportActive = null;

        foreach (var frame in frames)
        {
            var time = frame.Time;
            var buffer = frame.Buffer;

            if (viewportActive is null || viewportActive.P0 != frame.ViewportWidth || viewportActive.P1 != frame.ViewportHeight)
            {
                if (viewportActive is not null)
                    viewportActive.Hide = time;
                viewportActive = new AnimLayer(viewports.Count == 0 ? 0 : time, frame.ViewportWidth, frame.ViewportHeight);
                viewports.Add(viewportActive);
            }

            if (!buffer.CursorVisible)
            {
                if (cursorActive is not null)
                {
                    cursorActive.Hide = time;
                    cursorActive = null;
                }
            }
            else if (cursorActive is null || cursorActive.P0 != buffer.CursorRow || cursorActive.P1 != buffer.CursorCol)
            {
                if (cursorActive is not null)
                    cursorActive.Hide = time;
                cursorActive = new AnimLayer(time, buffer.CursorRow, buffer.CursorCol);
                cursors.Add(cursorActive);
            }

            for (var row = 0; row < canvasHeight; row++)
            {
                if (activeByRow[row] is { Buffer: ScreenBuffer activeBuf } && activeBuf.RowEquals(buffer, row))
                    continue;

                if (activeByRow[row] is null && previous is not null && previous.RowEquals(buffer, row))
                    continue;

                if (buffer.IsRowBlank(row))
                {
                    if (activeByRow[row] is not { Buffer: ScreenBuffer prior } || prior.IsRowBlank(row))
                        continue;

                    RetireRowLayer(activeByRow, row, time);
                    if (!IsLikelyFullClear(previous, buffer))
                        continue;
                }
                else
                {
                    HideRowLayer(activeByRow, row, time);
                }

                var layer = new AnimLayer(time, row, buffer: buffer);
                rows.Add(layer);
                activeByRow[row] = layer;
            }

            previous = buffer;
        }

        return new LayerSet(rows, cursors, viewports);
    }

    private static void HideRowLayer(AnimLayer?[] activeByRow, int row, double time)
    {
        if (activeByRow[row] is { } layer)
            layer.Hide = time;
    }

    private static void RetireRowLayer(AnimLayer?[] activeByRow, int row, double time)
    {
        HideRowLayer(activeByRow, row, time);
        activeByRow[row] = null;
    }

    private static bool IsLikelyFullClear(ScreenBuffer? previous, ScreenBuffer current)
    {
        if (previous is null)
            return false;

        var changedToBlank = 0;
        for (var row = 0; row < current.Height; row++)
        {
            if (!previous.RowEquals(current, row) && current.IsRowBlank(row))
                changedToBlank++;
        }

        return changedToBlank >= Math.Max(1, current.Height / 2);
    }

    private static string BuildLayeredSvg(
        LayerSet layers,
        SvgMetrics metrics,
        ResolvedRenderSettings render,
        TerminalTheme theme,
        int canvasWidth,
        int canvasHeight)
    {
        var rowLayers = layers.Rows;
        var cursorLayers = layers.Cursors;
        var viewportLayers = layers.Viewports;
        var fadeText = LayerFadeSeconds.ToString("0.######", CultureInfo.InvariantCulture);
        var cursorOpacityText = CursorBlockOpacity.ToString("0.######", CultureInfo.InvariantCulture);

        if (viewportLayers.Count == 0)
            viewportLayers = [new AnimLayer(0, canvasWidth, canvasHeight)];

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
            AppendLayerStyle(sb, $".layer-{i}", rowLayers[i].Show, rowLayers[i].Hide);

        for (var i = 0; i < cursorLayers.Count; i++)
            AppendLayerStyle(sb, $".cursor-layer-{i}", cursorLayers[i].Show, cursorLayers[i].Hide);

        for (var i = 0; i < viewportLayers.Count; i++)
        {
            var viewportSelector = metrics.HasChrome
                ? $".viewport-mask-{i}, .viewport-bg-{i}, .viewport-chrome-{i}"
                : $".viewport-mask-{i}, .viewport-bg-{i}";
            AppendLayerStyle(sb, viewportSelector, viewportLayers[i].Show, viewportLayers[i].Hide);
        }

        sb.AppendLine("</style>");

        if (metrics.HasChrome)
        {
            var shadowBlur = (metrics.FontSize * 0.25).ToString("0.##", CultureInfo.InvariantCulture);
            sb.AppendLine("<defs>");
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"<filter id=\"window-shadow\" x=\"-25%\" y=\"-25%\" width=\"150%\" height=\"150%\">" +
                $"<feDropShadow dx=\"0\" dy=\"2\" stdDeviation=\"{shadowBlur}\" flood-opacity=\"0.22\"/></filter>");
        }
        else
        {
            sb.AppendLine("<defs>");
        }

        sb.AppendLine(CultureInfo.InvariantCulture,
            $"<mask id=\"viewport-mask\" x=\"0\" y=\"0\" width=\"{metrics.SvgWidth:0.##}\" height=\"{metrics.SvgHeight:0.##}\" maskUnits=\"userSpaceOnUse\">");
        for (var i = 0; i < viewportLayers.Count; i++)
        {
            var viewport = viewportLayers[i];
            var viewportWidth = metrics.ViewportPixelWidth(viewport.P0);
            var viewportHeight = metrics.ViewportPixelHeight(viewport.P1);
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"<rect class=\"layer viewport-mask-{i}\" x=\"{metrics.TerminalOriginX:0.##}\" y=\"{metrics.TerminalOriginY:0.##}\" width=\"{viewportWidth:0.##}\" height=\"{viewportHeight:0.##}\" fill=\"white\"/>");
        }

        sb.AppendLine("</mask>");
        sb.AppendLine("</defs>");

        if (metrics.HasChrome)
        {
            var chrome = WindowChromeTheme.For(render.Window, WindowChromeTheme.IsLightBackground(render.Theme.Bg));
            for (var i = 0; i < viewportLayers.Count; i++)
            {
                var viewport = viewportLayers[i];
                AppendWindowChrome(sb, metrics, chrome, render.Window, viewport.P0, viewport.P1, i);
            }
        }

        sb.AppendLine("<g mask=\"url(#viewport-mask)\">");
        for (var i = 0; i < viewportLayers.Count; i++)
        {
            var viewport = viewportLayers[i];
            var viewportWidth = metrics.ViewportPixelWidth(viewport.P0);
            var viewportHeight = metrics.ViewportPixelHeight(viewport.P1);
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"<rect class=\"bg layer viewport-bg-{i}\" x=\"{metrics.TerminalOriginX:0.##}\" y=\"{metrics.TerminalOriginY:0.##}\" width=\"{viewportWidth:0.##}\" height=\"{viewportHeight:0.##}\" fill=\"{render.Theme.Bg}\"/>");
        }

        var origin = metrics.ContentOrigin;
        for (var i = 0; i < rowLayers.Count; i++)
        {
            var layer = rowLayers[i];
            sb.AppendLine(CultureInfo.InvariantCulture, $"<g class=\"layer layer-{i}\">");
            AppendRow(sb, layer.Buffer!, layer.P0, origin.X, origin.Y, metrics, theme);
            sb.AppendLine("</g>");
        }

        for (var i = 0; i < cursorLayers.Count; i++)
        {
            var layer = cursorLayers[i];
            sb.AppendLine(CultureInfo.InvariantCulture, $"<g class=\"layer cursor-layer-{i}\">");
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"<rect class=\"cursor-block\" x=\"{origin.X + layer.P1 * metrics.CharWidth:0.##}\" y=\"{origin.Y + layer.P0 * metrics.LineHeight:0.##}\" width=\"{metrics.CharWidth:0.##}\" height=\"{metrics.LineHeight:0.##}\"/>");
            sb.AppendLine("</g>");
        }

        sb.AppendLine("</g>");
        sb.AppendLine("</svg>");
        return sb.ToString();
    }

    private static void AppendLayerStyle(StringBuilder sb, string selector, double showTime, double? hideTime)
    {
        sb.Append(selector);
        if (hideTime is double hide)
        {
            sb.Append(" { animation-name: layer-in, layer-out; animation-delay: ");
            sb.Append(showTime.ToString("0.######", CultureInfo.InvariantCulture));
            sb.Append("s, ");
            sb.Append(hide.ToString("0.######", CultureInfo.InvariantCulture));
            sb.AppendLine("s; }");
            return;
        }

        sb.Append(" { animation-name: layer-in; animation-delay: ");
        sb.Append(showTime.ToString("0.######", CultureInfo.InvariantCulture));
        sb.AppendLine("s; }");
    }

    private static void AppendWindowChrome(
        StringBuilder sb,
        SvgMetrics metrics,
        WindowChromePalette chrome,
        WindowStyle window,
        int cols,
        int rows,
        int layerIndex)
    {
        var x = metrics.TerminalOriginX;
        var y = metrics.FrameOriginY;
        var width = metrics.ViewportPixelWidth(cols);
        var terminalHeight = metrics.ViewportPixelHeight(rows);
        var totalHeight = metrics.TitleBarHeight + terminalHeight;
        var rx = metrics.CornerRadius;
        var titleClass = $"layer viewport-chrome-{layerIndex}";

        sb.AppendLine(CultureInfo.InvariantCulture,
            $"<rect class=\"{titleClass}\" x=\"{x:0.##}\" y=\"{y:0.##}\" width=\"{width:0.##}\" height=\"{totalHeight:0.##}\" rx=\"{rx:0.##}\" ry=\"{rx:0.##}\" fill=\"none\" stroke=\"{chrome.Border}\" stroke-width=\"1\" filter=\"url(#window-shadow)\"/>");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"<rect class=\"{titleClass}\" x=\"{x:0.##}\" y=\"{y:0.##}\" width=\"{width:0.##}\" height=\"{metrics.TitleBarHeight:0.##}\" rx=\"{rx:0.##}\" ry=\"{rx:0.##}\" fill=\"{chrome.TitleBarBg}\"/>");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"<line class=\"{titleClass}\" x1=\"{x:0.##}\" y1=\"{y + metrics.TitleBarHeight:0.##}\" x2=\"{x + width:0.##}\" y2=\"{y + metrics.TitleBarHeight:0.##}\" stroke=\"{chrome.Border}\" stroke-width=\"1\"/>");

        if (window == WindowStyle.Macos)
            AppendMacWindowButtons(sb, titleClass, x, y, metrics, chrome);
        else
            AppendWindowsWindowButtons(sb, titleClass, x, y, width, metrics, chrome);
    }

    private static void AppendMacWindowButtons(
        StringBuilder sb,
        string layerClass,
        double frameX,
        double frameY,
        SvgMetrics metrics,
        WindowChromePalette chrome)
    {
        var diameter = metrics.FontSize * 0.5;
        var radius = diameter / 2d;
        var gap = metrics.FontSize * 0.35;
        var centerY = frameY + metrics.TitleBarHeight / 2d;
        var centerX = frameX + metrics.FontSize * 0.625;
        var colors = new[] { chrome.MacClose, chrome.MacMinimize, chrome.MacMaximize };
        for (var i = 0; i < colors.Length; i++)
        {
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"<circle class=\"{layerClass}\" cx=\"{centerX:0.##}\" cy=\"{centerY:0.##}\" r=\"{radius:0.##}\" fill=\"{colors[i]}\"/>");
            centerX += diameter + gap;
        }
    }

    private static void AppendWindowsWindowButtons(
        StringBuilder sb,
        string layerClass,
        double frameX,
        double frameY,
        double frameWidth,
        SvgMetrics metrics,
        WindowChromePalette chrome)
    {
        var size = metrics.FontSize * 0.6875;
        var gap = metrics.FontSize * 0.375;
        var inset = metrics.FontSize * 0.5;
        var top = frameY + (metrics.TitleBarHeight - size) / 2d;
        var left = frameX + frameWidth - inset - (size * 3) - (gap * 2);
        for (var i = 0; i < 3; i++)
        {
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"<rect class=\"{layerClass}\" x=\"{left:0.##}\" y=\"{top:0.##}\" width=\"{size:0.##}\" height=\"{size:0.##}\" rx=\"{(size * 0.15):0.##}\" ry=\"{(size * 0.15):0.##}\" fill=\"{chrome.WinButton}\"/>");
            left += size + gap;
        }
    }

    private static string BuildEmptySvg(ResolvedRenderSettings render, int width, int height)
    {
        var metrics = CreateMetrics(render.FontSize, width, height, render.Window, render.Theme.Bg);
        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{metrics.SvgWidth:0.##}\" height=\"{metrics.SvgHeight:0.##}\" viewBox=\"0 0 {metrics.SvgWidth:0.##} {metrics.SvgHeight:0.##}\">");
        if (metrics.HasChrome)
        {
            var shadowBlur = (metrics.FontSize * 0.25).ToString("0.##", CultureInfo.InvariantCulture);
            sb.AppendLine("<defs>");
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"<filter id=\"window-shadow\" x=\"-25%\" y=\"-25%\" width=\"150%\" height=\"150%\">" +
                $"<feDropShadow dx=\"0\" dy=\"2\" stdDeviation=\"{shadowBlur}\" flood-opacity=\"0.22\"/></filter>");
            sb.AppendLine("</defs>");
            var chrome = WindowChromeTheme.For(render.Window, WindowChromeTheme.IsLightBackground(render.Theme.Bg));
            AppendWindowChrome(sb, metrics, chrome, render.Window, width, height, 0);
            var bgRect = metrics.TerminalBackgroundRect(width, height);
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"<rect x=\"{bgRect.X:0.##}\" y=\"{bgRect.Y:0.##}\" width=\"{bgRect.Width:0.##}\" height=\"{bgRect.Height:0.##}\" fill=\"{render.Theme.Bg}\"/>");
        }
        else
        {
            var bgRect = metrics.BackgroundRect();
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"<rect x=\"{bgRect.X:0.##}\" y=\"{bgRect.Y:0.##}\" width=\"{bgRect.Width:0.##}\" height=\"{bgRect.Height:0.##}\" fill=\"{render.Theme.Bg}\"/>");
        }

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
                var cellBg = ResolveBackground(cell, theme);
                if (cellBg is null)
                {
                    col++;
                    continue;
                }
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

            var visibleLen = runText.Length;
            while (visibleLen > 0 && runText[visibleLen - 1] == ' ')
                visibleLen--;

            if (visibleLen == 0 && runBg is null)
                continue;

            var drawWidth = (visibleLen > 0 ? visibleLen : runWidth) * metrics.CharWidth;
            var drawText = visibleLen == runText.Length ? runText.ToString() : runText.ToString(0, visibleLen);
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
        if (color.Length != 7 || color[0] != '#'
            || !TryHexNibble(color[1], out var r0) || !TryHexNibble(color[2], out var r1)
            || !TryHexNibble(color[3], out var g0) || !TryHexNibble(color[4], out var g1)
            || !TryHexNibble(color[5], out var b0) || !TryHexNibble(color[6], out var b1))
        {
            return false;
        }

        r = (r0 << 4) | r1;
        g = (g0 << 4) | g1;
        b = (b0 << 4) | b1;
        return true;
    }

    private static bool TryHexNibble(char c, out int value)
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
                var sb = t_escape ??= new StringBuilder(64);
                sb.Clear();
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

    private static List<ReplayFrame> OptimizeFrames(IReadOnlyList<ReplayFrame> frames, int maxFps)
    {
        if (maxFps <= 0)
            return frames is List<ReplayFrame> list ? list : [.. frames];

        if (frames.Count <= 1)
            return frames is List<ReplayFrame> single ? single : [.. frames];

        var normalized = NormalizeTiming(frames, maxFps);
        var reduced = ReduceFrames(normalized, maxFps);
        return SpreadCollapsedFrameTimes(reduced, maxFps);
    }

    private static List<ReplayFrame> NormalizeTiming(IReadOnlyList<ReplayFrame> frames, int maxFps)
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
            if (Math.Abs(quantizedTime - frames[i].Time) < 1e-9)
                normalized.Add(frames[i]);
            else
                normalized.Add(new ReplayFrame(quantizedTime, frames[i].Buffer, frames[i].ViewportWidth, frames[i].ViewportHeight, frames[i].Signature));
            lastTime = quantizedTime;
        }

        return normalized;
    }

    private static List<ReplayFrame> ReduceFrames(IReadOnlyList<ReplayFrame> frames, int maxFps)
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
            if (TryDeferFrame(frame, minimumInterval, ref lastKeptTime, ref lastKeptSignature, ref pending, ref pendingSignature))
                continue;

            if (pending is not null)
            {
                reduced.Add(pending);
                lastKeptTime = pending.Time;
                lastKeptSignature = pendingSignature;
                pending = null;
                if (TryDeferFrame(frame, minimumInterval, ref lastKeptTime, ref lastKeptSignature, ref pending, ref pendingSignature))
                    continue;

                if (frame.Signature == lastKeptSignature)
                    continue;
            }

            reduced.Add(frame);
            lastKeptTime = frame.Time;
            lastKeptSignature = frame.Signature;
        }

        if (pending is not null && !ReferenceEquals(reduced[^1], pending))
            reduced.Add(pending);

        var last = frames[^1];
        if (reduced[^1].Signature != last.Signature || Math.Abs(reduced[^1].Time - last.Time) > 1e-9)
            reduced.Add(last);

        return reduced;
    }

    private static bool TryDeferFrame(
        ReplayFrame frame,
        double minimumInterval,
        ref double lastKeptTime,
        ref ulong lastKeptSignature,
        ref ReplayFrame? pending,
        ref ulong pendingSignature)
    {
        if (frame.Time - lastKeptTime >= minimumInterval)
            return false;

        if (frame.Signature != lastKeptSignature)
        {
            pending = frame;
            pendingSignature = frame.Signature;
        }

        return true;
    }

    private static List<ReplayFrame> SpreadCollapsedFrameTimes(IReadOnlyList<ReplayFrame> frames, int maxFps)
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
        Math.Abs(time - frame.Time) < 1e-9
            ? frame
            : new ReplayFrame(time, frame.Buffer, frame.ViewportWidth, frame.ViewportHeight, frame.Signature);

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

    private static SvgMetrics CreateMetrics(int fontSize, int cols, int rows, WindowStyle window, string bgColor)
    {
        var lineHeight = fontSize * LineHeightFactor;
        var charWidth = fontSize * CharWidthFactor;
        var (innerPaddingH, innerPaddingV) = ResolveInnerPadding(fontSize);
        double outerMargin;
        double titleBarHeight;
        double cornerRadius;
        if (window == WindowStyle.None)
        {
            outerMargin = Padding;
            titleBarHeight = 0;
            cornerRadius = 0;
        }
        else
        {
            outerMargin = Math.Max(4, fontSize * 0.375);
            titleBarHeight = fontSize * 1.75;
            cornerRadius = window == WindowStyle.Macos ? fontSize * 0.5 : fontSize * 0.25;
        }

        var contentOriginX = outerMargin + innerPaddingH;
        var contentOriginY = outerMargin + titleBarHeight + innerPaddingV;
        var contentWidth = cols * charWidth + innerPaddingH * 2;
        var contentHeight = rows * lineHeight + innerPaddingV * 2;
        var svgWidth = contentWidth + outerMargin * 2;
        var svgHeight = titleBarHeight + contentHeight + outerMargin * 2;

        return new SvgMetrics(
            charWidth,
            lineHeight,
            fontSize * 0.8,
            fontSize,
            innerPaddingH,
            innerPaddingV,
            contentOriginX,
            contentOriginY,
            contentWidth,
            contentHeight,
            svgWidth,
            svgHeight,
            window,
            outerMargin,
            titleBarHeight,
            cornerRadius);
    }

    private static (double horizontal, double vertical) ResolveInnerPadding(int fontSize)
    {
        var horizontal = Math.Clamp(fontSize * 10.0 / 16.0, InnerPaddingHorizontalMin, InnerPaddingHorizontalMax);
        var vertical = Math.Clamp(fontSize * 4.0 / 16.0, InnerPaddingVerticalMin, InnerPaddingVerticalMax);
        return (horizontal, vertical);
    }

    private readonly record struct SvgMetrics(
        double CharWidth,
        double LineHeight,
        double BaselineOffset,
        double FontSize,
        double InnerPaddingH,
        double InnerPaddingV,
        double ContentOriginX,
        double ContentOriginY,
        double ContentWidth,
        double ContentHeight,
        double SvgWidth,
        double SvgHeight,
        WindowStyle Window,
        double OuterMargin,
        double TitleBarHeight,
        double CornerRadius)
    {
        internal bool HasChrome => Window != WindowStyle.None;
        internal double TerminalOriginX => OuterMargin;
        internal double TerminalOriginY => OuterMargin + TitleBarHeight;
        internal double FrameOriginY => OuterMargin;
        internal (double X, double Y) ContentOrigin => (ContentOriginX, ContentOriginY);
        internal double ViewportPixelWidth(int cols) => cols * CharWidth + InnerPaddingH * 2;
        internal double ViewportPixelHeight(int rows) => rows * LineHeight + InnerPaddingV * 2;
        internal (double X, double Y, double Width, double Height) BackgroundRect() =>
            (OuterMargin, OuterMargin, SvgWidth - OuterMargin * 2, SvgHeight - OuterMargin * 2);
        internal (double X, double Y, double Width, double Height) TerminalBackgroundRect(int cols, int rows) =>
            (TerminalOriginX, TerminalOriginY, ViewportPixelWidth(cols), ViewportPixelHeight(rows));
    }

    private readonly struct LayerSet(List<AnimLayer> rows, List<AnimLayer> cursors, List<AnimLayer> viewports)
    {
        internal List<AnimLayer> Rows { get; } = rows;
        internal List<AnimLayer> Cursors { get; } = cursors;
        internal List<AnimLayer> Viewports { get; } = viewports;
    }

    private sealed class AnimLayer(double show, int p0, int p1 = 0, ScreenBuffer? buffer = null)
    {
        internal double Show { get; } = show;
        internal double? Hide { get; set; }
        internal int P0 { get; } = p0;
        internal int P1 { get; } = p1;
        internal ScreenBuffer? Buffer { get; } = buffer;
    }
}

