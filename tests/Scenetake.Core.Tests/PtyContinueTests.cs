using System.Text;
using Scenetake.TestSupport;

namespace Scenetake.Core.Tests;

public sealed class PtyContinueTests
{
    [Test]
    public async Task StripClearHomeAndText() => await Assert.That(PtyContinueCases.StripClearHomeAndText()).IsTrue();

    [Test]
    public async Task SplitCsiAcrossChunks() => await Assert.That(PtyContinueCases.SplitCsiAcrossChunks()).IsTrue();

    [Test]
    public async Task KeepClearAfterPhaseEnd() => await Assert.That(PtyContinueCases.KeepClearAfterPhaseEnd()).IsTrue();

    [Test]
    public async Task KeepAlternateScreenAndEndPhase() => await Assert.That(PtyContinueCases.KeepAlternateScreenAndEndPhase()).IsTrue();

    [Test]
    public async Task KeepAlternateScreenExitAndFollowingText() => await Assert.That(PtyContinueCases.KeepAlternateScreenExitAndFollowingText()).IsTrue();

    [Test]
    public async Task StripAlternateScreenExitCleanup() => await Assert.That(PtyContinueCases.StripAlternateScreenExitCleanup()).IsTrue();

    [Test]
    public async Task StripOscAndInitOnly() => await Assert.That(PtyContinueCases.StripOscAndInitOnly()).IsTrue();

    [Test]
    public async Task KeepLineEraseDuringLeadingPhase() => await Assert.That(PtyContinueCases.KeepLineEraseDuringLeadingPhase()).IsTrue();

    [Test]
    public async Task StripPrivateModes() => await Assert.That(PtyContinueCases.StripPrivateModes()).IsTrue();

    [Test]
    public async Task StripLeadingNewlines() => await Assert.That(PtyContinueCases.StripLeadingNewlines()).IsTrue();

    [Test]
    public async Task KeepLargePassthroughChunk() => await Assert.That(PtyContinueCases.KeepLargePassthroughChunk()).IsTrue();

    [Test]
    public async Task StripCopilotStyleAlternateScreenExit() => await Assert.That(PtyContinueCases.StripCopilotStyleAlternateScreenExit()).IsTrue();

    [Test]
    public async Task StripCopilotBannerTeardownAfterAlternateScreenExit() => await Assert.That(PtyContinueCases.StripCopilotBannerTeardownAfterAlternateScreenExit()).IsTrue();

    [Test]
    public async Task FastPathOutputIsBufferIndependent() => await Assert.That(PtyContinueCases.FastPathOutputIsBufferIndependent()).IsTrue();

    [Test]
    public async Task StripLeadingCursorPositionQuery() => await Assert.That(PtyContinueCases.StripLeadingCursorPositionQuery()).IsTrue();
}

internal static class PtyContinueCases
{

    internal static string Filter(params string[] chunks)
    {
        var filter = new PtyLeadingInitFilter();
        var output = new List<byte>();
        foreach (var chunk in chunks)
        {
            var filtered = filter.Process(Encoding.UTF8.GetBytes(chunk));
            output.AddRange(filtered.ToArray());
        }

        output.AddRange(filter.Complete().ToArray());
        return Encoding.UTF8.GetString(output.ToArray());
    }

    internal static bool StripClearHomeAndText()
    {
        var output = Filter("\u001b[?25l\u001b[2J\u001b[m\u001b[Htext");
        return output == "text";
    }

    internal static bool StripLeadingCursorPositionQuery()
    {
        // ConPTY started at the recording's cursor (InheritCursor) opens with a cursor position query, which
        // scenetake answers itself. It is dropped there, like the other startup controls. A query a program
        // sends after its first output stays, as every control in the output does.
        var leading = Filter("\u001b[6n\u001b[?9001h\u001b[?1004h\u001b[m\u001b]0;bash\u0007\u001b[?25htext");
        var later = Filter("text\u001b[6nmore");
        return leading == "text" && later == "text\u001b[6nmore";
    }

    internal static bool SplitCsiAcrossChunks()
    {
        var output = Filter("\u001b[2", "Jtext");
        return output == "text";
    }

    internal static bool KeepClearAfterPhaseEnd()
    {
        var output = Filter("text\u001b[2J");
        return output == "text\u001b[2J";
    }

    internal static bool KeepAlternateScreenAndEndPhase()
    {
        var output = Filter("\u001b[2J\u001b[?1049hmatrix\u001b[2J");
        return output == "\u001b[?1049hmatrix\u001b[2J";
    }

    internal static bool KeepAlternateScreenExitAndFollowingText()
    {
        var output = Filter("\u001b[2J\u001b[H\u001b[?1049hmatrix\u001b[?1049lafter");
        return output == "\u001b[?1049hmatrix\u001b[?1049lafter";
    }

    internal static bool StripAlternateScreenExitCleanup()
    {
        var output = Filter("\u001b[2J", "\u001b[H\u001b[?1049hmatrix\u001b[?1049l", "\u001b[?25l\u001b[H\u001b[K\r\n\u001b[K\r\n\u001b[H\u001b[?25hafter");
        return output == "\u001b[?1049hmatrix\u001b[?1049lafter";
    }

    internal static bool StripOscAndInitOnly()
    {
        var output = Filter("\u001b]0;title\u0007\u001b[?25h\u001b[0m");
        return output == "";
    }

    internal static bool KeepLineEraseDuringLeadingPhase()
    {
        var output = Filter("\u001b[2Ktext");
        return output == "\u001b[2Ktext";
    }

    internal static bool StripPrivateModes()
    {
        var output = Filter("\u001b[?9001h\u001b[?1004l\u001b[?2004htext");
        return output == "text";
    }

    internal static bool StripLeadingNewlines()
    {
        var output = Filter(string.Join("", Enumerable.Repeat("\r\n", 20)), "\u001b[?1049hvim");
        return output == "\u001b[?1049hvim";
    }

    internal static bool KeepLargePassthroughChunk()
    {
        var payload = new string('x', 100_000);
        var output = Filter("start", payload + "\u001b[2Jtail");
        return output == "start" + payload + "\u001b[2Jtail";
    }

    internal static bool StripCopilotStyleAlternateScreenExit()
    {
        var output = Filter(
            "\u001b[?1049hcopilot",
            "\u001b[?1049l\u001b[?12h\u001b[?25l\u001b[H\u001b[K\r\n\u001b[K\r\n\u001b[H\u001b[?25hafter");
        return output == "\u001b[?1049hcopilot\u001b[?1049lafter";
    }

    internal static bool StripCopilotBannerTeardownAfterAlternateScreenExit()
    {
        var output = Filter(
            "\u001b[?1049hcopilot",
            "\u001b[=0;1u\u001b[>4;0m\u001b[?1049l",
            "  \r\n  \u001b[38;2;145;152;161m╭─╮╭─╮   Changes    \u001b[38;2;22;198;12m+0 \u001b[38;2;231;72;86m-0\u001b[m\r\n" +
            "  \u001b[38;2;145;152;161m╰─╯╰─╯   AI Credits \u001b[m7.62 (13s)\r\n" +
            "  \u001b[38;2;145;152;161m█ ▘▝ █   Tokens     ↑ \u001b[m30.3k \u001b[38;2;145;152;161m(30.3k written) • ↓ \u001b[m47\r\n" +
            "   \u001b[38;2;145;152;161m▔▔▔▔    Resume     \u001b[mcopilot --resume=d8f9c819-9dde-4249-a215-bb41236820c5\u001b[8;1H\u001b[?25h",
            "$ ");
        return output == "\u001b[?1049hcopilot\u001b[=0;1u\u001b[>4;0m\u001b[?1049l$ ";
    }

    internal static bool FastPathOutputIsBufferIndependent()
    {
        var filter = new PtyLeadingInitFilter();
        var source = Encoding.UTF8.GetBytes("hello");
        var output = filter.Process(source);

        source[0] = (byte)'X';

        return Encoding.UTF8.GetString(output.Span) == "hello";
    }
}
