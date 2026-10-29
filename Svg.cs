using System.Globalization;
using System.Text;

internal static class SvgRender
{
    internal static void WriteSvg(
        IReadOnlyList<CastEvent> events,
        int width,
        int height,
        ResolvedRenderSettings render,
        string outputPath,
        double? loopDurationOverride = null)
    {
        // Render fully before opening the file, so a render failure leaves no file behind.
        var svg = Render(events, width, height, render, loopDurationOverride);
        using var stream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 1 << 16);
        svg.WriteTo(stream);
    }

    /// <summary>
    /// Above this size an SVG is hard to share: GitHub rejects image uploads over 10MB, and
    /// browsers slow down on that many layers. Heavy output belongs in mp4 instead.
    /// </summary>
    internal const long LargeSvgBytes = 10_000_000;

    /// <summary>
    /// The warning to print for an SVG of <paramref name="bytes"/>, or null when it is small enough.
    /// <paramref name="fromCast"/> picks the mp4 suggestion: the <c>mp4</c> subcommand for casts,
    /// <c>--format mp4</c> on the scenario path.
    /// </summary>
    internal static string? SizeWarning(long bytes, int maxFps, bool fromCast)
    {
        if (bytes <= LargeSvgBytes)
            return null;

        var size = (bytes / 1_000_000.0).ToString("0.0", CultureInfo.InvariantCulture); // decimal MB, as in the mp4 log
        var mp4 = fromCast ? "scenetake mp4 <cast>" : "--format mp4";
        var sampling = maxFps == 0 ? "--max-fps 20" : "a lower --max-fps";
        return $"SVG is {size}MB (over 10MB). Heavy output such as full-screen TUIs is much smaller as mp4 ({mp4}) or as SVG with {sampling}";
    }

    /// <summary>Renders the SVG as a string. <see cref="WriteSvg"/> streams to disk instead.</summary>
    internal static string RenderToString(
        IReadOnlyList<CastEvent> events,
        int width,
        int height,
        ResolvedRenderSettings render,
        double? loopDurationOverride = null)
    {
        using var stream = new MemoryStream();
        Render(events, width, height, render, loopDurationOverride).WriteTo(stream);
        return SvgDocument.Utf8NoBom.GetString(stream.GetBuffer(), 0, (int)stream.Length);
    }

    private static SvgDocument Render(
        IReadOnlyList<CastEvent> events,
        int width,
        int height,
        ResolvedRenderSettings render,
        double? loopDurationOverride)
    {
        var (canvasWidth, canvasHeight) = TerminalReplay.ResolveCanvasSize(width, height, events);
        var theme = TerminalTheme.FromResolved(render.Theme);
        var frames = TerminalReplay.BuildFrames(events, width, height, canvasWidth, canvasHeight, theme);
        var loopDuration = loopDurationOverride ?? TerminalReplay.ComputeLoopDuration(events);
        return SvgFrameRenderer.Render(events, width, height, frames, render, canvasWidth, canvasHeight, loopDuration);
    }
}

/// <summary>
/// A rendered SVG: head and tail text around the row layers, which are already encoded
/// as UTF-8. Keeping row layers encoded holds memory near the output size.
/// </summary>
internal sealed class SvgDocument(string head, Utf8ChunkBuffer? rowLayers, string tail)
{
    internal static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    internal static SvgDocument FromText(string text) => new(text, null, "");

    internal void WriteTo(Stream stream)
    {
        WriteText(stream, head);
        rowLayers?.WriteTo(stream);
        WriteText(stream, tail);
    }

    private static void WriteText(Stream stream, string text)
    {
        if (text.Length != 0)
            stream.Write(Utf8NoBom.GetBytes(text));
    }
}

/// <summary>Append-only UTF-8 text in 1MB chunks, so a large SVG body never needs one contiguous buffer.</summary>
internal sealed class Utf8ChunkBuffer
{
    private const int ChunkSize = 1 << 20;
    private readonly List<(byte[] Bytes, int Length)> _filled = [];
    private byte[] _current = [];
    private int _used;
    private char[] _chars = new char[4096];

    /// <summary>
    /// Appends <paramref name="text"/>. Each append is encoded on its own, so it must not
    /// end in the middle of a surrogate pair.
    /// </summary>
    internal void Append(StringBuilder text)
    {
        var length = text.Length;
        if (_chars.Length < length)
            _chars = new char[Math.Max(length, _chars.Length * 2)];

        text.CopyTo(0, _chars, 0, length);
        var chars = _chars.AsSpan(0, length);
        var byteCount = SvgDocument.Utf8NoBom.GetByteCount(chars);
        if (_current.Length - _used < byteCount)
        {
            if (_used > 0)
                _filled.Add((_current, _used));
            _current = new byte[Math.Max(ChunkSize, byteCount)];
            _used = 0;
        }

        _used += SvgDocument.Utf8NoBom.GetBytes(chars, _current.AsSpan(_used));
    }

    internal void WriteTo(Stream stream)
    {
        foreach (var (bytes, length) in _filled)
            stream.Write(bytes, 0, length);
        if (_used > 0)
            stream.Write(_current, 0, _used);
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
    internal const string FontSizeTagPrefix = "st:font-size=";
    internal const string FontFamilyTagPrefix = "st:font-family=";
    internal const string WindowTagPrefix = "st:window=";
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

    internal static string ResolveCastMp4OutputPath(string castPath, string? outputArg) =>
        ResolveOutputPath(castPath, outputArg, ".mp4");

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

internal static class SvgFrameRenderer
{
    // Cell metrics in units of font-size. The mp4 rasterizer rounds the same values to whole pixels.
    internal const double LineHeightFactor = 1.25;
    internal const double CharWidthFactor = 0.62;
    internal const double BaselineFactor = 0.8;
    private const double Padding = 8.0;
    private const double InnerPaddingHorizontalMin = 4.0;
    private const double InnerPaddingHorizontalMax = 16.0;
    private const double InnerPaddingVerticalMin = 2.0;
    private const double InnerPaddingVerticalMax = 8.0;
    private const double LayerFadeSeconds = 0.001;
    private const double CursorBlockOpacity = 0.5;
    private const string Space = " ";

    // Window chrome — fixed px; does not scale with terminal font-size
    internal const double ChromeTitleBarHeight = 34.0;
    internal const double ChromeSidePadding = 14.0;
    internal const double ChromeTopPadding = 9.0;
    private const double ChromeOuterMargin = 12.0;
    private const double ChromeMacCornerRadius = 8.0;
    private const double ChromeWindowsCornerRadius = 4.0;
    internal const double MacButtonDiameter = 16.0;
    internal const double MacButtonGap = 10.0;
    internal const double WinButtonSize = 17.0;
    internal const double WinButtonGap = 5.5;
    internal const double WinButtonCornerRadius = 2.55;
    private const string WindowShadowFilter =
        "<filter id=\"window-shadow\" x=\"-25%\" y=\"-25%\" width=\"150%\" height=\"150%\"><feDropShadow dx=\"0\" dy=\"2\" stdDeviation=\"4\" flood-opacity=\"0.22\"/></filter>";

    private static readonly ChromePalette MacDark = new("#323232", "#1a1a1a", "#8a8a8a");
    private static readonly ChromePalette MacLight = new("#dcdcdc", "#b0b0b0", "#8a8a8a");
    private static readonly ChromePalette WinDark = new("#2d2d2d", "#404040", "#8a8a8a");
    private static readonly ChromePalette WinLight = new("#f3f3f3", "#d0d0d0", "#8a8a8a");

    internal readonly record struct ChromePalette(string TitleBarBg, string Border, string WinButton);

    /// <summary>macOS close, minimize, and zoom buttons, left to right.</summary>
    internal static readonly string[] MacButtonColors = ["#ff5f57", "#febc2e", "#28c840"];

    [ThreadStatic] private static StringBuilder? t_runText;
    [ThreadStatic] private static StringBuilder? t_runForm;
    [ThreadStatic] private static StringBuilder? t_listForm;
    [ThreadStatic] private static List<GlyphGroup>? t_glyphGroups;
    [ThreadStatic] private static StringBuilder? t_escape;

    /// <summary>
    /// Renders <paramref name="frames"/> (from <see cref="TerminalReplay.BuildFrames"/> with the
    /// same events and sizes). Frames carry no screen contents, so this replays
    /// <paramref name="events"/> once more and encodes each changed row as it goes.
    /// </summary>
    internal static SvgDocument Render(
        IReadOnlyList<CastEvent> events,
        int initialWidth,
        int initialHeight,
        IReadOnlyList<ReplayFrame> frames,
        ResolvedRenderSettings render,
        int canvasWidth,
        int canvasHeight,
        double loopDuration)
    {
        if (frames.Count == 0)
            return SvgDocument.FromText(BuildEmptySvg(render, canvasWidth, canvasHeight));

        frames = OptimizeFrames(frames, render.MaxFps);
        var theme = TerminalTheme.FromResolved(render.Theme);
        var metrics = CreateMetrics(render.FontSize, canvasWidth, canvasHeight, render.Window);

        var rowLayers = new Utf8ChunkBuffer();
        var layers = BuildLayers(events, initialWidth, initialHeight, frames, canvasWidth, canvasHeight, theme, metrics, rowLayers);
        loopDuration = ResolveLoopDuration(loopDuration, layers);

        var (head, tail) = BuildLayeredSvg(layers, metrics, render, theme, canvasWidth, canvasHeight, loopDuration);
        return new SvgDocument(head, rowLayers, tail);
    }

    private static double ResolveLoopDuration(double loopDuration, LayerSet layers)
    {
        if (loopDuration > 0)
            return loopDuration;

        var maxTime = 0.0;
        foreach (var layer in EnumerateLayers(layers))
        {
            maxTime = Math.Max(maxTime, layer.Show);
            if (layer.Hide is double hide)
                maxTime = Math.Max(maxTime, hide);
        }

        return Math.Max(maxTime + LayerFadeSeconds, LayerFadeSeconds);
    }

    private static IEnumerable<AnimLayer> EnumerateLayers(LayerSet layers)
    {
        foreach (var layer in layers.Rows)
            yield return layer;
        foreach (var layer in layers.Cursors)
            yield return layer;
        foreach (var layer in layers.Viewports)
            yield return layer;
    }

    /// <summary>
    /// Replays <paramref name="events"/> and builds row, cursor, and viewport layers for the
    /// selected <paramref name="frames"/>. Each new row layer is encoded into
    /// <paramref name="rowLayers"/> immediately. Comparisons use a copy of each row's active
    /// layer and of the previous frame, so memory is two screens rather than one per frame.
    /// </summary>
    private static LayerSet BuildLayers(
        IReadOnlyList<CastEvent> events,
        int initialWidth,
        int initialHeight,
        IReadOnlyList<ReplayFrame> frames,
        int canvasWidth,
        int canvasHeight,
        TerminalTheme theme,
        SvgMetrics metrics,
        Utf8ChunkBuffer rowLayers)
    {
        var rows = new List<AnimLayer>();
        var cursors = new List<AnimLayer>();
        var viewports = new List<AnimLayer>();
        var activeByRow = new AnimLayer?[canvasHeight];
        var activeCells = new ScreenCell[canvasHeight][];
        var previousCells = new ScreenCell[canvasHeight][];
        for (var row = 0; row < canvasHeight; row++)
        {
            activeCells[row] = new ScreenCell[canvasWidth];
            previousCells[row] = new ScreenCell[canvasWidth];
        }

        var hasPrevious = false;
        AnimLayer? cursorActive = null;
        AnimLayer? viewportActive = null;
        var fragment = new StringBuilder(4096);
        var origin = metrics.ContentOrigin;
        var next = 0;

        foreach (var capture in TerminalReplay.EnumerateCaptures(events, initialWidth, initialHeight, canvasWidth, canvasHeight, theme))
        {
            if (capture.Ordinal != frames[next].Ordinal)
                continue;

            var frame = frames[next++];
            var time = frame.Time;
            var buffer = capture.View;

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

            // Depends only on the previous and current frame, so compute it at most once per frame.
            bool? likelyFullClear = null;
            for (var row = 0; row < canvasHeight; row++)
            {
                var active = activeByRow[row];
                if (active is not null && buffer.RowEquals(row, activeCells[row]))
                    continue;

                if (active is null && hasPrevious && buffer.RowEquals(row, previousCells[row]))
                    continue;

                if (buffer.IsRowBlank(row))
                {
                    if (active is null || ScreenBuffer.IsRowBlank(activeCells[row], buffer.DefaultStyle))
                        continue;

                    RetireRowLayer(activeByRow, row, time);
                    likelyFullClear ??= hasPrevious && IsLikelyFullClear(previousCells, buffer);
                    if (!likelyFullClear.Value)
                        continue;
                }
                else
                {
                    HideRowLayer(activeByRow, row, time);
                }

                fragment.Clear();
                fragment.Append(CultureInfo.InvariantCulture, $"<g class=\"layer layer-{rows.Count}\">");
                AppendRow(fragment, buffer, row, origin.X, origin.Y, metrics, theme);
                fragment.AppendLine("</g>");
                rowLayers.Append(fragment);

                var layer = new AnimLayer(time, row);
                rows.Add(layer);
                activeByRow[row] = layer;
                buffer.CopyRowTo(row, activeCells[row]);
            }

            for (var row = 0; row < canvasHeight; row++)
                buffer.CopyRowTo(row, previousCells[row]);
            hasPrevious = true;

            if (next == frames.Count)
                break;
        }

        if (next != frames.Count)
            throw new InvalidOperationException($"SVG replay reached {next} of {frames.Count} frames.");

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

    private static bool IsLikelyFullClear(ScreenCell[][] previous, ScreenBuffer current)
    {
        var changedToBlank = 0;
        for (var row = 0; row < current.Height; row++)
        {
            if (!current.RowEquals(row, previous[row]) && current.IsRowBlank(row))
                changedToBlank++;
        }

        return changedToBlank >= Math.Max(1, current.Height / 2);
    }

    /// <summary>Builds the text before and after the row layers, which <see cref="BuildLayers"/> has already encoded.</summary>
    private static (string Head, string Tail) BuildLayeredSvg(
        LayerSet layers,
        SvgMetrics metrics,
        ResolvedRenderSettings render,
        TerminalTheme theme,
        int canvasWidth,
        int canvasHeight,
        double loopDuration)
    {
        var rowLayers = layers.Rows;
        var cursorLayers = layers.Cursors;
        var viewportLayers = layers.Viewports;
        var loopDurationText = loopDuration.ToString("0.######", CultureInfo.InvariantCulture);
        var cursorOpacityText = CursorBlockOpacity.ToString("0.######", CultureInfo.InvariantCulture);

        if (viewportLayers.Count == 0)
            viewportLayers = [new AnimLayer(0, canvasWidth, canvasHeight)];

        var sb = new StringBuilder(64 * 1024);
        sb.AppendLine(CultureInfo.InvariantCulture, $"<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{metrics.SvgWidth:0.##}\" height=\"{metrics.SvgHeight:0.##}\" viewBox=\"0 0 {metrics.SvgWidth:0.##} {metrics.SvgHeight:0.##}\" preserveAspectRatio=\"xMidYMid meet\" role=\"img\" aria-label=\"scenetake output\">");

        sb.AppendLine("<style>");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"text {{ font-family: {render.FontFamily}; font-size: {render.FontSize}px; white-space: pre; }}");
        sb.AppendLine("text { dominant-baseline: alphabetic; }");
        // Row-layer background rects carry no class; this selector gives them crisp edges.
        sb.AppendLine(".bg,g.layer>rect:not([class]) { shape-rendering: crispEdges; }");
        sb.AppendLine(".layer { opacity: 0; }");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $".cursor-block {{ fill: {theme.Foreground}; fill-opacity: {cursorOpacityText}; }}");

        var keyframes = new LoopKeyframeRegistry();
        var layerRules = new List<(string Selector, string Keyframe)>();

        // Row layers that share a keyframe share one rule: ".layer-1,.layer-5 { animation:… }".
        // Full-screen redraws make thousands of row layers, so one rule each was a large share of the style.
        var rowSelectors = new Dictionary<string, StringBuilder>(StringComparer.Ordinal);
        for (var i = 0; i < rowLayers.Count; i++)
        {
            var keyframe = keyframes.Register(rowLayers[i].Show, rowLayers[i].Hide, loopDuration);
            if (rowSelectors.TryGetValue(keyframe, out var selectors))
            {
                selectors.Append(",.layer-").Append(i.ToString(CultureInfo.InvariantCulture));
            }
            else
            {
                rowSelectors[keyframe] = new StringBuilder(".layer-").Append(i.ToString(CultureInfo.InvariantCulture));
                layerRules.Add((keyframe, keyframe)); // placeholder, resolved below in first-use order
            }
        }

        for (var i = 0; i < layerRules.Count; i++)
            layerRules[i] = (rowSelectors[layerRules[i].Keyframe].ToString(), layerRules[i].Keyframe);

        for (var i = 0; i < cursorLayers.Count; i++)
        {
            var keyframe = keyframes.Register(cursorLayers[i].Show, cursorLayers[i].Hide, loopDuration);
            layerRules.Add(($".cursor-layer-{i}", keyframe));
        }

        for (var i = 0; i < viewportLayers.Count; i++)
        {
            var viewportSelector = metrics.HasChrome
                ? $".viewport-mask-{i},.viewport-bg-{i},.viewport-chrome-{i}"
                : $".viewport-mask-{i},.viewport-bg-{i}";
            var keyframe = keyframes.Register(viewportLayers[i].Show, viewportLayers[i].Hide, loopDuration);
            layerRules.Add((viewportSelector, keyframe));
        }

        keyframes.AppendAll(sb);
        foreach (var (selector, keyframe) in layerRules)
            AppendLayerCycleStyle(sb, selector, keyframe, loopDurationText);

        sb.AppendLine("</style>");

        sb.AppendLine("<defs>");
        if (metrics.HasChrome)
            sb.AppendLine(WindowShadowFilter);

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
            var chrome = ResolveChromePalette(render.Window, render.Theme.Bg);
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

        var head = sb.ToString();
        sb.Clear();

        var origin = metrics.ContentOrigin;
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
        return (head, sb.ToString());
    }

    private static void AppendLayerCycleStyle(StringBuilder sb, string selector, string keyframeName, string loopDurationText)
    {
        sb.Append(selector);
        sb.Append(" { animation:");
        sb.Append(keyframeName);
        sb.Append(' ');
        sb.Append(loopDurationText);
        sb.AppendLine("s linear infinite; }");
    }

    private sealed class LoopKeyframeRegistry
    {
        private readonly Dictionary<string, string> _namesByBody = new(StringComparer.Ordinal);
        private readonly List<(string Name, string Body)> _ordered = [];

        private int _nextId;

        internal string Register(double showTime, double? hideTime, double loopDuration)
        {
            var body = BuildCompactCycleKeyframesBody(showTime, hideTime, loopDuration);
            if (_namesByBody.TryGetValue(body, out var existing))
                return existing;

            var name = $"k{_nextId++}";
            _namesByBody[body] = name;
            _ordered.Add((name, body));
            return name;
        }

        internal void AppendAll(StringBuilder sb)
        {
            foreach (var (name, body) in _ordered)
            {
                sb.Append("@keyframes ");
                sb.Append(name);
                sb.Append(" { ");
                sb.Append(body);
                sb.AppendLine(" }");
            }
        }
    }

    private static string BuildCompactCycleKeyframesBody(double showTime, double? hideTime, double loopDuration)
    {
        var sb = new StringBuilder(96);
        if (showTime <= 0)
        {
            if (hideTime is double hide)
            {
                AppendOpacityRange(sb, 0, hide, loopDuration, 1);
                AppendOpacityRange(sb, GapAfter(hide, loopDuration), loopDuration, loopDuration, 0);
                return sb.ToString();
            }

            AppendOpacityRange(sb, 0, loopDuration, loopDuration, 1);
            return sb.ToString();
        }

        AppendOpacityRange(sb, 0, GapBefore(showTime, loopDuration), loopDuration, 0);
        if (hideTime is double hideTimeValue)
        {
            AppendOpacityRange(sb, showTime, hideTimeValue, loopDuration, 1);
            AppendOpacityRange(sb, GapAfter(hideTimeValue, loopDuration), loopDuration, loopDuration, 0);
            return sb.ToString();
        }

        AppendOpacityRange(sb, showTime, loopDuration, loopDuration, 1);
        return sb.ToString();
    }

    private static double GapBefore(double time, double loopDuration) =>
        Math.Max(0, time - (LayerFadeSeconds * 0.5));

    private static double GapAfter(double time, double loopDuration) =>
        Math.Min(loopDuration, time + LayerFadeSeconds);

    private static void AppendOpacityRange(
        StringBuilder sb,
        double startTime,
        double endTime,
        double loopDuration,
        int opacity)
    {
        if (sb.Length > 0)
            sb.Append(' ');

        sb.Append(CultureInfo.InvariantCulture, $"{FormatCyclePercent(startTime, loopDuration)}%,");
        sb.Append(CultureInfo.InvariantCulture, $"{FormatCyclePercent(endTime, loopDuration)}%");
        sb.Append(CultureInfo.InvariantCulture, $"{{opacity:{opacity};}}");
    }

    private static string FormatCyclePercent(double time, double loopDuration)
    {
        if (loopDuration <= 0)
            return "0";

        return (time / loopDuration * 100.0).ToString("0.######", CultureInfo.InvariantCulture);
    }

    internal static ChromePalette ResolveChromePalette(WindowStyle window, string terminalBg)
    {
        var light = IsLightTerminalBg(terminalBg);
        return window switch
        {
            WindowStyle.Macos => light ? MacLight : MacDark,
            WindowStyle.Windows => light ? WinLight : WinDark,
            _ => MacDark,
        };
    }

    private static bool IsLightTerminalBg(string bg)
    {
        if (!CellColors.TryParseHexColor(bg, out var r, out var g, out var b))
            return false;

        return (0.299 * r) + (0.587 * g) + (0.114 * b) > 140;
    }

    private static void AppendWindowChrome(
        StringBuilder sb,
        SvgMetrics metrics,
        ChromePalette chrome,
        WindowStyle window,
        int cols,
        int rows,
        int layerIndex)
    {
        var x = metrics.OuterMargin;
        var y = metrics.OuterMargin;
        var width = metrics.ViewportPixelWidth(cols);
        var terminalHeight = metrics.ViewportPixelHeight(rows);
        var barH = metrics.TitleBarHeight;
        var totalHeight = barH + terminalHeight;
        var rx = metrics.CornerRadius;
        var right = x + width;
        var barBottom = y + barH;
        var cls = $"layer viewport-chrome-{layerIndex}";

        sb.AppendLine(CultureInfo.InvariantCulture,
            $"<rect class=\"{cls}\" x=\"{x:0.##}\" y=\"{y:0.##}\" width=\"{width:0.##}\" height=\"{totalHeight:0.##}\" rx=\"{rx:0.##}\" ry=\"{rx:0.##}\" fill=\"none\" stroke=\"{chrome.Border}\" stroke-width=\"1\" filter=\"url(#window-shadow)\"/>");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"<path class=\"{cls}\" d=\"M {x + rx:0.##},{y:0.##} H {right - rx:0.##} Q {right:0.##},{y:0.##} {right:0.##},{y + rx:0.##} V {barBottom:0.##} H {x:0.##} V {y + rx:0.##} Q {x:0.##},{y:0.##} {x + rx:0.##},{y:0.##} Z\" fill=\"{chrome.TitleBarBg}\"/>");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"<line class=\"{cls}\" x1=\"{x:0.##}\" y1=\"{barBottom:0.##}\" x2=\"{right:0.##}\" y2=\"{barBottom:0.##}\" stroke=\"{chrome.Border}\" stroke-width=\"1\"/>");

        if (window == WindowStyle.Macos)
        {
            var radius = MacButtonDiameter / 2d;
            var cx = x + ChromeSidePadding + radius;
            var cy = y + ChromeTopPadding + radius;
            foreach (var color in MacButtonColors)
            {
                sb.AppendLine(CultureInfo.InvariantCulture,
                    $"<circle class=\"{cls}\" cx=\"{cx:0.##}\" cy=\"{cy:0.##}\" r=\"{radius:0.##}\" fill=\"{color}\"/>");
                cx += MacButtonDiameter + MacButtonGap;
            }

            return;
        }

        var size = WinButtonSize;
        var gap = WinButtonGap;
        var top = y + ChromeTopPadding;
        var left = right - ChromeSidePadding - (size * 3) - (gap * 2);
        for (var i = 0; i < 3; i++)
        {
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"<rect class=\"{cls}\" x=\"{left:0.##}\" y=\"{top:0.##}\" width=\"{size:0.##}\" height=\"{size:0.##}\" rx=\"{WinButtonCornerRadius:0.##}\" ry=\"{WinButtonCornerRadius:0.##}\" fill=\"{chrome.WinButton}\"/>");
            left += size + gap;
        }
    }

    private static string BuildEmptySvg(ResolvedRenderSettings render, int width, int height)
    {
        var metrics = CreateMetrics(render.FontSize, width, height, render.Window);
        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{metrics.SvgWidth:0.##}\" height=\"{metrics.SvgHeight:0.##}\" viewBox=\"0 0 {metrics.SvgWidth:0.##} {metrics.SvgHeight:0.##}\">");
        if (metrics.HasChrome)
        {
            sb.AppendLine("<defs>");
            sb.AppendLine(WindowShadowFilter);
            sb.AppendLine("</defs>");
            var chrome = ResolveChromePalette(render.Window, render.Theme.Bg);
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

    /// <summary>
    /// Appends one row on a single line: background spans first, then the row's text in
    /// whichever encoding is shorter for it.
    /// <list type="bullet">
    /// <item>Run form: one &lt;text&gt; per style run, spread with <c>textLength</c>. Small for prose.</item>
    /// <item>Glyph-list form: one &lt;text&gt; per style with an <c>x</c> per glyph and no spaces.
    /// Small for sparse colored output such as matrix rain.</item>
    /// </list>
    /// Rows with underlined cells always use the run form, because the underline runs under the spaces.
    /// </summary>
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
        var hasUnderline = AppendBackgroundSpans(sb, buffer, row, originX, rowY, metrics, theme);

        var runForm = t_runForm ??= new StringBuilder(1024);
        runForm.Clear();
        AppendRunText(runForm, buffer, row, originX, textY, metrics, theme);
        if (hasUnderline)
        {
            sb.Append(runForm);
            return;
        }

        var listForm = t_listForm ??= new StringBuilder(1024);
        listForm.Clear();
        AppendGlyphListText(listForm, buffer, row, originX, textY, metrics, theme);
        sb.Append(listForm.Length < runForm.Length ? listForm : runForm);
    }

    /// <summary>
    /// One rect per run of cells with the same non-theme background, regardless of foreground
    /// or style. A wide cell's continuation takes the wide cell's background. Returns whether
    /// any cell in the row is underlined.
    /// </summary>
    private static bool AppendBackgroundSpans(
        StringBuilder sb, ScreenBuffer buffer, int row, double originX, double rowY, SvgMetrics metrics, TerminalTheme theme)
    {
        var hasUnderline = false;
        string? spanBg = null;
        string? previousBg = null;
        var spanStart = 0;
        for (var col = 0; col <= buffer.Width; col++)
        {
            string? bg = null;
            if (col < buffer.Width)
            {
                var cell = buffer.GetCell(row, col);
                hasUnderline |= cell.Underline;
                bg = cell.IsWideContinuation ? previousBg : CellColors.ResolveBackground(cell, theme);
            }

            if (!string.Equals(bg, spanBg, StringComparison.Ordinal))
            {
                if (spanBg is not null)
                {
                    sb.Append(CultureInfo.InvariantCulture,
                        $"<rect x=\"{originX + spanStart * metrics.CharWidth:0.##}\" y=\"{rowY:0.##}\" width=\"{(col - spanStart) * metrics.CharWidth:0.##}\" height=\"{metrics.LineHeight:0.##}\" fill=\"{spanBg}\"/>");
                }

                spanBg = bg;
                spanStart = col;
            }

            previousBg = bg;
        }

        return hasUnderline;
    }

    /// <summary>Run form: the original encoding (runs break on any color or style change), without background rects.</summary>
    private static void AppendRunText(
        StringBuilder sb, ScreenBuffer buffer, int row, double originX, double textY, SvgMetrics metrics, TerminalTheme theme)
    {
        var col = 0;
        while (col < buffer.Width)
        {
            var cell = buffer.GetCell(row, col);
            if (cell.IsWideContinuation)
            {
                col++;
                continue;
            }

            if (cell.Text == Space && CellColors.ResolveBackground(cell, theme) is null)
            {
                col++;
                continue;
            }

            var runStart = col;
            var runFg = CellColors.ResolveTintedForeground(buffer, row, col, cell, theme);
            var runBg = CellColors.ResolveBackground(cell, theme);
            var runBold = cell.Bold;
            var runItalic = cell.Italic;
            var runUnderline = cell.Underline;

            if (IsBlockElement(cell.Text))
            {
                AppendBlockElement(sb, originX + runStart * metrics.CharWidth, textY, runFg, runBold, cell.Text, metrics);
                col = runStart + (cell.IsWide ? 2 : 1);
                continue;
            }

            var runText = (t_runText ??= new StringBuilder(64));
            runText.Clear();
            runText.Append(cell.Text);
            col++;

            while (col < buffer.Width)
            {
                var next = buffer.GetCell(row, col);
                if (next.IsWideContinuation)
                    break;

                if (IsBlockElement(next.Text))
                    break;

                var nextFg = CellColors.ResolveTintedForeground(buffer, row, col, next, theme);
                var nextBg = CellColors.ResolveBackground(next, theme);
                if (nextFg != runFg || nextBg != runBg || next.Bold != runBold
                    || next.Italic != runItalic || next.Underline != runUnderline || next.IsWide)
                {
                    break;
                }

                runText.Append(next.Text);
                col++;
            }

            var visibleLen = runText.Length;
            while (visibleLen > 0 && runText[visibleLen - 1] == ' ')
                visibleLen--;

            if (visibleLen == 0)
                continue; // only spaces: the background span already covers them

            var drawText = visibleLen == runText.Length ? runText.ToString() : runText.ToString(0, visibleLen);
            sb.Append(CultureInfo.InvariantCulture,
                $"<text x=\"{originX + runStart * metrics.CharWidth:0.##}\" y=\"{textY:0.##}\" fill=\"{runFg}\"");
            AppendFontStyle(sb, runBold, runItalic, runUnderline);
            sb.Append(CultureInfo.InvariantCulture, $" textLength=\"{visibleLen * metrics.CharWidth:0.##}\">{EscapeXml(drawText)}</text>");
        }
    }

    /// <summary>
    /// Glyph-list form: one &lt;text&gt; per (fg, bold, italic) with an x per glyph, spaces omitted.
    /// Block elements keep their stretched single-glyph encoding, and cells of more than one
    /// UTF-16 unit (surrogate pairs, combining marks) get their own &lt;text&gt;: SVG assigns x
    /// values per addressable character, which browsers count differently for those.
    /// </summary>
    private static void AppendGlyphListText(
        StringBuilder sb, ScreenBuffer buffer, int row, double originX, double textY, SvgMetrics metrics, TerminalTheme theme)
    {
        var groups = t_glyphGroups ??= [];
        var used = 0;
        for (var col = 0; col < buffer.Width; col++)
        {
            var cell = buffer.GetCell(row, col);
            var text = cell.Text;
            if (cell.IsWideContinuation || text.Length == 0 || (text.Length == 1 && char.IsWhiteSpace(text[0])))
                continue;

            var fg = CellColors.ResolveTintedForeground(buffer, row, col, cell, theme);
            var x = originX + col * metrics.CharWidth;
            if (IsBlockElement(text))
            {
                AppendBlockElement(sb, x, textY, fg, cell.Bold, text, metrics);
                continue;
            }

            if (text.Length != 1)
            {
                sb.Append(CultureInfo.InvariantCulture, $"<text x=\"{x:0.##}\" y=\"{textY:0.##}\" fill=\"{fg}\"");
                AppendFontStyle(sb, cell.Bold, cell.Italic, underline: false);
                sb.Append('>').Append(EscapeXml(text)).Append("</text>");
                continue;
            }

            GlyphGroup? group = null;
            for (var i = 0; i < used; i++)
            {
                var candidate = groups[i];
                if (candidate.Bold == cell.Bold && candidate.Italic == cell.Italic && string.Equals(candidate.Fg, fg, StringComparison.Ordinal))
                {
                    group = candidate;
                    break;
                }
            }

            if (group is null)
            {
                if (used == groups.Count)
                    groups.Add(new GlyphGroup());
                group = groups[used++];
                group.Reset(fg, cell.Bold, cell.Italic);
            }
            else
            {
                group.Xs.Append(' ');
            }

            group.Xs.Append(x.ToString("0.##", CultureInfo.InvariantCulture));
            group.Text.Append(EscapeXml(text));
        }

        for (var i = 0; i < used; i++)
        {
            var group = groups[i];
            sb.Append("<text x=\"").Append(group.Xs).Append(CultureInfo.InvariantCulture, $"\" y=\"{textY:0.##}\" fill=\"{group.Fg}\"");
            AppendFontStyle(sb, group.Bold, group.Italic, underline: false);
            sb.Append('>').Append(group.Text).Append("</text>");
        }
    }

    /// <summary>Block elements (U+2580–U+259F) are stretched to exactly one cell so they align with box drawing.</summary>
    private static void AppendBlockElement(StringBuilder sb, double x, double textY, string fg, bool bold, string text, SvgMetrics metrics)
    {
        sb.Append(CultureInfo.InvariantCulture, $"<text x=\"{x:0.##}\" y=\"{textY:0.##}\" fill=\"{fg}\"");
        AppendFontStyle(sb, bold, italic: false, underline: false);
        sb.Append(CultureInfo.InvariantCulture, $" textLength=\"{metrics.CharWidth:0.##}\" lengthAdjust=\"spacingAndGlyphs\">{EscapeXml(text)}</text>");
    }

    // Presentation attributes; normal weight, upright style, and no decoration are the defaults.
    private static void AppendFontStyle(StringBuilder sb, bool bold, bool italic, bool underline)
    {
        if (bold)
            sb.Append(" font-weight=\"bold\"");
        if (italic)
            sb.Append(" font-style=\"italic\"");
        if (underline)
            sb.Append(" text-decoration=\"underline\"");
    }

    private sealed class GlyphGroup
    {
        internal string Fg { get; private set; } = "";
        internal bool Bold { get; private set; }
        internal bool Italic { get; private set; }
        internal StringBuilder Xs { get; } = new(256);
        internal StringBuilder Text { get; } = new(64);

        internal void Reset(string fg, bool bold, bool italic)
        {
            Fg = fg;
            Bold = bold;
            Italic = italic;
            Xs.Clear();
            Text.Clear();
        }
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
                normalized.Add(frames[i].WithTime(quantizedTime));
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
            : frame.WithTime(time);

    private static bool HaveSameTime(double left, double right) =>
        Math.Abs(left - right) <= 1e-9;

    private static bool IsBlockElement(string text)
    {
        // A cell may hold a base character plus combining marks, so read the first scalar
        // rather than assuming a second UTF-16 unit is a low surrogate.
        if (string.IsNullOrEmpty(text) || !Rune.TryGetRuneAt(text, 0, out var first))
            return false;

        return first.Value is >= 0x2580 and <= 0x259F and not (0x2591 or 0x2592 or 0x2593);
    }

    private static SvgMetrics CreateMetrics(int fontSize, int cols, int rows, WindowStyle window)
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
            outerMargin = ChromeOuterMargin;
            titleBarHeight = ChromeTitleBarHeight;
            cornerRadius = window == WindowStyle.Macos ? ChromeMacCornerRadius : ChromeWindowsCornerRadius;
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
            fontSize * BaselineFactor,
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

    internal static (double horizontal, double vertical) ResolveInnerPadding(int fontSize)
    {
        var horizontal = Math.Clamp(fontSize * 10.0 / 16.0, InnerPaddingHorizontalMin, InnerPaddingHorizontalMax);
        var vertical = Math.Clamp(fontSize * 4.0 / 16.0, InnerPaddingVerticalMin, InnerPaddingVerticalMax);
        return (horizontal, vertical);
    }

    private readonly record struct SvgMetrics(
        double CharWidth,
        double LineHeight,
        double BaselineOffset,
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

    private sealed class AnimLayer(double show, int p0, int p1 = 0)
    {
        internal double Show { get; } = show;
        internal double? Hide { get; set; }
        internal int P0 { get; } = p0;
        internal int P1 { get; } = p1;
    }
}

