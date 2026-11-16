#:sdk Microsoft.NET.Sdk
#:property TargetFramework=net10.0
#:property Nullable=enable
#:property ImplicitUsings=enable
#:include ../Terminal.cs
#:include ../CastTimingPipeline.cs
#:include ../CastReader.cs
#:include SvgTestStubs.cs
#:include ../Svg.cs

var failures = 0;
failures += Run("ParseWindowAcceptsKnownValues", ParseWindowAcceptsKnownValues);
failures += Run("ParseWindowRejectsUnknownValue", ParseWindowRejectsUnknownValue);
failures += Run("ScenarioResolveReadsWindow", ScenarioResolveReadsWindow);
failures += Run("ApplySvgOverridesWindowWins", ApplySvgOverridesWindowWins);
failures += Run("CastTagReadsWindow", CastTagReadsWindow);
failures += Run("MacosChromeAddsTrafficLights", MacosChromeAddsTrafficLights);
failures += Run("WindowsChromeAddsButtons", WindowsChromeAddsButtons);
failures += Run("ChromeIncreasesSvgHeight", ChromeIncreasesSvgHeight);
failures += Run("PlainSvgHasNoChrome", PlainSvgHasNoChrome);
failures += Run("ScenarioResolveReadsStyle", ScenarioResolveReadsStyle);
failures += Run("ScenarioResolveRejectsBadStyle", ScenarioResolveRejectsBadStyle);
failures += Run("BackgroundImagePathFollowsBaseFolder", BackgroundImagePathFollowsBaseFolder);
failures += Run("CastTagsReadStyle", CastTagsReadStyle);
failures += Run("ChromeShowsTitle", ChromeShowsTitle);
failures += Run("FitWindowTitleCutsToRoom", FitWindowTitleCutsToRoom);
failures += Run("BackgroundMarginAndPaddingShapeCanvas", BackgroundMarginAndPaddingShapeCanvas);
failures += Run("ParseOpacityAndBlur", ParseOpacityAndBlur);
failures += Run("OpacityAndBlurNeedBackground", OpacityAndBlurNeedBackground);
failures += Run("TranslucentWindowSvg", TranslucentWindowSvg);
failures += Run("FrostedWindowSvg", FrostedWindowSvg);

return failures == 0 ? 0 : 1;

static bool ScenarioResolveReadsStyle()
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

static bool ScenarioResolveRejectsBadStyle()
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

static bool BackgroundImagePathFollowsBaseFolder()
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

static bool CastTagsReadStyle()
{
    // st:window-title wins over the cast's title; without it the cast's title shows. A value scenetake does not
    // accept is warned about and left out.
    var tagged = ReadHeader("\"title\":\"Cast Title\",\"tags\":[\"st:window=macos\",\"st:window-title=Bar Title\",\"st:background=#000000 #ffffff\",\"st:margin=5\",\"st:padding=3\",\"st:opacity=0.85\",\"st:blur=8\"]");
    var plain = ReadHeader("\"title\":\"Cast Title\",\"tags\":[\"st:window=macos\"]");
    var hidden = ReadHeader("\"title\":\"Cast Title\",\"tags\":[\"st:window-title=\"]");
    var invalid = ReadHeader("\"tags\":[\"st:margin=999\",\"st:padding=x\",\"st:background=wall.png\",\"st:opacity=2\",\"st:blur=-1\"]");
    return tagged.Window == WindowStyle.Macos
        && tagged.WindowTitle == "Bar Title"
        && tagged.Background?.Colors.SequenceEqual(["#000000", "#ffffff"]) == true
        && tagged.Margin == 5 && tagged.Padding == 3
        && tagged is { Opacity: 0.85, Blur: 8 }
        && plain.WindowTitle == "Cast Title"
        && plain is { Opacity: null, Blur: null }
        && hidden.WindowTitle == ""
        && invalid.Margin is null && invalid.Padding is null && invalid.Background is null
        && invalid is { Opacity: null, Blur: null };

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

static bool ChromeShowsTitle()
{
    var macos = RenderStyled(WindowStyle.Macos, title: "Demo <1> & \"2\"");
    var windows = RenderStyled(WindowStyle.Windows, title: "Demo");
    var plain = RenderStyled(WindowStyle.None, title: "Demo");
    var empty = RenderStyled(WindowStyle.Macos, title: "");
    return macos.Contains(">Demo &lt;1&gt; &amp; &quot;2&quot;</text>", StringComparison.Ordinal)
        && macos.Contains("text-anchor=\"middle\"", StringComparison.Ordinal)
        && macos.Contains("style=\"font-size:13px\"", StringComparison.Ordinal)
        && windows.Contains(">Demo</text>", StringComparison.Ordinal)
        && !windows.Contains("text-anchor", StringComparison.Ordinal)
        && !plain.Contains(">Demo</text>", StringComparison.Ordinal)
        && !empty.Contains("font-size:13px", StringComparison.Ordinal);
}

static bool FitWindowTitleCutsToRoom()
{
    // macOS keeps the buttons' width clear on both sides: 2 * (14 + 3*16 + 2*10 + 10) = 184 px, and a cell of the
    // 13 px title is 8.06 px. At 400 px there is room for 26 cells.
    var cut = SvgFrameRenderer.FitWindowTitle(new string('a', 40), WindowStyle.Macos, 400);
    var fits = SvgFrameRenderer.FitWindowTitle(new string('a', 26), WindowStyle.Macos, 400);
    // A wide character takes two cells, and is never split.
    var wide = SvgFrameRenderer.FitWindowTitle("日本語のタイトルです日本語のタイトルです", WindowStyle.Macos, 400);
    var none = SvgFrameRenderer.FitWindowTitle("Demo", WindowStyle.Macos, 190);
    var windows = SvgFrameRenderer.FitWindowTitle("Demo", WindowStyle.Windows, 400);
    return cut is { Text: var cutText, Centered: true, X: 200 } && cutText == new string('a', 25) + "…"
        && fits?.Text == new string('a', 26)
        && wide?.Text == "日本語のタイトルです日本…"
        && none is null
        && windows is { Text: "Demo", Centered: false, X: 14 };
}

static bool BackgroundMarginAndPaddingShapeCanvas()
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

static bool ParseOpacityAndBlur()
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

static bool OpacityAndBlurNeedBackground()
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

static bool TranslucentWindowSvg()
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

static bool FrostedWindowSvg()
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

static string RenderStyled(WindowStyle window, string? title = null, BackgroundSpec? background = null, int? margin = null, int? padding = null, double? opacity = null, int? blur = null)
{
    var castPath = Path.Combine(Path.GetTempPath(), $"st-styled-{Guid.NewGuid():N}.cast");
    try
    {
        File.WriteAllText(castPath, "{\"version\":3,\"term\":{\"cols\":40,\"rows\":6}}\n[0.0,\"o\",\"hello\\r\\n\"]\n");
        var recording = CastReader.Read(castPath);
        var settings = recording.RenderSettings with { Window = window, WindowTitle = title, Background = background, Margin = margin, Padding = padding, Opacity = opacity, Blur = blur };
        return SvgRender.RenderToString(recording.Events, recording.Width, recording.Height, settings, recording.LoopDuration);
    }
    finally
    {
        File.Delete(castPath);
    }
}

static double ExtractSvgWidth(string svg)
{
    var marker = "width=\"";
    var start = svg.IndexOf(marker, svg.IndexOf("<svg", StringComparison.Ordinal), StringComparison.Ordinal) + marker.Length;
    var end = svg.IndexOf('"', start);
    return double.Parse(svg[start..end], System.Globalization.CultureInfo.InvariantCulture);
}

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

static bool ParseWindowAcceptsKnownValues()
{
    return RenderSettingsResolver.TryParseWindow("none", out var none, out _) && none == WindowStyle.None
        && RenderSettingsResolver.TryParseWindow("macos", out var macos, out _) && macos == WindowStyle.Macos
        && RenderSettingsResolver.TryParseWindow("windows", out var windows, out _) && windows == WindowStyle.Windows;
}

static bool ParseWindowRejectsUnknownValue()
{
    return !RenderSettingsResolver.TryParseWindow("gnome", out _, out var error)
        && error.Contains("gnome", StringComparison.Ordinal);
}

static bool ScenarioResolveReadsWindow()
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

static bool ApplySvgOverridesWindowWins()
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

static bool CastTagReadsWindow()
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

static string RenderMinimal(string windowTagValue)
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

static bool MacosChromeAddsTrafficLights()
{
    var svg = RenderMinimal("macos");
    return svg.Contains("viewport-chrome-0", StringComparison.Ordinal)
        && svg.Contains("<circle", StringComparison.Ordinal)
        && svg.Contains("window-shadow", StringComparison.Ordinal)
        && CountOccurrences(svg, "<circle") >= 3;
}

static bool WindowsChromeAddsButtons()
{
    var svg = RenderMinimal("windows");
    return svg.Contains("viewport-chrome-0", StringComparison.Ordinal)
        && CountOccurrences(svg, "viewport-chrome-0") >= 4
        && svg.Contains("window-shadow", StringComparison.Ordinal);
}

static bool ChromeIncreasesSvgHeight()
{
    var plain = RenderMinimal("");
    var macos = RenderMinimal("macos");
    var plainHeight = ExtractSvgHeight(plain);
    var macosHeight = ExtractSvgHeight(macos);
    return macosHeight > plainHeight + 20;
}

static bool PlainSvgHasNoChrome()
{
    var svg = RenderMinimal("");
    return !svg.Contains("<filter id=\"window-shadow\"", StringComparison.Ordinal)
        && !svg.Contains("<circle", StringComparison.Ordinal);
}

static double ExtractSvgHeight(string svg)
{
    var marker = "height=\"";
    var start = svg.IndexOf(marker, StringComparison.Ordinal);
    if (start < 0)
        return 0;
    start += marker.Length;
    var end = svg.IndexOf('"', start);
    return double.Parse(svg[start..end], System.Globalization.CultureInfo.InvariantCulture);
}

static int CountOccurrences(string text, string value)
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
