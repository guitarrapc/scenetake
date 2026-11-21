using System.Diagnostics;
using System.Globalization;
using Scenetake.TestSupport;

namespace Scenetake.Tests;

// render.speed and --speed (.github/docs/spec_svg.md → Playback speed) through a published binary (SCENETAKE_BIN). The
// timeline itself is tested in Scenetake.Core.Tests (SvgRenderTests). The scenarios run in bash. The mp4 and GIF tests
// need ffmpeg on PATH, and skip without it.

public sealed class SpeedTests
{
    [Test]
    public async Task ScenarioCastAndSvgCommand() => await Assert.That(SpeedCases.ScenarioCastAndSvgCommand()).IsTrue();

    [Test]
    public async Task OutputsEntryAndCapture() => await Assert.That(SpeedCases.OutputsEntryAndCapture()).IsTrue();

    [Test]
    public async Task Errors() => await Assert.That(SpeedCases.Errors()).IsTrue();

    [Test]
    public async Task DefaultFpsFollowsSpeed() => await Assert.That(SpeedCases.DefaultFpsFollowsSpeed()).IsTrue();

    [Test]
    [Category("Ffmpeg")]
    public async Task VideoLengths() => await Assert.That(Skipping.Run(SpeedCases.VideoLengths)).IsTrue();

    [Test]
    [Category("Ffmpeg")]
    public async Task DefaultFpsKeepsTheRecordingsFrames() => await Assert.That(Skipping.Run(SpeedCases.DefaultFpsKeepsTheRecordingsFrames)).IsTrue();
}

internal static class SpeedCases
{
    private const string Steps = "shell: bash\nsteps:\n  - echo one\n  - run: sleep 1\n    execution-duration: 1\n  - echo two\n";

    internal static bool Check(bool passed, string what)
    {
        if (!passed)
            Console.Error.WriteLine($"  failed: {what}");
        return passed;
    }

    // The scenario's speed is a tag of the cast, which keeps the recorded times. The cast's line says how long its
    // outputs play, the SVG plays half as long, and the svg command's --speed 1 plays the recording.
    internal static bool ScenarioCastAndSvgCommand()
    {
        var bin = TestEnv.Binary;
        TestEnv.RequireOnPath("bash");
        var folder = NewFolder("scenario");
        File.WriteAllText(Path.Combine(folder, "fast.yaml"), "render:\n  speed: 2\n" + Steps);
        File.WriteAllText(Path.Combine(folder, "plain.yaml"), Steps);

        var (fastExit, fastErr) = Exec(bin, folder, ["--format", "svg", "fast.yaml"]);
        var (plainExit, plainErr) = Exec(bin, folder, ["--format", "svg", "plain.yaml"]);
        var (oneExit, oneErr) = Exec(bin, folder, ["svg", "--speed", "1", "fast.cast", "one"]);
        var header = File.ReadLines(Path.Combine(folder, "fast.cast")).First();
        var fastLoop = LoopSeconds(Path.Combine(folder, "fast.svg"));
        var plainLoop = LoopSeconds(Path.Combine(folder, "plain.svg"));
        var oneLoop = LoopSeconds(Path.Combine(folder, "one.svg"));
        var (recorded, played) = CastLine(fastErr);

        var ok = Check(fastExit == 0 && plainExit == 0 && oneExit == 0, $"exit {fastExit}, {plainExit}, {oneExit}")
            && Check(header.Contains("\"st:speed=2\"", StringComparison.Ordinal), $"tag: {header}")
            && Check(!File.ReadLines(Path.Combine(folder, "plain.cast")).First().Contains("st:speed", StringComparison.Ordinal), "no tag at 1x")
            && Check(Math.Abs(LastEventTime(Path.Combine(folder, "fast.cast")) - LastEventTime(Path.Combine(folder, "plain.cast"))) < 0.5, "the cast keeps the recorded times")
            && Check(Math.Abs(fastLoop * 2 - plainLoop) < 0.3, $"2x loop {fastLoop}s, 1x {plainLoop}s")
            && Check(Math.Abs(oneLoop - fastLoop * 2) < 0.01, $"--speed 1 loop {oneLoop}s")
            && Check(Math.Abs(played * 2 - recorded) < 0.2, $"the cast's line: {recorded}s, {played}s at 2x")
            && Check(fastErr.Contains("Written: fast.svg", StringComparison.Ordinal) && fastErr.Contains("s at 2x)\n", StringComparison.Ordinal), "the SVG's line")
            && Check(!oneErr.Contains(" at ", StringComparison.Ordinal) && !plainErr.Contains(" at ", StringComparison.Ordinal), "1x lines name no speed");
        if (!ok)
            Console.Error.WriteLine(fastErr + plainErr + oneErr);
        return ok;
    }

    // An outputs entry takes speed as the option does, and capture saves --speed as a render key.
    internal static bool OutputsEntryAndCapture()
    {
        var bin = TestEnv.Binary;
        TestEnv.RequireOnPath("bash");
        var folder = NewFolder("outputs");
        File.WriteAllText(Path.Combine(folder, "demo.yaml"), Steps + "outputs:\n  - format: svg\n  - format: svg\n    speed: 1.5\n    file: social.svg\n");
        var (exit, stderr) = Exec(bin, folder, ["demo.yaml"]);
        var normal = LoopSeconds(Path.Combine(folder, "demo.svg"));
        var social = LoopSeconds(Path.Combine(folder, "social.svg"));

        var (captureExit, captureErr) = Exec(bin, folder, ["capture", "--pipe", "--shell", "bash", "--speed", "1.5", "--save-scenario", "saved.yaml", "--", "echo", "hi"]);
        var saved = captureExit == 0 ? File.ReadAllText(Path.Combine(folder, "saved.yaml")) : "";

        var ok = Check(exit == 0, $"exit {exit}")
            && Check(Math.Abs(social * 1.5 - normal) < 0.01, $"entry at 1.5x: {social}s, the other {normal}s")
            && Check(stderr.Contains("social.svg", StringComparison.Ordinal) && stderr.Contains("s at 1.5x)", StringComparison.Ordinal), "the entry's line")
            && Check(captureExit == 0 && saved.Contains("render:\n  speed: 1.5\n", StringComparison.Ordinal), $"capture: {saved}")
            && Check(File.ReadLines(Path.Combine(folder, "saved.cast")).First().Contains("\"st:speed=1.5\"", StringComparison.Ordinal), "capture's cast");
        if (!ok)
            Console.Error.WriteLine(stderr + captureErr);
        return ok;
    }

    internal static bool Errors()
    {
        var bin = TestEnv.Binary;
        TestEnv.RequireOnPath("bash");
        var folder = NewFolder("errors");
        File.WriteAllText(Path.Combine(folder, "range.yaml"), "render:\n  speed: 5\n" + Steps);
        File.WriteAllText(Path.Combine(folder, "entry.yaml"), Steps + "outputs:\n  - format: svg\n    speed: fast\n");
        File.WriteAllText(Path.Combine(folder, "plain.yaml"), Steps);
        var (rangeExit, rangeErr) = Exec(bin, folder, ["range.yaml"]);
        var (entryExit, entryErr) = Exec(bin, folder, ["entry.yaml"]);
        var (_, _) = Exec(bin, folder, ["plain.yaml"]);
        var (optionExit, optionErr) = Exec(bin, folder, ["svg", "--speed", "0.1", "plain.cast"]);
        var (twiceExit, twiceErr) = Exec(bin, folder, ["svg", "--speed", "2", "--speed", "3", "plain.cast"]);
        var (stillExit, stillErr) = Exec(bin, folder, ["svg", "--speed", "2", "--at", "1", "plain.cast"]);
        var (pngExit, pngErr) = Exec(bin, folder, ["png", "--speed", "2", "plain.cast"]);

        return Check(rangeExit == 1 && rangeErr.Contains("invalid render.speed: must be between 0.25 and 4: 5", StringComparison.Ordinal) && !File.Exists(Path.Combine(folder, "range.cast")), $"range: {rangeErr}")
            & Check(entryExit == 1 && entryErr.Contains("invalid speed value: fast", StringComparison.Ordinal) && entryErr.Contains("outputs", StringComparison.Ordinal), $"entry: {entryErr}")
            & Check(optionExit == 1 && optionErr.Contains("speed must be between 0.25 and 4: 0.1", StringComparison.Ordinal), $"option: {optionErr}")
            & Check(twiceExit == 1 && twiceErr.Contains("duplicate option: --speed", StringComparison.Ordinal), $"twice: {twiceErr}")
            & Check(stillExit == 1 && stillErr.Contains("--speed has no effect with --at: a still SVG does not play", StringComparison.Ordinal), $"still: {stillErr}")
            & Check(pngExit == 1 && pngErr.Contains("--speed applies to animation: a PNG is one screen", StringComparison.Ordinal), $"png: {pngErr}");
    }

    // Without --fps the frame rate is the default times the speed, rounded up, up to the format's highest, and never
    // below the default. A given --fps stays.
    internal static bool DefaultFpsFollowsSpeed()
    {
        var ok = true;
        foreach (var (speed, mp4, gif) in new[] { (1.0, 30, 20), (0.5, 30, 20), (1.1, 33, 22), (1.25, 38, 25), (1.5, 45, 30), (2.0, 60, 40), (2.5, 60, 50), (4.0, 60, 50) })
        {
            var options = new Mp4Options();
            ok &= Check(options.ToSettings(speed).Fps == mp4 && options.ToGifSettings(speed).Fps == gif,
                $"{speed}x: mp4 {options.ToSettings(speed).Fps} (want {mp4}), GIF {options.ToGifSettings(speed).Fps} (want {gif})");
        }

        var given = new Mp4Options { Fps = 24 };
        return ok & Check(given.ToSettings(2).Fps == 24 && given.ToGifSettings(2).Fps == 24, "--fps 24 stays at 2x");
    }

    // At 2x with the default frame rate, an mp4 and a GIF have the frames of the recording at the default rate, so the
    // same number of frames, and the GIF shows as many screens as at 1x.
    internal static bool DefaultFpsKeepsTheRecordingsFrames()
    {
        var bin = TestEnv.Binary;
        TestEnv.RequireOnPath("bash", "ffmpeg");
        var folder = NewFolder("default-fps");
        File.WriteAllText(Path.Combine(folder, "plain.yaml"), Steps);
        var (castExit, _) = Exec(bin, folder, ["plain.yaml"]);
        var (oneExit, oneErr) = Exec(bin, folder, ["mp4", "plain.cast", "one"]);
        var (twoExit, twoErr) = Exec(bin, folder, ["mp4", "--speed", "2", "plain.cast", "two"]);
        var (gifOneExit, gifOneErr) = Exec(bin, folder, ["gif", "plain.cast", "one"]);
        var (gifTwoExit, gifTwoErr) = Exec(bin, folder, ["gif", "--speed", "2", "plain.cast", "two"]);

        var ok = Check(castExit == 0 && oneExit == 0 && twoExit == 0 && gifOneExit == 0 && gifTwoExit == 0, "exit codes")
            && Check(oneErr.Contains(", 30fps, ", StringComparison.Ordinal) && twoErr.Contains(", 60fps, ", StringComparison.Ordinal), "mp4: 30 and 60 fps")
            && Check(Math.Abs(Frames(oneErr) - Frames(twoErr)) <= 1, $"mp4 frames: 1x {Frames(oneErr)}, 2x {Frames(twoErr)}")
            && Check(gifOneErr.Contains(", 20fps, ", StringComparison.Ordinal) && gifTwoErr.Contains(", 40fps, ", StringComparison.Ordinal), "GIF: 20 and 40 fps")
            && Check(Math.Abs(Frames(gifOneErr) - Frames(gifTwoErr)) <= 1, $"GIF frames: 1x {Frames(gifOneErr)}, 2x {Frames(gifTwoErr)}");
        if (!ok)
            Console.Error.WriteLine(oneErr + twoErr + gifOneErr + gifTwoErr);
        return ok;
    }

    private static int Frames(string stderr)
    {
        var end = stderr.IndexOf(" frames,", StringComparison.Ordinal);
        if (end < 0)
            return -1;
        var start = stderr.LastIndexOf(' ', end - 1) + 1;
        return int.Parse(stderr[start..end], CultureInfo.InvariantCulture);
    }

    // With a given --fps, an mp4 and a GIF at 2x have half the frames of the recording's length at that rate.
    internal static bool VideoLengths()
    {
        var bin = TestEnv.Binary;
        TestEnv.RequireOnPath("bash", "ffmpeg");
        var folder = NewFolder("video");
        File.WriteAllText(Path.Combine(folder, "plain.yaml"), Steps);
        var (castExit, _) = Exec(bin, folder, ["plain.yaml"]);
        var (mp4Exit, mp4Err) = Exec(bin, folder, ["mp4", "--fps", "10", "plain.cast", "normal"]);
        var (fastExit, fastErr) = Exec(bin, folder, ["mp4", "--fps", "10", "--speed", "2", "plain.cast", "fast"]);
        var (gifExit, gifErr) = Exec(bin, folder, ["gif", "--fps", "10", "--speed", "2", "plain.cast", "fast"]);

        var normal = Frames(mp4Err);
        var fast = Frames(fastErr);
        var gif = Frames(gifErr);
        var ok = Check(castExit == 0 && mp4Exit == 0 && fastExit == 0 && gifExit == 0, $"exit {castExit}, {mp4Exit}, {fastExit}, {gifExit}")
            && Check(Math.Abs(fast * 2 - normal) <= 2, $"mp4 frames: 2x {fast}, 1x {normal}")
            && Check(gif == fast, $"GIF frames {gif}, mp4 {fast}")
            && Check(fastErr.Contains($"{fast} frames, {(fast / 10.0).ToString("F1", CultureInfo.InvariantCulture)}s at 2x,", StringComparison.Ordinal), "the mp4's line");
        if (!ok)
            Console.Error.WriteLine(mp4Err + fastErr + gifErr);
        return ok;
    }

    // The cast's line: `Written: fast.cast  (32 events, 5.7s, 2.8s at 2x)`. NaN when it has no speed.
    private static (double Recorded, double Played) CastLine(string stderr)
    {
        var start = stderr.IndexOf(" events, ", StringComparison.Ordinal);
        var end = stderr.IndexOf("s at ", start < 0 ? 0 : start, StringComparison.Ordinal);
        if (start < 0 || end < 0)
            return (double.NaN, double.NaN);

        var parts = stderr[(start + " events, ".Length)..end].Split("s, ");
        return parts.Length == 2
            ? (double.Parse(parts[0], CultureInfo.InvariantCulture), double.Parse(parts[1], CultureInfo.InvariantCulture))
            : (double.NaN, double.NaN);
    }

    // The SVG's loop: `.l { visibility: hidden; animation: 9.043s step-start infinite; }`.
    private static double LoopSeconds(string svg)
    {
        var text = File.Exists(svg) ? File.ReadAllText(svg) : "";
        const string Marker = ".l { visibility: hidden; animation: ";
        var start = text.IndexOf(Marker, StringComparison.Ordinal);
        if (start < 0)
            return double.NaN;
        start += Marker.Length;
        return double.Parse(text[start..text.IndexOf('s', start)], CultureInfo.InvariantCulture);
    }

    // asciicast v3 events hold the time since the previous one.
    private static double LastEventTime(string cast)
    {
        var total = 0.0;
        foreach (var line in File.ReadLines(cast).Skip(1))
        {
            if (line.StartsWith('['))
                total += double.Parse(line[1..line.IndexOf(',')], CultureInfo.InvariantCulture);
        }

        return total;
    }

    private static string NewFolder(string name)
    {
        var folder = Path.Combine(Path.GetTempPath(), "scenetake-speed-test", name);
        if (Directory.Exists(folder))
            Directory.Delete(folder, recursive: true);
        return Directory.CreateDirectory(folder).FullName;
    }

    private static (int ExitCode, string Stderr) Exec(string bin, string folder, string[] arguments)
    {
        var psi = new ProcessStartInfo(bin) { RedirectStandardError = true, RedirectStandardOutput = true, UseShellExecute = false, WorkingDirectory = folder };
        foreach (var argument in arguments)
            psi.ArgumentList.Add(argument);
        psi.Environment.Remove("SCENETAKE_OUTPUTS");
        using var process = Process.Start(psi)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(TimeSpan.FromSeconds(120)))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"{string.Join(' ', arguments)} ran for over 120 s");
        }

        _ = stdout.Result;
        return (process.ExitCode, stderr.Result.Replace("\r\n", "\n", StringComparison.Ordinal));
    }
}
