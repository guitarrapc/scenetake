using Scenetake.TestSupport;

namespace Scenetake.Core.Tests;

public sealed class CastTimingTests
{
    [Test]
    public async Task CollapseIdleGapLong() => await Assert.That(CastTimingCases.CollapseIdleGapLong()).IsTrue();

    [Test]
    public async Task PreserveShortGap() => await Assert.That(CastTimingCases.PreserveShortGap()).IsTrue();

    [Test]
    public async Task CollapseIdleGapsWithOriginalPreservesEmitSpacing() => await Assert.That(CastTimingCases.CollapseIdleGapsWithOriginalPreservesEmitSpacing()).IsTrue();

    [Test]
    public async Task LinearCompressScales() => await Assert.That(CastTimingCases.LinearCompressScales()).IsTrue();

    [Test]
    public async Task LinearCompressUnderMax() => await Assert.That(CastTimingCases.LinearCompressUnderMax()).IsTrue();

    [Test]
    public async Task AdjustTimelineCombined() => await Assert.That(CastTimingCases.AdjustTimelineCombined()).IsTrue();

    [Test]
    public async Task CoalesceTimedTextMerges() => await Assert.That(CastTimingCases.CoalesceTimedTextMerges()).IsTrue();

    [Test]
    public async Task CoalesceTimedUtf8Merges() => await Assert.That(CastTimingCases.CoalesceTimedUtf8Merges()).IsTrue();

    [Test]
    public async Task MergeBurstGapsCollapses() => await Assert.That(CastTimingCases.MergeBurstGapsCollapses()).IsTrue();

    [Test]
    public async Task HighlightSplitterPreservesSegments() => await Assert.That(CastTimingCases.HighlightSplitterPreservesSegments()).IsTrue();

    [Test]
    public async Task HighlightSplitterKeepsSgrAcrossBoundary() => await Assert.That(CastTimingCases.HighlightSplitterKeepsSgrAcrossBoundary()).IsTrue();

    [Test]
    public async Task HighlightSplitterNeverCutsInsideASequence() => await Assert.That(CastTimingCases.HighlightSplitterNeverCutsInsideASequence()).IsTrue();

    [Test]
    public async Task HighlightSplitterCountsReadsInTheJoinedOutput() => await Assert.That(CastTimingCases.HighlightSplitterCountsReadsInTheJoinedOutput()).IsTrue();

    [Test]
    public async Task HighlightSplitterKeepsTheSequencesAtTheEnd() => await Assert.That(CastTimingCases.HighlightSplitterKeepsTheSequencesAtTheEnd()).IsTrue();

    [Test]
    public async Task HighlightSplitterKeepsColonColors() => await Assert.That(CastTimingCases.HighlightSplitterKeepsColonColors()).IsTrue();

    [Test]
    public async Task HighlightSplitterKeepsExtendedColors() => await Assert.That(CastTimingCases.HighlightSplitterKeepsExtendedColors()).IsTrue();

    [Test]
    public async Task HighlightSplitterCountsACrlfOnce() => await Assert.That(CastTimingCases.HighlightSplitterCountsACrlfOnce()).IsTrue();
}

internal static class CastTimingCases
{

    internal static bool CollapseIdleGapLong()
    {
        var times = new[] { 0.0, 30.0, 30.5 };
        CastTimingPipeline.CollapseIdleGaps(times, silenceThreshold: 2.0, maxIdle: 0.2);
        return Math.Abs(times[1] - 0.2) < 1e-9 && Math.Abs(times[2] - 0.7) < 1e-9;
    }

    internal static bool PreserveShortGap()
    {
        var times = new[] { 0.0, 0.5, 1.0 };
        CastTimingPipeline.CollapseIdleGaps(times, silenceThreshold: 2.0, maxIdle: 0.2);
        return Math.Abs(times[1] - 0.5) < 1e-9 && Math.Abs(times[2] - 1.0) < 1e-9;
    }

    internal static bool CollapseIdleGapsWithOriginalPreservesEmitSpacing()
    {
        var emit = new[] { 0.0, 0.15, 12.0, 12.15 };
        var original = new[] { 0.0, 0.1, 12.0, 12.1 };
        CastTimingPipeline.CollapseIdleGapsWithOriginal(emit, original, silenceThreshold: 2.0, maxIdle: 0.2);
        return Math.Abs(emit[1] - 0.15) < 1e-9
            && Math.Abs(emit[2] - 0.35) < 1e-9
            && Math.Abs(emit[3] - 0.50) < 1e-9;
    }

    internal static bool LinearCompressScales()
    {
        var times = new[] { 0.0, 5.0, 10.0 };
        CastTimingPipeline.LinearCompress(times, maxDuration: 5.0);
        return Math.Abs(times[0]) < 1e-9
            && Math.Abs(times[1] - 2.5) < 1e-9
            && Math.Abs(times[2] - 5.0) < 1e-9;
    }

    internal static bool LinearCompressUnderMax()
    {
        var times = new[] { 0.0, 1.0, 2.0 };
        CastTimingPipeline.LinearCompress(times, maxDuration: 5.0);
        return Math.Abs(times[1] - 1.0) < 1e-9 && Math.Abs(times[2] - 2.0) < 1e-9;
    }

    internal static bool MergeBurstGapsCollapses()
    {
        var times = new[] { 0.0, 0.02, 0.04, 1.0 };
        var original = new[] { 0.0, 0.01, 0.02, 1.0 };
        CastTimingPipeline.MergeBurstGaps(times, original, burstThreshold: 0.05);
        return Math.Abs(times[1]) < 1e-9
            && Math.Abs(times[2]) < 1e-9
            && Math.Abs(times[3] - 1.0) < 1e-9;
    }

    internal static bool AdjustTimelineCombined()
    {
        var times = Enumerable.Range(0, 201).Select(static i => i * 0.5).ToArray();
        var adjusted = CastTimingPipeline.AdjustTimeline(times, new CastTimingSettings(2.0, 0.2, 10.0, StreamOutput: true));
        return Math.Abs(adjusted[^1] - 10.0) < 1e-9;
    }

    internal static bool CoalesceTimedTextMerges()
    {
        var coalesced = CastTimingPipeline.CoalesceTimedText(
        [
            (0.1, "a"),
        (0.1, "b"),
        (0.2, "c"),
    ]);
        return coalesced.Count == 2
            && coalesced[0].Text == "ab"
            && coalesced[1].Text == "c";
    }

    internal static bool CoalesceTimedUtf8Merges()
    {
        var coalesced = CastTimingPipeline.CoalesceTimedUtf8(
        [
            (0.1, (ReadOnlyMemory<byte>)new byte[] { (byte)'a' }),
        (0.1, (ReadOnlyMemory<byte>)new byte[] { (byte)'b' }),
        (0.2, (ReadOnlyMemory<byte>)new byte[] { (byte)'c' }),
    ]);
        return coalesced.Count == 2
            && coalesced[0].Data.Length == 2
            && coalesced[0].Data[0] == (byte)'a'
            && coalesced[0].Data[1] == (byte)'b'
            && coalesced[1].Data[0] == (byte)'c';
    }

    internal static bool HighlightSplitterPreservesSegments()
    {
        var highlighted = "\u001b[31mhello\u001b[0m\nworld";
        var split = HighlightSplitter.SplitByRawSegments(highlighted, ["hello" + "\n", "world"]);
        return split.Count == 2
            && split[0].Contains("hello")
            && split[1].Contains("world");
    }

    internal static bool HighlightSplitterKeepsSgrAcrossBoundary()
    {
        var highlighted = "\u001b[31mhello world\u001b[0m";
        var split = HighlightSplitter.SplitByRawSegments(highlighted, ["hello ", "world"]);
        return split.Count == 2
            && split[0].Contains("hello ", StringComparison.Ordinal)
            && split[0].Contains("\u001b[31m", StringComparison.Ordinal)
            && split[0].Contains("\u001b[0m", StringComparison.Ordinal)
            && split[1].Contains("\u001b[31m", StringComparison.Ordinal)
            && split[1].Contains("world", StringComparison.Ordinal)
            && split[1].Contains("\u001b[0m", StringComparison.Ordinal);
    }

    internal static bool HighlightSplitterNeverCutsInsideASequence()
    {
        // The first read ends inside a title, so it has no visible characters of its own. A cut after its last byte would
        // put the color's reset inside the title, which would end it there and show the rest.
        var highlighted = "\u001b[32m\u001b7\u001b]0;API_KEY=abcdefgh\u0007done\u001b[0m\n";
        var split = HighlightSplitter.SplitByRawSegments(highlighted, ["\u001b7\u001b]0;API_KEY=abc", "defgh\u0007done\n"]);
        return split.Count == 2
            && string.Concat(split).Contains("\u001b]0;API_KEY=abcdefgh\u0007", StringComparison.Ordinal)
            && split[1].Contains("done", StringComparison.Ordinal);
    }

    internal static bool HighlightSplitterCountsReadsInTheJoinedOutput()
    {
        // The title runs from the first read into the second, so the second read's own text before its BEL is not
        // visible. Counted alone it would be, and "c" would go with the second read. A CR that ends a sequence becomes the
        // LF after it in the highlighted text.
        var title = HighlightSplitter.SplitByRawSegments("a\u001b]0;title\u0007bc", ["a\u001b]0;t", "itle\u0007b", "c"]);
        var cr = HighlightSplitter.SplitByRawSegments("a\u001b\nbc", ["a\u001b\r\n", "b", "c"]);
        return title.Count == 3 && title[1].EndsWith('b') && title[2] == "c"
            && cr.Count == 3 && cr[1].EndsWith('b') && cr[2] == "c";
    }

    internal static bool HighlightSplitterKeepsTheSequencesAtTheEnd()
    {
        // A sequence after the last visible character, such as one that shows the cursor again, belongs to the last read.
        var one = HighlightSplitter.SplitByRawSegments("\u001b[32mdone\u001b[0m\n\u001b[?25h", ["done\n\u001b[?25h"]);
        var two = HighlightSplitter.SplitByRawSegments("\u001b[32mdone\u001b[0m\n\u001b[?25h", ["done\n", "\u001b[?25h"]);
        return one[0].EndsWith("\u001b[?25h", StringComparison.Ordinal) && two[1] == "\u001b[?25h";
    }

    internal static bool HighlightSplitterKeepsColonColors()
    {
        // An underline style written with a colon is no reset, so the second read still opens the highlight's green.
        var split = HighlightSplitter.SplitByRawSegments("\u001b[32mab\u001b[4:3mc\u001b[1mdef\u001b[0m", ["ab\u001b[4:3mc\u001b[1md", "ef"]);
        return split.Count == 2 && split[1].StartsWith("\u001b[32m", StringComparison.Ordinal);
    }

    internal static bool HighlightSplitterKeepsExtendedColors()
    {
        // The 0 values of a 256 or RGB color are no reset, `0;32` resets and then sets green, and `>4;2m` is no color at
        // all. The part after each cut opens the color that is active there again.
        static string Second(string highlighted, string first, string second) => HighlightSplitter.SplitByRawSegments(highlighted, [first, second])[1];
        // An underline before each color shows that the color did not reset what came before it.
        return Second("\u001b[4mA\u001b[38;2;255;0;0mERR\u001b[1m: bad\u001b[0m", "\u001b[4mA\u001b[38;2;255;0;0mERR\u001b[1m: b", "ad\u001b[0m").StartsWith("\u001b[4m\u001b[38;2;255;0;0m\u001b[1m", StringComparison.Ordinal)
            && Second("\u001b[4mA\u001b[38;5;0mab\u001b[1mcd\u001b[0m", "\u001b[4mA\u001b[38;5;0mab\u001b[1mc", "d\u001b[0m").StartsWith("\u001b[4m\u001b[38;5;0m\u001b[1m", StringComparison.Ordinal)
            && Second("\u001b[0;32mPASS\u001b[1m ok\u001b[0m", "\u001b[0;32mPASS\u001b[1m o", "k\u001b[0m").StartsWith("\u001b[0;32m\u001b[1m", StringComparison.Ordinal)
            && Second("\u001b[32mab\u001b[>4;2mcd\u001b[0m", "\u001b[32mab\u001b[>4;2mc", "d\u001b[0m").StartsWith("\u001b[32md", StringComparison.Ordinal);
    }

    internal static bool HighlightSplitterCountsACrlfOnce()
    {
        // The highlighting turns CRLF into one LF, so a read that ends with one counts it once.
        var split = HighlightSplitter.SplitByRawSegments("one\ntwo\n", ["one\r\n", "two\n"]);
        return split.Count == 2 && split[0] == "one\n" && split[1] == "two\n";
    }
}
