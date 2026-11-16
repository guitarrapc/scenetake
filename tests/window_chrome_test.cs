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

return failures == 0 ? 0 : 1;

static bool ScenarioResolveReadsStyle()
{
    var scenario = new Scenario
    {
        Render = new ScenarioRender { Window = "macos", WindowTitle = " My\tDemo ", Background = "#123, #abcdef", Margin = 20, Padding = 6 },
    };
    return RenderSettingsResolver.TryResolve(scenario, null, out var settings, out var error)
        && error.Length == 0
        && settings.WindowTitle == "MyDemo"
        && settings.Background is { ImagePath: null } background
        && background.Colors.SequenceEqual(["#112233", "#abcdef"])
        && settings.Margin == 20
        && settings.Padding == 6;
}

static bool ScenarioResolveRejectsBadStyle()
{
    var cases = new (ScenarioRender Render, string Expected)[]
    {
        (new ScenarioRender { Margin = 201 }, "invalid render.margin: must be between 0 and 200 pixels: 201"),
        (new ScenarioRender { Padding = -1 }, "invalid render.padding: must be between 0 and 100 pixels: -1"),
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
        var found = RenderSettingsResolver.TryParseBackground("wall.png", folder, out var background, out var error)
            && background.ImagePath == Path.Combine(folder, "wall.png") && background.Colors.Count == 0;
        var missing = !RenderSettingsResolver.TryParseBackground("gone.png", folder, out _, out var missingError)
            && missingError.Contains("background image not found: gone.png (resolved to ", StringComparison.Ordinal);
        var wrongType = !RenderSettingsResolver.TryParseBackground("wall.bmp", folder, out _, out var typeError)
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
    var tagged = ReadHeader("\"title\":\"Cast Title\",\"tags\":[\"st:window=macos\",\"st:window-title=Bar Title\",\"st:background=#000000 #ffffff\",\"st:margin=5\",\"st:padding=3\"]");
    var plain = ReadHeader("\"title\":\"Cast Title\",\"tags\":[\"st:window=macos\"]");
    var hidden = ReadHeader("\"title\":\"Cast Title\",\"tags\":[\"st:window-title=\"]");
    var invalid = ReadHeader("\"tags\":[\"st:margin=999\",\"st:padding=x\",\"st:background=wall.png\"]");
    return tagged.Window == WindowStyle.Macos
        && tagged.WindowTitle == "Bar Title"
        && tagged.Background?.Colors.SequenceEqual(["#000000", "#ffffff"]) == true
        && tagged.Margin == 5 && tagged.Padding == 3
        && plain.WindowTitle == "Cast Title"
        && hidden.WindowTitle == ""
        && invalid.Margin is null && invalid.Padding is null && invalid.Background is null;

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

static string RenderStyled(WindowStyle window, string? title = null, BackgroundSpec? background = null, int? margin = null, int? padding = null)
{
    var castPath = Path.Combine(Path.GetTempPath(), $"st-styled-{Guid.NewGuid():N}.cast");
    try
    {
        File.WriteAllText(castPath, "{\"version\":3,\"term\":{\"cols\":40,\"rows\":6}}\n[0.0,\"o\",\"hello\\r\\n\"]\n");
        var recording = CastReader.Read(castPath);
        var settings = recording.RenderSettings with { Window = window, WindowTitle = title, Background = background, Margin = margin, Padding = padding };
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
