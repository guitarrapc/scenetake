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

static string RenderCast(string castPath, int? maxFps = null)
{
    var recording = CastReader.Read(castPath);
    var theme = TerminalTheme.FromResolved(recording.RenderSettings.Theme);
    var (cw, ch) = TerminalReplay.ResolveCanvasSize(recording.Width, recording.Height, recording.Events);
    var frames = TerminalReplay.BuildFrames(recording.Events, recording.Width, recording.Height, cw, ch, theme);
    var render = maxFps is int fps
        ? recording.RenderSettings with { MaxFps = fps }
        : recording.RenderSettings;
    return SvgFrameRenderer.Render(frames, render, cw, ch);
}

static bool CurlOutputSurvivesFrameSampling()
{
    var svg = RenderCast("samples/basic.cast", maxFps: 12);
    return svg.Contains("% Total", StringComparison.Ordinal)
        && svg.Contains("HTTP/1.1 301 Moved Permanently", StringComparison.Ordinal)
        && svg.Contains("Dload  Upload", StringComparison.Ordinal);
}

static bool EchoOutputNoDuplicateCommandLine()
{
    var svg = RenderCast("samples/basic.cast");
    return CountVisibleLayersWithText(svg, 5.15, "$ echo &quot;Wait for 2 seconds...&quot;") <= 1;
}

static bool MarkerDelayAdvancesTiming()
{
    var recording = CastReader.Read("tests/marker-delay.cast");
    var outputs = recording.Events.Where(e => e.Kind == CastEventKind.Output).ToList();
    if (outputs.Count < 2)
        return false;

    if (Math.Abs(outputs[0].Time - 0.5) > 1e-6 || Math.Abs(outputs[1].Time - 1.5) > 1e-6)
        return false;

    var svg = RenderCast("tests/marker-delay.cast");
    return svg.Contains("animation-delay: 0.5s, 1.5s", StringComparison.Ordinal)
        && svg.Contains("animation-delay: 1.5s", StringComparison.Ordinal);
}

static int CountVisibleLayersWithText(string svg, double timeSeconds, string text)
{
    var styles = SvgRenderTestPatterns.LayerStylePattern.Matches(svg)
        .ToDictionary(
            m => int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture),
            m => (
                Show: double.Parse(m.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture),
                Hide: m.Groups[3].Success
                    ? double.Parse(m.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture)
                    : (double?)null));

    var count = 0;
    foreach (var match in SvgRenderTestPatterns.LayerTextPattern.Matches(svg).Cast<System.Text.RegularExpressions.Match>())
    {
        if (!match.Groups[2].Value.Contains(text, StringComparison.Ordinal))
            continue;

        var id = int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        if (!styles.TryGetValue(id, out var timing))
            continue;

        if (timing.Show <= timeSeconds && (timing.Hide is null || timing.Hide > timeSeconds))
            count++;
    }

    return count;
}

file static class SvgRenderTestPatterns
{
    internal static readonly System.Text.RegularExpressions.Regex LayerTextPattern = new(
        """<g class="layer layer-(\d+)">\s*<text[^>]*>(.*?)</text>""",
        System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.Compiled);

    internal static readonly System.Text.RegularExpressions.Regex LayerStylePattern = new(
        """\.layer-(\d+) \{ animation-name: layer-in(?:, layer-out)?; animation-delay: ([0-9.]+)s(?:, ([0-9.]+)s)?; \}""",
        System.Text.RegularExpressions.RegexOptions.Compiled);
}
