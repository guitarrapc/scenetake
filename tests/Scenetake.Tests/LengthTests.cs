using System.Globalization;
using System.Text.RegularExpressions;
using Scenetake.TestSupport;

namespace Scenetake.Tests;

// render.length and --length through a published binary (SCENETAKE_BIN, spec_svg.md → Length): the scenario's key, the
// cast's st:length tag, the svg command's option, an outputs entry, capture, and the errors. The scenarios run in bash.

public sealed class LengthTests
{
    [Test]
    public async Task ScenarioCastAndSvgCommand() => await Assert.That(LengthCases.ScenarioCastAndSvgCommand(TestEnv.Binary)).IsTrue();

    [Test]
    public async Task OutputsEntryAndCapture() => await Assert.That(LengthCases.OutputsEntryAndCapture(TestEnv.Binary)).IsTrue();

    [Test]
    public async Task Errors() => await Assert.That(LengthCases.Errors(TestEnv.Binary)).IsTrue();
}

internal static partial class LengthCases
{
    // Three steps with long pauses: about 15 s as recorded, and about 5 s at the shortest.
    private const string Steps = """
        shell: bash
        settings:
          typing-speed: 0.1
          typing-jitter: 0
          pre-delay: 0.5
          post-delay: 3
        steps:
          - echo one
          - echo two
          - echo three
        """;

    internal static bool ScenarioCastAndSvgCommand(string scenetake) => InFolder(folder =>
    {
        File.WriteAllText(Path.Combine(folder, "demo.yaml"), Steps.Replace("steps:", "render:\n  length: 8\nsteps:", StringComparison.Ordinal));
        var (exit, stderr) = Mp4FfmpegCases.Exec(scenetake, folder, ["--format", "svg", "demo.yaml"]);
        var cast = File.ReadAllText(Path.Combine(folder, "demo.cast"));
        var svg = Loop(Path.Combine(folder, "demo.svg"));

        // The svg command keeps the cast's length, and --length replaces it.
        var (shorterExit, shorterErr) = Mp4FfmpegCases.Exec(scenetake, folder, ["svg", "demo.cast", "shorter", "--length", "6"]);
        var (keptExit, keptErr) = Mp4FfmpegCases.Exec(scenetake, folder, ["svg", "demo.cast", "kept"]);
        var (speedExit, speedErr) = Mp4FfmpegCases.Exec(scenetake, folder, ["svg", "demo.cast", "sped", "--speed", "2"]);
        var natural = Loop(Path.Combine(folder, "sped.svg")) * 2;

        // Too short to keep: it plays at its shortest, with a warning.
        var (tooShortExit, tooShortErr) = Mp4FfmpegCases.Exec(scenetake, folder, ["svg", "demo.cast", "tooshort", "--length", "2"]);
        return Check(exit == 0 && cast.Contains("\"st:length=8\"", StringComparison.Ordinal) && Math.Abs(svg - 8) < 0.1
                && stderr.Contains("8.0s, length 8s", StringComparison.Ordinal), $"scenario: exit {exit}, loop {svg}{Environment.NewLine}{stderr}")
            && Check(shorterExit == 0 && Math.Abs(Loop(Path.Combine(folder, "shorter.svg")) - 6) < 0.1, $"--length 6: exit {shorterExit}{Environment.NewLine}{shorterErr}")
            && Check(keptExit == 0 && Math.Abs(Loop(Path.Combine(folder, "kept.svg")) - 8) < 0.1, $"the cast's length: exit {keptExit}{Environment.NewLine}{keptErr}")
            && Check(speedExit == 0 && natural > 12, $"--speed replaces the cast's length: exit {speedExit}, natural {natural}{Environment.NewLine}{speedErr}")
            && Check(tooShortExit == 0 && tooShortErr.Contains("Warning: length 2s is shorter than this recording can play", StringComparison.Ordinal)
                && Loop(Path.Combine(folder, "tooshort.svg")) > 3, $"--length 2: exit {tooShortExit}{Environment.NewLine}{tooShortErr}");
    });

    internal static bool OutputsEntryAndCapture(string scenetake) => InFolder(folder =>
    {
        // An outputs entry fits its own file: the README's SVG as recorded, a shorter one for X.
        File.WriteAllText(Path.Combine(folder, "demo.yaml"), Steps + "\noutputs:\n  - format: svg\n  - format: svg\n    length: 7\n    file: short\n");
        var (exit, stderr) = Mp4FfmpegCases.Exec(scenetake, folder, ["demo.yaml"]);
        var full = Loop(Path.Combine(folder, "demo.svg"));
        var shortened = Loop(Path.Combine(folder, "short.svg"));

        var (captureExit, captureErr) = Mp4FfmpegCases.Exec(scenetake, folder, ["capture", "--pipe", "--shell", "bash", "--length", "6", "--save-scenario", "saved.yaml", "--", "echo", "hi"]);
        var saved = captureExit == 0 ? File.ReadAllText(Path.Combine(folder, "saved.yaml")) : "";
        return Check(exit == 0 && full > 12 && Math.Abs(shortened - 7) < 0.1, $"outputs: exit {exit}, {full}s and {shortened}s{Environment.NewLine}{stderr}")
            && Check(captureExit == 0 && saved.Contains("render:\n  length: 6\n", StringComparison.Ordinal), $"capture: exit {captureExit}{Environment.NewLine}{captureErr}{saved}");
    });

    internal static bool Errors(string scenetake) => InFolder(folder =>
    {
        File.WriteAllText(Path.Combine(folder, "demo.yaml"), Steps);
        // A cast for the svg command, which takes a still with --at: a still does not play, so a length does nothing.
        // The scenario path stores the length in the cast with --at, as it stores a speed.
        File.WriteAllText(Path.Combine(folder, "one.yaml"), "shell: bash\nsteps:\n  - echo hi\n");
        Mp4FfmpegCases.Exec(scenetake, folder, ["one.yaml"]);
        File.WriteAllText(Path.Combine(folder, "both.yaml"), Steps.Replace("steps:", "render:\n  speed: 2\n  length: 8\nsteps:", StringComparison.Ordinal));
        var cases = new (string[] Args, string Expected)[]
        {
            (["--speed", "2", "--length", "5", "demo.yaml"], "--speed and --length both set how long the output plays"),
            (["--length", "5", "--speed", "2", "demo.yaml"], "--speed and --length both set how long the output plays"),
            (["--length", "0.5", "demo.yaml"], "length must be between 1 and 600 seconds"),
            (["--length", "soon", "demo.yaml"], "invalid length value: soon"),
            (["svg", "one.cast", "still", "--at", "1", "--length", "5"], "--length has no effect with --at"),
            (["both.yaml"], "render.speed and render.length both set how long the output plays: keep one"),
        };

        var ok = true;
        foreach (var (args, expected) in cases)
        {
            var (exit, stderr) = Mp4FfmpegCases.Exec(scenetake, folder, args);
            ok &= Check(exit == 1 && stderr.Contains(expected, StringComparison.Ordinal), $"{string.Join(' ', args)}: exit {exit}{Environment.NewLine}{stderr}");
        }

        return ok;
    });

    /// <summary>The SVG's loop in seconds, from its animation.</summary>
    private static double Loop(string svgPath) =>
        File.Exists(svgPath) && LoopPattern().Match(File.ReadAllText(svgPath)) is { Success: true } match
            ? double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture)
            : double.NaN;

    [GeneratedRegex(@"animation:\s*([\d.]+)s")]
    private static partial Regex LoopPattern();

    private static bool InFolder(Func<string, bool> test)
    {
        var folder = Directory.CreateTempSubdirectory("scenetake-length-").FullName;
        try
        {
            return test(folder);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    private static bool Check(bool passed, string message)
    {
        if (!passed)
            Console.Error.WriteLine($"  {message}");
        return passed;
    }
}
