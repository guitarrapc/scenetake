using Scenetake.TestSupport;

namespace Scenetake.Core.Tests;

public sealed class WindowChromeTests
{
    [Test]
    public async Task LightBackgroundTakesShortHex() => await Assert.That(WindowChromeCases.LightBackgroundTakesShortHex()).IsTrue();

    [Test]
    public async Task BoldKeepsAColorItCannotBrighten() => await Assert.That(WindowChromeCases.BoldKeepsAColorItCannotBrighten()).IsTrue();

    [Test]
    public async Task ParseWindowAcceptsKnownValues() => await Assert.That(WindowChromeCases.ParseWindowAcceptsKnownValues()).IsTrue();

    [Test]
    public async Task ParseWindowRejectsUnknownValue() => await Assert.That(WindowChromeCases.ParseWindowRejectsUnknownValue()).IsTrue();

    [Test]
    public async Task ScenarioResolveReadsWindow() => await Assert.That(WindowChromeCases.ScenarioResolveReadsWindow()).IsTrue();

    [Test]
    public async Task ApplySvgOverridesWindowWins() => await Assert.That(WindowChromeCases.ApplySvgOverridesWindowWins()).IsTrue();

    [Test]
    public async Task CastTagReadsWindow() => await Assert.That(WindowChromeCases.CastTagReadsWindow()).IsTrue();

    [Test]
    public async Task MacosChromeAddsTrafficLights() => await Assert.That(WindowChromeCases.MacosChromeAddsTrafficLights()).IsTrue();

    [Test]
    public async Task WindowsChromeIsATabRow() => await Assert.That(WindowChromeCases.WindowsChromeIsATabRow()).IsTrue();

    [Test]
    public async Task WinTabRowFitsItsTitle() => await Assert.That(WindowChromeCases.WinTabRowFitsItsTitle()).IsTrue();

    [Test]
    public async Task ChromeIncreasesSvgHeight() => await Assert.That(WindowChromeCases.ChromeIncreasesSvgHeight()).IsTrue();

    [Test]
    public async Task PlainSvgHasNoChrome() => await Assert.That(WindowChromeCases.PlainSvgHasNoChrome()).IsTrue();

    [Test]
    public async Task ScenarioResolveReadsStyle() => await Assert.That(WindowChromeCases.ScenarioResolveReadsStyle()).IsTrue();

    [Test]
    public async Task ScenarioResolveRejectsBadStyle() => await Assert.That(WindowChromeCases.ScenarioResolveRejectsBadStyle()).IsTrue();

    [Test]
    public async Task BackgroundImagePathFollowsBaseFolder() => await Assert.That(WindowChromeCases.BackgroundImagePathFollowsBaseFolder()).IsTrue();

    [Test]
    public async Task CastTagsReadStyle() => await Assert.That(WindowChromeCases.CastTagsReadStyle()).IsTrue();

    [Test]
    public async Task ChromeShowsTitle() => await Assert.That(WindowChromeCases.ChromeShowsTitle()).IsTrue();

    [Test]
    public async Task FitWindowTitleCutsToRoom() => await Assert.That(WindowChromeCases.FitWindowTitleCutsToRoom()).IsTrue();

    [Test]
    public async Task BackgroundMarginAndPaddingShapeCanvas() => await Assert.That(WindowChromeCases.BackgroundMarginAndPaddingShapeCanvas()).IsTrue();

    [Test]
    public async Task ParseOpacityAndBlur() => await Assert.That(WindowChromeCases.ParseOpacityAndBlur()).IsTrue();

    [Test]
    public async Task OpacityAndBlurNeedBackground() => await Assert.That(WindowChromeCases.OpacityAndBlurNeedBackground()).IsTrue();

    [Test]
    public async Task TranslucentWindowSvg() => await Assert.That(WindowChromeCases.TranslucentWindowSvg()).IsTrue();

    [Test]
    public async Task FrostedWindowSvg() => await Assert.That(WindowChromeCases.FrostedWindowSvg()).IsTrue();

    [Test]
    public async Task WindowOnBackgroundCastsShadow() => await Assert.That(WindowChromeCases.WindowOnBackgroundCastsShadow()).IsTrue();

    [Test]
    public async Task DarkMacWindowHasEdgeLine() => await Assert.That(WindowChromeCases.DarkMacWindowHasEdgeLine()).IsTrue();

    [Test]
    public async Task TitleWidthIsNoNarrowerThanSans() => await Assert.That(WindowChromeCases.TitleWidthIsNoNarrowerThanSans()).IsTrue();
}

internal static class WindowChromeCases
{
internal static bool ScenarioResolveReadsStyle()
{
    var scenario = new Scenario
    {
        Render = new ScenarioRender { Window = "macos", WindowTitle = " My\tDemo ", Background = "#123, #abcdef", Margin = 20, Padding = 6, Opacity = 0.85, Blur = 8 },
    };
    return RenderSettingsResolver.TryResolve(scenario, null, out var settings, out var error)
        && error.Length == 0
        && settings.WindowTitle == "MyDemo"
        && settings.Background is { ImagePath: null } background
        && background.Colors.SequenceEqual(["#112233", "#abcdef"])
        && settings.Margin == 20
        && settings.Padding == 6
        && settings is { Opacity: 0.85, Blur: 8, WindowOpacity: 0.85, WindowBlur: 8 };
}

internal static bool ScenarioResolveRejectsBadStyle()
{
    var cases = new (ScenarioRender Render, string Expected)[]
    {
    (new ScenarioRender { Margin = 201 }, "invalid render.margin: must be between 0 and 200 pixels: 201"),
    (new ScenarioRender { Padding = -1 }, "invalid render.padding: must be between 0 and 100 pixels: -1"),
    (new ScenarioRender { Opacity = 1.5 }, "invalid render.opacity: must be between 0 and 1: 1.5"),
    (new ScenarioRender { Opacity = -0.1 }, "invalid render.opacity: must be between 0 and 1: -0.1"),
    (new ScenarioRender { Opacity = double.NaN }, "invalid render.opacity: must be between 0 and 1: NaN"),
    (new ScenarioRender { Blur = 51 }, "invalid render.blur: must be between 0 and 50 pixels: 51"),
    (new ScenarioRender { Background = "#12345" }, "invalid render.background: invalid background color (expected #rgb or #rrggbb): #12345"),
    (new ScenarioRender { Background = "#111 #222 #333 #444 #555" }, "background takes at most 4 colors"),
    // No scenario folder, as in the playground: an image cannot be read.
    (new ScenarioRender { Background = "wall.png" }, "background here takes colors only"),
    };

    var ok = true;
    foreach (var (render, expected) in cases)
    {
        var resolved = RenderSettingsResolver.TryResolve(new Scenario { Render = render }, null, out _, out var error);
        if (resolved || !error.Contains(expected, StringComparison.Ordinal))
        {
            Console.Error.WriteLine($"  expected \"{expected}\", got resolved={resolved} \"{error}\"");
            ok = false;
        }
    }

    return ok;
}

internal static bool BackgroundImagePathFollowsBaseFolder()
{
    var folder = Directory.CreateTempSubdirectory("st-background-").FullName;
    try
    {
        File.WriteAllBytes(Path.Combine(folder, "wall.png"), [0x89, 0x50, 0x4E, 0x47]);
        var found = RenderSettingsResolver.TryParseBackground("wall.png", BackgroundImageSource.FromFolder(folder), out var background, out var error)
            && background.ImagePath == Path.Combine(folder, "wall.png") && background.Colors.Count == 0;
        var missing = !RenderSettingsResolver.TryParseBackground("gone.png", BackgroundImageSource.FromFolder(folder), out _, out var missingError)
            && missingError.Contains("background image not found: gone.png (resolved to ", StringComparison.Ordinal);
        var wrongType = !RenderSettingsResolver.TryParseBackground("wall.bmp", BackgroundImageSource.FromFolder(folder), out _, out var typeError)
            && typeError.Contains(".png, .jpg, .gif, or .webp", StringComparison.Ordinal);
        if (!found || !missing || !wrongType)
            Console.Error.WriteLine($"  found={found} ({error}) missing={missing} ({missingError}) wrongType={wrongType} ({typeError})");
        return found && missing && wrongType;
    }
    finally
    {
        Directory.Delete(folder, recursive: true);
    }
}

internal static bool CastTagsReadStyle()
{
    // st:window-title wins over the cast's title; without it the cast's title shows. A value scenetake does not
    // accept is warned about and left out.
    var tagged = ReadHeader("\"title\":\"Cast Title\",\"tags\":[\"st:window=macos\",\"st:window-title=Bar Title\",\"st:background=#000000 #ffffff\",\"st:margin=5\",\"st:padding=3\",\"st:opacity=0.85\",\"st:blur=8\",\"st:loop=false\",\"st:end-hold=2.5\",\"st:fade-out=0.75\"]");
    var plain = ReadHeader("\"title\":\"Cast Title\",\"tags\":[\"st:window=macos\"]");
    var hidden = ReadHeader("\"title\":\"Cast Title\",\"tags\":[\"st:window-title=\"]");
    var invalid = ReadHeader("\"tags\":[\"st:margin=999\",\"st:padding=x\",\"st:background=wall.png\",\"st:opacity=2\",\"st:blur=-1\",\"st:loop=no\",\"st:end-hold=61\",\"st:fade-out=-1\"]");
    return tagged.Window == WindowStyle.Macos
        && tagged.WindowTitle == "Bar Title"
        && tagged.Background?.Colors.SequenceEqual(["#000000", "#ffffff"]) == true
        && tagged.Margin == 5 && tagged.Padding == 3
        && tagged is { Opacity: 0.85, Blur: 8, Loop: false, EndHold: 2.5, FadeOut: 0.75, Loops: false }
        && plain.WindowTitle == "Cast Title"
        && plain is { Opacity: null, Blur: null, Loop: null, EndHold: null, FadeOut: null, Loops: true }
        && hidden.WindowTitle == ""
        && invalid.Margin is null && invalid.Padding is null && invalid.Background is null
        && invalid is { Opacity: null, Blur: null, Loop: null, EndHold: null, FadeOut: null };

    static ResolvedRenderSettings ReadHeader(string fields)
    {
        var castPath = Path.Combine(Path.GetTempPath(), $"st-style-{Guid.NewGuid():N}.cast");
        try
        {
            File.WriteAllText(castPath,
                "{\"version\":3,\"term\":{\"cols\":40,\"rows\":6}," + fields + "}\n[0.0,\"o\",\"hello\\r\\n\"]\n");
            return CastReader.Read(castPath).RenderSettings;
        }
        finally
        {
            File.Delete(castPath);
        }
    }
}

internal static bool ChromeShowsTitle()
{
    var macos = RenderStyled(WindowStyle.Macos, title: "Demo <1> & \"2\"");
    var windows = RenderStyled(WindowStyle.Windows, title: "Demo");
    var plain = RenderStyled(WindowStyle.None, title: "Demo");
    var empty = RenderStyled(WindowStyle.Macos, title: "");
    // The title is in the system's sans-serif, bold on macOS as the system draws it.
    return macos.Contains(">Demo &lt;1&gt; &amp; &quot;2&quot;</text>", StringComparison.Ordinal)
        && macos.Contains("text-anchor=\"middle\"", StringComparison.Ordinal)
        && macos.Contains("style=\"font:bold 13px -apple-system,BlinkMacSystemFont,'SF Pro Text','Segoe UI',", StringComparison.Ordinal)
        && windows.Contains(">Demo</text>", StringComparison.Ordinal)
        && windows.Contains("style=\"font:12px -apple-system,", StringComparison.Ordinal)
        && !windows.Contains("text-anchor", StringComparison.Ordinal)
        && !plain.Contains(">Demo</text>", StringComparison.Ordinal)
        && !empty.Contains("13px -apple-system", StringComparison.Ordinal);
}

internal static bool FitWindowTitleCutsToRoom()
{
    // macOS keeps the buttons' width clear on both sides: 2 * (14 + 3*16 + 2*10 + 10) = 184 px, so 400 px leaves
    // 216. At 13 px an 'a' is 0.67 em, 8.71 px: 24 fit. Cut, 23 leave room for the 1.05 em ellipsis (13.65 px).
    var cut = SvgFrameRenderer.FitWindowTitle(new string('a', 40), WindowStyle.Macos, 400);
    var fits = SvgFrameRenderer.FitWindowTitle(new string('a', 24), WindowStyle.Macos, 400);
    var over = SvgFrameRenderer.FitWindowTitle(new string('a', 25), WindowStyle.Macos, 400);
    // 'W' is the widest letter, 1.13 em: 13 fit with the ellipsis.
    var widest = SvgFrameRenderer.FitWindowTitle(new string('W', 40), WindowStyle.Macos, 400);
    // A wide character is two cells of the terminal's font, 16.12 px, and is never split. A letter with a mark is
    // as wide as its base letter.
    var wide = SvgFrameRenderer.FitWindowTitle("日本語のタイトルです日本語のタイトルです", WindowStyle.Macos, 400);
    var accented = SvgFrameRenderer.FitWindowTitle(new string('é', 40), WindowStyle.Macos, 400);
    // Below room for two cells, 16.12 px, there is no title.
    var none = SvgFrameRenderer.FitWindowTitle("Demo", WindowStyle.Macos, 190);
    var windows = SvgFrameRenderer.FitWindowTitle("Demo", WindowStyle.Windows, 400);
    var ok = cut is { Text: var cutText, Centered: true, X: 200 } && cutText == new string('a', 23) + "…"
        && fits?.Text == new string('a', 24)
        && over?.Text == new string('a', 23) + "…"
        && widest?.Text == new string('W', 13) + "…"
        && wide?.Text == "日本語のタイトルです日本…"
        && accented?.Text == new string('é', 23) + "…"
        && none is null
        && windows is { Text: "Demo", Centered: false, X: 42 };
    if (!ok)
        Console.Error.WriteLine($"  cut={cut?.Text} fits={fits?.Text} over={over?.Text} widest={widest?.Text} wide={wide?.Text} accented={accented?.Text} none={none} windows={windows}");
    return ok;
}

internal static bool TitleWidthIsNoNarrowerThanSans()
{
    // Each ASCII width is at least what the widest common sans-serif in bold gives it (Verdana Bold, measured):
    // spot checks at 13 px.
    var checks = new (string Text, double MinEm)[] { (" ", 0.342), ("i", 0.342), ("m", 1.058), ("W", 1.128), ("M", 0.957), ("%", 1.272), ("0", 0.711) };
    var ok = true;
    foreach (var (text, minEm) in checks)
    {
        var width = SvgFrameRenderer.TitleWidth(text);
        if (width < minEm * 13)
        {
            Console.Error.WriteLine($"  '{text}': {width} px, below {minEm * 13} px");
            ok = false;
        }
    }

    // Outside ASCII: a base letter's width, W's for anything else.
    return ok
        && SvgFrameRenderer.TitleWidth("é") == SvgFrameRenderer.TitleWidth("e")
        && SvgFrameRenderer.TitleWidth("Ж") == SvgFrameRenderer.TitleWidth("W")
        && Math.Abs(SvgFrameRenderer.TitleWidth("日") - 16.12) < 0.001;
}

internal static bool WindowOnBackgroundCastsShadow()
{
    // On a background the window's whole shape casts a desktop window's shadow, filtered over the canvas. A
    // translucent window keeps it outside itself with a mask. Without a background the 1-pixel frame keeps its
    // faint shadow, as before.
    var background = new BackgroundSpec(["#4b6cb7", "#182848"], null);
    var opaque = RenderStyled(WindowStyle.Macos, background: background);
    var translucent = RenderStyled(WindowStyle.Windows, background: background, opacity: 0.85);
    var plain = RenderStyled(WindowStyle.Macos);
    var shape = new System.Text.RegularExpressions.Regex("<rect class=\"l [a-z0-9]+ viewport-chrome-0\" x=\"32\" y=\"32\" width=\"[0-9.]+\" height=\"[0-9.]+\" rx=\"8\" ry=\"8\" fill=\"#000\" filter=\"url\\(#window-shadow\\)\"/>");
    var maskedShape = new System.Text.RegularExpressions.Regex("rx=\"8\" ry=\"8\" fill=\"#000\" filter=\"url\\(#window-shadow\\)\" mask=\"url\\(#window-outside-0\\)\"/>");
    var ok = opaque.Contains("<feDropShadow dx=\"0\" dy=\"10\" stdDeviation=\"14\" flood-opacity=\"0.45\"/>", StringComparison.Ordinal)
        && opaque.Contains("<filter id=\"window-shadow\" filterUnits=\"userSpaceOnUse\" x=\"0\" y=\"0\"", StringComparison.Ordinal)
        && shape.IsMatch(opaque)
        // The frame no longer has a shadow of its own.
        && CountOccurrences(opaque, "filter=\"url(#window-shadow)\"") == 1
        && !opaque.Contains("window-outside", StringComparison.Ordinal)
        && maskedShape.IsMatch(translucent)
        && translucent.Contains("<mask id=\"window-outside-0\" maskUnits=\"userSpaceOnUse\"", StringComparison.Ordinal)
        && plain.Contains("<feDropShadow dx=\"0\" dy=\"2\" stdDeviation=\"4\" flood-opacity=\"0.22\"/>", StringComparison.Ordinal)
        && plain.Contains("fill=\"none\" stroke=\"#1a1a1a\" stroke-width=\"1\" filter=\"url(#window-shadow)\"/>", StringComparison.Ordinal)
        && !plain.Contains("fill=\"#000\"", StringComparison.Ordinal);
    if (!ok)
        Console.Error.WriteLine("  shadow markup not as expected");
    return ok;
}

internal static bool DarkMacWindowHasEdgeLine()
{
    // A dark macOS window has a faint white line just inside its edge, drawn after the terminal, whose background
    // reaches the edge. A light one and Windows windows have none.
    var dark = RenderStyled(WindowStyle.Macos);
    var light = RenderStyled(WindowStyle.Macos, lightTheme: true);
    var windows = RenderStyled(WindowStyle.Windows);
    var line = new System.Text.RegularExpressions.Regex("<rect class=\"l [a-z0-9]+ viewport-chrome-0\" x=\"12.5\" y=\"12.5\" width=\"[0-9.]+\" height=\"[0-9.]+\" rx=\"7.5\" ry=\"7.5\" fill=\"none\" stroke=\"#ffffff\" stroke-opacity=\"0.16\"/>");
    var match = line.Match(dark);
    return match.Success
        && match.Index > dark.IndexOf("</g>", StringComparison.Ordinal)
        && !light.Contains("stroke=\"#ffffff\"", StringComparison.Ordinal)
        && !windows.Contains("stroke=\"#ffffff\"", StringComparison.Ordinal);
}

internal static bool BackgroundMarginAndPaddingShapeCanvas()
{
    var chrome = RenderStyled(WindowStyle.Macos);
    var gradient = RenderStyled(WindowStyle.Macos, background: new BackgroundSpec(["#000000", "#808080", "#ffffff"], null));
    var solid = RenderStyled(WindowStyle.None, background: new BackgroundSpec(["#123456"], null));
    var margin = RenderStyled(WindowStyle.Macos, margin: 0);
    var padding = RenderStyled(WindowStyle.Macos, padding: 20);
    var width = ExtractSvgWidth(chrome);
    // With chrome the default margin is 12; a background makes it 32; margin 0 removes it. The default padding at
    // 16 px is 10 px across and 4 px down.
    return Math.Abs(ExtractSvgWidth(gradient) - (width + 40)) < 0.01
        && gradient.Contains("<linearGradient id=\"desktop\"", StringComparison.Ordinal)
        && CountOccurrences(gradient, "<stop ") == 3
        && gradient.Contains("fill=\"url(#desktop)\"", StringComparison.Ordinal)
        && solid.Contains("fill=\"#123456\"", StringComparison.Ordinal)
        && !solid.Contains("linearGradient", StringComparison.Ordinal)
        && Math.Abs(ExtractSvgWidth(margin) - (width - 24)) < 0.01
        && Math.Abs(ExtractSvgWidth(padding) - (width + 20)) < 0.01
        && Math.Abs(ExtractSvgHeight(padding) - (ExtractSvgHeight(chrome) + 32)) < 0.01;
}

internal static bool ParseOpacityAndBlur()
{
    var ok = RenderSettingsResolver.TryParseOpacity("0.85", out var opacity, out _) && opacity == 0.85
        && RenderSettingsResolver.TryParseOpacity(" 0 ", out var zero, out _) && zero == 0
        && RenderSettingsResolver.TryParseOpacity("1", out var one, out _) && one == 1
        && RenderSettingsResolver.TryParseBlur("8", out var blur, out _) && blur == 8
        && RenderSettingsResolver.TryParseBlur("0", out var noBlur, out _) && noBlur == 0
        && RenderSettingsResolver.FormatOpacity(0.85) == "0.85"
        && RenderSettingsResolver.FormatOpacity(1d / 3) == "0.333";
    var cases = new (bool Parsed, string Error, string Expected)[]
    {
    (RenderSettingsResolver.TryParseOpacity("1.5", out _, out var high), high, "opacity must be between 0 and 1: 1.5"),
    (RenderSettingsResolver.TryParseOpacity("-0.1", out _, out var low), low, "opacity must be between 0 and 1: -0.1"),
    (RenderSettingsResolver.TryParseOpacity("85%", out _, out var percent), percent, "invalid opacity value: 85% (expected a number from 0 to 1, such as 0.85)"),
    (RenderSettingsResolver.TryParseOpacity("NaN", out _, out var nan), nan, "invalid opacity value: NaN"),
    (RenderSettingsResolver.TryParseBlur("51", out _, out var wide), wide, "blur must be between 0 and 50 pixels: 51"),
    (RenderSettingsResolver.TryParseBlur("2.5", out _, out var fraction), fraction, "blur"),
    };
    foreach (var (parsed, error, expected) in cases)
    {
        if (parsed || !error.Contains(expected, StringComparison.Ordinal))
        {
            Console.Error.WriteLine($"  expected \"{expected}\", got parsed={parsed} \"{error}\"");
            ok = false;
        }
    }

    return ok;
}

internal static bool OpacityAndBlurNeedBackground()
{
    // Without a background the window stays opaque, so neither setting does anything, and both say so. Blur alone
    // has nothing to show through.
    var background = new BackgroundSpec(["#123456"], null);
    var plain = new ResolvedRenderSettings(16, RenderSettingsResolver.DefaultFontFamily, default, WindowStyle.None);
    string Notes(ResolvedRenderSettings settings) => string.Join(" | ", RenderSettingsResolver.FindIneffectiveStyle(settings));
    var cases = new (ResolvedRenderSettings Settings, string Expected)[]
    {
    (plain with { Opacity = 0.85 }, "opacity has no effect without a background: the window stays opaque"),
    (plain with { Blur = 8 }, "blur has no effect without a background: the window stays opaque"),
    (plain with { Opacity = 0.85, Blur = 8 }, "opacity and blur have no effect without a background: the window stays opaque"),
    (plain with { Background = background, Blur = 8 }, "blur has no effect on an opaque window: set an opacity below 1, such as 0.85"),
    (plain with { Background = background, Opacity = 1, Blur = 8 }, "blur has no effect on an opaque window: set an opacity below 1, such as 0.85"),
    (plain with { Background = background, Opacity = 0.85, Blur = 8 }, ""),
    (plain with { Background = background, Opacity = 0.85 }, ""),
    (plain with { Opacity = 1, Blur = 0 }, ""),
    };

    var ok = true;
    foreach (var (settings, expected) in cases)
    {
        if (Notes(settings) != expected)
        {
            Console.Error.WriteLine($"  expected \"{expected}\", got \"{Notes(settings)}\"");
            ok = false;
        }
    }

    return ok
        && (plain with { Opacity = 0.5, Blur = 8 }) is { WindowOpacity: 1, WindowBlur: 0 }
        && (plain with { Background = background, Blur = 8 }) is { WindowOpacity: 1, WindowBlur: 0 }
        && (plain with { Background = background, Opacity = 0.5, Blur = 8 }) is { WindowOpacity: 0.5, WindowBlur: 8 };
}

internal static bool TranslucentWindowSvg()
{
    // The terminal's default background and the title bar take the opacity; text, buttons, and the border do not.
    // With chrome, the terminal's square bottom corners are cut to the window's rounded ones.
    var background = new BackgroundSpec(["#123456"], null);
    var chrome = RenderStyled(WindowStyle.Macos, background: background, opacity: 0.8);
    var plain = RenderStyled(WindowStyle.None, background: background, opacity: 0.8);
    var opaque = RenderStyled(WindowStyle.Macos, background: background);
    var noBackground = RenderStyled(WindowStyle.Macos, opacity: 0.8);
    return CountOccurrences(chrome, "fill-opacity=\"0.8\"") == 2
        && chrome.Contains("viewport-bg-0\"", StringComparison.Ordinal)
        && System.Text.RegularExpressions.Regex.IsMatch(chrome, "viewport-bg-0\"[^>]*fill-opacity=\"0.8\" clip-path=\"url\\(#window-clip-0\\)\"/>")
        && chrome.Contains("<clipPath id=\"window-clip-0\"><rect x=\"32\" y=\"32\"", StringComparison.Ordinal)
        && chrome.Contains("rx=\"8\" ry=\"8\"/></clipPath>", StringComparison.Ordinal)
        && !chrome.Contains("frost", StringComparison.Ordinal)
        && !chrome.Contains("desktop-paint", StringComparison.Ordinal)
        // Without chrome the terminal is square, so nothing is clipped.
        && CountOccurrences(plain, "fill-opacity=\"0.8\"") == 1
        && !plain.Contains("clipPath", StringComparison.Ordinal)
        && !opaque.Contains("fill-opacity=\"", StringComparison.Ordinal)
        && !opaque.Contains("clipPath", StringComparison.Ordinal)
        && !noBackground.Contains("fill-opacity=\"", StringComparison.Ordinal);
}

internal static bool FrostedWindowSvg()
{
    // The background is defined once and drawn twice: sharp over the canvas, and blurred inside the window.
    var colors = RenderStyled(WindowStyle.Macos, background: new BackgroundSpec(["#000000", "#ffffff"], null), opacity: 0.8, blur: 6);
    var image = RenderStyled(WindowStyle.None, background: new BackgroundSpec([], "wall.png", [0x89, 0x50, 0x4E, 0x47]), opacity: 0.5, blur: 4);
    var opaque = RenderStyled(WindowStyle.Macos, background: new BackgroundSpec(["#123456"], null), blur: 6);
    return colors.Contains("<rect id=\"desktop-paint\" x=\"0\" y=\"0\"", StringComparison.Ordinal)
        && colors.Contains("fill=\"url(#desktop)\"", StringComparison.Ordinal)
        && colors.Contains("<feGaussianBlur stdDeviation=\"6\" edgeMode=\"duplicate\"/>", StringComparison.Ordinal)
        && colors.Contains("color-interpolation-filters=\"sRGB\"", StringComparison.Ordinal)
        && colors.Contains("<use href=\"#desktop-paint\"/>", StringComparison.Ordinal)
        && System.Text.RegularExpressions.Regex.IsMatch(colors, "<use href=\"#desktop-paint\" class=\"l [a-z0-9]+\" filter=\"url\\(#frost\\)\" clip-path=\"url\\(#window-clip-0\\)\"/>")
        // The frost is under the chrome: the blurred copy comes before the title bar.
        && colors.IndexOf("filter=\"url(#frost)\"", StringComparison.Ordinal) < colors.IndexOf("fill-opacity=\"0.8\"", StringComparison.Ordinal)
        // An image is embedded once, and the frost without chrome is cut to the square terminal.
        && CountOccurrences(image, "data:image/png;base64,") == 1
        && image.Contains("<image id=\"desktop-paint\" href=", StringComparison.Ordinal)
        && image.Contains("<clipPath id=\"window-clip-0\">", StringComparison.Ordinal)
        && image.Contains("rx=\"0\" ry=\"0\"/></clipPath>", StringComparison.Ordinal)
        && !opaque.Contains("frost", StringComparison.Ordinal);
}

internal static string RenderStyled(WindowStyle window, string? title = null, BackgroundSpec? background = null, int? margin = null, int? padding = null, double? opacity = null, int? blur = null, bool lightTheme = false)
{
    var castPath = Path.Combine(Path.GetTempPath(), $"st-styled-{Guid.NewGuid():N}.cast");
    try
    {
        File.WriteAllText(castPath, "{\"version\":3,\"term\":{\"cols\":40,\"rows\":6}}\n[0.0,\"o\",\"hello\\r\\n\"]\n");
        var recording = CastReader.Read(castPath);
        var settings = recording.RenderSettings with { Window = window, WindowTitle = title, Background = background, Margin = margin, Padding = padding, Opacity = opacity, Blur = blur };
        if (lightTheme)
            settings = settings with { Theme = settings.Theme with { Bg = "#ffffff", Fg = "#000000" } };
        return SvgRender.RenderToString(recording.Events, recording.Width, recording.Height, settings, recording.LoopDuration);
    }
    finally
    {
        File.Delete(castPath);
    }
}

internal static double ExtractSvgWidth(string svg)
{
    var marker = "width=\"";
    var start = svg.IndexOf(marker, svg.IndexOf("<svg", StringComparison.Ordinal), StringComparison.Ordinal) + marker.Length;
    var end = svg.IndexOf('"', start);
    return double.Parse(svg[start..end], System.Globalization.CultureInfo.InvariantCulture);
}


// A theme may give a color as #rgb, and the cast keeps it so: white is light, as #ffffff is, and the PNG, GIF, and mp4
// draw #f80 as #ff8800, not as the grey they fall back to for a color they cannot read.
internal static bool LightBackgroundTakesShortHex() =>
    SvgFrameRenderer.IsLightTerminalBg("#fff") && SvgFrameRenderer.IsLightTerminalBg("#FFFFFF")
    && !SvgFrameRenderer.IsLightTerminalBg("#000") && !SvgFrameRenderer.IsLightTerminalBg("#0d1117")
    && CellColors.TryParseHexColor("#f80", out var r, out var g, out var b) && (r, g, b) == (255, 136, 0)
    && !CellColors.TryParseHexColor("#f8g", out _, out _, out _);

// Bold white stays as the theme spells it, #fff included: the matrix-rain tint finds the theme's white by its text.
// A color bold does brighten comes back as #rrggbb.
internal static bool BoldKeepsAColorItCannotBrighten() =>
    CellColors.ApplyIntensity("#fff", bold: true, faint: false) == "#fff"
    && CellColors.ApplyIntensity("#ffffff", bold: true, faint: false) == "#ffffff"
    && CellColors.ApplyIntensity("#ccc", bold: true, faint: false) == "#f5f5f5"
    && CellColors.ApplyIntensity("#ccc", bold: false, faint: true) == "#999999";

internal static bool ParseWindowAcceptsKnownValues()
{
    return RenderSettingsResolver.TryParseWindow("none", out var none, out _) && none == WindowStyle.None
        && RenderSettingsResolver.TryParseWindow("macos", out var macos, out _) && macos == WindowStyle.Macos
        && RenderSettingsResolver.TryParseWindow("windows", out var windows, out _) && windows == WindowStyle.Windows;
}

internal static bool ParseWindowRejectsUnknownValue()
{
    return !RenderSettingsResolver.TryParseWindow("gnome", out _, out var error)
        && error.Contains("gnome", StringComparison.Ordinal);
}

internal static bool ScenarioResolveReadsWindow()
{
    var scenario = new Scenario
    {
        Render = new ScenarioRender
        {
            Window = "macos",
            Theme = new ScenarioTheme { Preset = "dark" },
        },
    };

    return RenderSettingsResolver.TryResolve(scenario, cliThemePreset: null, out var settings, out var error)
        && error.Length == 0
        && settings.Window == WindowStyle.Macos;
}

internal static bool ApplySvgOverridesWindowWins()
{
    var baseSettings = new ResolvedRenderSettings(
        16,
        RenderSettingsResolver.DefaultFontFamily,
        RenderSettingsResolver.TryGetPreset("dark", out var theme) ? theme : default,
        WindowStyle.None);

    var overridden = RenderSettingsResolver.ApplySvgOverrides(
        baseSettings,
        fontSizeOverride: null,
        fontFamilyOverride: null,
        themePresetOverride: null,
        windowOverride: WindowStyle.Windows,
        maxFpsOverride: null,
        out var error);

    return error.Length == 0 && overridden.Window == WindowStyle.Windows;
}

internal static bool CastTagReadsWindow()
{
    var castPath = Path.Combine(Path.GetTempPath(), $"st-window-{Guid.NewGuid():N}.cast");
    try
    {
        File.WriteAllText(castPath,
            """
        {"version":3,"term":{"cols":40,"rows":6,"type":"xterm-256color","theme":{"fg":"#d0d0d0","bg":"#282c34","palette":"#151515:#ac4142:#7e8e50:#e5b567:#6c99bb:#9f4e85:#7dd6cf:#d0d0d0:#505050:#ac4142:#7e8e50:#e5b567:#6c99bb:#9f4e85:#7dd6cf:#f5f5f5"}},"tags":["st:font-size=16","st:window=macos"]}
        [0.0,"o","hello\r\n"]
        [0.05,"x","0"]
        """);

        var recording = CastReader.Read(castPath);
        return recording.RenderSettings.Window == WindowStyle.Macos;
    }
    finally
    {
        if (File.Exists(castPath))
            File.Delete(castPath);
    }
}

internal static string RenderMinimal(string windowTagValue)
{
    var castPath = Path.Combine(Path.GetTempPath(), $"st-chrome-{Guid.NewGuid():N}.cast");
    try
    {
        var tags = windowTagValue.Length == 0
            ? "[\"st:font-size=16\"]"
            : $"[\"st:font-size=16\",\"st:window={windowTagValue}\"]";
        File.WriteAllText(castPath,
            "{\"version\":3,\"term\":{\"cols\":40,\"rows\":6,\"type\":\"xterm-256color\",\"theme\":{\"fg\":\"#d0d0d0\",\"bg\":\"#282c34\",\"palette\":\"#151515:#ac4142:#7e8e50:#e5b567:#6c99bb:#9f4e85:#7dd6cf:#d0d0d0:#505050:#ac4142:#7e8e50:#e5b567:#6c99bb:#9f4e85:#7dd6cf:#f5f5f5\"}},\"tags\":" + tags + "}\n" +
            "[0.0,\"o\",\"hello\\r\\n\"]\n" +
            "[0.05,\"x\",\"0\"]\n");

        var recording = CastReader.Read(castPath);
        return SvgRender.RenderToString(recording.Events, recording.Width, recording.Height, recording.RenderSettings, recording.LoopDuration);
    }
    finally
    {
        if (File.Exists(castPath))
            File.Delete(castPath);
    }
}

internal static bool MacosChromeAddsTrafficLights()
{
    var svg = RenderMinimal("macos");
    return svg.Contains("viewport-chrome-0", StringComparison.Ordinal)
        && svg.Contains("<circle", StringComparison.Ordinal)
        && svg.Contains("window-shadow", StringComparison.Ordinal)
        && CountOccurrences(svg, "<circle") >= 3;
}

internal static bool WindowsChromeIsATabRow()
{
    // A Windows Terminal tab row: the active tab in the terminal's color, an outlined terminal icon, the title in
    // the tab, and the minimize, maximize, and close glyphs. No macOS-style line under the bar, and no filled
    // buttons. The window is 40 cols, 416 px, at x = 12: the tab runs from 20 to 140, at least 120 wide.
    var dark = RenderStyled(WindowStyle.Windows, title: "Demo");
    var light = RenderStyled(WindowStyle.Windows, title: "Demo", lightTheme: true);
    var minimal = RenderMinimal("windows");
    var ok = dark.Contains("d=\"M 20,46 V 24 Q 20,18 26,18 H 134 Q 140,18 140,24 V 46 Z\" fill=\"#282c34\"/>", StringComparison.Ordinal)
        && dark.Contains("fill=\"#1c1c1c\"/>", StringComparison.Ordinal)
        && dark.Contains("width=\"14\" height=\"11\" rx=\"2\" ry=\"2\" fill=\"none\" stroke=\"#d6d6d6\"", StringComparison.Ordinal)
        && dark.Contains("x=\"54\" y=\"36.5\" style=\"font:12px -apple-system,", StringComparison.Ordinal)
        && dark.Contains("width=\"9\" height=\"9\" rx=\"1\" ry=\"1\" fill=\"none\" stroke=\"#d6d6d6\"", StringComparison.Ordinal)
        && !dark.Contains("<line class=\"l", StringComparison.Ordinal)
        && light.Contains("V 46 Z\" fill=\"#ffffff\"/>", StringComparison.Ordinal)
        && light.Contains("fill=\"#ebebeb\"/>", StringComparison.Ordinal)
        && light.Contains("stroke=\"#333333\"", StringComparison.Ordinal)
        && minimal.Contains("window-shadow", StringComparison.Ordinal);
    if (!ok)
        Console.Error.WriteLine("  Windows tab row markup not as expected");
    return ok;
}

internal static bool WinTabRowFitsItsTitle()
{
    // The tab is at least 120 px; a longer title widens it by the width that cuts titles (12 px here), and the
    // new-tab and dropdown buttons follow it while they fit. A narrow window drops them and the title, then the tab.
    var demo = SvgFrameRenderer.LayoutWinTabRow("Demo", 400);
    var longTitle = SvgFrameRenderer.LayoutWinTabRow("A longer window title", 600);
    var cut = SvgFrameRenderer.LayoutWinTabRow(new string('a', 80), 600);
    var narrow = SvgFrameRenderer.LayoutWinTabRow("Demo", 250);
    var tiny = SvgFrameRenderer.LayoutWinTabRow("Demo", 200);
    var longWidth = 34 + SvgFrameRenderer.MeasureTitle("A longer window title", 12) + 32;
    var ok = demo is { HasTab: true, TabLeft: 8, TabRight: 128, Title: "Demo", TitleX: 42, Extras: true }
        && Math.Abs(longTitle.TabRight - (8 + longWidth)) < 0.001 && longTitle.Extras
        && cut.Title?.EndsWith('…') == true && cut.Extras
        && cut.TabRight + 56 <= 600 - 3 * 46 - 10 + 0.001
        && narrow is { HasTab: true, Title: null, Extras: false }
        && !tiny.HasTab;
    if (!ok)
        Console.Error.WriteLine($"  demo={demo} long={longTitle} cut={cut} narrow={narrow} tiny={tiny}");
    return ok;
}

internal static bool ChromeIncreasesSvgHeight()
{
    var plain = RenderMinimal("");
    var macos = RenderMinimal("macos");
    var plainHeight = ExtractSvgHeight(plain);
    var macosHeight = ExtractSvgHeight(macos);
    return macosHeight > plainHeight + 20;
}

internal static bool PlainSvgHasNoChrome()
{
    var svg = RenderMinimal("");
    return !svg.Contains("<filter id=\"window-shadow\"", StringComparison.Ordinal)
        && !svg.Contains("<circle", StringComparison.Ordinal);
}

internal static double ExtractSvgHeight(string svg)
{
    var marker = "height=\"";
    var start = svg.IndexOf(marker, StringComparison.Ordinal);
    if (start < 0)
        return 0;
    start += marker.Length;
    var end = svg.IndexOf('"', start);
    return double.Parse(svg[start..end], System.Globalization.CultureInfo.InvariantCulture);
}

internal static int CountOccurrences(string text, string value)
{
    var count = 0;
    var index = 0;
    while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
    {
        count++;
        index += value.Length;
    }

    return count;
}
}
