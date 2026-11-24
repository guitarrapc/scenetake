using static Scenetake.Core.ScenarioCast;
using static Scenetake.Core.Tests.RerecordCases;

namespace Scenetake.Core.Tests;

// A step's id (.github/docs/spec_scenario.md → Step ids): --at and an outputs entry's at name a step by it, for the
// screen right after its output, or where a card or a clear step shows. The cast keeps each id's time in its st:ids
// tag, so a cast alone resolves it too.

public sealed class StepIdTests
{
    [Test]
    public async Task AtReadsSecondsEndOrAnId() => await Assert.That(StepIdCases.AtReadsSecondsEndOrAnId()).IsTrue();

    [Test]
    public async Task CastKeepsWhereEachIdIs() => await Assert.That(StepIdCases.CastKeepsWhereEachIdIs()).IsTrue();

    [Test]
    public async Task IdsThatAtCannotName() => await Assert.That(StepIdCases.IdsThatAtCannotName()).IsTrue();
}

internal static class StepIdCases
{
    internal static bool AtReadsSecondsEndOrAnId()
    {
        bool Seconds(string text, double expected) => RenderSettingsResolver.TryParseStillAt(text, out var at, out _) && at.Step is null && at.Seconds.Equals(expected);
        bool Id(string text, string expected) => RenderSettingsResolver.TryParseStillAt(text, out var at, out _) && at.Step == expected;
        bool Fails(string text) => !RenderSettingsResolver.TryParseStillAt(text, out _, out var error) && error.Contains("end, or a step's id", StringComparison.Ordinal);
        return Expect(Seconds("2.5", 2.5) && Seconds(" End ", double.PositiveInfinity), "seconds and end")
            && Expect(Id("compare", "compare") && Id(" _build-1 ", "_build-1") && Id("Step2", "Step2"), "ids")
            && Expect(Fails("1st") && Fails("my step") && Fails("-x") && Fails("a.b") && Fails("2s"), "neither")
            && Expect(RenderSettingsResolver.FormatStillAt(4.56, "compare") == "at compare (4.56s)", RenderSettingsResolver.FormatStillAt(4.56, "compare"))
            && Expect(StepIds.Parse("a:1 b") is null && StepIds.Parse("1a:5") is null && StepIds.Parse("a:-5") is null && StepIds.Parse("") is null, "malformed tags")
            && Expect(StepIds.Parse("a:1500 b-2:20") is { } ids && ids.Find("b-2") == 0.02 && ids.Find("a") == 1.5 && ids.Find("c") is null, "a tag");
    }

    internal static bool CastKeepsWhereEachIdIs()
    {
        const string yaml = """
            settings:
              typing-speed: 0.05
              typing-jitter: 0
              post-delay: 1
            steps:
              - card: ["Title"]
                id: title
              - id: compare
                run: compare
              - clear: true
                name: Part two
                id: part-two
              - run: three
            """;
        var scenario = ParseScenario(yaml);
        var times = new List<StepTime>();
        var rerecorder = new Rerecorder(null, null);
        var (events, _) = GenerateAsync(scenario, new GenerateOptions { LogRunning = false, Times = times, Rerecorder = rerecorder },
            step => Task.FromResult(Pipe(step.Command.Cmd == "compare" ? "a\nb\n" : "out\n"))).GetAwaiter().GetResult();
        using var writer = new StringWriter();
        WriteCast(writer, scenario, events, "bash", 1700000000, RenderSettingsResolver.Resolve(scenario), rerecorder.Slots, times);
        var recording = CastReader.ReadText(writer.ToString());
        var ids = recording.RenderSettings.StepIds;
        var read = recording.Events;
        double At(string data) => read.First(ev => ev.Data.Contains(data, StringComparison.Ordinal)).Time;
        bool Resolves(string id, out double seconds) =>
            RenderSettingsResolver.TryResolveStillAt(new StillTime(0, id), recording.RenderSettings, out seconds, out _);

        // The command's output has ended, and the next step's comment has not shown yet. The card shows, and the clear
        // step has drawn its comment.
        return Expect(ids is not null && string.Join(",", ids.Ids) == "title,compare,part-two", $"ids: {string.Join(",", ids?.Ids ?? [])}")
            && Expect(Resolves("compare", out var compare) && compare >= At("a\r\nb\r\n") && compare < At("# Part two"), $"compare at {compare}")
            && Expect(Resolves("title", out var title) && Math.Abs(title - At("\u001b[?25l\u001b[H\u001b[2J")) < 0.0011, $"title at {title}")
            && Expect(Resolves("part-two", out var partTwo) && partTwo >= At("# Part two") && partTwo < read[^1].Time, $"part-two at {partTwo}")
            && Expect(!RenderSettingsResolver.TryResolveStillAt(new StillTime(0, "nope"), recording.RenderSettings, out _, out var missing)
                && missing == "--at nope: no step in the cast has this id (it has title, compare, part-two)", missing)
            && Expect(!RenderSettingsResolver.TryResolveStillAt(new StillTime(0, "x"), recording.RenderSettings with { StepIds = null }, out _, out var none)
                && none.StartsWith("--at x: the cast has no step ids", StringComparison.Ordinal), none);
    }

    internal static bool IdsThatAtCannotName()
    {
        string? Error(string steps) => FindStepKeyError(ParseScenario("steps:\n" + steps));
        var cases = new (string Steps, string Expected)[]
        {
            ("  - run: a\n    id: x\n  - run: b\n    id: x\n", "Steps 1 and 2 have the same id 'x': each id names one step for --at."),
            ("  - run: a\n    id: 1st\n", "Step 1 key 'id' must start with a letter or '_' and hold only letters, digits, '-', and '_'"),
            ("  - run: a\n    id: true\n", "Step 1 key 'id' must start with a letter or '_'"),
            ("  - run: a\n    id: END\n", "Step 1 key 'id' cannot be 'END', which --at reads as the final screen."),
            ("  - run: a\n    show: none\n    id: setup\n  - b\n", "Step 1 key 'id' has no effect with 'show: none'"),
            ("  - run: a\n    background: true\n    id: server\n  - b\n", "Step 1 key 'id' does not apply to a 'background: true' step"),
        };

        var ok = true;
        foreach (var (steps, expected) in cases)
        {
            var error = Error(steps);
            ok &= Expect(error?.Contains(expected, StringComparison.Ordinal) == true, $"{steps.ReplaceLineEndings(" ")}: {error}");
        }

        return ok && Expect(Error("  - run: a\n    id: a\n  - card: hi\n    id: b\n  - clear: true\n    id: c\n") is null, "ids on a command, a card, and a clear step");
    }
}
