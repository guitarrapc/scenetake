#:sdk Microsoft.NET.Sdk
#:property TargetFramework=net10.0
#:property Nullable=enable
#:property ImplicitUsings=enable
#:property AllowUnsafeBlocks=true
#:property InvariantGlobalization=true
#:property OptimizationPreference=Size
#:property IlcFoldIdenticalMethodBodies=true
#:package VYaml@1.6.1
#:package MiniPty@1.7.0
#:package MiniPty.Capture@1.7.0
#:package MiniPty.Console@1.7.0
#:package StbTrueTypeSharp@1.26.13
#:include Terminal.cs
#:include Svg.cs
#:include Raster.cs
#:include Mp4.cs
#:include Png.cs
#:include Gif.cs
#:include CastReader.cs
#:include JsonEscape.cs
#:include PtyLeadingInitFilter.cs
#:include CastTimingPipeline.cs
#:include PipeStreamCapture.cs
#:include InteractivePtyInput.cs
#:include KeyRecording.cs
#:include OutputWait.cs
#:include Kernel32.cs
#:include ScenarioKeys.cs
#:include Scenario.cs
#:include Highlight.cs
#:include CastComposer.cs
#:include SecretMask.cs
#:include StepChunks.cs
#:include StepOutputs.cs
#:include ShellState.cs
#:include BackgroundSteps.cs
#:include Rerecord.cs

using MiniPty;
using MiniPty.Capture;
using MiniPty.Console;
using System.Diagnostics;
using System.Globalization;
using System.Buffers;
using System.Runtime.InteropServices;
using System.Text;
using static ScenarioCast;

// On Windows a pipe child gets a console of its own, whose code page is the OEM code page (932 on Japanese
// Windows). pwsh writes redirected output in that code page, so a character it lacks, such as an emoji,
// became "?", and native commands started from it (cmd's echo, ping) wrote that code page too. Setting the
// console's output encoding to UTF-8 first makes pwsh and those commands write UTF-8, which the pipe reader
// decodes. It runs in the child's own console, never in the terminal that started scenetake, and it is not
// part of the typed command or the logs. A failure, which only a child with no console could have, is ignored.
const string PowerShellUtf8OutputPrefix = "try { [Console]::OutputEncoding = [System.Text.UTF8Encoding]::new() } catch { }; ";
const string InteractiveNeedsTerminalError = "Interactive PTY steps require a real terminal (stdin and stdout must not be redirected).";

if (args.Length < 1)
{
    PrintUsage();
    return 1;
}

if (args[0] is "init")
{
    if (args.Length == 2 && args[1] is "-h" or "--help")
    {
        PrintInitHelp();
        return 0;
    }

    if (!TryParseInitArgs(args, out var initPathArg, out var initShell, out var initError))
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
    File.WriteAllText(initPath, CreateInitialScenarioYaml(initShell), Encoding.UTF8);
    Console.Error.WriteLine($"Created: {FormatDisplayPath(initPath)}");
    return 0;
}

if (args[0] is "svg")
{
    if (args.Length == 2 && args[1] is "-h" or "--help")
    {
        PrintSvgHelp();
        return 0;
    }

    if (!TryParseSvgArgs(args, out var castArg, out var svgOutputArg, out var svgFontSize, out var svgFontFamily, out var svgThemePreset, out var svgWindow, out var svgMaxFps, out var svgStyle, out var svgStillAt, out var svgError))
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

    if (ScenarioGivenForCast(castPath, "svg") is { } svgScenarioError)
    {
        Console.Error.WriteLine($"Error: {svgScenarioError}");
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

        svgRenderSettings = svgStyle.ToOverrides().Apply(svgRenderSettings);
        if (FindCropError(recording, svgRenderSettings) is { } svgCropError)
        {
            Console.Error.WriteLine($"Error: {svgCropError}");
            return 1;
        }

        foreach (var note in RenderSettingsResolver.FindIneffectiveStyle(svgRenderSettings, svg: svgStillAt is null))
            Console.Error.WriteLine($"Warning: {note}");
        SvgRender.WriteSvg(recording.Events, recording.Width, recording.Height, svgRenderSettings, svgOutputPath, recording.LoopDuration, svgStillAt);
        PrintSvgWritten(svgOutputPath, recording, svgRenderSettings, svgStillAt);
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
        PrintMp4Help();
        return 0;
    }

    if (!TryParseMp4Args(args, out var mp4CastArg, out var mp4OutputArg, out var mp4FontSize, out var mp4FontFamily, out var mp4ThemePreset, out var mp4Window, out var mp4Options, out var mp4Style, out var mp4Error))
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

    if (ScenarioGivenForCast(mp4CastPath, "mp4") is { } mp4ScenarioError)
    {
        Console.Error.WriteLine($"Error: {mp4ScenarioError}");
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

        mp4RenderSettings = mp4Style.ToOverrides().Apply(mp4RenderSettings);
        if (FindCropError(recording, mp4RenderSettings) is { } mp4CropError)
        {
            Console.Error.WriteLine($"Error: {mp4CropError}");
            return 1;
        }

        foreach (var note in RenderSettingsResolver.FindIneffectiveStyle(mp4RenderSettings, svg: false))
            Console.Error.WriteLine($"Warning: {note}");
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
        Console.Error.WriteLine(ex switch
        {
            CastReadException => $"Error: {ex.Message}",
            FontNotFoundException or FrameTooLargeException => $"Error: {ex.Message}",
            _ => $"Error: mp4 render failed: {ex.Message}",
        });
        return 1;
    }
}

if (args[0] is "gif")
{
    if (args.Length == 2 && args[1] is "-h" or "--help")
    {
        PrintGifHelp();
        return 0;
    }

    if (!TryParseGifArgs(args, out var gifCastArg, out var gifOutputArg, out var gifFontSize, out var gifFontFamily, out var gifThemePreset, out var gifWindow, out var gifOptions, out var gifStyle, out var gifError))
    {
        Console.Error.WriteLine($"Error: {gifError}");
        PrintGifUsage();
        return 1;
    }

    var gifCastPath = Path.GetFullPath(gifCastArg);
    if (!File.Exists(gifCastPath))
    {
        Console.Error.WriteLine($"Error: {FormatDisplayPath(gifCastPath)} not found");
        return 1;
    }

    if (ScenarioGivenForCast(gifCastPath, "gif") is { } gifScenarioError)
    {
        Console.Error.WriteLine($"Error: {gifScenarioError}");
        return 1;
    }

    var gifOutputPath = RenderSettingsResolver.ResolveCastGifOutputPath(gifCastPath, gifOutputArg);
    if (!TryCheckOutputPaths(gifCastPath, [gifOutputPath], out var gifOutputError))
    {
        Console.Error.WriteLine($"Error: {gifOutputError}");
        return 1;
    }

    if (!Ffmpeg.TryResolveForGif(out var gifFfmpeg, out var gifFfmpegError))
    {
        Console.Error.WriteLine($"Error: {gifFfmpegError}");
        return 1;
    }

    Console.Error.WriteLine($"Loading: {FormatDisplayPath(gifCastPath)}");
    try
    {
        var recording = CastReader.Read(gifCastPath);
        var gifRenderSettings = RenderSettingsResolver.ApplySvgOverrides(
            recording.RenderSettings,
            gifFontSize,
            gifFontFamily,
            gifThemePreset,
            gifWindow,
            maxFpsOverride: null,
            out var gifOverrideError);
        if (gifOverrideError.Length != 0)
        {
            Console.Error.WriteLine($"Error: {gifOverrideError}");
            return 1;
        }

        gifRenderSettings = gifStyle.ToOverrides().Apply(gifRenderSettings);
        if (FindCropError(recording, gifRenderSettings) is { } gifCropError)
        {
            Console.Error.WriteLine($"Error: {gifCropError}");
            return 1;
        }

        foreach (var note in RenderSettingsResolver.FindIneffectiveStyle(gifRenderSettings, svg: false, gif: true))
            Console.Error.WriteLine($"Warning: {note}");
        var result = GifRender.WriteGif(recording.Events, recording.Width, recording.Height, gifRenderSettings, gifOptions.ToGifSettings(), gifOutputPath, gifFfmpeg, recording.LoopDuration);
        PrintGifWritten(gifOutputPath, result, fromCast: true);
        Console.Error.WriteLine($"Done: {FormatDisplayPath(gifOutputPath)}");
        return 0;
    }
    catch (Exception ex)
    {
        // As for mp4: a cast that cannot be read removes a GIF from an earlier run, and font and frame-size errors,
        // found before anything is drawn, leave it as it was. WriteGif removes it once encoding starts.
        if (ex is not (FontNotFoundException or FrameTooLargeException))
            DeleteFileIfExists(gifOutputPath);
        Console.Error.WriteLine(ex switch
        {
            CastReadException or FontNotFoundException or FrameTooLargeException => $"Error: {ex.Message}",
            _ => $"Error: GIF render failed: {ex.Message}",
        });
        return 1;
    }
}

if (args[0] is "png")
{
    if (args.Length == 2 && args[1] is "-h" or "--help")
    {
        PrintPngHelp();
        return 0;
    }

    if (!TryParsePngArgs(args, out var pngCastArg, out var pngOutputArg, out var pngFontSize, out var pngFontFamily, out var pngThemePreset, out var pngWindow, out var pngFontFile, out var pngStyle, out var pngStillAt, out var pngError))
    {
        Console.Error.WriteLine($"Error: {pngError}");
        PrintPngUsage();
        return 1;
    }

    var pngCastPath = Path.GetFullPath(pngCastArg);
    if (!File.Exists(pngCastPath))
    {
        Console.Error.WriteLine($"Error: {FormatDisplayPath(pngCastPath)} not found");
        return 1;
    }

    if (ScenarioGivenForCast(pngCastPath, "png") is { } pngScenarioError)
    {
        Console.Error.WriteLine($"Error: {pngScenarioError}");
        return 1;
    }

    var pngOutputPath = RenderSettingsResolver.ResolveCastPngOutputPath(pngCastPath, pngOutputArg);
    if (!TryCheckOutputPaths(pngCastPath, [pngOutputPath], out var pngOutputError))
    {
        Console.Error.WriteLine($"Error: {pngOutputError}");
        return 1;
    }

    // A cast keeps background colors only, so an image comes from --background, and decoding it needs ffmpeg.
    string? pngFfmpeg = null;
    if (pngStyle.Background?.ImagePath is not null)
    {
        if (!Ffmpeg.TryResolveImageDecoder(out var imageDecoder, out var decoderError))
        {
            Console.Error.WriteLine($"Error: {decoderError}");
            return 1;
        }

        pngFfmpeg = imageDecoder;
    }

    Console.Error.WriteLine($"Loading: {FormatDisplayPath(pngCastPath)}");
    try
    {
        var recording = CastReader.Read(pngCastPath);
        var pngRenderSettings = RenderSettingsResolver.ApplySvgOverrides(
            recording.RenderSettings,
            pngFontSize,
            pngFontFamily,
            pngThemePreset,
            pngWindow,
            maxFpsOverride: null,
            out var pngOverrideError);
        if (pngOverrideError.Length != 0)
        {
            Console.Error.WriteLine($"Error: {pngOverrideError}");
            return 1;
        }

        pngRenderSettings = pngStyle.ToOverrides().Apply(pngRenderSettings);
        if (FindCropError(recording, pngRenderSettings) is { } pngCropError)
        {
            Console.Error.WriteLine($"Error: {pngCropError}");
            return 1;
        }

        foreach (var note in RenderSettingsResolver.FindIneffectiveStyle(pngRenderSettings, svg: false))
            Console.Error.WriteLine($"Warning: {note}");
        var at = pngStillAt ?? double.PositiveInfinity;
        var result = PngRender.WritePng(recording.Events, recording.Width, recording.Height, pngRenderSettings, at, pngOutputPath, pngFontFile, pngFfmpeg);
        PrintPngWritten(pngOutputPath, result, at);
        Console.Error.WriteLine($"Done: {FormatDisplayPath(pngOutputPath)}");
        return 0;
    }
    catch (Exception ex)
    {
        // As for mp4: a cast that cannot be read removes a PNG from an earlier run, and font and frame-size errors,
        // found before anything is drawn, leave it as it was.
        if (ex is not (FontNotFoundException or FrameTooLargeException))
            DeleteFileIfExists(pngOutputPath);
        Console.Error.WriteLine(ex switch
        {
            CastReadException or FontNotFoundException or FrameTooLargeException => $"Error: {ex.Message}",
            _ => $"Error: PNG render failed: {ex.Message}",
        });
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
    PrintHelp();
    return 0;
}

if (args[0] is "capture" && args.Length > 1 && args[1] is "-h" or "--help")
{
    PrintCaptureHelp();
    return 0;
}

// `capture` comes here too: it records the one-step scenario that its command makes, without a file.
if (!TryParseRunArgs(args, out var scenarioArg, out var outputArg, out var outputFormat, out var verbose, out var strict, out var runFontSize, out var runFontFamily, out var runThemePreset, out var runWindow, out var runMaxFps, out var runMp4Options, out var runStyle, out var runStillAt, out var capture, out var saveKeysArg, out var saveOutputsArg, out var replayArg, out var runError))
{
    Console.Error.WriteLine($"Error: {runError}");
    if (args[0] is "capture")
        PrintCaptureUsage();
    else
        PrintUsage();
    return 1;
}

var scenarioPath = Path.GetFullPath(scenarioArg);
if (capture is null && !File.Exists(scenarioPath))
{
    Console.Error.WriteLine($"Error: {FormatDisplayPath(scenarioPath)} not found");
    return 1;
}

var outputStem = RenderSettingsResolver.ResolveOutputStem(scenarioPath, outputArg);
var outputPath = outputStem + ".cast";
var svgPath = outputFormat == OutputFormat.Svg ? outputStem + ".svg" : null;
var mp4Path = outputFormat == OutputFormat.Mp4 ? outputStem + ".mp4" : null;
var pngPath = outputFormat == OutputFormat.Png ? outputStem + ".png" : null;
var gifPath = outputFormat == OutputFormat.Gif ? outputStem + ".gif" : null;

Console.Error.WriteLine(capture is null ? $"Loading: {FormatDisplayPath(scenarioPath)}" : $"Capturing: {capture.CommandLine}");
string yaml;
Scenario scenario;
try
{
    yaml = capture?.Yaml ?? File.ReadAllText(scenarioPath, Encoding.UTF8);
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

// A key scenetake does not read used to be ignored without a word, so a typo such as `hightlight` or
// `ptty` turned its feature off and the recording looked right otherwise. These are warnings, before the
// errors below: `step:` for `steps:` is named before "no steps to record".
foreach (var unknownKey in FindUnknownScenarioKeys(yaml))
    Console.Error.WriteLine($"Warning: {unknownKey}");

if (!RenderSettingsResolver.TryResolve(scenario, runThemePreset, out var renderSettings, out var themeError, BackgroundImageSource.FromFolder(Path.GetDirectoryName(scenarioPath)!)))
{
    Console.Error.WriteLine($"Error: {themeError}");
    PrintUsage();
    return 1;
}

// The cast records width and height as its terminal size. Check them before running anything:
// a size the cast format does not allow would otherwise fail only when SVG or mp4 reads the cast back.
var scenarioWidth = scenario.Width ?? RenderSettingsResolver.DefaultTerminalCols;
var scenarioHeight = scenario.Height ?? RenderSettingsResolver.DefaultTerminalRows;
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

// --replay takes each step's output from an outputs file that --save-outputs wrote, and runs no command, so the
// look can change without running the commands again. It is read before anything else needs it.
var replayPath = replayArg is null ? null : Path.GetFullPath(replayArg);
StepOutputs? replayOutputs = null;
if (replayPath is not null)
{
    try
    {
        replayOutputs = StepOutputs.Parse(File.ReadAllText(replayPath, Encoding.UTF8));
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException)
    {
        Console.Error.WriteLine($"Error: cannot read the outputs file {FormatDisplayPath(replayPath)}: {ex.Message}");
        return 1;
    }
}

// An interactive step needs scenetake's own stdin and stdout to be a terminal. Checking it here stops
// the run before pre and the steps before it, which otherwise ran only to be thrown away. A replay types nothing.
if (replayOutputs is null && (Console.IsInputRedirected || Console.IsOutputRedirected) && HasInteractiveStep(scenario))
{
    Console.Error.WriteLine($"Error: {InteractiveNeedsTerminalError}");
    return 1;
}

// --save-keys writes what a person types in the interactive steps. Without one there would be nothing to write,
// which is better said before the recording than after it.
var saveKeysPath = saveKeysArg is null ? null : Path.GetFullPath(saveKeysArg);
if (saveKeysPath is not null && !HasInteractiveStep(scenario))
{
    Console.Error.WriteLine("Error: --save-keys saves the keys typed in 'interactive: true' steps, and this scenario has none.");
    return 1;
}

// The OS reads a path only up to a NUL, so pre, post and the pipe steps would run in the folder named
// before it. A PTY step refuses the NUL itself, but only after pre has run.
if (scenario.Cwd is not null && scenario.Cwd.Contains('\0'))
{
    Console.Error.WriteLine($"Error: cwd cannot contain a NUL character: {scenario.Cwd}");
    return 1;
}

// A replay runs nothing in cwd, so a scenario replays on a machine without that folder.
if (replayOutputs is null && !string.IsNullOrWhiteSpace(scenario.Cwd))
{
    // A relative cwd is resolved against the scenario file's folder, so a scenario runs in the same
    // folder wherever scenetake is started from. It used to follow the current directory, and
    // `scenetake demo/scenario.yaml` started from the parent folder looked for cwd under the parent.
    var writtenCwd = scenario.Cwd;
    try
    {
        scenario.Cwd = Path.GetFullPath(writtenCwd, Path.GetDirectoryName(scenarioPath)!);
    }
    catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
    {
        Console.Error.WriteLine($"Error: cwd is not a valid path: {writtenCwd} ({ex.Message})");
        return 1;
    }

    // pre runs in cwd too, so cwd must exist before anything runs. Without this check the first command
    // failed with the OS message "The directory name is invalid", which names neither cwd nor the folder
    // it was resolved to.
    if (!Directory.Exists(scenario.Cwd))
    {
        var resolvedCwd = Path.IsPathFullyQualified(writtenCwd) ? "" : $" (resolved against the scenario's folder: {scenario.Cwd})";
        Console.Error.WriteLine($"Error: cwd folder does not exist: {writtenCwd}{resolvedCwd}. pre runs in cwd too, so create the folder before running scenetake.");
        return 1;
    }
}

// The cast is written only after pre and the recording, so the outputs are checked first. When pre
// has commands, their folder is checked after pre instead, because pre may create it.
var preHasCommands = replayOutputs is null && scenario.Pre?.Any(command => !string.IsNullOrWhiteSpace(command)) == true;
var saveOutputsPath = saveOutputsArg is null ? null : Path.GetFullPath(saveOutputsArg);
if (!TryCheckOutputPaths(scenarioPath, [outputPath, svgPath, mp4Path, pngPath, gifPath, saveKeysPath, saveOutputsPath], out var outputPathError, requireFolder: !preHasCommands))
{
    Console.Error.WriteLine($"Error: {outputPathError}");
    return 1;
}

foreach (var (option, path, others) in new[]
{
    ("--save-keys", saveKeysPath, new[] { outputPath, svgPath, mp4Path, pngPath, gifPath, saveOutputsPath }),
    ("--save-outputs", saveOutputsPath, new[] { outputPath, svgPath, mp4Path, pngPath, gifPath }),
})
{
    if (path is not null && others.Any(other => other is not null && IsSamePath(other, path)))
    {
        Console.Error.WriteLine($"Error: {option} would write over another output: {FormatDisplayPath(path)}");
        return 1;
    }
}

// The outputs that a replay writes would replace the file it reads from.
if (replayPath is not null && new[] { outputPath, svgPath, mp4Path, pngPath, gifPath }.Any(path => path is not null && IsSamePath(path, replayPath)))
{
    Console.Error.WriteLine($"Error: --replay reads the file that an output would write over: {FormatDisplayPath(replayPath)}");
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
renderSettings = runStyle.ToOverrides().Apply(renderSettings);

// A crop that leaves nothing of the terminal is known from the scenario alone, so it stops the run before anything runs.
if (TerminalReplay.CheckCrop(renderSettings.Crop, scenarioWidth, scenarioHeight) is { } cropError)
{
    Console.Error.WriteLine($"Error: {cropError}");
    return 1;
}

// The cast keeps the settings for a later `scenetake svg`, so only mp4, GIF, and still output skip the SVG's notes.
foreach (var note in RenderSettingsResolver.FindIneffectiveStyle(renderSettings,
    svg: outputFormat is OutputFormat.Svg or OutputFormat.Cast && runStillAt is null, gif: outputFormat == OutputFormat.Gif))
    Console.Error.WriteLine($"Warning: {note}");

// A YAML background image needs SVG, mp4, PNG, or GIF output: the cast keeps background colors only.
if (outputFormat == OutputFormat.Cast && renderSettings.Background?.ImagePath is not null)
{
    Console.Error.WriteLine("Error: render.background with an image file requires --format svg, mp4, png, or gif: a cast keeps colors only");
    return 1;
}

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
        var mp4Settings = runMp4Options.ToSettings();
        var layout = Mp4Render.CreateLayout(renderSettings, scenarioWidth, scenarioHeight, mp4Settings);
        Mp4Render.OutputSize(layout, mp4Settings);
        GlyphSource.Create(renderSettings.FontFamily, runMp4Options.FontFile, layout);
    }
    catch (Exception ex) when (ex is FontNotFoundException or FrameTooLargeException)
    {
        Console.Error.WriteLine($"Error: {ex.Message}");
        return 1;
    }

    ffmpegPath = resolvedFfmpeg;
}

// A PNG needs a font and a frame it can draw, and ffmpeg only to decode a background image. Checked before running
// anything, as for mp4.
if (outputFormat == OutputFormat.Png)
{
    if (renderSettings.Background?.ImagePath is not null)
    {
        if (!Ffmpeg.TryResolveImageDecoder(out var imageDecoder, out var decoderError))
        {
            Console.Error.WriteLine($"Error: {decoderError}");
            return 1;
        }

        ffmpegPath = imageDecoder;
    }

    try
    {
        GlyphSource.Create(renderSettings.FontFamily, runMp4Options.FontFile, PngRender.CreateLayout(renderSettings, scenarioWidth, scenarioHeight));
    }
    catch (Exception ex) when (ex is FontNotFoundException or FrameTooLargeException)
    {
        Console.Error.WriteLine($"Error: {ex.Message}");
        return 1;
    }
    finally
    {
        LoadedFace.ReleaseAll();
    }
}

// A GIF needs ffmpeg (any build), a font, and a frame it can draw, checked before running anything as for mp4.
if (outputFormat == OutputFormat.Gif)
{
    if (!Ffmpeg.TryResolveForGif(out var gifFfmpeg, out var gifFfmpegError))
    {
        Console.Error.WriteLine($"Error: {gifFfmpegError}");
        return 1;
    }

    try
    {
        var layout = Mp4Render.CreateLayout(renderSettings, scenarioWidth, scenarioHeight, runMp4Options.ToGifSettings());
        GlyphSource.Create(renderSettings.FontFamily, runMp4Options.FontFile, layout);
    }
    catch (Exception ex) when (ex is FontNotFoundException or FrameTooLargeException)
    {
        Console.Error.WriteLine($"Error: {ex.Message}");
        return 1;
    }
    finally
    {
        LoadedFace.ReleaseAll();
    }

    ffmpegPath = gifFfmpeg;
}

var deterministicSeed = ComputeDeterministicSeed(yaml);
var deterministicTimestamp = ComputeDeterministicTimestamp(deterministicSeed);

if (replayOutputs is not null)
    return await ReplayAsync(replayOutputs, replayPath!);

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

// What each command leaves in its shell (the folder, variables, functions, aliases) goes on to the next one, from
// pre through the steps to post, as in a terminal. The sh family carries the folder and variables only, and other
// shells than bash, zsh, and PowerShell start each command afresh.
using var shellState = ShellState.Create(shell.FileName, IsPowerShellLaunch(shell));
if ((scenario.Pre?.Count ?? 0) + (scenario.Steps?.Count ?? 0) + (scenario.Post?.Count ?? 0) > 1)
{
    if (shellState is null)
        Console.Error.WriteLine($"Note: in {shell.DisplayName}, each command starts in a new shell: cd, variables, functions, and aliases do not carry over to the next step. bash, zsh, and PowerShell carry them.");
    else if (!shellState.CarriesFunctionsAndAliases)
        Console.Error.WriteLine($"Note: in {shell.DisplayName}, cd and variables carry over to the next step, but functions and aliases do not. bash, zsh, and PowerShell carry them too.");
}

// The commands of background steps, such as a server that later steps call. They are stopped after the last step,
// and before post on every path, so post can clean up what they used.
using var backgroundSteps = new BackgroundSteps();

var preExitCode = await RunScenarioCommandsAsync(scenario.Pre, "pre", scenario.Cwd, shell, shellState, verbose);
if (preExitCode != 0)
    return preExitCode;

// Stops the run with an error after pre has succeeded. post still runs, so what pre set up is torn down.
async Task<int> StopAfterPreAsync(string error)
{
    Console.Error.WriteLine($"Error: {error}");
    backgroundSteps.StopAll();
    var cleanupExitCode = await RunScenarioCommandsAsync(scenario.Post, "post", scenario.Cwd, shell, shellState, verbose);
    return cleanupExitCode != 0 ? cleanupExitCode : 1;
}

// pre may create the output folder, or put a directory or a link to the input where an output goes, so
// every output check runs again. Found here, they stop the run before the recording, instead of failing
// the cast write or overwriting the input.
if (!TryCheckOutputPaths(scenarioPath, [outputPath, svgPath, mp4Path, pngPath, gifPath, saveKeysPath], out var outputCheckError))
    return await StopAfterPreAsync(outputCheckError);

// pre or the steps may install fonts. The mp4 render scans again when it starts, instead of using
// the scan made before pre.
FontCatalog.Rescan();

if (verbose)
    PrintPhase("steps");

// --save-outputs names the outputs file: what each step printed in this run, which --replay and the playground
// replay. SCENETAKE_OUTPUTS does the same for samples/regenerate.cs, which sets it for every sample.
var outputsFileArg = saveOutputsPath ?? Environment.GetEnvironmentVariable(StepOutputs.EnvironmentVariable);
List<RecordedStep>? recordedSteps = string.IsNullOrWhiteSpace(outputsFileArg) ? null : [];

// The commands inherit scenetake's environment, so a secret variable's value is hidden wherever a step prints it.
// So is one that pre or a step exports for the commands after it.
var masker = SecretMasker.ForScenario(scenario, SecretMasker.ProcessEnvironment());
masker?.Learn(shellState?.TakeExports());

// After each step: what the shell state could not restore, and the secrets the step exported, before its output is masked.
async Task<CommandExecution> AfterStep(Task<CommandExecution> running)
{
    var execution = await running;
    ReportShellStateWarnings(shellState);
    masker?.Learn(shellState?.TakeExports());
    return execution;
}

// The keys a person types in the interactive steps, for --save-keys. They are written even when the recording
// stops at a later step: getting them again would mean doing the whole session again.
List<SavedStepKeys>? savedKeys = saveKeysPath is null ? null : [];
string? SaveKeys()
{
    if (saveKeysPath is null || savedKeys is null)
        return null;
    if (savedKeys.Count == 0)
    {
        // The recording stopped before an interactive step ran.
        Console.Error.WriteLine("Warning: --save-keys wrote nothing: no interactive step ran.");
        return null;
    }

    try
    {
        // Windows reports no input mode, so a password was saved like any other key, and the file says so.
        File.WriteAllText(saveKeysPath, RecordedKeys.FormatFile(Path.GetFileName(scenarioPath), savedKeys, everyKeySaved: OperatingSystem.IsWindows()), new UTF8Encoding(false));
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
    {
        return $"cannot write the keys: {ex.Message}";
    }

    Console.Error.WriteLine($"Written: {FormatDisplayPath(saveKeysPath)}  (keys of {savedKeys.Count} interactive step{(savedKeys.Count == 1 ? "" : "s")})");
    return null;
}

// The cast an earlier recording wrote here: a step that prints what it printed then keeps its events and timing,
// so recording an unchanged scenario again gives the same cast and SVG (spec_scenario.md → Recording again).
var previousCast = PreviousCast.TryRead(outputPath, scenarioWidth, scenarioHeight, out var previousCastProblem);
// A replay of the outputs file must give the cast, so with one the cast comes from this run alone.
if (recordedSteps is not null && (previousCast is not null || previousCastProblem is not null))
{
    previousCast = null;
    previousCastProblem = Rerecording.OutputsFileRecordsThisRun;
}

var rerecorder = new Rerecorder(previousCast, masker);

Console.Error.WriteLine("Generating cast...");
List<CastEvent> events;
FailedStep? strictFailure;
try
{
    var generateOptions = new GenerateOptions
    {
        Verbose = verbose,
        Strict = strict,
        TrackPtyCursor = OperatingSystem.IsWindows(),
        Record = recordedSteps,
        Masker = masker,
        SavedKeys = savedKeys,
        Rerecorder = rerecorder,
    };
    (events, strictFailure) = await GenerateAsync(scenario, deterministicSeed, generateOptions, step => AfterStep(step switch
    {
        { Background: true } => RunBackgroundAsync(step, scenario.Cwd, shell, shellState, backgroundSteps),
        { UseInteractive: true } => RunInteractivePtyAsync(step.Command.Cmd, scenario.Cwd, shell, shellState, step.Width, step.Height, verbose),
        { UsePty: true } when step.Keys.Count > 0 || step.Timeout is not null => RunPtyWithInputAsync(step, scenario.Cwd, shell, shellState, verbose),
        _ => RunCommandCoreAsync(step.Command.Cmd, scenario.Cwd, shell, shellState, step.UsePty, step.Width, step.Height, verbose, step.PtyStartCursor, step.Timeout),
    }));
    // The steps are over: what ran in the background is no longer needed, and post comes after the outputs.
    backgroundSteps.StopAll();
}
catch (Exception ex)
{
    // A step can fail in many ways, such as a command the PTY cannot start, or a working folder that an
    // earlier step removed. Each stops the recording, and post must still run. --verbose keeps the type
    // and stack, which an unexpected exception needs.
    if (SaveKeys() is { } keysError)
        Console.Error.WriteLine($"Error: {keysError}");
    return await StopAfterPreAsync(verbose ? ex.ToString() : ex.Message);
}

// The keys first, so nothing below that stops the run loses them. A failure to write them fails the run at the
// end, after the other outputs.
var keysWriteError = SaveKeys();
if (keysWriteError is not null)
    Console.Error.WriteLine($"Error: {keysWriteError}");

// --strict stopped at a failing step. Nothing else is written, as for any error that stops the recording, so
// the outputs of an earlier run stay as they were, and post runs. The cast is not there to show why the
// step failed, so the end of its output is part of the error.
if (strictFailure is not null)
    return await StopAfterPreAsync(FormatStrictFailure(strictFailure));

// OSC titles and similar sequences are never drawn, but a committed cast handed them to every reader:
// ConPTY sets the title to the shell's full path, often under C:\Users\<name>.
var filterMap = new int[events.Count + 1];
events = CastOscFilter.RemovePrivateSequences(events, filterMap);
var stepSlots = Rerecording.MapIndices(rerecorder.Slots, filterMap);

try
{
    WriteCast(scenario, events, outputPath, ShellEnvName(shell.FileName), deterministicTimestamp, renderSettings, stepSlots);
}
catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
{
    // A step may remove the output folder or lock the file.
    return await StopAfterPreAsync($"cannot write the cast: {ex.Message}");
}

// Said before the files are listed, so a reader who finds stars in the recording knows where they came from.
if (masker?.Summary() is { } maskSummary)
    Console.Error.WriteLine($"{maskSummary} (settings.mask-auto: false turns off the automatic ones)");

var duration = events.Count > 0 ? events[^1].Time : 0.0;
// With the cast as the only output, its line also says how large an SVG or mp4 of it would be.
var convertedSizes = outputFormat == OutputFormat.Cast ? $", {DescribeConvertedSizes(events, scenarioWidth, scenarioHeight, renderSettings)}" : "";
Console.Error.WriteLine($"Written: {FormatDisplayPath(outputPath)}  ({events.Count} events, {duration:F1}s{convertedSizes})");
foreach (var line in Rerecording.Describe(rerecorder, previousCastProblem, FormatDisplayPath(outputPath)))
    Console.Error.WriteLine(line);

if (recordedSteps is not null)
{
    var outputsFilePath = Path.GetFullPath(outputsFileArg!);
    try
    {
        new StepOutputs
        {
            Scenetake = GetAppVersion(),
            Os = StepOutputs.CurrentOs,
            Shell = ShellEnvName(shell.FileName),
            Width = scenarioWidth,
            Height = scenarioHeight,
            Steps = recordedSteps,
        }.WriteFile(outputsFilePath);
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
    {
        return await StopAfterPreAsync($"cannot write the outputs file: {ex.Message}");
    }

    Console.Error.WriteLine($"Written: {FormatDisplayPath(outputsFilePath)}");
}

var exitCode = RenderFromCast(outputFormat, outputPath, svgPath, mp4Path, pngPath, gifPath, renderSettings, runMp4Options, runStillAt, ffmpegPath);
if (keysWriteError is not null)
    exitCode = 1;

var postExitCode = await RunScenarioCommandsAsync(scenario.Post, "post", scenario.Cwd, shell, shellState, verbose);
if (postExitCode != 0)
    return postExitCode;

if (exitCode != 0)
    return exitCode;

PrintDone(outputPath, svgPath ?? mp4Path ?? pngPath ?? gifPath);
return 0;

// --replay: each step takes the output that the outputs file recorded for it, as in the playground, and no command
// runs, so pre, post, show: none, and background steps are skipped. What changes how the steps look takes effect:
// typing, prompt, comments, highlights, timing, render, and the order of the steps. A step whose run text is not in
// the file stops the replay: its output would have to be made up.
async Task<int> ReplayAsync(StepOutputs outputs, string path)
{
    Console.Error.WriteLine($"Replaying: {FormatDisplayPath(path)}  (recorded by scenetake {outputs.Scenetake} on {outputs.Os} with {outputs.Shell}, {outputs.Width}x{outputs.Height}). No command runs.");
    if (scenario.Pre is { Count: > 0 } || scenario.Post is { Count: > 0 })
        Console.Error.WriteLine("Note: pre and post do not run in a replay.");
    if (!string.IsNullOrWhiteSpace(scenario.Shell) && !string.Equals(ShellEnvName(scenario.Shell.Trim()), outputs.Shell, StringComparison.OrdinalIgnoreCase))
        Console.Error.WriteLine($"Warning: shell has no effect in a replay: the outputs were recorded with {outputs.Shell}");

    var replay = new StepOutputsReplay(outputs);
    // The outputs were masked when they were recorded. Masking again hides what the YAML adds, such as a new
    // settings.mask text, and the secrets in this machine's environment.
    var replayMasker = SecretMasker.ForScenario(scenario, SecretMasker.ProcessEnvironment());
    // As when recording: a step whose output is the same as in the cast here keeps its events, and keep-output keeps them.
    var replayPrevious = PreviousCast.TryRead(outputPath, scenarioWidth, scenarioHeight, out var replayPreviousProblem);
    var replayRerecorder = new Rerecorder(replayPrevious, replayMasker);
    Console.Error.WriteLine("Generating cast...");
    var (replayed, failure) = await GenerateAsync(scenario, deterministicSeed, new GenerateOptions
    {
        Verbose = verbose,
        Strict = strict,
        TrackPtyCursor = replay.TrackPtyCursor,
        LogRunning = false,
        Masker = replayMasker,
        Rerecorder = replayRerecorder,
        Replay = replay,
    }, replay.RunAsync);

    var missing = replay.Steps.Where(static step => !step.Recorded).ToList();
    if (missing.Count > 0)
    {
        var error = new StringBuilder($"--replay: {FormatDisplayPath(path)} has no output for {(missing.Count == 1 ? "this step" : "these steps")}:");
        foreach (var step in missing)
            error.Append(Environment.NewLine).Append($"  step {step.Index + 1} ({step.Kind}): {step.Run}");
        error.Append(Environment.NewLine).Append("A step matches a recorded one by its run text and by pipe, pty, or interactive. Record again with --save-outputs after changing a command. Nothing was written.");
        Console.Error.WriteLine($"Error: {error}");
        return 1;
    }

    foreach (var step in replay.Steps)
    {
        foreach (var notice in step.Notices)
            Console.Error.WriteLine($"Warning: step {step.Index + 1} was {notice}");
    }

    if (failure is not null)
    {
        Console.Error.WriteLine($"Error: {FormatStrictFailure(failure)}");
        return 1;
    }

    var replayMap = new int[replayed.Count + 1];
    replayed = CastOscFilter.RemovePrivateSequences(replayed, replayMap);
    try
    {
        WriteCast(scenario, replayed, outputPath, outputs.Shell, deterministicTimestamp, renderSettings, Rerecording.MapIndices(replayRerecorder.Slots, replayMap));
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
    {
        Console.Error.WriteLine($"Error: cannot write the cast: {ex.Message}");
        return 1;
    }

    if (replayMasker?.Summary() is { } replayMaskSummary)
        Console.Error.WriteLine($"{replayMaskSummary} (settings.mask-auto: false turns off the automatic ones)");

    var replayDuration = replayed.Count > 0 ? replayed[^1].Time : 0.0;
    var replaySizes = outputFormat == OutputFormat.Cast ? $", {DescribeConvertedSizes(replayed, scenarioWidth, scenarioHeight, renderSettings)}" : "";
    Console.Error.WriteLine($"Written: {FormatDisplayPath(outputPath)}  ({replayed.Count} events, {replayDuration:F1}s{replaySizes})");
    foreach (var line in Rerecording.Describe(replayRerecorder, replayPreviousProblem, FormatDisplayPath(outputPath)))
        Console.Error.WriteLine(line);

    var renderExitCode = RenderFromCast(outputFormat, outputPath, svgPath, mp4Path, pngPath, gifPath, renderSettings, runMp4Options, runStillAt, ffmpegPath);
    if (renderExitCode != 0)
        return renderExitCode;

    PrintDone(outputPath, svgPath ?? mp4Path ?? pngPath ?? gifPath);
    return 0;
}

// SVG and mp4 are rendered from the cast as written, not from the in-memory events. The cast stores
// millisecond times, so only the cast gives the same output as `scenetake svg|mp4 <cast>`. The header
// carries the font, theme, and window overrides. --max-fps is not stored, so it is applied again.
// Returns 1 when the render failed, which removes that output.
static int RenderFromCast(OutputFormat outputFormat, string outputPath, string? svgPath, string? mp4Path, string? pngPath, string? gifPath, ResolvedRenderSettings renderSettings, Mp4Options runMp4Options, double? runStillAt, string? ffmpegPath)
{
    if (outputFormat == OutputFormat.Svg)
    {
        try
        {
            var written = CastReader.Read(outputPath);
            var svgSettings = RenderSettingsResolver.WithImageBackground(written.RenderSettings, renderSettings) with { MaxFps = renderSettings.MaxFps };
            SvgRender.WriteSvg(written.Events, written.Width, written.Height, svgSettings, svgPath!, written.LoopDuration, runStillAt);
            PrintSvgWritten(svgPath!, written, svgSettings, runStillAt);
            if (SvgRender.SizeWarning(new FileInfo(svgPath!).Length, svgSettings.MaxFps, fromCast: false) is { } svgSizeWarning)
                Console.Error.WriteLine($"Warning: {svgSizeWarning}");
        }
        catch (Exception ex)
        {
            DeleteFileIfExists(svgPath!);
            Console.Error.WriteLine($"Error: SVG render failed: {ex.Message}");
            return 1;
        }
    }

    if (outputFormat == OutputFormat.Mp4)
    {
        try
        {
            // WriteMp4 encodes to a temporary file and renames it, so a failure leaves no partial .mp4.
            var written = CastReader.Read(outputPath);
            var result = Mp4Render.WriteMp4(written.Events, written.Width, written.Height, RenderSettingsResolver.WithImageBackground(written.RenderSettings, renderSettings), runMp4Options.ToSettings(), mp4Path!, ffmpegPath!, written.LoopDuration);
            PrintMp4Written(mp4Path!, result);
        }
        catch (Exception ex)
        {
            // An .mp4 from an earlier run no longer matches the new cast.
            DeleteFileIfExists(mp4Path!);
            Console.Error.WriteLine($"Error: mp4 render failed: {ex.Message}");
            return 1;
        }
    }

    if (outputFormat == OutputFormat.Png)
    {
        try
        {
            // Drawn from the cast as written, as SVG and mp4 are.
            var written = CastReader.Read(outputPath);
            var pngSettings = RenderSettingsResolver.WithImageBackground(written.RenderSettings, renderSettings);
            var at = runStillAt ?? double.PositiveInfinity;
            var result = PngRender.WritePng(written.Events, written.Width, written.Height, pngSettings, at, pngPath!, runMp4Options.FontFile, ffmpegPath);
            PrintPngWritten(pngPath!, result, at);
        }
        catch (Exception ex)
        {
            // A .png from an earlier run no longer matches the new cast.
            DeleteFileIfExists(pngPath!);
            Console.Error.WriteLine($"Error: PNG render failed: {ex.Message}");
            return 1;
        }
    }

    if (outputFormat == OutputFormat.Gif)
    {
        try
        {
            // WriteGif encodes to a temporary file and renames it, as WriteMp4 does.
            var written = CastReader.Read(outputPath);
            var result = GifRender.WriteGif(written.Events, written.Width, written.Height, RenderSettingsResolver.WithImageBackground(written.RenderSettings, renderSettings), runMp4Options.ToGifSettings(), gifPath!, ffmpegPath!, written.LoopDuration);
            PrintGifWritten(gifPath!, result, fromCast: false);
        }
        catch (Exception ex)
        {
            // A .gif from an earlier run no longer matches the new cast.
            DeleteFileIfExists(gifPath!);
            Console.Error.WriteLine($"Error: GIF render failed: {ex.Message}");
            return 1;
        }
    }

    return 0;
}

// The cast and the --format output, when there is one.
static void PrintDone(string castPath, string? renderedPath) =>
    Console.Error.WriteLine(renderedPath is null ? $"Done: {FormatDisplayPath(castPath)}" : $"Done: {FormatDisplayPath(castPath)}, {FormatDisplayPath(renderedPath)}");

static string FormatDisplayPath(string fullPath) =>
    Path.GetRelativePath(Directory.GetCurrentDirectory(), fullPath).Replace('\\', '/');

static bool TryParseInitArgs(string[] args, out string? path, out string? shell, out string error)
{
    path = null;
    shell = null;
    error = "";
    for (var i = 1; i < args.Length; i++)
    {
        var arg = args[i];
        if (arg == "--shell" || arg.StartsWith("--shell=", StringComparison.Ordinal))
        {
            string value;
            if (arg == "--shell")
            {
                if (i + 1 >= args.Length)
                {
                    error = "--shell requires a value";
                    return false;
                }

                value = args[++i];
            }
            else
            {
                value = arg["--shell=".Length..];
                if (value.Length == 0)
                {
                    error = "--shell requires a value";
                    return false;
                }
            }

            if (shell is not null)
            {
                error = "duplicate option: --shell";
                return false;
            }

            // The names a scenario's shell key takes. Each picks the syntax of the template's steps;
            // powershell (Windows PowerShell) runs the same steps as pwsh.
            if (value is not ("bash" or "pwsh" or "powershell"))
            {
                error = $"--shell must be bash, pwsh, or powershell: {value}";
                return false;
            }

            shell = value;
            continue;
        }

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
    out StyleOptions style,
    out double? stillAt,
    out string error)
{
    castPath = "";
    style = new StyleOptions();
    outputPath = null;
    fontSizeOverride = null;
    fontFamilyOverride = null;
    themePresetOverride = null;
    windowOverride = null;
    maxFpsOverride = null;
    stillAt = null;
    error = "";

    for (var i = 1; i < args.Length; i++)
    {
        var arg = args[i];
        if (TryConsumeAtArg(args, ref i, ref stillAt, out error))
        {
            if (error.Length != 0)
                return false;
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

        if (TryConsumeStyleArg(args, ref i, style, out error))
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

    // A still SVG shows one screen, so the options that shape the animation do nothing. Here they are not stored in a
    // cast either, so naming one is a mistake.
    if (stillAt.HasValue && AnimationOption(maxFpsOverride.HasValue, style) is string animationOption)
    {
        error = $"{animationOption} has no effect with --at: a still SVG does not play";
        return false;
    }

    if (castPath.Length != 0)
        return true;

    error = "cast path is required";
    return false;
}

/// <summary>The first option given that only an animation uses, or null.</summary>
static string? AnimationOption(bool maxFps, StyleOptions style) =>
    maxFps ? "--max-fps"
    : style.Loop is bool loop ? (loop ? "--loop" : "--no-loop")
    : style.EndHold is not null ? "--end-hold"
    : style.FadeOut is not null ? "--fade-out"
    : null;

static bool TryParseRunArgs(
    string[] args,
    out string scenarioPath,
    out string? outputPath,
    out OutputFormat outputFormat,
    out bool verbose,
    out bool strict,
    out int? fontSizeOverride,
    out string? fontFamilyOverride,
    out string? themePresetOverride,
    out WindowStyle? windowOverride,
    out int? maxFpsOverride,
    out Mp4Options mp4Options,
    out StyleOptions style,
    out double? stillAt,
    out CaptureRequest? capture,
    out string? saveKeysPath,
    out string? saveOutputsPath,
    out string? replayPath,
    out string error)
{
    scenarioPath = "";
    style = new StyleOptions();
    outputPath = null;
    saveKeysPath = null;
    saveOutputsPath = null;
    replayPath = null;
    // `capture` records one command given here instead of a scenario file. It writes an SVG unless --format says
    // otherwise: it is for a quick image, and the cast stays next to it for other formats.
    var captureMode = args.Length > 0 && args[0] == "capture";
    capture = captureMode ? new CaptureRequest() : null;
    outputFormat = captureMode ? OutputFormat.Svg : OutputFormat.Cast;
    verbose = false;
    strict = false;
    fontSizeOverride = null;
    fontFamilyOverride = null;
    themePresetOverride = null;
    windowOverride = null;
    maxFpsOverride = null;
    mp4Options = new Mp4Options();
    stillAt = null;
    error = "";

    for (var i = captureMode ? 1 : 0; i < args.Length; i++)
    {
        var arg = args[i];
        if (capture is not null)
        {
            // Everything after -- is the command, word by word, so its own options are never read as scenetake's.
            if (arg == "--")
            {
                capture.Words = args[(i + 1)..];
                break;
            }

            if (TryConsumeCaptureArg(args, ref i, capture, ref outputPath, out error))
            {
                if (error.Length != 0)
                    return false;
                continue;
            }
        }
        // capture has no interactive step, so it has no keys to save.
        else if (TryTakeOptionValue(args, ref i, "--save-keys", out var keysPath, out error))
        {
            if (error.Length == 0 && saveKeysPath is not null)
                error = "duplicate option: --save-keys";
            if (error.Length != 0)
                return false;
            saveKeysPath = keysPath;
            continue;
        }
        // capture records a command, not a scenario file, so it has no YAML to replay its outputs into.
        else if (TryTakeOptionValue(args, ref i, "--save-outputs", out var outputsPath, out error))
        {
            if (error.Length == 0 && saveOutputsPath is not null)
                error = "duplicate option: --save-outputs";
            if (error.Length != 0)
                return false;
            saveOutputsPath = outputsPath;
            continue;
        }
        else if (TryTakeOptionValue(args, ref i, "--replay", out var replayFile, out error))
        {
            if (error.Length == 0 && replayPath is not null)
                error = "duplicate option: --replay";
            if (error.Length != 0)
                return false;
            replayPath = replayFile;
            continue;
        }

        if (arg == "--verbose")
        {
            verbose = true;
            continue;
        }

        if (arg == "--strict")
        {
            strict = true;
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

        if (TryConsumeStyleArg(args, ref i, style, out error))
        {
            if (error.Length != 0)
                return false;
            continue;
        }

        if (TryConsumeAtArg(args, ref i, ref stillAt, out error))
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

        if (capture is not null)
        {
            if (capture.Command is not null)
            {
                error = $"capture takes one command: quote it as one argument, or put it after --: {arg}";
                return false;
            }

            capture.Command = arg;
            continue;
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

    if (Mp4OptionError(mp4Options, outputFormat) is string mp4OptionError)
    {
        error = mp4OptionError;
        return false;
    }

    if (outputFormat is OutputFormat.Mp4 or OutputFormat.Gif && maxFpsOverride.HasValue)
    {
        error = $"--max-fps applies to SVG, use --fps for {(outputFormat == OutputFormat.Gif ? "GIF" : "mp4")}";
        return false;
    }

    if (outputFormat == OutputFormat.Png && maxFpsOverride.HasValue)
    {
        error = "--max-fps applies to an animated SVG: a PNG is one screen";
        return false;
    }

    if (outputFormat == OutputFormat.Cast && maxFpsOverride.HasValue)
    {
        error = "--max-fps requires --format svg";
        return false;
    }

    // --at picks one screen, which an SVG or a PNG shows. The cast has every screen, and an mp4 and a GIF play them all.
    if (stillAt.HasValue && outputFormat is OutputFormat.Cast or OutputFormat.Mp4 or OutputFormat.Gif)
    {
        error = outputFormat switch
        {
            OutputFormat.Mp4 => "--at applies to SVG and PNG: an mp4 plays the whole recording",
            OutputFormat.Gif => "--at applies to SVG and PNG: a GIF plays the whole recording",
            _ => "--at requires --format svg or png",
        };
        return false;
    }

    if (stillAt.HasValue && maxFpsOverride.HasValue)
    {
        error = "--max-fps has no effect with --at: a still SVG does not play";
        return false;
    }

    // A cast keeps background colors but names no image file, so an image would be lost without a word.
    if (outputFormat == OutputFormat.Cast && style.Background?.ImagePath is not null)
    {
        error = "--background with an image file requires --format svg, mp4, png, or gif: a cast keeps colors only";
        return false;
    }

    // A replay runs no command: no step prints anything new, and no one types.
    if (replayPath is not null && saveOutputsPath is not null)
    {
        error = "--save-outputs saves what the commands print, and --replay runs none: record without --replay to save new outputs";
        return false;
    }

    if (replayPath is not null && saveKeysPath is not null)
    {
        error = "--save-keys saves the keys typed in interactive steps, and --replay runs none";
        return false;
    }

    if (capture is not null)
        return capture.TryFinish(out scenarioPath, out error);

    if (scenarioPath.Length != 0)
        return true;

    error = "scenario path is required";
    return false;
}

/// <summary>
/// The options only <c>capture</c> takes: the terminal's <c>--width</c> and <c>--height</c>, <c>--shell</c>,
/// <c>--timeout</c>, <c>--pipe</c>, and <c>-o</c> / <c>--output</c>, which names the files as the scenario path's
/// [output] does.
/// </summary>
static bool TryConsumeCaptureArg(string[] args, ref int i, CaptureRequest capture, ref string? outputPath, out string error)
{
    error = "";
    var arg = args[i];
    if (arg == "--pipe")
    {
        if (capture.Pipe)
            error = "duplicate option: --pipe";
        capture.Pipe = true;
        return true;
    }

    foreach (var (name, isWidth) in (ReadOnlySpan<(string, bool)>)[("--width", true), ("--height", false)])
    {
        if (!TryTakeOptionValue(args, ref i, name, out var value, out error))
            continue;
        if (error.Length != 0)
            return true;

        var (min, max) = isWidth
            ? (RenderSettingsResolver.MinTerminalCols, RenderSettingsResolver.MaxTerminalCols)
            : (RenderSettingsResolver.MinTerminalRows, RenderSettingsResolver.MaxTerminalRows);
        if ((isWidth ? capture.Width : capture.Height) is not null)
            error = $"duplicate option: {name}";
        else if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var cells) || cells < min || cells > max)
            error = $"{name} must be a whole number of {(isWidth ? "columns" : "rows")} from {min} to {max}: {value}";
        else if (isWidth)
            capture.Width = cells;
        else
            capture.Height = cells;
        return true;
    }

    if (TryTakeOptionValue(args, ref i, "--shell", out var shell, out error))
    {
        if (error.Length == 0 && capture.Shell is not null)
            error = "duplicate option: --shell";
        else if (error.Length == 0)
            capture.Shell = shell;
        return true;
    }

    if (TryTakeOptionValue(args, ref i, "--timeout", out var timeout, out error))
    {
        if (error.Length != 0)
            return true;
        if (capture.Timeout is not null)
            error = "duplicate option: --timeout";
        else if (!double.TryParse(timeout, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) || !double.IsFinite(seconds) || seconds <= 0)
            error = $"--timeout must be seconds above 0, such as 5: {timeout}";
        else
            capture.Timeout = seconds;
        return true;
    }

    foreach (var name in (ReadOnlySpan<string>)["-o", "--output"])
    {
        if (!TryTakeOptionValue(args, ref i, name, out var output, out error))
            continue;
        if (error.Length == 0 && outputPath is not null)
            error = $"duplicate option: {name}";
        else if (error.Length == 0)
            outputPath = output;
        return true;
    }

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
        case "png":
            format = OutputFormat.Png;
            return true;
        case "gif":
            format = OutputFormat.Gif;
            return true;
        default:
            error = $"unknown format: {value} (expected cast, svg, mp4, png, or gif)";
            return false;
    }
}

/// <summary>Reads <c>--at</c> and its value when <c>args[i]</c> is one, and returns whether it was.</summary>
static bool TryConsumeAtArg(string[] args, ref int i, ref double? at, out string error)
{
    if (!TryTakeOptionValue(args, ref i, "--at", out var value, out error))
        return false;

    if (error.Length != 0)
        return true;
    if (at is not null)
        error = "duplicate option: --at";
    else if (RenderSettingsResolver.TryParseStillAt(value, out var parsed, out error))
        at = parsed;
    return true;
}



static Task<CommandExecution> RunCommand(string cmd, string? cwd, ShellLaunch shell, ShellState? state) =>
    RunCommandCoreAsync(cmd, cwd, shell, state, false, 120, 24);

/// <param name="ptyStartCursor">
/// For a PTY step on Windows, the recording's cursor as a 1-based row and column. ConPTY then starts drawing
/// there (<see cref="PtyStartInfo.InheritCursor"/>) instead of on an empty screen of its own. Null elsewhere.
/// </param>
/// <param name="timeout">For a pipe step, seconds after which its process tree is ended (<c>timeout</c>). A PTY step with a timeout runs in <see cref="RunPtyWithInputAsync"/>.</param>
static async Task<CommandExecution> RunCommandCoreAsync(string cmd, string? cwd, ShellLaunch shell, ShellState? state, bool usePty, int width, int height, bool verbose = false, (int Row, int Column)? ptyStartCursor = null, double? timeout = null)
{
    var prepared = PrepareCommand(state, cmd);
    if (usePty)
    {
        var result = await PtyCapture.RunAsync(new PtyStartInfo
        {
            FileName = shell.FileName,
            Arguments = BuildPtyShellArguments(shell, prepared.Command),
            WorkingDirectory = cwd,
            Size = new(width, height),
            Environment = ChildEnvironment(prepared),
            // ConPTY asks for the cursor position (ESC[6n) and holds the child's output until a report comes.
            // The report is written up front, so nothing waits, and ConPTY consumes it before the child reads.
            InheritCursor = ptyStartCursor is not null,
        }, new PtyCaptureOptions
        {
            Completion = new PtyCompleteOptions
            {
                DecodeOutput = false,
                Input = ptyStartCursor is { } cursor ? $"\u001b[{cursor.Row};{cursor.Column}R" : null,
                SendEofAfterInput = false,
            },
        });
        state?.Complete(keep: true);
        if (verbose)
            Console.Error.WriteLine($"scenetake: pty verbose: chunks={result.Chunks.Count} bytes={result.Output.Length}");
        return new CommandExecution(result.ExitCode, true, "", "", ToTimedBytes(result.Chunks), null, null);
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
    psi.ArgumentList.Add(OperatingSystem.IsWindows() && IsPowerShellLaunch(shell) ? PowerShellUtf8OutputPrefix + prepared.Command : prepared.Command);
    if (!string.IsNullOrWhiteSpace(cwd)) psi.WorkingDirectory = cwd;
    foreach (var (name, value) in ChildEnvironment(prepared))
    {
        // A null value removes the variable from the child's environment.
        if (value is null)
            psi.Environment.Remove(name);
        else
            psi.Environment[name] = value;
    }

    using var proc = new Process { StartInfo = psi };
    proc.Start();
    // A step that may be stopped runs in a job on Windows, which ends what the tree kill misses (ProcessJob).
    using var job = timeout is null ? null : ProcessJob.TryCreate(proc);
    var readTask = PipeStreamCapture.ReadAsync(proc);
    var stopped = false;
    if (timeout is { } seconds)
    {
        // A pipe has no terminal to press Ctrl+C in. The whole tree ends, so no grandchild keeps the pipes open, and
        // what it printed so far is the step's output.
        using var limit = new CancellationTokenSource(DelayOf(seconds));
        try
        {
            await proc.WaitForExitAsync(limit.Token);
        }
        catch (OperationCanceledException)
        {
            stopped = true;
            try
            {
                if (job is not null)
                    job.Terminate();
                else
                    proc.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
                // It exited just now.
            }

            PrintTimeoutStop(seconds);
        }
    }

    var pipeChunks = await readTask;
    await proc.WaitForExitAsync();
    // A command that scenetake stopped leaves the state as it was.
    state?.Complete(keep: !stopped);
    var (stdout, stderr) = CollectPipeOutputs(pipeChunks);
    // The author asked for the stop, so the exit code it causes is no failure.
    return CommandExecution.FromPipeStream(stdout, stderr, stopped ? 0 : proc.ExitCode, pipeChunks);
}

static void PrintTimeoutStop(double seconds) =>
    Console.Error.WriteLine($"  stopped after {seconds.ToString("0.###", CultureInfo.InvariantCulture)} s (timeout)");

/// <summary>
/// Runs a non-interactive PTY step that sets <c>keys</c> or <c>timeout</c>: writes each key to the PTY at its
/// time while recording the output, and stops the command when its timeout passes. Steps without either keep
/// <see cref="PtyCapture"/>, so their recordings stay as they were.
/// </summary>
static async Task<CommandExecution> RunPtyWithInputAsync(StepStart step, string? cwd, ShellLaunch shell, ShellState? state, bool verbose)
{
    if (!Pty.IsSupported)
        throw new InvalidOperationException("PTY is not supported on this operating system.");

    var prepared = PrepareCommand(state, step.Command.Cmd);
    var session = Pty.Start(new PtyStartInfo
    {
        FileName = shell.FileName,
        Arguments = BuildPtyShellArguments(shell, prepared.Command),
        WorkingDirectory = cwd,
        Size = new(step.Width, step.Height),
        Environment = ChildEnvironment(prepared),
        InheritCursor = step.PtyStartCursor is not null,
    });
    try
    {
        var stopwatch = Stopwatch.StartNew();
        var chunks = new List<PtyCaptureChunk>();
        // Only a step whose keys wait for text reads its output as text.
        PtyOutputWatcher? output = null;
        foreach (var key in step.Keys)
        {
            if (key.Wait is not null)
            {
                output = new PtyOutputWatcher();
                break;
            }
        }
        var pump = PumpPtyOutputAsync(session, chunks, stopwatch, output);
        // As in RunCommandCoreAsync: ConPTY waits for this report before it draws, and consumes it before the child reads.
        if (step.PtyStartCursor is { } cursor)
            await session.WriteInputAsync($"\u001b[{cursor.Row};{cursor.Column}R", Encoding.UTF8);

        var exit = session.WaitForExitAsync();
        using var stopKeys = new CancellationTokenSource();
        var keys = WriteKeysAsync(session, step, exit, stopwatch, output, stopKeys.Token);
        var stopped = false;
        if (step.Timeout is { } timeout)
        {
            var left = timeout - stopwatch.Elapsed.TotalSeconds;
            var timedOut = left > 0
                ? await Task.WhenAny(exit, Task.Delay(DelayOf(left))) != exit
                : !exit.IsCompleted;
            if (timedOut)
            {
                stopped = true;
                stopKeys.Cancel();
                PrintTimeoutStop(timeout);
                await StopPtyChildAsync(session, exit);
            }
        }

        var status = await exit;
        stopKeys.Cancel();
        try
        {
            await keys;
        }
        catch (OperationCanceledException)
        {
            // The command ended or was stopped before its last key.
        }

        // Output ends shortly after the child exits. A descendant that still holds the terminal would keep it open,
        // and disposing the session hangs the terminal up, which ends it.
        if (await Task.WhenAny(pump, Task.Delay(TimeSpan.FromSeconds(5))) != pump)
            session.Dispose();
        await pump;

        // A command that scenetake stopped leaves the state as it was.
        state?.Complete(keep: !stopped);
        if (verbose)
            Console.Error.WriteLine($"scenetake: pty verbose: chunks={chunks.Count} keys={step.Keys.Count} stopped={stopped}");
        return new CommandExecution(stopped ? 0 : status.ExitCode, true, "", "", ToTimedBytes(chunks), null, null);
    }
    finally
    {
        session.Dispose();
    }
}

static async Task PumpPtyOutputAsync(PtySession session, List<PtyCaptureChunk> chunks, Stopwatch stopwatch, PtyOutputWatcher? output = null)
{
    try
    {
        await foreach (var chunk in session.ReadOutputAsync().ConfigureAwait(false))
        {
            chunks.Add(new PtyCaptureChunk(stopwatch.Elapsed, chunk.Data.ToArray()));
            output?.Append(chunk.Data.Span);
        }
    }
    catch (ObjectDisposedException)
    {
        // The session was disposed to end a descendant that kept the terminal open; the output read so far stays.
    }
}

/// <summary>
/// Writes each key at its time since the command started. Times are kept against the start, so a slow write does
/// not push the later keys back. A wait for text restarts the count where the text appeared. Stops when the
/// command exits, as a person stops typing into a closed program.
/// </summary>
static async Task WriteKeysAsync(PtySession session, StepStart step, Task exit, Stopwatch stopwatch, PtyOutputWatcher? output, CancellationToken cancel)
{
    var due = 0.0;
    foreach (var key in step.Keys)
    {
        due += key.Delay;
        var wait = due - stopwatch.Elapsed.TotalSeconds;
        if (wait > 0)
        {
            var delay = Task.Delay(DelayOf(wait), cancel);
            if (await Task.WhenAny(delay, exit) == exit)
                return;
            await delay;
        }

        if (exit.IsCompleted)
            return;

        if (key.Wait is { } text && output is not null)
        {
            // With a timeout the step waits until it. Without one a wait gives up after a while, so a text that
            // never appears does not hold the recording up forever.
            var found = output.WaitAsync(text);
            var limit = Task.Delay(step.Timeout is null ? TimeSpan.FromSeconds(ScenarioCast.KeyWaitLimit) : Timeout.InfiniteTimeSpan, cancel);
            var first = await Task.WhenAny(found, exit, limit);
            if (first != found)
            {
                output.StopWaiting();
                if (first == exit)
                    return;
                cancel.ThrowIfCancellationRequested();
                Console.Error.WriteLine($"Warning: step {step.Index + 1} keys entry {key.Entry} waited {ScenarioCast.KeyWaitLimit:0} s for {JsonEscape.JsonString(text)} and goes on without it. Give the step a 'timeout' to wait longer.");
            }

            due = stopwatch.Elapsed.TotalSeconds;
            continue;
        }

        // A later wait looks for the answer to this key, not for what came before it.
        output?.Clear();
        try
        {
            await session.WriteInputAsync(key.Text, Encoding.UTF8, cancel);
        }
        catch (IOException)
        {
            // The command closed its terminal as the key went out.
            if (await ExitsSoonAsync(exit))
                return;
            throw;
        }
    }
}

/// <summary>Whether a command that a write to its terminal just failed for exits within a second.</summary>
static async Task<bool> ExitsSoonAsync(Task exit) =>
    await Task.WhenAny(exit, Task.Delay(TimeSpan.FromSeconds(1))) == exit;

/// <summary>A wait in seconds as a delay that Task.Delay accepts: about 24 days at most, which a recording never reaches.</summary>
static TimeSpan DelayOf(double seconds) =>
    seconds * 1000 >= int.MaxValue ? TimeSpan.FromMilliseconds(int.MaxValue) : TimeSpan.FromSeconds(Math.Max(seconds, 0));

/// <summary>
/// Stops a PTY step's command as a person would: Ctrl+C first, so a TUI can leave the alternate screen and restore
/// the terminal, which the recording then shows. A command that is still running after
/// <see cref="ScenarioCast.TimeoutInterruptGrace"/> seconds gets SIGTERM, and a second later it is ended.
/// </summary>
static async Task StopPtyChildAsync(PtySession session, Task exit)
{
    try
    {
        await session.WriteInputAsync("\u0003", Encoding.UTF8);
    }
    catch (IOException)
    {
        // It closed its terminal just as the timeout passed.
        if (await ExitsSoonAsync(exit))
            return;
        throw;
    }

    if (await Task.WhenAny(exit, Task.Delay(TimeSpan.FromSeconds(ScenarioCast.TimeoutInterruptGrace))) == exit)
        return;

    try
    {
        session.Kill(PtySignal.Terminate);
        if (await Task.WhenAny(exit, Task.Delay(TimeSpan.FromSeconds(1))) == exit)
            return;
        session.Kill();
    }
    catch (InvalidOperationException)
    {
        // It exited between the checks.
    }
}

static bool IsPowerShellLaunch(ShellLaunch shell) =>
    shell.Arguments.Length > 0 && string.Equals(shell.Arguments[^1], "-Command", StringComparison.OrdinalIgnoreCase);

static async Task<CommandExecution> RunInteractivePtyAsync(
    string cmd,
    string? cwd,
    ShellLaunch shell,
    ShellState? state,
    int width,
    int height,
    bool verbose)
{
    if (!Pty.IsSupported)
        throw new InvalidOperationException("PTY is not supported on this operating system.");

    if (Console.IsInputRedirected || Console.IsOutputRedirected)
    {
        throw new InvalidOperationException(InteractiveNeedsTerminalError);
    }

    var prepared = PrepareCommand(state, cmd);
    using var session = Pty.Start(new PtyStartInfo
    {
        FileName = shell.FileName,
        Arguments = BuildPtyShellArguments(shell, prepared.Command),
        WorkingDirectory = cwd,
        Size = new(width, height),
        Environment = ChildEnvironment(prepared),
    });

    var stopwatch = Stopwatch.StartNew();
    var chunks = new List<PtyCaptureChunk>();
    var inputChunks = new List<PtyInputChunk>();
    var hiddenInput = new List<TimeSpan>();
    var inputRecorder = new InteractivePtyInputRecorder(stopwatch, inputChunks, () => session.InputMode, hiddenInput);
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

    state?.Complete(keep: true);
    if (verbose)
    {
        Console.Error.WriteLine($"scenetake: interactive pty verbose: chunks={chunks.Count} input={inputChunks.Count}");
    }

    return new CommandExecution(exitCode, true, "", "", ToTimedBytes(chunks), inputChunks.ConvertAll(static chunk => new TimedBytes(chunk.Time, chunk.Data)), null)
    {
        HiddenInputTimes = hiddenInput,
    };
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

static async Task<int> RunScenarioCommandsAsync(List<string>? commands, string phase, string? cwd, ShellLaunch shell, ShellState? state, bool verbose)
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
            execution = await RunCommand(cmd, cwd, shell, state);
            ReportShellStateWarnings(state);
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


/// <summary>
/// The error for a run that stopped at <paramref name="failure"/>: a --strict run, or a <c>show: none</c> step that
/// failed. Without the "Error: " prefix.
/// </summary>
static string FormatStrictFailure(FailedStep failure)
{
    var builder = new StringBuilder(
        failure.Background ? $"background step {failure.Number} {failure.Problem}: {failure.Cmd}"
        : failure.Hidden ? $"step {failure.Number} (show: none) exited {failure.ExitCode}, and the steps after it may rely on it: {failure.Cmd}"
        : $"--strict stopped at step {failure.Number}, which exited {failure.ExitCode}: {failure.Cmd}");
    foreach (var line in failure.OutputTail)
        builder.Append(Environment.NewLine).Append("  | ").Append(line);
    builder.Append(Environment.NewLine).Append(
        failure.Background ? "Nothing was written. Set 'continue-on-error: true' on the step if the steps after it can do without it, or raise 'wait-timeout'."
        : failure.Hidden ? "Nothing was written. Set 'continue-on-error: true' on the step if it may fail."
        : "Nothing was written. Set 'continue-on-error: true' on a step that is meant to fail.");
    return builder.ToString();
}

/// <summary>
/// Starts a <c>background: true</c> step's command as a pipe step would start, with the shell state so far, and waits
/// until it is ready (<see cref="BackgroundSteps.StartAsync"/>).
/// </summary>
static Task<CommandExecution> RunBackgroundAsync(StepStart step, string? cwd, ShellLaunch shell, ShellState? state, BackgroundSteps background)
{
    var prepared = state?.PrepareDetached(step.Command.Cmd) ?? new PreparedCommand(step.Command.Cmd, null);
    var psi = new ProcessStartInfo(shell.FileName)
    {
        UseShellExecute = false,
        CreateNoWindow = true,
    };
    foreach (var arg in shell.Arguments)
        psi.ArgumentList.Add(arg);
    psi.ArgumentList.Add(OperatingSystem.IsWindows() && IsPowerShellLaunch(shell) ? PowerShellUtf8OutputPrefix + prepared.Command : prepared.Command);
    if (!string.IsNullOrWhiteSpace(cwd))
        psi.WorkingDirectory = cwd;
    foreach (var (name, value) in ChildEnvironment(prepared))
    {
        if (value is null)
            psi.Environment.Remove(name);
        else
            psi.Environment[name] = value;
    }

    return background.StartAsync(step.Index + 1, step.Command.Cmd, IsStepFlagSet(step.Command.Extra, "continue-on-error"), psi, step.WaitFor, step.WaitLimit);
}

static void ReportShellStateWarnings(ShellState? state)
{
    if (state is null)
        return;
    foreach (var warning in state.TakeWarnings())
        Console.Error.WriteLine($"Warning: {warning}");
}

/// <summary>The command to start for <paramref name="cmd"/>: as it is, or with the shell state of the commands before it.</summary>
static PreparedCommand PrepareCommand(ShellState? state, string cmd) =>
    state?.Prepare(cmd) ?? new PreparedCommand(cmd, null);

/// <summary>The variables every command gets, with those that carry the shell state.</summary>
static Dictionary<string, string?> ChildEnvironment(PreparedCommand prepared)
{
    var environment = CreateChildEnvironmentOverlay();
    if (prepared.Environment is { } extra)
    {
        foreach (var (name, value) in extra)
            environment[name] = value;
    }

    return environment;
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

    // .NET starts a name with no "/" from the current directory before PATH (pre, post and pipe steps; PTY steps
    // search PATH only). A cloned repository with a file named like the shell would then run that file as the
    // shell. Starting the PATH one by full path would change $0, so refuse instead.
    if (!requested.Contains('/') && CurrentDirectoryHasFile(requested))
        throw new InvalidOperationException($"Shell '{requested}' was requested, but the current directory has a file named '{requested}', which would start instead of the one on PATH. Run scenetake from another directory, or set the shell to a full path.");

    // PowerShell is also available on Unix; it does not accept POSIX shells' combined -lc switch.
    // Keep the executable as requested so names, relative paths and symlink traversal are preserved.
    var arguments = Path.GetFileName(requested) is "pwsh" or "powershell"
        ? new[] { "-NoLogo", "-NoProfile", "-Command" }
        : new[] { "-lc" };
    return new ShellLaunch(requested, arguments, requested);
}

static bool CurrentDirectoryHasFile(string name)
{
    try
    {
        // File.Exists, as .NET's lookup uses: a link counts even when its target is missing.
        return File.Exists(Path.Combine(Directory.GetCurrentDirectory(), name));
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
    {
        // No current directory to read, so .NET cannot start a file from it either.
        return false;
    }
}

static ShellLaunch ResolveWindowsShell(string? requested)
{
    if (string.IsNullOrWhiteSpace(requested))
    {
        if (TryResolveExecutableOnWindows("pwsh", out var pwsh))
            return new ShellLaunch(pwsh, ["-NoLogo", "-NoProfile", "-Command"], "pwsh");

        if (TryResolveExecutableOnWindows("powershell", out var powershell))
            return new ShellLaunch(powershell, ["-NoLogo", "-NoProfile", "-Command"], "powershell");

        throw new InvalidOperationException("No supported shell was found on Windows. Install pwsh or powershell, or set the scenario's shell to a Git Bash or MSYS bash.exe path.");
    }

    if (Path.IsPathRooted(requested) || requested.Contains(Path.DirectorySeparatorChar) || requested.Contains(Path.AltDirectorySeparatorChar))
        return ResolveWindowsShellPath(requested);

    if (IsPowerShellName(requested))
    {
        var exeName = NormalizeWindowsShellName(requested);
        if (TryResolveExecutableOnWindows(exeName, out var resolved))
            return new ShellLaunch(resolved, ["-NoLogo", "-NoProfile", "-Command"], exeName);

        throw new InvalidOperationException($"Shell '{requested}' was requested, but '{exeName}' could not be found on Windows.");
    }

    if (string.Equals(NormalizeWindowsShellName(requested), "cmd", StringComparison.OrdinalIgnoreCase))
    {
        if (TryResolveExecutableOnWindows("cmd", out var resolved))
            return new ShellLaunch(resolved, ["/c"], "cmd");

        throw new InvalidOperationException("Shell 'cmd' was requested, but cmd.exe could not be found on Windows.");
    }

    if (string.Equals(NormalizeWindowsShellName(requested), "bash", StringComparison.OrdinalIgnoreCase))
    {
        if (TryResolveWindowsBash(out var resolved))
            return new ShellLaunch(resolved, ["-lc"], "bash");

        throw new InvalidOperationException("Shell 'bash' was requested, but a Git Bash or MSYS bash.exe could not be found. WSL bash is intentionally not used.");
    }

    if (TryResolveExecutableOnWindows(requested, out var resolvedExecutable))
        return new ShellLaunch(resolvedExecutable, ["-c"], requested);

    throw new InvalidOperationException($"Shell '{requested}' could not be resolved on Windows.");
}

static ShellLaunch ResolveWindowsShellPath(string requested)
{
    // A path names the file to start. Looking its name up instead would start another pwsh, cmd or bash, or
    // refuse one that exists. The full path drops a trailing dot or space as Windows does, so "cmd.exe."
    // still gets cmd's arguments. It also gives cmd backslashes: cmd reads each "/" in its own path as a switch.
    string fullPath;
    try
    {
        fullPath = Path.GetFullPath(requested.Trim());
    }
    catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
    {
        throw new InvalidOperationException($"Shell '{requested}' was requested, but it is not a valid path: {ex.Message}");
    }

    string[]? arguments = NormalizeWindowsShellName(fullPath) switch
    {
        "pwsh" or "powershell" => ["-NoLogo", "-NoProfile", "-Command"],
        "cmd" => ["/c"],
        "bash" => ["-lc"],
        _ => null,
    };

    if (arguments is ["-lc"] && IsWslBashPath(fullPath))
        throw new InvalidOperationException($"Shell '{requested}' was requested, but it is WSL bash, which scenetake does not use. Set a Git Bash or MSYS bash.exe path.");

    if (arguments is null)
    {
        // Any other shell starts as written, as it always has.
        if (File.Exists(requested))
            return new ShellLaunch(requested, ["-c"], requested);
    }
    else if (File.Exists(fullPath))
    {
        return new ShellLaunch(fullPath, arguments, fullPath);
    }

    throw new InvalidOperationException($"Shell '{requested}' was requested, but the path does not exist.");
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
        // Not ProgramData: every user can create folders there, so another account could add a Git\bin\bash.exe.
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
    // PATH only, never the current directory: a cloned repository or an extracted archive could hold its own
    // pwsh, cmd or bash.exe, which would then run as the shell.
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


/// <summary>
/// The starter scenario. Its steps are written for <paramref name="shell"/> (bash, pwsh or powershell) and
/// the file sets that shell; with no shell they are written for the OS default shell, which the file
/// leaves unset.
/// </summary>
static string CreateInitialScenarioYaml(string? shell)
{
    // The default shell is pwsh on Windows and $SHELL elsewhere. pwsh has no `1>&2`, so the stderr step
    // written for bash recorded a pwsh ParserError in the first demo a Windows user made. The other steps
    // use commands that pwsh and POSIX shells both run. A Windows user who records with Git Bash, or a
    // Linux user who records with pwsh, picks the syntax with --shell, and the file then names that shell
    // so the steps never run in the other one.
    var powerShellSyntax = shell is null ? OperatingSystem.IsWindows() : shell is "pwsh" or "powershell";
    var shellLine = shell is not null
        ? $"shell: {shell}".PadRight(29) + "# The steps below are written for this shell. Values: bash, pwsh, powershell, or a path"
        : powerShellSyntax
            ? "# shell: pwsh                # Default on Windows. The steps below are written for pwsh, and scenetake init --shell bash writes them for bash"
            : "# shell: bash                # Default: $SHELL. The steps below are written for POSIX shells, and scenetake init --shell pwsh writes them for pwsh";
    var stderrStep = powerShellSyntax
        ? """
            run: "[Console]::Error.WriteLine('stderr sample')"   # pwsh. With shell: bash, use: echo "stderr sample" 1>&2
        """
        : """
            run: echo "stderr sample" 1>&2   # With shell: pwsh, use: "[Console]::Error.WriteLine('stderr sample')"
        """;
    return $"""
    # scenetake starter scenario. Edit the values below and add commands under steps.
    title: "My App"            # Optional cast title
    # width: 80                  # Default: 80
    # height: 24                 # Default: 24
    # cwd: /your/path            # Optional working directory for pre, steps, and post. A relative path is relative to this file
    {shellLine}

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
      stderr-color: {DefaultStderrColorSpec}       # Applied only when stderr has no ANSI SGR. "none" keeps it plain (git and many tools print progress to stderr).
      # stream-output: false         # When true, replay captured chunk times in the cast.
      # max-duration: 15             # Cap step output timeline, and also enables stream emit.
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
        run: echo 'line1 alpha'; echo 'line2 beta gamma'; echo 'line3'; echo 'line4 delta'
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
    {stderrStep}
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
    Console.Error.WriteLine("Usage: scenetake [--verbose] [--strict] [--format cast|svg|mp4|png|gif] [--save-keys FILE] [--save-outputs FILE] [--replay FILE] [--at TIME] [--font-size N] [--font-family FAMILIES] [--theme NAME] [--window none|macos|windows] [--max-fps N] [--fps N] [--crf N] [--max-size WxH | --size WxH] [--font-file PATH] <scenario.yaml> [output]");
    Console.Error.WriteLine("       scenetake svg [--at TIME] [--font-size N] [--font-family FAMILIES] [--theme NAME] [--window none|macos|windows] [--max-fps N] <input.cast> [output.svg]");
    Console.Error.WriteLine("       scenetake mp4 [--font-size N] [--font-family FAMILIES] [--font-file PATH] [--theme NAME] [--window none|macos|windows] [--fps N] [--crf N] [--max-size WxH | --size WxH] <input.cast> [output.mp4]");
    Console.Error.WriteLine("       scenetake png [--at TIME] [--font-size N] [--font-family FAMILIES] [--font-file PATH] [--theme NAME] [--window none|macos|windows] <input.cast> [output.png]");
    Console.Error.WriteLine("       scenetake gif [--fps N] [--max-size WxH | --size WxH] [--no-loop] [--font-size N] [--font-family FAMILIES] [--font-file PATH] [--theme NAME] [--window none|macos|windows] <input.cast> [output.gif]");
    Console.Error.WriteLine("       scenetake capture [options] [-o output] -- <command>");
    Console.Error.WriteLine("       scenetake init [--shell bash|pwsh|powershell] [scenario.yaml]");
    Console.Error.WriteLine("Run 'scenetake --help' for what each option does.");
}

static void PrintCaptureUsage()
{
    Console.Error.WriteLine("Usage: scenetake capture [--width N] [--height N] [--shell NAME] [--pipe] [--timeout N] [-o output] [options] -- <command>");
    Console.Error.WriteLine("       scenetake capture [options] \"<command>\"");
    Console.Error.WriteLine("Run 'scenetake capture --help' for what each option does.");
}

static void PrintCaptureHelp() =>
    Console.Out.Write(
        HelpText.CaptureIntro + "\n" +
        HelpText.CaptureOptions + "\n" +
        HelpText.Recording(capture: true) + "\n" +
        HelpText.Render(scenario: true) + "\n" +
        HelpText.SvgOptions + "\n" +
        HelpText.StillOptions + "\n" +
        HelpText.Mp4Options + "\n" +
        HelpText.GifOptions + "\n" +
        HelpText.Environment);

static void PrintGifUsage()
{
    Console.Error.WriteLine("Usage: scenetake gif [--fps N] [--max-size WxH | --size WxH] [--no-loop] [--font-size N] [--font-family FAMILIES] [--font-file PATH] [--theme NAME] [--window none|macos|windows] <input.cast> [output.gif]");
    Console.Error.WriteLine("Run 'scenetake gif --help' for what each option does.");
}

static void PrintPngUsage()
{
    Console.Error.WriteLine("Usage: scenetake png [--at TIME] [--font-size N] [--font-family FAMILIES] [--font-file PATH] [--theme NAME] [--window none|macos|windows] <input.cast> [output.png]");
    Console.Error.WriteLine("Run 'scenetake png --help' for what each option does.");
}

static void PrintMp4Usage()
{
    Console.Error.WriteLine("Usage: scenetake mp4 [--font-size N] [--font-family FAMILIES] [--font-file PATH] [--theme NAME] [--window none|macos|windows] [--fps N] [--crf N] [--max-size WxH | --size WxH] <input.cast> [output.mp4]");
    Console.Error.WriteLine("Run 'scenetake mp4 --help' for what each option does.");
}

/// <summary>The SVG's line: its size in pixels, as a page first shows it, and its loop, or for a still the screen it shows.</summary>
static void PrintSvgWritten(string path, CastRecording recording, ResolvedRenderSettings render, double? stillAt = null)
{
    var size = SvgRender.FormatSize(SvgRender.Measure(recording.Events, recording.Width, recording.Height, render));
    if (stillAt is double at)
    {
        Console.Error.WriteLine($"Written: {FormatDisplayPath(path)}  ({size}, still, {RenderSettingsResolver.FormatStillAt(at)})");
        return;
    }

    var loop = RenderSettingsResolver.ResolveLoopDuration(recording.LoopDuration, recording.Events, render);
    Console.Error.WriteLine($"Written: {FormatDisplayPath(path)}  ({size}, {loop:F1}s)");
}

/// <summary>The GIF's line, and a warning when it is larger than Zenn takes, with what makes it smaller.</summary>
static void PrintGifWritten(string path, GifResult result, bool fromCast)
{
    Console.Error.WriteLine(
        $"Written: {FormatDisplayPath(path)}  ({result.Width}x{result.Height}, {result.Fps}fps, {result.Frames} frames, {FormatBytes(result.Bytes)}, font: {result.Font.Family})");
    // A smaller font keeps text sharp and shrinks the GIF most. Scaling blurs text, and its blended colors can even
    // make the GIF larger. A lower frame rate helps full-screen programs, whose screen changes every frame.
    if (result.Bytes > GifRender.LargeGifBytes)
    {
        var fps = result.Fps > 10 ? $", and a lower --fps (now {result.Fps}) helps a full-screen program" : "";
        var mp4 = fromCast ? "scenetake mp4 <cast>" : "--format mp4";
        Console.Error.WriteLine($"Warning: GIF is {FormatBytes(result.Bytes)}, over the 3MB that Zenn takes. A smaller --font-size makes it smaller and keeps the text sharp{fps}. X and Reddit take mp4 ({mp4}).");
    }
}

static void PrintPngWritten(string path, PngResult result, double at)
{
    var colors = result.Colors switch { 0 => "over 256 colors", 1 => "1 color", var n => $"{n} colors" };
    Console.Error.WriteLine(
        $"Written: {FormatDisplayPath(path)}  ({result.Width}x{result.Height}, {RenderSettingsResolver.FormatStillAt(at)}, {colors}, {FormatBytes(result.Bytes)}, font: {result.Font.Family})");
}

/// <summary>
/// How large an SVG and an mp4 of a recording would be, for the cast's line when the cast is the only output: the
/// size decides what a demo is for, such as a README or a 1920x1080 video, before anything is converted. An mp4
/// too large to encode is left out, and its conversion reports why.
/// </summary>
static string DescribeConvertedSizes(IReadOnlyList<CastEvent> events, int width, int height, ResolvedRenderSettings render)
{
    var text = $"as SVG {SvgRender.FormatSize(SvgRender.Measure(events, width, height, render))}";
    try
    {
        var (mp4Width, mp4Height) = Mp4Render.Measure(events, width, height, render, Mp4Settings.Default);
        text += $", as mp4 {mp4Width}x{mp4Height}";
    }
    catch (FrameTooLargeException)
    {
    }

    return text;
}

static void PrintMp4Written(string path, Mp4Result result) =>
    Console.Error.WriteLine(
        $"Written: {FormatDisplayPath(path)}  ({result.Width}x{result.Height}, {result.Fps}fps, {result.Frames} frames, {FormatBytes(result.Bytes)}, font: {result.Font.Family})");

/// <summary>
/// The error for <c>svg</c> or <c>mp4</c> given a scenario instead of a cast, or null. <c>scenetake svg
/// demo.yaml</c> is an easy slip, and the JSON parser's error ("'title: …' is an invalid JSON literal") came
/// first and did not say what was wrong. A .yaml or .yml file is a scenario unless its first non-blank
/// character is "{", which starts a cast's JSON header.
/// </summary>
static string? ScenarioGivenForCast(string inputPath, string format)
{
    var extension = Path.GetExtension(inputPath);
    if (!extension.Equals(".yaml", StringComparison.OrdinalIgnoreCase) && !extension.Equals(".yml", StringComparison.OrdinalIgnoreCase))
        return null;

    try
    {
        using var reader = new StreamReader(inputPath);
        int c;
        do
            c = reader.Read();
        while (c != -1 && char.IsWhiteSpace((char)c));
        if (c == '{')
            return null;
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
    {
        // The cast read reports a file that cannot be read.
        return null;
    }

    var display = FormatDisplayPath(inputPath);
    var output = format switch { "svg" => "an SVG", "png" => "a PNG", "gif" => "a GIF", _ => "an mp4" };
    return $"{display} is a scenario, and {format} converts a cast file. To record the scenario with {output}, run: scenetake --format {format} {display}";
}

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
static (uint Volume, ulong Index, uint Links, string? FinalPath)? WindowsFileId(string path)
{
    // A handle that asks for no access opens even when another process denies sharing, which a read handle
    // would not. The \\?\ form is not limited to 260 characters. A path already in a device form is used
    // as it is, since a second prefix would make it invalid.
    const uint FileShareReadWriteDelete = 7, OpenExisting = 3;
    var full = Path.GetFullPath(path);
    var name = full.StartsWith(@"\\?\", StringComparison.Ordinal) || full.StartsWith(@"\\.\", StringComparison.Ordinal) || full.StartsWith(@"\??\", StringComparison.Ordinal) ? full
        : full.StartsWith(@"\\", StringComparison.Ordinal) ? @"\\?\UNC\" + full[2..]
        : @"\\?\" + full;
    using var handle = Kernel32.CreateFile(name, 0, FileShareReadWriteDelete, OpenExisting, 0);
    if (handle.IsInvalid || !Kernel32.GetFileInformationByHandle(handle, out var info))
        return null;

    // Some network file systems report serial number 0 for every file, and repeat indexes across the
    // volumes they serve, so the caller compares the final path there. A path that does not fit is
    // asked for again with the length Windows returned, which counts the terminating null.
    string? finalPath = null;
    var buffer = new char[1024];
    for (var attempt = 0; attempt < 2 && finalPath is null; attempt++)
    {
        var length = Kernel32.GetFinalPathNameByHandle(handle, buffer, 0);
        if (length == 0)
            break;
        if (length < buffer.Length)
            finalPath = new string(buffer, 0, (int)length);
        else
            buffer = new char[length];
    }

    return (info.VolumeSerialNumber, ((ulong)info.FileIndexHigh << 32) | info.FileIndexLow, info.NumberOfLinks, finalPath);
}

// A crop that leaves no row or column of the cast's terminal, at the largest size its resize events reach. Checked
// before rendering, so the error names the crop instead of coming from inside a render.
static string? FindCropError(CastRecording recording, ResolvedRenderSettings settings)
{
    var (canvasWidth, canvasHeight) = TerminalReplay.ResolveCanvasSize(recording.Width, recording.Height, recording.Events);
    return TerminalReplay.CheckCrop(settings.Crop, canvasWidth, canvasHeight);
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
/// <summary>
/// Reads <c>args[i]</c> and its value when it is <c>--window-title</c>, <c>--background</c>, <c>--margin</c>, or
/// <c>--padding</c>, and returns whether it was. A problem with the value goes to <paramref name="error"/>. A
/// background image path is relative to the current folder.
/// </summary>
static bool TryConsumeStyleArg(string[] args, ref int i, StyleOptions style, out string error)
{
    error = "";
    var arg = args[i];

    // An empty title hides the cast's title, so `--window-title ""` and `--window-title=` are values.
    if (arg == "--window-title" || arg.StartsWith("--window-title=", StringComparison.Ordinal))
    {
        string title;
        if (arg != "--window-title")
        {
            title = arg["--window-title=".Length..];
        }
        else if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
        {
            title = args[++i];
        }
        else
        {
            error = "--window-title requires a value (\"\" hides the title)";
            return true;
        }

        if (style.WindowTitle is not null)
            error = "duplicate option: --window-title";
        else
            style.WindowTitle = RenderSettingsResolver.NormalizeWindowTitle(title);
        return true;
    }

    if (TryTakeOptionValue(args, ref i, "--background", out var background, out error))
    {
        if (error.Length != 0)
            return true;
        if (style.Background is not null)
            error = "duplicate option: --background";
        else if (RenderSettingsResolver.TryParseBackground(background, BackgroundImageSource.FromFolder(Directory.GetCurrentDirectory()), out var parsed, out error))
            style.Background = parsed;
        return true;
    }

    if (TryTakeOptionValue(args, ref i, "--margin", out var margin, out error))
    {
        if (error.Length != 0)
            return true;
        if (style.Margin is not null)
            error = "duplicate option: --margin";
        else if (RenderSettingsResolver.TryParseMargin(margin, out var parsed, out error))
            style.Margin = parsed;
        return true;
    }

    if (TryTakeOptionValue(args, ref i, "--padding", out var padding, out error))
    {
        if (error.Length != 0)
            return true;
        if (style.Padding is not null)
            error = "duplicate option: --padding";
        else if (RenderSettingsResolver.TryParsePadding(padding, out var parsed, out error))
            style.Padding = parsed;
        return true;
    }

    if (TryTakeOptionValue(args, ref i, "--opacity", out var opacity, out error))
    {
        if (error.Length != 0)
            return true;
        if (style.Opacity is not null)
            error = "duplicate option: --opacity";
        else if (RenderSettingsResolver.TryParseOpacity(opacity, out var parsed, out error))
            style.Opacity = parsed;
        return true;
    }

    if (TryTakeOptionValue(args, ref i, "--blur", out var blur, out error))
    {
        if (error.Length != 0)
            return true;
        if (style.Blur is not null)
            error = "duplicate option: --blur";
        else if (RenderSettingsResolver.TryParseBlur(blur, out var parsed, out error))
            style.Blur = parsed;
        return true;
    }

    // --loop undoes a cast's st:loop=false, so both forms exist. Either one is given once.
    if (arg is "--loop" or "--no-loop")
    {
        if (style.Loop is not null)
            error = $"duplicate option: {(style.Loop.Value ? "--loop" : "--no-loop")} and {arg}";
        else
            style.Loop = arg == "--loop";
        return true;
    }

    if (TryTakeOptionValue(args, ref i, "--end-hold", out var endHold, out error))
    {
        if (error.Length != 0)
            return true;
        if (style.EndHold is not null)
            error = "duplicate option: --end-hold";
        else if (RenderSettingsResolver.TryParseEndHold(endHold, out var parsed, out error))
            style.EndHold = parsed;
        return true;
    }

    if (TryTakeOptionValue(args, ref i, "--fade-out", out var fadeOut, out error))
    {
        if (error.Length != 0)
            return true;
        if (style.FadeOut is not null)
            error = "duplicate option: --fade-out";
        else if (RenderSettingsResolver.TryParseFadeOut(fadeOut, out var parsed, out error))
            style.FadeOut = parsed;
        return true;
    }

    if (TryTakeOptionValue(args, ref i, "--fit", out var fit, out error))
    {
        if (error.Length != 0)
            return true;
        if (style.Fit is not null)
            error = "duplicate option: --fit";
        else if (RenderSettingsResolver.TryParseFit(fit, out var parsed, out error))
            style.Fit = parsed;
        return true;
    }

    foreach (var edge in (ReadOnlySpan<string>)["top", "bottom", "left", "right"])
    {
        var name = "--crop-" + edge;
        if (!TryTakeOptionValue(args, ref i, name, out var cells, out error))
            continue;
        if (error.Length != 0)
            return true;
        if (style.Crop(edge) is not null)
            error = $"duplicate option: {name}";
        else if (RenderSettingsResolver.TryParseCrop(cells, "crop-" + edge, out var parsed, out error))
            style.SetCrop(edge, parsed);
        return true;
    }

    return false;
}

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
    // --size sets the exact video size, so a --max-size limit would have nothing to do.
    const string SizeConflictError = "--size and --max-size cannot be used together";
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
        else if (error.Length == 0 && options.Size.HasValue)
            error = SizeConflictError;
        else if (error.Length == 0 && Mp4Settings.TryParseMaxSize(value, out var maxWidth, out var maxHeight, out error))
            options.MaxSize = (maxWidth, maxHeight);
    }
    else if (TryTakeOptionValue(args, ref i, "--size", out value, out error))
    {
        if (error.Length == 0 && options.Size.HasValue)
            error = "duplicate option: --size";
        else if (error.Length == 0 && options.MaxSize.HasValue)
            error = SizeConflictError;
        else if (error.Length == 0 && Mp4Settings.TryParseSize(value, out var frameWidth, out var frameHeight, out error))
            options.Size = (frameWidth, frameHeight);
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

    var name = arg.Split('=')[0];
    options.FirstOption ??= name;
    if (name != "--font-file")
        options.FirstVideoOption ??= name;
    if (name == "--crf")
        options.FirstCrfOption ??= name;
    else if (name != "--font-file")
        options.FirstFrameOption ??= name;
    return true;
}

/// <summary>
/// The error for an mp4 option that <paramref name="format"/> does not take, or null. <c>--fps</c>, <c>--max-size</c>,
/// and <c>--size</c> shape a video, which a GIF is too. <c>--crf</c> is H.264's quality. <c>--font-file</c> picks the font
/// of every raster output: mp4, PNG, and GIF.
/// </summary>
static string? Mp4OptionError(Mp4Options options, OutputFormat format)
{
    if (options.FirstCrfOption is string crf && format != OutputFormat.Mp4)
        return format == OutputFormat.Gif ? $"{crf} applies to mp4: a GIF has no H.264 quality" : $"{crf} requires --format mp4";
    if (options.FirstFrameOption is string frame && format is not (OutputFormat.Mp4 or OutputFormat.Gif))
        return $"{frame} requires --format mp4 or gif";
    if (options.FontFile is not null && format is not (OutputFormat.Mp4 or OutputFormat.Png or OutputFormat.Gif))
        return "--font-file requires --format mp4, png, or gif";
    if (format == OutputFormat.Gif && options.Fps > GifRender.MaxFps)
        return $"--fps for a GIF must be between 1 and {GifRender.MaxFps}: {options.Fps} (a GIF counts each frame's delay in hundredths of a second)";
    return null;
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
    out StyleOptions style,
    out string error)
{
    castPath = "";
    style = new StyleOptions();
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
        if (IsMaxFpsArg(arg))
        {
            error = "--max-fps applies to SVG, use --fps for mp4";
            return false;
        }

        if (arg is "--loop" or "--no-loop")
        {
            error = $"{arg} applies to SVG: the player decides whether an mp4 plays again";
            return false;
        }

        if (TryConsumeFontSizeArg(args, ref i, ref fontSizeOverride, out error)
            || TryConsumeFontFamilyArg(args, ref i, ref fontFamilyOverride, out error)
            || TryConsumeThemeArg(args, ref i, ref themePresetOverride, out error)
            || TryConsumeWindowArg(args, ref i, ref windowOverride, out error)
            || TryConsumeMp4Arg(args, ref i, mp4Options, out error)
            || TryConsumeStyleArg(args, ref i, style, out error))
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

static bool TryParseGifArgs(
    string[] args,
    out string castPath,
    out string? outputPath,
    out int? fontSizeOverride,
    out string? fontFamilyOverride,
    out string? themePresetOverride,
    out WindowStyle? windowOverride,
    out Mp4Options options,
    out StyleOptions style,
    out string error)
{
    castPath = "";
    style = new StyleOptions();
    outputPath = null;
    fontSizeOverride = null;
    fontFamilyOverride = null;
    themePresetOverride = null;
    windowOverride = null;
    options = new Mp4Options();
    error = "";

    for (var i = 1; i < args.Length; i++)
    {
        var arg = args[i];
        if (IsMaxFpsArg(arg))
        {
            error = "--max-fps applies to SVG, use --fps for GIF";
            return false;
        }

        if (arg == "--at" || arg.StartsWith("--at=", StringComparison.Ordinal))
        {
            error = "--at applies to SVG and PNG: a GIF plays the whole recording";
            return false;
        }

        if (TryConsumeFontSizeArg(args, ref i, ref fontSizeOverride, out error)
            || TryConsumeFontFamilyArg(args, ref i, ref fontFamilyOverride, out error)
            || TryConsumeThemeArg(args, ref i, ref themePresetOverride, out error)
            || TryConsumeWindowArg(args, ref i, ref windowOverride, out error)
            || TryConsumeMp4Arg(args, ref i, options, out error)
            || TryConsumeStyleArg(args, ref i, style, out error))
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

    if (Mp4OptionError(options, OutputFormat.Gif) is string optionError)
    {
        error = optionError;
        return false;
    }

    if (castPath.Length != 0)
        return true;

    error = "cast path is required";
    return false;
}

static bool TryParsePngArgs(
    string[] args,
    out string castPath,
    out string? outputPath,
    out int? fontSizeOverride,
    out string? fontFamilyOverride,
    out string? themePresetOverride,
    out WindowStyle? windowOverride,
    out string? fontFile,
    out StyleOptions style,
    out double? stillAt,
    out string error)
{
    castPath = "";
    style = new StyleOptions();
    outputPath = null;
    fontSizeOverride = null;
    fontFamilyOverride = null;
    themePresetOverride = null;
    windowOverride = null;
    fontFile = null;
    stillAt = null;
    error = "";
    var mp4Options = new Mp4Options();

    for (var i = 1; i < args.Length; i++)
    {
        var arg = args[i];
        if (IsMaxFpsArg(arg))
        {
            error = "--max-fps applies to an animated SVG: a PNG is one screen";
            return false;
        }

        if (TryConsumeAtArg(args, ref i, ref stillAt, out error)
            || TryConsumeFontSizeArg(args, ref i, ref fontSizeOverride, out error)
            || TryConsumeFontFamilyArg(args, ref i, ref fontFamilyOverride, out error)
            || TryConsumeThemeArg(args, ref i, ref themePresetOverride, out error)
            || TryConsumeWindowArg(args, ref i, ref windowOverride, out error)
            || TryConsumeMp4Arg(args, ref i, mp4Options, out error)
            || TryConsumeStyleArg(args, ref i, style, out error))
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

    // A PNG is one screen at the terminal's own size, so the options of a video or an animation do not apply.
    if (mp4Options.FirstVideoOption is string videoOption)
    {
        error = $"{videoOption} applies to {(videoOption == "--crf" ? "mp4" : "mp4 and GIF")}: a PNG is one screen at the terminal's own size";
        return false;
    }

    if (AnimationOption(false, style) is string animationOption)
    {
        error = $"{animationOption} applies to animation: a PNG is one screen, which --at picks";
        return false;
    }

    fontFile = mp4Options.FontFile;
    if (castPath.Length != 0)
        return true;

    error = "cast path is required";
    return false;
}

// Usage after an error goes to stderr and stays short; --help goes to stdout and says what each option
// does, with its range and default (HelpText).
static void PrintInitUsage()
{
    Console.Error.WriteLine("Usage: scenetake init [--shell bash|pwsh|powershell] [scenario.yaml]");
    Console.Error.WriteLine("Run 'scenetake init --help' for what each option does.");
}

static void PrintSvgUsage()
{
    Console.Error.WriteLine("Usage: scenetake svg [--at TIME] [--font-size N] [--font-family FAMILIES] [--theme NAME] [--window none|macos|windows] [--max-fps N] <input.cast> [output.svg]");
    Console.Error.WriteLine("Run 'scenetake svg --help' for what each option does.");
}

static void PrintHelp() =>
    Console.Out.Write(
        $"scenetake {GetAppVersion()}\n" +
        HelpText.Intro + "\n" +
        HelpText.Commands + "\n" +
        HelpText.Recording() + "\n" +
        HelpText.Render(scenario: true) + "\n" +
        HelpText.SvgOptions + "\n" +
        HelpText.StillOptions + "\n" +
        HelpText.Mp4Options + "\n" +
        HelpText.GifOptions + "\n" +
        HelpText.Environment + "\n" +
        HelpText.More);

static void PrintGifHelp() =>
    Console.Out.Write(
        HelpText.GifIntro + "\n" +
        HelpText.Render(scenario: false) + "\n" +
        HelpText.GifOptions + "\n" +
        HelpText.Environment);

static void PrintSvgHelp() =>
    Console.Out.Write(
        HelpText.SvgIntro + "\n" +
        HelpText.Render(scenario: false) + "\n" +
        HelpText.SvgOptions + "\n" +
        HelpText.StillOptions);

static void PrintPngHelp() =>
    Console.Out.Write(
        HelpText.PngIntro + "\n" +
        HelpText.Render(scenario: false, animation: false) + "\n" +
        HelpText.PngOptions + "\n" +
        HelpText.Environment);

static void PrintMp4Help() =>
    Console.Out.Write(
        HelpText.Mp4Intro + "\n" +
        HelpText.Render(scenario: false) + "\n" +
        HelpText.Mp4Options + "\n" +
        HelpText.Environment);

static void PrintInitHelp() => Console.Out.Write(HelpText.Init);

static void PrintVersion()
{
    Console.WriteLine(GetAppVersion());
}

static string GetAppVersion()
{
    var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
    return version?.ToString(3) ?? "0.0.0";
}

static TimedBytes[] ToTimedBytes(IReadOnlyList<PtyCaptureChunk> chunks)
{
    var result = new TimedBytes[chunks.Count];
    for (var i = 0; i < chunks.Count; i++)
        result[i] = new TimedBytes(chunks[i].Time, chunks[i].Data);
    return result;
}

record ShellLaunch(string FileName, string[] Arguments, string DisplayName);

enum OutputFormat
{
    Cast,
    Svg,
    Mp4,
    Png,
    Gif,
}

/// <summary>mp4 options from the command line. Unset values take the <see cref="Mp4Settings"/> defaults.</summary>
/// <summary>
/// <c>--window-title</c>, <c>--background</c>, <c>--margin</c>, and <c>--padding</c>, which the scenario path and the
/// svg and mp4 commands share. Each replaces the scenario's or the cast's value.
/// </summary>
sealed class StyleOptions
{
    internal string? WindowTitle { get; set; }
    internal BackgroundSpec? Background { get; set; }
    internal int? Margin { get; set; }
    internal int? Padding { get; set; }
    internal double? Opacity { get; set; }
    internal int? Blur { get; set; }
    internal bool? Loop { get; set; }
    internal double? EndHold { get; set; }
    internal double? FadeOut { get; set; }
    internal FitMode? Fit { get; set; }
    internal int? CropTop { get; set; }
    internal int? CropBottom { get; set; }
    internal int? CropLeft { get; set; }
    internal int? CropRight { get; set; }

    internal int? Crop(string edge) => edge switch
    {
        "top" => CropTop,
        "bottom" => CropBottom,
        "left" => CropLeft,
        _ => CropRight,
    };

    internal void SetCrop(string edge, int cells)
    {
        switch (edge)
        {
            case "top": CropTop = cells; break;
            case "bottom": CropBottom = cells; break;
            case "left": CropLeft = cells; break;
            default: CropRight = cells; break;
        }
    }

    internal RenderStyleOverrides ToOverrides() =>
        new(WindowTitle, Background, Margin, Padding, Opacity, Blur, Loop, EndHold, FadeOut, Fit, CropTop, CropBottom, CropLeft, CropRight);
}

/// <summary>
/// <c>scenetake capture</c>: one command recorded without a YAML file. It becomes the scenario that a file with one
/// step would hold, and the scenario path records that as it records a file: the typed command, its output, masking,
/// and every output format. The command runs in a PTY unless <c>--pipe</c> is given, so tools color their output
/// as they do in a terminal.
/// </summary>
sealed class CaptureRequest
{
    /// <summary>The command as one argument, as a step's <c>run</c> holds it.</summary>
    internal string? Command { get; set; }

    /// <summary>The command's words after <c>--</c>, which the caller's shell already split.</summary>
    internal string[]? Words { get; set; }

    internal int? Width { get; set; }
    internal int? Height { get; set; }
    internal string? Shell { get; set; }
    internal double? Timeout { get; set; }
    internal bool Pipe { get; set; }

    /// <summary>The command line that the recording types and the shell runs.</summary>
    internal string CommandLine { get; private set; } = "";

    /// <summary>The scenario as YAML. Its text seeds the typing, as a file's does, so the same capture types the same way.</summary>
    internal string Yaml { get; private set; } = "";

    /// <summary>
    /// Builds the scenario, and the path a scenario file for it would have: the files are written next to it, in the
    /// current folder, named after the command (<c>git status</c> writes <c>git.cast</c> and <c>git.svg</c>).
    /// </summary>
    internal bool TryFinish(out string scenarioPath, out string error)
    {
        scenarioPath = "";
        error = "";
        if (Command is not null && Words is not null)
        {
            error = $"capture takes one command: give it as one argument or after --, not both: {Command}";
            return false;
        }

        CommandLine = Words is not null ? JoinWords(Words) : Command ?? "";
        if (string.IsNullOrWhiteSpace(CommandLine))
        {
            error = "capture needs a command: scenetake capture [options] -- <command>";
            return false;
        }

        var yaml = new StringBuilder();
        yaml.Append("title: ").Append(QuoteYaml(CommandLine)).Append('\n');
        if (Width is int width)
            yaml.Append("width: ").Append(width.ToString(CultureInfo.InvariantCulture)).Append('\n');
        if (Height is int height)
            yaml.Append("height: ").Append(height.ToString(CultureInfo.InvariantCulture)).Append('\n');
        if (Shell is not null)
            yaml.Append("shell: ").Append(QuoteYaml(Shell)).Append('\n');
        yaml.Append("steps:\n  - run: ").Append(QuoteYaml(CommandLine)).Append('\n');
        if (!Pipe)
            yaml.Append("    pty: true\n");
        if (Timeout is double timeout)
            yaml.Append("    timeout: ").Append(timeout.ToString("R", CultureInfo.InvariantCulture)).Append('\n');
        Yaml = yaml.ToString();

        scenarioPath = Path.Combine(Directory.GetCurrentDirectory(), OutputName() + ".yaml");
        return true;
    }

    /// <summary>
    /// The words as one command line for the shell that runs it. A word with a space, a quote, or another character a
    /// shell reads is quoted for that shell, so it reaches the command as the caller's shell passed it.
    /// </summary>
    private string JoinWords(string[] words)
    {
        var shell = Shell ?? (OperatingSystem.IsWindows() ? "pwsh" : Environment.GetEnvironmentVariable("SHELL") ?? "bash");
        var name = Path.GetFileNameWithoutExtension(shell.Trim()).ToLowerInvariant();
        var line = new StringBuilder();
        foreach (var word in words)
        {
            if (line.Length > 0)
                line.Append(' ');
            if (word.Length > 0 && IsPlainWord(word))
                line.Append(word);
            else if (name is "pwsh" or "powershell")
                line.Append('\'').Append(word.Replace("'", "''")).Append('\'');
            else if (name is "cmd")
                line.Append('"').Append(word.Replace("\"", "\"\"")).Append('"');
            else
                line.Append('\'').Append(word.Replace("'", "'\\''")).Append('\'');
        }

        return line.ToString();

        // Letters, digits, and the punctuation that no shell reads specially in a word.
        static bool IsPlainWord(string word)
        {
            foreach (var c in word)
            {
                if (!char.IsAsciiLetterOrDigit(c) && "-_./=:,+@%^".IndexOf(c) < 0)
                    return false;
            }

            return true;
        }
    }

    /// <summary>The command's name without its folder and extension, such as <c>git</c> or <c>build</c>, or <c>capture</c>.</summary>
    private string OutputName()
    {
        var first = Words is { Length: > 0 } ? Words[0] : CommandLine.TrimStart().Split([' ', '\t'], 2)[0].Trim('"', '\'');
        var file = Path.GetFileNameWithoutExtension(first.Replace('\\', '/').Split('/')[^1]);
        var name = new StringBuilder();
        foreach (var c in file)
        {
            if (char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.')
                name.Append(c);
        }

        var trimmed = name.ToString().Trim('.');
        return trimmed.Length > 0 ? trimmed : "capture";
    }

    /// <summary>A YAML double-quoted scalar.</summary>
    private static string QuoteYaml(string text)
    {
        var quoted = new StringBuilder("\"");
        foreach (var c in text)
        {
            quoted.Append(c switch
            {
                '\\' => "\\\\",
                '"' => "\\\"",
                '\n' => "\\n",
                '\r' => "\\r",
                '\t' => "\\t",
                < ' ' => $"\\x{(int)c:x2}",
                _ => c.ToString(),
            });
        }

        return quoted.Append('"').ToString();
    }
}

sealed class Mp4Options
{
    internal int? Fps { get; set; }
    internal int? Crf { get; set; }
    internal (int Width, int Height)? MaxSize { get; set; }
    internal (int Width, int Height)? Size { get; set; }
    internal string? FontFile { get; set; }

    /// <summary>The first mp4 option given, for errors when mp4 output is not selected.</summary>
    internal string? FirstOption { get; set; }

    /// <summary>The first of them that only video takes: every one but <c>--font-file</c>, which a PNG takes too.</summary>
    internal string? FirstVideoOption { get; set; }

    /// <summary><c>--crf</c> when given: H.264's quality, which only mp4 takes.</summary>
    internal string? FirstCrfOption { get; set; }

    /// <summary>The first of <c>--fps</c>, <c>--max-size</c>, and <c>--size</c>, which mp4 and GIF take.</summary>
    internal string? FirstFrameOption { get; set; }

    /// <summary>The settings a GIF uses: those of mp4, with the GIF's own default frame rate.</summary>
    internal Mp4Settings ToGifSettings() => ToSettings() with { Fps = Fps ?? GifRender.DefaultFps };

    internal Mp4Settings ToSettings() => new(
        Fps ?? Mp4Settings.DefaultFps,
        Crf ?? Mp4Settings.DefaultCrf,
        MaxSize?.Width ?? 0,
        MaxSize?.Height ?? 0,
        Size?.Width ?? 0,
        Size?.Height ?? 0,
        FontFile);
}


/// <summary>
/// The text of --help for the scenario path and the svg, mp4 and init commands. Each option says what it
/// does, its range and its default, so the README is not needed to use one. ASCII only, as error messages
/// are, so every console code page shows it.
/// </summary>
static class HelpText
{
    internal const string Intro = """
        Records a YAML scenario as an asciinema v3 cast, and optionally as an animated SVG, an mp4,
        a GIF, or a PNG of one screen. The steps run for real: ordinary steps are shown with
        simulated typing, pty steps run in a pseudo-terminal.

        """;

    internal const string Commands = """
        Usage:
          scenetake [options] <scenario.yaml> [output]   Run a scenario and record it
          scenetake svg [options] <input.cast> [output]  Convert a cast file to an animated SVG
          scenetake mp4 [options] <input.cast> [output]  Convert a cast file to an mp4 (needs ffmpeg)
          scenetake png [options] <input.cast> [output]  Draw one screen of a cast file as a PNG
          scenetake gif [options] <input.cast> [output]  Convert a cast file to a GIF (needs ffmpeg)
          scenetake capture [options] -- <command>       Record one command without a scenario file
          scenetake init [--shell NAME] [scenario.yaml]  Write a commented starter scenario
          scenetake --version                            Print the version
          scenetake svg|mp4|png|gif|capture|init --help  Show the options of that command

        [output] names the files without an extension: 'out' writes out.cast and out.svg, and an
        extension given is replaced. Without it, the files are named after the scenario, next to it.

        """;

    internal const string CaptureIntro = """
        Usage: scenetake capture [options] -- <command>
               scenetake capture [options] "<command>"

        Records one command without a scenario file, as a scenario with that one step would: the
        command is typed, then its output follows. Without --format it writes an SVG, and the cast
        next to it. The files go to the current folder, named after the command ('git status'
        writes git.cast and git.svg), unless -o names them.

        """;

    internal const string CaptureOptions = """
        Capture:
          --width N                Terminal columns, 1-512 (default 80)
          --height N               Terminal rows, 1-512 (default 24)
          --shell NAME             Shell that runs the command: bash, pwsh, powershell, or a path
                                   (default: $SHELL or bash, pwsh on Windows)
          --pipe                   Capture the output through pipes instead of a PTY. In a PTY, the
                                   default, tools see a terminal and color their output.
          --timeout N              Stop the command after N seconds, as a step's timeout does, for a
                                   command that does not end by itself (default: none)
          -o, --output NAME        Name the files without an extension: '-o demo' writes demo.cast
                                   and demo.svg

        """;

    /// <summary>The recording options. <c>capture</c> writes an SVG when no format is given, a scenario only the cast.</summary>
    internal static string Recording(bool capture = false) =>
        (capture
            ? """
                Recording:
                  --format cast|svg|mp4|png|gif
                                           Write the cast and an SVG (svg, the default), an mp4, a PNG, or a
                                           GIF rendered from it, or only the cast (cast)

                """
            : """
                Recording:
                  --format cast|svg|mp4|png|gif
                                           Write only the cast (cast, the default), or the cast and an SVG,
                                           an mp4, a PNG, or a GIF rendered from it
                  --save-keys FILE         Write the keys typed in the 'interactive: true' steps to FILE as
                                           'keys' entries, which replay them without a person. Written even
                                           when the run stops at a later step. On Windows a password typed
                                           in a step is saved too.
                  --save-outputs FILE      Write what each step printed to FILE (JSON), for --replay.
                                           Secrets are hidden as in the cast.
                  --replay FILE            Run no command: take each step's output from FILE, written by
                                           --save-outputs. Changes to typing, comments, highlights, timing,
                                           and render apply. A step whose run changed needs a new recording.
                                           pre and post do not run.

                """)
        + RecordingRest;

    private const string RecordingRest = """
          --strict                 Stop at the first step that exits non-zero unless it sets
                                   'continue-on-error: true'. Write no output but the --save-keys
                                   file, and run post. Exit 1. If a post command fails, return that
                                   command's exit code.
          --verbose                Show pre and post labels and phase markers, and the type and stack
                                   of an error that stops the run

        """;

    /// <summary>
    /// The rendering options. On a scenario they override its <c>render</c> settings, the theme only as a
    /// preset under the scenario's own colors. On a cast they replace what the cast header stores.
    /// </summary>
    /// <param name="animation">Whether to list the options of an animation, which a PNG does not take.</param>
    internal static string Render(bool scenario, bool animation = true) =>
        (scenario
            ? "Rendering for SVG, mp4, and PNG (overrides the scenario's render settings, and is stored in the cast):\n"
            : "Rendering (overrides what the cast header stores, and the default applies when neither sets it):\n")
        + """
              --font-size N            Font size, 1-128 (default 16)
              --font-family FAMILIES   CSS font-family list, such as "'JetBrains Mono', monospace"

            """
        + (scenario
            ? """
                  --theme NAME             Color theme preset (default dark). The scenario's render.theme fg,
                                           bg and palette still apply on top of it.

                """
            : """
                  --theme NAME             Color theme, replacing the colors the cast stores (default dark)

                """)
        + """
                                       Names: dark, light, macos-dark, macos-light, dracula, nord,
                                       one-dark, gruvbox-dark, catppuccin-mocha, tokyo-night,
                                       solarized-dark, solarized-light, github-dark, github-light

            """
        + """
              --window none|macos|windows
                                       Draw a window title bar around the terminal (default none)
              --window-title TEXT      Text in the title bar (default: the title the scenario or cast has).
                                       "" shows none.
              --background VALUE       Fill around the terminal: a color such as #1e3c72, 2-4 colors for a
                                       gradient from the top-left corner ("#1e3c72 #2a5298"), or a .png,
                                       .jpg, .gif or .webp file that covers it. A cast keeps colors only.
              --margin N               Pixels around the terminal or window, 0-200 (default 8, 12 with a
                                       window, 32 with a background, and mp4 has one only with a background)
              --padding N              Pixels between the terminal's edge and its text, 0-100 (default from
                                       the font size)
              --opacity N              How much of the window's own fill covers the background, 0-1
                                       (default 1). 0.85 lets the background show through the terminal's
                                       default background and the title bar, and text stays opaque. Needs
                                       --background.
              --blur N                 Blur the background seen through a translucent window, 0-50 pixels
                                       (default 0). Needs --opacity below 1.
              --fit none|height|width|both
                                       Move the bottom edge (height), the right edge (width), or both in to
                                       the last row and column the recording shows, the cursor included
                                       (default height: no empty rows below the content). none draws the
                                       whole terminal. The cast keeps the terminal's size.
              --crop-top N, --crop-bottom N, --crop-left N, --crop-right N
                                       Cut N rows or columns from that edge before --fit, 0-511 (default 0)

            """
        + (animation
            ? """
                  --end-hold N             Seconds the final screen stays before the SVG starts again or the
                                           mp4 ends, 0-60 (default: as long as the pause before the last output)
                  --fade-out N             Fade the terminal's contents out over N seconds after the end hold,
                                           0-10 (default 0). The window and the background stay.

                """
            : "");

    internal const string SvgOptions = """
        SVG (--format svg, or the svg command):
          --max-fps N              Cap the animation frame rate, 1-120. 0 keeps every frame (default).
          --no-loop                Play the animation once and stop on the final screen (default: loop)
          --loop                   Loop, also when the scenario or the cast says not to

        """;

    internal const string StillOptions = """
        Still image (--format png, the png command, or --at with SVG):
          --at TIME                Draw the screen shown TIME seconds into the recording, such as 2.5,
                                   or end for the final screen. A PNG shows the final screen without it.
                                   With SVG, --at makes a still SVG without animation, which --max-fps,
                                   --no-loop, --end-hold and --fade-out do not apply to.

        """;

    internal const string PngOptions = """
        PNG (the png command, or --format png):
          --at TIME                Draw the screen shown TIME seconds into the recording, such as 2.5,
                                   or end for the final screen (default end)
          --font-file PATH         A font file (TTF, OTF, TTC) to use before the --font-family fonts

        The PNG is the frame an mp4 shows at that time, at the terminal's own size, with a palette
        when it has 256 colors or fewer. It needs no ffmpeg, except to decode a --background image.

        """;

    internal const string PngIntro = """
        Usage: scenetake png [options] <input.cast> [output]

        Draws one screen of an asciinema v2 or v3 cast file as a PNG. [output] names the PNG. Without
        it, the PNG is written next to the cast with the same name and .png.

        """;

    internal const string Mp4Options = """
        mp4 (--format mp4 or the mp4 command, needs ffmpeg with libx264):
          --fps N                  Frame rate, 1-60 (default 30)
          --crf N                  H.264 quality, 1-51, where a higher value makes a smaller file (default 23)
          --max-size WxH           Scale down to fit in WxH, keeping the shape and never scaling up.
                                   1280x1024 fits X (Twitter) uploads.
          --size WxH               Make the video exactly WxH (both even): the terminal is centered on
                                   the theme background, and a larger one is scaled down to fit.
                                   Not with --max-size.
          --font-file PATH         A font file (TTF, OTF, TTC) to use before the --font-family fonts.
                                   PNG and GIF take it too.

        """;

    internal const string GifOptions = """
        GIF (--format gif or the gif command, needs ffmpeg):
          --fps N                  Frame rate, 1-50 (default 20). A GIF counts each frame's delay in
                                   hundredths of a second. A lower rate makes a full-screen program
                                   smaller, and changes little for typing.
          --max-size WxH           Scale down to fit in WxH, keeping the shape and never scaling up.
                                   Scaling blurs text and can make a GIF larger. For a smaller GIF,
                                   such as Zenn's 3MB, use a smaller --font-size.
          --size WxH               Make the GIF exactly WxH (both even), as for mp4. Not with --max-size.
          --no-loop                Play the GIF once and stop on the final screen (default: loop)
          --loop                   Loop, also when the scenario or the cast says not to
          --font-file PATH         A font file (TTF, OTF, TTC) to use before the --font-family fonts

        The frames are those of an mp4, with one palette of 256 colors for the whole GIF.

        """;

    internal const string GifIntro = """
        Usage: scenetake gif [options] <input.cast> [output]

        Converts an asciinema v2 or v3 cast file to an animated GIF with ffmpeg (any build). [output]
        names the GIF. Without it, the GIF is written next to the cast with the same name and .gif.

        """;

    internal const string Environment = """
        Environment:
          SCENETAKE_FFMPEG         The ffmpeg to use for mp4 and GIF, and for a PNG's background image,
                                   instead of the one on PATH

        """;

    internal const string More = """
        Scenario format, samples and output modes: https://github.com/guitarrapc/scenetake

        """;

    internal const string SvgIntro = """
        Usage: scenetake svg [options] <input.cast> [output]

        Converts an asciinema v2 or v3 cast file to an animated SVG. [output] names the SVG. Without
        it, the SVG is written next to the cast with the same name and .svg.

        """;

    internal const string Mp4Intro = """
        Usage: scenetake mp4 [options] <input.cast> [output]

        Converts an asciinema v2 or v3 cast file to an H.264 mp4 with ffmpeg (libx264). [output]
        names the mp4. Without it, the mp4 is written next to the cast with the same name and .mp4.
        Install ffmpeg with 'winget install Gyan.FFmpeg' or 'scoop install ffmpeg' (Windows),
        'brew install ffmpeg' (macOS), or 'sudo apt install ffmpeg' (Debian, Ubuntu).

        """;

    internal const string Init = """
        Usage: scenetake init [--shell bash|pwsh|powershell] [scenario.yaml]

        Writes a commented starter scenario to scenario.yaml in the current folder, or to the path
        given. An existing file is not overwritten.

          --shell bash|pwsh|powershell
                                   Write the steps for this shell and set 'shell:' in the file.
                                   Without it, the steps suit the default shell: pwsh on Windows,
                                   $SHELL elsewhere.

        """;
}
