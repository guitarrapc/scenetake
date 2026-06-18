#:sdk Microsoft.NET.Sdk
#:property TargetFramework=net10.0
#:property Nullable=enable
#:property ImplicitUsings=enable
#:include ../Terminal.cs

var failures = 0;
failures += Run("SgrColorAndReset", SgrColorAndReset);
failures += Run("MoveCursorAndOverwrite", MoveCursorAndOverwrite);
failures += Run("CjkWideCharacter", CjkWideCharacter);
failures += Run("TrueColorForeground", TrueColorForeground);
failures += Run("ColonDelimitedTrueColor", ColonDelimitedTrueColor);
failures += Run("AlternateScreen", AlternateScreen);
failures += Run("CursorVisibility", CursorVisibility);
failures += Run("SplitEscapeAcrossChunks", SplitEscapeAcrossChunks);
failures += Run("InsertBlankCharacters", InsertBlankCharacters);
failures += Run("ScrollRegion", ScrollRegion);

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

internal readonly record struct ResolvedTheme(string Fg, string Bg, string Palette);
