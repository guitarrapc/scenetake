using System.Globalization;
using System.Text;

namespace Scenetake.Core;

internal static class SvgRender
{
    /// <param name="stillAt">
    /// Seconds into the recording for a still SVG of the screen shown then, without animation, or
    /// <see cref="double.PositiveInfinity"/> for the final screen. Null renders the animation.
    /// </param>
    /// <returns>
    /// The SVG's size in pixels, as <see cref="Measure"/> gives it, for the canvas this SVG drew: a still's fit looks
    /// at its own screen only.
    /// </returns>
    internal static (double Width, double Height) WriteSvg(
        IReadOnlyList<CastEvent> events,
        int width,
        int height,
        ResolvedRenderSettings render,
        string outputPath,
        double? loopDurationOverride = null,
        double? stillAt = null)
    {
        // Render fully before opening the file, so a render failure leaves no file behind.
        var svg = Render(events, width, height, render, loopDurationOverride, stillAt, out var size);
        using var stream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 1 << 16);
        svg.WriteTo(stream);
        return size;
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

    /// <summary>
    /// The SVG's size in pixels, as its width and height attributes give it: the size a page first shows it at. It
    /// needs no rendering, so the scenario path can report it for a cast that is not converted yet.
    /// </summary>
    internal static (double Width, double Height) Measure(IReadOnlyList<CastEvent> events, int width, int height, ResolvedRenderSettings render)
    {
        var canvas = TerminalReplay.ResolveCanvas(events, width, height, render.Crop, render.Fit, TerminalTheme.FromResolved(render.Theme));
        return SvgFrameRenderer.Measure(render, canvas.Width, canvas.Height);
    }

    /// <summary>A measured size as the CLI prints it: whole pixels, such as <c>830x504 px</c>.</summary>
    internal static string FormatSize((double Width, double Height) size) => string.Create(CultureInfo.InvariantCulture,
        $"{Math.Round(size.Width, MidpointRounding.AwayFromZero):0}x{Math.Round(size.Height, MidpointRounding.AwayFromZero):0} px");

    /// <summary>Renders the SVG as UTF-8 bytes, which the playground hands to the browser without a string.</summary>
    internal static byte[] RenderToUtf8(
        IReadOnlyList<CastEvent> events,
        int width,
        int height,
        ResolvedRenderSettings render,
        double? loopDurationOverride = null,
        double? stillAt = null)
    {
        using var stream = new MemoryStream();
        Render(events, width, height, render, loopDurationOverride, stillAt, out _).WriteTo(stream);
        return stream.ToArray();
    }

    /// <summary>Renders the SVG as a string. <see cref="WriteSvg"/> streams to disk instead.</summary>
    internal static string RenderToString(
        IReadOnlyList<CastEvent> events,
        int width,
        int height,
        ResolvedRenderSettings render,
        double? loopDurationOverride = null,
        double? stillAt = null)
    {
        using var stream = new MemoryStream();
        Render(events, width, height, render, loopDurationOverride, stillAt, out _).WriteTo(stream);
        return SvgDocument.Utf8NoBom.GetString(stream.GetBuffer(), 0, (int)stream.Length);
    }

    /// <summary>
    /// How many seconds an animated SVG plays: one loop, with speed or length, end-hold, and fade-out applied, as
    /// <see cref="Render"/> times it.
    /// </summary>
    internal static double PlayedSeconds(IReadOnlyList<CastEvent> events, ResolvedRenderSettings render, double? loopDurationOverride = null)
    {
        events = RenderSettingsResolver.ApplySpeed(events, render, ref loopDurationOverride);
        return RenderSettingsResolver.ResolveLoopDuration(loopDurationOverride ?? TerminalReplay.ComputeLoopDuration(events), events, render);
    }

    private static SvgDocument Render(
        IReadOnlyList<CastEvent> events,
        int width,
        int height,
        ResolvedRenderSettings render,
        double? loopDurationOverride,
        double? stillAt,
        out (double Width, double Height) size)
    {
        var theme = TerminalTheme.FromResolved(render.Theme);
        var canvas = TerminalReplay.ResolveCanvas(events, width, height, render.Crop, render.Fit, theme, stillAt);
        size = SvgFrameRenderer.Measure(render, canvas.Width, canvas.Height);
        if (stillAt is double at)
        {
            // The one screen shown at that time, drawn from the start: the animation's frames, timing, and fade do
            // not apply. Before the first screen there is nothing to draw but the terminal and its window.
            var all = TerminalReplay.BuildFrames(events, width, height, canvas, theme, lastOfSameTime: true);
            IReadOnlyList<ReplayFrame> still = TerminalReplay.SelectStill(all, at) is { } frame ? [frame.WithTime(0)] : [];
            return SvgFrameRenderer.Render(events, width, height, still, render with { MaxFps = 0, FadeOut = null }, canvas, loopDuration: 1, still: true);
        }

        events = RenderSettingsResolver.ApplySpeed(events, render, ref loopDurationOverride);
        // Without --max-fps only the last of frames that share a time is drawn (DropZeroLengthFrames).
        var frames = TerminalReplay.BuildFrames(events, width, height, canvas, theme, lastOfSameTime: render.MaxFps <= 0);
        var loopDuration = RenderSettingsResolver.ResolveLoopDuration(
            loopDurationOverride ?? TerminalReplay.ComputeLoopDuration(events), events, render);
        return SvgFrameRenderer.Render(events, width, height, frames, render, canvas, loopDuration);
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

    /// <summary>
    /// Text in the window's title bar. A scenario's null leaves it to the cast's title. Read from a cast, it is
    /// <c>st:window-title</c> when the cast has one and the cast's title otherwise. Empty shows no title.
    /// </summary>
    internal string? WindowTitle { get; init; }

    /// <summary>
    /// The recording's own title: the scenario's <c>title</c>, or the cast's. The SVG takes it as its accessible name when
    /// <see cref="WindowTitle"/> is empty.
    /// </summary>
    internal string? Title { get; init; }

    /// <summary>What fills the canvas around the terminal, or null for a transparent canvas.</summary>
    internal BackgroundSpec? Background { get; init; }

    /// <summary>Pixels around the terminal or window, or null for the default.</summary>
    internal int? Margin { get; init; }

    /// <summary>Pixels between the terminal's edge and its text on every side, or null for the default from the font size.</summary>
    internal int? Padding { get; init; }

    /// <summary>
    /// How opaque the terminal's own background and the title bar are, 0 to 1, or null for 1. Shown only over a
    /// background: see <see cref="WindowOpacity"/>.
    /// </summary>
    internal double? Opacity { get; init; }

    /// <summary>How far the background under the window is blurred, as a Gaussian's standard deviation in pixels, or null for none.</summary>
    internal int? Blur { get; init; }

    /// <summary>
    /// The opacity drawn: the set one over a background, else 1. Without a background the window would show
    /// whatever is behind the image, such as a page whose color changes with its light or dark mode.
    /// </summary>
    internal double WindowOpacity => Background is null ? 1 : Opacity ?? 1;

    /// <summary>The blur drawn: the set one when the window lets the background through, else 0.</summary>
    internal int WindowBlur => WindowOpacity < 1 ? Blur ?? 0 : 0;

    /// <summary>Whether the SVG starts again after its end, or null for yes. An mp4 does not loop: a player decides.</summary>
    internal bool? Loop { get; init; }

    /// <summary>
    /// Seconds the final screen stays before the SVG starts again or the mp4 ends, or null for the gap before the
    /// last output (<see cref="TerminalReplay.ComputeLoopDuration"/>).
    /// </summary>
    internal double? EndHold { get; init; }

    /// <summary>Seconds over which the terminal's contents fade out after <see cref="EndHold"/>, or null for none.</summary>
    internal double? FadeOut { get; init; }

    /// <summary>
    /// How much faster than recorded an SVG, mp4, or GIF plays, such as 1.5, or null for as recorded
    /// (<see cref="RenderSettingsResolver.ApplySpeed"/>). <see cref="EndHold"/> and <see cref="FadeOut"/> are seconds of
    /// the output and do not change with it.
    /// </summary>
    internal double? Speed { get; init; }

    /// <summary>The speed played: the set one, or 1.</summary>
    internal double PlaybackSpeed => Speed ?? 1;

    /// <summary>
    /// The most seconds an SVG, mp4, or GIF plays, or null for as recorded (<see cref="PlaybackLength"/>). Not with
    /// <see cref="Speed"/>: each decides how long the output plays.
    /// </summary>
    internal double? Length { get; init; }

    /// <summary>
    /// Where each recorded step's output is among the cast's events, as pairs of its first event and its count, from
    /// the cast's <c>st:steps</c> tag. <see cref="Length"/> needs them. Null for a cast without the tag.
    /// </summary>
    internal int[]? StepRanges { get; init; }

    /// <summary>
    /// Each step <c>id</c> and where its step's output has ended, from the cast's <c>st:ids</c> tag: the screen that
    /// <c>--at</c> with the id shows. Null for a cast without the tag.
    /// </summary>
    internal StepIds? StepIds { get; init; }

    internal bool Loops => Loop ?? true;

    /// <summary>The fade an SVG draws: none when it plays once, because it stops on its final screen.</summary>
    internal double SvgFadeOut => Loops ? FadeOut ?? 0 : 0;

    /// <summary>Cells cut from each edge of the terminal before it is drawn. All zero draws the whole terminal.</summary>
    internal CropSpec Crop { get; init; }

    /// <summary>Which edges move in to the last row and column that the recording shows (<see cref="TerminalReplay.ResolveCanvas"/>).</summary>
    internal FitMode Fit { get; init; }
}

/// <summary>
/// What fills the canvas around the terminal: one color, a gradient of two to four colors from the top-left
/// corner to the bottom-right one, or an image that covers the canvas. A cast header keeps the colors. It
/// keeps no image: a cast names no file of the computer it was recorded on.
/// </summary>
/// <param name="ImagePath">The image file, or in the playground the name the page gave the image. Its extension gives the image type.</param>
/// <param name="ImageBytes">The image, when it is held in memory (the playground). Null reads <paramref name="ImagePath"/>.</param>
internal sealed record BackgroundSpec(IReadOnlyList<string> Colors, string? ImagePath, byte[]? ImageBytes = null)
{
    internal string TagValue => string.Join(' ', Colors);
}

/// <summary>
/// Where a background image comes from: a file relative to a folder (the scenetake command), or an image that the
/// playground page holds by name, because a browser page reads no files.
/// </summary>
internal sealed class BackgroundImageSource
{
    private readonly string? _folder;
    private readonly IReadOnlyDictionary<string, byte[]>? _images;

    private BackgroundImageSource(string? folder, IReadOnlyDictionary<string, byte[]>? images)
    {
        _folder = folder;
        _images = images;
    }

    /// <summary>Files, with a relative path starting from <paramref name="folder"/>.</summary>
    internal static BackgroundImageSource FromFolder(string folder) => new(folder, null);

    /// <summary>
    /// Images in memory, keyed by file name. A path finds the image with its file name: the playground has no
    /// folders, and so a scenario written for the CLI, with <c>../wall.webp</c>, finds <c>wall.webp</c>.
    /// </summary>
    internal static BackgroundImageSource FromImages(IReadOnlyDictionary<string, byte[]> images) => new(null, images);

    internal bool TryFind(string value, out BackgroundSpec background, out string error)
    {
        background = null!;
        error = "";
        if (_images is not null)
        {
            // Both separators: a Windows path is not split by Path.GetFileName on the browser's runtime.
            var name = value[(value.LastIndexOfAny(['/', '\\']) + 1)..];
            if (!RenderSettingsResolver.IsBackgroundImageName(name))
            {
                error = $"background must be colors such as #1e3c72 #2a5298, or a .png, .jpg, .gif, or .webp image: {value}";
                return false;
            }

            if (!_images.TryGetValue(name, out var bytes))
            {
                error = _images.Count == 0
                    ? $"background image not loaded: {name}"
                    : $"background image not loaded: {name} (loaded: {string.Join(", ", _images.Keys.Order(StringComparer.Ordinal))})";
                return false;
            }

            if (bytes.LongLength > RenderSettingsResolver.MaxBackgroundImageBytes)
            {
                error = $"background image is larger than {RenderSettingsResolver.MaxBackgroundImageBytes / (1024 * 1024)} MB, and every SVG embeds it: {name}";
                return false;
            }

            background = new BackgroundSpec([], name, bytes);
            return true;
        }

        string path;
        try
        {
            path = Path.GetFullPath(value, _folder!);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            error = $"background is neither colors nor a valid path: {value}";
            return false;
        }

        if (!RenderSettingsResolver.IsBackgroundImageName(path))
        {
            error = $"background must be colors such as #1e3c72 #2a5298, or a .png, .jpg, .gif, or .webp file: {value}";
            return false;
        }

        var info = new FileInfo(path);
        if (!info.Exists)
        {
            error = Path.IsPathFullyQualified(value)
                ? $"background image not found: {value}"
                : $"background image not found: {value} (resolved to {path})";
            return false;
        }

        if (info.Length > RenderSettingsResolver.MaxBackgroundImageBytes)
        {
            error = $"background image is larger than {RenderSettingsResolver.MaxBackgroundImageBytes / (1024 * 1024)} MB, and every SVG embeds it: {value}";
            return false;
        }

        background = new BackgroundSpec([], path);
        return true;
    }
}

/// <summary>
/// Window title, background, margin, padding, opacity, blur, loop, end hold, fade-out, speed, and length from the
/// command line or the playground: each set one replaces the cast's or the scenario's. Speed and length each decide
/// how long the output plays, so the one set replaces the other too.
/// </summary>
internal sealed record RenderStyleOverrides(
    string? WindowTitle,
    BackgroundSpec? Background,
    int? Margin,
    int? Padding,
    double? Opacity = null,
    int? Blur = null,
    bool? Loop = null,
    double? EndHold = null,
    double? FadeOut = null,
    FitMode? Fit = null,
    int? CropTop = null,
    int? CropBottom = null,
    int? CropLeft = null,
    int? CropRight = null,
    double? Speed = null,
    double? Length = null)
{
    internal static readonly RenderStyleOverrides None = new(null, null, null, null);

    internal ResolvedRenderSettings Apply(ResolvedRenderSettings settings) => settings with
    {
        WindowTitle = WindowTitle ?? settings.WindowTitle,
        Background = Background ?? settings.Background,
        Margin = Margin ?? settings.Margin,
        Padding = Padding ?? settings.Padding,
        Opacity = Opacity ?? settings.Opacity,
        Blur = Blur ?? settings.Blur,
        Loop = Loop ?? settings.Loop,
        EndHold = EndHold ?? settings.EndHold,
        FadeOut = FadeOut ?? settings.FadeOut,
        Speed = Length is not null ? null : Speed ?? settings.Speed,
        Length = Speed is not null ? null : Length ?? settings.Length,
        Fit = Fit ?? settings.Fit,
        Crop = new CropSpec(CropTop ?? settings.Crop.Top, CropBottom ?? settings.Crop.Bottom, CropLeft ?? settings.Crop.Left, CropRight ?? settings.Crop.Right),
    };
}

internal readonly record struct ResolvedTheme(string Fg, string Bg, string Palette);

internal static class RenderSettingsResolver
{
    internal const int MinFontSize = 1, MaxFontSize = 128;
    internal const int MinTerminalCols = 1, MaxTerminalCols = 512;
    internal const int MinTerminalRows = 1, MaxTerminalRows = 512;

    /// <summary>
    /// The terminal size when a scenario leaves out width or height. 80 columns make an SVG 830 pixels wide at the
    /// default font size, which a README shows at its own size. 120 columns made it 1226 pixels, shown at about two
    /// thirds with 11-pixel text.
    /// </summary>
    internal const int DefaultTerminalCols = 80, DefaultTerminalRows = 24;
    internal const int DefaultFontSize = 16;
    internal const int DefaultMaxFps = 0;
    internal const int MinMaxFps = 0, MaxMaxFps = 120;
    // The count must fit DefaultFontFamily (11 families), which every cast header records.
    // At 10, scenetake's own casts failed validation on read and warned on every svg/mp4 conversion.
    internal const int MaxFontFamilyLength = 256, MaxFontFamilyCount = 16;
    internal const string FontSizeTagPrefix = "st:font-size=";
    internal const string FontFamilyTagPrefix = "st:font-family=";
    internal const string WindowTagPrefix = "st:window=";
    internal const string WindowTitleTagPrefix = "st:window-title=";
    internal const string BackgroundTagPrefix = "st:background=";
    internal const string MarginTagPrefix = "st:margin=";
    internal const string PaddingTagPrefix = "st:padding=";
    internal const string OpacityTagPrefix = "st:opacity=";
    internal const string BlurTagPrefix = "st:blur=";
    internal const string LoopTagPrefix = "st:loop=";
    internal const string EndHoldTagPrefix = "st:end-hold=";
    internal const string FadeOutTagPrefix = "st:fade-out=";
    internal const string SpeedTagPrefix = "st:speed=";
    internal const string LengthTagPrefix = "st:length=";
    internal const string IdsTagPrefix = "st:ids=";
    internal const string FitTagPrefix = "st:fit=";
    internal const string CropTopTagPrefix = "st:crop-top=";
    internal const string CropBottomTagPrefix = "st:crop-bottom=";
    internal const string CropLeftTagPrefix = "st:crop-left=";
    internal const string CropRightTagPrefix = "st:crop-right=";
    internal const int MinCrop = 0, MaxCrop = MaxTerminalCols - 1;
    internal const string ExpectedFitNames = "none, height, width, both";

    /// <summary>
    /// The fit of a scenario without <c>render.fit</c> and of a cast without <c>st:fit</c>: the rows below the last one
    /// the recording shows are left out, and the width stays the terminal's.
    /// </summary>
    internal const FitMode DefaultFit = FitMode.Height;
    internal const int MinMargin = 0, MaxMargin = 200;
    internal const int MinPadding = 0, MaxPadding = 100;
    internal const double MinOpacity = 0, MaxOpacity = 1;
    internal const int MinBlur = 0, MaxBlur = 50;
    internal const double MinEndHold = 0, MaxEndHold = 60;
    internal const double MinFadeOut = 0, MaxFadeOut = 10;

    /// <summary>
    /// The speeds a recording plays at. Typing at 4x is already a blur, and a quarter of the speed is enough to follow
    /// a full-screen program that redraws fast.
    /// </summary>
    internal const double MinSpeed = 0.25, MaxSpeed = 4;

    /// <summary>The lengths a recording plays in at most, in seconds: up to X's 140 for a free account and well past it.</summary>
    internal const double MinLength = 1, MaxLength = 600;

    /// <summary>The margin when a background is set and no margin is: enough of the background to show around the window.</summary>
    internal const int DefaultBackgroundMargin = 32;
    internal const int MaxBackgroundColors = 4;

    /// <summary>Every SVG embeds the image, so a large photo makes every recording large.</summary>
    internal const long MaxBackgroundImageBytes = 8L * 1024 * 1024;
    internal const int MaxWindowTitleLength = 256;
    private static readonly string[] BackgroundImageExtensions = [".png", ".jpg", ".jpeg", ".gif", ".webp"];
    internal const string DarkName = "dark", LightName = "light";
    internal const string MacosWindowName = "macos", WindowsWindowName = "windows", NoneWindowName = "none";
    internal static string ExpectedPresetNames => string.Join(", ", PresetNames);
    internal static string ExpectedWindowNames => $"{NoneWindowName}|{MacosWindowName}|{WindowsWindowName}";
    internal const string DefaultFontFamily =
        "ui-monospace, \"Cascadia Mono\", \"Cascadia Code\", \"JetBrains Mono\", \"Noto Sans Mono\", SFMono-Regular, Menlo, Monaco, Consolas, \"Liberation Mono\", monospace";

    private static readonly ResolvedTheme DarkTheme = new(
        "#d0d0d0", "#282c34",
        "#151515:#ac4142:#7e8e50:#e5b567:#6c99bb:#9f4e85:#7dd6cf:#d0d0d0:#505050:#ac4142:#7e8e50:#e5b567:#6c99bb:#9f4e85:#7dd6cf:#f5f5f5");
    private static readonly ResolvedTheme LightTheme = new(
        "#383838", "#fafafa",
        "#383838:#c82828:#548b2e:#a88800:#2871aa:#9a4a96:#008787:#585858:#686868:#e74c3c:#69a845:#d4a017:#3498db:#c678dd:#20b2aa:#fafafa");

    /// <summary>
    /// The named color themes, in the order that help, errors, and the docs list them. Each is the terminal palette
    /// that its project publishes, in ANSI order (black, red, green, yellow, blue, magenta, cyan, white, then the
    /// bright eight), all under the MIT license:
    /// <list type="bullet">
    /// <item>dracula: dracula/windows-terminal, dracula.json</item>
    /// <item>nord: nordtheme/xresources, src/nord</item>
    /// <item>one-dark: joshdick/onedark.vim, term/One Dark.Xresources</item>
    /// <item>gruvbox-dark: morhetz/gruvbox-contrib, xresources/gruvbox-dark.xresources</item>
    /// <item>catppuccin-mocha: catppuccin/windows-terminal, mocha.json</item>
    /// <item>tokyo-night: folke/tokyonight.nvim, extras/windows_terminal/tokyonight_night.json</item>
    /// <item>solarized-dark and solarized-light: altercation/solarized, xresources/solarized, with the light base
    /// tones that file gives. Its bright colors are the base tones, as Solarized defines them.</item>
    /// <item>github-dark and github-light: @primer/primitives 11.10.0, dist/css/functional/themes, the --ansi-*
    /// colors with fgColor-default and bgColor-default</item>
    /// <item>macos-dark and macos-light: mbadolato/iTerm2-Color-Schemes, windowsterminal/Apple System Colors.json and
    /// Apple System Colors Light.json, macOS's system colors as a terminal palette. Apple publishes no palette file
    /// for Terminal.app.</item>
    /// </list>
    /// </summary>
    private static readonly (string Name, ResolvedTheme Theme)[] Presets =
    [
        (DarkName, DarkTheme),
        (LightName, LightTheme),
        ("macos-dark", new("#ffffff", "#1e1e1e",
            "#1a1a1a:#cc372e:#26a439:#cdac08:#0869cb:#9647bf:#479ec2:#98989d:#464646:#ff453a:#32d74b:#ffd60a:#0a84ff:#bf5af2:#76d6ff:#ffffff")),
        ("macos-light", new("#000000", "#feffff",
            "#1a1a1a:#cc372e:#26a439:#cdac08:#0869cb:#9647bf:#479ec2:#98989d:#464646:#ff453a:#32d74b:#e5bc00:#0a84ff:#bf5af2:#69c9f2:#ffffff")),
        ("dracula", new("#f8f8f2", "#282a36",
            "#21222c:#ff5555:#50fa7b:#f1fa8c:#bd93f9:#ff79c6:#8be9fd:#f8f8f2:#6272a4:#ff6e6e:#69ff94:#ffffa5:#d6acff:#ff92df:#a4ffff:#ffffff")),
        ("nord", new("#d8dee9", "#2e3440",
            "#3b4252:#bf616a:#a3be8c:#ebcb8b:#81a1c1:#b48ead:#88c0d0:#e5e9f0:#4c566a:#bf616a:#a3be8c:#ebcb8b:#81a1c1:#b48ead:#8fbcbb:#eceff4")),
        ("one-dark", new("#abb2bf", "#282c34",
            "#2c323c:#e06c75:#98c379:#e5c07b:#61afef:#c678dd:#56b6c2:#5c6370:#3e4452:#e06c75:#98c379:#e5c07b:#61afef:#c678dd:#56b6c2:#abb2bf")),
        ("gruvbox-dark", new("#ebdbb2", "#282828",
            "#282828:#cc241d:#98971a:#d79921:#458588:#b16286:#689d6a:#a89984:#928374:#fb4934:#b8bb26:#fabd2f:#83a598:#d3869b:#8ec07c:#ebdbb2")),
        ("catppuccin-mocha", new("#cdd6f4", "#1e1e2e",
            "#45475a:#f38ba8:#a6e3a1:#f9e2af:#89b4fa:#f5c2e7:#94e2d5:#bac2de:#585b70:#f38ba8:#a6e3a1:#f9e2af:#89b4fa:#f5c2e7:#94e2d5:#a6adc8")),
        ("tokyo-night", new("#c0caf5", "#1a1b26",
            "#15161e:#f7768e:#9ece6a:#e0af68:#7aa2f7:#bb9af7:#7dcfff:#a9b1d6:#414868:#f7768e:#9ece6a:#e0af68:#7aa2f7:#9d7cd8:#7dcfff:#c0caf5")),
        ("solarized-dark", new("#839496", "#002b36",
            "#073642:#dc322f:#859900:#b58900:#268bd2:#d33682:#2aa198:#eee8d5:#002b36:#cb4b16:#586e75:#657b83:#839496:#6c71c4:#93a1a1:#fdf6e3")),
        ("solarized-light", new("#657b83", "#fdf6e3",
            "#eee8d5:#dc322f:#859900:#b58900:#268bd2:#d33682:#2aa198:#073642:#fdf6e3:#cb4b16:#93a1a1:#839496:#657b83:#6c71c4:#586e75:#002b36")),
        ("github-dark", new("#f0f6fc", "#0d1117",
            "#2f3742:#ff7b72:#3fb950:#d29922:#58a6ff:#be8fff:#39c5cf:#f0f6fc:#656c76:#ffa198:#56d364:#e3b341:#79c0ff:#d2a8ff:#56d4dd:#ffffff")),
        ("github-light", new("#1f2328", "#ffffff",
            "#1f2328:#cf222e:#116329:#4d2d00:#0969da:#8250df:#1b7c83:#59636e:#393f46:#a40e26:#1a7f37:#633c01:#218bff:#a475f9:#3192aa:#818b98")),
    ];

    /// <summary>The named color themes, in their listed order.</summary>
    internal static IEnumerable<string> PresetNames => Presets.Select(static preset => preset.Name);

    internal static string DefaultFg => DarkTheme.Fg;
    internal static string DefaultBg => DarkTheme.Bg;
    internal static string DefaultPalette => DarkTheme.Palette;

    internal static bool IsValidTerminalSize(int cols, int rows) =>
        cols is >= MinTerminalCols and <= MaxTerminalCols &&
        rows is >= MinTerminalRows and <= MaxTerminalRows;

    internal static bool TryGetPreset(string name, out ResolvedTheme theme)
    {
        var trimmed = name.Trim();
        foreach (var preset in Presets)
        {
            if (string.Equals(preset.Name, trimmed, StringComparison.OrdinalIgnoreCase))
            {
                theme = preset.Theme;
                return true;
            }
        }

        theme = default;
        return false;
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

    internal static bool TryParseMargin(string text, out int margin, out string error) =>
        TryParsePixels(text, "margin", MinMargin, MaxMargin, out margin, out error);

    internal static bool TryParsePadding(string text, out int padding, out string error) =>
        TryParsePixels(text, "padding", MinPadding, MaxPadding, out padding, out error);

    internal static bool IsValidMargin(int margin) => margin is >= MinMargin and <= MaxMargin;

    internal static bool IsValidPadding(int padding) => padding is >= MinPadding and <= MaxPadding;

    internal static bool TryParseBlur(string text, out int blur, out string error) =>
        TryParsePixels(text, "blur", MinBlur, MaxBlur, out blur, out error);

    internal static bool IsValidBlur(int blur) => blur is >= MinBlur and <= MaxBlur;

    internal static bool IsValidOpacity(double opacity) => opacity is >= MinOpacity and <= MaxOpacity;

    internal static bool TryParseOpacity(string text, out double opacity, out string error)
    {
        error = "";
        if (!double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out opacity) || !double.IsFinite(opacity))
        {
            error = $"invalid opacity value: {text} (expected a number from 0 to 1, such as 0.85)";
            return false;
        }

        if (!IsValidOpacity(opacity))
        {
            error = $"opacity must be between 0 and 1: {text.Trim()}";
            return false;
        }

        return true;
    }

    /// <summary>An opacity as it is written in a cast tag and an SVG attribute: up to three decimals, no exponent.</summary>
    internal static string FormatOpacity(double opacity) => opacity.ToString("0.###", CultureInfo.InvariantCulture);

    /// <summary>Reads <c>true</c> or <c>false</c>, as a cast's <c>st:loop</c> tag holds it.</summary>
    internal static bool TryParseLoop(string text, out bool loop, out string error)
    {
        error = "";
        if (bool.TryParse(text.Trim(), out loop))
            return true;

        error = $"invalid loop value: {text} (expected true or false)";
        return false;
    }

    internal static bool TryParseFit(string text, out FitMode fit, out string error)
    {
        error = "";
        switch (text.Trim().ToLowerInvariant())
        {
            case "none":
                fit = FitMode.None;
                return true;
            case "height":
                fit = FitMode.Height;
                return true;
            case "width":
                fit = FitMode.Width;
                return true;
            case "both":
                fit = FitMode.Both;
                return true;
            default:
                fit = FitMode.None;
                error = $"unknown fit: {text} (expected: {ExpectedFitNames})";
                return false;
        }
    }

    internal static string FitToTagValue(FitMode fit) => fit switch
    {
        FitMode.Height => "height",
        FitMode.Width => "width",
        FitMode.Both => "both",
        _ => "none",
    };

    /// <summary>Reads cells to cut from one edge, as <c>--crop-top</c> and the others take them.</summary>
    internal static bool TryParseCrop(string text, string name, out int cells, out string error)
    {
        error = "";
        if (!int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out cells))
        {
            error = $"invalid {name} value: {text} (expected whole cells from {MinCrop} to {MaxCrop})";
            return false;
        }

        if (!IsValidCrop(cells))
        {
            error = $"{name} must be between {MinCrop} and {MaxCrop} cells: {cells}";
            return false;
        }

        return true;
    }

    internal static bool IsValidCrop(int cells) => cells is >= MinCrop and <= MaxCrop;

    internal static bool TryParseEndHold(string text, out double seconds, out string error) =>
        TryParseSeconds(text, "end-hold", MinEndHold, MaxEndHold, out seconds, out error);

    internal static bool TryParseFadeOut(string text, out double seconds, out string error) =>
        TryParseSeconds(text, "fade-out", MinFadeOut, MaxFadeOut, out seconds, out error);

    internal static bool IsValidEndHold(double seconds) => double.IsFinite(seconds) && seconds is >= MinEndHold and <= MaxEndHold;

    internal static bool IsValidFadeOut(double seconds) => double.IsFinite(seconds) && seconds is >= MinFadeOut and <= MaxFadeOut;

    /// <summary>Reads a playback speed, as <c>--speed</c> and <c>st:speed</c> hold it: a factor such as 1.5.</summary>
    internal static bool TryParseSpeed(string text, out double speed, out string error)
    {
        error = "";
        // A trailing x, as the CLI writes a speed (1.5x), is read too.
        var number = text.Trim();
        if (number.EndsWith('x') || number.EndsWith('X'))
            number = number[..^1];
        if (!double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out speed) || !double.IsFinite(speed))
        {
            error = $"invalid speed value: {text} (expected a factor from {FormatSeconds(MinSpeed)} to {FormatSeconds(MaxSpeed)}, such as 1.5 for one and a half times as fast)";
            return false;
        }

        if (!IsValidSpeed(speed))
        {
            error = $"speed must be between {FormatSeconds(MinSpeed)} and {FormatSeconds(MaxSpeed)}: {text.Trim()}";
            return false;
        }

        return true;
    }

    internal static bool IsValidSpeed(double speed) => double.IsFinite(speed) && speed is >= MinSpeed and <= MaxSpeed;

    /// <summary>Reads a length, as <c>--length</c> and <c>st:length</c> hold it: seconds such as 15, and a trailing s is read too.</summary>
    internal static bool TryParseLength(string text, out double seconds, out string error)
    {
        var number = text.Trim();
        if (number.EndsWith('s') || number.EndsWith('S'))
            number = number[..^1];
        return TryParseSeconds(number, "length", MinLength, MaxLength, out seconds, out error);
    }

    internal static bool IsValidLength(double seconds) => double.IsFinite(seconds) && seconds is >= MinLength and <= MaxLength;

    /// <summary>A speed as the CLI shows it, such as <c>1.5x</c>.</summary>
    internal static string FormatSpeed(double speed) => FormatSeconds(speed) + "x";

    /// <summary>
    /// How long an output plays, as the CLI's Written lines and the playground show it: <c>12.0s</c>, with the speed
    /// when it is not the recorded one (<c>8.0s at 1.5x</c>), or with the length it was fitted to (<c>15.0s, length 15s</c>).
    /// </summary>
    internal static string FormatPlayed(double seconds, ResolvedRenderSettings render) =>
        render.Length is double length
            ? string.Create(CultureInfo.InvariantCulture, $"{seconds:F1}s, length {FormatSeconds(length)}s")
            : render.PlaybackSpeed == 1
            ? string.Create(CultureInfo.InvariantCulture, $"{seconds:F1}s")
            : string.Create(CultureInfo.InvariantCulture, $"{seconds:F1}s at {FormatSpeed(render.PlaybackSpeed)}");

    /// <summary>Seconds as a cast tag holds them: up to three decimals, no exponent.</summary>
    internal static string FormatSeconds(double seconds) => seconds.ToString("0.###", CultureInfo.InvariantCulture);

    private static bool TryParseSeconds(string text, string name, double min, double max, out double seconds, out string error)
    {
        error = "";
        if (!double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out seconds) || !double.IsFinite(seconds))
        {
            error = $"invalid {name} value: {text} (expected seconds from {FormatSeconds(min)} to {FormatSeconds(max)}, such as 1.5)";
            return false;
        }

        if (seconds < min || seconds > max)
        {
            error = $"{name} must be between {FormatSeconds(min)} and {FormatSeconds(max)} seconds: {text.Trim()}";
            return false;
        }

        return true;
    }

    /// <summary>
    /// The loop of a recording with its render settings: the playback to the last event, then <c>end-hold</c> (or
    /// the default hold already in <paramref name="loopDuration"/>), then <c>fade-out</c>. The SVG's loop and the
    /// mp4's length.
    /// </summary>
    /// <param name="loopDuration">The loop with the default hold, <see cref="TerminalReplay.ComputeLoopDuration"/>.</param>
    internal static double ResolveLoopDuration(double loopDuration, IReadOnlyList<CastEvent> events, ResolvedRenderSettings settings)
    {
        if (events.Count == 0)
            return loopDuration;

        var end = settings.EndHold is double hold ? events[^1].Time + hold : loopDuration;
        return end + (settings.FadeOut ?? 0);
    }

    /// <summary>
    /// The recording on the timeline that <see cref="ResolvedRenderSettings.Speed"/> plays: each event's time, and the
    /// loop with the default hold, divided by the speed. Typing, pauses, and output all go faster together, so the
    /// demo keeps its rhythm. An explicit end hold and the fade-out are added afterwards in seconds of the output
    /// (<see cref="ResolveLoopDuration"/>): they say how long a viewer sees the final screen. A still is not played,
    /// and its time stays the recording's. With <see cref="ResolvedRenderSettings.Length"/>, the recording fitted to
    /// that length instead (<see cref="PlaybackLength"/>).
    /// </summary>
    internal static IReadOnlyList<CastEvent> ApplySpeed(IReadOnlyList<CastEvent> events, ResolvedRenderSettings settings, ref double? loopDuration)
    {
        events = TerminalReplay.WithoutHoldEvent(events, settings.EndHold);
        if (FitLength(events, settings, loopDuration) is { } fitted)
        {
            loopDuration = fitted.LoopDuration;
            return fitted.Events;
        }

        var speed = settings.PlaybackSpeed;
        if (speed == 1)
            return events;

        var scaled = new CastEvent[events.Count];
        for (var i = 0; i < scaled.Length; i++)
            scaled[i] = events[i] with { Time = events[i].Time / speed };
        if (loopDuration is double loop)
            loopDuration = loop / speed;
        return scaled;
    }

    /// <summary>
    /// The recording fitted to <see cref="ResolvedRenderSettings.Length"/>, or null without a length, and for a cast
    /// without step ranges, which plays as recorded (<see cref="LengthNote"/> says so).
    /// </summary>
    internal static PlaybackLength.Result? FitLength(IReadOnlyList<CastEvent> events, ResolvedRenderSettings settings, double? loopDuration) =>
        settings.Length is double length && settings.StepRanges is { } steps
            ? PlaybackLength.Fit(TerminalReplay.WithoutHoldEvent(events, settings.EndHold), steps, loopDuration ?? TerminalReplay.ComputeLoopDuration(events), settings.EndHold, settings.FadeOut ?? 0, length)
            : null;

    /// <summary>
    /// How much faster than recorded the output plays, for the default frame rate of an mp4 and a GIF: the speed, or
    /// with a length the recorded length over the played one.
    /// </summary>
    internal static double FrameSpeed(IReadOnlyList<CastEvent> events, ResolvedRenderSettings settings, double? loopDuration) =>
        FitLength(events, settings, loopDuration) is { } fitted && fitted.Played > 0 ? Math.Max(1, fitted.Natural / fitted.Played) : settings.PlaybackSpeed;

    /// <summary>
    /// A warning for a length that the output does not keep, or null: a recording that is longer at its shortest, and
    /// a cast without step ranges, written by another tool or an earlier scenetake, which plays as recorded. The first
    /// names the steps that take longest at the shortest pace: by their number in the time per step when the
    /// scenario's run gives <paramref name="names"/>, and otherwise by the command each one types.
    /// </summary>
    internal static string? LengthNote(IReadOnlyList<CastEvent> events, ResolvedRenderSettings settings, double? loopDuration, StepNames? names = null)
    {
        if (settings.Length is not double length)
            return null;
        if (settings.StepRanges is not { } stepRanges)
            return $"length {FormatSeconds(length)}s has no effect: the cast has no step ranges (st:steps), which scenetake writes when it records. It plays as recorded";
        return FitLength(events, settings, loopDuration) is { } fitted && fitted.Played > length + 0.05
            ? string.Create(CultureInfo.InvariantCulture, $"length {FormatSeconds(length)}s is too short for this recording, which takes at least {fitted.Played:F1}s.{LongestSteps(PlaybackLength.StepsAtShortest(stepRanges, fitted, names))} Leave out steps or output, or set speed instead")
            : null;
    }

    /// <summary>
    /// The steps that take longest at their shortest, up to three, as a sentence that starts with a space, or nothing
    /// for a recording of one step.
    /// </summary>
    private static string LongestSteps(PlaybackLength.StepShortest[] steps)
    {
        if (steps.Length < 2)
            return "";

        // Longest first, and in the order of the recording where two are as long.
        var taken = new bool[steps.Length];
        var text = new StringBuilder(" Longest steps then: ");
        var shown = Math.Min(3, steps.Length);
        for (var k = 0; k < shown; k++)
        {
            var longest = -1;
            for (var i = 0; i < steps.Length; i++)
            {
                if (!taken[i] && (longest < 0 || steps[i].Seconds > steps[longest].Seconds))
                    longest = i;
            }

            taken[longest] = true;
            var step = steps[longest];
            if (k > 0)
                text.Append(", ");
            text.Append(CultureInfo.InvariantCulture, $"{step.Label} ({step.Seconds:F1}s)");
        }

        return text.Append('.').ToString();
    }

    /// <summary>
    /// Settings that are set but have no effect, one message each: opacity or blur without a background, blur on an
    /// opaque window, and, for an SVG, a fade-out on one that plays once. Both the CLI and the playground show them
    /// as warnings.
    /// </summary>
    /// <param name="svg">Whether an SVG is written, where a fade-out needs the loop.</param>
    /// <param name="svg">Whether the output is an animated SVG, or a cast that keeps the settings for one.</param>
    /// <param name="gif">Whether the output is a GIF, which plays once without a fade as an SVG does.</param>
    internal static List<string> FindIneffectiveStyle(ResolvedRenderSettings settings, bool svg = true, bool gif = false)
    {
        var notes = new List<string>();
        var translucent = settings.Opacity is double opacity && opacity < 1;
        var blurred = settings.Blur is int blur && blur > 0;
        if (settings.Background is null && (translucent || blurred))
            notes.Add($"{(translucent && blurred ? "opacity and blur have" : translucent ? "opacity has" : "blur has")} no effect without a background: the window stays opaque");
        else if (blurred && !translucent)
            notes.Add("blur has no effect on an opaque window: set an opacity below 1, such as 0.85");
        if (svg && !settings.Loops && settings.FadeOut is > 0)
            notes.Add("fade-out has no effect on an SVG that plays once: it stops on its final screen (an mp4 still fades)");
        if (gif && !settings.Loops && settings.FadeOut is > 0)
            notes.Add("fade-out has no effect on a GIF that plays once: it stops on its final screen");
        return notes;
    }

    private static bool TryParsePixels(string text, string name, int min, int max, out int value, out string error)
    {
        error = "";
        if (!int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
        {
            error = $"invalid {name} value: {text} (expected whole pixels from {min} to {max})";
            return false;
        }

        if (value < min || value > max)
        {
            error = $"{name} must be between {min} and {max} pixels: {value}";
            return false;
        }

        return true;
    }

    /// <summary>A name with an image extension that a background takes.</summary>
    internal static bool IsBackgroundImageName(string name) =>
        BackgroundImageExtensions.Contains(Path.GetExtension(name).ToLowerInvariant());

    /// <summary>
    /// Reads a background: colors (<c>#rgb</c> or <c>#rrggbb</c>, separated by spaces or commas) or an image.
    /// </summary>
    /// <param name="images">Where an image is found, or null where a background takes colors only: a cast header.</param>
    internal static bool TryParseBackground(string text, BackgroundImageSource? images, out BackgroundSpec background, out string error)
    {
        background = null!;
        error = "";
        var value = text.Trim();
        if (value.Length == 0)
        {
            error = "background requires a value";
            return false;
        }

        var tokens = value.Split([' ', ',', '\t'], StringSplitOptions.RemoveEmptyEntries);
        if (tokens.All(static token => token.StartsWith('#')))
        {
            if (tokens.Length > MaxBackgroundColors)
            {
                error = $"background takes at most {MaxBackgroundColors} colors: {value}";
                return false;
            }

            var colors = new List<string>(tokens.Length);
            foreach (var token in tokens)
            {
                if (!CastReader.TryParseHexColor(token, out var color))
                {
                    error = $"invalid background color (expected #rgb or #rrggbb): {token}";
                    return false;
                }

                // #rgb becomes #rrggbb: the mp4 rasterizer reads only the long form.
                colors.Add((color.Length == 4 ? $"#{color[1]}{color[1]}{color[2]}{color[2]}{color[3]}{color[3]}" : color).ToLowerInvariant());
            }

            background = new BackgroundSpec(colors, null);
            return true;
        }

        if (images is null)
        {
            error = $"background here takes colors only, such as #1e3c72 #2a5298: {value}";
            return false;
        }

        return images.TryFind(value, out background, out error);
    }

    /// <summary>A window title without control characters, at most <see cref="MaxWindowTitleLength"/> characters.</summary>
    internal static string NormalizeWindowTitle(string text)
    {
        var builder = new StringBuilder(Math.Min(text.Length, MaxWindowTitleLength));
        foreach (var c in text)
        {
            if (builder.Length == MaxWindowTitleLength)
                break;
            if (!char.IsControl(c))
                builder.Append(c);
        }

        return builder.ToString().Trim();
    }

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

    /// <param name="images">
    /// Where a <c>render.background</c> image is found: the scenario file's folder on the scenetake command, the
    /// page's images in the playground. Null takes background colors only.
    /// </param>
    internal static bool TryResolve(
        Scenario scenario,
        string? cliThemePreset,
        out ResolvedRenderSettings settings,
        out string error,
        BackgroundImageSource? images = null)
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

        settings = new ResolvedRenderSettings(fontSize, fontFamily, theme, window)
        {
            Fit = DefaultFit,
            Title = scenario.Title is { } title ? NormalizeWindowTitle(title) : null,
        };
        if (render?.WindowTitle is { } windowTitle)
            settings = settings with { WindowTitle = NormalizeWindowTitle(windowTitle) };

        if (!string.IsNullOrWhiteSpace(render?.Background))
        {
            if (!TryParseBackground(render!.Background!, images, out var background, out error))
            {
                error = $"invalid render.background: {error}";
                return false;
            }

            settings = settings with { Background = background };
        }

        if (render?.Margin is int margin)
        {
            if (!IsValidMargin(margin))
            {
                error = $"invalid render.margin: must be between {MinMargin} and {MaxMargin} pixels: {margin}";
                return false;
            }

            settings = settings with { Margin = margin };
        }

        if (render?.Padding is int padding)
        {
            if (!IsValidPadding(padding))
            {
                error = $"invalid render.padding: must be between {MinPadding} and {MaxPadding} pixels: {padding}";
                return false;
            }

            settings = settings with { Padding = padding };
        }

        if (render?.Opacity is double opacityValue)
        {
            if (!double.IsFinite(opacityValue) || !IsValidOpacity(opacityValue))
            {
                error = $"invalid render.opacity: must be between 0 and 1: {opacityValue.ToString(CultureInfo.InvariantCulture)}";
                return false;
            }

            settings = settings with { Opacity = opacityValue };
        }

        if (render?.Blur is int blurValue)
        {
            if (!IsValidBlur(blurValue))
            {
                error = $"invalid render.blur: must be between {MinBlur} and {MaxBlur} pixels: {blurValue}";
                return false;
            }

            settings = settings with { Blur = blurValue };
        }

        if (render?.Loop is bool loop)
            settings = settings with { Loop = loop };

        if (render?.EndHold is double endHold)
        {
            if (!IsValidEndHold(endHold))
            {
                error = $"invalid render.end-hold: must be between {FormatSeconds(MinEndHold)} and {FormatSeconds(MaxEndHold)} seconds: {endHold.ToString(CultureInfo.InvariantCulture)}";
                return false;
            }

            settings = settings with { EndHold = endHold };
        }

        if (render?.FadeOut is double fadeOut)
        {
            if (!IsValidFadeOut(fadeOut))
            {
                error = $"invalid render.fade-out: must be between {FormatSeconds(MinFadeOut)} and {FormatSeconds(MaxFadeOut)} seconds: {fadeOut.ToString(CultureInfo.InvariantCulture)}";
                return false;
            }

            settings = settings with { FadeOut = fadeOut };
        }

        if (render?.Speed is double speed)
        {
            if (!IsValidSpeed(speed))
            {
                error = $"invalid render.speed: must be between {FormatSeconds(MinSpeed)} and {FormatSeconds(MaxSpeed)}: {speed.ToString(CultureInfo.InvariantCulture)}";
                return false;
            }

            settings = settings with { Speed = speed };
        }

        if (render?.Length is double length)
        {
            if (!IsValidLength(length))
            {
                error = $"invalid render.length: must be between {FormatSeconds(MinLength)} and {FormatSeconds(MaxLength)} seconds: {length.ToString(CultureInfo.InvariantCulture)}";
                return false;
            }

            if (render.Speed is not null)
            {
                error = "render.speed and render.length both set how long the output plays: keep one";
                return false;
            }

            settings = settings with { Length = length };
        }

        if (render?.Fit is { } fitText)
        {
            if (!TryParseFit(fitText, out var fit, out error))
            {
                error = $"invalid render.fit: {error}";
                return false;
            }

            settings = settings with { Fit = fit };
        }

        int[] crop = [render?.CropTop ?? 0, render?.CropBottom ?? 0, render?.CropLeft ?? 0, render?.CropRight ?? 0];
        string[] cropKeys = ["crop-top", "crop-bottom", "crop-left", "crop-right"];
        for (var i = 0; i < crop.Length; i++)
        {
            if (!IsValidCrop(crop[i]))
            {
                error = $"invalid render.{cropKeys[i]}: must be between {MinCrop} and {MaxCrop} cells: {crop[i]}";
                return false;
            }
        }

        settings = settings with { Crop = new CropSpec(crop[0], crop[1], crop[2], crop[3]) };
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

    /// <summary>The settings read back from a cast, with the run's background image: the cast keeps background colors only.</summary>
    internal static ResolvedRenderSettings WithImageBackground(ResolvedRenderSettings fromCast, ResolvedRenderSettings run) =>
        run.Background?.ImagePath is not null ? fromCast with { Background = run.Background } : fromCast;

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

    internal static string ResolveCastPngOutputPath(string castPath, string? outputArg) =>
        ResolveOutputPath(castPath, outputArg, ".png");

    internal static string ResolveCastGifOutputPath(string castPath, string? outputArg) =>
        ResolveOutputPath(castPath, outputArg, ".gif");

    /// <summary>
    /// <c>--at</c>: seconds from the start of the recording, 0 or more with decimals, <c>end</c> for the final screen
    /// (<see cref="double.PositiveInfinity"/>), or a step's <c>id</c>, which the cast gives a time
    /// (<see cref="TryResolveStillAt"/>). A time past the end gives the final screen too.
    /// </summary>
    internal static bool TryParseStillAt(string text, out StillTime at, out string error)
    {
        error = "";
        var value = text.Trim();
        if (value.Equals("end", StringComparison.OrdinalIgnoreCase))
        {
            at = new StillTime(double.PositiveInfinity, null);
            return true;
        }

        if (double.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var seconds) && double.IsFinite(seconds))
        {
            at = new StillTime(seconds, null);
            return true;
        }

        if (ScenarioCast.IsStepId(value))
        {
            at = new StillTime(0, value);
            return true;
        }

        at = new StillTime(0, null);
        error = $"invalid --at value: {text} (expected seconds from the start, such as 2.5, end, or a step's id)";
        return false;
    }

    /// <summary>
    /// The seconds of <paramref name="at"/> in a cast read with <paramref name="settings"/>: its own, or for a step's
    /// id where the cast's <c>st:ids</c> tag says the step's output has ended. False, with the error, for an id that
    /// the cast does not have.
    /// </summary>
    internal static bool TryResolveStillAt(StillTime at, ResolvedRenderSettings settings, out double seconds, out string error)
    {
        error = "";
        seconds = at.Seconds;
        if (at.Step is not { } step)
            return true;
        if (settings.StepIds?.Find(step) is double time)
        {
            seconds = time;
            return true;
        }

        error = settings.StepIds is { } ids
            ? $"--at {step}: no step in the cast has this id (it has {string.Join(", ", ids.Ids)})"
            : $"--at {step}: the cast has no step ids, which scenetake writes when a step has an id. Give the time in seconds, or record it again";
        return false;
    }

    /// <summary>The still's time as the log prints it: <c>at 2.5s</c>, <c>final screen</c>, or <c>at compare (12.3s)</c>.</summary>
    internal static string FormatStillAt(double at, string? step = null) =>
        step is not null ? $"at {step} ({FormatSeconds(at)}s)"
        : double.IsPositiveInfinity(at) ? "final screen" : $"at {FormatSeconds(at)}s";

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

    /// <summary>The class and keyframes of the group that fades the terminal's contents out; <see cref="ClassNamePool"/> never gives it.</summary>
    private const string FadeClass = "fade";

    // A <use> plus the id it needs costs about this much. Shorter rows are cheaper to repeat.
    private const int MinReusableRowChars = 48;
    private const string Space = " ";

    // Window chrome — fixed px; does not scale with terminal font-size
    internal const double ChromeTitleBarHeight = 34.0;
    internal const double ChromeSidePadding = 14.0;
    internal const double ChromeTopPadding = 9.0;
    private const double ChromeOuterMargin = 12.0;
    internal const double ChromeMacCornerRadius = 8.0;
    internal const double ChromeWindowsCornerRadius = 8.0;
    internal const double MacButtonDiameter = 16.0;
    internal const double MacButtonGap = 10.0;

    // Windows: the tab row of Windows Terminal. The active tab, in the terminal's color, holds an icon, the title,
    // and a close button; the new-tab and dropdown buttons follow it, and the caption buttons sit at the right.
    // Positions are from the window's top-left corner.
    internal const double WinTabLeft = 8.0;
    internal const double WinTabTop = 6.0;
    internal const double WinTabCornerRadius = 6.0;
    internal const double WinTabMinWidth = 120.0;

    /// <summary>The middle of the tab's content, on a half pixel so that 1-pixel lines are sharp.</summary>
    internal const double WinTabCenterY = 20.5;

    /// <summary>From the tab's left edge to the title: 10 px, the 15 px icon, and 9 px.</summary>
    internal const double WinTabTitleX = 34.0;

    /// <summary>From the title's end to the tab's right edge: 12 px, the 8 px close glyph, and 12 px.</summary>
    internal const double WinTabTitleEnd = 32.0;

    /// <summary>The new-tab and dropdown buttons after the tab, centered 20 and 46 px past it.</summary>
    internal const double WinTabExtras = 56.0;

    /// <summary>The width of each caption button, minimize, maximize, and close, at the right of the row.</summary>
    internal const double WinCaptionWidth = 46.0;

    /// <summary>The middle of the caption glyphs, on a half pixel so that 1-pixel lines are sharp.</summary>
    internal const double WinCaptionCenterY = 17.5;

    internal const double WinTitleFontSize = 12.0;

    /// <summary>The title's baseline below the window's top edge: a 12 px line centered in the tab.</summary>
    internal const double WinTitleBaseline = 24.5;
    private const string WindowShadowFilter =
        "<filter id=\"window-shadow\" x=\"-25%\" y=\"-25%\" width=\"150%\" height=\"150%\"><feDropShadow dx=\"0\" dy=\"2\" stdDeviation=\"4\" flood-opacity=\"0.22\"/></filter>";

    // On a background, the window casts a desktop window's shadow: its whole shape, offset down and blurred.
    internal const double WindowShadowOffsetY = 10;
    internal const double WindowShadowDeviation = 14;
    internal const double WindowShadowOpacity = 0.45;

    /// <summary>The window title's font: the viewer's system sans-serif, as a desktop's title bar draws it.</summary>
    internal const string ChromeTitleFontFamily = "-apple-system,BlinkMacSystemFont,'SF Pro Text','Segoe UI','Helvetica Neue',Helvetica,Arial,'Noto Sans','DejaVu Sans',sans-serif";

    /// <summary>
    /// The width of each printable ASCII character, U+0020 to U+007E, in a bold sans-serif title, in em: the widest
    /// of Verdana, Segoe UI, Arial, Tahoma, Trebuchet MS, and Calibri in bold, rounded up. Verdana is the widest
    /// nearly everywhere, and DejaVu Sans, the usual sans-serif on Linux, has nearly its widths. A title measured
    /// with these is no narrower than it draws in any of them, so a cut title stays clear of the buttons.
    /// </summary>
    private static readonly double[] TitleCharWidths =
    [
        0.35, 0.41, 0.59, 0.87, 0.72, 1.28, 0.87, 0.34, 0.55, 0.55, 0.72, 0.87, 0.37, 0.48, 0.37, 0.69, // ' ' to '/'
        0.72, 0.72, 0.72, 0.72, 0.72, 0.72, 0.72, 0.72, 0.72, 0.72, 0.41, 0.41, 0.87, 0.87, 0.87, 0.62, // '0' to '?'
        0.98, 0.78, 0.77, 0.73, 0.83, 0.69, 0.65, 0.82, 0.84, 0.55, 0.56, 0.78, 0.64, 0.96, 0.85, 0.85, // '@' to 'O'
        0.74, 0.85, 0.79, 0.71, 0.69, 0.82, 0.77, 1.13, 0.77, 0.74, 0.70, 0.55, 0.69, 0.55, 0.87, 0.72, // 'P' to '_'
        0.72, 0.67, 0.70, 0.59, 0.70, 0.67, 0.43, 0.70, 0.72, 0.35, 0.41, 0.68, 0.35, 1.06, 0.72, 0.69, // U+0060 to 'o'
        0.70, 0.70, 0.50, 0.60, 0.46, 0.72, 0.65, 0.98, 0.67, 0.66, 0.60, 0.72, 0.64, 0.72, 0.87,       // 'p' to '~'
    ];

    /// <summary>The width of the ellipsis that ends a cut title, in em, measured as <see cref="TitleCharWidths"/> are.</summary>
    private const double TitleEllipsisWidth = 1.05;

    /// <summary>The width of a character outside ASCII whose base letter is not ASCII either, in em: W's, the widest letter.</summary>
    private const double TitleOtherWidth = 1.13;

    // The macOS window title's size.
    internal const double ChromeTitleFontSize = 13.0;

    /// <summary>The macOS title's baseline below the window's top edge: a 13 px line centered in the 34 px bar.</summary>
    internal const double ChromeTitleBaseline = 21.5;

    /// <summary>The title's font size: 13 px in a macOS bar, 12 px in a Windows Terminal tab.</summary>
    internal static double TitleFontSize(WindowStyle window) => window == WindowStyle.Windows ? WinTitleFontSize : ChromeTitleFontSize;

    /// <summary>Room kept between the title and the buttons.</summary>
    private const double ChromeTitleGap = 10.0;

    // A dark macOS window has a faint white line just inside its dark border, so its edge shows on a dark background.
    private static readonly ChromePalette MacDark = new("#323232", "#1a1a1a", "#c8c8c8", EdgeHighlight: 0.16);
    private static readonly ChromePalette MacLight = new("#dcdcdc", "#b0b0b0", "#4a4a4a");

    // Windows: the tab row is darker (lighter in light mode) than the active tab, which takes the terminal's color.
    private static readonly ChromePalette WinDark = new("#1c1c1c", "#404040", "#d6d6d6");
    private static readonly ChromePalette WinLight = new("#ebebeb", "#d0d0d0", "#333333");

    /// <param name="TitleBarBg">The title bar, or on Windows the tab row.</param>
    /// <param name="TitleText">The title, and on Windows the icon and the button glyphs.</param>
    /// <param name="EdgeHighlight">The opacity of a white 1-pixel line just inside the window's edge; 0 for none.</param>
    internal readonly record struct ChromePalette(string TitleBarBg, string Border, string TitleText, double EdgeHighlight = 0);

    /// <summary>
    /// A Windows Terminal tab row, relative to the window's top-left corner: the active tab's edges, the title in
    /// it, cut as <see cref="FitWindowTitle"/> cuts it, and whether the new-tab and dropdown buttons fit after it.
    /// No tab when the window is too narrow for one beside the caption buttons.
    /// </summary>
    internal readonly record struct WinTabRow(bool HasTab, double TabLeft, double TabRight, string? Title, double TitleX, bool Extras);

    /// <summary>
    /// Lays out a Windows Terminal tab row in a window <paramref name="windowWidth"/> wide. The tab fits its
    /// title, as measured to cut it, and is at least <see cref="WinTabMinWidth"/> wide, so the SVG and the mp4
    /// draw the same tab whatever font draws the title.
    /// </summary>
    internal static WinTabRow LayoutWinTabRow(string? title, double windowWidth)
    {
        var fitted = FitWindowTitle(title, WindowStyle.Windows, windowWidth);
        var tabWidth = Math.Max(WinTabMinWidth, WinTabTitleX + MeasureTitle(fitted?.Text, WinTitleFontSize) + WinTabTitleEnd);
        var available = windowWidth - WinTabLeft - 3 * WinCaptionWidth - ChromeTitleGap;
        var extras = tabWidth + WinTabExtras <= available;
        tabWidth = Math.Min(tabWidth, available);
        var hasTab = tabWidth >= WinTabTitleX + WinTabTitleEnd;
        return new(hasTab, WinTabLeft, WinTabLeft + tabWidth, hasTab ? fitted?.Text : null, WinTabLeft + WinTabTitleX, extras && hasTab);
    }

    /// <summary>macOS close, minimize, and zoom buttons, left to right.</summary>
    internal static readonly string[] MacButtonColors = ["#ff5f57", "#febc2e", "#28c840"];

    [ThreadStatic] private static StringBuilder? t_runText;
    [ThreadStatic] private static StringBuilder? t_runForm;
    [ThreadStatic] private static StringBuilder? t_listForm;
    [ThreadStatic] private static List<GlyphGroup>? t_glyphGroups;
    [ThreadStatic] private static Dictionary<string, GlyphGroup>[]? t_glyphGroupsByStyle;
    [ThreadStatic] private static string?[]? t_rowForegrounds;
    [ThreadStatic] private static RowParts? t_rowParts;
    [ThreadStatic] private static List<BlockPart>? t_blockParts;
    [ThreadStatic] private static StringBuilder? t_escape;

    /// <summary>
    /// Renders <paramref name="frames"/> (from <see cref="TerminalReplay.BuildFrames"/> with the
    /// same events and sizes). Frames carry no screen contents, so this replays
    /// <paramref name="events"/> once more and encodes each changed row as it goes.
    /// </summary>
    /// <param name="still">
    /// <paramref name="frames"/> is one screen shown from the start (<see cref="SvgRender.WriteSvg"/>'s still): the SVG
    /// has no animation, so every element shows without CSS animation support.
    /// </param>
    internal static SvgDocument Render(
        IReadOnlyList<CastEvent> events,
        int initialWidth,
        int initialHeight,
        IReadOnlyList<ReplayFrame> frames,
        ResolvedRenderSettings render,
        CanvasWindow canvas,
        double loopDuration,
        bool still = false)
    {
        var canvasWidth = canvas.Width;
        var canvasHeight = canvas.Height;
        if (frames.Count == 0)
            return SvgDocument.FromText(BuildEmptySvg(render, canvasWidth, canvasHeight));

        frames = OptimizeFrames(frames, render.MaxFps);
        var theme = TerminalTheme.FromResolved(render.Theme);
        var metrics = CreateMetrics(render, canvasWidth, canvasHeight);

        var context = new EncodeContext(metrics, theme, canvasWidth, canvasHeight);
        var layers = BuildLayers(events, initialWidth, initialHeight, frames, canvas, context);
        loopDuration = ResolveLoopDuration(loopDuration, layers);

        var (head, rowKeyframes, tail) = BuildLayeredSvg(layers, context, render, canvasWidth, canvasHeight, loopDuration, still);
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
        CanvasWindow canvas,
        EncodeContext context)
    {
        var canvasWidth = canvas.Width;
        var canvasHeight = canvas.Height;
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

        foreach (var (frame, buffer) in TerminalReplay.ReplaySelected(events, initialWidth, initialHeight, canvas, theme, frames))
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
                // The row-local pass also measures the absolute form, so the absolute form is made
                // only when it is kept: when it is shorter, or while the row's savings have not paid for
                // its rule. It is made from the row-local encoding (AppendRowAt), not encoded again.
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
                    shape = AppendRowAt(absolute, buffer, row, origin.X, rowTop, context, local);
                    fragment = absolute;
                }
                var layer = new AnimLayer(time, row) { ElementCount = shape.Elements, SingleText = shape.SingleText };

                // Scrolling moves unchanged rows to other lines. Reuse the first layer that showed the
                // same content, after comparing bytes exactly: row-local content as it is, absolute
                // content made again at that layer's line.
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
                            AppendRowAt(verify, buffer, row, origin.X, sourceTop, context, local);
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
        double loopDuration,
        bool still)
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

        var keyframes = new LoopKeyframeRegistry(loopDurationText);
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

        var windowOpacity = render.WindowOpacity;
        var windowBlur = render.WindowBlur;
        var fillOpacity = FillOpacityAttribute(windowOpacity);

        // A translucent window clips its background, and its blurred copy of the background, to the window's shape.
        var clipsWindow = windowOpacity < 1 && (metrics.HasChrome || windowBlur > 0);

        // With chrome on a background, the window casts a shape's shadow; a translucent window keeps it outside.
        var chrome = metrics.HasChrome ? ResolveChromePalette(render.Window, render.Theme.Bg) : default;
        var castsShadow = metrics.HasChrome && render.Background is not null;
        var masksShadow = castsShadow && windowOpacity < 1;
        var edgeHighlight = metrics.HasChrome && chrome.EdgeHighlight > 0;

        // Mask, background, the blurred copy of the background if any, and the chrome's parts: its shadow on a
        // background, frame, bar, line, three buttons, the title if any, and the edge line if any.
        var viewportElements = 2 + (windowBlur > 0 ? 1 : 0) + ChromeElementCount(render.Window) + (metrics.HasChrome && !string.IsNullOrEmpty(render.WindowTitle) ? 1 : 0)
            + (castsShadow ? 1 : 0) + (edgeHighlight ? 1 : 0);
        for (var i = 0; i < viewportLayers.Count; i++)
            keyframes.Use(viewportKeyframeIds[i], viewportElements);

        keyframes.AssignNames(context.Names);

        var sb = new StringBuilder(64 * 1024);
        sb.AppendLf(CultureInfo.InvariantCulture, $"<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLf(CultureInfo.InvariantCulture,
            $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{metrics.SvgWidth:0.##}\" height=\"{metrics.SvgHeight:0.##}\" viewBox=\"0 0 {metrics.SvgWidth:0.##} {metrics.SvgHeight:0.##}\" preserveAspectRatio=\"xMidYMid meet\" role=\"img\" aria-label=\"{EscapeXml(AccessibleName(render))}\">");

        sb.AppendLf("<style>");
        sb.AppendLf(CultureInfo.InvariantCulture,
            $"text {{ font-family: {render.FontFamily}; font-size: {render.FontSize}px; white-space: pre; }}");
        sb.AppendLf("text { dominant-baseline: alphabetic; }");
        // Background and block rects in rows carry no class, also inside reused <use> content. This rule gives them crisp edges.
        sb.AppendLf(".bg,rect:not([class]) { shape-rendering: crispEdges; }");
        // Every animated element: hidden by default, switched at its window's offsets, looping. Played once, each keeps
        // its value at the end of the loop: the elements of the final screen show from there on, and no others.
        // A still has one screen, all of it shown, so it leaves this rule and the timing rules out: a viewer
        // without CSS animation, such as an image converter, then shows it too.
        if (!still)
        {
            sb.AppendLf(CultureInfo.InvariantCulture,
                $".l {{ visibility: hidden; animation: {loopDurationText}s step-start {(render.Loops ? "infinite" : "forwards")}; }}");
        }

        var fadeOut = still ? 0 : render.SvgFadeOut;
        if (fadeOut > 0 && loopDuration > 0)
        {
            // The terminal's contents fade out at the end of each loop. Its background, the window, and the
            // background around it stay. The loop starts again at full opacity.
            var fadeStart = Math.Max(0, (loopDuration - fadeOut) / loopDuration * 100);
            sb.AppendLf(CultureInfo.InvariantCulture,
                $"@keyframes {FadeClass}{{0%,{fadeStart:0.######}%{{opacity:1}}to{{opacity:0}}}}");
            sb.AppendLf(CultureInfo.InvariantCulture, $".{FadeClass}{{animation:{loopDurationText}s linear infinite {FadeClass}}}");
        }
        sb.AppendLf(CultureInfo.InvariantCulture,
            $".c {{ fill: {theme.Foreground}; fill-opacity: {cursorOpacityText}; }}");
        for (var row = 0; row < context.RowClasses.Length; row++)
        {
            if (context.RowClasses[row] is { } rowClass)
                sb.AppendLf(RowRule(rowClass, RowBaseline(metrics, row)));
        }

        context.Fills.AppendRules(sb);
        if (!still)
            keyframes.AppendAll(sb);
        sb.AppendLf("</style>");

        sb.AppendLf("<defs>");
        if (metrics.HasChrome)
            AppendShadowFilter(sb, metrics, castsShadow);
        if (render.Background is { } backgroundDefs)
            AppendBackgroundDefs(sb, metrics, backgroundDefs, windowBlur);
        if (clipsWindow)
        {
            for (var i = 0; i < viewportLayers.Count; i++)
                AppendWindowClip(sb, metrics, viewportLayers[i].P0, viewportLayers[i].P1, i);
        }

        if (masksShadow)
        {
            for (var i = 0; i < viewportLayers.Count; i++)
                AppendOutsideWindowMask(sb, metrics, viewportLayers[i].P0, viewportLayers[i].P1, i);
        }

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

        if (render.Background is { } background)
            AppendBackground(sb, metrics, background, windowBlur);

        if (windowBlur > 0)
        {
            for (var i = 0; i < viewportLayers.Count; i++)
                AppendFrost(sb, i, keyframes.Name(viewportKeyframeIds[i]));
        }

        if (metrics.HasChrome)
        {
            for (var i = 0; i < viewportLayers.Count; i++)
            {
                var viewport = viewportLayers[i];
                AppendWindowChrome(sb, metrics, chrome, render.Window, render.WindowTitle, viewport.P0, viewport.P1, i, keyframes.Name(viewportKeyframeIds[i]), fillOpacity, castsShadow, masksShadow, render.Theme.Bg);
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
                $"<rect class=\"bg l {keyframes.Name(viewportKeyframeIds[i])} viewport-bg-{i}\" x=\"{metrics.TerminalOriginX:0.##}\" y=\"{metrics.TerminalOriginY:0.##}\" width=\"{viewportWidth:0.##}\" height=\"{viewportHeight:0.##}\" fill=\"{render.Theme.Bg}\"{fillOpacity}{(clipsWindow && metrics.HasChrome ? $" clip-path=\"url(#window-clip-{i})\"" : "")}/>");
        }

        // The rows and cursors go in the fade group, over the terminal's background, which does not fade.
        var fades = fadeOut > 0 && loopDuration > 0;
        if (fades)
            sb.AppendLf(CultureInfo.InvariantCulture, $"<g class=\"{FadeClass}\">");

        var head = sb.ToString();
        sb.Clear();

        var origin = metrics.ContentOrigin;
        foreach (var i in standaloneCursors)
        {
            var layer = cursorLayers[i];
            sb.AppendLf(CultureInfo.InvariantCulture,
                $"<use href=\"#c\" x=\"{origin.X + layer.P1 * metrics.CharWidth:0.##}\" y=\"{origin.Y + layer.P0 * metrics.LineHeight:0.##}\" class=\"l {keyframes.Name(cursorKeyframeIds[i])}\"/>");
        }

        if (fades)
            sb.AppendLf("</g>");
        sb.AppendLf("</g>");

        // Over the terminal, whose background reaches the window's edge.
        if (edgeHighlight)
        {
            for (var i = 0; i < viewportLayers.Count; i++)
                AppendEdgeHighlight(sb, metrics, chrome, viewportLayers[i].P0, viewportLayers[i].P1, i, keyframes.Name(viewportKeyframeIds[i]));
        }

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
    /// Deduplicated timing for show/hide windows within the loop. Each animated element has class
    /// <c>l</c> (hidden, step-start timing, looping) plus a short class naming its window.
    /// <list type="bullet">
    /// <item>Windows switch <c>visibility</c>. Browsers skip hidden elements when painting, but still
    /// raster layers at opacity 0, which made playback stutter whenever typing changed a layer.</item>
    /// <item>A window that ends inside the loop is an <c>animation-delay</c> to its start plus keyframes
    /// shared by every window of the same length, <c>@keyframes Z{L%{visibility:visible}}</c>: with
    /// step-start timing a keyframe's value holds from the keyframe before it up to its own offset.
    /// Most windows then cost one short class rule, and browsers resolve far fewer keyframes on
    /// every frame.</item>
    /// <item>Switch times are whole milliseconds. A delay is exact, but a percentage of the loop is not,
    /// so a percentage step is at most 1µs of the loop, and offsets round so that an element is shown no
    /// later and hidden no earlier than its millisecond. A layer and its replacement then overlap for
    /// under 2µs rather than leave a gap in which the row is empty.</item>
    /// </list>
    /// Names are given after all elements are counted: the most used get the shortest names, and
    /// windows nothing refers to are not written.
    /// </summary>
    private sealed class LoopKeyframeRegistry
    {
        private const long ToLoopEnd = -1;
        private static readonly (long Show, long Hide) Never = (-1, -1);
        private readonly decimal _loop;
        private readonly decimal _percentScale;
        private readonly Dictionary<(long Show, long Hide), int> _ids = [];
        private readonly List<(long Show, long Hide)> _windows = [];
        private readonly List<int> _uses = [];
        private readonly Dictionary<long, string> _lengthNames = [];
        private string?[] _names = [];

        private enum WindowKind { Never, Always, ToLoopEnd, Span }

        /// <param name="loopDurationText">The loop in seconds, as the <c>l</c> rule writes it.</param>
        internal LoopKeyframeRegistry(string loopDurationText)
        {
            _loop = decimal.Parse(loopDurationText, NumberStyles.Float, CultureInfo.InvariantCulture);
            // One step of a percentage is at most 1µs of the loop.
            var decimals = _loop <= 0 ? 1 : Math.Clamp((int)Math.Ceiling(Math.Log10((double)_loop) + 4), 1, 9);
            _percentScale = (decimal)Math.Pow(10, decimals);
        }

        internal int Register(double showTime, double? hideTime)
        {
            var show = Math.Max(0, Milliseconds(showTime));
            var hide = hideTime is double time && Milliseconds(time) / 1000m < _loop ? Milliseconds(time) : ToLoopEnd;
            var window = show / 1000m >= _loop || (hide != ToLoopEnd && hide <= show) ? Never : (show, hide);
            if (_ids.TryGetValue(window, out var existing))
                return existing;

            var id = _windows.Count;
            _ids[window] = id;
            _windows.Add(window);
            _uses.Add(0);
            return id;
        }

        internal void Use(int id, int count = 1) => _uses[id] += count;

        /// <summary>
        /// Names window classes and the keyframes shared by span lengths. A span from 0 needs no delay,
        /// so its class is the shared keyframes' name. A later span names them once, in its own rule.
        /// </summary>
        internal void AssignNames(ClassNamePool pool)
        {
            var lengths = new Dictionary<long, NameEntry>();
            var entries = new List<NameEntry>();
            for (var id = 0; id < _windows.Count; id++)
            {
                if (_uses[id] == 0)
                    continue;

                var (show, hide) = _windows[id];
                if (KindOf(id) == WindowKind.Span)
                {
                    var length = hide - show;
                    if (!lengths.TryGetValue(length, out var shared))
                    {
                        shared = new NameEntry(0, id, -1, length);
                        lengths[length] = shared;
                        entries.Add(shared);
                    }

                    shared.Uses += show == 0 ? _uses[id] : 1;
                    if (show == 0)
                        continue;
                }

                entries.Add(new NameEntry(_uses[id], id, id, 0));
            }

            _names = new string?[_windows.Count];
            // No two entries are alike in all three, so this order is the one a stable sort gives.
            var ordered = entries.ToArray();
            Array.Sort(ordered, NameEntry.Compare);
            foreach (var entry in ordered)
            {
                var name = pool.Next();
                if (entry.Window < 0)
                    _lengthNames[entry.Length] = name;
                else
                    _names[entry.Window] = name;
            }
        }

        // A window's class, or (Window -1) the keyframes that the spans of one length share. The most used get the
        // shortest names, then the one first used. A class over a value tuple keeps LINQ's sort out of the binary.
        private sealed class NameEntry(int uses, int first, int window, long length)
        {
            internal int Uses { get; set; } = uses;
            internal int First { get; } = first;
            internal int Window { get; } = window;
            internal long Length { get; } = length;

            internal static int Compare(NameEntry a, NameEntry b) =>
                a.Uses != b.Uses ? b.Uses.CompareTo(a.Uses) : a.First != b.First ? a.First.CompareTo(b.First) : a.Window.CompareTo(b.Window);
        }

        internal string Name(int id)
        {
            var (show, hide) = _windows[id];
            var name = KindOf(id) == WindowKind.Span && show == 0 ? _lengthNames.GetValueOrDefault(hide - show) : _names[id];
            return name ?? throw new InvalidOperationException($"Keyframe {id} has no name; it was not counted as used.");
        }

        internal void AppendAll(StringBuilder sb)
        {
            var writtenLengths = new HashSet<long>();
            var lengthClasses = new HashSet<long>();
            for (var id = 0; id < _windows.Count; id++)
            {
                if (_uses[id] == 0)
                    continue;

                var (show, hide) = _windows[id];
                switch (KindOf(id))
                {
                    case WindowKind.Never:
                        // No rule: the l rule names no animation, so the element stays hidden.
                        break;
                    case WindowKind.Always:
                        sb.Append('.').Append(_names[id]).AppendLf("{visibility:visible}");
                        break;
                    case WindowKind.ToLoopEnd:
                    {
                        var name = _names[id];
                        sb.Append("@keyframes ").Append(name).Append('{').Append(Percent(show, roundUp: false)).AppendLf("%{visibility:hidden}to{visibility:visible}}");
                        sb.Append('.').Append(name).Append("{animation-name:").Append(name).AppendLf("}");
                        break;
                    }
                    case WindowKind.Span:
                    {
                        var length = hide - show;
                        var shared = _lengthNames[length];
                        if (writtenLengths.Add(length))
                            sb.Append("@keyframes ").Append(shared).Append('{').Append(Percent(length, roundUp: true)).AppendLf("%{visibility:visible}}");

                        if (show != 0)
                            sb.Append('.').Append(_names[id]).Append("{animation-delay:").Append(Seconds(show)).Append("s;animation-name:").Append(shared).AppendLf("}");
                        else if (lengthClasses.Add(length))
                            sb.Append('.').Append(shared).Append("{animation-name:").Append(shared).AppendLf("}");
                        break;
                    }
                }
            }
        }

        private WindowKind KindOf(int id)
        {
            var (show, hide) = _windows[id];
            if (show < 0)
                return WindowKind.Never;
            if (hide != ToLoopEnd)
                return WindowKind.Span;
            return show == 0 ? WindowKind.Always : WindowKind.ToLoopEnd;
        }

        private static long Milliseconds(double seconds) => (long)Math.Round(seconds * 1000, MidpointRounding.AwayFromZero);

        private static string Seconds(long milliseconds) => (milliseconds / 1000m).ToString("0.###", CultureInfo.InvariantCulture);

        private string Percent(long milliseconds, bool roundUp)
        {
            if (_loop <= 0)
                return "0";

            var scaled = milliseconds / 1000m / _loop * 100m * _percentScale;
            var steps = roundUp ? Math.Ceiling(scaled) : Math.Floor(scaled);
            return (steps / _percentScale).ToString("0.#########", CultureInfo.InvariantCulture);
        }
    }

    /// <summary>
    /// Short class names, shared by rows, colors, and keyframes so they never collide: a–z, then a
    /// letter followed by letters or digits. Lowercase only, because an SVG inlined in an HTML page
    /// without a doctype matches class names regardless of case. Skips <c>l</c>, <c>c</c>, <c>bg</c>,
    /// and <c>fade</c>, which the SVG uses itself, and words CSS reserves for <c>animation-name</c>.
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
            name is "l" or "c" or "bg" or FadeClass
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
        // The text of each column's x at the content's left edge, and of the width of a run of n columns, made when a
        // row first needs it. Every glyph and run writes one, and formatting it each time made a new string. Index
        // canvasWidth is the right edge, for a span that ends there.
        private readonly double _originX;
        private readonly string?[] _columnX;
        private readonly string?[] _columnsWidth;

        internal EncodeContext(SvgMetrics metrics, TerminalTheme theme, int canvasWidth, int canvasHeight)
        {
            Metrics = metrics;
            Theme = theme;
            RowClasses = new string?[canvasHeight];
            RowSavings = new int[canvasHeight];
            Fills = new FillClasses(theme.Foreground, Names);
            _originX = metrics.ContentOrigin.X;
            _columnX = new string?[canvasWidth + 1];
            _columnsWidth = new string?[canvasWidth + 1];
            LineHeightText = Format(metrics.LineHeight);
        }

        /// <summary><see cref="Format"/> of <c>originX + col × CharWidth</c>.</summary>
        internal string ColumnX(double originX, int col)
        {
            // Same expression as the uncached one, so the double and its text are the same.
            if (originX != _originX || (uint)col >= (uint)_columnX.Length)
                return Format(originX + col * Metrics.CharWidth);
            return _columnX[col] ??= Format(originX + col * Metrics.CharWidth);
        }

        /// <summary><see cref="Format"/> of <c>count × CharWidth</c>.</summary>
        internal string ColumnsWidth(int count)
        {
            if ((uint)count >= (uint)_columnsWidth.Length)
                return Format(count * Metrics.CharWidth);
            return _columnsWidth[count] ??= Format(count * Metrics.CharWidth);
        }

        /// <summary><see cref="Format"/> of the line height.</summary>
        internal string LineHeightText { get; }

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

    internal static bool IsLightTerminalBg(string bg)
    {
        if (!CellColors.TryParseHexColor(bg, out var r, out var g, out var b))
            return false;

        return (0.299 * r) + (0.587 * g) + (0.114 * b) > 140;
    }

    /// <summary>
    /// The title as the bar shows it, with its x relative to the window's left edge: its center on macOS, its
    /// start on Windows. A title wider than the room the buttons leave is cut and ends in an ellipsis. Null when
    /// there is no title, or no room for two cells of the terminal's font at the title's size. Widths are
    /// <see cref="TitleWidth"/>'s, no narrower than any common sans-serif draws them, so the SVG and the mp4 cut
    /// a title at the same place, and a cut title stays clear of the buttons whatever font the viewer has.
    /// </summary>
    internal static (string Text, double X, bool Centered)? FitWindowTitle(string? title, WindowStyle window, double windowWidth)
    {
        if (window == WindowStyle.None || string.IsNullOrEmpty(title))
            return null;

        double room, x;
        bool centered;
        if (window == WindowStyle.Macos)
        {
            // Centered over the whole bar, so the buttons' width is kept clear on both sides.
            var buttons = ChromeSidePadding + MacButtonDiameter * 3 + MacButtonGap * 2;
            room = windowWidth - 2 * (buttons + ChromeTitleGap);
            x = windowWidth / 2;
            centered = true;
        }
        else
        {
            // In the tab, which leaves room for the new-tab and dropdown buttons and the caption buttons.
            room = windowWidth - WinTabLeft - WinTabTitleX - WinTabTitleEnd - WinTabExtras - 3 * WinCaptionWidth - ChromeTitleGap;
            x = WinTabLeft + WinTabTitleX;
            centered = false;
        }

        var fontSize = TitleFontSize(window);
        if (room < 2 * fontSize * CharWidthFactor)
            return null;

        var elements = new List<(string Text, double Width)>();
        var total = 0.0;
        var enumerator = StringInfo.GetTextElementEnumerator(title);
        while (enumerator.MoveNext())
        {
            var element = enumerator.GetTextElement();
            var width = TitleWidth(element, fontSize);
            elements.Add((element, width));
            total += width;
        }

        if (total <= room)
            return (title, x, centered);

        var builder = new StringBuilder();
        var used = 0.0;
        var ellipsis = TitleEllipsisWidth * fontSize;
        foreach (var (element, width) in elements)
        {
            if (used + width + ellipsis > room)
                break;
            builder.Append(element);
            used += width;
        }

        return builder.Length == 0 ? null : (builder.Append('…').ToString(), x, centered);
    }

    /// <summary>
    /// The width in pixels that a title gives one text element: a wide character as two cells of the terminal's
    /// font, which no sans-serif's CJK reaches; ASCII from <see cref="TitleCharWidths"/>; a letter with a mark as
    /// its base letter; anything else as W.
    /// </summary>
    internal static double TitleWidth(string element, double fontSize = ChromeTitleFontSize)
    {
        if (!Rune.TryGetRuneAt(element, 0, out var rune))
            return 0;
        if (ScreenBuffer.IsWideCodePoint(rune.Value))
            return 2 * CharWidthFactor * fontSize;
        if (element == "…")
            return TitleEllipsisWidth * fontSize;

        var code = rune.Value;
        if (code > 0x7E && element.Normalize(NormalizationForm.FormD)[0] is var letter && letter <= 0x7E)
            code = letter;
        var em = code is >= 0x20 and <= 0x7E ? TitleCharWidths[code - 0x20] : TitleOtherWidth;
        return em * fontSize;
    }

    /// <summary>The width of a title as <see cref="FitWindowTitle"/> measures it, its ellipsis included; 0 for none.</summary>
    internal static double MeasureTitle(string? text, double fontSize)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        var width = 0.0;
        var enumerator = StringInfo.GetTextElementEnumerator(text);
        while (enumerator.MoveNext())
            width += TitleWidth(enumerator.GetTextElement(), fontSize);
        return width;
    }

    /// <summary>
    /// The background's gradient, if any. With <paramref name="blur"/>, also the background itself, drawn twice
    /// by reference: sharp over the canvas, and blurred by the "frost" filter behind the window.
    /// </summary>
    private static void AppendBackgroundDefs(StringBuilder sb, SvgMetrics metrics, BackgroundSpec background, int blur)
    {
        if (background.Colors.Count >= 2)
        {
            // objectBoundingBox units: the gradient runs from the canvas's top-left corner to its bottom-right one.
            sb.AppendLf("<linearGradient id=\"desktop\" x1=\"0\" y1=\"0\" x2=\"1\" y2=\"1\">");
            for (var i = 0; i < background.Colors.Count; i++)
            {
                var offset = i / (double)(background.Colors.Count - 1);
                sb.AppendLf(CultureInfo.InvariantCulture, $"<stop offset=\"{offset:0.###}\" stop-color=\"{background.Colors[i]}\"/>");
            }

            sb.AppendLf("</linearGradient>");
        }

        if (blur <= 0)
            return;

        AppendBackgroundPaint(sb, metrics, background, " id=\"desktop-paint\"");

        // The filter region is the canvas. Edge pixels repeat outward, so a window near the canvas's edge does not
        // blur in transparent black. sRGB matches the mp4 renderer, which blurs the stored pixel values.
        sb.AppendLf(CultureInfo.InvariantCulture,
            $"<filter id=\"frost\" x=\"0\" y=\"0\" width=\"1\" height=\"1\" color-interpolation-filters=\"sRGB\"><feGaussianBlur stdDeviation=\"{blur}\" edgeMode=\"duplicate\"/></filter>");
    }

    /// <summary>
    /// The background over the whole canvas, behind the window. With <paramref name="blur"/>, a reference to the
    /// copy in the defs, so an image is embedded once.
    /// </summary>
    private static void AppendBackground(StringBuilder sb, SvgMetrics metrics, BackgroundSpec background, int blur)
    {
        if (blur > 0)
            sb.AppendLf("<use href=\"#desktop-paint\"/>");
        else
            AppendBackgroundPaint(sb, metrics, background, "");
    }

    /// <summary>The background's element. An image covers the canvas and is cropped to it, centered.</summary>
    private static void AppendBackgroundPaint(StringBuilder sb, SvgMetrics metrics, BackgroundSpec background, string id)
    {
        if (background.ImagePath is { } path)
        {
            var mime = Path.GetExtension(path).ToLowerInvariant() switch
            {
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                _ => "image/jpeg",
            };
            sb.AppendLf(CultureInfo.InvariantCulture,
                $"<image{id} href=\"data:{mime};base64,{Convert.ToBase64String(background.ImageBytes ?? File.ReadAllBytes(path))}\" x=\"0\" y=\"0\" width=\"{metrics.SvgWidth:0.##}\" height=\"{metrics.SvgHeight:0.##}\" preserveAspectRatio=\"xMidYMid slice\"/>");
            return;
        }

        var fill = background.Colors.Count == 1 ? background.Colors[0] : "url(#desktop)";
        sb.AppendLf(CultureInfo.InvariantCulture,
            $"<rect{id} x=\"0\" y=\"0\" width=\"{metrics.SvgWidth:0.##}\" height=\"{metrics.SvgHeight:0.##}\" fill=\"{fill}\"/>");
    }

    /// <summary>The shape of a viewport layer's window: the title bar and terminal, with the window's corners.</summary>
    private static void AppendWindowClip(StringBuilder sb, SvgMetrics metrics, int cols, int rows, int layerIndex)
    {
        var rx = metrics.CornerRadius;
        sb.AppendLf(CultureInfo.InvariantCulture,
            $"<clipPath id=\"window-clip-{layerIndex}\"><rect x=\"{metrics.OuterMargin:0.##}\" y=\"{metrics.OuterMargin:0.##}\" width=\"{metrics.ViewportPixelWidth(cols):0.##}\" height=\"{metrics.TitleBarHeight + metrics.ViewportPixelHeight(rows):0.##}\" rx=\"{rx:0.##}\" ry=\"{rx:0.##}\"/></clipPath>");
    }

    /// <summary>
    /// The window's shadow filter. Without a background, the faint shadow of its 1-pixel frame. On one, a desktop
    /// window's shadow of its whole shape; its region is the canvas, so a small window's shadow is not cut short.
    /// </summary>
    private static void AppendShadowFilter(StringBuilder sb, SvgMetrics metrics, bool castsShadow)
    {
        if (!castsShadow)
        {
            sb.AppendLf(WindowShadowFilter);
            return;
        }

        sb.AppendLf(CultureInfo.InvariantCulture,
            $"<filter id=\"window-shadow\" filterUnits=\"userSpaceOnUse\" x=\"0\" y=\"0\" width=\"{metrics.SvgWidth:0.##}\" height=\"{metrics.SvgHeight:0.##}\"><feDropShadow dx=\"0\" dy=\"{WindowShadowOffsetY:0.##}\" stdDeviation=\"{WindowShadowDeviation:0.##}\" flood-opacity=\"{WindowShadowOpacity:0.##}\"/></filter>");
    }

    /// <summary>Everything but a viewport layer's window, for the shadow of a translucent window.</summary>
    private static void AppendOutsideWindowMask(StringBuilder sb, SvgMetrics metrics, int cols, int rows, int layerIndex)
    {
        var rx = metrics.CornerRadius;
        sb.AppendLf(CultureInfo.InvariantCulture,
            $"<mask id=\"window-outside-{layerIndex}\" maskUnits=\"userSpaceOnUse\" x=\"0\" y=\"0\" width=\"{metrics.SvgWidth:0.##}\" height=\"{metrics.SvgHeight:0.##}\"><rect width=\"{metrics.SvgWidth:0.##}\" height=\"{metrics.SvgHeight:0.##}\" fill=\"white\"/><rect x=\"{metrics.OuterMargin:0.##}\" y=\"{metrics.OuterMargin:0.##}\" width=\"{metrics.ViewportPixelWidth(cols):0.##}\" height=\"{metrics.TitleBarHeight + metrics.ViewportPixelHeight(rows):0.##}\" rx=\"{rx:0.##}\" ry=\"{rx:0.##}\" fill=\"black\"/></mask>");
    }

    /// <summary>The faint white line just inside a viewport layer's window edge, over the title bar and the terminal.</summary>
    private static void AppendEdgeHighlight(StringBuilder sb, SvgMetrics metrics, ChromePalette chrome, int cols, int rows, int layerIndex, string? keyframe)
    {
        var cls = keyframe is null ? $"viewport-chrome-{layerIndex}" : $"l {keyframe} viewport-chrome-{layerIndex}";
        var rx = Math.Max(metrics.CornerRadius - 0.5, 0);
        sb.AppendLf(CultureInfo.InvariantCulture,
            $"<rect class=\"{cls}\" x=\"{metrics.OuterMargin + 0.5:0.##}\" y=\"{metrics.OuterMargin + 0.5:0.##}\" width=\"{metrics.ViewportPixelWidth(cols) - 1:0.##}\" height=\"{metrics.TitleBarHeight + metrics.ViewportPixelHeight(rows) - 1:0.##}\" rx=\"{rx:0.##}\" ry=\"{rx:0.##}\" fill=\"none\" stroke=\"#ffffff\" stroke-opacity=\"{chrome.EdgeHighlight:0.##}\"/>");
    }

    /// <summary>The blurred background, cut to a viewport layer's window, under the window's translucent fill.</summary>
    private static void AppendFrost(StringBuilder sb, int layerIndex, string? keyframe)
    {
        var cls = keyframe is null ? "" : $" class=\"l {keyframe}\"";
        sb.AppendLf(CultureInfo.InvariantCulture,
            $"<use href=\"#desktop-paint\"{cls} filter=\"url(#frost)\" clip-path=\"url(#window-clip-{layerIndex})\"/>");
    }

    /// <summary>A fill-opacity attribute for a translucent window's fill; empty for an opaque one.</summary>
    private static string FillOpacityAttribute(double opacity) =>
        opacity < 1 ? $" fill-opacity=\"{RenderSettingsResolver.FormatOpacity(opacity)}\"" : "";

    private static void AppendWindowChrome(
        StringBuilder sb,
        SvgMetrics metrics,
        ChromePalette chrome,
        WindowStyle window,
        string? title,
        int cols,
        int rows,
        int layerIndex,
        string? keyframe,
        string fillOpacity,
        bool castsShadow,
        bool masksShadow,
        string terminalBg)
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

        if (castsShadow)
        {
            // The window's shape in black casts the shadow and lies under the window. A translucent window would show
            // it, so the mask then keeps the shadow outside the window.
            var mask = masksShadow ? $" mask=\"url(#window-outside-{layerIndex})\"" : "";
            sb.AppendLf(CultureInfo.InvariantCulture,
                $"<rect class=\"{cls}\" x=\"{x:0.##}\" y=\"{y:0.##}\" width=\"{width:0.##}\" height=\"{totalHeight:0.##}\" rx=\"{rx:0.##}\" ry=\"{rx:0.##}\" fill=\"#000\" filter=\"url(#window-shadow)\"{mask}/>");
            sb.AppendLf(CultureInfo.InvariantCulture,
                $"<rect class=\"{cls}\" x=\"{x:0.##}\" y=\"{y:0.##}\" width=\"{width:0.##}\" height=\"{totalHeight:0.##}\" rx=\"{rx:0.##}\" ry=\"{rx:0.##}\" fill=\"none\" stroke=\"{chrome.Border}\" stroke-width=\"1\"/>");
        }
        else
        {
            sb.AppendLf(CultureInfo.InvariantCulture,
                $"<rect class=\"{cls}\" x=\"{x:0.##}\" y=\"{y:0.##}\" width=\"{width:0.##}\" height=\"{totalHeight:0.##}\" rx=\"{rx:0.##}\" ry=\"{rx:0.##}\" fill=\"none\" stroke=\"{chrome.Border}\" stroke-width=\"1\" filter=\"url(#window-shadow)\"/>");
        }
        sb.AppendLf(CultureInfo.InvariantCulture,
            $"<path class=\"{cls}\" d=\"M {x + rx:0.##},{y:0.##} H {right - rx:0.##} Q {right:0.##},{y:0.##} {right:0.##},{y + rx:0.##} V {barBottom:0.##} H {x:0.##} V {y + rx:0.##} Q {x:0.##},{y:0.##} {x + rx:0.##},{y:0.##} Z\" fill=\"{chrome.TitleBarBg}\"{fillOpacity}/>");

        if (window == WindowStyle.Windows)
        {
            AppendWinTabRow(sb, chrome, cls, title, x, y, width, barBottom, fillOpacity, terminalBg);
            return;
        }

        sb.AppendLf(CultureInfo.InvariantCulture,
            $"<line class=\"{cls}\" x1=\"{x:0.##}\" y1=\"{barBottom:0.##}\" x2=\"{right:0.##}\" y2=\"{barBottom:0.##}\" stroke=\"{chrome.Border}\" stroke-width=\"1\"/>");

        // The style attribute wins over the stylesheet's font for terminal text; font attributes would not. A macOS
        // title is bold, as the system draws it.
        if (FitWindowTitle(title, window, width) is { } fitted)
        {
            sb.AppendLf(CultureInfo.InvariantCulture,
                $"<text class=\"{cls}\" x=\"{x + fitted.X:0.##}\" y=\"{y + ChromeTitleBaseline:0.##}\" style=\"font:bold {ChromeTitleFontSize:0.##}px {ChromeTitleFontFamily}\"{(fitted.Centered ? " text-anchor=\"middle\"" : "")} fill=\"{chrome.TitleText}\">{EscapeXml(fitted.Text)}</text>");
        }

        var radius = MacButtonDiameter / 2d;
        var cx = x + ChromeSidePadding + radius;
        var cy = y + ChromeTopPadding + radius;
        foreach (var color in MacButtonColors)
        {
            sb.AppendLf(CultureInfo.InvariantCulture,
                $"<circle class=\"{cls}\" cx=\"{cx:0.##}\" cy=\"{cy:0.##}\" r=\"{radius:0.##}\" fill=\"{color}\"/>");
            cx += MacButtonDiameter + MacButtonGap;
        }
    }

    /// <summary>The elements a window's chrome draws besides the title, its shadow, and its edge line.</summary>
    private static int ChromeElementCount(WindowStyle window) => window switch
    {
        // Frame, bar, line, and three buttons.
        WindowStyle.Macos => 6,
        // Frame, tab row, tab, icon (2), close, new tab, dropdown, and three caption buttons. A narrow window draws
        // fewer, which only changes which timing gets the shorter name.
        WindowStyle.Windows => 11,
        _ => 0,
    };

    /// <summary>
    /// The Windows Terminal tab row over the bar: the active tab in the terminal's color with a generic terminal
    /// icon, the title, and a close glyph; the new-tab and dropdown glyphs; and the minimize, maximize, and close
    /// glyphs at the right. The icon is an outlined window with a prompt, no shell's logo.
    /// </summary>
    private static void AppendWinTabRow(StringBuilder sb, ChromePalette chrome, string cls, string? title, double x, double y, double width, double barBottom, string fillOpacity, string terminalBg)
    {
        var glyph = $"fill=\"none\" stroke=\"{chrome.TitleText}\" stroke-width=\"1\"";
        var row = LayoutWinTabRow(title, width);
        if (row.HasTab)
        {
            var left = x + row.TabLeft;
            var right = x + row.TabRight;
            var top = y + WinTabTop;
            var r = WinTabCornerRadius;
            var cy = y + WinTabCenterY;
            sb.AppendLf(CultureInfo.InvariantCulture,
                $"<path class=\"{cls}\" d=\"M {left:0.##},{barBottom:0.##} V {top + r:0.##} Q {left:0.##},{top:0.##} {left + r:0.##},{top:0.##} H {right - r:0.##} Q {right:0.##},{top:0.##} {right:0.##},{top + r:0.##} V {barBottom:0.##} Z\" fill=\"{terminalBg}\"{fillOpacity}/>");

            // The icon: a 15 × 12 outlined window with a prompt in it.
            var ix = left + 10;
            var iy = cy - 6.5;
            sb.AppendLf(CultureInfo.InvariantCulture,
                $"<rect class=\"{cls}\" x=\"{ix + 0.5:0.##}\" y=\"{iy + 0.5:0.##}\" width=\"14\" height=\"11\" rx=\"2\" ry=\"2\" {glyph}/>");
            sb.AppendLf(CultureInfo.InvariantCulture,
                $"<path class=\"{cls}\" d=\"M {ix + 4:0.##},{iy + 4:0.##} L {ix + 6.5:0.##},{iy + 6.5:0.##} L {ix + 4:0.##},{iy + 9:0.##} M {ix + 8:0.##},{iy + 9:0.##} H {ix + 11:0.##}\" {glyph} stroke-linecap=\"round\" stroke-linejoin=\"round\"/>");

            if (row.Title is { } text)
            {
                sb.AppendLf(CultureInfo.InvariantCulture,
                    $"<text class=\"{cls}\" x=\"{x + row.TitleX:0.##}\" y=\"{y + WinTitleBaseline:0.##}\" style=\"font:{WinTitleFontSize:0.##}px {ChromeTitleFontFamily}\" fill=\"{chrome.TitleText}\">{EscapeXml(text)}</text>");
            }

            var close = right - 16;
            sb.AppendLf(CultureInfo.InvariantCulture,
                $"<path class=\"{cls}\" d=\"M {close - 4:0.##},{cy - 4:0.##} L {close + 4:0.##},{cy + 4:0.##} M {close + 4:0.##},{cy - 4:0.##} L {close - 4:0.##},{cy + 4:0.##}\" {glyph}/>");

            if (row.Extras)
            {
                var plus = right + 20;
                var down = right + 46;
                sb.AppendLf(CultureInfo.InvariantCulture,
                    $"<path class=\"{cls}\" d=\"M {plus - 5:0.##},{cy:0.##} H {plus + 5:0.##} M {plus:0.##},{cy - 5:0.##} V {cy + 5:0.##}\" {glyph}/>");
                sb.AppendLf(CultureInfo.InvariantCulture,
                    $"<path class=\"{cls}\" d=\"M {down - 4:0.##},{cy - 2:0.##} L {down:0.##},{cy + 2:0.##} L {down + 4:0.##},{cy - 2:0.##}\" {glyph}/>");
            }
        }

        // Minimize, maximize, and close, each centered in its 46 px.
        var edge = x + width;
        var captionY = y + WinCaptionCenterY;
        var minimize = edge - WinCaptionWidth * 2.5;
        var maximize = edge - WinCaptionWidth * 1.5;
        var closeWindow = edge - WinCaptionWidth * 0.5;
        sb.AppendLf(CultureInfo.InvariantCulture,
            $"<path class=\"{cls}\" d=\"M {minimize - 5:0.##},{captionY:0.##} H {minimize + 5:0.##}\" {glyph}/>");
        sb.AppendLf(CultureInfo.InvariantCulture,
            $"<rect class=\"{cls}\" x=\"{maximize - 4.5:0.##}\" y=\"{captionY - 4.5:0.##}\" width=\"9\" height=\"9\" rx=\"1\" ry=\"1\" {glyph}/>");
        sb.AppendLf(CultureInfo.InvariantCulture,
            $"<path class=\"{cls}\" d=\"M {closeWindow - 5:0.##},{captionY - 5:0.##} L {closeWindow + 5:0.##},{captionY + 5:0.##} M {closeWindow + 5:0.##},{captionY - 5:0.##} L {closeWindow - 5:0.##},{captionY + 5:0.##}\" {glyph}/>");
    }

    /// <summary>
    /// The name a screen reader says for the image: the title bar's text, which is the scenario's title unless
    /// <c>window-title</c> gives another, so the name matches what the image shows. Then the title, when the window title
    /// is <c>""</c>. A recording with neither is a terminal recording.
    /// </summary>
    internal static string AccessibleName(ResolvedRenderSettings render) =>
        !string.IsNullOrWhiteSpace(render.WindowTitle) ? render.WindowTitle!
        : !string.IsNullOrWhiteSpace(render.Title) ? render.Title!
        : DefaultAccessibleName;

    /// <summary>The SVG's accessible name for a recording without a title.</summary>
    internal const string DefaultAccessibleName = "Terminal recording";

    private static string BuildEmptySvg(ResolvedRenderSettings render, int width, int height)
    {
        var metrics = CreateMetrics(render, width, height);
        var windowBlur = render.WindowBlur;
        var fillOpacity = FillOpacityAttribute(render.WindowOpacity);
        var clipsWindow = render.WindowOpacity < 1 && (metrics.HasChrome || windowBlur > 0);
        var castsShadow = metrics.HasChrome && render.Background is not null;
        var masksShadow = castsShadow && render.WindowOpacity < 1;
        var sb = new StringBuilder();
        sb.AppendLf(CultureInfo.InvariantCulture, $"<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLf(CultureInfo.InvariantCulture,
            $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{metrics.SvgWidth:0.##}\" height=\"{metrics.SvgHeight:0.##}\" viewBox=\"0 0 {metrics.SvgWidth:0.##} {metrics.SvgHeight:0.##}\" role=\"img\" aria-label=\"{EscapeXml(AccessibleName(render))}\">");
        if (metrics.HasChrome || render.Background is not null)
        {
            sb.AppendLf(CultureInfo.InvariantCulture, $"<style>text {{ font-family: {render.FontFamily}; white-space: pre; }}</style>");
            sb.AppendLf("<defs>");
            if (metrics.HasChrome)
                AppendShadowFilter(sb, metrics, castsShadow);
            if (render.Background is { } backgroundDefs)
                AppendBackgroundDefs(sb, metrics, backgroundDefs, windowBlur);
            if (clipsWindow)
                AppendWindowClip(sb, metrics, width, height, 0);
            if (masksShadow)
                AppendOutsideWindowMask(sb, metrics, width, height, 0);
            sb.AppendLf("</defs>");
        }

        if (render.Background is { } background)
            AppendBackground(sb, metrics, background, windowBlur);
        if (windowBlur > 0)
            AppendFrost(sb, 0, keyframe: null);

        if (metrics.HasChrome)
        {
            var chrome = ResolveChromePalette(render.Window, render.Theme.Bg);
            AppendWindowChrome(sb, metrics, chrome, render.Window, render.WindowTitle, width, height, 0, keyframe: null, fillOpacity, castsShadow, masksShadow, render.Theme.Bg);
            var bgRect = metrics.TerminalBackgroundRect(width, height);
            sb.AppendLf(CultureInfo.InvariantCulture,
                $"<rect x=\"{bgRect.X:0.##}\" y=\"{bgRect.Y:0.##}\" width=\"{bgRect.Width:0.##}\" height=\"{bgRect.Height:0.##}\" fill=\"{render.Theme.Bg}\"{fillOpacity}{(clipsWindow ? " clip-path=\"url(#window-clip-0)\"" : "")}/>");
            if (chrome.EdgeHighlight > 0)
                AppendEdgeHighlight(sb, metrics, chrome, width, height, 0, keyframe: null);
        }
        else
        {
            var bgRect = metrics.BackgroundRect();
            sb.AppendLf(CultureInfo.InvariantCulture,
                $"<rect x=\"{bgRect.X:0.##}\" y=\"{bgRect.Y:0.##}\" width=\"{bgRect.Width:0.##}\" height=\"{bgRect.Height:0.##}\" fill=\"{render.Theme.Bg}\"{fillOpacity}/>");
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
        // Each cell's foreground, resolved once for the forms below, which each looked it up again.
        var foregrounds = ResolveRowForegrounds(buffer, row, theme);
        // What AppendRowAt needs to make the absolute encodings of this row from this one.
        var parts = t_rowParts ??= new RowParts();
        parts.Clear(foregrounds);
        var hasUnderline = AppendBackgroundSpans(sb, buffer, row, originX, rowTop, context, out var backgroundRects);
        var blockRects = AppendBlockRects(sb, buffer, row, originX, rowTop, metrics, foregrounds, absoluteRowTop, out var blockYGrowth);
        parts.UsesStart = sb.Length;
        var glyphUses = AppendSoftGlyphs(sb, buffer, row, originX, textYAttribute, context, foregrounds, parts.UseYs);
        parts.UsesEnd = sb.Length;
        var rectsLength = sb.Length - start;

        var runForm = t_runForm ??= new StringBuilder(1024);
        runForm.Clear();
        var runTexts = AppendRunText(runForm, buffer, row, originX, textYAttribute, context, foregrounds, parts.RunYs);
        var texts = runTexts;
        var listLength = int.MaxValue;
        var listTexts = 0;
        parts.HasUnderline = hasUnderline;
        parts.GlyphUses = glyphUses;
        if (hasUnderline)
        {
            sb.Append(runForm);
        }
        else
        {
            var listForm = t_listForm ??= new StringBuilder(1024);
            listForm.Clear();
            listTexts = AppendGlyphListText(listForm, buffer, row, originX, textYAttribute, context, foregrounds, parts.ListYs);
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

    /// <summary>
    /// The row's absolute encoding at <paramref name="top"/>, as <see cref="AppendRow"/> with <c>rowLocal: false</c>
    /// gives it, made from the row-local encoding in <paramref name="local"/> that the last AppendRow call wrote. The
    /// rects are drawn again at that top, and the soft glyphs and the text are copied with a <c>y</c> put in where
    /// each one takes it. Encoding the row again resolved every cell's colors and wrote every glyph a second time,
    /// and a third for the check of a scrolled row against an absolute layer.
    /// </summary>
    private static RowShape AppendRowAt(StringBuilder sb, ScreenBuffer buffer, int row, double originX, double top, EncodeContext context, StringBuilder local)
    {
        var parts = t_rowParts!;
        var start = sb.Length;
        var textY = string.Create(CultureInfo.InvariantCulture, $" y=\"{top + context.Metrics.BaselineOffset:0.##}\"");
        AppendBackgroundSpans(sb, buffer, row, originX, top, context, out var backgroundRects);
        var blockRects = AppendBlockRects(sb, buffer, row, originX, top, context.Metrics, parts.Foregrounds, top, out _);
        AppendWithY(sb, local, parts.UsesStart, parts.UsesEnd, parts.UseYs, textY);

        // Each <text> gets one y, so the forms' lengths with it tell which AppendRow would keep.
        var runForm = t_runForm!;
        var useList = false;
        if (!parts.HasUnderline)
        {
            var listForm = t_listForm!;
            useList = listForm.Length + parts.ListYs.Count * textY.Length < runForm.Length + parts.RunYs.Count * textY.Length;
        }

        var form = useList ? t_listForm! : runForm;
        var ys = useList ? parts.ListYs : parts.RunYs;
        AppendWithY(sb, form, 0, form.Length, ys, textY);
        var elements = backgroundRects + blockRects + parts.GlyphUses + ys.Count;
        return new RowShape(elements, elements == 1 && ys.Count == 1, sb.Length - start);
    }

    // source[start..end] with y put in at each of the places, which are positions in source in rising order.
    private static void AppendWithY(StringBuilder sb, StringBuilder source, int start, int end, List<int> places, string y)
    {
        var from = start;
        foreach (var place in places)
        {
            sb.Append(source, from, place - from).Append(y);
            from = place;
        }

        sb.Append(source, from, end - from);
    }

    /// <summary>
    /// What <see cref="AppendRowAt"/> takes from the row-local encoding of a row: where its soft glyphs' &lt;use&gt;s
    /// are, where each element there and in both text forms takes its <c>y</c>, and the row's foregrounds.
    /// </summary>
    private sealed class RowParts
    {
        internal string?[] Foregrounds { get; private set; } = [];
        internal int UsesStart { get; set; }
        internal int UsesEnd { get; set; }
        internal List<int> UseYs { get; } = [];
        internal List<int> RunYs { get; } = [];
        internal List<int> ListYs { get; } = [];
        internal bool HasUnderline { get; set; }
        internal int GlyphUses { get; set; }

        internal void Clear(string?[] foregrounds)
        {
            Foregrounds = foregrounds;
            UseYs.Clear();
            RunYs.Clear();
            ListYs.Clear();
            HasUnderline = false;
            GlyphUses = 0;
        }
    }

    private static string Format(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>
    /// Each cell's foreground as <see cref="CellColors.ResolveTintedForeground"/> gives it, in a buffer that the next
    /// call fills again. A wide cell's continuation gets null: no form draws one.
    /// </summary>
    private static string?[] ResolveRowForegrounds(ScreenBuffer buffer, int row, TerminalTheme theme)
    {
        var foregrounds = t_rowForegrounds;
        if (foregrounds is null || foregrounds.Length < buffer.Width)
            t_rowForegrounds = foregrounds = new string?[buffer.Width];
        for (var col = 0; col < buffer.Width; col++)
        {
            var cell = buffer.GetCell(row, col);
            foregrounds[col] = cell.IsWideContinuation ? null : CellColors.ResolveTintedForeground(buffer, row, col, cell, theme);
        }

        return foregrounds;
    }

    /// <summary>
    /// One rect per run of cells with the same non-theme background, regardless of foreground
    /// or style. A wide cell's continuation takes the wide cell's background. Returns whether
    /// any cell in the row is underlined.
    /// </summary>
    private static bool AppendBackgroundSpans(
        StringBuilder sb, ScreenBuffer buffer, int row, double originX, double rowTop, EncodeContext context, out int rects)
    {
        var theme = context.Theme;
        rects = 0;
        var hasUnderline = false;
        string? spanBg = null;
        string? previousBg = null;
        string? rowTopText = null;
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
                    sb.Append("<rect x=\"").Append(context.ColumnX(originX, spanStart))
                        .Append("\" y=\"").Append(rowTopText ??= Format(rowTop))
                        .Append("\" width=\"").Append(context.ColumnsWidth(col - spanStart))
                        .Append("\" height=\"").Append(context.LineHeightText)
                        .Append("\" fill=\"").Append(spanBg).Append("\"/>");
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
        StringBuilder sb, ScreenBuffer buffer, int row, double originX, double rowTop, SvgMetrics metrics, string?[] foregrounds,
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

            AddBlockParts(parts, codepoint, col * 8, cell.IsWide ? 2 : 1, foregrounds[col]!);
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
        StringBuilder sb, ScreenBuffer buffer, int row, double originX, string textYAttribute, EncodeContext context, string?[] foregrounds, List<int> ys)
    {
        var uses = 0;
        for (var col = 0; col < buffer.Width; col++)
        {
            var cell = buffer.GetCell(row, col);
            if (cell.Glyph is not { } glyph)
                continue;

            // A soft glyph's cell may be a wide continuation, which ResolveRowForegrounds leaves out.
            var fill = foregrounds[col] ?? CellColors.ResolveTintedForeground(buffer, row, col, cell, context.Theme);
            sb.Append("<use href=\"#").Append(context.Glyphs.IdFor(glyph)).Append('"')
                .Append(" x=\"").Append(context.ColumnX(originX, col)).Append('"');
            ys.Add(sb.Length);
            sb.Append(textYAttribute);
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
        StringBuilder sb, ScreenBuffer buffer, int row, double originX, string textYAttribute, EncodeContext context, string?[] foregrounds, List<int> ys)
    {
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
            var runFg = foregrounds[col]!;
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

                var nextFg = foregrounds[col];
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

            AppendTextStart(sb, runFg, context.Fills);
            sb.Append(" x=\"").Append(context.ColumnX(originX, runStart)).Append('"');
            ys.Add(sb.Length);
            sb.Append(textYAttribute);
            AppendFontStyle(sb, runBold, runItalic, runUnderline);
            sb.Append(" textLength=\"").Append(context.ColumnsWidth(drawnEnd - runStart)).Append("\">");
            AppendEscapedXml(sb, runText, drawnChars);
            sb.Append("</text>");
            texts++;
        }

        return texts;
    }

    /// <summary>The first <paramref name="length"/> characters of <paramref name="text"/>, escaped as <see cref="EscapeXml"/> escapes them.</summary>
    private static void AppendEscapedXml(StringBuilder sb, StringBuilder text, int length)
    {
        foreach (var chunk in text.GetChunks())
        {
            if (length == 0)
                break;

            var span = chunk.Span;
            if (span.Length > length)
                span = span[..length];
            length -= span.Length;
            var start = 0;
            for (var i = 0; i < span.Length; i++)
            {
                var c = span[i];
                if (c is not ('&' or '<' or '>' or '"') && !IsXmlInvalid(c))
                    continue;

                sb.Append(span[start..i]);
                switch (c)
                {
                    case '&': sb.Append("&amp;"); break;
                    case '<': sb.Append("&lt;"); break;
                    case '>': sb.Append("&gt;"); break;
                    case '"': sb.Append("&quot;"); break;
                    default: sb.Append('�'); break;
                }

                start = i + 1;
            }

            sb.Append(span[start..]);
        }
    }

    /// <summary>
    /// Glyph-list form: one &lt;text&gt; per (fg, bold, italic) with an x per glyph, spaces omitted.
    /// Cells of more than one UTF-16 unit (surrogate pairs, combining marks) get their own
    /// &lt;text&gt;: SVG assigns x values per addressable character, which browsers count
    /// differently for those. Returns the number of &lt;text&gt; elements.
    /// </summary>
    private static int AppendGlyphListText(
        StringBuilder sb, ScreenBuffer buffer, int row, double originX, string textYAttribute, EncodeContext context, string?[] foregrounds, List<int> ys)
    {
        var texts = 0;
        var groups = t_glyphGroups ??= [];
        // The groups by foreground, one table per bold and italic pair. A program with many colors, such as a matrix
        // rain with ~190 shades of green, has as many groups in a row, and finding each glyph's group by walking the
        // list cost a pass over them per glyph.
        var byStyle = t_glyphGroupsByStyle ??= new Dictionary<string, GlyphGroup>[4];
        var used = 0;
        for (var col = 0; col < buffer.Width; col++)
        {
            var cell = buffer.GetCell(row, col);
            var text = cell.Text;
            if (cell.IsWideContinuation || text.Length == 0 || (text.Length == 1 && char.IsWhiteSpace(text[0])) || cell.Glyph is not null || IsBlockCell(text))
                continue;

            var fg = foregrounds[col]!;
            var x = context.ColumnX(originX, col);
            if (text.Length != 1)
            {
                AppendTextStart(sb, fg, context.Fills);
                sb.Append(" x=\"").Append(x).Append('"');
                ys.Add(sb.Length);
                sb.Append(textYAttribute);
                AppendFontStyle(sb, cell.Bold, cell.Italic, underline: false);
                sb.Append('>').Append(EscapeXml(text)).Append("</text>");
                texts++;
                continue;
            }

            var byFg = byStyle[(cell.Bold ? 1 : 0) | (cell.Italic ? 2 : 0)] ??= [];
            if (byFg.TryGetValue(fg, out var group))
            {
                group.Xs.Append(' ');
            }
            else
            {
                if (used == groups.Count)
                    groups.Add(new GlyphGroup());
                group = groups[used++];
                group.Reset(fg, cell.Bold, cell.Italic);
                byFg.Add(fg, group);
            }

            group.Xs.Append(x);
            group.Text.Append(EscapeXml(text));
        }

        foreach (var byFg in byStyle)
            byFg?.Clear();

        for (var i = 0; i < used; i++)
        {
            var group = groups[i];
            AppendTextStart(sb, group.Fg, context.Fills);
            sb.Append(" x=\"").Append(group.Xs).Append('"');
            ys.Add(sb.Length);
            sb.Append(textYAttribute);
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

    /// <summary>The SVG's width and height in pixels for a terminal of <paramref name="cols"/> by <paramref name="rows"/>.</summary>
    internal static (double Width, double Height) Measure(ResolvedRenderSettings render, int cols, int rows)
    {
        var metrics = CreateMetrics(render, cols, rows);
        return (metrics.SvgWidth, metrics.SvgHeight);
    }

    private static SvgMetrics CreateMetrics(ResolvedRenderSettings render, int cols, int rows)
    {
        var fontSize = render.FontSize;
        var window = render.Window;
        var lineHeight = fontSize * LineHeightFactor;
        var charWidth = fontSize * CharWidthFactor;
        var (innerPaddingH, innerPaddingV) = ResolveInnerPadding(fontSize, render.Padding);
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

        outerMargin = ResolveMargin(render.Margin, render.Background is not null, outerMargin);

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

    /// <summary>Padding between the terminal's edge and its text: <paramref name="padding"/> on every side when set, else from the font size.</summary>
    internal static (double horizontal, double vertical) ResolveInnerPadding(int fontSize, int? padding = null)
    {
        if (padding is int fixedPadding)
            return (fixedPadding, fixedPadding);

        var horizontal = Math.Clamp(fontSize * 10.0 / 16.0, InnerPaddingHorizontalMin, InnerPaddingHorizontalMax);
        var vertical = Math.Clamp(fontSize * 4.0 / 16.0, InnerPaddingVerticalMin, InnerPaddingVerticalMax);
        return (horizontal, vertical);
    }

    /// <summary>
    /// The margin around the terminal or window: <paramref name="margin"/> when set; with a background,
    /// <see cref="RenderSettingsResolver.DefaultBackgroundMargin"/>, so some of it shows; else the default of the
    /// window style, room for the shadow with chrome.
    /// </summary>
    internal static double ResolveMargin(int? margin, bool hasBackground, double styleDefault) =>
        margin ?? (hasBackground ? RenderSettingsResolver.DefaultBackgroundMargin : styleDefault);

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

