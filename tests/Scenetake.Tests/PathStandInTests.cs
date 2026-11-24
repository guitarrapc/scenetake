using Scenetake.TestSupport;

namespace Scenetake.Tests;

// Paths that show as a stand-in (.github/docs/spec_scenario.md → Paths in the recording): the forms of a folder that
// the CLI collects, and a recording in bash through a published binary (SCENETAKE_BIN). The masker's rules are tested
// in Scenetake.Core.Tests (PathStandInTests).

public sealed class PathStandInTests
{
    [Test]
    public async Task FormsOfAFolder() => await Assert.That(PathStandInCliCases.FormsOfAFolder()).IsTrue();

    [Test]
    public async Task IntegrationRecordingShowsStandIns() => await Assert.That(PathStandInCliCases.IntegrationRecordingShowsStandIns(TestEnv.Binary)).IsTrue();
}

internal static class PathStandInCliCases
{
    internal static bool FormsOfAFolder()
    {
        var profile = Path.GetFullPath(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        var ok = Check(FolderPaths.Forms(profile) is [var first, ..] && SameText(first, profile), "the folder itself comes first")
            & Check(FolderPaths.Forms("relative/x").Count == 0 && FolderPaths.Forms(null).Count == 0 && FolderPaths.Forms(" ").Count == 0, "no forms for a relative or empty path")
            & Check(PersonalDetails.HomeFolders().Exists(home => SameText(home, profile)), "the home folders hold the profile");

        if (OperatingSystem.IsWindows())
        {
            // Git Bash writes HOME as /c/Users/name.
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
            var (exit, stderr) = Mp4FfmpegCases.Exec(scenetake, folder, ["demo.yaml"]);
            var cast = exit == 0 ? File.ReadAllText(Path.Combine(folder, "demo.cast")) : "";
            var ok = Check(exit == 0, $"exit {exit}{Environment.NewLine}{stderr}")
                && Check(cast.Contains("\"~/my-app\\r\\n\"", StringComparison.Ordinal), "pwd shows cwd-as")
                && Check(cast.Contains("\"my-app $ \"", StringComparison.Ordinal), "the prompt's {dir} at cwd")
                && Check(cast.Contains("\"~/notes\\r\\n\"", StringComparison.Ordinal), "the home folder shows as ~")
                && Check(stderr.Contains("Masked personal details: home folder as ~", StringComparison.Ordinal), $"the summary line{Environment.NewLine}{stderr}");

            if (ok && OperatingSystem.IsWindows() && Environment.GetEnvironmentVariable("TEMP") is { } temp)
            {
                // Under the profile, in its long or its 8.3 form, TEMP shows from ~ on.
                var home = PersonalDetails.HomeFolders().Find(form => temp.StartsWith(form.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase));
                if (home is not null)
                {
                    var shown = ("~" + temp[home.TrimEnd('\\').Length..]).Replace("\\", "\\\\", StringComparison.Ordinal);
                    ok = Check(cast.Contains($"\"{shown}\\r\\n\"", StringComparison.Ordinal), $"TEMP shows as {shown}");
                }
            }

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
