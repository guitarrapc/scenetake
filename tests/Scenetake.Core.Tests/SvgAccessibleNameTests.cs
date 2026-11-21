using System.Text.RegularExpressions;

namespace Scenetake.Core.Tests;

// The SVG's accessible name (.github/docs/spec_svg.md → Accessible name): what a screen reader says for the image. It is
// the title bar's text, which is the scenario's title unless window-title gives another, then the title when the window
// title is "", and "Terminal recording" without either.

public sealed class SvgAccessibleNameTests
{
    [Test]
    public async Task NamesTheRecording() => await Assert.That(SvgAccessibleNameCases.NamesTheRecording()).IsTrue();
}

internal static partial class SvgAccessibleNameCases
{
    private const string Hello = "[0.1,\"o\",\"hello\\r\\n\"]\n";

    internal static bool NamesTheRecording()
    {
        var cases = new (string What, string Header, string Events, bool Still, string Expected)[]
        {
            ("the title", "\"title\":\"My Demo\"", Hello, false, "My Demo"),
            ("the window title, which the bar shows", "\"title\":\"My Demo\",\"tags\":[\"st:window-title=demo.yaml\"]", Hello, false, "demo.yaml"),
            ("the title, when the window title is empty", "\"title\":\"My Demo\",\"tags\":[\"st:window-title=\"]", Hello, false, "My Demo"),
            ("a recording with an empty title", "\"title\":\"\"", Hello, false, "Terminal recording"),
            ("a recording without a title", "\"env\":{}", Hello, false, "Terminal recording"),
            ("characters that XML escapes", "\"title\":\"a \\\"b\\\" & <c>\"", Hello, false, "a &quot;b&quot; &amp; &lt;c&gt;"),
            ("a still SVG", "\"title\":\"My Demo\"", Hello, true, "My Demo"),
            ("a recording with no output", "\"title\":\"My Demo\"", "", false, "My Demo"),
        };

        var ok = true;
        foreach (var (what, header, events, still, expected) in cases)
        {
            var svg = Render(header, events, still);
            var root = SvgRoot().Match(svg);
            var label = root.Success ? root.Groups["label"].Value : null;
            if (label != expected)
            {
                Console.Error.WriteLine($"  {what}: aria-label {(label is null ? "missing" : $"\"{label}\"")}, expected \"{expected}\"{Environment.NewLine}  {svg.Split('\n').FirstOrDefault(static line => line.StartsWith("<svg", StringComparison.Ordinal))}");
                ok = false;
            }
        }

        return ok;
    }

    private static string Render(string header, string events, bool still)
    {
        var path = Path.Combine(Path.GetTempPath(), $"scenetake-svg-name-{Guid.NewGuid():N}.cast");
        try
        {
            File.WriteAllText(path, "{\"version\":3,\"term\":{\"cols\":40,\"rows\":6}," + header + "}\n" + events);
            var recording = CastReader.Read(path);
            return SvgRender.RenderToString(recording.Events, recording.Width, recording.Height, recording.RenderSettings, recording.LoopDuration,
                still ? double.PositiveInfinity : null);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [GeneratedRegex("<svg [^>]*role=\"img\" aria-label=\"(?<label>[^\"]*)\"")]
    private static partial Regex SvgRoot();
}
