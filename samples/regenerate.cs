#:sdk Microsoft.NET.Sdk
#:property TargetFramework=net10.0
#:property Nullable=enable
#:property ImplicitUsings=enable

// Regenerate sample .cast and .svg files from every *.yaml in this directory, with the .outputs.json file that
// the playground replays, then a .gif of every cast with `scenetake gif` (needs ffmpeg).
//
// Usage (from repository root):
//   dotnet run samples/regenerate.cs
//   dotnet run samples/regenerate.cs -- --gifs            # only the GIFs, from the casts as they are
//   dotnet run samples/regenerate.cs -- --theme-presets   # only the color theme previews

using System.Diagnostics;
using System.Text.RegularExpressions;

var repoRoot = FindRepoRoot(Directory.GetCurrentDirectory());
var toolPath = Path.Combine(repoRoot, "scenetake.cs");
var samplesDir = Path.Combine(repoRoot, "samples");

if (!File.Exists(toolPath))
{
    Console.Error.WriteLine($"Error: {toolPath} not found");
    return 1;
}

if (RunReadmeSync(repoRoot) != 0)
    return 1;

// The color theme previews need no recording, so `-- --theme-presets` renders them alone.
if (args.Contains("--theme-presets"))
    return RenderThemePresets(repoRoot, toolPath);

// The GIFs need no recording either, and some casts are recorded by hand (interactive-pty, password-prompt), so
// `-- --gifs` converts every cast as it is.
if (args.Contains("--gifs"))
    return RenderGifs(repoRoot, toolPath, samplesDir);

var scenarios = Directory
    .EnumerateFiles(samplesDir, "*.yaml", SearchOption.TopDirectoryOnly)
    .OrderBy(Path.GetFileName, StringComparer.Ordinal)
    .ToArray();

if (scenarios.Length == 0)
{
    Console.Error.WriteLine($"Error: no .yaml files in {samplesDir}");
    return 1;
}

var failures = 0;
foreach (var scenario in scenarios)
{
    var name = Path.GetFileName(scenario);
    Console.Error.WriteLine();
    Console.Error.WriteLine($"=== {name} ===");

    if (RunScenario(repoRoot, toolPath, scenario) != 0)
        failures++;
}

if (failures > 0)
{
    Console.Error.WriteLine();
    Console.Error.WriteLine($"Failed: {failures}/{scenarios.Length} sample(s)");
    return 1;
}

if (RenderThemePresets(repoRoot, toolPath) != 0)
    return 1;

if (RenderGifs(repoRoot, toolPath, samplesDir) != 0)
    return 1;

Console.Error.WriteLine();
Console.Error.WriteLine($"Done: {scenarios.Length} sample(s), the theme previews, and the GIFs");
return 0;

// samples/<name>.gif: every cast in this folder as a GIF, made by `scenetake gif`, which needs ffmpeg. A cast keeps
// background colors but no image, so a scenario whose render.background is an image (translucent.yaml) passes it
// again with --background.
static int RenderGifs(string repoRoot, string toolPath, string samplesDir)
{
    Console.Error.WriteLine();
    Console.Error.WriteLine("=== GIFs ===");
    var casts = Directory.EnumerateFiles(samplesDir, "*.cast", SearchOption.TopDirectoryOnly).OrderBy(Path.GetFileName, StringComparer.Ordinal);
    foreach (var cast in casts)
    {
        var psi = new ProcessStartInfo { FileName = "dotnet", WorkingDirectory = repoRoot, UseShellExecute = false };
        foreach (var arg in new[] { "run", toolPath, "--", "gif" })
            psi.ArgumentList.Add(arg);
        if (BackgroundImage(Path.ChangeExtension(cast, ".yaml")) is string image)
        {
            psi.ArgumentList.Add("--background");
            psi.ArgumentList.Add(Path.GetRelativePath(repoRoot, image).Replace('\\', '/'));
        }

        psi.ArgumentList.Add(Path.GetRelativePath(repoRoot, cast).Replace('\\', '/'));
        psi.ArgumentList.Add(Path.GetRelativePath(repoRoot, Path.ChangeExtension(cast, ".gif")).Replace('\\', '/'));

        using var process = Process.Start(psi);
        if (process is null)
        {
            Console.Error.WriteLine("Error: failed to start dotnet");
            return 1;
        }

        process.WaitForExit();
        if (process.ExitCode != 0)
            return process.ExitCode;
    }

    return 0;
}

// The image file a scenario's render.background names, from its folder, or null for a color, a gradient, or none.
static string? BackgroundImage(string scenarioPath)
{
    if (!File.Exists(scenarioPath))
        return null;

    var match = Regex.Match(File.ReadAllText(scenarioPath),
        @"^\s+background:\s*[""']?([^""'#\s]+\.(?:png|jpe?g|gif|webp))[""']?\s*(?:#.*)?$", RegexOptions.Multiline | RegexOptions.IgnoreCase);
    return match.Success ? Path.Combine(Path.GetDirectoryName(scenarioPath)!, match.Groups[1].Value) : null;
}

// samples/theme-presets/<name>.svg: palette.cast in each named color theme, with the name in a macOS title bar.
// The names are RenderSettingsResolver.Presets in Svg.cs; svg_render_test checks that every theme has a current
// preview and that no preview is left of a theme that is gone.
static int RenderThemePresets(string repoRoot, string toolPath)
{
    string[] themes =
    [
        "dark", "light", "macos-dark", "macos-light", "dracula", "nord", "one-dark", "gruvbox-dark",
        "catppuccin-mocha", "tokyo-night", "solarized-dark", "solarized-light", "github-dark", "github-light",
    ];
    Console.Error.WriteLine();
    Console.Error.WriteLine("=== theme-presets ===");
    foreach (var theme in themes)
    {
        var psi = new ProcessStartInfo { FileName = "dotnet", WorkingDirectory = repoRoot, UseShellExecute = false };
        foreach (var arg in new[] { "run", toolPath, "--", "svg", "--theme", theme, "--window", "macos", "--window-title", theme,
            "samples/theme-presets/palette.cast", $"samples/theme-presets/{theme}.svg" })
            psi.ArgumentList.Add(arg);

        using var process = Process.Start(psi);
        if (process is null)
        {
            Console.Error.WriteLine("Error: failed to start dotnet");
            return 1;
        }

        process.WaitForExit();
        if (process.ExitCode != 0)
            return process.ExitCode;
    }

    return 0;
}

static int RunReadmeSync(string repoRoot)
{
    var psi = new ProcessStartInfo
    {
        FileName = "dotnet",
        WorkingDirectory = repoRoot,
        UseShellExecute = false,
    };
    psi.ArgumentList.Add("run");
    psi.ArgumentList.Add("tests/readme_sync_test.cs");

    using var process = Process.Start(psi);
    if (process is null)
    {
        Console.Error.WriteLine("Error: failed to start readme sync test");
        return 1;
    }

    process.WaitForExit();
    return process.ExitCode;
}

static int RunScenario(string repoRoot, string toolPath, string scenarioPath)
{
    var relativeScenario = Path.GetRelativePath(repoRoot, scenarioPath).Replace('\\', '/');

    var psi = new ProcessStartInfo
    {
        FileName = "dotnet",
        WorkingDirectory = repoRoot,
        UseShellExecute = false,
    };
    psi.ArgumentList.Add("run");
    psi.ArgumentList.Add(toolPath);
    psi.ArgumentList.Add("--");
    psi.ArgumentList.Add("--format");
    psi.ArgumentList.Add("svg");
    psi.ArgumentList.Add(relativeScenario);
    // The playground replays what each step printed in this run (.github/docs/spec_playground.md). The outputs
    // file comes from the same run as the cast, so a replay of the unmodified YAML gives the same cast.
    psi.Environment["SCENETAKE_OUTPUTS"] = Path.Combine(
        Path.GetDirectoryName(scenarioPath)!,
        Path.GetFileNameWithoutExtension(scenarioPath) + ".outputs.json");

    using var process = Process.Start(psi);
    if (process is null)
    {
        Console.Error.WriteLine("Error: failed to start dotnet");
        return 1;
    }

    process.WaitForExit();
    return process.ExitCode;
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
