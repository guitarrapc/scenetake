using System.Diagnostics;
using Scenetake.TestSupport;

namespace Scenetake.Tests;

// settings.mask-personal (.github/docs/spec_scenario.md → Masking personal details). The masker's rules are tested in
// Scenetake.Core.Tests (SecretMaskTests). These check what the CLI collects and that a recording hides it. They need git
// on PATH, and the integration test a published binary (SCENETAKE_BIN). It records in the default shell: pwsh on
// Windows, $SHELL elsewhere.

public sealed class PersonalDetailsTests
{
    [Test]
    public async Task CollectGivesTheMachineNames() => await Assert.That(PersonalDetailsCases.CollectGivesTheMachineNames()).IsTrue();

    [Test]
    public async Task CollectReadsGlobalGitConfigOnly() => await Assert.That(PersonalDetailsCases.CollectReadsGlobalGitConfigOnly()).IsTrue();

    [Test]
    public async Task IntegrationRecordingAndReplayHideTheGitIdentity() => await Assert.That(PersonalDetailsCases.IntegrationRecordingAndReplayHideTheGitIdentity()).IsTrue();
}

internal static class PersonalDetailsCases
{
    internal static bool Check(bool passed, string what)
    {
        if (!passed)
            Console.Error.WriteLine($"  failed: {what}");
        return passed;
    }

    private static bool Has(List<PersonalDetail> details, string value, string kind) =>
        details.Exists(detail => detail.Kind == kind && string.Equals(detail.Value, value, StringComparison.OrdinalIgnoreCase));

    internal static bool CollectGivesTheMachineNames()
    {
        var details = PersonalDetails.Collect(null);
        var home = Path.GetFileName(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile).TrimEnd('/', '\\'));
        return Check(Has(details, Environment.UserName, PersonalDetail.UserName), "the user name")
            & Check(Has(details, home, PersonalDetail.HomeFolderName), "the home folder name")
            & Check(Has(details, Environment.MachineName, PersonalDetail.ComputerName), "the computer name");
    }

    // The global config, with a conditional include for the repository's folder, gives the name and both emails. The
    // repository's own identity is left out: a demo repository may set one on purpose.
    internal static bool CollectReadsGlobalGitConfigOnly()
    {
        TestEnv.RequireOnPath("git");
        var folder = NewFolder("git-scope");
        var repo = Directory.CreateDirectory(Path.Combine(folder, "repo")).FullName;
        Git(repo, "init", "-q");
        Git(repo, "config", "user.name", "Local Demo");
        // git's own spelling of the folder: a temp path can be a short 8.3 name on Windows, which includeIf would not match.
        var top = Git(repo, "rev-parse", "--show-toplevel").Trim();

        var work = Path.Combine(folder, "work.gitconfig");
        File.WriteAllText(work, "[user]\n\temail = work@example.com\n");
        var global = Path.Combine(folder, "global.gitconfig");
        File.WriteAllText(global, $"[user]\n\tname = Global Person\n\temail = global@example.com\n[includeIf \"gitdir:{top}/\"]\n\tpath = {work.Replace('\\', '/')}\n");

        var saved = new Dictionary<string, string?>();
        foreach (var name in new[] { "GIT_CONFIG_GLOBAL", "GIT_CONFIG_NOSYSTEM", "GIT_CONFIG_COUNT", "GIT_CONFIG_KEY_0", "GIT_CONFIG_VALUE_0" })
            saved[name] = Environment.GetEnvironmentVariable(name);
        try
        {
            Environment.SetEnvironmentVariable("GIT_CONFIG_GLOBAL", global);
            Environment.SetEnvironmentVariable("GIT_CONFIG_NOSYSTEM", "1");
            var inRepo = PersonalDetails.Collect(repo);
            var outside = PersonalDetails.Collect(folder);
            // An identity that the environment gives git (the command scope) reaches every step, as the global one does.
            Environment.SetEnvironmentVariable("GIT_CONFIG_COUNT", "1");
            Environment.SetEnvironmentVariable("GIT_CONFIG_KEY_0", "user.email");
            Environment.SetEnvironmentVariable("GIT_CONFIG_VALUE_0", "ci@example.com");
            var command = PersonalDetails.Collect(folder);
            return Check(Has(inRepo, "Global Person", PersonalDetail.GitName), "the global name")
                & Check(Has(inRepo, "global@example.com", PersonalDetail.GitEmail), "the global email")
                & Check(Has(inRepo, "work@example.com", PersonalDetail.GitEmail), "the email the repository's folder includes")
                & Check(!inRepo.Exists(static detail => detail.Value == "Local Demo"), "no repository identity")
                & Check(!Has(outside, "work@example.com", PersonalDetail.GitEmail), "no included email outside that folder")
                & Check(Has(command, "ci@example.com", PersonalDetail.GitEmail), "the email of the command scope");
        }
        finally
        {
            foreach (var (name, value) in saved)
                Environment.SetEnvironmentVariable(name, value);
        }
    }

    // git var prints the identity a commit would take, the same way in every shell. The environment gives it, so the
    // test does not depend on the machine's git config, and the CLI reads the same variables as personal details.
    internal static bool IntegrationRecordingAndReplayHideTheGitIdentity()
    {
        var bin = TestEnv.Binary;
        TestEnv.RequireOnPath("git");
        var folder = NewFolder("integration");
        const string Steps = "steps:\n  - git var GIT_AUTHOR_IDENT\n";
        File.WriteAllText(Path.Combine(folder, "on.yaml"), Steps);
        File.WriteAllText(Path.Combine(folder, "off.yaml"), "settings:\n  mask-personal: false\n" + Steps);
        var env = new Dictionary<string, string>
        {
            ["GIT_AUTHOR_NAME"] = "Ada Lovelace",
            ["GIT_AUTHOR_EMAIL"] = "ada@example.com",
            ["GIT_AUTHOR_DATE"] = "2026-01-02T03:04:05Z",
        };
        const string Line = "personal details: git name, git email (settings.mask-personal: false shows them)";

        var (onExit, onErr) = Exec(bin, folder, ["on.yaml"], env);
        var on = File.ReadAllText(Path.Combine(folder, "on.cast"));
        var (offExit, offErr) = Exec(bin, folder, ["--save-outputs", "off.outputs.json", "off.yaml"], env);
        var off = File.ReadAllText(Path.Combine(folder, "off.cast"));
        // The outputs file holds the identity, as the recording showed it. A replay with the setting on hides it again.
        var (replayExit, replayErr) = Exec(bin, folder, ["--replay", "off.outputs.json", "on.yaml", "replayed"], env);
        var replayed = File.ReadAllText(Path.Combine(folder, "replayed.cast"));

        var ok = Check(onExit == 0 && offExit == 0 && replayExit == 0, $"exit {onExit}, {offExit}, {replayExit}")
            && Check(!on.Contains("Ada Lovelace", StringComparison.Ordinal) && !on.Contains("ada@example.com", StringComparison.Ordinal), "on: hidden")
            && Check(on.Contains("************ <***************>", StringComparison.Ordinal), "on: stars of the same width")
            && Check(onErr.Contains(Line, StringComparison.Ordinal), "on: the summary line")
            && Check(off.Contains("Ada Lovelace <ada@example.com>", StringComparison.Ordinal), "off: shown")
            && Check(!offErr.Contains("personal details", StringComparison.Ordinal), "off: no summary line")
            && Check(!replayed.Contains("Ada Lovelace", StringComparison.Ordinal) && replayErr.Contains(Line, StringComparison.Ordinal), "replay: hidden");
        if (!ok)
            Console.Error.WriteLine(onErr + offErr + replayErr);
        return ok;
    }

    private static string NewFolder(string name)
    {
        var folder = Path.Combine(Path.GetTempPath(), "scenetake-personal-test", name);
        if (Directory.Exists(folder))
            Directory.Delete(folder, recursive: true);
        return Directory.CreateDirectory(folder).FullName;
    }

    private static string Git(string folder, params string[] arguments)
    {
        var psi = new ProcessStartInfo("git") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, WorkingDirectory = folder };
        foreach (var argument in arguments)
            psi.ArgumentList.Add(argument);
        using var process = Process.Start(psi)!;
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"git {string.Join(' ', arguments)}: {process.StandardError.ReadToEnd()}");
        return output;
    }

    private static (int ExitCode, string Stderr) Exec(string bin, string folder, string[] arguments, Dictionary<string, string> env)
    {
        var psi = new ProcessStartInfo(bin) { RedirectStandardError = true, RedirectStandardOutput = true, UseShellExecute = false, WorkingDirectory = folder };
        foreach (var argument in arguments)
            psi.ArgumentList.Add(argument);
        psi.Environment.Remove("SCENETAKE_OUTPUTS");
        foreach (var (key, value) in env)
            psi.Environment[key] = value;
        using var process = Process.Start(psi)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(TimeSpan.FromSeconds(120)))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"{string.Join(' ', arguments)} ran for over 120 s");
        }

        _ = stdout.Result;
        return (process.ExitCode, stderr.Result.Replace("\r\n", "\n", StringComparison.Ordinal));
    }
}
