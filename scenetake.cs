#:sdk Microsoft.NET.Sdk
#:property TargetFramework=net10.0
#:property Nullable=enable
#:property ImplicitUsings=enable
#:property AllowUnsafeBlocks=true
#:package VYaml@1.3.0
#:package MiniPty@1.4.0
#:package MiniPty.Capture@1.4.0
#:package MiniPty.Console@1.4.0
#:package StbTrueTypeSharp@1.26.13
#:include Terminal.cs
#:include Svg.cs
#:include Raster.cs
#:include Mp4.cs
#:include CastReader.cs
#:include JsonEscape.cs
#:include PtyLeadingInitFilter.cs
#:include CastTimingPipeline.cs
#:include PipeStreamCapture.cs

using MiniPty;
using MiniPty.Capture;
using MiniPty.Console;
using System.Diagnostics;
using System.Globalization;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using VYaml.Annotations;
using VYaml.Serialization;

const string DefaultPrompt = "$ ";
const double DefaultSpeed = 0.05;
const double DefaultJitter = 0.015;
const double DefaultPreDelay = 0.2;
const double DefaultPostDelay = 0.5;
const double DefaultExecutionDuration = 0.05;
const double DefaultSilenceThreshold = 2.0;
const double DefaultMaxIdle = 0.2;
const double DefaultBurstThreshold = 0.05;
const string DefaultStderrColorSpec = "red";
const string SgrReset = "\u001b[0m";

if (args.Length < 1)
{
    PrintUsage();
    return 1;
}

if (args[0] is "init")
{
    if (args.Length == 2 && args[1] is "-h" or "--help")
    {
        PrintInitUsage();
        return 0;
    }

    if (!TryParseInitArgs(args, out var initPathArg, out var initError))
    {
        Console.Error.WriteLine($"Error: {initError}");
        PrintInitUsage();
        return 1;
    }

    var initPath = initPathArg is not null
        ? Path.GetFullPath(initPathArg)
        : Path.GetFullPath("scenario.yaml");

    if (File.Exists(initPath))
    {
        Console.Error.WriteLine($"Error: {FormatDisplayPath(initPath)} already exists");
        return 1;
    }

    Directory.CreateDirectory(Path.GetDirectoryName(initPath) ?? ".");
    File.WriteAllText(initPath, CreateInitialScenarioYaml(), Encoding.UTF8);
    Console.Error.WriteLine($"Created: {FormatDisplayPath(initPath)}");
    return 0;
}

if (args[0] is "svg")
{
    if (args.Length == 2 && args[1] is "-h" or "--help")
    {
        PrintSvgUsage();
        return 0;
    }

    if (!TryParseSvgArgs(args, out var castArg, out var svgOutputArg, out var svgFontSize, out var svgFontFamily, out var svgThemePreset, out var svgWindow, out var svgMaxFps, out var svgError))
    {
        Console.Error.WriteLine($"Error: {svgError}");
        PrintSvgUsage();
        return 1;
    }

    var castPath = Path.GetFullPath(castArg);
    if (!File.Exists(castPath))
    {
        Console.Error.WriteLine($"Error: {FormatDisplayPath(castPath)} not found");
        return 1;
    }

    var svgOutputPath = RenderSettingsResolver.ResolveCastSvgOutputPath(castPath, svgOutputArg);
    if (!TryCheckOutputPaths(castPath, [svgOutputPath], out var svgOutputError))
    {
        Console.Error.WriteLine($"Error: {svgOutputError}");
        return 1;
    }

    Console.Error.WriteLine($"Loading: {FormatDisplayPath(castPath)}");

    try
    {
        var recording = CastReader.Read(castPath);
        var svgRenderSettings = RenderSettingsResolver.ApplySvgOverrides(
            recording.RenderSettings,
            svgFontSize,
            svgFontFamily,
            svgThemePreset,
            svgWindow,
            svgMaxFps,
            out var svgOverrideError);
        if (svgOverrideError.Length != 0)
        {
            Console.Error.WriteLine($"Error: {svgOverrideError}");
            return 1;
        }

        SvgRender.WriteSvg(recording.Events, recording.Width, recording.Height, svgRenderSettings, svgOutputPath, recording.LoopDuration);
        Console.Error.WriteLine($"Written: {FormatDisplayPath(svgOutputPath)}  ({recording.Events.Count} events, {recording.LoopDuration:F1}s)");
        if (SvgRender.SizeWarning(new FileInfo(svgOutputPath).Length, svgRenderSettings.MaxFps, fromCast: true) is { } svgSizeWarning)
            Console.Error.WriteLine($"Warning: {svgSizeWarning}");
        Console.Error.WriteLine($"Done: {FormatDisplayPath(svgOutputPath)}");
        return 0;
    }
    catch (CastReadException ex)
    {
        DeleteFileIfExists(svgOutputPath);
        Console.Error.WriteLine($"Error: {ex.Message}");
        return 1;
    }
    catch (Exception ex)
    {
        DeleteFileIfExists(svgOutputPath);
        Console.Error.WriteLine($"Error: SVG render failed: {ex.Message}");
        return 1;
    }
}

if (args[0] is "mp4")
{
    if (args.Length == 2 && args[1] is "-h" or "--help")
    {
        PrintMp4Usage();
        return 0;
    }

    if (!TryParseMp4Args(args, out var mp4CastArg, out var mp4OutputArg, out var mp4FontSize, out var mp4FontFamily, out var mp4ThemePreset, out var mp4Window, out var mp4Options, out var mp4Error))
    {
        Console.Error.WriteLine($"Error: {mp4Error}");
        PrintMp4Usage();
        return 1;
    }

    var mp4CastPath = Path.GetFullPath(mp4CastArg);
    if (!File.Exists(mp4CastPath))
    {
        Console.Error.WriteLine($"Error: {FormatDisplayPath(mp4CastPath)} not found");
        return 1;
    }

    var mp4OutputPath = RenderSettingsResolver.ResolveCastMp4OutputPath(mp4CastPath, mp4OutputArg);
    if (!TryCheckOutputPaths(mp4CastPath, [mp4OutputPath], out var mp4OutputError))
    {
        Console.Error.WriteLine($"Error: {mp4OutputError}");
        return 1;
    }

    if (!Ffmpeg.TryResolve(out var mp4Ffmpeg, out var mp4FfmpegError))
    {
        Console.Error.WriteLine($"Error: {mp4FfmpegError}");
        return 1;
    }

    Console.Error.WriteLine($"Loading: {FormatDisplayPath(mp4CastPath)}");
    try
    {
        var recording = CastReader.Read(mp4CastPath);
        var mp4RenderSettings = RenderSettingsResolver.ApplySvgOverrides(
            recording.RenderSettings,
            mp4FontSize,
            mp4FontFamily,
            mp4ThemePreset,
            mp4Window,
            maxFpsOverride: null,
            out var mp4OverrideError);
        if (mp4OverrideError.Length != 0)
        {
            Console.Error.WriteLine($"Error: {mp4OverrideError}");
            return 1;
        }

        var result = Mp4Render.WriteMp4(recording.Events, recording.Width, recording.Height, mp4RenderSettings, mp4Options.ToSettings(), mp4OutputPath, mp4Ffmpeg, recording.LoopDuration);
        PrintMp4Written(mp4OutputPath, result);
        Console.Error.WriteLine($"Done: {FormatDisplayPath(mp4OutputPath)}");
        return 0;
    }
    catch (Exception ex)
    {
        // As with SVG, a cast that cannot be read removes an output from an earlier run, so it is not
        // mistaken for a video of this cast. Font and frame-size errors stop the render before it
        // writes anything and leave that output as it was, as the scenario path does when it finds
        // them before pre. WriteMp4 removes it once encoding starts.
        if (ex is not (FontNotFoundException or FrameTooLargeException))
            DeleteFileIfExists(mp4OutputPath);
        Console.Error.WriteLine(ex is CastReadException or FontNotFoundException or FrameTooLargeException ? $"Error: {ex.Message}" : $"Error: mp4 render failed: {ex.Message}");
        return 1;
    }
}

if (args[0] is "--version")
{
    PrintVersion();
    return 0;
}

if (args[0] is "-h" or "--help")
{
    PrintUsage();
    return 0;
}

if (!TryParseRunArgs(args, out var scenarioArg, out var outputArg, out var outputFormat, out var verbose, out var runFontSize, out var runFontFamily, out var runThemePreset, out var runWindow, out var runMaxFps, out var runMp4Options, out var runError))
{
    Console.Error.WriteLine($"Error: {runError}");
    PrintUsage();
    return 1;
}

var scenarioPath = Path.GetFullPath(scenarioArg);
if (!File.Exists(scenarioPath))
{
    Console.Error.WriteLine($"Error: {FormatDisplayPath(scenarioPath)} not found");
    return 1;
}

var outputStem = RenderSettingsResolver.ResolveOutputStem(scenarioPath, outputArg);
var outputPath = outputStem + ".cast";
var svgPath = outputFormat == OutputFormat.Svg ? outputStem + ".svg" : null;
var mp4Path = outputFormat == OutputFormat.Mp4 ? outputStem + ".mp4" : null;

Console.Error.WriteLine($"Loading: {FormatDisplayPath(scenarioPath)}");
// Register VYaml formatters explicitly for NativeAOT (source generator cannot call __Register via reflection)
Scenario.__RegisterVYamlFormatter();
ScenarioSettings.__RegisterVYamlFormatter();
ScenarioRender.__RegisterVYamlFormatter();
ScenarioTheme.__RegisterVYamlFormatter();
string yaml;
Scenario scenario;
try
{
    yaml = File.ReadAllText(scenarioPath, Encoding.UTF8);
    scenario = ParseScenario(yaml);
}
catch (Exception ex)
{
    // The read can fail like any file read. Parsing a string in memory fails only on what the file holds,
    // and the YAML library throws several exception types for that, some of them not public, so no
    // narrower catch can name them all.
    Console.Error.WriteLine($"Error: cannot read {FormatDisplayPath(scenarioPath)}: {ex.Message}");
    return 1;
}

if (!RenderSettingsResolver.TryResolve(scenario, runThemePreset, out var renderSettings, out var themeError))
{
    Console.Error.WriteLine($"Error: {themeError}");
    PrintUsage();
    return 1;
}

// The cast records width and height as its terminal size. Check them before running anything:
// a size the cast format does not allow would otherwise fail only when SVG or mp4 reads the cast back.
var scenarioWidth = scenario.Width ?? 120;
var scenarioHeight = scenario.Height ?? 24;
if (!RenderSettingsResolver.IsValidTerminalSize(scenarioWidth, scenarioHeight))
{
    Console.Error.WriteLine(
        $"Error: width must be between {RenderSettingsResolver.MinTerminalCols} and {RenderSettingsResolver.MaxTerminalCols}, and height between {RenderSettingsResolver.MinTerminalRows} and {RenderSettingsResolver.MaxTerminalRows}: {scenarioWidth}x{scenarioHeight}");
    return 1;
}

// scenetake refuses step keys that contradict each other, and a run given as a list or a mapping. They
// are checked before anything runs, so pre and the steps before the bad one never run for a scenario
// that is refused anyway.
if (FindStepKeyError(scenario) is { } stepKeyError)
{
    Console.Error.WriteLine($"Error: {stepKeyError}");
    return 1;
}

// The cast is written only after pre and the recording, so the outputs are checked first. When pre
// has commands, their folder is checked after pre instead, because pre may create it.
var preHasCommands = scenario.Pre?.Any(command => !string.IsNullOrWhiteSpace(command)) == true;
if (!TryCheckOutputPaths(scenarioPath, [outputPath, svgPath, mp4Path], out var outputPathError, requireFolder: !preHasCommands))
{
    Console.Error.WriteLine($"Error: {outputPathError}");
    return 1;
}

if (runFontSize is int runFontSizeValue)
    renderSettings = renderSettings with { FontSize = runFontSizeValue };
if (runFontFamily is string runFontFamilyValue)
    renderSettings = renderSettings with { FontFamily = runFontFamilyValue };
if (runWindow is WindowStyle runWindowValue)
    renderSettings = renderSettings with { Window = runWindowValue };
if (runMaxFps is int runMaxFpsValue)
    renderSettings = renderSettings with { MaxFps = runMaxFpsValue };

// mp4 needs ffmpeg, a font, and a frame size that ffmpeg and x264 accept. Check them before running
// anything, so a long recording does not fail at the end.
string? ffmpegPath = null;
if (outputFormat == OutputFormat.Mp4)
{
    if (!Ffmpeg.TryResolve(out var resolvedFfmpeg, out var ffmpegError))
    {
        Console.Error.WriteLine($"Error: {ffmpegError}");
        return 1;
    }

    try
    {
        var layout = RasterLayout.Create(renderSettings.FontSize, scenarioWidth, scenarioHeight, renderSettings.Window);
        Mp4Render.OutputSize(layout, runMp4Options.ToSettings());
        GlyphSource.Create(renderSettings.FontFamily, runMp4Options.FontFile, layout);
    }
    catch (Exception ex) when (ex is FontNotFoundException or FrameTooLargeException)
    {
        Console.Error.WriteLine($"Error: {ex.Message}");
        return 1;
    }

    ffmpegPath = resolvedFfmpeg;
}

var deterministicSeed = ComputeDeterministicSeed(yaml);
var deterministicTimestamp = ComputeDeterministicTimestamp(deterministicSeed);

ShellLaunch shell;
try
{
    shell = ResolveShell(scenario);
}
catch (InvalidOperationException ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    return 1;
}

Console.Error.WriteLine($"Using shell: {shell.DisplayName}");

var preExitCode = await RunScenarioCommandsAsync(scenario.Pre, "pre", scenario.Cwd, shell, verbose);
if (preExitCode != 0)
    return preExitCode;

// Stops the run with an error after pre has succeeded. post still runs, so what pre set up is torn down.
async Task<int> StopAfterPreAsync(string error)
{
    Console.Error.WriteLine($"Error: {error}");
    var cleanupExitCode = await RunScenarioCommandsAsync(scenario.Post, "post", scenario.Cwd, shell, verbose);
    return cleanupExitCode != 0 ? cleanupExitCode : 1;
}

// pre may create the output folder, or put a directory or a link to the input where an output goes, so
// every output check runs again. Found here, they stop the run before the recording, instead of failing
// the cast write or overwriting the input.
if (!TryCheckOutputPaths(scenarioPath, [outputPath, svgPath, mp4Path], out var outputCheckError))
    return await StopAfterPreAsync(outputCheckError);

// pre or the steps may install fonts. The mp4 render scans again when it starts, instead of using
// the scan made before pre.
FontCatalog.Rescan();

if (verbose)
    PrintPhase("steps");

Console.Error.WriteLine("Generating cast...");
List<CastEvent> events;
try
{
    events = await GenerateAsync(scenario, shell, deterministicSeed, verbose);
}
catch (Exception ex)
{
    // A step can fail in many ways, such as a command the PTY cannot start, or a working folder that an
    // earlier step removed. Each stops the recording, and post must still run. --verbose keeps the type
    // and stack, which an unexpected exception needs.
    return await StopAfterPreAsync(verbose ? ex.ToString() : ex.Message);
}

try
{
    WriteCast(scenario, events, outputPath, shell, deterministicTimestamp, renderSettings);
}
catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
{
    // A step may remove the output folder or lock the file.
    return await StopAfterPreAsync($"cannot write the cast: {ex.Message}");
}

var duration = events.Count > 0 ? events[^1].Time : 0.0;
Console.Error.WriteLine($"Written: {FormatDisplayPath(outputPath)}  ({events.Count} events, {duration:F1}s)");

// SVG and mp4 are rendered from the cast as written, not from the in-memory events. The cast stores
// millisecond times, so only the cast gives the same output as `scenetake svg|mp4 <cast>`. The header
// carries the font, theme, and window overrides. --max-fps is not stored, so it is applied again.
var exitCode = 0;
if (outputFormat == OutputFormat.Svg)
{
    try
    {
        var written = CastReader.Read(outputPath);
        var svgSettings = written.RenderSettings with { MaxFps = renderSettings.MaxFps };
        SvgRender.WriteSvg(written.Events, written.Width, written.Height, svgSettings, svgPath!, written.LoopDuration);
        Console.Error.WriteLine($"Written: {FormatDisplayPath(svgPath!)}");
        if (SvgRender.SizeWarning(new FileInfo(svgPath!).Length, svgSettings.MaxFps, fromCast: false) is { } svgSizeWarning)
            Console.Error.WriteLine($"Warning: {svgSizeWarning}");
    }
    catch (Exception ex)
    {
        DeleteFileIfExists(svgPath!);
        Console.Error.WriteLine($"Error: SVG render failed: {ex.Message}");
        exitCode = 1;
    }
}

if (outputFormat == OutputFormat.Mp4)
{
    try
    {
        // WriteMp4 encodes to a temporary file and renames it, so a failure leaves no partial .mp4.
        var written = CastReader.Read(outputPath);
        var result = Mp4Render.WriteMp4(written.Events, written.Width, written.Height, written.RenderSettings, runMp4Options.ToSettings(), mp4Path!, ffmpegPath!, written.LoopDuration);
        PrintMp4Written(mp4Path!, result);
    }
    catch (Exception ex)
    {
        // An .mp4 from an earlier run no longer matches the new cast.
        DeleteFileIfExists(mp4Path!);
        Console.Error.WriteLine($"Error: mp4 render failed: {ex.Message}");
        exitCode = 1;
    }
}

var postExitCode = await RunScenarioCommandsAsync(scenario.Post, "post", scenario.Cwd, shell, verbose);
if (postExitCode != 0)
    return postExitCode;

if (exitCode != 0)
    return exitCode;

if (outputFormat == OutputFormat.Svg)
    Console.Error.WriteLine($"Done: {FormatDisplayPath(outputPath)}, {FormatDisplayPath(svgPath!)}");
else if (outputFormat == OutputFormat.Mp4)
    Console.Error.WriteLine($"Done: {FormatDisplayPath(outputPath)}, {FormatDisplayPath(mp4Path!)}");
else
    Console.Error.WriteLine($"Done: {FormatDisplayPath(outputPath)}");
return 0;

static string FormatDisplayPath(string fullPath) =>
    Path.GetRelativePath(Directory.GetCurrentDirectory(), fullPath).Replace('\\', '/');

static bool TryParseInitArgs(string[] args, out string? path, out string error)
{
    path = null;
    error = "";
    for (var i = 1; i < args.Length; i++)
    {
        var arg = args[i];
        if (arg.StartsWith('-'))
        {
            error = $"unknown option: {arg}";
            return false;
        }

        if (path is not null)
        {
            error = $"unexpected argument: {arg}";
            return false;
        }

        path = arg;
    }

    return true;
}

static bool TryParseSvgArgs(
    string[] args,
    out string castPath,
    out string? outputPath,
    out int? fontSizeOverride,
    out string? fontFamilyOverride,
    out string? themePresetOverride,
    out WindowStyle? windowOverride,
    out int? maxFpsOverride,
    out string error)
{
    castPath = "";
    outputPath = null;
    fontSizeOverride = null;
    fontFamilyOverride = null;
    themePresetOverride = null;
    windowOverride = null;
    maxFpsOverride = null;
    error = "";

    for (var i = 1; i < args.Length; i++)
    {
        var arg = args[i];
        if (TryConsumeFontSizeArg(args, ref i, ref fontSizeOverride, out error))
        {
            if (error.Length != 0)
                return false;
            continue;
        }

        if (TryConsumeFontFamilyArg(args, ref i, ref fontFamilyOverride, out error))
        {
            if (error.Length != 0)
                return false;
            continue;
        }

        if (TryConsumeThemeArg(args, ref i, ref themePresetOverride, out error))
        {
            if (error.Length != 0)
                return false;
            continue;
        }

        if (TryConsumeWindowArg(args, ref i, ref windowOverride, out error))
        {
            if (error.Length != 0)
                return false;
            continue;
        }

        if (TryConsumeMaxFpsArg(args, ref i, ref maxFpsOverride, out error))
        {
            if (error.Length != 0)
                return false;
            continue;
        }

        if (arg.StartsWith('-'))
        {
            error = $"unknown option: {arg}";
            return false;
        }

        if (castPath.Length == 0)
        {
            castPath = arg;
            continue;
        }

        if (outputPath is null)
        {
            outputPath = arg;
            continue;
        }

        error = $"unexpected argument: {arg}";
        return false;
    }

    if (castPath.Length != 0)
        return true;

    error = "cast path is required";
    return false;
}

static bool TryParseRunArgs(
    string[] args,
    out string scenarioPath,
    out string? outputPath,
    out OutputFormat outputFormat,
    out bool verbose,
    out int? fontSizeOverride,
    out string? fontFamilyOverride,
    out string? themePresetOverride,
    out WindowStyle? windowOverride,
    out int? maxFpsOverride,
    out Mp4Options mp4Options,
    out string error)
{
    scenarioPath = "";
    outputPath = null;
    outputFormat = OutputFormat.Cast;
    verbose = false;
    fontSizeOverride = null;
    fontFamilyOverride = null;
    themePresetOverride = null;
    windowOverride = null;
    maxFpsOverride = null;
    mp4Options = new Mp4Options();
    error = "";

    for (var i = 0; i < args.Length; i++)
    {
        var arg = args[i];
        if (arg == "--verbose")
        {
            verbose = true;
            continue;
        }

        if (TryConsumeFontSizeArg(args, ref i, ref fontSizeOverride, out error))
        {
            if (error.Length != 0)
                return false;
            continue;
        }

        if (TryConsumeFontFamilyArg(args, ref i, ref fontFamilyOverride, out error))
        {
            if (error.Length != 0)
                return false;
            continue;
        }

        if (TryConsumeThemeArg(args, ref i, ref themePresetOverride, out error))
        {
            if (error.Length != 0)
                return false;
            continue;
        }

        if (TryConsumeWindowArg(args, ref i, ref windowOverride, out error))
        {
            if (error.Length != 0)
                return false;
            continue;
        }

        if (TryConsumeMaxFpsArg(args, ref i, ref maxFpsOverride, out error))
        {
            if (error.Length != 0)
                return false;
            continue;
        }

        if (TryConsumeMp4Arg(args, ref i, mp4Options, out error))
        {
            if (error.Length != 0)
                return false;
            continue;
        }

        if (arg == "--format")
        {
            if (i + 1 >= args.Length)
            {
                error = "--format requires a value";
                return false;
            }

            if (!TryParseOutputFormat(args[++i], out outputFormat, out error))
                return false;

            continue;
        }

        if (arg.StartsWith("--format=", StringComparison.Ordinal))
        {
            if (!TryParseOutputFormat(arg["--format=".Length..], out outputFormat, out error))
                return false;

            continue;
        }

        if (arg.StartsWith('-'))
        {
            error = $"unknown option: {arg}";
            return false;
        }

        if (scenarioPath.Length == 0)
        {
            scenarioPath = arg;
            continue;
        }

        if (outputPath is null)
        {
            outputPath = arg;
            continue;
        }

        error = $"unexpected argument: {arg}";
        return false;
    }

    if (outputFormat != OutputFormat.Mp4 && mp4Options.FirstOption is string mp4Option)
    {
        error = $"{mp4Option} requires --format mp4";
        return false;
    }

    if (outputFormat == OutputFormat.Mp4 && maxFpsOverride.HasValue)
    {
        error = "--max-fps applies to SVG; use --fps for mp4";
        return false;
    }

    if (outputFormat == OutputFormat.Cast && maxFpsOverride.HasValue)
    {
        error = "--max-fps requires --format svg";
        return false;
    }

    if (scenarioPath.Length != 0)
        return true;

    error = "scenario path is required";
    return false;
}

static bool TryConsumeFontSizeArg(string[] args, ref int i, ref int? fontSizeOverride, out string error)
{
    error = "";
    var arg = args[i];
    string? value;

    if (arg == "--font-size")
    {
        if (i + 1 >= args.Length)
        {
            error = "--font-size requires a value";
            return true;
        }

        value = args[++i];
    }
    else if (arg.StartsWith("--font-size=", StringComparison.Ordinal))
    {
        value = arg["--font-size=".Length..];
        if (value.Length == 0)
        {
            error = "--font-size requires a value";
            return true;
        }
    }
    else
    {
        return false;
    }

    if (fontSizeOverride.HasValue)
    {
        error = "duplicate option: --font-size";
        return true;
    }

    if (!RenderSettingsResolver.TryParseFontSize(value, out var parsed, out error))
        return true;

    fontSizeOverride = parsed;
    return true;
}

static bool TryConsumeMaxFpsArg(string[] args, ref int i, ref int? maxFpsOverride, out string error)
{
    error = "";
    var arg = args[i];
    string? value;

    if (arg == "--max-fps")
    {
        if (i + 1 >= args.Length)
        {
            error = "--max-fps requires a value";
            return true;
        }

        value = args[++i];
    }
    else if (arg.StartsWith("--max-fps=", StringComparison.Ordinal))
    {
        value = arg["--max-fps=".Length..];
        if (value.Length == 0)
        {
            error = "--max-fps requires a value";
            return true;
        }
    }
    else
    {
        return false;
    }

    if (maxFpsOverride.HasValue)
    {
        error = "duplicate option: --max-fps";
        return true;
    }

    if (!RenderSettingsResolver.TryParseMaxFps(value, out var parsed, out error))
        return true;

    maxFpsOverride = parsed;
    return true;
}

static bool TryConsumeFontFamilyArg(string[] args, ref int i, ref string? fontFamilyOverride, out string error)
{
    error = "";
    var arg = args[i];
    string? value;

    if (arg == "--font-family")
    {
        if (i + 1 >= args.Length)
        {
            error = "--font-family requires a value";
            return true;
        }

        value = args[++i];
    }
    else if (arg.StartsWith("--font-family=", StringComparison.Ordinal))
    {
        value = arg["--font-family=".Length..];
        if (value.Length == 0)
        {
            error = "--font-family requires a value";
            return true;
        }
    }
    else
    {
        return false;
    }

    if (fontFamilyOverride is not null)
    {
        error = "duplicate option: --font-family";
        return true;
    }

    if (!RenderSettingsResolver.TryParseFontFamily(value, out var parsed, out var parseError))
    {
        error = $"invalid --font-family value: {parseError}";
        return true;
    }

    fontFamilyOverride = parsed;
    return true;
}

static bool TryConsumeThemeArg(string[] args, ref int i, ref string? themePresetOverride, out string error)
{
    error = "";
    var arg = args[i];
    string? value;

    if (arg == "--theme")
    {
        if (i + 1 >= args.Length)
        {
            error = "--theme requires a value";
            return true;
        }

        value = args[++i];
    }
    else if (arg.StartsWith("--theme=", StringComparison.Ordinal))
    {
        value = arg["--theme=".Length..];
        if (value.Length == 0)
        {
            error = "--theme requires a value";
            return true;
        }
    }
    else
    {
        return false;
    }

    if (themePresetOverride is not null)
    {
        error = "duplicate option: --theme";
        return true;
    }

    if (!RenderSettingsResolver.TryParsePreset(value, out var parsed, out error))
        return true;

    themePresetOverride = parsed;
    return true;
}

static bool TryConsumeWindowArg(string[] args, ref int i, ref WindowStyle? windowOverride, out string error)
{
    error = "";
    var arg = args[i];
    string? value;

    if (arg == "--window")
    {
        if (i + 1 >= args.Length)
        {
            error = "--window requires a value";
            return true;
        }

        value = args[++i];
    }
    else if (arg.StartsWith("--window=", StringComparison.Ordinal))
    {
        value = arg["--window=".Length..];
        if (value.Length == 0)
        {
            error = "--window requires a value";
            return true;
        }
    }
    else
    {
        return false;
    }

    if (windowOverride is not null)
    {
        error = "duplicate option: --window";
        return true;
    }

    if (!RenderSettingsResolver.TryParseWindow(value, out var parsed, out error))
        return true;

    windowOverride = parsed;
    return true;
}

static bool TryParseOutputFormat(string value, out OutputFormat format, out string error)
{
    format = OutputFormat.Cast;
    error = "";
    switch (value.ToLowerInvariant())
    {
        case "cast":
            format = OutputFormat.Cast;
            return true;
        case "svg":
            format = OutputFormat.Svg;
            return true;
        case "mp4":
            format = OutputFormat.Mp4;
            return true;
        default:
            error = $"unknown format: {value}";
            return false;
    }
}

static Scenario ParseScenario(string yaml)
{
    var bytes = Encoding.UTF8.GetBytes(yaml);
    return YamlSerializer.Deserialize<Scenario>(bytes) ?? new Scenario();
}

static async Task<List<CastEvent>> GenerateAsync(Scenario scenario, ShellLaunch shell, int deterministicSeed, bool verbose)
{
    var settings = scenario.Settings ?? new ScenarioSettings();
    var prompt    = settings.Prompt ?? DefaultPrompt;
    var speed     = settings.TypingSpeed ?? DefaultSpeed;
    var jitter    = settings.TypingJitter ?? DefaultJitter;
    var preDelay  = settings.PreDelay ?? DefaultPreDelay;
    var postDelay = settings.PostDelay ?? DefaultPostDelay;
    var defaultExecutionDuration = settings.ExecutionDuration ?? DefaultExecutionDuration;
    var defaultStderrStyle = ParseStyleOrFallback(settings.StderrColor, SgrNamed(DefaultStderrColorSpec), "settings.stderr-color");
    var events = new List<CastEvent>();
    var rng    = new Random(deterministicSeed);
    double t   = 0.5;

    var steps = scenario.Steps ?? [];
    for (var i = 0; i < steps.Count; i++)
    {
        var command = ParseCommand(steps[i]);
        if (string.IsNullOrWhiteSpace(command.Cmd)) continue;

        var cmdSpeed  = GetDouble(command.Extra, speed, "typing-speed");
        var cmdJitter = GetDouble(command.Extra, jitter, "typing-jitter");
        var cmdPre    = GetDouble(command.Extra, preDelay, "pre-delay");
        var cmdPost   = GetDouble(command.Extra, postDelay, "post-delay");
        var cmdExecutionDuration = GetDouble(command.Extra, defaultExecutionDuration, "execution-duration");
        var cmdCastTiming = ResolveCastTiming(settings, command.Extra);
        var cmdStderrStyle = GetOverrideStyle(command.Extra, "stderr-color", defaultStderrStyle, "stderr-color", command.Cmd);
        var cmdHighlights = GetHighlights(command.Extra, command.Cmd);
        var hasRunHighlight = TryGetStepStyle(command.Extra, "run-highlight", "run-highlight", command.Cmd, out var runHighlightStyle);
        var usePty = GetBool(command.Extra, false, "pty");
        var useInteractive = GetBool(command.Extra, false, "interactive");
        var normalizeTyping = GetBool(command.Extra, false, "normalize-typing");

        if (events.Count == 0)
            t += preDelay;

        if (TryFormatNameComment(command.Name, command.Cmd, out var nameLine, out var nameDisplayText))
        {
            var prefix = CastEventLayout.NameCommentPrefixForLastEvent(events);
            events.Add(CastEvent.Marker(Math.Round(t, 6), nameDisplayText));
            events.Add(CastEvent.Output(Math.Round(t, 6), prefix + nameLine));
            t += 0.05;
        }

        EmitTypedCommand(events, ref t, prompt, command.Cmd, cmdSpeed, cmdJitter, rng, hasRunHighlight, runHighlightStyle);

        Console.Error.WriteLine($"  running: {command.Cmd}");
        var width = scenario.Width ?? 120;
        var height = scenario.Height ?? 24;
        var execution = useInteractive
            ? await RunInteractivePtyAsync(command.Cmd, scenario.Cwd, shell, width, height, verbose)
            : await RunCommandCoreAsync(command.Cmd, scenario.Cwd, shell, usePty, width, height, verbose);
        if (execution.ExitCode != 0)
            WarnStepExitCode(command.Name, command.Cmd, execution.ExitCode);
        var commandStart = t;
        if (execution.IsPty)
        {
            t = useInteractive
                ? EmitInteractivePtyStepOutput(
                    events,
                    commandStart,
                    execution.PtyByteChunks!,
                    execution.PtyInputChunks,
                    normalizeTyping,
                    cmdSpeed,
                    cmdCastTiming,
                    width,
                    height,
                    verbose)
                : EmitPtyStepOutput(
                    events,
                    commandStart,
                    execution.PtyByteChunks!,
                    cmdExecutionDuration,
                    cmdCastTiming,
                    verbose);
        }
        else
        {
            t = EmitPipeStepOutput(
                events,
                commandStart,
                execution.PipeStreamChunks!,
                cmdStderrStyle,
                cmdHighlights,
                command.Cmd,
                cmdExecutionDuration,
                cmdCastTiming);
        }

        t += cmdPost;
        t += cmdPre;
    }

    if (events.Count > 0)
        events.Add(CastEvent.Output(Math.Round(t, 6), prompt));

    return events;
}

static CastTimingSettings ResolveCastTiming(ScenarioSettings settings, Dictionary<string, object?>? stepExtra)
{
    var silenceThreshold = settings.SilenceThreshold ?? DefaultSilenceThreshold;
    var maxIdle = settings.MaxIdle ?? DefaultMaxIdle;
    double? maxDuration = settings.MaxDuration;
    var streamOutput = settings.StreamOutput ?? false;
    if (stepExtra is null)
        return new CastTimingSettings(silenceThreshold, maxIdle, maxDuration, streamOutput);

    return new CastTimingSettings(
        GetDouble(stepExtra, silenceThreshold, "silence-threshold"),
        GetDouble(stepExtra, maxIdle, "max-idle"),
        GetOptionalDouble(stepExtra, maxDuration, "max-duration"),
        GetBool(stepExtra, streamOutput, "stream-output"));
}

static double EmitPipeStepOutput(
    List<CastEvent> events,
    double commandStart,
    IReadOnlyList<PipeStreamChunk> chunks,
    string stderrStyle,
    List<HighlightSpec>? highlights,
    string cmd,
    double executionDuration,
    CastTimingSettings timing)
{
    if (chunks.Count == 0)
        return commandStart + executionDuration;

    if (!timing.UsesStreamEmission())
        return EmitPipeStepOutputInstant(events, commandStart, chunks, stderrStyle, highlights, cmd, executionDuration);

    var timedChunks = BuildStartupAdjustedPipeChunks(chunks, executionDuration, stderrStyle);
    ApplyBurstMergeToChunks(timedChunks, chunks);
    ApplyCastTimingToChunks(timedChunks, timing);
    var coalesced = CastTimingPipeline.CoalesceTimedText(timedChunks);
    EmitTimedTextCastEvents(events, commandStart, coalesced, highlights, cmd);
    return Math.Max(commandStart + executionDuration, commandStart + coalesced[^1].Time);
}

static double EmitPipeStepOutputInstant(
    List<CastEvent> events,
    double commandStart,
    IReadOnlyList<PipeStreamChunk> chunks,
    string stderrStyle,
    List<HighlightSpec>? highlights,
    string cmd,
    double executionDuration)
{
    var (stdout, stderr) = CollectPipeOutputs(chunks);
    var merged = MergePipeOutput(stdout, stderr, stderrStyle);
    if (string.IsNullOrEmpty(merged))
        return commandStart + executionDuration;

    var output = highlights is not null
        ? ApplyHighlights(merged, highlights, cmd)
        : merged;
    events.Add(CastEvent.Output(
        Math.Round(commandStart + executionDuration, 6),
        NormalizeNewlines(output)));
    return commandStart + executionDuration;
}

static string MergePipeOutput(string stdout, string stderr, string stderrStyle)
{
    stdout = SanitizeInstantEmitStdout(stdout);
    if (string.IsNullOrEmpty(stdout))
    {
        if (string.IsNullOrEmpty(stderr) || string.IsNullOrEmpty(stderrStyle) || ContainsAnsiSgr(stderr))
            return stderr;

        return WrapWithStylePreserveTrailingNewlines(stderr, stderrStyle);
    }

    if (string.IsNullOrEmpty(stderr))
        return stdout;

    if (string.IsNullOrEmpty(stderrStyle) || ContainsAnsiSgr(stderr))
        return string.Concat(stdout, stderr);

    return string.Concat(stdout, WrapWithStylePreserveTrailingNewlines(stderr, stderrStyle));
}

static string SanitizeInstantEmitStdout(string stdout)
{
    if (string.IsNullOrEmpty(stdout))
        return stdout;

    return stdout.IndexOf('\0') >= 0 ? "" : stdout;
}

static (string Stdout, string Stderr) CollectPipeOutputs(IReadOnlyList<PipeStreamChunk> chunks)
{
    if (chunks.Count == 0)
        return ("", "");

    StringBuilder? stdoutBuilder = null;
    StringBuilder? stderrBuilder = null;
    foreach (var chunk in chunks)
    {
        if (chunk.IsStderr)
        {
            stderrBuilder ??= new StringBuilder();
            stderrBuilder.Append(chunk.Text);
            continue;
        }

        stdoutBuilder ??= new StringBuilder();
        stdoutBuilder.Append(chunk.Text);
    }

    return (stdoutBuilder?.ToString() ?? "", stderrBuilder?.ToString() ?? "");
}

static void ApplyBurstMergeToChunks(
    List<(double Time, string Text)> chunks,
    IReadOnlyList<PipeStreamChunk> rawChunks)
{
    if (chunks.Count <= 1)
        return;

    var count = rawChunks.Count;
    var useStack = count <= 256;
    double[]? rentedOriginal = null;
    double[]? rentedTimes = null;
    Span<double> originalSeconds = useStack
        ? stackalloc double[count]
        : (rentedOriginal = ArrayPool<double>.Shared.Rent(count)).AsSpan(0, count);
    Span<double> times = useStack
        ? stackalloc double[count]
        : (rentedTimes = ArrayPool<double>.Shared.Rent(count)).AsSpan(0, count);
    try
    {
        for (var i = 0; i < rawChunks.Count; i++)
        {
            originalSeconds[i] = rawChunks[i].Time.TotalSeconds;
            times[i] = chunks[i].Time;
        }

        CastTimingPipeline.MergeBurstGaps(times, originalSeconds, DefaultBurstThreshold);
        for (var i = 0; i < chunks.Count; i++)
            chunks[i] = (times[i], chunks[i].Text);
    }
    finally
    {
        if (rentedOriginal is not null)
            ArrayPool<double>.Shared.Return(rentedOriginal, clearArray: false);
        if (rentedTimes is not null)
            ArrayPool<double>.Shared.Return(rentedTimes, clearArray: false);
    }
}

static double EmitPtyStepOutput(
    List<CastEvent> events,
    double commandStart,
    IReadOnlyList<PtyCaptureChunk> chunks,
    double executionDuration,
    CastTimingSettings timing,
    bool verbose)
{
    var ptyFilter = new PtyLeadingInitFilter();
    var timedChunks = new List<(double Time, double OriginalTime, ReadOnlyMemory<byte> Data)>();
    var startupOffset = 0.0;
    var startupOffsetResolved = false;

    void AddChunk(double chunkSeconds, ReadOnlyMemory<byte> data)
    {
        if (!startupOffsetResolved)
        {
            startupOffset = PtyCastTiming.ComputeStartupOffset(chunkSeconds, executionDuration);
            startupOffsetResolved = true;
            if (verbose && startupOffset > 0)
                Console.Error.WriteLine($"scenetake: pty startup compress: {startupOffset:F3}s");
        }

        timedChunks.Add((
            PtyCastTiming.AdjustChunkSeconds(chunkSeconds, startupOffset),
            chunkSeconds,
            data));
    }

    foreach (var chunk in chunks)
    {
        var data = ptyFilter.Process(chunk.Data);
        if (data.IsEmpty)
            continue;

        AddChunk(chunk.Time.TotalSeconds, data);
    }

    var tail = ptyFilter.Complete();
    if (!tail.IsEmpty)
    {
        var tailSeconds = chunks.Count > 0
            ? chunks[^1].Time.TotalSeconds
            : 0.0;
        AddChunk(tailSeconds, tail);
    }

    if (verbose)
        Console.Error.WriteLine($"scenetake: pty cleanup: stripped {ptyFilter.StrippedByteCount} bytes");

    if (timedChunks.Count == 0)
        return commandStart + executionDuration;

    if (timing.UsesStreamEmission())
    {
        ApplyBurstMergeToPtyChunks(timedChunks);
        ApplyCastTimingToPtyChunks(timedChunks, timing);
    }

    var coalescedInput = new List<(double Time, ReadOnlyMemory<byte> Data)>(timedChunks.Count);
    for (var i = 0; i < timedChunks.Count; i++)
        coalescedInput.Add((timedChunks[i].Time, timedChunks[i].Data));
    var coalesced = CastTimingPipeline.CoalesceTimedUtf8(coalescedInput);
    foreach (var (time, data) in coalesced)
    {
        events.Add(CastEvent.OutputUtf8(
            Math.Round(commandStart + time, 6),
            data));
    }

    return Math.Max(commandStart + executionDuration, commandStart + coalesced[^1].Time);
}

static double EmitInteractivePtyStepOutput(
    List<CastEvent> events,
    double commandStart,
    IReadOnlyList<PtyCaptureChunk> chunks,
    IReadOnlyList<PtyInputChunk>? inputChunks,
    bool normalizeTyping,
    double typingSpeed,
    CastTimingSettings castTiming,
    int width,
    int height,
    bool verbose)
{
    var ptyFilter = new PtyLeadingInitFilter();
    var timedChunks = new List<(double Time, ReadOnlyMemory<byte> Data)>();

    foreach (var chunk in chunks)
    {
        var data = ptyFilter.Process(chunk.Data);
        if (data.IsEmpty)
            continue;

        timedChunks.Add((chunk.Time.TotalSeconds, data));
    }

    var tail = ptyFilter.Complete();
    if (!tail.IsEmpty)
    {
        var tailSeconds = chunks.Count > 0
            ? chunks[^1].Time.TotalSeconds
            : 0.0;
        timedChunks.Add((tailSeconds, tail));
    }

    if (verbose)
    {
        var inputCount = inputChunks?.Count ?? 0;
        Console.Error.WriteLine(
            $"scenetake: interactive pty cleanup: stripped {ptyFilter.StrippedByteCount} bytes, input={inputCount}, normalize-typing={normalizeTyping}");
    }

    if (timedChunks.Count == 0)
    {
        var lastSeconds = 0.0;
        if (chunks.Count > 0)
            lastSeconds = Math.Max(lastSeconds, chunks[^1].Time.TotalSeconds);
        if (inputChunks is { Count: > 0 })
            lastSeconds = Math.Max(lastSeconds, inputChunks[^1].Time.TotalSeconds);
        return commandStart + lastSeconds;
    }

    List<(double Time, ReadOnlyMemory<byte> Data)> emitChunks;

    if (normalizeTyping && inputChunks is { Count: > 0 })
    {
        var inputTimes = new double[inputChunks.Count];
        var inputBytes = new (double Time, ReadOnlyMemory<byte> Data)[inputChunks.Count];
        for (var i = 0; i < inputChunks.Count; i++)
        {
            inputTimes[i] = inputChunks[i].Time.TotalSeconds;
            inputBytes[i] = (inputChunks[i].Time.TotalSeconds, inputChunks[i].Data);
        }

        var windows = InteractivePtyCastEmit.BuildInputEchoWindows(inputTimes);
        var normalized = InteractivePtyCastEmit.NormalizeOutputChunks(
            timedChunks,
            windows,
            typingSpeed,
            width,
            height,
            inputBytes);
        InteractivePtyCastEmit.CollapseIdleAfterNormalize(
            normalized,
            castTiming.SilenceThreshold,
            castTiming.MaxIdle);
        emitChunks = normalized.Select(c => (c.Time, c.Data)).ToList();
    }
    else
    {
        if (normalizeTyping && verbose)
            Console.Error.WriteLine("scenetake: normalize-typing: no input chunks; using wall-clock output");
        emitChunks = timedChunks;
    }

    var coalesced = CastTimingPipeline.CoalesceTimedUtf8(emitChunks);
    foreach (var (time, data) in coalesced)
    {
        events.Add(CastEvent.OutputUtf8(
            Math.Round(commandStart + time, 6),
            data));
    }

    return commandStart + coalesced[^1].Time;
}

static List<(double Time, string Text)> BuildStartupAdjustedPipeChunks(
    IReadOnlyList<PipeStreamChunk> chunks,
    double executionDuration,
    string stderrStyle)
{
    var startupOffset = PtyCastTiming.ComputeStartupOffset(chunks[0].Time.TotalSeconds, executionDuration);
    var timedChunks = new List<(double Time, string Text)>(chunks.Count);
    foreach (var chunk in chunks)
    {
        var adjusted = PtyCastTiming.AdjustChunkSeconds(chunk.Time.TotalSeconds, startupOffset);
        var text = FormatPipeChunkText(chunk, stderrStyle);
        timedChunks.Add((adjusted, text));
    }

    return timedChunks;
}

static string FormatPipeChunkText(PipeStreamChunk chunk, string stderrStyle)
{
    if (!chunk.IsStderr)
        return chunk.Text;

    if (string.IsNullOrEmpty(stderrStyle) || ContainsAnsiSgr(chunk.Text))
        return chunk.Text;

    return WrapWithStylePreserveTrailingNewlines(chunk.Text, stderrStyle);
}

static void ApplyCastTimingToChunks(List<(double Time, string Text)> chunks, CastTimingSettings timing)
{
    if (chunks.Count == 0)
        return;

    var count = chunks.Count;
    var useStack = count <= 256;
    double[]? rentedTimes = null;
    Span<double> times = useStack
        ? stackalloc double[count]
        : (rentedTimes = ArrayPool<double>.Shared.Rent(count)).AsSpan(0, count);
    try
    {
        for (var i = 0; i < count; i++)
            times[i] = chunks[i].Time;

        CastTimingPipeline.AdjustTimelineInPlace(times, timing);
        for (var i = 0; i < chunks.Count; i++)
            chunks[i] = (times[i], chunks[i].Text);
    }
    finally
    {
        if (rentedTimes is not null)
            ArrayPool<double>.Shared.Return(rentedTimes, clearArray: false);
    }
}

static void ApplyCastTimingToPtyChunks(
    List<(double Time, double OriginalTime, ReadOnlyMemory<byte> Data)> chunks,
    CastTimingSettings timing)
{
    if (chunks.Count == 0)
        return;

    var count = chunks.Count;
    var useStack = count <= 256;
    double[]? rentedTimes = null;
    Span<double> times = useStack
        ? stackalloc double[count]
        : (rentedTimes = ArrayPool<double>.Shared.Rent(count)).AsSpan(0, count);
    try
    {
        for (var i = 0; i < count; i++)
            times[i] = chunks[i].Time;

        CastTimingPipeline.AdjustTimelineInPlace(times, timing);
        for (var i = 0; i < chunks.Count; i++)
            chunks[i] = (times[i], chunks[i].OriginalTime, chunks[i].Data);
    }
    finally
    {
        if (rentedTimes is not null)
            ArrayPool<double>.Shared.Return(rentedTimes, clearArray: false);
    }
}

static void ApplyBurstMergeToPtyChunks(List<(double Time, double OriginalTime, ReadOnlyMemory<byte> Data)> chunks)
{
    if (chunks.Count <= 1)
        return;

    var count = chunks.Count;
    var useStack = count <= 256;
    double[]? rentedOriginal = null;
    double[]? rentedTimes = null;
    Span<double> originalSeconds = useStack
        ? stackalloc double[count]
        : (rentedOriginal = ArrayPool<double>.Shared.Rent(count)).AsSpan(0, count);
    Span<double> times = useStack
        ? stackalloc double[count]
        : (rentedTimes = ArrayPool<double>.Shared.Rent(count)).AsSpan(0, count);
    try
    {
        for (var i = 0; i < count; i++)
        {
            originalSeconds[i] = chunks[i].OriginalTime;
            times[i] = chunks[i].Time;
        }

        CastTimingPipeline.MergeBurstGaps(times, originalSeconds, DefaultBurstThreshold);
        for (var i = 0; i < chunks.Count; i++)
            chunks[i] = (times[i], chunks[i].OriginalTime, chunks[i].Data);
    }
    finally
    {
        if (rentedOriginal is not null)
            ArrayPool<double>.Shared.Return(rentedOriginal, clearArray: false);
        if (rentedTimes is not null)
            ArrayPool<double>.Shared.Return(rentedTimes, clearArray: false);
    }
}

static void EmitTimedTextCastEvents(
    List<CastEvent> events,
    double commandStart,
    IReadOnlyList<(double Time, string Text)> coalesced,
    List<HighlightSpec>? highlights,
    string cmd)
{
    if (coalesced.Count == 0)
        return;

    if (highlights is null)
    {
        foreach (var (time, text) in coalesced)
            events.Add(CastEvent.Output(Math.Round(commandStart + time, 6), NormalizeNewlines(text)));
        return;
    }

    var mergedBuilder = new StringBuilder();
    var rawSegments = new List<string>(coalesced.Count);
    for (var i = 0; i < coalesced.Count; i++)
    {
        var text = coalesced[i].Text;
        mergedBuilder.Append(text);
        rawSegments.Add(text);
    }

    var merged = mergedBuilder.ToString();
    var highlighted = ApplyHighlights(merged, highlights, cmd);
    var split = HighlightSplitter.SplitByRawSegments(highlighted, rawSegments);
    for (var i = 0; i < coalesced.Count; i++)
    {
        events.Add(CastEvent.Output(
            Math.Round(commandStart + coalesced[i].Time, 6),
            NormalizeNewlines(split[i])));
    }
}

static void EmitTypedCommand(
    List<CastEvent> events,
    ref double t,
    string prompt,
    string cmd,
    double speed,
    double jitter,
    Random rng,
    bool hasRunHighlight,
    string runHighlightStyle)
{
    events.Add(CastEvent.Output(Math.Round(t, 6), prompt));
    t += 0.05;

    if (hasRunHighlight && !string.IsNullOrEmpty(runHighlightStyle))
        events.Add(CastEvent.Output(Math.Round(t, 6), runHighlightStyle));

    foreach (var ch in cmd)
    {
        events.Add(CastEvent.Output(Math.Round(t, 6), TypingChars.Get(ch)));
        var delay = speed + rng.NextDouble() * 2 * jitter - jitter;
        t += Math.Max(delay, 0.005);
    }

    if (hasRunHighlight && !string.IsNullOrEmpty(runHighlightStyle))
        events.Add(CastEvent.Output(Math.Round(t, 6), SgrReset));

    events.Add(CastEvent.Output(Math.Round(t, 6), "\r\n"));
    t += 0.15;
}

/// <summary>The error for the first step whose run is a list or a mapping or whose keys contradict each other, or null when none is.</summary>
static string? FindStepKeyError(Scenario scenario)
{
    foreach (var step in scenario.Steps ?? [])
    {
        // A list or a mapping under run would otherwise be run as its type name.
        if (step is Dictionary<object, object?> map && map.TryGetValue("run", out var run) && run is System.Collections.IEnumerable and not string)
            return "Step key 'run' must be a command string, not a list or a mapping.";

        var command = ParseCommand(step);
        if (string.IsNullOrWhiteSpace(command.Cmd))
            continue;

        // GetBool warns about a value it cannot read, and the recording reads these keys again.
        var usePty = IsTrue(command.Extra, "pty");
        var useInteractive = IsTrue(command.Extra, "interactive");
        if (useInteractive && !usePty)
            return "Step key 'interactive: true' requires 'pty: true'.";
        if (IsTrue(command.Extra, "normalize-typing") && !useInteractive)
            return "Step key 'normalize-typing: true' requires 'interactive: true'.";
    }

    return null;

    static bool IsTrue(Dictionary<string, object?> extra, string key) =>
        extra.TryGetValue(key, out var value) && (value is true || (bool.TryParse(value?.ToString(), out var parsed) && parsed));
}

static CommandEntry ParseCommand(object? item)
{
    if (item is string s) return new CommandEntry { Cmd = s };
    if (item is Dictionary<object, object?> map)
    {
        var extra = new Dictionary<string, object?>(map.Count);
        var cmd = "";
        string? name = null;
        foreach (var (rawKey, value) in map)
        {
            var key = rawKey?.ToString() ?? "";
            switch (key)
            {
                case "run":
                    cmd = value?.ToString() ?? "";
                    break;
                case "name":
                    name = value?.ToString();
                    break;
                default:
                    extra[key] = value;
                    break;
            }
        }

        return new CommandEntry { Cmd = cmd, Name = name, Extra = extra };
    }
    return new CommandEntry();
}

static Task<CommandExecution> RunCommand(string cmd, string? cwd, ShellLaunch shell) =>
    RunCommandCoreAsync(cmd, cwd, shell, false, 120, 24);

static async Task<CommandExecution> RunCommandCoreAsync(string cmd, string? cwd, ShellLaunch shell, bool usePty, int width, int height, bool verbose = false)
{
    if (usePty)
    {
        var result = await PtyCapture.RunAsync(new PtyStartInfo
        {
            FileName = shell.FileName,
            Arguments = BuildPtyShellArguments(shell, cmd),
            WorkingDirectory = cwd,
            Size = new(width, height),
            Environment = CreateChildEnvironmentOverlay(),
        }, new PtyCaptureOptions { Completion = new PtyCompleteOptions { DecodeOutput = false } });
        if (verbose)
            Console.Error.WriteLine($"scenetake: pty verbose: chunks={result.Chunks.Count} bytes={result.Output.Length}");
        return CommandExecution.FromCapture(result);
    }

    var psi = new ProcessStartInfo(shell.FileName)
    {
        RedirectStandardOutput = true,
        RedirectStandardError  = true,
        UseShellExecute        = false,
        CreateNoWindow         = true,
    };
    foreach (var arg in shell.Arguments)
        psi.ArgumentList.Add(arg);
    psi.ArgumentList.Add(cmd);
    if (!string.IsNullOrWhiteSpace(cwd)) psi.WorkingDirectory = cwd;
    foreach (var (name, value) in CreateChildEnvironmentOverlay())
        psi.Environment[name] = value;

    using var proc = new Process { StartInfo = psi };
    proc.Start();
    var pipeChunks = await PipeStreamCapture.ReadAsync(proc);
    await proc.WaitForExitAsync();
    var (stdout, stderr) = CollectPipeOutputs(pipeChunks);
    return CommandExecution.FromPipeStream(stdout, stderr, proc.ExitCode, pipeChunks);
}

static async Task<CommandExecution> RunInteractivePtyAsync(
    string cmd,
    string? cwd,
    ShellLaunch shell,
    int width,
    int height,
    bool verbose)
{
    if (!Pty.IsSupported)
        throw new InvalidOperationException("PTY is not supported on this operating system.");

    if (Console.IsInputRedirected || Console.IsOutputRedirected)
    {
        throw new InvalidOperationException(
            "Interactive PTY steps require a real terminal (stdin and stdout must not be redirected).");
    }

    using var session = Pty.Start(new PtyStartInfo
    {
        FileName = shell.FileName,
        Arguments = BuildPtyShellArguments(shell, cmd),
        WorkingDirectory = cwd,
        Size = new(width, height),
        Environment = CreateChildEnvironmentOverlay(),
    });

    var stopwatch = Stopwatch.StartNew();
    var chunks = new List<PtyCaptureChunk>();
    var inputChunks = new List<PtyInputChunk>();
    var inputRecorder = new InteractivePtyInputRecorder(stopwatch, inputChunks);
    var pumpTask = PumpInteractiveOutputAsync(session, chunks, stopwatch, verbose);

    Console.Error.WriteLine("  attaching host terminal for interactive PTY input...");

    int exitCode;
    using (var attachCts = new CancellationTokenSource())
    using (var consoleInput = PtyConsoleInput.Attach(session, new PtyConsoleAttachOptions
    {
        SyncHostSize = false,
        InputObserver = inputRecorder,
    }))
    {
        var exitTask = session.WaitForExitAsync(attachCts.Token);
        _ = exitTask.ContinueWith(
            static (_, state) => ((CancellationTokenSource)state!).Cancel(),
            attachCts,
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

        consoleInput.PumpInputUntil(attachCts.Token);

        exitCode = exitTask.IsCompletedSuccessfully ? exitTask.Result.ExitCode : session.ExitCode ?? -1;
        await pumpTask.ConfigureAwait(false);
    }

    if (!OperatingSystem.IsWindows())
        ResetUnixHostCursorColumn();

    if (verbose)
    {
        Console.Error.WriteLine($"scenetake: interactive pty verbose: chunks={chunks.Count} input={inputChunks.Count}");
    }

    return CommandExecution.FromInteractivePty(exitCode, chunks, inputChunks);
}

static async Task PumpInteractiveOutputAsync(
    PtySession session,
    List<PtyCaptureChunk> chunks,
    Stopwatch stopwatch,
    bool verbose)
{
#pragma warning disable CA2000 // Host standard output is process-owned and must not be disposed.
    var stdout = Console.OpenStandardOutput();
#pragma warning restore CA2000

    await foreach (var chunk in session.ReadOutputAsync().ConfigureAwait(false))
    {
        var elapsed = stopwatch.Elapsed;
        var data = chunk.Data.ToArray().AsMemory();
        chunks.Add(new PtyCaptureChunk(elapsed, data));

        await stdout.WriteAsync(chunk.Data, CancellationToken.None).ConfigureAwait(false);

        if (verbose)
            Console.Error.WriteLine($"scenetake: interactive pty chunk: {data.Length} bytes at {elapsed.TotalSeconds:F3}s");
    }
}

static void ResetUnixHostCursorColumn()
{
#pragma warning disable CA2000 // Host standard output is process-owned and must not be disposed.
    var stdout = Console.OpenStandardOutput();
#pragma warning restore CA2000

    stdout.Write("\r\n"u8);
    stdout.Flush();
}

// Commands run behind scenetake's pipe or PTY, never the host terminal, so they guess color depth from environment
// hints such as WT_SESSION or TERM_PROGRAM that depend on where scenetake was launched. The cast and SVG renderer
// supports 24-bit color, so every command is told so and records the same colors from any host.
static Dictionary<string, string?> CreateChildEnvironmentOverlay() =>
    new(StringComparer.Ordinal) { ["COLORTERM"] = "truecolor" };

static string[] BuildPtyShellArguments(ShellLaunch shell, string cmd)
{
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && IsPowerShellName(shell.DisplayName))
        return [.. shell.Arguments.TakeWhile(static x => !string.Equals(x, "-Command", StringComparison.OrdinalIgnoreCase)), "-Command", cmd];

    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && string.Equals(shell.DisplayName, "cmd", StringComparison.OrdinalIgnoreCase))
        return ["/c", cmd];

    return [.. shell.Arguments, cmd];
}

static async Task<int> RunScenarioCommandsAsync(List<string>? commands, string phase, string? cwd, ShellLaunch shell, bool verbose)
{
    if (commands is null || commands.Count == 0)
        return 0;

    var phasePrinted = false;
    foreach (var cmd in commands)
    {
        if (string.IsNullOrWhiteSpace(cmd))
            continue;

        if (verbose)
        {
            if (!phasePrinted)
            {
                PrintPhase(phase);
                phasePrinted = true;
            }

            Console.Error.WriteLine($"Running {phase}:");
            Console.Error.WriteLine(cmd);
        }

        CommandExecution execution;
        try
        {
            execution = await RunCommand(cmd, cwd, shell);
        }
        catch (Exception ex)
        {
            PrintScenarioCommandFailure(phase, cmd, 1);
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        if (!string.IsNullOrEmpty(execution.Output))
            Console.Out.Write(execution.Output);
        if (!string.IsNullOrEmpty(execution.Stderr))
            Console.Error.Write(execution.Stderr);

        if (execution.ExitCode == 0)
            continue;

        PrintScenarioCommandFailure(phase, cmd, execution.ExitCode);
        return execution.ExitCode;
    }

    return 0;
}

static void PrintPhase(string name) => Console.Error.WriteLine($"== {name} ==");

static void PrintScenarioCommandFailure(string phase, string cmd, int exitCode)
{
    Console.Error.WriteLine($"{phase} failed (exit code {exitCode}):");
    Console.Error.WriteLine(cmd);
}

static string WrapWithStylePreserveTrailingNewlines(string text, string styleOpen)
{
    if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(styleOpen))
        return text;

    var splitAt = text.Length;
    while (splitAt > 0)
    {
        var ch = text[splitAt - 1];
        if (ch is '\n' or '\r')
            splitAt--;
        else
            break;
    }

    if (splitAt == text.Length)
        return string.Concat(styleOpen, text, SgrReset);

    var body = text[..splitAt];
    var trailingNewlines = text[splitAt..];
    return string.Concat(styleOpen, body, SgrReset, trailingNewlines);
}

static bool ContainsAnsiSgr(string text)
{
    for (var i = 0; i + 2 < text.Length; i++)
    {
        if (text[i] != '\u001b' || text[i + 1] != '[')
            continue;

        for (var j = i + 2; j < text.Length; j++)
        {
            var ch = text[j];
            if ((uint)(ch - '0') <= 9 || ch == ';')
                continue;

            if (ch == 'm')
                return true;

            break;
        }
    }

    return false;
}

static string NormalizeNewlines(string s)
    => s.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "\r\n");

static bool TryFormatNameComment(string? raw, string cmd, out string coloredLine, out string displayText)
{
    coloredLine = "";
    displayText = "";
    if (string.IsNullOrWhiteSpace(raw))
        return false;

    var value = raw.Trim();
    var colorOpen = SgrNamed("cyan");
    if (value.StartsWith('['))
    {
        var close = value.IndexOf(']');
        if (close > 1)
        {
            var colorName = value[1..close];
            displayText = value[(close + 1)..].TrimStart();
            if (!TryParseStyle(colorName, out var parsedStyle))
            {
                Warn("name", cmd, $"unknown color/style '{colorName}'");
                colorOpen = SgrNamed("cyan");
            }
            else
            {
                colorOpen = parsedStyle;
            }
        }
        else
        {
            displayText = value;
        }
    }
    else
    {
        displayText = value;
    }

    if (string.IsNullOrWhiteSpace(displayText))
    {
        Warn("name", cmd, "empty name text after color prefix");
        return false;
    }

    coloredLine = string.IsNullOrEmpty(colorOpen)
        ? $"# {displayText}\r\n"
        : $"{colorOpen}# {displayText}{SgrReset}\r\n";
    return true;
}

static string GetOverrideStyle(Dictionary<string, object?> extra, string key, string fallbackStyle, string scope, string cmd)
    => extra.TryGetValue(key, out var raw)
        ? ParseStyleOrFallback(raw?.ToString(), fallbackStyle, scope, cmd)
        : fallbackStyle;

static bool TryGetStepStyle(Dictionary<string, object?> extra, string key, string scope, string cmd, out string style)
{
    style = "";
    if (!extra.TryGetValue(key, out var raw))
        return false;

    var value = raw?.ToString();
    if (TryParseStyle(value, out style))
        return true;

    Warn(scope, cmd, $"unknown color/style '{value}'");
    return false;
}

static string SgrOpen(string codes) => $"\u001b[{codes}m";

static string SgrNamed(string name)
{
    return TryNamedForegroundCode(name, out var code)
        ? SgrOpen(code.ToString(CultureInfo.InvariantCulture))
        : "";
}

static List<HighlightSpec>? GetHighlights(Dictionary<string, object?> extra, string cmd)
{
    if (!extra.TryGetValue("highlight", out var raw) || raw is not List<object?> list || list.Count == 0)
        return null;

    var specs = new List<HighlightSpec>(list.Count);
    foreach (var item in list)
    {
        if (item is not Dictionary<object, object?> map) continue;
        var colorRaw = map.GetValueOrDefault("color")?.ToString();
        if (!TryParseStyle(colorRaw, out var colorOpen))
        {
            Warn("highlight", cmd, $"unknown color/style '{colorRaw}'");
            continue;
        }

        if (string.IsNullOrEmpty(colorOpen))
            continue;

        if (!map.TryGetValue("at", out var atRaw) || atRaw is null)
        {
            Warn("highlight", cmd, "missing 'at'");
            continue;
        }

        var ats = new List<string>();
        if (atRaw is List<object?> atList)
        {
            foreach (var a in atList)
            {
                var s = a?.ToString();
                if (!string.IsNullOrWhiteSpace(s)) ats.Add(s);
            }
        }
        else
        {
            var s = atRaw.ToString();
            if (!string.IsNullOrWhiteSpace(s)) ats.Add(s);
        }

        if (ats.Count == 0)
        {
            Warn("highlight", cmd, "missing 'at'");
            continue;
        }

        specs.Add(new HighlightSpec(colorOpen, ats));
    }

    return specs.Count > 0 ? specs : null;
}

static bool TryNamedForegroundCode(string? name, out int code)
{
    code = name?.Trim().ToLowerInvariant() switch
    {
        "black" => 30,
        "red" => 31,
        "green" => 32,
        "yellow" => 33,
        "blue" => 34,
        "magenta" => 35,
        "cyan" => 36,
        "white" => 37,
        "bright-black" or "gray" or "grey" => 90,
        "bright-red" => 91,
        "bright-green" => 92,
        "bright-yellow" => 93,
        "bright-blue" => 94,
        "bright-magenta" => 95,
        "bright-cyan" => 96,
        "bright-white" => 97,
        _ => 0,
    };

    return code != 0;
}

static bool TryParseStyle(string? raw, out string styleOpen)
{
    styleOpen = "";
    if (string.IsNullOrWhiteSpace(raw))
        return false;

    var text = raw.Trim();
    if (text.Equals("none", StringComparison.OrdinalIgnoreCase) ||
        text.Equals("off", StringComparison.OrdinalIgnoreCase) ||
        text.Equals("default", StringComparison.OrdinalIgnoreCase) ||
        text.Equals("reset", StringComparison.OrdinalIgnoreCase))
    {
        styleOpen = "";
        return true;
    }

    if (TryNamedForegroundCode(text, out var namedCode))
    {
        styleOpen = SgrOpen(namedCode.ToString(CultureInfo.InvariantCulture));
        return true;
    }

    if (TryParseSgrLiteral(text, out var sgrCodes))
    {
        styleOpen = SgrOpen(sgrCodes);
        return true;
    }

    if (TryParseStyleWords(text, out var wordCodes))
    {
        styleOpen = SgrOpen(wordCodes);
        return true;
    }

    return false;
}

static bool TryParseSgrLiteral(string raw, out string codes)
{
    codes = "";
    var text = raw.Trim();

    if (text.StartsWith("\\e[", StringComparison.OrdinalIgnoreCase))
        text = text[3..];
    else if (text.StartsWith("\\x1b[", StringComparison.OrdinalIgnoreCase))
        text = text[5..];
    else if (text.StartsWith("\\u001b[", StringComparison.OrdinalIgnoreCase))
        text = text[7..];
    else if (text.Length >= 2 && text[0] == '\u001b' && text[1] == '[')
        text = text[2..];
    else if (text.StartsWith("[", StringComparison.Ordinal))
        text = text[1..];

    if (text.EndsWith("m", StringComparison.OrdinalIgnoreCase))
        text = text[..^1];

    if (text.Length == 0)
        return false;

    if (text[0] == ';' || text[^1] == ';')
        return false;

    var parts = text.Split(';', StringSplitOptions.TrimEntries);
    if (parts.Length == 0)
        return false;

    var normalized = new List<string>(parts.Length);
    for (var i = 0; i < parts.Length; i++)
    {
        var part = parts[i];
        if (!int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out var value))
            return false;

        if (value is 38 or 48 && i + 1 < parts.Length && parts[i + 1] == "5")
        {
            if (i + 2 >= parts.Length)
                return false;

            if (!int.TryParse(parts[i + 2], NumberStyles.None, CultureInfo.InvariantCulture, out var colorIndex))
                return false;

            if (colorIndex is < 0 or > 255)
                return false;

            normalized.Add(value.ToString(CultureInfo.InvariantCulture));
            normalized.Add("5");
            normalized.Add(colorIndex.ToString(CultureInfo.InvariantCulture));
            i += 2;
            continue;
        }

        if (value is 38 or 48 && i + 1 < parts.Length && parts[i + 1] == "2")
        {
            if (i + 4 >= parts.Length)
                return false;

            if (!int.TryParse(parts[i + 2], NumberStyles.None, CultureInfo.InvariantCulture, out var r) ||
                !int.TryParse(parts[i + 3], NumberStyles.None, CultureInfo.InvariantCulture, out var g) ||
                !int.TryParse(parts[i + 4], NumberStyles.None, CultureInfo.InvariantCulture, out var b))
            {
                return false;
            }

            if (r is < 0 or > 255 || g is < 0 or > 255 || b is < 0 or > 255)
                return false;

            normalized.Add(value.ToString(CultureInfo.InvariantCulture));
            normalized.Add("2");
            normalized.Add(r.ToString(CultureInfo.InvariantCulture));
            normalized.Add(g.ToString(CultureInfo.InvariantCulture));
            normalized.Add(b.ToString(CultureInfo.InvariantCulture));
            i += 4;
            continue;
        }

        if (value is < 0 or > 107)
            return false;

        normalized.Add(value.ToString(CultureInfo.InvariantCulture));
    }

    codes = string.Join(';', normalized);
    return true;
}

static bool TryParseStyleWords(string raw, out string codes)
{
    codes = "";
    var tokens = raw
        .Split([' ', ',', '+'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(static t => t.Trim().ToLowerInvariant())
        .ToArray();

    if (tokens.Length == 0)
        return false;

    var bold = false;
    var underline = false;
    var intensityRequested = false;
    string? fgCodes = null;
    string? bgCodes = null;
    int simpleFgCode = 0;

    foreach (var token in tokens)
    {
        if (token is "bold")
        {
            bold = true;
            continue;
        }

        if (token is "underline")
        {
            underline = true;
            continue;
        }

        if (token is "bright")
        {
            intensityRequested = true;
            continue;
        }

        if (TryParseFgBgToken(token, out var isBackground, out var colorName))
        {
            if (isBackground)
            {
                if (!TryParseStyleColor(colorName, isBackground: true, out bgCodes, out _))
                    return false;
            }
            else
            {
                if (!TryParseStyleColor(colorName, isBackground: false, out fgCodes, out simpleFgCode))
                    return false;
            }

            continue;
        }

        if (TryNamedForegroundCode(token, out var directFg))
        {
            simpleFgCode = directFg;
            fgCodes = directFg.ToString(CultureInfo.InvariantCulture);
            continue;
        }

        return false;
    }

    if (intensityRequested)
    {
        if (simpleFgCode is >= 30 and <= 37)
            fgCodes = (simpleFgCode + 60).ToString(CultureInfo.InvariantCulture);
        else if (fgCodes is null)
            bold = true;
    }

    var list = new List<string>(4);
    if (bold) list.Add("1");
    if (underline) list.Add("4");
    if (fgCodes is not null) list.Add(fgCodes);
    if (bgCodes is not null) list.Add(bgCodes);
    if (list.Count == 0) return false;

    codes = string.Join(';', list);
    return true;
}

static bool TryParseStyleColor(string raw, bool isBackground, out string codes, out int simpleForegroundCode)
{
    codes = "";
    simpleForegroundCode = 0;

    if (TryNamedForegroundCode(raw, out var fgCode))
    {
        simpleForegroundCode = isBackground ? 0 : fgCode;
        codes = (isBackground ? fgCode + 10 : fgCode).ToString(CultureInfo.InvariantCulture);
        return true;
    }

    if (TryParseTrueColorValue(raw, out var r, out var g, out var b))
    {
        codes = isBackground
            ? string.Create(CultureInfo.InvariantCulture, $"48;2;{r};{g};{b}")
            : string.Create(CultureInfo.InvariantCulture, $"38;2;{r};{g};{b}");
        return true;
    }

    if (!int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var colorIndex))
        return false;

    if (colorIndex is < 0 or > 255)
        return false;

    codes = isBackground
        ? $"48;5;{colorIndex.ToString(CultureInfo.InvariantCulture)}"
        : $"38;5;{colorIndex.ToString(CultureInfo.InvariantCulture)}";
    return true;
}

static bool TryParseTrueColorValue(string raw, out int r, out int g, out int b)
{
    r = g = b = 0;
    var text = raw.Trim();
    if (text.Length == 0)
        return false;

    if (text[0] == '#')
    {
        var hex = text[1..];
        if (hex.Length == 3)
        {
            if (!TryParseHexComponent(hex[0], out var rNibble) ||
                !TryParseHexComponent(hex[1], out var gNibble) ||
                !TryParseHexComponent(hex[2], out var bNibble))
            {
                return false;
            }

            r = rNibble * 17;
            g = gNibble * 17;
            b = bNibble * 17;
            return true;
        }

        if (hex.Length != 6)
            return false;

        if (!TryParseHexPair(hex[0], hex[1], out r) ||
            !TryParseHexPair(hex[2], hex[3], out g) ||
            !TryParseHexPair(hex[4], hex[5], out b))
        {
            return false;
        }

        return true;
    }

    var parts = text.Split(',', StringSplitOptions.TrimEntries);
    if (parts.Length != 3)
        return false;

    if (!int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out r) ||
        !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out g) ||
        !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out b))
    {
        return false;
    }

    return r is >= 0 and <= 255 &&
           g is >= 0 and <= 255 &&
           b is >= 0 and <= 255;
}

static bool TryParseHexPair(char hi, char lo, out int value)
{
    value = 0;
    if (!TryParseHexComponent(hi, out var high) || !TryParseHexComponent(lo, out var low))
        return false;

    value = high * 16 + low;
    return true;
}

static bool TryParseHexComponent(char c, out int value)
{
    if (c is >= '0' and <= '9')
    {
        value = c - '0';
        return true;
    }

    if (c is >= 'a' and <= 'f')
    {
        value = c - 'a' + 10;
        return true;
    }

    if (c is >= 'A' and <= 'F')
    {
        value = c - 'A' + 10;
        return true;
    }

    value = 0;
    return false;
}

static bool TryParseFgBgToken(string token, out bool isBackground, out string colorName)
{
    isBackground = false;
    colorName = "";
    if (token.StartsWith("fg:", StringComparison.Ordinal))
    {
        var value = token[3..].Trim();
        if (value.Length == 0)
            return false;

        isBackground = false;
        colorName = value;
        return true;
    }

    if (token.StartsWith("bg:", StringComparison.Ordinal))
    {
        var value = token[3..].Trim();
        if (value.Length == 0)
            return false;

        isBackground = true;
        colorName = value;
        return true;
    }

    return false;
}

static string ApplyHighlights(string output, List<HighlightSpec> specs, string cmd)
{
    var text = NormalizeToLf(output);
    var lineCount = CountLines(text.AsSpan());
    var lineRangesArray = ArrayPool<int>.Shared.Rent(lineCount * 2);

    try
    {
        var lineRanges = lineRangesArray.AsSpan(0, lineCount * 2);
        FillLineRanges(text.AsSpan(), lineRanges);

        var paint = new ushort[lineCount][];
        var styleIds = new Dictionary<string, ushort>(StringComparer.Ordinal);
        var stylesList = new List<string> { "" };

        foreach (var spec in specs)
        {
            var styleId = GetOrAddStyleId(styleIds, stylesList, spec.ColorOpen);
            foreach (var at in spec.At)
                ApplyAt(lineRanges, paint, at, styleId, cmd);
        }

        var styles = CollectionsMarshal.AsSpan(stylesList);
        var sb = new StringBuilder(text.Length + specs.Count * 16);
        for (var i = 0; i < lineCount; i++)
        {
            if (i > 0) sb.Append('\n');
            var start = lineRanges[i * 2];
            var length = lineRanges[i * 2 + 1];
            AppendPaintedLine(sb, text.AsSpan(start, length), paint[i], styles);
        }

        return sb.ToString();
    }
    finally
    {
        ArrayPool<int>.Shared.Return(lineRangesArray, clearArray: false);
    }
}

static string NormalizeToLf(string text)
{
    if (text.IndexOf('\r') < 0)
        return text;

    var sb = new StringBuilder(text.Length);
    var span = text.AsSpan();
    for (var i = 0; i < span.Length; i++)
    {
        var ch = span[i];
        if (ch == '\r')
        {
            sb.Append('\n');
            if (i + 1 < span.Length && span[i + 1] == '\n')
                i++;
            continue;
        }

        sb.Append(ch);
    }

    return sb.ToString();
}

static int CountLines(ReadOnlySpan<char> text)
{
    var count = 1;
    for (var i = 0; i < text.Length; i++)
    {
        if (text[i] == '\n')
            count++;
    }

    return count;
}

static void FillLineRanges(ReadOnlySpan<char> text, Span<int> lineRanges)
{
    var start = 0;
    var line = 0;
    for (var i = 0; i < text.Length; i++)
    {
        if (text[i] != '\n')
            continue;

        lineRanges[line * 2] = start;
        lineRanges[line * 2 + 1] = i - start;
        line++;
        start = i + 1;
    }

    lineRanges[line * 2] = start;
    lineRanges[line * 2 + 1] = text.Length - start;
}

static void ApplyAt(ReadOnlySpan<int> lines, ushort[][] paint, string at, ushort styleId, string cmd)
{
    if (!TryParseAt(at, out var lineLo, out var lineHi, out var openLineEnd, out var colLo, out var colHi, out var openColEnd, out var hasCol))
    {
        Warn("highlight", cmd, $"invalid at '{at}'");
        return;
    }

    var lineCount = lines.Length / 2;
    if (lineLo > lineCount)
    {
        Warn("highlight", cmd, $"at '{at}' starts after output");
        return;
    }

    if (openLineEnd)
    {
        lineHi = lineCount;
    }
    else if (lineHi > lineCount)
    {
        Warn("highlight", cmd, $"at '{at}': lines {lineCount + 1}-{lineHi} are missing");
        lineHi = lineCount;
    }

    for (var line = lineLo; line <= lineHi; line++)
    {
        var li = line - 1;
        var contentLength = lines[li * 2 + 1];
        int start, end;
        if (hasCol)
        {
            start = colLo - 1;
            end = openColEnd ? contentLength : colHi;
            if (start >= contentLength)
            {
                if (lineLo == lineHi)
                    Warn("highlight", cmd, $"at '{at}' starts after output");
                continue;
            }
            if (end > contentLength) end = contentLength;
        }
        else
        {
            start = 0;
            end = contentLength;
        }

        if (start >= end) continue;
        var row = paint[li] ??= new ushort[contentLength];
        for (var c = start; c < end; c++)
            row[c] = styleId;
    }
}

static void AppendPaintedLine(StringBuilder sb, ReadOnlySpan<char> line, ushort[]? row, ReadOnlySpan<string> styles)
{
    if (row is null)
    {
        sb.Append(line);
        return;
    }

    ushort cur = 0;
    for (var i = 0; i < line.Length; i++)
    {
        var next = row[i];
        if (next != cur)
        {
            if (cur != 0) sb.Append(SgrReset);
            if (next != 0) sb.Append(styles[next]);
            cur = next;
        }
        sb.Append(line[i]);
    }

    if (cur != 0) sb.Append(SgrReset);
}

static ushort GetOrAddStyleId(Dictionary<string, ushort> styleIds, List<string> styles, string style)
{
    if (styleIds.TryGetValue(style, out var id))
        return id;

    if (styles.Count >= ushort.MaxValue)
        throw new InvalidOperationException("Too many unique highlight styles in one output.");

    id = (ushort)styles.Count;
    styles.Add(style);
    styleIds[style] = id;
    return id;
}

static bool TryParseAt(string at, out int lineLo, out int lineHi, out bool openLineEnd, out int colLo, out int colHi, out bool openColEnd, out bool hasCol)
{
    lineLo = lineHi = colLo = colHi = 0;
    openLineEnd = openColEnd = hasCol = false;
    var span = at.AsSpan().Trim();
    if (span.IsEmpty) return false;

    var colon = span.IndexOf(':');
    if (!TryParseSpan(span[..(colon >= 0 ? colon : span.Length)], allowOpenEnd: true, out lineLo, out lineHi, out openLineEnd))
        return false;

    if (colon < 0) return true;
    hasCol = true;
    return TryParseColSpan(span[(colon + 1)..], out colLo, out colHi, out openColEnd);
}

static bool TryParseSpan(ReadOnlySpan<char> span, bool allowOpenEnd, out int lo, out int hi, out bool openEnd)
{
    lo = hi = 0;
    openEnd = false;
    var dash = span.IndexOf('-');
    if (dash < 0)
        return TryPosInt(span, out lo) && (hi = lo) > 0;

    if (allowOpenEnd && dash == span.Length - 1)
    {
        if (!TryPosInt(span[..dash], out lo) || lo < 1)
            return false;

        hi = lo;
        openEnd = true;
        return true;
    }

    if (!TryPosInt(span[..dash], out lo) || !TryPosInt(span[(dash + 1)..], out hi) || lo < 1 || hi < 1)
        return false;

    if (lo > hi) (lo, hi) = (hi, lo);
    return true;
}

static bool TryParseColSpan(ReadOnlySpan<char> span, out int lo, out int hi, out bool openEnd)
{
    lo = hi = 0;
    openEnd = false;
    if (span.EndsWith("-"))
    {
        openEnd = true;
        span = span[..^1];
        return TryPosInt(span, out lo) && lo > 0;
    }

    return TryParseSpan(span, allowOpenEnd: false, out lo, out hi, out _);
}

static bool TryPosInt(ReadOnlySpan<char> span, out int value)
{
    value = 0;
    if (span.IsEmpty) return false;
    foreach (var ch in span)
    {
        if (ch is < '0' or > '9') return false;
        value = value * 10 + (ch - '0');
    }
    return value > 0;
}

static string ParseStyleOrFallback(string? raw, string fallbackStyle, string scope, string? cmd = null)
{
    if (string.IsNullOrWhiteSpace(raw))
        return fallbackStyle;

    if (TryParseStyle(raw, out var style))
        return style;

    Warn(scope, cmd, $"unknown color/style '{raw}'");
    return fallbackStyle;
}

static void Warn(string scope, string? cmd, string detail)
{
    if (string.IsNullOrWhiteSpace(cmd))
        Console.Error.WriteLine($"Warning: {scope}: {detail}");
    else
        Console.Error.WriteLine($"Warning: {scope} ({cmd}): {detail}");
}

static void WarnStepExitCode(string? name, string cmd, int exitCode)
{
    if (string.IsNullOrWhiteSpace(name))
        Console.Error.WriteLine($"Warning: step exited {exitCode}: {cmd}");
    else
        Console.Error.WriteLine($"Warning: step exited {exitCode} ({name}): {cmd}");
}

static void WriteCast(
    Scenario scenario,
    List<CastEvent> events,
    string outputPath,
    ShellLaunch shell,
    long timestamp,
    ResolvedRenderSettings renderSettings)
{
    using var writer = new StreamWriter(outputPath, append: false, encoding: new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    writer.NewLine = "\n";

    var width = scenario.Width ?? 120;
    var height = scenario.Height ?? 24;
    var title = scenario.Title ?? "";
    var fontSize = renderSettings.FontSize.ToString(CultureInfo.InvariantCulture);
    var fontFamilyTag = RenderSettingsResolver.FontFamilyTagPrefix + renderSettings.FontFamily;
    var tags = new List<string>
    {
        RenderSettingsResolver.FontSizeTagPrefix + fontSize,
        fontFamilyTag,
    };
    if (renderSettings.Window != WindowStyle.None)
        tags.Add(RenderSettingsResolver.WindowTagPrefix + RenderSettingsResolver.WindowToTagValue(renderSettings.Window));
    var tagsJson = string.Join(",", tags.ConvertAll(static tag => JsonEscape.JsonString(tag)));
    writer.WriteLine(
        $"{{\"version\":3,\"term\":{{\"cols\":{width},\"rows\":{height}" +
        $",\"type\":\"xterm-256color\"" +
        $",\"theme\":{{\"fg\":{JsonEscape.JsonString(renderSettings.Theme.Fg)},\"bg\":{JsonEscape.JsonString(renderSettings.Theme.Bg)},\"palette\":{JsonEscape.JsonString(renderSettings.Theme.Palette)}}}}}" +
        $",\"timestamp\":{timestamp},\"title\":{JsonEscape.JsonString(title)}" +
        $",\"env\":{{\"SHELL\":{JsonEscape.JsonString(shell.EnvValue)}}}" +
        $",\"tags\":[{tagsJson}]}}");

    var intervalError = 0.0;
    var previousAbs = 0.0;
    for (var i = 0; i < events.Count; i++)
    {
        var ev = events[i];
        var exact = i == 0 ? ev.Time : ev.Time - previousAbs;
        previousAbs = ev.Time;
        var code = ev.Kind switch
        {
            CastEventKind.Resize => 'r',
            CastEventKind.Marker => 'm',
            _ => 'o',
        };
        writer.Write('[');
        WriteCastInterval(writer, exact, ref intervalError);
        writer.Write(',');
        writer.Write('"');
        writer.Write(code);
        writer.Write("\",");
        if (ev.HasUtf8Output)
            JsonEscape.WriteJsonUtf8String(writer, ev.Utf8Output.Span);
        else
            JsonEscape.WriteJsonString(writer, ev.Data);
        writer.WriteLine(']');
    }

    writer.Write('[');
    WriteCastInterval(writer, events.Count > 0 ? 0.05 : 0, ref intervalError);
    writer.WriteLine(",\"x\",\"0\"]");
}

static void WriteCastInterval(TextWriter writer, double exact, ref double error)
{
    const double scale = 1000.0;
    var scaled = exact * scale + error;
    var quantized = Math.Round(scaled, MidpointRounding.AwayFromZero);
    error = scaled - quantized;
    var seconds = quantized / scale;
    if (seconds == 0)
    {
        writer.Write("0.000");
        return;
    }

    Span<char> buf = stackalloc char[16];
    seconds.TryFormat(buf, out var n, "0.000", CultureInfo.InvariantCulture);
    writer.Write(buf[..n]);
}

static double GetDouble(Dictionary<string, object?> d, double def, params string[] keys)
{
    foreach (var key in keys)
    {
        if (d.TryGetValue(key, out var value) &&
            double.TryParse(value?.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }
    }

    return def;
}

static double? GetOptionalDouble(Dictionary<string, object?> d, double? def, params string[] keys)
{
    foreach (var key in keys)
    {
        if (d.TryGetValue(key, out var value) &&
            double.TryParse(value?.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }
    }

    return def;
}

static bool GetBool(Dictionary<string, object?> d, bool def, params string[] keys)
{
    foreach (var key in keys)
    {
        if (!d.TryGetValue(key, out var value))
            continue;

        if (value is bool b)
            return b;

        if (bool.TryParse(value?.ToString(), out var parsed))
            return parsed;

        Warn(key, null, $"invalid boolean value '{value}'; using {def.ToString().ToLowerInvariant()}");
        return def;
    }

    return def;
}

static ShellLaunch ResolveShell(Scenario scenario)
{
    var requested = scenario.Shell;

    // The OS reads a path or a name only up to a NUL, so a shell with one would start a different file.
    // Windows matches cmd, pwsh, powershell and bash by name alone, so its own checks would not catch it.
    if (requested is not null && requested.Contains('\0'))
        throw new InvalidOperationException($"Shell '{requested}' was requested, but a shell cannot contain a NUL character.");

    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        return ResolveWindowsShell(requested);

    if (string.IsNullOrWhiteSpace(requested))
        requested = Environment.GetEnvironmentVariable("SHELL");

    if (string.IsNullOrWhiteSpace(requested))
        requested = "bash";

    // A shell given by an absolute path that does not exist would fail only at the first command, after
    // pre may have run. A name or a relative path is started as written. A full path would change the
    // shell's $0 and its messages in the recording, and pipe and PTY commands each look a name up by their
    // own rules, so a lookup here could pick a file that neither start would. A path with ".." is left to
    // the start too: File.Exists removes ".." as text, and the OS applies it after the links before it.
    if (Path.IsPathRooted(requested) && !requested.Split('/').Contains("..") && !File.Exists(requested))
        throw new InvalidOperationException($"Shell '{requested}' was requested, but it could not be found.");

    return new ShellLaunch(requested, ["-lc"], requested, requested);
}

static ShellLaunch ResolveWindowsShell(string? requested)
{
    if (string.IsNullOrWhiteSpace(requested))
    {
        if (TryResolveExecutableOnWindows("pwsh", out var pwsh))
            return new ShellLaunch(pwsh, ["-NoLogo", "-NoProfile", "-Command"], pwsh, "pwsh");

        if (TryResolveExecutableOnWindows("powershell", out var powershell))
            return new ShellLaunch(powershell, ["-NoLogo", "-NoProfile", "-Command"], powershell, "powershell");

        throw new InvalidOperationException("No supported shell was found on Windows. Install pwsh or powershell, or set the scenario's shell to a Git Bash or MSYS bash.exe path.");
    }

    if (IsPowerShellName(requested))
    {
        var exeName = NormalizeWindowsShellName(requested);
        if (TryResolveExecutableOnWindows(exeName, out var resolved))
            return new ShellLaunch(resolved, ["-NoLogo", "-NoProfile", "-Command"], resolved, exeName);

        throw new InvalidOperationException($"Shell '{requested}' was requested, but '{exeName}' could not be found on Windows.");
    }

    if (string.Equals(NormalizeWindowsShellName(requested), "cmd", StringComparison.OrdinalIgnoreCase))
    {
        if (TryResolveExecutableOnWindows("cmd", out var resolved))
            return new ShellLaunch(resolved, ["/c"], resolved, "cmd");

        throw new InvalidOperationException("Shell 'cmd' was requested, but cmd.exe could not be found on Windows.");
    }

    if (string.Equals(NormalizeWindowsShellName(requested), "bash", StringComparison.OrdinalIgnoreCase))
    {
        if (TryResolveWindowsBash(out var resolved))
            return new ShellLaunch(resolved, ["-lc"], resolved, "bash");

        throw new InvalidOperationException("Shell 'bash' was requested, but a Git Bash or MSYS bash.exe could not be found. WSL bash is intentionally not used.");
    }

    if (Path.IsPathRooted(requested) || requested.Contains(Path.DirectorySeparatorChar) || requested.Contains(Path.AltDirectorySeparatorChar))
    {
        if (File.Exists(requested))
            return new ShellLaunch(requested, ["-c"], requested, requested);

        throw new InvalidOperationException($"Shell '{requested}' was requested, but the path does not exist.");
    }

    if (TryResolveExecutableOnWindows(requested, out var resolvedExecutable))
        return new ShellLaunch(resolvedExecutable, ["-c"], resolvedExecutable, requested);

    throw new InvalidOperationException($"Shell '{requested}' could not be resolved on Windows.");
}

static bool IsPowerShellName(string shell)
{
    var name = NormalizeWindowsShellName(shell);
    return name is "pwsh" or "powershell";
}

static string NormalizeWindowsShellName(string shell)
    => Path.GetFileNameWithoutExtension(shell.Trim()).ToLowerInvariant();

static bool TryResolveWindowsBash(out string resolved)
{
    var candidateRoots = new[]
    {
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
        Environment.GetEnvironmentVariable("ProgramW6432") ?? string.Empty,
        Environment.GetEnvironmentVariable("ProgramFiles") ?? string.Empty,
        Environment.GetEnvironmentVariable("ProgramFiles(x86)") ?? string.Empty,
        Environment.GetEnvironmentVariable("LOCALAPPDATA") ?? string.Empty,
        Environment.GetEnvironmentVariable("ProgramData") ?? string.Empty,
    };

    var candidatePaths = new[]
    {
        @"Git\bin\bash.exe",
        @"Git\usr\bin\bash.exe",
        @"msys64\usr\bin\bash.exe",
        @"GitHub\Desktop\bin\bash.exe",
    };

    foreach (var root in candidateRoots.Where(static r => !string.IsNullOrWhiteSpace(r)))
    {
        foreach (var suffix in candidatePaths)
        {
            var candidate = Path.Combine(root, suffix);
            if (File.Exists(candidate))
            {
                resolved = candidate;
                return true;
            }
        }
    }

    if (TryResolveExecutableOnWindows("bash.exe", out resolved) && !IsWslBashPath(resolved))
        return true;

    resolved = string.Empty;
    return false;
}

static bool IsWslBashPath(string path)
    => path.Contains(@"\Windows\System32\bash.exe", StringComparison.OrdinalIgnoreCase)
       || path.Contains(@"\WindowsApps\", StringComparison.OrdinalIgnoreCase);

static bool TryResolveExecutableOnWindows(string executableName, out string resolved)
{
    if (File.Exists(executableName))
    {
        resolved = Path.GetFullPath(executableName);
        return true;
    }

    var pathEnv = Environment.GetEnvironmentVariable("PATH");
    if (!string.IsNullOrWhiteSpace(pathEnv))
    {
        var paths = pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var pathext = (Environment.GetEnvironmentVariable("PATHEXT") ?? ".EXE;.CMD;.BAT;.COM")
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var directory in paths)
        {
            if (Path.HasExtension(executableName))
            {
                var candidate = Path.Combine(directory, executableName);
                if (File.Exists(candidate))
                {
                    resolved = candidate;
                    return true;
                }
                continue;
            }

            foreach (var extension in pathext)
            {
                var candidate = Path.Combine(directory, executableName + extension);
                if (File.Exists(candidate))
                {
                    resolved = candidate;
                    return true;
                }
            }
        }
    }

    resolved = string.Empty;
    return false;
}

static int ComputeDeterministicSeed(string yaml)
{
    var normalized = yaml.Replace("\r\n", "\n").Replace("\r", "\n");
    var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
    return BitConverter.ToInt32(bytes, 0);
}

static long ComputeDeterministicTimestamp(int seed)
{
    const long baseUnixTime = 1700000000;
    const long spanSeconds = 365L * 24 * 60 * 60;
    return baseUnixTime + (uint)seed % spanSeconds;
}

static string CreateInitialScenarioYaml()
{
    return $"""
    # scenetake starter scenario. Edit the values below and add commands under steps.
    title: "My App"            # Optional cast title
    # width: 120                 # Default: 120
    # height: 24                 # Default: 24
    # cwd: /your/path            # Optional working directory for all steps
    # shell: bash                # Optional shell override: bash, pwsh, powershell, or a path

    # Optional SVG rendering metadata. Written to the cast header (v3 tags / term.theme) and used by --format svg.
    # render:
    #   font-size: 16
    #   font-family: '"JetBrains Mono", Consolas, monospace'
    #   theme:
    #     preset: dark
    #     fg: "#d0d0d0"
    #     bg: "#282c34"

    # Default settings for all steps. Can be overridden per step by using a mapping with "run" and timing keys.
    settings:
      prompt: "{DefaultPrompt}"             # Default prompt shown before each command.
      typing-speed: {DefaultSpeed}       # Seconds per character on average.
      typing-jitter: {DefaultJitter}     # Random typing variance (+/- seconds).
      pre-delay: {DefaultPreDelay}           # Pause before typing each step.
      post-delay: {DefaultPostDelay}          # Pause after output before the next step.
      execution-duration: {DefaultExecutionDuration} # Optional cast wait after command execution.
      stderr-color: {DefaultStderrColorSpec}       # Applied only when stderr has no ANSI SGR.
      # stream-output: false         # When true, replay captured chunk times in the cast.
      # max-duration: 15             # Cap step output timeline; also enables stream emit.
      # silence-threshold: {DefaultSilenceThreshold}       # Stream mode: collapse idle gaps longer than this (seconds).
      # max-idle: {DefaultMaxIdle}                # Stream mode: collapsed idle gap duration (seconds).

    # Optional setup commands. They run before steps, but are not recorded in the cast.
    # pre:
    #   - echo "run before steps"

    # Add one command per step. Use a mapping when you want to override per-command timing.
    steps:
      - echo "Hello, World!"
      - run: pwd
        post-delay: 1.5
      - name: "[cyan]Styled command typing + output highlight"
        run: printf 'line1 alpha\nline2 beta gamma\nline3\nline4 delta\n'
        run-highlight: "bold fg:bright-cyan"
        highlight:
          - at: "1"
            color: "fg:bright-green"
          - at: "2:7-10"
            color: "underline fg:yellow"
          - at: "2:12-"
            color: "bold fg:magenta"
          - at: "3"
            color: "fg:white bg:212"
          - at: "4"
            color: "fg:black bg:114"
      - name: "[bright-yellow]stderr color override"
        run: echo "stderr sample" 1>&2
        stderr-color: "bold fg:red"
      - name: "run in a PTY"
        run: echo "Running in a PTY, which is useful for TUI app and tty-based output"
        pty: true

    # Optional teardown commands. They run last and are not recorded in the cast.
    # post:
    #   - echo "run after steps"

    """;
}

static void PrintUsage()
{
    Console.Error.WriteLine("Usage: scenetake [--verbose] [--format cast|svg|mp4] [--font-size N] [--font-family FAMILIES] [--theme dark|light] [--window none|macos|windows] [--max-fps N] [--fps N] [--crf N] [--max-size WxH] [--font-file PATH] <scenario.yaml> [output]");
    Console.Error.WriteLine("       scenetake svg [--font-size N] [--font-family FAMILIES] [--theme dark|light] [--window none|macos|windows] [--max-fps N] <input.cast> [output.svg]");
    Console.Error.WriteLine("       scenetake mp4 [--font-size N] [--font-family FAMILIES] [--font-file PATH] [--theme dark|light] [--window none|macos|windows] [--fps N] [--crf N] [--max-size WxH] <input.cast> [output.mp4]");
    Console.Error.WriteLine("       scenetake init [scenario.yaml]");
    Console.Error.WriteLine("       scenetake --help");
}

static void PrintMp4Usage()
{
    Console.Error.WriteLine("Usage: scenetake mp4 [--font-size N] [--font-family FAMILIES] [--font-file PATH] [--theme dark|light] [--window none|macos|windows] [--fps N] [--crf N] [--max-size WxH] <input.cast> [output.mp4]");
    Console.Error.WriteLine("Converts an existing asciinema v2/v3 cast file to H.264 mp4. Requires ffmpeg with libx264 on PATH,");
    Console.Error.WriteLine($"or set {Ffmpeg.PathVariable} to its path. --max-size 1280x1024 fits X (Twitter) uploads.");
}

static void PrintMp4Written(string path, Mp4Result result) =>
    Console.Error.WriteLine(
        $"Written: {FormatDisplayPath(path)}  ({result.Width}x{result.Height}, {result.Fps}fps, {result.Frames} frames, {FormatBytes(result.Bytes)}, font: {result.Font.Family})");

// Decimal units (1MB = 1,000,000 bytes), the same as the SVG size warning. Sizes just under 1MB
// that round up to "1000.0KB" print as 1.0MB instead.
static string FormatBytes(long bytes)
{
    var kilobytes = string.Create(CultureInfo.InvariantCulture, $"{bytes / 1_000.0:0.0}");
    return bytes >= 1_000_000 || kilobytes == "1000.0"
        ? string.Create(CultureInfo.InvariantCulture, $"{bytes / 1_000_000.0:0.0}MB")
        : kilobytes + "KB";
}

/// <summary>
/// Checks that each output can be written: it is not a directory, it is not the input file, and, with
/// <paramref name="requireFolder"/>, its folder exists. A failed render deletes its output, so writing
/// over the input would lose it.
/// </summary>
static bool TryCheckOutputPaths(string inputPath, IEnumerable<string?> outputPaths, out string error, bool requireFolder = true)
{
    foreach (var outputPath in outputPaths)
    {
        if (outputPath is null)
            continue;

        var directory = Path.GetDirectoryName(Path.GetFullPath(outputPath)) ?? ".";
        if (requireFolder && !Directory.Exists(directory))
        {
            error = $"output folder does not exist: {FormatDisplayPath(directory)}";
            return false;
        }

        if (Directory.Exists(outputPath))
        {
            error = $"output path is a directory: {FormatDisplayPath(outputPath)}";
            return false;
        }

        if (IsSamePath(outputPath, inputPath))
        {
            error = $"the output would replace the input file: {FormatDisplayPath(inputPath)} (pass another output path)";
            return false;
        }
    }

    error = "";
    return true;
}

/// <summary>
/// Whether two paths name the same file. Paths are compared ignoring case, because a volume can ignore
/// case on any OS. On Windows two existing files with the same volume serial number and file index are
/// the same file too, which catches symbolic links, junctions, hard links, mapped and subst drives, and
/// \\?\, \??\ and UNC spellings. On a volume that reports serial number 0, the final paths Windows gives
/// the two open files are compared instead, which still catches links. Elsewhere links on every part of
/// each path are followed first, because a symbolic link reaches the same file under another path. A hard
/// link is not detected there.
/// </summary>
static bool IsSamePath(string left, string right) =>
    OperatingSystem.IsWindows()
        // Windows links are not walked by hand. Windows resolves a relative link target against the path as
        // it was opened, not the folder a junction leads to, so a walk could name another file. The file
        // index and the final path come from Windows itself.
        ? string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase)
            || (WindowsFileId(left) is { } a && WindowsFileId(right) is { } b && IsSameWindowsFile(a, b))
        : string.Equals(FollowLinks(left), FollowLinks(right), StringComparison.OrdinalIgnoreCase);

static bool IsSameWindowsFile((uint Volume, ulong Index, uint Links, string? FinalPath) left, (uint Volume, ulong Index, uint Links, string? FinalPath) right)
{
    if (left.Volume != 0)
        return left.Volume == right.Volume && left.Index == right.Index;

    // A volume with serial number 0 can repeat an index across the file systems it serves, so the index
    // alone is not enough. A final path that could not be read proves nothing. Two names in one folder
    // with the same index are a hard link, which both report as more than one link. A bind mount can put
    // files of two file systems in one folder, and a single-link file there is never a hard link.
    if (SharedName(left.FinalPath) is not { } leftPath || SharedName(right.FinalPath) is not { } rightPath)
        return false;
    return string.Equals(leftPath, rightPath, StringComparison.OrdinalIgnoreCase)
        || (left.Index == right.Index && left.Links > 1 && right.Links > 1
            && string.Equals(Path.GetDirectoryName(leftPath), Path.GetDirectoryName(rightPath), StringComparison.OrdinalIgnoreCase));

    // WSL serves the same files under two server names, and the final path keeps the one the path was
    // opened with.
    static string? SharedName(string? finalPath)
    {
        const string alias = @"\\?\UNC\wsl$\";
        return finalPath is not null && finalPath.StartsWith(alias, StringComparison.OrdinalIgnoreCase)
            ? @"\\?\UNC\wsl.localhost\" + finalPath[alias.Length..]
            : finalPath;
    }
}

/// <summary>The full path with every link on the way replaced by its target.</summary>
static string FollowLinks(string path)
{
    // One count for the whole walk, not a depth per link. A target may pass through earlier links again,
    // and a depth limit alone lets that double the work at every level. Linux follows at most 40 links
    // in one path, so a path that needs more names no file there.
    var linksLeft = 40;
    return FollowLinksWithin(Path.GetFullPath(path), ref linksLeft);
}

static string FollowLinksWithin(string full, ref int linksLeft)
{
    var root = Path.GetPathRoot(full) ?? "";
    var current = root;
    foreach (var part in full[root.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
    {
        // A link's target is used as the link wrote it, so it may hold "..". Unix applies ".." to the folder
        // reached through the links before it. Path.GetFullPath would remove it as text.
        if (part == ".")
            continue;
        if (part == "..")
        {
            current = Path.GetDirectoryName(current) ?? root;
            continue;
        }

        current = Path.Combine(current, part);
        try
        {
            FileSystemInfo info = Directory.Exists(current) ? new DirectoryInfo(current) : new FileInfo(current);
            if (info.LinkTarget is { } target && linksLeft-- > 0)
            {
                // The target may be relative to the link's folder, and may pass through another link,
                // such as macOS /var to /private/var, so it is walked again.
                current = FollowLinksWithin(Path.Combine(Path.GetDirectoryName(current) ?? root, target), ref linksLeft);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A link that cannot be followed is compared as it is written.
        }
    }

    return current;
}

/// <summary>The volume serial number, file index and final path of an existing file, or null when the file cannot be opened.</summary>
static unsafe (uint Volume, ulong Index, uint Links, string? FinalPath)? WindowsFileId(string path)
{
    // A handle that asks for no access opens even when another process denies sharing, which a read handle
    // would not. The \\?\ form is not limited to 260 characters. A path already in a device form is used
    // as it is, since a second prefix would make it invalid.
    const uint FileShareReadWriteDelete = 7, OpenExisting = 3;
    var full = Path.GetFullPath(path);
    var name = full.StartsWith(@"\\?\", StringComparison.Ordinal) || full.StartsWith(@"\\.\", StringComparison.Ordinal) || full.StartsWith(@"\??\", StringComparison.Ordinal) ? full
        : full.StartsWith(@"\\", StringComparison.Ordinal) ? @"\\?\UNC\" + full[2..]
        : @"\\?\" + full;
    using var handle = CreateFileW(name, 0, FileShareReadWriteDelete, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);
    if (handle.IsInvalid || !GetFileInformationByHandle(handle, out var info))
        return null;

    // Some network file systems report serial number 0 for every file, and repeat indexes across the
    // volumes they serve, so the caller compares the final path there. A path that does not fit is
    // asked for again with the length Windows returned, which counts the terminating null.
    string? finalPath = null;
    var buffer = new char[1024];
    for (var attempt = 0; attempt < 2 && finalPath is null; attempt++)
    {
        uint length;
        fixed (char* chars = buffer)
            length = GetFinalPathNameByHandleW(handle, chars, (uint)buffer.Length, 0);
        if (length == 0)
            break;
        if (length < buffer.Length)
            finalPath = new string(buffer, 0, (int)length);
        else
            buffer = new char[length];
    }

    return (info.VolumeSerialNumber, ((ulong)info.FileIndexHigh << 32) | info.FileIndexLow, info.NumberOfLinks, finalPath);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, ExactSpelling = true)]
    static extern Microsoft.Win32.SafeHandles.SafeFileHandle CreateFileW(string fileName, uint desiredAccess, uint shareMode, IntPtr securityAttributes, uint creationDisposition, uint flagsAndAttributes, IntPtr templateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool GetFileInformationByHandle(Microsoft.Win32.SafeHandles.SafeFileHandle file, out WindowsFileInformation information);

    [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
    static extern uint GetFinalPathNameByHandleW(Microsoft.Win32.SafeHandles.SafeFileHandle file, char* path, uint length, uint flags);
}

static void DeleteFileIfExists(string path)
{
    try
    {
        if (File.Exists(path))
            File.Delete(path);
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
    {
        // Keep the original error. A file that cannot be removed stays as it was.
    }
}

/// <summary>Matches <paramref name="name"/> as <c>--name value</c> or <c>--name=value</c>. Returns true when it matched, with error set when the value is missing.</summary>
static bool TryTakeOptionValue(string[] args, ref int i, string name, out string value, out string error)
{
    value = "";
    error = "";
    var arg = args[i];
    if (arg == name)
    {
        // "--font-file --fps 30" means the value was left out, not a font file named "--fps".
        if (i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal))
            error = $"{name} requires a value";
        else
            value = args[++i];
        return true;
    }

    if (arg.StartsWith(name + "=", StringComparison.Ordinal))
    {
        value = arg[(name.Length + 1)..];
        if (value.Length == 0)
            error = $"{name} requires a value";
        return true;
    }

    return false;
}

static bool TryConsumeMp4Arg(string[] args, ref int i, Mp4Options options, out string error)
{
    var arg = args[i];
    if (TryTakeOptionValue(args, ref i, "--fps", out var value, out error))
    {
        if (error.Length == 0 && options.Fps.HasValue)
            error = "duplicate option: --fps";
        else if (error.Length == 0 && Mp4Settings.TryParseFps(value, out var fps, out error))
            options.Fps = fps;
    }
    else if (TryTakeOptionValue(args, ref i, "--crf", out value, out error))
    {
        if (error.Length == 0 && options.Crf.HasValue)
            error = "duplicate option: --crf";
        else if (error.Length == 0 && Mp4Settings.TryParseCrf(value, out var crf, out error))
            options.Crf = crf;
    }
    else if (TryTakeOptionValue(args, ref i, "--max-size", out value, out error))
    {
        if (error.Length == 0 && options.MaxSize.HasValue)
            error = "duplicate option: --max-size";
        else if (error.Length == 0 && Mp4Settings.TryParseMaxSize(value, out var maxWidth, out var maxHeight, out error))
            options.MaxSize = (maxWidth, maxHeight);
    }
    else if (TryTakeOptionValue(args, ref i, "--font-file", out value, out error))
    {
        if (error.Length == 0 && options.FontFile is not null)
            error = "duplicate option: --font-file";
        else if (error.Length == 0)
            options.FontFile = Path.GetFullPath(value);
    }
    else
    {
        return false;
    }

    options.FirstOption ??= arg.Split('=')[0];
    return true;
}

static bool IsMaxFpsArg(string arg) => arg == "--max-fps" || arg.StartsWith("--max-fps=", StringComparison.Ordinal);

static bool TryParseMp4Args(
    string[] args,
    out string castPath,
    out string? outputPath,
    out int? fontSizeOverride,
    out string? fontFamilyOverride,
    out string? themePresetOverride,
    out WindowStyle? windowOverride,
    out Mp4Options mp4Options,
    out string error)
{
    castPath = "";
    outputPath = null;
    fontSizeOverride = null;
    fontFamilyOverride = null;
    themePresetOverride = null;
    windowOverride = null;
    mp4Options = new Mp4Options();
    error = "";

    for (var i = 1; i < args.Length; i++)
    {
        var arg = args[i];
        if (TryConsumeFontSizeArg(args, ref i, ref fontSizeOverride, out error)
            || TryConsumeFontFamilyArg(args, ref i, ref fontFamilyOverride, out error)
            || TryConsumeThemeArg(args, ref i, ref themePresetOverride, out error)
            || TryConsumeWindowArg(args, ref i, ref windowOverride, out error)
            || TryConsumeMp4Arg(args, ref i, mp4Options, out error))
        {
            if (error.Length != 0)
                return false;
            continue;
        }

        if (IsMaxFpsArg(arg))
        {
            error = "--max-fps applies to SVG; use --fps for mp4";
            return false;
        }

        if (arg.StartsWith('-'))
        {
            error = $"unknown option: {arg}";
            return false;
        }

        if (castPath.Length == 0)
        {
            castPath = arg;
            continue;
        }

        if (outputPath is null)
        {
            outputPath = arg;
            continue;
        }

        error = $"unexpected argument: {arg}";
        return false;
    }

    if (castPath.Length != 0)
        return true;

    error = "cast path is required";
    return false;
}

static void PrintInitUsage()
{
    Console.Error.WriteLine("Usage: scenetake init [scenario.yaml]");
    Console.Error.WriteLine("Creates a commented starter YAML scenario file.");
}

static void PrintSvgUsage()
{
    Console.Error.WriteLine("Usage: scenetake svg [--font-size N] [--font-family FAMILIES] [--theme dark|light] [--window none|macos|windows] [--max-fps N] <input.cast> [output.svg]");
    Console.Error.WriteLine("Converts an existing asciinema v2/v3 cast file to animated SVG.");
}

static void PrintVersion()
{
    Console.WriteLine(GetAppVersion());
}

static string GetAppVersion()
{
    var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
    return version?.ToString(3) ?? "0.0.0";
}

/// <summary>The BY_HANDLE_FILE_INFORMATION that GetFileInformationByHandle fills. Each FILETIME is two 32-bit halves, so the 64-bit fields are packed to 4 bytes.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
internal struct WindowsFileInformation
{
    public uint FileAttributes;
    public long CreationTime;
    public long LastAccessTime;
    public long LastWriteTime;
    public uint VolumeSerialNumber;
    public uint FileSizeHigh;
    public uint FileSizeLow;
    public uint NumberOfLinks;
    public uint FileIndexHigh;
    public uint FileIndexLow;
}

enum OutputFormat
{
    Cast,
    Svg,
    Mp4,
}

/// <summary>mp4 options from the command line. Unset values take the <see cref="Mp4Settings"/> defaults.</summary>
sealed class Mp4Options
{
    internal int? Fps { get; set; }
    internal int? Crf { get; set; }
    internal (int Width, int Height)? MaxSize { get; set; }
    internal string? FontFile { get; set; }

    /// <summary>The first mp4 option given, for errors when mp4 output is not selected.</summary>
    internal string? FirstOption { get; set; }

    internal Mp4Settings ToSettings() => new(
        Fps ?? Mp4Settings.DefaultFps,
        Crf ?? Mp4Settings.DefaultCrf,
        MaxSize?.Width ?? 0,
        MaxSize?.Height ?? 0,
        FontFile);
}

internal static class PtyCastTiming
{
    internal static double ComputeStartupOffset(double firstEmittedChunkSeconds, double executionDuration) =>
        Math.Max(0.0, firstEmittedChunkSeconds - Math.Max(0.0, executionDuration));

    internal static double AdjustChunkSeconds(double chunkSeconds, double startupOffset) =>
        chunkSeconds - startupOffset;
}

static class TypingChars
{
    private static readonly string[] Cache = CreateCache();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string Get(char c) => (uint)c < 128 ? Cache[c] : c.ToString();

    private static string[] CreateCache()
    {
        var cache = new string[128];
        for (var i = 0; i < cache.Length; i++)
            cache[i] = ((char)i).ToString();
        return cache;
    }
}

record HighlightSpec(string ColorOpen, List<string> At);

/// <summary>
/// Records host keyboard bytes forwarded by <see cref="PtyConsoleInput.Attach"/> during an interactive PTY step.
/// </summary>
sealed class InteractivePtyInputRecorder(Stopwatch stopwatch, List<PtyInputChunk> chunks) : IPtyConsoleInputObserver
{
    public void OnForwardedInput(TimeSpan elapsed, ReadOnlySpan<byte> data)
    {
        _ = elapsed;
        if (data.IsEmpty)
            return;

        chunks.Add(new PtyInputChunk(stopwatch.Elapsed, data.ToArray().AsMemory()));
    }
}

/// <summary>
/// Timestamped host→PTY input bytes captured during an interactive step (same clock as <see cref="PtyCaptureChunk"/> output).
/// </summary>
readonly record struct PtyInputChunk(TimeSpan Time, ReadOnlyMemory<byte> Data);

readonly record struct CommandExecution(
    int ExitCode,
    bool IsPty,
    string Output,
    string Stderr,
    IReadOnlyList<PtyCaptureChunk>? PtyByteChunks,
    IReadOnlyList<PtyInputChunk>? PtyInputChunks,
    IReadOnlyList<PipeStreamChunk>? PipeStreamChunks)
{
    public static CommandExecution FromCapture(PtyCaptureResult result) =>
        new(result.ExitCode, true, "", "", result.Chunks, null, null);

    public static CommandExecution FromInteractivePty(
        int exitCode,
        IReadOnlyList<PtyCaptureChunk> outputChunks,
        IReadOnlyList<PtyInputChunk> inputChunks) =>
        new(exitCode, true, "", "", outputChunks, inputChunks, null);

    public static CommandExecution FromPipeStream(
        string stdout,
        string stderr,
        int exitCode,
        IReadOnlyList<PipeStreamChunk> chunks) =>
        new(exitCode, false, stdout, stderr, null, null, chunks);

    public static CommandExecution FromPipe(string stdout, string stderr, int exitCode) =>
        new(exitCode, false, stdout, stderr, null, null, []);
}

record ShellLaunch(string FileName, string[] Arguments, string EnvValue, string DisplayName);

[YamlObject(NamingConvention.KebabCase)]
public partial class Scenario
{
    public string? Title { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public string? Cwd { get; set; }
    public string? Shell { get; set; }
    public ScenarioSettings? Settings { get; set; }
    public ScenarioRender? Render { get; set; }
    public List<string>? Pre { get; set; }
    public List<object?>? Steps { get; set; }
    public List<string>? Post { get; set; }
}

[YamlObject(NamingConvention.KebabCase)]
public partial class ScenarioSettings
{
    public string? Prompt { get; set; }
    public double? TypingSpeed { get; set; }
    public double? TypingJitter { get; set; }
    public double? PreDelay { get; set; }
    public double? PostDelay { get; set; }
    public double? ExecutionDuration { get; set; }
    public double? SilenceThreshold { get; set; }
    public double? MaxIdle { get; set; }
    public double? MaxDuration { get; set; }
    public bool? StreamOutput { get; set; }
    public string? StderrColor { get; set; }
}

[YamlObject(NamingConvention.KebabCase)]
public partial class ScenarioRender
{
    public int? FontSize { get; set; }
    public string? FontFamily { get; set; }
    public string? Window { get; set; }
    public ScenarioTheme? Theme { get; set; }
}

[YamlObject(NamingConvention.KebabCase)]
public partial class ScenarioTheme
{
    public string? Preset { get; set; }
    public string? Fg { get; set; }
    public string? Bg { get; set; }
    public string? Palette { get; set; }
}

public class CommandEntry
{
    public string Cmd { get; set; } = "";
    public string? Name { get; set; }
    public Dictionary<string, object?> Extra { get; set; } = new();
}
