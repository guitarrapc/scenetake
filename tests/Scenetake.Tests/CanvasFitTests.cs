using Scenetake.TestSupport;

namespace Scenetake.Tests;

// render.fit and render.crop-* (.github/docs/spec_svg.md → Fit and crop): the part of the terminal that a render
// draws.

public sealed class CanvasFitTests
{
    [Test]
    public async Task FitMovesEdgesToContentAndCursor() => await Assert.That(CanvasFitCases.FitMovesEdgesToContentAndCursor()).IsTrue();

    [Test]
    public async Task FitCoversEveryScreen() => await Assert.That(CanvasFitCases.FitCoversEveryScreen()).IsTrue();

    [Test]
    public async Task StillFitLooksAtItsScreenOnly() => await Assert.That(CanvasFitCases.StillFitLooksAtItsScreenOnly()).IsTrue();

    [Test]
    public async Task HiddenCursorIsNotContent() => await Assert.That(CanvasFitCases.HiddenCursorIsNotContent()).IsTrue();

    [Test]
    public async Task ColoredBlankIsContent() => await Assert.That(CanvasFitCases.ColoredBlankIsContent()).IsTrue();

    [Test]
    public async Task CropShiftsCellsAndCursor() => await Assert.That(CanvasFitCases.CropShiftsCellsAndCursor()).IsTrue();

    [Test]
    public async Task CropThenFit() => await Assert.That(CanvasFitCases.CropThenFit()).IsTrue();

    [Test]
    public async Task WideCharacterCutByCropIsBlank() => await Assert.That(CanvasFitCases.WideCharacterCutByCropIsBlank()).IsTrue();

    [Test]
    public async Task CropLeavingNothingIsAnError() => await Assert.That(CanvasFitCases.CropLeavingNothingIsAnError()).IsTrue();

    [Test]
    public async Task FittedSvgEqualsTheSmallTerminal() => await Assert.That(CanvasFitCases.FittedSvgEqualsTheSmallTerminal()).IsTrue();

    [Test]
    public async Task CroppedSvgEqualsTheSmallTerminal() => await Assert.That(CanvasFitCases.CroppedSvgEqualsTheSmallTerminal()).IsTrue();

    [Test]
    public async Task Mp4SizeFollowsFit() => await Assert.That(CanvasFitCases.Mp4SizeFollowsFit()).IsTrue();

    [Test]
    public async Task ScenarioKeysResolve() => await Assert.That(CanvasFitCases.ScenarioKeysResolve()).IsTrue();

    [Test]
    public async Task OverridesReplaceEachEdge() => await Assert.That(CanvasFitCases.OverridesReplaceEachEdge()).IsTrue();

    [Test]
    public async Task CastTagsRoundTrip() => await Assert.That(CanvasFitCases.CastTagsRoundTrip()).IsTrue();
}

internal static class CanvasFitCases
{

    internal static bool Expect(bool condition, string message)
    {
        if (!condition)
            Console.Error.WriteLine($"  {message}");
        return condition;
    }

    internal static ResolvedTheme Dark() => new(RenderSettingsResolver.DefaultFg, RenderSettingsResolver.DefaultBg, RenderSettingsResolver.DefaultPalette);

    internal static TerminalTheme Theme() => TerminalTheme.FromResolved(Dark());

    internal static List<CastEvent> Events(params string[] outputs) =>
        [.. outputs.Select((text, i) => CastEvent.Output(0.1 * (i + 1), text))];

    internal static string Window(CanvasWindow canvas) => $"{canvas.Left},{canvas.Top},{canvas.Width}x{canvas.Height}";

    internal static bool SameWindow(string label, string expected, CanvasWindow actual) =>
        Expect(expected == Window(actual), $"{label}: expected {expected}, got {Window(actual)}");

    internal static CanvasWindow Resolve(List<CastEvent> events, int width, int height, FitMode fit, CropSpec crop = default, double? stillAt = null) =>
        TerminalReplay.ResolveCanvas(events, width, height, crop, fit, Theme(), stillAt);

    internal static bool FitMovesEdgesToContentAndCursor()
    {
        // "abc" on row 0, "de" on row 1, and the cursor after "e" at column 2.
        var events = Events("abc\r\nde");
        return SameWindow("both", "0,0,3x2", Resolve(events, 20, 10, FitMode.Both))
            & SameWindow("height", "0,0,20x2", Resolve(events, 20, 10, FitMode.Height))
            & SameWindow("width", "0,0,3x10", Resolve(events, 20, 10, FitMode.Width))
            & SameWindow("none", "0,0,20x10", Resolve(events, 20, 10, FitMode.None))
            // The cursor after the longest line takes one more column.
            & SameWindow("cursor", "0,0,6x1", Resolve(Events("abcde"), 20, 10, FitMode.Both));
    }

    internal static bool FitCoversEveryScreen()
    {
        // A long line, then a cleared screen with one character: the long line was shown, so its columns stay.
        var events = Events("a long line\r\n", "\u001b[2J\u001b[Hx");
        return SameWindow("every screen", "0,0,11x2", Resolve(events, 20, 10, FitMode.Both));
    }

    internal static bool StillFitLooksAtItsScreenOnly()
    {
        var events = Events("a long line\r\n", "\u001b[2J\u001b[Hx");
        return SameWindow("final screen", "0,0,2x1", Resolve(events, 20, 10, FitMode.Both, stillAt: double.PositiveInfinity))
            & SameWindow("first screen", "0,0,11x2", Resolve(events, 20, 10, FitMode.Both, stillAt: 0.15));
    }

    internal static bool HiddenCursorIsNotContent() =>
        SameWindow("hidden cursor", "0,0,3x1", Resolve(Events("abc\r\n\u001b[?25l"), 20, 10, FitMode.Both));

    internal static bool ColoredBlankIsContent() =>
        SameWindow("red spaces", "0,0,4x1", Resolve(Events("ab\u001b[41m  \u001b[0m\r\u001b[?25l"), 20, 10, FitMode.Both));

    internal static bool CropShiftsCellsAndCursor()
    {
        var theme = Theme();
        var buffer = new ScreenBuffer(10, 5, theme);
        new AnsiParser(buffer, theme).Process("0123456789\r\nabcdefghij\r\nxy");
        // Rows 1 to 4 and columns 2 to 9: "cdefghij" on the first row, and the cursor after "xy" at column 0 of the second.
        var view = TerminalReplay.ViewForCanvas(buffer, new CanvasWindow(2, 1, 8, 4));
        var hidden = TerminalReplay.ViewForCanvas(buffer, new CanvasWindow(3, 0, 7, 5));
        return Expect(view.Width == 8 && view.Height == 4, $"window size {view.Width}x{view.Height}")
            & Expect(view.GetCell(0, 0).Text == "c" && view.GetCell(0, 7).Text == "j" && view.GetCell(1, 0).Text == " ",
                $"cells: '{view.GetCell(0, 0).Text}' '{view.GetCell(0, 7).Text}' '{view.GetCell(1, 0).Text}'")
            & Expect(view.CursorVisible && view.CursorRow == 1 && view.CursorCol == 0, $"cursor at {view.CursorRow},{view.CursorCol} visible {view.CursorVisible}")
            & Expect(!hidden.CursorVisible, "a cursor left of the window is hidden");
    }

    internal static bool CropThenFit()
    {
        // crop-left 2 cuts "ab", and fit then ends after "cdef" and the cursor after it.
        var events = Events("abcdef");
        return SameWindow("crop then fit", "2,0,5x1", Resolve(events, 20, 10, FitMode.Both, new CropSpec(0, 0, 2, 0)))
            & SameWindow("crop only", "2,1,17x7", Resolve(events, 20, 10, FitMode.None, new CropSpec(1, 2, 2, 1)));
    }

    internal static bool WideCharacterCutByCropIsBlank()
    {
        var theme = Theme();
        var buffer = new ScreenBuffer(10, 2, theme);
        new AnsiParser(buffer, theme).Process("あいう");
        var left = TerminalReplay.ViewForCanvas(buffer, new CanvasWindow(1, 0, 5, 2));
        var right = TerminalReplay.ViewForCanvas(buffer, new CanvasWindow(0, 0, 3, 2));
        return Expect(left.GetCell(0, 0).Text == " " && !left.GetCell(0, 0).IsWideContinuation, "the half cut at the left is blank")
            & Expect(left.GetCell(0, 1).Text == "い", $"the next character stays: '{left.GetCell(0, 1).Text}'")
            & Expect(right.GetCell(0, 2).Text == " " && !right.GetCell(0, 2).IsWide, "the half cut at the right is blank");
    }

    internal static bool CropLeavingNothingIsAnError()
    {
        var rows = TerminalReplay.CheckCrop(new CropSpec(5, 5, 0, 0), 20, 10);
        var cols = TerminalReplay.CheckCrop(new CropSpec(0, 0, 15, 5), 20, 10);
        var thrown = false;
        try
        {
            Resolve(Events("x"), 20, 10, FitMode.None, new CropSpec(10, 0, 0, 0));
        }
        catch (ArgumentException)
        {
            thrown = true;
        }

        return Expect(rows == "crop-top 5 and crop-bottom 5 leave no row of the 10-row terminal", $"rows: {rows}")
            & Expect(cols == "crop-left 15 and crop-right 5 leave no column of the 20-column terminal", $"cols: {cols}")
            & Expect(TerminalReplay.CheckCrop(new CropSpec(4, 5, 9, 10), 20, 10) is null, "one row and one column left is fine")
            & Expect(thrown, "ResolveCanvas throws for a crop that leaves nothing");
    }

    internal static ResolvedRenderSettings Render(FitMode fit = FitMode.None, CropSpec crop = default) =>
        new ResolvedRenderSettings(16, RenderSettingsResolver.DefaultFontFamily, Dark()) with { Fit = fit, Crop = crop };

    internal static bool FittedSvgEqualsTheSmallTerminal()
    {
        // Fit draws exactly what a terminal of the fitted size would show: the same cells, cursor, and viewport.
        var events = Events("abc\r\n", "de");
        var fitted = SvgRender.RenderToString(events, 20, 10, Render(FitMode.Both), 1);
        var small = SvgRender.RenderToString(events, 3, 2, Render(), 1);
        var measured = SvgRender.Measure(events, 20, 10, Render(FitMode.Both));
        return Expect(fitted == small, "the fitted SVG differs from the SVG of a 3x2 terminal")
            & Expect(measured == SvgRender.Measure(events, 3, 2, Render()), $"Measure: {measured}");
    }

    internal static bool CroppedSvgEqualsTheSmallTerminal()
    {
        // Cutting the right and bottom edges of a terminal whose text stays inside them is a smaller terminal.
        var events = Events("abc\r\n", "de");
        var cropped = SvgRender.RenderToString(events, 20, 10, Render(crop: new CropSpec(0, 8, 0, 17)), 1);
        var small = SvgRender.RenderToString(events, 3, 2, Render(), 1);
        return Expect(cropped == small, "the cropped SVG differs from the SVG of a 3x2 terminal");
    }

    internal static bool Mp4SizeFollowsFit()
    {
        var events = Events("abc\r\n", "de");
        var settings = new Mp4Settings(30, 23, 0, 0, 0, 0, null);
        var fitted = Mp4Render.Measure(events, 20, 10, Render(FitMode.Both), settings);
        var small = Mp4Render.Measure(events, 3, 2, Render(), settings);
        var whole = Mp4Render.Measure(events, 20, 10, Render(), settings);
        return Expect(fitted == small, $"fitted {fitted} vs small {small}") & Expect(whole != small, "the whole terminal is larger");
    }

    internal static bool ScenarioKeysResolve()
    {
        var ok = RenderSettingsResolver.TryResolve(
            new Scenario { Render = new ScenarioRender { Fit = "Height", CropTop = 1, CropRight = 3 } }, null, out var settings, out var error);
        var badFit = RenderSettingsResolver.TryResolve(new Scenario { Render = new ScenarioRender { Fit = "wide" } }, null, out _, out var fitError);
        var badCrop = RenderSettingsResolver.TryResolve(new Scenario { Render = new ScenarioRender { CropLeft = 512 } }, null, out _, out var cropError);
        RenderSettingsResolver.TryResolve(new Scenario(), null, out var unset, out _);
        RenderSettingsResolver.TryResolve(new Scenario { Render = new ScenarioRender { Fit = "none" } }, null, out var none, out _);
        return Expect(ok && settings.Fit == FitMode.Height && settings.Crop == new CropSpec(1, 0, 0, 3), $"resolved: {error} {settings.Fit} {settings.Crop}")
            & Expect(unset.Fit == FitMode.Height && none.Fit == FitMode.None, $"default height, none kept: {unset.Fit} {none.Fit}")
            & Expect(!badFit && fitError == "invalid render.fit: unknown fit: wide (expected: none, height, width, both)", $"fit error: {fitError}")
            & Expect(!badCrop && cropError == "invalid render.crop-left: must be between 0 and 511 cells: 512", $"crop error: {cropError}");
    }

    internal static bool OverridesReplaceEachEdge()
    {
        var settings = Render(FitMode.Both, new CropSpec(1, 1, 1, 1));
        var applied = new RenderStyleOverrides(null, null, null, null, Fit: FitMode.Width, CropTop: 2, CropRight: 0).Apply(settings);
        var none = RenderStyleOverrides.None.Apply(settings);
        return Expect(applied.Fit == FitMode.Width && applied.Crop == new CropSpec(2, 1, 1, 0), $"applied: {applied.Fit} {applied.Crop}")
            & Expect(none.Fit == FitMode.Both && none.Crop == new CropSpec(1, 1, 1, 1), $"none: {none.Fit} {none.Crop}");
    }

    internal static bool CastTagsRoundTrip()
    {
        var tagged = ReadHeader("\"st:fit=both\",\"st:crop-top=2\",\"st:crop-bottom=1\",\"st:crop-left=3\",\"st:crop-right=4\"");
        var none = ReadHeader("\"st:fit=none\"");
        var invalid = ReadHeader("\"st:fit=wide\",\"st:crop-top=-1\",\"st:crop-left=x\"");
        var plain = ReadHeader("");
        return Expect(tagged.Settings.Fit == FitMode.Both && tagged.Settings.Crop == new CropSpec(2, 1, 3, 4), $"tagged: {tagged.Settings.Fit} {tagged.Settings.Crop}")
            & Expect(none.Settings.Fit == FitMode.None, $"st:fit=none: {none.Settings.Fit}")
            & Expect(invalid.Settings.Fit == FitMode.Height && invalid.Settings.Crop.IsEmpty, $"invalid: {invalid.Settings.Fit} {invalid.Settings.Crop}")
            & Expect(invalid.Warnings.Contains("Warning: cast: invalid fit 'wide', using the default fit (height)", StringComparison.Ordinal)
                && invalid.Warnings.Contains("Warning: cast: invalid crop-top '-1', using no crop", StringComparison.Ordinal)
                && invalid.Warnings.Contains("Warning: cast: invalid crop-left 'x', using no crop", StringComparison.Ordinal), $"warnings: {invalid.Warnings}")
            & Expect(plain.Settings.Fit == FitMode.Height && plain.Settings.Crop.IsEmpty && plain.Warnings.Length == 0, "no tags: the default fit (height) and no crop");

        static (ResolvedRenderSettings Settings, string Warnings) ReadHeader(string tags)
        {
            var path = Path.Combine(Path.GetTempPath(), $"scenetake-fit-{Guid.NewGuid():N}.cast");
            var original = Console.Error;
            using var captured = new StringWriter();
            // The reader writes its warnings to stderr. Tests run one at a time, so no other test writes while it is swapped.
#pragma warning disable TUnit0055
            try
            {
                File.WriteAllText(path, "{\"version\":3,\"term\":{\"cols\":20,\"rows\":10},\"tags\":[" + tags + "]}\n[0.1,\"o\",\"x\"]\n");
                Console.SetError(captured);
                return (CastReader.Read(path).RenderSettings, captured.ToString());
            }
            finally
            {
                Console.SetError(original);
                File.Delete(path);
            }
#pragma warning restore TUnit0055
        }
    }
}
