using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using static Scenetake.Core.ScenarioCast;
using Scenetake.TestSupport;

namespace Scenetake.Tests;

// Shell state across steps, and the step keys show, clear, and background (.github/docs/spec_scenario.md → Shell
// state across steps, steps). The unit tests need only the sources. The integration tests record fixtures with a
// published binary (SCENETAKE_BIN) and need bash, on Windows Git Bash. The PowerShell, zsh, and sh ones skip when
// the shell is not on PATH.

public sealed class ShellStateTests
{
    [Test]
    public async Task DeclarationsSplitOutsideQuotes() => await Assert.That(ShellStateCases.DeclarationsSplitOutsideQuotes()).IsTrue();

    [Test]
    public async Task ScalarValuesAreUnquoted() => await Assert.That(ShellStateCases.ScalarValuesAreUnquoted()).IsTrue();

    [Test]
    public async Task RestoreCarriesWhatChanged() => await Assert.That(ShellStateCases.RestoreCarriesWhatChanged()).IsTrue();

    [Test]
    public async Task LoginAliasesDoNotTurnOnExpansion() => await Assert.That(ShellStateCases.LoginAliasesDoNotTurnOnExpansion()).IsTrue();

    [Test]
    public async Task LoginAliasesStayOffWithStepAlias() => await Assert.That(ShellStateCases.LoginAliasesStayOffWithStepAlias()).IsTrue();

    [Test]
    public async Task QuoteKeepsSingleQuotes() => await Assert.That(ShellStateCases.QuoteKeepsSingleQuotes()).IsTrue();

    [Test]
    public async Task StateForEachShell() => await Assert.That(ShellStateCases.StateForEachShell()).IsTrue();

    [Test]
    public async Task ShellRestoreSharesFirstLine() => await Assert.That(ShellStateCases.ShellRestoreSharesFirstLine()).IsTrue();

    [Test]
    public async Task UnquoteJoinsParts() => await Assert.That(ShellStateCases.UnquoteJoinsParts()).IsTrue();

    [Test]
    public async Task ZshDeclarationsByName() => await Assert.That(ShellStateCases.ZshDeclarationsByName()).IsTrue();

    [Test]
    public async Task ZshRestoreCarriesWhatChanged() => await Assert.That(ShellStateCases.ZshRestoreCarriesWhatChanged()).IsTrue();

    [Test]
    public async Task ZshRestoreCarriesPromptOptions() => await Assert.That(ShellStateCases.ZshRestoreCarriesPromptOptions()).IsTrue();

    [Test]
    public async Task ShellPromptScriptsOnlyWhenOn() => await Assert.That(ShellStateCases.ShellPromptScriptsOnlyWhenOn()).IsTrue();

    [Test]
    public async Task ShellPromptAsTheCastShowsIt() => await Assert.That(ShellStateCases.ShellPromptAsTheCastShowsIt()).IsTrue();

    [Test]
    public async Task ShellPromptComposes() => await Assert.That(ShellStateCases.ShellPromptComposes()).IsTrue();

    [Test]
    public async Task ShellPromptReplays() => await Assert.That(ShellStateCases.ShellPromptReplays()).IsTrue();

    [Test]
    public async Task PosixShellRestoreCarriesWhatChanged() => await Assert.That(ShellStateCases.PosixShellRestoreCarriesWhatChanged()).IsTrue();

    [Test]
    public async Task PowerShellRunsTheCommandFromItsOwnFile() => await Assert.That(ShellStateCases.PowerShellRunsTheCommandFromItsOwnFile()).IsTrue();

    [Test]
    public async Task ShowAndClearErrors() => await Assert.That(ShellStateCases.ShowAndClearErrors()).IsTrue();

    [Test]
    public async Task ShowAndClearAreKnownKeys() => await Assert.That(ShellStateCases.ShowAndClearAreKnownKeys()).IsTrue();

    [Test]
    public async Task CardErrors() => await Assert.That(ShellStateCases.CardErrors()).IsTrue();

    [Test]
    public async Task CardLinesSplitAndStyle() => await Assert.That(ShellStateCases.CardLinesSplitAndStyle()).IsTrue();

    [Test]
    public async Task CardComposition() => await Assert.That(ShellStateCases.CardComposition()).IsTrue();

    [Test]
    public async Task CardFollowsFitAndCrop() => await Assert.That(ShellStateCases.CardFollowsFitAndCrop()).IsTrue();

    [Test]
    public async Task CardCropErrors() => await Assert.That(ShellStateCases.CardCropErrors()).IsTrue();

    [Test]
    public async Task PromptPartsAndErrors() => await Assert.That(ShellStateCases.PromptPartsAndErrors()).IsTrue();

    [Test]
    public async Task FolderNamesHideThePath() => await Assert.That(ShellStateCases.FolderNamesHideThePath()).IsTrue();

    [Test]
    public async Task PromptFolderReplays() => await Assert.That(ShellStateCases.PromptFolderReplays()).IsTrue();

    [Test]
    public async Task PlaygroundComposesCards() => await Assert.That(ShellStateCases.PlaygroundComposesCards()).IsTrue();

    [Test]
    public async Task PlaygroundComposesShowAndClear() => await Assert.That(ShellStateCases.PlaygroundComposesShowAndClear()).IsTrue();

    [Test]
    public async Task HiddenStepFailureStops() => await Assert.That(ShellStateCases.HiddenStepFailureStops(continueOnError: false)).IsTrue();

    [Test]
    public async Task HiddenStepContinueOnError() => await Assert.That(ShellStateCases.HiddenStepFailureStops(continueOnError: true)).IsTrue();

    [Test]
    public async Task MaskerLearnsExportedSecrets() => await Assert.That(ShellStateCases.MaskerLearnsExportedSecrets()).IsTrue();

    [Test]
    public async Task BackgroundErrors() => await Assert.That(ShellStateCases.BackgroundErrors()).IsTrue();

    [Test]
    public async Task BackgroundStepStartsAndRecordsNothing() => await Assert.That(ShellStateCases.BackgroundStepStartsAndRecordsNothing()).IsTrue();

    [Test]
    public async Task BackgroundProblemStops() => await Assert.That(ShellStateCases.BackgroundProblemStops(continueOnError: false)).IsTrue();

    [Test]
    public async Task BackgroundProblemContinueOnError() => await Assert.That(ShellStateCases.BackgroundProblemStops(continueOnError: true)).IsTrue();

    [Test]
    public async Task PlaygroundSkipsBackground() => await Assert.That(ShellStateCases.PlaygroundSkipsBackground()).IsTrue();

    [Test]
    public async Task IntegrationBashCarriesState() => await Assert.That(ShellStateCases.IntegrationBashCarriesState(TestEnv.RepoRoot)).IsTrue();

    [Test]
    public async Task IntegrationBashLoginAliasesStayOff() => await Assert.That(ShellStateCases.IntegrationBashLoginAliasesStayOff(TestEnv.RepoRoot)).IsTrue();

    [Test]
    public async Task IntegrationPromptFollowsFolder() => await Assert.That(ShellStateCases.IntegrationPromptFollowsFolder(TestEnv.RepoRoot)).IsTrue();

    [Test]
    public async Task IntegrationPowerShellPromptFollowsFolder() => await Assert.That(ShellStateCases.IntegrationPowerShellPromptFollowsFolder(TestEnv.RepoRoot)).IsTrue();

    [Test]
    public async Task IntegrationBashShellPrompt() => await Assert.That(ShellStateCases.IntegrationBashShellPrompt(TestEnv.RepoRoot)).IsTrue();

    [Test]
    public async Task IntegrationBashReadonlyLoginPrompt() => await Assert.That(ShellStateCases.IntegrationBashReadonlyLoginPrompt(TestEnv.RepoRoot)).IsTrue();

    [Test]
    public async Task IntegrationZshReadonlyLoginPrompt() => await Assert.That(ShellStateCases.IntegrationZshReadonlyLoginPrompt(TestEnv.RepoRoot)).IsTrue();

    [Test]
    public async Task IntegrationZshShellPrompt() => await Assert.That(ShellStateCases.IntegrationZshShellPrompt(TestEnv.RepoRoot)).IsTrue();

    [Test]
    public async Task IntegrationPowerShellShellPrompt() => await Assert.That(ShellStateCases.IntegrationPowerShellShellPrompt(TestEnv.RepoRoot)).IsTrue();

    [Test]
    public async Task IntegrationWindowsPowerShellShellPrompt() => await Assert.That(ShellStateCases.IntegrationWindowsPowerShellShellPrompt(TestEnv.RepoRoot)).IsTrue();

    [Test]
    public async Task IntegrationPowerShellCarriesState() => await Assert.That(ShellStateCases.IntegrationPowerShellCarriesState(TestEnv.RepoRoot)).IsTrue();

    [Test]
    public async Task IntegrationWindowsPowerShellCarriesState() => await Assert.That(ShellStateCases.IntegrationWindowsPowerShellCarriesState(TestEnv.RepoRoot)).IsTrue();

    [Test]
    public async Task IntegrationPowerShellErrorsShowAsAlone() => await Assert.That(ShellStateCases.IntegrationPowerShellErrorsShowAsAlone(TestEnv.RepoRoot)).IsTrue();

    [Test]
    public async Task IntegrationPowerShellExitCodes() => await Assert.That(ShellStateCases.IntegrationPowerShellExitCodes(TestEnv.RepoRoot)).IsTrue();

    [Test]
    public async Task IntegrationWindowsPowerShellExitCodes() => await Assert.That(ShellStateCases.IntegrationWindowsPowerShellExitCodes(TestEnv.RepoRoot)).IsTrue();

    [Test]
    public async Task IntegrationShellPromptWarnsInOtherShells() => await Assert.That(ShellStateCases.IntegrationShellPromptWarnsInOtherShells(TestEnv.RepoRoot)).IsTrue();

    [Test]
    public async Task IntegrationWindowsPowerShellErrorsShowAsAlone() => await Assert.That(ShellStateCases.IntegrationWindowsPowerShellErrorsShowAsAlone(TestEnv.RepoRoot)).IsTrue();

    [Test]
    public async Task IntegrationPowerShellKeepsNamesThatDifferInCase() => await Assert.That(ShellStateCases.IntegrationPowerShellKeepsNamesThatDifferInCase(TestEnv.RepoRoot)).IsTrue();

    [Test]
    public async Task IntegrationZshCarriesState() => await Assert.That(ShellStateCases.IntegrationZshCarriesState(TestEnv.RepoRoot)).IsTrue();

    [Test]
    public async Task IntegrationShCarriesState() => await Assert.That(ShellStateCases.IntegrationShCarriesState(TestEnv.RepoRoot)).IsTrue();

    [Test]
    public async Task IntegrationHiddenStepFailureStops() => await Assert.That(ShellStateCases.IntegrationHiddenStepFailureStops(TestEnv.RepoRoot)).IsTrue();

    [Test]
    public async Task IntegrationBackgroundRunsUntilPost() => await Assert.That(ShellStateCases.IntegrationBackgroundRunsUntilPost(TestEnv.RepoRoot)).IsTrue();

    [Test]
    public async Task IntegrationBackgroundOutputIsReadAfterTheWait() => await Assert.That(ShellStateCases.IntegrationBackgroundOutputIsReadAfterTheWait(TestEnv.RepoRoot)).IsTrue();

    [Test]
    public async Task IntegrationBackgroundExitStops() => await Assert.That(ShellStateCases.IntegrationBackgroundExitStops(TestEnv.RepoRoot)).IsTrue();

    [Test]
    public async Task IntegrationBackgroundWaitTimeoutStops() => await Assert.That(ShellStateCases.IntegrationBackgroundWaitTimeoutStops(TestEnv.RepoRoot)).IsTrue();

    [Test]
    public async Task IntegrationBackgroundLongWaitTimeout() => await Assert.That(ShellStateCases.IntegrationBackgroundLongWaitTimeout(TestEnv.RepoRoot)).IsTrue();

    [Test]
    public async Task IntegrationBackgroundContinueOnError() => await Assert.That(ShellStateCases.IntegrationBackgroundContinueOnError(TestEnv.RepoRoot)).IsTrue();

    [Test]
    public async Task IntegrationBackgroundWaitReadsTheConsoleCodePage() => await Assert.That(ShellStateCases.IntegrationBackgroundWaitReadsTheConsoleCodePage(TestEnv.RepoRoot)).IsTrue();
}

internal static class ShellStateCases
{

    internal static bool Expect(bool condition, string message)
    {
        if (!condition)
            Console.Error.WriteLine($"  {message}");
        return condition;
    }

    // bash 3.2 (macOS) writes a line break inside double quotes; bash 5.2 writes $'...'. Neither ends the statement.
    internal static bool DeclarationsSplitOutsideQuotes()
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

    internal static bool ScalarValuesAreUnquoted() =>
        Expect(BashDump.ScalarValue("declare -x A=\"a \\\"b\\\" \\$c \\\\ `d`\"") == "a \"b\" $c \\ `d`", "double quotes")
        && Expect(BashDump.ScalarValue("declare -x B=$'x\\ny\\tz \\'q\\''") == "x\ny\tz 'q'", "dollar quotes")
        && Expect(BashDump.ScalarValue("declare -x C='plain'") == "plain", "single quotes")
        && Expect(BashDump.ScalarValue("declare -a D=([0]=\"x\")") is null, "an array has no scalar value")
        && Expect(BashDump.ScalarValue("declare -- E") is null, "no value");

    internal static bool RestoreCarriesWhatChanged()
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
    internal static bool LoginAliasesDoNotTurnOnExpansion()
    {
        const string aliases = "alias ll='ls -l'\nalias ls='ls -F --color=auto'\n";
        var restore = BashDump.BuildRestore("", "", "", "", aliases, aliases, "/work", []);
        return Expect(!restore.Contains("expand_aliases", StringComparison.Ordinal), restore)
            && Expect(!restore.Contains("alias ", StringComparison.Ordinal), restore);
    }

    // Once a step defines an alias, expansion is on, and the login files' aliases would expand with it: Git Bash's
    // node='winpty node.exe' then failed in a pipe with "stdin is not a tty". They are removed, in the step after the
    // one that defines the alias and in every later one, and a login alias that a step redefined stays.
    internal static bool LoginAliasesStayOffWithStepAlias()
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

    internal static bool QuoteKeepsSingleQuotes() =>
        Expect(BashDump.Quote("it's") == "'it'\\''s'", BashDump.Quote("it's"))
        && Expect(PowerShellState.Literal("it's") == "'it''s'", PowerShellState.Literal("it's"));

    internal static bool StateForEachShell()
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
    internal static bool ShellRestoreSharesFirstLine()
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

    internal static bool UnquoteJoinsParts() =>
        Expect(ShellText.Unquote("'it'\\''s'") == "it's", "bash and zsh")
        && Expect(ShellText.Unquote("'it'\"'\"'s'") == "it's", "dash")
        && Expect(ShellText.Unquote("$'it\\'s'") == "it's", "ksh93")
        && Expect(ShellText.Unquote("$'x\\ny\\tz'") == "x\ny\tz", "dollar quotes")
        && Expect(ShellText.Unquote("\"a \\\"b\\\" \\$c\"") == "a \"b\" $c", "double quotes")
        && Expect(ShellText.Unquote("plain\\ word") == "plain word", "a backslash")
        && Expect(ShellText.AssignedValue("export T='s 3'") == "s 3", "export -p")
        && Expect(ShellText.AssignedValue("arr=([0]=\"a\")") is null, "an array");

    // zsh writes one statement per line, $'...' for a line break, and a tied pair such as PATH and path as two.
    internal static bool ZshDeclarationsByName()
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

    internal static bool ZshRestoreCarriesWhatChanged()
    {
        const string baseVars = "export -T PATH path=( /usr/bin )\ntypeset -g -aT PATH path=( /usr/bin )\ntypeset -g KEEP=same\ntypeset -g GONE=x\ntypeset -g -i10 RANDOM=1\ntypeset -g -a argv=( base )\n";
        const string finalVars = "export -T PATH path=( /venv/bin /usr/bin )\ntypeset -g -aT PATH path=( /venv/bin /usr/bin )\ntypeset -g KEEP=same\nexport API_TOKEN='it'\\''s-secret'\ntypeset -g -i10 RANDOM=2\ntypeset -g -a argv=( final )\ntypeset -g __scenetake_dir=/tmp/x\nexport OLDPWD=/home\n";
        const string baseFunctions = "functions[old]=:\nfunctions[__scenetake_dump]=:\n";
        const string finalFunctions = "functions[greet]=$'\\techo hi'\nfunctions[__scenetake_dump]=:\nfunctions[__scenetake_save]=:\n";
        const string baseAliases = "aliases[run-help]=man\ngaliases[G]=\\|\\ grep\nsaliases[txt]=cat\n";
        const string finalAliases = "aliases[hi]=greet\naliases[run-help]=man\n";
        var exports = new List<KeyValuePair<string, string?>>();
        var restore = ZshDump.BuildRestore(baseVars, finalVars, baseFunctions, finalFunctions, baseAliases, finalAliases, "", "", "/work/it's", exports);

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

    // A PROMPT such as '$(git_prompt_info)' needs prompt_subst in every later shell, and only the prompt options carry.
    internal static bool ZshRestoreCarriesPromptOptions()
    {
        var exports = new List<KeyValuePair<string, string?>>();
        var restore = ZshDump.BuildRestore("", "", "", "", "", "", "promptsubst=off promptbang=on promptpercent=on\n", "promptsubst=on promptbang=on promptpercent=off\n", "/w", exports);
        var none = ZshDump.BuildRestore("", "", "", "", "", "", "", "", "/w", exports);
        return Expect(restore.Contains("builtin setopt promptsubst\n", StringComparison.Ordinal) && restore.Contains("builtin unsetopt promptpercent\n", StringComparison.Ordinal), restore)
            && Expect(!restore.Contains("promptbang", StringComparison.Ordinal), "an option that did not change")
            && Expect(!none.Contains("setopt", StringComparison.Ordinal), "no dumps, as from a shell that wrote none")
            && Expect(ZshDump.SplitOptions("promptsubst=on a;b=on promptbang=maybe =on x=\n").Count == 1, "only a name of letters that is on or off")
            // zsh loads commands by itself once a prompt reads it, and refuses it in the next restore. mapfile is a plain
            // variable until a step loads zsh/mapfile.
            && Expect(ZshDump.IsSkippedVariable("commands") && ZshDump.IsSkippedVariable("widgets") && !ZshDump.IsSkippedVariable("mapfile") && !ZshDump.IsSkippedVariable("SRANDOM"), "the tables zsh loads by itself")
            && ModuleTablesAndReadonlyNamesStay();

        // A table that zmodload loaded prints without a value, and restored, a fresh shell would refuse the module. The
        // login files' readonly PROMPT changes when a step sets PS1, and cannot be assigned.
        static bool ModuleTablesAndReadonlyNamesStay()
        {
            var tables = ZshDump.BuildRestore("", "typeset -A mapfile\ntypeset -g -ar errnos\ntypeset -g mapfile2=plain\n", "", "", "", "", "", "", "/w", []);
            var shared = ZshDump.BuildRestore("typeset -g -r PROMPT='ro'\ntypeset -g PS1='ro'\n", "typeset -g -r PROMPT='mine'\ntypeset -g PS1='mine'\n", "", "", "", "", "", "", "/w", []);
            // The readonly half of a tied pair is on its second line, and a readonly variable a step unset after
            // `typeset +r` cannot be unset in a fresh shell either.
            var tied = ZshDump.BuildRestore("export -T PATH path=( /bin )\ntypeset -g -arT PATH path=( /bin )\ntypeset -g -r FOO=1\n", "export -T PATH path=( /bin /opt )\ntypeset -g -arT PATH path=( /bin /opt )\n", "", "", "", "", "", "", "/w", []);
            // A step's `readonly PATH` puts -r on the first line, which would make path readonly before its own line.
            var stepTied = ZshDump.BuildRestore("export -T PATH path=( /bin )\ntypeset -g -aT PATH path=( /bin )\n", "export -rT PATH path=( /bin /run )\ntypeset -g -aT PATH path=( /bin /run )\ntypeset -g -r FOO=1\n", "", "", "", "", "", "", "/w", []);
            return Expect(!tables.Contains("typeset -A mapfile\n", StringComparison.Ordinal) && !tables.Contains("errnos", StringComparison.Ordinal) && tables.Contains("mapfile2=plain", StringComparison.Ordinal), tables)
                && Expect(!shared.Contains("PROMPT", StringComparison.Ordinal) && shared.Contains("typeset -g PS1='mine'\n", StringComparison.Ordinal), shared)
                && Expect(!tied.Contains("path", StringComparison.Ordinal) && !tied.Contains("FOO", StringComparison.Ordinal), tied)
                && Expect(stepTied.Contains("export -T PATH path=( /bin /run )\ntypeset -g -aT PATH path=( /bin /run )\n", StringComparison.Ordinal) && stepTied.Contains("typeset -g -r FOO=1\n", StringComparison.Ordinal), stepTied)
                && Expect(ZshDump.WithoutReadonly("typeset -g -arT PATH path=( /r )\ntypeset -r -T FOO foo=( r )") == "typeset -g -aT PATH path=( /r )\ntypeset -T FOO foo=( r )", "readonly taken out of each line")
                && Expect(ZshDump.IsReadonly("typeset -g -r PROMPT=x") && ZshDump.IsReadonly("typeset -Ar sysparams") && !ZshDump.IsReadonly("typeset -g -i10 RANDOM=1") && !ZshDump.IsReadonly("typeset -g mirror=r"), "readonly flags");
        }
    }

    // With settings.shell-prompt off, no script draws a prompt or clears the login files' one. zsh's note the prompt
    // options either way, since those carry as in a terminal.
    internal static bool ShellPromptScriptsOnlyWhenOn()
    {
        var bashOff = BashState.BuildScript("x=1\n");
        var bashOn = BashState.BuildScript("x=1\n", 72);
        var zshOff = ZshState.BuildFiles("/s", null, "x=1\n").Single(static file => file.Name == "finish.zsh").Text;
        var zshOn = ZshState.BuildFiles("/s", null, "x=1\n", 72).Single(static file => file.Name == "finish.zsh").Text;
        return Expect(!bashOff.Contains("prompt", StringComparison.Ordinal) && !bashOff.Contains("PS1", StringComparison.Ordinal), "bash draws or clears a prompt while shell-prompt is off")
            && Expect(!zshOff.Contains("__scenetake_prompt", StringComparison.Ordinal) && !zshOff.Contains("PS1", StringComparison.Ordinal), "zsh draws or clears a prompt while shell-prompt is off")
            && Expect(bashOn.Contains("__scenetake_save() { __scenetake_prompt \"$?\" || :; ", StringComparison.Ordinal) && bashOn.Contains(": 72 ))", StringComparison.Ordinal), bashOn)
            && Expect(bashOn.IndexOf("builtin unset PS1 PROMPT_COMMAND", StringComparison.Ordinal) is >= 0 and var bashClear && bashClear < bashOn.IndexOf("base.vars", StringComparison.Ordinal), "bash clears the login files' prompt before it notes the shell")
            && Expect(zshOn.Contains("__scenetake_save() { __scenetake_prompt $? || :; ", StringComparison.Ordinal) && zshOn.Contains(": 72 ))", StringComparison.Ordinal), zshOn)
            && Expect(zshOn.IndexOf("PS1=''", StringComparison.Ordinal) is >= 0 and var zshClear && zshClear < zshOn.IndexOf("__scenetake_dump base", StringComparison.Ordinal), "zsh clears the login files' prompt before it notes the shell")
            && Expect(!PowerShellSave(null).Contains("function:prompt", StringComparison.Ordinal), "PowerShell draws a prompt while shell-prompt is off")
            && Expect(PowerShellSave(72) is var on && on.IndexOf("function:prompt", StringComparison.Ordinal) is >= 0 and var drawAt && drawAt < on.IndexOf("Export-Clixml", StringComparison.Ordinal), "PowerShell draws the prompt before the save");

        static string PowerShellSave(int? columns)
        {
            using var state = ShellState.Create("pwsh", isPowerShell: true, columns)!;
            var text = state.Prepare("Write-Output hi").Command;
            var folder = Regex.Match(text, @"^\$__scenetake_dir = '((?:[^']|'')*)';").Groups[1].Value.Replace("''", "'", StringComparison.Ordinal);
            return File.ReadAllText(Path.Combine(folder, "save.ps1"));
        }
    }

    // The markers of bash's \[ and \], and a title that names the folder, are not in the cast. A line break moves to the
    // start of the next line, as the terminal the shell writes to makes it.
    internal static bool ShellPromptAsTheCastShowsIt() =>
        Expect(PromptTemplate.ShellPrompt("\u0001\u001b[32m\u0002dir\u0001\u001b[0m\u0002\u001b]0;ada@box: /home/ada\u0007 $ ") == "\u001b[32mdir\u001b[0m $ ", "markers and the title")
            && Expect(PromptTemplate.ShellPrompt("top\nnext\r\n> \r") == "top\r\nnext\r\n> \r", "line breaks")
            && Expect(PromptTemplate.ShellPrompt("\u001b]4;1;rgb:ff/00/00\u0007> \u001b") == "\u001b]4;1;rgb:ff/00/00\u0007> ", "a color OSC stays, and an ESC left open goes");

    // The shell's prompt shows from the step after the scenario set one, masked and counted as output is, and the
    // outputs file keeps it masked. Before then, settings.prompt shows, and the outputs file keeps none.
    internal static bool ShellPromptComposes()
    {
        static (string Text, List<RecordedStep> Recorded, GenerateOptions Options, SecretMasker Masker) Compose(bool on)
        {
            var scenario = ParseScenario($"settings:\n  prompt: \"tpl $ \"\n  shell-prompt: {(on ? "true" : "false")}\n  end-prompt: true\n  mask: [secret]\n  typing-speed: 0.001\n  typing-jitter: 0\nsteps:\n  - echo one\n  - echo two\n");
            var masker = SecretMasker.ForScenario(scenario, null)!;
            var drawn = new Queue<string?>([null, "\u001b[32msecret\u001b[0m\n> ", "end > "]);
            var recorded = new List<RecordedStep>();
            var options = new GenerateOptions { LogRunning = false, Masker = masker, Record = recorded, ShellPrompt = () => drawn.Dequeue() };
            var (events, _) = GenerateAsync(scenario, options, step => Task.FromResult(CommandExecution.FromPipeStream("out\n", "", 0, [new PipeStreamChunk(TimeSpan.Zero, false, "out\n")]))).GetAwaiter().GetResult();
            return (string.Concat(events.Select(static ev => ev.HasUtf8Output ? Encoding.UTF8.GetString(ev.Utf8Output.Span) : ev.Data)), recorded, options, masker);
        }

        var (text, recorded, options, masker) = Compose(on: true);
        var (offText, offRecorded, offOptions, _) = Compose(on: false);
        return Expect(text.Contains("tpl $ echo one", StringComparison.Ordinal) && text.Contains("\u001b[32m******\u001b[0m\r\n> echo two", StringComparison.Ordinal) && text.Contains("end > ", StringComparison.Ordinal), Show(text))
            && Expect(recorded[0].Prompt is null && recorded[1].Prompt == "\u001b[32m******\u001b[0m\r\n> " && options.EndShellPrompt == "end > ", $"recorded: {Show(recorded[1].Prompt)}, {Show(options.EndShellPrompt)}")
            && Expect(masker.Summary() is not null, "the masked word was only in a prompt, and was not counted")
            && Expect(offText.Contains("tpl $ echo two", StringComparison.Ordinal) && offRecorded.TrueForAll(static step => step.Prompt is null) && offOptions.EndShellPrompt is null, $"shell-prompt off: {Show(offText)}");
    }

    // The outputs file keeps each step's prompt and the closing one, and a replay draws them while shell-prompt is on.
    internal static bool ShellPromptReplays()
    {
        var outputs = new StepOutputs
        {
            Scenetake = "test",
            Os = "linux",
            Shell = "bash",
            Width = 40,
            Height = 8,
            EndPrompt = "end > ",
            Steps =
            [
                RecordedStep.Pipe("echo one", 0, [new PipeStreamChunk(TimeSpan.Zero, false, "one\n")]),
                RecordedStep.Pipe("echo two", 0, [], prompt: "\u001b[32mapp\u001b[0m > "),
            ],
        };
        using var stream = new MemoryStream();
        outputs.Write(stream);
        var json = Encoding.UTF8.GetString(stream.ToArray());
        var parsed = StepOutputs.Parse(json);
        if (!Expect(parsed.EndPrompt == "end > " && parsed.Steps[0].Prompt is null && parsed.Steps[1].Prompt == "\u001b[32mapp\u001b[0m > ", $"the prompts did not round-trip: {json}"))
            return false;

        const string yaml = "width: 40\nheight: 8\nsettings:\n  typing-speed: 0.01\n  typing-jitter: 0\n  prompt: \"tpl $ \"\n  shell-prompt: true\n  end-prompt: true\nsteps:\n  - echo one\n  - echo two\n";
        var on = CastOutput(Encoding.UTF8.GetString(PlaygroundRender.RenderScenario(yaml, parsed, new PlaygroundOptions(), renderSvg: false).Cast!));
        var off = CastOutput(Encoding.UTF8.GetString(PlaygroundRender.RenderScenario(yaml.Replace("shell-prompt: true", "shell-prompt: false", StringComparison.Ordinal), parsed, new PlaygroundOptions(), renderSvg: false).Cast!));
        return Expect(on.Contains("tpl $ echo one", StringComparison.Ordinal) && on.Contains("\u001b[32mapp\u001b[0m > echo two", StringComparison.Ordinal) && on.Contains("end > ", StringComparison.Ordinal), Show(on))
            && Expect(off.Contains("tpl $ echo two", StringComparison.Ordinal) && !off.Contains("end > ", StringComparison.Ordinal), $"shell-prompt off: {Show(off)}");
    }

    // dash writes a quote as '"'"' and a line break inside the quotes; export -p lists the exported names again.
    internal static bool PosixShellRestoreCarriesWhatChanged()
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

    // The user's lines keep their numbers and their text: the command is a file of its own, which the shell's command
    // line only names through the folder.
    internal static bool PowerShellRunsTheCommandFromItsOwnFile()
    {
        using var state = ShellState.Create("pwsh", isPowerShell: true)!;
        const string Command = "Write-Output one\nWrite-Output 'it''s two' # a comment";
        var text = state.Prepare(Command).Command;
        var folder = Regex.Match(text, @"^\$__scenetake_dir = '((?:[^']|'')*)';").Groups[1].Value.Replace("''", "'", StringComparison.Ordinal);
        return Expect(!text.Contains('\n', StringComparison.Ordinal) && !text.Contains("Write-Output", StringComparison.Ordinal), text)
            && Expect(Directory.Exists(folder), $"folder: {folder}")
            && Expect(File.ReadAllText(Path.Combine(folder, "command.ps1")) == Command, "command.ps1")
            && Expect(!File.ReadAllText(Path.Combine(folder, "run.ps1")).Contains(folder, StringComparison.OrdinalIgnoreCase), "run.ps1 names the folder");
    }

    internal static bool ShowAndClearErrors()
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

    internal static bool CardErrors()
    {
        var ok = true;
        ok &= ExpectError("- run: echo hi\n  card: Title", "Step 1 has both 'run' and 'card'");
        ok &= ExpectError("- card: Title\n  clear: true", "Step 1 has both 'card' and 'clear'");
        ok &= ExpectError("- card: Title\n  pty: true", "Step 1 key 'pty' does not apply to a 'card' step");
        ok &= ExpectError("- card: Title\n  name: Scene", "Step 1 key 'name' does not apply to a 'card' step");
        ok &= ExpectError("- card: {title: x}", "Step 1 key 'card' must be a line of text or a list of lines");
        ok &= ExpectError("- card: [\"\", \"[bold]\"]", "Step 1 key 'card' has no text to show");
        ok &= ExpectError("- card: [a, b, c, d, e, f, g, h, i]", "Step 1 card has 9 lines, more than the terminal's 8 rows", height: 8);
        ok &= ExpectError("- card: \"[bold]0123456789abc\"", "Step 1 card line 1 is 13 columns wide, wider than the terminal's 12", width: 12);
        ok &= ExpectError("- card: \"日本語のタイトルです\"", "Step 1 card line 1 is 20 columns wide, wider than the terminal's 12", width: 12);
        ok &= ExpectError("- card: \"a\\tb\"", "Step 1 card line 1 holds a control character");
        ok &= ExpectError("- card: \"a\\u0085b\"", "Step 1 card line 1 holds a control character");
        ok &= ExpectError("- card: \"a\\x7fb\"", "Step 1 card line 1 holds a control character");
        // A zero-width space takes no column, as the terminal draws it.
        ok &= ExpectError("- card: \"0123456789ab\\u200B\"", null, width: 12);
        ok &= ExpectError("- card: [my-app, Install: npm i -g my-app]", "Step 1 card line 2 is read as a mapping, because YAML takes \": \" in it as a key. Quote the line");
        // A card alone is a recording, unlike a clear step alone.
        ok &= ExpectError("- card: Title", null);
        ok &= ExpectError("- card: |\n    my-app\n    npm i -g my-app\n  post-delay: 3\n  pre-delay: 0\n- echo hi\n- card: [\"[bold fg:cyan] my-app \", 42, ~, \"\"]", null);
        return ok;

        static bool ExpectError(string steps, string? expected, int width = 80, int height = 24)
        {
            var yaml = $"width: {width}\nheight: {height}\nsteps:\n" + string.Join('\n', steps.Split('\n').Select(static line => "  " + line));
            var error = FindStepKeyError(ParseScenario(yaml));
            return expected is null
                ? Expect(error is null, $"unexpected error: {error}")
                : Expect(error is not null && error.StartsWith(expected, StringComparison.Ordinal), $"expected '{expected}', got '{error}'");
        }
    }

    // Without fit a card is centered in its window and needs no mark. A card as tall as its window still has the cell right
    // of its last line, unless that line reaches the last column. A window that a crop moves from the corner moves the
    // card and the mark with it.
    internal static bool CardComposition()
    {
        var whole = ComposeCard(["a", "b"], CanvasWindow.Whole(10, 9), null, markCorner: false);
        var marked = ComposeCard(["a", "b"], CanvasWindow.Whole(10, 6), null, markCorner: true);
        var tall = ComposeCard(["a", "b"], CanvasWindow.Whole(10, 2), null, markCorner: true);
        var full = ComposeCard(["a", "0123456789"], CanvasWindow.Whole(10, 2), null, markCorner: true);
        var cropped = ComposeCard(["a"], new CanvasWindow(2, 1, 6, 4), null, markCorner: true);
        var masker = SecretMasker.ForScenario(ParseScenario("settings:\n  mask: [\"hunter2\"]\nsteps:\n  - echo hi\n"), null);
        var masked = ComposeCard(["pass: hunter2"], CanvasWindow.Whole(20, 3), masker, markCorner: false);
        return Expect(whole == "\u001b[?25l\u001b[H\u001b[2J\u001b[4;5Ha\u001b[5;5Hb", Show(whole))
            && Expect(marked == "\u001b[?25l\u001b[H\u001b[2J\u001b[6;10H\u001b[1m \u001b[0m\u001b[3;5Ha\u001b[4;5Hb", Show(marked))
            && Expect(tall.Contains("\u001b[2;10H\u001b[1m ", StringComparison.Ordinal), Show(tall))
            && Expect(!full.Contains("\u001b[1m", StringComparison.Ordinal), Show(full))
            && Expect(cropped == "\u001b[?25l\u001b[H\u001b[2J\u001b[5;8H\u001b[1m \u001b[0m\u001b[3;5Ha", Show(cropped))
            && Expect(masked.Contains("pass: *******", StringComparison.Ordinal) && !masked.Contains("hunter2", StringComparison.Ordinal), Show(masked));
    }

    // A card is centered in what the crop leaves, so one larger than that is an error before anything runs. CropSpec is
    // top, bottom, left, right.
    internal static bool CardCropErrors()
    {
        var scenario = ParseScenario("width: 40\nheight: 10\nsteps:\n  - card: [one, two, three, four, five]\n  - card: \"[bold]0123456789012345678901234567890123\"\n");
        return Expect(FindCardCropError(scenario, new CropSpec(7, 0, 0, 0)) == "Step 1 card has 5 lines, more than the 3 rows the crop leaves. Leave lines out, or crop less.", "rows")
            && Expect(FindCardCropError(scenario, new CropSpec(0, 0, 10, 10)) == "Step 2 card line 1 is 34 columns wide, wider than the 20 columns the crop leaves. Shorten it, or crop less.", "columns")
            && Expect(FindCardCropError(scenario, new CropSpec(2, 3, 3, 3)) is null, "5 lines in the 5 rows and 34 columns the crop leaves")
            && Expect(FindCardCropError(scenario, new CropSpec(10, 0, 0, 0)) is null, "a crop that leaves nothing has its own error")
            && Expect(FindCardCropError(scenario, default) is null, "no crop");
    }

    // Where the composer puts a card for the scenario's fit and crop, in the cast the playground composes.
    internal static bool CardFollowsFitAndCrop()
    {
        // fit: both moves the right edge in to the widest content, "$ echo hi" and the cursor after it, 10 columns, and the
        // bottom to the closing prompt, 3 rows. "my-app" is centered in them: row 1, column 2, and the mark keeps the
        // window's last cell.
        var both = CardScreen("render:\n  fit: both\n", "- card: my-app\n- echo hi", out var bothCast);
        // crop-top: 2 cuts the first two rows. The steps show in rows 2 to 4, and the card keeps a blank row around its
        // 3 lines: 5 rows from row 2, which center it at rows 3 to 5.
        var crop = CardScreen("height: 10\nrender:\n  crop-top: 2\n", "- card: [title, sub, third]\n- echo a\n- echo b", out var cropCast);
        // A step that writes row 12 and moves the cursor home in one event shows 13 rows, below the typed command.
        var moved = CardScreen("height: 14\n", "- card: [title]\n- printf 'top\\n\\n\\n\\n\\n\\n\\n\\n\\n\\n\\nrow12\\033[H'", out var movedCast, "top\n\n\n\n\n\n\n\n\n\n\nrow12\u001b[H");
        // Without fit the card is centered in the whole terminal.
        var none = CardScreen("render:\n  fit: none\n", "- card: my-app\n- echo hi", out _);
        return Expect(both is not null && both[1].TrimEnd() == "  my-app" && bothCast.Contains("\u001b[3;10H\u001b[1m ", StringComparison.Ordinal), $"fit both: {Show(bothCast)}")
            && Expect(crop is not null && crop[3].Trim() == "title" && crop[5].Trim() == "third", $"crop: {Show(cropCast)}")
            && Expect(moved is not null && moved[6].Trim() == "title", $"cursor moved up: {Show(movedCast)}")
            && Expect(none is not null && none[11].TrimEnd() == new string(' ', 37) + "my-app", "fit none");

        // The screen of the card, the cast's first output event, and the whole cast's output.
        static string[]? CardScreen(string head, string steps, out string cast, string output = "hi\n")
        {
            var yaml = head + "settings:\n  typing-speed: 0.01\n  typing-jitter: 0\n  pre-delay: 0\n  post-delay: 0\nsteps:\n"
                + string.Join('\n', steps.Split('\n').Select(static line => "  " + line)) + "\n";
            var scenario = ParseScenario(yaml);
            var width = scenario.Width ?? 80;
            var height = scenario.Height ?? 24;
            // The last step prints `output`, and a step before it, if any, prints "a".
            var lines = steps.Split('\n');
            var recorded = new List<RecordedStep>();
            if (lines.Length > 2)
                recorded.Add(RecordedStep.Pipe(lines[^2].TrimStart('-', ' '), 0, [new PipeStreamChunk(TimeSpan.Zero, false, "a\n")]));
            recorded.Add(RecordedStep.Pipe(lines[^1].TrimStart('-', ' '), 0, [new PipeStreamChunk(TimeSpan.Zero, false, output)]));
            var outputs = new StepOutputs { Scenetake = "test", Os = "linux", Shell = "bash", Width = width, Height = height, Steps = recorded };
            var result = PlaygroundRender.RenderScenario(yaml, outputs, new PlaygroundOptions(), renderSvg: false);
            cast = result.Cast is null ? $"render failed: {result.Error}" : CastOutput(Encoding.UTF8.GetString(result.Cast));
            if (result.Cast is null)
                return null;
            // The card's screen ends where the next step clears it.
            var first = cast[..cast.IndexOf(ClearScreen + "\u001b[?25h", StringComparison.Ordinal)];
            var theme = TerminalTheme.ForMeasuring();
            var buffer = new ScreenBuffer(width, height, theme);
            new AnsiParser(buffer, theme).Process(first);
            return [.. Enumerable.Range(0, height).Select(row => string.Concat(Enumerable.Range(0, width).Select(col => buffer.GetCell(row, col).Text is { Length: > 0 } text ? text : " ")))];
        }
    }

    internal static bool CardLinesSplitAndStyle() =>
        Expect(CardLines("one\ntwo\n") is ["one", "two"], "a block's lines, without the last line break")
        && Expect(CardLines(new List<object?> { "a\nb", 3, null }) is ["a", "b", "3", ""], "list entries, numbers, and empty entries")
        && Expect(CardLines(true) is null && CardLines(new List<object?> { new Dictionary<object, object?>() }) is null, "a value that is not text")
        && Expect(SplitCardStyle("[bold fg:cyan]  my-app  ") == ("bold fg:cyan", "my-app"), "a style prefix")
        && Expect(SplitCardStyle("  plain  ") == (null, "plain"), "no prefix")
        && Expect(DisplayWidth("aé日本") == 6 && DisplayWidth("é") == 1, "display width")
        && Expect(DisplayWidth("a‍b­﻿") == 2 && DisplayWidth("👩‍💻") == 4, "zero-width characters");

    // A title card, a command, then a closing card, composed as the playground composes them. The cast is played on
    // scenetake's own terminal to check where each line lands.
    internal static bool PlaygroundComposesCards()
    {
        const string yaml = """
        width: 30
        height: 9
        settings:
          typing-speed: 0.01
          typing-jitter: 0
          pre-delay: 0
          post-delay: 0
        steps:
          - card:
              - "[bold fg:cyan]my-app"
              - ""
              - 日本語
          - name: Scene
            run: ls
          - card: npm i -g my-app
            post-delay: 3
        """;
        var outputs = new StepOutputs
        {
            Scenetake = "test",
            Os = "linux",
            Shell = "bash",
            Width = 30,
            Height = 9,
            Steps = [RecordedStep.Pipe("ls", 0, [new PipeStreamChunk(TimeSpan.Zero, false, "file.txt\n")])],
        };
        var result = PlaygroundRender.RenderScenario(yaml, outputs, new PlaygroundOptions(), renderSvg: false);
        if (!Expect(result.Error is null && result.Cast is not null, $"render failed: {result.Error}"))
            return false;

        var output = CastOutput(Encoding.UTF8.GetString(result.Cast!));
        // The step after a card starts with the clear that ends the card.
        var cardCleared = output.IndexOf(ClearScreen + "\u001b[?25h", StringComparison.Ordinal);
        if (!Expect(cardCleared > 0, $"no clear after the title card: {Show(output)}"))
            return false;
        var title = Play(output[..cardCleared], 30, 9, out _);
        var middle = Play(output[..output.LastIndexOf("\u001b[?25l" + ClearScreen, StringComparison.Ordinal)], 30, 9, out var middleCursorShown);
        var end = Play(output, 30, 9, out var endCursorShown);
        // With fit: height (the default), cards are centered in the rows the rest uses: the comment, the typed command,
        // its output, and the cursor below it, 4 rows, and at least the tallest card's 3 lines and a blank row above and
        // below, 5. Three lines in 5 rows start at row 1 (0-based). "my-app" is 6 wide in 30, and "日本語" is 6 too.
        return Expect(title[1] == new string(' ', 12) + "my-app" + new string(' ', 12) && title[3].StartsWith(new string(' ', 12) + "日本語", StringComparison.Ordinal),
                $"the title card's lines: {Show(string.Join('\n', title))}")
            && Expect(output.Contains("\u001b[5;30H\u001b[1m \u001b[0m", StringComparison.Ordinal), $"the mark in the window's last cell: {Show(output)}")
            && Expect(LastOutputEvent(Encoding.UTF8.GetString(result.Cast!)) == (3.0, "\u001b[?25l"), "the closing card stays for its post-delay, 3 s, until an event that changes nothing")
            && Expect(output.Contains("\u001b[1;36mmy-app\u001b[0m", StringComparison.Ordinal), "the line's style")
            && Expect(middle[0].StartsWith("# Scene", StringComparison.Ordinal) && middle[1].StartsWith("$ ls", StringComparison.Ordinal) && middle[2].StartsWith("file.txt", StringComparison.Ordinal),
                $"the step after the card starts on a cleared screen: {Show(string.Join('\n', middle))}")
            && Expect(middleCursorShown, "the cursor stayed hidden after the card")
            && Expect(end[2] == new string(' ', 7) + "npm i -g my-app" + new string(' ', 8) && end.Count(static row => row.Trim().Length > 0) == 1,
                $"the closing card: {Show(string.Join('\n', end))}")
            && Expect(!endCursorShown && !output.EndsWith("$ ", StringComparison.Ordinal), "a prompt or a cursor over the closing card");

        static string[] Play(string text, int width, int height, out bool cursorVisible)
        {
            var theme = TerminalTheme.ForMeasuring();
            var buffer = new ScreenBuffer(width, height, theme);
            new AnsiParser(buffer, theme).Process(text);
            cursorVisible = buffer.CursorVisible;
            return [.. Enumerable.Range(0, height).Select(row => string.Concat(Enumerable.Range(0, width).Select(col =>
            buffer.GetCell(row, col) is var cell && cell.IsWideContinuation ? "" : cell.Text is { Length: > 0 } text ? text : " ")))];
        }
    }

    internal static bool PromptPartsAndErrors()
    {
        var plain = ParsePrompt("$ ");
        var styled = ParsePrompt(new List<object?> { new Dictionary<object, object?> { ["text"] = "demo", ["color"] = "bold fg:green" }, " {dir} $ ", 42, null });
        var ok = Expect(plain.Render("x") == "$ " && !plain.UsesDir, "plain text")
            && Expect(styled.UsesDir && styled.Render("my-app") == "\u001b[1;32mdemo\u001b[0m my-app $ 42", Show(styled.Render("my-app")))
            && Expect(ParsePrompt(null).Render("") == DefaultPrompt, "the default");
        ok &= ExpectError("prompt: \"{dir} $ \"", null);
        ok &= ExpectError("prompt:\n    - text: demo\n      color: green\n    - \" $ \"", null);
        ok &= ExpectError("prompt:\n    a: b", "'settings.prompt' must be text or a list of parts");
        ok &= ExpectError("prompt:\n    - [a, b]", "settings.prompt part 1 must be text or a mapping with 'text' and 'color'");
        ok &= ExpectError("prompt:\n    - color: green", "settings.prompt part 1 needs 'text'");
        ok &= ExpectError("prompt:\n    - text: x\n      color: [red]", "settings.prompt part 1 key 'color' must be a style");
        var unknown = FindUnknownScenarioKeys("settings:\n  prompt:\n    - text: x\n      colour: red\nsteps:\n  - echo hi\n");
        return ok && Expect(unknown.Count == 1 && unknown[0].Contains("in settings.prompt part 1 (did you mean 'color'?)", StringComparison.Ordinal), string.Join("; ", unknown));

        static bool ExpectError(string settings, string? expected)
        {
            var error = FindStepKeyError(ParseScenario($"settings:\n  {settings}\nsteps:\n  - echo hi\n"));
            return expected is null
                ? Expect(error is null, $"unexpected error: {error}")
                : Expect(error is not null && error.StartsWith(expected, StringComparison.Ordinal), $"expected '{expected}', got '{error}'");
        }
    }

    // {dir} shows a folder's last name, ~ for the home folder, and / for the root, never the rest of the path.
    internal static bool FolderNamesHideThePath()
    {
        var ok = Expect(PromptTemplate.FolderName("/home/me/my-app", "/home/me") == "my-app", "a folder")
            && Expect(PromptTemplate.FolderName("/home/me/", "/home/me") == "~", "the home folder")
            && Expect(PromptTemplate.FolderName("/", "/home/me") == "/", "the root")
            && Expect(PromptTemplate.FolderName("/home/me/my-app/", null) == "my-app", "a trailing slash")
            // PowerShell's ~ is the user profile when HOME names another folder: both hide the user name.
            && Expect(PromptTemplate.FolderName("/home/me", "/opt/home", "/home/me") == "~" && PromptTemplate.FolderName("/opt/home", "/opt/home", "/home/me") == "~", "a second home");
        if (OperatingSystem.IsWindows())
        {
            ok &= Expect(PromptTemplate.FolderName(@"C:\Users\me", @"C:\Users\me") == "~", "Windows home")
                && Expect(PromptTemplate.FolderName("/c/Users/me", @"C:\Users\Me") == "~", "Git Bash's home")
                && Expect(PromptTemplate.FolderName("/c/Users/me/src", @"C:\Users\me") == "src", "Git Bash's folder")
                && Expect(PromptTemplate.FolderName(@"D:\", @"C:\Users\me") == @"D:\", "a drive's root")
                && Expect(PromptTemplate.NativePath("/cygdrive/d/work") == @"D:\work" && PromptTemplate.NativePath("/tmp") == "/tmp", "native paths");
        }

        return ok;
    }

    // The outputs file keeps each step's folder and the closing one, and a replay draws the same prompts.
    internal static bool PromptFolderReplays()
    {
        var outputs = new StepOutputs
        {
            Scenetake = "test",
            Os = "linux",
            Shell = "bash",
            Width = 40,
            Height = 8,
            EndFolder = "src",
            Steps =
            [
                RecordedStep.Pipe("echo one", 0, [new PipeStreamChunk(TimeSpan.Zero, false, "one\n")], folder: "~"),
            RecordedStep.Pipe("cd src", 0, [], folder: "my-app"),
        ],
        };
        using var stream = new MemoryStream();
        outputs.Write(stream);
        var parsed = StepOutputs.Parse(Encoding.UTF8.GetString(stream.ToArray()));
        if (!Expect(parsed.EndFolder == "src" && parsed.Steps[0].Folder == "~" && parsed.Steps[1].Folder == "my-app", "the folders did not round-trip"))
            return false;

        const string yaml = "width: 40\nheight: 8\nsettings:\n  typing-speed: 0.01\n  typing-jitter: 0\n  prompt: \"{dir} $ \"\n  end-prompt: true\nsteps:\n  - echo one\n  - cd src\n";
        var result = PlaygroundRender.RenderScenario(yaml, parsed, new PlaygroundOptions(), renderSvg: false);
        var output = CastOutput(Encoding.UTF8.GetString(result.Cast!));
        var noFolders = PlaygroundRender.RenderScenario(yaml, StepOutputs.Parse(Encoding.UTF8.GetString(stream.ToArray()).Replace("\"folder\"", "\"other\"").Replace("\"endFolder\"", "\"other\"")), new PlaygroundOptions(), renderSvg: false);
        // The closing prompt, and the event after it that sets the final screen's hold.
        return Expect(output.Contains("~ $ echo one", StringComparison.Ordinal) && output.Contains("my-app $ cd src", StringComparison.Ordinal) && output.EndsWith("src $ \u001b[0m", StringComparison.Ordinal), Show(output))
            && Expect(CastOutput(Encoding.UTF8.GetString(noFolders.Cast!)).Contains("~ $ cd src", StringComparison.Ordinal), "a file without folders shows ~");
    }

    internal static bool ShowAndClearAreKnownKeys()
    {
        var unknown = FindUnknownScenarioKeys("steps:\n  - run: echo hi\n    show: none\n  - clear: true\n    name: Scene\n  - echo done\n  - card: [Title]\n    post-delay: 2\n");
        return Expect(unknown.Count == 0, string.Join("; ", unknown));
    }

    // The playground runs nothing: a hidden step takes no recorded output and no time, and the clear is the cast's own.
    internal static bool PlaygroundComposesShowAndClear()
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

    internal static bool HiddenStepFailureStops(bool continueOnError)
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
        var (events, failure) = GenerateAsync(ParseScenario(yaml), new GenerateOptions { LogRunning = false }, step =>
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

    internal static bool MaskerLearnsExportedSecrets()
    {
        var masker = SecretMasker.ForScenario(new Scenario(), [])!;
        masker.Learn([new("MY_API_KEY", "correct-horse-battery-staple"), new("GREETING", "hello-there-friend")]);
        var off = SecretMasker.ForScenario(ParseScenario("settings:\n  mask-auto: false\n  mask: [listed-text]\nsteps: [echo]"), [])!;
        off.Learn([new("MY_API_KEY", "correct-horse-battery-staple")]);
        return Expect(masker.MaskText("[correct-horse-battery-staple]") == "[" + new string('*', 28) + "]", masker.MaskText("[correct-horse-battery-staple]"))
            && Expect(masker.MaskText("hello-there-friend") == "hello-there-friend", "a variable whose name is no secret")
            && Expect(off.MaskText("correct-horse-battery-staple") == "correct-horse-battery-staple", "mask-auto: false learned a value");
    }

    internal static bool BackgroundErrors()
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

    internal static bool BackgroundStepStartsAndRecordsNothing()
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
        var (events, failure) = GenerateAsync(ParseScenario(yaml), new GenerateOptions { LogRunning = false }, step =>
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

    internal static bool BackgroundProblemStops(bool continueOnError)
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
        var (_, failure) = GenerateAsync(ParseScenario(yaml), new GenerateOptions { LogRunning = false }, step =>
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
    internal static bool PlaygroundSkipsBackground()
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

    internal static bool IntegrationBackgroundRunsUntilPost(string repoRoot)
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

    internal static bool IntegrationBackgroundOutputIsReadAfterTheWait(string repoRoot)
    {
        // Each background command prints 1MB, more than any pipe holds, and only then writes its file. A command whose
        // output nobody read would block on the full pipe and never write it.
        if (!TryGetBinary(out var bin))
            return true;

        var run = RunFixture(bin, repoRoot, "background-chatty.yaml");
        return Expect(run.ExitCode == 0, $"exit {run.ExitCode}\n{run.Stderr}")
            && Expect(run.Text.Contains("after-wait=done no-wait=done", StringComparison.Ordinal), $"the next step: {Show(run.Text)}");
    }

    internal static bool IntegrationBackgroundExitStops(string repoRoot)
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

    internal static bool IntegrationBackgroundWaitTimeoutStops(string repoRoot)
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

    internal static bool IntegrationBackgroundLongWaitTimeout(string repoRoot)
    {
        if (!TryGetBinary(out var bin))
            return true;

        var run = RunFixture(bin, repoRoot, "background-long-wait-timeout.yaml");
        return Expect(run.ExitCode == 0, $"exit {run.ExitCode}\n{run.Stderr}")
            && Expect(run.Stderr.Contains("ready after", StringComparison.Ordinal), run.Stderr)
            && Expect(run.Text.Contains("after", StringComparison.Ordinal), "the next step");
    }

    internal static bool IntegrationBackgroundContinueOnError(string repoRoot)
    {
        if (!TryGetBinary(out var bin))
            return true;

        var run = RunFixture(bin, repoRoot, "background-continue.yaml");
        return Expect(run.ExitCode == 0, $"exit {run.ExitCode}\n{run.Stderr}")
            && Expect(run.Stderr.Contains("Warning: background step 1 exited 3 before it printed \"never printed\", and the steps go on (continue-on-error)", StringComparison.Ordinal), run.Stderr)
            && Expect(run.Text.Contains("after", StringComparison.Ordinal), "the next step");
    }

    internal static bool IntegrationBackgroundWaitReadsTheConsoleCodePage(string repoRoot)
    {
        // cmd writes the code page of its console, not UTF-8, as it does in a pipe step, which reads it with
        // PipeOutputDecoder. A background step read only UTF-8, so a wait for a word outside ASCII never matched.
        if (!OperatingSystem.IsWindows())
        {
            Skip.Test("Windows only: the console code page");
            return true;
        }

        if (!TryGetBinary(out var bin))
            return true;

        var codePage = Kernel32.GetOEMCP();
        var word = codePage switch
        {
            932 => "準備",
            437 or 850 or 858 => "prêt",
            _ => null,
        };
        if (word is null)
        {
            Skip.Test($"no test word for code page {codePage}");
            return true;
        }

        var folder = Path.Combine(Path.GetTempPath(), $"scenetake-background-cp-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        try
        {
            var scenario = Path.Combine(folder, "background-cmd.yaml");
            File.WriteAllText(scenario, $"""
                shell: cmd
                width: 60
                height: 6
                settings:
                  typing-speed: 0.001
                  pre-delay: 0
                  post-delay: 0
                steps:
                  - run: echo {word} & ping -n 30 127.0.0.1 >nul
                    background: true
                    wait: "{word}"
                    wait-timeout: 10
                  - run: echo after
                """);
            var (exitCode, stderr) = RunBinary(bin, repoRoot, [scenario, Path.Combine(folder, "out")]);
            return Expect(exitCode == 0, $"exit {exitCode}\n{stderr}")
                && Expect(stderr.Contains("ready after", StringComparison.Ordinal), stderr);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    internal static bool IntegrationBashCarriesState(string repoRoot)
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

    // The prompt's {dir} follows pre's cd, a step's cd, and a hidden step's cd, and a replay of the outputs file draws the
    // same prompts, byte for byte.
    internal static bool IntegrationPromptFollowsFolder(string repoRoot)
    {
        if (!TryGetBinary(out var bin))
            return true;

        var stem = Path.Combine(Path.GetTempPath(), $"scenetake-prompt-{Guid.NewGuid():N}");
        var outputs = stem + ".outputs.json";
        try
        {
            var fixture = Path.Combine("tests", "fixtures", "shell-state-bash-prompt.yaml");
            var (recordExit, recordStderr) = RunBinary(bin, repoRoot, ["--save-outputs", outputs, fixture, stem]);
            var recorded = File.Exists(stem + ".cast") ? File.ReadAllBytes(stem + ".cast") : [];
            var (replayExit, replayStderr) = RunBinary(bin, repoRoot, ["--replay", outputs, fixture, stem + "-replay"]);
            var replayed = File.Exists(stem + "-replay.cast") ? File.ReadAllBytes(stem + "-replay.cast") : [];
            var text = CastOutput(Encoding.UTF8.GetString(recorded));
            var outputsText = File.Exists(outputs) ? File.ReadAllText(outputs) : "";
            var prompt = static (string dir) => $"\u001b[34m{dir}\u001b[0m $ ";
            // The step that removed its folder leaves the next one where bash starts: the folder the test runs in.
            var start = Path.GetFileName(repoRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            return Expect(recordExit == 0 && replayExit == 0, $"{recordStderr}\n{replayStderr}")
                && Expect(text.Contains(prompt("tests") + "echo first", StringComparison.Ordinal)
                    && text.Contains(prompt("tests") + "cd ********", StringComparison.Ordinal)
                    && text.Contains(prompt("********") + "echo second", StringComparison.Ordinal)
                    && text.Contains(prompt("tests") + "echo third", StringComparison.Ordinal)
                    && text.Contains(prompt(start) + "echo fourth", StringComparison.Ordinal)
                    && text.EndsWith(prompt(start), StringComparison.Ordinal), Show(text))
                // The run keeps the YAML's text, which the scenario holds anyway. The folder is what a step printed.
                && Expect(outputsText.Contains("\"folder\": \"********\"", StringComparison.Ordinal) && !outputsText.Contains("\"folder\": \"fixtures\"", StringComparison.Ordinal),
                    $"the outputs file holds the masked folder: {outputsText}")
                && Expect(recorded.AsSpan().SequenceEqual(replayed), "the replay drew other prompts");
        }
        finally
        {
            foreach (var file in new[] { stem + ".cast", stem + "-replay.cast", outputs })
                File.Delete(file);
        }
    }

    internal static (int ExitCode, string Stderr) RunBinary(string bin, string repoRoot, string[] arguments)
    {
        var psi = new ProcessStartInfo(bin) { RedirectStandardError = true, RedirectStandardOutput = true, UseShellExecute = false, WorkingDirectory = repoRoot };
        foreach (var argument in arguments)
            psi.ArgumentList.Add(argument);
        using var process = Process.Start(psi)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(TimeSpan.FromSeconds(180)))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"{string.Join(' ', arguments)} ran for over 180 s");
        }

        return (process.ExitCode, stderr.Result);
    }

    // HOME is a folder whose .bash_profile defines aliases. Before the fix, a step's alias turned expansion on for them
    // too, as Git Bash's node='winpty node.exe' then failed in a pipe with "stdin is not a tty".
    internal static bool IntegrationBashLoginAliasesStayOff(string repoRoot)
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

    internal static bool IntegrationPowerShellPromptFollowsFolder(string repoRoot)
    {
        if (!TryGetBinary(out var bin))
            return true;
        if (!IsOnPath("pwsh"))
        {
            Skip.Test("pwsh is not on PATH");
            return true;
        }

        var run = RunFixture(bin, repoRoot, "shell-state-pwsh-prompt.yaml");
        var text = StripEscapes(run.Text);
        return Expect(run.ExitCode == 0, $"exit {run.ExitCode}\n{run.Stderr}")
            && Expect(text.Contains("tests > Write-Output first", StringComparison.Ordinal)
                && text.Contains("tests > Set-Location fixtures", StringComparison.Ordinal)
                && text.Contains("fixtures > Write-Output second", StringComparison.Ordinal)
                && text.TrimEnd().EndsWith("fixtures >", StringComparison.Ordinal), Show(text));
    }

    internal static bool IntegrationPowerShellCarriesState(string repoRoot)
    {
        if (!TryGetBinary(out var bin))
            return true;
        if (!IsOnPath("pwsh"))
        {
            Skip.Test("pwsh is not on PATH");
            return true;
        }

        var run = RunFixture(bin, repoRoot, "shell-state-pwsh.yaml", PowerShellStateEnvironment());
        return PowerShellStateCarried(run)
            && Expect(run.Text.Contains("Exception: boom", StringComparison.Ordinal) && !run.Text.Contains("Line |", StringComparison.Ordinal), "the error's display");
    }

    internal static bool IntegrationWindowsPowerShellCarriesState(string repoRoot)
    {
        // Windows PowerShell's error display is its own, so only the state is checked.
        if (!TryGetBinary(out var bin))
            return true;
        if (!OperatingSystem.IsWindows() || !IsOnPath("powershell"))
        {
            Skip.Test("Windows PowerShell is not on PATH");
            return true;
        }

        return RunInWindowsPowerShell(bin, repoRoot, "shell-state-pwsh.yaml", PowerShellStateEnvironment()) is { } run && PowerShellStateCarried(run);
    }

    internal static bool IntegrationPowerShellErrorsShowAsAlone(string repoRoot)
    {
        if (!TryGetBinary(out var bin))
            return true;
        if (!IsOnPath("pwsh"))
        {
            Skip.Test("pwsh is not on PATH");
            return true;
        }

        var run = RunFixture(bin, repoRoot, "shell-state-pwsh-errors.yaml");
        var text = run.Text;
        return PowerShellErrorsShownAsAlone(run)
            && Expect(text.Contains("Exception: boom", StringComparison.Ordinal), $"the error on the first line: {Show(text)}")
            && Expect(text.Contains("   2 |  throw 'second'", StringComparison.Ordinal), $"the error on the second line: {Show(text)}")
            && Expect(text.Contains("   1 |  Get-Item -Path )", StringComparison.Ordinal), $"the syntax error: {Show(text)}");
    }

    // Windows PowerShell writes where each error is, so the positions must be the command's own.
    internal static bool IntegrationWindowsPowerShellErrorsShowAsAlone(string repoRoot)
    {
        if (!TryGetBinary(out var bin))
            return true;
        if (!OperatingSystem.IsWindows() || !IsOnPath("powershell"))
        {
            Skip.Test("Windows PowerShell is not on PATH");
            return true;
        }

        if (RunInWindowsPowerShell(bin, repoRoot, "shell-state-pwsh-errors.yaml", null) is not { } run)
            return false;
        var text = run.Text;
        return PowerShellErrorsShownAsAlone(run)
            && Expect(text.Contains("At line:1 char:1\n+ throw 'boom'\n", StringComparison.Ordinal), $"the error on the first line: {Show(text)}")
            && Expect(text.Contains("At line:2 char:1\n+ throw 'second'\n", StringComparison.Ordinal), $"the error on the second line: {Show(text)}")
            && Expect(text.Contains("\n+ Get-Item -Path )\n", StringComparison.Ordinal), $"the syntax error: {Show(text)}");
    }

    internal static bool IntegrationPowerShellExitCodes(string repoRoot)
    {
        if (!TryGetBinary(out var bin))
            return true;
        if (!IsOnPath("pwsh"))
        {
            Skip.Test("pwsh is not on PATH");
            return true;
        }

        if (!PowerShellExitCodesKept(RunFixture(bin, repoRoot, "shell-state-pwsh-exit-codes.yaml")))
            return false;

        // The first step's end block fails and its clean block does not, and the second the other way round, so only
        // the second exits 1 when $? is read after the clean block.
        var clean = RunFixture(bin, repoRoot, "shell-state-pwsh-clean.yaml");
        return Expect(clean.ExitCode == 0, $"exit {clean.ExitCode}\n{clean.Stderr}")
            && Expect(clean.Text.Contains("\nend-ran\n", StringComparison.Ordinal) && clean.Text.Contains("in-clean", StringComparison.Ordinal), $"the blocks ran: {Show(clean.Text)}")
            && Expect(!clean.Stderr.Contains("exited 1: end { Write-Error 'in-end' }", StringComparison.Ordinal), $"a failing end block before a clean block: {clean.Stderr}")
            && Expect(clean.Stderr.Contains("step exited 1: end { 'end-' + 'ran' } clean { Write-Error ('in-' + 'clean') }", StringComparison.Ordinal), $"a failing clean block: {clean.Stderr}");
    }

    internal static bool IntegrationWindowsPowerShellExitCodes(string repoRoot)
    {
        if (!TryGetBinary(out var bin))
            return true;
        if (!OperatingSystem.IsWindows() || !IsOnPath("powershell"))
        {
            Skip.Test("Windows PowerShell is not on PATH");
            return true;
        }

        return RunInWindowsPowerShell(bin, repoRoot, "shell-state-pwsh-exit-codes.yaml", null) is { } run && PowerShellExitCodesKept(run);
    }

    // Each step's exit code shows in the warning scenetake prints for it, and a step that exits 0 prints none. The
    // named blocks' output is joined from parts, so only a step that ran prints it.
    private static bool PowerShellExitCodesKept((int ExitCode, string Stderr, string Text) run) =>
        Expect(run.ExitCode == 0, $"exit {run.ExitCode}\n{run.Stderr}")
            && Expect(run.Stderr.Contains("step exited 1: Write-Error 'write-error-fails'", StringComparison.Ordinal), $"Write-Error: {run.Stderr}")
            && Expect(run.Stderr.Contains("step exited 1: if ($null -eq $IsWindows", StringComparison.Ordinal), $"a failing native command: {run.Stderr}")
            && Expect(run.Stderr.Contains("step exited 1: 1/0", StringComparison.Ordinal), $"an error that stops the last statement: {run.Stderr}")
            && Expect(run.Stderr.Contains("step exited 7: exit 7", StringComparison.Ordinal), $"exit 7: {run.Stderr}")
            && Expect(!run.Stderr.Contains("exited 1: 'returns'", StringComparison.Ordinal), $"return: {run.Stderr}")
            && Expect(run.Stderr.Contains("step exited 1: end { Write-Error 'end-block-fails' }", StringComparison.Ordinal), $"a failing end block: {run.Stderr}")
            && Expect(run.Text.Contains("\nbegin-block\nend-block\n", StringComparison.Ordinal) && !run.Stderr.Contains("exited 1: begin {", StringComparison.Ordinal), $"named blocks: {Show(run.Text)}\n{run.Stderr}");

    // scenetake draws the prompt of bash, zsh, and PowerShell only, so in cmd and the sh family shell-prompt warns that
    // settings.prompt shows.
    internal static bool IntegrationShellPromptWarnsInOtherShells(string repoRoot)
    {
        if (!TryGetBinary(out var bin))
            return true;
        var shell = OperatingSystem.IsWindows() ? "cmd" : "sh";
        if (!IsOnPath(shell))
        {
            Skip.Test($"{shell} is not on PATH");
            return true;
        }

        var fixture = Path.Combine(Path.GetTempPath(), $"scenetake-shell-prompt-{shell}-{Guid.NewGuid():N}.yaml");
        File.WriteAllText(fixture, $"shell: {shell}\nsettings:\n  prompt: \"tpl $ \"\n  shell-prompt: true\n  typing-speed: 0.001\n  typing-jitter: 0\nsteps:\n  - echo one\n");
        try
        {
            var run = RunFixture(bin, repoRoot, fixture);
            return Expect(run.ExitCode == 0, $"exit {run.ExitCode}\n{run.Stderr}")
                && Expect(run.Stderr.Contains($"Warning: settings.shell-prompt draws the prompt of bash, zsh, and PowerShell: in {shell} the steps show settings.prompt.", StringComparison.Ordinal), run.Stderr)
                && Expect(run.Text.Contains("tpl $ echo one", StringComparison.Ordinal), Show(run.Text));
        }
        finally
        {
            File.Delete(fixture);
        }
    }

    // What shell-state-pwsh-errors.yaml shows in either PowerShell. [int]::Parse('x') stops only its own statement, so
    // the step goes on and exits 0, as it does alone. using namespace is valid only where a script begins.
    private static bool PowerShellErrorsShownAsAlone((int ExitCode, string Stderr, string Text) run)
    {
        var text = run.Text;
        return Expect(run.ExitCode == 0, $"exit {run.ExitCode}\n{run.Stderr}")
            && Expect(!text.Contains("__scenetake", StringComparison.Ordinal) && !text.Contains("scenetake-state", StringComparison.Ordinal), $"the wrapper shows: {Show(text)}")
            && Expect(text.Contains("after-statement-error", StringComparison.Ordinal) && !run.Stderr.Contains("exited 1: [int]::Parse", StringComparison.Ordinal), $"the step after the statement's error: {Show(text)}\n{run.Stderr}")
            && Expect(text.Contains("using-works", StringComparison.Ordinal), $"using namespace: {Show(text)}");
    }

    // Windows runs the same state scripts in Windows PowerShell when pwsh is not installed. The fixture is a pwsh one,
    // run from a copy that names powershell. Null when the fixture names no shell to replace.
    private static (int ExitCode, string Stderr, string Text)? RunInWindowsPowerShell(string bin, string repoRoot, string fixtureName, Dictionary<string, string?>? environment)
    {
        var pwsh = File.ReadAllText(Path.Combine(repoRoot, "tests", "fixtures", fixtureName));
        if (!Expect(pwsh.Contains("shell: pwsh", StringComparison.Ordinal), $"{fixtureName} names no shell"))
            return null;

        var fixture = Path.Combine(Path.GetTempPath(), $"scenetake-shell-state-powershell-{Guid.NewGuid():N}.yaml");
        File.WriteAllText(fixture, pwsh.Replace("shell: pwsh", "shell: powershell", StringComparison.Ordinal));
        try
        {
            return RunFixture(bin, repoRoot, fixture, environment);
        }
        finally
        {
            File.Delete(fixture);
        }
    }

    // Records a fixture with --save-outputs, then replays the outputs file. Text is the cast's output with its escapes.
    private static (bool Ran, string Stderr, string Text, string Outputs, bool ReplaySame) RecordAndReplay(string bin, string repoRoot, string fixtureName)
    {
        var stem = Path.Combine(Path.GetTempPath(), $"scenetake-shell-prompt-{Guid.NewGuid():N}");
        var outputs = stem + ".outputs.json";
        try
        {
            var fixture = Path.Combine("tests", "fixtures", fixtureName);
            var (recordExit, recordStderr) = RunBinary(bin, repoRoot, ["--save-outputs", outputs, fixture, stem]);
            var recorded = File.Exists(stem + ".cast") ? File.ReadAllBytes(stem + ".cast") : [];
            var (replayExit, replayStderr) = RunBinary(bin, repoRoot, ["--replay", outputs, fixture, stem + "-replay"]);
            var replayed = File.Exists(stem + "-replay.cast") ? File.ReadAllBytes(stem + "-replay.cast") : [];
            return (recordExit == 0 && replayExit == 0, $"{recordStderr}\n{replayStderr}", CastOutput(Encoding.UTF8.GetString(recorded)),
                File.Exists(outputs) ? File.ReadAllText(outputs) : "", recorded.Length > 0 && recorded.AsSpan().SequenceEqual(replayed));
        }
        finally
        {
            foreach (var file in new[] { stem + ".cast", stem + "-replay.cast", outputs })
                File.Delete(file);
        }
    }

    // A bash older than 4.4, macOS's /bin/bash, cannot expand PS1: it says so once, and settings.prompt shows.
    internal static bool IntegrationBashShellPrompt(string repoRoot)
    {
        if (!TryGetBinary(out var bin))
            return true;

        var run = RecordAndReplay(bin, repoRoot, "shell-state-bash-shell-prompt.yaml");
        var text = run.Text;
        if (!Expect(run.Ran && run.ReplaySame, $"the record or the replay failed, or the replay drew other prompts:\n{run.Stderr}")
            || !Expect(!text.Contains("__scenetake", StringComparison.Ordinal), $"scenetake's own names show: {Show(text)}"))
            return false;

        // The first step prints the version of the bash that scenetake ran, which may not be the one on this PATH.
        var version = Regex.Match(text, @"first bash=(\d+)\.(\d+)");
        if (!Expect(version.Success, $"the first step's version: {Show(text)}")
            || !Expect(!run.Stderr.Contains("draws the prompt of", StringComparison.Ordinal), $"bash was taken for a shell that draws no prompt: {run.Stderr}"))
            return false;
        var bashVersion = int.Parse(version.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) * 100 + int.Parse(version.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
        var warned = Regex.Matches(run.Stderr, "draws PS1 with bash 4.4 or later").Count;
        if (bashVersion < 404)
        {
            return Expect(warned == 1, $"the warning, once: {run.Stderr}")
                && Expect(text.Contains("tpl $ echo fourth", StringComparison.Ordinal) && text.EndsWith("tpl $ ", StringComparison.Ordinal), Show(text));
        }

        var masked = new string('*', "masked-word".Length);
        string Prompt(string folder, int status) => $"\u001b[32m{folder}\u001b[0m [{status}] {masked} > ";
        return Expect(warned == 0, $"a bash {version.Value} that draws PS1 warned: {run.Stderr}")
            && Expect(text.Contains("tpl $ echo \"first", StringComparison.Ordinal), $"the prompt before PS1 is set: {Show(text)}")
            && Expect(text.Contains("tpl $ echo title-hook", StringComparison.Ordinal), $"a PROMPT_COMMAND that sets no PS1 drew a prompt: {Show(text)}")
            && Expect(text.Contains(Prompt("tests", 0) + "echo second", StringComparison.Ordinal), $"the PS1 of a show: none step: {Show(text)}")
            && Expect(text.Contains(Prompt("fixtures", 1) + "echo third", StringComparison.Ordinal), $"the folder and $? of a failed step: {Show(text)}")
            // The typed command joins "tra""ced", so only bash's trace writes "+ echo traced".
            && Expect(text.Contains(Prompt("fixtures", 0) + "set -x;", StringComparison.Ordinal) && text.Contains("+ echo traced", StringComparison.Ordinal), $"the set -x step: {Show(text)}")
            && Expect(text.Contains(Prompt("fixtures", 0) + "n=0;", StringComparison.Ordinal), $"a step stopped by timeout changed the prompt: {Show(text)}")
            && Expect(run.Outputs.Contains($"{masked} > \"", StringComparison.Ordinal) && !run.Outputs.Contains("masked-word", StringComparison.Ordinal), $"the outputs file: {run.Outputs}")
            && (bashVersion >= 501 ? PromptCommandArrayDrawn() : OnlyFirstElementRan());

        // From bash 5.1 on, the array's second element sets PS1 from the command's $? and counts the prompts.
        bool PromptCommandArrayDrawn() =>
            Expect(text.Contains("pc1 [0] > false", StringComparison.Ordinal) && text.Contains("pc2 [1] > echo fourth", StringComparison.Ordinal) && text.EndsWith("pc3 [0] > ", StringComparison.Ordinal),
                $"a PROMPT_COMMAND array, $? in its second element, and what it changed: {Show(text)}")
            && Expect(run.Outputs.Contains("\"endPrompt\": \"pc3 [0] > \"", StringComparison.Ordinal), $"the closing prompt in the outputs file: {run.Outputs}");

        // Before 5.1, bash runs only the first element, true, so the PS1 set before stays.
        bool OnlyFirstElementRan() =>
            Expect(text.Contains(Prompt("fixtures", 1) + "echo fourth", StringComparison.Ordinal) && !text.Contains("pc1", StringComparison.Ordinal), $"bash {version.Value} ran more than the first element: {Show(text)}");
    }

    // HOME is a folder whose .bash_profile makes PROMPT_COMMAND or PS1 readonly, as some audit setups do, so clearing
    // the login files' prompt fails there, and must say nothing into the recording.
    internal static bool IntegrationBashReadonlyLoginPrompt(string repoRoot)
    {
        if (!TryGetBinary(out var bin))
            return true;

        // With set -e from the login files too, a failed clear would end the shell before each command. The readonly hook
        // still runs at each command, so hooked counts the two commands before it. A hook that runs only once PS1
        // changes gives 1.
        var hook = RunWithLoginFile(bin, repoRoot, ".bash_profile", "set -e\nreadonly PROMPT_COMMAND='hooked=$((hooked + 1))'\n", "shell: bash", "  - run: PS1='mine > '\n    show: none\n  - echo \"hooked=$hooked\"\n");
        // A readonly PS1 cannot be cleared, so the login files' prompt shows from the first command on, and a step's
        // PROMPT_COMMAND still runs.
        var ps1 = RunWithLoginFile(bin, repoRoot, ".bash_profile", "readonly PS1='ro> '\n", "shell: bash", "  - echo again\n  - run: PROMPT_COMMAND='cnt=$((cnt + 1))'\n    show: none\n  - echo \"cnt=$cnt\"\n");
        var old = hook.Stderr.Contains("draws PS1 with bash 4.4", StringComparison.Ordinal);
        return Expect(hook.ExitCode == 0 && !hook.Stderr.Contains("step exited", StringComparison.Ordinal), $"exit {hook.ExitCode}\n{hook.Stderr}")
            && Expect(!hook.Text.Contains("readonly", StringComparison.Ordinal) && !hook.Text.Contains("scenetake-state", StringComparison.Ordinal), $"the failed clear shows: {Show(hook.Text)}")
            && Expect(hook.Text.Contains("tpl $ echo one\none\n", StringComparison.Ordinal) && (old || (hook.Text.Contains("mine > echo two\ntwo\n", StringComparison.Ordinal) && hook.Text.Contains("hooked=2\n", StringComparison.Ordinal))), Show(hook.Text))
            && Expect(ps1.ExitCode == 0 && ((ps1.Text.Contains("ro> echo again", StringComparison.Ordinal) && ps1.Text.Contains("cnt=1\n", StringComparison.Ordinal)) || ps1.Stderr.Contains("draws PS1 with bash 4.4", StringComparison.Ordinal)), $"a readonly PS1 from the login files: {Show(ps1.Text)}\n{ps1.Stderr}");
    }

    // zsh stops reading a startup file at an assignment to a readonly parameter. A readonly PS1 must not stop the state.
    internal static bool IntegrationZshReadonlyLoginPrompt(string repoRoot)
    {
        if (!TryGetBinary(out var bin))
            return true;
        if (OperatingSystem.IsWindows() || !IsOnPath("zsh"))
        {
            Skip.Test("zsh is not on PATH");
            return true;
        }

        // PS1, PROMPT, and prompt share one value, each readonly on its own, so the login files' prompt shows at `echo
        // again`, and a step still sets the shared value through another name. The readonly one must then not be
        // restored, or zsh stops reading the restore and nothing carries.
        const string Carry = "  - cd /\n  - export CARRIED=yes\n  - echo \"at=$PWD carried=$CARRIED\"\n";
        var ps1 = RunWithLoginFile(bin, repoRoot, ".zshenv", "readonly PS1='ro%# '\n", "shell: zsh", "  - echo again\n  - run: PROMPT='mine > '\n    show: none\n" + Carry);
        var upper = RunWithLoginFile(bin, repoRoot, ".zshenv", "readonly PROMPT='ro%# '\n", "shell: zsh", "  - echo again\n  - run: PS1='mine > '\n    show: none\n" + Carry);
        // A readonly precmd_functions cannot be emptied, and its hook runs at each command, so pcs counts the six before it.
        var lower = RunWithLoginFile(bin, repoRoot, ".zshenv", "readonly prompt='lo%# '\npc() { pcs=$(( pcs + 1 )) }\nprecmd_functions=(pc)\nreadonly precmd_functions\n", "shell: zsh",
            "  - echo again\n  - run: PS1='mine > '\n    show: none\n" + Carry + "  - echo \"pcs=$pcs\"\n");
        return Readonly(ps1, "ro", "a readonly PS1") && Readonly(upper, "ro", "a readonly PROMPT") && Readonly(lower, "lo", "a readonly prompt and precmd_functions")
            && Expect(lower.Text.Contains("pcs=6\n", StringComparison.Ordinal), $"the readonly hook did not run at each command: {Show(lower.Text)}");

        // %# is # for root and % otherwise.
        static bool Readonly((int ExitCode, string Stderr, string Text) run, string login, string what) =>
            Expect(run.ExitCode == 0 && !run.Stderr.Contains("read-only", StringComparison.Ordinal), $"{what}: exit {run.ExitCode}\n{run.Stderr}")
            && Expect(Regex.IsMatch(run.Text, $@"{login}[#%] echo again"), $"{what}, its prompt: {Show(run.Text)}")
            && Expect(run.Text.Contains("at=/ carried=yes", StringComparison.Ordinal) && run.Text.Contains("mine > echo two", StringComparison.Ordinal), $"{what}, the state: {Show(run.Text)}");
    }

    // Records `echo one`, then the given steps, then `echo two`, with shell-prompt on, in a HOME that holds one login file.
    private static (int ExitCode, string Stderr, string Text) RunWithLoginFile(string bin, string repoRoot, string loginFile, string login, string shellLine, string steps)
    {
        var home = Path.Combine(Path.GetTempPath(), $"scenetake-login-{Guid.NewGuid():N}");
        Directory.CreateDirectory(home);
        try
        {
            File.WriteAllText(Path.Combine(home, loginFile), login);
            var fixture = Path.Combine(home, "scenario.yaml");
            File.WriteAllText(fixture, $"{shellLine}\nsettings:\n  prompt: \"tpl $ \"\n  shell-prompt: true\n  typing-speed: 0.001\n  typing-jitter: 0\nsteps:\n  - echo one\n{steps}  - echo two\n");
            return RunFixture(bin, repoRoot, fixture, new() { ["HOME"] = home, ["ZDOTDIR"] = null });
        }
        finally
        {
            Directory.Delete(home, recursive: true);
        }
    }

    internal static bool IntegrationZshShellPrompt(string repoRoot)
    {
        if (!TryGetBinary(out var bin))
            return true;
        if (OperatingSystem.IsWindows() || !IsOnPath("zsh"))
        {
            Skip.Test("zsh is not on PATH");
            return true;
        }

        var run = RecordAndReplay(bin, repoRoot, "shell-state-zsh-shell-prompt.yaml");
        var text = run.Text;
        return Expect(run.Ran && run.ReplaySame, $"the record or the replay failed, or the replay drew other prompts:\n{run.Stderr}")
            && Expect(text.Contains("tpl $ echo first", StringComparison.Ordinal), $"the prompt before PS1 is set: {Show(text)}")
            && Expect(text.Contains("\u001b[32mtests\u001b[39m [0] > echo second", StringComparison.Ordinal), $"the PS1 of a show: none step: {Show(text)}")
            && Expect(text.Contains("\u001b[32mfixtures\u001b[39m [1] > echo third", StringComparison.Ordinal), $"the folder and $? of a failed step: {Show(text)}")
            && Expect(text.Contains("fixtures (main) > echo fourth", StringComparison.Ordinal), $"prompt_subst in the shell that set it: {Show(text)}")
            && Expect(text.Contains("fixtures (main) > echo fifth", StringComparison.Ordinal), $"prompt_subst in the next shell: {Show(text)}")
            && Expect(text.Contains("st=3 > echo sixth", StringComparison.Ordinal) && text.EndsWith("st=0 > ", StringComparison.Ordinal), $"precmd and $?: {Show(text)}")
            && Expect(!run.Stderr.Contains("can't change type", StringComparison.Ordinal), $"a table of zsh's was restored: {run.Stderr}")
            && Expect(!text.Contains("__scenetake", StringComparison.Ordinal), $"scenetake's own names show: {Show(text)}");
    }

    internal static bool IntegrationPowerShellShellPrompt(string repoRoot)
    {
        if (!TryGetBinary(out var bin))
            return true;
        if (!IsOnPath("pwsh"))
        {
            Skip.Test("pwsh is not on PATH");
            return true;
        }

        var run = RecordAndReplay(bin, repoRoot, "shell-state-pwsh-shell-prompt.yaml");
        return Expect(run.Ran && run.ReplaySame, $"the record or the replay failed, or the replay drew other prompts:\n{run.Stderr}")
            && Expect(run.Text.Contains("\u001b[92m[tests]\u001b[0m ok=True > Write-Output second", StringComparison.Ordinal), $"Write-Host's color: {Show(run.Text)}")
            && PowerShellPromptShown(StripEscapes(run.Text), run.Stderr);
    }

    internal static bool IntegrationWindowsPowerShellShellPrompt(string repoRoot)
    {
        if (!TryGetBinary(out var bin))
            return true;
        if (!OperatingSystem.IsWindows() || !IsOnPath("powershell"))
        {
            Skip.Test("Windows PowerShell is not on PATH");
            return true;
        }

        return RunInWindowsPowerShell(bin, repoRoot, "shell-state-pwsh-shell-prompt.yaml", null) is { } run
            && Expect(run.ExitCode == 0, $"exit {run.ExitCode}\n{run.Stderr}")
            && PowerShellPromptShown(run.Text, run.Stderr);
    }

    // What shell-state-pwsh-shell-prompt.yaml shows in either PowerShell, without escapes.
    private static bool PowerShellPromptShown(string text, string stderr) =>
        Expect(text.Contains("tpl > Write-Output first", StringComparison.Ordinal), $"the prompt before the prompt function is set: {Show(text)}")
            && Expect(text.Contains("[tests] ok=True > Write-Output second", StringComparison.Ordinal), $"the prompt function of a show: none step: {Show(text)}")
            && Expect(text.Contains("[fixtures] ok=False > Write-Output third", StringComparison.Ordinal), $"the location and $? of a failed step: {Show(text)}")
            && Expect(text.Contains("tpl > Write-Output fourth", StringComparison.Ordinal) && text.TrimEnd().EndsWith("tpl >", StringComparison.Ordinal), $"a prompt function that throws: {Show(text)}")
            && Expect(Regex.Matches(stderr, "Warning: the prompt function failed, and the steps show settings.prompt: broken prompt").Count == 1, $"the warning, once: {stderr}")
            && Expect(!text.Contains("__scenetake", StringComparison.Ordinal), $"scenetake's own names show: {Show(text)}");

    // SCENETAKE_TEST_INHERITED is in the shell's environment from the start, so only the removed-variable list of the
    // state keeps it away from the next step. DEMO, which a step set, is simply left out of the state.
    private static Dictionary<string, string?> PowerShellStateEnvironment() => new() { ["SCENETAKE_TEST_INHERITED"] = "from-scenetake" };

    // What shell-state-pwsh.yaml prints when each step gets the state of the steps before it.
    private static bool PowerShellStateCarried((int ExitCode, string Stderr, string Text) run)
    {
        var text = run.Text;
        return Expect(run.ExitCode == 0, $"exit {run.ExitCode}\n{run.Stderr}")
            && Expect(text.Contains("start=tests", StringComparison.Ordinal), $"pre's location: {Show(text)}")
            && Expect(text.Contains("now=fixtures", StringComparison.Ordinal), "a step's location")
            && Expect(text.Contains("demo=carried plain=plain-value pre=pre-value", StringComparison.Ordinal), "variables")
            && Expect(text.Contains("hello alias", StringComparison.Ordinal), "the alias and the function")
            && Expect(text.Contains("demo=[]", StringComparison.Ordinal), "a removed variable")
            && Expect(text.Contains("inherited=from-scenetake", StringComparison.Ordinal), "an inherited variable")
            && Expect(text.Contains("inherited=[] False", StringComparison.Ordinal), "a removed inherited variable")
            && Expect(text.Contains("pref=Stop", StringComparison.Ordinal), "a preference variable")
            && Expect(run.Stderr.Contains("step exited 7: exit 7", StringComparison.Ordinal), "the exit code")
            && Expect(text.Contains("after-exit=plain-value", StringComparison.Ordinal), "exit dropped the state");
    }

    // Both names are in the shell's environment from the start, so a removal reaches the next step only through the
    // state's list of removed names. A table that matches names in any case keeps one of the two, so the steps that
    // remove both, or set both, fail on every run.
    internal static bool IntegrationPowerShellKeepsNamesThatDifferInCase(string repoRoot)
    {
        if (!TryGetBinary(out var bin))
            return true;
        if (OperatingSystem.IsWindows() || !IsOnPath("pwsh"))
        {
            Skip.Test("Linux and macOS only, with pwsh on PATH: Windows has one variable for both names");
            return true;
        }

        var run = RunFixture(bin, repoRoot, "shell-state-pwsh-env-case.yaml", new() { ["sct_case"] = "lower", ["SCT_CASE"] = "upper" });
        var text = run.Text;
        return Expect(run.ExitCode == 0, $"exit {run.ExitCode}\n{run.Stderr}")
            && Expect(text.Contains("start lower=[lower] upper=[upper]", StringComparison.Ordinal), $"the inherited names: {Show(text)}")
            && Expect(text.Contains("one-removed lower=[] True upper=[upper]", StringComparison.Ordinal), $"one name removed: {Show(text)}")
            && Expect(text.Contains("both-removed lower=True upper=True", StringComparison.Ordinal), $"both names removed: {Show(text)}")
            && Expect(text.Contains("upper-set lower=True upper=[changed]", StringComparison.Ordinal), $"one name set again: {Show(text)}")
            && Expect(text.Contains("new lower=[one] upper=[two]", StringComparison.Ordinal), $"both names set in one step: {Show(text)}");
    }

    // HOME is a folder of login files that move ZDOTDIR and reorder PATH, as many users' files and macOS's path_helper do.
    internal static bool IntegrationZshCarriesState(string repoRoot)
    {
        if (!TryGetBinary(out var bin))
            return true;
        if (OperatingSystem.IsWindows() || !IsOnPath("zsh"))
        {
            Skip.Test("zsh is not on PATH");
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
                && Expect(text.Contains($"child=[0][][carried][{home}/zdot]", StringComparison.Ordinal), $"a child zsh saw more than the exported variables, or another ZDOTDIR: {Show(text)}")
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
    internal static bool IntegrationShCarriesState(string repoRoot)
    {
        if (!TryGetBinary(out var bin))
            return true;
        if (!IsOnPath("sh"))
        {
            Skip.Test("sh is not on PATH");
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

    internal static bool IntegrationHiddenStepFailureStops(string repoRoot)
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
    internal static string? ErrorLine(string text, string command)
    {
        var match = Regex.Match(text, "^(.*)" + command + ":", RegexOptions.Multiline);
        return match.Success ? Regex.Match(match.Groups[1].Value, @"(\d+)\D*$").Groups[1].Value : null;
    }

    internal static bool TryGetBinary(out string bin)
    {
        bin = TestEnv.Binary;
        return true;
    }

    internal static bool IsOnPath(string name)
    {
        foreach (var folder in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            if (File.Exists(Path.Combine(folder, name)) || File.Exists(Path.Combine(folder, name + ".exe")))
                return true;
        }

        return false;
    }

    // environment: variables to set for scenetake, a null value removes one.
    internal static (int ExitCode, string Stderr, string Text) RunFixture(string bin, string repoRoot, string fixture, Dictionary<string, string?>? environment = null)
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
    internal static string CastOutput(string cast)
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

    // The last output event of an asciicast v3 cast: its interval from the event before it, and its text.
    internal static (double Interval, string Text) LastOutputEvent(string cast)
    {
        var last = (0.0, "");
        foreach (var line in cast.Split('\n').Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;
            using var json = JsonDocument.Parse(line);
            if (json.RootElement[1].GetString() == "o")
                last = (json.RootElement[0].GetDouble(), json.RootElement[2].GetString() ?? "");
        }

        return last;
    }

    internal static string StripEscapes(string text) =>
        Regex.Replace(text, @"\x1b(\[[0-9;?]*[ -/]*[@-~]|\][^\x07\x1b]*(\x07|\x1b\\)|[()][0-9A-Za-z]|[=>78])", "").Replace("\r", "");

    // A string with its control characters escaped, for messages.
    internal static string Show(string? text) =>
        text is null ? "null" : "\"" + string.Concat(text.Select(static c => char.IsControl(c) && c != '\n' ? $"\\u{(int)c:x4}" : c.ToString())) + "\"";

}
