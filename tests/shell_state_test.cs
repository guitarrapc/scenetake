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
#:include ../ShellState.cs

// Shell state across steps, and the step keys show, clear, and background (.github/docs/spec_scenario.md → Shell
// state across steps, steps). The unit tests need only the sources. The integration tests record fixtures with a
// published binary (SCENETAKE_BIN) and need bash, on Windows Git Bash. The PowerShell, zsh, and sh ones skip when
// the shell is not on PATH.

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
failures += Run("LoginAliasesStayOffWithStepAlias", LoginAliasesStayOffWithStepAlias);
failures += Run("QuoteKeepsSingleQuotes", QuoteKeepsSingleQuotes);
failures += Run("StateForEachShell", StateForEachShell);
failures += Run("ShellRestoreSharesFirstLine", ShellRestoreSharesFirstLine);
failures += Run("UnquoteJoinsParts", UnquoteJoinsParts);
failures += Run("ZshDeclarationsByName", ZshDeclarationsByName);
failures += Run("ZshRestoreCarriesWhatChanged", ZshRestoreCarriesWhatChanged);
failures += Run("PosixShellRestoreCarriesWhatChanged", PosixShellRestoreCarriesWhatChanged);
failures += Run("PowerShellWrapsOnFirstAndLastLine", PowerShellWrapsOnFirstAndLastLine);
failures += Run("ShowAndClearErrors", ShowAndClearErrors);
failures += Run("ShowAndClearAreKnownKeys", ShowAndClearAreKnownKeys);
failures += Run("PlaygroundComposesShowAndClear", PlaygroundComposesShowAndClear);
failures += Run("HiddenStepFailureStops", () => HiddenStepFailureStops(continueOnError: false));
failures += Run("HiddenStepContinueOnError", () => HiddenStepFailureStops(continueOnError: true));
failures += Run("MaskerLearnsExportedSecrets", MaskerLearnsExportedSecrets);
failures += Run("BackgroundErrors", BackgroundErrors);
failures += Run("BackgroundStepStartsAndRecordsNothing", BackgroundStepStartsAndRecordsNothing);
failures += Run("BackgroundProblemStops", () => BackgroundProblemStops(continueOnError: false));
failures += Run("BackgroundProblemContinueOnError", () => BackgroundProblemStops(continueOnError: true));
failures += Run("PlaygroundSkipsBackground", PlaygroundSkipsBackground);
failures += Run("IntegrationBashCarriesState", () => IntegrationBashCarriesState(repoRoot));
failures += Run("IntegrationBashLoginAliasesStayOff", () => IntegrationBashLoginAliasesStayOff(repoRoot));
failures += Run("IntegrationPowerShellCarriesState", () => IntegrationPowerShellCarriesState(repoRoot));
failures += Run("IntegrationZshCarriesState", () => IntegrationZshCarriesState(repoRoot));
failures += Run("IntegrationShCarriesState", () => IntegrationShCarriesState(repoRoot));
failures += Run("IntegrationHiddenStepFailureStops", () => IntegrationHiddenStepFailureStops(repoRoot));
failures += Run("IntegrationBackgroundRunsUntilPost", () => IntegrationBackgroundRunsUntilPost(repoRoot));
failures += Run("IntegrationBackgroundExitStops", () => IntegrationBackgroundExitStops(repoRoot));
failures += Run("IntegrationBackgroundWaitTimeoutStops", () => IntegrationBackgroundWaitTimeoutStops(repoRoot));
failures += Run("IntegrationBackgroundContinueOnError", () => IntegrationBackgroundContinueOnError(repoRoot));

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

// Once a step defines an alias, expansion is on, and the login files' aliases would expand with it: Git Bash's
// node='winpty node.exe' then failed in a pipe with "stdin is not a tty". They are removed, in the step after the
// one that defines the alias and in every later one, and a login alias that a step redefined stays.
static bool LoginAliasesStayOffWithStepAlias()
{
    const string login = "alias ls='ls -F --color=auto'\nalias node='winpty node.exe'\nalias ll='ls -l'\n";
    // The step that defines hi and redefines ll ran with the login aliases still defined.
    var first = BashDump.BuildRestore("", "", "", "", login,
        "alias hi='echo hi'\nalias ll='ls -la'\nalias ls='ls -F --color=auto'\nalias node='winpty node.exe'\n", "/work", []);
    // The next step: its fresh shell has the login aliases again, and the restore removed them before it ended.
    var next = BashDump.BuildRestore("", "", "", "", login, "alias hi='echo hi'\nalias ll='ls -la'\n", "/work", []);

    bool Check(string restore, string which) =>
        Expect(restore.Contains("alias hi='echo hi'\n", StringComparison.Ordinal), $"{which}: the step's alias\n{restore}")
        && Expect(restore.Contains("alias ll='ls -la'\n", StringComparison.Ordinal) && !restore.Contains("unalias 'll'", StringComparison.Ordinal), $"{which}: a redefined login alias\n{restore}")
        && Expect(restore.Contains("builtin unalias 'ls'\n", StringComparison.Ordinal) && restore.Contains("builtin unalias 'node'\n", StringComparison.Ordinal), $"{which}: the login aliases\n{restore}")
        && Expect(restore.EndsWith("builtin shopt -s expand_aliases\n", StringComparison.Ordinal), $"{which}: expansion after the aliases\n{restore}");

    return Check(first, "first") && Check(next, "next");
}

static bool QuoteKeepsSingleQuotes() =>
    Expect(BashDump.Quote("it's") == "'it'\\''s'", BashDump.Quote("it's"))
    && Expect(PowerShellState.Literal("it's") == "'it''s'", PowerShellState.Literal("it's"));

static bool StateForEachShell()
{
    using var bash = ShellState.Create(OperatingSystem.IsWindows() ? @"C:\Program Files\Git\usr\bin\bash.exe" : "/bin/bash", isPowerShell: false);
    using var pwsh = ShellState.Create("pwsh", isPowerShell: true);
    using var zsh = ShellState.Create("/bin/zsh", isPowerShell: false);
    var ok = Expect(bash is BashState, "bash");
    ok &= Expect(pwsh is PowerShellState, "pwsh");
    ok &= Expect(zsh is ZshState, "zsh");
    foreach (var name in new[] { "/bin/sh", "dash", "ash", "ksh", "ksh93", "mksh" })
    {
        using var sh = ShellState.Create(name, isPowerShell: false);
        ok &= Expect(sh is PosixShellState { CarriesFunctionsAndAliases: false }, $"{name}: the sh family");
    }

    foreach (var name in new[] { "fish", "tcsh", "cmd", "nu" })
    {
        using var other = ShellState.Create(name, isPowerShell: false);
        ok &= Expect(other is null, $"{name} carries nothing");
    }

    // bash: the command is unchanged, and BASH_ENV names the restore script.
    var prepared = bash!.Prepare("echo hi");
    ok &= Expect(prepared.Command == "echo hi", prepared.Command);
    ok &= Expect(prepared.Environment is { } env && env.TryGetValue("BASH_ENV", out var script) && File.Exists(script!), "BASH_ENV script");

    // zsh: the command is unchanged, and ZDOTDIR is a folder of startup files.
    prepared = zsh!.Prepare("echo hi");
    ok &= Expect(prepared.Command == "echo hi", prepared.Command);
    ok &= Expect(prepared.Environment is { } zshEnv && zshEnv.TryGetValue("ZDOTDIR", out var folder) && zshEnv.Count == 1
        && new[] { ".zshenv", ".zprofile", ".zlogin", "finish.zsh" }.All(name => File.Exists(Path.Combine(folder!, name))), "ZDOTDIR folder");
    return ok;
}

// sh: the restore shares the first line, so the user's lines keep their numbers.
static bool ShellRestoreSharesFirstLine()
{
    using var state = ShellState.Create("dash", isPowerShell: false)!;
    var prepared = state.Prepare("echo one\nnosuchcommand");
    var lines = prepared.Command.Split('\n');
    var script = Regex.Match(lines[0], "^\\. '([^']+)'; ").Groups[1].Value;
    return Expect(prepared.Environment is null, "no environment")
        && Expect(lines.Length == 2, $"lines: {lines.Length}")
        && Expect(lines[0].EndsWith("; echo one", StringComparison.Ordinal) && File.Exists(script), lines[0])
        && Expect(lines[1] == "nosuchcommand", lines[1]);
}

static bool UnquoteJoinsParts() =>
    Expect(ShellText.Unquote("'it'\\''s'") == "it's", "bash and zsh")
    && Expect(ShellText.Unquote("'it'\"'\"'s'") == "it's", "dash")
    && Expect(ShellText.Unquote("$'it\\'s'") == "it's", "ksh93")
    && Expect(ShellText.Unquote("$'x\\ny\\tz'") == "x\ny\tz", "dollar quotes")
    && Expect(ShellText.Unquote("\"a \\\"b\\\" \\$c\"") == "a \"b\" $c", "double quotes")
    && Expect(ShellText.Unquote("plain\\ word") == "plain word", "a backslash")
    && Expect(ShellText.AssignedValue("export T='s 3'") == "s 3", "export -p")
    && Expect(ShellText.AssignedValue("arr=([0]=\"a\")") is null, "an array");

// zsh writes one statement per line, $'...' for a line break, and a tied pair such as PATH and path as two.
static bool ZshDeclarationsByName()
{
    const string dump = "typeset -g foo=bar\n"
        + "export -T PATH path=( /usr/bin /bin )\n"
        + "typeset -g -aT PATH path=( /usr/bin /bin )\n"
        + "typeset -g -A h=( [k]='v w' )\n"
        + "export E=$'line1\\nline2'\n"
        + "typeset -g -i10 RANDOM=5\n"
        + "typeset -g declared\n";
    var map = ZshDump.SplitDeclarations(dump);
    return Expect(map.Count == 6, $"names: {string.Join(" | ", map.Keys)}")
        && Expect(map["path"] == "export -T PATH path=( /usr/bin /bin )\ntypeset -g -aT PATH path=( /usr/bin /bin )", $"path: {map["path"]}")
        && Expect(map["foo"] == "typeset -g foo=bar", map["foo"])
        && Expect(map["h"] == "typeset -g -A h=( [k]='v w' )", map["h"])
        && Expect(map.ContainsKey("E") && map.ContainsKey("RANDOM") && map.ContainsKey("declared"), "E, RANDOM, declared")
        && Expect(ZshDump.DeclarationName("echo hi") is null, "another line");
}

static bool ZshRestoreCarriesWhatChanged()
{
    const string baseVars = "export -T PATH path=( /usr/bin )\ntypeset -g -aT PATH path=( /usr/bin )\ntypeset -g KEEP=same\ntypeset -g GONE=x\ntypeset -g -i10 RANDOM=1\ntypeset -g -a argv=( base )\n";
    const string finalVars = "export -T PATH path=( /venv/bin /usr/bin )\ntypeset -g -aT PATH path=( /venv/bin /usr/bin )\ntypeset -g KEEP=same\nexport API_TOKEN='it'\\''s-secret'\ntypeset -g -i10 RANDOM=2\ntypeset -g -a argv=( final )\ntypeset -g __scenetake_dir=/tmp/x\nexport OLDPWD=/home\n";
    const string baseFunctions = "functions[old]=:\nfunctions[__scenetake_dump]=:\n";
    const string finalFunctions = "functions[greet]=$'\\techo hi'\nfunctions[__scenetake_dump]=:\nfunctions[__scenetake_save]=:\n";
    const string baseAliases = "aliases[run-help]=man\ngaliases[G]=\\|\\ grep\nsaliases[txt]=cat\n";
    const string finalAliases = "aliases[hi]=greet\naliases[run-help]=man\n";
    var exports = new List<KeyValuePair<string, string?>>();
    var restore = ZshDump.BuildRestore(baseVars, finalVars, baseFunctions, finalFunctions, baseAliases, finalAliases, "/work/it's", exports);

    return Expect(restore.StartsWith("builtin cd -q -- '/work/it'\\''s'", StringComparison.Ordinal), $"cd first: {restore}")
        && Expect(restore.Contains("export -T PATH path=( /venv/bin /usr/bin )\ntypeset -g -aT PATH path=( /venv/bin /usr/bin )\n", StringComparison.Ordinal), "the changed tied pair")
        && Expect(restore.Contains("export API_TOKEN='it'\\''s-secret'\n", StringComparison.Ordinal), "new variable")
        && Expect(restore.Contains("export OLDPWD=/home\n", StringComparison.Ordinal), "OLDPWD, for cd -")
        && Expect(!restore.Contains("KEEP", StringComparison.Ordinal), "an unchanged variable")
        && Expect(restore.Contains("builtin unset -v -- 'GONE'\n", StringComparison.Ordinal), "a removed variable")
        && Expect(!restore.Contains("RANDOM", StringComparison.Ordinal) && !restore.Contains("argv", StringComparison.Ordinal), "a variable zsh changes by itself")
        && Expect(!restore.Contains("__scenetake", StringComparison.Ordinal), "scenetake's own names")
        && Expect(restore.Contains("functions[greet]=$'\\techo hi'\n", StringComparison.Ordinal), "a new function")
        && Expect(restore.Contains("builtin unfunction -- old\n", StringComparison.Ordinal), "a removed function")
        && Expect(restore.Contains("aliases[hi]=greet\n", StringComparison.Ordinal) && !restore.Contains("run-help", StringComparison.Ordinal), "only the new alias")
        && Expect(restore.Contains("builtin unalias -- G\n", StringComparison.Ordinal) && restore.Contains("builtin unalias -s -- txt\n", StringComparison.Ordinal), "removed global and suffix aliases")
        && Expect(restore.IndexOf("export -T PATH", StringComparison.Ordinal) < restore.IndexOf("functions[greet]", StringComparison.Ordinal)
            && restore.IndexOf("functions[greet]", StringComparison.Ordinal) < restore.IndexOf("aliases[hi]", StringComparison.Ordinal), "variables, then functions, then aliases")
        && Expect(exports.Count == 2 && exports.Any(static e => e.Key == "API_TOKEN" && e.Value == "it's-secret") && exports.Any(static e => e.Key == "OLDPWD"),
            $"exports: {string.Join(", ", exports.Select(static e => $"{e.Key}={e.Value}"))}");
}

// dash writes a quote as '"'"' and a line break inside the quotes; export -p lists the exported names again.
static bool PosixShellRestoreCarriesWhatChanged()
{
    const string baseVars = "HOME='/root'\nIFS=' \t\n'\nKEEP='same'\nGONE='x'\nPS1='$ '\n";
    const string finalVars = "HOME='/root'\nIFS=' \t\n'\nKEEP='same'\nNOTE='it'\"'\"'s\ntwo lines'\nAPI_TOKEN='s3cr3t value'\nPS1='$ '\n_='/bin/true'\n__scenetake_dir='/tmp/x'\nOLDPWD='/home'\n";
    const string baseExports = "export HOME='/root'\nexport KEEP='same'\n";
    const string finalExports = "export API_TOKEN='s3cr3t value'\nexport HOME='/root'\nexport KEEP='same'\nexport MARK\n";
    var exports = new List<KeyValuePair<string, string?>>();
    var restore = PosixShellDump.BuildRestore(baseVars, finalVars, baseExports, finalExports, "/work/it's", exports);

    return Expect(restore.StartsWith("command cd -- '/work/it'\\''s'", StringComparison.Ordinal), $"cd first: {restore}")
        && Expect(restore.Contains("NOTE='it'\"'\"'s\ntwo lines'\n", StringComparison.Ordinal), "a value with a quote and a line break")
        && Expect(restore.Contains("API_TOKEN='s3cr3t value'\n", StringComparison.Ordinal) && restore.Contains("export API_TOKEN\n", StringComparison.Ordinal), "a new exported variable")
        && Expect(restore.Contains("export MARK\n", StringComparison.Ordinal), "an exported name without a value")
        && Expect(restore.Contains("OLDPWD='/home'\n", StringComparison.Ordinal), "OLDPWD, for cd -")
        && Expect(!restore.Contains("KEEP", StringComparison.Ordinal) && !restore.Contains("HOME", StringComparison.Ordinal), "an unchanged variable")
        && Expect(restore.Contains("unset -v GONE\n", StringComparison.Ordinal), "a removed variable")
        && Expect(!restore.Contains("_=", StringComparison.Ordinal) && !restore.Contains("__scenetake", StringComparison.Ordinal), "the shell's and scenetake's own names")
        && Expect(restore.IndexOf("API_TOKEN='", StringComparison.Ordinal) < restore.IndexOf("export API_TOKEN", StringComparison.Ordinal), "the value before export")
        && Expect(exports.Count == 2 && exports.Any(static e => e.Key == "API_TOKEN" && e.Value == "s3cr3t value") && exports.Any(static e => e.Key == "MARK" && e.Value is null),
            $"exports: {string.Join(", ", exports.Select(static e => $"{e.Key}={e.Value}"))}");
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

static bool BackgroundErrors()
{
    var ok = true;
    ok &= ExpectError("- run: serve\n  wait: Listening", "Step 1 key 'wait' applies to a 'background: true' step");
    ok &= ExpectError("- run: serve\n  wait-timeout: 5", "Step 1 key 'wait-timeout' applies to a 'background: true' step");
    ok &= ExpectError("- run: serve\n  background: maybe", "Step 1 key 'background' must be true or false");
    ok &= ExpectError("- run: serve\n  background: true\n  pty: true", "Step 1 key 'pty' does not apply to a 'background: true' step");
    ok &= ExpectError("- run: serve\n  background: true\n  show: output", "Step 1 key 'show' does not apply to a 'background: true' step");
    ok &= ExpectError("- run: serve\n  background: true\n  timeout: 5", "Step 1 key 'timeout' does not apply to a 'background: true' step");
    ok &= ExpectError("- run: serve\n  background: true\n  name: Server", "Step 1 key 'name' does not apply to a 'background: true' step");
    ok &= ExpectError("- run: serve\n  background: true\n  wait-timeout: 5", "Step 1 key 'wait-timeout' needs 'wait'");
    ok &= ExpectError("- run: serve\n  background: true\n  wait: ready\n  wait-timeout: 0", "Step 1 key 'wait-timeout' must be a number of seconds above 0");
    ok &= ExpectError("- run: serve\n  background: true\n  wait: \"  \"", "Step 1 key 'wait' must be text the command prints");
    ok &= ExpectError("- run: serve\n  background: true", "The scenario has no steps to record");
    ok &= ExpectError("- run: serve\n  background: true\n  wait: Listening on\n  wait-timeout: 60\n  continue-on-error: true\n- run: watch\n  background: false\n- curl localhost", null);
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

static bool BackgroundStepStartsAndRecordsNothing()
{
    const string yaml = """
        steps:
          - run: serve
            background: true
            wait: Listening on
          - run: watch
            background: true
            wait: ready
            wait-timeout: 5
          - run: worker
            background: true
          - curl localhost
        """;
    var started = new List<StepStart>();
    var (events, failure) = GenerateAsync(ParseScenario(yaml), 1, new GenerateOptions { LogRunning = false }, step =>
    {
        started.Add(step);
        return Task.FromResult(step.Background
            ? CommandExecution.FromPipe("", "", 0)
            : CommandExecution.FromPipeStream("ok\n", "", 0, [new PipeStreamChunk(TimeSpan.Zero, false, "ok\n")]));
    }).GetAwaiter().GetResult();

    var output = string.Concat(events.Where(static e => e.Kind == CastEventKind.Output).Select(static e => e.Data));
    return Expect(failure is null, $"failure: {failure}")
        && Expect(started.Count == 4, $"started: {started.Count}")
        && Expect(started[0] is { Background: true, WaitFor: "Listening on", WaitLimit: BackgroundWaitLimit, Show: StepShow.None }, $"serve: {started[0]}")
        && Expect(started[1] is { Background: true, WaitFor: "ready", WaitLimit: 5 }, $"watch: {started[1]}")
        && Expect(started[2] is { Background: true, WaitFor: null }, $"worker: {started[2]}")
        && Expect(!started[3].Background, "curl ran in the background")
        && Expect(!output.Contains("serve", StringComparison.Ordinal) && !output.Contains("worker", StringComparison.Ordinal), "a background step was typed")
        && Expect(output.Contains("ok", StringComparison.Ordinal), "the step after it");
}

static bool BackgroundProblemStops(bool continueOnError)
{
    var yaml = $"""
        steps:
          - run: serve
            background: true
            wait: Listening
            continue-on-error: {(continueOnError ? "true" : "false")}
          - curl localhost
        """;
    var started = new List<string>();
    var (_, failure) = GenerateAsync(ParseScenario(yaml), 1, new GenerateOptions { LogRunning = false }, step =>
    {
        started.Add(step.Command.Cmd);
        return Task.FromResult(step.Background
            ? CommandExecution.FromPipe("port in use\n", "", 3) with { BackgroundProblem = "exited 3 before it printed \"Listening\"" }
            : CommandExecution.FromPipe("ok\n", "", 0));
    }).GetAwaiter().GetResult();

    if (continueOnError)
        return Expect(failure is null, "continue-on-error stopped the recording") && Expect(started.Count == 2, string.Join(", ", started));

    return Expect(failure is { Background: true, Number: 1, ExitCode: 3, Problem: "exited 3 before it printed \"Listening\"" }, $"failure: {failure}")
        && Expect(failure!.OutputTail.SequenceEqual(["port in use"]), string.Join(" | ", failure.OutputTail))
        && Expect(started.Count == 1, "a step ran after the background step failed");
}

// The playground runs nothing: a background step takes no recorded output, and the steps list leaves it out.
static bool PlaygroundSkipsBackground()
{
    const string yaml = """
        width: 40
        height: 6
        steps:
          - run: serve
            background: true
            wait: Listening
          - curl localhost
        """;
    var outputs = new StepOutputs
    {
        Scenetake = "test",
        Os = "linux",
        Shell = "bash",
        Width = 40,
        Height = 6,
        Steps = [RecordedStep.Pipe("curl localhost", 0, [new PipeStreamChunk(TimeSpan.Zero, false, "hello\n")])],
    };
    var result = PlaygroundRender.RenderScenario(yaml, outputs, new PlaygroundOptions(), renderSvg: false);
    var output = result.Cast is null ? "" : CastOutput(Encoding.UTF8.GetString(result.Cast));
    return Expect(result.Error is null, $"render failed: {result.Error}")
        && Expect(result.Steps.Count == 1 && result.Steps[0] is { Run: "curl localhost", Recorded: true }, $"steps: {string.Join(", ", result.Steps.Select(static s => s.Run))}")
        && Expect(!output.Contains("serve", StringComparison.Ordinal) && output.Contains("hello", StringComparison.Ordinal), Show(output));
}

static bool IntegrationBackgroundRunsUntilPost(string repoRoot)
{
    if (!TryGetBinary(out var bin))
        return true;

    var run = RunFixture(bin, repoRoot, "background-bash.yaml");
    return Expect(run.ExitCode == 0, $"exit {run.ExitCode}\n{run.Stderr}")
        && Expect(run.Stderr.Contains("ready after", StringComparison.Ordinal), $"the wait for the state's variable: {run.Stderr}")
        && Expect(run.Text.Contains("counter is running", StringComparison.Ordinal), $"the next step: {Show(run.Text)}")
        && Expect(run.Stderr.Contains("stopped (background)", StringComparison.Ordinal), "the stop")
        && Expect(run.Stderr.Contains("post sees it stopped", StringComparison.Ordinal), $"post: {run.Stderr}")
        && Expect(!run.Text.Contains("while :", StringComparison.Ordinal), "the background command was recorded");
}

static bool IntegrationBackgroundExitStops(string repoRoot)
{
    if (!TryGetBinary(out var bin))
        return true;

    var run = RunFixture(bin, repoRoot, "background-exits.yaml");
    return Expect(run.ExitCode == 1, $"exit {run.ExitCode}\n{run.Stderr}")
        && Expect(run.Stderr.Contains("background step 2 exited 3 before it printed \"never printed\"", StringComparison.Ordinal), run.Stderr)
        && Expect(run.Stderr.Contains("| starting", StringComparison.Ordinal), "the output tail")
        && Expect(run.Stderr.Contains("post-ran", StringComparison.Ordinal), "post did not run")
        && Expect(!run.Stderr.Contains("running: echo after", StringComparison.Ordinal), "a step ran after it")
        && Expect(run.Text.Length == 0, "a cast was written");
}

static bool IntegrationBackgroundWaitTimeoutStops(string repoRoot)
{
    if (!TryGetBinary(out var bin))
        return true;

    var stopwatch = Stopwatch.StartNew();
    var run = RunFixture(bin, repoRoot, "background-wait-timeout.yaml");
    stopwatch.Stop();
    return Expect(run.ExitCode == 1, $"exit {run.ExitCode}\n{run.Stderr}")
        && Expect(run.Stderr.Contains("background step 2 did not print \"never printed\" within 1 s", StringComparison.Ordinal), run.Stderr)
        && Expect(run.Stderr.Contains("| working", StringComparison.Ordinal), "the output tail")
        && Expect(run.Stderr.Contains("post-ran", StringComparison.Ordinal), "post did not run")
        // The command sleeps 60 s: it was ended, not waited for.
        && Expect(stopwatch.Elapsed < TimeSpan.FromSeconds(30), $"took {stopwatch.Elapsed.TotalSeconds:0.0} s");
}

static bool IntegrationBackgroundContinueOnError(string repoRoot)
{
    if (!TryGetBinary(out var bin))
        return true;

    var run = RunFixture(bin, repoRoot, "background-continue.yaml");
    return Expect(run.ExitCode == 0, $"exit {run.ExitCode}\n{run.Stderr}")
        && Expect(run.Stderr.Contains("Warning: background step 1 exited 3 before it printed \"never printed\", and the steps go on (continue-on-error)", StringComparison.Ordinal), run.Stderr)
        && Expect(run.Text.Contains("after", StringComparison.Ordinal), "the next step");
}

static bool IntegrationBashCarriesState(string repoRoot)
{
    if (!TryGetBinary(out var bin))
        return true;

    var stopwatch = Stopwatch.StartNew();
    var run = RunFixture(bin, repoRoot, "shell-state-bash.yaml");
    stopwatch.Stop();
    var text = run.Text;
    return Expect(run.ExitCode == 0, $"exit {run.ExitCode}\n{run.Stderr}")
        // Two steps sleep 30 s and are stopped after 1 s: a stop that misses the sleep waits for it.
        && Expect(stopwatch.Elapsed < TimeSpan.FromSeconds(25), $"took {stopwatch.Elapsed.TotalSeconds:0.0} s")
        && Expect(text.Contains("after-pty-timeout=fixtures", StringComparison.Ordinal), "a stopped PTY step changed the folder")
        && Expect(text.Contains("start=tests", StringComparison.Ordinal), "pre's cd")
        && Expect(text.Contains("now=fixtures", StringComparison.Ordinal), "a step's cd")
        && Expect(text.Contains("demo=carried plain=plain-value pre=pre-value", StringComparison.Ordinal), "variables")
        && Expect(text.Contains("hello alias", StringComparison.Ordinal) && text.Contains("hello function", StringComparison.Ordinal), "the alias and the function")
        && Expect(text.Contains("child=[][][carried]", StringComparison.Ordinal), "a child bash saw more than the exported variables")
        && Expect(text.Contains("demo=[unset]", StringComparison.Ordinal), "unset")
        && Expect(ErrorLine(text, "nosuchcommand") is { } line && line == ErrorLine(text, "nosuchplain"), $"the error's line number: {Show(text)}")
        && Expect(run.Stderr.Contains("step exited 3: exit 3", StringComparison.Ordinal), "the exit code")
        && Expect(text.Contains("after-timeout=fixtures", StringComparison.Ordinal), "a stopped step changed the folder")
        && Expect(!text.Contains("correct-horse-battery-staple", StringComparison.Ordinal) && text.Contains("[****", StringComparison.Ordinal), "an exported secret was not hidden");
}

// HOME is a folder whose .bash_profile defines aliases. Before the fix, a step's alias turned expansion on for them
// too, as Git Bash's node='winpty node.exe' then failed in a pipe with "stdin is not a tty".
static bool IntegrationBashLoginAliasesStayOff(string repoRoot)
{
    if (!TryGetBinary(out var bin))
        return true;

    var home = Path.Combine(Path.GetTempPath(), $"scenetake-bash-home-{Guid.NewGuid():N}");
    Directory.CreateDirectory(home);
    try
    {
        File.WriteAllText(Path.Combine(home, ".bash_profile"), "alias login_only='echo login-alias-expanded'\nalias date='echo login-date-alias'\n");
        var run = RunFixture(bin, repoRoot, "shell-state-bash-login-alias.yaml", new() { ["HOME"] = home });
        var text = run.Text;
        return Expect(run.ExitCode == 0, $"exit {run.ExitCode}\n{run.Stderr}")
            && Expect(Regex.Matches(text, @"year=\d{4}").Count == 2, $"date ran as the login alias: {Show(text)}")
            && Expect(text.Contains("step-alias", StringComparison.Ordinal), "the step's alias")
            && Expect(text.Contains("login-only-off", StringComparison.Ordinal) && !text.Contains("login-alias-expanded", StringComparison.Ordinal), "a login alias expanded")
            && Expect(!text.Contains("login-date-alias", StringComparison.Ordinal), "the login alias for date expanded")
            && Expect(text.Contains("step-date", StringComparison.Ordinal), "a step's alias that redefines a login alias");
    }
    finally
    {
        Directory.Delete(home, recursive: true);
    }
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

// HOME is a folder of login files that move ZDOTDIR and reorder PATH, as many users' files and macOS's path_helper do.
static bool IntegrationZshCarriesState(string repoRoot)
{
    if (!TryGetBinary(out var bin))
        return true;
    if (OperatingSystem.IsWindows() || !IsOnPath("zsh"))
    {
        Console.Error.WriteLine("  skip: zsh is not on PATH");
        return true;
    }

    var home = Path.Combine(Path.GetTempPath(), $"scenetake-zsh-home-{Guid.NewGuid():N}");
    Directory.CreateDirectory(Path.Combine(home, "zdot"));
    try
    {
        File.WriteAllText(Path.Combine(home, ".zshenv"), "export ZDOTDIR=\"$HOME/zdot\"\n");
        File.WriteAllText(Path.Combine(home, "zdot", ".zprofile"), "path=(/usr/bin ${path:#/usr/bin})\n");
        File.WriteAllText(Path.Combine(home, "zdot", ".zlogin"), "FROM_ZLOGIN=login-value\n");

        var stopwatch = Stopwatch.StartNew();
        var run = RunFixture(bin, repoRoot, "shell-state-zsh.yaml", new() { ["HOME"] = home, ["ZDOTDIR"] = null });
        stopwatch.Stop();
        var text = run.Text;
        return Expect(run.ExitCode == 0, $"exit {run.ExitCode}\n{run.Stderr}")
            && Expect(!run.Stderr.Contains("Note:", StringComparison.Ordinal), "a note about the shell")
            && Expect(stopwatch.Elapsed < TimeSpan.FromSeconds(25), $"took {stopwatch.Elapsed.TotalSeconds:0.0} s")
            && Expect(text.Contains("start=tests", StringComparison.Ordinal), $"pre's cd: {Show(text)}")
            && Expect(text.Contains("now=fixtures", StringComparison.Ordinal), "a step's cd")
            && Expect(text.Contains("demo=carried plain=plain-value pre=pre-value sky=blue", StringComparison.Ordinal), "variables")
            && Expect(text.Contains("hello alias", StringComparison.Ordinal) && text.Contains("HELLO FUNCTION", StringComparison.Ordinal), "the alias, the global alias, and the function")
            && Expect(text.Contains($"child=[0][][carried][{home}/zdot]", StringComparison.Ordinal), "a child zsh saw more than the exported variables, or another ZDOTDIR")
            && Expect(text.Contains("demo=[unset] hi=[0]", StringComparison.Ordinal), "unset and unalias")
            && Expect(text.Contains("first=/opt/scenetake-step login=login-value", StringComparison.Ordinal), "the step's PATH after the login files, or the login files did not run")
            && Expect(text.Contains(":2: command not found: nosuchcommand", StringComparison.Ordinal), "the error's line number")
            && Expect(run.Stderr.Contains("step exited 3: exit 3", StringComparison.Ordinal), "the exit code")
            && Expect(text.Contains("after-timeout=fixtures", StringComparison.Ordinal) && text.Contains("after-pty-timeout=fixtures", StringComparison.Ordinal), "a stopped step changed the folder")
            && Expect(!text.Contains("correct-horse-battery-staple", StringComparison.Ordinal) && text.Contains("[****", StringComparison.Ordinal), "an exported secret was not hidden");
    }
    finally
    {
        Directory.Delete(home, recursive: true);
    }
}

// sh is dash on Debian and Ubuntu, bash in POSIX mode on macOS, and Git's sh on Windows.
static bool IntegrationShCarriesState(string repoRoot)
{
    if (!TryGetBinary(out var bin))
        return true;
    if (!IsOnPath("sh"))
    {
        Console.Error.WriteLine("  skip: sh is not on PATH");
        return true;
    }

    var stopwatch = Stopwatch.StartNew();
    var run = RunFixture(bin, repoRoot, "shell-state-sh.yaml");
    stopwatch.Stop();
    var text = run.Text;
    return Expect(run.ExitCode == 0, $"exit {run.ExitCode}\n{run.Stderr}")
        && Expect(run.Stderr.Contains("Note: in sh, cd and variables carry over to the next step, but functions and aliases do not.", StringComparison.Ordinal), "the note")
        && Expect(stopwatch.Elapsed < TimeSpan.FromSeconds(25), $"took {stopwatch.Elapsed.TotalSeconds:0.0} s")
        && Expect(text.Contains("start=tests", StringComparison.Ordinal), $"pre's cd: {Show(text)}")
        && Expect(text.Contains("now=fixtures", StringComparison.Ordinal), "a step's cd")
        && Expect(text.Contains("demo=carried plain=plain-value pre=pre-value", StringComparison.Ordinal), "variables")
        && Expect(text.Contains("note=[it's\ntwo lines]", StringComparison.Ordinal), "a value with a quote and a line break")
        && Expect(text.Contains("no-function", StringComparison.Ordinal), "a function carried")
        && Expect(text.Contains("child=[][carried]", StringComparison.Ordinal), "a child sh saw more than the exported variables")
        && Expect(text.Contains("demo=[unset]", StringComparison.Ordinal), "unset")
        && Expect(ErrorLine(text, "nosuchcommand") is { } line && line == ErrorLine(text, "nosuchplain"), $"the error's line number: {Show(text)}")
        && Expect(run.Stderr.Contains("step exited 3: exit 3", StringComparison.Ordinal), "the exit code")
        && Expect(text.Contains("after-timeout=fixtures", StringComparison.Ordinal) && text.Contains("after-pty-timeout=fixtures", StringComparison.Ordinal), "a stopped step changed the folder")
        && Expect(!text.Contains("correct-horse-battery-staple", StringComparison.Ordinal) && text.Contains("[****", StringComparison.Ordinal), "an exported secret was not hidden");
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

// The line number in the error for a command the shell could not find: 2 in "bash: line 2: ", "sh: 2: ", and
// "ksh93[2]: ". ksh93 writes "sh: line 2: " in a plain run and "sh[2]: " with an EXIT trap set, so only the number
// is compared. Empty when the shell writes none, as busybox ash does, and null when no line names the command.
static string? ErrorLine(string text, string command)
{
    var match = Regex.Match(text, "^(.*)" + command + ":", RegexOptions.Multiline);
    return match.Success ? Regex.Match(match.Groups[1].Value, @"(\d+)\D*$").Groups[1].Value : null;
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

// environment: variables to set for scenetake, a null value removes one.
static (int ExitCode, string Stderr, string Text) RunFixture(string bin, string repoRoot, string fixture, Dictionary<string, string?>? environment = null)
{
    var stem = Path.Combine(Path.GetTempPath(), $"scenetake-shell-state-{Guid.NewGuid():N}");
    var psi = new ProcessStartInfo(bin)
    {
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        WorkingDirectory = repoRoot,
    };
    foreach (var (name, value) in environment ?? [])
    {
        if (value is null)
            psi.Environment.Remove(name);
        else
            psi.Environment[name] = value;
    }
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
