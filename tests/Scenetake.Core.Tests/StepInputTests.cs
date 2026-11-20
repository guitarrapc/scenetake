using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using static Scenetake.Core.ScenarioCast;
using Scenetake.TestSupport;

namespace Scenetake.Core.Tests;

// Step keys `keys` and `timeout` (.github/docs/spec_scenario.md → steps, spec_pty.md → Keys and timeout).
// The parsing tests need only the sources. The integration tests record fixtures with a published binary
// (SCENETAKE_BIN) and need bash; on Windows that is Git Bash.

[Category("Smoke")]
public sealed class StepInputTests
{
    [Test]
    public async Task KeyNamesGiveTerminalBytes() => await Assert.That(StepInputCases.KeyNamesGiveTerminalBytes()).IsTrue();

    [Test]
    public async Task StepInputErrors() => await Assert.That(StepInputCases.StepInputErrors()).IsTrue();

    [Test]
    public async Task ValidStepInputHasNoError() => await Assert.That(StepInputCases.ValidStepInputHasNoError()).IsTrue();

    [Test]
    public async Task KeyActionsTimeKeysAndTyping() => await Assert.That(StepInputCases.KeyActionsTimeKeysAndTyping()).IsTrue();

    [Test]
    public async Task KeyActionsWaitForText() => await Assert.That(StepInputCases.KeyActionsWaitForText()).IsTrue();

    [Test]
    public async Task KeyEntryKeysAreChecked() => await Assert.That(StepInputCases.KeyEntryKeysAreChecked()).IsTrue();

    [Test]
    public async Task PlaygroundIgnoresKeysAndTimeout() => await Assert.That(StepInputCases.PlaygroundIgnoresKeysAndTimeout()).IsTrue();

    [Test]
    public async Task IntegrationKeysReachTheCommand() => await Assert.That(StepInputCases.IntegrationKeysReachTheCommand(TestEnv.RepoRoot)).IsTrue();

    [Test]
    public async Task IntegrationTimeoutStopsCommands() => await Assert.That(StepInputCases.IntegrationTimeoutStopsCommands(TestEnv.RepoRoot)).IsTrue();

    [Test]
    public async Task IntegrationPipeStepGetsNoInput() => await Assert.That(StepInputCases.IntegrationPipeStepGetsNoInput(TestEnv.RepoRoot)).IsTrue();

    [Test]
    public async Task IntegrationKeysWithoutPtyStopBeforeRecording() => await Assert.That(StepInputCases.IntegrationKeysWithoutPtyStopBeforeRecording(TestEnv.RepoRoot)).IsTrue();

    [Test]
    public async Task IntegrationWaitTypesWhenTheTextAppears() => await Assert.That(StepInputCases.IntegrationWaitTypesWhenTheTextAppears(TestEnv.RepoRoot)).IsTrue();

    [Test]
    public async Task IntegrationWaitGivesUp() => await Assert.That(StepInputCases.IntegrationWaitGivesUp(TestEnv.RepoRoot)).IsTrue();

    [Test]
    public async Task IntegrationSaveKeysNeedsAnInteractiveStep() => await Assert.That(StepInputCases.IntegrationSaveKeysNeedsAnInteractiveStep(TestEnv.RepoRoot)).IsTrue();
}

internal static class StepInputCases
{

internal static bool Expect(bool condition, string message)
{
    if (!condition)
        Console.Error.WriteLine($"  {message}");
    return condition;
}

internal static bool KeyNamesGiveTerminalBytes()
{
    var cases = new (string Name, string? Text)[]
    {
    ("enter", "\r"), ("Return", "\r"), ("tab", "\t"), ("space", " "), ("backspace", "\u007f"),
    ("escape", "\u001b"), ("ESC", "\u001b"),
    ("up", "\u001b[A"), ("down", "\u001b[B"), ("right", "\u001b[C"), ("left", "\u001b[D"),
    ("home", "\u001b[H"), ("end", "\u001b[F"), ("page-up", "\u001b[5~"), ("pagedown", "\u001b[6~"),
    ("insert", "\u001b[2~"), ("delete", "\u001b[3~"), ("f1", "\u001bOP"), ("F5", "\u001b[15~"), ("f12", "\u001b[24~"),
    ("ctrl+c", "\u0003"), ("CTRL+Z", "\u001a"), ("ctrl+a", "\u0001"), (" ctrl+d ", "\u0004"),
    ("alt+x", "\u001bx"), ("alt+.", "\u001b."),
    // Not keys scenetake knows: a typo, ctrl with a digit, alt with nothing or a name, f13.
    ("entr", null), ("ctrl+1", null), ("ctrl+", null), ("alt+", null), ("alt+up", null), ("f13", null), ("", null),
    };

    var ok = true;
    foreach (var (name, text) in cases)
        ok &= Expect(KeyText(name) == text, $"KeyText(\"{name}\") gave {Show(KeyText(name))}, expected {Show(text)}");
    return ok;
}

internal static bool StepInputErrors()
{
    var cases = new (string Steps, string Expected)[]
    {
    ("- run: cat\n  keys: [{key: enter}]", "Step 1 key 'keys' requires 'pty: true'"),
    ("- run: cat\n  pty: true\n  interactive: true\n  keys: [{key: enter}]", "Step 1 key 'keys' does not apply to 'interactive: true'"),
    ("- run: cat\n  pty: true\n  interactive: true\n  timeout: 5", "Step 1 key 'timeout' does not apply to 'interactive: true'"),
    ("- run: cat\n  timeout: 0", "Step 1 key 'timeout' must be a number of seconds above 0, such as 5, not '0'"),
    ("- run: cat\n  timeout: -1", "must be a number of seconds above 0"),
    ("- run: cat\n  timeout: 5s", "must be a number of seconds above 0, such as 5, not '5s'"),
    ("- run: cat\n  timeout: true", "must be a number of seconds above 0, such as 5, not 'true'"),
    ("- run: cat\n  timeout: [5]", "must be a number of seconds above 0, such as 5, not a list"),
    ("- run: cat\n  pty: true\n  keys: enter", "Step 1 key 'keys' must be a list"),
    ("- run: cat\n  pty: true\n  keys: [enter]", "Step 1 keys entry 1 must be a mapping with 'sleep', 'wait', 'type', or 'key'"),
    ("- run: cat\n  pty: true\n  keys: [{kye: q}]", "Step 1 keys entry 1 needs 'sleep', 'wait', 'type', or 'key'"),
    ("- run: cat\n  pty: true\n  keys: [{type: a, key: enter}]", "Step 1 keys entry 1 has both 'type' and 'key'"),
    ("- run: cat\n  pty: true\n  keys: [{sleep: -1}]", "Step 1 keys entry 1 'sleep' must be a number of seconds"),
    ("- run: cat\n  pty: true\n  keys: [{sleep: 1s}]", "'sleep' must be a number of seconds, such as 0.5, not '1s'"),
    ("- run: cat\n  pty: true\n  keys: [{type: ''}]", "Step 1 keys entry 1 'type' must be text to type"),
    ("- run: cat\n  pty: true\n  keys: [{type: [a, b]}]", "'type' must be text to type, not a list"),
    ("- run: cat\n  pty: true\n  keys: [{key: entr}]", "Step 1 keys entry 1 has an unknown key 'entr'. Use a name such as enter"),
    ("- echo first\n- run: cat\n  pty: true\n  keys: [{sleep: 1}, {key: ctrl+1}]", "Step 2 keys entry 2 has an unknown key 'ctrl+1'"),
    ("- run: cat\n  pty: true\n  keys: [{wait: ''}]", "Step 1 keys entry 1 'wait' must be text the program prints, such as \"[y/N]\", not ''"),
    // Spaces and line breaks at the ends do not count when the output is matched, so they alone match nothing.
    ("- run: cat\n  pty: true\n  keys: [{wait: \"  \\n\"}]", "Step 1 keys entry 1 'wait' must be text the program prints"),
    ("- run: cat\n  pty: true\n  keys: [{wait: [a]}]", "'wait' must be text the program prints, such as \"[y/N]\", not a list"),
    ("- run: cat\n  pty: true\n  keys: [{wait: {a: b}}]", "'wait' must be text the program prints"),
    };

    var ok = true;
    foreach (var (steps, expected) in cases)
    {
        var error = FindStepKeyError(ParseScenario("steps:\n" + Indent(steps)));
        ok &= Expect(error is not null && error.Contains(expected, StringComparison.Ordinal),
            $"for\n{steps}\n  expected an error with \"{expected}\", got {Show(error)}");
    }

    return ok;
}

internal static bool ValidStepInputHasNoError()
{
    var yaml = """
    steps:
      - run: btop
        pty: true
        timeout: 4.5
        keys:
          - sleep: 2
          - sleep: 1
            key: Down
          - type: ":q"
          - type: 42
          - key: ctrl+c
          - wait: "[y/N]"
            type: "y\n"
          - sleep: 1
            wait: 42
            key: enter
          - wait: "done"
      - run: ping localhost
        timeout: 3
    """;
    var error = FindStepKeyError(ParseScenario(yaml));
    return Expect(error is null, $"expected no error, got {Show(error)}")
        & Expect(FindUnknownScenarioKeys(yaml).Count == 0, $"expected no unknown keys, got {string.Join("; ", FindUnknownScenarioKeys(yaml))}");
}

internal static bool KeyActionsTimeKeysAndTyping()
{
    var step = ParseCommand(ParseScenario("""
    steps:
      - run: vim
        pty: true
        keys:
          - sleep: 1
          - sleep: 0.5
            key: escape
          - type: "a😀\nb"
          - key: enter
          - sleep: 2
    """).Steps![0]);

    var actions = BuildKeyActions(step.Extra, speed: 0.1, jitter: 0, new Random(1));
    var expected = new (double Delay, string Text)[]
    {
    (1.5, "\u001b"),   // both sleeps, then the key
    (0, "a"),          // typing starts right after the key
    (0.1, "😀"),       // one grapheme, after a typing delay
    (0.1, "\r"),       // a line break is Enter
    (0.1, "b"),
    (0.1, "\r"),       // the key after typed text waits one typing delay
                       // The trailing sleep writes nothing, so it is no action.
    };

    var ok = Expect(actions.Count == expected.Length, $"expected {expected.Length} actions, got {actions.Count}: {string.Join(", ", actions.Select(a => $"{a.Delay}:{Show(a.Text)}"))}");
    for (var i = 0; ok && i < expected.Length; i++)
    {
        ok &= Expect(Math.Abs(actions[i].Delay - expected[i].Delay) < 1e-9 && actions[i].Text == expected[i].Text,
            $"action {i + 1}: got {actions[i].Delay}:{Show(actions[i].Text)}, expected {expected[i].Delay}:{Show(expected[i].Text)}");
    }

    // A step without keys draws nothing from the random sequence, so the typing of other steps is unchanged.
    var random = new Random(7);
    var none = BuildKeyActions(ParseCommand("echo hi").Extra, 0.1, 0.05, random);
    ok &= Expect(none.Count == 0 && random.Next() == new Random(7).Next(), "a step without keys drew from the random sequence");
    return ok;
}

internal static bool KeyActionsWaitForText()
{
    const string withWaits = """
    steps:
      - run: setup
        pty: true
        keys:
          - sleep: 1
            wait: "Continue? [y/N]"
            type: "yn"
          - wait: 42
          - key: enter
          - sleep: 0.5
    """;
    var actions = BuildKeyActions(ParseCommand(ParseScenario(withWaits).Steps![0]).Extra, speed: 0.1, jitter: 0, new Random(1));
    var expected = new (double Delay, string Text, string? Wait, int Entry)[]
    {
    (1, "", "Continue? [y/N]", 1),   // the sleep, then the wait, which writes nothing
    (0, "y", null, 1),               // typing starts when the text is there
    (0.1, "n", null, 1),
    (0.1, "", "42", 2),              // a number is text too, and the wait after typed text waits one typing delay
    (0, "\r", null, 3),
    };

    var ok = Expect(actions.Count == expected.Length, $"expected {expected.Length} actions, got {actions.Count}: {string.Join(", ", actions.Select(a => $"{a.Delay}:{Show(a.Text)}:{Show(a.Wait)}:{a.Entry}"))}");
    for (var i = 0; ok && i < expected.Length; i++)
    {
        var (delay, text, wait, entry) = expected[i];
        ok &= Expect(Math.Abs(actions[i].Delay - delay) < 1e-9 && actions[i].Text == text && actions[i].Wait == wait && actions[i].Entry == entry,
            $"action {i + 1}: got {actions[i].Delay}:{Show(actions[i].Text)}:{Show(actions[i].Wait)}:{actions[i].Entry}, expected {delay}:{Show(text)}:{Show(wait)}:{entry}");
    }

    // A wait draws no typing delay, so the steps after it type as they would without it.
    var withRandom = new Random(7);
    BuildKeyActions(ParseCommand(ParseScenario(withWaits).Steps![0]).Extra, 0.1, 0.05, withRandom);
    var withoutRandom = new Random(7);
    BuildKeyActions(ParseCommand(ParseScenario("""
    steps:
      - run: setup
        pty: true
        keys:
          - type: "yn"
          - key: enter
    """).Steps![0]).Extra, 0.1, 0.05, withoutRandom);
    ok &= Expect(withRandom.Next() == withoutRandom.Next(), "a wait drew from the random sequence");
    return ok;
}

internal static bool KeyEntryKeysAreChecked()
{
    var findings = FindUnknownScenarioKeys("""
    steps:
      - run: top
        pty: true
        timout: 3
        keys:
          - slep: 1
            key: q
          - sleep: 1
            type: x
            delay: 2
    """);
    var expected = new[]
    {
    "unknown key 'timout' in step 1 (did you mean 'timeout'?)",
    "unknown key 'slep' in step 1 keys entry 1 (did you mean 'sleep'?)",
    "unknown key 'delay' in step 1 keys entry 2",
};

    var ok = Expect(findings.Count == expected.Length, $"expected {expected.Length} findings, got: {string.Join("; ", findings)}");
    foreach (var message in expected)
        ok &= Expect(findings.Contains(message), $"missing finding \"{message}\" in: {string.Join("; ", findings)}");
    return ok;
}

// The playground has no command to type into or stop: the output it replays already holds what the keys did.
// The step still matches its recording, and the steps after it type as on the CLI.
internal static bool PlaygroundIgnoresKeysAndTimeout()
{
    const string withKeys = """
    width: 40
    height: 6
    steps:
      - run: cat
        pty: true
        timeout: 2
        keys:
          - type: "hi"
          - key: ctrl+d
      - echo after
    """;
    const string withoutKeys = """
    width: 40
    height: 6
    steps:
      - run: cat
        pty: true
        timeout: 2
      - echo after
    """;
    // What cat echoed in a 40x6 PTY, and the pipe output of echo.
    var recordedCat = new StepOutputs
    {
        Scenetake = "test",
        Os = "linux",
        Shell = "bash",
        Width = 40,
        Height = 6,
        Steps =
        [
            RecordedStep.Pty("cat", 0, null, [new TimedBytes(TimeSpan.FromSeconds(0.1), Encoding.UTF8.GetBytes("hi"))]),
        RecordedStep.Pipe("echo after", 0, [new PipeStreamChunk(TimeSpan.Zero, false, "after\n")]),
    ],
    };
    var result = PlaygroundRender.RenderScenario(withKeys, recordedCat, new PlaygroundOptions());
    var ok = Expect(result.Error is null, $"render failed: {result.Error}");
    ok &= Expect(result.Steps.All(static step => step.Recorded), "a step lost its recording");
    ok &= Expect(result.NotApplied.Count == 0, $"keys or timeout listed as not applied: {string.Join("; ", result.NotApplied)}");

    // The typed command after the keys step types with the delays the CLI draws: keys typing draws two delays
    // first. Without keys, `echo after` would start typing earlier in the random sequence.
    var plain = PlaygroundRender.RenderScenario(withoutKeys, recordedCat, new PlaygroundOptions());
    ok &= Expect(plain.Error is null, $"render without keys failed: {plain.Error}");
    ok &= Expect(!Encoding.UTF8.GetString(result.Cast!).Equals(Encoding.UTF8.GetString(plain.Cast!), StringComparison.Ordinal),
        "removing keys did not change the typing of later steps, so the playground does not draw their delays");
    return ok;
}

internal static bool IntegrationKeysReachTheCommand(string repoRoot)
{
    if (!TryGetBinary(out var bin))
        return true;

    var run = RunFixture(bin, repoRoot, "step-input-keys.yaml");
    return Expect(run.ExitCode == 0, $"exit {run.ExitCode}: {run.Stderr}")
        && Expect(run.Text.Contains("hello:abc", StringComparison.Ordinal), $"read did not get the typed keys; cast text: {run.Text}");
}

internal static bool IntegrationTimeoutStopsCommands(string repoRoot)
{
    if (!TryGetBinary(out var bin))
        return true;

    var run = RunFixture(bin, repoRoot, "step-input-timeout.yaml");
    // Three steps of 1 s, one of them with the 2 s Ctrl+C grace and the SIGTERM second, plus typing and startup.
    // Without the timeouts the run takes at least 90 s.
    var ok = Expect(run.ExitCode == 0, $"exit {run.ExitCode}: {run.Stderr}");
    ok &= Expect(run.Elapsed < TimeSpan.FromSeconds(40), $"took {run.Elapsed.TotalSeconds:F1} s");
    ok &= Expect(run.Text.Contains("interrupted", StringComparison.Ordinal), $"Ctrl+C did not reach the trap; cast text: {run.Text}");
    ok &= Expect(run.Text.Contains("pipe-started", StringComparison.Ordinal), "the pipe step lost what it printed before the timeout");
    ok &= Expect(run.Text.Contains("after-timeouts", StringComparison.Ordinal), "the step after the timeouts did not run");
    ok &= Expect(Regex.Matches(run.Stderr, @"stopped after 1 s \(timeout\)").Count == 3, $"expected three stop lines in stderr: {run.Stderr}");
    ok &= Expect(!run.Stderr.Contains("exited with code", StringComparison.Ordinal), $"a stop by timeout warned as a failure: {run.Stderr}");
    return ok;
}

internal static bool IntegrationPipeStepGetsNoInput(string repoRoot)
{
    if (!TryGetBinary(out var bin))
        return true;

    // scenetake's input is a pipe that stays open with nothing in it, as a terminal is when no one types. pre and a
    // step that read it get the end of input at once.
    var run = RunFixtureWithInput(bin, repoRoot, "step-input-stdin.yaml", keepInputOpen: true, []);
    var ok = Expect(run.ExitCode == 0, $"exit {run.ExitCode}: {run.Stderr}");
    ok &= Expect(run.Elapsed < TimeSpan.FromSeconds(30), $"took {run.Elapsed.TotalSeconds:F1} s");
    ok &= Expect(run.Text.Contains("step-read:[]", StringComparison.Ordinal) && run.Text.Contains("after-read", StringComparison.Ordinal),
        $"cast text: {run.Text}");
    ok &= Expect(run.Stderr.Contains("pre-read:[]", StringComparison.Ordinal), $"stderr: {run.Stderr}");
    return ok;
}

internal static bool IntegrationKeysWithoutPtyStopBeforeRecording(string repoRoot)
{
    if (!TryGetBinary(out var bin))
        return true;

    var run = RunFixture(bin, repoRoot, "step-input-keys-without-pty.yaml");
    return Expect(run.ExitCode != 0, "a keys step without pty was recorded")
        && Expect(run.Stderr.Contains("Step 1 key 'keys' requires 'pty: true'", StringComparison.Ordinal), $"stderr: {run.Stderr}")
        && Expect(!run.Stderr.Contains("running:", StringComparison.Ordinal), "a step ran before the error");
}

internal static bool IntegrationWaitTypesWhenTheTextAppears(string repoRoot)
{
    if (!TryGetBinary(out var bin))
        return true;

    var run = RunFixture(bin, repoRoot, "step-input-wait.yaml");
    var ok = Expect(run.ExitCode == 0, $"exit {run.ExitCode}: {run.Stderr}");
    // Typed at once, the answer would go to the read before the prompt, and the prompt would get nothing.
    ok &= Expect(run.Text.Contains("hello:abc early:.", StringComparison.Ordinal),
        $"the answer was not typed after the prompt; cast text: {run.Text}");
    // Each wait for the same prompt finds a prompt of its own: a second wait that matched the first prompt again
    // would type b before the second early read, which would take it.
    ok &= Expect(run.Text.Contains("got:a\r", StringComparison.Ordinal) && run.Text.Contains("got:b\r", StringComparison.Ordinal),
        $"a prompt did not get its answer; cast text: {run.Text}");
    ok &= Expect(!run.Stderr.Contains("goes on without it", StringComparison.Ordinal), $"a wait gave up: {run.Stderr}");
    return ok;
}

internal static bool IntegrationWaitGivesUp(string repoRoot)
{
    if (!TryGetBinary(out var bin))
        return true;

    var run = RunFixture(bin, repoRoot, "step-input-wait-missing.yaml");
    var ok = Expect(run.ExitCode == 0, $"exit {run.ExitCode}: {run.Stderr}");
    // Without a timeout the wait gives up after 10 s and the keys go on: Ctrl+C ends the sleep.
    ok &= Expect(run.Stderr.Contains("Warning: step 1 keys entry 1 waited 10 s for \"never printed\" and goes on without it", StringComparison.Ordinal),
        $"no warning for the wait that gave up: {run.Stderr}");
    // With a timeout the wait lasts until it, and the stop is no warning.
    ok &= Expect(run.Stderr.Contains("stopped after 2 s (timeout)", StringComparison.Ordinal), $"the step timeout did not stop the wait: {run.Stderr}");
    ok &= Expect(Regex.Matches(run.Stderr, "goes on without it").Count == 1, $"the step with a timeout gave up its wait: {run.Stderr}");
    ok &= Expect(run.Text.Contains("after-waits", StringComparison.Ordinal), "the step after the waits did not run");
    // 10 s, the 2 s timeout, typing, startup. Without the limit the first sleep alone takes 30 s.
    ok &= Expect(run.Elapsed < TimeSpan.FromSeconds(28), $"took {run.Elapsed.TotalSeconds:F1} s");
    return ok;
}

internal static bool IntegrationSaveKeysNeedsAnInteractiveStep(string repoRoot)
{
    if (!TryGetBinary(out var bin))
        return true;

    var keys = Path.Combine(Path.GetTempPath(), $"scenetake-keys-{Guid.NewGuid():N}.yaml");
    var run = RunFixture(bin, repoRoot, "step-input-keys.yaml", "--save-keys", keys);
    return Expect(run.ExitCode != 0, "--save-keys without an interactive step recorded")
        && Expect(run.Stderr.Contains("--save-keys saves the keys typed in 'interactive: true' steps, and this scenario has none", StringComparison.Ordinal), $"stderr: {run.Stderr}")
        && Expect(!run.Stderr.Contains("running:", StringComparison.Ordinal), "a step ran before the error")
        && Expect(!File.Exists(keys), "a keys file was written");
}

internal static bool TryGetBinary(out string bin)
{
    bin = TestEnv.Binary;
    return true;
}

internal static (int ExitCode, string Stderr, string Text, TimeSpan Elapsed) RunFixture(string bin, string repoRoot, string fixture, params string[] options) =>
    RunFixtureWithInput(bin, repoRoot, fixture, keepInputOpen: false, options);

// keepInputOpen: scenetake's input is a pipe that this process holds open and never writes to.
internal static (int ExitCode, string Stderr, string Text, TimeSpan Elapsed) RunFixtureWithInput(string bin, string repoRoot, string fixture, bool keepInputOpen, string[] options)
{
    var stem = Path.Combine(Path.GetTempPath(), $"scenetake-step-input-{Guid.NewGuid():N}");
    var psi = new ProcessStartInfo(bin)
    {
        RedirectStandardInput = keepInputOpen,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        WorkingDirectory = repoRoot,
    };
    foreach (var option in options)
        psi.ArgumentList.Add(option);
    psi.ArgumentList.Add(Path.Combine("tests", "fixtures", fixture));
    psi.ArgumentList.Add(stem);

    var stopwatch = Stopwatch.StartNew();
    using var process = Process.Start(psi)!;
    var stdout = process.StandardOutput.ReadToEndAsync();
    var stderr = process.StandardError.ReadToEndAsync();
    if (!process.WaitForExit(TimeSpan.FromSeconds(120)))
    {
        process.Kill(entireProcessTree: true);
        throw new TimeoutException($"{fixture} ran for over 120 s");
    }

    stopwatch.Stop();
    var cast = stem + ".cast";
    var text = File.Exists(cast) ? CastText(cast) : "";
    if (File.Exists(cast))
        File.Delete(cast);
    return (process.ExitCode, stdout.Result + stderr.Result, text, stopwatch.Elapsed);
}

// The output events of a cast as plain text, without escape sequences.
internal static string CastText(string path)
{
    var builder = new StringBuilder();
    foreach (var line in File.ReadLines(path).Skip(1))
    {
        using var json = JsonDocument.Parse(line);
        if (json.RootElement[1].GetString() == "o")
            builder.Append(json.RootElement[2].GetString());
    }

    return Regex.Replace(builder.ToString(), @"\x1b(\[[0-9;?]*[ -/]*[@-~]|\][^\x07\x1b]*(\x07|\x1b\\)|[()][0-9A-Za-z]|[=>78])", "");
}

internal static string Indent(string text) => string.Join('\n', text.Split('\n').Select(static line => "  " + line));

// A string with its control characters escaped, for messages. JsonSerializer has no reflection in these builds.
internal static string Show(string? text) =>
    text is null ? "null" : "\"" + string.Concat(text.Select(static c => char.IsControl(c) ? $"\\u{(int)c:x4}" : c.ToString())) + "\"";

}
