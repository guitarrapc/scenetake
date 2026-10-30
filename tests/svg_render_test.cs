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
failures += Run("ShadesAreTranslucentRects", ShadesAreTranslucentRects);
failures += Run("WriteSvgMatchesRenderToString", WriteSvgMatchesRenderToString);
failures += Run("GlyphListForSparseColoredRow", GlyphListForSparseColoredRow);
failures += Run("RunFormForProse", RunFormForProse);
failures += Run("MultiCodeUnitCellsStayOutOfXLists", MultiCodeUnitCellsStayOutOfXLists);
failures += Run("BackgroundSpanCoversStyleChanges", BackgroundSpanCoversStyleChanges);
failures += Run("UnderlinedRowUsesRunForm", UnderlinedRowUsesRunForm);
failures += Run("RowLayersShareKeyframeRules", RowLayersShareKeyframeRules);
failures += Run("StepKeyframesAreCompact", StepKeyframesAreCompact);
failures += Run("ScrolledRowsReuseContent", ScrolledRowsReuseContent);
failures += Run("CursorJoinsRowLayers", CursorJoinsRowLayers);
failures += Run("SvgUsesLfLineEndings", SvgUsesLfLineEndings);
failures += Run("ThemeForegroundIsInherited", ThemeForegroundIsInherited);
failures += Run("SingleTextLayerHasNoWrapper", SingleTextLayerHasNoWrapper);
failures += Run("RowLocalContentDropsY", RowLocalContentDropsY);
failures += Run("KeyframeNamesAreShort", KeyframeNamesAreShort);
failures += Run("TypingRowsGetRowClasses", TypingRowsGetRowClasses);
failures += Run("RowsLandOnTheirLines", RowsLandOnTheirLines);
failures += Run("CursorSitsOnItsRow", CursorSitsOnItsRow);
failures += Run("EveryKeyframeIsUsed", EveryKeyframeIsUsed);
failures += Run("ClassNamesAreLowercase", ClassNamesAreLowercase);
failures += Run("ZeroLengthFramesAreDropped", ZeroLengthFramesAreDropped);
failures += Run("RunTextLengthCountsCells", RunTextLengthCountsCells);
failures += Run("UnderlinedSpacesKeepUnderline", UnderlinedSpacesKeepUnderline);
failures += Run("UnderlinedBlocksKeepUnderline", UnderlinedBlocksKeepUnderline);
failures += Run("PlainBlocksStayOutOfText", PlainBlocksStayOutOfText);
failures += Run("BlockOnlyRowsDecodeToTheirRow", BlockOnlyRowsDecodeToTheirRow);
failures += Run("XmlInvalidCharactersAreReplaced", XmlInvalidCharactersAreReplaced);
failures += Run("ScenarioThemeColorsAreValidated", ScenarioThemeColorsAreValidated);
failures += Run("CastWarningsSayCast", CastWarningsSayCast);
failures += Run("CombiningMarkDoesNotFailRender", CombiningMarkDoesNotFailRender);
failures += Run("LargeSvgWarnsWithAlternatives", LargeSvgWarnsWithAlternatives);
failures += Run("DefaultFontFamilyPassesValidation", DefaultFontFamilyPassesValidation);
failures += Run("OwnCastReadsWithoutFontFamilyWarning", OwnCastReadsWithoutFontFamilyWarning);
failures += Run("TooManyFontFamiliesStillWarns", TooManyFontFamiliesStillWarns);
failures += Run("SoftFontGlyphsAreUses", SoftFontGlyphsAreUses);
failures += Run("SoftFontThroughG0IsTheSame", SoftFontThroughG0IsTheSame);
failures += Run("UnderlinedSoftGlyphsKeepUnderline", UnderlinedSoftGlyphsKeepUnderline);
failures += Run("SoftGlyphIdsAvoidOtherIds", SoftGlyphIdsAvoidOtherIds);

return failures == 0 ? 0 : 1;

// WriteSvg streams pre-encoded row layers to disk. It must produce exactly the bytes of the
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

// Soft fonts (DECDLD). Glyphs are written as rows of '#' and '.', top row first.

/// <summary>Sixel data for one glyph: one band per six rows, one character per column, top pixel in bit 0.</summary>
static string SixelGlyph(string[] rows)
{
    var sb = new System.Text.StringBuilder();
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

/// <summary>A DECDLD string loading <paramref name="glyphs"/> from code 0x21, with a matrix the size of the first glyph.</summary>
static string Decdld(string name, params string[][] glyphs) =>
    $"\u001bP1;1;0;{glyphs[0][0].Length};0;2;{glyphs[0].Length};0{{" + name + string.Join(';', glyphs.Select(SixelGlyph)) + "\u001b\\";

/// <summary>An "o" event at <paramref name="time"/> with <paramref name="text"/> escaped for JSON.</summary>
static string OutputEvent(double time, string text)
{
    var sb = new System.Text.StringBuilder("[").Append(time.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(", \"o\", \"");
    foreach (var ch in text)
    {
        if (ch is '"' or '\\')
            sb.Append('\\').Append(ch);
        else if (ch < ' ')
            sb.Append("\\u").Append(((int)ch).ToString("x4", System.Globalization.CultureInfo.InvariantCulture));
        else
            sb.Append(ch);
    }

    return sb.Append("\"]").ToString();
}

static bool SoftFontGlyphsAreUses()
{
    // The in-box ConPTY form: the font in G1, its characters between SO and SI. Each glyph is one path in
    // <defs>, in its own pixels, scaled to the 9.92 × 20 cell and lifted by the 12.8 baseline offset. Each
    // cell is a <use> of it in the cell's color. The codes the program wrote (! and ") are not text.
    string[] corner = ["##...", "##...", "....#", ".....", ".....", "....."];
    string[] bar = ["#####", ".....", ".....", ".....", ".....", "....."];
    var svg = RenderInline(10, 2, OutputEvent(0, Decdld("@", corner, bar) + "\u001b)@\u001b[31m\u000E!\"!\u000F\u001b[0mx"));
    var paths = System.Text.RegularExpressions.Regex.Matches(svg,
        """<path id="([a-z0-9]+)" transform="translate\(0 -12.8\) scale\(1.984 3.33333\)" d="([^"]+)"/>""");
    var pathById = paths.ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value);
    var fills = SvgRenderTestPatterns.FillClassPattern.Matches(svg).ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value);
    var uses = SvgRenderTestPatterns.GlyphUsePattern.Matches(svg).ToList();
    return paths.Count == 2
        && uses.Count == 3
        && uses.Select(u => pathById.GetValueOrDefault(u.Groups[1].Value)).SequenceEqual(["M0 0h2v2h-2zM4 2h1v1h-1z", "M0 0h5v1h-5z", "M0 0h2v2h-2zM4 2h1v1h-1z"])
        && uses.Select(u => u.Groups[2].Value).SequenceEqual(["18", "27.92", "37.84"])
        && uses.All(u => string.Equals(fills.GetValueOrDefault(u.Groups[4].Value), "#ac4142", StringComparison.OrdinalIgnoreCase))
        && DecodeRowLayers(svg).Any(layer => layer is { Row: 0, Text: "□□□x" })
        && !System.Text.RegularExpressions.Regex.IsMatch(svg, """<text[^>]*>[^<]*(!|&quot;|")""");
}

static bool SoftFontThroughG0IsTheSame()
{
    // A program's own form (Unix, out-of-band ConPTY): the font named " @" in G0. It draws exactly as the in-box form.
    string[] corner = ["##...", "##...", "....#", ".....", ".....", "....."];
    var inBox = RenderInline(10, 2, OutputEvent(0, Decdld("@", corner) + "\u001b)@\u000E!\u000Fx"));
    var own = RenderInline(10, 2, OutputEvent(0, Decdld(" @", corner) + "\u001b( @!\u001b(Bx"));
    return inBox == own && SvgRenderTestPatterns.GlyphUsePattern.Matches(own).Count == 1;
}

static bool UnderlinedSoftGlyphsKeepUnderline()
{
    // Like a block element, an underlined glyph puts an underlined space in the run over its cell.
    string[] corner = ["##...", "##...", "....#", ".....", ".....", "....."];
    var svg = RenderInline(10, 2, OutputEvent(0, Decdld("@", corner) + "\u001b(@\u001b[4m!!\u001b(Bab\u001b[0m"));
    return svg.Contains("text-decoration=\"underline\" textLength=\"39.68\">  ab</text>", StringComparison.Ordinal)
        && SvgRenderTestPatterns.GlyphUsePattern.Matches(svg).Count == 2;
}

static bool SoftGlyphIdsAvoidOtherIds()
{
    // 40 different glyphs get 40 ids, none of them the cursor's "c" or starting with "r" like reused rows (r0, r1, …).
    var glyphs = Enumerable.Range(0, 40)
        .Select(k => Enumerable.Range(0, 12).Select(y => new string(Enumerable.Range(0, 5).Select(x => x == k % 5 && y == k / 5 ? '#' : '.').ToArray())).ToArray())
        .ToArray();
    var codes = new string(Enumerable.Range(0x21, 40).Select(c => (char)c).ToArray());
    var svg = RenderInline(40, 2, OutputEvent(0, Decdld("@", glyphs) + "\u001b(@" + codes));
    var ids = System.Text.RegularExpressions.Regex.Matches(svg, """<path id="([a-z0-9]+)" """).Select(m => m.Groups[1].Value).ToList();
    return ids.Count == 40 && ids.Distinct().Count() == 40 && ids[0] == "a"
        && ids.All(id => id != "c" && !id.StartsWith('r'))
        && SvgRenderTestPatterns.GlyphUsePattern.Matches(svg).Count == 40;
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
    // At 8.5s the echo command and its output are both on screen, so the command line must show exactly once.
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
    // Surrogate pairs and combining marks get their own <text>, because browsers count x values for them differently.
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
    return perKeyframe.SequenceEqual([3, 3]);
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

static bool CursorJoinsRowLayers()
{
    // One cursor rect in <defs>, and each position is a <use> of it. A position with the same timing as
    // a row layer on its row goes inside that layer, without timing of its own.
    var svg = RenderCast("samples/basic.cast");
    var uses = SvgRenderTestPatterns.CursorPattern.Matches(svg).Select(m => m.Value).ToList();
    return svg.Split("<rect id=\"c\" class=\"c\"").Length == 2
        && uses.Count(u => !u.Contains("class=", StringComparison.Ordinal)) > uses.Count / 2
        && uses.Any(u => u.Contains("class=\"l ", StringComparison.Ordinal));
}

static bool CombiningMarkDoesNotFailRender()
{
    // "e" + U+0301 shares one cell. Its second UTF-16 unit is not a low surrogate, so reading it as one throws.
    var svg = RenderInline(20, 2, """[0.0, "o", "café ok"]""");
    return DecodeRowLayers(svg).Any(r => r.Text.StartsWith("caf", StringComparison.Ordinal) && r.Text.Contains("́ ok", StringComparison.Ordinal));
}

static bool LargeSvgWarnsWithAlternatives()
{
    // Over 10MB warns. The suggestion depends on the entry point and on --max-fps already being set.
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
    // Casts written by scenetake record the default stack in st:font-family, and reading one must not warn.
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

/// <summary>The top edge of terminal row <paramref name="row"/>, from the first viewport and the inner padding.</summary>
static double RowTop(string svg, int row)
{
    var inv = System.Globalization.CultureInfo.InvariantCulture;
    var fontSize = double.Parse(SvgRenderTestPatterns.FontSizePattern.Match(svg).Groups[1].Value, inv);
    var terminalTop = double.Parse(SvgRenderTestPatterns.ViewportBackgroundPattern.Match(svg).Groups[1].Value, inv);
    return terminalTop + Math.Clamp(fontSize * 4 / 16, 2, 8) + row * fontSize * 1.25;
}

/// <summary>
/// Rebuilds each row layer's visible text from either encoding. A run &lt;text&gt; places its
/// characters one cell apart from its x, and a glyph-list &lt;text&gt; gives every character its own x.
/// A &lt;use&gt; layer takes the text of the content it references. Layers are numbered in
/// document order, and columns are relative to the leftmost glyph in the SVG. Row is the terminal
/// row the layer is drawn on, from its row class, its own y, or the source row plus a &lt;use&gt;'s dy.
/// </summary>
static List<(int Layer, string Keyframe, string Text, int Row)> DecodeRowLayers(string svg)
{
    var inv = System.Globalization.CultureInfo.InvariantCulture;
    var fontSize = double.Parse(SvgRenderTestPatterns.FontSizePattern.Match(svg).Groups[1].Value, inv);
    var cellWidth = fontSize * 0.62;
    var lineHeight = fontSize * 1.25;
    var baselineOffset = fontSize * 0.8;
    var terminalTop = double.Parse(SvgRenderTestPatterns.ViewportBackgroundPattern.Match(svg).Groups[1].Value, inv);
    var firstBaseline = terminalTop + Math.Clamp(fontSize * 4 / 16, 2, 8) + baselineOffset;
    var rowRules = SvgRenderTestPatterns.RowClassPattern.Matches(svg).ToDictionary(m => m.Groups[1].Value, m => double.Parse(m.Groups[2].Value, inv));
    var reusable = SvgRenderTestPatterns.ReusedContentPattern.Matches(svg).ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value);
    var baselineById = new Dictionary<string, double>();
    var layers = new List<(int Layer, string Keyframe, List<(double X, string Glyph)> Glyphs, double Baseline)>();
    var minX = double.MaxValue;
    foreach (System.Text.RegularExpressions.Match layer in SvgRenderTestPatterns.RowLayerPattern.Matches(svg))
    {
        var classes = layer.Groups["cls"].Value.Split(' ');
        var rowClass = classes.Skip(2).FirstOrDefault(rowRules.ContainsKey);
        var markup = layer.Groups["ref"].Success ? reusable[layer.Groups["ref"].Value] : layer.Groups["content"].Value;
        double baseline;
        if (rowClass is not null)
            baseline = rowRules[rowClass];
        else if (layer.Groups["ref"].Success)
            baseline = baselineById[layer.Groups["ref"].Value] + double.Parse(layer.Groups["dy"].Value, inv);
        else if (SvgRenderTestPatterns.TextYPattern.Match(markup) is { Success: true } textY)
            baseline = double.Parse(textY.Groups[1].Value, inv);
        else if (SvgRenderTestPatterns.GlyphUsePattern.Match(markup) is { Success: true, Groups: var useGroups } && useGroups[3].Success)
            baseline = double.Parse(useGroups[3].Value, inv);
        else if (SvgRenderTestPatterns.RectYPattern.Match(markup) is { Success: true } rectY)
        {
            // A block rect starts anywhere in its cell (▄ at half the line, ▁ at 7/8), so its row is the line it starts in.
            var firstTop = firstBaseline - baselineOffset;
            baseline = firstBaseline + Math.Floor((double.Parse(rectY.Groups[1].Value, inv) - firstTop) / lineHeight + 1e-6) * lineHeight;
        }
        else
            baseline = double.NaN;

        if (SvgRenderTestPatterns.ReuseIdPattern.Match(markup) is { Success: true } id)
            baselineById[id.Groups[1].Value] = baseline;

        var glyphs = new List<(double X, string Glyph)>();
        foreach (System.Text.RegularExpressions.Match text in SvgRenderTestPatterns.TextPattern.Matches(markup))
        {
            var xs = text.Groups[2].Value.Split(' ').Select(v => double.Parse(v, inv)).ToArray();
            var content = System.Net.WebUtility.HtmlDecode(text.Groups[3].Value);
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

        // A soft font glyph is a <use> of its path at its cell's x. It decodes as □.
        foreach (System.Text.RegularExpressions.Match use in SvgRenderTestPatterns.GlyphUsePattern.Matches(markup))
            glyphs.Add((double.Parse(use.Groups[2].Value, inv), "□"));

        foreach (var (x, _) in glyphs)
            minX = Math.Min(minX, x);
        layers.Add((layers.Count, classes[1], glyphs, baseline));
    }

    var decoded = new List<(int Layer, string Keyframe, string Text, int Row)>();
    foreach (var (id, keyframe, glyphs, baseline) in layers)
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

        var row = double.IsNaN(baseline) ? -1 : (int)Math.Round((baseline - firstBaseline) / lineHeight);
        decoded.Add((id, keyframe, text.ToString().Trim(), row));
    }

    return decoded;
}

/// <summary>The text each row shows at <paramref name="timeSeconds"/>, from the layers visible then.</summary>
static Dictionary<int, string> VisibleRowsAt(string svg, double timeSeconds)
{
    var keyframes = ParseKeyframeTimings(svg);
    var rows = new Dictionary<int, string>();
    foreach (var (_, keyframe, text, row) in DecodeRowLayers(svg))
    {
        if (keyframes.TryGetValue(keyframe, out var timing) && timing.Show <= timeSeconds && (timing.Hide is null || timing.Hide > timeSeconds))
            rows[row] = text;
    }

    return rows;
}

static Dictionary<string, (double Show, double? Hide)> ParseKeyframeTimings(string svg)
{
    var loopDuration = ParseLoopDuration(svg);
    return SvgRenderTestPatterns.KeyframePattern.Matches(svg).ToDictionary(m => m.Groups[1].Value, m => ParseCycleTiming(m.Groups[2].Value, loopDuration));
}

/// <summary>
/// Every &lt;text&gt; with the color it renders in: its fill class's color, or the color the
/// enclosing group passes down when it has none.
/// </summary>
static List<(string Fill, string Text)> DecodeTextFills(string svg)
{
    var defaultFill = SvgRenderTestPatterns.DefaultFillPattern.Match(svg).Groups[1].Value;
    var fills = SvgRenderTestPatterns.FillClassPattern.Matches(svg).ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value);
    var result = new List<(string Fill, string Text)>();
    foreach (System.Text.RegularExpressions.Match text in SvgRenderTestPatterns.TextPattern.Matches(svg))
    {
        var fill = text.Groups[1].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(c => fills.GetValueOrDefault(c)).FirstOrDefault(f => f is not null) ?? defaultFill;
        result.Add((fill, System.Net.WebUtility.HtmlDecode(text.Groups[3].Value)));
    }

    return result;
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
    var keyframe = SvgRenderTestPatterns.KeyframePattern.Match(svg);
    return svg.Contains("animation: 2.5s step-end infinite", StringComparison.Ordinal)
        && keyframe.Success
        && svg.Contains($".{keyframe.Groups[1].Value}{{animation-name:{keyframe.Groups[1].Value}}}", StringComparison.Ordinal);
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
        && SvgRenderTestPatterns.KeyframePattern.IsMatch(svg);
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
        // Both A (green) and B (white next to green) render green, and nothing renders in the theme's foreground.
        var texts = DecodeTextFills(RenderCast(path));
        return texts.Count > 0
            && texts.All(t => string.Equals(t.Fill, "#7e8e50", StringComparison.OrdinalIgnoreCase))
            && string.Concat(texts.Select(t => t.Text)).Contains('A') && string.Concat(texts.Select(t => t.Text)).Contains('B');
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

static int CountVisibleGreenDateFragments(string svg) =>
    DecodeTextFills(svg).Count(t => string.Equals(t.Fill, "#7e8e50", StringComparison.OrdinalIgnoreCase)
        && (t.Text.Contains("Fri", StringComparison.Ordinal) || t.Text.Contains("Jul", StringComparison.Ordinal)));

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
    foreach (var (_, keyframeName, rowText, _) in DecodeRowLayers(svg))
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
        // Block elements are rects covering their exact part of the cell (9.92 × 20px at 16px), not
        // whole cells: ▘ and ▝ are quarter cells, ▔▔▔▔ is one merged rect an eighth of a line high.
        // Box drawing stays text.
        var rects = SvgRenderTestPatterns.RowRectPattern.Matches(svg).Select(m => (Width: m.Groups[1].Value, Height: m.Groups[2].Value, Fill: m.Groups[3].Value)).ToList();
        return DecodeRowLayers(svg).Any(r => r.Text.Contains("╭─╮╭─╮", StringComparison.Ordinal))
            && !svg.Contains('▘') && !svg.Contains('▝') && !svg.Contains('▔') && !svg.Contains('█')
            && !svg.Contains("lengthAdjust", StringComparison.Ordinal)
            && rects.Count(r => r == ("4.96", "10", "#5FED83") || r == ("4.96", "10", "#5fed83")) == 2
            && rects.Count(r => r.Width == "39.68" && r.Height == "2.5") == 1
            && rects.Count(r => r.Width == "9.92" && r.Height == "20") == 2;
    }
    finally
    {
        File.Delete(path);
    }
}

static bool ShadesAreTranslucentRects()
{
    // ░▒▓ as 25%, 50%, and 75% opacity, as the mp4 rasterizer draws them. Different opacities do not merge.
    var svg = RenderInline(10, 2, """[0.0, "o", "░▒▓"]""");
    var opacities = SvgRenderTestPatterns.RowRectPattern.Matches(svg).Select(m => m.Groups[4].Value).ToList();
    return opacities.SequenceEqual(["0.25", "0.5", "0.75"]) && !svg.Contains('░');
}

static bool SvgUsesLfLineEndings()
{
    var svg = RenderCast("samples/basic.cast");
    return !svg.Contains('\r') && svg.Contains("</style>\n<defs>\n", StringComparison.Ordinal);
}

static bool ThemeForegroundIsInherited()
{
    // Text in the theme's foreground has no fill of its own. The mask group passes it down.
    // Other colors are classes with one rule each.
    var svg = RenderInline(40, 2, """[0.0, "o", "plain \u001b[31mred\u001b[0m plain"]""");
    var texts = DecodeTextFills(svg);
    return SvgRenderTestPatterns.DefaultFillPattern.Match(svg).Groups[1].Value == "#d0d0d0"
        && !System.Text.RegularExpressions.Regex.IsMatch(svg, "<text[^>]* fill=")
        && texts.Any(t => t.Fill == "#d0d0d0" && t.Text.Contains("plain", StringComparison.Ordinal))
        && texts.Any(t => t.Fill != "#d0d0d0" && t.Text == "red")
        && SvgRenderTestPatterns.FillClassPattern.Matches(svg).Count == 1;
}

static bool SingleTextLayerHasNoWrapper()
{
    var svg = RenderCast("samples/basic.cast");
    return System.Text.RegularExpressions.Regex.IsMatch(svg, """^<text class="l [A-Za-z]""", System.Text.RegularExpressions.RegexOptions.Multiline)
        && !System.Text.RegularExpressions.Regex.IsMatch(svg, """<g class="l [^"]*"><text[^>]*>[^<]*</text></g>""");
}

static bool RowLocalContentDropsY()
{
    // Row-local layers (those with a row class) draw at y=0, so none of their <text> may carry y.
    // The encoders decide y, and the writers then add the layer classes: one path for grouped layers and
    // two for unwrapped ones, with and without a fill class of their own. pty.cast has grouped layers
    // with and without fill-class text and unwrapped layers in the theme color. Green typing with the
    // cursor hidden adds unwrapped layers with a fill class, because a cursor would put each in a group.
    static List<System.Text.RegularExpressions.Match> LocalLayers(string svg)
    {
        var rowClasses = SvgRenderTestPatterns.RowClassPattern.Matches(svg).Select(m => m.Groups[1].Value).ToHashSet();
        return SvgRenderTestPatterns.RowLayerPattern.Matches(svg)
            .Where(m => !m.Groups["ref"].Success && m.Groups["cls"].Value.Split(' ').Skip(2).Any(rowClasses.Contains))
            .ToList();
    }

    var typed = new System.Text.StringBuilder("[0.0, \"o\", \"\\u001b[?25l\\u001b[32m\"]\n");
    for (var i = 0; i < 12; i++)
        typed.Append(System.Globalization.CultureInfo.InvariantCulture, $"[{0.1 * (i + 1):0.0}, \"o\", \"{(char)('a' + i)}\"]\n");
    var pty = LocalLayers(RenderCast("samples/pty.cast"));
    var green = LocalLayers(RenderInline(20, 2, typed.ToString()));
    static bool Grouped(System.Text.RegularExpressions.Match m) => m.Value.StartsWith("<g ", StringComparison.Ordinal);
    return pty.Any(m => Grouped(m) && m.Value.Contains("<text class=\"", StringComparison.Ordinal))
        && pty.Any(m => Grouped(m) && !m.Value.Contains("<text class=\"", StringComparison.Ordinal))
        && pty.Any(m => !Grouped(m) && m.Groups["cls"].Value.Split(' ').Length == 3)
        && green.Any(m => !Grouped(m) && m.Groups["cls"].Value.Split(' ').Length == 4)
        && pty.Concat(green).All(m => !SvgRenderTestPatterns.TextYPattern.IsMatch(m.Value));
}

static bool TypingRowsGetRowClasses()
{
    // A typed line saves about 10 bytes per layer as row-local content, less than its row's CSS rule.
    // Rows get a class only because those savings are added up per row.
    return SvgRenderTestPatterns.RowClassPattern.Matches(RenderCast("samples/basic.cast")).Count > 0;
}

static bool RowsLandOnTheirLines()
{
    // Six lines of sparse two-color glyphs scroll through a 3-row terminal, so rows get row classes
    // and lines move up a row unchanged. After line k, row 0 shows line k-1 and row 1 shows line k.
    var events = new System.Text.StringBuilder();
    for (var k = 0; k < 6; k++)
        events.Append($"[{(k == 0 ? "0.0" : "0.5")}, \"o\", \"{SparseLine(k)}\\r\\n\"]\n");
    var svg = RenderInline(40, 3, events.ToString());

    string Text(int k) => string.Join(' ', Enumerable.Range(0, 8).Select(i => i % 2 == 0 ? (char)('A' + k) : (char)('a' + k)));
    var reuses = SvgRenderTestPatterns.RowLayerPattern.Matches(svg).Where(m => m.Groups["ref"].Success).ToList();
    var rowClasses = SvgRenderTestPatterns.RowClassPattern.Matches(svg).Select(m => m.Groups[1].Value).ToHashSet();
    bool At(double time, int row, string expected) => VisibleRowsAt(svg, time).TryGetValue(row, out var text) && text == expected;
    return reuses.Any(m => m.Groups["cls"].Value.Split(' ').Skip(2).Any(rowClasses.Contains))  // a row-local <use>
        && reuses.Any(m => m.Groups["dy"].Success)                                                // an absolute <use>
        && At(0.25, 0, Text(0))
        && At(0.75, 0, Text(0)) && At(0.75, 1, Text(1))
        && Enumerable.Range(2, 4).All(k => At(k * 0.5 + 0.25, 0, Text(k - 1)) && At(k * 0.5 + 0.25, 1, Text(k)));
}

static string SparseLine(int k)
{
    var line = new System.Text.StringBuilder();
    for (var i = 0; i < 8; i++)
        line.Append(i % 2 == 0 ? $"\\u001b[31m{(char)('A' + k)} " : $"\\u001b[32m{(char)('a' + k)} ");
    return line.Append("\\u001b[0m").ToString();
}

static bool CursorSitsOnItsRow()
{
    // A cursor inside a row layer is drawn at that row's top edge, whether the layer is row-local
    // (y relative to the baseline) or absolute. A cursor with its own timing is checked on a cast
    // where each "\r\n" moves it down a row without changing that row, so no row layer carries it.
    // On row 0 it has its own timing too: it hides at 0.5s while "ab" stays, and a cursor joins only a
    // layer on its row with the same timing.
    var inv = System.Globalization.CultureInfo.InvariantCulture;
    var moves = RenderInline(10, 4, """
        [0.0, "o", "ab"]
        [0.5, "o", "\r\n"]
        [1.0, "o", "\r\n"]
        """);
    var ownTiming = SvgRenderTestPatterns.CursorPattern.Matches(moves)
        .Where(m => m.Value.Contains(" class=", StringComparison.Ordinal))
        .Select(m => double.Parse(m.Groups[2].Value, inv))
        .ToList();
    if (ownTiming.Count != 3 || Enumerable.Range(0, 3).Any(row => Math.Abs(ownTiming[row] - RowTop(moves, row)) > 0.01))
        return false;

    var svg = RenderCast("samples/basic.cast");
    var rowRules = SvgRenderTestPatterns.RowClassPattern.Matches(svg).ToDictionary(m => m.Groups[1].Value, m => double.Parse(m.Groups[2].Value, inv));
    var baselineOffset = double.Parse(SvgRenderTestPatterns.FontSizePattern.Match(svg).Groups[1].Value, inv) * 0.8;
    var checkedLocal = 0;
    var checkedAbsolute = 0;
    foreach (System.Text.RegularExpressions.Match layer in SvgRenderTestPatterns.RowLayerPattern.Matches(svg))
    {
        if (!layer.Value.StartsWith("<g ", StringComparison.Ordinal) || SvgRenderTestPatterns.CursorPattern.Match(layer.Value) is not { Success: true } cursor)
            continue;

        var y = double.Parse(cursor.Groups[2].Value, inv);
        if (layer.Groups["cls"].Value.Split(' ').Skip(2).FirstOrDefault(rowRules.ContainsKey) is { } rowClass)
        {
            if (Math.Abs(y + baselineOffset) > 0.01)
                return false;
            checkedLocal++;
        }
        else
        {
            var textY = double.Parse(SvgRenderTestPatterns.TextYPattern.Match(layer.Value).Groups[1].Value, inv);
            if (Math.Abs(y - (textY - baselineOffset)) > 0.01)
                return false;
            checkedAbsolute++;
        }
    }

    return checkedLocal > 0 && checkedAbsolute > 0;
}

static bool KeyframeNamesAreShort()
{
    // basic has 223 keyframes: one or two characters each, and the most used get the one-letter
    // names that rows and colors left over.
    var svg = RenderCast("samples/basic.cast");
    var names = SvgRenderTestPatterns.KeyframePattern.Matches(svg).Select(m => m.Groups[1].Value).ToList();
    int Uses(string name) => System.Text.RegularExpressions.Regex.Matches(svg, $"class=\"[^\"]*\\b{name}\\b").Count;
    var mostUsed = names.OrderByDescending(Uses).First();
    return names.Count > 26 && names.All(n => n.Length <= 2) && names.Distinct().Count() == names.Count && mostUsed.Length == 1;
}

static bool EveryKeyframeIsUsed()
{
    // In pty.cast some cursor positions share their timing with a row layer. Their own keyframes are
    // unused once they move into that layer, and must not be written.
    var svg = RenderCast("samples/pty.cast");
    var body = svg[svg.IndexOf("</style>", StringComparison.Ordinal)..];
    var used = System.Text.RegularExpressions.Regex.Matches(body, "class=\"([^\"]*)\"").SelectMany(m => m.Groups[1].Value.Split(' ')).ToHashSet();
    var names = SvgRenderTestPatterns.KeyframePattern.Matches(svg).Select(m => m.Groups[1].Value).ToList();
    return names.Count > 0 && names.All(used.Contains);
}

static bool ClassNamesAreLowercase()
{
    // An SVG inlined in an HTML page without a doctype matches classes regardless of case, so no two
    // names may differ only by case. pty.cast uses several hundred names.
    var svg = RenderCast("samples/pty.cast");
    var rules = System.Text.RegularExpressions.Regex.Matches(svg, @"^\.([A-Za-z0-9]+)\{", System.Text.RegularExpressions.RegexOptions.Multiline)
        .Select(m => m.Groups[1].Value).ToList();
    return rules.Count > 52 && rules.All(name => name == name.ToLowerInvariant());
}

static bool ZeroLengthFramesAreDropped()
{
    // Three writes in the same millisecond: only the last screen is ever visible, so no layer may be
    // shown and hidden at the same offset.
    var svg = RenderInline(20, 2, """
        [0.5, "o", "red"]
        [0.0, "o", " part"]
        [0.0, "o", " done"]
        [0.5, "o", "!"]
        """);
    var timings = ParseKeyframeTimings(svg).Values;
    return timings.Count > 0
        && timings.All(t => t.Hide is null || t.Hide > t.Show)
        && VisibleRowsAt(svg, 0.75).GetValueOrDefault(0) == "red part done";
}

static bool RunTextLengthCountsCells()
{
    // "café au lait|" is 13 cells but 14 UTF-16 units (e + U+0301). textLength must span 13 cells
    // (13 × 9.92 = 128.96), and an underlined wide character must span its 2 cells (19.84).
    var accent = RenderInline(20, 2, """[0.0, "o", "café au lait|"]""");
    var wide = RenderInline(20, 2, """[0.0, "o", "\u001b[4m日\u001b[0m"]""");
    return accent.Contains("textLength=\"128.96\">café au lait|</text>", StringComparison.Ordinal)
        && wide.Contains("textLength=\"19.84\">日</text>", StringComparison.Ordinal);
}

static bool UnderlinedSpacesKeepUnderline()
{
    // Underlined spaces before the row's last underlined character are drawn with their underline, as
    // in mp4: the three leading spaces (29.76 = 3 cells) and the space that starts the green run.
    // Spaces after the last underlined character ("x   ") are not.
    var leading = RenderInline(20, 2, """[0.0, "o", "\u001b[4m   \u001b[0m|\u001b[4mx   \u001b[0m|"]""");
    var between = RenderInline(20, 2, """[0.0, "o", "\u001b[4;31mfoo\u001b[32m bar\u001b[0m"]""");
    return leading.Contains("text-decoration=\"underline\" textLength=\"29.76\">   </text>", StringComparison.Ordinal)
        && leading.Contains("text-decoration=\"underline\" textLength=\"9.92\">x</text>", StringComparison.Ordinal)
        && between.Contains("text-decoration=\"underline\" textLength=\"39.68\"> bar</text>", StringComparison.Ordinal);
}

static bool UnderlinedBlocksKeepUnderline()
{
    // A block element is a rect, not text, so its underline needs a run over its cell. mp4 underlines up
    // to the row's last underlined character that is not a space, and a block is not a space. The two
    // underlined ▀ and the " ab" after them form one 5-cell run (49.6).
    var svg = RenderInline(20, 2, """[0.0, "o", "\u001b[4m▀▀ ab\u001b[0m"]""");
    return svg.Contains("text-decoration=\"underline\" textLength=\"49.6\">   ab</text>", StringComparison.Ordinal);
}

static bool PlainBlocksStayOutOfText()
{
    // Without an underline a block element needs no text: its rect draws it. The underlined z puts the
    // row in the run form, where blocks written as spaces would turn "ab" into "   ab" at column 0.
    var svg = RenderInline(20, 2, """[0.0, "o", "▀▀ ab \u001b[4mz\u001b[0m"]""");
    return svg.Contains(">ab</text>", StringComparison.Ordinal) && !svg.Contains(">   ab</text>", StringComparison.Ordinal);
}

static bool BlockOnlyRowsDecodeToTheirRow()
{
    // A layer of block rects alone decodes to the row its first rect starts in. ▄ starts half a line
    // down and ▁ seven eighths down, so rounding the rect's y to a row would put both one row too low.
    var svg = RenderInline(10, 4, """
        [0.0, "o", "x\r\n"]
        [0.5, "o", "▄▄▄▄\r\n"]
        [1.0, "o", "▁▁▁▁"]
        """);
    var rows = DecodeRowLayers(svg).Where(layer => layer.Text.Length == 0).Select(layer => layer.Row).ToList();
    return rows.Contains(1) && rows.Contains(2) && rows.All(row => row is 1 or 2);
}

static bool XmlInvalidCharactersAreReplaced()
{
    // U+FFFF and U+FFFE are not allowed in XML, and one of them makes the whole SVG fail to load.
    var svg = RenderInline(20, 2, """[0.0, "o", "a\uFFFFb\uFFFEc"]""");
    var document = new System.Xml.XmlDocument();
    document.LoadXml(svg);
    return svg.Contains("a\uFFFDb\uFFFDc", StringComparison.Ordinal);
}

static bool ScenarioThemeColorsAreValidated()
{
    // Theme colors go into the cast header, and the cast reader replaces values it does not accept
    // with the defaults. The scenario must reject them up front instead.
    bool Rejects(ScenarioTheme theme, string key) =>
        !RenderSettingsResolver.TryResolve(new Scenario { Render = new ScenarioRender { Theme = theme } }, null, out _, out var error)
        && error.Contains(key, StringComparison.Ordinal);
    var palette16 = string.Join(':', Enumerable.Repeat("#123456", 16));
    return Rejects(new ScenarioTheme { Fg = "white" }, "render.theme.fg")
        && Rejects(new ScenarioTheme { Bg = "#123456ff" }, "render.theme.bg")
        && Rejects(new ScenarioTheme { Palette = string.Join(':', Enumerable.Repeat("#123456", 8)) }, "render.theme.palette")
        && Rejects(new ScenarioTheme { Palette = palette16 + ":notacolor" }, "render.theme.palette")
        && RenderSettingsResolver.TryResolve(new Scenario { Render = new ScenarioRender { Theme = new ScenarioTheme { Fg = "#fff", Bg = "#123456", Palette = palette16 } } }, null, out _, out _);
}

static bool CastWarningsSayCast()
{
    // Cast warnings also show during mp4 conversion, so they name the cast, not SVG.
    var path = Path.Combine(Path.GetTempPath(), $"scenetake-cast-warning-{Guid.NewGuid():N}.cast");
    try
    {
        File.WriteAllText(path, "{\"version\":3,\"term\":{\"cols\":10,\"rows\":2}}\n[0.0, \"z\", \"x\"]\n");
        var (_, warnings) = ReadCastCapturingWarnings(path);
        return warnings.Contains("Warning: cast: unsupported cast event code 'z'", StringComparison.Ordinal)
            && !warnings.Contains("svg:", StringComparison.Ordinal);
    }
    finally
    {
        File.Delete(path);
    }
}

file static class SvgRenderTestPatterns
{
    // A row layer, one per line, with its class list in group "cls" ("l K", then a row class R and, on a
    // single <text>, a fill class F):
    //   <g class="l K[ R]">content</g>             (R moves row-local content to its row)
    //   <text class="l K[ R][ F]" …>…</text>       (a layer of one <text>, without the group)
    //   <use href="#rM"[ y="dy"] class="l K[ R]"/> (reusing earlier content, shifted by dy when absolute)
    internal static readonly System.Text.RegularExpressions.Regex RowLayerPattern = new(
        """^(?:<g class="(?<cls>l [^"]*)">(?<content>.*)</g>|(?<content><text class="(?<cls>l [^"]*)"[^>]*>.*</text>)|<use href="#(?<ref>r\d+)"(?: y="(?<dy>[^"]*)")? class="(?<cls>l [^"]*)"/>)$""",
        System.Text.RegularExpressions.RegexOptions.Multiline | System.Text.RegularExpressions.RegexOptions.Compiled);

    // Content that later layers reuse, inside its first layer.
    internal static readonly System.Text.RegularExpressions.Regex ReusedContentPattern = new(
        """<g id="(r\d+)">(.*?)</g>""",
        System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.Compiled);

    internal static readonly System.Text.RegularExpressions.Regex ReuseIdPattern = new(
        """<g id="(r\d+)">""",
        System.Text.RegularExpressions.RegexOptions.Compiled);

    // A cursor position: a <use> of the one cursor rect, inside a row layer or with its own timing.
    internal static readonly System.Text.RegularExpressions.Regex CursorPattern = new(
        """<use href="#c" x="([0-9.]+)" y="(-?[0-9.]+)"(?: class="l [a-z][a-z0-9]*")?/>""",
        System.Text.RegularExpressions.RegexOptions.Compiled);

    // A soft font glyph in a row: a <use> of its path, at the text baseline. Not the cursor (#c) or a reused row (#rN).
    // Groups: 1 the path id, 2 x, 3 y (absolute content only), 4 the fill class (none for the theme's foreground).
    internal static readonly System.Text.RegularExpressions.Regex GlyphUsePattern = new(
        """<use href="#(?!c"|r\d)([a-z][a-z0-9]*)" x="([0-9.]+)"(?: y="(-?[0-9.]+)")?(?: class="([a-z0-9]+)")?/>""",
        System.Text.RegularExpressions.RegexOptions.Compiled);

    // Groups: 1 the class list (with a fill class unless the text has the theme's foreground), 2 x, 3 the glyphs.
    internal static readonly System.Text.RegularExpressions.Regex TextPattern = new(
        """<text(?: class="([^"]*)")? x="([^"]*)"[^>]*>(.*?)</text>""",
        System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.Compiled);

    // The y of an absolute <text> (row-local text has none). Group 1: y.
    internal static readonly System.Text.RegularExpressions.Regex TextYPattern = new(
        """<text[^>]* y="(-?[0-9.]+)"[^>]*>""",
        System.Text.RegularExpressions.RegexOptions.Compiled);

    internal static readonly System.Text.RegularExpressions.Regex RectYPattern = new(
        """<rect x="[0-9.]+" y="(-?[0-9.]+)""",
        System.Text.RegularExpressions.RegexOptions.Compiled);

    // A <text> whose x attribute lists several positions (glyph-list form).
    internal static readonly System.Text.RegularExpressions.Regex GlyphListPattern = new(
        """<text(?: class="[^"]*")? x="([0-9.]+(?: [0-9.]+)+)"[^>]*>(.*?)</text>""",
        System.Text.RegularExpressions.RegexOptions.Compiled);

    // Background and block rects inside row layers carry no class.
    // Groups: 1 width, 2 height, 3 fill, 4 fill-opacity (shades only).
    internal static readonly System.Text.RegularExpressions.Regex RowRectPattern = new(
        """<rect x="[0-9.]+" y="-?[0-9.]+" width="([0-9.]+)" height="([0-9.]+)" fill="([^"]+)"(?: fill-opacity="([0-9.]+)")?/>""",
        System.Text.RegularExpressions.RegexOptions.Compiled);

    internal static readonly System.Text.RegularExpressions.Regex FillClassPattern = new(
        """^\.([a-z][a-z0-9]*)\{fill:(#[0-9A-Fa-f]+)\}$""",
        System.Text.RegularExpressions.RegexOptions.Multiline | System.Text.RegularExpressions.RegexOptions.Compiled);

    // Groups: 1 the row class, 2 the baseline y it moves row-local content to.
    internal static readonly System.Text.RegularExpressions.Regex RowClassPattern = new(
        """^\.([a-z][a-z0-9]*)\{transform:translate\(0,([0-9.]+)px\)\}$""",
        System.Text.RegularExpressions.RegexOptions.Multiline | System.Text.RegularExpressions.RegexOptions.Compiled);

    // The terminal background of the first viewport. Group 1: its top edge.
    internal static readonly System.Text.RegularExpressions.Regex ViewportBackgroundPattern = new(
        """class="bg l [a-z0-9]+ viewport-bg-0" x="[0-9.]+" y="([0-9.]+)""",
        System.Text.RegularExpressions.RegexOptions.Compiled);

    internal static readonly System.Text.RegularExpressions.Regex DefaultFillPattern = new(
        """<g mask="url\(#viewport-mask\)" fill="(#[0-9A-Fa-f]+)">""",
        System.Text.RegularExpressions.RegexOptions.Compiled);

    internal static readonly System.Text.RegularExpressions.Regex FontSizePattern = new(
        """font-size: ([0-9]+)px""",
        System.Text.RegularExpressions.RegexOptions.Compiled);

    internal static readonly System.Text.RegularExpressions.Regex KeyframePattern = new(
        """^@keyframes ([a-z][a-z0-9]*)\{(.+)\}$""",
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
