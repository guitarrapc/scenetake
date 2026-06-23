#:sdk Microsoft.NET.Sdk
#:property TargetFramework=net10.0
#:property Nullable=enable
#:property ImplicitUsings=enable

var failures = 0;
failures += Run("CompressSlowStartup", CompressSlowStartup);
failures += Run("KeepFastStartup", KeepFastStartup);
failures += Run("PreserveChunkSpacing", PreserveChunkSpacing);
failures += Run("NoOffsetWhenFirstChunkMatchesExecutionDuration", NoOffsetWhenFirstChunkMatchesExecutionDuration);
failures += Run("IgnoreNegativeExecutionDuration", IgnoreNegativeExecutionDuration);

return failures == 0 ? 0 : 1;

static int Run(string name, Func<bool> test)
{
    try
    {
        if (test())
        {
            Console.Error.WriteLine($"ok {name}");
            return 0;
        }

        Console.Error.WriteLine($"FAIL {name}");
        return 1;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"FAIL {name}: {ex.Message}");
        return 1;
    }
}

// Mirrors PtyCastTiming in scenetake.cs
static double ComputeStartupOffset(double firstEmittedChunkSeconds, double executionDuration) =>
    Math.Max(0.0, firstEmittedChunkSeconds - Math.Max(0.0, executionDuration));

static double AdjustChunkSeconds(double chunkSeconds, double startupOffset) =>
    chunkSeconds - startupOffset;

static bool CompressSlowStartup()
{
    const double executionDuration = 0.05;
    var offset = ComputeStartupOffset(0.301, executionDuration);
    return Math.Abs(offset - 0.251) < 1e-9
        && Math.Abs(AdjustChunkSeconds(0.301, offset) - executionDuration) < 1e-9;
}

static bool KeepFastStartup()
{
    const double executionDuration = 0.05;
    var offset = ComputeStartupOffset(0.01, executionDuration);
    return offset == 0.0
        && Math.Abs(AdjustChunkSeconds(0.01, offset) - 0.01) < 1e-9;
}

static bool PreserveChunkSpacing()
{
    const double offset = 0.25;
    var first = AdjustChunkSeconds(0.30, offset);
    var second = AdjustChunkSeconds(0.36, offset);
    return Math.Abs((second - first) - 0.06) < 1e-9;
}

static bool NoOffsetWhenFirstChunkMatchesExecutionDuration() =>
    ComputeStartupOffset(0.05, 0.05) == 0.0;

static bool IgnoreNegativeExecutionDuration()
{
    const double firstChunkSeconds = 0.30;
    var offset = ComputeStartupOffset(firstChunkSeconds, -1.0);
    return offset == firstChunkSeconds
        && AdjustChunkSeconds(firstChunkSeconds, offset) >= 0.0;
}
