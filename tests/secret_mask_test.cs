#:sdk Microsoft.NET.Sdk
#:property TargetFramework=net10.0
#:property Nullable=enable
#:property ImplicitUsings=enable
#:package VYaml@1.6.1
#:include ../Terminal.cs
#:include ../CastTimingPipeline.cs
#:include ../CastReader.cs
#:include ../Svg.cs
#:include ../JsonEscape.cs
#:include ../PtyLeadingInitFilter.cs
#:include ../ScenarioKeys.cs
#:include ../Scenario.cs
#:include ../Highlight.cs
#:include ../CastComposer.cs
#:include ../Rerecord.cs
#:include ../SecretMask.cs
#:include ../StepChunks.cs
#:include ../StepOutputs.cs
#:include ../PlaygroundRender.cs

// Secret masking (.github/docs/spec_scenario.md → Masking secrets). Every secret here is put together at run time,
// so no file in the repository holds a whole token that a secret scanner would report.

using System.Text;
using static ScenarioCast;

var failures = 0;

failures += Run("ListedTextIsHidden", ListedTextIsHidden);
failures += Run("ListedTextSplitAcrossChunks", ListedTextSplitAcrossChunks);
failures += Run("ColorCodesDoNotSplitASecret", ColorCodesDoNotSplitASecret);
failures += Run("CursorMovesSeparateText", CursorMovesSeparateText);
failures += Run("ConPtyCursorForwardIsASpace", ConPtyCursorForwardIsASpace);
failures += Run("TextAfterACutOffStringIsFound", TextAfterACutOffStringIsFound);
failures += Run("UnendedDataStringIsReadAsText", UnendedDataStringIsReadAsText);
failures += Run("TextJoinsWhereTheTerminalJoinsIt", TextJoinsWhereTheTerminalJoinsIt);
failures += Run("StringsTheCastHidesStay", StringsTheCastHidesStay);
failures += Run("CutOffStringsLeaveNoSecretInTheCast", CutOffStringsLeaveNoSecretInTheCast);
failures += Run("StreamedPipeCutsFollowTheCast", StreamedPipeCutsFollowTheCast);
failures += Run("StreamedStepLeavesNoSecretInTheCast", StreamedStepLeavesNoSecretInTheCast);
failures += Run("WideCharactersKeepTheirWidth", WideCharactersKeepTheirWidth);
failures += Run("KnownTokensKeepTheirPrefix", KnownTokensKeepTheirPrefix);
failures += Run("TokenLookalikesStay", TokenLookalikesStay);
failures += Run("SecretPairsHideTheValue", SecretPairsHideTheValue);
failures += Run("OtherPairsStay", OtherPairsStay);
failures += Run("UrlPasswordIsHidden", UrlPasswordIsHidden);
failures += Run("PrivateKeyBodyIsHidden", PrivateKeyBodyIsHidden);
failures += Run("JsonWebTokenKeepsItsHeader", JsonWebTokenKeepsItsHeader);
failures += Run("SecretEnvironmentValuesAreHidden", SecretEnvironmentValuesAreHidden);
failures += Run("MaskAutoFalseHidesListedTextOnly", MaskAutoFalseHidesListedTextOnly);
failures += Run("MaskingTwiceChangesNothing", MaskingTwiceChangesNothing);
failures += Run("PipeStreamsAreMaskedSeparately", PipeStreamsAreMaskedSeparately);
failures += Run("SummaryCountsByKind", SummaryCountsByKind);
failures += Run("SecretKeyNames", SecretKeyNames);
failures += Run("SettingsKeysParse", SettingsKeysParse);
failures += Run("RecordingHidesCommandCommentAndOutput", RecordingHidesCommandCommentAndOutput);

if (failures > 0)
{
    Console.Error.WriteLine($"Failed: {failures} secret mask test(s)");
    return 1;
}

Console.Error.WriteLine("all secret mask tests passed");
return 0;

static int Run(string name, Func<bool> test)
{
    try
    {
        if (test())
        {
            Console.Error.WriteLine($"ok {name}");
            return 0;
        }
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"  {ex}");
    }

    Console.Error.WriteLine($"FAIL {name}");
    return 1;
}

static bool Expect(bool condition, string message)
{
    if (!condition)
        Console.Error.WriteLine($"  {message}");
    return condition;
}

static bool Same(string label, string expected, string actual) =>
    Expect(expected == actual, $"{label}: expected {Show(expected)}, got {Show(actual)}");

static string Show(string text) => "\"" + text.Replace("\u001b", "\\e").Replace("\r", "\\r").Replace("\n", "\\n") + "\"";

static string Stars(int count) => new('*', count);

static string Repeat(string text, int length)
{
    var sb = new StringBuilder();
    while (sb.Length < length)
        sb.Append(text);
    return sb.ToString(0, length);
}

static SecretMasker Masker(string? yamlSettings = null, IEnumerable<KeyValuePair<string, string?>>? environment = null) =>
    SecretMasker.ForScenario(ParseScenario($"settings:\n{yamlSettings ?? "  prompt: \"$ \""}\nsteps:\n  - echo hi\n"), environment)
        ?? throw new InvalidOperationException("no masker");

static string MaskPty(SecretMasker masker, params string[] chunks)
{
    var input = chunks.Select((text, i) => new TimedBytes(TimeSpan.FromMilliseconds(i), Encoding.UTF8.GetBytes(text))).ToArray();
    var output = masker.MaskPty(input);
    return string.Join("|", output.Select(static chunk => Encoding.UTF8.GetString(chunk.Data.Span)));
}

static bool ListedTextIsHidden()
{
    var masker = Masker("  mask:\n    - \"s3cret value\"");
    return Same("listed", "the value is ************.\n", masker.MaskText("the value is s3cret value.\n"));
}

static bool ListedTextSplitAcrossChunks()
{
    var masker = Masker("  mask:\n    - abcdef");
    // Each chunk keeps its place: the stars stay in the chunk that held the characters.
    return Same("split", "x***|***y", MaskPty(masker, "xabc", "defy"));
}

static bool ColorCodesDoNotSplitASecret()
{
    var token = "gh" + "p_" + Repeat("aB3", 36);
    var colored = token[..10] + "\u001b[1m" + token[10..] + "\u001b[0m";
    var expected = "ghp_" + Stars(6) + "\u001b[1m" + Stars(30) + "\u001b[0m";
    return Same("colored", expected, Masker().MaskText(colored));
}

static bool CursorMovesSeparateText()
{
    // A full-screen program draws "abc" and "def" in different places: they are not one word.
    var masker = Masker("  mask:\n    - abcdef");
    return Same("cursor", "abc\u001b[5;1Hdef", masker.MaskText("abc\u001b[5;1Hdef"));
}

static bool ConPtyCursorForwardIsASpace()
{
    // ConPTY writes `TOKEN: value` with the space as a cursor move.
    return Same("cuf", "TOKEN:\u001b[1C" + Stars(10), Masker().MaskText("TOKEN:\u001b[1Cvalue12345"));
}

// The secret follows a control string that ends early: the cast filter, the SVG renderer, or the asciinema player ends
// it there, so the text after it is shown. Each case has a later terminator or none at all, so a masker that waits for
// the string's ST misses the secret.
static (string Label, string Text)[] CutOffStrings() =>
[
    ("title ended by CSI", "b \u001b]0;t\u001b[0m hunter2secret\n"),
    ("title ended by CAN", "b \u001b]0;t\u0018 hunter2secret\n"),
    ("title ended by SUB", "b \u001b]0;t\u001a hunter2secret\u0007\n"),
    ("DCS ended by BEL", "\u001bPq\u0007 hunter2secret\u001b\\"),
    ("DCS ended by CAN", "\u001bPq\u0018 hunter2secret\n"),
    ("DCS ended by CSI", "\u001bPq\u001b[0m hunter2secret\u001b\\"),
    ("DCS ended by a C1 control", "\u001bPq\u0085hunter2secret\u001b\\"),
    ("color OSC ended by C1 ST", "\u001b]4;1;rgb:0/0/0\u009c hunter2secret\u001b\\"),
    ("color OSC ended by NEL", "\u001b]4;1;rgb:0/0/0\u0085hunter2secret\u001b\\"),
    ("APC ended by CSI", "\u001b_Ga=T\u001b[0m hunter2secret\u001b\\"),
    ("SOS ended by SUB", "\u001bXs\u001a hunter2secret\u001b\\"),
    ("PM ended by CAN", "\u001b^p\u0018 hunter2secret\u001b\\"),
];

// Nothing ends these strings, and the cast keeps their bytes, so what follows the introducer is read as the program's
// output. The last ESC of the output starts nothing: the cast filter drops it.
static (string Label, string Text)[] UnendedDataStrings() =>
[
    ("DCS", "d \u001bPq hunter2secret\n"),
    ("APC", "\u001b_Ga=T hunter2secret\n"),
    ("color OSC", "\u001b]4;1;rgb:0/0/0 hunter2secret\n"),
    ("DCS with an ESC at the end", "d \u001bPq hunter2secret\u001b"),
];

static bool TextAfterACutOffStringIsFound()
{
    var ok = true;
    foreach (var (label, text) in CutOffStrings())
        ok &= Same(label, text.Replace("hunter2secret", Stars(13)), Masker("  mask:\n    - hunter2secret").MaskText(text));
    return ok;
}

static bool UnendedDataStringIsReadAsText()
{
    var ok = true;
    foreach (var (label, text) in UnendedDataStrings())
        ok &= Same(label, text.Replace("hunter2secret", Stars(13)), Masker("  mask:\n    - hunter2secret").MaskText(text));
    return ok;
}

static bool TextJoinsWhereTheTerminalJoinsIt()
{
    // Between the halves stands only what draws no character and leaves the cursor in place: a string, an escape that
    // another one cuts off or CAN cancels, a terminator that comes after its string has ended, SO or SI between sets
    // that both hold ASCII, or a C1 control that neither moves the cursor nor starts a sequence. So a player shows
    // "hunter2secret" in one piece. A line break, NEL, or another control that moves the cursor keeps the halves apart.
    var joined = new (string Label, string Between)[]
    {
        ("cancelled title", "\u001b]0;t\u0018"),
        ("title ended by the next sequence", "\u001b]0;t\u001b[0m"),
        ("color OSC ended by C1 ST", "\u001b]4;1;rgb:0/0/0\u009c"),
        ("image", "\u001bPq#0~\u001b\\"),
        ("ST after a cancelled title", "\u001b]0;t\u0018\u001b\\"),
        ("mark passed through tmux", "\u001bPtmux;\u001b\u001b]633;E;x\u0007\u001b\\"),
        ("bell", "\u0007"),
        ("C1 control that does nothing", "\u001b]4;1;rgb:0/0/0\u0081"),
        ("escape cancelled by CAN", "\u001b\u0018"),
        ("CSI cut off by the next sequence", "\u001b[\u001b[0m"),
        ("CSI cancelled by CAN", "\u001b[1\u0018"),
        ("character set choice cut off by the next sequence", "\u001b(\u001b[0m"),
        ("character set choice cancelled by CAN", "\u001b(\u0018"),
        ("escape cancelled by SUB", "\u001b\u001a"),
        ("character set shift", "\u000e"),
        ("shift back", "\u000f"),
        ("erase to the end of the line", "\u001b[K"),
        ("erase to the end of the line with its parameter", "\u001b[0K"),
    };
    var apart = new (string Label, string Between)[]
    {
        ("next line", "\u001b]4;1;rgb:0/0/0\u0085"),
        ("index", "\u0084"),
        ("reverse index", "\u008d"),
        ("line break", "\n"),
        ("carriage return", "\r"),
        ("backspace", "\b"),
        ("vertical tab", "\u000b"),
        ("form feed", "\u000c"),
        ("cursor move", "\u001b[5;1H"),
        ("next line escape", "\u001bE"),
        ("erase of the whole line", "\u001b[2K"),
        ("erase to the start of the line", "\u001b[1K"),
        ("reverse index escape", "\u001bM"),
    };

    var ok = true;
    foreach (var (label, between) in joined)
        ok &= Same(label, "***" + between + Stars(10), Masker("  mask:\n    - hunter2secret").MaskText("hun" + between + "ter2secret"));
    foreach (var (label, between) in apart)
        ok &= Same(label, "hun" + between + "ter2secret", Masker("  mask:\n    - hunter2secret").MaskText("hun" + between + "ter2secret"));

    // A colored match as grep writes it: the color and an erase to the end of the line on each side of the key.
    ok &= Same("colored match", "GITHUB_\u001b[01;31m\u001b[KTOKEN\u001b[m\u001b[K=" + Stars(12),
        Masker().MaskText("GITHUB_\u001b[01;31m\u001b[KTOKEN\u001b[m\u001b[K=abcd1234efgh"));
    return ok;
}

static bool StringsTheCastHidesStay()
{
    // The cast filter removes these titles, links, marks, and the clipboard with their text, and no player draws the
    // data of a DCS, SOS, PM, or APC that ends. Stars here would be a false positive: a changed image, or a count of a
    // secret that the recording does not show.
    var token = "sk" + "-" + Repeat("aZ_9-", 24);
    var cases = new (string Label, string Text)[]
    {
        ("title ended by BEL", "\u001b]0;API_KEY=abcdefgh\u0007after\n"),
        ("title ended by ST", "\u001b]0;API_KEY=abcdefgh\u001b\\after\n"),
        ("title ended by CAN", "\u001b]0;API_KEY=abcdefgh\u0018after\n"),
        ("title ended by SUB", "\u001b]0;API_KEY=abcdefgh\u001aafter\n"),
        ("title ended by CSI", "\u001b]0;API_KEY=abcdefgh\u001b[0mafter\n"),
        ("unended title, dropped by the cast", "\u001b]0;API_KEY=abcdefgh\n"),
        ("title with a C1 control, dropped by the cast up to BEL", "\u001b]0;x\u0085API_KEY=abcdefgh\u0007after\n"),
        ("hyperlink target", "\u001b]8;;https://app:pass1234@db.example/\u001b\\link\u001b]8;;\u001b\\\n"),
        ("shell integration mark", "\u001b]633;E;export API_KEY=abcdefgh;nonce\u0007\n"),
        ("clipboard", "\u001b]52;c;" + token + "\u0007\n"),
        ("Sixel-like data", "\u001bPq#0;2;0;0;0?" + token + "\u001b\\\n"),
        ("DCS ended by ST", "\u001bP1$r" + token + "\u001b\\\n"),
        ("DCS data with a character that is not a C1 control", "\u001bPq" + (char)0xA0 + token + "\u001b\\\n"),
        ("APC ended by ST", "\u001b_Ga=T;" + token + "\u001b\\\n"),
        ("PM ended by ST", "\u001b^" + token + "\u001b\\\n"),
        ("SOS ended by ST", "\u001bX" + token + "\u001b\\\n"),
        ("mark passed through tmux", "\u001bPtmux;\u001b\u001b]633;E;export API_KEY=abcdefgh;nonce\u0007\u001b\\\n"),
        ("image passed through tmux", "\u001bPtmux;\u001b\u001b_Ga=T;" + token + "\u001b\u001b\\\u001b\\\n"),
    };

    var ok = true;
    foreach (var (label, text) in cases)
    {
        var masker = Masker();
        ok &= Same(label, text, masker.MaskText(text)) & Expect(masker.Summary() is null, $"{label}: counted {masker.Summary()}");
    }

    return ok;
}

static bool CutOffStringsLeaveNoSecretInTheCast()
{
    // The cast filter removes titles and other private OSCs as the step is composed, so this checks what the cast
    // holds, for pipe and PTY output, not only what the masker returns.
    var ok = true;
    foreach (var (label, text) in CutOffStrings().Concat(UnendedDataStrings()).Append(("joined across a CAN", "hun\u001b]0;t\u0018ter2secret\n")))
    {
        foreach (var pty in new[] { false, true })
        {
            var execution = pty
                ? new CommandExecution(0, true, "", "", [new TimedBytes(TimeSpan.Zero, Encoding.UTF8.GetBytes(text))], null, null) { PtyLeadingInitRemoved = true }
                : CommandExecution.FromPipeStream(text, "", 0, [new PipeStreamChunk(TimeSpan.Zero, false, text)]);
            var cast = RecordOneStep("  - printf something\n", execution);
            ok &= Expect(!cast.Contains("secret", StringComparison.Ordinal), $"{label} ({(pty ? "PTY" : "pipe")}): the cast holds the secret: {Show(cast)}");
        }
    }

    return ok;
}

static bool StreamedPipeCutsFollowTheCast()
{
    // Streamed output puts the reads of both streams in time order and colors each stderr read that has no colors of
    // its own. A cut stands only where the cast writes a byte that ends a sequence or follows a lone ESC: a cut that
    // ends nothing would make the masker read the rest of a string as text.
    PipeStreamChunk Out(string text) => new(TimeSpan.Zero, false, text);
    PipeStreamChunk Err(string text) => new(TimeSpan.Zero, true, text);
    static string Flags(PipeReadCuts[] cuts) => string.Join(" ", cuts.Select(static cut => cut switch
    {
        PipeReadCuts.None => "-",
        PipeReadCuts.Before => "B",
        PipeReadCuts.AfterText => "A",
        _ => "BA",
    }));

    var cases = new (string Label, PipeStreamChunk[] Reads, string Style, string Expected)[]
    {
        ("colored stderr", [Out("a"), Out("b"), Err("e1"), Out("c"), Err("e2")], "\u001b[31m", "- - BA B BA"),
        ("stderr without color", [Out("a"), Out("b"), Err("e1"), Out("c"), Err("e2")], "", "- - - - -"),
        ("stderr with colors of its own", [Out("a"), Err("\u001b[1mw\u001b[0m"), Out("b"), Err("x")], "", "- - B -"),
        ("stderr colored after one with colors of its own", [Err("\u001b[1mw\u001b[0m"), Err("x")], "\u001b[31m", "- BA"),
        ("BEL from the other stream", [Out("\u001b]0;t"), Err("\u0007"), Out("h")], "", "- - B"),
        ("ESC that ends a read", [Out("x\u001b"), Err("y"), Out("]")], "", "- - B"),
        ("escape left open at the end of a read", [Out("x\u001b("), Err("y"), Out("B"), Out("z\u001b[3"), Err("y"), Out("1m")], "", "- - B - B B"),
        ("plain text from the other stream", [Out("\u001b]0;"), Err("w\n"), Out("x")], "", "- - -"),
        ("CR of a CRLF split between reads, which the cast moves", [Err("\r"), Err("\nx")], "\u001b[31m", "- BA"),
        ("CSI that a line feed runs inside", [Out("x\u001b[1;3"), Err("\n"), Out("2m")], "", "- - -"),
        ("CSI that a bell runs inside", [Out("x\u001b[1;3"), Err("\u0007"), Out("2m")], "", "- - -"),
        ("CSI that parameter bytes go on", [Out("x\u001b[1;3"), Err("45%"), Out("2m")], "", "- - -"),
        ("ESC that a line feed runs inside", [Out("x\u001b"), Err("\n"), Out("[32m")], "", "- - -"),
        ("empty read", [Out("x\u001b["), Err(""), Out("1m")], "", "- - -"),
        ("CSI over two reads", [Out("x\u001b["), Out("1"), Err("y"), Out("h")], "", "- - - B"),
        ("escape over two reads", [Out("x\u001b"), Out("("), Err("B"), Out("h")], "", "- - - B"),
        ("digit after an ESC", [Out("x\u001b"), Err("7"), Out("]")], "", "- - B"),
        ("CAN from the other stream", [Out("\u001b]0;t"), Err("\u0018"), Out("h")], "", "- - B"),
        ("intermediate after a lone ESC", [Out("x\u001b"), Err("."), Out("]")], "", "- - B"),
        ("intermediate after an escape with one", [Out("x\u001b("), Err("."), Out("B")], "", "- - -"),
        ("DEL inside a CSI", [Out("x\u001b[1"), Err("\u007f"), Out("m")], "", "- - -"),
        ("DEL after a lone ESC, which the terminal that plays the cast ignores", [Out("x\u001b"), Err("\u007f"), Out("]")], "", "- - -"),
    };

    var ok = true;
    foreach (var (label, reads, style, expected) in cases)
        ok &= Same(label, expected, Flags(StreamedPipeCuts(reads, style)));
    return ok;
}

static bool StreamedStepLeavesNoSecretInTheCast()
{
    var cases = new (string Label, PipeStreamChunk[] Chunks)[]
    {
        ("stderr title split across reads", [new(TimeSpan.Zero, true, "\u001b]0;t"), new(TimeSpan.FromMilliseconds(10), true, "hunter2secret\u0007\n")]),
        ("stdout title cut by stderr", [new(TimeSpan.Zero, false, "\u001b]0;t"), new(TimeSpan.FromMilliseconds(5), true, "warn\n"), new(TimeSpan.FromMilliseconds(10), false, "hunter2secret\u0007\n")]),
        ("stdout DCS cut by stderr", [new(TimeSpan.Zero, false, "\u001bPq"), new(TimeSpan.FromMilliseconds(5), true, "warn\n"), new(TimeSpan.FromMilliseconds(10), false, " hunter2secret\u001b\\\n")]),
        ("stdout title ended by a BEL from stderr", [new(TimeSpan.Zero, false, "\u001b]0;t"), new(TimeSpan.FromMilliseconds(5), true, "\u0007"), new(TimeSpan.FromMilliseconds(10), false, "hunter2secret\n")]),
        ("ESC that ends a read", [new(TimeSpan.Zero, false, "x\u001b"), new(TimeSpan.FromMilliseconds(5), true, "y"), new(TimeSpan.FromMilliseconds(10), false, "]0;hunter2secret\u0007\n")]),
        ("title that plain stderr does not end", [new(TimeSpan.Zero, false, "hun\u001b]0;"), new(TimeSpan.FromMilliseconds(5), true, "w\n"), new(TimeSpan.FromMilliseconds(10), false, "x\u0007ter2secret\n")]),
        ("title left open before a line break", [new(TimeSpan.Zero, true, "\u001b[1mhun\u001b]0;t\n"), new(TimeSpan.FromMilliseconds(10), true, "ter2secret\n")]),
        ("escape left open at the end of a read", [new(TimeSpan.Zero, false, "x \u001b("), new(TimeSpan.FromMilliseconds(5), true, "y\n"), new(TimeSpan.FromMilliseconds(10), false, "hunter2secret\n")]),
        ("CSI left open at the end of a read", [new(TimeSpan.Zero, false, "x \u001b["), new(TimeSpan.FromMilliseconds(5), true, "y\n"), new(TimeSpan.FromMilliseconds(10), false, "hunter2secret\n")]),
        ("CSI left open over two reads", [new(TimeSpan.Zero, false, "x\u001b["), new(TimeSpan.FromMilliseconds(1), false, "1"), new(TimeSpan.FromMilliseconds(5), true, "y"), new(TimeSpan.FromMilliseconds(10), false, "hunter2secret\n")]),
        ("ESC that a digit from stderr ends", [new(TimeSpan.Zero, false, "x\u001b"), new(TimeSpan.FromMilliseconds(5), true, "7"), new(TimeSpan.FromMilliseconds(10), false, "]0;hunter2secret\u0007\n")]),
        ("title that a CAN from stderr ends", [new(TimeSpan.Zero, false, "\u001b]0;t"), new(TimeSpan.FromMilliseconds(5), true, "\u0018"), new(TimeSpan.FromMilliseconds(10), false, "hunter2secret\n")]),
        ("ESC that an intermediate from stderr joins", [new(TimeSpan.Zero, false, "x\u001b"), new(TimeSpan.FromMilliseconds(5), true, "."), new(TimeSpan.FromMilliseconds(10), false, "]0;hunter2secret\u0007\n")]),
    };

    var ok = true;
    foreach (var (label, chunks) in cases)
    {
        var stdout = string.Concat(chunks.Where(static chunk => !chunk.IsStderr).Select(static chunk => chunk.Text));
        var stderr = string.Concat(chunks.Where(static chunk => chunk.IsStderr).Select(static chunk => chunk.Text));
        foreach (var color in new[] { "red", "none" })
        {
            var cast = RecordOneStep($"  - run: printf something\n    stream-output: true\n    stderr-color: {color}\n", CommandExecution.FromPipeStream(stdout, stderr, 0, chunks));
            ok &= Expect(!Shown(cast).Contains("hunter2secret", StringComparison.Ordinal), $"{label} (stderr-color {color}): the cast shows the secret: {Show(cast)}");
        }
    }

    // The color around the first read closes before its line break, a line feed or a carriage return, so the token
    // starts a line of its own in the cast.
    var body = Repeat("aB3", 36);
    foreach (var lineBreak in new[] { "\n", "\r" })
    {
        var token = new PipeStreamChunk[] { new(TimeSpan.Zero, true, "Done\u001b]0;t" + lineBreak), new(TimeSpan.FromMilliseconds(10), true, "gh" + "p_" + body + "\n") };
        var tokenCast = RecordOneStep("  - run: printf something\n    stream-output: true\n", CommandExecution.FromPipeStream("", string.Concat(token.Select(static chunk => chunk.Text)), 0, token));
        ok &= Expect(!tokenCast.Contains(body, StringComparison.Ordinal), $"token after {Show(lineBreak)}: the cast holds it: {Show(tokenCast)}");
    }

    // Output that is not streamed holds stdout whole and then stderr, so stdout's title runs on to its own BEL: a cut
    // at the stderr read would split "hunter2secret".
    var instant = new PipeStreamChunk[] { new(TimeSpan.Zero, false, "hun\u001b]0;t"), new(TimeSpan.FromMilliseconds(5), true, "\u0007"), new(TimeSpan.FromMilliseconds(10), false, "x\u0007ter2secret\n") };
    foreach (var color in new[] { "red", "none" })
    {
        var instantCast = RecordOneStep($"  - run: printf something\n    stderr-color: {color}\n", CommandExecution.FromPipeStream("hun\u001b]0;tx\u0007ter2secret\n", "\u0007", 0, instant));
        ok &= Expect(!Shown(instantCast).Contains("hunter2secret", StringComparison.Ordinal), $"output that is not streamed (stderr-color {color}): the cast shows the secret: {Show(instantCast)}");
    }

    // A line feed from stderr runs inside the color that stdout split between two reads, so the token follows the color
    // in the cast as it does in stdout.
    var split = new PipeStreamChunk[]
    {
        new(TimeSpan.Zero, false, "x \u001b[1;3"),
        new(TimeSpan.FromMilliseconds(5), true, "\n"),
        new(TimeSpan.FromMilliseconds(10), false, "2mgh" + "p_" + body + "\u001b[0m\n"),
    };
    var splitCast = RecordOneStep("  - run: printf something\n    stream-output: true\n    stderr-color: none\n", CommandExecution.FromPipeStream("x \u001b[1;32mgh" + "p_" + body + "\u001b[0m\n", "\n", 0, split));
    ok &= Expect(!splitCast.Contains(body, StringComparison.Ordinal), $"token after a split color: the cast holds it: {Show(splitCast)}");

    // A title that one stream writes in two reads, with nothing between them, stays one title in the cast.
    var masker = Masker();
    var title = new PipeStreamChunk[] { new(TimeSpan.Zero, false, "\u001b]0;API_KEY="), new(TimeSpan.FromMilliseconds(10), false, "abcdefgh\u0007\n") };
    return ok
        & Expect(ReferenceEquals(title, masker.MaskPipe(title, StreamedPipeCuts(title, "\u001b[31m"))), "a title in two reads of one stream stays")
        & Expect(masker.Summary() is null, $"counted {masker.Summary()}");
}

// The text of a cast as a player shows it on one line: without colors, the data of a DCS, SOS, PM, or APC up to the
// next ESC (no case here ends one earlier), and the controls that draw nothing.
static string Shown(string cast) =>
    System.Text.RegularExpressions.Regex.Replace(cast, "\u001b\\[[0-?]*[ -/]*[@-~]|\u001b[PX^_][^\u001b]*(\u001b\\\\)?|[\u0000-\u0009\u000b-\u001f]", "");

// The cast of a one-step scenario that masks "hunter2secret", after the cast filter.
static string RecordOneStep(string step, CommandExecution execution)
{
    var scenario = ParseScenario("settings:\n  mask:\n    - hunter2secret\nsteps:\n" + step);
    var options = new GenerateOptions { LogRunning = false, Masker = SecretMasker.ForScenario(scenario, null) };
    var (events, _) = GenerateAsync(scenario, 1, options, _ => Task.FromResult(execution)).GetAwaiter().GetResult();
    return string.Concat(CastOscFilter.RemovePrivateSequences(events).Select(static ev => ev.HasUtf8Output ? Encoding.UTF8.GetString(ev.Utf8Output.Span) : ev.Data));
}

static bool WideCharactersKeepTheirWidth()
{
    var masker = Masker("  mask:\n    - 秘密の値");
    return Same("wide", "鍵は********です", masker.MaskText("鍵は秘密の値です"));
}

static bool KnownTokensKeepTheirPrefix()
{
    var masker = Masker();
    var cases = new (string Prefix, string Body)[]
    {
        ("gh" + "p_", Repeat("Ab1", 36)),
        ("github" + "_pat_", Repeat("A1_b", 82)),
        ("gl" + "pat-", Repeat("x-Y_9", 20)),
        ("xo" + "xb-", "1234-5678-" + Repeat("ab", 24)),
        ("AK" + "IA", Repeat("Q7W", 16)),
        ("sk" + "-", "proj-" + Repeat("aZ_9-", 40)),
        ("sk" + "_live_", Repeat("aZ9", 24)),
        ("AI" + "za", Repeat("Sy_-9", 35)),
        ("np" + "m_", Repeat("aZ9", 36)),
        ("hf" + "_", Repeat("aZ9", 34)),
    };

    var ok = true;
    foreach (var (prefix, body) in cases)
        ok &= Same(prefix, $"key: {prefix}{Stars(body.Length)} end", masker.MaskText($"key: {prefix}{body} end"));
    return ok;
}

static bool TokenLookalikesStay()
{
    var masker = Masker();
    var ok = true;
    foreach (var text in new[]
    {
        "gh" + "p_" + Repeat("Ab1", 20),               // too short
        "task" + "-" + Repeat("ab", 30),               // sk- inside a word
        "AK" + "IA" + Repeat("Q7W", 15),               // one short
        "AK" + "IA" + Repeat("Q7W", 17),               // one long
        "commit 1f3c5a7d9b2e4f6a8c0d1e3f5a7b9c2d4e6f8a0b",
        "eyJ" + "hbGc.short.x",
    })
    {
        ok &= Same(text, text, masker.MaskText(text));
    }

    return ok;
}

static bool SecretPairsHideTheValue()
{
    var masker = Masker();
    var cases = new (string Input, string Expected)[]
    {
        ("API_KEY=abcd1234", "API_KEY=" + Stars(8)),
        ("export GITHUB_TOKEN=xyz12345", "export GITHUB_TOKEN=" + Stars(8)),
        ("\"password\": \"hunter2x\",", "\"password\": \"" + Stars(8) + "\","),
        ("password: hunter2x", "password: " + Stars(8)),
        ("db.password = 'p@ss-word'", "db.password = '" + Stars(9) + "'"),
        ("mysql --password=abc12345 -u root", "mysql --password=" + Stars(8) + " -u root"),
        ("Authorization: Bearer abcdef0123456789", "Authorization: Bearer " + Stars(16)),
        ("\"Authorization\": \"Basic dXNlcjpwYXNz\"", "\"Authorization\": \"Basic " + Stars(12) + "\""),
        ("aws_secret_access_key = abcdEFGH1234", "aws_secret_access_key = " + Stars(12)),
        ("x-api-key: k-123456", "x-api-key: " + Stars(8)),
        ("clientSecret:\"zz99yy88\"", "clientSecret:\"" + Stars(8) + "\""),
        ("AccountName=demo;AccountKey=abc+def/123==;", "AccountName=demo;AccountKey=" + Stars(13) + ";"),
    };

    var ok = true;
    foreach (var (input, expected) in cases)
        ok &= Same(input, expected, masker.MaskText(input));
    return ok;
}

static bool OtherPairsStay()
{
    var masker = Masker();
    var ok = true;
    foreach (var text in new[]
    {
        "token_endpoint: https://example.com/oauth/token",
        "PASSWORD_FILE=/run/secrets/db",
        "max_tokens: 100",
        "token_count: 5",
        "TOKEN=$GITHUB_TOKEN",
        "if token == expected:",
        "\"token\": null",
        "secretKeyRef: name",
        "PUBLIC_KEY=ssh-ed25519",
        "password: ********",
        "\"token\": \"not set yet\"",
        "Password: ",
        "KEY=value123",
    })
    {
        ok &= Same(text, text, masker.MaskText(text));
    }

    return ok;
}

static bool UrlPasswordIsHidden()
{
    var masker = Masker();
    return Same("url", "DATABASE_URL=postgres://app:" + Stars(7) + "@db:5432/app", masker.MaskText("DATABASE_URL=postgres://app:hunter2@db:5432/app"))
        & Same("no password", "https://example.com:8080/path@x", masker.MaskText("https://example.com:8080/path@x"))
        & Same("ssh", "git@github.com:org/repo.git", masker.MaskText("git@github.com:org/repo.git"));
}

static bool PrivateKeyBodyIsHidden()
{
    var begin = "-----BEGIN OPENSSH " + "PRIVATE KEY-----";
    var end = "-----END OPENSSH " + "PRIVATE KEY-----";
    var input = $"{begin}\r\nb3BlbnNzaC1rZXk\r\nAAAAABG5vbmU=\r\n{end}\r\n";
    var expected = $"{begin}\r\n{Stars(15)}\r\n{Stars(13)}\r\n{end}\r\n";
    return Same("pem", expected, Masker().MaskText(input));
}

static bool JsonWebTokenKeepsItsHeader()
{
    var header = "ey" + "JhbGciOiJIUzI1NiJ9";
    var payload = "ey" + "JzdWIiOiIxMjM0NTY3ODkwIn0";
    var signature = "dozjgNryP4J3jVmNHl0w5N_XgL0n3I9PlFUP0THsR8U";
    var token = $"{header}.{payload}.{signature}";
    return Same("jwt", $"id={header}.{Stars(payload.Length + 1 + signature.Length)} ok", Masker().MaskText($"id={token} ok"));
}

static bool SecretEnvironmentValuesAreHidden()
{
    var environment = new Dictionary<string, string?>
    {
        ["MY_SERVICE_TOKEN"] = "opaque-value-123",
        ["HOME"] = "/home/demo-user",
        ["SHORT_SECRET"] = "abc",
        ["PORT_PASSWORD"] = "12345678",
    };
    var masker = Masker(environment: environment);
    return Same("token value", "got " + Stars(16) + " back", masker.MaskText("got opaque-value-123 back"))
        & Same("home", "cd /home/demo-user", masker.MaskText("cd /home/demo-user"))
        & Same("short", "abc", masker.MaskText("abc"))
        & Same("digits", "12345678", masker.MaskText("12345678"));
}

static bool MaskAutoFalseHidesListedTextOnly()
{
    var masker = Masker("  mask:\n    - hello\n  mask-auto: false", new Dictionary<string, string?> { ["API_TOKEN"] = "zzzzzzzzzz" });
    return Same("listed", "*****", masker.MaskText("hello"))
        & Same("auto off", "API_KEY=abcd1234", masker.MaskText("API_KEY=abcd1234"))
        & Same("environment off", "zzzzzzzzzz", masker.MaskText("zzzzzzzzzz"))
        & Expect(SecretMasker.ForScenario(ParseScenario("settings:\n  mask-auto: false\nsteps:\n  - echo hi\n"), null) is null,
            "mask-auto: false without mask texts gives no masker");
}

static bool MaskingTwiceChangesNothing()
{
    var masker = Masker();
    var chunks = new[] { new TimedBytes(TimeSpan.Zero, Encoding.UTF8.GetBytes("API_KEY=abcd1234\r\n")) };
    var once = masker.MaskPty(chunks);
    var twice = Masker().MaskPty(once);
    return Expect(!ReferenceEquals(once, chunks), "the first pass masks")
        & Expect(ReferenceEquals(once, twice), "the second pass returns the same chunks");
}

static bool PipeStreamsAreMaskedSeparately()
{
    var masker = Masker("  mask:\n    - abcdef");
    var chunks = new[]
    {
        new PipeStreamChunk(TimeSpan.Zero, false, "abc"),
        new PipeStreamChunk(TimeSpan.FromMilliseconds(1), true, "zzz"),
        new PipeStreamChunk(TimeSpan.FromMilliseconds(2), false, "def\n"),
        new PipeStreamChunk(TimeSpan.FromMilliseconds(3), true, "abcdef\n"),
    };
    var masked = masker.MaskPipe(chunks);
    return Same("stdout", "***|***\n", masked[0].Text + "|" + masked[2].Text)
        & Same("stderr", "zzz|******\n", masked[1].Text + "|" + masked[3].Text)
        & Expect(masked.Select(static chunk => chunk.Time).SequenceEqual(chunks.Select(static chunk => chunk.Time)), "times stay");
}

static bool SummaryCountsByKind()
{
    var masker = Masker("  mask:\n    - hello");
    masker.MaskText("hello API_KEY=abcd1234 and API_TOKEN=efgh5678 hello");
    return Same("summary", "Masked 4 secrets: 2 key=value secret, 2 settings.mask", masker.Summary() ?? "(none)")
        & Expect(Masker().Summary() is null, "nothing hidden gives no summary");
}

static bool SecretKeyNames()
{
    var ok = true;
    foreach (var name in new[] { "GITHUB_TOKEN", "TOKEN", "db_password", "clientSecret", "x-api-key", "AWS_SECRET_ACCESS_KEY", "AccountKey", "APIKey", "--password", "Authorization", "MYSQL_ROOT_PASSWORD" })
        ok &= Expect(SecretMasker.IsSecretKey(name), $"{name} names a secret");
    foreach (var name in new[] { "token_endpoint", "PASSWORD_FILE", "PUBLIC_KEY", "KEY", "secretKeyRef", "max_tokens", "TOKENIZERS_PARALLELISM", "author", "SSH_AUTH_SOCK", "PWD" })
        ok &= Expect(!SecretMasker.IsSecretKey(name), $"{name} does not name a secret");
    return ok;
}

static bool SettingsKeysParse()
{
    var scenario = ParseScenario("settings:\n  mask:\n    - one\n    - two\n  mask-auto: false\nsteps:\n  - echo hi\n");
    var unknown = FindUnknownScenarioKeys("settings:\n  mask: [one]\n  mask-auto: true\nsteps:\n  - echo hi\n");
    return Expect(scenario.Settings?.Mask is ["one", "two"], "mask is read as a list")
        & Expect(scenario.Settings?.MaskAuto == false, "mask-auto is read")
        & Expect(unknown.Count == 0, $"no unknown keys: {string.Join("; ", unknown)}")
        & Same("type error", "'settings.mask' must be a list, not 'abc'.", FindValueTypeError("settings:\n  mask: abc\nsteps:\n  - echo hi\n") ?? "(none)");
}

static bool RecordingHidesCommandCommentAndOutput()
{
    var scenario = ParseScenario(
        "settings:\n  mask:\n    - hunter2x\nsteps:\n  - name: log in with hunter2x\n    run: login --password hunter2x\n");
    var recorded = new List<RecordedStep>();
    var masker = SecretMasker.ForScenario(scenario, null);
    var options = new GenerateOptions { LogRunning = false, Record = recorded, Masker = masker };
    var (events, _) = GenerateAsync(scenario, 1, options, step => Task.FromResult(CommandExecution.FromPipeStream(
        "welcome, hunter2x\n", "", 0, [new PipeStreamChunk(TimeSpan.Zero, false, "welcome, hunter2x\n")]))).GetAwaiter().GetResult();

    var shown = string.Concat(events.Select(static ev => ev.Kind == CastEventKind.Marker ? $"[{ev.Data}]" : ev.HasUtf8Output ? Encoding.UTF8.GetString(ev.Utf8Output.Span) : ev.Data));
    using var stream = new MemoryStream();
    new StepOutputs { Scenetake = "test", Os = "linux", Shell = "bash", Width = 80, Height = 24, Steps = recorded }.Write(stream);
    var outputsJson = Encoding.UTF8.GetString(stream.ToArray());
    // The outputs file keeps the step's run text as the YAML has it: that is how a replay finds the step.
    return Expect(!shown.Contains("hunter2x", StringComparison.Ordinal), $"the cast shows the secret: {Show(shown)}")
        & Expect(shown.Contains("[log in with ********]", StringComparison.Ordinal), $"the marker is masked: {Show(shown)}")
        & Expect(shown.Contains("welcome, ********", StringComparison.Ordinal), $"the output is masked: {Show(shown)}")
        & Expect(!outputsJson.Contains("welcome, hunter2x", StringComparison.Ordinal) && outputsJson.Contains("welcome, ********", StringComparison.Ordinal),
            $"the outputs file holds the masked output: {outputsJson}");
}
