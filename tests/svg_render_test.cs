#:sdk Microsoft.NET.Sdk
#:property TargetFramework=net10.0
#:property Nullable=enable
#:property ImplicitUsings=enable
#:include ../Terminal.cs
#:include ../CastReader.cs
#:include SvgTestStubs.cs
#:include ../Svg.cs

var failures = 0;
failures += Run("CurlOutputSurvivesFrameSampling", CurlOutputSurvivesFrameSampling);
failures += Run("EchoOutputNoDuplicateCommandLine", EchoOutputNoDuplicateCommandLine);
failures += Run("MarkerDelayAdvancesTiming", MarkerDelayAdvancesTiming);

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

static bool CurlOutputSurvivesFrameSampling()
{
    var recording = CastReader.Read("samples/basic.cast");
    var theme = TerminalTheme.FromResolved(recording.RenderSettings.Theme);
    var (cw, ch) = TerminalReplay.ResolveCanvasSize(recording.Width, recording.Height, recording.Events);
    var frames = TerminalReplay.BuildFrames(recording.Events, recording.Width, recording.Height, cw, ch, theme);
    var render = recording.RenderSettings with { MaxFps = 12 };
    var svg = SvgFrameRenderer.Render(frames, render, cw, ch);

    return svg.Contains("% Total", StringComparison.Ordinal)
        && svg.Contains("HTTP/1.1 301 Moved Permanently", StringComparison.Ordinal)
        && svg.Contains("Dload  Upload", StringComparison.Ordinal);
}

static bool EchoOutputNoDuplicateCommandLine()
{
    var recording = CastReader.Read("samples/basic.cast");
    var theme = TerminalTheme.FromResolved(recording.RenderSettings.Theme);
    var (cw, ch) = TerminalReplay.ResolveCanvasSize(recording.Width, recording.Height, recording.Events);
    var frames = TerminalReplay.BuildFrames(recording.Events, recording.Width, recording.Height, cw, ch, theme);
    var svg = SvgFrameRenderer.Render(frames, recording.RenderSettings, cw, ch);

    var command = "$ echo &quot;Wait for 2 seconds...&quot;";
    var visibleAfterEnter = CountVisibleLayersWithText(svg, 5.15, command);
    return visibleAfterEnter <= 1;
}

static bool MarkerDelayAdvancesTiming()
{
    var recording = CastReader.Read("tests/marker-delay.cast");
    var outputs = recording.Events.Where(e => e.Kind == CastEventKind.Output).ToList();
    if (outputs.Count < 2)
        return false;

    if (Math.Abs(outputs[0].Time - 0.5) > 1e-6)
        return false;

    if (Math.Abs(outputs[1].Time - 1.5) > 1e-6)
        return false;

    var theme = TerminalTheme.FromResolved(recording.RenderSettings.Theme);
    var (cw, ch) = TerminalReplay.ResolveCanvasSize(recording.Width, recording.Height, recording.Events);
    var frames = TerminalReplay.BuildFrames(recording.Events, recording.Width, recording.Height, cw, ch, theme);
    var svg = SvgFrameRenderer.Render(frames, recording.RenderSettings, cw, ch);

    return svg.Contains("animation-delay: 0.5s, 1.5s", StringComparison.Ordinal)
        && svg.Contains("animation-delay: 1.5s", StringComparison.Ordinal);
}

static int CountVisibleLayersWithText(string svg, double timeSeconds, string text)
{
    var layerPattern = new System.Text.RegularExpressions.Regex(
        """<g class="layer layer-(\d+)">\s*<text[^>]*>(.*?)</text>""",
        System.Text.RegularExpressions.RegexOptions.Singleline);
    var stylePattern = new System.Text.RegularExpressions.Regex(
        """\.layer-(\d+) \{ animation-name: layer-in(?:, layer-out)?; animation-delay: ([0-9.]+)s(?:, ([0-9.]+)s)?; \}""");

    var styles = stylePattern.Matches(svg)
        .ToDictionary(
            m => int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture),
            m => (
                Show: double.Parse(m.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture),
                Hide: m.Groups[3].Success
                    ? double.Parse(m.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture)
                    : (double?)null));

    var count = 0;
    foreach (System.Text.RegularExpressions.Match match in layerPattern.Matches(svg))
    {
        if (!match.Groups[2].Value.Contains(text, StringComparison.Ordinal))
            continue;

        var id = int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        if (!styles.TryGetValue(id, out var timing))
            continue;

        if (timing.Show > timeSeconds)
            continue;

        if (timing.Hide is null || timing.Hide > timeSeconds)
            count++;
    }

    return count;
}
