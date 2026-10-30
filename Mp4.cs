using System.Diagnostics;
using System.Globalization;
using System.Text;

/// <summary>mp4 options. Defaults and the reasons for them are in .github/docs/spec_mp4.md.</summary>
internal readonly record struct Mp4Settings(int Fps, int Crf, int MaxWidth, int MaxHeight, string? FontFile)
{
    internal const int DefaultFps = 30, MinFps = 1, MaxFps = 60;
    // CRF 0 is lossless, and x264 then writes the High 4:4:4 Predictive profile, which X and many players reject.
    internal const int DefaultCrf = 23, MinCrf = 1, MaxCrf = 51;
    internal const int MinSize = 16, MaxSize = 16384;
    // x264 settings chosen for terminal frames. The measurements are in spec_mp4.md → Lessons Learned.
    internal const string Preset = "slower";
    internal const int PresetRefs = 8;          // x264 "slower"
    internal const int KeyframeSeconds = 30;    // longest gap between keyframes, for seeking
    internal const int Level52DpbMacroblocks = 184_320; // H.264 level 5.2 decoded picture buffer

    internal static Mp4Settings Default => new(DefaultFps, DefaultCrf, 0, 0, null);

    internal static bool TryParseFps(string text, out int fps, out string error) =>
        TryParseInt(text, "--fps", MinFps, MaxFps, out fps, out error);

    internal static bool TryParseCrf(string text, out int crf, out string error) =>
        TryParseInt(text, "--crf", MinCrf, MaxCrf, out crf, out error);

    /// <summary>Parses <c>WxH</c>, for example <c>1280x1024</c>.</summary>
    internal static bool TryParseMaxSize(string text, out int width, out int height, out string error)
    {
        width = height = 0;
        var parts = text.Split('x', 'X');
        if (parts.Length != 2
            || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out width)
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out height)
            || width is < MinSize or > MaxSize || height is < MinSize or > MaxSize)
        {
            error = $"--max-size must be WIDTHxHEIGHT with each side between {MinSize} and {MaxSize} (for example 1280x1024): {text}";
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

    internal Mp4FrameRenderer(
        IReadOnlyList<CastEvent> events,
        int width,
        int height,
        ResolvedRenderSettings render,
        Mp4Settings settings,
        double loopDuration)
    {
        _events = events;
        _initialWidth = width;
        _initialHeight = height;
        _theme = TerminalTheme.FromResolved(render.Theme);
        _fps = settings.Fps;
        var (cols, rows) = TerminalReplay.ResolveCanvasSize(width, height, events);
        Layout = RasterLayout.Create(render.FontSize, cols, rows, render.Window);
        Glyphs = GlyphSource.Create(render.FontFamily, settings.FontFile, Layout);
        FrameCount = Math.Max(1, (int)Math.Ceiling(loopDuration * settings.Fps - 1e-9));
    }

    internal RasterLayout Layout { get; }

    internal GlyphSource Glyphs { get; }

    internal int FrameCount { get; }

    /// <summary>Calls <paramref name="sink"/> with each RGB24 frame in order. The buffer is reused between calls.</summary>
    internal void Render(Action<byte[]> sink)
    {
        var raster = new FrameRasterizer(Layout, _theme, Glyphs);
        var cutoff = TerminalReplay.FindTrailingBlankCutoff(_events, _initialWidth, _initialHeight, Layout.Cols, Layout.Rows, _theme);
        var stepper = new ReplayStepper(_events, _initialWidth, _initialHeight, Layout.Cols, Layout.Rows, _theme, cutoff);
        for (var frame = 0; frame < FrameCount; frame++)
        {
            if (stepper.AdvanceTo(frame / (double)_fps))
                raster.Draw(stepper.View);
            sink(raster.Pixels);
        }
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

        var loopDuration = loopDurationOverride ?? TerminalReplay.ComputeLoopDuration(events);
        var renderer = new Mp4FrameRenderer(events, width, height, render, settings, loopDuration);
        var layout = renderer.Layout;
        var (outputWidth, outputHeight) = OutputSize(layout, settings);

        // An output from an earlier run is deleted before encoding. Ctrl+C ends the process from the
        // handler below, so deleting it only after a failure would leave it behind in that case.
        if (File.Exists(fullOutputPath))
            File.Delete(fullOutputPath);

        var tempPath = Path.Combine(directory, $".{Path.GetFileName(outputPath)}.{Guid.NewGuid():N}.partial");
        var arguments = BuildArguments(layout.Width, layout.Height, settings, outputWidth, outputHeight, tempPath);

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
    /// <item><c>-g</c>: each keyframe redraws the whole screen, and a mostly static terminal needs few.
    /// One at least every <see cref="Mp4Settings.KeyframeSeconds"/> keeps seeking cheap.</item>
    /// <item><c>-refs</c>: capped for large frames so the stream stays within level 5.2, which hardware
    /// decoders support widely. 8 references at 4K would need level 6.0.</item>
    /// </list>
    /// </summary>
    internal static List<string> BuildArguments(int width, int height, Mp4Settings settings, int outputWidth, int outputHeight, string outputPath)
    {
        var scale = outputWidth != width || outputHeight != height
            ? $"scale={outputWidth}:{outputHeight}:flags=lanczos:out_color_matrix=bt709:out_range=tv"
            : "scale=out_color_matrix=bt709:out_range=tv";
        var macroblocks = ((outputWidth + 15) / 16) * ((outputHeight + 15) / 16);
        var refs = Math.Clamp(Mp4Settings.Level52DpbMacroblocks / macroblocks, 1, Mp4Settings.PresetRefs);
        return
        [
            "-hide_banner", "-loglevel", "error",
            "-f", "rawvideo", "-pix_fmt", "rgb24", "-s", $"{width}x{height}",
            "-r", settings.Fps.ToString(CultureInfo.InvariantCulture), "-i", "-",
            "-vf", scale + ",format=yuv420p",
            "-c:v", "libx264", "-preset", Mp4Settings.Preset, "-crf", settings.Crf.ToString(CultureInfo.InvariantCulture),
            "-psy", "0", "-refs", refs.ToString(CultureInfo.InvariantCulture),
            "-g", (settings.Fps * Mp4Settings.KeyframeSeconds).ToString(CultureInfo.InvariantCulture),
            "-colorspace", "bt709", "-color_primaries", "bt709", "-color_trc", "bt709", "-color_range", "tv",
            "-movflags", "+faststart", "-f", "mp4", "-y", outputPath,
        ];
    }

    /// <summary>The encoded size: the frame fitted within <c>--max-size</c>.</summary>
    internal static (int Width, int Height) OutputSize(RasterLayout layout, Mp4Settings settings)
    {
        // x264 rejects a side over 16384. A wide terminal reaches that without --max-size, for
        // example 512 columns from font size 51.
        var (width, height) = FitWithin(layout.Width, layout.Height, settings.MaxWidth, settings.MaxHeight);
        if (width > Mp4Settings.MaxSize || height > Mp4Settings.MaxSize)
        {
            throw new FrameTooLargeException(
                $"an mp4 video of {width}x{height} is too large for x264, which accepts at most {Mp4Settings.MaxSize} on each side; pass --max-size, or use a smaller --font-size or terminal size");
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
    private static void TryDelete(string path)
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

    internal static string InstallHint()
    {
        var hint = new StringBuilder("Install ffmpeg with libx264, or set ");
        hint.Append(PathVariable).Append(" to its path:");
        if (OperatingSystem.IsWindows())
            hint.Append(Environment.NewLine).Append("  winget install Gyan.FFmpeg   (or: scoop install ffmpeg)");
        else if (OperatingSystem.IsMacOS())
            hint.Append(Environment.NewLine).Append("  brew install ffmpeg");
        else
            hint.Append(Environment.NewLine).Append("  sudo apt install ffmpeg   (Fedora: ffmpeg from RPM Fusion; ffmpeg-free has no libx264)");
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
