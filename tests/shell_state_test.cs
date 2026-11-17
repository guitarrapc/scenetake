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
#:include ../SecretMask.cs
#:include ../StepChunks.cs
#:include ../StepOutputs.cs
#:include ../PlaygroundRender.cs
#:include ../ShellState.cs

// Shell state across steps, and the step keys show and clear (.github/docs/spec_scenario.md → Shell state across
// steps, steps). The unit tests need only the sources. The integration tests record fixtures with a published binary
// (SCENETAKE_BIN) and need bash, on Windows Git Bash; the PowerShell one also needs pwsh.

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using static ScenarioCast;

var repoRoot = FindRepoRoot(Directory.GetCurrentDirectory());
var failures = 0;

failures += Run("DeclarationsSplitOutsideQuotes", DeclarationsSplitOutsideQuotes);
failures += Run("ScalarValuesAreUnquoted", ScalarValuesAreUnquoted);
failures += Run("RestoreCarriesWhatChanged", RestoreCarriesWhatChanged);
failures += Run("LoginAliasesDoNotTurnOnExpansion", LoginAliasesDoNotTurnOnExpansion);
failures += Run("QuoteKeepsSingleQuotes", QuoteKeepsSingleQuotes);
failures += Run("StateOnlyForBashAndPowerShell", StateOnlyForBashAndPowerShell);
failures += Run("PowerShellWrapsOnFirstAndLastLine", PowerShellWrapsOnFirstAndLastLine);
failures += Run("ShowAndClearErrors", ShowAndClearErrors);
failures += Run("ShowAndClearAreKnownKeys", ShowAndClearAreKnownKeys);
failures += Run("PlaygroundComposesShowAndClear", PlaygroundComposesShowAndClear);
failures += Run("HiddenStepFailureStops", () => HiddenStepFailureStops(continueOnError: false));
failures += Run("HiddenStepContinueOnError", () => HiddenStepFailureStops(continueOnError: true));
failures += Run("MaskerLearnsExportedSecrets", MaskerLearnsExportedSecrets);
failures += Run("IntegrationBashCarriesState", () => IntegrationBashCarriesState(repoRoot));
failures += Run("IntegrationPowerShellCarriesState", () => IntegrationPowerShellCarriesState(repoRoot));
failures += Run("IntegrationHiddenStepFailureStops", () => IntegrationHiddenStepFailureStops(repoRoot));

if (failures > 0)
{
    Console.Error.WriteLine($"Failed: {failures} shell state test(s)");
    return 1;
}

Console.Error.WriteLine("all shell state tests passed");
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

// bash 3.2 (macOS) writes a line break inside double quotes; bash 5.2 writes $'...'. Neither ends the statement.
static bool DeclarationsSplitOutsideQuotes()
{
    const string dump = "declare -x A=\"multi\nline \\\"q\\\" it's\"\n"
        + "declare -a ARR=([0]=\"one\" [1]=\"two words\")\n"
        + "declare -- N=\"5\"\n"
        + "declare -x B=$'x\\ny \\' z'\n"
        + "declare -- E\n"
        + "declare -ir EUID=\"1000\"\n";
    var map = BashDump.SplitDeclarations(dump);
    return Expect(map.Count == 6, $"statements: {string.Join(" | ", map.Keys)}")
        && Expect(map["A"] == "declare -x A=\"multi\nline \\\"q\\\" it's\"", $"A: {map["A"]}")
        && Expect(map["ARR"] == "declare -a ARR=([0]=\"one\" [1]=\"two words\")", $"ARR: {map["ARR"]}")
        && Expect(map["B"] == "declare -x B=$'x\\ny \\' z'", $"B: {map["B"]}")
        && Expect(map["E"] == "declare -- E", $"E: {map["E"]}")
        && Expect(map.ContainsKey("EUID"), "EUID missing");
}

static bool ScalarValuesAreUnquoted() =>
    Expect(BashDump.ScalarValue("declare -x A=\"a \\\"b\\\" \\$c \\\\ `d`\"") == "a \"b\" $c \\ `d`", "double quotes")
    && Expect(BashDump.ScalarValue("declare -x B=$'x\\ny\\tz \\'q\\''") == "x\ny\tz 'q'", "dollar quotes")
    && Expect(BashDump.ScalarValue("declare -x C='plain'") == "plain", "single quotes")
    && Expect(BashDump.ScalarValue("declare -a D=([0]=\"x\")") is null, "an array has no scalar value")
    && Expect(BashDump.ScalarValue("declare -- E") is null, "no value");

static bool RestoreCarriesWhatChanged()
{
    const string baseVars = "declare -x PATH=\"/usr/bin\"\ndeclare -- KEEP=\"same\"\ndeclare -- GONE=\"x\"\ndeclare -i RANDOM=\"1\"\ndeclare -- BASH_COMMAND=\"a\"\n";
    const string finalVars = "declare -x PATH=\"/venv/bin:/usr/bin\"\ndeclare -- KEEP=\"same\"\ndeclare -x NEW=\"secret-value-1\"\ndeclare -i RANDOM=\"2\"\ndeclare -- BASH_COMMAND=\"b\"\ndeclare -- __scenetake_dir=\"/tmp/x\"\ndeclare -- OLDPWD=\"/home\"\n";
    const string baseFunctions = "old () \n{ \n    :\n}\n";
    const string finalFunctions = "greet () \n{ \n    echo hi\n}\ndeclare -fx greet\n__scenetake_save () \n{ \n    :\n}\n";
    const string baseAliases = "alias ls='ls --color'\n";
    const string finalAliases = "alias hi='greet'\nalias ls='ls --color'\n";
    var exports = new List<KeyValuePair<string, string?>>();
    var restore = BashDump.BuildRestore(baseVars, finalVars, baseFunctions, finalFunctions, baseAliases, finalAliases, "/work/it's", exports);

    return Expect(restore.StartsWith("builtin cd -- '/work/it'\\''s'", StringComparison.Ordinal), $"cd first: {restore}")
        && Expect(restore.Contains("declare -x PATH=\"/venv/bin:/usr/bin\"\n", StringComparison.Ordinal), "changed PATH")
        && Expect(restore.Contains("declare -x NEW=\"secret-value-1\"\n", StringComparison.Ordinal), "new variable")
        && Expect(restore.Contains("declare -- OLDPWD=\"/home\"\n", StringComparison.Ordinal), "OLDPWD, for cd -")
        && Expect(!restore.Contains("KEEP", StringComparison.Ordinal), "an unchanged variable")
        && Expect(restore.Contains("builtin unset -v GONE\n", StringComparison.Ordinal), "a removed variable")
        && Expect(!restore.Contains("RANDOM", StringComparison.Ordinal) && !restore.Contains("BASH_COMMAND", StringComparison.Ordinal), "a variable bash changes by itself")
        && Expect(!restore.Contains("__scenetake", StringComparison.Ordinal), "scenetake's own names")
        && Expect(restore.Contains("greet () \n{ \n    echo hi\n}\ndeclare -fx greet\n", StringComparison.Ordinal), "a new exported function")
        && Expect(restore.Contains("builtin unset -f old\n", StringComparison.Ordinal), "a removed function")
        && Expect(restore.Contains("alias hi='greet'\n", StringComparison.Ordinal) && !restore.Contains("alias ls=", StringComparison.Ordinal), "only the new alias")
        && Expect(restore.Contains("builtin shopt -s expand_aliases\n", StringComparison.Ordinal), "alias expansion")
        && Expect(restore.IndexOf("declare -x PATH", StringComparison.Ordinal) < restore.IndexOf("greet ()", StringComparison.Ordinal)
            && restore.IndexOf("greet ()", StringComparison.Ordinal) < restore.IndexOf("alias hi", StringComparison.Ordinal), "variables, then functions, then aliases")
        && Expect(exports.Count == 2 && exports.Any(static e => e.Key == "PATH" && e.Value == "/venv/bin:/usr/bin") && exports.Any(static e => e.Key == "NEW" && e.Value == "secret-value-1"),
            $"exports: {string.Join(", ", exports.Select(static e => $"{e.Key}={e.Value}"))}");
}

// Git Bash's login files define aliases such as ls. Turning expansion on for them would change what ls prints.
static bool LoginAliasesDoNotTurnOnExpansion()
{
    const string aliases = "alias ll='ls -l'\nalias ls='ls -F --color=auto'\n";
    var restore = BashDump.BuildRestore("", "", "", "", aliases, aliases, "/work", []);
    return Expect(!restore.Contains("expand_aliases", StringComparison.Ordinal), restore)
        && Expect(!restore.Contains("alias ", StringComparison.Ordinal), restore);
}

static bool QuoteKeepsSingleQuotes() =>
    Expect(BashDump.Quote("it's") == "'it'\\''s'", BashDump.Quote("it's"))
    && Expect(PowerShellState.Literal("it's") == "'it''s'", PowerShellState.Literal("it's"));

static bool StateOnlyForBashAndPowerShell()
{
    using var bash = ShellState.Create(OperatingSystem.IsWindows() ? @"C:\Program Files\Git\usr\bin\bash.exe" : "/bin/bash", isPowerShell: false);
    using var pwsh = ShellState.Create("pwsh", isPowerShell: true);
    using var zsh = ShellState.Create("zsh", isPowerShell: false);
    using var sh = ShellState.Create("/bin/sh", isPowerShell: false);
    var ok = Expect(bash is BashState, "bash");
    ok &= Expect(pwsh is PowerShellState, "pwsh");
    ok &= Expect(zsh is null && sh is null, "other shells carry nothing");

    // bash: the command is unchanged, and BASH_ENV names the restore script.
    var prepared = bash!.Prepare("echo hi");
    ok &= Expect(prepared.Command == "echo hi", prepared.Command);
    ok &= Expect(prepared.Environment is { } env && env.TryGetValue("BASH_ENV", out var script) && File.Exists(script!), "BASH_ENV script");
    return ok;
}

// The user's lines keep their numbers: the restore shares the first line, and the save comes after the last.
static bool PowerShellWrapsOnFirstAndLastLine()
{
    using var state = ShellState.Create("pwsh", isPowerShell: true)!;
    var command = state.Prepare("Write-Output one\nWrite-Output two # a comment").Command;
    var lines = command.Split('\n');
    return Expect(lines.Length == 3, $"lines: {lines.Length}")
        && Expect(lines[0].EndsWith("try { Write-Output one", StringComparison.Ordinal), lines[0])
        && Expect(lines[1] == "Write-Output two # a comment", lines[1])
        && Expect(lines[2].StartsWith("$__scenetake_ok = $? }", StringComparison.Ordinal) && lines[2].EndsWith("if (-not $__scenetake_ok) { exit 1 }", StringComparison.Ordinal), lines[2]);
}

static bool ShowAndClearErrors()
{
    var ok = true;
    ok &= ExpectError("- run: echo hi\n  show: maybe", "Step 1 key 'show' must be all, output, or none");
    ok &= ExpectError("- run: echo hi\n  show: none\n  name: Hidden", "Step 1 key 'name' has no effect with 'show: none'");
    ok &= ExpectError("- run: echo hi\n  show: none\n  highlight: [{color: red, at: \"1\"}]", "Step 1 key 'highlight' has no effect with 'show: none'");
    ok &= ExpectError("- run: echo hi\n  show: none\n  post-delay: 1", "Step 1 key 'post-delay' has no effect with 'show: none'");
    ok &= ExpectError("- run: echo hi\n  show: output\n  run-highlight: bold", "Step 1 key 'run-highlight' styles the typed command, which 'show: output' leaves out");
    ok &= ExpectError("- run: echo hi\n  clear: true", "Step 1 has both 'run' and 'clear'");
    ok &= ExpectError("- echo hi\n- clear: false", "Step 2 key 'clear' must be true");
    ok &= ExpectError("- echo hi\n- clear: true\n  pty: true", "Step 2 key 'pty' does not apply to a 'clear: true' step");
    ok &= ExpectError("- run: echo hi\n  show: none\n- clear: true", "The scenario has no steps to record");
    ok &= ExpectError("- run: echo prepare\n  show: none\n  pty: true\n  timeout: 2\n  continue-on-error: true\n- run: echo banner\n  show: OUTPUT\n  name: Title\n- clear: true\n  name: Scene\n  post-delay: 1\n- echo hi", null);
    return ok;

    static bool ExpectError(string steps, string? expected)
    {
        var yaml = "steps:\n" + string.Join('\n', steps.Split('\n').Select(static line => "  " + line));
        var error = FindStepKeyError(ParseScenario(yaml));
        return expected is null
            ? Expect(error is null, $"unexpected error: {error}")
            : Expect(error is not null && error.StartsWith(expected, StringComparison.Ordinal), $"expected '{expected}', got '{error}'");
    }
}

static bool ShowAndClearAreKnownKeys()
{
    var unknown = FindUnknownScenarioKeys("steps:\n  - run: echo hi\n    show: none\n  - clear: true\n    name: Scene\n  - echo done\n");
    return Expect(unknown.Count == 0, string.Join("; ", unknown));
}

// The playground runs nothing: a hidden step takes no recorded output and no time, and the clear is the cast's own.
static bool PlaygroundComposesShowAndClear()
{
    const string yaml = """
        width: 40
        height: 8
        settings:
          typing-speed: 0.01
          typing-jitter: 0
          pre-delay: 0
          post-delay: 0
        steps:
          - run: mkdir demo && cd demo
            show: none
          - ls
          - run: banner
            show: output
          - clear: true
            name: Scene 2
          - echo after
        """;
    var outputs = new StepOutputs
    {
        Scenetake = "test",
        Os = "linux",
        Shell = "bash",
        Width = 40,
        Height = 8,
        Steps =
        [
            RecordedStep.Pipe("ls", 0, [new PipeStreamChunk(TimeSpan.Zero, false, "file.txt\n")]),
            RecordedStep.Pipe("banner", 0, [new PipeStreamChunk(TimeSpan.Zero, false, "=== BANNER ===\n")]),
            RecordedStep.Pipe("echo after", 0, [new PipeStreamChunk(TimeSpan.Zero, false, "after\n")]),
        ],
    };
    var result = PlaygroundRender.RenderScenario(yaml, outputs, new PlaygroundOptions(), renderSvg: false);
    if (!Expect(result.Error is null && result.Cast is not null, $"render failed: {result.Error}"))
        return false;

    var output = CastOutput(Encoding.UTF8.GetString(result.Cast!));
    var clear = output.IndexOf(ClearScreen, StringComparison.Ordinal);
    return Expect(!output.Contains("mkdir", StringComparison.Ordinal), "the hidden step was typed")
        && Expect(output.Contains("$ ls", StringComparison.Ordinal) && output.Contains("file.txt", StringComparison.Ordinal), "ls")
        && Expect(output.Contains("=== BANNER ===", StringComparison.Ordinal) && !output.Contains("banner", StringComparison.Ordinal), $"show: output typed its command: {Show(output)}")
        && Expect(clear > output.IndexOf("BANNER", StringComparison.Ordinal), "the clear comes after the banner")
        && Expect(output.IndexOf("# Scene 2", StringComparison.Ordinal) == clear + ClearScreen.Length + "\u001b[36m".Length, $"the comment follows the clear: {Show(output)}")
        && Expect(result.Steps.Count == 3 && result.Steps.All(static step => step.Recorded), $"steps: {string.Join(", ", result.Steps.Select(static s => $"{s.Run}:{s.Recorded}"))}");
}

static bool HiddenStepFailureStops(bool continueOnError)
{
    var yaml = $"""
        steps:
          - echo before
          - run: prepare
            show: none
            continue-on-error: {(continueOnError ? "true" : "false")}
          - echo after
        """;
    var started = new List<string>();
    var (events, failure) = GenerateAsync(ParseScenario(yaml), 1, new GenerateOptions { LogRunning = false }, step =>
    {
        started.Add($"{step.Command.Cmd}:{step.Show}");
        return Task.FromResult(step.Show == StepShow.None
            ? CommandExecution.FromPipe("", "it failed\n", 4)
            : CommandExecution.FromPipe("ok\n", "", 0));
    }).GetAwaiter().GetResult();

    if (continueOnError)
    {
        return Expect(failure is null, "continue-on-error stopped the recording")
            && Expect(started.SequenceEqual(["echo before:All", "prepare:None", "echo after:All"]), string.Join(", ", started));
    }

    return Expect(failure is { Hidden: true, Number: 2, ExitCode: 4 }, $"failure: {failure}")
        && Expect(started.SequenceEqual(["echo before:All", "prepare:None"]), string.Join(", ", started))
        && Expect(!events.Any(static e => e.Kind == CastEventKind.Output && e.Data.Contains("prepare", StringComparison.Ordinal)), "the hidden step was recorded");
}

static bool MaskerLearnsExportedSecrets()
{
    var masker = SecretMasker.ForScenario(new Scenario(), [])!;
    masker.Learn([new("MY_API_KEY", "correct-horse-battery-staple"), new("GREETING", "hello-there-friend")]);
    var off = SecretMasker.ForScenario(ParseScenario("settings:\n  mask-auto: false\n  mask: [listed-text]\nsteps: [echo]"), [])!;
    off.Learn([new("MY_API_KEY", "correct-horse-battery-staple")]);
    return Expect(masker.MaskText("[correct-horse-battery-staple]") == "[" + new string('*', 28) + "]", masker.MaskText("[correct-horse-battery-staple]"))
        && Expect(masker.MaskText("hello-there-friend") == "hello-there-friend", "a variable whose name is no secret")
        && Expect(off.MaskText("correct-horse-battery-staple") == "correct-horse-battery-staple", "mask-auto: false learned a value");
}

static bool IntegrationBashCarriesState(string repoRoot)
{
    if (!TryGetBinary(out var bin))
        return true;

    var run = RunFixture(bin, repoRoot, "shell-state-bash.yaml");
    var text = run.Text;
    return Expect(run.ExitCode == 0, $"exit {run.ExitCode}\n{run.Stderr}")
        && Expect(text.Contains("start=tests", StringComparison.Ordinal), "pre's cd")
        && Expect(text.Contains("now=fixtures", StringComparison.Ordinal), "a step's cd")
        && Expect(text.Contains("demo=carried plain=plain-value pre=pre-value", StringComparison.Ordinal), "variables")
        && Expect(text.Contains("hello alias", StringComparison.Ordinal) && text.Contains("hello function", StringComparison.Ordinal), "the alias and the function")
        && Expect(text.Contains("child=[][][carried]", StringComparison.Ordinal), "a child bash saw more than the exported variables")
        && Expect(text.Contains("demo=[unset]", StringComparison.Ordinal), "unset")
        && Expect(text.Contains("line 2: nosuchcommand", StringComparison.Ordinal), $"the error's line number: {Show(text)}")
        && Expect(run.Stderr.Contains("step exited 3: exit 3", StringComparison.Ordinal), "the exit code")
        && Expect(text.Contains("after-timeout=fixtures", StringComparison.Ordinal), "a stopped step changed the folder")
        && Expect(!text.Contains("correct-horse-battery-staple", StringComparison.Ordinal) && text.Contains("[****", StringComparison.Ordinal), "an exported secret was not hidden");
}

static bool IntegrationPowerShellCarriesState(string repoRoot)
{
    if (!TryGetBinary(out var bin))
        return true;
    if (!IsOnPath("pwsh"))
    {
        Console.Error.WriteLine("  skip: pwsh is not on PATH");
        return true;
    }

    var run = RunFixture(bin, repoRoot, "shell-state-pwsh.yaml");
    var text = run.Text;
    return Expect(run.ExitCode == 0, $"exit {run.ExitCode}\n{run.Stderr}")
        && Expect(text.Contains("start=tests", StringComparison.Ordinal), $"pre's location: {Show(text)}")
        && Expect(text.Contains("now=fixtures", StringComparison.Ordinal), "a step's location")
        && Expect(text.Contains("demo=carried plain=plain-value pre=pre-value", StringComparison.Ordinal), "variables")
        && Expect(text.Contains("hello alias", StringComparison.Ordinal), "the alias and the function")
        && Expect(text.Contains("demo=[]", StringComparison.Ordinal), "a removed variable")
        && Expect(text.Contains("pref=Stop", StringComparison.Ordinal), "a preference variable")
        && Expect(text.Contains("Exception: boom", StringComparison.Ordinal) && !text.Contains("Line |", StringComparison.Ordinal), "the error's display")
        && Expect(run.Stderr.Contains("step exited 7: exit 7", StringComparison.Ordinal), "the exit code")
        && Expect(text.Contains("after-exit=plain-value", StringComparison.Ordinal), "exit dropped the state");
}

static bool IntegrationHiddenStepFailureStops(string repoRoot)
{
    if (!TryGetBinary(out var bin))
        return true;

    var run = RunFixture(bin, repoRoot, "shell-state-hidden-fails.yaml");
    return Expect(run.ExitCode == 1, $"exit {run.ExitCode}\n{run.Stderr}")
        && Expect(run.Stderr.Contains("step 2 (show: none) exited 4", StringComparison.Ordinal), run.Stderr)
        && Expect(run.Stderr.Contains("| preparing", StringComparison.Ordinal), "the output tail")
        && Expect(run.Stderr.Contains("post-ran", StringComparison.Ordinal), "post did not run")
        && Expect(run.Text.Length == 0, "a cast was written");
}

static bool TryGetBinary(out string bin)
{
    bin = Environment.GetEnvironmentVariable("SCENETAKE_BIN") ?? "";
    if (!string.IsNullOrWhiteSpace(bin) && File.Exists(bin))
        return true;

    Console.Error.WriteLine("  skip: set SCENETAKE_BIN to a published scenetake binary");
    return false;
}

static bool IsOnPath(string name)
{
    foreach (var folder in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
    {
        if (File.Exists(Path.Combine(folder, name)) || File.Exists(Path.Combine(folder, name + ".exe")))
            return true;
    }

    return false;
}

static (int ExitCode, string Stderr, string Text) RunFixture(string bin, string repoRoot, string fixture)
{
    var stem = Path.Combine(Path.GetTempPath(), $"scenetake-shell-state-{Guid.NewGuid():N}");
    var psi = new ProcessStartInfo(bin)
    {
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        WorkingDirectory = repoRoot,
    };
    psi.ArgumentList.Add(Path.Combine("tests", "fixtures", fixture));
    psi.ArgumentList.Add(stem);

    using var process = Process.Start(psi)!;
    var stdout = process.StandardOutput.ReadToEndAsync();
    var stderr = process.StandardError.ReadToEndAsync();
    if (!process.WaitForExit(TimeSpan.FromSeconds(180)))
    {
        process.Kill(entireProcessTree: true);
        throw new TimeoutException($"{fixture} ran for over 180 s");
    }

    var cast = stem + ".cast";
    var text = File.Exists(cast) ? StripEscapes(CastOutput(File.ReadAllText(cast))) : "";
    if (File.Exists(cast))
        File.Delete(cast);
    return (process.ExitCode, stdout.Result + stderr.Result, text);
}

// The output events of a cast, joined, with their escape sequences.
static string CastOutput(string cast)
{
    var builder = new StringBuilder();
    foreach (var line in cast.Split('\n').Skip(1))
    {
        if (string.IsNullOrWhiteSpace(line))
            continue;
        using var json = JsonDocument.Parse(line);
        if (json.RootElement[1].GetString() == "o")
            builder.Append(json.RootElement[2].GetString());
    }

    return builder.ToString();
}

static string StripEscapes(string text) =>
    Regex.Replace(text, @"\x1b(\[[0-9;?]*[ -/]*[@-~]|\][^\x07\x1b]*(\x07|\x1b\\)|[()][0-9A-Za-z]|[=>78])", "").Replace("\r", "");

// A string with its control characters escaped, for messages.
static string Show(string? text) =>
    text is null ? "null" : "\"" + string.Concat(text.Select(static c => char.IsControl(c) && c != '\n' ? $"\\u{(int)c:x4}" : c.ToString())) + "\"";

static string FindRepoRoot(string start)
{
    var directory = new DirectoryInfo(start);
    while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "scenetake.cs")))
        directory = directory.Parent;
    return directory?.FullName ?? throw new InvalidOperationException("repository root not found");
}
