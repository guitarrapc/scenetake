#:sdk Microsoft.NET.Sdk
#:property TargetFramework=net10.0
#:property Nullable=enable
#:property ImplicitUsings=enable
#:include ../PtyLeadingInitFilter.cs

using System.Text;

var failures = 0;
failures += Run("StripClearHomeAndText", StripClearHomeAndText);
failures += Run("SplitCsiAcrossChunks", SplitCsiAcrossChunks);
failures += Run("KeepClearAfterPhaseEnd", KeepClearAfterPhaseEnd);
failures += Run("KeepAlternateScreenAndEndPhase", KeepAlternateScreenAndEndPhase);
failures += Run("KeepAlternateScreenExitAndFollowingText", KeepAlternateScreenExitAndFollowingText);
failures += Run("StripAlternateScreenExitCleanup", StripAlternateScreenExitCleanup);
failures += Run("StripOscAndInitOnly", StripOscAndInitOnly);
failures += Run("KeepLineEraseDuringLeadingPhase", KeepLineEraseDuringLeadingPhase);
failures += Run("StripPrivateModes", StripPrivateModes);
failures += Run("StripLeadingNewlines", StripLeadingNewlines);
failures += Run("KeepLargePassthroughChunk", KeepLargePassthroughChunk);
failures += Run("StripCopilotStyleAlternateScreenExit", StripCopilotStyleAlternateScreenExit);
failures += Run("FastPathOutputIsBufferIndependent", FastPathOutputIsBufferIndependent);

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

static string Filter(params string[] chunks)
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

static bool StripClearHomeAndText()
{
    var output = Filter("\u001b[?25l\u001b[2J\u001b[m\u001b[Htext");
    return output == "text";
}

static bool SplitCsiAcrossChunks()
{
    var output = Filter("\u001b[2", "Jtext");
    return output == "text";
}

static bool KeepClearAfterPhaseEnd()
{
    var output = Filter("text\u001b[2J");
    return output == "text\u001b[2J";
}

static bool KeepAlternateScreenAndEndPhase()
{
    var output = Filter("\u001b[2J\u001b[?1049hmatrix\u001b[2J");
    return output == "\u001b[?1049hmatrix\u001b[2J";
}

static bool KeepAlternateScreenExitAndFollowingText()
{
    var output = Filter("\u001b[2J\u001b[H\u001b[?1049hmatrix\u001b[?1049lafter");
    return output == "\u001b[?1049hmatrix\u001b[?1049lafter";
}

static bool StripAlternateScreenExitCleanup()
{
    var output = Filter("\u001b[2J", "\u001b[H\u001b[?1049hmatrix\u001b[?1049l", "\u001b[?25l\u001b[H\u001b[K\r\n\u001b[K\r\n\u001b[H\u001b[?25hafter");
    return output == "\u001b[?1049hmatrix\u001b[?1049lafter";
}

static bool StripOscAndInitOnly()
{
    var output = Filter("\u001b]0;title\u0007\u001b[?25h\u001b[0m");
    return output == "";
}

static bool KeepLineEraseDuringLeadingPhase()
{
    var output = Filter("\u001b[2Ktext");
    return output == "\u001b[2Ktext";
}

static bool StripPrivateModes()
{
    var output = Filter("\u001b[?9001h\u001b[?1004l\u001b[?2004htext");
    return output == "text";
}

static bool StripLeadingNewlines()
{
    var output = Filter(string.Join("", Enumerable.Repeat("\r\n", 20)), "\u001b[?1049hvim");
    return output == "\u001b[?1049hvim";
}

static bool KeepLargePassthroughChunk()
{
    var payload = new string('x', 100_000);
    var output = Filter("start", payload + "\u001b[2Jtail");
    return output == "start" + payload + "\u001b[2Jtail";
}

static bool StripCopilotStyleAlternateScreenExit()
{
    var output = Filter(
        "\u001b[?1049hcopilot",
        "\u001b[?1049l\u001b[?12h\u001b[?25l\u001b[H\u001b[K\r\n\u001b[K\r\n\u001b[H\u001b[?25hafter");
    return output == "\u001b[?1049hcopilot\u001b[?1049lafter";
}

static bool FastPathOutputIsBufferIndependent()
{
    var filter = new PtyLeadingInitFilter();
    var source = Encoding.UTF8.GetBytes("hello");
    var output = filter.Process(source);

    source[0] = (byte)'X';

    return Encoding.UTF8.GetString(output.Span) == "hello";
}
