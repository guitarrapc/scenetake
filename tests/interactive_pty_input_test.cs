#:sdk Microsoft.NET.Sdk
#:property TargetFramework=net10.0
#:property Nullable=enable
#:property ImplicitUsings=enable
#:package MiniPty.Console@1.0.4-local
#:include ../InteractivePtyInputRecorder.cs

using System.Diagnostics;
using System.Text;

var failures = 0;
failures += Run("OnForwardedInput_RecordsElapsedAndBytes", OnForwardedInput_RecordsElapsedAndBytes);
failures += Run("OnForwardedInput_IgnoresEmptySpan", OnForwardedInput_IgnoresEmptySpan);

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

static bool OnForwardedInput_RecordsElapsedAndBytes()
{
    var stopwatch = Stopwatch.StartNew();
    var chunks = new List<PtyInputChunk>();
    var recorder = new InteractivePtyInputRecorder(stopwatch, chunks);

    recorder.OnForwardedInput(TimeSpan.Zero, "hello"u8);
    Thread.Sleep(5);
    recorder.OnForwardedInput(TimeSpan.FromSeconds(1), "!"u8);

    if (chunks.Count != 2)
        return false;
    if (Encoding.UTF8.GetString(chunks[0].Data.Span) != "hello")
        return false;
    if (Encoding.UTF8.GetString(chunks[1].Data.Span) != "!")
        return false;
    return chunks[1].Time >= chunks[0].Time;
}

static bool OnForwardedInput_IgnoresEmptySpan()
{
    var stopwatch = Stopwatch.StartNew();
    var chunks = new List<PtyInputChunk>();
    var recorder = new InteractivePtyInputRecorder(stopwatch, chunks);

    recorder.OnForwardedInput(TimeSpan.Zero, ReadOnlySpan<byte>.Empty);

    return chunks.Count == 0;
}
