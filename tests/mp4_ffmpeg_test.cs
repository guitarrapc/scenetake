#:sdk Microsoft.NET.Sdk
#:property TargetFramework=net10.0
#:property Nullable=enable
#:property ImplicitUsings=enable

// mp4 output through a published scenetake binary: CLI rules, ffmpeg failures, and real encodes.
// Real encodes need ffmpeg (with libx264) and ffprobe on PATH; they print "skip" otherwise.
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

    if (FindOnPath("ffmpeg") is null || FindOnPath("ffprobe") is null)
    {
        Console.Error.WriteLine("skip mp4 encode tests: ffmpeg and ffprobe not found on PATH");
    }
    else
    {
        failures += Run("SubcommandWritesH264", () => SubcommandWritesH264(scenetake, repoRoot, work));
        failures += Run("MaxSizeDownscales", () => MaxSizeDownscales(scenetake, repoRoot, work));
        failures += Run("ScenarioFormatMp4", () => ScenarioFormatMp4(scenetake, repoRoot, work));

        // Windows delivers Ctrl+C per console; sending one to a child needs a separate console,
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
    // A 60s cast with changing text keeps the encode running long enough to interrupt it.
    var dir = Directory.CreateDirectory(Path.Combine(work, "ctrlc")).FullName;
    var cast = Path.Combine(dir, "long.cast");
    var lines = new List<string> { """{"version":3,"term":{"cols":80,"rows":24}}""" };
    for (var i = 0; i < 1200; i++)
        lines.Add($"[0.05, \"o\", \"line {i} {new string((char)('a' + i % 26), 60)}\\r\\n\"]");
    File.WriteAllLines(cast, lines);
    var output = Path.Combine(dir, "long.mp4");

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

/// <summary>A stand-in ffmpeg: answers -encoders (with or without libx264), and fails any encode.</summary>
static string WriteFakeFfmpeg(string work, string name, bool hasLibx264)
{
    var encoder = hasLibx264 ? " V....D libx264              libx264 H.264" : " V....D libopenh264          OpenH264 H.264";
    if (OperatingSystem.IsWindows())
    {
        var path = Path.Combine(work, name + ".cmd");
        File.WriteAllText(path,
            "@echo off\r\n" +
            $"echo %* | findstr /C:\"-encoders\" >nul && (echo {encoder}& exit /b 0)\r\n" +
            "echo simulated encoder failure 1>&2\r\n" +
            "exit /b 3\r\n");
        return path;
    }
    else
    {
        var path = Path.Combine(work, name);
        File.WriteAllText(path,
            "#!/bin/sh\n" +
            $"for a in \"$@\"; do [ \"$a\" = -encoders ] && {{ echo \"{encoder}\"; exit 0; }}; done\n" +
            "echo \"simulated encoder failure\" >&2\n" +
            "exit 3\n");
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        return path;
    }
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
