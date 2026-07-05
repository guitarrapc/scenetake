#:sdk Microsoft.NET.Sdk
#:property TargetFramework=net10.0
#:property Nullable=enable
#:property ImplicitUsings=enable

// Update Version in Directory.Build.props and npm/package.json.
//
// Usage (from repository root):
//   dotnet run scripts/set_version.cs -- 1.4.0
//   dotnet run scripts/set_version.cs -- v1.4.0

using System.Text.Json;
using System.Text.RegularExpressions;

var versionElementRegex = new Regex(@"<Version>\s*([^<]+?)\s*</Version>", RegexOptions.CultureInvariant);
var packageVersionRegex = new Regex(@"""version""\s*:\s*""[^""]+""", RegexOptions.CultureInvariant);
var versionRegex = new Regex(@"^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$", RegexOptions.CultureInvariant);

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: dotnet run scripts/set_version.cs -- <version>");
    Console.Error.WriteLine("Example: dotnet run scripts/set_version.cs -- 1.4.0");
    return 1;
}

var newVersion = NormalizeVersion(args[0]);
if (!IsValidVersion(newVersion, versionRegex))
{
    Console.Error.WriteLine($"Invalid version: {args[0]}");
    Console.Error.WriteLine("Expected semver like 1.4.0 or 1.4.0-beta.1");
    return 1;
}

var repoRoot = FindRepoRoot(Directory.GetCurrentDirectory());
var propsPath = Path.Combine(repoRoot, "Directory.Build.props");
var packageJsonPath = Path.Combine(repoRoot, "npm", "package.json");

if (!File.Exists(propsPath))
{
    Console.Error.WriteLine($"Error: {propsPath} not found");
    return 1;
}

if (!File.Exists(packageJsonPath))
{
    Console.Error.WriteLine($"Error: {packageJsonPath} not found");
    return 1;
}

var propsText = File.ReadAllText(propsPath);
var propsMatch = versionElementRegex.Match(propsText);
if (!propsMatch.Success)
{
    Console.Error.WriteLine($"Error: <Version> element not found in {propsPath}");
    return 1;
}

var packageJsonText = File.ReadAllText(packageJsonPath);
using var packageJson = JsonDocument.Parse(packageJsonText);
if (!packageJson.RootElement.TryGetProperty("version", out var versionElement) ||
    versionElement.GetString() is not string oldPackageVersion)
{
    Console.Error.WriteLine($"Error: \"version\" field not found in {packageJsonPath}");
    return 1;
}

var oldPropsVersion = propsMatch.Groups[1].Value;
if (oldPropsVersion == newVersion && oldPackageVersion == newVersion)
{
    Console.Error.WriteLine($"Version already {newVersion}");
    return 0;
}

var updatedProps = versionElementRegex.Replace(propsText, $"<Version>{newVersion}</Version>");
File.WriteAllText(propsPath, updatedProps);

var updatedPackageJson = packageVersionRegex.Replace(
    packageJsonText,
    $"\"version\": \"{newVersion}\"");
File.WriteAllText(packageJsonPath, updatedPackageJson);

Console.Error.WriteLine($"Directory.Build.props: {oldPropsVersion} -> {newVersion}");
Console.Error.WriteLine($"npm/package.json: {oldPackageVersion} -> {newVersion}");
return 0;

static string NormalizeVersion(string raw)
{
    var trimmed = raw.Trim();
    return trimmed.StartsWith('v') || trimmed.StartsWith('V')
        ? trimmed[1..]
        : trimmed;
}

static bool IsValidVersion(string version, Regex versionRegex) =>
    versionRegex.IsMatch(version);

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
