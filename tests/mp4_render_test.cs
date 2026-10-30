#:sdk Microsoft.NET.Sdk
#:property TargetFramework=net10.0
#:property Nullable=enable
#:property ImplicitUsings=enable
#:property AllowUnsafeBlocks=true
#:package StbTrueTypeSharp@1.26.13
#:include ../Terminal.cs
#:include ../CastTimingPipeline.cs
#:include ../CastReader.cs
#:include SvgTestStubs.cs
#:include ../Svg.cs
#:include ../Raster.cs
#:include ../Mp4.cs

// mp4 frame rendering without ffmpeg: layout, colors, procedural glyphs, timing, and options.
// Tests that draw frames need one installed font. They print "skip" when none is found.
//
// Usage (from repository root):
//   dotnet run tests/mp4_render_test.cs

var failures = 0;
failures += Run("LayoutRoundsSvgMetrics", LayoutRoundsSvgMetrics);
failures += Run("FitWithinKeepsAspectAndEvenSize", FitWithinKeepsAspectAndEvenSize);
failures += Run("OptionParsing", OptionParsing);
failures += Run("FfmpegArgumentsUseH264Bt709", FfmpegArgumentsUseH264Bt709);
failures += Run("FontFamilyListParsing", FontFamilyListParsing);
failures += Run("BlockElementsFillCellEdges", BlockElementsFillCellEdges);
failures += Run("BoxLinesReachCellEdges", BoxLinesReachCellEdges);
failures += Run("DoubleCornerJoinsInsideCell", DoubleCornerJoinsInsideCell);
failures += Run("BrailleDrawsDots", BrailleDrawsDots);
failures += Run("SyntheticBoldAndItalic", SyntheticBoldAndItalic);
failures += Run("FrameColorsMatchTheme", FrameColorsMatchTheme);
failures += Run("CursorBlendsHalfForeground", CursorBlendsHalfForeground);
failures += Run("NothingShownBeforeFirstEvent", NothingShownBeforeFirstEvent);
failures += Run("FrameShowsEventsUpToItsTime", FrameShowsEventsUpToItsTime);
failures += Run("TrailingBlankRestoreIsTrimmed", TrailingBlankRestoreIsTrimmed);
failures += Run("MatrixTintMatchesSvg", MatrixTintMatchesSvg);
failures += Run("MacosChromeDrawsTrafficLights", MacosChromeDrawsTrafficLights);
failures += Run("WindowsChromeDrawsButtons", WindowsChromeDrawsButtons);
failures += Run("BoxDrawingAtTinySizesStaysInCell", BoxDrawingAtTinySizesStaysInCell);
failures += Run("HugeFrameIsReportedClearly", HugeFrameIsReportedClearly);
failures += Run("FfmpegProbeTimesOut", FfmpegProbeTimesOut);
failures += Run("OutputPathIsCheckedFirst", OutputPathIsCheckedFirst);
failures += Run("FontTableOutsideFileIsRejected", FontTableOutsideFileIsRejected);
failures += Run("SymbolOnlyFontIsRejected", SymbolOnlyFontIsRejected);
failures += Run("UnreadableFontFileIsReported", UnreadableFontFileIsReported);
failures += Run("X264SideLimitIsChecked", X264SideLimitIsChecked);
failures += Run("FontFileThatCannotLoadIsAnError", FontFileThatCannotLoadIsAnError);
failures += Run("ShortFontTableIsRejected", ShortFontTableIsRejected);
failures += Run("DefaultFontsPassTableChecks", DefaultFontsPassTableChecks);
failures += Run("OutOfRangeGlyphIdsAreMissing", OutOfRangeGlyphIdsAreMissing);
failures += Run("EveryFontFileFaceMustLoad", EveryFontFileFaceMustLoad);
failures += Run("FontFileIsNotHeldOpen", FontFileIsNotHeldOpen);
failures += Run("FontFileIsReleasedAfterRender", FontFileIsReleasedAfterRender);
failures += Run("LinkedFontFileLoads", LinkedFontFileLoads);
failures += Run("InstalledFontsCanBeRescanned", InstalledFontsCanBeRescanned);
failures += Run("LogNamesAFontThatLoads", LogNamesAFontThatLoads);

return failures == 0 ? 0 : 1;

// A test that needs an installed font and finds none is skipped, not failed: some CI runners have no fonts.
static int Run(string name, Func<bool> test)
{
    try
    {
        if (test())
        {
            Console.Error.WriteLine($"ok {name}");
            return 0;
        }

        Console.Error.WriteLine($"FAIL {name}");
        return 1;
    }
    catch (Exception ex) when (ex is FontNotFoundException or SkipTestException)
    {
        Console.Error.WriteLine($"skip {name}: {ex.Message}");
        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"FAIL {name}: {ex}");
        return 1;
    }
}

static bool LayoutRoundsSvgMetrics()
{
    // 16px: cell 9.92 × 20 → 10 × 20, baseline 12.8 → 13, padding 10 × 4 (SVG inner padding).
    var layout = RasterLayout.Create(16, 200, 60);
    var odd = RasterLayout.Create(16, 3, 1); // 3*10+20 = 50 wide, 20+8 = 28 high
    var tiny = RasterLayout.Create(13, 1, 1); // 8.06 → 8 wide, 16.25 → 16 high; 8+16=24, 16+8=24 (pad clamps)
    var chrome = RasterLayout.Create(16, 10, 3, WindowStyle.Macos); // 34px title bar + 3*20 + 2*4 = 102
    return layout is { CellWidth: 10, CellHeight: 20, Baseline: 13, PadX: 10, PadY: 4, Width: 2020, Height: 1208, TitleBarHeight: 0 }
        && odd.Width % 2 == 0 && odd.Height % 2 == 0
        && tiny is { CellWidth: 8, CellHeight: 16 } && tiny.Width % 2 == 0
        && chrome is { TitleBarHeight: 34, Height: 102 } && chrome.RowY(0) == 38 && layout.RowY(0) == 4;
}

static bool FitWithinKeepsAspectAndEvenSize() =>
    Mp4Render.FitWithin(2020, 1208, 1280, 1024) == (1280, 764)
    && Mp4Render.FitWithin(820, 488, 1280, 1024) == (820, 488)   // never upscales
    && Mp4Render.FitWithin(1000, 3000, 1280, 1024) == (340, 1024) // height-limited
    && Mp4Render.FitWithin(820, 488, 0, 0) == (820, 488)
    && Mp4Render.FitWithin(820, 1288, 1280, 1024) == (650, 1024); // 1288 × 1024/1288 in floating point is 1023.99…

static bool OptionParsing() =>
    Mp4Settings.TryParseFps("60", out var fps, out _) && fps == 60
    && !Mp4Settings.TryParseFps("0", out _, out _)
    && !Mp4Settings.TryParseFps("61", out _, out _)
    && !Mp4Settings.TryParseFps("30.5", out _, out _)
    && !Mp4Settings.TryParseCrf("0", out _, out var crfError) && crfError == "--crf must be between 1 and 51: 0" // lossless 0 is High 4:4:4
    && Mp4Settings.TryParseCrf("1", out _, out _) && !Mp4Settings.TryParseCrf("52", out _, out _)
    && Mp4Settings.TryParseMaxSize("1280x1024", out var w, out var h, out _) && (w, h) == (1280, 1024)
    && !Mp4Settings.TryParseMaxSize("1280", out _, out _, out _)
    && !Mp4Settings.TryParseMaxSize("8x8", out _, out _, out _)
    && !Mp4Settings.TryParseMaxSize("-1x100", out _, out _, out _);

static bool FfmpegArgumentsUseH264Bt709()
{
    var plain = string.Join(' ', Mp4Render.BuildArguments(820, 488, Mp4Settings.Default, 820, 488, "out.mp4"));
    var scaled = string.Join(' ', Mp4Render.BuildArguments(2020, 1208, Mp4Settings.Default with { Crf = 28, Fps = 60 }, 1280, 764, "out.mp4"));
    // 3860×2168 is 242×136 = 32,912 macroblocks: level 5.2's 184,320-macroblock buffer fits 5 references.
    var fourK = string.Join(' ', Mp4Render.BuildArguments(3860, 2168, Mp4Settings.Default, 3860, 2168, "out.mp4"));
    return plain.Contains("-f rawvideo -pix_fmt rgb24 -s 820x488 -r 30 -i -", StringComparison.Ordinal)
        && plain.Contains("-vf scale=out_color_matrix=bt709:out_range=tv,format=yuv420p", StringComparison.Ordinal)
        && plain.Contains("-c:v libx264 -preset slower -crf 23 -psy 0 -refs 8 -x264-params merange=128 -g 900", StringComparison.Ordinal)
        && plain.Contains("-colorspace bt709 -color_primaries bt709 -color_trc bt709", StringComparison.Ordinal)
        && plain.Contains("-movflags +faststart -f mp4", StringComparison.Ordinal)
        && scaled.Contains("scale=1280:764:flags=lanczos:out_color_matrix=bt709", StringComparison.Ordinal)
        && scaled.Contains("-crf 28 -psy 0 -refs 8 -x264-params merange=128 -g 1800", StringComparison.Ordinal)
        && fourK.Contains("-refs 5 ", StringComparison.Ordinal);
}

static bool FontFamilyListParsing() =>
    GlyphSource.ParseFamilyList("ui-monospace, \"Cascadia Mono\", 'Ubuntu Mono' ,monospace,")
        .SequenceEqual(["ui-monospace", "Cascadia Mono", "Ubuntu Mono", "monospace"]);

static bool BlockElementsFillCellEdges()
{
    var layout = RasterLayout.Create(16, 1, 1);
    if (!CellGlyphs.TryDraw(0x2588, layout, 1, out var full) || full.Alpha.Any(a => a != 255))
        return false;
    if (full.Width != layout.CellWidth || full.Height != layout.CellHeight || full.OffsetY != -layout.Baseline)
        return false;

    // ▚ = upper-left and lower-right quadrants.
    CellGlyphs.TryDraw(0x259A, layout, 1, out var diagonal);
    int At(RasterGlyph g, int x, int y) => g.Alpha[y * g.Width + x];
    var w = layout.CellWidth;
    var h = layout.CellHeight;
    return At(diagonal, 0, 0) == 255 && At(diagonal, w - 1, h - 1) == 255
        && At(diagonal, w - 1, 0) == 0 && At(diagonal, 0, h - 1) == 0
        && !CellGlyphs.TryDraw('A', layout, 1, out _)
        && !CellGlyphs.TryDraw(0x2571, layout, 1, out _); // diagonals come from the font
}

static bool BoxLinesReachCellEdges()
{
    var layout = RasterLayout.Create(16, 1, 1);
    CellGlyphs.TryDraw(0x2500, layout, 1, out var horizontal); // ─
    CellGlyphs.TryDraw(0x2502, layout, 1, out var vertical);   // │
    CellGlyphs.TryDraw(0x256D, layout, 1, out var arc);        // ╭
    var w = layout.CellWidth;
    var h = layout.CellHeight;
    bool AnyInColumn(RasterGlyph g, int x) => Enumerable.Range(0, h).Any(y => g.Alpha[y * w + x] != 0);
    bool AnyInRow(RasterGlyph g, int y) => Enumerable.Range(0, w).Any(x => g.Alpha[y * w + x] != 0);
    return AnyInColumn(horizontal, 0) && AnyInColumn(horizontal, w - 1) && !AnyInRow(horizontal, 0)
        && AnyInRow(vertical, 0) && AnyInRow(vertical, h - 1) && !AnyInColumn(vertical, 0)
        && AnyInRow(arc, h - 1) && AnyInColumn(arc, w - 1) && !AnyInRow(arc, 0) && !AnyInColumn(arc, 0);
}

static bool DoubleCornerJoinsInsideCell()
{
    // ╔: both lines reach the right and bottom edges, and nothing is drawn above or left of the outer corner.
    var layout = RasterLayout.Create(32, 1, 1);
    CellGlyphs.TryDraw(0x2554, layout, 1, out var corner);
    var w = layout.CellWidth;
    var h = layout.CellHeight;
    int RunsInColumn(int x)
    {
        var runs = 0;
        for (var y = 0; y < h; y++)
        {
            if (corner.Alpha[y * w + x] != 0 && (y == 0 || corner.Alpha[(y - 1) * w + x] == 0))
                runs++;
        }

        return runs;
    }

    return RunsInColumn(w - 1) == 2 // two horizontal lines reach the right edge
        && Enumerable.Range(0, w).Count(x => corner.Alpha[(h - 1) * w + x] != 0 && (x == 0 || corner.Alpha[(h - 1) * w + x - 1] == 0)) == 2
        && corner.Alpha[0] == 0 && corner.Alpha[(h / 2 - 4) * w] == 0;
}

static bool BrailleDrawsDots()
{
    var layout = RasterLayout.Create(16, 1, 1);
    CellGlyphs.TryDraw(0x28FF, layout, 1, out var all);   // ⣿ all eight dots
    CellGlyphs.TryDraw(0x2801, layout, 1, out var first); // ⠁ dot 1 only (upper left)
    var w = layout.CellWidth;
    var h = layout.CellHeight;
    bool Filled(RasterGlyph g, double fx, double fy) => g.Alpha[(int)(fy * h) * w + (int)(fx * w)] != 0;
    return Filled(all, 0.25, 0.0625) && Filled(all, 0.75, 0.9375)
        && Filled(first, 0.25, 0.0625) && !Filled(first, 0.75, 0.0625) && !Filled(first, 0.25, 0.9375);
}

static bool SyntheticBoldAndItalic()
{
    var bar = new RasterGlyph(1, 4, 0, -4, [255, 255, 255, 255]); // a 1px vertical bar above the baseline
    var bold = GlyphSource.Embolden(bar, 1);
    var italic = GlyphSource.Slant(bar);
    int Column(RasterGlyph g, int row)
    {
        var best = 0;
        for (var x = 1; x < g.Width; x++)
        {
            if (g.Alpha[row * g.Width + x] > g.Alpha[row * g.Width + best])
                best = x;
        }

        return g.OffsetX + best;
    }

    return bold.Width == 2 && bold.Alpha.All(a => a == 255)
        && Column(italic, 0) > Column(italic, 3); // the top leans right
}

static bool FrameColorsMatchTheme()
{
    // Red background space at col 0, green █ at col 2. Dark theme: bg #282c34, palette 1 #ac4142, palette 2 #7e8e50.
    var (frames, layout) = Render(
        """[0.0,"o","\u001b[41m \u001b[0m \u001b[32m█\u001b[0m\u001b[?25l"]""", fps: 10);
    var last = frames[^1];
    return Pixel(last, layout, 1, 1) == (40, 44, 52)
        && CellPixel(last, layout, 0, 0) == (0xac, 0x41, 0x42)
        && CellPixel(last, layout, 0, 1) == (40, 44, 52)
        && CellPixel(last, layout, 0, 2) == (0x7e, 0x8e, 0x50);
}

static bool CursorBlendsHalfForeground()
{
    // Cursor visible at (0, 0) on a blank cell: theme fg #d0d0d0 at alpha 128 over bg #282c34.
    var (frames, layout) = Render("""[0.0,"o",""]""", fps: 10);
    static byte Blend(int bg, int fg) => (byte)(bg + (fg - bg) * 128 / 255);
    return CellPixel(frames[^1], layout, 0, 0) == (Blend(40, 208), Blend(44, 208), Blend(52, 208));
}

static bool NothingShownBeforeFirstEvent()
{
    // Like the SVG: no cursor until the first event at 1.0s.
    var (frames, layout) = Render("""[1.0,"o","█"]""", fps: 10);
    return CellPixel(frames[0], layout, 0, 0) == (40, 44, 52)
        && CellPixel(frames[9], layout, 0, 0) == (40, 44, 52)
        && CellPixel(frames[10], layout, 0, 0) == (0xd0, 0xd0, 0xd0);
}

static bool FrameShowsEventsUpToItsTime()
{
    // Frame k shows events with time ≤ k / fps. The loop holds the last frame for one more idle gap (0.5s).
    var (frames, layout) = Render(
        """
        [0.0,"o","\u001b[?25l█"]
        [0.5,"o","█"]
        """, fps: 10);
    return frames.Count == 10
        && CellPixel(frames[4], layout, 0, 1) == (40, 44, 52)
        && CellPixel(frames[5], layout, 0, 1) == (0xd0, 0xd0, 0xd0);
}

static bool TrailingBlankRestoreIsTrimmed()
{
    // A full-screen app exits to a blank main screen. Like the SVG, the video ends on the app's last screen.
    var (frames, layout) = Render(
        """
        [0.0,"o","\u001b[?1049h\u001b[2J\u001b[H█\u001b[?25l"]
        [1.0,"o","\u001b[?1049l"]
        """, fps: 10);
    return CellPixel(frames[^1], layout, 0, 0) == (0xd0, 0xd0, 0xd0);
}

static bool MatrixTintMatchesSvg()
{
    // White next to green draws as bright green (palette 10 #7e8e50), as in the SVG.
    var (frames, layout) = Render("""[0.0,"o","\u001b[32m█\u001b[37m█\u001b[0m\u001b[?25l"]""", fps: 10);
    return CellPixel(frames[^1], layout, 0, 1) == (0x7e, 0x8e, 0x50);
}

static bool MacosChromeDrawsTrafficLights()
{
    // Dark theme → MacDark: title bar #323232, separator #1a1a1a. Buttons centered at y=17, x=22/48/74 (SVG geometry).
    var (frames, layout) = Render("""[0.0,"o","█\u001b[?25l"]""", fps: 10, window: "macos");
    var last = frames[^1];
    return layout.TitleBarHeight == 34
        && Pixel(last, layout, 22, 17) == (0xff, 0x5f, 0x57)
        && Pixel(last, layout, 48, 17) == (0xfe, 0xbc, 0x2e)
        && Pixel(last, layout, 74, 17) == (0x28, 0xc8, 0x40)
        && Pixel(last, layout, layout.Width - 5, 5) == (0x32, 0x32, 0x32)
        && Pixel(last, layout, layout.Width - 5, 33) == (0x1a, 0x1a, 0x1a)
        && CellPixel(last, layout, 0, 0) == (0xd0, 0xd0, 0xd0); // terminal content starts below the title bar
}

static bool WindowsChromeDrawsButtons()
{
    // Dark theme → WinDark: title bar #2d2d2d, buttons #8a8a8a, right-aligned 14px from the edge.
    var (frames, layout) = Render("""[0.0,"o","\u001b[?25l"]""", fps: 10, window: "windows");
    var last = frames[^1];
    var lastButtonCenter = layout.Width - 14 - 9;
    return Pixel(last, layout, lastButtonCenter, 17) == (0x8a, 0x8a, 0x8a)
        && Pixel(last, layout, 5, 5) == (0x2d, 0x2d, 0x2d)
        && Pixel(last, layout, layout.Width - 5, 5) == (0x2d, 0x2d, 0x2d);
}

static (List<byte[]> Frames, RasterLayout Layout) Render(string events, int fps, string? window = null)
{
    var path = Path.Combine(Path.GetTempPath(), $"scenetake-mp4-{Guid.NewGuid():N}.cast");
    try
    {
        var tags = window is null ? "" : $",\"tags\":[\"st:window={window}\"]";
        File.WriteAllText(path, "{\"version\":3,\"term\":{\"cols\":8,\"rows\":3,\"type\":\"xterm-256color\"}" + tags + "}\n" + events + "\n");
        var recording = CastReader.Read(path);
        var renderer = new Mp4FrameRenderer(recording.Events, recording.Width, recording.Height, recording.RenderSettings,
            Mp4Settings.Default with { Fps = fps }, recording.LoopDuration);
        var frames = new List<byte[]>();
        renderer.Render(pixels => frames.Add((byte[])pixels.Clone()));
        return (frames, renderer.Layout);
    }
    finally
    {
        File.Delete(path);
    }
}

static (byte, byte, byte) Pixel(byte[] frame, RasterLayout layout, int x, int y)
{
    var offset = (y * layout.Width + x) * 3;
    return (frame[offset], frame[offset + 1], frame[offset + 2]);
}

static (byte, byte, byte) CellPixel(byte[] frame, RasterLayout layout, int row, int col) =>
    Pixel(frame, layout, layout.CellX(col) + layout.CellWidth / 2, layout.RowY(row) + layout.CellHeight / 2);

static bool BoxDrawingAtTinySizesStaysInCell()
{
    // At font sizes 1–3 a cell is 1×1, 1×3, or 2×4 pixels, narrower than a heavy (2px) or double (4px)
    // line. At size 1 any write past the 1×1 mask throws, so drawing each of these without an exception
    // shows the lines are clipped. At sizes 2 and 3 an overrun can wrap into the next mask row without
    // throwing, so those sizes only show that drawing succeeds.
    int[] glyphs = [0x2501, 0x2503, 0x2551, 0x2550, 0x256C, 0x250F, 0x254B];
    foreach (var fontSize in new[] { 1, 2, 3 })
    {
        var layout = RasterLayout.Create(fontSize, 1, 1);
        foreach (var codepoint in glyphs)
        {
            if (!CellGlyphs.TryDraw(codepoint, layout, 1, out _))
                return false;
        }
    }

    return true;
}

static bool HugeFrameIsReportedClearly()
{
    // ffmpeg takes a raw frame only while (width + 128) × (height + 128) is below 2^28. For 512×512
    // cells that holds at font size 36 (11296x23056) and fails at 37 (11808x23568).
    try
    {
        RasterLayout.Create(37, 512, 512);
        return false;
    }
    catch (FrameTooLargeException ex)
    {
        var fits = RasterLayout.Create(36, 512, 512);
        return ex.Message.Contains("11808x23568", StringComparison.Ordinal) && (fits.Width, fits.Height) == (11296, 23056);
    }
}

static bool X264SideLimitIsChecked()
{
    // x264 rejects a side over 16384. 512 columns are 15904 pixels wide at font size 50 and 16416 at
    // 51. --max-size scales the video below the limit, so it makes the same frame encodable.
    var wide = RasterLayout.Create(51, 512, 24);
    try
    {
        Mp4Render.OutputSize(wide, Mp4Settings.Default);
        return false;
    }
    catch (FrameTooLargeException ex)
    {
        return ex.Message.Contains("16416x", StringComparison.Ordinal)
            && Mp4Render.OutputSize(RasterLayout.Create(50, 512, 24), Mp4Settings.Default).Width == 15904
            && Mp4Render.OutputSize(wide, Mp4Settings.Default with { MaxWidth = 1280, MaxHeight = 1024 }).Width == 1280;
    }
}

static bool FfmpegProbeTimesOut()
{
    // A "ffmpeg" that never answers must fail at the timeout, not hang until it exits. The fake runs
    // for 5s and the timeout is 1s.
    var directory = Path.Combine(Path.GetTempPath(), $"scenetake-slow-ffmpeg-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    try
    {
        string path;
        if (OperatingSystem.IsWindows())
        {
            path = Path.Combine(directory, "ffmpeg.cmd");
            File.WriteAllText(path, "@ping -n 6 127.0.0.1 >nul\r\n");
        }
        else
        {
            path = Path.Combine(directory, "ffmpeg");
            File.WriteAllText(path, "#!/bin/sh\nsleep 5\n");
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        var watch = System.Diagnostics.Stopwatch.StartNew();
        var found = Ffmpeg.HasLibx264(path, out var error, timeoutMilliseconds: 1_000);
        return !found && error.Contains("did not respond", StringComparison.Ordinal) && watch.Elapsed < TimeSpan.FromSeconds(4);
    }
    finally
    {
        Directory.Delete(directory, recursive: true);
    }
}

static bool OutputPathIsCheckedFirst()
{
    // A missing output folder or a directory in the output's place is reported by name before any
    // frame is drawn. The ffmpeg path does not exist, so reaching ffmpeg would fail differently.
    var recording = CastReader.Read("tests/marker-delay.cast");
    var directory = Path.Combine(Path.GetTempPath(), $"scenetake-mp4-out-{Guid.NewGuid():N}");
    Directory.CreateDirectory(Path.Combine(directory, "taken.mp4"));
    try
    {
        string Error(string output)
        {
            try
            {
                Mp4Render.WriteMp4(recording.Events, recording.Width, recording.Height, recording.RenderSettings, Mp4Settings.Default, output, "no-such-ffmpeg", recording.LoopDuration);
                return "";
            }
            catch (Mp4Exception ex)
            {
                return ex.Message;
            }
        }

        var missing = Path.Combine(directory, "missing", "x.mp4");
        var taken = Path.Combine(directory, "taken.mp4");
        return Error(missing) == $"output folder does not exist: {Path.GetDirectoryName(missing)}"
            && Error(taken) == $"output path is a directory: {taken}";
    }
    finally
    {
        Directory.Delete(directory, recursive: true);
    }
}

static bool FontTableOutsideFileIsRejected()
{
    // stb_truetype reads tables without bounds checks, so a table past the end of the file would crash
    // the process during rendering. The header scan must reject the file instead.
    var path = CopyInstalledTrueTypeFont(data => PatchTableOffset(data, "glyf", 0x7FF00000));
    try
    {
        var scanRejects = false;
        try
        {
            FontCatalog.ReadFaces(path);
        }
        catch (InvalidDataException)
        {
            scanRejects = true;
        }

        return scanRejects && FontFileError(path).Contains(path, StringComparison.Ordinal);
    }
    finally
    {
        File.Delete(path);
    }
}

static bool SymbolOnlyFontIsRejected()
{
    // A font whose cmap has no Unicode subtable (Symbol, Wingdings) cannot be loaded by stb_truetype.
    // It must be left out at scan time, so --font-file fails before recording and a family list skips it.
    var path = CopyInstalledTrueTypeFont(data => PatchCmapToSymbol(data));
    try
    {
        return FontCatalog.ReadFaces(path).Count == 0 && FontFileError(path).Contains("no usable outline font", StringComparison.Ordinal);
    }
    finally
    {
        File.Delete(path);
    }
}

static bool UnreadableFontFileIsReported()
{
    // A --font-file without read permission must give the font error, not an unhandled exception.
    if (OperatingSystem.IsWindows())
        throw new SkipTestException("needs Unix file modes");

    var path = CopyInstalledTrueTypeFont(_ => { });
    try
    {
        File.SetUnixFileMode(path, UnixFileMode.None);
        try
        {
            using (File.OpenRead(path))
                throw new SkipTestException("running as a user who can read any file");
        }
        catch (UnauthorizedAccessException)
        {
        }

        return FontFileError(path).Contains(path, StringComparison.Ordinal);
    }
    finally
    {
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        File.Delete(path);
    }
}

static bool FontFileThatCannotLoadIsAnError()
{
    // With loca renamed to "CFF ", the header scan sees outlines it can draw, but stb_truetype cannot
    // load the face. The scan alone would accept the file and the render would use another font.
    var path = CopyInstalledTrueTypeFont(data => PatchTableTag(data, "loca", "CFF "));
    try
    {
        return FontCatalog.ReadFaces(path).Count == 1 && FontFileError(path).Contains(path, StringComparison.Ordinal);
    }
    finally
    {
        File.Delete(path);
    }
}

static bool ShortFontTableIsRejected()
{
    // Every variant passes the header scan: each table lies inside the file, and head, hhea, and hmtx
    // exist. stb_truetype reads the fields and tables changed here without bounds checks, so drawing
    // from any of these fonts could crash the process. Each one must fail to load instead.
    Action<byte[]>[] variants =
    [
        data =>
        {
            PatchTableOffset(data, "loca", (uint)data.Length);
            PatchTableLength(data, "loca", 0);
        },
        data => PatchTableLength(data, "glyf", 0),  // the loca entries then point past the end of glyf
        data => PatchTableLength(data, "head", 51), // indexToLocFormat is at 50 and 51
        data => PatchTableLength(data, "hhea", 35), // numberOfHMetrics is at 34 and 35
        data => PatchTableLength(data, "maxp", 5),  // numGlyphs is at 4 and 5
        data => PatchTableLength(data, "hmtx", 0),
        data => WriteUInt16(data, TableOffset(data, "hhea") + 34, 0), // the face then has no long metrics
    ];
    foreach (var patch in variants)
    {
        var path = CopyInstalledTrueTypeFont(patch);
        try
        {
            if (FontCatalog.ReadFaces(path).Count != 1 || !FontFileError(path).Contains(path, StringComparison.Ordinal))
                return false;
        }
        finally
        {
            File.Delete(path);
        }
    }

    return true;
}

static bool OutOfRangeGlyphIdsAreMissing()
{
    // stb_truetype reads hmtx by the glyph id the cmap gives, without checking it against maxp's glyph
    // count. Both fonts load: one lowers the glyph count to 1, the other maps only 'A' to 0xE0000000
    // in a format 12 subtable, whose 32-bit id comes back negative. 'A' must read as missing in both.
    // Space reads as missing too: its id is 1 or more, outside the first font's one glyph, and the
    // second font's cmap, which maps only 'A', is the one in use.
    var fewGlyphs = CopyInstalledTrueTypeFont(data => WriteUInt16(data, TableOffset(data, "maxp") + 4, 1));
    var negativeId = RewriteInstalledTrueTypeFont(data => MapToGlyph(data, 'A', 0xE0000000));
    try
    {
        return new[] { fewGlyphs, negativeId }.All(path =>
            LoadedFace.TryLoad(FontCatalog.ReadFaces(path)[0]) is { } face && face.GlyphIndex('A') == 0 && face.GlyphIndex(' ') == 0);
    }
    finally
    {
        LoadedFace.ReleaseAll();
        File.Delete(fewGlyphs);
        File.Delete(negativeId);
    }
}

static bool EveryFontFileFaceMustLoad()
{
    // The collection holds one static face twice. The first is marked regular (OS/2 weight 400), and the
    // second bold (700) with an empty hmtx. The scan accepts both, and the family picks the second as
    // its bold face, or as its bold-italic face when the source is italic. Checking only the regular
    // face would let that text fall back to another font without a word. A variable source would give
    // the family no bold face to check. The test proves something only when the second face reads as
    // 700 and the first face loads, so it checks both.
    var path = Path.Combine(Path.GetTempPath(), $"scenetake-font-{Guid.NewGuid():N}.ttc");
    var source = File.ReadAllBytes(InstalledSingleFaceTrueType(nonVariable: true));
    WriteUInt16(source, TableOffset(source, "OS/2") + 4, 400);
    var bold = (byte[])source.Clone();
    WriteUInt16(bold, TableOffset(bold, "OS/2") + 4, 700);
    PatchTableLength(bold, "hmtx", 0);
    File.WriteAllBytes(path, Collection(source, bold));
    try
    {
        var faces = FontCatalog.ReadFaces(path);
        if (faces.Count != 2 || faces[1].Weight != 700 || !LoadedFace.CanLoad(faces[0]))
            throw new SkipTestException("the installed font cannot make a regular and bold collection");
        return FontFileError(path).Contains(path, StringComparison.Ordinal);
    }
    finally
    {
        File.Delete(path);
    }
}

static bool FontFileIsNotHeldOpen()
{
    // The scenario path checks --font-file before pre, and pre may replace the file. Windows cannot
    // replace a file that is memory-mapped, so the check must not keep it mapped. Unix allows either.
    // The unchanged copy must also be accepted, so the check does not pass by rejecting every font.
    var path = CopyInstalledTrueTypeFont(_ => { });
    var accepted = FontFileError(path) == "";
    File.WriteAllBytes(path, File.ReadAllBytes(path));
    File.Delete(path);
    return accepted;
}

static bool FontFileIsReleasedAfterRender()
{
    // post may replace or delete the --font-file. Windows cannot while the render keeps it mapped, so
    // WriteMp4 must release every face it loaded. The render must draw text in that font, which needs
    // only a process that reads the frames, so a stand-in ffmpeg does. Unix lets a mapped file be
    // replaced, so the test can fail only on Windows.
    var font = CopyInstalledTrueTypeFont(_ => { });
    var cast = Path.Combine(Path.GetTempPath(), $"scenetake-mp4-{Guid.NewGuid():N}.cast");
    var output = Path.ChangeExtension(cast, ".mp4");
    var ffmpeg = WriteFrameReadingFfmpeg(Path.GetTempPath());
    try
    {
        File.WriteAllText(cast, "{\"version\":3,\"term\":{\"cols\":8,\"rows\":2}}\n[0.1, \"o\", \"Ab\"]\n");
        var recording = CastReader.Read(cast);
        var result = Mp4Render.WriteMp4(recording.Events, recording.Width, recording.Height, recording.RenderSettings,
            Mp4Settings.Default with { FontFile = font }, output, ffmpeg, recording.LoopDuration);
        File.WriteAllBytes(font, File.ReadAllBytes(font));
        File.Delete(font);
        return result.Font.Path == font;
    }
    finally
    {
        // Cleanup must not throw: a font still held would hide the failure above.
        foreach (var path in new[] { cast, output, font, ffmpeg })
            TryDeleteFile(path);
    }
}

static bool LogNamesAFontThatLoads()
{
    // An installed family whose regular file fails the load checks draws no regular text, so the log
    // must not name it. A copy of an installed font with an empty hmtx gets a family name no installed
    // font has, so it is the only face of its family and that family's regular face. Reflection puts it
    // in the installed list without installing it. The copy's own family comes next in the chain, so
    // the log should name that family's regular face, which must load for the test to prove anything.
    var broken = CopyInstalledTrueTypeFont(data => PatchTableLength(data, "hmtx", 0));
    var installed = typeof(FontCatalog).GetField("s_installed", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
    var copied = FontCatalog.ReadFaces(broken)[0];
    var face = copied with { Family = $"scenetake-broken-{Guid.NewGuid():N}", TypographicFamily = null, PostScriptName = null };
    var original = FontFamilyFaces.FromFaces(copied.Family, FontCatalog.Installed.Where(f => f.Matches(copied.Family)).ToList());
    installed.SetValue(null, new List<FontFaceInfo> { face }.Concat(FontCatalog.Installed).ToList());
    try
    {
        if (original is null || !LoadedFace.CanLoad(original.Regular))
            throw new SkipTestException("the copied font's own family does not load");
        var glyphs = GlyphSource.Create($"\"{face.Family}\", \"{copied.Family}\"", null, RasterLayout.Create(16, 1, 1));
        return !LoadedFace.CanLoad(face) && glyphs.Primary == original.Regular;
    }
    finally
    {
        FontCatalog.Rescan();
        LoadedFace.ReleaseAll();
        TryDeleteFile(broken);
    }
}

static void TryDeleteFile(string path)
{
    try
    {
        File.Delete(path);
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
    {
    }
}

/// <summary>A stand-in ffmpeg that reads every frame from stdin, then writes its output file (the last argument).</summary>
static string WriteFrameReadingFfmpeg(string directory)
{
    if (OperatingSystem.IsWindows())
    {
        // Not "more >nul": with enough LF bytes in its input, more stops reading and waits, and raw frames
        // can hold that many. spec_mp4.md records the measurement.
        const string WindowsStdinSink = "powershell -NoProfile -NonInteractive -Command \"[Console]::OpenStandardInput().CopyTo([IO.Stream]::Null)\"";
        var path = Path.Combine(directory, $"scenetake-ffmpeg-{Guid.NewGuid():N}.cmd");
        File.WriteAllText(path,
            "@echo off\r\n" +
            WindowsStdinSink + "\r\n" +
            ":last\r\n" +
            "if not \"%~2\"==\"\" (shift & goto last)\r\n" +
            "echo video> \"%~1\"\r\n" +
            "exit /b 0\r\n");
        return path;
    }
    else
    {
        var path = Path.Combine(directory, $"scenetake-ffmpeg-{Guid.NewGuid():N}");
        File.WriteAllText(path,
            "#!/bin/sh\n" +
            "cat >/dev/null\n" +
            "for last; do :; done\n" +
            "echo video > \"$last\"\n" +
            "exit 0\n");
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        return path;
    }
}

static bool LinkedFontFileLoads()
{
    // A font reached through a symbolic link must load like the file itself. A link's own size is
    // not the font's, so a size taken from the link would fail the table checks.
    var font = CopyInstalledTrueTypeFont(_ => { });
    var link = Path.Combine(Path.GetTempPath(), $"scenetake-font-link-{Guid.NewGuid():N}.ttf");
    try
    {
        try
        {
            File.CreateSymbolicLink(link, font);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new SkipTestException("this machine does not allow symbolic links");
        }

        return FontFileError(link) == "";
    }
    finally
    {
        File.Delete(link);
        File.Delete(font);
    }
}

static bool InstalledFontsCanBeRescanned()
{
    // pre may install a font, so the render scans again after pre. A rescan must not reuse the earlier scan.
    var first = FontCatalog.Installed;
    FontCatalog.Rescan();
    return !ReferenceEquals(first, FontCatalog.Installed) && FontCatalog.Installed.Count == first.Count;
}

static unsafe bool DefaultFontsPassTableChecks()
{
    // The table checks must not reject a font that stb_truetype loads. These are the fonts every
    // render tries, so a wrong length rule would show up here.
    var names = GlyphSource.DefaultMonospaceFamilies().Concat(GlyphSource.FallbackFamilies()).ToList();
    var faces = FontCatalog.Installed.Where(face => names.Any(face.Matches)).ToList();
    if (faces.Count == 0)
        throw new FontNotFoundException("none of the default fonts is installed");

    foreach (var face in faces)
    {
        var data = File.ReadAllBytes(face.Path);
        bool stbLoads;
        fixed (byte* bytes = data)
        {
            try
            {
                var offset = StbTrueTypeSharp.StbTrueType.stbtt_GetFontOffsetForIndex(bytes, face.Index);
                using var info = new StbTrueTypeSharp.StbTrueType.stbtt_fontinfo();
                stbLoads = offset >= 0 && StbTrueTypeSharp.StbTrueType.stbtt_InitFont(info, bytes, offset) != 0;
            }
            catch (Exception)
            {
                stbLoads = false;
            }
        }

        if (stbLoads && LoadedFace.TryLoad(face) is null)
        {
            Console.Error.WriteLine($"  rejected {face.Path} #{face.Index}");
            return false;
        }
    }

    return true;
}

/// <summary>The FontNotFoundException message for <paramref name="path"/> as --font-file, or "" when it loads.</summary>
static string FontFileError(string path)
{
    try
    {
        GlyphSource.Create("monospace", path, RasterLayout.Create(16, 1, 1));
        return "";
    }
    catch (FontNotFoundException ex)
    {
        return ex.Message;
    }
}

/// <summary>Copies an installed single-face TrueType font to a temporary file, changed by <paramref name="patch"/>.</summary>
static string CopyInstalledTrueTypeFont(Action<byte[]> patch) =>
    RewriteInstalledTrueTypeFont(data =>
    {
        patch(data);
        return data;
    });

/// <summary>Copies an installed single-face TrueType font to a temporary file, replaced by what <paramref name="patch"/> returns.</summary>
static string RewriteInstalledTrueTypeFont(Func<byte[], byte[]> patch)
{
    var data = patch(File.ReadAllBytes(InstalledSingleFaceTrueType()));
    var path = Path.Combine(Path.GetTempPath(), $"scenetake-font-{Guid.NewGuid():N}.ttf");
    File.WriteAllBytes(path, data);
    return path;
}

static string InstalledSingleFaceTrueType(bool nonVariable = false) =>
    FontCatalog.Installed
        .Where(face => !nonVariable || !face.Variable)
        .Select(face => face.Path)
        .FirstOrDefault(p => p.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase) && FontCatalog.ReadFaces(p).Count == 1)
    ?? throw new FontNotFoundException("no installed single-face TrueType font to copy");

static int TableOffset(byte[] data, string tag) =>
    (int)System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(TableRecord(data, tag) + 8));

static void WriteUInt16(byte[] data, int at, ushort value) =>
    System.Buffers.Binary.BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(at), value);

/// <summary>Appends a format 12 cmap subtable that maps <paramref name="codepoint"/> to <paramref name="glyph"/>, and points every Unicode encoding record at it.</summary>
static byte[] MapToGlyph(byte[] data, int codepoint, uint glyph)
{
    var cmap = TableOffset(data, "cmap");
    var subtable = new byte[28];
    WriteUInt16(subtable, 0, 12);
    System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(subtable.AsSpan(4), (uint)subtable.Length);
    System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(subtable.AsSpan(12), 1);
    System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(subtable.AsSpan(16), (uint)codepoint);
    System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(subtable.AsSpan(20), (uint)codepoint);
    System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(subtable.AsSpan(24), glyph);
    var result = data.Concat(subtable).ToArray();
    var count = System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(result.AsSpan(cmap + 2));
    for (var i = 0; i < count; i++)
    {
        var record = cmap + 4 + i * 8;
        var platform = System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(result.AsSpan(record));
        var encoding = System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(result.AsSpan(record + 2));
        if (platform == 0 || (platform == 3 && encoding is 1 or 10))
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(result.AsSpan(record + 4), (uint)(data.Length - cmap));
    }

    return result;
}

/// <summary>A font collection (TTC) of two single-face fonts, with each face's table offsets moved to where it lands.</summary>
static byte[] Collection(byte[] first, byte[] second)
{
    const int header = 12 + 2 * 4;
    var secondStart = header + ((first.Length + 3) & ~3);
    var result = new byte[secondStart + second.Length];
    System.Text.Encoding.ASCII.GetBytes("ttcf").CopyTo(result, 0);
    System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(result.AsSpan(4), 0x00010000);
    System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(result.AsSpan(8), 2);
    System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(result.AsSpan(12), header);
    System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(result.AsSpan(16), (uint)secondStart);
    foreach (var (face, start) in new[] { (first, header), (second, secondStart) })
    {
        face.CopyTo(result, start);
        var tables = System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(face.AsSpan(4));
        for (var i = 0; i < tables; i++)
        {
            var offset = result.AsSpan(start + 12 + i * 16 + 8, 4);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(offset, System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(offset) + (uint)start);
        }
    }

    return result;
}

static int TableRecord(byte[] data, string tag)
{
    var count = System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(4));
    for (var i = 0; i < count; i++)
    {
        var record = 12 + i * 16;
        if (System.Text.Encoding.ASCII.GetString(data, record, 4) == tag)
            return record;
    }

    throw new FontNotFoundException($"font has no {tag} table");
}

static void PatchTableOffset(byte[] data, string tag, uint offset) =>
    System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(TableRecord(data, tag) + 8), offset);

static void PatchTableLength(byte[] data, string tag, uint length) =>
    System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(TableRecord(data, tag) + 12), length);

static void PatchTableTag(byte[] data, string tag, string newTag) =>
    System.Text.Encoding.ASCII.GetBytes(newTag).CopyTo(data, TableRecord(data, tag));

// Every cmap encoding record becomes Windows platform 3, encoding 0 (symbol), as in Symbol.ttf.
static void PatchCmapToSymbol(byte[] data)
{
    var cmap = (int)System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(TableRecord(data, "cmap") + 8));
    var count = System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(cmap + 2));
    for (var i = 0; i < count; i++)
    {
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(cmap + 4 + i * 8), 3);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(cmap + 6 + i * 8), 0);
    }
}

sealed class SkipTestException(string reason) : Exception(reason);
