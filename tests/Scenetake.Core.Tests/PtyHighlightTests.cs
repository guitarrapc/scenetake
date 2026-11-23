using System.Globalization;
using System.Text;
using static Scenetake.Core.ScenarioCast;
using static Scenetake.Core.Tests.RerecordCases;

namespace Scenetake.Core.Tests;

// highlight on a pty: true step (.github/docs/spec_highlight.md → PTY output): lines end at a line feed, a carriage
// return alone stays in its line, the program's own colors neither end the highlight nor are lost after it, and the
// parts of the output join to the same text however the reads fell, so recording again finds the step the same.

public sealed class PtyHighlightTests
{
    [Test]
    public async Task LineKeepsTheProgramsLineBreaks() => await Assert.That(PtyHighlightCases.LineKeepsTheProgramsLineBreaks()).IsTrue();

    [Test]
    public async Task CarriageReturnAloneStaysInItsLine() => await Assert.That(PtyHighlightCases.CarriageReturnAloneStaysInItsLine()).IsTrue();

    [Test]
    public async Task ProgramColorsDoNotEndTheHighlight() => await Assert.That(PtyHighlightCases.ProgramColorsDoNotEndTheHighlight()).IsTrue();

    [Test]
    public async Task RangeEndBringsBackTheProgramsColors() => await Assert.That(PtyHighlightCases.RangeEndBringsBackTheProgramsColors()).IsTrue();

    [Test]
    public async Task StyleStaysOutOfEscapeSequences() => await Assert.That(PtyHighlightCases.StyleStaysOutOfEscapeSequences()).IsTrue();

    [Test]
    public async Task PartsJoinTheSameHoweverTheReadsFell() => await Assert.That(PtyHighlightCases.PartsJoinTheSameHoweverTheReadsFell()).IsTrue();

    [Test]
    public async Task RecordingAgainFindsTheStepTheSame() => await Assert.That(PtyHighlightCases.RecordingAgainFindsTheStepTheSame()).IsTrue();

    [Test]
    public async Task ReplayColorsAsTheScenarioSaysNow() => await Assert.That(PtyHighlightCases.ReplayColorsAsTheScenarioSaysNow()).IsTrue();

    [Test]
    public async Task KeysWithNothingToColorWarn() => await Assert.That(PtyHighlightCases.KeysWithNothingToColorWarn()).IsTrue();
}

internal static class PtyHighlightCases
{
    private const string Yellow = "\u001b[1;33m";
    private const string Reset = "\u001b[0m";

    private static string Paint(string color, string at, params string[] reads) =>
        string.Concat(ApplyPtyHighlights(reads, [new HighlightSpec(color, [at])], "cmd"));

    internal static bool LineKeepsTheProgramsLineBreaks() =>
        Expect(Paint(Yellow, "2", "line1\r\nline2\r\nline3\r\n") == $"line1\r\n{Yellow}line2{Reset}\r\nline3\r\n", "CRLF line")
        // A bare line feed ends a line too, and the last line, without one, is closed at the end.
        & Expect(Paint(Yellow, "2", "a\nb") == $"a\n{Yellow}b{Reset}", "LF line and last line")
        & Expect(Paint(Yellow, "1:2-3", "abcd\r\n") == $"a{Yellow}bc{Reset}d\r\n", "columns");

    internal static bool CarriageReturnAloneStaysInItsLine() =>
        // A progress bar redraws its line: the whole of it is line 1, and the next line is line 2.
        Expect(Paint(Yellow, "2", "10%\r99%\r\ndone\r\n") == $"10%\r99%\r\n{Yellow}done{Reset}\r\n", "progress line")
        & Expect(Paint(Yellow, "1", "10%\r99%\r\n") == $"{Yellow}10%\r99%{Reset}\r\n", "painted progress line");

    internal static bool ProgramColorsDoNotEndTheHighlight()
    {
        // bg:blue on a line where the program writes red and resets: the blue opens again after each of its sequences.
        const string blue = "\u001b[44m";
        var painted = Paint(blue, "1", "plain \u001b[31mred\u001b[0m tail\r\n");
        return Expect(painted == $"{blue}plain \u001b[31m{blue}red\u001b[0m{blue} tail{Reset}\r\n", $"got {Escape(painted)}");
    }

    internal static bool RangeEndBringsBackTheProgramsColors()
    {
        // The program's yellow is open when the underline ends: it comes back after the reset, for the rest of the line.
        const string underline = "\u001b[4m";
        var painted = Paint(underline, "1:6-8", "\u001b[33mabc123 rest\u001b[m\r\n");
        // Columns count the sequences as written: a b at 6-7, ESC [ 0 ; 3 2 m at 8-14, and c at 15.
        var reset = Paint(underline, "1:6-15", "\u001b[33mab\u001b[0;32mc123\u001b[m\r\n");
        return Expect(painted == $"\u001b[33m{underline}abc{Reset}\u001b[33m123 rest\u001b[m\r\n", $"got {Escape(painted)}")
            // 0;32 resets and sets green: green comes back, not yellow.
            & Expect(reset == $"\u001b[33m{underline}ab\u001b[0;32m{underline}c{Reset}\u001b[0;32m123\u001b[m\r\n", $"reset and set: {Escape(reset)}")
            & ManyProgramColorsComeBackAsTheLastFew();
    }

    // lolcat colors every character without a reset. Where the highlight ends, at the end of the line, only the last 8
    // of the program's 101 sequences are written again, and the last of them is the color that shows.
    private static bool ManyProgramColorsComeBackAsTheLastFew()
    {
        var line = new StringBuilder("\u001b[1m");
        for (var k = 0; k < 100; k++)
            line.Append(CultureInfo.InvariantCulture, $"\u001b[38;5;{k}mx");
        var painted = Paint("\u001b[4m", "1", line + "\r\n", "next\r\n");
        var end = painted.IndexOf(Reset, StringComparison.Ordinal);
        var restored = painted[(end + Reset.Length)..painted.IndexOf('\r', end)];
        var expected = string.Concat(Enumerable.Range(92, 8).Select(static k => $"\u001b[38;5;{k}m"));
        return Expect(restored == expected, $"restored after the range: {Escape(restored)}");
    }

    internal static bool StyleStaysOutOfEscapeSequences()
    {
        // Column 3 is inside the window title: the style waits for the first character after the BEL.
        var painted = Paint(Yellow, "1:3-", "\u001b]0;title\u0007text\r\n");
        return Expect(painted == $"\u001b]0;title\u0007{Yellow}text{Reset}\r\n", $"got {Escape(painted)}");
    }

    internal static bool PartsJoinTheSameHoweverTheReadsFell()
    {
        // Cut at every place, the parts join to the text painted in one read, and each part keeps its own characters.
        const string text = "\u001b[33mabc\u001b[m one\r\n\u001b]0;t\u0007two \u001b[31mred\u001b[0m\r\n10%\r99%\r\n";
        var specs = new List<HighlightSpec> { new("\u001b[4m", ["1:7-", "3"]), new("\u001b[44m", ["2"]) };
        var whole = string.Concat(ApplyPtyHighlights([text], specs, "cmd"));
        var ok = true;
        for (var cut = 0; cut <= text.Length; cut++)
        {
            var parts = ApplyPtyHighlights([text[..cut], "", text[cut..]], specs, "cmd");
            ok &= Expect(string.Concat(parts) == whole, $"cut at {cut}: {Escape(string.Concat(parts))}")
                & Expect(parts[1].Length == 0, $"cut at {cut}: the empty read got {Escape(parts[1])}")
                & Expect(Visible(parts[0]) == Visible(text[..cut]), $"cut at {cut}: the first part has other characters");
        }

        return ok;
    }

    internal static bool RecordingAgainFindsTheStepTheSame()
    {
        const string yaml = """
            steps:
              - run: tool
                pty: true
                highlight:
                  - color: yellow
                    at: "2"
            """;
        const string output = "one\r\n\u001b[32mtwo\u001b[0m\r\nthree\r\n";
        // The second run's reads fall elsewhere, at another time: the step prints the same, so the cast stays as it was.
        var (first, _) = Record(yaml, _ => Pty((0.10, output[..7]), (0.20, output[7..])));
        var (second, result) = Record(yaml, _ => Pty((0.15, output[..12]), (0.31, output[12..])), earlier: first);
        return Expect(first.Contains("\\u001b[33m", StringComparison.Ordinal), "the highlight is in the cast")
            & Expect(result.Same.Count == 1 && result.Changed.Count == 0, $"same {result.Same.Count}, changed {result.Changed.Count}")
            & Expect(second == first, $"the cast changed: {FirstDifference(first, second)}");
    }

    internal static bool ReplayColorsAsTheScenarioSaysNow()
    {
        const string yaml = """
            steps:
              - run: tool
                pty: true
                highlight:
                  - color: yellow
                    at: "1"
            """;
        var recorded = new List<RecordedStep>();
        var scenario = ParseScenario(yaml);
        GenerateAsync(scenario, new GenerateOptions { LogRunning = false, Record = recorded },
            _ => Task.FromResult(Pty((0.1, "alpha\r\n")) with { PtyLeadingInitRemoved = true })).GetAwaiter().GetResult();
        var outputs = new StepOutputs { Scenetake = "test", Os = "linux", Shell = "bash", Width = 80, Height = 24, Steps = recorded };
        // The outputs file keeps the output without the highlight, and a replay with another color paints it.
        var replay = new StepOutputsReplay(outputs);
        var (replayed, _) = GenerateAsync(ParseScenario(yaml.Replace("yellow", "magenta", StringComparison.Ordinal)),
            new GenerateOptions { LogRunning = false, Replay = replay }, replay.RunAsync).GetAwaiter().GetResult();
        var text = string.Concat(replayed.Select(static ev => ev.HasUtf8Output ? Encoding.UTF8.GetString(ev.Utf8Output.Span) : ev.Data));
        var stored = string.Concat(recorded[0].PtyChunks!.Select(static chunk => Encoding.UTF8.GetString(chunk.Data.Span)));
        return Expect(stored == "alpha\r\n", $"outputs file: {Escape(stored)}")
            & Expect(text.Contains("\u001b[35malpha\u001b[0m\r\n", StringComparison.Ordinal), $"replay: {Escape(text)}");
    }

    internal static bool KeysWithNothingToColorWarn()
    {
        const string yaml = """
            steps:
              - run: tool
                pty: true
                stderr-color: yellow
              - run: vim
                pty: true
                interactive: true
                highlight:
                  - color: yellow
                    at: "1"
            """;
        var original = Console.Error;
        using var captured = new StringWriter();
#pragma warning disable TUnit0055
        Console.SetError(captured);
        try
        {
            var (events, _) = GenerateAsync(ParseScenario(yaml), new GenerateOptions { LogRunning = false }, step => Task.FromResult(step.UseInteractive
                ? new CommandExecution(0, true, "", "", [new TimedBytes(TimeSpan.Zero, Encoding.UTF8.GetBytes("edit\r\n"))], [], null)
                : Pty((0.1, "out\r\n")) with { PtyLeadingInitRemoved = true })).GetAwaiter().GetResult();
            var warnings = captured.ToString();
            var text = string.Concat(events.Select(static ev => ev.HasUtf8Output ? Encoding.UTF8.GetString(ev.Utf8Output.Span) : ev.Data));
            return Expect(warnings.Contains("Warning: stderr-color (tool): does nothing with pty: true", StringComparison.Ordinal), $"stderr-color: {warnings}")
                & Expect(warnings.Contains("Warning: highlight (vim): does nothing with interactive: true", StringComparison.Ordinal), $"highlight: {warnings}")
                & Expect(!text.Contains("\u001b[33medit", StringComparison.Ordinal), "the interactive step is not painted");
        }
        finally
        {
            Console.SetError(original);
        }
#pragma warning restore TUnit0055
    }

    // The characters that draw, without escape sequences, which the highlight adds to.
    private static string Visible(string text)
    {
        var inside = EscapeSequences.Inside(text);
        var sb = new StringBuilder();
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != '\u001b' && !inside[i])
                sb.Append(text[i]);
        }

        return sb.ToString();
    }

    private static string Escape(string text) => text.Replace("\u001b", "\\e", StringComparison.Ordinal).Replace("\r", "\\r", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal);
}
