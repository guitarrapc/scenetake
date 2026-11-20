#:sdk Microsoft.NET.Sdk
#:property TargetFramework=net10.0
#:property Nullable=enable
#:property ImplicitUsings=enable
#:include ../ScenarioBatch.cs

// Several scenarios in one run (.github/docs/spec_cli.md → Several scenarios). The unit tests need only the source.
// The integration tests record small scenarios with a published binary (SCENETAKE_BIN), in the default shell: pwsh on
// Windows, $SHELL elsewhere.

using System.Diagnostics;

var failures = 0;
failures += Run("ScenarioArguments", ScenarioArguments);
failures += Run("ExpandOrderAndDuplicates", ExpandOrderAndDuplicates);
failures += Run("ExpandErrors", ExpandErrors);
failures += Run("ChildArgumentsDropTheOtherScenarios", ChildArgumentsDropTheOtherScenarios);
failures += Run("SummaryLines", SummaryLines);
failures += Run("IntegrationRecordsEachScenario", IntegrationRecordsEachScenario);
failures += Run("IntegrationTwoYamlFilesAreTwoScenarios", IntegrationTwoYamlFilesAreTwoScenarios);
failures += Run("IntegrationFailureDoesNotStopTheOthers", IntegrationFailureDoesNotStopTheOthers);
failures += Run("IntegrationRefusesBeforeRecording", IntegrationRefusesBeforeRecording);
failures += Run("IntegrationOneMatchIsAnOrdinaryRun", IntegrationOneMatchIsAnOrdinaryRun);

return failures == 0 ? 0 : 1;

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

static bool Check(bool passed, string what)
{
    if (!passed)
        Console.Error.WriteLine($"  failed: {what}");
    return passed;
}

static bool ScenarioArguments()
{
    var ok = true;
    foreach (var arg in new[] { "a.yaml", "docs/A.YML", "demos/*", "demo?.txt" })
        ok &= Check(ScenarioBatch.IsScenarioArgument(arg), $"{arg} is a scenario");
    foreach (var arg in new[] { "out", "out.cast", "out.svg", "yaml" })
        ok &= Check(!ScenarioBatch.IsScenarioArgument(arg), $"{arg} is an output name");
    return ok;
}

static bool ExpandOrderAndDuplicates()
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
    // On Windows a name matches in any case, as the file system does.
    var caseOk = ScenarioBatch.TryExpand(["*.YAML"], folder, out var upper, out _);
    ok &= Check(OperatingSystem.IsWindows() ? caseOk && upper.Count == 3 : !caseOk, $"*.YAML: {caseOk} {Names(upper)}");
    return ok;
}

static bool ExpandErrors()
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

static bool ChildArgumentsDropTheOtherScenarios()
{
    string[] args = ["--format", "svg", "a.yaml", "--strict", "docs/*.yaml", "--theme=dracula"];
    var child = ScenarioBatch.ChildArguments(args, [2, 4], "/work/docs/b.yaml");
    return Check(string.Join(" ", child) == "--format svg --strict --theme=dracula /work/docs/b.yaml", string.Join(" ", child));
}

static bool SummaryLines()
{
    static string Display(string path) => path;
    var ok = true;
    ok &= Check(ScenarioBatch.Summary(3, 3, [], [], false, Display) == "Recorded 3 scenarios.", "all");
    ok &= Check(ScenarioBatch.Summary(3, 1, ["a.yaml", "b.yaml"], [], false, Display) == "Recorded 1 of 3 scenarios. Failed: a.yaml, b.yaml", "failed");
    ok &= Check(ScenarioBatch.Summary(3, 1, [], ["b.yaml", "c.yaml"], true, Display) == "Stopped by Ctrl+C: recorded 1 of 3 scenarios. Not recorded: b.yaml, c.yaml", "stopped");
    return ok;
}

// A wildcard as PowerShell passes it on, unexpanded, records each match in ordinal order, each next to its YAML.
static bool IntegrationRecordsEachScenario()
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
static bool IntegrationTwoYamlFilesAreTwoScenarios()
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

static bool IntegrationFailureDoesNotStopTheOthers()
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
static bool IntegrationRefusesBeforeRecording()
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
        (["a.yaml", "*.yml"], null, "no file matches *.yml"),
    };

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
static bool IntegrationOneMatchIsAnOrdinaryRun()
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

static void WriteScenario(string path, string step) =>
    File.WriteAllText(path, $"title: {Path.GetFileNameWithoutExtension(path)}\nwidth: 40\nheight: 6\nsteps:\n  - {step}\n");

static string NewFolder(string name)
{
    var folder = Path.Combine(Path.GetTempPath(), "scenetake-batch-test", name);
    if (Directory.Exists(folder))
        Directory.Delete(folder, recursive: true);
    return Directory.CreateDirectory(folder).FullName;
}

static (int ExitCode, string Stderr) Exec(string bin, string folder, string[] arguments, Dictionary<string, string>? env = null)
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

static bool TryGetBinary(out string bin)
{
    bin = Environment.GetEnvironmentVariable("SCENETAKE_BIN") ?? "";
    if (!string.IsNullOrWhiteSpace(bin) && File.Exists(bin))
        return true;

    Console.Error.WriteLine("  skip: set SCENETAKE_BIN to a published scenetake binary");
    return false;
}
