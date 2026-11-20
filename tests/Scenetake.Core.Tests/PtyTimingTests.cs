using Scenetake.TestSupport;

namespace Scenetake.Core.Tests;

public sealed class PtyTimingTests
{
    [Test]
    public async Task CompressSlowStartup() => await Assert.That(PtyTimingCases.CompressSlowStartup()).IsTrue();

    [Test]
    public async Task KeepFastStartup() => await Assert.That(PtyTimingCases.KeepFastStartup()).IsTrue();

    [Test]
    public async Task PreserveChunkSpacing() => await Assert.That(PtyTimingCases.PreserveChunkSpacing()).IsTrue();

    [Test]
    public async Task NoOffsetWhenFirstChunkMatchesExecutionDuration() => await Assert.That(PtyTimingCases.NoOffsetWhenFirstChunkMatchesExecutionDuration()).IsTrue();

    [Test]
    public async Task IgnoreNegativeExecutionDuration() => await Assert.That(PtyTimingCases.IgnoreNegativeExecutionDuration()).IsTrue();
}

internal static class PtyTimingCases
{

    // Mirrors PtyCastTiming in CastComposer.cs
    internal static double ComputeStartupOffset(double firstEmittedChunkSeconds, double executionDuration) =>
        Math.Max(0.0, firstEmittedChunkSeconds - Math.Max(0.0, executionDuration));

    internal static double AdjustChunkSeconds(double chunkSeconds, double startupOffset) =>
        chunkSeconds - startupOffset;

    internal static bool CompressSlowStartup()
    {
        const double executionDuration = 0.05;
        var offset = ComputeStartupOffset(0.301, executionDuration);
        return Math.Abs(offset - 0.251) < 1e-9
            && Math.Abs(AdjustChunkSeconds(0.301, offset) - executionDuration) < 1e-9;
    }

    internal static bool KeepFastStartup()
    {
        const double executionDuration = 0.05;
        var offset = ComputeStartupOffset(0.01, executionDuration);
        return offset == 0.0
            && Math.Abs(AdjustChunkSeconds(0.01, offset) - 0.01) < 1e-9;
    }

    internal static bool PreserveChunkSpacing()
    {
        const double offset = 0.25;
        var first = AdjustChunkSeconds(0.30, offset);
        var second = AdjustChunkSeconds(0.36, offset);
        return Math.Abs((second - first) - 0.06) < 1e-9;
    }

    internal static bool NoOffsetWhenFirstChunkMatchesExecutionDuration() =>
        ComputeStartupOffset(0.05, 0.05) == 0.0;

    internal static bool IgnoreNegativeExecutionDuration()
    {
        const double firstChunkSeconds = 0.30;
        var offset = ComputeStartupOffset(firstChunkSeconds, -1.0);
        return offset == firstChunkSeconds
            && AdjustChunkSeconds(firstChunkSeconds, offset) >= 0.0;
    }
}
