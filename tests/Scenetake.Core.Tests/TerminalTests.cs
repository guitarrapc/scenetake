using System.Text;
using Scenetake.TestSupport;

namespace Scenetake.Core.Tests;

public sealed class TerminalTests
{
    [Test]
    public async Task SgrColorAndReset() => await Assert.That(TerminalCases.SgrColorAndReset()).IsTrue();

    [Test]
    public async Task MoveCursorAndOverwrite() => await Assert.That(TerminalCases.MoveCursorAndOverwrite()).IsTrue();

    [Test]
    public async Task CjkWideCharacter() => await Assert.That(TerminalCases.CjkWideCharacter()).IsTrue();

    [Test]
    public async Task TrueColorForeground() => await Assert.That(TerminalCases.TrueColorForeground()).IsTrue();

    [Test]
    public async Task ColonDelimitedTrueColor() => await Assert.That(TerminalCases.ColonDelimitedTrueColor()).IsTrue();

    [Test]
    public async Task AlternateScreen() => await Assert.That(TerminalCases.AlternateScreen()).IsTrue();

    [Test]
    public async Task CursorVisibility() => await Assert.That(TerminalCases.CursorVisibility()).IsTrue();

    [Test]
    public async Task SplitEscapeAcrossChunks() => await Assert.That(TerminalCases.SplitEscapeAcrossChunks()).IsTrue();

    [Test]
    public async Task SplitUtf8AcrossChunks() => await Assert.That(TerminalCases.SplitUtf8AcrossChunks()).IsTrue();

    [Test]
    public async Task Utf8ProcessMatchesString() => await Assert.That(TerminalCases.Utf8ProcessMatchesString()).IsTrue();

    [Test]
    public async Task CsiParameterWhitespace() => await Assert.That(TerminalCases.CsiParameterWhitespace()).IsTrue();

    [Test]
    public async Task InvalidUtf8Replacement() => await Assert.That(TerminalCases.InvalidUtf8Replacement()).IsTrue();

    [Test]
    public async Task InsertBlankCharacters() => await Assert.That(TerminalCases.InsertBlankCharacters()).IsTrue();

    [Test]
    public async Task ScrollRegion() => await Assert.That(TerminalCases.ScrollRegion()).IsTrue();

    [Test]
    public async Task ScrollRegionByCount() => await Assert.That(TerminalCases.ScrollRegionByCount()).IsTrue();

    [Test]
    public async Task InsertAndDeleteLines() => await Assert.That(TerminalCases.InsertAndDeleteLines()).IsTrue();

    [Test]
    public async Task SameTimeFramesKeepLastScreen() => await Assert.That(TerminalCases.SameTimeFramesKeepLastScreen()).IsTrue();

    [Test]
    public async Task SameTimeFramesWithBlankFinalScreen() => await Assert.That(TerminalCases.SameTimeFramesWithBlankFinalScreen()).IsTrue();

    [Test]
    public async Task NameCommentPrefixIgnoresTrailingAnsiControls() => await Assert.That(TerminalCases.NameCommentPrefixIgnoresTrailingAnsiControls()).IsTrue();

    [Test]
    public async Task AlternateScreenRestoreKeepsNextPromptAdjacent() => await Assert.That(TerminalCases.AlternateScreenRestoreKeepsNextPromptAdjacent()).IsTrue();

    [Test]
    public async Task VimRedrawAtScenarioHeight() => await Assert.That(TerminalCases.VimRedrawAtScenarioHeight()).IsTrue();

    [Test]
    public async Task SoftFontThroughG1AndShiftOut() => await Assert.That(TerminalCases.SoftFontThroughG1AndShiftOut()).IsTrue();

    [Test]
    public async Task SoftFontThroughG0WithIntermediate() => await Assert.That(TerminalCases.SoftFontThroughG0WithIntermediate()).IsTrue();

    [Test]
    public async Task DesignationWithIntermediatePrintsNothing() => await Assert.That(TerminalCases.DesignationWithIntermediatePrintsNothing()).IsTrue();

    [Test]
    public async Task SoftFontSplitAcrossChunks() => await Assert.That(TerminalCases.SoftFontSplitAcrossChunks()).IsTrue();

    [Test]
    public async Task SoftFontDesignatedBeforeLoad() => await Assert.That(TerminalCases.SoftFontDesignatedBeforeLoad()).IsTrue();

    [Test]
    public async Task UndefinedSoftFontCodeIsBlank() => await Assert.That(TerminalCases.UndefinedSoftFontCodeIsBlank()).IsTrue();

    [Test]
    public async Task SoftFontMatrixSizeFromData() => await Assert.That(TerminalCases.SoftFontMatrixSizeFromData()).IsTrue();

    [Test]
    public async Task SoftFontEraseControl() => await Assert.That(TerminalCases.SoftFontEraseControl()).IsTrue();

    [Test]
    public async Task OtherDcsStaySkipped() => await Assert.That(TerminalCases.OtherDcsStaySkipped()).IsTrue();

    [Test]
    public async Task SosPmApcStringsPrintNothing() => await Assert.That(TerminalCases.SosPmApcStringsPrintNothing()).IsTrue();

    [Test]
    public async Task SoftGlyphCellsDifferFromText() => await Assert.That(TerminalCases.SoftGlyphCellsDifferFromText()).IsTrue();

    [Test]
    public async Task SaveRestoreKeepsCharsets() => await Assert.That(TerminalCases.SaveRestoreKeepsCharsets()).IsTrue();

    [Test]
    public async Task ResetDropsSoftFonts() => await Assert.That(TerminalCases.ResetDropsSoftFonts()).IsTrue();

    [Test]
    public async Task SingleShiftMapsOneCharacter() => await Assert.That(TerminalCases.SingleShiftMapsOneCharacter()).IsTrue();

    [Test]
    public async Task CastCursorTrackerFollowsEvents() => await Assert.That(TerminalCases.CastCursorTrackerFollowsEvents()).IsTrue();

    [Test]
    public async Task OscFilterDropsTitlesAndPaths() => await Assert.That(TerminalCases.OscFilterDropsTitlesAndPaths()).IsTrue();

    [Test]
    public async Task OscFilterFollowsSequencesAcrossEvents() => await Assert.That(TerminalCases.OscFilterFollowsSequencesAcrossEvents()).IsTrue();

    [Test]
    public async Task OscFilterKeepsColorsAndOtherEscapes() => await Assert.That(TerminalCases.OscFilterKeepsColorsAndOtherEscapes()).IsTrue();

    [Test]
    public async Task OscFilterKeepsEventsWithoutOsc() => await Assert.That(TerminalCases.OscFilterKeepsEventsWithoutOsc()).IsTrue();
}

internal static class TerminalCases
{
internal static bool CastCursorTrackerFollowsEvents()
{
    // A Windows PTY step starts ConPTY at the cursor this reports, so it must match what a player shows:
    // a 1-based row and column, wrapped lines, scrolling at the bottom, UTF-8 events from earlier PTY
    // steps, and only the events added since the last call.
    var tracker = new CastCursorTracker(10, 4);
    var events = new List<CastEvent> { CastEvent.Output(0, "$ ls\r\n") };
    var afterTyping = tracker.Advance(events);
    events.Add(CastEvent.Output(0.1, "0123456789ab"));
    var afterWrap = tracker.Advance(events);
    events.Add(CastEvent.Marker(0.2, "name"));
    events.Add(CastEvent.OutputUtf8(0.3, Encoding.UTF8.GetBytes("\r\n1\r\n2\r\n3\r\n")));
    var afterScroll = tracker.Advance(events);
    var again = tracker.Advance(events);
    return afterTyping == (2, 1) && afterWrap == (3, 3) && afterScroll == (4, 1) && again == (4, 1);
}


internal static TerminalTheme DarkTheme() =>
    TerminalTheme.FromResolved(new ResolvedTheme(
        "#d0d0d0",
        "#282c34",
        "#151515:#ac4142:#7e8e50:#e5b567:#6c99bb:#9f4e85:#7dd6cf:#d0d0d0:#505050:#ac4142:#7e8e50:#e5b567:#6c99bb:#9f4e85:#7dd6cf:#f5f5f5"));

internal static void Feed(AnsiParser parser, params string[] chunks)
{
    foreach (var chunk in chunks)
        parser.Process(chunk);
}

internal static bool SgrColorAndReset()
{
    var theme = DarkTheme();
    var buffer = new ScreenBuffer(8, 2, theme);
    var parser = new AnsiParser(buffer, theme);
    parser.Process("\u001b[31mA\u001b[0mB");

    return buffer.GetCell(0, 0).Text == "A"
        && buffer.GetCell(0, 0).Foreground == theme.AnsiPalette[1]
        && buffer.GetCell(0, 1).Text == "B"
        && buffer.GetCell(0, 1).Foreground == theme.Foreground;
}

internal static bool MoveCursorAndOverwrite()
{
    var theme = DarkTheme();
    var buffer = new ScreenBuffer(8, 2, theme);
    var parser = new AnsiParser(buffer, theme);
    parser.Process("ABC\u001b[1D!");

    return buffer.GetCell(0, 0).Text == "A"
        && buffer.GetCell(0, 1).Text == "B"
        && buffer.GetCell(0, 2).Text == "!";
}

internal static bool CjkWideCharacter()
{
    var theme = DarkTheme();
    var buffer = new ScreenBuffer(8, 2, theme);
    var parser = new AnsiParser(buffer, theme);
    parser.Process("\u4e2d");

    return buffer.GetCell(0, 0).Text == "\u4e2d"
        && buffer.GetCell(0, 0).IsWide
        && buffer.GetCell(0, 1).IsWideContinuation
        && buffer.CursorCol == 2;
}

internal static bool TrueColorForeground()
{
    var theme = DarkTheme();
    var buffer = new ScreenBuffer(8, 2, theme);
    var parser = new AnsiParser(buffer, theme);
    parser.Process("\u001b[38;2;255;128;0mA\u001b[0m");

    return buffer.GetCell(0, 0).Text == "A"
        && string.Equals(buffer.GetCell(0, 0).Foreground, "#FF8000", StringComparison.OrdinalIgnoreCase);
}

internal static bool ColonDelimitedTrueColor()
{
    var theme = DarkTheme();
    var buffer = new ScreenBuffer(8, 2, theme);
    var parser = new AnsiParser(buffer, theme);
    parser.Process("\u001b[38:2:255:128:0mA\u001b[0m");

    return buffer.GetCell(0, 0).Text == "A"
        && string.Equals(buffer.GetCell(0, 0).Foreground, "#FF8000", StringComparison.OrdinalIgnoreCase);
}

internal static bool AlternateScreen()
{
    var theme = DarkTheme();
    var buffer = new ScreenBuffer(8, 4, theme);
    var parser = new AnsiParser(buffer, theme);
    parser.Process("MAIN\u001b[?1049hALT\u001b[?1049l");

    return buffer.GetCell(0, 0).Text == "M"
        && buffer.GetCell(0, 1).Text == "A"
        && buffer.GetCell(0, 2).Text == "I"
        && buffer.GetCell(0, 3).Text == "N";
}

internal static bool CursorVisibility()
{
    var theme = DarkTheme();
    var buffer = new ScreenBuffer(8, 2, theme);
    var parser = new AnsiParser(buffer, theme);
    parser.Process("\u001b[?25l");

    return !buffer.CursorVisible;
}

internal static bool SplitEscapeAcrossChunks()
{
    var theme = DarkTheme();
    var buffer = new ScreenBuffer(12, 2, theme);
    var parser = new AnsiParser(buffer, theme);
    Feed(parser, "\u001b[", "31mA\u001b[", "0mB");

    return buffer.GetCell(0, 0).Text == "A"
        && buffer.GetCell(0, 1).Text == "B";
}

internal static void FeedUtf8(AnsiParser parser, byte[][] chunks)
{
    foreach (var chunk in chunks)
        parser.ProcessUtf8(chunk);
}

internal static bool SplitUtf8AcrossChunks()
{
    var theme = DarkTheme();
    var buffer = new ScreenBuffer(8, 2, theme);
    var parser = new AnsiParser(buffer, theme);
    var utf8 = "\u4e2d"u8.ToArray();
    FeedUtf8(parser, [utf8[..2], utf8[2..]]);

    return buffer.GetCell(0, 0).Text == "\u4e2d"
        && buffer.GetCell(0, 0).IsWide
        && buffer.GetCell(0, 1).IsWideContinuation
        && buffer.CursorCol == 2;
}

internal static bool Utf8ProcessMatchesString()
{
    var theme = DarkTheme();
    var stringBuffer = new ScreenBuffer(12, 2, theme);
    var utf8Buffer = new ScreenBuffer(12, 2, theme);
    var stringParser = new AnsiParser(stringBuffer, theme);
    var utf8Parser = new AnsiParser(utf8Buffer, theme);
    const string text = "\u001b[31mA\u001b[0mB\u4e2d";
    stringParser.Process(text);
    utf8Parser.ProcessUtf8(Encoding.UTF8.GetBytes(text));

    for (var col = 0; col < 4; col++)
    {
        if (stringBuffer.GetCell(0, col).Text != utf8Buffer.GetCell(0, col).Text)
            return false;
        if (stringBuffer.GetCell(0, col).Foreground != utf8Buffer.GetCell(0, col).Foreground)
            return false;
    }

    return stringBuffer.CursorCol == utf8Buffer.CursorCol;
}

internal static bool CsiParameterWhitespace()
{
    var theme = DarkTheme();
    var buffer = new ScreenBuffer(8, 2, theme);
    var parser = new AnsiParser(buffer, theme);
    parser.Process("\u001b[ 31mA\u001b[0m");

    return buffer.GetCell(0, 0).Text == "A"
        && buffer.GetCell(0, 0).Foreground == theme.AnsiPalette[1];
}

internal static bool InvalidUtf8Replacement()
{
    var theme = DarkTheme();
    var buffer = new ScreenBuffer(8, 2, theme);
    var parser = new AnsiParser(buffer, theme);
    parser.ProcessUtf8([0xFF]);

    return buffer.GetCell(0, 0).Text == "\uFFFD";
}

internal static bool InsertBlankCharacters()
{
    var theme = DarkTheme();
    var buffer = new ScreenBuffer(6, 2, theme);
    var parser = new AnsiParser(buffer, theme);
    parser.Process("ABCD\u001b[2G\u001b[2@Z");

    return buffer.GetCell(0, 0).Text == "A"
        && buffer.GetCell(0, 1).Text == "Z"
        && buffer.GetCell(0, 3).Text == "B";
}

internal static bool ScrollRegion()
{
    var theme = DarkTheme();
    var buffer = new ScreenBuffer(5, 4, theme);
    var parser = new AnsiParser(buffer, theme);
    parser.Process("\u001b[1;1HAAAAA\u001b[2;1HBBBBB\u001b[3;1HCCCCC\u001b[4;1HDDDDD");
    parser.Process("\u001b[2;3r\u001b[3;1H\n");

    return buffer.GetCell(0, 0).Text == "A"
        && buffer.GetCell(1, 0).Text == "C"
        && buffer.GetCell(2, 0).Text == " "
        && buffer.GetCell(3, 0).Text == "D";
}

/// <summary>Rows A–E with a red C, scroll region rows 2–4, after <paramref name="sequence"/>: the first letter of each row.</summary>
internal static (string Rows, bool RedMoved) ScrollRegionAfter(string sequence, int redRow)
{
    var theme = DarkTheme();
    var buffer = new ScreenBuffer(3, 5, theme);
    var parser = new AnsiParser(buffer, theme);
    parser.Process("\u001b[1;1HAAA\u001b[2;1HBBB\u001b[3;1H\u001b[31mCCC\u001b[m\u001b[4;1HDDD\u001b[5;1HEEE\u001b[2;4r");
    parser.Process(sequence);
    var rows = string.Concat(Enumerable.Range(0, buffer.Height).Select(row => buffer.GetCell(row, 0).Text == " " ? "." : buffer.GetCell(row, 0).Text));
    var red = redRow < 0 || (buffer.GetCell(redRow, 2).Text == "C" && buffer.GetCell(redRow, 2).Foreground == theme.AnsiPalette[1]);
    return (rows, red);
}

internal static bool ScrollRegionByCount()
{
    // Whole rows move together, styles included; rows outside the region stay.
    return ScrollRegionAfter("\u001b[2S", redRow: -1) == ("AD..E", true)
        && ScrollRegionAfter("\u001b[1S", redRow: 1) == ("ACD.E", true)
        && ScrollRegionAfter("\u001b[2T", redRow: -1) == ("A..BE", true)
        && ScrollRegionAfter("\u001b[1T", redRow: 3) == ("A.BCE", true)
        && ScrollRegionAfter("\u001b[9S", redRow: -1) == ("A...E", true)
        && ScrollRegionAfter("\u001b[9T", redRow: -1) == ("A...E", true)
        && ScrollRegionAfter("\u001b[4;1H\n", redRow: 1) == ("ACD.E", true);
}

internal static bool InsertAndDeleteLines()
{
    // Lines move from the cursor row to the bottom of the scroll region; outside it nothing happens.
    return ScrollRegionAfter("\u001b[3;1H\u001b[L", redRow: 3) == ("AB.CE", true)
        && ScrollRegionAfter("\u001b[2;1H\u001b[2M", redRow: -1) == ("AD..E", true)
        && ScrollRegionAfter("\u001b[3;1H\u001b[9M", redRow: -1) == ("AB..E", true)
        && ScrollRegionAfter("\u001b[2;1H\u001b[2L", redRow: -1) == ("A..BE", true)
        && ScrollRegionAfter("\u001b[5;1H\u001b[M", redRow: 2) == ("ABCDE", true);
}

internal static List<CastEvent> OutputEvents(params (double Time, string Data)[] events) =>
    events.Select(e => e.Data.StartsWith('#') ? CastEvent.Marker(e.Time, e.Data) : CastEvent.Output(e.Time, e.Data)).ToList();

internal static bool SameTimeFramesKeepLastScreen()
{
    // Writes within one millisecond: only the screen after the last of them is kept, at the event
    // that wrote it. A marker between same-time writes does not split them. The cursor hidden and
    // shown again at 1.0 leaves the screen as it was, so that time adds no frame.
    var theme = DarkTheme();
    var events = OutputEvents(
        (0.5, "a"), (0.5, "#mark"), (0.5, "b"), (0.5, "c"),
        (1.0, "\u001b[?25l"), (1.0, "\u001b[?25h"),
        (1.5, "d"), (2.0, "e"), (2.0, "#end"));
    var all = TerminalReplay.BuildFrames(events, 8, 2, CanvasWindow.Whole(8, 2), theme);
    var last = TerminalReplay.BuildFrames(events, 8, 2, CanvasWindow.Whole(8, 2), theme, lastOfSameTime: true);

    // What a caller that keeps the last of frames sharing a time sees, without repeats of an unchanged screen.
    var kept = all.Where((f, i) => i + 1 == all.Count || all[i + 1].Time != f.Time).ToList();
    kept = kept.Where((f, i) => i == 0 || kept[i - 1].Signature != f.Signature).ToList();
    return all.Count == 7
        && last.Select(f => (f.Time, f.Signature)).SequenceEqual(kept.Select(f => (f.Time, f.Signature)))
        && last.Select(f => f.EventIndex).SequenceEqual([3, 6, 7]);
}

internal static bool SameTimeFramesWithBlankFinalScreen()
{
    // A blank final screen after leaving the alternate screen is trimmed, which needs every frame:
    // both modes return the same list.
    var theme = DarkTheme();
    var events = OutputEvents(
        (0.0, "\u001b[?1049h"), (0.5, "x"), (0.5, "y"), (1.0, "\u001b[2J"), (1.0, "\u001b[?1049l"));
    var all = TerminalReplay.BuildFrames(events, 8, 2, CanvasWindow.Whole(8, 2), theme);
    var last = TerminalReplay.BuildFrames(events, 8, 2, CanvasWindow.Whole(8, 2), theme, lastOfSameTime: true);
    return all.Count > 0
        && !all[^1].IsBlank
        && last.Select(f => (f.Time, f.Signature, f.EventIndex)).SequenceEqual(all.Select(f => (f.Time, f.Signature, f.EventIndex)));
}

internal static bool NameCommentPrefixIgnoresTrailingAnsiControls()
{
    var events = new List<CastEvent>
{
    CastEvent.OutputUtf8(0, Encoding.UTF8.GetBytes("Running inside PTY\r\n\u001b]0;bash\a\u001b[?25h")),
    CastEvent.OutputUtf8(0.1, Encoding.UTF8.GetBytes("\u001b[?9001l\u001b[?1004l")),
};

    return CastEventLayout.NameCommentPrefixForLastEvent(events) == "";
}

internal static bool VimRedrawAtScenarioHeight()
{
    var theme = DarkTheme();
    var buffer = new ScreenBuffer(80, 24, theme);
    var parser = new AnsiParser(buffer, theme);

    parser.Process("\u001b[?1049h\u001b[2J");
    parser.Process("\u001b[3;1H#\u001b[10X");
    parser.Process("\u001b[3;3Hscenetake\u001b[5;1HEnglish\u001b[24;1H-- INSERT --\u001b[3;1H");

    return buffer.GetCell(2, 0).Text == "#"
        && buffer.GetCell(2, 2).Text == "s"
        && RowText(buffer, 23).Contains("INSERT", StringComparison.Ordinal)
        && buffer.CursorRow == 2
        && buffer.CursorCol == 0;
}

internal static bool AlternateScreenRestoreKeepsNextPromptAdjacent()
{
    var theme = DarkTheme();
    var buffer = new ScreenBuffer(80, 24, theme);
    var parser = new AnsiParser(buffer, theme);
    parser.Process("$ matrix 3\r\n");
    parser.Process("\u001b[?1049h");
    parser.Process("\u001b[80GX");
    parser.Process("\u001b[?1049l");
    parser.Process("$ ");

    return RowText(buffer, 0).StartsWith("$ matrix 3", StringComparison.Ordinal)
        && RowText(buffer, 1) == "$"
        && buffer.IsRowBlank(2)
        && buffer.CursorRow == 1
        && buffer.CursorCol == 2;
}

// Soft fonts (DECDLD). Glyphs are written as rows of '#' and '.', top row first.

internal static string[] Diagonal() => ["#....", ".#...", "..#..", "...#.", "....#", "....."];
internal static string[] TopBar() => ["#####", ".....", ".....", ".....", ".....", "....."];

/// <summary>Sixel data for one glyph: one band per six rows, one character per column, top pixel in bit 0.</summary>
internal static string SixelGlyph(string[] rows)
{
    var sb = new StringBuilder();
    var width = rows.Max(r => r.Length);
    for (var band = 0; band * 6 < rows.Length; band++)
    {
        if (band > 0)
            sb.Append('/');
        for (var x = 0; x < width; x++)
        {
            var bits = 0;
            for (var k = 0; k < 6; k++)
            {
                var y = band * 6 + k;
                if (y < rows.Length && x < rows[y].Length && rows[y][x] == '#')
                    bits |= 1 << k;
            }

            sb.Append((char)(0x3F + bits));
        }
    }

    return sb.ToString();
}

/// <summary>A DECDLD string: <c>ESC P parameters { name glyph;glyph… ESC \</c>.</summary>
internal static string Decdld(string parameters, string name, params string[][] glyphs) =>
    "\u001bP" + parameters + "{" + name + string.Join(';', glyphs.Select(SixelGlyph)) + "\u001b\\";

internal static bool GlyphIs(ScreenCell cell, string[] rows)
{
    if (cell.Glyph is not { } glyph || glyph.Width != rows[0].Length || glyph.Height != rows.Length)
        return false;
    for (var y = 0; y < rows.Length; y++)
    {
        for (var x = 0; x < rows[y].Length; x++)
        {
            if (glyph.IsSet(x, y) != (rows[y][x] == '#'))
                return false;
        }
    }

    return true;
}

internal static bool SoftFontThroughG1AndShiftOut()
{
    // The in-box Windows ConPTY's form: the font renamed to "@", designated into G1, and each run of
    // its characters between SO and SI. Text outside SO/SI stays ASCII.
    var theme = DarkTheme();
    var buffer = new ScreenBuffer(10, 2, theme);
    var parser = new AnsiParser(buffer, theme);
    parser.Process(Decdld("1;1;0;5;0;2;6;0", "@", Diagonal(), TopBar()) + "\u001b)@x\u000E!\"\u000F!");

    return buffer.GetCell(0, 0) is { Text: "x", Glyph: null }
        && GlyphIs(buffer.GetCell(0, 1), Diagonal()) && buffer.GetCell(0, 1).Text == "!"
        && GlyphIs(buffer.GetCell(0, 2), TopBar())
        && buffer.GetCell(0, 3) is { Text: "!", Glyph: null }
        && buffer.CursorCol == 4;
}

internal static bool SoftFontThroughG0WithIntermediate()
{
    // The form a program writes itself (matrix on Unix, or the out-of-band ConPTY): a name with an
    // intermediate, designated into G0, then ASCII designated back.
    var theme = DarkTheme();
    var buffer = new ScreenBuffer(10, 2, theme);
    var parser = new AnsiParser(buffer, theme);
    parser.Process(Decdld("1;1;0;5;0;2;6;0", " @", Diagonal()) + "\u001b( @!\u001b(B!");

    return GlyphIs(buffer.GetCell(0, 0), Diagonal())
        && buffer.GetCell(0, 1) is { Text: "!", Glyph: null }
        && buffer.CursorCol == 2;
}

internal static bool DesignationWithIntermediatePrintsNothing()
{
    // ESC ( SP @ is one sequence. Reading only one byte after "(" used to print the "@".
    // DEC line drawing (ESC ( 0) is not drawn as lines and stays ASCII.
    var theme = DarkTheme();
    var buffer = new ScreenBuffer(10, 2, theme);
    var parser = new AnsiParser(buffer, theme);
    parser.Process("\u001b( @AB\u001b(0q\u001b(B");

    return RowText(buffer, 0) == "ABq" && buffer.CursorCol == 3;
}

internal static bool SoftFontSplitAcrossChunks()
{
    var theme = DarkTheme();
    var buffer = new ScreenBuffer(10, 2, theme);
    var parser = new AnsiParser(buffer, theme);
    var load = Decdld("1;1;0;5;0;2;6;0", " @", Diagonal()) + "\u001b( @!";
    var cuts = new[] { 3, 12, load.IndexOf('\\'), load.Length - 3 };
    var start = 0;
    foreach (var cut in cuts)
    {
        parser.Process(load[start..cut]);
        start = cut;
    }

    parser.Process(load[start..]);
    return GlyphIs(buffer.GetCell(0, 0), Diagonal()) && buffer.CursorCol == 1;
}

internal static bool SoftFontDesignatedBeforeLoad()
{
    // A designation names a set. The font is used once it loads, whichever comes first.
    var theme = DarkTheme();
    var buffer = new ScreenBuffer(10, 2, theme);
    var parser = new AnsiParser(buffer, theme);
    parser.Process("\u001b( @!" + Decdld("1;1;0;5;0;2;6;0", " @", Diagonal()) + "!");

    return buffer.GetCell(0, 0) is { Text: "!", Glyph: null } && GlyphIs(buffer.GetCell(0, 1), Diagonal());
}

internal static bool UndefinedSoftFontCodeIsBlank()
{
    // One glyph at 0x21. 0x22 has none and draws blank, as on DEC terminals. Space stays a space in a 94-character set.
    var theme = DarkTheme();
    var buffer = new ScreenBuffer(10, 2, theme);
    var parser = new AnsiParser(buffer, theme);
    parser.Process(Decdld("1;1;0;5;0;2;6;0", "@", Diagonal()) + "\u001b(@\" !");

    return buffer.GetCell(0, 0) is { Text: " ", Glyph: null }
        && buffer.GetCell(0, 1) is { Text: " ", Glyph: null }
        && GlyphIs(buffer.GetCell(0, 2), Diagonal());
}

internal static bool SoftFontMatrixSizeFromData()
{
    // Pcmw 0 and Pcmh 0: the width is the widest glyph, the height six rows per band.
    string[] tall = ["#....", ".....", ".....", ".....", ".....", ".....", "....#"];
    var theme = DarkTheme();
    var buffer = new ScreenBuffer(10, 2, theme);
    var parser = new AnsiParser(buffer, theme);
    parser.Process(Decdld("1;1;0;0;0;2;0;0", "@", tall) + "\u001b(@!");

    return buffer.GetCell(0, 0).Glyph is { Width: 5, Height: 12 } glyph
        && glyph.IsSet(0, 0) && glyph.IsSet(4, 6) && !glyph.IsSet(4, 0);
}

internal static bool SoftFontEraseControl()
{
    // Pe 1 replaces only the codes it loads; Pe 0 replaces the whole font.
    var theme = DarkTheme();
    var buffer = new ScreenBuffer(10, 2, theme);
    var parser = new AnsiParser(buffer, theme);
    parser.Process(Decdld("1;1;0;5;0;2;6;0", "@", Diagonal(), Diagonal()) + Decdld("1;2;1;5;0;2;6;0", "@", TopBar()) + "\u001b(@!\"");
    parser.Process(Decdld("1;2;0;5;0;2;6;0", "@", TopBar()) + "!\"");

    return GlyphIs(buffer.GetCell(0, 0), Diagonal()) && GlyphIs(buffer.GetCell(0, 1), TopBar())
        && buffer.GetCell(0, 2) is { Text: " ", Glyph: null } && GlyphIs(buffer.GetCell(0, 3), TopBar());
}

internal static string OscFilterText(List<CastEvent> events) =>
    string.Concat(events.Where(e => e.Kind == CastEventKind.Output)
        .Select(e => e.HasUtf8Output ? Encoding.UTF8.GetString(e.Utf8Output.Span) : e.Data));

internal static bool OscFilterDropsTitlesAndPaths()
{
    // ConPTY's title is the shell's full path. vim, shells and ls put folders, host names and file URLs in
    // OSC 0/2, 7, 9;9, 133, 633 and 8; OSC 52 carries the clipboard. Only what a terminal draws is left.
    var events = new List<CastEvent>
{
    CastEvent.OutputUtf8(0, Encoding.UTF8.GetBytes("out 日本\r\n\u001b]0;C:\\Users\\me\\scoop\\shims\\pwsh.EXE\a\u001b[?25h")),
    CastEvent.Output(0.1, "\u001b]2;README.md (D:\\work\\secret) - VIM\u001b\\vim"),
    CastEvent.Output(0.2, "\u001b]7;file://host/home/me\a\u001b]9;9;C:\\Users\\me\a\u001b]133;A\a\u001b]633;P;Cwd=/home/me\a$ "),
    CastEvent.Output(0.3, "\u001b]8;;file://host/home/me/a.txt\aa.txt\u001b]8;;\a \u001b]52;c;c2VjcmV0\a\u001b]l old-style title\u001b\\"),
};
    var filtered = CastOscFilter.RemovePrivateSequences(events);
    var text = OscFilterText(filtered);
    var ok = text == "out 日本\r\n\u001b[?25hvim$ a.txt " && filtered.Count == 4;
    if (!ok)
        Console.Error.WriteLine($"  text: {text.Replace("\u001b", "\\e")} ({filtered.Count} events)");
    return ok;
}

internal static bool OscFilterFollowsSequencesAcrossEvents()
{
    // A PTY read can end anywhere in a sequence: after ESC, inside the code, inside the body, or between
    // the ESC and backslash of ST. An event left with nothing is dropped, except the last one, which
    // sets the recording's length and stays empty.
    var events = new List<CastEvent>
{
    CastEvent.OutputUtf8(0.0, "a\u001b"u8.ToArray()),
    CastEvent.OutputUtf8(0.1, "]"u8.ToArray()),
    CastEvent.OutputUtf8(0.2, "0;C:\\Users\\me"u8.ToArray()),
    CastEvent.OutputUtf8(0.3, "\\pwsh.EXE\u001b"u8.ToArray()),
    CastEvent.OutputUtf8(0.4, "\\b\u001b"u8.ToArray()),
    CastEvent.Marker(0.45, "step"),
    CastEvent.OutputUtf8(0.5, "[1mc\u001b]2"u8.ToArray()),
    CastEvent.OutputUtf8(0.6, ";title\a"u8.ToArray()),
};
    var filtered = CastOscFilter.RemovePrivateSequences(events);
    var text = OscFilterText(filtered);
    var times = string.Join(",", filtered.Select(e => e.Time.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)));
    var ok = text == "ab\u001b[1mc"
        && times == "0,0.4,0.45,0.5,0.6"
        && filtered[^1].Kind == CastEventKind.Output && OscFilterText([filtered[^1]]) == ""
        && filtered[2].Kind == CastEventKind.Marker;
    if (!ok)
        Console.Error.WriteLine($"  text: {text.Replace("\u001b", "\\e")} times: {times}");
    return ok;
}

internal static bool OscFilterKeepsColorsAndOtherEscapes()
{
    // Color OSCs set the palette and stay. CSI, DCS (soft fonts) and a bare ESC are not OSCs. An ESC inside
    // a dropped OSC that is not ST ends it, and the sequence it starts is kept.
    var input = "\u001b]4;1;rgb:ff/00/00\a\u001b]11;#000000\u001b\\\u001b]104\a\u001b[31mred\u001b[0m\u001bP1;1{ @\u001b\\\u001b7\u001b]0;t\u001b[2Kx";
    var filtered = CastOscFilter.RemovePrivateSequences([CastEvent.Output(0, input)]);
    var text = OscFilterText(filtered);
    var expected = "\u001b]4;1;rgb:ff/00/00\a\u001b]11;#000000\u001b\\\u001b]104\a\u001b[31mred\u001b[0m\u001bP1;1{ @\u001b\\\u001b7\u001b[2Kx";
    if (text != expected)
        Console.Error.WriteLine($"  text: {text.Replace("\u001b", "\\e")}");
    return text == expected;
}

internal static bool OscFilterKeepsEventsWithoutOsc()
{
    // Events without an OSC are passed on as they are: same instance data, string events stay strings.
    var typed = CastEvent.Output(0, "🎉");
    var sgr = CastEvent.Output(0.1, "\u001b[1;96m");
    var bytes = CastEvent.OutputUtf8(0.2, "\u001b[?25lplain\r\n"u8.ToArray());
    var filtered = CastOscFilter.RemovePrivateSequences([typed, sgr, bytes]);
    return filtered.Count == 3 && filtered[0] == typed && filtered[1] == sgr
        && filtered[2].Utf8Output.Equals(bytes.Utf8Output);
}

internal static bool OtherDcsStaySkipped()
{
    // DECRQSS and a Sixel image (whose data has "{") are not soft fonts and print nothing.
    var theme = DarkTheme();
    var buffer = new ScreenBuffer(10, 2, theme);
    var parser = new AnsiParser(buffer, theme);
    parser.Process("\u001bP$qm\u001b\\A\u001bPq#0;2;0;0;0#0~~{{\u001b\\B\u001bP1;1{@\u001b\\C");

    return RowText(buffer, 0) == "ABC" && buffer.GetCell(0, 2).Glyph is null;
}

internal static bool SosPmApcStringsPrintNothing()
{
    // An inline image's data (APC), a privacy message (PM), and SOS are strings like DCS. The masker does not search
    // their data, so drawing it would show text that was never masked.
    var theme = DarkTheme();
    var buffer = new ScreenBuffer(10, 4, theme);
    var parser = new AnsiParser(buffer, theme);
    parser.Process("A\u001b_Ga=T;QUJD\u001b\\B\u001b^pm\u001b\\C\u001bXsos\u001b\\D\r\n");
    // A string cut off by CAN, SUB, or another sequence hides only itself, as in a terminal. BEL ends it too.
    parser.Process("E\u001b_Gdata\u001b[1mF\u001b_x\u0018G\u001b^y\u001aH\u001bXz\u0007I\r\n");
    // A read that ends inside the string, or on an ESC in it, waits for the next one.
    parser.Process("\u001b_Gda");
    parser.Process("ta\u001b");
    parser.Process("\\J\r\n");
    // A program that asks tmux to pass a sequence through writes each ESC in it twice. The first one does nothing, so
    // the DCS after it is still a string.
    parser.Process("\u001b\u001bPq API\u001b\\K");

    return RowText(buffer, 0) == "ABCD" && RowText(buffer, 1) == "EFGHI" && RowText(buffer, 2) == "J" && RowText(buffer, 3) == "K";
}

internal static bool SoftGlyphCellsDifferFromText()
{
    // A glyph cell and a plain cell with the same code are different rows. Equal bitmaps are one glyph.
    var theme = DarkTheme();
    var soft = new ScreenBuffer(4, 1, theme);
    new AnsiParser(soft, theme).Process(Decdld("1;1;0;5;0;2;6;0", "@", Diagonal()) + "\u001b(@!");
    var other = new ScreenBuffer(4, 1, theme);
    new AnsiParser(other, theme).Process(Decdld("1;1;0;5;0;2;6;0", " A", Diagonal()) + "\u001b( A!");
    var plain = new ScreenBuffer(4, 1, theme);
    new AnsiParser(plain, theme).Process("!");

    var copy = new ScreenCell[4];
    plain.CopyRowTo(0, copy);
    return !soft.RowEquals(plain, 0) && !soft.RowEquals(0, copy)
        && soft.RowHash(0) != plain.RowHash(0)
        && soft.ComputeCanvasSignature(4, 1) != plain.ComputeCanvasSignature(4, 1)
        && !soft.IsRowBlank(0)
        && ReferenceEquals(soft.GetCell(0, 0).Glyph, other.GetCell(0, 0).Glyph)
        && soft.RowEquals(other, 0);
}

internal static bool SaveRestoreKeepsCharsets()
{
    // DECSC saves the designations and the GL shift with the cursor, and DECRC brings them back.
    var theme = DarkTheme();
    var buffer = new ScreenBuffer(10, 2, theme);
    var parser = new AnsiParser(buffer, theme);
    parser.Process(Decdld("1;1;0;5;0;2;6;0", "@", Diagonal()) + "\u001b)@\u000E\u001b7\u000F\u001b8!");

    return GlyphIs(buffer.GetCell(0, 0), Diagonal());
}

internal static bool ResetDropsSoftFonts()
{
    // RIS resets the designations and drops every soft font.
    var theme = DarkTheme();
    var buffer = new ScreenBuffer(10, 2, theme);
    var parser = new AnsiParser(buffer, theme);
    parser.Process(Decdld("1;1;0;5;0;2;6;0", "@", Diagonal()) + "\u001b(@\u001bc!\u001b(@!");

    return buffer.GetCell(0, 0) is { Text: "!", Glyph: null } && buffer.GetCell(0, 1) is { Text: "!", Glyph: null };
}

internal static bool SingleShiftMapsOneCharacter()
{
    // SS2 takes the next character from G2 only.
    var theme = DarkTheme();
    var buffer = new ScreenBuffer(10, 2, theme);
    var parser = new AnsiParser(buffer, theme);
    parser.Process(Decdld("1;1;0;5;0;2;6;0", "@", Diagonal()) + "\u001b*@\u001bN!!");

    return GlyphIs(buffer.GetCell(0, 0), Diagonal()) && buffer.GetCell(0, 1) is { Text: "!", Glyph: null };
}

internal static string RowText(ScreenBuffer buffer, int row)
{
    var chars = new char[buffer.Width];
    for (var col = 0; col < buffer.Width; col++)
        chars[col] = buffer.GetCell(row, col).Text.Length == 1 ? buffer.GetCell(row, col).Text[0] : ' ';
    return new string(chars).TrimEnd();
}
}
