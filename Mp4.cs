using System.Diagnostics;
using System.Globalization;
using System.Text;

/// <summary>mp4 options. Defaults and the reasons for them are in .github/docs/spec_mp4.md.</summary>
/// <remarks><see cref="FrameWidth"/> and <see cref="FrameHeight"/> are <c>--size</c>; 0 means none.</remarks>
internal readonly record struct Mp4Settings(int Fps, int Crf, int MaxWidth, int MaxHeight, int FrameWidth, int FrameHeight, string? FontFile)
{
    internal const int DefaultFps = 30, MinFps = 1, MaxFps = 60;
    // CRF 0 is lossless, and x264 then writes the High 4:4:4 Predictive profile, which X and many players reject.
    internal const int DefaultCrf = 23, MinCrf = 1, MaxCrf = 51;
    internal const int MinSize = 16, MaxSize = 16384;
    // x264 settings chosen for terminal frames. The measurements are in spec_mp4.md → Lessons Learned.
    internal const string Preset = "slower";
    internal const int PresetRefs = 8;          // x264 "slower"
    internal const int MotionSearchRange = 128; // pixels; "slower" searches 16, less than one row at the default font size
    internal const int SceneCutThreshold = 10;  // x264 defaults to 40; 0 would turn scene cuts off
    internal const int MaxForcedKeyframes = 1000; // keeps -force_key_frames well inside Windows' 32767-character command line
    internal const int KeyframeSeconds = 30;    // longest gap between keyframes, for seeking
    internal const int Level52DpbMacroblocks = 184_320; // H.264 level 5.2 decoded picture buffer

    internal static Mp4Settings Default => new(DefaultFps, DefaultCrf, 0, 0, 0, 0, null);

    internal static bool TryParseFps(string text, out int fps, out string error) =>
        TryParseInt(text, "--fps", MinFps, MaxFps, out fps, out error);

    internal static bool TryParseCrf(string text, out int crf, out string error) =>
        TryParseInt(text, "--crf", MinCrf, MaxCrf, out crf, out error);

    /// <summary>Parses <c>--max-size WxH</c>, for example <c>1280x1024</c>.</summary>
    internal static bool TryParseMaxSize(string text, out int width, out int height, out string error) =>
        TryParseDimensions(text, "--max-size", "1280x1024", out width, out height, out error);

    /// <summary>Parses <c>--size WxH</c>, for example <c>1920x1080</c>. The video is exactly this size, so both sides must be even for yuv420p.</summary>
    internal static bool TryParseSize(string text, out int width, out int height, out string error)
    {
        if (!TryParseDimensions(text, "--size", "1920x1080", out width, out height, out error))
            return false;

        if (((width | height) & 1) != 0)
        {
            error = $"--size must have an even width and height, which yuv420p requires: {text}";
            return false;
        }

        return true;
    }

    private static bool TryParseDimensions(string text, string name, string example, out int width, out int height, out string error)
    {
        width = height = 0;
        var parts = text.Split('x', 'X');
        if (parts.Length != 2
            || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out width)
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out height)
            || width is < MinSize or > MaxSize || height is < MinSize or > MaxSize)
        {
            error = $"{name} must be WIDTHxHEIGHT with each side between {MinSize} and {MaxSize} (for example {example}): {text}";
            return false;
        }

        error = "";
        return true;
    }

    private static bool TryParseInt(string text, string name, int min, int max, out int value, out string error)
    {
        if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value) || value < min || value > max)
        {
            error = $"{name} must be between {min} and {max}: {text}";
            return false;
        }

        error = "";
        return true;
    }
}

internal sealed class Mp4Exception(string message) : Exception(message);

internal sealed record Mp4Result(int Width, int Height, int Fps, int Frames, long Bytes, FontFaceInfo Font);

/// <summary>
/// Renders a recording at a constant frame rate. Frame <c>k</c> shows the screen after
/// every event with time ≤ <c>k / fps</c>, through the same loop period as the SVG.
/// </summary>
internal sealed class Mp4FrameRenderer
{
    private readonly IReadOnlyList<CastEvent> _events;
    private readonly int _initialWidth;
    private readonly int _initialHeight;
    private readonly TerminalTheme _theme;
    private readonly int _fps;
    private readonly ResolvedRenderSettings _render;
    private readonly string? _fontFile;
    private readonly string? _ffmpegPath;
    private readonly double _fadeOut;
    private readonly double _fadeStart;
    private FixedRatePlan? _plan;

    /// <param name="loopDuration">The video's length, with the end hold and the fade-out (<see cref="RenderSettingsResolver.ResolveLoopDuration"/>).</param>
    /// <param name="ffmpegPath">ffmpeg, which decodes a background image. Needed only when the background is an image.</param>
    internal Mp4FrameRenderer(
        IReadOnlyList<CastEvent> events,
        int width,
        int height,
        ResolvedRenderSettings render,
        Mp4Settings settings,
        double loopDuration,
        string? ffmpegPath = null)
    {
        _events = events;
        _initialWidth = width;
        _initialHeight = height;
        _theme = TerminalTheme.FromResolved(render.Theme);
        _fps = settings.Fps;
        _render = render;
        _fontFile = settings.FontFile;
        _ffmpegPath = ffmpegPath;
        var (cols, rows) = TerminalReplay.ResolveCanvasSize(width, height, events);
        Layout = Mp4Render.CreateLayout(render, cols, rows, settings);
        Glyphs = GlyphSource.Create(render.FontFamily, settings.FontFile, Layout);
        FrameCount = CountFrames(loopDuration, settings.Fps);
        _fadeOut = render.FadeOut ?? 0;
        _fadeStart = loopDuration - _fadeOut;
    }

    /// <summary>Frames in a loop of <paramref name="loopDuration"/> seconds, at least one.</summary>
    internal static int CountFrames(double loopDuration, int fps) => Math.Max(1, (int)Math.Ceiling(loopDuration * fps - 1e-9));

    internal RasterLayout Layout { get; }

    internal GlyphSource Glyphs { get; }

    internal int FrameCount { get; }

    /// <summary>Where the replay stops and where full-screen programs switch screens. One parse of the recording, done once.</summary>
    internal FixedRatePlan Plan =>
        _plan ??= TerminalReplay.PlanFixedRate(_events, _initialWidth, _initialHeight, Layout.Cols, Layout.Rows, _theme, _fps, FrameCount);

    /// <summary>Frames to encode as keyframes: see <see cref="Mp4Render.ChooseKeyframes"/>.</summary>
    internal IReadOnlyList<int> Keyframes => Mp4Render.ChooseKeyframes(Plan.ScreenSwitchFrames, _fps);

    /// <summary>
    /// Glyphs for the window title: the SVG title's sans-serif families (<see cref="SvgFrameRenderer.ChromeTitleFontFamily"/>)
    /// that are installed here, at the title's size. With none of them and no fallback font installed either, the
    /// terminal's font, as the title used before.
    /// </summary>
    internal static GlyphSource CreateTitleGlyphs(WindowStyle window, string fontFamily, string? fontFile)
    {
        var titleLayout = RasterLayout.Create((int)SvgFrameRenderer.TitleFontSize(window), 1, 1);
        try
        {
            return GlyphSource.Create(SvgFrameRenderer.ChromeTitleFontFamily, null, titleLayout);
        }
        catch (FontNotFoundException)
        {
            return GlyphSource.Create(fontFamily, fontFile, titleLayout);
        }
    }

    /// <summary>Calls <paramref name="sink"/> with each RGB24 frame in order. The buffer is reused between calls.</summary>
    internal void Render(Action<byte[]> sink)
    {
        var desktop = _render.Background is { } background
            ? DesktopRaster.Render(Layout, background, (frameWidth, frameHeight) => Ffmpeg.DecodeImage(
                _ffmpegPath ?? throw new Mp4Exception("a background image needs ffmpeg to decode it"), background.ImagePath!, frameWidth, frameHeight))
            : null;

        GlyphSource? titleGlyphs = null;
        if (Layout.Window != WindowStyle.None && !string.IsNullOrEmpty(_render.WindowTitle))
            titleGlyphs = CreateTitleGlyphs(Layout.Window, _render.FontFamily, _fontFile);

        var raster = new FrameRasterizer(Layout, _theme, Glyphs, desktop, titleGlyphs, _render.WindowTitle, _render.WindowOpacity, _render.WindowBlur);
        var stepper = new ReplayStepper(_events, _initialWidth, _initialHeight, Layout.Cols, Layout.Rows, _theme, Plan.StopIndex);
        byte[]? empty = null;
        byte[]? faded = null;
        for (var frame = 0; frame < FrameCount; frame++)
        {
            if (stepper.AdvanceTo(frame / (double)_fps))
                raster.Draw(stepper.View);

            // The fade-out comes after the last event, so the screen no longer changes. A frame stands for the time up
            // to the next one, so the last frame of the video is fully faded.
            var fade = _fadeOut > 0 ? Math.Clamp(((frame + 1) / (double)_fps - _fadeStart) / _fadeOut, 0, 1) : 0;
            if (fade <= 0)
            {
                sink(raster.Pixels);
                continue;
            }

            if (empty is null)
            {
                (empty, faded) = (DrawEmpty(raster, stepper.View), new byte[raster.Pixels.Length]);
                raster.Draw(stepper.View);
            }

            Blend(raster.Pixels, empty, fade, faded!);
            sink(faded!);
        }
    }

    /// <summary>
    /// The frame without the terminal's contents: its background, the window, and the background around it, which a
    /// fade-out does not touch. Leaves <paramref name="raster"/> drawn empty.
    /// </summary>
    private byte[] DrawEmpty(FrameRasterizer raster, ScreenBuffer view)
    {
        raster.Draw(new ScreenBuffer(view.Width, view.Height, _theme) { CursorVisible = false });
        return raster.Pixels.ToArray();
    }

    /// <summary>Writes <paramref name="frame"/> moved toward <paramref name="empty"/> by <paramref name="amount"/>, 0 to 1.</summary>
    internal static void Blend(byte[] frame, byte[] empty, double amount, byte[] destination)
    {
        var weight = (int)Math.Round(amount * 256);
        for (var i = 0; i < frame.Length; i++)
            destination[i] = (byte)(frame[i] + (((empty[i] - frame[i]) * weight + 128) >> 8));
    }
}

internal static class Mp4Render
{
    /// <summary>
    /// Renders and encodes to <paramref name="outputPath"/> through ffmpeg at <paramref name="ffmpegPath"/>.
    /// Every font file it loaded is released when it returns or throws, because <c>post</c>, which runs
    /// even after a failed render, may replace or delete one, and Windows does not allow that while it is mapped.
    /// </summary>
    internal static Mp4Result WriteMp4(
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

    private static Mp4Result Encode(
        IReadOnlyList<CastEvent> events,
        int width,
        int height,
        ResolvedRenderSettings render,
        Mp4Settings settings,
        string outputPath,
        string ffmpegPath,
        double? loopDurationOverride)
    {
        // Check the output path first: ffmpeg would report a missing folder under the temporary file's
        // name, and a directory in the way would only fail at the final rename, after the whole encode.
        var fullOutputPath = Path.GetFullPath(outputPath);
        var directory = Path.GetDirectoryName(fullOutputPath) ?? ".";
        if (!Directory.Exists(directory))
            throw new Mp4Exception($"output folder does not exist: {directory}");
        if (Directory.Exists(fullOutputPath))
            throw new Mp4Exception($"output path is a directory: {fullOutputPath}");

        var loopDuration = RenderSettingsResolver.ResolveLoopDuration(
            loopDurationOverride ?? TerminalReplay.ComputeLoopDuration(events), events, render);
        var renderer = new Mp4FrameRenderer(events, width, height, render, settings, loopDuration, ffmpegPath);
        var layout = renderer.Layout;
        var (outputWidth, outputHeight) = OutputSize(layout, settings);

        // An output from an earlier run is deleted before encoding. Ctrl+C ends the process from the
        // handler below, so deleting it only after a failure would leave it behind in that case.
        if (File.Exists(fullOutputPath))
            File.Delete(fullOutputPath);

        var tempPath = Path.Combine(directory, $".{Path.GetFileName(outputPath)}.{Guid.NewGuid():N}.partial");
        var arguments = BuildArguments(layout.Width, layout.Height, settings, outputWidth, outputHeight, tempPath, renderer.Keyframes);

        // On Ctrl+C the process ends soon after this handler returns, and the catch and finally
        // blocks below may not get to run. So the handler stops ffmpeg and removes the partial file
        // itself. It is registered before ffmpeg starts, so no Ctrl+C can leave a partial file behind.
        FfmpegProcess? ffmpeg = null;
        ConsoleCancelEventHandler onCancel = (_, _) =>
        {
            Volatile.Read(ref ffmpeg)?.Kill();
            TryDelete(tempPath);
        };
        Console.CancelKeyPress += onCancel;
        try
        {
            Volatile.Write(ref ffmpeg, FfmpegProcess.Start(ffmpegPath, arguments));
            renderer.Render(ffmpeg.Write);
            ffmpeg.Finish();
            File.Move(tempPath, outputPath, overwrite: true);
        }
        catch
        {
            ffmpeg?.Kill();
            TryDelete(tempPath);
            throw;
        }
        finally
        {
            Console.CancelKeyPress -= onCancel;
            ffmpeg?.Dispose();
        }

        return new Mp4Result(outputWidth, outputHeight, settings.Fps, renderer.FrameCount, new FileInfo(outputPath).Length, renderer.Glyphs.Primary);
    }

    /// <summary>
    /// ffmpeg arguments: raw RGB24 on stdin, an optional lanczos downscale, BT.709 conversion and
    /// tags (untagged BT.601 output shifts theme colors in players), and H.264 High via libx264.
    /// <list type="bullet">
    /// <item><c>-psy 0</c>: psychovisual tuning spends bits on texture detail, which terminal frames of
    /// flat color and sharp text do not have.</item>
    /// <item><c>merange</c>: a scroll moves text by whole rows, 20 pixels at the default font size. The
    /// preset's 16-pixel search misses it and codes the moved text again. A wider search also finds the
    /// same character elsewhere on screen, which matters for redrawn TUIs.</item>
    /// <item><c>scenecut</c>: when output scrolls the screen, x264's default threshold starts a keyframe
    /// that codes the whole screen again, though most of it only moved. A lower threshold keeps
    /// those changes in predicted frames. It also misses some screens a TUI replaces, which
    /// <c>-force_key_frames</c> covers.</item>
    /// <item><c>-g</c>: each keyframe redraws the whole screen, and a mostly static terminal needs few.
    /// One at least every <see cref="Mp4Settings.KeyframeSeconds"/> keeps seeking cheap.</item>
    /// <item><c>-force_key_frames</c>: <paramref name="keyframes"/>, where a full-screen program replaced the
    /// screen. Each time is half a frame early, because ffmpeg forces the first frame at or after it.</item>
    /// <item><c>-refs</c>: capped for large frames so the stream stays within level 5.2, which hardware
    /// decoders support widely. 8 references at 4K would need level 6.0.</item>
    /// </list>
    /// </summary>
    internal static List<string> BuildArguments(
        int width, int height, Mp4Settings settings, int outputWidth, int outputHeight, string outputPath, IReadOnlyList<int>? keyframes = null)
    {
        var scale = outputWidth != width || outputHeight != height
            ? $"scale={outputWidth}:{outputHeight}:flags=lanczos:out_color_matrix=bt709:out_range=tv"
            : "scale=out_color_matrix=bt709:out_range=tv";
        var macroblocks = ((outputWidth + 15) / 16) * ((outputHeight + 15) / 16);
        var refs = Math.Clamp(Mp4Settings.Level52DpbMacroblocks / macroblocks, 1, Mp4Settings.PresetRefs);
        List<string> arguments =
        [
            "-hide_banner", "-loglevel", "error",
            "-f", "rawvideo", "-pix_fmt", "rgb24", "-s", $"{width}x{height}",
            "-r", settings.Fps.ToString(CultureInfo.InvariantCulture), "-i", "-",
            "-vf", scale + ",format=yuv420p",
            "-c:v", "libx264", "-preset", Mp4Settings.Preset, "-crf", settings.Crf.ToString(CultureInfo.InvariantCulture),
            "-psy", "0", "-refs", refs.ToString(CultureInfo.InvariantCulture),
            "-x264-params", string.Create(CultureInfo.InvariantCulture, $"merange={Mp4Settings.MotionSearchRange}:scenecut={Mp4Settings.SceneCutThreshold}"),
            "-g", (settings.Fps * Mp4Settings.KeyframeSeconds).ToString(CultureInfo.InvariantCulture),
        ];
        if (keyframes is { Count: > 0 })
        {
            arguments.Add("-force_key_frames");
            arguments.Add(string.Join(',', keyframes.Select(frame => ((frame - 0.5) / settings.Fps).ToString("0.######", CultureInfo.InvariantCulture))));
        }

        arguments.AddRange(
        [
            "-colorspace", "bt709", "-color_primaries", "bt709", "-color_trc", "bt709", "-color_range", "tv",
            "-movflags", "+faststart", "-f", "mp4", "-y", outputPath,
        ]);
        return arguments;
    }

    /// <summary>
    /// The frames to force as keyframes, from the frames where a full-screen program entered or left the
    /// alternate screen and the new screen first shows. x264's own scene-cut test misses some of these, more
    /// so when <c>--size</c> pads the frame, and then codes the new screen in predicted frames at lower quality
    /// (spec_mp4.md → Lessons Learned → Scene-cut threshold). A switch less than a second after the last
    /// forced keyframe is left to x264, so a program that flips screens rapidly does not turn every frame
    /// into a keyframe, and at most <see cref="Mp4Settings.MaxForcedKeyframes"/> are forced.
    /// </summary>
    internal static List<int> ChooseKeyframes(IReadOnlyList<int> screenSwitchFrames, int fps)
    {
        var chosen = new List<int>();
        foreach (var frame in screenSwitchFrames)
        {
            if (chosen.Count == Mp4Settings.MaxForcedKeyframes)
                break;
            if (chosen.Count == 0 || frame - chosen[^1] >= fps)
                chosen.Add(frame);
        }

        return chosen;
    }

    /// <summary>The drawn frame: the terminal at its own size, or inside the <c>--size</c> frame.</summary>
    internal static RasterLayout CreateLayout(int fontSize, int cols, int rows, WindowStyle window, Mp4Settings settings) =>
        CreateLayout(new ResolvedRenderSettings(fontSize, RenderSettingsResolver.DefaultFontFamily, default, window), cols, rows, settings);

    /// <summary>The drawn frame for <paramref name="render"/>'s font size, window, padding, margin, and background.</summary>
    internal static RasterLayout CreateLayout(ResolvedRenderSettings render, int cols, int rows, Mp4Settings settings)
    {
        var layout = RasterLayout.Create(render.FontSize, cols, rows, render.Window, render.Padding, render.Margin, render.Background is not null);
        return settings.FrameWidth > 0 ? layout.FitFrame(settings.FrameWidth, settings.FrameHeight) : layout;
    }

    /// <summary>
    /// The size an mp4 of the recording would have, without rendering: the scenario path reports it for a cast that
    /// is not converted yet. Throws as <see cref="OutputSize"/> does for a frame too large to encode.
    /// </summary>
    internal static (int Width, int Height) Measure(IReadOnlyList<CastEvent> events, int width, int height, ResolvedRenderSettings render, Mp4Settings settings)
    {
        var (cols, rows) = TerminalReplay.ResolveCanvasSize(width, height, events);
        return OutputSize(CreateLayout(render, cols, rows, settings), settings);
    }

    /// <summary>The encoded size: <c>--size</c>, or the frame fitted within <c>--max-size</c>.</summary>
    internal static (int Width, int Height) OutputSize(RasterLayout layout, Mp4Settings settings)
    {
        // --size is even and within x264's limit. A drawn frame larger than it is scaled down to it.
        if (settings.FrameWidth > 0)
            return (settings.FrameWidth, settings.FrameHeight);

        // x264 rejects a side over 16384. A wide terminal reaches that without --max-size, for
        // example 512 columns from font size 51.
        var (width, height) = FitWithin(layout.Width, layout.Height, settings.MaxWidth, settings.MaxHeight);
        if (width > Mp4Settings.MaxSize || height > Mp4Settings.MaxSize)
        {
            throw new FrameTooLargeException(
                $"an mp4 video of {width}x{height} is too large for x264, which accepts at most {Mp4Settings.MaxSize} on each side: pass --max-size, or use a smaller --font-size or terminal size");
        }

        return (width, height);
    }

    /// <summary>
    /// Largest even size within the limits that keeps the aspect ratio, and never larger than the
    /// frame. A limit of 0 means none. Integer arithmetic, because a floating-point scale can land
    /// just below the limit and lose 2 pixels to the even rounding.
    /// </summary>
    internal static (int Width, int Height) FitWithin(int width, int height, int maxWidth, int maxHeight)
    {
        var tooWide = maxWidth > 0 && width > maxWidth;
        var tooHigh = maxHeight > 0 && height > maxHeight;
        if (!tooWide && !tooHigh)
            return (width, height);

        // The side with the smaller ratio limit / size sets the scale: maxWidth / width <= maxHeight / height.
        var widthLimited = tooWide && (maxHeight <= 0 || (long)maxWidth * height <= (long)maxHeight * width);
        long fitWidth, fitHeight;
        if (widthLimited)
        {
            fitWidth = maxWidth;
            fitHeight = (long)height * maxWidth / width;
        }
        else
        {
            fitHeight = maxHeight;
            fitWidth = (long)width * maxHeight / height;
        }

        fitWidth = Math.Max(2, fitWidth);
        fitHeight = Math.Max(2, fitHeight);
        return ((int)(fitWidth - (fitWidth & 1)), (int)(fitHeight - (fitHeight & 1)));
    }

    /// <summary>
    /// Deletes the partial output, retrying for a moment. On Windows, Ctrl+C also reaches the real
    /// ffmpeg (behind a launcher such as a Scoop shim, which is the process we wait for). That
    /// ffmpeg may still be finalizing and holding the file open after the launcher has exited.
    /// </summary>
    internal static void TryDelete(string path)
    {
        var deadline = Environment.TickCount64 + 3_000;
        while (true)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                if (Environment.TickCount64 >= deadline)
                    return;
                Thread.Sleep(50);
            }
        }
    }
}

/// <summary>Finds ffmpeg and checks that it can encode H.264 with libx264.</summary>
internal static class Ffmpeg
{
    /// <summary>
    /// Decodes an image to RGB24 pixels of exactly <paramref name="width"/> × <paramref name="height"/>: scaled to
    /// cover that size and cropped to it, centered, as the SVG's <c>preserveAspectRatio="xMidYMid slice"</c> does.
    /// </summary>
    internal static byte[] DecodeImage(string ffmpegPath, string imagePath, int width, int height)
    {
        var psi = new System.Diagnostics.ProcessStartInfo(ffmpegPath)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var argument in new[]
        {
            "-hide_banner", "-loglevel", "error", "-i", imagePath,
            "-vf", string.Create(CultureInfo.InvariantCulture, $"scale={width}:{height}:force_original_aspect_ratio=increase,crop={width}:{height}"),
            "-frames:v", "1", "-f", "rawvideo", "-pix_fmt", "rgb24", "pipe:1",
        })
            psi.ArgumentList.Add(argument);

        using var process = System.Diagnostics.Process.Start(psi) ?? throw new Mp4Exception($"cannot start ffmpeg to read the background image: {imagePath}");
        var stderr = process.StandardError.ReadToEndAsync();
        using var pixels = new MemoryStream();
        process.StandardOutput.BaseStream.CopyTo(pixels);
        process.WaitForExit();
        if (process.ExitCode != 0 || pixels.Length != (long)width * height * 3)
            throw new Mp4Exception($"ffmpeg could not read the background image {imagePath}: {stderr.Result.Trim()}");
        return pixels.ToArray();
    }

    internal const string PathVariable = "SCENETAKE_FFMPEG";

    /// <summary>Resolves ffmpeg from <c>SCENETAKE_FFMPEG</c>, else <c>PATH</c>, and checks for libx264. The error ends with install hints.</summary>
    internal static bool TryResolve(out string path, out string error)
    {
        if (!TryLocate(out path, out error))
        {
            error += Environment.NewLine + InstallHint();
            return false;
        }

        if (!HasLibx264(path, out error))
        {
            error += Environment.NewLine + InstallHint();
            return false;
        }

        return true;
    }

    internal static bool TryLocate(out string path, out string error)
    {
        error = "";
        var configured = Environment.GetEnvironmentVariable(PathVariable);
        if (!string.IsNullOrEmpty(configured))
        {
            path = Path.GetFullPath(configured);
            if (File.Exists(path))
                return true;

            error = $"{PathVariable} points to a file that does not exist: {configured}";
            return false;
        }

        var name = OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg";
        foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(directory.Trim().Trim('"'), name);
            if (File.Exists(candidate))
            {
                path = candidate;
                return true;
            }
        }

        path = "";
        error = "mp4 output needs ffmpeg, which was not found on PATH";
        return false;
    }

    internal static bool HasLibx264(string path, out string error, int timeoutMilliseconds = 15_000)
    {
        error = "";
        try
        {
            var startInfo = new ProcessStartInfo(path)
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            startInfo.ArgumentList.Add("-hide_banner");
            startInfo.ArgumentList.Add("-encoders");
            using var process = Process.Start(startInfo)!;
            process.StandardInput.Close(); // a wrapper that waits for input sees end of input instead of the terminal

            // Read asynchronously: a synchronous read would block until ffmpeg closes stdout, before the timeout starts.
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(timeoutMilliseconds) || !stdout.Wait(timeoutMilliseconds))
            {
                process.Kill(entireProcessTree: true);
                error = string.Create(CultureInfo.InvariantCulture, $"ffmpeg did not respond within {timeoutMilliseconds / 1000.0:0.#}s: {path}");
                return false;
            }

            _ = stderr.Wait(timeoutMilliseconds);
            if (stdout.Result.Contains(" libx264 ", StringComparison.Ordinal))
                return true;

            error = $"ffmpeg has no libx264 encoder: {path}";
            return false;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException or InvalidOperationException)
        {
            error = $"cannot run ffmpeg at {path}: {ex.Message}";
            return false;
        }
    }

    /// <summary>
    /// Resolves the ffmpeg that decodes a background image for a PNG. It encodes nothing, so libx264 is not needed.
    /// </summary>
    internal static bool TryResolveImageDecoder(out string path, out string error)
    {
        if (TryLocate(out path, out error))
            return true;

        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(PathVariable)))
            error = $"a PNG with a background image needs ffmpeg to decode the image, which was not found on PATH. Install ffmpeg, or set {PathVariable} to its path. A background color or gradient needs no ffmpeg.";
        return false;
    }

    /// <summary>
    /// Resolves ffmpeg for GIF output. palettegen, paletteuse, and the GIF encoder are in every ffmpeg build, so
    /// libx264 is not checked.
    /// </summary>
    internal static bool TryResolveForGif(out string path, out string error)
    {
        if (TryLocate(out path, out error))
            return true;

        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(PathVariable)))
            error = "GIF output needs ffmpeg, which was not found on PATH";
        error += Environment.NewLine + InstallHint(libx264: false);
        return false;
    }

    /// <param name="libx264">Whether the output needs libx264, as mp4 does. A GIF needs any ffmpeg.</param>
    internal static string InstallHint(bool libx264 = true)
    {
        var hint = new StringBuilder(libx264 ? "Install ffmpeg with libx264, or set " : "Install ffmpeg, or set ");
        hint.Append(PathVariable).Append(" to its path:");
        if (OperatingSystem.IsWindows())
            hint.Append(Environment.NewLine).Append("  winget install Gyan.FFmpeg   (or: scoop install ffmpeg)");
        else if (OperatingSystem.IsMacOS())
            hint.Append(Environment.NewLine).Append("  brew install ffmpeg");
        else if (libx264)
            hint.Append(Environment.NewLine).Append("  sudo apt install ffmpeg   (Fedora: ffmpeg from RPM Fusion, as ffmpeg-free has no libx264)");
        else
            hint.Append(Environment.NewLine).Append("  sudo apt install ffmpeg   (Fedora: sudo dnf install ffmpeg-free)");
        return hint.ToString();
    }
}

/// <summary>An ffmpeg process reading raw frames on stdin. Keeps the tail of its stderr for error messages.</summary>
internal sealed class FfmpegProcess : IDisposable
{
    private const int StderrLines = 20;
    private readonly Process _process;
    private readonly Stream _stdin;
    private readonly Queue<string> _stderr = new();

    private FfmpegProcess(Process process)
    {
        _process = process;
        _stdin = process.StandardInput.BaseStream;
    }

    internal static FfmpegProcess Start(string path, IReadOnlyList<string> arguments)
    {
        var startInfo = new ProcessStartInfo(path)
        {
            RedirectStandardInput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        Process process;
        try
        {
            process = Process.Start(startInfo) ?? throw new Mp4Exception($"cannot start ffmpeg: {path}");
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            throw new Mp4Exception($"cannot start ffmpeg at {path}: {ex.Message}");
        }

        var ffmpeg = new FfmpegProcess(process);
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is null)
                return;
            lock (ffmpeg._stderr)
            {
                ffmpeg._stderr.Enqueue(e.Data);
                if (ffmpeg._stderr.Count > StderrLines)
                    ffmpeg._stderr.Dequeue();
            }
        };
        process.BeginErrorReadLine();
        return ffmpeg;
    }

    internal void Write(byte[] frame)
    {
        try
        {
            _stdin.Write(frame);
        }
        catch (IOException)
        {
            throw Failure("ffmpeg stopped reading frames");
        }
    }

    internal void Finish()
    {
        try
        {
            _stdin.Close();
        }
        catch (IOException)
        {
            throw Failure("ffmpeg stopped reading frames");
        }

        _process.WaitForExit();
        if (_process.ExitCode != 0)
            throw Failure($"ffmpeg exited with code {_process.ExitCode}");
    }

    /// <summary>
    /// Stops ffmpeg (and any launcher such as a Scoop shim) and waits briefly for it to exit, so
    /// it no longer holds the output file open: on Windows an open file cannot be deleted.
    /// </summary>
    internal void Kill()
    {
        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
                _process.WaitForExit(5_000);
            }
        }
        catch (InvalidOperationException)
        {
        }
        catch (System.ComponentModel.Win32Exception)
        {
        }
    }

    public void Dispose()
    {
        Kill();
        _process.Dispose();
    }

    private Mp4Exception Failure(string what)
    {
        // The timed wait does not wait for the async stderr reader, but the untimed one does.
        if (_process.WaitForExit(5_000))
        {
            _process.WaitForExit();
            if (!what.Contains("exited", StringComparison.Ordinal))
                what += $" (ffmpeg exited with code {_process.ExitCode})";
        }

        string tail;
        lock (_stderr)
            tail = string.Join(Environment.NewLine, _stderr);
        return new Mp4Exception(tail.Length == 0 ? what : $"{what}:{Environment.NewLine}{tail}");
    }
}
