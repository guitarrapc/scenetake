#:sdk Microsoft.NET.Sdk
#:property TargetFramework=net10.0
#:property Nullable=enable
#:property ImplicitUsings=enable
#:property AllowUnsafeBlocks=true
#:include ../PipeStreamCapture.cs
#:include ../Kernel32.cs

// PipeOutputDecoder reads a Windows child's redirected output: a line that is valid UTF-8 as UTF-8, any
// other line in the console's legacy code page. The cross-platform cases use Latin-1 as the legacy code
// page; the Windows cases use code page 932 through the OS, as scenetake does on Japanese Windows.
//
// Usage (from repository root):
//   dotnet run tests/pipe_output_decoder_test.cs

using System.Text;

var failures = 0;
failures += Run("Utf8LineStaysUtf8", Utf8LineStaysUtf8);
failures += Run("InvalidUtf8LineUsesLegacyCodePage", InvalidUtf8LineUsesLegacyCodePage);
failures += Run("EachLineChoosesItsOwnDecoding", EachLineChoosesItsOwnDecoding);
failures += Run("Utf8SequenceSplitAcrossReads", Utf8SequenceSplitAcrossReads);
failures += Run("BomAtStartIsSkipped", BomAtStartIsSkipped);
failures += Run("FlushDecodesHeldBytes", FlushDecodesHeldBytes);
failures += Run("OutputFitsOneCharPerByte", OutputFitsOneCharPerByte);
if (OperatingSystem.IsWindows())
{
    failures += Run("Cp932LineDecodesWithTheOs", Cp932LineDecodesWithTheOs);
    failures += Run("Cp932LeadByteSplitAcrossReads", Cp932LeadByteSplitAcrossReads);
}
else
{
    Console.Error.WriteLine("skip Cp932 cases: Windows only");
}

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
        Console.Error.WriteLine($"FAIL {name}: {ex}");
        return 1;
    }
}

// Feeds each read to the decoder as PipeStreamCapture does, then the end of the stream (flush).
static string Decode(ILegacyTextDecoder legacy, params byte[][] reads)
{
    var decoder = new PipeOutputDecoder(legacy);
    var text = new StringBuilder();
    foreach (var read in reads.Append([]))
    {
        var chars = new char[read.Length + PipeOutputDecoder.MaxPendingBytes];
        var count = decoder.GetChars(read, 0, read.Length, chars, 0, flush: read.Length == 0);
        text.Append(chars, 0, count);
    }

    return text.ToString();
}

static byte[] Utf8Bytes(string text) => Encoding.UTF8.GetBytes(text);

static bool Expect(string actual, string expected)
{
    if (actual == expected)
        return true;
    Console.Error.WriteLine($"  expected: {Escape(expected)}{Environment.NewLine}  actual:   {Escape(actual)}");
    return false;

    static string Escape(string text) => text.Replace("\r", "\\r", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal);
}

static bool Utf8LineStaysUtf8() =>
    Expect(Decode(new Latin1Decoder(), Utf8Bytes("bash 絵文字 🎉 ✓\r\n")), "bash 絵文字 🎉 ✓\r\n");

static bool InvalidUtf8LineUsesLegacyCodePage() =>
    // 0xE9 is "é" in Latin-1 and a UTF-8 lead byte without its continuation bytes.
    Expect(Decode(new Latin1Decoder(), [(byte)'c', (byte)'a', (byte)'f', 0xE9, (byte)'\r', (byte)'\n']), "café\r\n");

static bool EachLineChoosesItsOwnDecoding() =>
    // A tool that writes UTF-8 and one that writes the legacy code page in the same step.
    Expect(Decode(new Latin1Decoder(), [.. Utf8Bytes("git 報告書.txt\n"), (byte)'p', 0xE9, (byte)'\n', .. Utf8Bytes("ok\n")]), "git 報告書.txt\npé\nok\n");

static bool Utf8SequenceSplitAcrossReads()
{
    // The 4 bytes of an emoji arrive in two reads. The first read returns nothing for them and holds them.
    var emoji = Utf8Bytes("🎉");
    var decoder = new PipeOutputDecoder(new Latin1Decoder());
    var chars = new char[8];
    var first = decoder.GetChars([(byte)'a', emoji[0], emoji[1]], 0, 3, chars, 0, flush: false);
    var firstText = new string(chars, 0, first);
    var second = decoder.GetChars([emoji[2], emoji[3], (byte)'\n'], 0, 3, chars, 0, flush: false);
    return Expect(firstText, "a") && Expect(new string(chars, 0, second), "🎉\n");
}

static bool BomAtStartIsSkipped() =>
    // The StreamReader this replaces skipped a BOM at the start of the stream, also when it arrived in pieces.
    Expect(Decode(new Latin1Decoder(), [0xEF, 0xBB, 0xBF, (byte)'a', (byte)'\n']), "a\n")
    && Expect(Decode(new Latin1Decoder(), [0xEF], [0xBB, 0xBF, (byte)'b']), "b")
    && Expect(Decode(new Latin1Decoder(), Utf8Bytes("x"), [0xEF, 0xBB, 0xBF]), "x﻿");

static bool FlushDecodesHeldBytes() =>
    // Bytes held for a character that never finishes come out at the end of the stream instead of being
    // lost: here they are not UTF-8, so the legacy code page reads them.
    Expect(Decode(new Latin1Decoder(), [(byte)'z', 0xE7, 0xB5]), "zçµ");

static bool OutputFitsOneCharPerByte()
{
    // PipeStreamCapture sizes its char buffer as the read plus MaxPendingBytes. Random bytes in random
    // reads must always fit.
    var random = new Random(1234);
    for (var round = 0; round < 200; round++)
    {
        var decoder = new PipeOutputDecoder(new Latin1Decoder());
        for (var i = 0; i < 20; i++)
        {
            var read = new byte[random.Next(0, 9)];
            random.NextBytes(read);
            var chars = new char[read.Length + PipeOutputDecoder.MaxPendingBytes];
            decoder.GetChars(read, 0, read.Length, chars, 0, flush: i == 19);
        }
    }

    return true;
}

static bool Cp932LineDecodesWithTheOs() =>
    // What cmd's echo writes on Japanese Windows: "cmd 漢字" in code page 932.
    Expect(Decode(new WindowsCodePageDecoder(932), [.. "cmd "u8, 0x8A, 0xBF, 0x8E, 0x9A, (byte)'\r', (byte)'\n']), "cmd 漢字\r\n");

static bool Cp932LeadByteSplitAcrossReads() =>
    // A read ends between the lead byte and the trail byte of "字" (0x8E 0x9A), and the trail byte of "漢"
    // (0xBF) has the value of no lead byte while 0x8E does, so only a walk from the line start finds the split.
    Expect(Decode(new WindowsCodePageDecoder(932), [0x8A, 0xBF, 0x8E], [0x9A, (byte)'\n']), "漢字\n");

sealed class Latin1Decoder : ILegacyTextDecoder
{
    public int Decode(ReadOnlySpan<byte> bytes, bool final, StringBuilder text)
    {
        text.Append(Encoding.Latin1.GetString(bytes));
        return bytes.Length;
    }
}
