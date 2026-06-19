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
failures += Run("SvgLoopsWithCastDuration", SvgLoopsWithCastDuration);
failures += Run("MaxFpsDoesNotChangeLoopDuration", MaxFpsDoesNotChangeLoopDuration);

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
    return SvgFrameRenderer.Render(frames, render, cw, ch, recording.LoopDuration);
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
    return svg.Contains("animation:k", StringComparison.Ordinal)
        && svg.Contains("1.5s linear infinite", StringComparison.Ordinal)
        && svg.Contains("@keyframes k0", StringComparison.Ordinal);
}

static bool SvgLoopsWithCastDuration()
{
    var recording = CastReader.Read("samples/basic.cast");
    var svg = RenderCast("samples/basic.cast");
    var durationText = recording.LoopDuration.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture);
    return svg.Contains($"animation:k0 {durationText}s linear infinite", StringComparison.Ordinal)
        && svg.Contains("@keyframes k0", StringComparison.Ordinal);
}

static bool MaxFpsDoesNotChangeLoopDuration()
{
    var recording = CastReader.Read("samples/basic.cast");
    var durationText = recording.LoopDuration.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture);
    var full = RenderCast("samples/basic.cast");
    var sampled = RenderCast("samples/basic.cast", maxFps: 12);
    return full.Contains($"{durationText}s linear infinite", StringComparison.Ordinal)
        && sampled.Contains($"{durationText}s linear infinite", StringComparison.Ordinal);
}

static int CountVisibleLayersWithText(string svg, double timeSeconds, string text)
{
    var loopDuration = ParseLoopDuration(svg);
    var keyframes = SvgRenderTestPatterns.KeyframePattern.Matches(svg)
        .ToDictionary(
            m => m.Groups[1].Value,
            m => ParseCycleTiming(m.Groups[2].Value, loopDuration));
    var layerKeyframes = SvgRenderTestPatterns.LayerAnimationPattern.Matches(svg)
        .ToDictionary(
            m => int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture),
            m => m.Groups[2].Value);

    var count = 0;
    foreach (var match in SvgRenderTestPatterns.LayerTextPattern.Matches(svg).Cast<System.Text.RegularExpressions.Match>())
    {
        if (!match.Groups[2].Value.Contains(text, StringComparison.Ordinal))
            continue;

        var id = int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        if (!layerKeyframes.TryGetValue(id, out var keyframeName) || !keyframes.TryGetValue(keyframeName, out var timing))
            continue;

        if (timing.Show <= timeSeconds && (timing.Hide is null || timing.Hide > timeSeconds))
            count++;
    }

    return count;
}

static double ParseLoopDuration(string svg)
{
    var match = SvgRenderTestPatterns.DurationPattern.Match(svg);
    if (!match.Success)
        return 0;

    return double.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
}

static (double Show, double? Hide) ParseCycleTiming(string keyframesBody, double loopDuration)
{
    if (loopDuration <= 0)
        return (0, null);

    var ranges = SvgRenderTestPatterns.OpacityRangePattern.Matches(keyframesBody)
        .Select(m => (
            Start: double.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) / 100.0 * loopDuration,
            End: double.Parse(m.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture) / 100.0 * loopDuration,
            Opacity: int.Parse(m.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture)))
        .ToList();

    if (ranges.Count == 0)
        return (0, null);

    double? show = null;
    double? hide = null;
    foreach (var range in ranges)
    {
        if (range.Opacity == 1)
            show ??= range.Start;

        if (range.Opacity == 0 && show is not null && range.Start > show)
            hide ??= range.Start;
    }

    return (show ?? 0, hide);
}

file static class SvgRenderTestPatterns
{
    internal static readonly System.Text.RegularExpressions.Regex LayerTextPattern = new(
        """<g class="layer layer-(\d+)">\s*<text[^>]*>(.*?)</text>""",
        System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.Compiled);

    internal static readonly System.Text.RegularExpressions.Regex KeyframePattern = new(
        """^@keyframes (k\d+) \{ (.+) \}$""",
        System.Text.RegularExpressions.RegexOptions.Multiline | System.Text.RegularExpressions.RegexOptions.Compiled);

    internal static readonly System.Text.RegularExpressions.Regex LayerAnimationPattern = new(
        """\.layer-(\d+) \{ animation:(k\d+)""",
        System.Text.RegularExpressions.RegexOptions.Compiled);

    internal static readonly System.Text.RegularExpressions.Regex DurationPattern = new(
        """animation:k\d+ ([0-9.]+)s linear infinite""",
        System.Text.RegularExpressions.RegexOptions.Compiled);

    internal static readonly System.Text.RegularExpressions.Regex OpacityRangePattern = new(
        """([0-9.]+)%,([0-9.]+)%\{opacity:([01]);\}""",
        System.Text.RegularExpressions.RegexOptions.Compiled);
}
