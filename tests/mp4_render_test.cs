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
// Tests that draw frames need one installed font; they print "skip" when none is found.
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
failures += RunFrames("FrameColorsMatchTheme", FrameColorsMatchTheme);
failures += RunFrames("CursorBlendsHalfForeground", CursorBlendsHalfForeground);
failures += RunFrames("NothingShownBeforeFirstEvent", NothingShownBeforeFirstEvent);
failures += RunFrames("FrameShowsEventsUpToItsTime", FrameShowsEventsUpToItsTime);
failures += RunFrames("TrailingBlankRestoreIsTrimmed", TrailingBlankRestoreIsTrimmed);
failures += RunFrames("MatrixTintMatchesSvg", MatrixTintMatchesSvg);
failures += RunFrames("MacosChromeDrawsTrafficLights", MacosChromeDrawsTrafficLights);
failures += RunFrames("WindowsChromeDrawsButtons", WindowsChromeDrawsButtons);

return failures == 0 ? 0 : 1;

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
    catch (Exception ex)
    {
        Console.Error.WriteLine($"FAIL {name}: {ex}");
        return 1;
    }
}

static int RunFrames(string name, Func<bool> test)
{
    try
    {
        return Run(name, test);
    }
    catch (FontNotFoundException ex)
    {
        Console.Error.WriteLine($"skip {name}: {ex.Message}");
        return 0;
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
    && Mp4Render.FitWithin(820, 488, 0, 0) == (820, 488);

static bool OptionParsing() =>
    Mp4Settings.TryParseFps("60", out var fps, out _) && fps == 60
    && !Mp4Settings.TryParseFps("0", out _, out _)
    && !Mp4Settings.TryParseFps("61", out _, out _)
    && !Mp4Settings.TryParseFps("30.5", out _, out _)
    && Mp4Settings.TryParseCrf("0", out _, out _) && !Mp4Settings.TryParseCrf("52", out _, out _)
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
        && plain.Contains("-c:v libx264 -preset slower -crf 23 -psy 0 -refs 8 -g 900", StringComparison.Ordinal)
        && plain.Contains("-colorspace bt709 -color_primaries bt709 -color_trc bt709", StringComparison.Ordinal)
        && plain.Contains("-movflags +faststart -f mp4", StringComparison.Ordinal)
        && scaled.Contains("scale=1280:764:flags=lanczos:out_color_matrix=bt709", StringComparison.Ordinal)
        && scaled.Contains("-crf 28 -psy 0 -refs 8 -g 1800", StringComparison.Ordinal)
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
    // ╔: both lines reach the right and bottom edges; nothing above or left of the outer corner.
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
    // Red background space at col 0; green █ at col 2. Dark theme: bg #282c34, palette 1 #ac4142, palette 2 #7e8e50.
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
    // Frame k shows events with time ≤ k / fps; the loop holds the last frame for one more idle gap (0.5s).
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
    // A full-screen app exits to a blank main screen; like the SVG, the video ends on the app's last screen.
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
