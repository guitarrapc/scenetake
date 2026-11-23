using System.Globalization;
using System.Text.RegularExpressions;
using static Scenetake.Core.ScenarioCast;
using static Scenetake.Core.Tests.RerecordCases;

namespace Scenetake.Core.Tests;

// The time per step (.github/docs/spec_scenario.md → Time per step): after the cast's Written line, the CLI prints each
// shown step's time in the cast, split into typing, output, and pauses, then the final screen and the whole cast. The
// warning of a length the recording cannot keep names the steps that take longest at their shortest, split as the table
// splits the cast. The recordings come from the composer and the cast writer, as the CLI makes them.

public sealed partial class StepTimesTests
{
    [Test]
    public async Task EachShownStepGetsItsTime() => await Assert.That(StepTimesCases.EachShownStepGetsItsTime()).IsTrue();

    [Test]
    public async Task FinalScreenWithTheClosingPrompt() => await Assert.That(StepTimesCases.FinalScreenWithTheClosingPrompt()).IsTrue();

    [Test]
    public async Task PartsAddUpToEachLine() => await Assert.That(StepTimesCases.PartsAddUpToEachLine()).IsTrue();

    [Test]
    public async Task LengthWarningNamesTheLongestSteps() => await Assert.That(StepTimesCases.LengthWarningNamesTheLongestSteps()).IsTrue();

    [Test]
    public async Task NoStepIsLongerAtItsShortest() => await Assert.That(StepTimesCases.NoStepIsLongerAtItsShortest()).IsTrue();
}

internal static partial class StepTimesCases
{
    // A card, a hidden step, a named step, a clear step, a step that shows its output alone, with a secret in its run
    // text, a background step, and a last step.
    private const string Mixed = """
        settings:
          typing-speed: 0.1
          typing-jitter: 0
          pre-delay: 0.5
          post-delay: 2
          execution-duration: 0.3
          mask:
            - s3cr3t
        steps:
          - card: ["[bold]Title", "second line"]
            post-delay: 1.5
          - run: setup
            show: none
          - name: "[green] First"
            run: echo one
          - clear: true
          - run: echo s3cr3t
            show: output
          - run: serve
            background: true
          - echo two
        """;

    private static CommandExecution Run(StepStart step) => Pipe("out\n");

    internal static bool EachShownStepGetsItsTime()
    {
        var (events, times) = Compose(Mixed, Run);
        var castEnd = events[^1].Time;
        var numbers = string.Join(",", times.Select(static time => time.Number));
        var labels = string.Join("|", times.Select(static time => time.Label));
        var last = times[^1];
        var spans = 0.0;
        for (var i = 0; i < times.Count; i++)
            spans += (i + 1 < times.Count ? times[i + 1].Start : times[i].End) - times[i].Start;

        return Expect(numbers == "1,3,4,5,7", $"the shown steps: {numbers}")
            && Expect(labels == "card: Title|First|clear|echo ******|echo two", $"the labels: {labels}")
            // The card holds the opening moment, its pre-delay, and its post-delay, which is how long it shows.
            && Expect(times[0].Start == 0 && Near(times[0].End, 0.5 + 0.5 + 1.5), $"the card: {times[0].Start} to {times[0].End}")
            && Expect(times[0].Typing == 0 && times[0].Output == 0 && times[2].Typing == 0 && times[2].Output == 0, "a card or a clear step types and prints nothing")
            // Eight characters at 0.1 s each, and Enter.
            && Expect(Near(last.Typing, 8 * 0.1 + 0.15), $"the typing: {last.Typing}")
            && Expect(Near(last.Output, 0.3) && Near(times[1].Output, 0.3), $"the output: {last.Output}, {times[1].Output}")
            && Expect(times[3].Typing == 0, $"show: output types nothing: {times[3].Typing}")
            // The step after the first runs from the end of the first's post-delay.
            && Expect(Near(times[2].Start, times[1].End + 2), $"the post-delay is the step's own: {times[1].End} then {times[2].Start}")
            // The final screen: the last post-delay and pre-delay, after the last output.
            && Expect(Near(castEnd - last.End, 2 + 0.5), $"the final screen: {castEnd - last.End}")
            && Expect(Near(spans + castEnd - last.End, castEnd), $"the steps and the final screen make the cast: {spans} and {castEnd - last.End} of {castEnd}")
            && Expect(Lines(times, castEnd), "the table");
    }

    internal static bool FinalScreenWithTheClosingPrompt()
    {
        // end-prompt: the closing prompt shows after the last post-delay, and the final screen holds it for half of it
        // and the pre-delay.
        var (events, times) = Compose("settings:\n  end-prompt: true\n  pre-delay: 0.5\n  post-delay: 2\nsteps:\n  - echo one\n", Run);
        var table = StepTimes.Format(times, events[^1].Time);
        return Expect(Near(events[^1].Time - times[^1].End, 2 + 1 + 0.5), $"the final screen: {events[^1].Time - times[^1].End}")
            && Expect(table.Count == 4 && table[2].EndsWith("3.5s  final screen", StringComparison.Ordinal), string.Join(Environment.NewLine, table));
    }

    internal static bool PartsAddUpToEachLine()
    {
        // Times that round badly one by one: the parts of each line still add up to it, and the last line to the cast.
        var random = new Random(7);
        var ok = true;
        for (var run = 0; run < 300 && ok; run++)
        {
            var times = new List<StepTime>();
            var t = 0.0;
            var count = random.Next(1, 14);
            for (var i = 0; i < count; i++)
            {
                var typing = Math.Round(random.NextDouble(), 3);
                var output = Math.Round(random.NextDouble() * 3, 3);
                var pauses = Math.Round(random.NextDouble() * 2, 3) + 0.05;
                var start = t;
                t += typing + output + pauses;
                times.Add(new StepTime(i + 1, $"step {i + 1}", i == 0 ? 0 : start) { Typing = typing, Output = output, End = t - pauses * random.Next(0, 2) });
            }

            ok &= Expect(Lines(times, t + Math.Round(random.NextDouble() * 2, 3)), $"run {run}");
        }

        return ok;
    }

    internal static bool LengthWarningNamesTheLongestSteps()
    {
        // A progress bar that redraws for 3 s never shrinks, so its step is the longest at the shortest, and the
        // steps that print a line at once come after it.
        const string yaml = """
            settings:
              typing-speed: 0.05
              typing-jitter: 0
            steps:
              - echo one
              - name: Build it
                run: build
                pty: true
              - echo three
            """;
        var bar = new List<(double, string)>();
        for (var i = 1; i <= 30; i++)
            bar.Add((i * 0.1, $"\r[{new string('#', i)}{new string(' ', 30 - i)}]"));
        bar.Add((3.1, "\r\n"));
        CommandExecution Bar(StepStart step) => step.UsePty ? Pty([.. bar]) : Pipe("out\n");
        var (cast, times) = Write(yaml, Bar);
        var recording = CastReader.ReadText(cast);
        var settings = recording.RenderSettings with { Length = 2 };
        var named = RenderSettingsResolver.LengthNote(recording.Events, settings, recording.LoopDuration, StepTimes.Names(times)) ?? "";
        var alone = RenderSettingsResolver.LengthNote(recording.Events, settings, recording.LoopDuration) ?? "";

        // One step: nothing to compare it with.
        var (oneCast, oneTimes) = Write("steps:\n  - name: Build it\n    run: build\n    pty: true\n", Bar);
        var one = CastReader.ReadText(oneCast);
        var single = RenderSettingsResolver.LengthNote(one.Events, one.RenderSettings with { Length = 1 }, one.LoopDuration, StepTimes.Names(oneTimes)) ?? "";
        // The bar's 3.1 s stay, with its typing and the moment to read it.
        return Expect(LongestPattern().IsMatch(named) && Takes(named, "At their shortest, step 2 (Build it) takes ") > 3.1, named)
            && Expect(Takes(alone, "At their shortest, the step that types \"build\" takes ") == Takes(named, "step 2 (Build it) takes ")
                && alone.Contains("the step that types \"echo one\"", StringComparison.Ordinal), alone)
            && Expect(single.Contains("is shorter than this recording can play", StringComparison.Ordinal) && !single.Contains("At their shortest", StringComparison.Ordinal), single);
    }

    internal static bool NoStepIsLongerAtItsShortest()
    {
        // Each step at its shortest against its time in the table: split the same way, it can only be shorter. A gap
        // across two steps, such as a card's pre-delay after the post-delay before it, is shared by its seconds.
        var (cast, times) = Write(Mixed, Run);
        var recording = CastReader.ReadText(cast);
        var settings = recording.RenderSettings with { Length = 1 };
        var fitted = RenderSettingsResolver.FitLength(recording.Events, settings, recording.LoopDuration)!;
        var shortest = PlaybackLength.StepsAtShortest(settings.StepRanges!, fitted, StepTimes.Names(times));
        var ok = Expect(shortest.Length == times.Count, $"{shortest.Length} steps of {times.Count}");
        for (var i = 0; i < Math.Min(shortest.Length, times.Count); i++)
        {
            var span = (i + 1 < times.Count ? times[i + 1].Start : times[i].End) - times[i].Start;
            ok &= Expect(shortest[i].Seconds <= span + 1e-6 && shortest[i].Seconds > 0, $"{shortest[i].Label}: {shortest[i].Seconds} at the shortest, {span} as recorded");
        }

        return ok;
    }

    [GeneratedRegex(@"takes \d+\.\ds, step \d \([^)]*\) \d+\.\ds, and step \d \([^)]*\) \d+\.\ds\.")]
    private static partial Regex LongestPattern();

    [GeneratedRegex(@"^    +(\d+)  +(\d+\.\d)s = +(\d+\.\d) \+ +(\d+\.\d) \+ +(\d+\.\d)  (.+)$")]
    private static partial Regex RowPattern();

    [GeneratedRegex(@"^  In all: typing (\d+\.\d)s, output (\d+\.\d)s, pauses (\d+\.\d)s(?:, final screen (\d+\.\d)s)?$")]
    private static partial Regex AllPattern();

    /// <summary>The table's lines: a row for each step whose parts add up to it, and the last line, which adds up to the cast.</summary>
    private static bool Lines(List<StepTime> times, double castEnd)
    {
        var table = StepTimes.Format(times, castEnd);
        var text = string.Join(Environment.NewLine, table);
        var ok = Expect(table.Count >= times.Count + 2 && table[0] == "  Time per step: typing + output + pauses", text);
        var rows = 0;
        foreach (var line in table)
        {
            if (RowPattern().Match(line) is { Success: true } row)
            {
                rows++;
                ok &= Expect(Tenths(row.Groups[2].Value) == Tenths(row.Groups[3].Value) + Tenths(row.Groups[4].Value) + Tenths(row.Groups[5].Value), line);
            }
        }

        var all = AllPattern().Match(table[^1]);
        var final = all.Groups[4].Success ? Tenths(all.Groups[4].Value) : 0;
        var cast = Tenths(castEnd.ToString("F1", CultureInfo.InvariantCulture));
        return ok
            && Expect(rows == times.Count, $"{rows} rows{Environment.NewLine}{text}")
            && Expect(all.Success && Tenths(all.Groups[1].Value) + Tenths(all.Groups[2].Value) + Tenths(all.Groups[3].Value) + final == cast, $"the cast is {cast} tenths{Environment.NewLine}{text}");
    }

    /// <summary>The seconds after <paramref name="prefix"/> in <paramref name="text"/>, or -1.</summary>
    private static double Takes(string text, string prefix)
    {
        var at = text.IndexOf(prefix, StringComparison.Ordinal);
        if (at < 0)
            return -1;
        var number = text[(at + prefix.Length)..];
        return double.Parse(number[..number.IndexOf('s', StringComparison.Ordinal)], CultureInfo.InvariantCulture);
    }

    private static int Tenths(string seconds) => (int)Math.Round(double.Parse(seconds, CultureInfo.InvariantCulture) * 10);

    private static (List<CastEvent> Events, List<StepTime> Times) Compose(string yaml, Func<StepStart, CommandExecution> run)
    {
        var scenario = ParseScenario(yaml);
        var times = new List<StepTime>();
        var options = new GenerateOptions { LogRunning = false, Masker = SecretMasker.ForScenario(scenario, environment: null), Times = times };
        var (events, _) = GenerateAsync(scenario, options, step => Task.FromResult(run(step))).GetAwaiter().GetResult();
        return (events, times);
    }

    /// <summary>The cast as the CLI writes it, with its st:steps tag, and the time per step.</summary>
    private static (string Cast, List<StepTime> Times) Write(string yaml, Func<StepStart, CommandExecution> run)
    {
        var scenario = ParseScenario(yaml);
        var times = new List<StepTime>();
        var masker = SecretMasker.ForScenario(scenario, environment: null);
        var rerecorder = new Rerecorder(null, masker);
        var options = new GenerateOptions { LogRunning = false, Masker = masker, Rerecorder = rerecorder, Times = times };
        var (events, _) = GenerateAsync(scenario, options, step => Task.FromResult(run(step))).GetAwaiter().GetResult();
        var map = new int[events.Count + 1];
        events = CastOscFilter.RemovePrivateSequences(events, map);
        using var writer = new StringWriter();
        WriteCast(writer, scenario, events, "bash", 1700000000, RenderSettingsResolver.Resolve(scenario), Rerecording.MapIndices(rerecorder.Slots, map));
        return (writer.ToString(), times);
    }

    private static bool Near(double a, double b) => Math.Abs(a - b) < 0.002;
}
