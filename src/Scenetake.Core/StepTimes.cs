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
}

/// <summary>
/// A scenario's shown steps for messages about its cast: where each begins in the cast, in seconds as recorded, where
/// the last one ends, and each one's name, such as <c>step 3 (Coloring stdout)</c>.
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
    /// The table: one line for each step, with its time split into typing, output, and pauses, then the final screen,
    /// and the whole cast split the same way. Each step's time runs to where the next step begins, so its post-delay
    /// is its own. The last step ends with its output, and the final screen holds the rest of the cast. Empty without
    /// a shown step. Times are in tenths of a second, and the parts of each line add up to its time, as the parts of
    /// the last line add up to the cast's length on its <c>Written:</c> line.
    /// </summary>
    internal static List<string> Format(IReadOnlyList<StepTime> steps, double castEnd)
    {
        var lines = new List<string>();
        if (steps.Count == 0)
            return lines;

        // Four cells for each step: its time, typing, output, and pauses, in tenths.
        var cells = new int[steps.Count * 4];
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

        var final = ToTenths(Math.Max(0, castEnd - steps[^1].End));
        var widths = new int[4];
        for (var i = 0; i < cells.Length; i++)
            widths[i % 4] = Math.Max(widths[i % 4], Seconds(cells[i]).Length);
        widths[0] = Math.Max(widths[0], Seconds(final).Length);
        var numberWidth = steps[^1].Number.ToString(CultureInfo.InvariantCulture).Length;
        lines.Add("  Time per step: typing + output + pauses");
        var line = new StringBuilder();
        for (var i = 0; i < steps.Count; i++)
        {
            line.Clear().Append("    ");
            Pad(line, steps[i].Number.ToString(CultureInfo.InvariantCulture), numberWidth).Append("  ");
            Pad(line, Seconds(cells[i * 4]), widths[0]).Append("s = ");
            Pad(line, Seconds(cells[i * 4 + 1]), widths[1]).Append(" + ");
            Pad(line, Seconds(cells[i * 4 + 2]), widths[2]).Append(" + ");
            Pad(line, Seconds(cells[i * 4 + 3]), widths[3]).Append("  ").Append(steps[i].Label);
            lines.Add(line.ToString());
        }

        if (final > 0)
        {
            Pad(line.Clear().Append(' ', 6 + numberWidth), Seconds(final), widths[0]).Append("s  final screen");
            lines.Add(line.ToString());
        }

        // The whole cast as the Written line gives it, less the final screen as its row gives it.
        var all = new int[3];
        Split(ToTenths(castEnd) - final, allTyping, allOutput, allPauses, all, 0);
        line.Clear().Append("  In all: typing ").Append(Seconds(all[0])).Append("s, output ").Append(Seconds(all[1])).Append("s, pauses ").Append(Seconds(all[2])).Append('s');
        if (final > 0)
            line.Append(", final screen ").Append(Seconds(final)).Append('s');
        lines.Add(line.ToString());
        return lines;
    }
    /// <summary>
    /// The steps as messages about the cast name them, such as <c>step 3 (Coloring stdout)</c>, with where each begins
    /// and where the last ends, as the table splits the cast. Null without a shown step.
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
            labels[i] = $"step {steps[i].Number} ({steps[i].Label})";
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
