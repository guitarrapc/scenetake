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
internal sealed class SvgDocument(string head, Action<Stream>? writeBody, string tail)
{
    internal static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    internal static SvgDocument FromText(string text) => new(text, null, "");

    internal void WriteTo(Stream stream)
    {
        WriteText(stream, head);
        writeBody?.Invoke(stream);
        WriteText(stream, tail);
    }

    internal static void WriteText(Stream stream, string text)
    {
        if (text.Length != 0)
            stream.Write(Utf8NoBom.GetBytes(text));
    }
}

/// <summary>
/// Line writes for SVG text. SVG output always ends lines with <c>\n</c>, so the file is the
/// same on every OS and Windows output carries no extra byte per line.
/// </summary>
internal static class SvgLineExtensions
{
    internal static StringBuilder AppendLf(this StringBuilder sb, string value) => sb.Append(value).Append('\n');

    internal static StringBuilder AppendLf(
        this StringBuilder sb,
        IFormatProvider? provider,
        [System.Runtime.CompilerServices.InterpolatedStringHandlerArgument(nameof(sb), nameof(provider))] ref StringBuilder.AppendInterpolatedStringHandler handler) =>
        sb.Append('\n');
}

/// <summary>A run of encoded bytes inside a <see cref="Utf8ChunkBuffer"/>.</summary>
internal readonly record struct Utf8Segment(int Chunk, int Offset, int Length);

/// <summary>Append-only UTF-8 text in 1MB chunks, so a large SVG body never needs one contiguous buffer.</summary>
internal sealed class Utf8ChunkBuffer
{
    private const int ChunkSize = 1 << 20;
    private readonly List<byte[]> _chunks = [];
    private int _used;
    private char[] _chars = new char[4096];
    private byte[] _scratch = new byte[4096];

    /// <summary>
    /// Appends <paramref name="text"/> as one contiguous segment. Each append is encoded on its
    /// own, so it must not end in the middle of a surrogate pair.
    /// </summary>
    internal Utf8Segment Append(StringBuilder text)
    {
        var bytes = Encode(text);
        var current = _chunks.Count == 0 ? null : _chunks[^1];
        if (current is null || current.Length - _used < bytes.Length)
        {
            current = new byte[Math.Max(ChunkSize, bytes.Length)];
            _chunks.Add(current);
            _used = 0;
        }

        bytes.CopyTo(current.AsSpan(_used));
        var segment = new Utf8Segment(_chunks.Count - 1, _used, bytes.Length);
        _used += bytes.Length;
        return segment;
    }

    /// <summary>Whether <paramref name="segment"/> holds exactly the UTF-8 encoding of <paramref name="text"/>.</summary>
    internal bool SegmentEquals(Utf8Segment segment, StringBuilder text) =>
        _chunks[segment.Chunk].AsSpan(segment.Offset, segment.Length).SequenceEqual(Encode(text));

    internal void Write(Stream stream, Utf8Segment segment)
    {
        if (segment.Length != 0)
            stream.Write(_chunks[segment.Chunk], segment.Offset, segment.Length);
    }

    /// <summary>
    /// Writes a segment that holds one <c>&lt;text</c> element with <paramref name="classes"/> added in
    /// front of its own class list, if it has one. Row rects never come here: a class would take them
    /// out of the crisp-edge rule for rects without one.
    /// </summary>
    internal void WriteWithClasses(Stream stream, Utf8Segment segment, string classes)
    {
        const int TagLength = 5; // "<text"
        var element = _chunks[segment.Chunk].AsSpan(segment.Offset, segment.Length);
        var rest = element[TagLength..];
        var classAttribute = " class=\""u8;
        stream.Write(element[..TagLength]);
        stream.Write(classAttribute);
        stream.Write(Encoding.ASCII.GetBytes(classes));
        if (rest.StartsWith(classAttribute))
        {
            stream.WriteByte((byte)' ');
            stream.Write(rest[classAttribute.Length..]);
        }
        else
        {
            stream.WriteByte((byte)'"');
            stream.Write(rest);
        }
    }

    private ReadOnlySpan<byte> Encode(StringBuilder text)
    {
        var length = text.Length;
        if (_chars.Length < length)
            _chars = new char[Math.Max(length, _chars.Length * 2)];

        text.CopyTo(0, _chars, 0, length);
        var chars = _chars.AsSpan(0, length);
        var byteCount = SvgDocument.Utf8NoBom.GetByteCount(chars);
        if (_scratch.Length < byteCount)
            _scratch = new byte[Math.Max(byteCount, _scratch.Length * 2)];

        var written = SvgDocument.Utf8NoBom.GetBytes(chars, _scratch);
        return _scratch.AsSpan(0, written);
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
    // The count must fit DefaultFontFamily (11 families), which every cast header records.
    // At 10, scenetake's own casts failed validation on read and warned on every svg/mp4 conversion.
    internal const int MaxFontFamilyLength = 256, MaxFontFamilyCount = 16;
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

        // The colors go into the cast header, and SVG and mp4 are rendered from the cast. Values the
        // cast reader does not accept would be replaced there by the defaults without a word.
        if (!string.IsNullOrWhiteSpace(theme?.Fg) && !CastReader.TryParseHexColor(theme.Fg, out _))
        {
            error = $"invalid render.theme.fg (expected #rgb or #rrggbb): {theme.Fg}";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(theme?.Bg) && !CastReader.TryParseHexColor(theme.Bg, out _))
        {
            error = $"invalid render.theme.bg (expected #rgb or #rrggbb): {theme.Bg}";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(theme?.Palette) && !CastReader.TryParsePalette(theme.Palette, out _))
        {
            error = $"invalid render.theme.palette (expected 16 colors as #rgb or #rrggbb, separated by ':'): {theme.Palette}";
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

    // A <use> plus the id it needs costs about this much. Shorter rows are cheaper to repeat.
    private const int MinReusableRowChars = 48;
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
    [ThreadStatic] private static List<BlockPart>? t_blockParts;
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

        var context = new EncodeContext(metrics, theme, canvasHeight);
        var layers = BuildLayers(events, initialWidth, initialHeight, frames, canvasWidth, canvasHeight, context);
        loopDuration = ResolveLoopDuration(loopDuration, layers);

        var (head, rowKeyframes, tail) = BuildLayeredSvg(layers, context, render, canvasWidth, canvasHeight, loopDuration);
        return new SvgDocument(head, stream => WriteRowLayers(stream, layers, rowKeyframes, context), tail);
    }

    /// <summary>
    /// Writes each row layer on one line:
    /// <list type="bullet">
    /// <item><c>&lt;g class="l K R"&gt;content&lt;/g&gt;</c>, where <c>K</c> names the timing and <c>R</c>, present
    /// for row-local content, moves it to its row. Content that later layers reuse gets an inner
    /// <c>&lt;g id="rN"&gt;</c>, and a cursor with the same timing on the same row goes last.</item>
    /// <item>A layer of one <c>&lt;text&gt;</c> carries the classes itself, without the group.</item>
    /// <item><c>&lt;use href="#rN" class="l K R"/&gt;</c> (or with <c>y="dy"</c> for absolute content) repeats earlier content.</item>
    /// </list>
    /// Empty layers (a row that went blank) draw nothing and are left out.
    /// </summary>
    private static void WriteRowLayers(Stream stream, LayerSet layers, string?[] keyframes, EncodeContext context)
    {
        var rows = layers.Rows;
        var metrics = context.Metrics;
        var origin = metrics.ContentOrigin;
        for (var i = 0; i < rows.Count; i++)
        {
            var layer = rows[i];
            if (keyframes[i] is not { } keyframe)
                continue;

            var classes = layer.RowLocal ? $"l {keyframe} {context.RowClasses[layer.P0]}" : $"l {keyframe}";
            if (layer.ReuseOf is int source)
            {
                SvgDocument.WriteText(stream, layer.RowLocal
                    ? $"<use href=\"#r{source}\" class=\"{classes}\"/>\n"
                    : string.Create(CultureInfo.InvariantCulture,
                        $"<use href=\"#r{source}\" y=\"{(layer.P0 - rows[source].P0) * metrics.LineHeight:0.##}\" class=\"{classes}\"/>\n"));
                continue;
            }

            if (layer.SingleText && !layer.Reused && layer.Cursor < 0)
            {
                context.Content.WriteWithClasses(stream, layer.Content, classes);
                stream.WriteByte((byte)'\n');
                continue;
            }

            SvgDocument.WriteText(stream, layer.Reused ? $"<g class=\"{classes}\"><g id=\"r{i}\">" : $"<g class=\"{classes}\">");
            context.Content.Write(stream, layer.Content);
            if (layer.Reused)
                SvgDocument.WriteText(stream, "</g>");
            if (layer.Cursor >= 0)
            {
                var cursor = layers.Cursors[layer.Cursor];
                var y = layer.RowLocal ? -metrics.BaselineOffset : origin.Y + cursor.P0 * metrics.LineHeight;
                SvgDocument.WriteText(stream, string.Create(CultureInfo.InvariantCulture,
                    $"<use href=\"#c\" x=\"{origin.X + cursor.P1 * metrics.CharWidth:0.##}\" y=\"{y:0.##}\"/>"));
            }

            SvgDocument.WriteText(stream, "</g>\n");
        }
    }

    /// <summary>Whether a row layer draws anything: blank rows produce empty layers, which are left out.</summary>
    private static bool IsWritten(AnimLayer layer) =>
        layer.ElementCount > 0 || layer.ReuseOf is not null || layer.Cursor >= 0;

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
    /// selected <paramref name="frames"/>. Each new row layer is encoded into the context's
    /// content buffer immediately. Comparisons use a copy of each row's active layer and of the
    /// previous frame, so memory is two screens rather than one per frame.
    /// </summary>
    private static LayerSet BuildLayers(
        IReadOnlyList<CastEvent> events,
        int initialWidth,
        int initialHeight,
        IReadOnlyList<ReplayFrame> frames,
        int canvasWidth,
        int canvasHeight,
        EncodeContext context)
    {
        var theme = context.Theme;
        var metrics = context.Metrics;
        var rowLayers = context.Content;
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
        var local = new StringBuilder(4096);
        var absolute = new StringBuilder(4096);
        var verify = new StringBuilder(4096);
        var firstLayerByRowHash = new Dictionary<ulong, int>();
        var origin = metrics.ContentOrigin;

        foreach (var (frame, buffer) in TerminalReplay.ReplaySelected(events, initialWidth, initialHeight, canvasWidth, canvasHeight, theme, frames))
        {
            var time = frame.Time;

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

                // Row-local content (baseline at y=0, moved by the row's class) drops y from every
                // <text>, but the layer then needs the row class, and the row its CSS rule once.
                // The row-local pass also measures the absolute form, so the absolute form is encoded
                // here only when it is kept: when it is shorter, or while the row's savings have not
                // paid for its rule.
                var rowTop = origin.Y + row * metrics.LineHeight;
                local.Clear();
                var localShape = AppendRow(local, buffer, row, origin.X, -metrics.BaselineOffset, rowLocal: true, context, rowTop);
                bool rowLocal;
                if (context.RowClasses[row] is { } rowClass)
                {
                    rowLocal = local.Length + 1 + rowClass.Length < localShape.AbsoluteLength;
                }
                else
                {
                    // Until the row has a class, add up what row-local content would have saved. Once that
                    // pays for the row's CSS rule, the row gets its class.
                    var nameLength = context.Names.PeekLength();
                    var saving = localShape.AbsoluteLength - (local.Length + 1 + nameLength);
                    if (saving > 0)
                        context.RowSavings[row] += saving;
                    rowLocal = context.RowSavings[row] > RowRule(new string('r', nameLength), RowBaseline(metrics, row)).Length + 1;
                }

                var fragment = local;
                var shape = localShape;
                if (!rowLocal)
                {
                    absolute.Clear();
                    shape = AppendRow(absolute, buffer, row, origin.X, rowTop, rowLocal: false, context, rowTop);
                    fragment = absolute;
                }
                var layer = new AnimLayer(time, row) { ElementCount = shape.Elements, SingleText = shape.SingleText };

                // Scrolling moves unchanged rows to other lines. Reuse the first layer that showed the
                // same content, after comparing bytes exactly: row-local content as it is, absolute
                // content re-encoded at that layer's line.
                var reused = false;
                if (fragment.Length >= MinReusableRowChars)
                {
                    var hash = buffer.RowHash(row);
                    if (firstLayerByRowHash.TryGetValue(hash, out var source))
                    {
                        var sourceLayer = rows[source];
                        bool same;
                        if (sourceLayer.RowLocal)
                        {
                            same = rowLayers.SegmentEquals(sourceLayer.Content, local);
                        }
                        else
                        {
                            verify.Clear();
                            var sourceTop = origin.Y + sourceLayer.P0 * metrics.LineHeight;
                            AppendRow(verify, buffer, row, origin.X, sourceTop, rowLocal: false, context, sourceTop);
                            same = rowLayers.SegmentEquals(sourceLayer.Content, verify);
                        }

                        if (same)
                        {
                            layer.ReuseOf = source;
                            layer.RowLocal = sourceLayer.RowLocal;
                            sourceLayer.Reused = true;
                            reused = true;
                        }
                    }
                    else
                    {
                        firstLayerByRowHash[hash] = rows.Count;
                    }
                }

                if (!reused)
                {
                    layer.Content = rowLayers.Append(fragment);
                    layer.RowLocal = rowLocal;
                }

                if (layer.RowLocal)
                    context.RowClasses[row] ??= context.Names.Next();

                rows.Add(layer);
                activeByRow[row] = layer;
                buffer.CopyRowTo(row, activeCells[row]);
            }

            for (var row = 0; row < canvasHeight; row++)
                buffer.CopyRowTo(row, previousCells[row]);
            hasPrevious = true;
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

    /// <summary>
    /// Builds the text before and after the row layers, which <see cref="BuildLayers"/> has already
    /// encoded. Returns each row layer's keyframe class, or null for a layer that is left out.
    /// </summary>
    private static (string Head, string?[] RowKeyframes, string Tail) BuildLayeredSvg(
        LayerSet layers,
        EncodeContext context,
        ResolvedRenderSettings render,
        int canvasWidth,
        int canvasHeight,
        double loopDuration)
    {
        var metrics = context.Metrics;
        var theme = context.Theme;
        var rowLayers = layers.Rows;
        var cursorLayers = layers.Cursors;
        var viewportLayers = layers.Viewports;
        var loopDurationText = loopDuration.ToString("0.######", CultureInfo.InvariantCulture);
        var cursorOpacityText = CursorBlockOpacity.ToString("0.######", CultureInfo.InvariantCulture);

        if (viewportLayers.Count == 0)
            viewportLayers = [new AnimLayer(0, canvasWidth, canvasHeight)];

        var keyframes = new LoopKeyframeRegistry(loopDuration);
        var rowKeyframeIds = new int[rowLayers.Count];
        for (var i = 0; i < rowLayers.Count; i++)
            rowKeyframeIds[i] = keyframes.Register(rowLayers[i].Show, rowLayers[i].Hide);

        var cursorKeyframeIds = new int[cursorLayers.Count];
        for (var i = 0; i < cursorLayers.Count; i++)
            cursorKeyframeIds[i] = keyframes.Register(cursorLayers[i].Show, cursorLayers[i].Hide);

        var viewportKeyframeIds = new int[viewportLayers.Count];
        for (var i = 0; i < viewportLayers.Count; i++)
            viewportKeyframeIds[i] = keyframes.Register(viewportLayers[i].Show, viewportLayers[i].Hide);

        // A cursor shown and hidden together with a row layer on its own row goes inside that layer,
        // after the row's content, so it needs no timing of its own. Other cursors stand alone.
        var layerByTiming = new Dictionary<(int Keyframe, int Row), int>();
        for (var i = 0; i < rowLayers.Count; i++)
        {
            if (rowLayers[i].ReuseOf is null)
                layerByTiming.TryAdd((rowKeyframeIds[i], rowLayers[i].P0), i);
        }

        var standaloneCursors = new List<int>();
        for (var i = 0; i < cursorLayers.Count; i++)
        {
            if (layerByTiming.TryGetValue((cursorKeyframeIds[i], cursorLayers[i].P0), out var rowIndex) && rowLayers[rowIndex].Cursor < 0)
                rowLayers[rowIndex].Cursor = i;
            else
                standaloneCursors.Add(i);
        }

        // Count each keyframe's references, so the most used get the shortest names and unused ones are dropped.
        for (var i = 0; i < rowLayers.Count; i++)
        {
            if (IsWritten(rowLayers[i]))
                keyframes.Use(rowKeyframeIds[i]);
        }

        foreach (var i in standaloneCursors)
            keyframes.Use(cursorKeyframeIds[i]);

        var viewportElements = 2 + (metrics.HasChrome ? 6 : 0); // mask, background, and the chrome's parts
        for (var i = 0; i < viewportLayers.Count; i++)
            keyframes.Use(viewportKeyframeIds[i], viewportElements);

        keyframes.AssignNames(context.Names);

        var sb = new StringBuilder(64 * 1024);
        sb.AppendLf(CultureInfo.InvariantCulture, $"<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLf(CultureInfo.InvariantCulture,
            $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{metrics.SvgWidth:0.##}\" height=\"{metrics.SvgHeight:0.##}\" viewBox=\"0 0 {metrics.SvgWidth:0.##} {metrics.SvgHeight:0.##}\" preserveAspectRatio=\"xMidYMid meet\" role=\"img\" aria-label=\"scenetake output\">");

        sb.AppendLf("<style>");
        sb.AppendLf(CultureInfo.InvariantCulture,
            $"text {{ font-family: {render.FontFamily}; font-size: {render.FontSize}px; white-space: pre; }}");
        sb.AppendLf("text { dominant-baseline: alphabetic; }");
        // Background and block rects in rows carry no class, also inside reused <use> content. This rule gives them crisp edges.
        sb.AppendLf(".bg,rect:not([class]) { shape-rendering: crispEdges; }");
        // Every animated element: hidden by default, switched exactly at its keyframe offsets, looping.
        sb.AppendLf(CultureInfo.InvariantCulture, $".l {{ opacity: 0; animation: {loopDurationText}s step-end infinite; }}");
        sb.AppendLf(CultureInfo.InvariantCulture,
            $".c {{ fill: {theme.Foreground}; fill-opacity: {cursorOpacityText}; }}");
        for (var row = 0; row < context.RowClasses.Length; row++)
        {
            if (context.RowClasses[row] is { } rowClass)
                sb.AppendLf(RowRule(rowClass, RowBaseline(metrics, row)));
        }

        context.Fills.AppendRules(sb);
        keyframes.AppendAll(sb);
        sb.AppendLf("</style>");

        sb.AppendLf("<defs>");
        if (metrics.HasChrome)
            sb.AppendLf(WindowShadowFilter);

        if (cursorLayers.Count > 0)
        {
            sb.AppendLf(CultureInfo.InvariantCulture,
                $"<rect id=\"c\" class=\"c\" width=\"{metrics.CharWidth:0.##}\" height=\"{metrics.LineHeight:0.##}\"/>");
        }

        context.Glyphs.AppendDefs(sb, metrics);

        sb.AppendLf(CultureInfo.InvariantCulture,
            $"<mask id=\"viewport-mask\" x=\"0\" y=\"0\" width=\"{metrics.SvgWidth:0.##}\" height=\"{metrics.SvgHeight:0.##}\" maskUnits=\"userSpaceOnUse\">");
        for (var i = 0; i < viewportLayers.Count; i++)
        {
            var viewport = viewportLayers[i];
            var viewportWidth = metrics.ViewportPixelWidth(viewport.P0);
            var viewportHeight = metrics.ViewportPixelHeight(viewport.P1);
            sb.AppendLf(CultureInfo.InvariantCulture,
                $"<rect class=\"l {keyframes.Name(viewportKeyframeIds[i])} viewport-mask-{i}\" x=\"{metrics.TerminalOriginX:0.##}\" y=\"{metrics.TerminalOriginY:0.##}\" width=\"{viewportWidth:0.##}\" height=\"{viewportHeight:0.##}\" fill=\"white\"/>");
        }

        sb.AppendLf("</mask>");
        sb.AppendLf("</defs>");

        if (metrics.HasChrome)
        {
            var chrome = ResolveChromePalette(render.Window, render.Theme.Bg);
            for (var i = 0; i < viewportLayers.Count; i++)
            {
                var viewport = viewportLayers[i];
                AppendWindowChrome(sb, metrics, chrome, render.Window, viewport.P0, viewport.P1, i, keyframes.Name(viewportKeyframeIds[i]));
            }
        }

        // Text in the theme's foreground color has no fill of its own and inherits this one.
        sb.AppendLf(CultureInfo.InvariantCulture, $"<g mask=\"url(#viewport-mask)\" fill=\"{theme.Foreground}\">");
        for (var i = 0; i < viewportLayers.Count; i++)
        {
            var viewport = viewportLayers[i];
            var viewportWidth = metrics.ViewportPixelWidth(viewport.P0);
            var viewportHeight = metrics.ViewportPixelHeight(viewport.P1);
            sb.AppendLf(CultureInfo.InvariantCulture,
                $"<rect class=\"bg l {keyframes.Name(viewportKeyframeIds[i])} viewport-bg-{i}\" x=\"{metrics.TerminalOriginX:0.##}\" y=\"{metrics.TerminalOriginY:0.##}\" width=\"{viewportWidth:0.##}\" height=\"{viewportHeight:0.##}\" fill=\"{render.Theme.Bg}\"/>");
        }

        var head = sb.ToString();
        sb.Clear();

        var origin = metrics.ContentOrigin;
        foreach (var i in standaloneCursors)
        {
            var layer = cursorLayers[i];
            sb.AppendLf(CultureInfo.InvariantCulture,
                $"<use href=\"#c\" x=\"{origin.X + layer.P1 * metrics.CharWidth:0.##}\" y=\"{origin.Y + layer.P0 * metrics.LineHeight:0.##}\" class=\"l {keyframes.Name(cursorKeyframeIds[i])}\"/>");
        }

        sb.AppendLf("</g>");
        sb.AppendLf("</svg>");

        var rowKeyframes = new string?[rowLayers.Count];
        for (var i = 0; i < rowLayers.Count; i++)
        {
            if (IsWritten(rowLayers[i]))
                rowKeyframes[i] = keyframes.Name(rowKeyframeIds[i]);
        }

        return (head, rowKeyframes, sb.ToString());
    }

    /// <summary>The y of a row's baseline: row-local content is drawn with its baseline at y=0 and moved here.</summary>
    private static double RowBaseline(SvgMetrics metrics, int row) =>
        metrics.ContentOrigin.Y + row * metrics.LineHeight + metrics.BaselineOffset;

    private static string RowRule(string rowClass, double baseline) =>
        string.Create(CultureInfo.InvariantCulture, $".{rowClass}{{transform:translate(0,{baseline:0.##}px)}}");

    /// <summary>
    /// Deduplicated keyframes for show/hide windows within the loop. Each animated element has
    /// class <c>l</c> (hidden, step-end timing, looping) plus a short class naming its keyframes, so a
    /// window costs one short keyframe rule and one class rule however many elements share it.
    /// Names are given after all elements are counted: the most used get the shortest names, and
    /// keyframes nothing refers to are not written.
    /// </summary>
    private sealed class LoopKeyframeRegistry(double loopDuration)
    {
        private readonly Dictionary<string, int> _idsByBody = new(StringComparer.Ordinal);
        private readonly List<string> _bodies = [];
        private readonly List<int> _uses = [];
        private readonly string _percentFormat = PercentFormat(loopDuration);
        private string?[] _names = [];

        internal int Register(double showTime, double? hideTime)
        {
            var body = BuildStepKeyframesBody(showTime, hideTime);
            if (_idsByBody.TryGetValue(body, out var existing))
                return existing;

            var id = _bodies.Count;
            _idsByBody[body] = id;
            _bodies.Add(body);
            _uses.Add(0);
            return id;
        }

        internal void Use(int id, int count = 1) => _uses[id] += count;

        internal void AssignNames(ClassNamePool pool)
        {
            _names = new string?[_bodies.Count];
            var order = Enumerable.Range(0, _bodies.Count).Where(id => _uses[id] > 0).OrderByDescending(id => _uses[id]).ThenBy(id => id);
            foreach (var id in order)
                _names[id] = pool.Next();
        }

        internal string Name(int id) => _names[id] ?? throw new InvalidOperationException($"Keyframe {id} has no name; it was not counted as used.");

        internal void AppendAll(StringBuilder sb)
        {
            for (var id = 0; id < _bodies.Count; id++)
            {
                if (_names[id] is not { } name)
                    continue;

                sb.Append("@keyframes ").Append(name).Append('{').Append(_bodies[id]).AppendLf("}");
                sb.Append('.').Append(name).Append("{animation-name:").Append(name).AppendLf("}");
            }
        }

        /// <summary>
        /// Visible from <paramref name="showTime"/> until <paramref name="hideTime"/>. With step-end timing
        /// the opacity switches exactly at each offset. The implicit 0% and 100% keyframes take the
        /// element's hidden opacity. A layer's hide and its replacement's show share one offset, so
        /// they switch together.
        /// </summary>
        private string BuildStepKeyframesBody(double showTime, double? hideTime)
        {
            var show = showTime <= 0 ? "0" : Percent(showTime);
            return hideTime is double hide
                ? $"{show}%{{opacity:1}}{Percent(hide)}%{{opacity:0}}"
                : $"{show}%{{opacity:1}}";
        }

        private string Percent(double time) =>
            loopDuration <= 0 ? "0" : (time / loopDuration * 100.0).ToString(_percentFormat, CultureInfo.InvariantCulture);

        /// <summary>Enough decimal places that one step of the percentage is at most 1ms of the loop.</summary>
        private static string PercentFormat(double loopDuration)
        {
            var decimals = loopDuration <= 0 ? 1 : Math.Clamp((int)Math.Ceiling(Math.Log10(loopDuration * 10)), 1, 6);
            return "0." + new string('#', decimals);
        }
    }

    /// <summary>
    /// Short class names, shared by rows, colors, and keyframes so they never collide: a–z, then a
    /// letter followed by letters or digits. Lowercase only, because an SVG inlined in an HTML page
    /// without a doctype matches class names regardless of case. Skips <c>l</c>, <c>c</c>, and
    /// <c>bg</c>, which the SVG uses itself, and words CSS reserves for <c>animation-name</c>.
    /// </summary>
    private sealed class ClassNamePool
    {
        private const string FirstChars = "abcdefghijklmnopqrstuvwxyz";
        private const string NextChars = FirstChars + "0123456789";
        private long _next;

        internal string Next()
        {
            var name = NameAt(_next++);
            while (IsReserved(name))
                name = NameAt(_next++);
            return name;
        }

        internal int PeekLength()
        {
            var index = _next;
            var name = NameAt(index);
            while (IsReserved(name))
                name = NameAt(++index);
            return name.Length;
        }

        private static bool IsReserved(string name) =>
            name is "l" or "c" or "bg"
            || (name.Length >= 4 && (name.Equals("none", StringComparison.OrdinalIgnoreCase)
                || name.Equals("unset", StringComparison.OrdinalIgnoreCase)
                || name.Equals("revert", StringComparison.OrdinalIgnoreCase)
                || name.Equals("initial", StringComparison.OrdinalIgnoreCase)
                || name.Equals("inherit", StringComparison.OrdinalIgnoreCase)
                || name.Equals("default", StringComparison.OrdinalIgnoreCase)));

        private static string NameAt(long index)
        {
            var length = 1;
            long count = FirstChars.Length;
            while (index >= count)
            {
                index -= count;
                length++;
                count *= NextChars.Length;
            }

            var chars = new char[length];
            for (var i = length - 1; i > 0; i--)
            {
                chars[i] = NextChars[(int)(index % NextChars.Length)];
                index /= NextChars.Length;
            }

            chars[0] = FirstChars[(int)index];
            return new string(chars);
        }
    }

    /// <summary>
    /// Text colors as classes. The theme's foreground is inherited from the enclosing group and
    /// needs nothing. Any other color gets a short class on first use, and one CSS rule. A CSS rule
    /// on <c>text</c> itself would not do, because it overrides every <c>fill</c> attribute.
    /// </summary>
    private sealed class FillClasses(string defaultFill, ClassNamePool names)
    {
        private readonly Dictionary<string, string> _classes = new(StringComparer.Ordinal);
        private readonly List<(string Fill, string Name)> _ordered = [];

        internal string? ClassFor(string fill)
        {
            if (string.Equals(fill, defaultFill, StringComparison.Ordinal))
                return null;

            if (!_classes.TryGetValue(fill, out var name))
            {
                name = names.Next();
                _classes[fill] = name;
                _ordered.Add((fill, name));
            }

            return name;
        }

        internal void AppendRules(StringBuilder sb)
        {
            foreach (var (fill, name) in _ordered)
                sb.Append('.').Append(name).Append("{fill:").Append(fill).AppendLf("}");
        }
    }

    /// <summary>State shared by row encoding and the text around it.</summary>
    private sealed class EncodeContext
    {
        internal EncodeContext(SvgMetrics metrics, TerminalTheme theme, int canvasHeight)
        {
            Metrics = metrics;
            Theme = theme;
            RowClasses = new string?[canvasHeight];
            RowSavings = new int[canvasHeight];
            Fills = new FillClasses(theme.Foreground, Names);
        }

        internal SvgMetrics Metrics { get; }
        internal TerminalTheme Theme { get; }
        internal ClassNamePool Names { get; } = new();
        internal FillClasses Fills { get; }
        internal SoftGlyphDefs Glyphs { get; } = new();
        internal Utf8ChunkBuffer Content { get; } = new();

        /// <summary>Per canvas row, the class that moves row-local content to that row, or null until a layer needs it.</summary>
        internal string?[] RowClasses { get; }

        /// <summary>Per canvas row without a class yet, the characters row-local content would have saved so far.</summary>
        internal int[] RowSavings { get; }
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
        int layerIndex,
        string? keyframe)
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
        var cls = keyframe is null ? $"viewport-chrome-{layerIndex}" : $"l {keyframe} viewport-chrome-{layerIndex}";

        sb.AppendLf(CultureInfo.InvariantCulture,
            $"<rect class=\"{cls}\" x=\"{x:0.##}\" y=\"{y:0.##}\" width=\"{width:0.##}\" height=\"{totalHeight:0.##}\" rx=\"{rx:0.##}\" ry=\"{rx:0.##}\" fill=\"none\" stroke=\"{chrome.Border}\" stroke-width=\"1\" filter=\"url(#window-shadow)\"/>");
        sb.AppendLf(CultureInfo.InvariantCulture,
            $"<path class=\"{cls}\" d=\"M {x + rx:0.##},{y:0.##} H {right - rx:0.##} Q {right:0.##},{y:0.##} {right:0.##},{y + rx:0.##} V {barBottom:0.##} H {x:0.##} V {y + rx:0.##} Q {x:0.##},{y:0.##} {x + rx:0.##},{y:0.##} Z\" fill=\"{chrome.TitleBarBg}\"/>");
        sb.AppendLf(CultureInfo.InvariantCulture,
            $"<line class=\"{cls}\" x1=\"{x:0.##}\" y1=\"{barBottom:0.##}\" x2=\"{right:0.##}\" y2=\"{barBottom:0.##}\" stroke=\"{chrome.Border}\" stroke-width=\"1\"/>");

        if (window == WindowStyle.Macos)
        {
            var radius = MacButtonDiameter / 2d;
            var cx = x + ChromeSidePadding + radius;
            var cy = y + ChromeTopPadding + radius;
            foreach (var color in MacButtonColors)
            {
                sb.AppendLf(CultureInfo.InvariantCulture,
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
            sb.AppendLf(CultureInfo.InvariantCulture,
                $"<rect class=\"{cls}\" x=\"{left:0.##}\" y=\"{top:0.##}\" width=\"{size:0.##}\" height=\"{size:0.##}\" rx=\"{WinButtonCornerRadius:0.##}\" ry=\"{WinButtonCornerRadius:0.##}\" fill=\"{chrome.WinButton}\"/>");
            left += size + gap;
        }
    }

    private static string BuildEmptySvg(ResolvedRenderSettings render, int width, int height)
    {
        var metrics = CreateMetrics(render.FontSize, width, height, render.Window);
        var sb = new StringBuilder();
        sb.AppendLf(CultureInfo.InvariantCulture, $"<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLf(CultureInfo.InvariantCulture,
            $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{metrics.SvgWidth:0.##}\" height=\"{metrics.SvgHeight:0.##}\" viewBox=\"0 0 {metrics.SvgWidth:0.##} {metrics.SvgHeight:0.##}\">");
        if (metrics.HasChrome)
        {
            sb.AppendLf("<defs>");
            sb.AppendLf(WindowShadowFilter);
            sb.AppendLf("</defs>");
            var chrome = ResolveChromePalette(render.Window, render.Theme.Bg);
            AppendWindowChrome(sb, metrics, chrome, render.Window, width, height, 0, keyframe: null);
            var bgRect = metrics.TerminalBackgroundRect(width, height);
            sb.AppendLf(CultureInfo.InvariantCulture,
                $"<rect x=\"{bgRect.X:0.##}\" y=\"{bgRect.Y:0.##}\" width=\"{bgRect.Width:0.##}\" height=\"{bgRect.Height:0.##}\" fill=\"{render.Theme.Bg}\"/>");
        }
        else
        {
            var bgRect = metrics.BackgroundRect();
            sb.AppendLf(CultureInfo.InvariantCulture,
                $"<rect x=\"{bgRect.X:0.##}\" y=\"{bgRect.Y:0.##}\" width=\"{bgRect.Width:0.##}\" height=\"{bgRect.Height:0.##}\" fill=\"{render.Theme.Bg}\"/>");
        }

        sb.AppendLf("</svg>");
        return sb.ToString();
    }

    /// <summary>
    /// The number of elements in an encoded row, whether its only element is a &lt;text&gt;, and, for
    /// row-local content, how many characters the same row would take with absolute coordinates.
    /// </summary>
    private readonly record struct RowShape(int Elements, bool SingleText, int AbsoluteLength);

    /// <summary>
    /// Appends one row on a single line: background spans first, then block elements as rects,
    /// then soft font glyphs as &lt;use&gt;s, then the row's text in whichever encoding is shorter for it.
    /// <list type="bullet">
    /// <item>Run form: one &lt;text&gt; per style run, spread with <c>textLength</c>. Small for prose.</item>
    /// <item>Glyph-list form: one &lt;text&gt; per style with an <c>x</c> per glyph and no spaces.
    /// Small for sparse colored output such as matrix rain.</item>
    /// </list>
    /// Rows with underlined cells always use the run form, because the underline runs under the spaces.
    /// <paramref name="rowTop"/> is the row's top edge. Row-local content has its baseline at y=0,
    /// so its &lt;text&gt; elements carry no <c>y</c>. For row-local content the result also gives the
    /// length of the absolute encoding at <paramref name="absoluteRowTop"/>, so the caller can choose a form
    /// before encoding the absolute one. The absolute encoding adds <c>y</c> to every
    /// &lt;text&gt;, moves every rect, and can pick the other text form.
    /// </summary>
    private static RowShape AppendRow(
        StringBuilder sb,
        ScreenBuffer buffer,
        int row,
        double originX,
        double rowTop,
        bool rowLocal,
        EncodeContext context,
        double absoluteRowTop)
    {
        var metrics = context.Metrics;
        var theme = context.Theme;
        var start = sb.Length;
        var absoluteTextY = string.Create(CultureInfo.InvariantCulture, $" y=\"{absoluteRowTop + metrics.BaselineOffset:0.##}\"");
        var textYAttribute = rowLocal ? "" : absoluteTextY;
        var hasUnderline = AppendBackgroundSpans(sb, buffer, row, originX, rowTop, metrics, theme, out var backgroundRects);
        var blockRects = AppendBlockRects(sb, buffer, row, originX, rowTop, metrics, theme, absoluteRowTop, out var blockYGrowth);
        var glyphUses = AppendSoftGlyphs(sb, buffer, row, originX, textYAttribute, context);
        var rectsLength = sb.Length - start;

        var runForm = t_runForm ??= new StringBuilder(1024);
        runForm.Clear();
        var runTexts = AppendRunText(runForm, buffer, row, originX, textYAttribute, context);
        var texts = runTexts;
        var listLength = int.MaxValue;
        var listTexts = 0;
        if (hasUnderline)
        {
            sb.Append(runForm);
        }
        else
        {
            var listForm = t_listForm ??= new StringBuilder(1024);
            listForm.Clear();
            listTexts = AppendGlyphListText(listForm, buffer, row, originX, textYAttribute, context);
            listLength = listForm.Length;
            if (listForm.Length < runForm.Length)
            {
                sb.Append(listForm);
                texts = listTexts;
            }
            else
            {
                sb.Append(runForm);
            }
        }

        var absoluteLength = sb.Length - start;
        if (rowLocal)
        {
            // Absolute content adds y to every <text> and changes the y of every rect. The absolute
            // encoding makes its own choice between the two text forms, so compare them again.
            var backgroundYGrowth = backgroundRects * (Format(absoluteRowTop).Length - Format(rowTop).Length);
            var absoluteRun = runForm.Length + runTexts * absoluteTextY.Length;
            var absoluteList = hasUnderline ? int.MaxValue : listLength + listTexts * absoluteTextY.Length;
            var glyphYGrowth = glyphUses * absoluteTextY.Length;
            absoluteLength = rectsLength + backgroundYGrowth + blockYGrowth + glyphYGrowth + (absoluteList < absoluteRun ? absoluteList : absoluteRun);
        }

        var elements = backgroundRects + blockRects + glyphUses + texts;
        return new RowShape(elements, elements == 1 && texts == 1, absoluteLength);
    }

    private static string Format(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>
    /// One rect per run of cells with the same non-theme background, regardless of foreground
    /// or style. A wide cell's continuation takes the wide cell's background. Returns whether
    /// any cell in the row is underlined.
    /// </summary>
    private static bool AppendBackgroundSpans(
        StringBuilder sb, ScreenBuffer buffer, int row, double originX, double rowTop, SvgMetrics metrics, TerminalTheme theme, out int rects)
    {
        rects = 0;
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
                        $"<rect x=\"{originX + spanStart * metrics.CharWidth:0.##}\" y=\"{rowTop:0.##}\" width=\"{(col - spanStart) * metrics.CharWidth:0.##}\" height=\"{metrics.LineHeight:0.##}\" fill=\"{spanBg}\"/>");
                    rects++;
                }

                spanBg = bg;
                spanStart = col;
            }

            previousBg = bg;
        }

        return hasUnderline;
    }

    /// <summary>
    /// Block elements (U+2580–U+259F) as rects covering their exact part of the cell, as the mp4
    /// rasterizer draws them: halves, eighths, quadrants, and shades as 25%, 50%, and 75% opacity.
    /// Font block glyphs keep the font's own height, which leaves seams between rows at a 1.25 line
    /// height. Parts of one color and height that touch along the row merge into one rect.
    /// </summary>
    private static int AppendBlockRects(
        StringBuilder sb, ScreenBuffer buffer, int row, double originX, double rowTop, SvgMetrics metrics, TerminalTheme theme,
        double absoluteRowTop, out int absoluteYGrowth)
    {
        absoluteYGrowth = 0;
        var parts = t_blockParts ??= [];
        parts.Clear();
        for (var col = 0; col < buffer.Width; col++)
        {
            var cell = buffer.GetCell(row, col);
            if (cell.IsWideContinuation || !TryGetBlockCodepoint(cell.Text, out var codepoint))
                continue;

            var fill = CellColors.ResolveTintedForeground(buffer, row, col, cell, theme);
            AddBlockParts(parts, codepoint, col * 8, cell.IsWide ? 2 : 1, fill);
        }

        if (parts.Count == 0)
            return 0;

        parts.Sort(BlockPart.Compare);
        var rects = 0;
        var current = parts[0];
        for (var i = 1; i <= parts.Count; i++)
        {
            if (i < parts.Count && parts[i].Continues(current))
            {
                current = current with { Width = current.Width + parts[i].Width };
                continue;
            }

            var partTop = current.Y * metrics.LineHeight / 8;
            absoluteYGrowth += Format(absoluteRowTop + partTop).Length - Format(rowTop + partTop).Length;
            sb.Append(CultureInfo.InvariantCulture,
                $"<rect x=\"{originX + current.X * metrics.CharWidth / 8:0.##}\" y=\"{rowTop + partTop:0.##}\" width=\"{current.Width * metrics.CharWidth / 8:0.##}\" height=\"{current.Height * metrics.LineHeight / 8:0.##}\" fill=\"{current.Fill}\"");
            if (current.Alpha < BlockPart.Opaque)
                sb.Append(" fill-opacity=\"").Append(current.Alpha switch { 1 => "0.25", 2 => "0.5", _ => "0.75" }).Append('"');
            sb.Append("/>");
            rects++;
            if (i < parts.Count)
                current = parts[i];
        }

        return rects;
    }

    /// <summary>Adds the parts of one block element at cell <paramref name="x"/> (in eighths of a cell), <paramref name="span"/> cells wide.</summary>
    private static void AddBlockParts(List<BlockPart> parts, int codepoint, int x, int span, string fill)
    {
        void Add(int left, int top, int width, int height, int alpha = BlockPart.Opaque) =>
            parts.Add(new BlockPart(x + left * span, top, width * span, height, fill, alpha));

        switch (codepoint)
        {
            case 0x2580: Add(0, 0, 8, 4); break;
            case >= 0x2581 and <= 0x2588: Add(0, 8 - (codepoint - 0x2580), 8, codepoint - 0x2580); break;
            case >= 0x2589 and <= 0x258F: Add(0, 0, 0x2590 - codepoint, 8); break;
            case 0x2590: Add(4, 0, 4, 8); break;
            case 0x2591: Add(0, 0, 8, 8, alpha: 1); break;
            case 0x2592: Add(0, 0, 8, 8, alpha: 2); break;
            case 0x2593: Add(0, 0, 8, 8, alpha: 3); break;
            case 0x2594: Add(0, 0, 8, 1); break;
            case 0x2595: Add(7, 0, 1, 8); break;
            default:
                // Quadrants U+2596–U+259F: bits 1 upper-left, 2 upper-right, 4 lower-left, 8 lower-right.
                ReadOnlySpan<byte> quadrants = [4, 8, 1, 1 | 4 | 8, 1 | 8, 1 | 2 | 4, 1 | 2 | 8, 2, 2 | 4, 2 | 4 | 8];
                var bits = quadrants[codepoint - 0x2596];
                if ((bits & 1) != 0) Add(0, 0, 4, 4);
                if ((bits & 2) != 0) Add(4, 0, 4, 4);
                if ((bits & 4) != 0) Add(0, 4, 4, 4);
                if ((bits & 8) != 0) Add(4, 4, 4, 4);
                break;
        }
    }

    /// <summary>
    /// Part of a block element: <see cref="X"/> and <see cref="Width"/> in eighths of a cell,
    /// <see cref="Y"/> and <see cref="Height"/> in eighths of a line, <see cref="Alpha"/> in quarters.
    /// </summary>
    private readonly record struct BlockPart(int X, int Y, int Width, int Height, string Fill, int Alpha)
    {
        internal const int Opaque = 4;

        internal bool Continues(BlockPart left) =>
            X == left.X + left.Width && Y == left.Y && Height == left.Height && Alpha == left.Alpha
            && string.Equals(Fill, left.Fill, StringComparison.Ordinal);

        internal static int Compare(BlockPart a, BlockPart b)
        {
            var result = string.CompareOrdinal(a.Fill, b.Fill);
            if (result == 0) result = a.Alpha.CompareTo(b.Alpha);
            if (result == 0) result = a.Y.CompareTo(b.Y);
            if (result == 0) result = a.Height.CompareTo(b.Height);
            if (result == 0) result = a.X.CompareTo(b.X);
            return result;
        }
    }

    /// <summary>
    /// Soft font (DECDLD) glyphs: one &lt;use&gt; of the glyph's path per cell. Like a &lt;text&gt; it sits
    /// at the row's baseline, so row-local content needs no <c>y</c>, and its color is a fill class, or
    /// inherited for the theme's foreground. Returns the number of &lt;use&gt;s.
    /// </summary>
    private static int AppendSoftGlyphs(
        StringBuilder sb, ScreenBuffer buffer, int row, double originX, string textYAttribute, EncodeContext context)
    {
        var uses = 0;
        for (var col = 0; col < buffer.Width; col++)
        {
            var cell = buffer.GetCell(row, col);
            if (cell.Glyph is not { } glyph)
                continue;

            var fill = CellColors.ResolveTintedForeground(buffer, row, col, cell, context.Theme);
            sb.Append("<use href=\"#").Append(context.Glyphs.IdFor(glyph)).Append('"')
                .Append(CultureInfo.InvariantCulture, $" x=\"{originX + col * context.Metrics.CharWidth:0.##}\"").Append(textYAttribute);
            if (context.Fills.ClassFor(fill) is { } fillClass)
                sb.Append(" class=\"").Append(fillClass).Append('"');
            sb.Append("/>");
            uses++;
        }

        return uses;
    }

    /// <summary>
    /// Soft font glyphs, each one &lt;path&gt; in &lt;defs&gt; that cells draw with a &lt;use&gt;. The path is
    /// in the glyph's own pixels, scaled to the cell and moved up by the baseline offset, so a &lt;use&gt;
    /// at a text baseline covers its cell. Ids are given on first use and skip <c>c</c> (the cursor)
    /// and names starting with <c>r</c> (reused rows are <c>r0</c>, <c>r1</c>, …).
    /// </summary>
    private sealed class SoftGlyphDefs
    {
        private const string FirstChars = "abdefghijklmnopqstuvwxyz";
        private const string NextChars = "abcdefghijklmnopqrstuvwxyz0123456789";
        private readonly Dictionary<SoftGlyph, string> _ids = [];
        private readonly List<(SoftGlyph Glyph, string Id)> _ordered = [];

        internal string IdFor(SoftGlyph glyph)
        {
            if (!_ids.TryGetValue(glyph, out var id))
            {
                id = NameAt(_ordered.Count);
                _ids[glyph] = id;
                _ordered.Add((glyph, id));
            }

            return id;
        }

        internal void AppendDefs(StringBuilder sb, SvgMetrics metrics)
        {
            foreach (var (glyph, id) in _ordered)
            {
                sb.Append(CultureInfo.InvariantCulture,
                    $"<path id=\"{id}\" transform=\"translate(0 {-metrics.BaselineOffset:0.##}) scale({metrics.CharWidth / glyph.Width:0.#####} {metrics.LineHeight / glyph.Height:0.#####})\" d=\"");
                AppendPixels(sb, glyph);
                sb.AppendLf("\"/>");
            }
        }

        /// <summary>
        /// The glyph's pixels as rects in one path: a run along a row, extended down over the rows
        /// below that have the same pixels free. One path keeps the rects free of seams when anti-aliased.
        /// </summary>
        internal static void AppendPixels(StringBuilder sb, SoftGlyph glyph)
        {
            Span<ulong> covered = stackalloc ulong[glyph.Height];
            for (var y = 0; y < glyph.Height; y++)
            {
                var free = glyph.Rows[y] & ~covered[y];
                while (free != 0)
                {
                    var x = System.Numerics.BitOperations.TrailingZeroCount(free);
                    var end = x;
                    while (end < glyph.Width && ((free >> end) & 1) != 0)
                        end++;

                    var run = (end == 64 ? ulong.MaxValue : (1UL << end) - 1) & ~((1UL << x) - 1);
                    var bottom = y + 1;
                    while (bottom < glyph.Height && (glyph.Rows[bottom] & ~covered[bottom] & run) == run)
                        bottom++;
                    for (var yy = y; yy < bottom; yy++)
                        covered[yy] |= run;

                    sb.Append(CultureInfo.InvariantCulture, $"M{x} {y}h{end - x}v{bottom - y}h{x - end}z");
                    free &= ~run;
                }
            }
        }

        private static string NameAt(int index)
        {
            if (index < FirstChars.Length)
                return ScreenBuffer.CharStr(FirstChars[index]);

            index -= FirstChars.Length;
            var length = 2;
            var count = FirstChars.Length * NextChars.Length;
            while (index >= count)
            {
                index -= count;
                length++;
                count *= NextChars.Length;
            }

            var chars = new char[length];
            for (var i = length - 1; i > 0; i--)
            {
                chars[i] = NextChars[index % NextChars.Length];
                index /= NextChars.Length;
            }

            chars[0] = FirstChars[index];
            return new string(chars);
        }
    }

    /// <summary>
    /// Run form: runs break on any color or style change. Background rects are drawn separately.
    /// Returns the number of &lt;text&gt; elements.
    /// <list type="bullet">
    /// <item><c>textLength</c> spans the run's cells, not its UTF-16 length, so combining marks and
    /// surrogate pairs do not stretch the run past its cells.</item>
    /// <item>Underlined spaces up to the row's last underlined character keep their underline, as in
    /// the mp4 rasterizer. Spaces after a run's last drawn cell are left out.</item>
    /// <item>A block element is a rect, and a soft font glyph a &lt;use&gt;. When one is underlined, the run
    /// carries a space in its cell, so the underline runs under it as it does in mp4.</item>
    /// </list>
    /// </summary>
    private static int AppendRunText(
        StringBuilder sb, ScreenBuffer buffer, int row, double originX, string textYAttribute, EncodeContext context)
    {
        var metrics = context.Metrics;
        var theme = context.Theme;
        var lastUnderlined = -1;
        for (var c = 0; c < buffer.Width; c++)
        {
            var underlined = buffer.GetCell(row, c);
            if (!underlined.IsWideContinuation && underlined.Underline && underlined.Text != Space)
                lastUnderlined = c;
        }

        bool Draws(ScreenCell candidate, int at) => candidate.Text != Space || (candidate.Underline && at <= lastUnderlined);
        static bool SkipsText(ScreenCell candidate) => IsShapeCell(candidate) && !candidate.Underline;
        static string RunText(ScreenCell candidate) => IsShapeCell(candidate) ? Space : candidate.Text;

        var texts = 0;
        var col = 0;
        while (col < buffer.Width)
        {
            var cell = buffer.GetCell(row, col);
            if (cell.IsWideContinuation || SkipsText(cell))
            {
                col++;
                continue;
            }

            if (!Draws(cell, col) && CellColors.ResolveBackground(cell, theme) is null)
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

            var runText = (t_runText ??= new StringBuilder(64));
            runText.Clear();
            runText.Append(RunText(cell));
            var drawnChars = 0;
            var drawnEnd = runStart;
            if (Draws(cell, col))
            {
                drawnChars = runText.Length;
                drawnEnd = col + (cell.IsWide ? 2 : 1);
            }

            col++;
            while (col < buffer.Width)
            {
                var next = buffer.GetCell(row, col);
                if (next.IsWideContinuation)
                    break;

                if (SkipsText(next))
                    break;

                var nextFg = CellColors.ResolveTintedForeground(buffer, row, col, next, theme);
                var nextBg = CellColors.ResolveBackground(next, theme);
                if (nextFg != runFg || nextBg != runBg || next.Bold != runBold
                    || next.Italic != runItalic || next.Underline != runUnderline || next.IsWide)
                {
                    break;
                }

                runText.Append(RunText(next));
                if (Draws(next, col))
                {
                    drawnChars = runText.Length;
                    drawnEnd = col + 1;
                }

                col++;
            }

            if (drawnChars == 0)
                continue; // only spaces: the background span already covers them

            var drawText = drawnChars == runText.Length ? runText.ToString() : runText.ToString(0, drawnChars);
            AppendTextStart(sb, runFg, context.Fills);
            sb.Append(CultureInfo.InvariantCulture, $" x=\"{originX + runStart * metrics.CharWidth:0.##}\"").Append(textYAttribute);
            AppendFontStyle(sb, runBold, runItalic, runUnderline);
            sb.Append(CultureInfo.InvariantCulture, $" textLength=\"{(drawnEnd - runStart) * metrics.CharWidth:0.##}\">{EscapeXml(drawText)}</text>");
            texts++;
        }

        return texts;
    }

    /// <summary>
    /// Glyph-list form: one &lt;text&gt; per (fg, bold, italic) with an x per glyph, spaces omitted.
    /// Cells of more than one UTF-16 unit (surrogate pairs, combining marks) get their own
    /// &lt;text&gt;: SVG assigns x values per addressable character, which browsers count
    /// differently for those. Returns the number of &lt;text&gt; elements.
    /// </summary>
    private static int AppendGlyphListText(
        StringBuilder sb, ScreenBuffer buffer, int row, double originX, string textYAttribute, EncodeContext context)
    {
        var metrics = context.Metrics;
        var theme = context.Theme;
        var texts = 0;
        var groups = t_glyphGroups ??= [];
        var used = 0;
        for (var col = 0; col < buffer.Width; col++)
        {
            var cell = buffer.GetCell(row, col);
            var text = cell.Text;
            if (cell.IsWideContinuation || text.Length == 0 || (text.Length == 1 && char.IsWhiteSpace(text[0])) || cell.Glyph is not null || IsBlockCell(text))
                continue;

            var fg = CellColors.ResolveTintedForeground(buffer, row, col, cell, theme);
            var x = originX + col * metrics.CharWidth;
            if (text.Length != 1)
            {
                AppendTextStart(sb, fg, context.Fills);
                sb.Append(CultureInfo.InvariantCulture, $" x=\"{x:0.##}\"").Append(textYAttribute);
                AppendFontStyle(sb, cell.Bold, cell.Italic, underline: false);
                sb.Append('>').Append(EscapeXml(text)).Append("</text>");
                texts++;
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
            AppendTextStart(sb, group.Fg, context.Fills);
            sb.Append(" x=\"").Append(group.Xs).Append('"').Append(textYAttribute);
            AppendFontStyle(sb, group.Bold, group.Italic, underline: false);
            sb.Append('>').Append(group.Text).Append("</text>");
            texts++;
        }

        return texts;
    }

    /// <summary>
    /// Opens a &lt;text&gt; with its color: nothing for the theme's foreground (inherited), a class
    /// for any other. The class comes first, so a layer of one &lt;text&gt; can add its own classes to it.
    /// </summary>
    private static void AppendTextStart(StringBuilder sb, string fill, FillClasses fills)
    {
        sb.Append("<text");
        if (fills.ClassFor(fill) is { } fillClass)
            sb.Append(" class=\"").Append(fillClass).Append('"');
    }

    // Presentation attributes. Normal weight, upright style, and no decoration are the defaults, so they are not written.
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

    // U+FFFE and U+FFFF (from `cat` of a binary file, for example) are not allowed anywhere in XML,
    // and one of them makes the whole SVG fail to load. Control characters never reach a cell.
    private static bool IsXmlInvalid(char c) => c is '\uFFFE' or '\uFFFF';

    private static string EscapeXml(string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] is '&' or '<' or '>' or '"' || IsXmlInvalid(text[i]))
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
                        case var c when IsXmlInvalid(c): sb.Append('\uFFFD'); break;
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
            return DropZeroLengthFrames(frames);

        if (frames.Count <= 1)
            return frames is List<ReplayFrame> single ? single : [.. frames];

        var normalized = NormalizeTiming(frames, maxFps);
        var reduced = ReduceFrames(normalized, maxFps);
        return SpreadCollapsedFrameTimes(reduced, maxFps);
    }

    /// <summary>
    /// Keeps only the last of frames that share a time. The others would be shown and hidden at the
    /// same offset, so they are never visible, but their layers still cost bytes (output written
    /// several times within one millisecond, as full-screen TUIs do).
    /// </summary>
    private static List<ReplayFrame> DropZeroLengthFrames(IReadOnlyList<ReplayFrame> frames)
    {
        var kept = new List<ReplayFrame>(frames.Count);
        for (var i = 0; i < frames.Count; i++)
        {
            if (i + 1 < frames.Count && HaveSameTime(frames[i + 1].Time, frames[i].Time))
                continue;
            kept.Add(frames[i]);
        }

        return kept;
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

    /// <summary>Whether the cell holds a block element (U+2580–U+259F, shades included), which is drawn as rects.</summary>
    private static bool IsBlockCell(string text) => TryGetBlockCodepoint(text, out _);

    /// <summary>Whether the cell is drawn as a shape rather than text: a block element or a soft font glyph.</summary>
    private static bool IsShapeCell(ScreenCell cell) => cell.Glyph is not null || IsBlockCell(cell.Text);

    private static bool TryGetBlockCodepoint(string text, out int codepoint)
    {
        // A cell may hold a base character plus combining marks, so read the first scalar
        // rather than assuming a second UTF-16 unit is a low surrogate.
        codepoint = 0;
        if (string.IsNullOrEmpty(text) || !Rune.TryGetRuneAt(text, 0, out var first) || first.Value is < 0x2580 or > 0x259F)
            return false;

        codepoint = first.Value;
        return true;
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

        /// <summary>Row layers: the encoded row, unless <see cref="ReuseOf"/> points at an earlier layer with the same content.</summary>
        internal Utf8Segment Content { get; set; }

        internal int? ReuseOf { get; set; }

        /// <summary>Whether a later layer reuses this layer's content (it then needs an id).</summary>
        internal bool Reused { get; set; }

        /// <summary>Row layers: content drawn with its baseline at y=0, moved to its row by the row's class.</summary>
        internal bool RowLocal { get; set; }

        /// <summary>Row layers: the number of elements in <see cref="Content"/>, which is 0 for a row that went blank.</summary>
        internal int ElementCount { get; set; }

        /// <summary>Row layers: <see cref="Content"/> is a single &lt;text&gt;, which can carry the layer's classes itself.</summary>
        internal bool SingleText { get; set; }

        /// <summary>Row layers: the cursor layer drawn inside this one (same row and timing), or -1.</summary>
        internal int Cursor { get; set; } = -1;
    }
}

