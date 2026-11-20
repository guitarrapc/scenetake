using System.Diagnostics;
using Scenetake.TestSupport;

namespace Scenetake.Tests;

// mp4 output through a published scenetake binary: CLI rules, ffmpeg failures, and real encodes.
// Real encodes need ffmpeg (with libx264) and ffprobe on PATH. They skip otherwise.

[Category("Ffmpeg")]
[Category("Smoke")]
public sealed class Mp4FfmpegTests
{
    // A folder for the files the tests write, removed after the last test of the class.
    private static readonly string Work = Directory.CreateTempSubdirectory("scenetake-mp4-test-").FullName;

    [After(Class)]
    public static void RemoveWork() => Directory.Delete(Work, recursive: true);

    [Test]
    public async Task Mp4OptionsRequireMp4Format() => await Assert.That(Skipping.Run(() => Mp4FfmpegCases.Mp4OptionsRequireMp4Format(TestEnv.Binary, TestEnv.RepoRoot, Work))).IsTrue();

    [Test]
    public async Task MaxFpsRejectedForMp4() => await Assert.That(Skipping.Run(() => Mp4FfmpegCases.MaxFpsRejectedForMp4(TestEnv.Binary, TestEnv.RepoRoot, Work))).IsTrue();

    [Test]
    public async Task MissingFfmpegShowsInstallHint() => await Assert.That(Skipping.Run(() => Mp4FfmpegCases.MissingFfmpegShowsInstallHint(TestEnv.Binary, TestEnv.RepoRoot, Work))).IsTrue();

    [Test]
    public async Task FfmpegWithoutLibx264IsRejected() => await Assert.That(Skipping.Run(() => Mp4FfmpegCases.FfmpegWithoutLibx264IsRejected(TestEnv.Binary, TestEnv.RepoRoot, Work))).IsTrue();

    [Test]
    public async Task FfmpegFailureLeavesNoFile() => await Assert.That(Skipping.Run(() => Mp4FfmpegCases.FfmpegFailureLeavesNoFile(TestEnv.Binary, TestEnv.RepoRoot, Work))).IsTrue();

    [Test]
    public async Task ScenarioKeepsCastWhenEncodeFails() => await Assert.That(Skipping.Run(() => Mp4FfmpegCases.ScenarioKeepsCastWhenEncodeFails(TestEnv.Binary, TestEnv.RepoRoot, Work))).IsTrue();

    [Test]
    public async Task ScenarioChecksFfmpegBeforePre() => await Assert.That(Skipping.Run(() => Mp4FfmpegCases.ScenarioChecksFfmpegBeforePre(TestEnv.Binary, TestEnv.RepoRoot, Work))).IsTrue();

    [Test]
    public async Task ScenarioChecksFontBeforePre() => await Assert.That(Skipping.Run(() => Mp4FfmpegCases.ScenarioChecksFontBeforePre(TestEnv.Binary, TestEnv.RepoRoot, Work))).IsTrue();

    [Test]
    public async Task ScenarioRejectsOversizedTerminal() => await Assert.That(Skipping.Run(() => Mp4FfmpegCases.ScenarioRejectsOversizedTerminal(TestEnv.Binary, TestEnv.RepoRoot, Work))).IsTrue();

    [Test]
    public async Task PostRunsAndStaleMp4GoesWhenEncodeFails() => await Assert.That(Skipping.Run(() => Mp4FfmpegCases.PostRunsAndStaleMp4GoesWhenEncodeFails(TestEnv.Binary, TestEnv.RepoRoot, Work))).IsTrue();

    [Test]
    public async Task StaleMp4GoesWhenCastIsBad() => await Assert.That(Skipping.Run(() => Mp4FfmpegCases.StaleMp4GoesWhenCastIsBad(TestEnv.Binary, TestEnv.RepoRoot, Work))).IsTrue();

    [Test]
    public async Task CliRulesForValuesAndFormats() => await Assert.That(Skipping.Run(() => Mp4FfmpegCases.CliRulesForValuesAndFormats(TestEnv.Binary, TestEnv.RepoRoot, Work))).IsTrue();

    [Test]
    public async Task CliRulesForWindowStyle() => await Assert.That(Skipping.Run(() => Mp4FfmpegCases.CliRulesForWindowStyle(TestEnv.Binary, TestEnv.RepoRoot, Work))).IsTrue();

    [Test]
    public async Task CliRulesForLoopAndEnd() => await Assert.That(Skipping.Run(() => Mp4FfmpegCases.CliRulesForLoopAndEnd(TestEnv.Binary, TestEnv.RepoRoot, Work))).IsTrue();

    [Test]
    public async Task CliRulesForStillImages() => await Assert.That(Skipping.Run(() => Mp4FfmpegCases.CliRulesForStillImages(TestEnv.Binary, TestEnv.RepoRoot, Work))).IsTrue();

    [Test]
    public async Task CliRulesForGif() => await Assert.That(Skipping.Run(() => Mp4FfmpegCases.CliRulesForGif(TestEnv.Binary, TestEnv.RepoRoot, Work))).IsTrue();

    [Test]
    public async Task HelpDescribesEveryOption() => await Assert.That(Skipping.Run(() => Mp4FfmpegCases.HelpDescribesEveryOption(TestEnv.Binary, TestEnv.RepoRoot, Work))).IsTrue();

    [Test]
    public async Task InitWritesStepsForItsShell() => await Assert.That(Skipping.Run(() => Mp4FfmpegCases.InitWritesStepsForItsShell(TestEnv.Binary, Work))).IsTrue();

    [Test]
    public async Task CaptureRecordsOneCommand() => await Assert.That(Skipping.Run(() => Mp4FfmpegCases.CaptureRecordsOneCommand(TestEnv.Binary, Work))).IsTrue();

    [Test]
    public async Task SubcommandsNeverReplaceTheirInput() => await Assert.That(Skipping.Run(() => Mp4FfmpegCases.SubcommandsNeverReplaceTheirInput(TestEnv.Binary, TestEnv.RepoRoot, Work))).IsTrue();

    [Test]
    public async Task SubcommandsNeverReplaceTheirBackground() => await Assert.That(Skipping.Run(() => Mp4FfmpegCases.SubcommandsNeverReplaceTheirBackground(TestEnv.Binary, TestEnv.RepoRoot, Work))).IsTrue();

    [Test]
    public async Task ScenarioChecksOutputPaths() => await Assert.That(Skipping.Run(() => Mp4FfmpegCases.ScenarioChecksOutputPaths(TestEnv.Binary, TestEnv.RepoRoot, Work))).IsTrue();

    [Test]
    public async Task ScenarioErrorsStopBeforePre() => await Assert.That(Skipping.Run(() => Mp4FfmpegCases.ScenarioErrorsStopBeforePre(TestEnv.Binary, TestEnv.RepoRoot, Work))).IsTrue();

    [Test]
    public async Task RelativeCwdFollowsScenarioFolder() => await Assert.That(Skipping.Run(() => Mp4FfmpegCases.RelativeCwdFollowsScenarioFolder(TestEnv.Binary, TestEnv.RepoRoot, Work))).IsTrue();

    [Test]
    public async Task UnknownKeysWarnWithHints() => await Assert.That(Skipping.Run(() => Mp4FfmpegCases.UnknownKeysWarnWithHints(TestEnv.Binary, TestEnv.RepoRoot, Work))).IsTrue();

    [Test]
    public async Task OtherKeySpellingsHaveNoEffect() => await Assert.That(Skipping.Run(() => Mp4FfmpegCases.OtherKeySpellingsHaveNoEffect(TestEnv.Binary, TestEnv.RepoRoot, Work))).IsTrue();

    [Test]
    public async Task WrongValueTypeNamesTheKey() => await Assert.That(Skipping.Run(() => Mp4FfmpegCases.WrongValueTypeNamesTheKey(TestEnv.Binary, TestEnv.RepoRoot, Work))).IsTrue();

    [Test]
    public async Task KnownKeysDoNotWarn() => await Assert.That(Skipping.Run(() => Mp4FfmpegCases.KnownKeysDoNotWarn(TestEnv.Binary, TestEnv.RepoRoot, Work))).IsTrue();

    [Test]
    public async Task StrictStopsAtFirstFailure() => await Assert.That(Skipping.Run(() => Mp4FfmpegCases.StrictStopsAtFirstFailure(TestEnv.Binary, TestEnv.RepoRoot, Work))).IsTrue();

    [Test]
    public async Task ScenarioErrorsAfterPreRunPost() => await Assert.That(Skipping.Run(() => Mp4FfmpegCases.ScenarioErrorsAfterPreRunPost(TestEnv.Binary, TestEnv.RepoRoot, Work))).IsTrue();

    [Test]
    public async Task SvgFailureWithAnOpenOutputIsReported() => await Assert.That(Skipping.Run(() => Mp4FfmpegCases.SvgFailureWithAnOpenOutputIsReported(TestEnv.Binary, TestEnv.RepoRoot, Work))).IsTrue();

    [Test]
    public async Task ScenarioChecksFrameSizeBeforePre() => await Assert.That(Skipping.Run(() => Mp4FfmpegCases.ScenarioChecksFrameSizeBeforePre(TestEnv.Binary, TestEnv.RepoRoot, Work))).IsTrue();

    [Test]
    public async Task SizeOptionRules() => await Assert.That(Skipping.Run(() => Mp4FfmpegCases.SizeOptionRules(TestEnv.Binary, TestEnv.RepoRoot, Work))).IsTrue();

    [Test]
    public async Task ScenarioChecksSizeFrameBeforePre() => await Assert.That(Skipping.Run(() => Mp4FfmpegCases.ScenarioChecksSizeFrameBeforePre(TestEnv.Binary, TestEnv.RepoRoot, Work))).IsTrue();

    [Test]
    public async Task EarlierMp4GoesWhenEncodingStarts() => await Assert.That(Skipping.Run(() => Mp4FfmpegCases.EarlierMp4GoesWhenEncodingStarts(TestEnv.Binary, TestEnv.RepoRoot, Work))).IsTrue();

    [Test]
    public async Task ScenarioOutputsList() => await Assert.That(Skipping.Run(() => Mp4FfmpegCases.ScenarioOutputsList(TestEnv.Binary, TestEnv.RepoRoot, Work))).IsTrue();

    [Test]
    public async Task ScenarioOutputsErrors() => await Assert.That(Skipping.Run(() => Mp4FfmpegCases.ScenarioOutputsErrors(TestEnv.Binary, TestEnv.RepoRoot, Work))).IsTrue();

    [Test]
    public async Task ScenarioOutputsFailingEntry() => await Assert.That(Skipping.Run(() => Mp4FfmpegCases.ScenarioOutputsFailingEntry(TestEnv.Binary, Work))).IsTrue();

    [Test]
    public async Task ScenarioOutputsFileNames() => await Assert.That(Skipping.Run(() => Mp4FfmpegCases.ScenarioOutputsFileNames(TestEnv.Binary, Work))).IsTrue();

    [RunOn(OS.Linux)]
    [Test]
    public async Task FontInstalledByPreOrStepIsUsed() => await Assert.That(Skipping.Run(() => Mp4FfmpegCases.FontInstalledByPreOrStepIsUsed(TestEnv.Binary, TestEnv.RepoRoot, Work))).IsTrue();

    [ExcludeOn(OS.Windows)]
    [Test]
    public async Task UnixShellStartsAsNamed() => await Assert.That(Skipping.Run(() => Mp4FfmpegCases.UnixShellStartsAsNamed(TestEnv.Binary, TestEnv.RepoRoot, Work))).IsTrue();

    [ExcludeOn(OS.Windows)]
    [Test]
    public async Task UnixPowerShellRunsCommands() => await Assert.That(Skipping.Run(() => Mp4FfmpegCases.UnixPowerShellRunsCommands(TestEnv.Binary, TestEnv.RepoRoot, Work))).IsTrue();

    [RunOn(OS.Windows)]
    [Test]
    public async Task WindowsShellPathStartsThatFile() => await Assert.That(Skipping.Run(() => Mp4FfmpegCases.WindowsShellPathStartsThatFile(TestEnv.Binary, TestEnv.RepoRoot, Work))).IsTrue();

    [RunOn(OS.Windows)]
    [Test]
    public async Task WindowsShellNameSkipsCurrentDirectory() => await Assert.That(Skipping.Run(() => Mp4FfmpegCases.WindowsShellNameSkipsCurrentDirectory(TestEnv.Binary, Work))).IsTrue();

    [ExcludeOn(OS.Windows)]
    [Test]
    public async Task UnixShellNameInCurrentDirectoryIsRefused() => await Assert.That(Skipping.Run(() => Mp4FfmpegCases.UnixShellNameInCurrentDirectoryIsRefused(TestEnv.Binary, TestEnv.RepoRoot, Work))).IsTrue();

    [Test]
    public async Task SubcommandWritesH264()
    {
        TestEnv.RequireOnPath("ffmpeg", "ffprobe");
        await Assert.That(Skipping.Run(() => Mp4FfmpegCases.SubcommandWritesH264(TestEnv.Binary, TestEnv.RepoRoot, Work))).IsTrue();
    }

    [Test]
    public async Task MaxSizeDownscales()
    {
        TestEnv.RequireOnPath("ffmpeg", "ffprobe");
        await Assert.That(Skipping.Run(() => Mp4FfmpegCases.MaxSizeDownscales(TestEnv.Binary, TestEnv.RepoRoot, Work))).IsTrue();
    }

    [Test]
    public async Task SizeEncodesExactFrame()
    {
        TestEnv.RequireOnPath("ffmpeg", "ffprobe");
        await Assert.That(Skipping.Run(() => Mp4FfmpegCases.SizeEncodesExactFrame(TestEnv.Binary, TestEnv.RepoRoot, Work))).IsTrue();
    }

    [Test]
    public async Task ScreenSwitchesAreKeyframes()
    {
        TestEnv.RequireOnPath("ffmpeg", "ffprobe");
        await Assert.That(Skipping.Run(() => Mp4FfmpegCases.ScreenSwitchesAreKeyframes(TestEnv.Binary, TestEnv.RepoRoot, Work))).IsTrue();
    }

    [Test]
    public async Task ScenarioFormatMp4()
    {
        TestEnv.RequireOnPath("ffmpeg", "ffprobe");
        await Assert.That(Skipping.Run(() => Mp4FfmpegCases.ScenarioFormatMp4(TestEnv.Binary, TestEnv.RepoRoot, Work))).IsTrue();
    }

    [Test]
    public async Task SubcommandWritesGif()
    {
        TestEnv.RequireOnPath("ffmpeg", "ffprobe");
        await Assert.That(Skipping.Run(() => Mp4FfmpegCases.SubcommandWritesGif(TestEnv.Binary, TestEnv.RepoRoot, Work))).IsTrue();
    }

    [Test]
    public async Task ScenarioFormatGif()
    {
        TestEnv.RequireOnPath("ffmpeg", "ffprobe");
        await Assert.That(Skipping.Run(() => Mp4FfmpegCases.ScenarioFormatGif(TestEnv.Binary, TestEnv.RepoRoot, Work))).IsTrue();
    }

    [Test]
    public async Task ScenarioOutputsGifAndMp4()
    {
        TestEnv.RequireOnPath("ffmpeg", "ffprobe");
        await Assert.That(Skipping.Run(() => Mp4FfmpegCases.ScenarioOutputsGifAndMp4(TestEnv.Binary, TestEnv.RepoRoot, Work))).IsTrue();
    }

    [Test]
    public async Task LargeGifWarns()
    {
        TestEnv.RequireOnPath("ffmpeg", "ffprobe");
        await Assert.That(Skipping.Run(() => Mp4FfmpegCases.LargeGifWarns(TestEnv.Binary, TestEnv.RepoRoot, Work))).IsTrue();
    }

    [ExcludeOn(OS.Windows)]
    [Test]
    public async Task CtrlCLeavesNoFiles()
    {
        TestEnv.RequireOnPath("ffmpeg", "ffprobe");
        await Assert.That(Skipping.Run(() => Mp4FfmpegCases.CtrlCLeavesNoFiles(TestEnv.Binary, TestEnv.RepoRoot, Work))).IsTrue();
    }
}

internal static class Mp4FfmpegCases
{

    internal static bool Mp4OptionsRequireMp4Format(string scenetake, string repoRoot, string work)
    {
        var scenario = WriteScenario(work, "options.yaml");
        var (exit, stderr) = Exec(scenetake, repoRoot, ["--fps", "30", scenario]);
        var (svgExit, svgStderr) = Exec(scenetake, repoRoot, ["--format", "svg", "--max-size=1280x1024", scenario]);
        return exit == 1 && stderr.Contains("--fps requires --format mp4", StringComparison.Ordinal)
            && svgExit == 1 && svgStderr.Contains("--max-size requires --format mp4", StringComparison.Ordinal);
    }

    internal static bool MaxFpsRejectedForMp4(string scenetake, string repoRoot, string work)
    {
        var (exit, stderr) = Exec(scenetake, repoRoot, ["mp4", "--max-fps", "20", "samples/basic.cast", Path.Combine(work, "x.mp4")]);
        var (runExit, runStderr) = Exec(scenetake, repoRoot, ["--format", "mp4", "--max-fps", "20", WriteScenario(work, "maxfps.yaml")]);
        return exit == 1 && stderr.Contains("use --fps for mp4", StringComparison.Ordinal)
            && runExit == 1 && runStderr.Contains("use --fps for mp4", StringComparison.Ordinal);
    }

    internal static bool MissingFfmpegShowsInstallHint(string scenetake, string repoRoot, string work)
    {
        var output = Path.Combine(work, "missing.mp4");
        var (exit, stderr) = Exec(scenetake, repoRoot, ["mp4", "samples/basic.cast", output],
            new() { ["SCENETAKE_FFMPEG"] = Path.Combine(work, "no-such-ffmpeg") });
        return exit == 1 && stderr.Contains("SCENETAKE_FFMPEG points to a file that does not exist", StringComparison.Ordinal)
            && stderr.Contains("Install ffmpeg with libx264", StringComparison.Ordinal)
            && !File.Exists(output);
    }

    internal static bool FfmpegWithoutLibx264IsRejected(string scenetake, string repoRoot, string work)
    {
        var fake = WriteFakeFfmpeg(work, "no-libx264", hasLibx264: false);
        var (exit, stderr) = Exec(scenetake, repoRoot, ["mp4", "samples/basic.cast", Path.Combine(work, "nolibx264.mp4")],
            new() { ["SCENETAKE_FFMPEG"] = fake });
        return exit == 1 && stderr.Contains("ffmpeg has no libx264 encoder", StringComparison.Ordinal);
    }

    internal static bool FfmpegFailureLeavesNoFile(string scenetake, string repoRoot, string work)
    {
        var fake = WriteFakeFfmpeg(work, "failing", hasLibx264: true);
        var output = Path.Combine(work, "failing.mp4");
        var (exit, stderr) = Exec(scenetake, repoRoot, ["mp4", "samples/basic.cast", output], new() { ["SCENETAKE_FFMPEG"] = fake });
        return exit == 1 && stderr.Contains("mp4 render failed", StringComparison.Ordinal)
            && stderr.Contains("simulated encoder failure", StringComparison.Ordinal)
            && !File.Exists(output)
            && !Directory.EnumerateFiles(work, "*.partial").Any();
    }

    internal static bool ScenarioKeepsCastWhenEncodeFails(string scenetake, string repoRoot, string work)
    {
        var fake = WriteFakeFfmpeg(work, "failing-scenario", hasLibx264: true);
        var stem = Path.Combine(work, "kept");
        var (exit, _) = Exec(scenetake, repoRoot, ["--format", "mp4", WriteScenario(work, "kept.yaml"), stem], new() { ["SCENETAKE_FFMPEG"] = fake });
        return exit == 1 && File.Exists(stem + ".cast") && !File.Exists(stem + ".mp4");
    }

    internal static bool SubcommandWritesH264(string scenetake, string repoRoot, string work)
    {
        var output = Path.Combine(work, "basic.mp4");
        var (exit, stderr) = Exec(scenetake, repoRoot, ["mp4", "samples/basic.cast", output]);
        if (exit != 0)
        {
            Console.Error.WriteLine(stderr);
            return false;
        }

        // x264 writes the settings it used into the stream. The search range and scene-cut threshold show that ffmpeg passed -x264-params on.
        var stream = Probe(output);
        var x264Settings = System.Text.Encoding.Latin1.GetString(File.ReadAllBytes(output));
        return stream["codec_name"] == "h264" && stream["profile"] == "High" && stream["pix_fmt"] == "yuv420p"
            && stream["color_space"] == "bt709" && stream["r_frame_rate"] == "30/1"
            && x264Settings.Contains("me_range=128 ", StringComparison.Ordinal)
            && x264Settings.Contains(" scenecut=10 ", StringComparison.Ordinal)
            && stderr.Contains("Written:", StringComparison.Ordinal);
    }

    // The gif command: a GIF89a that loops, at 20fps by default, whose frame delays add up to the loop the SVG has,
    // with the end hold. --no-loop leaves the loop extension out, and --fps sets the delays.
    internal static bool SubcommandWritesGif(string scenetake, string repoRoot, string work)
    {
        var output = Path.Combine(work, "basic.gif");
        var (exit, stderr) = Exec(scenetake, repoRoot, ["gif", "samples/basic.cast", output]);
        var once = Path.Combine(work, "basic-once.gif");
        var (onceExit, onceStderr) = Exec(scenetake, repoRoot, ["gif", "--no-loop", "--fps", "10", "samples/basic.cast", once]);
        if (exit != 0 || onceExit != 0)
        {
            Console.Error.WriteLine(stderr + onceStderr);
            return false;
        }

        var looping = ReadGif(output);
        var single = ReadGif(once);
        var svg = Path.Combine(work, "basic-loop.svg");
        Exec(scenetake, repoRoot, ["svg", "samples/basic.cast", svg]);
        var loop = double.Parse(System.Text.RegularExpressions.Regex.Match(File.ReadAllText(svg), @"\.l \{ visibility: hidden; animation: ([\d.]+)s").Groups[1].Value,
            System.Globalization.CultureInfo.InvariantCulture);
        var ok = looping.Loops && !single.Loops
            && looping.Delays.All(static d => d == 5) && single.Delays.All(static d => d == 10)
            && Math.Abs(looping.Delays.Sum() / 100.0 - loop) <= 0.05 && Math.Abs(single.Delays.Sum() / 100.0 - loop) <= 0.1
            && stderr.Contains("20fps", StringComparison.Ordinal) && onceStderr.Contains("10fps", StringComparison.Ordinal)
            && !Directory.EnumerateFiles(work, ".basic*").Any();
        if (!ok)
            Console.Error.WriteLine($"  loops {looping.Loops}/{single.Loops}, frames {looping.Delays.Count}/{single.Delays.Count}, seconds {looping.Delays.Sum() / 100.0}/{single.Delays.Sum() / 100.0} for a {loop}s loop, delays {string.Join(',', looping.Delays.Distinct())}/{string.Join(',', single.Delays.Distinct())}");
        return ok;
    }

    internal static bool ScenarioFormatGif(string scenetake, string repoRoot, string work)
    {
        var scenario = WriteScenario(work, "gif.yaml");
        var (exit, stderr) = Exec(scenetake, repoRoot, ["--format", "gif", "--window", "macos", scenario]);
        var gif = Path.ChangeExtension(scenario, ".gif");
        var ok = exit == 0 && File.Exists(Path.ChangeExtension(scenario, ".cast")) && File.Exists(gif) && ReadGif(gif).Delays.Count > 0
            && stderr.Contains($"Done: ", StringComparison.Ordinal);
        if (!ok)
            Console.Error.WriteLine(stderr);
        return ok;
    }

    // Noise in 256 colors on every cell changes the whole screen each frame, which no GIF compresses well.
    internal static bool LargeGifWarns(string scenetake, string repoRoot, string work)
    {
        const string Glyphs = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        var random = new Random(7);
        var events = new System.Text.StringBuilder();
        for (var frame = 0; frame < 20; frame++)
        {
            var screen = new System.Text.StringBuilder("\\u001b[H");
            for (var cell = 0; cell < 80 * 24; cell++)
                screen.Append($"\\u001b[38;5;{random.Next(256)};48;5;{random.Next(256)}m{Glyphs[random.Next(Glyphs.Length)]}");
            events.Append($"[{(frame == 0 ? 0 : 0.1):0.0},\"o\",\"{screen}\"]\n");
        }

        var cast = Path.Combine(work, "noise.cast");
        File.WriteAllText(cast, "{\"version\":3,\"term\":{\"cols\":80,\"rows\":24,\"type\":\"xterm-256color\"}}\n" + events);
        var (exit, stderr) = Exec(scenetake, repoRoot, ["gif", cast, Path.Combine(work, "noise.gif")]);
        var ok = exit == 0 && stderr.Contains("over the 3MB that Zenn takes. A smaller --font-size makes it smaller", StringComparison.Ordinal)
            && stderr.Contains("scenetake mp4 <cast>", StringComparison.Ordinal);
        if (!ok)
            Console.Error.WriteLine(stderr);
        return ok;
    }

    /// <summary>Walks a GIF's blocks: whether it has the NETSCAPE loop extension, and each frame's delay in hundredths of a second.</summary>
    internal static (bool Loops, List<int> Delays) ReadGif(string path)
    {
        var b = File.ReadAllBytes(path);
        if (System.Text.Encoding.ASCII.GetString(b, 0, 6) != "GIF89a")
            throw new InvalidDataException("not a GIF89a");

        var p = 13 + ((b[10] & 0x80) != 0 ? 3 << ((b[10] & 7) + 1) : 0);
        var loops = false;
        var delays = new List<int>();
        void SkipSubBlocks()
        {
            while (b[p] != 0)
                p += b[p] + 1;
            p++;
        }

        while (b[p] != 0x3B)
        {
            if (b[p] == 0x21)
            {
                var label = b[p + 1];
                if (label == 0xF9)
                    delays.Add(b[p + 4] | (b[p + 5] << 8));
                if (label == 0xFF && System.Text.Encoding.ASCII.GetString(b, p + 3, 11) == "NETSCAPE2.0")
                    loops = true;
                p += 2;
                SkipSubBlocks();
            }
            else if (b[p] == 0x2C)
            {
                var flags = b[p + 9];
                p += 10 + ((flags & 0x80) != 0 ? 3 << ((flags & 7) + 1) : 0);
                p++; // LZW minimum code size
                SkipSubBlocks();
            }
            else
            {
                throw new InvalidDataException($"unexpected block 0x{b[p]:x2} at {p}");
            }
        }

        return (loops, delays);
    }

    // --fps, --max-size, --size, --font-file, and the loop options are GIF's too. --crf, --max-fps, and --at are not.
    internal static bool CliRulesForGif(string scenetake, string repoRoot, string work)
    {
        var scenario = WriteScenario(work, "gif-rules.yaml");
        var cases = new (string[] Args, string Expected)[]
        {
        (["gif", "--crf", "20", "samples/basic.cast"], "--crf applies to mp4: a GIF has no H.264 quality"),
        (["gif", "--fps", "51", "samples/basic.cast"], "--fps for a GIF must be between 1 and 50: 51"),
        (["gif", "--max-fps", "10", "samples/basic.cast"], "--max-fps applies to SVG, use --fps for GIF"),
        (["gif", "--at", "1", "samples/basic.cast"], "--at applies to SVG and PNG: a GIF plays the whole recording"),
        (["gif", "--size", "640x480", "--max-size", "640x480", "samples/basic.cast"], "--size and --max-size cannot be used together"),
        (["gif", scenario], "is a scenario, and gif converts a cast file. To record the scenario with a GIF, run: scenetake --format gif "),
        (["--format", "gif", "--crf", "20", scenario], "--crf applies to mp4"),
        (["--format", "gif", "--at", "1", scenario], "--at applies to SVG and PNG: a GIF plays the whole recording"),
        (["--format", "gif", "--max-fps", "10", scenario], "--max-fps applies to SVG, use --fps for GIF"),
        (["--format", "gif", "--fps", "60", scenario], "--fps for a GIF must be between 1 and 50: 60"),
        (["--format", "svg", "--fps", "20", scenario], "--fps requires --format mp4 or gif"),
        (["--format", "png", "--crf", "20", scenario], "--crf requires --format mp4"),
        };

        var ok = true;
        foreach (var (args, expected) in cases)
        {
            var (exit, stderr) = Exec(scenetake, repoRoot, args);
            if (exit != 1 || !stderr.Contains(expected, StringComparison.Ordinal))
            {
                Console.Error.WriteLine($"  {string.Join(' ', args)}: exit {exit}, expected \"{expected}\" in:{Environment.NewLine}{stderr}");
                ok = false;
            }
        }

        // Any ffmpeg makes a GIF, so the hint leaves libx264 out. The scenario path says so before recording anything.
        var noFfmpeg = new Dictionary<string, string> { ["SCENETAKE_FFMPEG"] = "", ["PATH"] = Path.Combine(work, "empty-path") };
        var (gifExit, gifStderr) = Exec(scenetake, repoRoot, ["gif", "samples/basic.cast", Path.Combine(work, "x.gif")], noFfmpeg);
        var (runExit, runStderr) = Exec(scenetake, repoRoot, ["--format", "gif", scenario], noFfmpeg);
        if (gifExit != 1 || !gifStderr.Contains("GIF output needs ffmpeg, which was not found on PATH", StringComparison.Ordinal)
            || !gifStderr.Contains("Install ffmpeg, or set SCENETAKE_FFMPEG", StringComparison.Ordinal) || gifStderr.Contains("libx264", StringComparison.Ordinal)
            || runExit != 1 || !runStderr.Contains("GIF output needs ffmpeg", StringComparison.Ordinal) || File.Exists(Path.ChangeExtension(scenario, ".cast")))
        {
            Console.Error.WriteLine($"  without ffmpeg: gif exit {gifExit}: {gifStderr}{Environment.NewLine}  --format gif exit {runExit}: {runStderr}");
            ok = false;
        }

        return ok;
    }

    internal static bool ScreenSwitchesAreKeyframes(string scenetake, string repoRoot, string work)
    {
        // The events of mp4_render_test's ScreenSwitchesBecomeKeyframes. At 10fps scenetake forces frames 12, 31,
        // and 61, and the times it passes must land on those frames, not the ones before or after.
        var cast = Path.Combine(work, "switches.cast");
        File.WriteAllText(cast, "{\"version\":3,\"term\":{\"cols\":20,\"rows\":5,\"type\":\"xterm-256color\"}}\n" +
            """
        [0.0,"o","$ "]
        [1.02,"o","\u001b[?1049h"]
        [0.13,"o","\u001b[H█"]
        [1.89,"o","\u001b[?1049l"]
        [0.49,"o","\u001b[?1049h\u001b[H█"]
        [1.48,"o","\u001b[?1049hX"]
        [1.01,"o","\u001b[?1049l"]
        """ + "\n");
        var output = Path.Combine(work, "switches.mp4");
        var (exit, stderr) = Exec(scenetake, repoRoot, ["mp4", "--fps", "10", cast, output]);
        if (exit != 0)
        {
            Console.Error.WriteLine(stderr);
            return false;
        }

        var keyframes = ProbeKeyframes(output);
        return keyframes.Contains(12) && keyframes.Contains(31) && keyframes.Contains(61);
    }

    internal static bool MaxSizeDownscales(string scenetake, string repoRoot, string work)
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

    internal static bool SizeEncodesExactFrame(string scenetake, string repoRoot, string work)
    {
        // pty.cast is 60×14, 620×288 at 16px. 640×360 pads it; 320×180 pads it to 16:9 and scales it down.
        // The scenario path takes --size too: its 40×6 terminal is 420×128.
        var padded = Path.Combine(work, "padded.mp4");
        var scaled = Path.Combine(work, "scaled.mp4");
        var (paddedExit, paddedStderr) = Exec(scenetake, repoRoot, ["mp4", "--size", "640x360", "--fps", "12", "samples/pty.cast", padded]);
        var (scaledExit, scaledStderr) = Exec(scenetake, repoRoot, ["mp4", "--size=320x180", "--window", "macos", "--fps", "12", "samples/pty.cast", scaled]);
        var stem = Path.Combine(work, "sized-scenario");
        var (scenarioExit, scenarioStderr) = Exec(scenetake, repoRoot, ["--format", "mp4", "--size", "1280x720", WriteScenario(work, "sized.yaml"), stem]);
        if (paddedExit != 0 || scaledExit != 0 || scenarioExit != 0)
        {
            Console.Error.WriteLine(paddedStderr + scaledStderr + scenarioStderr);
            return false;
        }

        static (string, string) Size(Dictionary<string, string> stream) => (stream["width"], stream["height"]);
        return Size(Probe(padded)) == ("640", "360") && paddedStderr.Contains("(640x360, 12fps", StringComparison.Ordinal)
            && Size(Probe(scaled)) == ("320", "180")
            && Size(Probe(stem + ".mp4")) == ("1280", "720");
    }

    internal static bool ScenarioFormatMp4(string scenetake, string repoRoot, string work)
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

    internal static bool CtrlCLeavesNoFiles(string scenetake, string repoRoot, string work)
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

    // A scenario with a title card and an outputs list writes the cast and every entry in one run: a dark SVG, a light
    // copy, and a PNG of the card. --format writes only its file, and a replay writes the list too.
    internal static bool ScenarioOutputsList(string scenetake, string repoRoot, string work)
    {
        var folder = Directory.CreateDirectory(Path.Combine(work, "outputs-list")).FullName;
        var scenario = Path.Combine(folder, "demo.yaml");
        File.WriteAllText(scenario, """
        title: "outputs demo"
        width: 40
        height: 6
        settings:
          typing-speed: 0.001
          pre-delay: 0
          post-delay: 0
        render:
          theme:
            preset: github-dark
        steps:
          - card: "[bold]my-app"
            post-delay: 1
          - run: echo hello
        outputs:
          - format: svg
          - format: svg
            theme: github-light
            file: demo-light
          - format: png
            file: card.png
            at: 0.6
            typo: 1
        """);
        var (exit, stderr) = Exec(scenetake, folder, [scenario]);
        var files = new[] { "demo.cast", "demo.svg", "demo-light.svg", "card.png" }.Select(name => Path.Combine(folder, name)).ToArray();
        var light = File.Exists(files[2]) ? File.ReadAllText(files[2]) : "";
        var dark = File.Exists(files[1]) ? File.ReadAllText(files[1]) : "";

        foreach (var file in files.Skip(1))
            File.Delete(file);
        var (formatExit, formatStderr) = Exec(scenetake, folder, ["--format", "png", scenario, "only"]);
        // Checked now: the runs below write the outputs again.
        var formatOnly = formatExit == 0 && File.Exists(Path.Combine(folder, "only.png")) && !File.Exists(files[1]) && !File.Exists(files[3]);

        var outputsFile = Path.Combine(folder, "demo.outputs.json");
        var (saveExit, saveStderr) = Exec(scenetake, folder, ["--save-outputs", outputsFile, scenario]);
        foreach (var file in files.Skip(1))
            File.Delete(file);
        var (replayExit, replayStderr) = Exec(scenetake, folder, ["--replay", outputsFile, scenario]);

        var checks = new (string Name, bool Passed)[]
        {
        ("the first run", exit == 0 && stderr.Contains("Done: demo.cast, demo.svg, demo-light.svg, card.png", StringComparison.Ordinal)),
        ("the picture", stderr.Contains("<picture><source media=\"(prefers-color-scheme: dark)\" srcset=\"demo.svg\"><img src=\"demo-light.svg\" alt=\"outputs demo\"></picture>", StringComparison.Ordinal)),
        ("the unknown key", stderr.Contains("unknown key 'typo' in outputs entry 3", StringComparison.Ordinal)),
        // github-light draws on white, github-dark on #0d1117.
        ("the light copy", light.Contains("#ffffff", StringComparison.OrdinalIgnoreCase) && !light.Contains("#0d1117", StringComparison.OrdinalIgnoreCase)),
        ("the dark one", dark.Contains("#0d1117", StringComparison.OrdinalIgnoreCase)),
        ("--format", formatOnly),
        ("the --format note", formatStderr.Contains("Note: --format png writes only that file: the scenario's outputs are not written.", StringComparison.Ordinal)),
        ("the replay", saveExit == 0 && replayExit == 0 && files.All(File.Exists) && replayStderr.Contains("Done: demo.cast, demo.svg, demo-light.svg, card.png", StringComparison.Ordinal)),
        };
        foreach (var (name, passed) in checks.Where(static check => !check.Passed))
            Console.Error.WriteLine($"  failed: {name}");
        var ok = checks.All(static check => check.Passed);
        if (!ok)
            Console.Error.WriteLine($"{stderr}{formatStderr}{saveStderr}{replayStderr}");
        return ok;
    }

    // Each entry is checked before anything runs, with the CLI's rules for its format, and the error names the entry and its key.
    internal static bool ScenarioOutputsErrors(string scenetake, string repoRoot, string work)
    {
        var folder = Directory.CreateDirectory(Path.Combine(work, "outputs-errors")).FullName;
        var ok = true;
        ok &= Expect("- svg", "outputs entry 1 must be a mapping with 'format'");
        ok &= Expect("- file: x.svg", "outputs entry 1 has no 'format'");
        ok &= Expect("- format: webm", "outputs entry 1: unknown format: webm");
        ok &= Expect("- format: cast", "outputs entry 1: the cast is written by every recording");
        ok &= Expect("- format: svg\n  file: shot.png", "outputs entry 1 key 'file' ends with .png, and the format is svg: name it shot.svg");
        ok &= Expect("- format: svg\n- format: svg", "outputs entry 1 and outputs entry 2 both write");
        ok &= Expect("- format: gif\n  crf: 20", "outputs entry 1 (gif): 'crf' applies to mp4: a GIF has no H.264 quality");
        ok &= Expect("- format: svg\n  fps: 10", "outputs entry 1 (svg): 'fps' requires format mp4 or gif");
        ok &= Expect("- format: mp4\n  at: 1", "outputs entry 1 (mp4): 'at' applies to SVG and PNG");
        ok &= Expect("- format: png\n  theme: nosuch", "outputs entry 1 (png): unknown theme");
        ok &= Expect("- format: svg\n  margin: 999", "outputs entry 1 (svg): ");
        ok &= Expect("- format: svg\n  loop: maybe", "outputs entry 1 key 'loop' must be true or false");
        ok &= Expect("- format: svg\n  file: \"a\\0b\"", "outputs entry 1 key 'file' cannot contain a NUL character");
        ok &= Expect("- format: png\n  font-file: \"a\\0b.ttf\"", "outputs entry 1 key 'font-file' cannot contain a NUL character");
        // Rendering the PNG would replace the image it is drawn on, and a failed render would remove it.
        File.WriteAllText(Path.Combine(folder, "bg.png"), "not read before the check");
        ok &= Expect("- format: png\n  file: bg\n  background: bg.png", "outputs entry 1 would write over the background image bg.png");
        return ok;

        bool Expect(string outputs, string expected)
        {
            var scenario = Path.Combine(folder, "bad.yaml");
            var marker = Path.Combine(folder, "pre-ran.txt");
            File.Delete(marker);
            File.WriteAllText(scenario, $"pre:\n  - echo ran > \"{marker}\"\nsteps:\n  - echo hello\noutputs:\n" + string.Join('\n', outputs.Split('\n').Select(static line => "  " + line)) + "\n");
            var (exit, stderr) = Exec(scenetake, folder, [scenario]);
            var good = exit == 1 && stderr.Contains(expected, StringComparison.Ordinal) && !File.Exists(marker);
            if (!good)
                Console.Error.WriteLine($"  expected '{expected}' (and nothing run), got exit {exit}: {stderr}");
            return good;
        }
    }

    // A GIF and an mp4 from one recording, each with its own options.
    internal static bool ScenarioOutputsGifAndMp4(string scenetake, string repoRoot, string work)
    {
        var folder = Directory.CreateDirectory(Path.Combine(work, "outputs-video")).FullName;
        var scenario = Path.Combine(folder, "video.yaml");
        File.WriteAllText(scenario, """
        width: 40
        height: 6
        steps:
          - run: echo hello
        outputs:
          - format: gif
            fps: 10
            font-size: 12
          - format: mp4
            max-size: 320x240
        """);
        var (exit, stderr) = Exec(scenetake, folder, [scenario]);
        var gif = Path.Combine(folder, "video.gif");
        var mp4 = Path.Combine(folder, "video.mp4");
        if (!(exit == 0 && File.Exists(gif) && File.Exists(mp4) && stderr.Contains("Done: video.cast, video.gif, video.mp4", StringComparison.Ordinal)))
        {
            Console.Error.WriteLine(stderr);
            return false;
        }

        // Each entry's options reach its file. A GIF of the same cast without them is the default: 20 fps, whose frames
        // last 5 hundredths, and the default font size, which is wider. The 40-column terminal is wider than 320 at any
        // font size the default gives.
        var plain = Path.Combine(folder, "plain.gif");
        var (plainExit, plainStderr) = Exec(scenetake, folder, ["gif", "video.cast", plain]);
        var delays = ReadGif(gif).Delays;
        var ok = plainExit == 0
            && delays.Count > 0 && delays.TrueForAll(static delay => delay % 10 == 0)
            && GifWidth(gif) < GifWidth(plain)
            && int.Parse(Probe(mp4)["width"], System.Globalization.CultureInfo.InvariantCulture) <= 320;
        if (!ok)
            Console.Error.WriteLine($"  delays {string.Join(',', delays)}, width {GifWidth(gif)} against {(File.Exists(plain) ? GifWidth(plain) : 0)}, mp4 {Probe(mp4)["width"]}{Environment.NewLine}{plainStderr}");
        return ok;

        static int GifWidth(string path) => File.ReadAllBytes(path) is var bytes ? bytes[6] | (bytes[7] << 8) : 0;
    }

    // An entry that fails while it renders removes its own file. The others are written, and the run exits 1 without
    // the Done line. The stand-in ffmpeg fails every encode, so the mp4 fails and the SVG, which needs no ffmpeg, does not.
    internal static bool ScenarioOutputsFailingEntry(string scenetake, string work)
    {
        var folder = Directory.CreateDirectory(Path.Combine(work, "outputs-failing")).FullName;
        var fake = WriteFakeFfmpeg(folder, "failing", hasLibx264: true);
        File.WriteAllText(Path.Combine(folder, "fail.yaml"), "width: 40\nheight: 6\nsteps:\n  - echo hello\noutputs:\n  - format: mp4\n  - format: svg\n");
        var (exit, stderr) = Exec(scenetake, folder, ["fail.yaml"], new() { ["SCENETAKE_FFMPEG"] = fake });
        var ok = exit == 1 && File.Exists(Path.Combine(folder, "fail.cast")) && File.Exists(Path.Combine(folder, "fail.svg"))
            && !File.Exists(Path.Combine(folder, "fail.mp4")) && !Directory.EnumerateFiles(folder, "*.partial").Any()
            && !stderr.Contains("Done:", StringComparison.Ordinal);
        if (!ok)
            Console.Error.WriteLine($"  exit {exit}{Environment.NewLine}{stderr}");
        return ok;
    }

    // A dot in an entry's file that is not an output format's extension is part of the name.
    internal static bool ScenarioOutputsFileNames(string scenetake, string work)
    {
        var folder = Directory.CreateDirectory(Path.Combine(work, "outputs-names")).FullName;
        File.WriteAllText(Path.Combine(folder, "names.yaml"), "width: 40\nheight: 6\nsteps:\n  - echo hello\noutputs:\n  - format: svg\n  - format: svg\n    file: names.light\n");
        var (exit, stderr) = Exec(scenetake, folder, ["names.yaml"]);
        var ok = exit == 0 && File.Exists(Path.Combine(folder, "names.svg")) && File.Exists(Path.Combine(folder, "names.light.svg"));
        if (!ok)
            Console.Error.WriteLine($"  exit {exit}{Environment.NewLine}{stderr}");
        return ok;
    }

    internal static string WriteScenario(string work, string name)
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
    internal static string WriteFakeFfmpeg(string work, string name, bool hasLibx264)
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
    internal static string WriteSucceedingFakeFfmpeg(string work, string name)
    {
        if (OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Unix only");
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

    internal static (int ExitCode, string Stderr) Exec(string file, string workingDirectory, string[] arguments, Dictionary<string, string>? environment = null)
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

    internal static Dictionary<string, string> Probe(string path)
    {
        var startInfo = new ProcessStartInfo(TestEnv.FindOnPath("ffprobe")!)
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

    /// <summary>Numbers of the frames ffprobe reports as keyframes, counted in presentation order from 0.</summary>
    internal static HashSet<int> ProbeKeyframes(string path)
    {
        var startInfo = new ProcessStartInfo(TestEnv.FindOnPath("ffprobe")!)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in new[] { "-v", "error", "-select_streams", "v:0", "-show_entries", "frame=key_frame", "-of", "csv=p=0", path })
            startInfo.ArgumentList.Add(argument);

        using var process = Process.Start(startInfo)!;
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"ffprobe failed for {path}");

        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return [.. lines.Select((line, frame) => (line, frame)).Where(x => x.line.StartsWith('1')).Select(x => x.frame)];
    }

    internal static bool ScenarioChecksFfmpegBeforePre(string scenetake, string repoRoot, string work)
    {
        // ffmpeg is checked before pre runs, so a missing ffmpeg records nothing.
        var directory = Directory.CreateDirectory(Path.Combine(work, "check-ffmpeg")).FullName;
        var scenario = WriteScenarioWithHooks(directory, "s.yaml");
        var (exit, stderr) = Exec(scenetake, repoRoot, ["--format", "mp4", scenario], new() { ["SCENETAKE_FFMPEG"] = Path.Combine(directory, "no-such-ffmpeg") });
        return exit == 1 && stderr.Contains("SCENETAKE_FFMPEG points to a file that does not exist", StringComparison.Ordinal)
            && !File.Exists(Path.Combine(directory, "s.cast")) && !File.Exists(Path.Combine(directory, "pre-ran.txt"));
    }

    internal static bool ScenarioChecksFontBeforePre(string scenetake, string repoRoot, string work)
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

    internal static bool ScenarioRejectsOversizedTerminal(string scenetake, string repoRoot, string work)
    {
        // The cast format allows 1–512 columns and rows. A larger scenario fails before pre, instead of
        // after the whole recording when SVG reads the cast back.
        var directory = Directory.CreateDirectory(Path.Combine(work, "oversized")).FullName;
        var scenario = WriteScenarioWithHooks(directory, "s.yaml", width: 600);
        var (exit, stderr) = Exec(scenetake, repoRoot, ["--format", "svg", scenario]);
        return exit == 1 && stderr.Contains("width must be between 1 and 512", StringComparison.Ordinal)
            && !File.Exists(Path.Combine(directory, "s.cast")) && !File.Exists(Path.Combine(directory, "pre-ran.txt"));
    }

    internal static bool PostRunsAndStaleMp4GoesWhenEncodeFails(string scenetake, string repoRoot, string work)
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

    internal static bool StaleMp4GoesWhenCastIsBad(string scenetake, string repoRoot, string work)
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

    // `capture` records one command without a scenario file: an SVG and its cast by default, named after the command in
    // the current folder, the command run in a PTY unless --pipe, and words after -- quoted again for the shell.
    internal static bool CaptureRecordsOneCommand(string scenetake, string work)
    {
        var directory = Directory.CreateDirectory(Path.Combine(work, "capture")).FullName;
        var ok = true;

        var (pipeExit, pipeStderr) = Exec(scenetake, directory, ["capture", "--pipe", "--format", "cast", "--", "echo", "hello"]);
        var pipeCast = Path.Combine(directory, "echo.cast");
        var pipeText = File.Exists(pipeCast) ? File.ReadAllText(pipeCast) : "";
        ok &= Check(pipeExit == 0 && pipeText.Contains("\"title\":\"echo hello\"", StringComparison.Ordinal) && pipeText.Contains("hello\\r\\n", StringComparison.Ordinal)
            && !File.Exists(Path.Combine(directory, "echo.svg")), $"pipe capture: exit {pipeExit}{Environment.NewLine}{pipeStderr}{pipeText}");

        var (svgExit, svgStderr) = Exec(scenetake, directory, ["capture", "-o", "greet", "echo hi"]);
        ok &= Check(svgExit == 0 && File.Exists(Path.Combine(directory, "greet.cast")) && File.Exists(Path.Combine(directory, "greet.svg"))
            && svgStderr.Contains("Capturing: echo hi", StringComparison.Ordinal), $"default SVG in a PTY: exit {svgExit}{Environment.NewLine}{svgStderr}");

        var (wordsExit, wordsStderr) = Exec(scenetake, directory, ["capture", "--pipe", "--format", "cast", "-o", "words", "--", "echo", "a b", "it's"]);
        var expectedLine = OperatingSystem.IsWindows() ? "Capturing: echo 'a b' 'it''s'" : "Capturing: echo 'a b' 'it'\\''s'";
        var wordsText = File.Exists(Path.Combine(directory, "words.cast")) ? File.ReadAllText(Path.Combine(directory, "words.cast")) : "";
        ok &= Check(wordsExit == 0 && wordsStderr.Contains(expectedLine, StringComparison.Ordinal) && wordsText.Contains("a b", StringComparison.Ordinal)
            && wordsText.Contains("it's", StringComparison.Ordinal), $"words: exit {wordsExit}{Environment.NewLine}{wordsStderr}{wordsText}");

        // --save-scenario writes the scenario before the command runs, with the style options as render keys, names the
        // files after it, and never replaces a file. Running the saved file gives the same SVG.
        var saved = Path.Combine(directory, "saved", "demo.yaml");
        var (saveExit, saveStderr) = Exec(scenetake, directory, ["capture", "--pipe", "--window", "macos", "--theme", "dracula", "--fit", "none", "--save-scenario", saved, "--", "echo", "a: b"]);
        var savedText = File.Exists(saved) ? File.ReadAllText(saved) : "";
        var savedSvg = Path.ChangeExtension(saved, ".svg");
        var firstSvg = File.Exists(savedSvg) ? File.ReadAllText(savedSvg) : "";
        ok &= Check(saveExit == 0 && savedText.Contains("render:\n  window: macos\n  fit: none\n  theme:\n    preset: \"dracula\"\n", StringComparison.Ordinal)
            && savedText.Contains("  - run: \"echo 'a: b'\"\n", StringComparison.Ordinal) && !savedText.Contains("pty:", StringComparison.Ordinal)
            && File.Exists(Path.ChangeExtension(saved, ".cast")) && firstSvg.Length > 0 && !File.Exists(Path.Combine(directory, "echo.svg")),
            $"--save-scenario: exit {saveExit}{Environment.NewLine}{saveStderr}{savedText}");
        var (againExit, againStderr) = Exec(scenetake, directory, ["--format", "svg", saved]);
        ok &= Check(againExit == 0 && File.ReadAllText(savedSvg) == firstSvg, $"the saved scenario records the same SVG: exit {againExit}{Environment.NewLine}{againStderr}");
        var (existsExit, existsStderr) = Exec(scenetake, directory, ["capture", "--save-scenario", saved, "--", "echo", "other"]);
        ok &= Check(existsExit == 1 && existsStderr.Contains("saved/demo.yaml already exists", StringComparison.Ordinal) && File.ReadAllText(saved) == savedText,
            $"--save-scenario keeps a file: exit {existsExit}{Environment.NewLine}{existsStderr}");

        // --strict on one command speaks of the command: a capture has no step to set continue-on-error on, and a
        // --save-scenario file, written before the command ran, stays.
        var failing = Path.Combine(directory, "failing.yaml");
        var (strictSavedExit, strictSavedStderr) = Exec(scenetake, directory, ["capture", "--strict", "--pipe", "--save-scenario", failing, "--", "exit", "3"]);
        ok &= Check(strictSavedExit == 1 && strictSavedStderr.Contains("Error: --strict stopped: the command exited 3: exit 3", StringComparison.Ordinal)
            && strictSavedStderr.Contains("Nothing was written but the scenario. Without --strict, the recording keeps a command that fails.", StringComparison.Ordinal)
            && !strictSavedStderr.Contains("continue-on-error", StringComparison.Ordinal) && File.Exists(failing) && !File.Exists(Path.ChangeExtension(failing, ".cast")),
            $"capture --strict --save-scenario: exit {strictSavedExit}{Environment.NewLine}{strictSavedStderr}");
        var (strictExit, strictStderr) = Exec(scenetake, directory, ["capture", "--strict", "--pipe", "-o", "strict", "--", "exit", "3"]);
        ok &= Check(strictExit == 1 && strictStderr.Contains("Nothing was written. Without --strict, the recording keeps a command that fails.", StringComparison.Ordinal)
            && !File.Exists(Path.Combine(directory, "strict.cast")), $"capture --strict: exit {strictExit}{Environment.NewLine}{strictStderr}");

        foreach (var (args, error) in new (string[] Args, string Error)[]
        {
        (["capture"], "capture needs a command"),
        (["capture", "a", "b"], "capture takes one command: quote it as one argument, or put it after --: b"),
        (["capture", "one", "--", "two"], "give it as one argument or after --, not both"),
        (["capture", "--height", "600", "--", "x"], "--height must be a whole number of rows from 1 to 512: 600"),
        (["capture", "--timeout", "0", "--", "x"], "--timeout must be seconds above 0"),
        (["capture", "--save-scenario", "a.yaml", "--save-scenario", "b.yaml", "--", "x"], "duplicate option: --save-scenario"),
        })
        {
            var (exit, stderr) = Exec(scenetake, directory, args);
            ok &= Check(exit == 1 && stderr.Contains(error, StringComparison.Ordinal), $"{string.Join(' ', args)}: exit {exit}{Environment.NewLine}{stderr}");
        }

        return ok;

        static bool Check(bool condition, string message)
        {
            if (!condition)
                Console.Error.WriteLine($"  {message}");
            return condition;
        }
    }

    internal static bool InitWritesStepsForItsShell(string scenetake, string work)
    {
        // init writes its steps for one shell. --shell picks it and sets shell: in the file, so the steps never
        // run in another shell; without --shell they are written for the OS default shell, and shell: stays
        // unset. Before, the template was written for bash only, and its stderr step recorded a pwsh
        // ParserError on Windows. Each template whose shell is here must record with no step failing.
        var directory = Directory.CreateDirectory(Path.Combine(work, "init")).FullName;
        const string bashStderr = "run: echo \"stderr sample\" 1>&2";
        const string pwshStderr = "run: \"[Console]::Error.WriteLine('stderr sample')\"";
        var templates = new (string Name, string[] Options, string? ShellLine, string StderrStep)[]
        {
        ("bash", ["--shell", "bash"], "shell: bash ", bashStderr),
        ("pwsh", ["--shell=pwsh"], "shell: pwsh ", pwshStderr),
        ("default", [], null, OperatingSystem.IsWindows() ? pwshStderr : bashStderr),
        };

        var written = true;
        var runnable = new List<(string Name, string Path)>();
        foreach (var (name, options, shellLine, stderrStep) in templates)
        {
            var path = Path.Combine(directory, $"{name}.yaml");
            var (exit, stderr) = Exec(scenetake, directory, ["init", .. options, path]);
            var lines = exit == 0 ? File.ReadAllLines(path) : [];
            var shellOk = shellLine is null
                ? !lines.Any(line => line.StartsWith("shell:", StringComparison.Ordinal))
                : lines.Any(line => line.StartsWith(shellLine, StringComparison.Ordinal));
            if (exit != 0 || !shellOk || !lines.Any(line => line.Trim() == stderrStep || line.Trim().StartsWith(stderrStep + " ", StringComparison.Ordinal)))
            {
                Console.Error.WriteLine($"  {name}: exit {exit}{Environment.NewLine}{stderr}{string.Join(Environment.NewLine, lines)}");
                written = false;
                continue;
            }

            // UTF-8 without a BOM, as the other files scenetake writes. Recorded unchanged, the template looks like a
            // demo: a window on a background, a title card and an end card, a prompt with the folder, and an SVG from
            // outputs. The reader who starts from init meets fit, masking, keys with wait, and a GIF output as comments.
            var bytes = File.ReadAllBytes(path);
            var commented = lines.Where(line => line.TrimStart().StartsWith('#')).Select(line => line.TrimStart('#', ' ')).ToList();
            var live = lines.Where(line => !line.TrimStart().StartsWith('#')).Select(line => line.Trim()).ToList();
            var missing = new[] { "fit: ", "mask: ", "mask-auto: ", "keys: ", "- wait: ", "- format: gif" }
                .Where(key => !commented.Any(line => line.StartsWith(key, StringComparison.Ordinal) || line.StartsWith(key.TrimEnd(), StringComparison.Ordinal)))
                .ToList();
            var notLive = new[] { "window: macos", "background: ", "- card:", "outputs:", "- format: svg", "- text: \"{dir}\"" }
                .Where(key => !live.Any(line => line.StartsWith(key, StringComparison.Ordinal)))
                .ToList();
            if (bytes is [0xEF, 0xBB, 0xBF, ..] || missing.Count != 0 || notLive.Count != 0)
            {
                Console.Error.WriteLine($"  {name}: BOM {bytes is [0xEF, 0xBB, 0xBF, ..]}, keys missing from the comments: {string.Join(", ", missing)}, not live: {string.Join(", ", notLive)}");
                written = false;
                continue;
            }

            // Windows finds bash in the Git folders, not on PATH, so only Unix checks PATH first. A missing
            // bash on Windows shows up as "could not be resolved" below.
            if (!OperatingSystem.IsWindows() && name != "default" && TestEnv.FindOnPath(name) is null)
            {
                Console.Error.WriteLine($"  {name}: not on PATH, template not recorded");
                continue;
            }

            runnable.Add((name, path));
        }

        // The templates type at the default speed for about 16 seconds each, so they record in parallel.
        var runs = runnable
            .Select(template => Task.Run(() => (template.Name, Result: Exec(scenetake, directory, [template.Path, Path.Combine(directory, template.Name)]))))
            .ToArray();
        Task.WaitAll(runs);
        var recorded = true;
        foreach (var (name, (exit, stderr)) in runs.Select(run => run.Result))
        {
            if (OperatingSystem.IsWindows() && name == "bash" && stderr.Contains("could not be resolved", StringComparison.Ordinal))
            {
                Console.Error.WriteLine("  bash: Git Bash not found, template not recorded");
                continue;
            }

            // Its outputs list writes the SVG next to the cast.
            if (exit != 0 || stderr.Contains("Warning:", StringComparison.Ordinal) || !File.Exists(Path.Combine(directory, name + ".svg")))
            {
                Console.Error.WriteLine($"  {name}: exit {exit}{Environment.NewLine}{stderr}");
                recorded = false;
            }
        }

        // A shell init has no template for, a missing value, and a second --shell are errors that write nothing.
        var refused = Path.Combine(directory, "refused.yaml");
        var (unknownExit, unknownStderr) = Exec(scenetake, directory, ["init", "--shell", "zsh", refused]);
        var (missingExit, missingStderr) = Exec(scenetake, directory, ["init", "--shell="]);
        var (twiceExit, twiceStderr) = Exec(scenetake, directory, ["init", "--shell", "bash", "--shell", "pwsh", refused]);
        var errorsOk = unknownExit == 1 && unknownStderr.Contains("--shell must be bash, pwsh, or powershell: zsh", StringComparison.Ordinal)
            && missingExit == 1 && missingStderr.Contains("--shell requires a value", StringComparison.Ordinal)
            && twiceExit == 1 && twiceStderr.Contains("duplicate option: --shell", StringComparison.Ordinal)
            && !File.Exists(refused);
        if (!errorsOk)
            Console.Error.WriteLine($"  errors:{Environment.NewLine}{unknownStderr}{missingStderr}{twiceStderr}");

        return written && recorded && errorsOk;
    }

    internal static bool CliRulesForValuesAndFormats(string scenetake, string repoRoot, string work)
    {
        // --max-fps only applies to SVG. A value that is another option means the value is missing. Range errors
        // use plain ASCII, which every console code page can show.
        var scenario = WriteScenario(work, "rules.yaml");
        var (maxFpsExit, maxFpsStderr) = Exec(scenetake, repoRoot, ["--max-fps", "10", scenario]);
        var (fontExit, fontStderr) = Exec(scenetake, repoRoot, ["--format", "mp4", "--font-file", "--fps", "30", scenario]);
        var (fpsExit, fpsStderr) = Exec(scenetake, repoRoot, ["mp4", "--fps", "99", "samples/basic.cast"]);
        // svg and mp4 take a cast. Given a scenario, they say so and give the command that records it, instead
        // of the JSON parser's error about the first YAML line. mp4 says it before looking for ffmpeg. The
        // default output is the scenario's own SVG or mp4 (what --format svg wrote), and a cast read that
        // fails deletes its output, so the slip used to delete that file: it must be left as it was. A .yaml
        // file that holds a cast (its first character is "{") still converts.
        var ownSvg = Path.ChangeExtension(scenario, ".svg");
        var ownMp4 = Path.ChangeExtension(scenario, ".mp4");
        File.WriteAllText(ownSvg, "svg of the scenario");
        File.WriteAllText(ownMp4, "mp4 of the scenario");
        var (yamlExit, yamlStderr) = Exec(scenetake, repoRoot, ["svg", scenario]);
        var (mp4YamlExit, mp4YamlStderr) = Exec(scenetake, repoRoot, ["mp4", scenario],
            new() { ["SCENETAKE_FFMPEG"] = Path.Combine(work, "no-such-ffmpeg") });
        var ownOutputsKept = File.Exists(ownSvg) && File.ReadAllText(ownSvg) == "svg of the scenario"
            && File.Exists(ownMp4) && File.ReadAllText(ownMp4) == "mp4 of the scenario";
        var castAsYaml = Path.Combine(work, "cast-named.yaml");
        File.Copy(Path.Combine(repoRoot, "samples", "basic.cast"), castAsYaml, overwrite: true);
        var (castYamlExit, castYamlStderr) = Exec(scenetake, repoRoot, ["svg", castAsYaml, Path.Combine(work, "cast-named.svg")]);
        // A second --format used to replace the first without a word. Both spellings count, and nothing runs.
        var (formatExit, formatStderr) = Exec(scenetake, repoRoot, ["--format", "svg", "--format=gif", scenario]);
        var ok = maxFpsExit == 1 && maxFpsStderr.Contains("--max-fps requires --format svg", StringComparison.Ordinal)
            && formatExit == 1 && formatStderr.Contains("--format takes one format. List several under 'outputs:' in the scenario, or record once and convert the cast for each other format", StringComparison.Ordinal)
            && !formatStderr.Contains("Generating cast", StringComparison.Ordinal)
            && fontExit == 1 && fontStderr.Contains("--font-file requires a value", StringComparison.Ordinal)
            && fpsExit == 1 && fpsStderr.Contains("--fps must be between 1 and 60: 99", StringComparison.Ordinal)
            && yamlExit == 1 && yamlStderr.Contains("is a scenario, and svg converts a cast file. To record the scenario with an SVG, run: scenetake --format svg ", StringComparison.Ordinal)
            && !yamlStderr.Contains("JSON", StringComparison.Ordinal)
            && mp4YamlExit == 1 && mp4YamlStderr.Contains("is a scenario, and mp4 converts a cast file. To record the scenario with an mp4, run: scenetake --format mp4 ", StringComparison.Ordinal)
            && !mp4YamlStderr.Contains("ffmpeg", StringComparison.Ordinal)
            && ownOutputsKept
            && castYamlExit == 0;
        if (!ok)
            Console.Error.WriteLine($"{maxFpsStderr}{fontStderr}{fpsStderr}{yamlStderr}{mp4YamlStderr}{castYamlStderr}{formatStderr}");
        return ok;
    }

    // --window-title, --background, --margin, and --padding: ranges, duplicates, an empty title, an image relative to
    // the current folder, and an image with cast output, which a cast cannot keep. The SVG shows what each set.
    internal static bool CliRulesForWindowStyle(string scenetake, string repoRoot, string work)
    {
        var scenario = WriteScenario(work, "style.yaml");
        var image = Path.Combine(work, "wallpaper.png");
        File.WriteAllBytes(image, Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg=="));
        var cases = new (string[] Args, string Expected)[]
        {
        (["svg", "--margin", "201", "samples/basic.cast"], "margin must be between 0 and 200 pixels: 201"),
        (["svg", "--padding", "-1", "samples/basic.cast"], "padding must be between 0 and 100 pixels: -1"),
        (["svg", "--padding", "4", "--padding", "5", "samples/basic.cast"], "duplicate option: --padding"),
        (["svg", "--background", "#12345", "samples/basic.cast"], "invalid background color (expected #rgb or #rrggbb): #12345"),
        (["svg", "--background", "#111 #222 #333 #444 #555", "samples/basic.cast"], "background takes at most 4 colors"),
        (["svg", "--background", "no-such.png", "samples/basic.cast"], "background image not found: no-such.png"),
        (["svg", "--background", "notes.txt", "samples/basic.cast"], "or a .png, .jpg, .gif, or .webp file: notes.txt"),
        (["--background", image, scenario], "--background with an image file requires --format svg, mp4, png, or gif"),
        (["svg", "--window-title", "a", "--window-title", "b", "samples/basic.cast"], "duplicate option: --window-title"),
        (["svg", "--opacity", "1.5", "samples/basic.cast"], "opacity must be between 0 and 1: 1.5"),
        (["svg", "--opacity", "85%", "samples/basic.cast"], "invalid opacity value: 85%"),
        (["svg", "--opacity", "0.5", "--opacity", "0.6", "samples/basic.cast"], "duplicate option: --opacity"),
        (["svg", "--blur", "51", "samples/basic.cast"], "blur must be between 0 and 50 pixels: 51"),
        (["svg", "--blur", "4", "--blur", "5", "samples/basic.cast"], "duplicate option: --blur"),
        };

        var ok = true;
        foreach (var (args, expected) in cases)
        {
            var (exit, stderr) = Exec(scenetake, repoRoot, args);
            if (exit != 1 || !stderr.Contains(expected, StringComparison.Ordinal))
            {
                Console.Error.WriteLine($"  {string.Join(' ', args)}: exit {exit}, expected \"{expected}\" in:{Environment.NewLine}{stderr}");
                ok = false;
            }
        }

        var svg = Path.Combine(work, "style.svg");
        var (styledExit, styledStderr) = Exec(scenetake, repoRoot,
            ["svg", "--window", "macos", "--window-title", "Styled <demo>", "--background", "#1e3c72,#2a5298", "--margin", "40", "--padding", "12", "samples/basic.cast", svg]);
        var styled = styledExit == 0 ? File.ReadAllText(svg) : "";
        ok &= Check(styledExit == 0 && styled.Contains(">Styled &lt;demo&gt;</text>", StringComparison.Ordinal)
            && styled.Contains("<linearGradient id=\"desktop\"", StringComparison.Ordinal),
            $"styled svg: exit {styledExit}: {styledStderr}");

        // An empty title hides the cast's own title ("Basic Demo").
        var (hiddenExit, _) = Exec(scenetake, repoRoot, ["svg", "--window", "macos", "--window-title=", "samples/basic.cast", svg]);
        ok &= Check(hiddenExit == 0 && !File.ReadAllText(svg).Contains("Basic Demo</text>", StringComparison.Ordinal), "--window-title= did not hide the title");
        var (shownExit, _) = Exec(scenetake, repoRoot, ["svg", "--window", "macos", "samples/basic.cast", svg]);
        ok &= Check(shownExit == 0 && File.ReadAllText(svg).Contains(">Basic Demo</text>", StringComparison.Ordinal), "the cast's title is not in the title bar");

        // Opacity and blur act on a background; without one they warn and leave the window opaque.
        var (frostExit, frostStderr) = Exec(scenetake, repoRoot, ["svg", "--background", "#1e3c72", "--opacity", "0.8", "--blur", "6", "samples/basic.cast", svg]);
        var frost = frostExit == 0 ? File.ReadAllText(svg) : "";
        ok &= Check(frostExit == 0 && frost.Contains("fill-opacity=\"0.8\"", StringComparison.Ordinal) && frost.Contains("stdDeviation=\"6\"", StringComparison.Ordinal)
            && !frostStderr.Contains("Warning", StringComparison.Ordinal), $"frosted svg: exit {frostExit}: {frostStderr}");
        var (bareExit, bareStderr) = Exec(scenetake, repoRoot, ["svg", "--opacity", "0.8", "samples/basic.cast", svg]);
        ok &= Check(bareExit == 0 && bareStderr.Contains("Warning: opacity has no effect without a background", StringComparison.Ordinal)
            && !File.ReadAllText(svg).Contains("fill-opacity=\"", StringComparison.Ordinal), $"opacity without a background: exit {bareExit}: {bareStderr}");

        // An image path is relative to the current folder, and the SVG embeds it.
        var (imageExit, imageStderr) = Exec(scenetake, work, ["svg", "--background", "wallpaper.png", Path.Combine(repoRoot, "samples", "basic.cast"), svg]);
        ok &= Check(imageExit == 0 && File.ReadAllText(svg).Contains("href=\"data:image/png;base64,", StringComparison.Ordinal), $"image background: exit {imageExit}: {imageStderr}");
        return ok;

        static bool Check(bool condition, string message)
        {
            if (!condition)
                Console.Error.WriteLine($"  {message}");
            return condition;
        }
    }

    // --loop / --no-loop, --end-hold, and --fade-out: ranges, duplicates, mp4 refusing the loop flags, the note on a fade
    // that an SVG played once leaves out, and render keys that the cast keeps for a later conversion.
    internal static bool CliRulesForLoopAndEnd(string scenetake, string repoRoot, string work)
    {
        var cases = new (string[] Args, string Expected)[]
        {
        (["svg", "--end-hold", "61", "samples/basic.cast"], "end-hold must be between 0 and 60 seconds: 61"),
        (["svg", "--end-hold", "2s", "samples/basic.cast"], "invalid end-hold value: 2s"),
        (["svg", "--fade-out", "11", "samples/basic.cast"], "fade-out must be between 0 and 10 seconds: 11"),
        (["svg", "--fade-out", "1", "--fade-out", "2", "samples/basic.cast"], "duplicate option: --fade-out"),
        (["svg", "--loop", "--no-loop", "samples/basic.cast"], "duplicate option: --loop and --no-loop"),
        (["mp4", "--no-loop", "samples/basic.cast"], "--no-loop applies to SVG"),
        (["mp4", "--loop", "samples/basic.cast"], "--loop applies to SVG"),
        };

        var ok = true;
        foreach (var (args, expected) in cases)
        {
            var (exit, stderr) = Exec(scenetake, repoRoot, args);
            if (exit != 1 || !stderr.Contains(expected, StringComparison.Ordinal))
            {
                Console.Error.WriteLine($"  {string.Join(' ', args)}: exit {exit}, expected \"{expected}\" in:{Environment.NewLine}{stderr}");
                ok = false;
            }
        }

        var svg = Path.Combine(work, "loop.svg");
        var (onceExit, onceStderr) = Exec(scenetake, repoRoot, ["svg", "--no-loop", "--fade-out", "1", "samples/basic.cast", svg]);
        var once = onceExit == 0 ? File.ReadAllText(svg) : "";
        ok &= Check(onceExit == 0 && once.Contains("step-start forwards", StringComparison.Ordinal) && !once.Contains("@keyframes fade", StringComparison.Ordinal)
            && onceStderr.Contains("Warning: fade-out has no effect on an SVG that plays once", StringComparison.Ordinal),
            $"--no-loop --fade-out: exit {onceExit}: {onceStderr}");

        // The scenario's keys go into the cast; --loop on the svg command undoes its loop: false.
        var scenario = Path.Combine(work, "ending.yaml");
        File.WriteAllText(scenario, """
        title: "ending"
        width: 40
        height: 6
        render:
          loop: false
          end-hold: 2.5
          fade-out: 0.75
        steps:
          - run: echo hello
        """);
        var cast = Path.Combine(work, "ending.cast");
        var (castExit, castStderr) = Exec(scenetake, work, [scenario, cast]);
        var header = castExit == 0 ? File.ReadLines(cast).First() : "";
        ok &= Check(castExit == 0 && header.Contains("\"st:loop=false\",\"st:end-hold=2.5\",\"st:fade-out=0.75\"", StringComparison.Ordinal),
            $"scenario cast: exit {castExit}: {castStderr}{header}");
        var (loopExit, loopStderr) = Exec(scenetake, work, ["svg", "--loop", cast, svg]);
        var looped = loopExit == 0 ? File.ReadAllText(svg) : "";
        ok &= Check(loopExit == 0 && looped.Contains("step-start infinite", StringComparison.Ordinal) && looped.Contains("<g class=\"fade\">", StringComparison.Ordinal),
            $"svg --loop on a cast with loop false: exit {loopExit}: {loopStderr}");

        // A value out of range in the YAML stops before anything runs.
        File.WriteAllText(scenario, """
        render:
          end-hold: 61
        steps:
          - run: echo hello
        """);
        var (rangeExit, rangeStderr) = Exec(scenetake, work, [scenario, cast]);
        ok &= Check(rangeExit == 1 && rangeStderr.Contains("invalid render.end-hold: must be between 0 and 60 seconds: 61", StringComparison.Ordinal),
            $"render.end-hold 61: exit {rangeExit}: {rangeStderr}");
        return ok;

        static bool Check(bool condition, string message)
        {
            if (!condition)
                Console.Error.WriteLine($"  {message}");
            return condition;
        }
    }

    // --at and PNG output: which formats take them, the options a still has no use for, and real output on both paths.
    internal static bool CliRulesForStillImages(string scenetake, string repoRoot, string work)
    {
        var scenario = WriteScenario(work, "still.yaml");
        var cases = new (string[] Args, string Expected)[]
        {
        (["--at", "1", scenario], "--at requires --format svg or png"),
        (["--format", "mp4", "--at", "1", scenario], "--at applies to SVG and PNG: an mp4 plays the whole recording"),
        (["--format", "png", "--max-fps", "10", scenario], "--max-fps applies to an animated SVG: a PNG is one screen"),
        (["--format", "png", "--fps", "30", scenario], "--fps requires --format mp4"),
        (["--format", "svg", "--font-file", "x.ttf", scenario], "--font-file requires --format mp4, png, or gif"),
        (["--format", "svg", "--at", "1", "--max-fps", "10", scenario], "--max-fps has no effect with --at"),
        (["--format", "webm", scenario], "unknown format: webm (expected cast, svg, mp4, png, or gif)"),
        (["svg", "--at", "1", "--no-loop", "samples/basic.cast"], "--no-loop has no effect with --at: a still SVG does not play"),
        (["svg", "--at", "1", "--fade-out", "1", "samples/basic.cast"], "--fade-out has no effect with --at"),
        (["png", "--fps", "30", "samples/basic.cast"], "--fps applies to mp4 and GIF: a PNG is one screen"),
        (["png", "--size", "640x480", "samples/basic.cast"], "--size applies to mp4"),
        (["png", "--end-hold", "1", "samples/basic.cast"], "--end-hold applies to animation: a PNG is one screen, which --at picks"),
        (["png", "--no-loop", "samples/basic.cast"], "--no-loop applies to animation"),
        (["png", "--max-fps", "10", "samples/basic.cast"], "--max-fps applies to an animated SVG"),
        (["png", "--at", "-1", "samples/basic.cast"], "invalid --at value: -1"),
        (["png", "--at", "soon", "samples/basic.cast"], "invalid --at value: soon (expected seconds from the start, such as 2.5, or end)"),
        (["png", "--at", "1", "--at", "2", "samples/basic.cast"], "duplicate option: --at"),
        (["png", "--at", "samples/basic.cast"], "invalid --at value: samples/basic.cast"),
        (["png", scenario], "is a scenario, and png converts a cast file. To record the scenario with a PNG, run: scenetake --format png "),
        (["png", "--background", Path.Combine(work, "missing.png"), "samples/basic.cast"], "background image not found"),
        };

        var ok = true;
        foreach (var (args, expected) in cases)
        {
            var (exit, stderr) = Exec(scenetake, repoRoot, args);
            if (exit != 1 || !stderr.Contains(expected, StringComparison.Ordinal))
            {
                Console.Error.WriteLine($"  {string.Join(' ', args)}: exit {exit}, expected \"{expected}\" in:{Environment.NewLine}{stderr}");
                ok = false;
            }
        }

        static bool IsPng(string path) => File.Exists(path) && File.ReadAllBytes(path).AsSpan(0, 8).SequenceEqual(new byte[] { 0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A });

        static bool Check(bool condition, string message)
        {
            if (!condition)
                Console.Error.WriteLine($"  {message}");
            return condition;
        }

        // The png command, the final screen by default and a time with --at. A font file is taken as for mp4.
        var final = Path.Combine(work, "final.png");
        var (finalExit, finalStderr) = Exec(scenetake, repoRoot, ["png", "--window", "macos", "samples/basic.cast", final]);
        ok &= Check(finalExit == 0 && IsPng(final) && finalStderr.Contains("final screen", StringComparison.Ordinal) && finalStderr.Contains("font: ", StringComparison.Ordinal),
            $"png: exit {finalExit}: {finalStderr}");
        var early = Path.Combine(work, "early.png");
        var (earlyExit, earlyStderr) = Exec(scenetake, repoRoot, ["png", "--at", "1.5", "samples/basic.cast", early]);
        ok &= Check(earlyExit == 0 && IsPng(early) && earlyStderr.Contains("at 1.5s", StringComparison.Ordinal)
            && !File.ReadAllBytes(early).SequenceEqual(File.ReadAllBytes(final)), $"png --at 1.5: exit {earlyExit}: {earlyStderr}");

        // A still SVG has no animation, and says so in its line.
        var stillSvg = Path.Combine(work, "still.svg");
        var (stillExit, stillStderr) = Exec(scenetake, repoRoot, ["svg", "--at", "end", "samples/basic.cast", stillSvg]);
        var still = stillExit == 0 ? File.ReadAllText(stillSvg) : "";
        ok &= Check(stillExit == 0 && !still.Contains("animation", StringComparison.Ordinal) && still.Contains("Done!", StringComparison.Ordinal)
            && stillStderr.Contains("still, final screen", StringComparison.Ordinal), $"svg --at end: exit {stillExit}: {stillStderr}");

        // The scenario path writes the cast and the PNG, and --at picks the screen there too.
        var (runExit, runStderr) = Exec(scenetake, repoRoot, ["--format", "png", "--at", "end", scenario]);
        ok &= Check(runExit == 0 && File.Exists(Path.ChangeExtension(scenario, ".cast")) && IsPng(Path.ChangeExtension(scenario, ".png"))
            && runStderr.Contains("Done: ", StringComparison.Ordinal), $"--format png: exit {runExit}: {runStderr}");

        // A PNG with a background image needs ffmpeg to decode it, and says so before recording anything.
        var image = Path.Combine(work, "wallpaper-still.png");
        File.WriteAllBytes(image, Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg=="));
        var (noFfmpegExit, noFfmpegStderr) = Exec(scenetake, repoRoot, ["png", "--background", image, "samples/basic.cast", Path.Combine(work, "bg.png")],
            new() { ["SCENETAKE_FFMPEG"] = "", ["PATH"] = Path.Combine(work, "empty-path") });
        ok &= Check(noFfmpegExit == 1 && noFfmpegStderr.Contains("a PNG with a background image needs ffmpeg to decode the image", StringComparison.Ordinal),
            $"png --background image without ffmpeg: exit {noFfmpegExit}: {noFfmpegStderr}");
        return ok;
    }

    internal static bool HelpDescribesEveryOption(string scenetake, string repoRoot, string work)
    {
        // --help prints, on stdout, what every option of that command does; usage after an error stays short,
        // on stderr, and points to --help. The option lists here follow the parsers, so an option added there
        // and left out of the help fails this test.
        string[] still = ["--font-size N", "--font-family FAMILIES", "--theme NAME", "--window none|macos|windows",
        "--window-title TEXT", "--background VALUE", "--margin N", "--padding N", "--opacity N", "--blur N",
        "--fit none|height|width|both", "--crop-top N", "--crop-bottom N", "--crop-left N", "--crop-right N"];
        string[] render = [.. still, "--end-hold N", "--fade-out N"];
        string[] svg = ["--max-fps N", "--no-loop", "--loop ", "--at TIME"];
        string[] mp4 = ["--fps N", "--crf N", "--max-size WxH", "--size WxH", "--font-file PATH", "SCENETAKE_FFMPEG"];
        string[] png = ["--at TIME", "--font-file PATH", "SCENETAKE_FFMPEG"];
        string[] gif = ["--fps N", "--max-size WxH", "--size WxH", "--no-loop", "--loop ", "--font-file PATH", "SCENETAKE_FFMPEG"];
        string[] capture = ["--width N", "--height N", "--shell NAME", "--pipe", "--timeout N", "-o, --output NAME", "(svg, the default)"];
        var commands = new (string[] Args, string[] Options)[]
        {
        (["--help"], ["--format cast|svg|mp4|png|gif", "--strict", "--verbose", .. render, .. svg, .. mp4, .. gif, "scenetake svg", "scenetake mp4", "scenetake png", "scenetake gif", "scenetake capture", "scenetake init", "--version"]),
        (["capture", "--help"], [.. capture, "--strict", "--verbose", .. render, .. svg, .. mp4, .. gif]),
        (["-h"], ["--format cast|svg|mp4|png|gif", "--strict", "--verbose"]),
        (["svg", "--help"], [.. render, .. svg]),
        (["mp4", "--help"], [.. render, .. mp4]),
        (["png", "--help"], [.. still, .. png]),
        (["gif", "--help"], [.. render, .. gif]),
        (["init", "--help"], ["--shell bash|pwsh|powershell"]),
        };

        var ok = true;
        foreach (var (args, options) in commands)
        {
            var (exit, stdout, stderr) = ExecCapture(scenetake, repoRoot, args);
            var missing = options.Where(option => !stdout.Contains(option, StringComparison.Ordinal)).ToList();
            var notAscii = stdout.Any(c => c > '~' && c is not ('\r' or '\n'));
            if (exit != 0 || stderr.Length != 0 || missing.Count != 0 || notAscii)
            {
                Console.Error.WriteLine($"  {string.Join(' ', args)}: exit {exit}, stderr '{stderr}', non-ASCII {notAscii}, missing: {string.Join(", ", missing)}");
                ok = false;
            }
        }

        // png rejects the options of an animation, so its help leaves them out.
        var (_, pngHelp, _) = ExecCapture(scenetake, repoRoot, ["png", "--help"]);
        foreach (var option in new[] { "--end-hold", "--fade-out", "--max-fps", "--no-loop", "--fps N" })
        {
            if (pngHelp.Contains(option, StringComparison.Ordinal))
            {
                Console.Error.WriteLine($"  png --help lists {option}, which png rejects");
                ok = false;
            }
        }

        // capture has one command: no step to set continue-on-error on, no pre or post, no interactive step for
        // --save-keys. Its help said all three, from the scenario's --strict text, and a reader looked for them.
        var (_, captureHelp, _) = ExecCapture(scenetake, repoRoot, ["capture", "--help"]);
        foreach (var text in new[] { "continue-on-error", "--save-keys", "post" })
        {
            if (captureHelp.Contains(text, StringComparison.Ordinal))
            {
                Console.Error.WriteLine($"  capture --help mentions {text}, which a capture does not have");
                ok = false;
            }
        }

        // After an error the usage goes to stderr, with the pointer to --help, and stdout stays empty.
        foreach (var (args, pointer) in new (string[] Args, string Pointer)[]
        {
        (["--formt", "svg", "x.yaml"], "Run 'scenetake --help'"),
        (["svg", "--fps", "30", "x.cast"], "Run 'scenetake svg --help'"),
        (["mp4", "--max-fps", "30", "x.cast"], "Run 'scenetake mp4 --help'"),
        (["png", "--fps", "30", "x.cast"], "Run 'scenetake png --help'"),
        (["gif", "--crf", "20", "x.cast"], "Run 'scenetake gif --help'"),
        (["init", "--shell", "zsh"], "Run 'scenetake init --help'"),
        (["capture", "--width", "0", "--", "echo"], "Run 'scenetake capture --help'"),
        })
        {
            var (exit, stdout, stderr) = ExecCapture(scenetake, repoRoot, args);
            if (exit != 1 || stdout.Length != 0 || !stderr.Contains(pointer, StringComparison.Ordinal) || !stderr.Contains("Usage:", StringComparison.Ordinal))
            {
                Console.Error.WriteLine($"  {string.Join(' ', args)}: exit {exit}, stdout '{stdout}', stderr:{Environment.NewLine}{stderr}");
                ok = false;
            }
        }

        // Every theme name that --theme accepts, as its error lists them, is in each command's help.
        var (_, _, themeError) = ExecCapture(scenetake, repoRoot, ["svg", "--theme", "no-such-theme", "samples/basic.cast"]);
        var listed = System.Text.RegularExpressions.Regex.Match(themeError, @"\(expected: ([^)]+)\)");
        var names = listed.Success ? listed.Groups[1].Value.Split(", ") : [];
        if (names.Length < 12)
        {
            Console.Error.WriteLine($"  the theme error lists {names.Length} names: {themeError}");
            ok = false;
        }

        foreach (var command in new[] { new[] { "--help" }, ["svg", "--help"], ["mp4", "--help"], ["png", "--help"], ["gif", "--help"], ["capture", "--help"] })
        {
            var (_, help, _) = ExecCapture(scenetake, repoRoot, command);
            var missing = names.Where(name => !System.Text.RegularExpressions.Regex.IsMatch(help, $@"(?<![\w-]){System.Text.RegularExpressions.Regex.Escape(name)}(?![\w-])")).ToList();
            if (missing.Count != 0)
            {
                Console.Error.WriteLine($"  {string.Join(' ', command)} leaves out the themes {string.Join(", ", missing)}");
                ok = false;
            }
        }

        return ok;
    }

    internal static (int ExitCode, string Stdout, string Stderr) ExecCapture(string file, string workingDirectory, string[] arguments)
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

        using var process = Process.Start(startInfo)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, stdout.Result, stderr);
    }

    // png and gif draw on a --background image. An output with the image's own name would replace it, and a GIF removes
    // its output when its encode starts, so a failed one would lose the image. The default output is plain.png or plain.gif.
    internal static bool SubcommandsNeverReplaceTheirBackground(string scenetake, string repoRoot, string work)
    {
        var directory = Directory.CreateDirectory(Path.Combine(work, "same-background")).FullName;
        File.Copy(Path.Combine(repoRoot, "samples", "basic.cast"), Path.Combine(directory, "plain.cast"), overwrite: true);
        var ok = true;
        foreach (var (command, image) in new[] { ("png", "plain.png"), ("gif", "plain.gif") })
        {
            var path = Path.Combine(directory, image);
            File.WriteAllText(path, "an image the user made");
            var (exit, stderr) = Exec(scenetake, directory, [command, "plain.cast", "--background", image]);
            if (exit != 1 || !stderr.Contains($"Error: the output would write over the background image {image}", StringComparison.Ordinal)
                || File.ReadAllText(path) != "an image the user made")
            {
                Console.Error.WriteLine($"  {command}: exit {exit}{Environment.NewLine}{stderr}");
                ok = false;
            }
        }

        return ok;
    }

    internal static bool SubcommandsNeverReplaceTheirInput(string scenetake, string repoRoot, string work)
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
    internal static bool TryLinkDirectory(string link, string target)
    {
        if (!OperatingSystem.IsWindows())
        {
            Directory.CreateSymbolicLink(link, target);
            return true;
        }

        var (exit, _) = Exec("cmd.exe", Path.GetDirectoryName(link)!, ["/c", "mklink", "/J", link, target]);
        return exit == 0 && Directory.Exists(link);
    }

    internal static bool ScenarioChecksOutputPaths(string scenetake, string repoRoot, string work)
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

    internal static bool ScenarioErrorsAfterPreRunPost(string scenetake, string repoRoot, string work)
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

        // A step that fails while the recording runs: a PTY command that holds a NUL, which the PTY refuses
        // to start. An interactive step whose output is redirected is refused before pre instead
        // (ScenarioErrorsStopBeforePre).
        var failures = new List<(int Exit, string Stderr, bool RanPost)>();
        foreach (var (name, step) in new[]
        {
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

    internal static bool ScenarioErrorsStopBeforePre(string scenetake, string repoRoot, string work)
    {
        // Each scenario must end with an error before anything runs, not with an unhandled exception: YAML the
        // parser rejects in different ways (a broken token, a value of the wrong type, a key given twice in a
        // step), a shell that does not exist, a shell or a cwd with a NUL, a run that is a list, a cwd that does
        // not exist, no steps, a step without run (an unquoted command with ": " is a mapping), a step that is
        // a list, and an interactive step while output is redirected, as it is here. Without their refusal
        // pre would run, so the pre marker shows the order.
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
        ("nocwd", $"cwd: '{cwd}/no-such-folder'\nsteps:\n  - run: echo hi\n", "Error: cwd folder does not exist"),
        ("norelcwd", "cwd: no-such-folder\nsteps:\n  - run: echo hi\n", $"(resolved against the scenario's folder: {Path.Combine(directory, "no-such-folder")})"),
        ("nosteps", "step:\n  - run: echo hi\n", "Error: The scenario has no steps to record"),
        ("emptysteps", "steps:\n  - \"\"\n", "Error: The scenario has no steps to record"),
        ("colon", "steps:\n  - echo hi\n  - echo key: value\n", "Error: Step 2 has no 'run' key (keys: echo key)"),
        // A run whose command holds ": " is a YAML error. The message shows the line, quoted as it should be.
        ("quote", "steps:\n  - run: echo \"answer: $a\"\n", "    - run: 'echo \"answer: $a\"'"),
        ("liststep", "steps:\n  - [echo, hi]\n", "Error: Step 1 must be a command string or a mapping with 'run', not a list"),
        ("interactive", "steps:\n  - run: echo hi\n  - run: echo two\n    pty: true\n    interactive: true\n", "Error: Interactive PTY steps require a real terminal"),
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

    internal static bool RelativeCwdFollowsScenarioFolder(string scenetake, string repoRoot, string work)
    {
        // A relative cwd is resolved against the scenario file's folder, not the folder scenetake is started
        // from: the same scenario runs in the same folder from anywhere. scenetake starts in the repository
        // here, and pre, a pipe step, a PTY step and post must all write their marker in <scenario folder>/inner.
        var directory = Directory.CreateDirectory(Path.Combine(work, "relative-cwd", "scenario")).FullName;
        var inner = Directory.CreateDirectory(Path.Combine(directory, "inner")).FullName;
        var sibling = Directory.CreateDirectory(Path.Combine(work, "relative-cwd", "sibling")).FullName;
        var scenario = Path.Combine(directory, "relative.yaml");
        File.WriteAllText(scenario, """
        width: 40
        height: 6
        cwd: inner
        pre:
          - echo pre > pre.txt
        steps:
          - run: echo step > step.txt
          - run: echo pty > pty.txt
            pty: true
        post:
          - echo post > post.txt
        """);
        var parent = Path.Combine(directory, "parent.yaml");
        File.WriteAllText(parent, "width: 40\nheight: 6\ncwd: ../sibling\nsteps:\n  - run: echo up > up.txt\n");

        var (exit, stderr) = Exec(scenetake, repoRoot, [scenario]);
        var (parentExit, parentStderr) = Exec(scenetake, repoRoot, [parent]);
        var marked = new[] { "pre.txt", "step.txt", "pty.txt", "post.txt" }.All(name => File.Exists(Path.Combine(inner, name)))
            && File.Exists(Path.Combine(sibling, "up.txt"))
            && !File.Exists(Path.Combine(repoRoot, "step.txt")) && !File.Exists(Path.Combine(repoRoot, "up.txt"));
        if (exit != 0 || parentExit != 0 || !marked)
            Console.Error.WriteLine($"  exit {exit}, parent exit {parentExit}{Environment.NewLine}{stderr}{parentStderr}");
        return exit == 0 && parentExit == 0 && marked;
    }

    internal static bool UnknownKeysWarnWithHints(string scenetake, string repoRoot, string work)
    {
        // A key scenetake does not read used to be ignored without a word, so `hightlight` or `ptty` turned
        // the feature off silently. Each is a warning with where it is and what was likely meant, and the
        // recording still runs. The published binary matters here: under Native AOT the YAML map reading
        // needs VYaml's object formatter, and a failure there would drop every warning.
        var directory = Directory.CreateDirectory(Path.Combine(work, "unknown-keys")).FullName;
        var scenario = Path.Combine(directory, "typos.yaml");
        File.WriteAllText(scenario, """
        width: 40
        height: 6
        setting:
          typing-speed: 0.01
        settings:
          typing_speed: 0.01
          pty: true
        render:
          fontsize: 16
          theme:
            preest: dark
        font-size: 16
        steps:
          - &base
            run: echo hi
            hightlight:
              - color: green
                at: "1"
            ptty: true
          - run: echo two
            highlight:
              - colour: green
                at: "1"
          - <<: *base
            run: echo three
            cwd: x
            TypingSpeed: 0.1
        """);
        var (exit, stderr) = Exec(scenetake, repoRoot, [scenario]);
        string[] expected =
        [
            "Warning: unknown key 'setting' at the top level (did you mean 'settings'?)",
        "Warning: unknown key 'font-size' at the top level (it belongs under render)",
        "Warning: unknown key 'typing_speed' in settings (did you mean 'typing-speed'?)",
        "Warning: unknown key 'pty' in settings (it belongs in a step)",
        "Warning: unknown key 'fontsize' in render (did you mean 'font-size'?)",
        "Warning: unknown key 'preest' in render.theme (did you mean 'preset'?)",
        "Warning: unknown key 'hightlight' in step 1 (did you mean 'highlight'?)",
        "Warning: unknown key 'ptty' in step 1 (did you mean 'pty'?)",
        "Warning: unknown key 'colour' in step 2 highlight 1 (did you mean 'color'?)",
        "Warning: unknown key '<<' in step 3 (YAML merge keys are not supported, so nothing is merged)",
        "Warning: unknown key 'cwd' in step 3 (it belongs at the top level)",
        "Warning: unknown key 'TypingSpeed' in step 3 (did you mean 'typing-speed'?)",
    ];
        var missing = expected.Where(line => !stderr.Contains(line, StringComparison.Ordinal)).ToList();
        var warnings = stderr.Split('\n').Count(line => line.Contains("unknown key", StringComparison.Ordinal));
        var ok = exit == 0 && missing.Count == 0 && warnings == expected.Length && File.Exists(Path.Combine(directory, "typos.cast"));
        if (!ok)
            Console.Error.WriteLine($"  exit {exit}, {warnings} warnings, missing:{Environment.NewLine}  {string.Join(Environment.NewLine + "  ", missing)}{Environment.NewLine}{stderr}");
        return ok;
    }

    internal static bool OtherKeySpellingsHaveNoEffect(string scenetake, string repoRoot, string work)
    {
        // VYaml converted typing_speed, typingSpeed and TypingSpeed to typing-speed before matching, so under
        // settings they took effect while the warning said scenetake does not read them. A step key never did.
        // Each spelling would make this short command take minutes to type; ignored, it takes about a second.
        var directory = Directory.CreateDirectory(Path.Combine(work, "key-spellings")).FullName;
        var results = new List<string>();
        foreach (var (section, key) in new[] { ("settings", "typing_speed"), ("settings", "typingSpeed"), ("settings", "TypingSpeed"), ("settings", "post_delay") })
        {
            var scenario = Path.Combine(directory, $"{key}.yaml");
            File.WriteAllText(scenario, $"width: 40\nheight: 6\n{section}:\n  {key}: 30\nsteps:\n  - echo hi\n");
            var (exit, stderr) = Exec(scenetake, repoRoot, [scenario]);
            var castPath = Path.Combine(directory, $"{key}.cast");
            var duration = File.Exists(castPath)
                ? File.ReadLines(castPath).Skip(1).Sum(line => double.Parse(line[1..line.IndexOf(',')], System.Globalization.CultureInfo.InvariantCulture))
                : double.NaN;
            var warned = stderr.Contains($"Warning: unknown key '{key}' in {section} (did you mean ", StringComparison.Ordinal);
            if (exit != 0 || !warned || !(duration < 10))
                results.Add($"{key}: exit {exit}, warned {warned}, duration {duration}{Environment.NewLine}{stderr}");
        }

        if (results.Count > 0)
            Console.Error.WriteLine("  " + string.Join(Environment.NewLine + "  ", results));
        return results.Count == 0;
    }

    internal static bool WrongValueTypeNamesTheKey(string scenetake, string repoRoot, string work)
    {
        // VYaml's own message, "Cannot detect a scalar value as double : Scalar 50ms at Line: 7, Col: 5", named
        // neither the key nor the type, and its line is the one after the value. A step value fell back to the
        // default with no message. Under settings or render the run stops; in a step it is a warning, as an
        // invalid boolean is.
        var directory = Directory.CreateDirectory(Path.Combine(work, "value-types")).FullName;
        (string Body, string Error)[] stops =
        [
            ("settings:\n  typing-speed: 50ms\n", "'settings.typing-speed' must be a number of seconds, such as 0.5, not '50ms'."),
        ("width: 100px\n", "'width' must be a whole number, not '100px'."),
        ("render:\n  font-size: 16.5\n", "'render.font-size' must be a whole number, not '16.5'."),
        ("settings:\n  stream-output: maybe\n", "'settings.stream-output' must be true or false, not 'maybe'."),
        ("settings: fast\n", "'settings' must be a mapping of keys, not 'fast'."),
        ("render:\n  theme: dark\n", "'render.theme' must be a mapping of keys, not 'dark'."),
    ];
        var results = new List<string>();
        for (var i = 0; i < stops.Length; i++)
        {
            var scenario = Path.Combine(directory, $"stop{i}.yaml");
            File.WriteAllText(scenario, $"{stops[i].Body}steps:\n  - echo hi\n");
            var (exit, stderr) = Exec(scenetake, repoRoot, [scenario]);
            if (exit != 1 || !stderr.Contains($"stop{i}.yaml: {stops[i].Error}", StringComparison.Ordinal) || File.Exists(Path.Combine(directory, $"stop{i}.cast")))
                results.Add($"stop{i}: exit {exit}{Environment.NewLine}{stderr}");
        }

        var stepScenario = Path.Combine(directory, "step.yaml");
        File.WriteAllText(stepScenario, "width: 40\nheight: 6\nsteps:\n  - run: echo hi\n    typing-speed: 50ms\n    max-duration: soon\n");
        var (stepExit, stepStderr) = Exec(scenetake, repoRoot, [stepScenario]);
        if (stepExit != 0
            || !stepStderr.Contains("Warning: typing-speed: '50ms' is not a number of seconds (such as 0.5), using 0.05", StringComparison.Ordinal)
            || !stepStderr.Contains("Warning: max-duration: 'soon' is not a number of seconds (such as 0.5), ignoring it", StringComparison.Ordinal))
            results.Add($"step: exit {stepExit}{Environment.NewLine}{stepStderr}");

        if (results.Count > 0)
            Console.Error.WriteLine("  " + string.Join(Environment.NewLine + "  ", results));
        return results.Count == 0;
    }

    internal static bool StrictStopsAtFirstFailure(string scenetake, string repoRoot, string work)
    {
        // A failing step is demo content by default: a warning, and the recording goes on to exit 0. With
        // --strict the first failing step ends the recording, as an error does: later steps do not run (they
        // may depend on it, and they run real commands), nothing is written, so the cast of an earlier run
        // stays, and post runs. With no cast to look at, the end of the step's output is in the error, as
        // plain text. A step with continue-on-error is neither a warning nor a stop. A PTY step's output is
        // shown too. Other subcommands have no steps and refuse the option.
        var directory = Directory.CreateDirectory(Path.Combine(work, "strict")).FullName;
        var cwd = directory.Replace('\\', '/');
        var failing = Path.Combine(directory, "failing.yaml");
        File.WriteAllText(failing, $"""
        width: 40
        height: 6
        shell: bash
        cwd: '{cwd}'
        steps:
          - echo ok
          - run: exit 4
            continue-on-error: true
          - run: |-
              printf '\033[31mcompiling\033[0m\n50%%\r100%%\n'; echo 'build failed here' >&2; exit 3
          - echo after > after-ran.txt
        post:
          - echo ran > post-ran.txt
        """);
        var pty = Path.Combine(directory, "pty.yaml");
        File.WriteAllText(pty, $"width: 40\nheight: 6\nshell: bash\ncwd: '{cwd}'\nsteps:\n  - run: echo pty-boom; exit 5\n    pty: true\n");
        var afterRan = Path.Combine(directory, "after-ran.txt");
        var postRan = Path.Combine(directory, "post-ran.txt");

        var (plainExit, plainStderr) = Exec(scenetake, repoRoot, [failing, Path.Combine(directory, "plain")]);
        var plainRanAll = File.Exists(afterRan) && File.Exists(Path.Combine(directory, "plain.cast"));

        File.Delete(afterRan);
        File.Delete(postRan);
        var earlierCast = Path.Combine(directory, "strict.cast");
        File.WriteAllText(earlierCast, "cast of an earlier run");
        var (strictExit, strictStderr) = Exec(scenetake, repoRoot, ["--strict", failing, Path.Combine(directory, "strict")]);
        var strictStopped = !File.Exists(afterRan) && File.Exists(postRan) && File.ReadAllText(earlierCast) == "cast of an earlier run";

        var (ptyExit, ptyStderr) = Exec(scenetake, repoRoot, ["--strict", pty, Path.Combine(directory, "pty")]);
        var (svgExit, svgStderr) = Exec(scenetake, repoRoot, ["svg", "--strict", "samples/basic.cast", Path.Combine(directory, "basic.svg")]);

        var ok = plainExit == 0 && plainRanAll
            && plainStderr.Contains("Warning: step exited 3: printf", StringComparison.Ordinal)
            && !plainStderr.Contains("exited 4", StringComparison.Ordinal)
            && strictExit == 1 && strictStopped
            && strictStderr.Contains("Error: --strict stopped at step 3, which exited 3: printf", StringComparison.Ordinal)
            && strictStderr.Contains("  | compiling", StringComparison.Ordinal)
            && strictStderr.Contains("  | 100%", StringComparison.Ordinal)
            && !strictStderr.Contains("  | 50%", StringComparison.Ordinal)
            && strictStderr.Contains("  | build failed here", StringComparison.Ordinal)
            && strictStderr.Contains("Nothing was written. Set 'continue-on-error: true' on a step that is meant to fail.", StringComparison.Ordinal)
            && !strictStderr.Contains('\u001b')
            && !strictStderr.Contains("Warning: step exited", StringComparison.Ordinal)
            && ptyExit == 1
            && ptyStderr.Contains("Error: --strict stopped at step 1, which exited 5: echo pty-boom; exit 5", StringComparison.Ordinal)
            && ptyStderr.Contains("  | pty-boom", StringComparison.Ordinal)
            && svgExit == 1 && svgStderr.Contains("unknown option: --strict", StringComparison.Ordinal);
        if (!ok)
            Console.Error.WriteLine($"  plain {plainExit} (ran all: {plainRanAll}), strict {strictExit} (stopped: {strictStopped}), pty {ptyExit}, svg {svgExit}{Environment.NewLine}{plainStderr}{strictStderr}{ptyStderr}{svgStderr}");
        return ok;
    }

    internal static bool KnownKeysDoNotWarn(string scenetake, string repoRoot, string work)
    {
        // No key scenetake reads may be reported: every key in spec_scenario.md, the samples, the test
        // fixtures, and what init writes for each shell. --format mp4 with an ffmpeg that does not exist
        // stops each run before anything executes, after the key check has printed.
        var directory = Directory.CreateDirectory(Path.Combine(work, "known-keys")).FullName;
        var everyKey = Path.Combine(directory, "every-key.yaml");
        File.WriteAllText(everyKey, """
        title: "every key"
        width: 40
        height: 6
        cwd: .
        shell: bash
        settings:
          prompt: "$ "
          typing-speed: 0.01
          typing-jitter: 0.01
          pre-delay: 0.1
          post-delay: 0.1
          execution-duration: 0.1
          stderr-color: red
          stream-output: false
          max-duration: 10
          silence-threshold: 2
          max-idle: 0.2
        render:
          font-size: 16
          font-family: monospace
          window: macos
          theme:
            preset: dark
            fg: "#ffffff"
            bg: "#000000"
            palette: "#000:#111:#222:#333:#444:#555:#666:#777:#888:#999:#aaa:#bbb:#ccc:#ddd:#eee:#fff"
        pre:
          - echo pre
        steps:
          - echo string form
          - name: "[cyan]map form"
            run: echo map form
            typing-speed: 0.01
            typing-jitter: 0.01
            pre-delay: 0.1
            post-delay: 0.1
            execution-duration: 0.1
            stream-output: true
            max-duration: 5
            silence-threshold: 1
            max-idle: 0.1
            run-highlight: bold
            highlight:
              - color: green
                at: "1"
            stderr-color: yellow
            continue-on-error: true
          - run: echo tty
            pty: true
            interactive: true
            normalize-typing: true
        post:
          - echo post
        """);

        var files = new List<string> { everyKey };
        files.AddRange(Directory.EnumerateFiles(Path.Combine(repoRoot, "samples"), "*.yaml"));
        files.AddRange(Directory.EnumerateFiles(Path.Combine(repoRoot, "tests", "fixtures"), "*.yaml"));
        foreach (var shell in new[] { "", "bash", "pwsh", "powershell" })
        {
            var init = Path.Combine(directory, $"init-{(shell.Length == 0 ? "default" : shell)}.yaml");
            File.Delete(init);
            Exec(scenetake, repoRoot, shell.Length == 0 ? ["init", init] : ["init", "--shell", shell, init]);
            files.Add(init);
        }

        var missingFfmpeg = new Dictionary<string, string> { ["SCENETAKE_FFMPEG"] = Path.Combine(directory, "no-such-ffmpeg") };
        var clean = true;
        foreach (var file in files)
        {
            if (!File.Exists(file))
            {
                Console.Error.WriteLine($"  missing {file}");
                clean = false;
                continue;
            }

            var (_, stderr) = Exec(scenetake, repoRoot, ["--format", "mp4", file, Path.Combine(directory, "out")], missingFfmpeg);
            if (stderr.Contains("unknown key", StringComparison.Ordinal) || stderr.Contains("has no 'run' key", StringComparison.Ordinal))
            {
                Console.Error.WriteLine($"  {Path.GetFileName(file)}:{Environment.NewLine}{stderr}");
                clean = false;
            }
        }

        return clean;
    }

    internal static bool UnixPowerShellRunsCommands(string scenetake, string repoRoot, string work)
    {
        var pwsh = TestEnv.FindOnPath("pwsh") ?? throw new TUnit.Core.Exceptions.SkipTestException("pwsh is not on PATH");
        var directory = Directory.CreateDirectory(Path.Combine(work, "unix pwsh")).FullName;
        File.CreateSymbolicLink(Path.Combine(directory, "powershell"), pwsh);
        var passed = true;
        foreach (var (name, shell, fromEnvironment) in new[]
        {
        ("name", "pwsh", false),
        ("absolute", pwsh, false),
        ("relative", Path.GetRelativePath(repoRoot, pwsh), false),
        ("environment-name", "pwsh", true),
        ("environment-path", pwsh, true),
        ("powershell-path", Path.Combine(directory, "powershell"), false),
    })
        {
            var scenario = Path.Combine(directory, name + ".yaml");
            // Pipe and PTY starts can resolve a relative executable from different working directories.
            // Use the same directory for this case so it tests PowerShell's arguments alone.
            var cwd = name == "relative" ? repoRoot : directory;
            var shellKey = fromEnvironment ? "" : $"shell: '{shell.Replace("'", "''", StringComparison.Ordinal)}'\n";
            File.WriteAllText(scenario, shellKey + $$"""
            cwd: '{{cwd.Replace("'", "''", StringComparison.Ordinal)}}'
            settings:
              typing-speed: 0
              typing-jitter: 0
              pre-delay: 0
              post-delay: 0
            pre:
              - Set-Content '{{directory.Replace("'", "''", StringComparison.Ordinal)}}/{{name}}-pre.txt' 'PRE_OK'
            steps:
              - run: "[Console]::WriteLine([string]::Concat('PIPE_', 'OK'))"
              - run: "[Console]::WriteLine([string]::Concat('PTY_', 'OK'))"
                pty: true
            post:
              - Set-Content '{{directory.Replace("'", "''", StringComparison.Ordinal)}}/{{name}}-post.txt' 'POST_OK'
            """);
            var (exit, stderr) = Exec(scenetake, repoRoot, [scenario, Path.Combine(directory, name)],
                fromEnvironment ? new() { ["SHELL"] = shell } : null);
            var cast = exit == 0 ? File.ReadAllText(Path.Combine(directory, name + ".cast")) : "";
            if (exit != 0 || stderr.Contains("Warning:", StringComparison.Ordinal)
                || !cast.Contains("PIPE_OK", StringComparison.Ordinal) || !cast.Contains("PTY_OK", StringComparison.Ordinal)
                || !File.Exists(Path.Combine(directory, name + "-pre.txt")) || !File.Exists(Path.Combine(directory, name + "-post.txt")))
            {
                Console.Error.WriteLine($"  {name}: exit {exit}{Environment.NewLine}{stderr}{cast}");
                passed = false;
            }
        }
        return passed;
    }

    internal static bool UnixShellStartsAsNamed(string scenetake, string repoRoot, string work)
    {
        // A shell shows the name it was started by as $0 and in its error messages, and the recording keeps
        // both, so a shell given by name must be started by that name in pipe and PTY steps. A dangling link
        // named like the shell earlier on PATH must not stop the run, which a check of its own could do.
        var directory = Directory.CreateDirectory(Path.Combine(work, "unix-shell")).FullName;
        var bash = TestEnv.FindOnPath("bash") ?? throw new TUnit.Core.Exceptions.SkipTestException("bash is not on PATH");
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

    internal static bool WindowsShellPathStartsThatFile(string scenetake, string repoRoot, string work)
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
        if (TestEnv.FindOnPath("pwsh") is { } pwsh)
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

    internal static bool UnixShellNameInCurrentDirectoryIsRefused(string scenetake, string repoRoot, string work)
    {
        if (OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Unix only");
        // .NET starts a bare name from the current directory before PATH, so a repository holding its own "bash"
        // would run it as the shell. Started from that folder, both a "shell: bash" scenario and one that falls
        // back to bash (no shell key, SHELL empty) must stop before pre. The same scenario started from another
        // folder runs with the bash on PATH, so the refusal comes from the current directory alone.
        var directory = Directory.CreateDirectory(Path.Combine(work, "cwd-shell-unix")).FullName;
        var binary = Path.GetFullPath(scenetake);
        var planted = Path.Combine(directory, "planted-ran.txt");
        var fake = Path.Combine(directory, "bash");
        File.WriteAllText(fake, $"#!/bin/sh\necho planted > '{planted}'\n");
        File.SetUnixFileMode(fake, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var steps = "pre:\n  - echo ran > pre-ran.txt\nsteps:\n  - run: echo hi\n";
        var named = Path.Combine(directory, "named.yaml");
        File.WriteAllText(named, $"cwd: '{directory}'\nshell: bash\n{steps}");
        var fallback = Path.Combine(directory, "fallback.yaml");
        File.WriteAllText(fallback, $"cwd: '{directory}'\n{steps}");
        var preMarker = Path.Combine(directory, "pre-ran.txt");

        var passed = true;
        foreach (var (name, scenario, environment) in new[] { ("named", named, (Dictionary<string, string>?)null), ("fallback", fallback, new() { ["SHELL"] = "" }) })
        {
            var (exit, stderr) = Exec(binary, directory, [scenario, Path.Combine(directory, name)], environment);
            if (exit != 1 || !stderr.Contains("the current directory has a file named 'bash'", StringComparison.Ordinal)
                || stderr.Contains("Unhandled", StringComparison.Ordinal) || File.Exists(planted) || File.Exists(preMarker))
            {
                Console.Error.WriteLine($"  {name}: exit {exit}{Environment.NewLine}{stderr}");
                passed = false;
            }
        }

        var (elsewhereExit, elsewhereStderr) = Exec(binary, repoRoot, [named, Path.Combine(directory, "elsewhere")]);
        if (elsewhereExit != 0 || File.Exists(planted) || !File.Exists(preMarker))
        {
            Console.Error.WriteLine($"  elsewhere: exit {elsewhereExit}{Environment.NewLine}{elsewhereStderr}");
            passed = false;
        }

        return passed;
    }

    internal static bool WindowsShellNameSkipsCurrentDirectory(string scenetake, string work)
    {
        // A repository could hold its own pwsh.exe or cmd.exe. scenetake used to check the current directory
        // before PATH, and the extensionless name made it pick these files. They are not programs, so starting
        // one fails; the shell from PATH writes the pre marker. The default shell (pwsh, then powershell) and
        // "shell: cmd" are both checked.
        var directory = Directory.CreateDirectory(Path.Combine(work, "cwd-shell-windows")).FullName;
        var binary = Path.GetFullPath(scenetake);
        foreach (var name in new[] { "pwsh", "pwsh.exe", "powershell", "powershell.exe", "cmd", "cmd.exe" })
            File.WriteAllText(Path.Combine(directory, name), "not a program");
        var cwd = directory.Replace('\\', '/');

        var passed = true;
        foreach (var (name, shellKey) in new[] { ("default", ""), ("cmd", "shell: cmd\n") })
        {
            var scenario = Path.Combine(directory, $"{name}.yaml");
            File.WriteAllText(scenario, $"cwd: '{cwd}'\n{shellKey}pre:\n  - echo ran > {name}-pre-ran.txt\nsteps:\n  - run: echo hi\n");
            var (exit, stderr) = Exec(binary, directory, [scenario, Path.Combine(directory, name)]);
            var marker = Path.Combine(directory, $"{name}-pre-ran.txt");
            if (exit != 0 || stderr.Contains("Unhandled", StringComparison.Ordinal) || !File.Exists(marker)
                || !File.ReadAllText(marker).Contains("ran", StringComparison.Ordinal))
            {
                Console.Error.WriteLine($"  {name}: exit {exit}{Environment.NewLine}{stderr}");
                passed = false;
            }
        }

        return passed;
    }

    internal static bool SvgFailureWithAnOpenOutputIsReported(string scenetake, string repoRoot, string work)
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

    internal static bool ScenarioChecksFrameSizeBeforePre(string scenetake, string repoRoot, string work)
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

    internal static bool SizeOptionRules(string scenetake, string repoRoot, string work)
    {
        // --size is an mp4 option, takes an even size, and cannot be combined with --max-size in either order.
        var scenario = WriteScenario(work, "size-rules.yaml");
        var output = Path.Combine(work, "size-rules.mp4");
        var (svgExit, svgStderr) = Exec(scenetake, repoRoot, ["--format", "svg", "--size", "1920x1080", scenario]);
        var (oddExit, oddStderr) = Exec(scenetake, repoRoot, ["mp4", "--size", "1921x1080", "samples/basic.cast", output]);
        var (rangeExit, rangeStderr) = Exec(scenetake, repoRoot, ["mp4", "--size", "1920x16386", "samples/basic.cast", output]);
        var (bothExit, bothStderr) = Exec(scenetake, repoRoot, ["mp4", "--size", "1920x1080", "--max-size", "1280x1024", "samples/basic.cast", output]);
        var (reverseExit, reverseStderr) = Exec(scenetake, repoRoot, ["--format", "mp4", "--max-size=1280x1024", "--size=1920x1080", scenario]);
        var (duplicateExit, duplicateStderr) = Exec(scenetake, repoRoot, ["mp4", "--size", "1920x1080", "--size=1280x720", "samples/basic.cast", output]);
        var (missingExit, missingStderr) = Exec(scenetake, repoRoot, ["mp4", "--size", "--fps", "30", "samples/basic.cast", output]);
        var (subcommandExit, subcommandStderr) = Exec(scenetake, repoRoot, ["svg", "--size", "1920x1080", "samples/basic.cast"]);
        return svgExit == 1 && svgStderr.Contains("--size requires --format mp4", StringComparison.Ordinal)
            && oddExit == 1 && oddStderr.Contains("--size must have an even width and height, which yuv420p requires: 1921x1080", StringComparison.Ordinal)
            && rangeExit == 1 && rangeStderr.Contains("--size must be WIDTHxHEIGHT with each side between 16 and 16384", StringComparison.Ordinal)
            && bothExit == 1 && bothStderr.Contains("--size and --max-size cannot be used together", StringComparison.Ordinal)
            && reverseExit == 1 && reverseStderr.Contains("--size and --max-size cannot be used together", StringComparison.Ordinal)
            && duplicateExit == 1 && duplicateStderr.Contains("duplicate option: --size", StringComparison.Ordinal)
            && missingExit == 1 && missingStderr.Contains("--size requires a value", StringComparison.Ordinal)
            && subcommandExit == 1 && subcommandStderr.Contains("unknown option: --size", StringComparison.Ordinal)
            && !File.Exists(output) && !File.Exists(Path.ChangeExtension(scenario, ".cast"));
    }

    internal static bool ScenarioChecksSizeFrameBeforePre(string scenetake, string repoRoot, string work)
    {
        // 512 columns at font size 50 are 15904 pixels wide. Padding that to the shape of 16×16384 before
        // scaling is far over ffmpeg's raw frame limit, so the run stops before pre or any recording.
        var directory = Directory.CreateDirectory(Path.Combine(work, "size-frame")).FullName;
        var fake = WriteFakeFfmpeg(directory, "ffmpeg-ok", hasLibx264: true);
        var scenario = WriteScenarioWithHooks(directory, "s.yaml", width: 512);
        var (exit, stderr) = Exec(scenetake, repoRoot, ["--format", "mp4", "--font-size", "50", "--size", "16x16384", scenario], new() { ["SCENETAKE_FFMPEG"] = fake });
        return exit == 1 && stderr.Contains("too large for ffmpeg: --size 16x16384 pads the 15904x", StringComparison.Ordinal)
            && !File.Exists(Path.Combine(directory, "s.cast")) && !File.Exists(Path.Combine(directory, "pre-ran.txt"));
    }

    internal static bool EarlierMp4GoesWhenEncodingStarts(string scenetake, string repoRoot, string work)
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

    internal static bool FontInstalledByPreOrStepIsUsed(string scenetake, string repoRoot, string work)
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
            throw new TUnit.Core.Exceptions.SkipTestException("no TrueType font with outlines and a Windows family name under /usr/share/fonts or /usr/local/share/fonts");

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
    internal static (byte[] Data, string Family)? TryRenameFontFamily(byte[] data)
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
    internal static string WriteFfmpegThatChecksFile(string work, string name, string watched, string log)
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
    internal static string WriteScenarioWithHooks(string directory, string name, int width = 40, string extraPre = "echo also ran")
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

}
