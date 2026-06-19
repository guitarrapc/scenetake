#:sdk Microsoft.NET.Sdk
#:property TargetFramework=net10.0
#:property Nullable=enable
#:property ImplicitUsings=enable
#:include ../Terminal.cs
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
        var theme = TerminalTheme.FromResolved(recording.RenderSettings.Theme);
        var (cw, ch) = TerminalReplay.ResolveCanvasSize(recording.Width, recording.Height, recording.Events);
        var frames = TerminalReplay.BuildFrames(recording.Events, recording.Width, recording.Height, cw, ch, theme);
        return SvgFrameRenderer.Render(frames, recording.RenderSettings, cw, ch);
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
