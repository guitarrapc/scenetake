using Scenetake.TestSupport;

namespace Scenetake.Tests;

// The path that shows as a stand-in (.github/docs/spec_scenario.md → Paths in the recording): the forms of a folder
// that the CLI collects, and a recording in bash through a published binary (SCENETAKE_BIN). The masker's rules are
// tested in Scenetake.Core.Tests (PathStandInTests).

public sealed class PathStandInTests
{
    [Test]
    public async Task FormsOfAFolder() => await Assert.That(PathStandInCliCases.FormsOfAFolder()).IsTrue();

    [Test]
    public async Task HomeFormsOfAFolder() => await Assert.That(PathStandInCliCases.HomeFormsOfAFolder()).IsTrue();

    [Test]
    public async Task IntegrationRecordingShowsStandIns() => await Assert.That(PathStandInCliCases.IntegrationRecordingShowsStandIns(TestEnv.Binary)).IsTrue();

    [Test]
    public async Task IntegrationHomeFormShowsTheStandIn() => await Assert.That(PathStandInCliCases.IntegrationHomeFormShowsTheStandIn(TestEnv.Binary)).IsTrue();
}

internal static class PathStandInCliCases
{
    internal static bool FormsOfAFolder()
    {
        var profile = Path.GetFullPath(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        var ok = Check(FolderPaths.Forms(profile) is [var first, ..] && SameText(first, profile), "the folder itself comes first")
            & Check(FolderPaths.Forms("relative/x").Count == 0 && FolderPaths.Forms(null).Count == 0 && FolderPaths.Forms(" ").Count == 0, "no forms for a relative or empty path");

        if (OperatingSystem.IsWindows())
        {
            // Git Bash writes a Windows folder as /c/Users/name.
            var gitBash = "/" + char.ToLowerInvariant(profile[0]) + profile[2..].Replace('\\', '/');
            return ok & Check(FolderPaths.Forms(gitBash).Exists(form => SameText(form, profile)), $"the Git Bash form {gitBash}");
        }

        // pwd prints the folder with its links resolved when the shell reads it from the system. On macOS the temp
        // folder is under a link itself (/var is /private/var).
        var folder = Directory.CreateTempSubdirectory("scenetake-forms-").FullName;
        try
        {
            Directory.CreateDirectory(Path.Combine(folder, "real"));
            var link = Path.Combine(folder, "link");
            Directory.CreateSymbolicLink(link, "real");
            var forms = FolderPaths.Forms(link);
            return ok & Check(forms is [var given, var resolved] && given == link && resolved.EndsWith("/real", StringComparison.Ordinal) && Directory.Exists(resolved),
                $"a link and its folder: {string.Join(", ", forms)}");
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    // The forms from ~ that a prompt writes: one for a folder under a home folder, none for the home folder itself, whose
    // ~ stands for every path, and none for a folder outside it.
    internal static bool HomeFormsOfAFolder()
    {
        var home = Directory.CreateTempSubdirectory("scenetake-home-").FullName;
        var outside = Directory.CreateTempSubdirectory("scenetake-outside-").FullName;
        try
        {
            var demo = Path.Combine(home, "src", "demo");
            Directory.CreateDirectory(demo);
            var forms = FolderPaths.HomeForms(demo, [null, "", home, home + Path.DirectorySeparatorChar]);
            // A name that starts with two dots is a folder under home, not the folder above it.
            var dotted = Path.Combine(home, "..hidden", "demo");
            Directory.CreateDirectory(dotted);
            var dottedForms = FolderPaths.HomeForms(dotted, [home]);
            return Check(forms is ["~/src/demo"], $"under home: {string.Join(", ", forms)}")
                & Check(dottedForms is ["~/..hidden/demo"], $"a folder named ..hidden: {string.Join(", ", dottedForms)}")
                & Check(FolderPaths.HomeForms(home, [home]).Count == 0, "the home folder")
                & Check(FolderPaths.HomeForms(outside, [home]).Count == 0, "a folder outside home")
                & Check(FolderPaths.HomeForms(demo, [outside]).Count == 0, "another home");
        }
        finally
        {
            Directory.Delete(home, recursive: true);
            Directory.Delete(outside, recursive: true);
        }
    }

    internal static bool IntegrationRecordingShowsStandIns(string scenetake)
    {
        var folder = Directory.CreateTempSubdirectory("scenetake-paths-").FullName;
        try
        {
            Directory.CreateDirectory(Path.Combine(folder, "demo"));
            // cmd's echo prints TEMP as Windows sets it, with the profile's 8.3 name when it has one.
            var windowsStep = OperatingSystem.IsWindows() ? "\n  - cmd //c echo %TEMP%" : "";
            File.WriteAllText(Path.Combine(folder, "demo.yaml"), """
                shell: bash
                cwd: ./demo
                cwd-as: ~/my-app
                settings:
                  prompt: "{dir} $ "
                  typing-speed: 0.01
                  typing-jitter: 0
                steps:
                  - pwd
                  - echo "$HOME/notes"
                """ + windowsStep + "\n");
            // No git identity, so no personal detail can star a part of the home folder's path.
            var noGit = Path.Combine(folder, "empty.gitconfig");
            File.WriteAllText(noGit, "");
            var environment = new Dictionary<string, string>
            {
                ["GIT_CONFIG_GLOBAL"] = noGit,
                ["GIT_CONFIG_NOSYSTEM"] = "1",
                ["GIT_AUTHOR_NAME"] = "",
                ["GIT_AUTHOR_EMAIL"] = "",
                ["GIT_COMMITTER_NAME"] = "",
                ["GIT_COMMITTER_EMAIL"] = "",
            };
            var (exit, stderr) = Mp4FfmpegCases.Exec(scenetake, folder, ["demo.yaml"], environment);
            var cast = exit == 0 ? File.ReadAllText(Path.Combine(folder, "demo.cast")) : "";
            // bash's HOME ends in the home folder's name, as Git Bash writes the profile too.
            var homeName = Path.GetFileName((Environment.GetEnvironmentVariable("HOME") ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)).TrimEnd('/', '\\'));
            var ok = Check(exit == 0, $"exit {exit}{Environment.NewLine}{stderr}")
                && Check(cast.Contains("\"~/my-app\\r\\n\"", StringComparison.Ordinal), "pwd shows cwd-as")
                && Check(cast.Contains("\"my-app $ \"", StringComparison.Ordinal), "the prompt's {dir} at cwd")
                && Check(cast.Contains($"/{homeName}/notes\\r\\n\"", StringComparison.Ordinal) && !cast.Contains("\"~/notes", StringComparison.Ordinal), $"the home folder shows as it is, ending in {homeName}")
                && Check(!stderr.Contains("Masked personal details", StringComparison.Ordinal), $"no summary line{Environment.NewLine}{stderr}");

            // TEMP shows as Windows sets it, in the profile's 8.3 form when it has one.
            if (ok && OperatingSystem.IsWindows() && Environment.GetEnvironmentVariable("TEMP") is { } temp)
                ok = Check(cast.Contains($"\"{temp.Replace("\\", "\\\\", StringComparison.Ordinal)}\\r\\n\"", StringComparison.Ordinal), $"TEMP shows as {temp}");

            if (!ok)
                Console.Error.WriteLine(cast);
            return ok;
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    // HOME is the test folder, so cwd is ~/demo there, and only the CLI's ~ forms can find `~/demo` in the typed command,
    // the output, and bash's \w prompt. Without them each shows ~/demo.
    internal static bool IntegrationHomeFormShowsTheStandIn(string scenetake)
    {
        var folder = Directory.CreateTempSubdirectory("scenetake-home-form-").FullName;
        try
        {
            Directory.CreateDirectory(Path.Combine(folder, "demo"));
            File.WriteAllText(Path.Combine(folder, "demo.yaml"), """
                shell: bash
                cwd: ./demo
                cwd-as: ~/my-app
                settings:
                  prompt: "$ "
                  shell-prompt: true
                  typing-speed: 0.01
                  typing-jitter: 0
                pre:
                  - PS1='[\w] '
                steps:
                  - echo '~/demo/notes'
                """ + "\n");
            var (exit, stderr) = Mp4FfmpegCases.Exec(scenetake, folder, ["demo.yaml"], new Dictionary<string, string> { ["HOME"] = folder });
            var cast = exit == 0 ? File.ReadAllText(Path.Combine(folder, "demo.cast")) : "";
            // bash before 4.4, as on macOS, draws no PS1 and warns.
            var ok = Check(exit == 0, $"exit {exit}{Environment.NewLine}{stderr}")
                && Check(cast.Contains("\"[~/my-app] \"", StringComparison.Ordinal) || stderr.Contains("draws PS1 with bash 4.4", StringComparison.Ordinal), "the prompt shows cwd-as")
                && Check(cast.Contains("\"~/my-app/notes\\r\\n\"", StringComparison.Ordinal), "the output shows cwd-as")
                && Check(!cast.Contains("~/demo", StringComparison.Ordinal), "~/demo shows");
            if (!ok)
                Console.Error.WriteLine(cast);
            return ok;
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    private static bool SameText(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private static bool Check(bool passed, string message)
    {
        if (!passed)
            Console.Error.WriteLine($"  {message}");
        return passed;
    }
}
