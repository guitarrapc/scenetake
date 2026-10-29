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
failures += Run("CurlOutputSurvivesFrameSampling", CurlOutputSurvivesFrameSampling);
failures += Run("EchoOutputNoDuplicateCommandLine", EchoOutputNoDuplicateCommandLine);
failures += Run("MarkerDelayAdvancesTiming", MarkerDelayAdvancesTiming);
failures += Run("SvgLoopsWithCastDuration", SvgLoopsWithCastDuration);
failures += Run("LoopDurationMatchesAggGifCycle", LoopDurationMatchesAggGifCycle);
failures += Run("MaxFpsDoesNotChangeLoopDuration", MaxFpsDoesNotChangeLoopDuration);
failures += Run("MatrixTintWhiteNearGreen", MatrixTintWhiteNearGreen);
failures += Run("MatrixTintIgnoresVerticalNeighbors", MatrixTintIgnoresVerticalNeighbors);
failures += Run("CopilotBannerBlockGlyphs", CopilotBannerBlockGlyphs);
failures += Run("WriteSvgMatchesRenderToString", WriteSvgMatchesRenderToString);
failures += Run("GlyphListForSparseColoredRow", GlyphListForSparseColoredRow);
failures += Run("RunFormForProse", RunFormForProse);
failures += Run("MultiCodeUnitCellsStayOutOfXLists", MultiCodeUnitCellsStayOutOfXLists);
failures += Run("BackgroundSpanCoversStyleChanges", BackgroundSpanCoversStyleChanges);
failures += Run("UnderlinedRowUsesRunForm", UnderlinedRowUsesRunForm);
failures += Run("RowLayersShareKeyframeRules", RowLayersShareKeyframeRules);
failures += Run("StepKeyframesAreCompact", StepKeyframesAreCompact);
failures += Run("ScrolledRowsReuseContent", ScrolledRowsReuseContent);
failures += Run("CursorRectsHaveNoWrapper", CursorRectsHaveNoWrapper);
failures += Run("CombiningMarkDoesNotFailRender", CombiningMarkDoesNotFailRender);
failures += Run("LargeSvgWarnsWithAlternatives", LargeSvgWarnsWithAlternatives);
failures += Run("DefaultFontFamilyPassesValidation", DefaultFontFamilyPassesValidation);
failures += Run("OwnCastReadsWithoutFontFamilyWarning", OwnCastReadsWithoutFontFamilyWarning);
failures += Run("TooManyFontFamiliesStillWarns", TooManyFontFamiliesStillWarns);

return failures == 0 ? 0 : 1;

// WriteSvg streams pre-encoded row layers to disk; it must produce exactly the bytes of the
// in-memory render, including resize shrink (canvas-sized copies) and --max-fps sampling.
static bool WriteSvgMatchesRenderToString()
{
    foreach (var (castPath, maxFps) in new[] { ("samples/resize.cast", 0), ("samples/pty.cast", 0), ("samples/pty.cast", 20) })
    {
        var recording = CastReader.Read(castPath);
        var render = recording.RenderSettings with { MaxFps = maxFps };
        var path = Path.Combine(Path.GetTempPath(), $"scenetake-write-svg-{Guid.NewGuid():N}.svg");
        try
        {
            SvgRender.WriteSvg(recording.Events, recording.Width, recording.Height, render, path, recording.LoopDuration);
            var expected = new System.Text.UTF8Encoding(false).GetBytes(
                SvgRender.RenderToString(recording.Events, recording.Width, recording.Height, render, recording.LoopDuration));
            if (!File.ReadAllBytes(path).AsSpan().SequenceEqual(expected))
                return false;
        }
        finally
        {
            File.Delete(path);
        }
    }

    return true;
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

static string RenderCast(string castPath, int? maxFps = null)
{
    var recording = CastReader.Read(castPath);
    var render = maxFps is int fps
        ? recording.RenderSettings with { MaxFps = fps }
        : recording.RenderSettings;
    return SvgRender.RenderToString(recording.Events, recording.Width, recording.Height, render, recording.LoopDuration);
}

static bool CurlOutputSurvivesFrameSampling()
{
    var rows = DecodeRowLayers(RenderCast("samples/basic.cast", maxFps: 12));
    return rows.Any(r => r.Text.Contains("% Total", StringComparison.Ordinal))
        && rows.Any(r => r.Text.Contains("HTTP/1.1 301 Moved Permanently", StringComparison.Ordinal))
        && rows.Any(r => r.Text.Contains("Dload  Upload", StringComparison.Ordinal));
}

static bool EchoOutputNoDuplicateCommandLine()
{
    // At 8.5s the echo command and its output are both on screen; the command line must show exactly once.
    // (The previous check at 5.15s with "<= 1" passed vacuously: the line is typed later in this recording.)
    var svg = RenderCast("samples/basic.cast");
    return CountVisibleLayersWithText(svg, 8.5, "$ echo \"Wait for 2 seconds...\"") == 1;
}

static bool GlyphListForSparseColoredRow()
{
    // Sparse glyphs in alternating colors: one <text> per color with an x per glyph beats one <text> per run.
    var svg = RenderInline(40, 2, """[0.0, "o", "\u001b[32mA\u001b[0m    \u001b[31mB\u001b[0m    \u001b[32mC\u001b[0m    \u001b[31mD\u001b[0m    \u001b[32mE\u001b[0m"]""");
    return SvgRenderTestPatterns.GlyphListPattern.IsMatch(svg)
        && DecodeRowLayers(svg).Any(r => r.Text == "A    B    C    D    E");
}

static bool RunFormForProse()
{
    var svg = RenderInline(40, 2, """[0.0, "o", "hello world, this is plain prose"]""");
    return !SvgRenderTestPatterns.GlyphListPattern.IsMatch(svg)
        && svg.Contains("textLength=", StringComparison.Ordinal)
        && !svg.Contains("font-weight=\"normal\"", StringComparison.Ordinal)
        && !svg.Contains("lengthAdjust=\"spacing\"", StringComparison.Ordinal)
        && DecodeRowLayers(svg).Any(r => r.Text == "hello world, this is plain prose");
}

static bool MultiCodeUnitCellsStayOutOfXLists()
{
    // Surrogate pairs and combining marks get their own <text>; browsers count x values for them differently.
    var svg = RenderInline(40, 2, """[0.0, "o", "\u001b[32mA\u001b[0m   \u001b[32m😀\u001b[0m   \u001b[32mB\u001b[0m   \u001b[32mé\u001b[0m   \u001b[32mC\u001b[0m   \u001b[32mD\u001b[0m"]""");
    var lists = SvgRenderTestPatterns.GlyphListPattern.Matches(svg).Select(m => m.Groups[2].Value).ToList();
    return lists.Count > 0
        && lists.All(text => !text.Contains('\ud83d') && !text.Contains('́'))
        && svg.Contains(">😀</text>", StringComparison.Ordinal)
        && svg.Contains(">é</text>", StringComparison.Ordinal);
}

static bool BackgroundSpanCoversStyleChanges()
{
    // One rect for "abcd" even though the foreground changes after "ab" (4 cells × 9.92px).
    var svg = RenderInline(10, 2, """[0.0, "o", "\u001b[41;37mab\u001b[32mcd\u001b[0m"]""");
    var rects = SvgRenderTestPatterns.RowRectPattern.Matches(svg);
    return rects.Count == 1 && rects[0].Groups[1].Value == "39.68"
        && svg.Contains(".bg,rect:not([class])", StringComparison.Ordinal);
}

static bool UnderlinedRowUsesRunForm()
{
    // The underline continues under spaces between words, which the glyph-list form omits.
    var svg = RenderInline(40, 2, """[0.0, "o", "\u001b[4;32mA\u001b[31m    B\u001b[32m    C\u001b[31m    D\u001b[32m    E\u001b[0m"]""");
    return !SvgRenderTestPatterns.GlyphListPattern.IsMatch(svg)
        && svg.Contains("text-decoration=\"underline\"", StringComparison.Ordinal);
}

static bool RowLayersShareKeyframeRules()
{
    // A full-screen redraw changes every row at once: those row layers share one keyframe class.
    var svg = RenderInline(10, 3, """
        [0.0, "o", "aaa\r\nbbb\r\nccc"]
        [1.0, "o", "\u001b[Hddd\r\neee\r\nfff"]
        """);
    var perKeyframe = DecodeRowLayers(svg).GroupBy(r => r.Keyframe).Select(g => g.Count()).ToList();
    return perKeyframe.SequenceEqual([3, 3]) && !svg.Contains(".layer-", StringComparison.Ordinal);
}

static bool StepKeyframesAreCompact()
{
    // Each keyframe lists only its switch points, at about 1ms precision, with step-end timing set once.
    var svg = RenderCast("samples/basic.cast");
    var keyframes = SvgRenderTestPatterns.KeyframePattern.Matches(svg);
    return keyframes.Count > 0
        && keyframes.All(m => SvgRenderTestPatterns.StepBodyPattern.IsMatch(m.Groups[2].Value))
        && keyframes.All(m => !System.Text.RegularExpressions.Regex.IsMatch(m.Groups[2].Value, @"\.\d{4}"))
        && svg.Contains("step-end infinite", StringComparison.Ordinal)
        && !svg.Contains("linear", StringComparison.Ordinal);
}

static bool ScrolledRowsReuseContent()
{
    // Output scrolls a 3-row terminal: "second line …" moves from row 1 to row 0 unchanged and is reused with <use>.
    var svg = RenderInline(40, 3, """
        [0.0, "o", "first line of the output\r\nsecond line of the output\r\n"]
        [0.5, "o", "third line of the output\r\n"]
        [1.0, "o", "fourth line of the output\r\n"]
        """);
    var rows = DecodeRowLayers(svg);
    return svg.Contains("<use href=\"#r", StringComparison.Ordinal)
        && svg.Contains("<g id=\"r", StringComparison.Ordinal)
        && rows.Count(r => r.Text == "second line of the output") >= 2
        && rows.Count(r => r.Text == "third line of the output") >= 2;
}

static bool CursorRectsHaveNoWrapper()
{
    var svg = RenderCast("samples/basic.cast");
    return SvgRenderTestPatterns.CursorPattern.IsMatch(svg)
        && !svg.Contains("cursor-layer", StringComparison.Ordinal);
}

static bool CombiningMarkDoesNotFailRender()
{
    // "e" + U+0301 shares one cell. 1.4.x threw here, treating the mark as a low surrogate.
    var svg = RenderInline(20, 2, """[0.0, "o", "café ok"]""");
    return DecodeRowLayers(svg).Any(r => r.Text.StartsWith("caf", StringComparison.Ordinal) && r.Text.Contains("́ ok", StringComparison.Ordinal));
}

static bool LargeSvgWarnsWithAlternatives()
{
    // Over 10MB warns; the suggestion depends on the entry point and on --max-fps already being set.
    var limit = SvgRender.LargeSvgBytes;
    var fromScenario = SvgRender.SizeWarning(28_215_000, maxFps: 0, fromCast: false);
    var fromCast = SvgRender.SizeWarning(28_215_000, maxFps: 0, fromCast: true);
    var sampled = SvgRender.SizeWarning(limit + 1, maxFps: 20, fromCast: true);
    return SvgRender.SizeWarning(limit, 0, fromCast: false) is null
        && SvgRender.SizeWarning(129_000, 0, fromCast: true) is null
        && fromScenario is not null
        && fromScenario.StartsWith("SVG is 28.2MB (over 10MB).", StringComparison.Ordinal)
        && fromScenario.Contains("as mp4 (--format mp4)", StringComparison.Ordinal)
        && fromScenario.Contains("with --max-fps 20", StringComparison.Ordinal)
        && fromCast is not null && fromCast.Contains("(scenetake mp4 <cast>)", StringComparison.Ordinal)
        && sampled is not null && sampled.Contains("with a lower --max-fps", StringComparison.Ordinal)
        && !sampled.Contains("--max-fps 20", StringComparison.Ordinal);
}

static bool DefaultFontFamilyPassesValidation() =>
    RenderSettingsResolver.TryParseFontFamily(RenderSettingsResolver.DefaultFontFamily, out var parsed, out _)
    && parsed == RenderSettingsResolver.DefaultFontFamily;

static bool OwnCastReadsWithoutFontFamilyWarning()
{
    // Casts written by scenetake record the default stack in st:font-family; reading one must not warn.
    var (recording, warnings) = ReadCastCapturingWarnings("samples/basic.cast");
    return !warnings.Contains("font-family", StringComparison.Ordinal)
        && recording.RenderSettings.FontFamily == RenderSettingsResolver.DefaultFontFamily;
}

static bool TooManyFontFamiliesStillWarns()
{
    var families = string.Join(", ", Enumerable.Range(1, RenderSettingsResolver.MaxFontFamilyCount + 1).Select(i => $"F{i}"));
    var path = Path.Combine(Path.GetTempPath(), $"scenetake-font-family-{Guid.NewGuid():N}.cast");
    try
    {
        File.WriteAllText(path, "{\"version\":3,\"term\":{\"cols\":10,\"rows\":2},\"tags\":[\"st:font-family=" + families + "\"]}\n[0.0, \"o\", \"x\"]\n");
        var (recording, warnings) = ReadCastCapturingWarnings(path);
        return warnings.Contains("invalid font-family", StringComparison.Ordinal)
            && recording.RenderSettings.FontFamily == RenderSettingsResolver.DefaultFontFamily;
    }
    finally
    {
        File.Delete(path);
    }
}

static (CastRecording Recording, string Warnings) ReadCastCapturingWarnings(string path)
{
    var original = Console.Error;
    using var captured = new StringWriter();
    Console.SetError(captured);
    try
    {
        return (CastReader.Read(path), captured.ToString());
    }
    finally
    {
        Console.SetError(original);
    }
}

static string RenderInline(int cols, int rows, string events)
{
    var path = Path.Combine(Path.GetTempPath(), $"scenetake-svg-inline-{Guid.NewGuid():N}.cast");
    try
    {
        File.WriteAllText(path, "{\"version\":3,\"term\":{\"cols\":" + cols + ",\"rows\":" + rows + ",\"type\":\"xterm-256color\"}}\n" + events + "\n");
        return RenderCast(path);
    }
    finally
    {
        File.Delete(path);
    }
}

/// <summary>
/// Rebuilds each row layer's visible text from either encoding: a run &lt;text&gt; places its
/// characters one cell apart from its x; a glyph-list &lt;text&gt; gives every character its own x.
/// A &lt;use&gt; layer takes the text of the content it references. Layers are numbered in
/// document order, and columns are relative to the leftmost glyph in the SVG.
/// </summary>
static List<(int Layer, string Keyframe, string Text)> DecodeRowLayers(string svg)
{
    var fontSize = double.Parse(SvgRenderTestPatterns.FontSizePattern.Match(svg).Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
    var cellWidth = fontSize * 0.62;
    var reusable = SvgRenderTestPatterns.ReusedContentPattern.Matches(svg).ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value);
    var layers = new List<(int Layer, string Keyframe, List<(double X, string Glyph)> Glyphs)>();
    var minX = double.MaxValue;
    foreach (System.Text.RegularExpressions.Match layer in SvgRenderTestPatterns.RowLayerPattern.Matches(svg))
    {
        var markup = layer.Groups["ref"].Success ? reusable[layer.Groups["ref"].Value] : layer.Groups["content"].Value;
        var glyphs = new List<(double X, string Glyph)>();
        foreach (System.Text.RegularExpressions.Match text in SvgRenderTestPatterns.TextPattern.Matches(markup))
        {
            var xs = text.Groups[1].Value.Split(' ').Select(v => double.Parse(v, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            var content = System.Net.WebUtility.HtmlDecode(text.Groups[2].Value);
            if (xs.Length > 1)
            {
                for (var i = 0; i < content.Length && i < xs.Length; i++)
                    glyphs.Add((xs[i], content[i].ToString()));
            }
            else
            {
                var elements = System.Globalization.StringInfo.GetTextElementEnumerator(content);
                for (var i = 0; elements.MoveNext(); i++)
                    glyphs.Add((xs[0] + i * cellWidth, elements.GetTextElement()));
            }
        }

        foreach (var (x, _) in glyphs)
            minX = Math.Min(minX, x);
        layers.Add((layers.Count, layer.Groups["k"].Value, glyphs));
    }

    var decoded = new List<(int Layer, string Keyframe, string Text)>();
    foreach (var (id, keyframe, glyphs) in layers)
    {
        var cells = new SortedDictionary<int, string>();
        foreach (var (x, glyph) in glyphs)
            cells[(int)Math.Round((x - minX) / cellWidth)] = glyph;

        var text = new System.Text.StringBuilder();
        foreach (var (col, glyph) in cells)
        {
            text.Append(' ', Math.Max(0, col - text.Length));
            text.Append(glyph);
        }

        decoded.Add((id, keyframe, text.ToString().Trim()));
    }

    return decoded;
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
    return svg.Contains("animation: 2.5s step-end infinite", StringComparison.Ordinal)
        && svg.Contains("@keyframes k0{", StringComparison.Ordinal)
        && svg.Contains(".k0{animation-name:k0}", StringComparison.Ordinal);
}

static bool LoopDurationMatchesAggGifCycle()
{
    var recording = CastReader.Read("samples/basic.cast");
    if (recording.Events.Count < 2)
        return false;

    // Verified against: agg samples/basic.cast --last-frame-duration 0 → 18.11s GIF loop.
    return Math.Abs(recording.LoopDuration - 18.11) < 0.05;
}

static bool SvgLoopsWithCastDuration()
{
    var recording = CastReader.Read("samples/basic.cast");
    var svg = RenderCast("samples/basic.cast");
    var durationText = recording.LoopDuration.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture);
    return svg.Contains($".l {{ opacity: 0; animation: {durationText}s step-end infinite; }}", StringComparison.Ordinal)
        && svg.Contains("@keyframes k0{", StringComparison.Ordinal);
}

static bool MatrixTintWhiteNearGreen()
{
    const string cast = """
        {"version":3,"term":{"cols":8,"rows":2,"type":"xterm-256color","theme":{"fg":"#d0d0d0","bg":"#282c34","palette":"#151515:#ac4142:#7e8e50:#e5b567:#6c99bb:#9f4e85:#7dd6cf:#d0d0d0:#505050:#ac4142:#7e8e50:#e5b567:#6c99bb:#9f4e85:#7dd6cf:#f5f5f5"}},"timestamp":0,"title":"matrix-tint","env":{}}
        [0.0, "o", "\u001b[32mA\u001b[37mB\u001b[39m"]
        """;

    var path = Path.Combine(Path.GetTempPath(), $"scenetake-matrix-tint-{Guid.NewGuid():N}.cast");
    try
    {
        File.WriteAllText(path, cast);
        var svg = RenderCast(path);
        return svg.Contains("fill=\"#7e8e50\"", StringComparison.OrdinalIgnoreCase)
            && !svg.Contains("fill=\"#d0d0d0\"", StringComparison.OrdinalIgnoreCase);
    }
    finally
    {
        File.Delete(path);
    }
}

static bool MatrixTintIgnoresVerticalNeighbors()
{
    const string cast = """
        {"version":3,"term":{"cols":16,"rows":3,"type":"xterm-256color","theme":{"fg":"#d0d0d0","bg":"#282c34","palette":"#151515:#ac4142:#7e8e50:#e5b567:#6c99bb:#9f4e85:#7dd6cf:#d0d0d0:#505050:#ac4142:#7e8e50:#e5b567:#6c99bb:#9f4e85:#7dd6cf:#f5f5f5"}},"timestamp":0,"title":"matrix-tint-vertical","env":{}}
        [0.0, "o", "$ \u001b[1;4;32mdate\u001b[0m\r\nFri, Jul  3, 2026\r\n"]
        """;

    var path = Path.Combine(Path.GetTempPath(), $"scenetake-matrix-tint-vertical-{Guid.NewGuid():N}.cast");
    try
    {
        File.WriteAllText(path, cast);
        var svg = RenderCast(path);
        return DecodeRowLayers(svg).Any(r => r.Text.Contains("Fri, Jul", StringComparison.Ordinal))
            && !svg.Contains(">i, J<", StringComparison.Ordinal)
            && CountVisibleGreenDateFragments(svg) == 0;
    }
    finally
    {
        File.Delete(path);
    }
}

static int CountVisibleGreenDateFragments(string svg)
{
    const string green = "fill=\"#7e8e50\"";
    var count = 0;
    var searchFrom = 0;
    while (true)
    {
        var fill = svg.IndexOf(green, searchFrom, StringComparison.OrdinalIgnoreCase);
        if (fill < 0)
            return count;

        var textStart = svg.IndexOf('>', fill) + 1;
        var textEnd = svg.IndexOf('<', textStart);
        if (textStart > 0 && textEnd > textStart)
        {
            var text = svg[textStart..textEnd];
            if (text.Contains("Fri", StringComparison.Ordinal)
                || text.Contains("Jul", StringComparison.Ordinal))
            {
                count++;
            }
        }

        searchFrom = fill + green.Length;
    }
}

static bool MaxFpsDoesNotChangeLoopDuration()
{
    var recording = CastReader.Read("samples/basic.cast");
    var durationText = recording.LoopDuration.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture);
    var full = RenderCast("samples/basic.cast");
    var sampled = RenderCast("samples/basic.cast", maxFps: 12);
    return full.Contains($"{durationText}s step-end infinite", StringComparison.Ordinal)
        && sampled.Contains($"{durationText}s step-end infinite", StringComparison.Ordinal);
}

static int CountVisibleLayersWithText(string svg, double timeSeconds, string text)
{
    var loopDuration = ParseLoopDuration(svg);
    var keyframes = SvgRenderTestPatterns.KeyframePattern.Matches(svg)
        .ToDictionary(
            m => m.Groups[1].Value,
            m => ParseCycleTiming(m.Groups[2].Value, loopDuration));
    var count = 0;
    foreach (var (_, keyframeName, rowText) in DecodeRowLayers(svg))
    {
        if (!rowText.Contains(text, StringComparison.Ordinal))
            continue;

        if (!keyframes.TryGetValue(keyframeName, out var timing))
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
    // Step keyframes: "S%{opacity:1}H%{opacity:0}" — visible from S until H (or the loop end).
    if (loopDuration <= 0)
        return (0, null);

    double? show = null;
    double? hide = null;
    foreach (System.Text.RegularExpressions.Match step in SvgRenderTestPatterns.StepPattern.Matches(keyframesBody))
    {
        var time = double.Parse(step.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) / 100.0 * loopDuration;
        if (step.Groups[2].Value == "1")
            show ??= time;
        else if (show is not null)
            hide ??= time;
    }

    return (show ?? 0, hide);
}

static bool CopilotBannerBlockGlyphs()
{
    const string cast = """
        {"version":3,"term":{"cols":40,"rows":6,"type":"xterm-256color","theme":{"fg":"#d0d0d0","bg":"#282c34","palette":"#151515:#ac4142:#7e8e50:#e5b567:#6c99bb:#9f4e85:#7dd6cf:#d0d0d0:#505050:#ac4142:#7e8e50:#e5b567:#6c99bb:#9f4e85:#7dd6cf:#f5f5f5"}},"timestamp":0,"title":"copilot-banner-glyphs","env":{}}
        [0.0, "o", "\u001b[38;2;133;52;243m  ╭─╮╭─╮\r\n  ╰─╯╰─╯  \u001b[38;2;145;152;161mCopilot v1.0.68 uses AI.\u001b[38;2;133;52;243m\r\n  █ \u001b[38;2;95;237;131m▘▝ \u001b[38;2;133;52;243m█  \u001b[38;2;145;152;161mCheck for mistakes.\u001b[38;2;133;52;243m\r\n   ▔▔▔▔ \u001b[m"]
        """;

    var path = Path.Combine(Path.GetTempPath(), $"scenetake-copilot-glyphs-{Guid.NewGuid():N}.cast");
    try
    {
        File.WriteAllText(path, cast);
        var svg = RenderCast(path);
        // Block glyphs render as text (same baseline as box drawing), not oversized rects.
        return DecodeRowLayers(svg).Any(r => r.Text.Contains("╭─╮╭─╮", StringComparison.Ordinal))
            && svg.Contains("▘", StringComparison.Ordinal)
            && svg.Contains("▝", StringComparison.Ordinal)
            && svg.Contains("▔", StringComparison.Ordinal)
            && svg.Contains("lengthAdjust=\"spacingAndGlyphs\"", StringComparison.Ordinal)
            && !svg.Contains("height=\"10\"", StringComparison.Ordinal);
    }
    finally
    {
        File.Delete(path);
    }
}

file static class SvgRenderTestPatterns
{
    // A row layer: <g class="l kN">content</g>, or <use href="#rM" y="…" class="l kN"/> reusing earlier content.
    internal static readonly System.Text.RegularExpressions.Regex RowLayerPattern = new(
        """<g class="l (?<k>k\d+)">(?<content>.*?)</g>|<use href="#(?<ref>r\d+)" y="[^"]*" class="l (?<k>k\d+)"/>""",
        System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.Compiled);

    // Content that later layers reuse, inside its first layer.
    internal static readonly System.Text.RegularExpressions.Regex ReusedContentPattern = new(
        """<g id="(r\d+)">(.*?)</g>""",
        System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.Compiled);

    internal static readonly System.Text.RegularExpressions.Regex CursorPattern = new(
        """<rect class="l k\d+ c" x="[0-9.]+" y="[0-9.]+" width="[0-9.]+" height="[0-9.]+"/>""",
        System.Text.RegularExpressions.RegexOptions.Compiled);

    internal static readonly System.Text.RegularExpressions.Regex TextPattern = new(
        """<text x="([^"]*)"[^>]*>(.*?)</text>""",
        System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.Compiled);

    // A <text> whose x attribute lists several positions (glyph-list form).
    internal static readonly System.Text.RegularExpressions.Regex GlyphListPattern = new(
        """<text x="([0-9.]+(?: [0-9.]+)+)"[^>]*>(.*?)</text>""",
        System.Text.RegularExpressions.RegexOptions.Compiled);

    // Background rects inside row layers carry no class; group 1 is the width.
    internal static readonly System.Text.RegularExpressions.Regex RowRectPattern = new(
        """<rect x="[0-9.]+" y="[0-9.]+" width="([0-9.]+)" height="[0-9.]+" fill="[^"]+"/>""",
        System.Text.RegularExpressions.RegexOptions.Compiled);

    internal static readonly System.Text.RegularExpressions.Regex FontSizePattern = new(
        """font-size: ([0-9]+)px""",
        System.Text.RegularExpressions.RegexOptions.Compiled);

    internal static readonly System.Text.RegularExpressions.Regex KeyframePattern = new(
        """^@keyframes (k\d+)\{(.+)\}\r?$""",
        System.Text.RegularExpressions.RegexOptions.Multiline | System.Text.RegularExpressions.RegexOptions.Compiled);

    internal static readonly System.Text.RegularExpressions.Regex DurationPattern = new(
        """animation: ([0-9.]+)s step-end infinite""",
        System.Text.RegularExpressions.RegexOptions.Compiled);

    internal static readonly System.Text.RegularExpressions.Regex StepPattern = new(
        """([0-9.]+)%\{opacity:([01])\}""",
        System.Text.RegularExpressions.RegexOptions.Compiled);

    // A whole keyframes body: a show step, optionally followed by a hide step.
    internal static readonly System.Text.RegularExpressions.Regex StepBodyPattern = new(
        """^[0-9.]+%\{opacity:1\}(?:[0-9.]+%\{opacity:0\})?$""",
        System.Text.RegularExpressions.RegexOptions.Compiled);
}
