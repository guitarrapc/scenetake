using System.Diagnostics;
using Scenetake.TestSupport;

namespace Scenetake.Tests;

// Several scenarios in one run (.github/docs/spec_cli.md → Several scenarios). The unit tests need only the source.
// The integration tests record small scenarios with a published binary (SCENETAKE_BIN), in the default shell: pwsh on
// Windows, $SHELL elsewhere.

public sealed class BatchTests
{
    [Test]
    public async Task ScenarioArguments() => await Assert.That(BatchCases.ScenarioArguments()).IsTrue();

    [Test]
    public async Task ExpandOrderAndDuplicates() => await Assert.That(BatchCases.ExpandOrderAndDuplicates()).IsTrue();

    [Test]
    public async Task ExpandErrors() => await Assert.That(BatchCases.ExpandErrors()).IsTrue();

    [Test]
    public async Task DevicePathsHaveNoWildcard() => await Assert.That(BatchCases.DevicePathsHaveNoWildcard()).IsTrue();

    [Test]
    public async Task NamedFileIsNotExpandedAgain() => await Assert.That(BatchCases.NamedFileIsNotExpandedAgain()).IsTrue();

    [Test]
    public async Task UnreadableFolderIsAnError() => await Assert.That(BatchCases.UnreadableFolderIsAnError()).IsTrue();

    [Test]
    public async Task SharedOutputIsAnError() => await Assert.That(BatchCases.SharedOutputIsAnError()).IsTrue();

    [Test]
    public async Task ChildArgumentsDropTheOtherScenarios() => await Assert.That(BatchCases.ChildArgumentsDropTheOtherScenarios()).IsTrue();

    [Test]
    public async Task SummaryLines() => await Assert.That(BatchCases.SummaryLines()).IsTrue();

    [Test]
    public async Task IntegrationRecordsEachScenario() => await Assert.That(BatchCases.IntegrationRecordsEachScenario()).IsTrue();

    [Test]
    public async Task IntegrationTwoYamlFilesAreTwoScenarios() => await Assert.That(BatchCases.IntegrationTwoYamlFilesAreTwoScenarios()).IsTrue();

    [Test]
    public async Task IntegrationFailureDoesNotStopTheOthers() => await Assert.That(BatchCases.IntegrationFailureDoesNotStopTheOthers()).IsTrue();

    [Test]
    public async Task IntegrationRefusesBeforeRecording() => await Assert.That(BatchCases.IntegrationRefusesBeforeRecording()).IsTrue();

    [Test]
    public async Task IntegrationOneMatchIsAnOrdinaryRun() => await Assert.That(BatchCases.IntegrationOneMatchIsAnOrdinaryRun()).IsTrue();

    [Test]
    public async Task IntegrationDevicePaths() => await Assert.That(BatchCases.IntegrationDevicePaths()).IsTrue();
}

internal static class BatchCases
{

    internal static bool Check(bool passed, string what)
    {
        if (!passed)
            Console.Error.WriteLine($"  failed: {what}");
        return passed;
    }

    internal static bool ScenarioArguments()
    {
        var ok = true;
        foreach (var arg in new[] { "a.yaml", "docs/A.YML", "demos/*", "demo?.txt" })
            ok &= Check(ScenarioBatch.IsScenarioArgument(arg), $"{arg} is a scenario");
        foreach (var arg in new[] { "out", "out.cast", "out.svg", "yaml" })
            ok &= Check(!ScenarioBatch.IsScenarioArgument(arg), $"{arg} is an output name");
        return ok;
    }

    internal static bool ExpandOrderAndDuplicates()
    {
        var folder = NewFolder("expand");
        foreach (var name in new[] { "b.yaml", "a.yaml", "B2.yaml", ".hidden.yaml", "notes.txt" })
            File.WriteAllText(Path.Combine(folder, name), "");
        Directory.CreateDirectory(Path.Combine(folder, "sub"));
        File.WriteAllText(Path.Combine(folder, "sub", "c.yaml"), "");

        static string Names(List<string> paths) => string.Join(",", paths.Select(Path.GetFileName));
        var ok = true;

        // Ordinal order puts upper case first. A dot file needs a pattern that starts with a dot, as in a shell.
        ok &= Check(ScenarioBatch.TryExpand(["*.yaml"], folder, out var all, out _) && Names(all) == "B2.yaml,a.yaml,b.yaml", $"*.yaml: {Names(all)}");
        ok &= Check(ScenarioBatch.TryExpand([".*.yaml"], folder, out var hidden, out _) && Names(hidden) == ".hidden.yaml", $".*.yaml: {Names(hidden)}");
        // Given order first, each file once.
        ok &= Check(ScenarioBatch.TryExpand(["b.yaml", "*.yaml", "./b.yaml"], folder, out var given, out _) && Names(given) == "b.yaml,B2.yaml,a.yaml", $"given order: {Names(given)}");
        ok &= Check(ScenarioBatch.TryExpand(["sub/*.yaml", "?.yaml"], folder, out var sub, out _) && Names(sub) == "c.yaml,a.yaml,b.yaml", $"folder and ?: {Names(sub)}");
        ok &= Check(all.TrueForAll(Path.IsPathFullyQualified), "full paths");
        // A file named twice, once as a Windows device path, is recorded once.
        if (OperatingSystem.IsWindows())
            ok &= Check(ScenarioBatch.TryExpand(["a.yaml", @"\\?\" + Path.Combine(folder, "a.yaml")], folder, out var device, out _) && device.Count == 1, $"a device spelling: {device.Count}");
        // On Windows a name matches in any case, as the file system does.
        var caseOk = ScenarioBatch.TryExpand(["*.YAML"], folder, out var upper, out _);
        ok &= Check(OperatingSystem.IsWindows() ? caseOk && upper.Count == 3 : !caseOk, $"*.YAML: {caseOk} {Names(upper)}");
        return ok;
    }

    internal static bool ExpandErrors()
    {
        var folder = NewFolder("expand-errors");
        File.WriteAllText(Path.Combine(folder, "a.yaml"), "");
        Directory.CreateDirectory(Path.Combine(folder, "sub"));

        var ok = true;
        foreach (var (pattern, expected) in new[]
        {
        ("*.yml", "no file matches *.yml"),
        ("none/*.yaml", "no file matches none/*.yaml"),
        ("missing.yaml", "missing.yaml not found"),
        ("s*/a.yaml", "a wildcard works in the file name only, such as docs/*.yaml: s*/a.yaml"),
        ("**/*.yaml", "'**' does not look into folders below"),
    })
        {
            var expanded = ScenarioBatch.TryExpand(["a.yaml", pattern], folder, out _, out var error);
            ok &= Check(!expanded && error.Contains(expected, StringComparison.Ordinal), $"{pattern}: {error}");
        }

        return ok;
    }

    // Windows allows no ? in a name, so the ? of \\?\, \??\, and //?/ is the path's own. Elsewhere it may be a pattern.
    internal static bool DevicePathsHaveNoWildcard()
    {
        var ok = Check(ScenarioBatch.HasWildcard(@"\\?\C:\work\*.yaml"), @"\\?\ with *");
        ok &= Check(ScenarioBatch.HasWildcard(@"\\?\C:\work\a.yaml") == !OperatingSystem.IsWindows(), @"\\?\ alone");
        ok &= Check(ScenarioBatch.HasWildcard(@"\??\C:\work\a.yaml") == !OperatingSystem.IsWindows(), @"\??\ alone");
        // scenetake prints a device path with forward slashes, which Windows takes too.
        ok &= Check(ScenarioBatch.HasWildcard("//?/C:/work/a.yaml") == !OperatingSystem.IsWindows(), "//?/ alone");
        ok &= Check(!ScenarioBatch.IsScenarioArgument(OperatingSystem.IsWindows() ? @"\\?\C:\work\out" : "out"), "a device path output name");
        ok &= Check(!ScenarioBatch.IsScenarioArgument(OperatingSystem.IsWindows() ? "//?/C:/work/out" : "out"), "a forward-slash device path output name");
        return ok;
    }

    // Each scenario's own process gets the full path of a file the parent found. A name that holds ? matched itself and
    // ab.yaml before, so every process started the batch again without end. Only Unix allows such a name.
    internal static bool NamedFileIsNotExpandedAgain()
    {
        var folder = NewFolder("named");
        File.WriteAllText(Path.Combine(folder, "ab.yaml"), "");
        var ok = Check(ScenarioBatch.TryExpand([Path.Combine(folder, "ab.yaml")], folder, out var full, out _) && full.Count == 1, "a full path");
        if (OperatingSystem.IsWindows())
            return ok;

        File.WriteAllText(Path.Combine(folder, "a?.yaml"), "");
        ok &= Check(ScenarioBatch.TryExpand([Path.Combine(folder, "a?.yaml")], folder, out var named, out _) && named.Count == 1 && named[0].EndsWith("a?.yaml", StringComparison.Ordinal), $"a?.yaml: {string.Join(",", named)}");
        // What bash gives for *.yaml: both names, each recorded once.
        ok &= Check(ScenarioBatch.TryExpand(["a?.yaml", "ab.yaml"], folder, out var both, out _) && both.Count == 2, $"a?.yaml ab.yaml: {both.Count}");
        return ok;
    }

    internal static bool UnreadableFolderIsAnError()
    {
        string folder;
        if (OperatingSystem.IsWindows())
        {
            folder = @"C:\System Volume Information";
        }
        else
        {
            folder = NewFolder("unreadable");
            File.SetUnixFileMode(folder, UnixFileMode.None);
        }

        try
        {
            // An administrator or root may read it anyway, and then there is nothing to test.
            try
            {
                _ = Directory.EnumerateFiles(folder).Any();
                Skip.Test("the folder can be read");
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }

            var expanded = ScenarioBatch.TryExpand([Path.Combine(folder, "*.yaml")], Path.GetTempPath(), out _, out var error);
            return Check(!expanded && error.StartsWith("cannot list the folder of ", StringComparison.Ordinal), error);
        }
        finally
        {
            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(folder, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    internal static bool SharedOutputIsAnError()
    {
        static string Display(string path) => Path.GetFileName(path);
        var folder = NewFolder("shared");
        var ok = Check(ScenarioBatch.FindSharedOutput([Path.Combine(folder, "a.yaml"), Path.Combine(folder, "b.yaml")], Display) is null, "own outputs");
        var error = ScenarioBatch.FindSharedOutput([Path.Combine(folder, "b.yaml"), Path.Combine(folder, "demo.yaml"), Path.Combine(folder, "demo.yml")], Display);
        ok &= Check(error == "demo.yaml and demo.yml would both write demo.cast: rename one of them", $"demo.yaml demo.yml: {error}");
        // Case is ignored on every OS, as macOS volumes ignore it by default.
        var cased = ScenarioBatch.FindSharedOutput([Path.Combine(folder, "Demo.yaml"), Path.Combine(folder, "demo.yml")], Display);
        ok &= Check(cased is not null, $"case: {cased}");
        if (OperatingSystem.IsWindows())
        {
            // A device path names the same file as its plain spelling, and a trailing dot in a name stays: demo..yaml
            // writes demo..cast.
            var device = ScenarioBatch.FindSharedOutput([Path.Combine(folder, "demo.yaml"), @"\\?\" + Path.Combine(folder, "demo.yml")], Display);
            ok &= Check(device is not null, "a device spelling");
            var dotted = ScenarioBatch.FindSharedOutput([@"\\?\" + Path.Combine(folder, "demo..yaml"), @"\\?\" + Path.Combine(folder, "demo.yaml")], Display);
            ok &= Check(dotted is null, $"a trailing dot: {dotted}");
        }

        return ok;
    }

    internal static bool ChildArgumentsDropTheOtherScenarios()
    {
        string[] args = ["--format", "svg", "a.yaml", "--strict", "docs/*.yaml", "--theme=dracula"];
        var child = ScenarioBatch.ChildArguments(args, [2, 4], "/work/docs/b.yaml");
        return Check(string.Join(" ", child) == "--format svg --strict --theme=dracula /work/docs/b.yaml", string.Join(" ", child));
    }

    internal static bool SummaryLines()
    {
        static string Display(string path) => path;
        var ok = true;
        ok &= Check(ScenarioBatch.Summary(3, 3, [], [], false, Display) == "Recorded 3 scenarios.", "all");
        ok &= Check(ScenarioBatch.Summary(3, 1, ["a.yaml", "b.yaml"], [], false, Display) == "Recorded 1 of 3 scenarios. Failed: a.yaml, b.yaml", "failed");
        ok &= Check(ScenarioBatch.Summary(3, 1, [], ["b.yaml", "c.yaml"], true, Display) == "Stopped by Ctrl+C: recorded 1 of 3 scenarios. Not recorded: b.yaml, c.yaml", "stopped");
        return ok;
    }

    // A wildcard as PowerShell passes it on, unexpanded, records each match in ordinal order, each next to its YAML.
    internal static bool IntegrationRecordsEachScenario()
    {
        if (!TryGetBinary(out var bin))
            return true;

        var folder = NewFolder("records");
        var demos = Directory.CreateDirectory(Path.Combine(folder, "demos")).FullName;
        WriteScenario(Path.Combine(demos, "b.yaml"), "echo beta");
        WriteScenario(Path.Combine(demos, "a.yaml"), "echo alpha");
        WriteScenario(Path.Combine(demos, ".draft.yaml"), "echo draft");

        var (exit, stderr) = Exec(bin, folder, ["--format", "svg", "demos/*.yaml"]);
        var loadA = stderr.IndexOf("Loading: demos/a.yaml", StringComparison.Ordinal);
        var loadB = stderr.IndexOf("Loading: demos/b.yaml", StringComparison.Ordinal);
        var ok = Check(exit == 0, $"exit {exit}")
            && Check(loadA >= 0 && loadB > loadA, "a before b")
            && Check(stderr.TrimEnd().EndsWith("Recorded 2 scenarios.", StringComparison.Ordinal), "the last line")
            && Check(new[] { "a.cast", "a.svg", "b.cast", "b.svg" }.All(name => File.Exists(Path.Combine(demos, name))), "the files")
            && Check(!File.Exists(Path.Combine(demos, ".draft.cast")), "a dot file is left out")
            && Check(File.ReadAllText(Path.Combine(demos, "b.cast")).Contains("beta", StringComparison.Ordinal), "b.cast is b's");
        if (!ok)
            Console.Error.WriteLine(stderr);
        return ok;
    }

    // Before, the second .yaml was the output name: a.yaml was written to b.cast, over b's own recording.
    internal static bool IntegrationTwoYamlFilesAreTwoScenarios()
    {
        if (!TryGetBinary(out var bin))
            return true;

        var folder = NewFolder("two-yaml");
        WriteScenario(Path.Combine(folder, "a.yaml"), "echo alpha");
        WriteScenario(Path.Combine(folder, "b.yaml"), "echo beta");

        var (exit, stderr) = Exec(bin, folder, ["a.yaml", "b.yaml"]);
        var a = Path.Combine(folder, "a.cast");
        var b = Path.Combine(folder, "b.cast");
        var ok = Check(exit == 0, $"exit {exit}")
            && Check(File.Exists(a) && File.ReadAllText(a).Contains("alpha", StringComparison.Ordinal), "a.cast")
            && Check(File.Exists(b) && File.ReadAllText(b).Contains("beta", StringComparison.Ordinal) && !File.ReadAllText(b).Contains("alpha", StringComparison.Ordinal), "b.cast is b's");

        // One scenario still takes an output name.
        var (stemExit, stemStderr) = Exec(bin, folder, ["a.yaml", "named"]);
        ok &= Check(stemExit == 0 && File.Exists(Path.Combine(folder, "named.cast")), $"output name: exit {stemExit}");
        if (!ok)
            Console.Error.WriteLine(stderr + stemStderr);
        return ok;
    }

    internal static bool IntegrationFailureDoesNotStopTheOthers()
    {
        if (!TryGetBinary(out var bin))
            return true;

        var folder = NewFolder("failure");
        WriteScenario(Path.Combine(folder, "a-fails.yaml"), "exit 3");
        WriteScenario(Path.Combine(folder, "b.yaml"), "echo beta");

        var (exit, stderr) = Exec(bin, folder, ["--strict", "a-fails.yaml", "b.yaml"]);
        var ok = Check(exit == 1, $"exit {exit}")
            && Check(!File.Exists(Path.Combine(folder, "a-fails.cast")), "--strict wrote nothing for a")
            && Check(File.Exists(Path.Combine(folder, "b.cast")), "b was recorded")
            && Check(stderr.TrimEnd().EndsWith("Recorded 1 of 2 scenarios. Failed: a-fails.yaml", StringComparison.Ordinal), "the last line");
        if (!ok)
            Console.Error.WriteLine(stderr);
        return ok;
    }

    // What names one file, a file that is not there, and a pattern that finds nothing stop the run before anything is
    // recorded.
    internal static bool IntegrationRefusesBeforeRecording()
    {
        if (!TryGetBinary(out var bin))
            return true;

        var folder = NewFolder("refuses");
        WriteScenario(Path.Combine(folder, "a.yaml"), "echo alpha");
        WriteScenario(Path.Combine(folder, "b.yaml"), "echo beta");

        var cases = new (string[] Args, Dictionary<string, string>? Env, string Expected)[]
        {
        (["a.yaml", "b.yaml", "out"], null, "unexpected argument: out. Several scenarios are each written next to their YAML, so an output name works with one scenario file only"),
        (["a.yaml", "out", "b.yaml"], null, "unexpected argument: b.yaml. An output name works with one scenario file only"),
        (["*.yaml", "out"], null, "unexpected argument: out. Several scenarios"),
        (["--save-outputs", "o.json", "a.yaml", "b.yaml"], null, "--save-outputs names one file, and 2 scenarios were given: record them one at a time"),
        (["--save-keys", "keys.yaml", "a.yaml", "b.yaml"], null, "--save-keys names one file"),
        (["--replay", "o.json", "*.yaml"], null, "--replay names one file"),
        (["a.yaml", "b.yaml"], new() { ["SCENETAKE_OUTPUTS"] = "o.json" }, "SCENETAKE_OUTPUTS names one file"),
        (["a.yaml", "missing.yaml"], null, "missing.yaml not found"),
        (["a.yaml", "*.json"], null, "no file matches *.json"),
        (["a.yaml", "b.yaml", "a.yml"], null, "a.yaml and a.yml would both write a.cast: rename one of them"),
        };
        WriteScenario(Path.Combine(folder, "a.yml"), "echo other");

        var ok = true;
        foreach (var (args, env, expected) in cases)
        {
            var (exit, stderr) = Exec(bin, folder, args, env);
            ok &= Check(exit == 1 && stderr.Contains($"Error: {expected}", StringComparison.Ordinal), $"{string.Join(' ', args)}: exit {exit}{Environment.NewLine}{stderr}");
            ok &= Check(Directory.GetFiles(folder, "*.cast").Length == 0, $"{string.Join(' ', args)} recorded nothing");
        }

        return ok;
    }

    // A wildcard that finds one file is an ordinary run, which takes the options that name one file.
    internal static bool IntegrationOneMatchIsAnOrdinaryRun()
    {
        if (!TryGetBinary(out var bin))
            return true;

        var folder = NewFolder("one-match");
        WriteScenario(Path.Combine(folder, "only.yaml"), "echo only");

        var (exit, stderr) = Exec(bin, folder, ["--save-outputs", "only.outputs.json", "o*.yaml"]);
        var ok = Check(exit == 0, $"exit {exit}")
            && Check(File.Exists(Path.Combine(folder, "only.cast")) && File.Exists(Path.Combine(folder, "only.outputs.json")), "the files")
            && Check(stderr.Contains("Done: only.cast", StringComparison.Ordinal) && !stderr.Contains("Recorded", StringComparison.Ordinal), "no summary line");
        if (!ok)
            Console.Error.WriteLine(stderr);
        return ok;
    }

    // A Windows device path names one file, as it did before several scenarios: as the scenario, as the output name, and
    // as the folder that each scenario's own process is started in.
    internal static bool IntegrationDevicePaths()
    {
        if (!OperatingSystem.IsWindows())
            return true;
        if (!TryGetBinary(out var bin))
            return true;

        var folder = NewFolder("device");
        WriteScenario(Path.Combine(folder, "a.yaml"), "echo alpha");
        WriteScenario(Path.Combine(folder, "b.yaml"), "echo beta");
        var device = @"\\?\" + folder;

        var (scenarioExit, scenarioStderr) = Exec(bin, folder, [Path.Combine(device, "a.yaml")]);
        var (outputExit, outputStderr) = Exec(bin, folder, ["a.yaml", Path.Combine(device, "out")]);
        var (batchExit, batchStderr) = Exec(bin, device, ["*.yaml"]);
        // Windows says which file a \\.\ path opens: a.yaml named so is a.yaml once, and c.yml named so writes c.cast, as
        // c.yaml does.
        WriteScenario(Path.Combine(folder, "c.yaml"), "echo third");
        WriteScenario(Path.Combine(folder, "c.yml"), "echo other");
        var (twiceExit, twiceStderr) = Exec(bin, folder, ["a.yaml", @"\\.\" + Path.Combine(folder, "a.yaml")]);
        var (sharedExit, sharedStderr) = Exec(bin, folder, ["c.yaml", @"\\.\" + Path.Combine(folder, "c.yml")]);
        var ok = Check(scenarioExit == 0 && File.Exists(Path.Combine(folder, "a.cast")), $"scenario: exit {scenarioExit}{Environment.NewLine}{scenarioStderr}")
            && Check(outputExit == 0 && File.Exists(Path.Combine(folder, "out.cast")), $"output name: exit {outputExit}{Environment.NewLine}{outputStderr}")
            && Check(batchExit == 0 && batchStderr.TrimEnd().EndsWith("Recorded 2 scenarios.", StringComparison.Ordinal) && File.Exists(Path.Combine(folder, "b.cast")), $"batch in a device folder: exit {batchExit}{Environment.NewLine}{batchStderr}")
            && Check(twiceExit == 0 && !twiceStderr.Contains("Recorded", StringComparison.Ordinal), $"one file named twice: exit {twiceExit}{Environment.NewLine}{twiceStderr}")
            // The error names the cast as the second spelling gives it, //./C:/.../c.cast.
            && Check(sharedExit == 1 && sharedStderr.Contains("Error: c.yaml and ", StringComparison.Ordinal) && sharedStderr.Contains("c.cast: rename one of them", StringComparison.Ordinal),
                $"two files that write one cast: exit {sharedExit}{Environment.NewLine}{sharedStderr}");
        return ok;
    }

    internal static void WriteScenario(string path, string step) =>
        File.WriteAllText(path, $"title: {Path.GetFileNameWithoutExtension(path)}\nwidth: 40\nheight: 6\nsteps:\n  - {step}\n");

    internal static string NewFolder(string name)
    {
        var folder = Path.Combine(Path.GetTempPath(), "scenetake-batch-test", name);
        if (Directory.Exists(folder))
            Directory.Delete(folder, recursive: true);
        return Directory.CreateDirectory(folder).FullName;
    }

    internal static (int ExitCode, string Stderr) Exec(string bin, string folder, string[] arguments, Dictionary<string, string>? env = null)
    {
        var psi = new ProcessStartInfo(bin) { RedirectStandardError = true, RedirectStandardOutput = true, UseShellExecute = false, WorkingDirectory = folder };
        foreach (var argument in arguments)
            psi.ArgumentList.Add(argument);
        psi.Environment.Remove("SCENETAKE_OUTPUTS");
        foreach (var (key, value) in env ?? [])
            psi.Environment[key] = value;
        using var process = Process.Start(psi)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(TimeSpan.FromSeconds(180)))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"{string.Join(' ', arguments)} ran for over 180 s");
        }

        return (process.ExitCode, stderr.Result.Replace("\r\n", "\n", StringComparison.Ordinal));
    }

    internal static bool TryGetBinary(out string bin)
    {
        bin = TestEnv.Binary;
        return true;
    }
}
