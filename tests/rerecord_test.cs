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
#:include ../Rerecord.cs
#:include ../SecretMask.cs
#:include ../StepChunks.cs
#:include ../StepOutputs.cs
#:include ../PlaygroundRender.cs

// Recording again to an earlier cast (.github/docs/spec_scenario.md → Recording again): a step that prints the same
// keeps its earlier events and timing, keep-output keeps them whatever the step prints, and only changed steps differ.
// The integration tests need SCENETAKE_BIN and bash.

using System.Diagnostics;
using System.Text;
using static ScenarioCast;

var repoRoot = FindRepoRoot(AppContext.BaseDirectory) ?? FindRepoRoot(Directory.GetCurrentDirectory())
    ?? throw new InvalidOperationException("repository root not found");
var failures = 0;

failures += Run("SameOutputGivesSameCast", SameOutputGivesSameCast);
failures += Run("ChunkSplitIsTheSameOutput", ChunkSplitIsTheSameOutput);
failures += Run("OnlyTheChangedStepDiffers", OnlyTheChangedStepDiffers);
failures += Run("KeepOutputKeepsTheEarlierOutput", KeepOutputKeepsTheEarlierOutput);
failures += Run("KeepOutputOfInstantOutput", KeepOutputOfInstantOutput);
failures += Run("NewTimingKeysTakeEffect", NewTimingKeysTakeEffect);
failures += Run("TypingChangesKeepOutputTiming", TypingChangesKeepOutputTiming);
failures += Run("StepsAreFoundByRunText", StepsAreFoundByRunText);
failures += Run("RepeatedCommandsMatchInOrder", RepeatedCommandsMatchInOrder);
failures += Run("FilteredEventsKeepTheirSteps", FilteredEventsKeepTheirSteps);
failures += Run("NoEarlierCastChangesNothing", NoEarlierCastChangesNothing);
failures += Run("TagRoundTrips", TagRoundTrips);
failures += Run("EarlierCastProblems", EarlierCastProblems);
failures += Run("KeepOutputKeyRules", KeepOutputKeyRules);
failures += Run("DescribeSaysWhatChanged", DescribeSaysWhatChanged);
failures += Run("IntegrationRecordingAgainGivesSameFiles", () => IntegrationRecordingAgainGivesSameFiles(repoRoot));
failures += Run("IntegrationChangedAndKeptSteps", () => IntegrationChangedAndKeptSteps(repoRoot));

if (failures > 0)
{
    Console.Error.WriteLine($"Failed: {failures} rerecord test(s)");
    return 1;
}

Console.Error.WriteLine("all rerecord tests passed");
return 0;

static int Run(string name, Func<bool> test)
{
    try
    {
        if (test())
        {
            Console.Error.WriteLine($"ok {name}");
            return 0;
        }
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"  {ex}");
    }

    Console.Error.WriteLine($"FAIL {name}");
    return 1;
}

static bool Expect(bool condition, string message)
{
    if (!condition)
        Console.Error.WriteLine($"  {message}");
    return condition;
}

static string? FindRepoRoot(string start)
{
    for (var dir = new DirectoryInfo(start); dir is not null; dir = dir.Parent)
    {
        if (File.Exists(Path.Combine(dir.FullName, "scenetake.cs")))
            return dir.FullName;
    }

    return null;
}

// A PTY step's output: each part at its time in seconds since the command started.
static CommandExecution Pty(params (double Time, string Text)[] parts) =>
    new(0, true, "", "", [.. parts.Select(static part => new TimedBytes(TimeSpan.FromSeconds(part.Time), Encoding.UTF8.GetBytes(part.Text)))], null, null);

static CommandExecution Pipe(string text) =>
    CommandExecution.FromPipeStream(text, "", 0, [new PipeStreamChunk(TimeSpan.FromMilliseconds(5), false, text)]);

// Records the scenario with the outputs that run gives each step, compared with an earlier cast when there is one,
// and returns the cast as scenetake writes it.
static (string Cast, RerecordResult Result) Record(string yaml, Func<StepStart, CommandExecution> run, string? earlier = null)
{
    var scenario = ParseScenario(yaml);
    var options = new GenerateOptions { LogRunning = false, Slots = [] };
    var (events, failure) = GenerateAsync(scenario, 7, options, step => Task.FromResult(run(step))).GetAwaiter().GetResult();
    if (failure is not null)
        throw new InvalidOperationException($"step {failure.Number} failed");

    var map = new int[events.Count + 1];
    events = CastOscFilter.RemovePrivateSequences(events, map);
    PreviousCast? previous = null;
    if (earlier is not null)
    {
        previous = PreviousCast.Parse(earlier.Split('\n'), out var problem);
        if (previous is null)
            throw new InvalidOperationException($"the earlier cast cannot be used: {problem}");
    }

    var result = Rerecording.Apply(events, Rerecording.MapIndices(options.Slots!, map), previous);
    using var writer = new StringWriter();
    WriteCast(writer, scenario, result.Events, "bash", 1700000000, RenderSettingsResolver.Resolve(scenario), result.Slots);
    return (writer.ToString(), result);
}

// The event lines of a cast, without the header, whose st:steps tag moves with the events.
static string[] EventLines(string cast) => cast.Split('\n')[1..];

static string FirstDifference(string a, string b)
{
    var left = a.Split('\n');
    var right = b.Split('\n');
    for (var i = 0; i < Math.Max(left.Length, right.Length); i++)
    {
        var l = i < left.Length ? left[i] : "(none)";
        var r = i < right.Length ? right[i] : "(none)";
        if (l != r)
            return $"line {i + 1}: {l} | {r}";
    }

    return "(same)";
}

// The output lines of the three steps: in a step that keeps its earlier events, they keep their intervals.
static List<string> ThreeStepsLines(string cast) => EventLines(cast)
    .Where(static line => line.Contains("hello\\r\\n", StringComparison.Ordinal) || line.Contains("world\\r\\n", StringComparison.Ordinal)
        || line.Contains("\"1\\r\\n\"", StringComparison.Ordinal) || line.Contains("\"2\\r\\n\"", StringComparison.Ordinal)
        || line.Contains("\"3\\r\\n\"", StringComparison.Ordinal) || line.Contains("three\\r\\n", StringComparison.Ordinal))
    .ToList();

static CommandExecution ThreeStepsOutput(StepStart step, double jitter) => step.Command.Cmd switch
{
    "hello" => Pty((0.1 + jitter, "hello\r\n"), (0.4 + 2 * jitter, "world\r\n")),
    "count" => Pty((0.05 + jitter, "1\r\n"), (0.25 + 3 * jitter, "2\r\n"), (0.6 - jitter, "3\r\n")),
    _ => Pipe("three\n"),
};

static bool SameOutputGivesSameCast()
{
    var first = Record(ThreeSteps, step => ThreeStepsOutput(step, 0));
    // The same output, read a few milliseconds earlier or later, as each recording reads it.
    var fresh = Record(ThreeSteps, step => ThreeStepsOutput(step, 0.0037));
    var again = Record(ThreeSteps, step => ThreeStepsOutput(step, 0.0037), first.Cast);
    return Expect(fresh.Cast != first.Cast, "the test's timing differences change a cast that is not compared")
        & Expect(again.Cast == first.Cast, $"the cast differs: {FirstDifference(first.Cast, again.Cast)}")
        & Expect(again.Result.Same.Count == 3 && again.Result.Changed.Count == 0, $"same {again.Result.Same.Count}, changed {again.Result.Changed.Count}")
        & Expect(first.Cast.Contains("\"st:steps=", StringComparison.Ordinal), "the cast has no st:steps tag");
}

static bool ChunkSplitIsTheSameOutput()
{
    const string yaml = "steps:\n  - run: two lines\n    pty: true\n";
    var first = Record(yaml, _ => Pty((0.1, "one\r\n"), (0.3, "two\r\n")));
    var again = Record(yaml, _ => Pty((0.1, "one\r"), (0.12, "\ntw"), (0.31, "o\r\n")), first.Cast);
    return Expect(again.Cast == first.Cast, $"the cast differs: {FirstDifference(first.Cast, again.Cast)}")
        & Expect(again.Result.Same.Count == 1, "the step was not the same");
}

static bool OnlyTheChangedStepDiffers()
{
    var first = Record(ThreeSteps, step => ThreeStepsOutput(step, 0));
    var again = Record(ThreeSteps, step => step.Command.Cmd == "count"
        ? Pty((0.05, "1\r\n"), (0.2, "2\r\n"), (0.5, "3\r\n"), (0.9, "4\r\n"))
        : ThreeStepsOutput(step, 0.0051), first.Cast);

    // The first step's events and the third step's output keep their lines, timing included.
    var before = EventLines(first.Cast);
    var after = EventLines(again.Cast);
    var keptLines = before.Where(static line => line.Contains("hello", StringComparison.Ordinal) || line.Contains("world", StringComparison.Ordinal) || line.Contains("three\\r\\n", StringComparison.Ordinal)).ToList();
    return Expect(again.Result.Changed is [{ Number: 2 }], $"changed: {string.Join(", ", again.Result.Changed.Select(static s => s.Number))}")
        & Expect(again.Result.Same.Count == 2, $"same {again.Result.Same.Count}")
        & Expect(keptLines.Count == 3 && keptLines.All(line => after.Contains(line)), $"lost a line of an unchanged step: {string.Join(" ", keptLines)}")
        & Expect(after.Any(static line => line.Contains("4\\r\\n", StringComparison.Ordinal)), "the changed step's new output is missing");
}

static bool KeepOutputKeepsTheEarlierOutput()
{
    const string yaml = "steps:\n  - run: date\n    pty: true\n    keep-output: true\n  - echo after\n";
    var first = Record(yaml, step => step.Command.Cmd == "date" ? Pty((0.1, "Mon 10:00:01\r\n")) : Pipe("after\n"));
    var again = Record(yaml, step => step.Command.Cmd == "date" ? Pty((0.13, "Tue 11:22:33\r\n")) : Pipe("after\n"), first.Cast);
    return Expect(again.Cast == first.Cast, $"the cast differs: {FirstDifference(first.Cast, again.Cast)}")
        & Expect(again.Result.Kept is [{ Number: 1 }], "the step was not kept");
}

static bool KeepOutputOfInstantOutput()
{
    // A pipe step's output appears at once, at the step's execution-duration: the earlier text takes this run's time.
    const string yaml = "steps:\n  - run: uuidgen\n    keep-output: true\n  - echo after\n";
    var first = Record(yaml, step => step.Command.Cmd == "uuidgen" ? Pipe("1111-aaaa\n") : Pipe("after\n"));
    var again = Record(yaml, step => step.Command.Cmd == "uuidgen" ? Pipe("2222-bbbb\n") : Pipe("after\n"), first.Cast);
    var empty = Record(yaml, step => step.Command.Cmd == "uuidgen" ? Pipe("") : Pipe("after\n"), first.Cast);
    return Expect(again.Cast == first.Cast, $"the cast differs: {FirstDifference(first.Cast, again.Cast)}")
        & Expect(again.Result.Kept.Count == 1, "the step was not kept")
        & Expect(empty.Cast == first.Cast, $"a step that printed nothing this time: {FirstDifference(first.Cast, empty.Cast)}");
}

static bool NewTimingKeysTakeEffect()
{
    // The same output, but max-duration now shortens it: the earlier timing would leave the new key without effect.
    const string yaml = "steps:\n  - run: slow\n    pty: true\n";
    var first = Record(yaml, _ => Pty((0.1, "a\r\n"), (1.5, "b\r\n")));
    var again = Record(yaml + "    max-duration: 0.5\n", _ => Pty((0.1, "a\r\n"), (1.5, "b\r\n")), first.Cast);
    var bLine = static (string cast) => EventLines(cast).First(static line => line.Contains("b\\r\\n", StringComparison.Ordinal));
    return Expect(again.Result.Same.Count == 1, "the output is the same")
        & Expect(bLine(again.Cast) != bLine(first.Cast), $"the earlier timing was kept: {bLine(again.Cast)}");
}

static bool TypingChangesKeepOutputTiming()
{
    // Typing is timed by the YAML, so a new typing speed applies, while the PTY output keeps its earlier timing.
    var first = Record(ThreeSteps, step => ThreeStepsOutput(step, 0));
    var faster = "settings:\n  typing-speed: 0.02\n" + ThreeSteps;
    var again = Record(faster, step => ThreeStepsOutput(step, 0.0029), first.Cast);
    var typed = static (string cast) => EventLines(cast).Where(static line => line.Contains("\"o\",\"e\"]", StringComparison.Ordinal)).ToList();
    return Expect(again.Result.Same.Count == 3, $"same {again.Result.Same.Count}")
        & Expect(ThreeStepsLines(again.Cast).SequenceEqual(ThreeStepsLines(first.Cast)), $"the output lost its timing: {string.Join(" ", ThreeStepsLines(again.Cast))}")
        & Expect(!typed(again.Cast).SequenceEqual(typed(first.Cast)), "the new typing speed has no effect");
}

static bool StepsAreFoundByRunText()
{
    // The steps move after a new first step, and keep their earlier output timing.
    var first = Record(ThreeSteps, step => ThreeStepsOutput(step, 0));
    var withNewStep = "steps:\n  - echo first\n" + ThreeSteps["steps:\n".Length..];
    var again = Record(withNewStep, step => step.Command.Cmd == "echo first" ? Pipe("first\n") : ThreeStepsOutput(step, 0.0043), first.Cast);
    return Expect(again.Result.Added is [{ Number: 1 }], $"added: {string.Join(", ", again.Result.Added.Select(static s => s.Number))}")
        & Expect(again.Result.Same.Count == 3, $"same {again.Result.Same.Count}")
        & Expect(ThreeStepsLines(again.Cast).SequenceEqual(ThreeStepsLines(first.Cast)), $"the moved steps lost their timing: {string.Join(" ", ThreeStepsLines(again.Cast))}");
}

static bool RepeatedCommandsMatchInOrder()
{
    const string yaml = "steps:\n  - run: next\n    pty: true\n  - run: next\n    pty: true\n";
    var count = 0;
    CommandExecution Next(double jitter, bool swap) => (count++ % 2 == 0) != swap ? Pty((0.1 + jitter, "A\r\n")) : Pty((0.2 + jitter, "B\r\n"));
    var first = Record(yaml, _ => Next(0, false));
    var again = Record(yaml, _ => Next(0.0061, false), first.Cast);
    var swapped = Record(yaml, _ => Next(0, true), first.Cast);
    return Expect(again.Cast == first.Cast, $"the cast differs: {FirstDifference(first.Cast, again.Cast)}")
        & Expect(swapped.Result.Changed.Count == 2, $"swapped outputs: changed {swapped.Result.Changed.Count}");
}

static bool FilteredEventsKeepTheirSteps()
{
    // ConPTY's title holds the shell's path. The filter drops an event that held only that, so the steps after it move.
    const string yaml = "steps:\n  - run: titled\n    pty: true\n  - run: after\n    pty: true\n";
    CommandExecution Output(StepStart step, double jitter) => step.Command.Cmd == "titled"
        ? Pty((0.05 + jitter, "\u001b]0;C:\\Users\\someone\\bash.exe\u0007"), (0.1 + jitter, "titled\r\n"))
        : Pty((0.1 + 2 * jitter, "after\r\n"), (0.3 + jitter, "done\r\n"));
    var first = Record(yaml, step => Output(step, 0));
    var again = Record(yaml, step => Output(step, 0.0047), first.Cast);
    return Expect(!first.Cast.Contains("someone", StringComparison.Ordinal), "the title was not removed")
        & Expect(again.Cast == first.Cast, $"the cast differs: {FirstDifference(first.Cast, again.Cast)}")
        & Expect(again.Result.Same.Count == 2, $"same {again.Result.Same.Count}");
}

static bool NoEarlierCastChangesNothing()
{
    var scenario = ParseScenario(ThreeSteps);
    var options = new GenerateOptions { LogRunning = false, Slots = [] };
    var (events, _) = GenerateAsync(scenario, 7, options, step => Task.FromResult(ThreeStepsOutput(step, 0))).GetAwaiter().GetResult();
    var map = new int[events.Count + 1];
    events = CastOscFilter.RemovePrivateSequences(events, map);
    var result = Rerecording.Apply(events, Rerecording.MapIndices(options.Slots!, map), null);
    return Expect(result.Events.SequenceEqual(events), "the events changed")
        & Expect(result.Added.Count == 3 && result.KeepMissing.Count == 0, $"added {result.Added.Count}");
}

static bool TagRoundTrips()
{
    StepSlot[] slots =
    [
        new(1, "a", 0x0123456789abcdefUL, 0xdeadbeefU, true, false, 4, 3, 1.2345, 2.5, 1.3),
        new(2, "b", 42UL, 7U, false, true, 9, 0, 3.0004, 3.05, 3.05),
    ];
    var tag = Rerecording.FormatTag(slots);
    var ok = Expect(tag == "st:steps=0123456789abcdef:deadbeef:c:4:3:1235:2500 000000000000002a:00000007:d:9:0:3000:3050", tag)
        & Expect(Rerecording.TryParseTag(tag[Rerecording.TagPrefix.Length..], 9, out var steps) && steps.Count == 2, "parse")
        && Expect(Describe(steps[0]) == "81985529216486895 3735928559 True 4 3 1235 2500", Describe(steps[0]))
        & Expect(Describe(steps[1]) == "42 7 False 9 0 3000 3050", Describe(steps[1]));
    // An entry that names events the cast does not have, or that is malformed, makes the tag unusable.
    const string id = "0000000000000000:00000000";
    foreach (var bad in new[] { $"{id}:c:5:5:0:1", $"{id}:x:0:1:0:1", $"{id}:c:0:1:0", "zz00000000000000:00000000:c:0:1:0:1", "0:0:c:0:1:0:1", $"{id}:c:-1:1:0:1", "0000000000000000:0000000A:c:0:1:0:1" })
        ok &= Expect(!Rerecording.TryParseTag(bad, 9, out _), $"accepted {bad}");
    return ok;

    static string Describe(PreviousStep step) => $"{step.Id} {step.Timing} {step.Clock} {step.First} {step.Count} {step.StartMs} {step.EndMs}";
}

static bool EarlierCastProblems()
{
    var directory = Path.Combine(Path.GetTempPath(), $"scenetake-rerecord-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    try
    {
        string? Problem(string text)
        {
            var path = Path.Combine(directory, "earlier.cast");
            File.WriteAllText(path, text);
            return PreviousCast.TryRead(path, out var problem) is null ? problem : "(read)";
        }

        var valid = Record(ThreeSteps, step => ThreeStepsOutput(step, 0)).Cast;
        return Expect(PreviousCast.TryRead(Path.Combine(directory, "missing.cast"), out var missing) is null && missing is null, $"a missing file: {missing}")
            & Expect(Problem(valid) == "(read)", $"a valid cast: {Problem(valid)}")
            & Expect(Problem("{\"version\":3,\"term\":{\"cols\":80,\"rows\":24}}\n[0.5,\"o\",\"$ \"]\n") is { } noTag && noTag.Contains("no step ranges", StringComparison.Ordinal), "a cast without the tag")
            & Expect(Problem("{\"version\":2,\"width\":80,\"height\":24}\n") is { } v2 && v2.Contains("v3", StringComparison.Ordinal), "a v2 cast")
            & Expect(Problem("not json\n") is { } garbage && garbage.Contains("not a cast", StringComparison.Ordinal), "a file that is not JSON")
            & Expect(Problem(valid.Replace("[0.050,\"o\",\"h\"]", "[0.050,\"i\",\"h\"]", StringComparison.Ordinal)) is { } input && input.Contains("does not write", StringComparison.Ordinal), $"an input event: {Problem(valid.Replace("[0.050,\"o\",\"h\"]", "[0.050,\"i\",\"h\"]", StringComparison.Ordinal))}");
    }
    finally
    {
        Directory.Delete(directory, recursive: true);
    }
}

static bool KeepOutputKeyRules()
{
    string? Error(string steps) => FindStepKeyError(ParseScenario("steps:\n" + steps));
    var unknown = FindUnknownScenarioKeys("steps:\n  - run: date\n    keep-output: true\n");
    return Expect(unknown.Count == 0, $"unknown keys: {string.Join("; ", unknown)}")
        & Expect(Error("  - run: date\n    keep-output: true\n") is null, "a shown step")
        & Expect(Error("  - run: date\n    show: none\n    keep-output: true\n") is { } none && none.Contains("'keep-output' has no effect with 'show: none'", StringComparison.Ordinal), $"show: none: {Error("  - run: date\n    show: none\n    keep-output: true\n")}")
        & Expect(Error("  - run: serve\n    background: true\n    keep-output: true\n") is { } background && background.Contains("'keep-output' does not apply to a 'background: true' step", StringComparison.Ordinal), "background")
        & Expect(Error("  - clear: true\n    keep-output: true\n") is not null, "clear");
}

static bool DescribeSaysWhatChanged()
{
    StepSlot Slot(int number, string run, bool keep = false) => new(number, run, (ulong)number, 0, true, keep, 0, 0, 0, 0, 0);
    var result = new RerecordResult([], [Slot(1, "ls"), Slot(2, "date", keep: true), Slot(3, "npm test"), Slot(4, "uuidgen", keep: true), Slot(5, "new one")],
        [Slot(1, "ls")], [Slot(2, "date", keep: true)], [Slot(3, "npm test")], [Slot(5, "new one")], [Slot(4, "uuidgen", keep: true)]);
    var lines = Rerecording.Describe(result, hadPrevious: true, problem: null, "demo.cast");
    var allSame = Rerecording.Describe(new RerecordResult([], [Slot(1, "ls")], [Slot(1, "ls")], [], [], [], []), true, null, "demo.cast");
    var first = Rerecording.Describe(new RerecordResult([], [Slot(1, "date", keep: true)], [], [], [], [Slot(1, "date", keep: true)], []), false, null, "demo.cast");
    var quiet = Rerecording.Describe(new RerecordResult([], [Slot(1, "ls")], [], [], [], [Slot(1, "ls")], []), false, null, "demo.cast");
    var problem = Rerecording.Describe(new RerecordResult([], [Slot(1, "ls")], [], [], [], [Slot(1, "ls")], []), false, "it has no step ranges: an earlier scenetake or another tool wrote it", "demo.cast");
    return Expect(lines is [
            "Compared with the earlier cast: 1 of 5 steps printed the same, 1 kept the earlier output (keep-output), 1 changed, 2 are new",
            "  changed: step 3: npm test",
            "  new: step 5: new one",
            "Note: step 4 sets keep-output, but the earlier cast has no output of it, so it shows what it printed now: uuidgen"],
            string.Join("\n", lines))
        & Expect(allSame is ["Compared with the earlier cast: all 1 step printed the same"], string.Join("\n", allSame))
        & Expect(first is ["Note: keep-output: there is no earlier cast at demo.cast yet, so every step shows what it printed now."], string.Join("\n", first))
        & Expect(quiet.Count == 0, string.Join("\n", quiet))
        & Expect(problem is ["Note: the earlier demo.cast was not compared with this recording: it has no step ranges: an earlier scenetake or another tool wrote it. The next recording compares with this one."], string.Join("\n", problem));
}

static bool TryGetBinary(out string bin)
{
    bin = Environment.GetEnvironmentVariable("SCENETAKE_BIN") ?? "";
    if (!string.IsNullOrWhiteSpace(bin) && File.Exists(bin))
        return true;

    Console.Error.WriteLine("  skip: set SCENETAKE_BIN to a published scenetake binary");
    return false;
}

static (int ExitCode, string Stderr) RunScenetake(string bin, string directory, params string[] args)
{
    var psi = new ProcessStartInfo(bin)
    {
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        WorkingDirectory = directory,
    };
    foreach (var arg in args)
        psi.ArgumentList.Add(arg);

    using var process = Process.Start(psi)!;
    var stdout = process.StandardOutput.ReadToEndAsync();
    var stderr = process.StandardError.ReadToEndAsync();
    if (!process.WaitForExit(TimeSpan.FromSeconds(120)))
    {
        process.Kill(entireProcessTree: true);
        throw new TimeoutException("scenetake ran for over 120 s");
    }

    return (process.ExitCode, stdout.Result + stderr.Result);
}

// Runs the test in a new folder with demo.yaml, and removes the folder after.
static bool WithScenario(string yaml, Func<string, string, bool> test)
{
    if (!TryGetBinary(out var bin))
        return true;

    var directory = Path.Combine(Path.GetTempPath(), $"scenetake-rerecord-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    try
    {
        File.WriteAllText(Path.Combine(directory, "demo.yaml"), yaml);
        return test(bin, directory);
    }
    finally
    {
        Directory.Delete(directory, recursive: true);
    }
}

static bool IntegrationRecordingAgainGivesSameFiles(string repoRoot) => WithScenario("""
    shell: bash
    steps:
      - run: printf 'hello\n'; sleep 0.3; printf 'world\n'
        pty: true
      - run: echo "random $RANDOM$RANDOM"
        pty: true
        keep-output: true
      - echo plain
    """, (bin, directory) =>
{
    var first = RunScenetake(bin, directory, "--format", "svg", "demo.yaml", "demo");
    var cast = File.ReadAllText(Path.Combine(directory, "demo.cast"));
    var svg = File.ReadAllText(Path.Combine(directory, "demo.svg"));
    var again = RunScenetake(bin, directory, "--format", "svg", "demo.yaml", "demo");
    var castAgain = File.ReadAllText(Path.Combine(directory, "demo.cast"));
    var svgAgain = File.ReadAllText(Path.Combine(directory, "demo.svg"));
    return Expect(first.ExitCode == 0 && again.ExitCode == 0, $"exit {first.ExitCode}, {again.ExitCode}\n{first.Stderr}\n{again.Stderr}")
        && Expect(first.Stderr.Contains("Note: keep-output: there is no earlier cast at demo.cast yet", StringComparison.Ordinal), first.Stderr)
        & Expect(again.Stderr.Contains("Compared with the earlier cast: 2 of 3 steps printed the same, 1 kept the earlier output (keep-output)", StringComparison.Ordinal), again.Stderr)
        & Expect(castAgain == cast, $"the cast differs: {FirstDifference(cast, castAgain)}")
        & Expect(svgAgain == svg, "the SVG differs");
});

static bool IntegrationChangedAndKeptSteps(string repoRoot) => WithScenario("""
    shell: bash
    steps:
      - run: printf 'same\n'
        pty: true
      - echo "changes $RANDOM$RANDOM$RANDOM"
    """, (bin, directory) =>
{
    var first = RunScenetake(bin, directory, "demo.yaml", "demo");
    var cast = File.ReadAllText(Path.Combine(directory, "demo.cast"));
    var again = RunScenetake(bin, directory, "demo.yaml", "demo");
    var castAgain = File.ReadAllText(Path.Combine(directory, "demo.cast"));
    var changedLines = EventLines(cast).Zip(EventLines(castAgain)).Where(static pair => pair.First != pair.Second).ToList();
    return Expect(first.ExitCode == 0 && again.ExitCode == 0, $"exit {first.ExitCode}, {again.ExitCode}\n{first.Stderr}\n{again.Stderr}")
        && Expect(!first.Stderr.Contains("Compared with", StringComparison.Ordinal), first.Stderr)
        & Expect(again.Stderr.Contains("Compared with the earlier cast: 1 of 2 steps printed the same, 1 changed", StringComparison.Ordinal)
            && again.Stderr.Contains("  changed: step 2: echo \"changes $RANDOM$RANDOM$RANDOM\"", StringComparison.Ordinal), again.Stderr)
        // Only the changed output: the PTY step before it keeps its timing.
        & Expect(changedLines.Count == 1 && changedLines[0].First.Contains("changes ", StringComparison.Ordinal), $"changed lines: {string.Join(" | ", changedLines)}");
});

static partial class Program
{
    private const string ThreeSteps = """
        steps:
          - run: hello
            pty: true
          - run: count
            pty: true
          - echo three
        """;
}
