using Scenetake.TestSupport;

namespace Scenetake.Tests;

// mp4 frame rendering without ffmpeg: layout, colors, procedural glyphs, timing, and options.
// Tests that draw frames need one installed font. They skip when none is found.

[Category("Ffmpeg")]
public sealed class Mp4RenderTests
{
    [Test]
    public async Task LayoutRoundsSvgMetrics() => await Assert.That(Skipping.Run(() => Mp4RenderCases.LayoutRoundsSvgMetrics())).IsTrue();

    [Test]
    public async Task FitWithinKeepsAspectAndEvenSize() => await Assert.That(Skipping.Run(() => Mp4RenderCases.FitWithinKeepsAspectAndEvenSize())).IsTrue();

    [Test]
    public async Task SizePadsTerminalIntoFrame() => await Assert.That(Skipping.Run(() => Mp4RenderCases.SizePadsTerminalIntoFrame())).IsTrue();

    [Test]
    public async Task SizeSetsOutputAndScale() => await Assert.That(Skipping.Run(() => Mp4RenderCases.SizeSetsOutputAndScale())).IsTrue();

    [Test]
    public async Task SizeTooFarFromTerminalShapeIsReported() => await Assert.That(Skipping.Run(() => Mp4RenderCases.SizeTooFarFromTerminalShapeIsReported())).IsTrue();

    [Test]
    public async Task SizePaddingUsesBackgroundAndFullTitleBar() => await Assert.That(Skipping.Run(() => Mp4RenderCases.SizePaddingUsesBackgroundAndFullTitleBar())).IsTrue();

    [Test]
    public async Task Mp4PadsAStripToThreeToOne() => await Assert.That(Skipping.Run(() => Mp4RenderCases.Mp4PadsAStripToThreeToOne())).IsTrue();

    [Test]
    public async Task Mp4StripFramesCenterTheTerminal() => await Assert.That(Skipping.Run(() => Mp4RenderCases.Mp4StripFramesCenterTheTerminal())).IsTrue();

    [Test]
    public async Task SizeShapePadsAtTheTerminalsOwnSize() => await Assert.That(Skipping.Run(() => Mp4RenderCases.SizeShapePadsAtTheTerminalsOwnSize())).IsTrue();

    [Test]
    public async Task BackgroundPlacesWindowInMargin() => await Assert.That(Skipping.Run(() => Mp4RenderCases.BackgroundPlacesWindowInMargin())).IsTrue();

    [Test]
    public async Task BackgroundGradientRunsCornerToCorner() => await Assert.That(Skipping.Run(() => Mp4RenderCases.BackgroundGradientRunsCornerToCorner())).IsTrue();

    [Test]
    public async Task WindowOnBackgroundHasBorderAndCorners() => await Assert.That(Skipping.Run(() => Mp4RenderCases.WindowOnBackgroundHasBorderAndCorners())).IsTrue();

    [Test]
    public async Task TranslucentWindowShowsBackground() => await Assert.That(Skipping.Run(() => Mp4RenderCases.TranslucentWindowShowsBackground())).IsTrue();

    [Test]
    public async Task BlurSoftensBackgroundUnderWindow() => await Assert.That(Skipping.Run(() => Mp4RenderCases.BlurSoftensBackgroundUnderWindow())).IsTrue();

    [Test]
    public async Task TitleIsSansSerifAtItsOwnWidths() => await Assert.That(Skipping.Run(() => Mp4RenderCases.TitleIsSansSerifAtItsOwnWidths())).IsTrue();

    [Test]
    public async Task OptionParsing() => await Assert.That(Skipping.Run(() => Mp4RenderCases.OptionParsing())).IsTrue();

    [Test]
    public async Task FfmpegArgumentsUseH264Bt709() => await Assert.That(Skipping.Run(() => Mp4RenderCases.FfmpegArgumentsUseH264Bt709())).IsTrue();

    [Test]
    public async Task GifArgumentsUseOnePaletteWithoutDither() => await Assert.That(Skipping.Run(() => Mp4RenderCases.GifArgumentsUseOnePaletteWithoutDither())).IsTrue();

    [Test]
    public async Task GifThatPlaysOnceHasNoFade() => await Assert.That(Skipping.Run(() => Mp4RenderCases.GifThatPlaysOnceHasNoFade())).IsTrue();

    [Test]
    public async Task FontFamilyListParsing() => await Assert.That(Skipping.Run(() => Mp4RenderCases.FontFamilyListParsing())).IsTrue();

    [Test]
    public async Task BlockElementsFillCellEdges() => await Assert.That(Skipping.Run(() => Mp4RenderCases.BlockElementsFillCellEdges())).IsTrue();

    [Test]
    public async Task BoxLinesReachCellEdges() => await Assert.That(Skipping.Run(() => Mp4RenderCases.BoxLinesReachCellEdges())).IsTrue();

    [Test]
    public async Task DoubleCornerJoinsInsideCell() => await Assert.That(Skipping.Run(() => Mp4RenderCases.DoubleCornerJoinsInsideCell())).IsTrue();

    [Test]
    public async Task BrailleDrawsDots() => await Assert.That(Skipping.Run(() => Mp4RenderCases.BrailleDrawsDots())).IsTrue();

    [Test]
    public async Task SyntheticBoldAndItalic() => await Assert.That(Skipping.Run(() => Mp4RenderCases.SyntheticBoldAndItalic())).IsTrue();

    [Test]
    public async Task FrameColorsMatchTheme() => await Assert.That(Skipping.Run(() => Mp4RenderCases.FrameColorsMatchTheme())).IsTrue();

    [Test]
    public async Task CursorBlendsHalfForeground() => await Assert.That(Skipping.Run(() => Mp4RenderCases.CursorBlendsHalfForeground())).IsTrue();

    [Test]
    public async Task NothingShownBeforeFirstEvent() => await Assert.That(Skipping.Run(() => Mp4RenderCases.NothingShownBeforeFirstEvent())).IsTrue();

    [Test]
    public async Task FrameShowsEventsUpToItsTime() => await Assert.That(Skipping.Run(() => Mp4RenderCases.FrameShowsEventsUpToItsTime())).IsTrue();

    [Test]
    public async Task TrailingBlankRestoreIsTrimmed() => await Assert.That(Skipping.Run(() => Mp4RenderCases.TrailingBlankRestoreIsTrimmed())).IsTrue();

    [Test]
    public async Task EndHoldAndFadeOutEndOnTheEmptyTerminal() => await Assert.That(Skipping.Run(() => Mp4RenderCases.EndHoldAndFadeOutEndOnTheEmptyTerminal())).IsTrue();

    [Test]
    public async Task ScreenSwitchesBecomeKeyframes() => await Assert.That(Skipping.Run(() => Mp4RenderCases.ScreenSwitchesBecomeKeyframes())).IsTrue();

    [Test]
    public async Task TrimmedRestoreForcesNoKeyframe() => await Assert.That(Skipping.Run(() => Mp4RenderCases.TrimmedRestoreForcesNoKeyframe())).IsTrue();

    [Test]
    public async Task ForcedKeyframesAreSpacedAndCapped() => await Assert.That(Skipping.Run(() => Mp4RenderCases.ForcedKeyframesAreSpacedAndCapped())).IsTrue();

    [Test]
    public async Task MatrixTintMatchesSvg() => await Assert.That(Skipping.Run(() => Mp4RenderCases.MatrixTintMatchesSvg())).IsTrue();

    [Test]
    public async Task MacosChromeDrawsTrafficLights() => await Assert.That(Skipping.Run(() => Mp4RenderCases.MacosChromeDrawsTrafficLights())).IsTrue();

    [Test]
    public async Task WindowsChromeDrawsTabRow() => await Assert.That(Skipping.Run(() => Mp4RenderCases.WindowsChromeDrawsTabRow())).IsTrue();

    [Test]
    public async Task BoxDrawingAtTinySizesStaysInCell() => await Assert.That(Skipping.Run(() => Mp4RenderCases.BoxDrawingAtTinySizesStaysInCell())).IsTrue();

    [Test]
    public async Task HugeFrameIsReportedClearly() => await Assert.That(Skipping.Run(() => Mp4RenderCases.HugeFrameIsReportedClearly())).IsTrue();

    [Test]
    public async Task FfmpegProbeTimesOut() => await Assert.That(Skipping.Run(() => Mp4RenderCases.FfmpegProbeTimesOut())).IsTrue();

    [Test]
    public async Task Libx264CheckRunsOncePerFfmpeg() => await Assert.That(Mp4RenderCases.Libx264CheckRunsOncePerFfmpeg()).IsTrue();

    [Test]
    public async Task OutputPathIsCheckedFirst() => await Assert.That(Skipping.Run(() => Mp4RenderCases.OutputPathIsCheckedFirst())).IsTrue();

    [Test]
    public async Task FontTableOutsideFileIsRejected() => await Assert.That(Skipping.Run(() => Mp4RenderCases.FontTableOutsideFileIsRejected())).IsTrue();

    [Test]
    public async Task SymbolOnlyFontIsRejected() => await Assert.That(Skipping.Run(() => Mp4RenderCases.SymbolOnlyFontIsRejected())).IsTrue();

    [Test]
    public async Task UnreadableFontFileIsReported() => await Assert.That(Skipping.Run(() => Mp4RenderCases.UnreadableFontFileIsReported())).IsTrue();

    [Test]
    public async Task X264SideLimitIsChecked() => await Assert.That(Skipping.Run(() => Mp4RenderCases.X264SideLimitIsChecked())).IsTrue();

    [Test]
    public async Task FontFileThatCannotLoadIsAnError() => await Assert.That(Skipping.Run(() => Mp4RenderCases.FontFileThatCannotLoadIsAnError())).IsTrue();

    [Test]
    public async Task ShortFontTableIsRejected() => await Assert.That(Skipping.Run(() => Mp4RenderCases.ShortFontTableIsRejected())).IsTrue();

    [Test]
    public async Task DefaultFontsPassTableChecks() => await Assert.That(Skipping.Run(() => Mp4RenderCases.DefaultFontsPassTableChecks())).IsTrue();

    [Test]
    public async Task SoftGlyphScalesByCoverage() => await Assert.That(Skipping.Run(() => Mp4RenderCases.SoftGlyphScalesByCoverage())).IsTrue();

    [Test]
    public async Task SoftGlyphFrameUsesCellColor() => await Assert.That(Skipping.Run(() => Mp4RenderCases.SoftGlyphFrameUsesCellColor())).IsTrue();

    [Test]
    public async Task OutOfRangeGlyphIdsAreMissing() => await Assert.That(Skipping.Run(() => Mp4RenderCases.OutOfRangeGlyphIdsAreMissing())).IsTrue();

    [Test]
    public async Task EveryFontFileFaceMustLoad() => await Assert.That(Skipping.Run(() => Mp4RenderCases.EveryFontFileFaceMustLoad())).IsTrue();

    [Test]
    public async Task FontFileIsNotHeldOpen() => await Assert.That(Skipping.Run(() => Mp4RenderCases.FontFileIsNotHeldOpen())).IsTrue();

    [Test]
    public async Task FontFileIsReleasedAfterRender() => await Assert.That(Skipping.Run(() => Mp4RenderCases.FontFileIsReleasedAfterRender())).IsTrue();

    [Test]
    public async Task LinkedFontFileLoads() => await Assert.That(Skipping.Run(() => Mp4RenderCases.LinkedFontFileLoads())).IsTrue();

    [Test]
    public async Task InstalledFontsCanBeRescanned() => await Assert.That(Skipping.Run(() => Mp4RenderCases.InstalledFontsCanBeRescanned())).IsTrue();

    [Test]
    public async Task LogNamesAFontThatLoads() => await Assert.That(Skipping.Run(() => Mp4RenderCases.LogNamesAFontThatLoads())).IsTrue();

    [Test]
    public async Task WindowShapesCoverWhatSamplingCovers() => await Assert.That(Mp4RenderCases.WindowShapesCoverWhatSamplingCovers()).IsTrue();

    [Test]
    public async Task ShadowMatchesTheBlurOfEveryRow() => await Assert.That(Mp4RenderCases.ShadowMatchesTheBlurOfEveryRow()).IsTrue();

    [Test]
    public async Task FramesDrawnInTurnMatchFramesDrawnAlone() => await Assert.That(Skipping.Run(() => Mp4RenderCases.FramesDrawnInTurnMatchFramesDrawnAlone())).IsTrue();
}

internal static class Mp4RenderCases
{

    internal static bool LayoutRoundsSvgMetrics()
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

    internal static bool BackgroundPlacesWindowInMargin()
    {
        // 10×3 at 16px with macOS chrome is 120 × 102. On a background the window keeps that size, 32 pixels in.
        var desktop = RasterLayout.Create(16, 10, 3, WindowStyle.Macos, desktop: true);
        var margin = RasterLayout.Create(16, 10, 3, WindowStyle.Macos, margin: 0, desktop: true);
        // Without a background the margin has nothing to show, so it does not apply.
        var noDesktop = RasterLayout.Create(16, 10, 3, WindowStyle.Macos, margin: 40);
        // Padding replaces the default 10 × 4.
        var padded = RasterLayout.Create(16, 10, 3, WindowStyle.None, padding: 12);
        // --size grows the background, and the window stays centered: (1920 - 184) / 2 = 868, (1080 - 166) / 2 = 457.
        var sized = desktop.FitFrame(1920, 1080);
        return desktop is { Width: 184, Height: 166, WindowX: 32, WindowY: 32, WindowWidth: 120, WindowHeight: 102, Desktop: true }
            && desktop.CellX(0) == 42 && desktop.RowY(0) == 32 + 34 + 4
            && margin is { Width: 120, Height: 102, WindowX: 0, WindowY: 0 }
            && noDesktop is { Width: 120, Height: 102, WindowX: 0, WindowWidth: 120, Desktop: false }
            && padded is { PadX: 12, PadY: 12, Width: 124, Height: 84 }
            && sized is { Width: 1920, Height: 1080, WindowX: 900, WindowY: 489, WindowWidth: 120 } && sized.CellX(0) == 910;
    }

    internal static bool BackgroundGradientRunsCornerToCorner()
    {
        var layout = RasterLayout.Create(16, 10, 3, WindowStyle.None, margin: 20, desktop: true);
        var pixels = DesktopRaster.Render(layout, new BackgroundSpec(["#000000", "#ffffff"], null), static (_, _) => throw new InvalidOperationException("no image"));
        var (width, height) = (layout.Width, layout.Height);
        int At(int x, int y) => pixels[(y * width + x) * 3];
        // Dark at the top-left corner, light at the bottom-right one, and the middle half way, as the SVG's
        // gradient from (0,0) to (1,1) in bounding-box units.
        var solid = DesktopRaster.Render(layout, new BackgroundSpec(["#123456"], null), static (_, _) => throw new InvalidOperationException("no image"));
        var image = DesktopRaster.Render(layout, new BackgroundSpec([], "wall.png"), (w, h) => new byte[w * h * 3]);
        return At(0, 0) < 5 && At(width - 1, height - 1) > 250 && Math.Abs(At(width / 2, height / 2) - 128) <= 3
            && solid[0] == 0x12 && solid[1] == 0x34 && solid[2] == 0x56 && solid[^1] == 0x56
            && image.Length == width * height * 3;
    }

    internal static bool WindowOnBackgroundHasBorderAndCorners()
    {
        var layout = RasterLayout.Create(16, 10, 3, WindowStyle.Macos, desktop: true);
        var theme = TerminalTheme.FromResolved(new ResolvedTheme("#d0d0d0", "#282c34", RenderSettingsResolver.DefaultPalette));
        var desktop = DesktopRaster.Render(layout, new BackgroundSpec(["#ff0000"], null), static (_, _) => throw new InvalidOperationException("no image"));
        var raster = new FrameRasterizer(layout, theme, GlyphSource.Create("monospace", null, layout), desktop);
        (int R, int G, int B) At(int x, int y)
        {
            var offset = (y * layout.Width + x) * 3;
            return (raster.Pixels[offset], raster.Pixels[offset + 1], raster.Pixels[offset + 2]);
        }

        // Outside the window: the background. The window's top-left corner is rounded, so its corner pixel is still
        // background, a little shaded by the shadow. Its top edge is the macOS dark border, the bar below is the title bar color, and the terminal
        // area is the theme background. Just inside the border, the dark macOS window's white line at alpha 41 (0.16).
        // Beside and below the window its shadow darkens the background, more near the window; far away it does not.
        var (x, y) = (layout.WindowX, layout.WindowY);
        var (width, height) = (layout.WindowWidth, layout.WindowHeight);
        static int Blend(int under, int over) => under + (over - under) * 41 / 255;
        var beside = At(x + width + 5, y + 60);
        var farther = At(x + width + 20, y + 60);
        var below = At(x + width / 2, y + height + 8);
        var ok = At(0, 0) == (255, 0, 0)
            && At(x, y) is { R: > 200, G: 0, B: 0 }
            && At(x + 20, y) == (0x1a, 0x1a, 0x1a)
            && At(x + 60, y + 5) == (0x32, 0x32, 0x32)
            && At(x + 60, y + 60) == (0x28, 0x2c, 0x34)
            && At(x + 1, y + 60) == (Blend(0x28, 255), Blend(0x2c, 255), Blend(0x34, 255))
            && beside is { R: > 150 and < 250, G: 0, B: 0 }
            && farther.R > beside.R && farther.R < 255
            && below is { R: > 120 and < 240, G: 0, B: 0 };
        if (!ok)
            Console.Error.WriteLine($"  corner {At(0, 0)} {At(x, y)}, border {At(x + 20, y)}, bar {At(x + 60, y + 5)}, terminal {At(x + 60, y + 60)}, edge {At(x + 1, y + 60)}, beside {beside}, farther {farther}, below {below}");
        return ok;
    }

    internal static bool TranslucentWindowShowsBackground()
    {
        // Theme bg #282c34 at opacity 0.5 (alpha 128) over a red background, in the terminal and in a row drawn again
        // after its text changed: the row comes back with the background showing through, not filled with the theme
        // color. The macOS title bar #323232 blends the same way; its border #1a1a1a stays opaque.
        var (frames, layout) = Render("""
        [0.0,"o","ab"]
        [0.5,"o","cd\r\nef"]
        """, fps: 10, window: "macos", tags: ["st:background=#ff0000", "st:opacity=0.5"]);
        static byte Blend(int under, int over) => (byte)(under + (over - under) * 128 / 255);
        var terminal = (Blend(255, 0x28), Blend(0, 0x2c), Blend(0, 0x34));
        var bar = (Blend(255, 0x32), Blend(0, 0x32), Blend(0, 0x32));
        var last = frames[^1];
        (byte, byte, byte) At(int x, int y)
        {
            var offset = (y * layout.Width + x) * 3;
            return (last[offset], last[offset + 1], last[offset + 2]);
        }

        var (x, y) = (layout.WindowX, layout.WindowY);
        var ok = CellPixel(last, layout, 0, 6) == terminal
            && CellPixel(last, layout, 2, 6) == terminal
            && At(x + 60, y + 5) == bar
            && At(x + 20, y) == (0x1a, 0x1a, 0x1a)
            && At(0, 0) == (255, 0, 0);
        if (!ok)
            Console.Error.WriteLine($"  row 0: {CellPixel(last, layout, 0, 6)}, row 2: {CellPixel(last, layout, 2, 6)}, bar: {At(x + 60, y + 5)}, expected {terminal} and {bar}");
        return ok;
    }

    internal static bool BlurSoftensBackgroundUnderWindow()
    {
        // A background red on the left half and blue on the right, under a window with no fill of its own (opacity 0)
        // and a blur of 4: inside the window the edge is soft, outside it is sharp, and more than 3 deviations from the
        // edge the colors are pure.
        var layout = RasterLayout.Create(16, 10, 3, WindowStyle.None, margin: 20, desktop: true);
        var theme = TerminalTheme.FromResolved(new ResolvedTheme("#d0d0d0", "#282c34", RenderSettingsResolver.DefaultPalette));
        var split = layout.Width / 2;
        var desktop = new byte[layout.Width * layout.Height * 3];
        for (var i = 0; i < desktop.Length; i += 3)
        {
            var red = i / 3 % layout.Width < split;
            desktop[i] = red ? (byte)255 : (byte)0;
            desktop[i + 2] = red ? (byte)0 : (byte)255;
        }

        var glyphs = GlyphSource.Create("monospace", null, layout);
        var blurred = new FrameRasterizer(layout, theme, glyphs, desktop, opacity: 0, blur: 4);
        var sharp = new FrameRasterizer(layout, theme, glyphs, desktop, opacity: 0);
        (int R, int G, int B) At(FrameRasterizer raster, int x, int y)
        {
            var offset = (y * layout.Width + x) * 3;
            return (raster.Pixels[offset], raster.Pixels[offset + 1], raster.Pixels[offset + 2]);
        }

        var inside = layout.WindowY + layout.WindowHeight / 2;
        var outside = layout.WindowY / 2;
        var nearEdge = At(blurred, split - 1, inside);
        var ok = nearEdge.R is > 60 and < 250 && nearEdge.B is > 5 and < 195
            && At(blurred, split - 13, inside) == (255, 0, 0)
            && At(blurred, split + 13, inside) == (0, 0, 255)
            && At(blurred, split - 1, outside) == (255, 0, 0)
            && At(blurred, split, outside) == (0, 0, 255)
            && At(sharp, split - 1, inside) == (255, 0, 0)
            && At(sharp, split, inside) == (0, 0, 255);
        if (!ok)
            Console.Error.WriteLine($"  near the edge {nearEdge}, 13 px left {At(blurred, split - 13, inside)}, outside {At(blurred, split - 1, outside)}");
        return ok;
    }

    internal static bool TitleIsSansSerifAtItsOwnWidths()
    {
        // The title is drawn at each glyph's own width, centered on the window by that width, as the SVG's
        // text-anchor centers it: four i's take far less room than four W's, and both are centered.
        var layout = RasterLayout.Create(16, 60, 3, WindowStyle.Macos);
        var theme = TerminalTheme.FromResolved(new ResolvedTheme("#d0d0d0", "#282c34", RenderSettingsResolver.DefaultPalette));
        var glyphs = GlyphSource.Create("monospace", null, layout);
        var titleGlyphs = GlyphSource.Create(SvgFrameRenderer.ChromeTitleFontFamily, null, RasterLayout.Create(13, 1, 1));
        (int Left, int Right) Ink(string title)
        {
            var raster = new FrameRasterizer(layout, theme, glyphs, titleGlyphs: titleGlyphs, title: title);
            var (left, right) = (int.MaxValue, int.MinValue);
            // Between the buttons' room on both sides, in the bar: pixels lighter than the bar are the title's.
            for (var py = 4; py < layout.TitleBarHeight - 4; py++)
            {
                for (var px = 100; px < layout.Width - 100; px++)
                {
                    if (raster.Pixels[(py * layout.Width + px) * 3] > 0x60)
                        (left, right) = (Math.Min(left, px), Math.Max(right, px));
                }
            }

            return (left, right);
        }

        var narrow = Ink("iiii");
        var wide = Ink("WWWW");
        var center = layout.Width / 2.0;
        var ok = narrow.Right > narrow.Left && wide.Right > wide.Left
            && (narrow.Right - narrow.Left) * 2 < wide.Right - wide.Left
            && Math.Abs((narrow.Left + narrow.Right + 1) / 2.0 - center) <= 3
            && Math.Abs((wide.Left + wide.Right + 1) / 2.0 - center) <= 3;
        if (!ok)
            Console.Error.WriteLine($"  iiii ink {narrow}, WWWW ink {wide}, center {center}");
        return ok;
    }

    internal static bool FitWithinKeepsAspectAndEvenSize() =>
        Mp4Render.FitWithin(2020, 1208, 1280, 1024) == (1280, 764)
        && Mp4Render.FitWithin(820, 488, 1280, 1024) == (820, 488)   // never upscales
        && Mp4Render.FitWithin(1000, 3000, 1280, 1024) == (340, 1024) // height-limited
        && Mp4Render.FitWithin(820, 488, 0, 0) == (820, 488)
        && Mp4Render.FitWithin(820, 1288, 1280, 1024) == (650, 1024); // 1288 × 1024/1288 in floating point is 1023.99…

    internal static bool SizePadsTerminalIntoFrame()
    {
        // 190×53 at 16px is 1920×1068: the 12 spare rows of pixels split 6 above and 6 below.
        var fits = RasterLayout.Create(16, 190, 53).FitFrame(1920, 1080);
        // 34 + 51×20 + 8 = 1062 with a title bar: the bar stays at the top, and 9 + 9 go around the terminal.
        var chrome = RasterLayout.Create(16, 190, 51, WindowStyle.Macos).FitFrame(1920, 1080);
        // 1×1 at 15px is 27×27 (cell 9×19, padding 9×4) before Create's even rounding: 37 spare pixels, 18 left and 19 right.
        var odd = RasterLayout.Create(15, 1, 1).FitFrame(64, 64);
        // 200×60 is 2020×1208, narrower than 16:9: padded at the sides to 2148×1208, to be scaled to 1920×1080.
        var narrow = RasterLayout.Create(16, 200, 60).FitFrame(1920, 1080);
        // 512×24 is 5140×488, wider than 16:9: padded above and below to 5140×2892.
        var wide = RasterLayout.Create(16, 512, 24).FitFrame(1920, 1080);
        // Larger in one side only: 190×60 is 1920×1208, so the width is padded too.
        var tall = RasterLayout.Create(16, 190, 60).FitFrame(1920, 1080);
        // A frame of exactly the terminal's size changes nothing.
        var exact = RasterLayout.Create(16, 80, 24).FitFrame(820, 488);
        return fits is { Width: 1920, Height: 1080, PadX: 10, PadY: 10, CellWidth: 10, CellHeight: 20 } && fits.RowY(0) == 10 && fits.CellX(0) == 10
            && chrome is { Width: 1920, Height: 1080, TitleBarHeight: 34, PadY: 13 } && chrome.RowY(0) == 47
            && odd is { Width: 64, Height: 64, PadX: 27, PadY: 22 }
            && narrow is { Width: 2148, Height: 1208, PadX: 74, PadY: 4 }
            && wide is { Width: 5140, Height: 2892, PadX: 10, PadY: 1206 }
            && tall is { Width: 2148, Height: 1208, PadX: 124, PadY: 4 }
            && exact == RasterLayout.Create(16, 80, 24);
    }

    internal static bool SizeSetsOutputAndScale()
    {
        var size = Mp4Settings.Default with { FrameWidth = 1920, FrameHeight = 1080 };
        var fits = Mp4Render.CreateLayout(16, 190, 53, WindowStyle.None, size);
        var large = Mp4Render.CreateLayout(16, 200, 60, WindowStyle.None, size);
        var fitsArgs = string.Join(' ', Mp4Render.BuildArguments(fits.Width, fits.Height, size, 1920, 1080, "out.mp4"));
        var largeArgs = string.Join(' ', Mp4Render.BuildArguments(large.Width, large.Height, size, 1920, 1080, "out.mp4"));
        return Mp4Render.OutputSize(fits, size) == (1920, 1080) && Mp4Render.OutputSize(large, size) == (1920, 1080)
            && fitsArgs.Contains("-s 1920x1080 ", StringComparison.Ordinal) && !fitsArgs.Contains("lanczos", StringComparison.Ordinal)
            && largeArgs.Contains("-s 2148x1208 ", StringComparison.Ordinal) && largeArgs.Contains("scale=1920:1080:flags=lanczos", StringComparison.Ordinal)
            && Mp4Render.CreateLayout(16, 80, 24, WindowStyle.Macos, Mp4Settings.Default) == RasterLayout.Create(16, 80, 24, WindowStyle.Macos);
    }

    internal static bool SizeTooFarFromTerminalShapeIsReported()
    {
        // 512×1 at 50px is 15904×79. Padding it to the shape of 16×16384 needs 15904×16285696 pixels before
        // scaling, far over ffmpeg's raw frame limit, while the same terminal fits a 16:9 frame.
        try
        {
            RasterLayout.Create(50, 512, 1).FitFrame(16, 16384);
            return false;
        }
        catch (FrameTooLargeException ex)
        {
            return ex.Message.Contains("15904x16285696", StringComparison.Ordinal)
                && ex.Message.Contains("--size 16x16384", StringComparison.Ordinal)
                && RasterLayout.Create(50, 512, 1).FitFrame(1920, 1080) is { Width: 15904, Height: 8946 };
        }
    }

    internal static bool SizePaddingUsesBackgroundAndFullTitleBar()
    {
        // 8×3 with a title bar is 100×102 at 16px. In 200×200 the cells start at x 10 + 50 and y 34 + 4 + 49.
        var (frames, layout) = Render("""[0.0,"o","█\u001b[?25l"]""", fps: 10, window: "macos", size: (200, 200));
        var last = frames[^1];
        var background = (0x28, 0x2c, 0x34);
        return layout is { Width: 200, Height: 200, PadX: 60, PadY: 53 } && last.Length == 200 * 200 * 3
            && Pixel(last, layout, 22, 17) == (0xff, 0x5f, 0x57)            // buttons keep their place at the left
            && Pixel(last, layout, 195, 5) == (0x32, 0x32, 0x32)            // the bar spans the whole frame
            && Pixel(last, layout, 195, 33) == (0x1a, 0x1a, 0x1a)
            && Pixel(last, layout, 5, 60) == background && Pixel(last, layout, 195, 195) == background
            && Pixel(last, layout, 100, 40) == background                   // padding between the bar and the terminal
            && CellPixel(last, layout, 0, 0) == (0xd0, 0xd0, 0xd0) && layout.CellX(0) == 60 && layout.RowY(0) == 87;
    }

    internal static bool Mp4PadsAStripToThreeToOne()
    {
        var gif = Mp4Settings.Default with { LimitAspect = false };
        // 80×5 at 16px is 820×108, 7.6:1. An mp4 pads it to 820×274 (820 / 3 rounded up to even), the cells centered:
        // 4 + (274 - 108) / 2 = 87 above them. A GIF keeps the strip, and --size pads to its own frame instead.
        var strip = Mp4Render.CreateLayout(16, 80, 5, WindowStyle.None, Mp4Settings.Default);
        var gifStrip = Mp4Render.CreateLayout(16, 80, 5, WindowStyle.None, gif);
        var sized = Mp4Render.CreateLayout(16, 80, 5, WindowStyle.None, Mp4Settings.Default with { FrameWidth = 1280, FrameHeight = 720 });
        // With a title bar, 34 + 80 + 8 = 122: the bar stays at the top, and 76 + 76 go around the terminal below it.
        var chrome = Mp4Render.CreateLayout(16, 80, 4, WindowStyle.Macos, Mp4Settings.Default);
        // 2×40 is 40×808, taller than 1:3: padded at the sides to 270×808.
        var tall = Mp4Render.CreateLayout(16, 2, 40, WindowStyle.None, Mp4Settings.Default);
        // 80×24 is 820×488, within the shape: nothing changes.
        var whole = Mp4Render.CreateLayout(16, 80, 24, WindowStyle.None, Mp4Settings.Default);
        // At the edge: 54×9 at 16px is 560×188, under 3:1 (564), and stays. 55×9 is 570×188, over it, padded to 570×190.
        var under = Mp4Render.CreateLayout(16, 54, 9, WindowStyle.None, Mp4Settings.Default);
        var over = Mp4Render.CreateLayout(16, 55, 9, WindowStyle.None, Mp4Settings.Default);
        var options = new Mp4Options();
        var ok = strip is { Width: 820, Height: 274, PadX: 10, PadY: 87 } && strip.RowY(0) == 87
            && gifStrip == RasterLayout.Create(16, 80, 5)
            && sized is { Width: 1280, Height: 720 }
            && chrome is { Width: 820, Height: 274, TitleBarHeight: 34, PadY: 80 } && chrome.RowY(0) == 114
            && tall is { Width: 270, Height: 808, PadX: 125, PadY: 4 }
            && whole == RasterLayout.Create(16, 80, 24)
            && under == RasterLayout.Create(16, 54, 9)
            && over is { Width: 570, Height: 190, PadY: 5 }
            // --max-size scales the padded frame, keeping its shape.
            && Mp4Render.OutputSize(strip, Mp4Settings.Default with { MaxWidth = 410, MaxHeight = 1024 }) == (410, 136)
            && options.ToSettings(1).LimitAspect && !options.ToGifSettings(1).LimitAspect;
        // 512×1 at 128px is about 40000×180, within ffmpeg's raw frame limit, and over it padded to 3:1.
        try
        {
            Mp4Render.CreateLayout(128, 512, 1, WindowStyle.None, Mp4Settings.Default);
            ok = false;
            Console.Error.WriteLine("  a 512x1 terminal at 128px padded to 3:1 was not reported");
        }
        catch (FrameTooLargeException e)
        {
            ok &= e.Message.Contains("which an mp4 pads to 3:1 without --size", StringComparison.Ordinal);
        }

        if (!ok)
            Console.Error.WriteLine($"  strip {strip}, chrome {chrome}, tall {tall}, under {under}, over {over}");
        return ok;
    }

    internal static bool Mp4StripFramesCenterTheTerminal()
    {
        // A 40×1 terminal is 420×28 at 16px, and 484×92 with the 32px margin of a background, 5.3:1. The mp4 is
        // 484×162, the window 35 pixels lower and the background above and below it.
        var path = Path.Combine(Path.GetTempPath(), $"scenetake-mp4-{Guid.NewGuid():N}.cast");
        try
        {
            File.WriteAllText(path,
                """{"version":3,"term":{"cols":40,"rows":1,"type":"xterm-256color"},"tags":["st:fit=none","st:background=#102030"]}""" + "\n"
                + """[0.0,"o","█\u001b[?25l"]""" + "\n");
            var recording = CastReader.Read(path);
            var renderer = new Mp4FrameRenderer(recording.Events, recording.Width, recording.Height, recording.RenderSettings,
                Mp4Settings.Default with { Fps = 10 },
                RenderSettingsResolver.ResolveLoopDuration(recording.LoopDuration, recording.Events, recording.RenderSettings));
            byte[]? last = null;
            renderer.Render(pixels => last = (byte[])pixels.Clone());
            var layout = renderer.Layout;
            var ok = layout is { Width: 484, Height: 162, WindowX: 32, WindowY: 67, Desktop: true }
                && Pixel(last!, layout, 2, 2) == (0x10, 0x20, 0x30) && Pixel(last!, layout, 2, 159) == (0x10, 0x20, 0x30)
                && CellPixel(last!, layout, 0, 0) == (0xd0, 0xd0, 0xd0);
            if (!ok)
                Console.Error.WriteLine($"  layout {layout}");
            return ok;
        }
        finally
        {
            File.Delete(path);
        }
    }

    internal static bool SizeShapePadsAtTheTerminalsOwnSize()
    {
        static RasterLayout Shaped(int cols, int rows, int w, int h, WindowStyle window = WindowStyle.None) =>
            Mp4Render.CreateLayout(16, cols, rows, window, Mp4Settings.Default with { ShapeWidth = w, ShapeHeight = h });

        // 80×5 at 16px is 820×108. 16:9 pads it above and below to 820×462 (820 × 9 / 16 = 461.25, up to even), the
        // cells centered: 4 + (462 - 108) / 2 = 181 above them. Nothing is scaled, so the text keeps its size.
        var strip = Shaped(80, 5, 16, 9);
        // 80×24 is 820×488, taller than 16:9: padded at the sides to 868×488 (488 × 16 / 9 = 867.6).
        var whole = Shaped(80, 24, 16, 9);
        // 9:16 for a phone, 1:1, and a shape written in pixels.
        var phone = Shaped(80, 5, 9, 16);
        var square = Shaped(80, 24, 1, 1);
        var pixels = Shaped(80, 5, 1920, 1080);
        // A frame already at the shape stays as it is.
        var exact = Shaped(80, 24, 820, 488);
        // The shape asked for wins over the 3:1 an mp4 takes without --size: 5:1 is wider, and stays.
        var wide = Shaped(80, 5, 5, 1);
        // With a title bar it stays at the top: 34 + 80 + 8 = 122 is padded to 462, 170 above the terminal below it.
        var chrome = Shaped(80, 4, 16, 9, WindowStyle.Macos);
        var options = new Mp4Options { Shape = (16, 9) };
        var ok = strip is { Width: 820, Height: 462, PadX: 10, PadY: 181, CellWidth: 10, CellHeight: 20 }
            && whole is { Width: 868, Height: 488, PadX: 34, PadY: 4 }
            && phone is { Width: 820, Height: 1458 }
            && square is { Width: 820, Height: 820 }
            && pixels is { Width: 820, Height: 462 }
            && exact == RasterLayout.Create(16, 80, 24)
            && wide is { Width: 820, Height: 164 }
            && chrome is { Width: 820, Height: 462, TitleBarHeight: 34, PadY: 174 } && chrome.RowY(0) == 208
            // --max-size scales the shaped frame: 820×462 to 410×230 (231 down to even).
            && Mp4Render.OutputSize(strip, Mp4Settings.Default with { ShapeWidth = 16, ShapeHeight = 9, MaxWidth = 410, MaxHeight = 1024 }) == (410, 230)
            // An mp4 and a GIF both take it, and the GIF still has no 3:1 of its own.
            && options.ToSettings(1) is { ShapeWidth: 16, ShapeHeight: 9, FrameWidth: 0 }
            && options.ToGifSettings(1) is { ShapeWidth: 16, ShapeHeight: 9, LimitAspect: false }
            && Mp4Render.CreateLayout(16, 80, 5, WindowStyle.None, Mp4Settings.Default with { LimitAspect = false, ShapeWidth = 16, ShapeHeight = 9 }) == strip;
        // 1:16384 would make 820×108 over 13 million pixels tall: the error names the shape before any int overflows.
        try
        {
            Shaped(80, 5, 1, 16384);
            ok = false;
            Console.Error.WriteLine("  a 1:16384 shape was not reported");
        }
        catch (FrameTooLargeException e)
        {
            ok &= e.Message.Contains("--size 1:16384 pads the 820x108 frame to that shape", StringComparison.Ordinal);
        }

        if (!ok)
            Console.Error.WriteLine($"  strip {strip}, whole {whole}, phone {phone}, square {square}, wide {wide}, chrome {chrome}");
        return ok;
    }

    internal static bool OptionParsing() =>
        Mp4Settings.TryParseFps("60", out var fps, out _) && fps == 60
        && !Mp4Settings.TryParseFps("0", out _, out _)
        && !Mp4Settings.TryParseFps("61", out _, out _)
        && !Mp4Settings.TryParseFps("30.5", out _, out _)
        && !Mp4Settings.TryParseCrf("0", out _, out var crfError) && crfError == "--crf must be between 1 and 51: 0" // lossless 0 is High 4:4:4
        && Mp4Settings.TryParseCrf("1", out _, out _) && !Mp4Settings.TryParseCrf("52", out _, out _)
        && Mp4Settings.TryParseMaxSize("1280x1024", out var w, out var h, out _) && (w, h) == (1280, 1024)
        && !Mp4Settings.TryParseMaxSize("1280", out _, out _, out _)
        && !Mp4Settings.TryParseMaxSize("8x8", out _, out _, out _)
        && !Mp4Settings.TryParseMaxSize("-1x100", out _, out _, out _)
        && Mp4Settings.TryParseSize("1920x1080", out var sw, out var sh, out _) && (sw, sh) == (1920, 1080)
        && Mp4Settings.TryParseSize("16X16384", out _, out _, out _)
        && !Mp4Settings.TryParseSize("1921x1080", out _, out _, out var oddError)
        && oddError == "--size must have an even width and height, which yuv420p requires: 1921x1080"
        && !Mp4Settings.TryParseSize("1920x1081", out _, out _, out _)
        && !Mp4Settings.TryParseSize("14x14", out _, out _, out var rangeError)
        && rangeError == "--size must be WIDTHxHEIGHT with each side between 16 and 16384 (for example 1920x1080), or a shape W:H such as 16:9: 14x14"
        && !Mp4Settings.TryParseSize("16386x1080", out _, out _, out _)
        && !Mp4Settings.TryParseSize("1920", out _, out _, out _)
        // --size W:H: whole numbers from 1, odd ones too, since only the padded frame needs even sides.
        && Mp4Settings.TryParseShape("16:9", out var shapeWidth, out var shapeHeight, out _) && (shapeWidth, shapeHeight) == (16, 9)
        && Mp4Settings.TryParseShape("1:1", out _, out _, out _) && Mp4Settings.TryParseShape("1920:1080", out _, out _, out _)
        && Mp4Settings.TryParseShape("16384:1", out _, out _, out _)
        && !Mp4Settings.TryParseShape("0:9", out _, out _, out var shapeError)
        && shapeError == "--size W:H must have each side between 1 and 16384 (for example 16:9): 0:9"
        && !Mp4Settings.TryParseShape("16384:16385", out _, out _, out _)
        && !Mp4Settings.TryParseShape("2.39:1", out _, out _, out _)
        && !Mp4Settings.TryParseShape("16:9:1", out _, out _, out _)
        && !Mp4Settings.TryParseShape("16:", out _, out _, out _)
        && !Mp4Settings.TryParseShape("-16:9", out _, out _, out _);

    internal static bool GifArgumentsUseOnePaletteWithoutDither()
    {
        // Pass 1 writes one palette from every frame. Pass 2 maps frames to it without dithering, stores only the changed
        // rectangle, and loops unless told not to. A scaled GIF scales in both passes, and an unscaled one passes through null.
        var palette = GifRender.PaletteArguments(820, 488, 20, 820, 488, "p.png");
        var looping = GifRender.GifArguments(820, 488, 20, 820, 488, "p.png", loops: true, "out.gif");
        var once = GifRender.GifArguments(820, 488, 20, 410, 244, "p.png", loops: false, "out.gif");
        var scaledPalette = GifRender.PaletteArguments(820, 488, 20, 410, 244, "p.png");
        string After(List<string> args, string name) => args[args.IndexOf(name) + 1];
        return After(palette, "-vf") == "null,palettegen=stats_mode=full" && palette[^1] == "p.png" && After(palette, "-r") == "20"
            && After(looping, "-lavfi") == "[0:v]null[frames];[frames][1:v]paletteuse=dither=none:diff_mode=rectangle"
            && After(looping, "-loop") == "0" && After(looping, "-f") is "rawvideo" && looping[^1] == "out.gif" && looping.Contains("gif")
            && After(once, "-loop") == "-1" && After(once, "-lavfi").StartsWith("[0:v]scale=410:244:flags=lanczos[frames];", StringComparison.Ordinal)
            && After(scaledPalette, "-vf") == "scale=410:244:flags=lanczos,palettegen=stats_mode=full";
    }

    internal static bool GifThatPlaysOnceHasNoFade()
    {
        // As an SVG that plays once, a GIF that plays once stops on its final screen, so it has no fade-out. One that loops keeps it.
        var render = new ResolvedRenderSettings(16, "monospace", default) with { FadeOut = 1.5 };
        var size = GifRender.OutputSize(RasterLayout.Create(16, 80, 24), Mp4Settings.Default with { MaxWidth = 410, MaxHeight = 1000 });
        return GifRender.ForGif(render).FadeOut == 1.5 && GifRender.ForGif(render with { Loop = false }).FadeOut is null
            && size.Width == 410 && size.Height < 488
            && GifRender.OutputSize(RasterLayout.Create(16, 80, 24), Mp4Settings.Default with { FrameWidth = 640, FrameHeight = 480 }) == (640, 480);
    }

    internal static bool FfmpegArgumentsUseH264Bt709()
    {
        var plain = string.Join(' ', Mp4Render.BuildArguments(820, 488, Mp4Settings.Default, 820, 488, "out.mp4"));
        var scaled = string.Join(' ', Mp4Render.BuildArguments(2020, 1208, Mp4Settings.Default with { Crf = 28, Fps = 60 }, 1280, 764, "out.mp4"));
        // 3860×2168 is 242×136 = 32,912 macroblocks: level 5.2's 184,320-macroblock buffer fits 5 references.
        var fourK = string.Join(' ', Mp4Render.BuildArguments(3860, 2168, Mp4Settings.Default, 3860, 2168, "out.mp4"));
        // Forced keyframes are half a frame early: ffmpeg forces the first frame at or after each time.
        var forced = string.Join(' ', Mp4Render.BuildArguments(80, 60, Mp4Settings.Default with { Fps = 10 }, 80, 60, "out.mp4", [12, 31, 61]));
        var noneForced = string.Join(' ', Mp4Render.BuildArguments(80, 60, Mp4Settings.Default, 80, 60, "out.mp4", []));
        return forced.Contains("-g 300 -force_key_frames 1.15,3.05,6.05 -colorspace", StringComparison.Ordinal)
            && !noneForced.Contains("-force_key_frames", StringComparison.Ordinal)
            && !plain.Contains("-force_key_frames", StringComparison.Ordinal)
            && plain.Contains("-f rawvideo -pix_fmt rgb24 -s 820x488 -r 30 -i -", StringComparison.Ordinal)
            && plain.Contains("-vf scale=out_color_matrix=bt709:out_range=tv,format=yuv420p", StringComparison.Ordinal)
            && plain.Contains("-c:v libx264 -preset slower -crf 23 -psy 0 -refs 8 -x264-params merange=128:scenecut=10 -g 900", StringComparison.Ordinal)
            && plain.Contains("-colorspace bt709 -color_primaries bt709 -color_trc bt709", StringComparison.Ordinal)
            && plain.Contains("-movflags +faststart -f mp4", StringComparison.Ordinal)
            && scaled.Contains("scale=1280:764:flags=lanczos:out_color_matrix=bt709", StringComparison.Ordinal)
            && scaled.Contains("-crf 28 -psy 0 -refs 8 -x264-params merange=128:scenecut=10 -g 1800", StringComparison.Ordinal)
            && fourK.Contains("-refs 5 ", StringComparison.Ordinal);
    }

    internal static bool FontFamilyListParsing() =>
        GlyphSource.ParseFamilyList("ui-monospace, \"Cascadia Mono\", 'Ubuntu Mono' ,monospace,")
            .SequenceEqual(["ui-monospace", "Cascadia Mono", "Ubuntu Mono", "monospace"]);

    internal static bool BlockElementsFillCellEdges()
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

    internal static bool BoxLinesReachCellEdges()
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

    internal static bool DoubleCornerJoinsInsideCell()
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

    internal static bool BrailleDrawsDots()
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

    internal static bool SyntheticBoldAndItalic()
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

    internal static bool FrameColorsMatchTheme()
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

    internal static bool CursorBlendsHalfForeground()
    {
        // Cursor visible at (0, 0) on a blank cell: theme fg #d0d0d0 at alpha 128 over bg #282c34.
        var (frames, layout) = Render("""[0.0,"o",""]""", fps: 10);
        static byte Blend(int bg, int fg) => (byte)(bg + (fg - bg) * 128 / 255);
        return CellPixel(frames[^1], layout, 0, 0) == (Blend(40, 208), Blend(44, 208), Blend(52, 208));
    }

    internal static bool NothingShownBeforeFirstEvent()
    {
        // Like the SVG: no cursor until the first event at 1.0s.
        var (frames, layout) = Render("""[1.0,"o","█"]""", fps: 10);
        return CellPixel(frames[0], layout, 0, 0) == (40, 44, 52)
            && CellPixel(frames[9], layout, 0, 0) == (40, 44, 52)
            && CellPixel(frames[10], layout, 0, 0) == (0xd0, 0xd0, 0xd0);
    }

    internal static bool FrameShowsEventsUpToItsTime()
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

    internal static bool EndHoldAndFadeOutEndOnTheEmptyTerminal()
    {
        // The last event is at 0.5s. end-hold 0.5 and fade-out 0.5 make the video 1.5s: 15 frames at 10fps. Frame k
        // stands for the time up to (k + 1) / 10, so the fade starts after frame 9 and frame 14 is fully faded: the
        // block cell has the theme's background, and the padding around the cells never changes.
        var (frames, layout) = Render(
            """
        [0.0,"o","\u001b[?25l█"]
        [0.5,"o","█"]
        """, fps: 10, tags: ["st:end-hold=0.5", "st:fade-out=0.5"]);
        if (frames.Count != 15)
        {
            Console.Error.WriteLine($"  {frames.Count} frames");
            return false;
        }

        var held = CellPixel(frames[9], layout, 0, 0);
        var fading = CellPixel(frames[12], layout, 0, 0);
        var faded = CellPixel(frames[14], layout, 0, 1);
        // Frame 12 is 60% of the way from the foreground #d0d0d0 to the background #282c34.
        var ok = held == (0xd0, 0xd0, 0xd0)
            && Math.Abs(fading.Item1 - 107.2) < 2 && Math.Abs(fading.Item2 - 109.6) < 2 && Math.Abs(fading.Item3 - 114.4) < 2
            && faded == (40, 44, 52)
            && Pixel(frames[14], layout, 0, 0) == Pixel(frames[0], layout, 0, 0);
        if (!ok)
            Console.Error.WriteLine($"  held {held}, fading {fading}, faded {faded}");
        return ok;
    }

    internal static bool TrailingBlankRestoreIsTrimmed()
    {
        // A full-screen app exits to a blank main screen. Like the SVG, the video ends on the app's last screen.
        var (frames, layout) = Render(
            """
        [0.0,"o","\u001b[?1049h\u001b[2J\u001b[H█\u001b[?25l"]
        [1.0,"o","\u001b[?1049l"]
        """, fps: 10);
        return CellPixel(frames[^1], layout, 0, 0) == (0xd0, 0xd0, 0xd0);
    }

    internal static bool ScreenSwitchesBecomeKeyframes()
    {
        // v3 times are intervals. At 10fps: the alternate screen opens blank at 1.02s (frame 11) and is drawn at
        // 1.15s (frame 12); leaving it at 3.04s shows the prompt again at once (frame 31). Opening and drawing at
        // 3.53s (frame 36) is less than a second after frame 31, so x264 decides that one. Another enter at 5.01s
        // is not a switch, because the screen is already in the alternate screen. Leaving at 6.02s is frame 61.
        var (plan, keyframes) = PlanFor(
            """
        [0.0,"o","$ "]
        [1.02,"o","\u001b[?1049h"]
        [0.13,"o","\u001b[H█"]
        [1.89,"o","\u001b[?1049l"]
        [0.49,"o","\u001b[?1049h\u001b[H█"]
        [1.48,"o","\u001b[?1049hX"]
        [1.01,"o","\u001b[?1049l"]
        """, fps: 10);
        return plan.StopIndex == 7
            && plan.ScreenSwitchFrames.SequenceEqual([12, 31, 36, 61])
            && keyframes.SequenceEqual([12, 31, 61]);
    }

    internal static bool TrimmedRestoreForcesNoKeyframe()
    {
        // The exit to a blank main screen is trimmed, so the frame that would show it is not forced either.
        // The switch into the alternate screen is in frame 0, which is a keyframe anyway.
        var (plan, keyframes) = PlanFor(
            """
        [0.0,"o","\u001b[?1049h\u001b[2J\u001b[H█\u001b[?25l"]
        [1.0,"o","\u001b[?1049l"]
        """, fps: 10);
        return plan.StopIndex == 1 && plan.ScreenSwitchFrames.Count == 0 && keyframes.Count == 0;
    }

    internal static bool ForcedKeyframesAreSpacedAndCapped() =>
        Mp4Render.ChooseKeyframes([5, 20, 34, 35, 64], 30).SequenceEqual([5, 35])
        && Mp4Render.ChooseKeyframes([.. Enumerable.Range(1, 1500).Select(i => i * 30)], 30) is { Count: Mp4Settings.MaxForcedKeyframes } capped
        && capped[^1] == Mp4Settings.MaxForcedKeyframes * 30;

    internal static (FixedRatePlan Plan, IReadOnlyList<int> Keyframes) PlanFor(string events, int fps)
    {
        var path = Path.Combine(Path.GetTempPath(), $"scenetake-mp4-{Guid.NewGuid():N}.cast");
        try
        {
            File.WriteAllText(path, "{\"version\":3,\"term\":{\"cols\":8,\"rows\":3,\"type\":\"xterm-256color\"}}\n" + events + "\n");
            var recording = CastReader.Read(path);
            var theme = TerminalTheme.FromResolved(recording.RenderSettings.Theme);
            var plan = TerminalReplay.PlanFixedRate(recording.Events, recording.Width, recording.Height, CanvasWindow.Whole(recording.Width, recording.Height),
                theme, fps, Mp4FrameRenderer.CountFrames(recording.LoopDuration, fps));
            return (plan, Mp4Render.ChooseKeyframes(plan.ScreenSwitchFrames, fps));
        }
        finally
        {
            File.Delete(path);
        }
    }

    internal static bool MatrixTintMatchesSvg()
    {
        // White next to green draws as bright green (palette 10 #7e8e50), as in the SVG.
        var (frames, layout) = Render("""[0.0,"o","\u001b[32m█\u001b[37m█\u001b[0m\u001b[?25l"]""", fps: 10);
        return CellPixel(frames[^1], layout, 0, 1) == (0x7e, 0x8e, 0x50);
    }

    internal static bool MacosChromeDrawsTrafficLights()
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

    internal static bool WindowsChromeDrawsTabRow()
    {
        // Dark theme → WinDark: the tab row #1c1c1c, the active tab from 8 to 128 px in the terminal's #282c34, and
        // the caption glyphs in #d6d6d6: the minimize line on row 17 around 115 px from the right, and the maximize
        // square's left side 73.5 px from the right.
        var layout = RasterLayout.Create(16, 60, 3, WindowStyle.Windows);
        var theme = TerminalTheme.FromResolved(new ResolvedTheme("#d0d0d0", "#282c34", RenderSettingsResolver.DefaultPalette));
        var raster = new FrameRasterizer(layout, theme, GlyphSource.Create("monospace", null, layout));
        (int R, int G, int B) At(int x, int y)
        {
            var offset = (y * layout.Width + x) * 3;
            return (raster.Pixels[offset], raster.Pixels[offset + 1], raster.Pixels[offset + 2]);
        }

        var ok = At(5, 5) == (0x1c, 0x1c, 0x1c)
            && At(layout.Width - 5, 30) == (0x1c, 0x1c, 0x1c)
            && At(7, 30) == (0x1c, 0x1c, 0x1c)
            && At(8, 30) == (0x28, 0x2c, 0x34)
            && At(60, 30) == (0x28, 0x2c, 0x34)
            && At(133, 30) == (0x1c, 0x1c, 0x1c)
            && At(layout.Width - 115, 17) == (0xd6, 0xd6, 0xd6)
            && At(layout.Width - 74, 17) == (0xd6, 0xd6, 0xd6);
        if (!ok)
            Console.Error.WriteLine($"  row {At(5, 5)} {At(layout.Width - 5, 30)} {At(7, 30)}, tab {At(8, 30)} {At(60, 30)}, after {At(133, 30)}, minimize {At(layout.Width - 115, 17)}, maximize {At(layout.Width - 74, 17)}");
        return ok;
    }

    internal static (List<byte[]> Frames, RasterLayout Layout) Render(string events, int fps, string? window = null, (int Width, int Height)? size = null, string[]? tags = null)
    {
        var path = Path.Combine(Path.GetTempPath(), $"scenetake-mp4-{Guid.NewGuid():N}.cast");
        try
        {
            // These tests check the drawing at the terminal's full size, so they turn off the default fit (canvas_fit_test
            // checks fit).
            string[] allTags = ["st:fit=none", .. window is null ? [] : new[] { $"st:window={window}" }, .. tags ?? []];
            var tagsJson = allTags.Length == 0 ? "" : $",\"tags\":[{string.Join(',', allTags.Select(static tag => $"\"{tag}\""))}]";
            File.WriteAllText(path, "{\"version\":3,\"term\":{\"cols\":8,\"rows\":3,\"type\":\"xterm-256color\"}" + tagsJson + "}\n" + events + "\n");
            var recording = CastReader.Read(path);
            var renderer = new Mp4FrameRenderer(recording.Events, recording.Width, recording.Height, recording.RenderSettings,
                Mp4Settings.Default with { Fps = fps, FrameWidth = size?.Width ?? 0, FrameHeight = size?.Height ?? 0 },
                RenderSettingsResolver.ResolveLoopDuration(recording.LoopDuration, recording.Events, recording.RenderSettings));
            var frames = new List<byte[]>();
            renderer.Render(pixels => frames.Add((byte[])pixels.Clone()));
            return (frames, renderer.Layout);
        }
        finally
        {
            File.Delete(path);
        }
    }

    internal static (byte, byte, byte) Pixel(byte[] frame, RasterLayout layout, int x, int y)
    {
        var offset = (y * layout.Width + x) * 3;
        return (frame[offset], frame[offset + 1], frame[offset + 2]);
    }

    internal static (byte, byte, byte) CellPixel(byte[] frame, RasterLayout layout, int row, int col) =>
        Pixel(frame, layout, layout.CellX(col) + layout.CellWidth / 2, layout.RowY(row) + layout.CellHeight / 2);

    internal static bool WindowShapesCoverWhatSamplingCovers()
    {
        // A rounded rectangle tells at once that a pixel is wholly inside or outside it. Each count must still be the
        // one its 16 sample points give, tested as the window's shapes were tested before (Inside below): for edges on
        // whole, half, and other pixels, each choice of rounded corners, radii from none to half the box, and with a
        // hole, as a border or a stroke has. A radius over half the box, or a hole with no size, is none the window
        // draws: the corner test throws for it, as it did before.
        var random = new Random(7);
        double[] starts = [0, 0.5, 3, 2.3, 10.75];
        var compared = 0;
        foreach (var x in starts)
        foreach (var width in new double[] { 5, 7.5, 20, 33.4 })
        foreach (var radius in new double[] { 0, 0.5, 2, 2.5, 3.75, -1 })
        foreach (var (roundTop, roundBottom) in new[] { (true, true), (true, false), (false, true), (false, false) })
        {
            if (radius > width / 2)
                continue;

            var y = starts[random.Next(starts.Length)];
            var height = width * (1 + random.NextDouble());
            var box = new FrameRasterizer.RoundedBox(x, y, width, height, radius, roundTop, roundBottom);
            bool InBox(double px, double py) => Inside(x, y, width, height, radius, roundTop, roundBottom, px, py);
            for (var py = (int)Math.Floor(y) - 1; py <= (int)Math.Ceiling(y + height) + 1; py++)
            {
                for (var px = (int)Math.Floor(x) - 1; px <= (int)Math.Ceiling(x + width) + 1; px++)
                {
                    compared++;
                    if (FrameRasterizer.Coverage(px, py, box, default, hasHole: false) != Sampled(px, py, InBox))
                    {
                        Console.Error.WriteLine($"  box {x},{y} {width}x{height} r{radius} {roundTop}/{roundBottom}: pixel {px},{py}");
                        return false;
                    }

                    // Inset by 1 as a window's border, by 0.5 as a stroke, and by 2 off center.
                    foreach (var (inset, shift) in new[] { (1.0, 0.0), (0.5, 0.0), (2.0, 0.3) })
                    {
                        var (hx, hy, hw, hh, hr) = (x + inset + shift, y + inset, width - 2 * inset, height - 2 * inset, radius - inset);
                        var hole = new FrameRasterizer.RoundedBox(hx, hy, hw, hh, hr, roundTop, roundBottom);
                        var sampled = Sampled(px, py, (sx, sy) => InBox(sx, sy) && !Inside(hx, hy, hw, hh, hr, roundTop, roundBottom, sx, sy));
                        if (FrameRasterizer.Coverage(px, py, box, hole, hasHole: true) != sampled)
                        {
                            Console.Error.WriteLine($"  box {x},{y} {width}x{height} r{radius} {roundTop}/{roundBottom}, hole inset {inset}: pixel {px},{py}");
                            return false;
                        }
                    }
                }
            }
        }

        return compared > 10000;
    }

    // How the window's shapes were tested before RoundedBox: a point test for each of the 4×4 sample points of a pixel.
    private static bool Inside(double left, double top, double width, double height, double radius, bool roundTop, bool roundBottom, double px, double py)
    {
        double right = left + width, bottom = top + height, r = Math.Max(radius, 0);
        if (px < left || px >= right || py < top || py >= bottom)
            return false;

        var nearTop = roundTop && py < top + r;
        var nearBottom = roundBottom && py > bottom - r;
        if (!nearTop && !nearBottom)
            return true;

        var cx = Math.Clamp(px, left + r, right - r);
        var cy = nearTop ? top + r : bottom - r;
        return (px - cx) * (px - cx) + (py - cy) * (py - cy) <= r * r;
    }

    private static int Sampled(int x, int y, Func<double, double, bool> inside)
    {
        var hits = 0;
        for (var sy = 0; sy < 4; sy++)
        {
            for (var sx = 0; sx < 4; sx++)
            {
                if (inside(x + (sx + 0.5) / 4, y + (sy + 0.5) / 4))
                    hits++;
            }
        }

        return hits;
    }

    internal static bool ShadowMatchesTheBlurOfEveryRow()
    {
        // The pass across leaves out the rows beside the middle of the window, which the pass down never reads. The
        // shadow must be the one the pass across over every row gives (ShadowOverEveryRow): for a window tall enough to
        // have such rows, one too short to have them, one with square corners, and one against the frame's edges.
        (int FrameWidth, int FrameHeight, int X, int Y, int Width, int Height, int Radius)[] windows =
        [
            (320, 420, 30, 30, 260, 340, 10),
            (220, 130, 20, 15, 180, 90, 10),
            (300, 300, 25, 25, 250, 250, 0),
            (200, 240, 0, 0, 200, 230, 8),
        ];
        foreach (var (frameWidth, frameHeight, x, y, width, height, radius) in windows)
        {
            var background = new byte[frameWidth * frameHeight * 3];
            for (var i = 0; i < background.Length; i++)
                background[i] = (byte)(i * 7 % 251);
            var drawn = (byte[])background.Clone();
            var expected = (byte[])background.Clone();
            FrameRasterizer.DrawWindowShadow(drawn, frameWidth, frameHeight, x, y, width, height, radius);
            ShadowOverEveryRow(expected, frameWidth, frameHeight, x, y, width, height, radius);
            if (!drawn.AsSpan().SequenceEqual(expected) || drawn.AsSpan().SequenceEqual(background))
            {
                Console.Error.WriteLine($"  window {x},{y} {width}x{height} r{radius} in {frameWidth}x{frameHeight}");
                return false;
            }
        }

        return true;
    }

    // The shadow as it was drawn before the pass across left rows out.
    private static void ShadowOverEveryRow(byte[] pixels, int frameWidth, int frameHeight, int x, int y, int width, int height, int radius)
    {
        var offset = (int)Math.Round(SvgFrameRenderer.WindowShadowOffsetY);
        var kernel = FrameRasterizer.GaussianKernel(SvgFrameRenderer.WindowShadowDeviation);
        var reach = kernel.Length / 2;
        var left = x - reach;
        var top = y + offset - reach;
        var areaWidth = width + reach * 2;
        var areaHeight = height + reach * 2;
        var shape = new FrameRasterizer.RoundedBox(x, y + offset, width, height, radius, roundTop: true, roundBottom: true);
        var coverage = new float[areaWidth * areaHeight];
        for (var row = reach; row < reach + height; row++)
        {
            for (var col = reach; col < reach + width; col++)
                coverage[row * areaWidth + col] = FrameRasterizer.ShapeCoverage(left + col, top + row, x, y + offset, width, height, radius, shape);
        }

        var across = new float[coverage.Length];
        for (var row = reach; row < reach + height; row++)
        {
            for (var col = 0; col < areaWidth; col++)
            {
                var sum = 0f;
                for (var k = 0; k < kernel.Length; k++)
                {
                    var source = col + k - reach;
                    if ((uint)source < (uint)areaWidth)
                        sum += coverage[row * areaWidth + source] * kernel[k];
                }

                across[row * areaWidth + col] = sum;
            }
        }

        var window = new FrameRasterizer.RoundedBox(x, y, width, height, radius, roundTop: true, roundBottom: true);
        var opacity = (float)SvgFrameRenderer.WindowShadowOpacity;
        for (var row = 0; row < areaHeight; row++)
        {
            var frameY = top + row;
            if ((uint)frameY >= (uint)frameHeight)
                continue;

            for (var col = 0; col < areaWidth; col++)
            {
                var frameX = left + col;
                if ((uint)frameX >= (uint)frameWidth)
                    continue;

                var inside = FrameRasterizer.ShapeCoverage(frameX, frameY, x, y, width, height, radius, window);
                if (inside >= 1)
                    continue;

                var sum = 0f;
                for (var k = 0; k < kernel.Length; k++)
                {
                    var source = row + k - reach;
                    if ((uint)source < (uint)areaHeight)
                        sum += across[source * areaWidth + col] * kernel[k];
                }

                var shade = opacity * sum * (1 - inside);
                if (shade <= 0)
                    continue;

                var offsetPixel = (frameY * frameWidth + frameX) * 3;
                for (var c = 0; c < 3; c++)
                    pixels[offsetPixel + c] = (byte)MathF.Round(pixels[offsetPixel + c] * (1 - shade));
            }
        }
    }

    internal static bool FramesDrawnInTurnMatchFramesDrawnAlone()
    {
        // A rasterizer draws only the rows that changed since its last frame. Each frame must be the one a new rasterizer
        // draws from nothing: when another row changes under a cursor that stays, when the cursor moves along its row,
        // when it moves to another row, and when it hides.
        var layout = RasterLayout.Create(16, 12, 4);
        var theme = TerminalTheme.FromResolved(new ResolvedTheme("#d0d0d0", "#282c34", RenderSettingsResolver.DefaultPalette));
        var glyphs = GlyphSource.Create("monospace", null, layout);
        var screen = new ScreenBuffer(12, 4, theme);
        var parser = new AnsiParser(screen, theme);
        var inTurn = new FrameRasterizer(layout, theme, glyphs);
        string[] changes = ["ab\r\ncd\r\nef\u001b[2;2H", "\u001b[4;1Hgh\u001b[2;2H", "\u001b[2;4H", "\u001b[3;1H", "\u001b[?25l"];
        foreach (var change in changes)
        {
            parser.Process(change);
            inTurn.Draw(screen);
            var alone = new FrameRasterizer(layout, theme, glyphs);
            alone.Draw(screen);
            if (!inTurn.Pixels.AsSpan().SequenceEqual(alone.Pixels))
            {
                Console.Error.WriteLine($"  differs after {change.Replace("\u001b", "ESC", StringComparison.Ordinal)}");
                return false;
            }
        }

        return true;
    }

    internal static bool SoftGlyphScalesByCoverage()
    {
        // A 12×24 matrix in a 10×20 cell (16px). Columns 0–5 set: output columns 0–4 cover glyph x 0–6 fully,
        // columns 5–9 nothing. One set pixel in a 2×2 matrix fills the cell's top-left quarter.
        var layout = RasterLayout.Create(16, 1, 1);
        var halfRows = Enumerable.Repeat(0x3FUL, 24).ToArray();
        var half = CellGlyphs.DrawSoft(SoftGlyph.Intern(12, 24, halfRows), layout);
        var quarter = CellGlyphs.DrawSoft(SoftGlyph.Intern(2, 2, [1UL, 0UL]), layout);
        int At(RasterGlyph g, int x, int y) => g.Alpha[y * g.Width + x];
        var w = layout.CellWidth;
        var h = layout.CellHeight;
        return half.Width == w && half.Height == h && half.OffsetX == 0 && half.OffsetY == -layout.Baseline
            && Enumerable.Range(0, h).All(y => At(half, 4, y) == 255 && At(half, 5, y) == 0)
            && At(quarter, w / 2 - 1, h / 2 - 1) == 255 && At(quarter, w / 2, h / 2 - 1) == 0 && At(quarter, w / 2 - 1, h / 2) == 0;
    }

    internal static bool SoftGlyphFrameUsesCellColor()
    {
        // A soft font glyph with every pixel set fills its cell in the cell's color: red (#ac4142) in the dark theme.
        // The in-box ConPTY form: the font in G1, its character between SO and SI. The space after it is background.
        var full = new string('~', 5) + "/" + new string('~', 5);
        var (frames, layout) = Render(
            "[0.0,\"o\",\"\\u001bP1;1;0;5;0;2;12;0{@" + full + "\\u001b\\\\\\u001b)@\\u001b[31m\\u000e!\\u000f \\u001b[0m\\u001b[?25l\"]", fps: 10);
        var last = frames[^1];
        return CellPixel(last, layout, 0, 0) == (0xac, 0x41, 0x42)
            && Pixel(last, layout, layout.CellX(0), layout.RowY(0)) == (0xac, 0x41, 0x42)
            && CellPixel(last, layout, 0, 1) == (40, 44, 52);
    }

    internal static bool BoxDrawingAtTinySizesStaysInCell()
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

    internal static bool HugeFrameIsReportedClearly()
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

    internal static bool X264SideLimitIsChecked()
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

    internal static bool Libx264CheckRunsOncePerFfmpeg()
    {
        // Each mp4 output checks ffmpeg before anything runs. One ffmpeg is asked for its encoders once, and again
        // when the file changes. The fake notes each run in runs.txt.
        var directory = Path.Combine(Path.GetTempPath(), $"scenetake-libx264-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, OperatingSystem.IsWindows() ? "ffmpeg.cmd" : "ffmpeg");
            var runs = Path.Combine(directory, "runs.txt");
            void WriteFake(string encoders)
            {
                if (OperatingSystem.IsWindows())
                {
                    File.WriteAllText(path, $"@echo x>>\"%~dp0runs.txt\"\r\n@echo {encoders}\r\n");
                }
                else
                {
                    File.WriteAllText(path, $"#!/bin/sh\necho x >> \"$(dirname \"$0\")/runs.txt\"\necho \"{encoders}\"\n");
                    File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
                }
            }

            int Runs() => File.Exists(runs) ? File.ReadAllLines(runs).Length : 0;

            WriteFake(" V....D libx264              libx264 H.264");
            var first = Ffmpeg.HasLibx264(path, out var firstError);
            var second = Ffmpeg.HasLibx264(path, out _);
            var ok = CanvasFitCases.Expect(first && second, $"libx264 not found: {firstError}") & CanvasFitCases.Expect(Runs() == 1, $"ffmpeg ran {Runs()} times for two checks");

            WriteFake(" V....D libx265 H.265");
            var changed = Ffmpeg.HasLibx264(path, out var changedError);
            return ok & CanvasFitCases.Expect(!changed && changedError.Contains("no libx264", StringComparison.Ordinal), $"a changed ffmpeg was not checked again: {changedError}")
                & CanvasFitCases.Expect(Runs() == 2, $"ffmpeg ran {Runs()} times after it changed");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    internal static bool FfmpegProbeTimesOut()
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

    internal static bool OutputPathIsCheckedFirst()
    {
        // A missing output folder or a directory in the output's place is reported by name before any
        // frame is drawn. The ffmpeg path does not exist, so reaching ffmpeg would fail differently.
        var recording = CastReader.Read(TestEnv.RepoPath("tests/marker-delay.cast"));
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

    internal static bool FontTableOutsideFileIsRejected()
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

    internal static bool SymbolOnlyFontIsRejected()
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

    internal static bool UnreadableFontFileIsReported()
    {
        // A --font-file without read permission must give the font error, not an unhandled exception.
        if (OperatingSystem.IsWindows())
            throw new TUnit.Core.Exceptions.SkipTestException("needs Unix file modes");

        var path = CopyInstalledTrueTypeFont(_ => { });
        try
        {
            File.SetUnixFileMode(path, UnixFileMode.None);
            try
            {
                using (File.OpenRead(path))
                    throw new TUnit.Core.Exceptions.SkipTestException("running as a user who can read any file");
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

    internal static bool FontFileThatCannotLoadIsAnError()
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

    internal static bool ShortFontTableIsRejected()
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

    internal static bool OutOfRangeGlyphIdsAreMissing()
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

    internal static bool EveryFontFileFaceMustLoad()
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
                throw new TUnit.Core.Exceptions.SkipTestException("the installed font cannot make a regular and bold collection");
            return FontFileError(path).Contains(path, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    internal static bool FontFileIsNotHeldOpen()
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

    internal static bool FontFileIsReleasedAfterRender()
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

    internal static bool LogNamesAFontThatLoads()
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
                throw new TUnit.Core.Exceptions.SkipTestException("the copied font's own family does not load");
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

    internal static void TryDeleteFile(string path)
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
    internal static string WriteFrameReadingFfmpeg(string directory)
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

    internal static bool LinkedFontFileLoads()
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
                throw new TUnit.Core.Exceptions.SkipTestException("this machine does not allow symbolic links");
            }

            return FontFileError(link) == "";
        }
        finally
        {
            File.Delete(link);
            File.Delete(font);
        }
    }

    internal static bool InstalledFontsCanBeRescanned()
    {
        // pre may install a font, so the render scans again after pre. A rescan must not reuse the earlier scan.
        var first = FontCatalog.Installed;
        FontCatalog.Rescan();
        return !ReferenceEquals(first, FontCatalog.Installed) && FontCatalog.Installed.Count == first.Count;
    }

    internal static unsafe bool DefaultFontsPassTableChecks()
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
    internal static string FontFileError(string path)
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
    internal static string CopyInstalledTrueTypeFont(Action<byte[]> patch) =>
        RewriteInstalledTrueTypeFont(data =>
        {
            patch(data);
            return data;
        });

    /// <summary>Copies an installed single-face TrueType font to a temporary file, replaced by what <paramref name="patch"/> returns.</summary>
    internal static string RewriteInstalledTrueTypeFont(Func<byte[], byte[]> patch)
    {
        var data = patch(File.ReadAllBytes(InstalledSingleFaceTrueType()));
        var path = Path.Combine(Path.GetTempPath(), $"scenetake-font-{Guid.NewGuid():N}.ttf");
        File.WriteAllBytes(path, data);
        return path;
    }

    internal static string InstalledSingleFaceTrueType(bool nonVariable = false) =>
        FontCatalog.Installed
            .Where(face => !nonVariable || !face.Variable)
            .Select(face => face.Path)
            .FirstOrDefault(p => p.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase) && FontCatalog.ReadFaces(p).Count == 1)
        ?? throw new FontNotFoundException("no installed single-face TrueType font to copy");

    internal static int TableOffset(byte[] data, string tag) =>
        (int)System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(TableRecord(data, tag) + 8));

    internal static void WriteUInt16(byte[] data, int at, ushort value) =>
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(at), value);

    /// <summary>Appends a format 12 cmap subtable that maps <paramref name="codepoint"/> to <paramref name="glyph"/>, and points every Unicode encoding record at it.</summary>
    internal static byte[] MapToGlyph(byte[] data, int codepoint, uint glyph)
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
    internal static byte[] Collection(byte[] first, byte[] second)
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

    internal static int TableRecord(byte[] data, string tag)
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

    internal static void PatchTableOffset(byte[] data, string tag, uint offset) =>
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(TableRecord(data, tag) + 8), offset);

    internal static void PatchTableLength(byte[] data, string tag, uint length) =>
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(TableRecord(data, tag) + 12), length);

    internal static void PatchTableTag(byte[] data, string tag, string newTag) =>
        System.Text.Encoding.ASCII.GetBytes(newTag).CopyTo(data, TableRecord(data, tag));

    // Every cmap encoding record becomes Windows platform 3, encoding 0 (symbol), as in Symbol.ttf.
    internal static void PatchCmapToSymbol(byte[] data)
    {
        var cmap = (int)System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(TableRecord(data, "cmap") + 8));
        var count = System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(cmap + 2));
        for (var i = 0; i < count; i++)
        {
            System.Buffers.Binary.BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(cmap + 4 + i * 8), 3);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(cmap + 6 + i * 8), 0);
        }
    }

}
