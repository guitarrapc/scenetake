#:sdk Microsoft.NET.Sdk
#:property TargetFramework=net10.0
#:property Nullable=enable
#:property ImplicitUsings=enable
#:package VYaml@1.6.1
#:include ../Terminal.cs
#:include ../CastTimingPipeline.cs
#:include ../CastReader.cs
#:include ../Svg.cs
#:include ../JsonEscape.cs
#:include ../PtyLeadingInitFilter.cs
#:include ../ScenarioKeys.cs
#:include ../Scenario.cs
#:include ../Highlight.cs
#:include ../CastComposer.cs
#:include ../StepChunks.cs
#:include ../StepOutputs.cs
#:include ../PlaygroundRender.cs

// The playground replays outputs files instead of running commands (.github/docs/spec_playground.md). These tests
// check that a replay gives the CLI's cast and SVG byte for byte, that steps match recorded output by run text,
// kind, and occurrence, and that an outputs file keeps no OSC text that the cast leaves out.
//
// Usage (from repository root):
//   dotnet run tests/playground_replay_test.cs
//   SCENETAKE_BIN=/path/to/scenetake dotnet run tests/playground_replay_test.cs   # also records a scenario

using System.Diagnostics;
using System.Text;

var repoRoot = FindRepoRoot(Directory.GetCurrentDirectory());
var failures = 0;
failures += Run("SamplesReplayToCommittedCasts", () => SamplesReplayToCommittedCasts(repoRoot));
failures += Run("MaskedOscGivesSameFilteredEvents", MaskedOscGivesSameFilteredEvents);
failures += Run("MaskKeepsColorOscAndLength", MaskKeepsColorOscAndLength);
failures += Run("MaskCarriesStateAcrossChunks", MaskCarriesStateAcrossChunks);
failures += Run("OutputsFileRoundTripsExactly", OutputsFileRoundTripsExactly);
failures += Run("OutputsFileMasksPrivateOsc", OutputsFileMasksPrivateOsc);
failures += Run("NthOccurrenceTakesNthRecording", NthOccurrenceTakesNthRecording);
failures += Run("ExtraOccurrenceTakesLastRecording", ExtraOccurrenceTakesLastRecording);
failures += Run("UnrecordedStepGetsPlaceholderLine", UnrecordedStepGetsPlaceholderLine);
failures += Run("KindMismatchGetsPlaceholderLine", KindMismatchGetsPlaceholderLine);
failures += Run("EditsBesidesRunKeepTheMatch", EditsBesidesRunKeepTheMatch);
failures += Run("PtySizeMismatchGivesNotice", PtySizeMismatchGivesNotice);
failures += Run("RecordedExitCodeWarns", RecordedExitCodeWarns);
failures += Run("ErrorsAreValuesNotExceptions", ErrorsAreValuesNotExceptions);
failures += Run("NotAppliedKeysAreListed", NotAppliedKeysAreListed);
failures += Run("UnquotedColorsWarn", UnquotedColorsWarn);
failures += Run("CastTabAppliesOverrides", () => CastTabAppliesOverrides(repoRoot));

var scenetake = Environment.GetEnvironmentVariable("SCENETAKE_BIN");
if (!string.IsNullOrWhiteSpace(scenetake) && File.Exists(scenetake))
    failures += Run("RecordedScenarioReplaysToSameCast", () => RecordedScenarioReplaysToSameCast(scenetake, repoRoot));
else
    Console.Error.WriteLine("skip RecordedScenarioReplaysToSameCast: set SCENETAKE_BIN to a scenetake binary");

if (failures > 0)
{
    Console.Error.WriteLine($"{failures} test(s) failed");
    return 1;
}

Console.Error.WriteLine("all playground replay tests passed");
return 0;

// Every committed sample with an outputs file replays to its committed cast and SVG. The showcase samples are
// recorded on Linux in a container (samples/showcase/record.sh), and the playground offers them too.
static bool SamplesReplayToCommittedCasts(string repoRoot)
{
    var outputsFiles = new[] { "samples", "samples/showcase" }
        .Select(folder => Path.Combine(repoRoot, folder))
        .Where(Directory.Exists)
        .SelectMany(static folder => Directory.GetFiles(folder, "*.outputs.json"))
        .OrderBy(static path => path, StringComparer.Ordinal)
        .ToArray();
    if (outputsFiles.Length == 0)
    {
        Console.Error.WriteLine("  no samples/*.outputs.json yet");
        return true;
    }

    var ok = true;
    foreach (var outputsPath in outputsFiles)
    {
        var samples = Path.GetDirectoryName(outputsPath)!;
        var name = Path.GetFileName(outputsPath)[..^".outputs.json".Length];
        var yaml = File.ReadAllText(Path.Combine(samples, name + ".yaml"), Encoding.UTF8);
        var outputs = StepOutputs.Parse(File.ReadAllText(outputsPath, Encoding.UTF8));
        var result = PlaygroundRender.RenderScenario(yaml, outputs, new PlaygroundOptions());
        if (result.Error is not null)
        {
            Console.Error.WriteLine($"  {name}: {result.Error}");
            ok = false;
            continue;
        }

        ok &= SameBytes($"{name}.cast", File.ReadAllBytes(Path.Combine(samples, name + ".cast")), result.Cast!);
        ok &= SameBytes($"{name}.svg", File.ReadAllBytes(Path.Combine(samples, name + ".svg")), result.Svg!);
        if (result.Steps.Any(static step => !step.Recorded))
        {
            Console.Error.WriteLine($"  {name}: a step has no recorded output");
            ok = false;
        }
    }

    return ok;
}

// The CLI records a scenario with SCENETAKE_OUTPUTS; the replay gives the cast and SVG it wrote.
static bool RecordedScenarioReplaysToSameCast(string scenetake, string repoRoot)
{
    var work = Directory.CreateTempSubdirectory("scenetake-playground-replay-").FullName;
    try
    {
        var yamlPath = Path.Combine(repoRoot, "tests", "fixtures", "playground-replay.yaml");
        var stem = Path.Combine(work, "replay");
        var outputsPath = Path.Combine(work, "replay.outputs.json");
        var psi = new ProcessStartInfo(scenetake) { UseShellExecute = false, RedirectStandardError = true, RedirectStandardOutput = true };
        foreach (var arg in new[] { "--format", "svg", yamlPath, stem })
            psi.ArgumentList.Add(arg);
        psi.Environment[StepOutputs.EnvironmentVariable] = outputsPath;
        using (var process = Process.Start(psi)!)
        {
            var stderr = process.StandardError.ReadToEnd();
            process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                Console.Error.WriteLine(stderr);
                return false;
            }
        }

        var outputs = StepOutputs.Parse(File.ReadAllText(outputsPath, Encoding.UTF8));
        var ok = true;
        // The run text is typed into the cast, so only what the commands printed must lose the OSC text.
        var printed = string.Concat(outputs.Steps.Select(static step =>
            string.Concat(step.PipeChunks.Select(static chunk => chunk.Text))
            + string.Concat(step.PtyChunks.Select(static chunk => Encoding.UTF8.GetString(chunk.Data.Span)))));
        foreach (var secret in new[] { "secret-pipe-title", "secret-pty-title", "secret-folder" })
        {
            if (printed.Contains(secret, StringComparison.Ordinal))
            {
                Console.Error.WriteLine($"  the outputs file holds '{secret}'");
                ok = false;
            }
        }

        ok &= Expect(printed.Contains("visible", StringComparison.Ordinal) && printed.Contains("pty out", StringComparison.Ordinal), "visible output stays");

        var yaml = File.ReadAllText(yamlPath, Encoding.UTF8);
        var result = PlaygroundRender.RenderScenario(yaml, outputs, new PlaygroundOptions());
        if (result.Error is not null)
        {
            Console.Error.WriteLine($"  {result.Error}");
            return false;
        }

        ok &= SameBytes("cast", File.ReadAllBytes(stem + ".cast"), result.Cast!);
        ok &= SameBytes("svg", File.ReadAllBytes(stem + ".svg"), result.Svg!);
        ok &= Expect(result.Steps.All(static step => step.Recorded), "every step is recorded");
        return ok;
    }
    finally
    {
        Directory.Delete(work, recursive: true);
    }
}

// A chunk sequence and its masked copy give the same events after CastOscFilter, also when an OSC is split
// between chunks or a chunk holds nothing but an OSC.
static bool MaskedOscGivesSameFilteredEvents()
{
    string[][] cases =
    [
        ["\u001b]0;C:\\Users\\name\\pwsh.exe\u0007", "hello\r\n"],
        ["before\u001b]0;ti", "tle\u0007after", "\r\n"],
        ["\u001b]", "7;file:///home/user", "\u001b\\", "done"],
        ["\u001b]8;;file:///home/x\u001b\\link\u001b]8;;\u001b\\ text"],
        ["\u001b]4;1;rgb:ff/00/00\u0007red", "\u001b]10;?\u001b\\"],
        ["\u001b]133;A\u0007$ ", "\u001b]633;P;Cwd=/home/user\u0007x"],
        ["a\u001b]0;open title never ended", "more", "end"],
        ["\u001b", "]2;split introducer\u0007", "ok"],
        ["\u001b[1mbold\u001b[0m", "\u001b]0;t\u001b[31mred"],
        ["\u001b]1234567;long code\u0007z"],
    ];

    var ok = true;
    foreach (var chunks in cases)
    {
        var original = new List<CastEvent>();
        var masked = new List<CastEvent>();
        var masker = new OscMasker();
        for (var i = 0; i < chunks.Length; i++)
        {
            var bytes = Encoding.UTF8.GetBytes(chunks[i]);
            original.Add(CastEvent.OutputUtf8(i, bytes));
            masked.Add(CastEvent.OutputUtf8(i, masker.Mask(bytes)));
        }

        var expected = Describe(CastOscFilter.RemovePrivateSequences(original));
        var actual = Describe(CastOscFilter.RemovePrivateSequences(masked));
        if (expected != actual)
        {
            Console.Error.WriteLine($"  {string.Join(" | ", chunks).Replace("\u001b", "ESC")}:\n    filtered: {expected}\n    masked:   {actual}");
            ok = false;
        }
    }

    return ok;

    static string Describe(List<CastEvent> events) =>
        string.Join(", ", events.Select(static ev => $"{ev.Time}:{(ev.HasUtf8Output ? Encoding.UTF8.GetString(ev.Utf8Output.Span) : ev.Data)}".Replace("\u001b", "ESC")));
}

static bool MaskKeepsColorOscAndLength()
{
    var input = "\u001b]4;1;rgb:ff/00/00\u0007\u001b]0;private title\u0007text";
    var masked = new OscMasker().Mask(input);
    return Expect(masked.Length == input.Length, "length is kept")
        & Expect(masked.StartsWith("\u001b]4;1;rgb:ff/00/00\u0007", StringComparison.Ordinal), "a color OSC is kept")
        & Expect(!masked.Contains("private", StringComparison.Ordinal), "a title is masked")
        & Expect(masked.EndsWith("\u0007text", StringComparison.Ordinal), "the terminator and later text are kept");
}

static bool MaskCarriesStateAcrossChunks()
{
    var masker = new OscMasker();
    var first = masker.Mask("ok \u001b]0;secr");
    var second = masker.Mask("et\u0007 visible");
    return Expect(first == "ok \u001b]0xxxxx", $"first chunk masked: {first.Replace("\u001b", "ESC")}")
        & Expect(second == "xx\u0007 visible", $"second chunk masked: {second.Replace("\u001b", "ESC")}");
}

static bool OutputsFileRoundTripsExactly()
{
    var outputs = new StepOutputs
    {
        Scenetake = "1.5.0",
        Os = "windows",
        Shell = "pwsh",
        Width = 80,
        Height = 24,
        Steps =
        [
            RecordedStep.Pipe("echo 日本語", 0, [new(TimeSpan.FromTicks(1234567), false, "日本語 👍\n"), new(TimeSpan.FromTicks(1234568), true, "err\n")]),
            RecordedStep.Pty("matrix", 3, (5, 1), [new(TimeSpan.FromTicks(98765), new byte[] { 0x1B, (byte)'[', (byte)'H', 0xE3, 0x81 })]),
            RecordedStep.Interactive("vim", 0, new ComposedPtyOutput([(0.1 + 0.2, new byte[] { (byte)'a' }), (1.0 / 3, Array.Empty<byte>())], 2.0 / 3)),
        ],
    };

    using var stream = new MemoryStream();
    outputs.Write(stream);
    var json = Encoding.UTF8.GetString(stream.ToArray());
    var parsed = StepOutputs.Parse(json);
    var pipe = parsed.Steps[0];
    var pty = parsed.Steps[1];
    var interactive = parsed.Steps[2];
    return Expect(!json.Contains('\r'), "lines end with LF")
        & Expect(json.Contains("日本語", StringComparison.Ordinal), "text stays readable")
        & Expect(parsed.Os == "windows" && parsed.Shell == "pwsh" && parsed.Width == 80 && parsed.Height == 24, "header")
        & Expect(pipe.PipeChunks.SequenceEqual(outputs.Steps[0].PipeChunks), "pipe chunks")
        & Expect(pty.ExitCode == 3 && pty.Cursor == (5, 1) && pty.PtyChunks[0].Time.Ticks == 98765 && pty.PtyChunks[0].Data.Span.SequenceEqual(new byte[] { 0x1B, (byte)'[', (byte)'H', 0xE3, 0x81 }), "pty chunk")
        & Expect(interactive.Composed!.End == 2.0 / 3 && interactive.Composed.Chunks[0].Time == 0.1 + 0.2 && interactive.Composed.Chunks[1].Time == 1.0 / 3, "times are exact");
}

static bool OutputsFileMasksPrivateOsc()
{
    var outputs = new StepOutputs
    {
        Scenetake = "1.5.0",
        Os = "linux",
        Shell = "bash",
        Width = 80,
        Height = 24,
        Steps =
        [
            RecordedStep.Pipe("a", 0, [new(TimeSpan.Zero, false, "\u001b]0;pipe-secret\u0007x")]),
            RecordedStep.Pty("b", 0, null, [new(TimeSpan.Zero, Encoding.UTF8.GetBytes("\u001b]7;file:///pty-secret\u001b\\y"))]),
            RecordedStep.Interactive("c", 0, new ComposedPtyOutput([(0.5, Encoding.UTF8.GetBytes("\u001b]2;interactive-secret\u0007z"))], 0.5)),
        ],
    };

    using var stream = new MemoryStream();
    outputs.Write(stream);
    var parsed = StepOutputs.Parse(Encoding.UTF8.GetString(stream.ToArray()));
    var all = parsed.Steps[0].PipeChunks[0].Text
        + Encoding.UTF8.GetString(parsed.Steps[1].PtyChunks[0].Data.Span)
        + Encoding.UTF8.GetString(parsed.Steps[2].Composed!.Chunks[0].Data.Span);
    return Expect(!all.Contains("secret", StringComparison.Ordinal), $"no OSC text remains: {all.Replace("\u001b", "ESC")}");
}

static bool NthOccurrenceTakesNthRecording()
{
    var outputs = Outputs(RecordedStep.Pipe("git status", 0, [Out("before\n")]), RecordedStep.Pipe("git status", 0, [Out("after\n")]));
    var result = Render(Yaml("git status", "git status"), outputs);
    var cast = Encoding.UTF8.GetString(result.Cast!);
    return Expect(cast.IndexOf("before", StringComparison.Ordinal) is var before && before >= 0 && cast.IndexOf("after", StringComparison.Ordinal) > before, "before, then after");
}

static bool ExtraOccurrenceTakesLastRecording()
{
    var outputs = Outputs(RecordedStep.Pipe("echo hi", 0, [Out("first\n")]), RecordedStep.Pipe("echo hi", 0, [Out("second\n")]));
    var result = Render(Yaml("echo hi", "echo hi", "echo hi"), outputs);
    var cast = Encoding.UTF8.GetString(result.Cast!);
    return Expect(Count(cast, "second") == 2 && Count(cast, "first") == 1, "the third step takes the last recording")
        & Expect(result.Steps.Count == 3 && result.Steps.All(static step => step.Recorded), "all recorded");
}

static bool UnrecordedStepGetsPlaceholderLine()
{
    var outputs = Outputs(RecordedStep.Pipe("echo hi", 0, [Out("hi\n")]));
    var result = Render(Yaml("echo hi", "ls -la"), outputs);
    var cast = Encoding.UTF8.GetString(result.Cast!);
    return Expect(cast.Contains("# no recorded output in the playground", StringComparison.Ordinal), "placeholder line")
        & Expect(result.Steps.Count == 2 && result.Steps[0].Recorded && !result.Steps[1].Recorded, "steps list marks the step");
}

static bool KindMismatchGetsPlaceholderLine()
{
    var outputs = Outputs(RecordedStep.Pipe("top", 0, [Out("pipe output\n")]));
    var yaml = "width: 80\nheight: 24\nsteps:\n  - run: top\n    pty: true\n";
    var result = Render(yaml, outputs);
    return Expect(result.Error is null, result.Error ?? "")
        && Expect(!result.Steps[0].Recorded && result.Steps[0].Kind == "pty", "a pty step does not take pipe output");
}

static bool EditsBesidesRunKeepTheMatch()
{
    var outputs = Outputs(RecordedStep.Pipe("printf 'a\\nb\\n'", 0, [Out("a\nb\n")]));
    var yaml = """
        width: 80
        height: 24
        settings:
          prompt: "% "
          typing-speed: 0.2
        render:
          window: macos
        steps:
          - name: renamed
            run: printf 'a\nb\n'
            highlight:
              - color: green
                at: "2"
            post-delay: 2
        """;
    var result = Render(yaml, outputs);
    var cast = Encoding.UTF8.GetString(result.Cast!);
    return Expect(result.Steps[0].Recorded, "still recorded")
        & Expect(cast.Contains("\\u001b[32mb", StringComparison.Ordinal), "the highlight colors the recorded line");
}

static bool PtySizeMismatchGivesNotice()
{
    var outputs = Outputs(RecordedStep.Pty("matrix", 0, null, [new(TimeSpan.FromMilliseconds(10), Encoding.UTF8.GetBytes("rain"))]));
    var yaml = "width: 40\nheight: 10\nsteps:\n  - run: matrix\n    pty: true\n";
    var result = Render(yaml, outputs);
    return Expect(result.Steps[0].Recorded, "recorded")
        & Expect(result.Steps[0].Notices.Any(static notice => notice.Contains("80x24", StringComparison.Ordinal)), "size notice");
}

static bool RecordedExitCodeWarns()
{
    var outputs = Outputs(RecordedStep.Pipe("exit 1", 1, []), RecordedStep.Pipe("exit 2", 2, []));
    var yaml = "width: 80\nheight: 24\nsteps:\n  - exit 1\n  - run: exit 2\n    continue-on-error: true\n";
    var result = Render(yaml, outputs);
    return Expect(result.Warnings.Count(static warning => warning.StartsWith("step exited", StringComparison.Ordinal)) == 1, string.Join("; ", result.Warnings));
}

static bool ErrorsAreValuesNotExceptions()
{
    var outputs = Outputs(RecordedStep.Pipe("echo hi", 0, [Out("hi\n")]));
    return Expect(Render("steps: [", outputs).Error is not null, "YAML syntax error")
        & Expect(Render("width: 0\nsteps:\n  - echo hi\n", outputs).Error?.Contains("width", StringComparison.Ordinal) == true, "terminal size")
        & Expect(Render("steps:\n  - run: echo hi\n    interactive: true\n", outputs).Error?.Contains("pty", StringComparison.Ordinal) == true, "step key error")
        & Expect(Render("step:\n  - echo hi\n", outputs).Error?.Contains("no steps", StringComparison.Ordinal) == true, "no steps")
        & Expect(PlaygroundRender.RenderScenario(Yaml("echo hi"), outputs, new PlaygroundOptions(Theme: "blue")).Error?.Contains("theme", StringComparison.Ordinal) == true, "bad theme option")
        & Expect(PlaygroundRender.RenderScenario(Yaml("echo hi"), outputs, new PlaygroundOptions(FontSize: 0)).Error is not null, "bad font size")
        & Expect(PlaygroundRender.RenderScenario(Yaml("echo hi"), outputs, new PlaygroundOptions(MaxFps: 500)).Error is not null, "bad max fps")
        & Expect(PlaygroundRender.RenderCast("not a cast", new PlaygroundOptions()).Error is not null, "bad cast")
        & Expect(Throws(static () => StepOutputs.Parse("{\"version\":9,\"width\":1,\"height\":1,\"steps\":[]}")), "unsupported outputs version");

    static bool Throws(Action action)
    {
        try
        {
            action();
            return false;
        }
        catch (FormatException)
        {
            return true;
        }
    }
}

static bool NotAppliedKeysAreListed()
{
    var outputs = Outputs(RecordedStep.Pipe("echo hi", 0, [Out("hi\n")]));
    var yaml = "width: 80\nheight: 24\nshell: pwsh\ncwd: work\npre:\n  - mkdir work\nsteps:\n  - echo hi\n";
    var result = Render(yaml, outputs);
    var sameShell = Render("width: 80\nheight: 24\nshell: bash\nsteps:\n  - echo hi\n", outputs);
    return Expect(result.NotApplied.Count == 3, string.Join("; ", result.NotApplied))
        & Expect(sameShell.NotApplied.Count == 0, "the recording's own shell is no note: " + string.Join("; ", sameShell.NotApplied));
}

// YAML reads `key: #1e3c72` as a key with no value and a comment, so the color was lost without a word. The CLI and
// the playground warn the same way: a color key gets the quoted color, a style key the quoted fg: style. A real
// comment, a quoted color, and a #fff inside a script block draw no warning.
static bool UnquotedColorsWarn()
{
    var yaml = """
        width: 80
        height: 24
        settings:
          stderr-color: #ff8800
        render:
          background: #1e3c72 #2a5298
          theme:
            bg: #222
            fg: "#eeeeee"
            palette: # 16 colors go here
        steps:
          - run: |
              echo "background: #fff"
          - run: echo hi
            highlight:
              - color: #00ff00
                at: "1"
        """;
    var expected = new[]
    {
        "line 4: 'stderr-color' has no value: YAML reads '#ff8800' as a comment. Quote the style: stderr-color: \"fg:#ff8800\"",
        "line 6: 'background' has no value: YAML reads '#1e3c72 #2a5298' as a comment. Quote the color: background: \"#1e3c72 #2a5298\"",
        "line 8: 'bg' has no value: YAML reads '#222' as a comment. Quote the color: bg: \"#222\"",
        "line 16: 'color' has no value: YAML reads '#00ff00' as a comment. Quote the style: color: \"fg:#00ff00\"",
    };
    var found = ScenarioCast.FindUnknownScenarioKeys(yaml);
    var ok = Expect(found.SequenceEqual(expected), "the four unquoted colors, in order; got:\n    " + string.Join("\n    ", found));

    // The playground lists them with its other warnings.
    var outputs = Outputs(RecordedStep.Pipe("echo hi", 0, [Out("hi\n")]));
    var result = Render("width: 80\nheight: 24\nrender:\n  background: #1e3c72\nsteps:\n  - echo hi\n", outputs);
    ok &= Expect(result.Warnings.Contains("line 4: 'background' has no value: YAML reads '#1e3c72' as a comment. Quote the color: background: \"#1e3c72\""),
        "the playground warning; got: " + string.Join("; ", result.Warnings));
    return ok;
}

static bool CastTabAppliesOverrides(string repoRoot)
{
    var castText = File.ReadAllText(Path.Combine(repoRoot, "samples", "resize.cast"), Encoding.UTF8);
    var plain = PlaygroundRender.RenderCast(castText, new PlaygroundOptions());
    var light = PlaygroundRender.RenderCast(castText, new PlaygroundOptions(Theme: "light", Window: "macos"));
    var recording = CastReader.ReadText(castText);
    var expected = SvgRender.RenderToUtf8(recording.Events, recording.Width, recording.Height, recording.RenderSettings, recording.LoopDuration);
    return Expect(plain.Error is null && plain.Svg!.AsSpan().SequenceEqual(expected), "no option gives the svg command's output")
        & Expect(light.Error is null && !light.Svg!.AsSpan().SequenceEqual(expected), "options change the SVG");
}

static StepOutputs Outputs(params RecordedStep[] steps) => new()
{
    Scenetake = "1.5.0",
    Os = "linux",
    Shell = "bash",
    Width = 80,
    Height = 24,
    Steps = steps,
};

static PipeStreamChunk Out(string text) => new(TimeSpan.FromMilliseconds(5), false, text);

static string Yaml(params string[] commands) =>
    "width: 80\nheight: 24\nsteps:\n" + string.Concat(commands.Select(static command => $"  - {command}\n"));

static PlaygroundResult Render(string yaml, StepOutputs outputs) =>
    PlaygroundRender.RenderScenario(yaml, outputs, new PlaygroundOptions(), renderSvg: false);

static int Count(string text, string value)
{
    var count = 0;
    for (var index = text.IndexOf(value, StringComparison.Ordinal); index >= 0; index = text.IndexOf(value, index + value.Length, StringComparison.Ordinal))
        count++;
    return count;
}

static bool SameBytes(string name, byte[] expected, byte[] actual)
{
    if (expected.AsSpan().SequenceEqual(actual))
        return true;

    var index = 0;
    while (index < expected.Length && index < actual.Length && expected[index] == actual[index])
        index++;
    var line = expected.AsSpan(0, Math.Min(index, expected.Length)).Count((byte)'\n') + 1;
    Console.Error.WriteLine($"  {name} differs at byte {index} (line {line}): expected {expected.Length} bytes, got {actual.Length}");
    return false;
}

static bool Expect(bool condition, string what)
{
    if (!condition)
        Console.Error.WriteLine($"  expected: {what}");
    return condition;
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
        Console.Error.WriteLine($"FAIL {name}: {ex}");
        return 1;
    }
}

static string FindRepoRoot(string start)
{
    var current = new DirectoryInfo(start);
    while (current is not null)
    {
        if (File.Exists(Path.Combine(current.FullName, "scenetake.cs")))
            return current.FullName;
        current = current.Parent;
    }

    throw new InvalidOperationException("repository root not found");
}
