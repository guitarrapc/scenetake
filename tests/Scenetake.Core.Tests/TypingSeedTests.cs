using System.Globalization;
using static Scenetake.Core.ScenarioCast;

namespace Scenetake.Core.Tests;

// Each step types from a random sequence seeded by the step itself, and starts on a whole millisecond
// (.github/docs/spec_scenario.md → Determinism): an edit to a comment, the look, or one step leaves the cast lines of the
// other steps as they were, so recording again shows only the step that changed.

public sealed class TypingSeedTests
{
    [Test]
    public async Task CommentsAndTheLookLeaveTypingAlone() => await Assert.That(TypingSeedCases.CommentsAndTheLookLeaveTypingAlone()).IsTrue();

    [Test]
    public async Task AnEditRetypesOnlyThatStep() => await Assert.That(TypingSeedCases.AnEditRetypesOnlyThatStep()).IsTrue();

    [Test]
    public async Task OneCommandTwiceTypesDifferently() => await Assert.That(TypingSeedCases.OneCommandTwiceTypesDifferently()).IsTrue();

    [Test]
    public async Task SecretsSeedNoTyping() => await Assert.That(TypingSeedCases.SecretsSeedNoTyping()).IsTrue();
}

internal static class TypingSeedCases
{
    private const string ThreeSteps = """
        settings:
          typing-jitter: 0.03
        steps:
          - name: one
            run: echo one
          - name: two
            run: echo two
          - name: three
            run: echo three
        """;

    internal static bool CommentsAndTheLookLeaveTypingAlone()
    {
        // Comments, the schema line, quoting, key order, the title, the look, outputs, pre and post: none of them is
        // typed. The whole file used to seed the typing, so each changed every typed interval.
        const string edited = """
            # yaml-language-server: $schema=https://raw.githubusercontent.com/guitarrapc/scenetake/v1.5.0/schema/scenario.schema.json
            # A demo of three steps.
            title: "Another title"
            render:
              theme:
                preset: nord
              window: macos
            outputs:
              - format: svg
            pre:
              - echo setup
            steps:
              - run: "echo one"      # quoted now
                name: one
              - name: two
                run: echo two
              - name: three
                run: 'echo three'
            settings:
              typing-jitter: 0.03
            post:
              - echo cleanup
            """;
        var before = string.Join('\n', CastLines(ThreeSteps));
        var after = string.Join('\n', CastLines(edited));
        return Expect(before == after, $"the cast's events changed:{Environment.NewLine}{Diff(before, after)}");
    }

    internal static bool AnEditRetypesOnlyThatStep()
    {
        // A new command in step two types anew, and the cast lines of steps one and three stay as they were. Step
        // three's first line is the pause after step two, which now ends at another time, so it is left out.
        var before = CastSteps(CastLines(ThreeSteps));
        var after = CastSteps(CastLines(ThreeSteps.Replace("run: echo two", "run: echo the second", StringComparison.Ordinal)));
        return Expect(before.Count == 3 && after.Count == 3, $"steps: {before.Count} and {after.Count}")
            && Expect(before[0] == after[0], $"step one changed:{Environment.NewLine}{Diff(before[0], after[0])}")
            && Expect(before[1] != after[1], "step two kept its lines")
            && Expect(WithoutFirstLine(before[2]) == WithoutFirstLine(after[2]), $"step three changed:{Environment.NewLine}{Diff(before[2], after[2])}");
    }

    internal static bool OneCommandTwiceTypesDifferently()
    {
        // The second step that types a command counts it again, so its typing is not a copy of the first one's.
        var steps = StepEvents(Compose("settings:\n  typing-jitter: 0.03\nsteps:\n  - name: a\n    run: echo hi\n  - name: b\n    run: echo hi\n"));
        return Expect(steps.Count == 2, $"steps: {steps.Count}")
            && Expect(Typing(steps[0]) != Typing(steps[1]), $"both typed with the same intervals: {Typing(steps[0])}");
    }

    internal static bool SecretsSeedNoTyping()
    {
        // The seed comes from the run text as the cast shows it, after masking. Two secrets of one length type the
        // same, so the typing cannot confirm a guess of the secret.
        const string template = "settings:\n  typing-jitter: 0.03\n  mask:\n    - {0}\nsteps:\n  - name: log in\n    run: login --password {0}\n";
        var first = string.Join('\n', CastLines(string.Format(CultureInfo.InvariantCulture, template, "hunter22")));
        var second = string.Join('\n', CastLines(string.Format(CultureInfo.InvariantCulture, template, "swordfis")));
        return Expect(!first.Contains("hunter22", StringComparison.Ordinal), "the cast shows the secret")
            && Expect(first == second, $"two secrets typed differently:{Environment.NewLine}{Diff(first, second)}");
    }

    /// <summary>The scenario's events, each step printing one line through a pipe, masked as the CLI masks them.</summary>
    private static List<CastEvent> Compose(string yaml)
    {
        var scenario = ParseScenario(yaml);
        var options = new GenerateOptions { LogRunning = false, Masker = SecretMasker.ForScenario(scenario, null) };
        var (events, _) = GenerateAsync(scenario, options, _ => Task.FromResult(CommandExecution.FromPipe("done\n", "", 0))).GetAwaiter().GetResult();
        return events;
    }

    /// <summary>The cast's event lines, as the CLI writes them, without the header.</summary>
    private static List<string> CastLines(string yaml)
    {
        var scenario = ParseScenario(yaml);
        using var writer = new StringWriter();
        WriteCast(writer, scenario, Compose(yaml), "bash", CastTimestamp, RenderSettingsResolver.Resolve(scenario));
        return [.. writer.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries).Skip(1)];
    }

    /// <summary>Each step's cast lines, from its marker line to the next one.</summary>
    private static List<string> CastSteps(List<string> lines)
    {
        var steps = new List<string>();
        foreach (var line in lines)
        {
            if (line.Contains("\"m\",", StringComparison.Ordinal))
                steps.Add("");
            if (steps.Count > 0)
                steps[^1] += line + "\n";
        }

        return steps;
    }

    private static string WithoutFirstLine(string step) => step[(step.IndexOf('\n', StringComparison.Ordinal) + 1)..];

    /// <summary>Each step's events, from its marker to the next one.</summary>
    private static List<List<CastEvent>> StepEvents(List<CastEvent> events)
    {
        var steps = new List<List<CastEvent>>();
        foreach (var ev in events)
        {
            if (ev.Kind == CastEventKind.Marker)
                steps.Add([]);
            if (steps.Count > 0)
                steps[^1].Add(ev);
        }

        return steps;
    }

    /// <summary>The intervals between a step's typed characters, one event each, which the jitter draws.</summary>
    private static string Typing(List<CastEvent> step)
    {
        var typed = step.Where(static ev => ev.Kind == CastEventKind.Output && !ev.HasUtf8Output && ev.Data.Length == 1).Select(static ev => ev.Time).ToList();
        return string.Join(" ", typed.Zip(typed.Skip(1), static (a, b) => (b - a).ToString("0.000", CultureInfo.InvariantCulture)));
    }

    /// <summary>The first lines where two texts differ.</summary>
    private static string Diff(string before, string after)
    {
        var a = before.Split('\n');
        var b = after.Split('\n');
        var lines = new List<string>();
        for (var i = 0; i < Math.Max(a.Length, b.Length) && lines.Count < 6; i++)
        {
            var left = i < a.Length ? a[i] : "(none)";
            var right = i < b.Length ? b[i] : "(none)";
            if (left != right)
                lines.Add($"  {i}: {left}  |  {right}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static bool Expect(bool condition, string message)
    {
        if (!condition)
            Console.Error.WriteLine($"  {message}");
        return condition;
    }
}
