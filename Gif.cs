using System.Globalization;

internal sealed record GifResult(int Width, int Height, int Fps, int Frames, long Bytes, FontFaceInfo Font);

/// <summary>
/// An animated GIF of a recording: the mp4's frames, at a GIF frame rate, turned into a GIF by ffmpeg with one palette
/// for the whole animation. The frames are drawn twice: the first pass finds the palette, the second maps each frame
/// to it, so ffmpeg never holds every frame in memory, as a one-pass <c>split</c> does.
/// </summary>
internal static class GifRender
{
    internal const int DefaultFps = 20;

    /// <summary>
    /// A GIF stores each frame's delay in hundredths of a second, and browsers slow delays under 2 hundredths down to
    /// 10, so 50 fps is the most a GIF plays at.
    /// </summary>
    internal const int MaxFps = 50;

    /// <summary>Zenn takes uploads up to 3MB. A larger GIF gets a warning with what makes it smaller.</summary>
    internal const long LargeGifBytes = 3_000_000;

    /// <summary>
    /// Renders and encodes to <paramref name="outputPath"/> through ffmpeg at <paramref name="ffmpegPath"/>. Every font
    /// file it loaded is released when it returns or throws, as for mp4.
    /// </summary>
    internal static GifResult WriteGif(
        IReadOnlyList<CastEvent> events,
        int width,
        int height,
        ResolvedRenderSettings render,
        Mp4Settings settings,
        string outputPath,
        string ffmpegPath,
        double? loopDurationOverride = null)
    {
        try
        {
            return Encode(events, width, height, render, settings, outputPath, ffmpegPath, loopDurationOverride);
        }
        finally
        {
            LoadedFace.ReleaseAll();
        }
    }

    /// <summary>The settings a GIF draws with: one that plays once has no fade-out, as an SVG that plays once has none.</summary>
    internal static ResolvedRenderSettings ForGif(ResolvedRenderSettings render) => render.Loops ? render : render with { FadeOut = null };

    /// <summary>The GIF's size: <c>--size</c>, or the frame fitted within <c>--max-size</c>.</summary>
    internal static (int Width, int Height) OutputSize(RasterLayout layout, Mp4Settings settings) =>
        settings.FrameWidth > 0
            ? (settings.FrameWidth, settings.FrameHeight)
            : Mp4Render.FitWithin(layout.Width, layout.Height, settings.MaxWidth, settings.MaxHeight);

    private static GifResult Encode(
        IReadOnlyList<CastEvent> events,
        int width,
        int height,
        ResolvedRenderSettings render,
        Mp4Settings settings,
        string outputPath,
        string ffmpegPath,
        double? loopDurationOverride)
    {
        var fullOutputPath = Path.GetFullPath(outputPath);
        var directory = Path.GetDirectoryName(fullOutputPath) ?? ".";
        if (!Directory.Exists(directory))
            throw new Mp4Exception($"output folder does not exist: {directory}");
        if (Directory.Exists(fullOutputPath))
            throw new Mp4Exception($"output path is a directory: {fullOutputPath}");

        render = ForGif(render);
        var loopDuration = RenderSettingsResolver.ResolveLoopDuration(
            loopDurationOverride ?? TerminalReplay.ComputeLoopDuration(events), events, render);
        var renderer = new Mp4FrameRenderer(events, width, height, render, settings, loopDuration, ffmpegPath);
        var layout = renderer.Layout;
        var (outputWidth, outputHeight) = OutputSize(layout, settings);

        // As for mp4: an earlier output goes before encoding, and the GIF is written to a temporary file that is
        // renamed only when ffmpeg succeeds. The palette is a temporary file too.
        if (File.Exists(fullOutputPath))
            File.Delete(fullOutputPath);

        var stem = Path.Combine(directory, $".{Path.GetFileName(outputPath)}.{Guid.NewGuid():N}");
        var palettePath = stem + ".palette.png";
        var tempPath = stem + ".partial";
        FfmpegProcess? ffmpeg = null;
        ConsoleCancelEventHandler onCancel = (_, _) =>
        {
            Volatile.Read(ref ffmpeg)?.Kill();
            Mp4Render.TryDelete(palettePath);
            Mp4Render.TryDelete(tempPath);
        };
        Console.CancelKeyPress += onCancel;
        try
        {
            foreach (var arguments in new[]
            {
                PaletteArguments(layout.Width, layout.Height, settings.Fps, outputWidth, outputHeight, palettePath),
                GifArguments(layout.Width, layout.Height, settings.Fps, outputWidth, outputHeight, palettePath, render.Loops, tempPath),
            })
            {
                Volatile.Write(ref ffmpeg, FfmpegProcess.Start(ffmpegPath, arguments));
                renderer.Render(ffmpeg.Write);
                ffmpeg.Finish();
                ffmpeg.Dispose();
                Volatile.Write(ref ffmpeg, null);
            }

            File.Move(tempPath, outputPath, overwrite: true);
        }
        catch
        {
            ffmpeg?.Kill();
            Mp4Render.TryDelete(tempPath);
            throw;
        }
        finally
        {
            Console.CancelKeyPress -= onCancel;
            ffmpeg?.Dispose();
            Mp4Render.TryDelete(palettePath);
        }

        return new GifResult(outputWidth, outputHeight, settings.Fps, renderer.FrameCount, new FileInfo(outputPath).Length, renderer.Glyphs.Primary);
    }

    /// <summary>
    /// The first pass: one palette of up to 256 colors from every frame (<c>stats_mode=full</c>), with one entry kept for
    /// transparency, which the second pass uses for pixels that did not change.
    /// </summary>
    internal static List<string> PaletteArguments(int width, int height, int fps, int outputWidth, int outputHeight, string palettePath) =>
    [
        .. InputArguments(width, height, fps),
        "-vf", Scale(width, height, outputWidth, outputHeight) + ",palettegen=stats_mode=full",
        "-update", "1", "-y", palettePath,
    ];

    /// <summary>
    /// The second pass: each frame mapped to the palette without dithering, which would speckle the flat backgrounds
    /// and the edges of text, and only the rectangle that changed since the previous frame is stored. A GIF that
    /// plays once has no loop count (<c>-loop -1</c>).
    /// </summary>
    internal static List<string> GifArguments(
        int width, int height, int fps, int outputWidth, int outputHeight, string palettePath, bool loops, string outputPath) =>
    [
        .. InputArguments(width, height, fps),
        "-i", palettePath,
        "-lavfi", $"[0:v]{Scale(width, height, outputWidth, outputHeight)}[frames];[frames][1:v]paletteuse=dither=none:diff_mode=rectangle",
        "-loop", loops ? "0" : "-1",
        "-f", "gif", "-y", outputPath,
    ];

    private static List<string> InputArguments(int width, int height, int fps) =>
    [
        "-hide_banner", "-loglevel", "error",
        "-f", "rawvideo", "-pix_fmt", "rgb24", "-s", $"{width}x{height}",
        "-r", fps.ToString(CultureInfo.InvariantCulture), "-i", "-",
    ];

    // A filter graph needs a filter on each chain, so an unscaled frame passes through null.
    private static string Scale(int width, int height, int outputWidth, int outputHeight) =>
        outputWidth != width || outputHeight != height
            ? $"scale={outputWidth}:{outputHeight}:flags=lanczos"
            : "null";
}
