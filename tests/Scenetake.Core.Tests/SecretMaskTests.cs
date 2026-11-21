using System.Text;
using static Scenetake.Core.ScenarioCast;
using Scenetake.TestSupport;

namespace Scenetake.Core.Tests;

// Secret masking (.github/docs/spec_scenario.md → Masking secrets). Every secret here is put together at run time,
// so no file in the repository holds a whole token that a secret scanner would report.

public sealed class SecretMaskTests
{
    [Test]
    public async Task ListedTextIsHidden() => await Assert.That(SecretMaskCases.ListedTextIsHidden()).IsTrue();

    [Test]
    public async Task ListedTextSplitAcrossChunks() => await Assert.That(SecretMaskCases.ListedTextSplitAcrossChunks()).IsTrue();

    [Test]
    public async Task ColorCodesDoNotSplitASecret() => await Assert.That(SecretMaskCases.ColorCodesDoNotSplitASecret()).IsTrue();

    [Test]
    public async Task CursorMovesSeparateText() => await Assert.That(SecretMaskCases.CursorMovesSeparateText()).IsTrue();

    [Test]
    public async Task ConPtyCursorForwardIsASpace() => await Assert.That(SecretMaskCases.ConPtyCursorForwardIsASpace()).IsTrue();

    [Test]
    public async Task TextAfterACutOffStringIsFound() => await Assert.That(SecretMaskCases.TextAfterACutOffStringIsFound()).IsTrue();

    [Test]
    public async Task UnendedDataStringIsReadAsText() => await Assert.That(SecretMaskCases.UnendedDataStringIsReadAsText()).IsTrue();

    [Test]
    public async Task TextJoinsWhereTheTerminalJoinsIt() => await Assert.That(SecretMaskCases.TextJoinsWhereTheTerminalJoinsIt()).IsTrue();

    [Test]
    public async Task StringsTheCastHidesStay() => await Assert.That(SecretMaskCases.StringsTheCastHidesStay()).IsTrue();

    [Test]
    public async Task CutOffStringsLeaveNoSecretInTheCast() => await Assert.That(SecretMaskCases.CutOffStringsLeaveNoSecretInTheCast()).IsTrue();

    [Test]
    public async Task StreamedPipeCutsFollowTheCast() => await Assert.That(SecretMaskCases.StreamedPipeCutsFollowTheCast()).IsTrue();

    [Test]
    public async Task StreamedStepLeavesNoSecretInTheCast() => await Assert.That(SecretMaskCases.StreamedStepLeavesNoSecretInTheCast()).IsTrue();

    [Test]
    public async Task WideCharactersKeepTheirWidth() => await Assert.That(SecretMaskCases.WideCharactersKeepTheirWidth()).IsTrue();

    [Test]
    public async Task KnownTokensKeepTheirPrefix() => await Assert.That(SecretMaskCases.KnownTokensKeepTheirPrefix()).IsTrue();

    [Test]
    public async Task TokenLookalikesStay() => await Assert.That(SecretMaskCases.TokenLookalikesStay()).IsTrue();

    [Test]
    public async Task SecretPairsHideTheValue() => await Assert.That(SecretMaskCases.SecretPairsHideTheValue()).IsTrue();

    [Test]
    public async Task OtherPairsStay() => await Assert.That(SecretMaskCases.OtherPairsStay()).IsTrue();

    [Test]
    public async Task UrlPasswordIsHidden() => await Assert.That(SecretMaskCases.UrlPasswordIsHidden()).IsTrue();

    [Test]
    public async Task PrivateKeyBodyIsHidden() => await Assert.That(SecretMaskCases.PrivateKeyBodyIsHidden()).IsTrue();

    [Test]
    public async Task JsonWebTokenKeepsItsHeader() => await Assert.That(SecretMaskCases.JsonWebTokenKeepsItsHeader()).IsTrue();

    [Test]
    public async Task SecretEnvironmentValuesAreHidden() => await Assert.That(SecretMaskCases.SecretEnvironmentValuesAreHidden()).IsTrue();

    [Test]
    public async Task MaskAutoFalseHidesListedTextOnly() => await Assert.That(SecretMaskCases.MaskAutoFalseHidesListedTextOnly()).IsTrue();

    [Test]
    public async Task MaskingTwiceChangesNothing() => await Assert.That(SecretMaskCases.MaskingTwiceChangesNothing()).IsTrue();

    [Test]
    public async Task PipeStreamsAreMaskedSeparately() => await Assert.That(SecretMaskCases.PipeStreamsAreMaskedSeparately()).IsTrue();

    [Test]
    public async Task SummaryCountsByKind() => await Assert.That(SecretMaskCases.SummaryCountsByKind()).IsTrue();

    [Test]
    public async Task OneTokenIsOneSecret() => await Assert.That(SecretMaskCases.OneTokenIsOneSecret()).IsTrue();

    [Test]
    public async Task SecretKeyNames() => await Assert.That(SecretMaskCases.SecretKeyNames()).IsTrue();

    [Test]
    public async Task SettingsKeysParse() => await Assert.That(SecretMaskCases.SettingsKeysParse()).IsTrue();

    [Test]
    public async Task PersonalDetailsAreHiddenAsWords() => await Assert.That(SecretMaskCases.PersonalDetailsAreHiddenAsWords()).IsTrue();

    [Test]
    public async Task PublishedNamesStay() => await Assert.That(SecretMaskCases.PublishedNamesStay()).IsTrue();

    [Test]
    public async Task GenericAndShortNamesStay() => await Assert.That(SecretMaskCases.GenericAndShortNamesStay()).IsTrue();

    [Test]
    public async Task MaskPersonalFalseShowsThem() => await Assert.That(SecretMaskCases.MaskPersonalFalseShowsThem()).IsTrue();

    [Test]
    public async Task PersonalSummaryIsItsOwnLine() => await Assert.That(SecretMaskCases.PersonalSummaryIsItsOwnLine()).IsTrue();

    [Test]
    public async Task PersonalDetailsInAPtyStream() => await Assert.That(SecretMaskCases.PersonalDetailsInAPtyStream()).IsTrue();

    [Test]
    public async Task RecordingHidesCommandCommentAndOutput() => await Assert.That(SecretMaskCases.RecordingHidesCommandCommentAndOutput()).IsTrue();

    [Test]
    public async Task ReplayTypesTheRunAsRecorded() => await Assert.That(SecretMaskCases.ReplayTypesTheRunAsRecorded()).IsTrue();

    [Test]
    public async Task HighlightsStayOutOfEscapeSequences() => await Assert.That(SecretMaskCases.HighlightsStayOutOfEscapeSequences()).IsTrue();

    [Test]
    public async Task HighlightInsideATitleLeavesNoSecretInTheCast() => await Assert.That(SecretMaskCases.HighlightInsideATitleLeavesNoSecretInTheCast()).IsTrue();

    [Test]
    public async Task StreamedHighlightLeavesTitlesWhole() => await Assert.That(SecretMaskCases.StreamedHighlightLeavesTitlesWhole()).IsTrue();
}

internal static class SecretMaskCases
{

    internal static bool Expect(bool condition, string message)
    {
        if (!condition)
            Console.Error.WriteLine($"  {message}");
        return condition;
    }

    internal static bool Same(string label, string expected, string actual) =>
        Expect(expected == actual, $"{label}: expected {Show(expected)}, got {Show(actual)}");

    internal static string Show(string text) => "\"" + text.Replace("\u001b", "\\e").Replace("\r", "\\r").Replace("\n", "\\n") + "\"";

    internal static string Stars(int count) => new('*', count);

    internal static string Repeat(string text, int length)
    {
        var sb = new StringBuilder();
        while (sb.Length < length)
            sb.Append(text);
        return sb.ToString(0, length);
    }

    internal static SecretMasker Masker(string? yamlSettings = null, IEnumerable<KeyValuePair<string, string?>>? environment = null) =>
        SecretMasker.ForScenario(ParseScenario($"settings:\n{yamlSettings ?? "  prompt: \"$ \""}\nsteps:\n  - echo hi\n"), environment)
            ?? throw new InvalidOperationException("no masker");

    internal static string MaskPty(SecretMasker masker, params string[] chunks)
    {
        var input = chunks.Select((text, i) => new TimedBytes(TimeSpan.FromMilliseconds(i), Encoding.UTF8.GetBytes(text))).ToArray();
        var output = masker.MaskPty(input);
        return string.Join("|", output.Select(static chunk => Encoding.UTF8.GetString(chunk.Data.Span)));
    }

    internal static bool ListedTextIsHidden()
    {
        var masker = Masker("  mask:\n    - \"s3cret value\"");
        return Same("listed", "the value is ************.\n", masker.MaskText("the value is s3cret value.\n"));
    }

    internal static bool ListedTextSplitAcrossChunks()
    {
        var masker = Masker("  mask:\n    - abcdef");
        // Each chunk keeps its place: the stars stay in the chunk that held the characters.
        return Same("split", "x***|***y", MaskPty(masker, "xabc", "defy"));
    }

    internal static bool ColorCodesDoNotSplitASecret()
    {
        var token = "gh" + "p_" + Repeat("aB3", 36);
        var colored = token[..10] + "\u001b[1m" + token[10..] + "\u001b[0m";
        var expected = "ghp_" + Stars(6) + "\u001b[1m" + Stars(30) + "\u001b[0m";
        return Same("colored", expected, Masker().MaskText(colored));
    }

    internal static bool CursorMovesSeparateText()
    {
        // A full-screen program draws "abc" and "def" in different places: they are not one word.
        var masker = Masker("  mask:\n    - abcdef");
        return Same("cursor", "abc\u001b[5;1Hdef", masker.MaskText("abc\u001b[5;1Hdef"));
    }

    internal static bool ConPtyCursorForwardIsASpace()
    {
        // ConPTY writes `TOKEN: value` with the space as a cursor move.
        return Same("cuf", "TOKEN:\u001b[1C" + Stars(10), Masker().MaskText("TOKEN:\u001b[1Cvalue12345"));
    }

    // The secret follows a control string that ends early: the cast filter, the SVG renderer, or the asciinema player ends
    // it there, so the text after it is shown. Each case has a later terminator or none at all, so a masker that waits for
    // the string's ST misses the secret.
    internal static (string Label, string Text)[] CutOffStrings() =>
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
    internal static (string Label, string Text)[] UnendedDataStrings() =>
    [
        ("DCS", "d \u001bPq hunter2secret\n"),
    ("APC", "\u001b_Ga=T hunter2secret\n"),
    ("color OSC", "\u001b]4;1;rgb:0/0/0 hunter2secret\n"),
    ("DCS with an ESC at the end", "d \u001bPq hunter2secret\u001b"),
];

    internal static bool TextAfterACutOffStringIsFound()
    {
        var ok = true;
        foreach (var (label, text) in CutOffStrings())
            ok &= Same(label, text.Replace("hunter2secret", Stars(13)), Masker("  mask:\n    - hunter2secret").MaskText(text));
        return ok;
    }

    internal static bool UnendedDataStringIsReadAsText()
    {
        var ok = true;
        foreach (var (label, text) in UnendedDataStrings())
            ok &= Same(label, text.Replace("hunter2secret", Stars(13)), Masker("  mask:\n    - hunter2secret").MaskText(text));
        return ok;
    }

    internal static bool TextJoinsWhereTheTerminalJoinsIt()
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

    internal static bool StringsTheCastHidesStay()
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

    internal static bool CutOffStringsLeaveNoSecretInTheCast()
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

    internal static bool StreamedPipeCutsFollowTheCast()
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

    internal static bool StreamedStepLeavesNoSecretInTheCast()
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
    internal static string Shown(string cast) =>
        System.Text.RegularExpressions.Regex.Replace(cast, "\u001b\\[[0-?]*[ -/]*[@-~]|\u001b[PX^_][^\u001b]*(\u001b\\\\)?|[\u0000-\u0009\u000b-\u001f]", "");

    // The cast of a one-step scenario that masks "hunter2secret", after the cast filter.
    internal static string RecordOneStep(string step, CommandExecution execution)
    {
        var scenario = ParseScenario("settings:\n  mask:\n    - hunter2secret\nsteps:\n" + step);
        var options = new GenerateOptions { LogRunning = false, Masker = SecretMasker.ForScenario(scenario, null) };
        var (events, _) = GenerateAsync(scenario, options, _ => Task.FromResult(execution)).GetAwaiter().GetResult();
        return string.Concat(CastOscFilter.RemovePrivateSequences(events).Select(static ev => ev.HasUtf8Output ? Encoding.UTF8.GetString(ev.Utf8Output.Span) : ev.Data));
    }

    internal static bool WideCharactersKeepTheirWidth()
    {
        var masker = Masker("  mask:\n    - 秘密の値");
        return Same("wide", "鍵は********です", masker.MaskText("鍵は秘密の値です"));
    }

    internal static bool KnownTokensKeepTheirPrefix()
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

    internal static bool TokenLookalikesStay()
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

    internal static bool SecretPairsHideTheValue()
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

    internal static bool OtherPairsStay()
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

    internal static bool UrlPasswordIsHidden()
    {
        var masker = Masker();
        return Same("url", "DATABASE_URL=postgres://app:" + Stars(7) + "@db:5432/app", masker.MaskText("DATABASE_URL=postgres://app:hunter2@db:5432/app"))
            & Same("no password", "https://example.com:8080/path@x", masker.MaskText("https://example.com:8080/path@x"))
            & Same("ssh", "git@github.com:org/repo.git", masker.MaskText("git@github.com:org/repo.git"));
    }

    internal static bool PrivateKeyBodyIsHidden()
    {
        var begin = "-----BEGIN OPENSSH " + "PRIVATE KEY-----";
        var end = "-----END OPENSSH " + "PRIVATE KEY-----";
        var input = $"{begin}\r\nb3BlbnNzaC1rZXk\r\nAAAAABG5vbmU=\r\n{end}\r\n";
        var expected = $"{begin}\r\n{Stars(15)}\r\n{Stars(13)}\r\n{end}\r\n";
        return Same("pem", expected, Masker().MaskText(input));
    }

    internal static bool JsonWebTokenKeepsItsHeader()
    {
        var header = "ey" + "JhbGciOiJIUzI1NiJ9";
        var payload = "ey" + "JzdWIiOiIxMjM0NTY3ODkwIn0";
        var signature = "dozjgNryP4J3jVmNHl0w5N_XgL0n3I9PlFUP0THsR8U";
        var token = $"{header}.{payload}.{signature}";
        return Same("jwt", $"id={header}.{Stars(payload.Length + 1 + signature.Length)} ok", Masker().MaskText($"id={token} ok"));
    }

    internal static bool SecretEnvironmentValuesAreHidden()
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

    internal static bool MaskAutoFalseHidesListedTextOnly()
    {
        var masker = Masker("  mask:\n    - hello\n  mask-auto: false", new Dictionary<string, string?> { ["API_TOKEN"] = "zzzzzzzzzz" });
        return Same("listed", "*****", masker.MaskText("hello"))
            & Same("auto off", "API_KEY=abcd1234", masker.MaskText("API_KEY=abcd1234"))
            & Same("environment off", "zzzzzzzzzz", masker.MaskText("zzzzzzzzzz"))
            & Expect(SecretMasker.ForScenario(ParseScenario("settings:\n  mask-auto: false\nsteps:\n  - echo hi\n"), null) is null,
                "mask-auto: false without mask texts gives no masker");
    }

    internal static bool MaskingTwiceChangesNothing()
    {
        var masker = Masker();
        var chunks = new[] { new TimedBytes(TimeSpan.Zero, Encoding.UTF8.GetBytes("API_KEY=abcd1234\r\n")) };
        var once = masker.MaskPty(chunks);
        var twice = Masker().MaskPty(once);
        return Expect(!ReferenceEquals(once, chunks), "the first pass masks")
            & Expect(ReferenceEquals(once, twice), "the second pass returns the same chunks");
    }

    internal static bool PipeStreamsAreMaskedSeparately()
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

    internal static bool SummaryCountsByKind()
    {
        // A text counts once however often it shows: "hello" twice is one secret.
        var masker = Masker("  mask:\n    - hello");
        masker.MaskText("hello API_KEY=abcd1234 and API_TOKEN=efgh5678 hello");
        return Same("summary", "Masked 3 secrets: 2 key=value secrets, 1 settings.mask", masker.Summary() ?? "(none)")
            & Expect(Masker().Summary() is null, "nothing hidden gives no summary");
    }

    internal static bool OneTokenIsOneSecret()
    {
        // The GitHub token finder hides what follows ghp_, and the key=value finder the whole value: one secret, of the
        // kind found first. The command that prints it shows it once more, and it is still one.
        var token = "gh" + "p_" + Repeat("aB3", 36);
        var masker = Masker();
        var typed = masker.MaskText($"echo \"GITHUB_TOKEN={token}\"");
        var printed = MaskPty(masker, $"GITHUB_TOKEN={token}\r\n");
        return Same("typed", $"echo \"GITHUB_TOKEN={Stars(40)}\"", typed)
            & Same("printed", $"GITHUB_TOKEN={Stars(40)}\r\n", printed)
            & Same("summary", "Masked 1 secret: 1 GitHub token", masker.Summary() ?? "(none)");
    }

    internal static bool SecretKeyNames()
    {
        var ok = true;
        foreach (var name in new[] { "GITHUB_TOKEN", "TOKEN", "db_password", "clientSecret", "x-api-key", "AWS_SECRET_ACCESS_KEY", "AccountKey", "APIKey", "--password", "Authorization", "MYSQL_ROOT_PASSWORD" })
            ok &= Expect(SecretMasker.IsSecretKey(name), $"{name} names a secret");
        foreach (var name in new[] { "token_endpoint", "PASSWORD_FILE", "PUBLIC_KEY", "KEY", "secretKeyRef", "max_tokens", "TOKENIZERS_PARALLELISM", "author", "SSH_AUTH_SOCK", "PWD" })
            ok &= Expect(!SecretMasker.IsSecretKey(name), $"{name} does not name a secret");
        return ok;
    }

    internal static bool SettingsKeysParse()
    {
        var scenario = ParseScenario("settings:\n  mask:\n    - one\n    - two\n  mask-auto: false\nsteps:\n  - echo hi\n");
        var unknown = FindUnknownScenarioKeys("settings:\n  mask: [one]\n  mask-auto: true\nsteps:\n  - echo hi\n");
        var personal = ParseScenario("settings:\n  mask-personal: false\nsteps:\n  - echo hi\n");
        var personalUnknown = FindUnknownScenarioKeys("settings:\n  mask-personal: true\nsteps:\n  - echo hi\n");
        return Expect(scenario.Settings?.Mask is ["one", "two"], "mask is read as a list")
            & Expect(scenario.Settings?.MaskAuto == false, "mask-auto is read")
            & Expect(unknown.Count == 0, $"no unknown keys: {string.Join("; ", unknown)}")
            & Expect(personal.Settings?.MaskPersonal == false, "mask-personal is read")
            & Expect(personalUnknown.Count == 0, $"mask-personal is no unknown key: {string.Join("; ", personalUnknown)}")
            & Same("type error", "'settings.mask' must be a list, not 'abc'.", FindValueTypeError("settings:\n  mask: abc\nsteps:\n  - echo hi\n") ?? "(none)");
    }

    // The details of a made-up person, as PersonalDetails.Collect would give them on their machine.
    internal static PersonalDetail[] AdaDetails() =>
    [
        new("adal", PersonalDetail.UserName),
        new("adal", PersonalDetail.HomeFolderName),
        new("Adas-MacBook-Pro", PersonalDetail.ComputerName),
        new("Ada Lovelace", PersonalDetail.GitName),
        new("ada+adal@example.com", PersonalDetail.GitEmail),
    ];

    internal static SecretMasker PersonalMasker(string yamlSettings = "  prompt: \"$ \"") =>
        SecretMasker.ForScenario(ParseScenario($"settings:\n{yamlSettings}\nsteps:\n  - echo hi\n"), null, AdaDetails())
            ?? throw new InvalidOperationException("no masker");

    internal static bool PersonalDetailsAreHiddenAsWords()
    {
        var masker = PersonalMasker();
        string Mask(string text) => masker.MaskText(text);
        return Same("unix home", "/home/****/app", Mask("/home/adal/app"))
            & Same("macOS home", "/Users/****", Mask("/Users/adal"))
            & Same("Windows home, other case", @"C:\Users\****\AppData", Mask(@"C:\Users\Adal\AppData"))
            & Same("Git Bash home", "/c/Users/****/src", Mask("/c/Users/ADAL/src"))
            & Same("JSON home", @"""C:\\Users\\****\\x""", Mask(@"""C:\\Users\\adal\\x"""))
            & Same("ls -l", "-rw-r--r--  1 ****  staff  3 a.txt", Mask("-rw-r--r--  1 adal  staff  3 a.txt"))
            & Same("prompt of a child shell", "****@****************:~$", Mask("adal@Adas-MacBook-Pro:~$"))
            & Same("computer name, other case", "****************.local", Mask("adas-macbook-pro.local"))
            & Same("id", "uid=501(****) gid=20(staff)", Mask("uid=501(adal) gid=20(staff)"))
            & Same("git log", "Author: ************ <********************>", Mask("Author: Ada Lovelace <ada+adal@example.com>"))
            & Same("an email with the user name", "****@other.example", Mask("adal@other.example"))
            & Same("a path that is not the home", @"D:\src\****\app", Mask(@"D:\src\adal\app"))
            & Same("inside longer words", "adalbert adal-work adal_x xadal", Mask("adalbert adal-work adal_x xadal"));
    }

    internal static bool PublishedNamesStay()
    {
        var masker = PersonalMasker();
        string Mask(string text) => masker.MaskText(text);
        return Same("web address", "https://github.com/adal/tool", Mask("https://github.com/adal/tool"))
            & Same("address without a scheme", "see github.com/adal/tool", Mask("see github.com/adal/tool"))
            & Same("ssh remote", "git@github.com:adal/tool.git", Mask("git@github.com:adal/tool.git"))
            & Same("package scope", "npm i @adal/tool", Mask("npm i @adal/tool"))
            & Same("handle", "thanks @adal!", Mask("thanks @adal!"))
            & Same("markdown link", "[x](https://github.com/adal)", Mask("[x](https://github.com/adal)"))
            & Same("colored address", "\u001b[35mhttps://github.com/adal\u001b[0m", Mask("\u001b[35mhttps://github.com/adal\u001b[0m"))
            // A computer name in an address is not a published name, and neither is an email.
            & Same("computer name in an address", "http://****************:3000", Mask("http://Adas-MacBook-Pro:3000"))
            & Same("email in an address", "mailto:********************", Mask("mailto:ada+adal@example.com"))
            // A path is not an address, also when it starts like one.
            & Same("relative path", "./****/x ../****/y ~/****", Mask("./adal/x ../adal/y ~/adal"))
            & Same("drive path with slashes", "C:/Users/****", Mask("C:/Users/adal"));
    }

    internal static bool GenericAndShortNamesStay()
    {
        PersonalDetail[] details =
        [
            new("runner", PersonalDetail.UserName),
            new("root", PersonalDetail.HomeFolderName),
            new("localhost", PersonalDetail.ComputerName),
            new("dev", PersonalDetail.ComputerName),
            new("jo", PersonalDetail.UserName),
            new("1001", PersonalDetail.UserName),
            new("Demo", PersonalDetail.GitName),
        ];
        var scenario = ParseScenario("settings:\n  mask-auto: false\nsteps:\n  - echo hi\n");
        var text = "/home/runner/work /root jo runs npm run dev on localhost as 1001, Demo";
        var withEmail = SecretMasker.ForScenario(scenario, null, [.. details, new PersonalDetail("dev@x.io", PersonalDetail.GitEmail)])!;
        return Expect(SecretMasker.ForScenario(scenario, null, details) is null, "nothing worth hiding gives no masker")
            & Same("an email is always hidden", "write to ********", withEmail.MaskText("write to dev@x.io"))
            & Same("the names stay", text, withEmail.MaskText(text));
    }

    internal static bool MaskPersonalFalseShowsThem()
    {
        var text = "/home/adal Ada Lovelace";
        var off = PersonalMasker("  mask-personal: false");
        var none = SecretMasker.ForScenario(ParseScenario("settings:\n  mask-personal: false\n  mask-auto: false\nsteps:\n  - echo hi\n"), null, AdaDetails());
        // mask-personal is separate from mask-auto: turning off the token finders keeps the personal details hidden.
        var autoOff = PersonalMasker("  mask-auto: false");
        return Same("mask-personal: false", text, off.MaskText(text))
            & Expect(off.PersonalSummary() is null, "no personal summary")
            & Expect(none is null, "nothing left to hide gives no masker")
            & Same("mask-auto: false", "/home/**** ************", autoOff.MaskText(text));
    }

    internal static bool PersonalSummaryIsItsOwnLine()
    {
        var masker = PersonalMasker();
        // The email holds the user name: it is hidden and named as the email.
        masker.MaskText("API_KEY=abcd1234 ada+adal@example.com /home/adal ADAL Ada Lovelace");
        var emailOnly = PersonalMasker();
        emailOnly.MaskText("by ada+adal@example.com");
        return Same("secrets", "Masked 1 secret: 1 key=value secret", masker.Summary() ?? "(none)")
            & Same("personal", "Masked personal details: user name, git name, git email", masker.PersonalSummary() ?? "(none)")
            & Same("email holding the user name", "Masked personal details: git email", emailOnly.PersonalSummary() ?? "(none)")
            & Expect(emailOnly.Summary() is null, "no secret summary");
    }

    internal static bool PersonalDetailsInAPtyStream()
    {
        // Split across reads and colored in parts, as a prompt or ls --color writes it.
        var masker = PersonalMasker();
        return Same("split and colored", "\u001b[1m**|**\u001b[0m@****************\r\n",
            MaskPty(masker, "\u001b[1mad", "al\u001b[0m@Adas-MacBook-Pro\r\n"));
    }

    internal static bool RecordingHidesCommandCommentAndOutput()
    {
        var scenario = ParseScenario(
            "settings:\n  mask:\n    - hunter2x\nsteps:\n  - name: log in with hunter2x\n    run: login --password hunter2x\n");
        var recorded = new List<RecordedStep>();
        var masker = SecretMasker.ForScenario(scenario, null);
        var options = new GenerateOptions { LogRunning = false, Record = recorded, Masker = masker };
        var (events, _) = GenerateAsync(scenario, options, step => Task.FromResult(CommandExecution.FromPipeStream(
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

    internal static bool ReplayTypesTheRunAsRecorded()
    {
        // A hidden step exports the secret halfway, as pre or a step can: the recording knows it only from then on, and a
        // replay's masker never does, so only the outputs file can tell the replay what the recording typed. The same run
        // text before and after the export makes the replay pick the recorded step it is about to take.
        var secret = "exported" + "secret9";
        var steps = $"  - echo {secret}\n  - run: export DEMO_API_TOKEN\n    show: none\n  - echo {secret}\n  - run: echo {secret} again\n    pty: true\n  - run: echo {secret} too\n    pty: true\n    interactive: true\n";
        var yaml = "steps:\n" + steps;
        var scenario = ParseScenario(yaml);
        var recorded = new List<RecordedStep>();
        var recordingMasker = SecretMasker.ForScenario(scenario, null)!;
        var (recording, _) = GenerateAsync(scenario, new GenerateOptions { LogRunning = false, Record = recorded, Masker = recordingMasker }, step =>
        {
            if (step.Show == StepShow.None)
            {
                recordingMasker.Learn([new KeyValuePair<string, string?>("DEMO_API_TOKEN", secret)]);
                return Task.FromResult(new CommandExecution(0, false, "", "", null, null, []));
            }

            var output = Encoding.UTF8.GetBytes("fetched\r\n");
            return Task.FromResult(step.UseInteractive
                ? new CommandExecution(0, true, "", "", [new TimedBytes(TimeSpan.Zero, output)], [], null)
                : step.UsePty
                    ? new CommandExecution(0, true, "", "", [new TimedBytes(TimeSpan.Zero, output)], null, null) { PtyLeadingInitRemoved = true }
                    : CommandExecution.FromPipeStream("fetched\n", "", 0, [new PipeStreamChunk(TimeSpan.Zero, false, "fetched\n")]));
        }).GetAwaiter().GetResult();
        var outputs = new StepOutputs { Scenetake = "test", Os = "linux", Shell = "bash", Width = 80, Height = 24, Steps = recorded };

        var replay = new StepOutputsReplay(outputs);
        var (replayed, _) = GenerateAsync(scenario, new GenerateOptions { LogRunning = false, Masker = SecretMasker.ForScenario(scenario, null), Replay = replay }, replay.RunAsync).GetAwaiter().GetResult();
        // The PTY step is a pipe step here: it has no recorded output, but its run text was masked all the same.
        var playgroundCast = PlaygroundText(PlaygroundRender.RenderScenario(yaml.Replace("again\n    pty: true\n", "again\n"), outputs, new PlaygroundOptions()));

        static string Text(List<CastEvent> events) => string.Concat(events.Select(static ev => ev.HasUtf8Output ? Encoding.UTF8.GetString(ev.Utf8Output.Span) : ev.Data));
        var recordingText = Text(recording);
        var stars = Stars(secret.Length);
        return Expect(recordingText.Contains($"echo {secret}\r\n", StringComparison.Ordinal) && recordingText.Contains($"echo {stars}\r\n", StringComparison.Ordinal)
                && recordingText.Contains($"echo {stars} again", StringComparison.Ordinal) && recordingText.Contains($"echo {stars} too", StringComparison.Ordinal),
                $"the recording types the secret once, before the export: {Show(recordingText)}")
            & Same("replay", recordingText, Text(replayed))
            & Expect(playgroundCast.Contains($"echo {stars}\r\n", StringComparison.Ordinal) && playgroundCast.Contains($"echo {stars} again", StringComparison.Ordinal)
                && playgroundCast.Contains($"echo {stars} too", StringComparison.Ordinal), $"the playground types the secret: {Show(playgroundCast)}");
    }

    // A playground cast's output events joined: the cast types one character per event.
    internal static string PlaygroundText(PlaygroundResult result) => result.Cast is { } bytes
        ? string.Concat(Encoding.UTF8.GetString(bytes).Split('\n').Skip(1).Where(static line => line.StartsWith('['))
            .Select(static line => System.Text.Json.JsonDocument.Parse(line).RootElement)
            .Where(static ev => ev[1].GetString() == "o").Select(static ev => ev[2].GetString()))
        : result.Error ?? "";

    internal static bool HighlightsStayOutOfEscapeSequences()
    {
        // A highlight paints columns of the output as written, escape sequences included. A color written inside a
        // sequence would end it early, so the change waits for the byte after the sequence.
        const string Red = "\u001b[31m";
        const string Reset = "\u001b[0m";
        static string Paint(string output, string at) => ApplyHighlights(output, [new HighlightSpec(Red, [at])], "cmd");
        var title = "\u001b]0;title API_KEY=abcdefgh\u0007done";
        return Same("range inside a title", title, Paint(title, "1:3-5"))
            & Same("range from inside a title", "\u001b]0;title API_KEY=abcdefgh\u0007" + Red + "done" + Reset, Paint(title, "1:3-"))
            & Same("range from inside a color", "ab\u001b[31m" + Red + "c" + Reset + "d\u001b[0mef", Paint("ab\u001b[31mcd\u001b[0mef", "1:4-8"))
            & Same("title open at the end of the line", Red + "\u001b]0;ti\ntle\u0007" + Reset + "X\nY", Paint("\u001b]0;ti\ntle\u0007X\nY", "1"))
            & Same("title with a C1 control, which the cast drops up to BEL", "\u001b]0;t\u0085secret\u0007" + Red + "done" + Reset, Paint("\u001b]0;t\u0085secret\u0007done", "1:8-"))
            & Same("plain text", "a" + Red + "bc" + Reset + "d\ne" + Red + "fg" + Reset + "h", Paint("abcd\nefgh", "1-2:2-3"));
    }

    internal static bool HighlightInsideATitleLeavesNoSecretInTheCast()
    {
        // The cast filter ends a title at the first ESC, so a color written inside one would show the rest of it, which
        // the masker skipped as part of the title.
        var ok = true;
        foreach (var stream in new[] { "", "    stream-output: true\n" })
        {
            var text = "\u001b]0;title API_KEY=abcdefgh\u0007done\n";
            var cast = RecordOneStep("  - run: printf something\n    highlight:\n      - at: \"1:3-5\"\n        color: red\n" + stream,
                CommandExecution.FromPipeStream(text, "", 0, [new PipeStreamChunk(TimeSpan.Zero, false, text)]));
            ok &= Expect(!cast.Contains("abcdefgh", StringComparison.Ordinal), $"{(stream.Length == 0 ? "instant" : "streamed")}: the cast holds the value: {Show(cast)}");
        }

        return ok;
    }

    internal static bool StreamedHighlightLeavesTitlesWhole()
    {
        // A streamed step's highlighted text is cut back into its reads, and each part closes the color it opened. The
        // first read ends inside a title, so a cut at its end would close the color inside the title and show the rest.
        var reads = new PipeStreamChunk[]
        {
        new(TimeSpan.FromMilliseconds(100), false, "\u001b7\u001b]0;API_KEY=abc"),
        new(TimeSpan.FromMilliseconds(400), false, "defgh\u0007done\n"),
        };
        var cast = RecordOneStep("  - run: printf something\n    stream-output: true\n    highlight:\n      - at: \"1\"\n        color: green\n",
            CommandExecution.FromPipeStream(string.Concat(reads.Select(static read => read.Text)), "", 0, reads));
        return Expect(!cast.Contains("defgh", StringComparison.Ordinal), $"the cast holds the end of the title: {Show(cast)}");
    }
}
