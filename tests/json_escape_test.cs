#:sdk Microsoft.NET.Sdk
#:property TargetFramework=net11.0
#:property Nullable=enable
#:property ImplicitUsings=enable
#:include ../JsonEscape.cs

using System.Globalization;
using System.Text;

var failures = 0;
failures += Run("QuotesAndBackslashes", QuotesAndBackslashes);
failures += Run("WhitespaceEscapes", WhitespaceEscapes);
failures += Run("ControlCharUnicode", ControlCharUnicode);
failures += Run("LongAsciiRun", LongAsciiRun);
failures += Run("Utf8Multibyte", Utf8Multibyte);
failures += Run("Utf8InvalidByte", Utf8InvalidByte);
failures += Run("CharAndUtf8Match", CharAndUtf8Match);
failures += Run("MixedAsciiAndEscapes", MixedAsciiAndEscapes);

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

static bool QuotesAndBackslashes()
{
    const string expected = "\"say \\\"hi\\\" \\\\ bye\"";
    return EncodeJsonString("say \"hi\" \\ bye") == expected
        && EncodeJsonUtf8String("say \"hi\" \\ bye"u8) == expected;
}

static bool WhitespaceEscapes()
{
    const string expected = "\"a\\nb\\rc\\td\"";
    return EncodeJsonString("a\nb\rc\td") == expected
        && EncodeJsonUtf8String("a\nb\rc\td"u8) == expected;
}

static bool ControlCharUnicode()
{
    const string expected = "\"\\u0001\"";
    return EncodeJsonString("\u0001") == expected
        && EncodeJsonUtf8String("\u0001"u8) == expected;
}

static bool LongAsciiRun()
{
    var plain = new string('a', 300);
    var expected = "\"" + plain + "\"";
    return EncodeJsonString(plain) == expected
        && EncodeJsonUtf8String(Encoding.UTF8.GetBytes(plain)) == expected;
}

static bool Utf8Multibyte()
{
    const string expected = "\"日本語\"";
    return EncodeJsonString("日本語") == expected
        && EncodeJsonUtf8String("日本語"u8) == expected;
}

static bool Utf8InvalidByte()
{
    return EncodeJsonUtf8String(new byte[] { 0xFF }) == "\"\\uFFFD\"";
}

static bool CharAndUtf8Match()
{
    const string text = "hello\tworld\r\n\"\\\"\u0007";
    return EncodeJsonString(text) == EncodeJsonUtf8String(Encoding.UTF8.GetBytes(text));
}

static bool MixedAsciiAndEscapes()
{
    var plain = new string('x', 200) + "\n" + new string('y', 200);
    var expected = "\"" + new string('x', 200) + "\\n" + new string('y', 200) + "\"";
    return EncodeJsonString(plain) == expected
        && EncodeJsonUtf8String(Encoding.UTF8.GetBytes(plain)) == expected;
}

static string EncodeJsonString(ReadOnlySpan<char> s)
{
    var sb = new StringBuilder();
    using var sw = new StringWriter(sb, CultureInfo.InvariantCulture);
    JsonEscape.WriteJsonString(sw, s);
    return sb.ToString();
}

static string EncodeJsonUtf8String(ReadOnlySpan<byte> utf8)
{
    var sb = new StringBuilder();
    using var sw = new StringWriter(sb, CultureInfo.InvariantCulture);
    JsonEscape.WriteJsonUtf8String(sw, utf8);
    return sb.ToString();
}
