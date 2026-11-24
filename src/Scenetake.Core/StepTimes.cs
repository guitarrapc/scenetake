using System.Globalization;
using System.Text;

namespace Scenetake.Core;

/// <summary>
/// A shown step's time in the cast, as recorded: a card, a clear step, or a command. The CLI prints them as a table
/// after the cast's <c>Written:</c> line (spec_scenario.md → Time per step), so the author sees which step makes the
/// demo long, and whether typing, output, or pauses do.
/// </summary>
internal sealed class StepTime(int number, string label, double start)
{
    /// <summary>The step's 1-based position in <c>steps</c>.</summary>
    internal int Number { get; } = number;

    /// <summary>The step's name, or its run text shortened, or a card's first line, after masking.</summary>
    internal string Label { get; } = label;

    /// <summary>When the step begins: its comment line, prompt, or pre-delay. 0 for the first step, which holds the opening moment.</summary>
    internal double Start { get; } = start;

    /// <summary>From the first typed character to the command's start, Enter included.</summary>
    internal double Typing { get; init; }

    /// <summary>From the command's start to the end of its output.</summary>
    internal double Output { get; init; }

    /// <summary>Where the step ends before its post-delay, or for a card after it: the card's post-delay is the time it shows.</summary>
    internal double End { get; init; }

    /// <summary>The step's <c>id</c>, which <c>--at</c> names, or null.</summary>
    internal string? Id { get; init; }

    /// <summary>
    /// The moment <c>--at</c> with the step's id shows: where a command's output has ended, where a clear step has
    /// cleared the screen, and where a card shows.
    /// </summary>
    internal double At { get; init; }
}

/// <summary><c>--at</c> as written: seconds, infinity for <c>end</c>, or a step's id, which the cast gives a time.</summary>
internal sealed class StillTime(double seconds, string? step)
{
    internal double Seconds { get; } = seconds;
    internal string? Step { get; } = step;
}

/// <summary>
/// The cast's <c>st:ids</c> tag: each step id and its <see cref="StepTime.At"/>, in seconds as recorded. Its value is
/// <c>id:ms</c> pairs, such as <c>compare:12345 deploy:23456</c>, each in whole milliseconds rounded up, so the moment
/// is never before the step's last output event, which the cast rounds to the nearest millisecond.
/// </summary>
internal sealed class StepIds(string[] ids, double[] times)
{
    internal string[] Ids { get; } = ids;
    internal double[] Times { get; } = times;

    internal double? Find(string id)
    {
        var index = Array.IndexOf(Ids, id);
        return index < 0 ? null : Times[index];
    }

    /// <summary>The tag for the steps that have an id, or null when none has one.</summary>
    internal static string? FormatTag(IReadOnlyList<StepTime>? steps)
    {
        StringBuilder? text = null;
        foreach (var step in steps ?? [])
        {
            if (step.Id is null)
                continue;
            text = text is null ? new StringBuilder(RenderSettingsResolver.IdsTagPrefix) : text.Append(' ');
            text.Append(step.Id).Append(':').Append(((long)Math.Ceiling(Math.Round(step.At * 1000, 3))).ToString(CultureInfo.InvariantCulture));
        }

        return text?.ToString();
    }

    /// <summary>The tag's value after its prefix, or null when it is malformed.</summary>
    internal static StepIds? Parse(string value)
    {
        var entries = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var ids = new string[entries.Length];
        var times = new double[entries.Length];
        for (var i = 0; i < entries.Length; i++)
        {
            var colon = entries[i].LastIndexOf(':');
            if (colon <= 0 || !ScenarioCast.IsStepId(entries[i][..colon])
                || !long.TryParse(entries[i].AsSpan(colon + 1), NumberStyles.None, CultureInfo.InvariantCulture, out var ms))
                return null;
            ids[i] = entries[i][..colon];
            times[i] = ms / 1000.0;
        }

        return entries.Length == 0 ? null : new StepIds(ids, times);
    }
}

/// <summary>
/// A scenario's shown steps for messages about its cast: where each begins in the cast, in seconds as recorded, where
/// the last one ends, and each one's name: its number in the table.
/// </summary>
internal sealed class StepNames(double[] starts, double end, string[] labels)
{
    internal double[] Starts { get; } = starts;
    internal double End { get; } = end;
    internal string[] Labels { get; } = labels;
}

/// <summary>The table of <see cref="StepTime"/>s, and the steps named for messages.</summary>
internal static class StepTimes
{
    /// <summary>
    /// The table: a column for each step's time, typing, output, and pauses, a row for each step, then the final
    /// screen, whose time is a pause, and the total. Each step's time runs to where the next step begins, so its
    /// post-delay is its own. The last step ends with its output, and the final screen holds the rest of the cast.
    /// Empty without a shown step. Times are in tenths of a second. The parts of each row add up to its time, and the
    /// total is the cast's length on its <c>Written:</c> line.
    /// </summary>
    internal static List<string> Format(IReadOnlyList<StepTime> steps, double castEnd)
    {
        var lines = new List<string>();
        if (steps.Count == 0)
            return lines;

        // Four cells for each row, the steps, the final screen, and the total: time, typing, output, and pauses, in tenths.
        var rows = steps.Count + 2;
        var cells = new int[rows * 4];
        var allTyping = 0.0;
        var allOutput = 0.0;
        var allPauses = 0.0;
        for (var i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            var span = Math.Max(0, (i + 1 < steps.Count ? steps[i + 1].Start : step.End) - step.Start);
            var typing = Math.Clamp(step.Typing, 0, span);
            var output = Math.Clamp(step.Output, 0, span - typing);
            var pauses = span - typing - output;
            allTyping += typing;
            allOutput += output;
            allPauses += pauses;
            cells[i * 4] = ToTenths(span);
            Split(cells[i * 4], typing, output, pauses, cells, i * 4 + 1);
        }

        var final = Math.Max(0, castEnd - steps[^1].End);
        var finalRow = steps.Count * 4;
        cells[finalRow] = cells[finalRow + 3] = ToTenths(final);
        var totalRow = finalRow + 4;
        cells[totalRow] = ToTenths(castEnd);
        Split(cells[totalRow], allTyping, allOutput, allPauses + final, cells, totalRow + 1);

        // Each column as wide as its heading or its widest number, the time with its unit.
        var widths = new[] { Math.Max(5, steps[^1].Number.ToString(CultureInfo.InvariantCulture).Length), 4, 6, 6, 6 };
        for (var i = 0; i < cells.Length; i++)
            widths[i % 4 + 1] = Math.Max(widths[i % 4 + 1], Seconds(cells[i]).Length + (i % 4 == 0 ? 1 : 0));

        var line = new StringBuilder("  ");
        Pad(line, "step", widths[0]);
        Pad(line.Append("  "), "time", widths[1]);
        Pad(line.Append("  "), "typing", widths[2]);
        Pad(line.Append("  "), "output", widths[3]);
        Pad(line.Append("  "), "pauses", widths[4]);
        lines.Add(line.ToString());
        for (var i = 0; i < steps.Count; i++)
            lines.Add(Row(line, steps[i].Number.ToString(CultureInfo.InvariantCulture), i * 4).Append("  ").Append(steps[i].Label).ToString());
        if (cells[finalRow] > 0)
            lines.Add(Row(line, "", finalRow, blankParts: true).Append("  final screen").ToString());
        lines.Add(Row(line, "total", totalRow).ToString());
        return lines;

        StringBuilder Row(StringBuilder text, string name, int at, bool blankParts = false)
        {
            Pad(text.Clear().Append("  "), name, widths[0]);
            Pad(text.Append("  "), Seconds(cells[at]) + "s", widths[1]);
            for (var c = 1; c < 4; c++)
                Pad(text.Append("  "), blankParts && c < 3 ? "" : Seconds(cells[at + c]), widths[c + 1]);
            return text;
        }
    }
    /// <summary>
    /// The steps as messages about the cast name them, by their number in the table, with where each begins and where
    /// the last ends, as the table splits the cast. Null without a shown step.
    /// </summary>
    internal static StepNames? Names(IReadOnlyList<StepTime> steps)
    {
        if (steps.Count == 0)
            return null;

        var starts = new double[steps.Count];
        var labels = new string[steps.Count];
        for (var i = 0; i < steps.Count; i++)
        {
            starts[i] = steps[i].Start;
            labels[i] = steps[i].Number.ToString(CultureInfo.InvariantCulture);
        }

        return new StepNames(starts, steps[^1].End, labels);
    }

    /// <summary>Seconds in whole tenths, rounded as the <c>Written:</c> lines round them (<c>F1</c>).</summary>
    private static int ToTenths(double seconds) =>
        (int)Math.Round(double.Parse(seconds.ToString("F1", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture) * 10);

    /// <summary>
    /// Typing, output, and pauses in whole tenths that add up to <paramref name="total"/> tenths, into
    /// <paramref name="into"/> from <paramref name="at"/>: each rounded down, then the tenths left over given to the
    /// parts that lost the most, or taken from those that lost the least.
    /// </summary>
    private static void Split(int total, double typing, double output, double pauses, int[] into, int at)
    {
        var left = total;
        for (var i = 0; i < 3; i++)
        {
            into[at + i] = (int)Math.Floor(Part(i) * 10);
            left -= into[at + i];
        }

        for (; left != 0; left -= Math.Sign(left))
        {
            var pick = -1;
            var pickLost = 0.0;
            for (var i = 0; i < 3; i++)
            {
                var lost = Part(i) * 10 - into[at + i];
                if (left < 0 && into[at + i] == 0)
                    continue;
                if (pick < 0 || (left > 0 ? lost > pickLost : lost < pickLost))
                {
                    pick = i;
                    pickLost = lost;
                }
            }

            if (pick < 0)
                break;
            into[at + pick] += Math.Sign(left);
        }

        double Part(int i) => i == 0 ? typing : i == 1 ? output : pauses;
    }

    private static StringBuilder Pad(StringBuilder line, string text, int width) =>
        line.Append(' ', Math.Max(0, width - text.Length)).Append(text);
    private static string Seconds(int tenths) =>
        string.Create(CultureInfo.InvariantCulture, $"{tenths / 10}.{tenths % 10}");
}
