using System.Globalization;
using System.Text;
using Scenetake.TestSupport;

namespace Scenetake.Core.Tests;

public sealed class JsonEscapeTests
{
    [Test]
    public async Task QuotesAndBackslashes() => await Assert.That(JsonEscapeCases.QuotesAndBackslashes()).IsTrue();

    [Test]
    public async Task WhitespaceEscapes() => await Assert.That(JsonEscapeCases.WhitespaceEscapes()).IsTrue();

    [Test]
    public async Task ControlCharUnicode() => await Assert.That(JsonEscapeCases.ControlCharUnicode()).IsTrue();

    [Test]
    public async Task LongAsciiRun() => await Assert.That(JsonEscapeCases.LongAsciiRun()).IsTrue();

    [Test]
    public async Task Utf8Multibyte() => await Assert.That(JsonEscapeCases.Utf8Multibyte()).IsTrue();

    [Test]
    public async Task Utf8InvalidByte() => await Assert.That(JsonEscapeCases.Utf8InvalidByte()).IsTrue();

    [Test]
    public async Task CharAndUtf8Match() => await Assert.That(JsonEscapeCases.CharAndUtf8Match()).IsTrue();

    [Test]
    public async Task MixedAsciiAndEscapes() => await Assert.That(JsonEscapeCases.MixedAsciiAndEscapes()).IsTrue();
}

internal static class JsonEscapeCases
{

    internal static bool QuotesAndBackslashes()
    {
        const string expected = "\"say \\\"hi\\\" \\\\ bye\"";
        return EncodeJsonString("say \"hi\" \\ bye") == expected
            && EncodeJsonUtf8String("say \"hi\" \\ bye"u8) == expected;
    }

    internal static bool WhitespaceEscapes()
    {
        const string expected = "\"a\\nb\\rc\\td\"";
        return EncodeJsonString("a\nb\rc\td") == expected
            && EncodeJsonUtf8String("a\nb\rc\td"u8) == expected;
    }

    internal static bool ControlCharUnicode()
    {
        const string expected = "\"\\u0001\"";
        return EncodeJsonString("\u0001") == expected
            && EncodeJsonUtf8String("\u0001"u8) == expected;
    }

    internal static bool LongAsciiRun()
    {
        var plain = new string('a', 300);
        var expected = "\"" + plain + "\"";
        return EncodeJsonString(plain) == expected
            && EncodeJsonUtf8String(Encoding.UTF8.GetBytes(plain)) == expected;
    }

    internal static bool Utf8Multibyte()
    {
        const string expected = "\"日本語\"";
        return EncodeJsonString("日本語") == expected
            && EncodeJsonUtf8String("日本語"u8) == expected;
    }

    internal static bool Utf8InvalidByte()
    {
        return EncodeJsonUtf8String(new byte[] { 0xFF }) == "\"\\uFFFD\"";
    }

    internal static bool CharAndUtf8Match()
    {
        const string text = "hello\tworld\r\n\"\\\"\u0007";
        return EncodeJsonString(text) == EncodeJsonUtf8String(Encoding.UTF8.GetBytes(text));
    }

    internal static bool MixedAsciiAndEscapes()
    {
        var plain = new string('x', 200) + "\n" + new string('y', 200);
        var expected = "\"" + new string('x', 200) + "\\n" + new string('y', 200) + "\"";
        return EncodeJsonString(plain) == expected
            && EncodeJsonUtf8String(Encoding.UTF8.GetBytes(plain)) == expected;
    }

    internal static string EncodeJsonString(ReadOnlySpan<char> s)
    {
        var sb = new StringBuilder();
        using var sw = new StringWriter(sb, CultureInfo.InvariantCulture);
        JsonEscape.WriteJsonString(sw, s);
        return sb.ToString();
    }

    internal static string EncodeJsonUtf8String(ReadOnlySpan<byte> utf8)
    {
        var sb = new StringBuilder();
        using var sw = new StringWriter(sb, CultureInfo.InvariantCulture);
        JsonEscape.WriteJsonUtf8String(sw, utf8);
        return sb.ToString();
    }
}
