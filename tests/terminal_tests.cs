#:sdk Microsoft.NET.Sdk
#:property TargetFramework=net10.0
#:property Nullable=enable
#:property ImplicitUsings=enable
#:include ../Terminal.cs
#:include ../CastTimingPipeline.cs

using System.Text;

var failures = 0;
failures += Run("SgrColorAndReset", SgrColorAndReset);
failures += Run("MoveCursorAndOverwrite", MoveCursorAndOverwrite);
failures += Run("CjkWideCharacter", CjkWideCharacter);
failures += Run("TrueColorForeground", TrueColorForeground);
failures += Run("ColonDelimitedTrueColor", ColonDelimitedTrueColor);
failures += Run("AlternateScreen", AlternateScreen);
failures += Run("CursorVisibility", CursorVisibility);
failures += Run("SplitEscapeAcrossChunks", SplitEscapeAcrossChunks);
failures += Run("SplitUtf8AcrossChunks", SplitUtf8AcrossChunks);
failures += Run("Utf8ProcessMatchesString", Utf8ProcessMatchesString);
failures += Run("CsiParameterWhitespace", CsiParameterWhitespace);
failures += Run("InvalidUtf8Replacement", InvalidUtf8Replacement);
failures += Run("InsertBlankCharacters", InsertBlankCharacters);
failures += Run("ScrollRegion", ScrollRegion);
failures += Run("NameCommentPrefixIgnoresTrailingAnsiControls", NameCommentPrefixIgnoresTrailingAnsiControls);
failures += Run("AlternateScreenRestoreKeepsNextPromptAdjacent", AlternateScreenRestoreKeepsNextPromptAdjacent);
failures += Run("VimRedrawAtScenarioHeight", VimRedrawAtScenarioHeight);
failures += Run("SoftFontThroughG1AndShiftOut", SoftFontThroughG1AndShiftOut);
failures += Run("SoftFontThroughG0WithIntermediate", SoftFontThroughG0WithIntermediate);
failures += Run("DesignationWithIntermediatePrintsNothing", DesignationWithIntermediatePrintsNothing);
failures += Run("SoftFontSplitAcrossChunks", SoftFontSplitAcrossChunks);
failures += Run("SoftFontDesignatedBeforeLoad", SoftFontDesignatedBeforeLoad);
failures += Run("UndefinedSoftFontCodeIsBlank", UndefinedSoftFontCodeIsBlank);
failures += Run("SoftFontMatrixSizeFromData", SoftFontMatrixSizeFromData);
failures += Run("SoftFontEraseControl", SoftFontEraseControl);
failures += Run("OtherDcsStaySkipped", OtherDcsStaySkipped);
failures += Run("SoftGlyphCellsDifferFromText", SoftGlyphCellsDifferFromText);
failures += Run("SaveRestoreKeepsCharsets", SaveRestoreKeepsCharsets);
failures += Run("ResetDropsSoftFonts", ResetDropsSoftFonts);
failures += Run("SingleShiftMapsOneCharacter", SingleShiftMapsOneCharacter);

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
        Console.Error.WriteLine($"FAIL {name}: {ex.Message}");
        return 1;
    }
}

static TerminalTheme DarkTheme() =>
    TerminalTheme.FromResolved(new ResolvedTheme(
        "#d0d0d0",
        "#282c34",
        "#151515:#ac4142:#7e8e50:#e5b567:#6c99bb:#9f4e85:#7dd6cf:#d0d0d0:#505050:#ac4142:#7e8e50:#e5b567:#6c99bb:#9f4e85:#7dd6cf:#f5f5f5"));

static void Feed(AnsiParser parser, params string[] chunks)
{
    foreach (var chunk in chunks)
        parser.Process(chunk);
}

static bool SgrColorAndReset()
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

static bool MoveCursorAndOverwrite()
{
    var theme = DarkTheme();
    var buffer = new ScreenBuffer(8, 2, theme);
    var parser = new AnsiParser(buffer, theme);
    parser.Process("ABC\u001b[1D!");

    return buffer.GetCell(0, 0).Text == "A"
        && buffer.GetCell(0, 1).Text == "B"
        && buffer.GetCell(0, 2).Text == "!";
}

static bool CjkWideCharacter()
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

static bool TrueColorForeground()
{
    var theme = DarkTheme();
    var buffer = new ScreenBuffer(8, 2, theme);
    var parser = new AnsiParser(buffer, theme);
    parser.Process("\u001b[38;2;255;128;0mA\u001b[0m");

    return buffer.GetCell(0, 0).Text == "A"
        && string.Equals(buffer.GetCell(0, 0).Foreground, "#FF8000", StringComparison.OrdinalIgnoreCase);
}

static bool ColonDelimitedTrueColor()
{
    var theme = DarkTheme();
    var buffer = new ScreenBuffer(8, 2, theme);
    var parser = new AnsiParser(buffer, theme);
    parser.Process("\u001b[38:2:255:128:0mA\u001b[0m");

    return buffer.GetCell(0, 0).Text == "A"
        && string.Equals(buffer.GetCell(0, 0).Foreground, "#FF8000", StringComparison.OrdinalIgnoreCase);
}

static bool AlternateScreen()
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

static bool CursorVisibility()
{
    var theme = DarkTheme();
    var buffer = new ScreenBuffer(8, 2, theme);
    var parser = new AnsiParser(buffer, theme);
    parser.Process("\u001b[?25l");

    return !buffer.CursorVisible;
}

static bool SplitEscapeAcrossChunks()
{
    var theme = DarkTheme();
    var buffer = new ScreenBuffer(12, 2, theme);
    var parser = new AnsiParser(buffer, theme);
    Feed(parser, "\u001b[", "31mA\u001b[", "0mB");

    return buffer.GetCell(0, 0).Text == "A"
        && buffer.GetCell(0, 1).Text == "B";
}

static void FeedUtf8(AnsiParser parser, byte[][] chunks)
{
    foreach (var chunk in chunks)
        parser.ProcessUtf8(chunk);
}

static bool SplitUtf8AcrossChunks()
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

static bool Utf8ProcessMatchesString()
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

static bool CsiParameterWhitespace()
{
    var theme = DarkTheme();
    var buffer = new ScreenBuffer(8, 2, theme);
    var parser = new AnsiParser(buffer, theme);
    parser.Process("\u001b[ 31mA\u001b[0m");

    return buffer.GetCell(0, 0).Text == "A"
        && buffer.GetCell(0, 0).Foreground == theme.AnsiPalette[1];
}

static bool InvalidUtf8Replacement()
{
    var theme = DarkTheme();
    var buffer = new ScreenBuffer(8, 2, theme);
    var parser = new AnsiParser(buffer, theme);
    parser.ProcessUtf8([0xFF]);

    return buffer.GetCell(0, 0).Text == "\uFFFD";
}

static bool InsertBlankCharacters()
{
    var theme = DarkTheme();
    var buffer = new ScreenBuffer(6, 2, theme);
    var parser = new AnsiParser(buffer, theme);
    parser.Process("ABCD\u001b[2G\u001b[2@Z");

    return buffer.GetCell(0, 0).Text == "A"
        && buffer.GetCell(0, 1).Text == "Z"
        && buffer.GetCell(0, 3).Text == "B";
}

static bool ScrollRegion()
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

static bool NameCommentPrefixIgnoresTrailingAnsiControls()
{
    var events = new List<CastEvent>
    {
        CastEvent.OutputUtf8(0, Encoding.UTF8.GetBytes("Running inside PTY\r\n\u001b]0;bash\a\u001b[?25h")),
        CastEvent.OutputUtf8(0.1, Encoding.UTF8.GetBytes("\u001b[?9001l\u001b[?1004l")),
    };

    return CastEventLayout.NameCommentPrefixForLastEvent(events) == "";
}

static bool VimRedrawAtScenarioHeight()
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

static bool AlternateScreenRestoreKeepsNextPromptAdjacent()
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

static string[] Diagonal() => ["#....", ".#...", "..#..", "...#.", "....#", "....."];
static string[] TopBar() => ["#####", ".....", ".....", ".....", ".....", "....."];

/// <summary>Sixel data for one glyph: one band per six rows, one character per column, top pixel in bit 0.</summary>
static string SixelGlyph(string[] rows)
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
static string Decdld(string parameters, string name, params string[][] glyphs) =>
    "\u001bP" + parameters + "{" + name + string.Join(';', glyphs.Select(SixelGlyph)) + "\u001b\\";

static bool GlyphIs(ScreenCell cell, string[] rows)
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

static bool SoftFontThroughG1AndShiftOut()
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

static bool SoftFontThroughG0WithIntermediate()
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

static bool DesignationWithIntermediatePrintsNothing()
{
    // ESC ( SP @ is one sequence. Reading only one byte after "(" used to print the "@".
    // DEC line drawing (ESC ( 0) is not drawn as lines and stays ASCII.
    var theme = DarkTheme();
    var buffer = new ScreenBuffer(10, 2, theme);
    var parser = new AnsiParser(buffer, theme);
    parser.Process("\u001b( @AB\u001b(0q\u001b(B");

    return RowText(buffer, 0) == "ABq" && buffer.CursorCol == 3;
}

static bool SoftFontSplitAcrossChunks()
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

static bool SoftFontDesignatedBeforeLoad()
{
    // A designation names a set. The font is used once it loads, whichever comes first.
    var theme = DarkTheme();
    var buffer = new ScreenBuffer(10, 2, theme);
    var parser = new AnsiParser(buffer, theme);
    parser.Process("\u001b( @!" + Decdld("1;1;0;5;0;2;6;0", " @", Diagonal()) + "!");

    return buffer.GetCell(0, 0) is { Text: "!", Glyph: null } && GlyphIs(buffer.GetCell(0, 1), Diagonal());
}

static bool UndefinedSoftFontCodeIsBlank()
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

static bool SoftFontMatrixSizeFromData()
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

static bool SoftFontEraseControl()
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

static bool OtherDcsStaySkipped()
{
    // DECRQSS and a Sixel image (whose data has "{") are not soft fonts and print nothing.
    var theme = DarkTheme();
    var buffer = new ScreenBuffer(10, 2, theme);
    var parser = new AnsiParser(buffer, theme);
    parser.Process("\u001bP$qm\u001b\\A\u001bPq#0;2;0;0;0#0~~{{\u001b\\B\u001bP1;1{@\u001b\\C");

    return RowText(buffer, 0) == "ABC" && buffer.GetCell(0, 2).Glyph is null;
}

static bool SoftGlyphCellsDifferFromText()
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

static bool SaveRestoreKeepsCharsets()
{
    // DECSC saves the designations and the GL shift with the cursor, and DECRC brings them back.
    var theme = DarkTheme();
    var buffer = new ScreenBuffer(10, 2, theme);
    var parser = new AnsiParser(buffer, theme);
    parser.Process(Decdld("1;1;0;5;0;2;6;0", "@", Diagonal()) + "\u001b)@\u000E\u001b7\u000F\u001b8!");

    return GlyphIs(buffer.GetCell(0, 0), Diagonal());
}

static bool ResetDropsSoftFonts()
{
    // RIS resets the designations and drops every soft font.
    var theme = DarkTheme();
    var buffer = new ScreenBuffer(10, 2, theme);
    var parser = new AnsiParser(buffer, theme);
    parser.Process(Decdld("1;1;0;5;0;2;6;0", "@", Diagonal()) + "\u001b(@\u001bc!\u001b(@!");

    return buffer.GetCell(0, 0) is { Text: "!", Glyph: null } && buffer.GetCell(0, 1) is { Text: "!", Glyph: null };
}

static bool SingleShiftMapsOneCharacter()
{
    // SS2 takes the next character from G2 only.
    var theme = DarkTheme();
    var buffer = new ScreenBuffer(10, 2, theme);
    var parser = new AnsiParser(buffer, theme);
    parser.Process(Decdld("1;1;0;5;0;2;6;0", "@", Diagonal()) + "\u001b*@\u001bN!!");

    return GlyphIs(buffer.GetCell(0, 0), Diagonal()) && buffer.GetCell(0, 1) is { Text: "!", Glyph: null };
}

static string RowText(ScreenBuffer buffer, int row)
{
    var chars = new char[buffer.Width];
    for (var col = 0; col < buffer.Width; col++)
        chars[col] = buffer.GetCell(row, col).Text.Length == 1 ? buffer.GetCell(row, col).Text[0] : ' ';
    return new string(chars).TrimEnd();
}

internal readonly record struct ResolvedTheme(string Fg, string Bg, string Palette);
