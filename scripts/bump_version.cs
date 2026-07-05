#:sdk Microsoft.NET.Sdk
#:property TargetFramework=net10.0
#:property Nullable=enable
#:property ImplicitUsings=enable

// Bump or set Version in Directory.Build.props and npm/package.json.
//
// Usage (from repository root):
//   dotnet run scripts/bump_version.cs -- major
//   dotnet run scripts/bump_version.cs -- minor
//   dotnet run scripts/bump_version.cs -- patch
//   dotnet run scripts/bump_version.cs -- 1.4.0

using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;

var versionElementRegex = new Regex(@"<Version>\s*([^<]+?)\s*</Version>", RegexOptions.CultureInvariant);
var packageVersionRegex = new Regex(@"""version""\s*:\s*""[^""]+""", RegexOptions.CultureInvariant);
var versionRegex = new Regex(@"^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$", RegexOptions.CultureInvariant);
var numericVersionRegex = new Regex(@"^(\d+)\.(\d+)\.(\d+)", RegexOptions.CultureInvariant);

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: dotnet run scripts/bump_version.cs -- <major|minor|patch|x.y.z>");
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
if (oldPropsVersion != oldPackageVersion)
{
    Console.Error.WriteLine(
        $"Error: version mismatch: Directory.Build.props='{oldPropsVersion}' npm/package.json='{oldPackageVersion}'");
    return 1;
}

var baseVersion = TryGetLatestTagVersion(repoRoot) ?? oldPropsVersion;
if (!TryParseVersion(baseVersion, numericVersionRegex, out var current))
{
    Console.Error.WriteLine($"Error: could not parse version: {baseVersion}");
    return 1;
}

if (!TryResolveTargetVersion(args[0], current, versionRegex, out var newVersion))
{
    Console.Error.WriteLine($"Invalid version argument: {args[0]}");
    Console.Error.WriteLine("Expected major, minor, patch, or semver like 1.4.0");
    return 1;
}

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

Console.Error.WriteLine($"Base version: {baseVersion}");
Console.Error.WriteLine($"Directory.Build.props: {oldPropsVersion} -> {newVersion}");
Console.Error.WriteLine($"npm/package.json: {oldPackageVersion} -> {newVersion}");
return 0;

static bool TryResolveTargetVersion(
    string raw,
    (int Major, int Minor, int Patch) current,
    Regex versionRegex,
    out string version)
{
    version = "";
    var arg = raw.Trim();
    if (arg.Equals("major", StringComparison.OrdinalIgnoreCase))
    {
        version = $"{current.Major + 1}.0.0";
        return true;
    }

    if (arg.Equals("minor", StringComparison.OrdinalIgnoreCase))
    {
        version = $"{current.Major}.{current.Minor + 1}.0";
        return true;
    }

    if (arg.Equals("patch", StringComparison.OrdinalIgnoreCase))
    {
        version = $"{current.Major}.{current.Minor}.{current.Patch + 1}";
        return true;
    }

    var normalized = arg.StartsWith('v') || arg.StartsWith('V') ? arg[1..] : arg;
    if (!versionRegex.IsMatch(normalized))
        return false;

    version = normalized;
    return true;
}

static bool TryParseVersion(string raw, Regex numericVersionRegex, out (int Major, int Minor, int Patch) version)
{
    version = default;
    var normalized = raw.Trim();
    if (normalized.StartsWith('v') || normalized.StartsWith('V'))
        normalized = normalized[1..];

    var match = numericVersionRegex.Match(normalized);
    if (!match.Success)
        return false;

    version = (
        int.Parse(match.Groups[1].Value),
        int.Parse(match.Groups[2].Value),
        int.Parse(match.Groups[3].Value));
    return true;
}

static string? TryGetLatestTagVersion(string repoRoot)
{
    var psi = new ProcessStartInfo
    {
        FileName = "git",
        WorkingDirectory = repoRoot,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
    };
    psi.ArgumentList.Add("tag");
    psi.ArgumentList.Add("--list");
    psi.ArgumentList.Add("v*");
    psi.ArgumentList.Add("--sort=-v:refname");

    using var process = Process.Start(psi);
    if (process is null)
        return null;

    var output = process.StandardOutput.ReadToEnd();
    process.WaitForExit();
    if (process.ExitCode != 0)
        return null;

    var latestTag = output
        .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .FirstOrDefault();

    if (latestTag is null)
        return null;

    return latestTag.StartsWith('v') || latestTag.StartsWith('V')
        ? latestTag[1..]
        : latestTag;
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
