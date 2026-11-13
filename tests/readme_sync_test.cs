#:sdk Microsoft.NET.Sdk
#:property TargetFramework=net11.0
#:property Nullable=enable
#:property ImplicitUsings=enable

// Verify README Scenario Format yaml blocks match samples/scenario_format.yaml.
//
// Usage (from repository root):
//   dotnet run tests/readme_sync_test.cs

var repoRoot = FindRepoRoot(Directory.GetCurrentDirectory());
var scenarioPath = Path.Combine(repoRoot, "samples", "scenario_format.yaml");
var expected = NormalizeNewlines(File.ReadAllText(scenarioPath));

var failures = 0;
failures += VerifyReadme(
    Path.Combine(repoRoot, "README.md"),
  "## Scenario Format",
    expected);
failures += VerifyReadme(
    Path.Combine(repoRoot, "README-ja.md"),
    "## シナリオファイル形式",
    expected);

if (failures > 0)
{
    Console.Error.WriteLine();
    Console.Error.WriteLine($"Failed: {failures} README sync check(s)");
    return 1;
}

Console.Error.WriteLine("ok readme scenario format sync");
return 0;

static int VerifyReadme(string readmePath, string sectionHeading, string expectedYaml)
{
    var readme = File.ReadAllText(readmePath);
    var sectionIndex = readme.IndexOf(sectionHeading, StringComparison.Ordinal);
    if (sectionIndex < 0)
    {
        Console.Error.WriteLine($"FAIL {readmePath}: section not found: {sectionHeading}");
        return 1;
    }

    var fenceStart = readme.IndexOf("```yaml", sectionIndex, StringComparison.Ordinal);
    if (fenceStart < 0)
    {
        Console.Error.WriteLine($"FAIL {readmePath}: ```yaml fence not found after {sectionHeading}");
        return 1;
    }

    var contentStart = fenceStart + "```yaml".Length;
    if (contentStart < readme.Length && readme[contentStart] == '\r')
        contentStart++;
    if (contentStart < readme.Length && readme[contentStart] == '\n')
        contentStart++;

    var fenceEnd = readme.IndexOf("```", contentStart, StringComparison.Ordinal);
    if (fenceEnd < 0)
    {
        Console.Error.WriteLine($"FAIL {readmePath}: closing ``` fence not found");
        return 1;
    }

    var actual = NormalizeNewlines(readme[contentStart..fenceEnd]);
    if (actual == expectedYaml)
        return 0;

    Console.Error.WriteLine($"FAIL {readmePath}: Scenario Format yaml block does not match samples/scenario_format.yaml");
    return 1;
}

static string NormalizeNewlines(string text)
{
    var normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
    return normalized.TrimEnd('\n') + '\n';
}

static string FindRepoRoot(string startDir)
{
    var dir = Path.GetFullPath(startDir);
    while (true)
    {
        if (File.Exists(Path.Combine(dir, "scenetake.cs")))
            return dir;

        var parent = Directory.GetParent(dir);
        if (parent is null)
            break;

        dir = parent.FullName;
    }

    Console.Error.WriteLine("Error: could not find scenetake.cs; run from the repository root");
    Environment.Exit(1);
    return "";
}
