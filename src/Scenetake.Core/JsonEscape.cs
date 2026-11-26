using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.Unicode;

namespace Scenetake.Core;

internal static class JsonEscape
{
    internal const int CharWriteChunk = 256;

    // Written the same from text and from bytes, so a cast read back and written again keeps its bytes.
    const char ReplacementChar = '�';
    const string ReplacementEscape = "\\uFFFD";

    internal static void WriteJsonString(TextWriter writer, ReadOnlySpan<char> s)
    {
        writer.Write('"');
        WriteJsonEscapedChars(writer, s);
        writer.Write('"');
    }

    internal static void WriteJsonUtf8String(TextWriter writer, ReadOnlySpan<byte> utf8)
    {
        writer.Write('"');
        Span<char> esc = stackalloc char[6];
        Span<char> runeChars = stackalloc char[2];
        var index = 0;
        while (index < utf8.Length)
        {
            if (utf8[index] <= 0x7F)
            {
                var c = (char)utf8[index];
                if (NeedsJsonEscapeOrControl(c))
                {
                    TryWriteJsonEscape(writer, esc, c);
                    index++;
                    continue;
                }

                var start = index;
                index++;
                while (index < utf8.Length && utf8[index] <= 0x7F)
                {
                    var d = (char)utf8[index];
                    if (NeedsJsonEscapeOrControl(d))
                        break;
                    index++;
                }

                WriteUnescapedAsciiRun(writer, utf8.Slice(start, index - start));
                continue;
            }

            var status = Rune.DecodeFromUtf8(utf8[index..], out var rune, out var consumed);
            if (status != OperationStatus.Done)
            {
                writer.Write(ReplacementEscape);
                index++;
                continue;
            }

            // Only bytes from 0x80 on get here, so no rune is a control character that needs an escape.
            if (rune.Value == ReplacementChar)
                writer.Write(ReplacementEscape);
            else
            {
                rune.TryEncodeToUtf16(runeChars, out var charCount);
                writer.Write(runeChars[..charCount]);
            }

            index += consumed;
        }

        writer.Write('"');
    }

    internal static string JsonString(ReadOnlySpan<char> s)
    {
        var sb = new StringBuilder(s.Length + 2);
        using (var sw = new StringWriter(sb, CultureInfo.InvariantCulture))
            WriteJsonString(sw, s);
        return sb.ToString();
    }

    static void WriteJsonEscapedChars(TextWriter writer, ReadOnlySpan<char> s)
    {
        Span<char> esc = stackalloc char[6];
        var i = 0;
        while (i < s.Length)
        {
            var c = s[i];
            if (NeedsJsonEscapeOrControl(c))
            {
                TryWriteJsonEscape(writer, esc, c);
                i++;
                continue;
            }

            var start = i;
            i++;
            while (i < s.Length && !NeedsJsonEscapeOrControl(s[i]))
                i++;
            writer.Write(s.Slice(start, i - start));
        }
    }

    static void WriteUnescapedAsciiRun(TextWriter writer, ReadOnlySpan<byte> asciiRun)
    {
        Span<char> chunk = stackalloc char[CharWriteChunk];
        var offset = 0;
        while (offset < asciiRun.Length)
        {
            var take = Math.Min(CharWriteChunk, asciiRun.Length - offset);
            for (var j = 0; j < take; j++)
                chunk[j] = (char)asciiRun[offset + j];
            writer.Write(chunk[..take]);
            offset += take;
        }
    }

    static bool NeedsJsonEscapeOrControl(char c) =>
        c < 0x20 || c is '"' or '\\' or '\b' or '\f' or '\n' or '\r' or '\t' or ReplacementChar;

    static void TryWriteJsonEscape(TextWriter writer, Span<char> esc, char c)
    {
        switch (c)
        {
            case '"': writer.Write("\\\""); break;
            case '\\': writer.Write("\\\\"); break;
            case '\b': writer.Write("\\b"); break;
            case '\f': writer.Write("\\f"); break;
            case '\n': writer.Write("\\n"); break;
            case '\r': writer.Write("\\r"); break;
            case '\t': writer.Write("\\t"); break;
            case ReplacementChar: writer.Write(ReplacementEscape); break;
            default: WriteJsonUnicodeEscape(writer, esc, c); break;
        }
    }

    static void WriteJsonUnicodeEscape(TextWriter writer, Span<char> esc, int codePoint)
    {
        esc[0] = '\\';
        esc[1] = 'u';
        ((uint)codePoint).TryFormat(esc[2..], out _, "x4");
        writer.Write(esc);
    }
}
