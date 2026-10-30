#:sdk Microsoft.NET.Sdk
#:property TargetFramework=net10.0
#:property Nullable=enable
#:property ImplicitUsings=enable

// mp4 output through a published scenetake binary: CLI rules, ffmpeg failures, and real encodes.
// Real encodes need ffmpeg (with libx264) and ffprobe on PATH. They print "skip" otherwise.
//
// Usage (from repository root):
//   SCENETAKE_BIN=/path/to/scenetake dotnet run tests/mp4_ffmpeg_test.cs

using System.Diagnostics;

var repoRoot = FindRepoRoot(Directory.GetCurrentDirectory());
var scenetake = Environment.GetEnvironmentVariable("SCENETAKE_BIN");
if (string.IsNullOrWhiteSpace(scenetake) || !File.Exists(scenetake))
{
    Console.Error.WriteLine("skip mp4 integration tests: set SCENETAKE_BIN to a published scenetake binary");
    return 0;
}

var work = Directory.CreateTempSubdirectory("scenetake-mp4-test-").FullName;
var failures = 0;
try
{
    failures += Run("Mp4OptionsRequireMp4Format", () => Mp4OptionsRequireMp4Format(scenetake, repoRoot, work));
    failures += Run("MaxFpsRejectedForMp4", () => MaxFpsRejectedForMp4(scenetake, repoRoot, work));
    failures += Run("MissingFfmpegShowsInstallHint", () => MissingFfmpegShowsInstallHint(scenetake, repoRoot, work));
    failures += Run("FfmpegWithoutLibx264IsRejected", () => FfmpegWithoutLibx264IsRejected(scenetake, repoRoot, work));
    failures += Run("FfmpegFailureLeavesNoFile", () => FfmpegFailureLeavesNoFile(scenetake, repoRoot, work));
    failures += Run("ScenarioKeepsCastWhenEncodeFails", () => ScenarioKeepsCastWhenEncodeFails(scenetake, repoRoot, work));
    failures += Run("ScenarioChecksFfmpegBeforePre", () => ScenarioChecksFfmpegBeforePre(scenetake, repoRoot, work));
    failures += Run("ScenarioChecksFontBeforePre", () => ScenarioChecksFontBeforePre(scenetake, repoRoot, work));
    failures += Run("ScenarioRejectsOversizedTerminal", () => ScenarioRejectsOversizedTerminal(scenetake, repoRoot, work));
    failures += Run("PostRunsAndStaleMp4GoesWhenEncodeFails", () => PostRunsAndStaleMp4GoesWhenEncodeFails(scenetake, repoRoot, work));
    failures += Run("StaleMp4GoesWhenCastIsBad", () => StaleMp4GoesWhenCastIsBad(scenetake, repoRoot, work));
    failures += Run("CliRulesForValuesAndFormats", () => CliRulesForValuesAndFormats(scenetake, repoRoot, work));
    failures += Run("SubcommandsNeverReplaceTheirInput", () => SubcommandsNeverReplaceTheirInput(scenetake, repoRoot, work));
    failures += Run("ScenarioChecksOutputPaths", () => ScenarioChecksOutputPaths(scenetake, repoRoot, work));
    failures += Run("ScenarioErrorsStopBeforePre", () => ScenarioErrorsStopBeforePre(scenetake, repoRoot, work));
    failures += Run("ScenarioErrorsAfterPreRunPost", () => ScenarioErrorsAfterPreRunPost(scenetake, repoRoot, work));
    failures += Run("SvgFailureWithAnOpenOutputIsReported", () => SvgFailureWithAnOpenOutputIsReported(scenetake, repoRoot, work));
    failures += Run("ScenarioChecksFrameSizeBeforePre", () => ScenarioChecksFrameSizeBeforePre(scenetake, repoRoot, work));
    failures += Run("EarlierMp4GoesWhenEncodingStarts", () => EarlierMp4GoesWhenEncodingStarts(scenetake, repoRoot, work));

    // The test copies a font from the Linux font folders and installs it where XDG_DATA_HOME points the
    // Linux scan. macOS has neither: it keeps fonts in other folders and scans ~/Library/Fonts. Windows
    // reads fixed font folders.
    if (OperatingSystem.IsLinux())
        failures += Run("FontInstalledByPreOrStepIsUsed", () => FontInstalledByPreOrStepIsUsed(scenetake, repoRoot, work));
    else
        Console.Error.WriteLine("skip FontInstalledByPreOrStepIsUsed: Linux only (XDG_DATA_HOME)");

    // Windows resolves its shells to full paths by other rules, so this Unix lookup has nothing to test there.
    if (OperatingSystem.IsWindows())
        Console.Error.WriteLine("skip UnixShellStartsAsNamed: Unix only");
    else
        failures += Run("UnixShellStartsAsNamed", () => UnixShellStartsAsNamed(scenetake, repoRoot, work));

    // Only Windows looks pwsh, powershell, cmd and bash up by name, so only there could a path to one of
    // them start another file.
    if (OperatingSystem.IsWindows())
        failures += Run("WindowsShellPathStartsThatFile", () => WindowsShellPathStartsThatFile(scenetake, repoRoot, work));
    else
        Console.Error.WriteLine("skip WindowsShellPathStartsThatFile: Windows only");

    if (FindOnPath("ffmpeg") is null || FindOnPath("ffprobe") is null)
    {
        Console.Error.WriteLine("skip mp4 encode tests: ffmpeg and ffprobe not found on PATH");
    }
    else
    {
        failures += Run("SubcommandWritesH264", () => SubcommandWritesH264(scenetake, repoRoot, work));
        failures += Run("MaxSizeDownscales", () => MaxSizeDownscales(scenetake, repoRoot, work));
        failures += Run("ScenarioFormatMp4", () => ScenarioFormatMp4(scenetake, repoRoot, work));

        // Windows delivers Ctrl+C per console. Sending one to a child needs a separate console,
        // which is too fragile for CI. The Windows behavior was checked by hand (spec_mp4.md).
        if (OperatingSystem.IsWindows())
            Console.Error.WriteLine("skip CtrlCLeavesNoFiles: Unix only (SIGINT)");
        else
            failures += Run("CtrlCLeavesNoFiles", () => CtrlCLeavesNoFiles(scenetake, repoRoot, work));
    }
}
finally
{
    Directory.Delete(work, recursive: true);
}

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
    catch (SkipTestException ex)
    {
        Console.Error.WriteLine($"skip {name}: {ex.Message}");
        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"FAIL {name}: {ex}");
        return 1;
    }
}

static bool Mp4OptionsRequireMp4Format(string scenetake, string repoRoot, string work)
{
    var scenario = WriteScenario(work, "options.yaml");
    var (exit, stderr) = Exec(scenetake, repoRoot, ["--fps", "30", scenario]);
    var (svgExit, svgStderr) = Exec(scenetake, repoRoot, ["--format", "svg", "--max-size=1280x1024", scenario]);
    return exit == 1 && stderr.Contains("--fps requires --format mp4", StringComparison.Ordinal)
        && svgExit == 1 && svgStderr.Contains("--max-size requires --format mp4", StringComparison.Ordinal);
}

static bool MaxFpsRejectedForMp4(string scenetake, string repoRoot, string work)
{
    var (exit, stderr) = Exec(scenetake, repoRoot, ["mp4", "--max-fps", "20", "samples/basic.cast", Path.Combine(work, "x.mp4")]);
    var (runExit, runStderr) = Exec(scenetake, repoRoot, ["--format", "mp4", "--max-fps", "20", WriteScenario(work, "maxfps.yaml")]);
    return exit == 1 && stderr.Contains("use --fps for mp4", StringComparison.Ordinal)
        && runExit == 1 && runStderr.Contains("use --fps for mp4", StringComparison.Ordinal);
}

static bool MissingFfmpegShowsInstallHint(string scenetake, string repoRoot, string work)
{
    var output = Path.Combine(work, "missing.mp4");
    var (exit, stderr) = Exec(scenetake, repoRoot, ["mp4", "samples/basic.cast", output],
        new() { ["SCENETAKE_FFMPEG"] = Path.Combine(work, "no-such-ffmpeg") });
    return exit == 1 && stderr.Contains("SCENETAKE_FFMPEG points to a file that does not exist", StringComparison.Ordinal)
        && stderr.Contains("Install ffmpeg with libx264", StringComparison.Ordinal)
        && !File.Exists(output);
}

static bool FfmpegWithoutLibx264IsRejected(string scenetake, string repoRoot, string work)
{
    var fake = WriteFakeFfmpeg(work, "no-libx264", hasLibx264: false);
    var (exit, stderr) = Exec(scenetake, repoRoot, ["mp4", "samples/basic.cast", Path.Combine(work, "nolibx264.mp4")],
        new() { ["SCENETAKE_FFMPEG"] = fake });
    return exit == 1 && stderr.Contains("ffmpeg has no libx264 encoder", StringComparison.Ordinal);
}

static bool FfmpegFailureLeavesNoFile(string scenetake, string repoRoot, string work)
{
    var fake = WriteFakeFfmpeg(work, "failing", hasLibx264: true);
    var output = Path.Combine(work, "failing.mp4");
    var (exit, stderr) = Exec(scenetake, repoRoot, ["mp4", "samples/basic.cast", output], new() { ["SCENETAKE_FFMPEG"] = fake });
    return exit == 1 && stderr.Contains("mp4 render failed", StringComparison.Ordinal)
        && stderr.Contains("simulated encoder failure", StringComparison.Ordinal)
        && !File.Exists(output)
        && !Directory.EnumerateFiles(work, "*.partial").Any();
}

static bool ScenarioKeepsCastWhenEncodeFails(string scenetake, string repoRoot, string work)
{
    var fake = WriteFakeFfmpeg(work, "failing-scenario", hasLibx264: true);
    var stem = Path.Combine(work, "kept");
    var (exit, _) = Exec(scenetake, repoRoot, ["--format", "mp4", WriteScenario(work, "kept.yaml"), stem], new() { ["SCENETAKE_FFMPEG"] = fake });
    return exit == 1 && File.Exists(stem + ".cast") && !File.Exists(stem + ".mp4");
}

static bool SubcommandWritesH264(string scenetake, string repoRoot, string work)
{
    var output = Path.Combine(work, "basic.mp4");
    var (exit, stderr) = Exec(scenetake, repoRoot, ["mp4", "samples/basic.cast", output]);
    if (exit != 0)
    {
        Console.Error.WriteLine(stderr);
        return false;
    }

    var stream = Probe(output);
    return stream["codec_name"] == "h264" && stream["profile"] == "High" && stream["pix_fmt"] == "yuv420p"
        && stream["color_space"] == "bt709" && stream["r_frame_rate"] == "30/1"
        && stderr.Contains("Written:", StringComparison.Ordinal);
}

static bool MaxSizeDownscales(string scenetake, string repoRoot, string work)
{
    var output = Path.Combine(work, "small.mp4");
    var (exit, stderr) = Exec(scenetake, repoRoot, ["mp4", "--max-size", "320x320", "--fps", "12", "samples/pty.cast", output]);
    if (exit != 0)
    {
        Console.Error.WriteLine(stderr);
        return false;
    }

    var stream = Probe(output);
    var width = int.Parse(stream["width"]);
    var height = int.Parse(stream["height"]);
    return width <= 320 && height <= 320 && width % 2 == 0 && height % 2 == 0 && stream["r_frame_rate"] == "12/1";
}

static bool ScenarioFormatMp4(string scenetake, string repoRoot, string work)
{
    var stem = Path.Combine(work, "scenario");
    var (exit, stderr) = Exec(scenetake, repoRoot, ["--format", "mp4", WriteScenario(work, "scenario.yaml"), stem]);
    if (exit != 0)
    {
        Console.Error.WriteLine(stderr);
        return false;
    }

    return File.Exists(stem + ".cast") && Probe(stem + ".mp4")["codec_name"] == "h264";
}

static bool CtrlCLeavesNoFiles(string scenetake, string repoRoot, string work)
{
    // A 60s cast with changing text keeps the encode running long enough to interrupt it. An output
    // from an earlier run must go too, though no catch block runs after Ctrl+C.
    var dir = Directory.CreateDirectory(Path.Combine(work, "ctrlc")).FullName;
    var cast = Path.Combine(dir, "long.cast");
    var lines = new List<string> { """{"version":3,"term":{"cols":80,"rows":24}}""" };
    for (var i = 0; i < 1200; i++)
        lines.Add($"[0.05, \"o\", \"line {i} {new string((char)('a' + i % 26), 60)}\\r\\n\"]");
    File.WriteAllLines(cast, lines);
    var output = Path.Combine(dir, "long.mp4");
    File.WriteAllText(output, "from an earlier run");

    var startInfo = new ProcessStartInfo(scenetake) { WorkingDirectory = repoRoot, RedirectStandardError = true, UseShellExecute = false };
    foreach (var argument in new[] { "mp4", cast, output })
        startInfo.ArgumentList.Add(argument);
    using var process = Process.Start(startInfo)!;
    var stderr = process.StandardError.ReadToEndAsync();

    var waited = Stopwatch.StartNew();
    while (!Directory.EnumerateFiles(dir, "*.partial").Any() && !process.HasExited && waited.Elapsed < TimeSpan.FromSeconds(60))
        Thread.Sleep(50);
    if (process.HasExited)
    {
        Console.Error.WriteLine($"scenetake exited before encoding started: {stderr.Result}");
        return false;
    }

    Thread.Sleep(300);
    using (var kill = Process.Start("kill", ["-INT", process.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)]))
        kill.WaitForExit();

    if (!process.WaitForExit(30_000))
    {
        process.Kill(entireProcessTree: true);
        Console.Error.WriteLine("scenetake did not stop after SIGINT");
        return false;
    }

    var left = Directory.EnumerateFiles(dir).Select(Path.GetFileName).Where(name => name != "long.cast").ToList();
    if (left.Count != 0)
        Console.Error.WriteLine($"left behind: {string.Join(", ", left)}");
    return process.ExitCode != 0 && left.Count == 0;
}

static string WriteScenario(string work, string name)
{
    var path = Path.Combine(work, name);
    File.WriteAllText(path, """
        title: "mp4 test"
        width: 40
        height: 6
        steps:
          - run: echo hello
        """);
    return path;
}

/// <summary>
/// A stand-in ffmpeg: answers -encoders (with or without libx264), and fails any encode after
/// writing to its output file (the last argument), as a real ffmpeg does once it has started.
/// Without that file, a test that no partial file is left would pass even with no cleanup.
/// </summary>
static string WriteFakeFfmpeg(string work, string name, bool hasLibx264)
{
    var encoder = hasLibx264 ? " V....D libx264              libx264 H.264" : " V....D libopenh264          OpenH264 H.264";
    if (OperatingSystem.IsWindows())
    {
        var path = Path.Combine(work, name + ".cmd");
        File.WriteAllText(path,
            "@echo off\r\n" +
            $"echo %* | findstr /C:\"-encoders\" >nul && (echo {encoder}& exit /b 0)\r\n" +
            ":last\r\n" +
            "if not \"%~2\"==\"\" (shift & goto last)\r\n" +
            "echo partial> \"%~1\"\r\necho simulated encoder failure 1>&2\r\nexit /b 3\r\n");
        return path;
    }
    else
    {
        var path = Path.Combine(work, name);
        File.WriteAllText(path,
            "#!/bin/sh\n" +
            $"for a in \"$@\"; do [ \"$a\" = -encoders ] && {{ echo \"{encoder}\"; exit 0; }}; done\n" +
            "for last; do :; done\n" +
            "echo partial > \"$last\"\necho \"simulated encoder failure\" >&2\nexit 3\n");
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        return path;
    }
}

/// <summary>A stand-in ffmpeg for Unix that answers -encoders with libx264, reads every frame, writes its output file (the last argument), and exits 0.</summary>
static string WriteSucceedingFakeFfmpeg(string work, string name)
{
    var path = Path.Combine(work, name);
    File.WriteAllText(path,
        "#!/bin/sh\n" +
        "for a in \"$@\"; do [ \"$a\" = -encoders ] && { echo \" V....D libx264              libx264 H.264\"; exit 0; }; done\n" +
        "cat >/dev/null\n" +
        "for last; do :; done\n" +
        "echo video > \"$last\"\nexit 0\n");
    File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    return path;
}

static (int ExitCode, string Stderr) Exec(string file, string workingDirectory, string[] arguments, Dictionary<string, string>? environment = null)
{
    var startInfo = new ProcessStartInfo(file)
    {
        WorkingDirectory = workingDirectory,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
    };
    foreach (var argument in arguments)
        startInfo.ArgumentList.Add(argument);
    foreach (var (name, value) in environment ?? [])
        startInfo.Environment[name] = value;

    using var process = Process.Start(startInfo)!;
    var stdout = process.StandardOutput.ReadToEndAsync();
    var stderr = process.StandardError.ReadToEnd();
    process.WaitForExit();
    _ = stdout.Result;
    return (process.ExitCode, stderr);
}

static Dictionary<string, string> Probe(string path)
{
    var startInfo = new ProcessStartInfo(FindOnPath("ffprobe")!)
    {
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
    };
    foreach (var argument in new[] { "-v", "error", "-select_streams", "v:0", "-show_entries", "stream=codec_name,profile,pix_fmt,color_space,r_frame_rate,width,height", "-of", "default=nw=1", path })
        startInfo.ArgumentList.Add(argument);

    using var process = Process.Start(startInfo)!;
    var output = process.StandardOutput.ReadToEnd();
    process.WaitForExit();
    var exit = process.ExitCode;
    var values = new Dictionary<string, string>(StringComparer.Ordinal);
    foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
    {
        var separator = line.IndexOf('=');
        if (separator > 0)
            values[line[..separator]] = line[(separator + 1)..];
    }

    return exit == 0 ? values : throw new InvalidOperationException($"ffprobe failed for {path}");
}

static string? FindOnPath(string name)
{
    var fileName = OperatingSystem.IsWindows() ? name + ".exe" : name;
    foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
    {
        var candidate = Path.Combine(directory.Trim().Trim('"'), fileName);
        if (File.Exists(candidate))
            return candidate;
    }

    return null;
}

static string FindRepoRoot(string start)
{
    var current = new DirectoryInfo(start);
    while (current is not null)
    {
        if (File.Exists(Path.Combine(current.FullName, "scenetake.cs")))
            return current.FullName;
        current = current.Parent;
    }

    throw new InvalidOperationException("repository root not found");
}

static bool ScenarioChecksFfmpegBeforePre(string scenetake, string repoRoot, string work)
{
    // ffmpeg is checked before pre runs, so a missing ffmpeg records nothing.
    var directory = Directory.CreateDirectory(Path.Combine(work, "check-ffmpeg")).FullName;
    var scenario = WriteScenarioWithHooks(directory, "s.yaml");
    var (exit, stderr) = Exec(scenetake, repoRoot, ["--format", "mp4", scenario], new() { ["SCENETAKE_FFMPEG"] = Path.Combine(directory, "no-such-ffmpeg") });
    return exit == 1 && stderr.Contains("SCENETAKE_FFMPEG points to a file that does not exist", StringComparison.Ordinal)
        && !File.Exists(Path.Combine(directory, "s.cast")) && !File.Exists(Path.Combine(directory, "pre-ran.txt"));
}

static bool ScenarioChecksFontBeforePre(string scenetake, string repoRoot, string work)
{
    // The font is checked before pre runs. The fake ffmpeg passes the libx264 check, so the font is what fails.
    var directory = Directory.CreateDirectory(Path.Combine(work, "check-font")).FullName;
    var fake = WriteFakeFfmpeg(directory, "ffmpeg-ok", hasLibx264: true);
    var scenario = WriteScenarioWithHooks(directory, "s.yaml");
    var missingFont = Path.Combine(directory, "no-such-font.ttf");
    var (exit, stderr) = Exec(scenetake, repoRoot, ["--format", "mp4", "--font-file", missingFont, scenario], new() { ["SCENETAKE_FFMPEG"] = fake });
    return exit == 1 && stderr.Contains(missingFont, StringComparison.Ordinal)
        && !File.Exists(Path.Combine(directory, "s.cast")) && !File.Exists(Path.Combine(directory, "pre-ran.txt"));
}

static bool ScenarioRejectsOversizedTerminal(string scenetake, string repoRoot, string work)
{
    // The cast format allows 1–512 columns and rows. A larger scenario fails before pre, instead of
    // after the whole recording when SVG reads the cast back.
    var directory = Directory.CreateDirectory(Path.Combine(work, "oversized")).FullName;
    var scenario = WriteScenarioWithHooks(directory, "s.yaml", width: 600);
    var (exit, stderr) = Exec(scenetake, repoRoot, ["--format", "svg", scenario]);
    return exit == 1 && stderr.Contains("width must be between 1 and 512", StringComparison.Ordinal)
        && !File.Exists(Path.Combine(directory, "s.cast")) && !File.Exists(Path.Combine(directory, "pre-ran.txt"));
}

static bool PostRunsAndStaleMp4GoesWhenEncodeFails(string scenetake, string repoRoot, string work)
{
    // After an encode failure the cast is kept, post still runs, and an .mp4 from an earlier run is
    // removed, because it no longer matches the new cast.
    var directory = Directory.CreateDirectory(Path.Combine(work, "encode-fails")).FullName;
    var fake = WriteFakeFfmpeg(directory, "failing", hasLibx264: true);
    var scenario = WriteScenarioWithHooks(directory, "s.yaml");
    var stale = Path.Combine(directory, "s.mp4");
    File.WriteAllText(stale, "from an earlier run");
    var (exit, _) = Exec(scenetake, repoRoot, ["--format", "mp4", scenario], new() { ["SCENETAKE_FFMPEG"] = fake });
    return exit == 1 && File.Exists(Path.Combine(directory, "s.cast")) && File.Exists(Path.Combine(directory, "post-ran.txt"))
        && !File.Exists(stale) && !Directory.EnumerateFiles(directory, "*.partial").Any();
}

static bool StaleMp4GoesWhenCastIsBad(string scenetake, string repoRoot, string work)
{
    // As with SVG, an mp4 subcommand that fails on its cast removes an older output of the same name.
    var directory = Directory.CreateDirectory(Path.Combine(work, "bad-cast")).FullName;
    var fake = WriteFakeFfmpeg(directory, "ffmpeg-ok", hasLibx264: true);
    var cast = Path.Combine(directory, "bad.cast");
    File.WriteAllText(cast, "{\"version\":3}\n");
    var stale = Path.Combine(directory, "bad.mp4");
    File.WriteAllText(stale, "from an earlier run");
    var (exit, stderr) = Exec(scenetake, repoRoot, ["mp4", cast], new() { ["SCENETAKE_FFMPEG"] = fake });
    return exit == 1 && stderr.Contains("cast header is missing terminal size", StringComparison.Ordinal) && !File.Exists(stale);
}

static bool CliRulesForValuesAndFormats(string scenetake, string repoRoot, string work)
{
    // --max-fps only applies to SVG. A value that is another option means the value is missing. Range errors
    // use plain ASCII, which every console code page can show.
    var scenario = WriteScenario(work, "rules.yaml");
    var (maxFpsExit, maxFpsStderr) = Exec(scenetake, repoRoot, ["--max-fps", "10", scenario]);
    var (fontExit, fontStderr) = Exec(scenetake, repoRoot, ["--format", "mp4", "--font-file", "--fps", "30", scenario]);
    var (fpsExit, fpsStderr) = Exec(scenetake, repoRoot, ["mp4", "--fps", "99", "samples/basic.cast"]);
    return maxFpsExit == 1 && maxFpsStderr.Contains("--max-fps requires --format svg", StringComparison.Ordinal)
        && fontExit == 1 && fontStderr.Contains("--font-file requires a value", StringComparison.Ordinal)
        && fpsExit == 1 && fpsStderr.Contains("--fps must be between 1 and 60: 99", StringComparison.Ordinal);
}

static bool SubcommandsNeverReplaceTheirInput(string scenetake, string repoRoot, string work)
{
    // Without an output path the output takes the input's name with the new extension, and an explicit
    // output gets that extension too (clip.cast becomes clip.mp4). An input with that extension is then
    // its own output: a failed render deletes it, and the scenario named scenario.cast would be
    // overwritten by its recording. Links reach the same file under another path, which comparing
    // the paths as written misses.
    var directory = Directory.CreateDirectory(Path.Combine(work, "same-path")).FullName;
    var fake = WriteFakeFfmpeg(directory, "ffmpeg-ok", hasLibx264: true);
    var video = Path.Combine(directory, "clip.mp4");
    File.WriteAllText(video, "a video, not a cast");
    var image = Path.Combine(directory, "image.svg");
    File.WriteAllText(image, "<svg/>");
    var scenario = Path.Combine(directory, "scenario.cast");
    File.Copy(WriteScenario(directory, "source.yaml"), scenario);
    var scenarioText = File.ReadAllText(scenario);

    var (mp4Exit, mp4Stderr) = Exec(scenetake, repoRoot, ["mp4", video], new() { ["SCENETAKE_FFMPEG"] = fake });
    var (swappedExit, _) = Exec(scenetake, repoRoot, ["mp4", video, Path.Combine(directory, "clip.cast")], new() { ["SCENETAKE_FFMPEG"] = fake });
    var (svgExit, svgStderr) = Exec(scenetake, repoRoot, ["svg", image]);
    var (scenarioExit, scenarioStderr) = Exec(scenetake, repoRoot, [scenario]);

    var real = Directory.CreateDirectory(Path.Combine(directory, "real")).FullName;
    var linkedVideo = Path.Combine(real, "linked.mp4");
    File.WriteAllText(linkedVideo, "another video");
    var linkedCast = Path.Combine(real, "in.cast");
    File.WriteAllText(linkedCast, "{\"version\":3,\"term\":{\"cols\":8,\"rows\":2}}\n[0.1, \"o\", \"x\"]\n");
    var castText = File.ReadAllText(linkedCast);
    var link = Path.Combine(directory, "link");
    var linkExit = 1;
    var fileLinkExit = 1;
    var chainedLinkExit = 1;
    var relativeResolved = true;
    if (TryLinkDirectory(link, real))
    {
        (linkExit, _) = Exec(scenetake, repoRoot, ["mp4", linkedVideo, Path.Combine(link, "linked.mp4")], new() { ["SCENETAKE_FFMPEG"] = fake });

        // The output may be a file link, or a link whose target passes through the directory link. On
        // Unix a link's target comes back as written, so the second needs the target followed too.
        // macOS temporary folders sit behind such a link (/var to /private/var).
        try
        {
            var fileLink = Path.Combine(real, "out.svg");
            File.CreateSymbolicLink(fileLink, linkedCast);
            (fileLinkExit, _) = Exec(scenetake, repoRoot, ["svg", linkedCast, fileLink]);
            var chainedLink = Path.Combine(real, "chained.svg");
            File.CreateSymbolicLink(chainedLink, Path.Combine(link, "in.cast"));
            (chainedLinkExit, _) = Exec(scenetake, repoRoot, ["svg", linkedCast, chainedLink]);

            // A relative link target reached through a directory link. Unix resolves it from the folder the
            // link sits in, real/inner, so it names the input. Windows resolves it from the path as opened,
            // same-path/inner-link, so it names same-path/in.cast, which is not the input and gets the SVG.
            var inner = Directory.CreateDirectory(Path.Combine(real, "inner")).FullName;
            File.CreateSymbolicLink(Path.Combine(inner, "up.svg"), Path.Combine("..", "in.cast"));
            var innerLink = Path.Combine(directory, "inner-link");
            if (TryLinkDirectory(innerLink, inner))
            {
                var (relativeExit, relativeStderr) = Exec(scenetake, repoRoot, ["svg", linkedCast, Path.Combine(innerLink, "up.svg")]);
                var refused = relativeExit == 1 && relativeStderr.Contains("would replace the input file", StringComparison.Ordinal);
                relativeResolved = OperatingSystem.IsWindows() ? relativeExit == 0 && !refused : refused;
                Directory.Delete(innerLink);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine("  (no file link: this machine does not allow one)");
        }

        // A recursive delete of the work folder fails on a Windows junction, so the link goes first.
        Directory.Delete(link);
    }
    else
    {
        Console.Error.WriteLine("  (no directory link: this machine does not allow one)");
    }

    // Unix applies ".." in a link's target to the folder that the link before it reached, so this link
    // names real/in.cast. Removing ".." as text would give same-path/in.cast, which does not exist.
    // Windows removes ".." as text, so the case is Unix only.
    var dotDotExit = 1;
    if (!OperatingSystem.IsWindows())
    {
        var inner = Directory.CreateDirectory(Path.Combine(real, "inner")).FullName;
        Directory.CreateSymbolicLink(Path.Combine(directory, "sub"), inner);
        var dotDotLink = Path.Combine(directory, "dot-dot.svg");
        File.CreateSymbolicLink(dotDotLink, "sub/../in.cast");
        (dotDotExit, _) = Exec(scenetake, repoRoot, ["svg", linkedCast, dotDotLink]);
    }

    // On Windows a \\?\ or \??\ path names the same file in another spelling, with no link to follow. It
    // must still be found while another process holds the input open without read sharing. The input is
    // not a cast, so a render that missed it would also exit 1. Each run must say why it stopped.
    var spellings = new List<(int Exit, string Stderr)>();
    if (OperatingSystem.IsWindows())
    {
        spellings.Add(Exec(scenetake, repoRoot, ["svg", image, @"\\?\" + image]));
        spellings.Add(Exec(scenetake, repoRoot, ["svg", image, @"\??\" + image]));
        using (new FileStream(image, FileMode.Open, FileAccess.Read, FileShare.Delete))
            spellings.Add(Exec(scenetake, repoRoot, ["svg", image, @"\\?\" + image]));
    }

    return mp4Exit == 1 && mp4Stderr.Contains("would replace the input file", StringComparison.Ordinal) && swappedExit == 1
        && svgExit == 1 && svgStderr.Contains("would replace the input file", StringComparison.Ordinal)
        && scenarioExit == 1 && scenarioStderr.Contains("would replace the input file", StringComparison.Ordinal)
        && linkExit == 1 && File.ReadAllText(linkedVideo) == "another video"
        && fileLinkExit == 1 && chainedLinkExit == 1 && dotDotExit == 1 && relativeResolved
        && spellings.All(run => run.Exit == 1 && run.Stderr.Contains("would replace the input file", StringComparison.Ordinal))
        && File.ReadAllText(linkedCast) == castText
        && File.ReadAllText(video) == "a video, not a cast" && File.ReadAllText(image) == "<svg/>" && File.ReadAllText(scenario) == scenarioText;
}

/// <summary>Makes <paramref name="link"/> a directory link to <paramref name="target"/>: a junction on Windows, which needs no privilege, or a symbolic link elsewhere.</summary>
static bool TryLinkDirectory(string link, string target)
{
    if (!OperatingSystem.IsWindows())
    {
        Directory.CreateSymbolicLink(link, target);
        return true;
    }

    var (exit, _) = Exec("cmd.exe", Path.GetDirectoryName(link)!, ["/c", "mklink", "/J", link, target]);
    return exit == 0 && Directory.Exists(link);
}

static bool ScenarioChecksOutputPaths(string scenetake, string repoRoot, string work)
{
    // The cast is written after pre and the recording. A directory where an output goes is found before
    // pre, so nothing runs. The output folder is checked after pre instead, because pre may create it:
    // a pre that creates it records normally, and a folder still missing stops the run before the
    // recording, with post run because pre succeeded. Without pre commands the folder is checked
    // before anything runs, so post does not run.
    var directory = Directory.CreateDirectory(Path.Combine(work, "output-path")).FullName;
    var preRan = Path.Combine(directory, "pre-ran.txt");
    var postRan = Path.Combine(directory, "post-ran.txt");
    var scenario = WriteScenarioWithHooks(directory, "s.yaml");
    Directory.CreateDirectory(Path.Combine(directory, "taken.svg"));
    var (takenExit, takenStderr) = Exec(scenetake, repoRoot, ["--format", "svg", scenario, Path.Combine(directory, "taken")]);
    var takenRanPre = File.Exists(preRan);

    var (missingExit, missingStderr) = Exec(scenetake, repoRoot, [scenario, Path.Combine(directory, "missing", "out")]);
    var missingRanPost = File.Exists(postRan);

    var creating = WriteScenarioWithHooks(directory, "creates.yaml", extraPre: "mkdir made");
    var (madeExit, madeStderr) = Exec(scenetake, repoRoot, [creating, Path.Combine(directory, "made", "out")]);
    if (madeExit != 0)
        Console.Error.WriteLine(madeStderr);

    // pre may also put a directory, or a link to the scenario, where the cast goes. Only the checks
    // after pre can see them. Windows makes a hard link, which needs no privilege. Unix makes a symbolic
    // link, because a hard link is detected only on Windows.
    File.Delete(postRan);
    var directoryPre = WriteScenarioWithHooks(directory, "dir.yaml", extraPre: "mkdir dir-out.cast");
    var (dirExit, dirStderr) = Exec(scenetake, repoRoot, [directoryPre, Path.Combine(directory, "dir-out")]);
    var dirRanPost = File.Exists(postRan);
    var linking = Path.Combine(directory, "links.yaml");
    var link = OperatingSystem.IsWindows() ? "New-Item -ItemType HardLink -Path linked.cast -Target links.yaml" : "ln -s links.yaml linked.cast";
    WriteScenarioWithHooks(directory, "links.yaml", extraPre: link);
    var linkingText = File.ReadAllText(linking);
    var (linkExit, linkStderr) = Exec(scenetake, repoRoot, [linking, Path.Combine(directory, "linked")]);

    var noPre = Path.Combine(directory, "no-pre.yaml");
    var noPrePostRan = Path.Combine(directory, "no-pre-post-ran.txt");
    File.WriteAllText(noPre, $"""
        title: "no pre"
        width: 40
        height: 6
        cwd: '{directory.Replace('\\', '/')}'
        steps:
          - run: echo hello
        post:
          - echo ran > no-pre-post-ran.txt
        """);
    var (noPreExit, noPreStderr) = Exec(scenetake, repoRoot, [noPre, Path.Combine(directory, "missing", "out")]);
    return takenExit == 1 && takenStderr.Contains("output path is a directory", StringComparison.Ordinal) && !takenRanPre
        && noPreExit == 1 && noPreStderr.Contains("output folder does not exist", StringComparison.Ordinal) && !File.Exists(noPrePostRan)
        && missingExit == 1 && missingStderr.Contains("output folder does not exist", StringComparison.Ordinal)
        && !missingStderr.Contains("Generating cast", StringComparison.Ordinal) && missingRanPost
        && madeExit == 0 && File.Exists(Path.Combine(directory, "made", "out.cast"))
        && dirExit == 1 && dirStderr.Contains("output path is a directory", StringComparison.Ordinal)
        && !dirStderr.Contains("Generating cast", StringComparison.Ordinal) && dirRanPost
        && linkExit == 1 && linkStderr.Contains("would replace the input file", StringComparison.Ordinal)
        && File.ReadAllText(linking) == linkingText;
}

static bool ScenarioErrorsAfterPreRunPost(string scenetake, string repoRoot, string work)
{
    // Step keys that contradict each other are found before pre, though the step comes after one that
    // runs. A step that removes the output folder makes the cast write fail after pre, so post must
    // still run, and the error is reported instead of ending scenetake with an unhandled exception.
    var directory = Directory.CreateDirectory(Path.Combine(work, "after-pre")).FullName;
    var cwd = directory.Replace('\\', '/');
    var invalid = Path.Combine(directory, "invalid.yaml");
    File.WriteAllText(invalid, $"""
        title: "invalid step"
        width: 40
        height: 6
        cwd: '{cwd}'
        pre:
          - echo ran > pre-ran.txt
        steps:
          - run: echo ran > step-ran.txt
          - run: echo two
            interactive: true
        """);
    var (invalidExit, invalidStderr) = Exec(scenetake, repoRoot, [invalid]);
    var invalidRan = File.Exists(Path.Combine(directory, "pre-ran.txt")) || File.Exists(Path.Combine(directory, "step-ran.txt"));

    var output = Directory.CreateDirectory(Path.Combine(directory, "out")).FullName;
    var removes = Path.Combine(directory, "removes.yaml");
    File.WriteAllText(removes, $"""
        title: "removes the output folder"
        width: 40
        height: 6
        cwd: '{cwd}'
        pre:
          - echo ran > pre-ran.txt
        steps:
          - run: {(OperatingSystem.IsWindows() ? "Remove-Item -Recurse -Force out" : "rm -rf out")}
        post:
          - echo ran > post-ran.txt
        """);
    var (removedExit, removedStderr) = Exec(scenetake, repoRoot, [removes, Path.Combine(output, "s")]);
    if (removedExit != 1)
        Console.Error.WriteLine(removedStderr);
    var removedRanPost = File.Exists(Path.Combine(directory, "post-ran.txt"));

    // Steps that fail while the recording runs, each in its own way: an interactive step whose output
    // is redirected, as it is here, and a PTY command that holds a NUL, which the PTY refuses to start.
    var failures = new List<(int Exit, string Stderr, bool RanPost)>();
    foreach (var (name, step) in new[]
    {
        ("interactive", "  - run: echo two\n    pty: true\n    interactive: true"),
        ("nul", "  - run: \"echo a\\0b\"\n    pty: true"),
    })
    {
        File.Delete(Path.Combine(directory, "post-ran.txt"));
        var scenario = Path.Combine(directory, $"{name}.yaml");
        File.WriteAllText(scenario, $"title: \"{name}\"\nwidth: 40\nheight: 6\ncwd: '{cwd}'\npre:\n  - echo ran > pre-ran.txt\nsteps:\n{step}\npost:\n  - echo ran > post-ran.txt\n");
        var (exit, stderr) = Exec(scenetake, repoRoot, [scenario]);
        if (exit != 1)
            Console.Error.WriteLine(stderr);
        failures.Add((exit, stderr, File.Exists(Path.Combine(directory, "post-ran.txt"))));
    }

    // A step that removes the scenario's working folder makes the next step fail to start, with an error
    // of yet another kind. post cannot start there either, so only the exit and the message are checked.
    // On Windows the step runs in cmd, whose cd moves the process out of the folder. pwsh's cd does not,
    // so pwsh could not remove the folder.
    var gone = Directory.CreateDirectory(Path.Combine(directory, "gone")).FullName;
    var goneScenario = Path.Combine(directory, "gone.yaml");
    var (goneShell, goneRemove) = OperatingSystem.IsWindows() ? ("cmd", "cd .. && rd /s /q gone") : ("bash", "cd .. && rm -rf gone");
    File.WriteAllText(goneScenario, $"title: \"gone\"\nwidth: 40\nheight: 6\nshell: {goneShell}\ncwd: '{gone.Replace('\\', '/')}'\npre:\n  - echo ran\nsteps:\n  - run: {goneRemove}\n  - run: echo after\npost:\n  - echo ran\n");
    var (goneExit, goneStderr) = Exec(scenetake, repoRoot, [goneScenario]);
    var goneReported = goneExit == 1 && goneStderr.Contains("Error: ", StringComparison.Ordinal) && !goneStderr.Contains("Unhandled", StringComparison.Ordinal)
        && !Directory.Exists(gone);
    if (!goneReported)
        Console.Error.WriteLine(goneStderr);

    return invalidExit == 1 && invalidStderr.Contains("requires 'pty: true'", StringComparison.Ordinal) && !invalidRan
        && removedExit == 1 && removedStderr.Contains("cannot write the cast", StringComparison.Ordinal) && removedRanPost
        && failures.All(failure => failure.Exit == 1 && failure.Stderr.Contains("Error: ", StringComparison.Ordinal)
            && !failure.Stderr.Contains("Unhandled", StringComparison.Ordinal) && failure.RanPost)
        && goneReported;
}

static bool ScenarioErrorsStopBeforePre(string scenetake, string repoRoot, string work)
{
    // Each scenario must end with an error before anything runs, not with an unhandled exception: YAML the
    // parser rejects in different ways (a broken token, a value of the wrong type, a key given twice in a
    // step), a shell that does not exist, a shell or a cwd with a NUL, and a run that is a list. Without
    // their refusal the NUL and list cases would run pre, so the pre marker shows the order.
    var directory = Directory.CreateDirectory(Path.Combine(work, "before-pre")).FullName;
    var cwd = directory.Replace('\\', '/');
    var missingShell = OperatingSystem.IsWindows() ? $"{cwd}/no-such-shell.exe" : "/no/such/shell";
    // The OS reads a shell path or name only up to a NUL, so each of these starts a shell that exists unless
    // the NUL is refused. On Unix the ".." after the NUL would also skip the check for a missing absolute path.
    var nulPath = OperatingSystem.IsWindows() ? Environment.SystemDirectory.Replace('\\', '/') + "/cmd.exe\\0" : "/bin/sh\\0/..";
    var nulName = OperatingSystem.IsWindows() ? "cmd.exe\\0" : "sh\\0zsh";
    // The OS reads this cwd as the test folder, so without the refusal pre runs there and leaves its marker.
    var nulCwd = $"{cwd}\\0/named-but-missing";
    var cases = new (string Name, string Body, string Error)[]
    {
        ("token", "title: \"unclosed\nsteps:\n  - run: echo hi\n", "Error: cannot read"),
        ("type", "width: abc\nsteps:\n  - run: echo hi\n", "Error: cannot read"),
        ("twice", "steps:\n  - run: echo a\n    run: echo b\n", "Error: cannot read"),
        ("shell", $"shell: '{missingShell}'\nsteps:\n  - run: echo hi\n", "Error: Shell"),
        ("nulpath", $"shell: \"{nulPath}\"\nsteps:\n  - run: echo hi\n", "a shell cannot contain a NUL character"),
        ("nulname", $"shell: \"{nulName}\"\nsteps:\n  - run: echo hi\n", "a shell cannot contain a NUL character"),
        ("nulcwd", $"cwd: \"{nulCwd}\"\nsteps:\n  - run: echo hi\n", "cwd cannot contain a NUL character"),
        ("list", "steps:\n  - run: [echo, hi]\n", "Error: Step key 'run' must be a command string"),
    };
    var refused = true;
    foreach (var (name, body, error) in cases)
    {
        var scenario = Path.Combine(directory, $"{name}.yaml");
        var cwdKey = body.StartsWith("cwd:", StringComparison.Ordinal) ? "" : $"cwd: '{cwd}'\n";
        File.WriteAllText(scenario, $"{cwdKey}pre:\n  - echo ran > {name}-pre-ran.txt\n{body}");
        var (exit, stderr) = Exec(scenetake, repoRoot, [scenario]);
        if (exit != 1 || !stderr.Contains(error, StringComparison.Ordinal) || stderr.Contains("Unhandled", StringComparison.Ordinal)
            || File.Exists(Path.Combine(directory, $"{name}-pre-ran.txt")))
        {
            Console.Error.WriteLine($"  {name}: exit {exit}{Environment.NewLine}{stderr}");
            refused = false;
        }
    }

    return refused;
}

static bool UnixShellStartsAsNamed(string scenetake, string repoRoot, string work)
{
    // A shell shows the name it was started by as $0 and in its error messages, and the recording keeps
    // both, so a shell given by name must be started by that name in pipe and PTY steps. A dangling link
    // named like the shell earlier on PATH must not stop the run, which a check of its own could do.
    var directory = Directory.CreateDirectory(Path.Combine(work, "unix-shell")).FullName;
    var bash = FindOnPath("bash") ?? throw new SkipTestException("bash is not on PATH");
    var steps = "steps:\n  - run: echo \"zero=$0\"\n  - run: echo \"ptyzero=$0\"\n    pty: true\n";

    var named = Path.Combine(directory, "named.yaml");
    File.WriteAllText(named, $"cwd: '{directory}'\nshell: bash\n{steps}");
    var (namedExit, namedStderr) = Exec(scenetake, repoRoot, [named, Path.Combine(directory, "named")]);
    var namedCast = namedExit == 0 ? File.ReadAllText(Path.Combine(directory, "named.cast")) : namedStderr;
    var keepsName = namedExit == 0 && namedCast.Contains("ptyzero=bash", StringComparison.Ordinal)
        && namedCast.Replace("ptyzero=bash", "", StringComparison.Ordinal).Contains("zero=bash", StringComparison.Ordinal);

    var stale = Directory.CreateDirectory(Path.Combine(directory, "stale")).FullName;
    File.CreateSymbolicLink(Path.Combine(stale, "bash"), Path.Combine(stale, "missing"));
    var (staleExit, staleStderr) = Exec(scenetake, repoRoot, [named, Path.Combine(directory, "stale")],
        new() { ["PATH"] = stale + ":" + Environment.GetEnvironmentVariable("PATH") });

    // "link/../bash" names real/bash, because the OS applies ".." after following the link. Removing ".."
    // as text gives directory/bash, which does not exist, so a check that does that refuses the shell.
    var real = Directory.CreateDirectory(Path.Combine(directory, "real", "sub")).Parent!.FullName;
    File.CreateSymbolicLink(Path.Combine(real, "bash"), bash);
    File.CreateSymbolicLink(Path.Combine(directory, "link"), Path.Combine(real, "sub"));
    var dotdot = Path.Combine(directory, "dotdot.yaml");
    File.WriteAllText(dotdot, $"cwd: '{directory}'\nshell: '{directory}/link/../bash'\n{steps}");
    var (dotdotExit, dotdotStderr) = Exec(scenetake, repoRoot, [dotdot, Path.Combine(directory, "dotdot")]);

    foreach (var (name, exit, stderr) in new[] { ("named", namedExit, namedCast), ("stale", staleExit, staleStderr), ("dotdot", dotdotExit, dotdotStderr) })
    {
        if (exit != 0 || name == "named" && !keepsName)
            Console.Error.WriteLine($"  {name}: exit {exit}{Environment.NewLine}{stderr}");
    }

    return keepsName && staleExit == 0 && dotdotExit == 0;
}

static bool WindowsShellPathStartsThatFile(string scenetake, string repoRoot, string work)
{
    // Each shell is a path whose file name is cmd, bash or pwsh, names that are otherwise looked up on PATH
    // or in the Git folders. A lookup by name would start the System32 cmd instead of the wrapper, start it
    // for a path that does not exist, start Git bash for the WSL one, and refuse pwsh when PATH has none.
    // The "dot" case starts cmd through "cmd.exe.", whose name only matches once the trailing dot is
    // dropped. Without that, cmd starts with "-c" and reads the "/" in its own path as switches, so pre runs
    // "md" instead of echo. Its PTY step checks that a path still gets cmd's arguments there.
    var directory = Directory.CreateDirectory(Path.Combine(work, "windows-shell")).FullName;
    var cwd = directory.Replace('\\', '/');
    var system = Environment.SystemDirectory.Replace('\\', '/');
    var pipeSteps = "steps:\n  - run: echo hi\n";
    var ptySteps = "steps:\n  - run: echo hi\n  - run: echo pty\n    pty: true\n";
    Directory.CreateDirectory(Path.Combine(directory, "wrap"));
    var wrapperMarker = Path.Combine(directory, "wrapper-ran.txt");
    File.WriteAllText(Path.Combine(directory, "wrap", "cmd.cmd"), $"@echo wrapper> \"{wrapperMarker}\"\r\n@\"%ComSpec%\" %*\r\n");
    var cases = new List<(string Name, string Shell, string Steps, Dictionary<string, string>? Environment, string? Error)>
    {
        ("wrapper", $"{cwd}/wrap/cmd.cmd", pipeSteps, null, null),
        ("missing", $"{cwd}/nowhere/cmd.exe", pipeSteps, null, $"Shell '{cwd}/nowhere/cmd.exe' was requested, but the path does not exist."),
        ("dot", $"{system}/cmd.exe.", ptySteps, null, null),
        ("wsl", $"{system}/bash.exe", pipeSteps, null, "but it is WSL bash"),
    };
    if (FindOnPath("pwsh") is { } pwsh)
    {
        var systemOnly = new Dictionary<string, string> { ["PATH"] = Environment.SystemDirectory + ";" + Environment.GetEnvironmentVariable("SystemRoot") };
        cases.Add(("pwsh", pwsh.Replace('\\', '/'), ptySteps, systemOnly, null));
    }

    var passed = true;
    foreach (var (name, shell, steps, environment, error) in cases)
    {
        var scenario = Path.Combine(directory, $"{name}.yaml");
        File.WriteAllText(scenario, $"cwd: '{cwd}'\nshell: '{shell}'\npre:\n  - echo ran > {name}-pre-ran.txt\n{steps}");
        var (exit, stderr) = Exec(scenetake, repoRoot, [scenario, Path.Combine(directory, name)], environment);
        var marker = Path.Combine(directory, $"{name}-pre-ran.txt");
        var ok = !stderr.Contains("Unhandled", StringComparison.Ordinal) && (error is null
            ? exit == 0 && File.Exists(marker) && File.ReadAllText(marker).Contains("ran", StringComparison.Ordinal)
            : exit == 1 && stderr.Contains(error, StringComparison.Ordinal) && !File.Exists(marker));
        if (name == "wrapper")
            ok &= File.Exists(wrapperMarker);
        if (!ok)
        {
            Console.Error.WriteLine($"  {name}: exit {exit}{Environment.NewLine}{stderr}");
            passed = false;
        }
    }

    return passed;
}

static bool SvgFailureWithAnOpenOutputIsReported(string scenetake, string repoRoot, string work)
{
    // A viewer may hold an earlier .svg open. On Windows the render then cannot replace it, and removing
    // it after the failure fails too, which must end with an error, not an unhandled exception. .NET
    // makes the write fail elsewhere too while this test holds the files, but only Windows refuses the
    // delete, so elsewhere the test passes either way.
    var directory = Directory.CreateDirectory(Path.Combine(work, "open-svg")).FullName;
    var cast = Path.Combine(directory, "bad.cast");
    File.WriteAllText(cast, "{\"version\":3}\n");
    var image = Path.Combine(directory, "bad.svg");
    File.WriteAllText(image, "<svg/>");
    var scenario = WriteScenarioWithHooks(directory, "s.yaml");
    var scenarioImage = Path.Combine(directory, "s.svg");
    File.WriteAllText(scenarioImage, "<svg/>");
    int castExit, scenarioExit;
    string castStderr, scenarioStderr;
    using (new FileStream(image, FileMode.Open, FileAccess.Read, FileShare.Read))
    using (new FileStream(scenarioImage, FileMode.Open, FileAccess.Read, FileShare.Read))
    {
        (castExit, castStderr) = Exec(scenetake, repoRoot, ["svg", cast]);
        (scenarioExit, scenarioStderr) = Exec(scenetake, repoRoot, ["--format", "svg", scenario]);
    }

    return castExit == 1 && castStderr.Contains("Error: cast header is missing terminal size", StringComparison.Ordinal)
        && !castStderr.Contains("Unhandled", StringComparison.Ordinal)
        && scenarioExit == 1 && scenarioStderr.Contains("Error: SVG render failed", StringComparison.Ordinal)
        && !scenarioStderr.Contains("Unhandled", StringComparison.Ordinal) && File.Exists(Path.Combine(directory, "post-ran.txt"));
}

static bool ScenarioChecksFrameSizeBeforePre(string scenetake, string repoRoot, string work)
{
    // 512 columns at font size 64 are 20512 pixels wide, over x264's 16384, which it would reject
    // only after the recording. The fake ffmpeg passes the libx264 check, so the size is what fails.
    var directory = Directory.CreateDirectory(Path.Combine(work, "frame-size")).FullName;
    var fake = WriteFakeFfmpeg(directory, "ffmpeg-ok", hasLibx264: true);
    var scenario = WriteScenarioWithHooks(directory, "s.yaml", width: 512);
    var (exit, stderr) = Exec(scenetake, repoRoot, ["--format", "mp4", "--font-size", "64", scenario], new() { ["SCENETAKE_FFMPEG"] = fake });
    return exit == 1 && stderr.Contains("20512x", StringComparison.Ordinal) && stderr.Contains("too large for x264", StringComparison.Ordinal)
        && !File.Exists(Path.Combine(directory, "s.cast")) && !File.Exists(Path.Combine(directory, "pre-ran.txt"));
}

static bool EarlierMp4GoesWhenEncodingStarts(string scenetake, string repoRoot, string work)
{
    // Ctrl+C ends scenetake from its handler, with no catch block left to run. So an earlier .mp4 must
    // already be gone while ffmpeg runs. The fake ffmpeg records whether it still exists at that point.
    // Font and frame-size errors stop the mp4 subcommand before encoding starts and keep the earlier
    // .mp4. The frame here comes from the cast: 512 columns at font size 64 are over x264's 16384.
    var directory = Directory.CreateDirectory(Path.Combine(work, "earlier-mp4")).FullName;
    var earlier = Path.Combine(directory, "s.mp4");
    var log = Path.Combine(directory, "seen.txt");
    var fake = WriteFfmpegThatChecksFile(directory, "ffmpeg-check", earlier, log);
    var scenario = WriteScenarioWithHooks(directory, "s.yaml");
    File.WriteAllText(earlier, "from an earlier run");
    var (scenarioExit, _) = Exec(scenetake, repoRoot, ["--format", "mp4", scenario], new() { ["SCENETAKE_FFMPEG"] = fake });
    var seenByScenario = File.Exists(log) ? File.ReadAllText(log).Trim() : "";

    File.WriteAllText(earlier, "from an earlier run");
    File.Delete(log);
    var cast = Path.Combine(directory, "s.cast");
    var (subcommandExit, _) = Exec(scenetake, repoRoot, ["mp4", cast, earlier], new() { ["SCENETAKE_FFMPEG"] = fake });
    var seenBySubcommand = File.Exists(log) ? File.ReadAllText(log).Trim() : "";

    File.WriteAllText(earlier, "from an earlier run");
    var (fontExit, _) = Exec(scenetake, repoRoot, ["mp4", "--font-file", Path.Combine(directory, "typo.ttf"), cast, earlier], new() { ["SCENETAKE_FFMPEG"] = fake });
    var keptAfterFont = File.Exists(earlier);

    var wide = Path.Combine(directory, "wide.cast");
    File.WriteAllLines(wide, ["""{"version":3,"term":{"cols":512,"rows":2},"tags":["st:font-size=64"]}""", """[0.1, "o", "x"]"""]);
    var (frameExit, frameStderr) = Exec(scenetake, repoRoot, ["mp4", wide, earlier], new() { ["SCENETAKE_FFMPEG"] = fake });
    return scenarioExit == 1 && seenByScenario == "absent" && subcommandExit == 1 && seenBySubcommand == "absent"
        && fontExit == 1 && keptAfterFont
        && frameExit == 1 && frameStderr.Contains("too large for x264", StringComparison.Ordinal)
        && !frameStderr.Contains("mp4 render failed", StringComparison.Ordinal) && File.Exists(earlier);
}

static bool FontInstalledByPreOrStepIsUsed(string scenetake, string repoRoot, string work)
{
    // pre or a step installs a font that the check before pre could not see. A font from a step is
    // found only because the render scans when it starts, after the steps, so the step case fails if
    // the scan runs right after pre. XDG_DATA_HOME points the scan at a folder this test owns. The font
    // is an installed one whose family name is rewritten, at the same length, to a name no installed
    // font has. It must have outlines: the scan skips a bitmap-only font, such as a color emoji font,
    // and correct code would fail.
    var source = new[] { "/usr/share/fonts", "/usr/local/share/fonts" }
        .Where(Directory.Exists)
        .SelectMany(root => Directory.EnumerateFiles(root, "*.ttf", SearchOption.AllDirectories))
        .Order(StringComparer.Ordinal)
        .Select(path => (Path: path, Renamed: TryRenameFontFamily(File.ReadAllBytes(path))))
        .FirstOrDefault(font => font.Renamed is not null);
    if (source.Renamed is not { } renamed)
        throw new SkipTestException("no TrueType font with outlines and a Windows family name under /usr/share/fonts or /usr/local/share/fonts");

    var used = true;
    foreach (var installer in new[] { "pre", "step" })
    {
        var directory = Directory.CreateDirectory(Path.Combine(work, $"font-from-{installer}")).FullName;
        File.WriteAllBytes(Path.Combine(directory, "renamed.ttf"), renamed.Data);
        var fake = WriteSucceedingFakeFfmpeg(directory, "ffmpeg-ok");
        const string install = "mkdir -p fonts && cp renamed.ttf fonts/";
        var scenario = Path.Combine(directory, "s.yaml");
        File.WriteAllText(scenario, $"""
            title: "font from {installer}"
            width: 40
            height: 6
            cwd: '{directory}'
            pre:
              - {(installer == "pre" ? install : "echo ran")}
            steps:
              - run: {(installer == "step" ? install : "echo hello")}
            """);
        var (exit, stderr) = Exec(scenetake, repoRoot, ["--format", "mp4", "--font-family", renamed.Family, scenario],
            new() { ["SCENETAKE_FFMPEG"] = fake, ["XDG_DATA_HOME"] = directory });
        if (exit != 0 || !stderr.Contains($"font: {renamed.Family}", StringComparison.Ordinal))
        {
            Console.Error.WriteLine($"  font installed by the {installer}, copied from {source.Path}:{Environment.NewLine}{stderr}");
            used = false;
        }
    }

    return used;
}

/// <summary>
/// The font with the letters of its family name (name ID 1) replaced with Q in every name record that
/// holds it, or null when it has no glyf and loca outlines or no Windows family name.
/// </summary>
static (byte[] Data, string Family)? TryRenameFontFamily(byte[] data)
{
    static int U16(byte[] d, int at) => System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(d.AsSpan(at));
    var records = Enumerable.Range(0, U16(data, 4)).Select(i => 12 + i * 16).ToList();
    int? Record(string tag)
    {
        foreach (var record in records)
        {
            if (System.Text.Encoding.ASCII.GetString(data, record, 4) == tag)
                return record;
        }

        return null;
    }

    if (Record("glyf") is null || Record("loca") is null || Record("name") is not { } name)
        return null;

    var table = (int)System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(name + 8));
    var strings = table + U16(data, table + 4);
    string? family = null;
    for (var i = 0; i < U16(data, table + 2); i++)
    {
        var record = table + 6 + i * 12;
        if (U16(data, record + 6) != 1 || U16(data, record) != 3)
            continue;
        var span = data.AsSpan(strings + U16(data, record + 10), U16(data, record + 8));
        family = System.Text.Encoding.BigEndianUnicode.GetString(span);
        break;
    }

    if (family is null)
        return null;

    var renamed = new string(family.Select(c => char.IsLetter(c) ? 'Q' : c).ToArray());
    foreach (var encoding in new[] { System.Text.Encoding.BigEndianUnicode, System.Text.Encoding.Latin1 })
    {
        var from = encoding.GetBytes(family);
        var to = encoding.GetBytes(renamed);
        for (var at = data.AsSpan(strings).IndexOf(from); at >= 0; at = data.AsSpan(strings).IndexOf(from))
            to.CopyTo(data, strings + at);
    }

    return (data, renamed);
}

/// <summary>A stand-in ffmpeg that, on an encode, writes "present" or "absent" for <paramref name="watched"/> to <paramref name="log"/>, then fails.</summary>
static string WriteFfmpegThatChecksFile(string work, string name, string watched, string log)
{
    const string encoder = " V....D libx264              libx264 H.264";
    if (OperatingSystem.IsWindows())
    {
        var path = Path.Combine(work, name + ".cmd");
        File.WriteAllText(path,
            "@echo off\r\n" +
            $"echo %* | findstr /C:\"-encoders\" >nul && (echo {encoder}& exit /b 0)\r\n" +
            $"if exist \"{watched}\" (echo present> \"{log}\") else (echo absent> \"{log}\")\r\n" +
            "exit /b 3\r\n");
        return path;
    }
    else
    {
        var path = Path.Combine(work, name);
        File.WriteAllText(path,
            "#!/bin/sh\n" +
            $"for a in \"$@\"; do [ \"$a\" = -encoders ] && {{ echo \"{encoder}\"; exit 0; }}; done\n" +
            $"if [ -e '{watched}' ]; then echo present > '{log}'; else echo absent > '{log}'; fi\n" +
            "exit 3\n");
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        return path;
    }
}

/// <summary>A scenario in <paramref name="directory"/> whose pre and post leave marker files there.</summary>
static string WriteScenarioWithHooks(string directory, string name, int width = 40, string extraPre = "echo also ran")
{
    var path = Path.Combine(directory, name);
    File.WriteAllText(path, $"""
        title: "mp4 hooks test"
        width: {width}
        height: 6
        cwd: '{directory.Replace('\\', '/')}'
        pre:
          - echo ran > pre-ran.txt
          - {extraPre}
        steps:
          - run: echo hello
        post:
          - echo ran > post-ran.txt
        """);
    return path;
}

sealed class SkipTestException(string reason) : Exception(reason);
