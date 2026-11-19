using System.Buffers;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

/// <summary>
/// Style strings, highlights, name comments, and warnings. Shared by the CLI and the playground.
/// </summary>
internal static partial class ScenarioCast
{
    internal static string WrapWithStylePreserveTrailingNewlines(string text, string styleOpen)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(styleOpen))
            return text;

        var splitAt = text.Length;
        while (splitAt > 0)
        {
            var ch = text[splitAt - 1];
            if (ch is '\n' or '\r')
                splitAt--;
            else
                break;
        }

        if (splitAt == text.Length)
            return string.Concat(styleOpen, text, SgrReset);

        var body = text[..splitAt];
        var trailingNewlines = text[splitAt..];
        return string.Concat(styleOpen, body, SgrReset, trailingNewlines);
    }

    internal static bool ContainsAnsiSgr(string text)
    {
        for (var i = 0; i + 2 < text.Length; i++)
        {
            if (text[i] != '\u001b' || text[i + 1] != '[')
                continue;

            for (var j = i + 2; j < text.Length; j++)
            {
                var ch = text[j];
                if ((uint)(ch - '0') <= 9 || ch == ';')
                    continue;

                if (ch == 'm')
                    return true;

                break;
            }
        }

        return false;
    }

    internal static string NormalizeNewlines(string s)
        => s.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "\r\n");

    internal static bool TryFormatNameComment(string? raw, string cmd, out string coloredLine, out string displayText)
    {
        coloredLine = "";
        displayText = "";
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        var value = raw.Trim();
        var colorOpen = SgrNamed("cyan");
        if (value.StartsWith('['))
        {
            var close = value.IndexOf(']');
            if (close > 1)
            {
                var colorName = value[1..close];
                displayText = value[(close + 1)..].TrimStart();
                if (!TryParseStyle(colorName, out var parsedStyle))
                {
                    Warn("name", cmd, $"unknown color/style '{colorName}'");
                    colorOpen = SgrNamed("cyan");
                }
                else
                {
                    colorOpen = parsedStyle;
                }
            }
            else
            {
                displayText = value;
            }
        }
        else
        {
            displayText = value;
        }

        if (string.IsNullOrWhiteSpace(displayText))
        {
            Warn("name", cmd, "empty name text after color prefix");
            return false;
        }

        coloredLine = string.IsNullOrEmpty(colorOpen)
            ? $"# {displayText}\r\n"
            : $"{colorOpen}# {displayText}{SgrReset}\r\n";
        return true;
    }

    internal static string GetOverrideStyle(Dictionary<string, object?> extra, string key, string fallbackStyle, string scope, string cmd)
        => extra.TryGetValue(key, out var raw)
            ? ParseStyleOrFallback(raw?.ToString(), fallbackStyle, scope, cmd)
            : fallbackStyle;

    internal static bool TryGetStepStyle(Dictionary<string, object?> extra, string key, string scope, string cmd, out string style)
    {
        style = "";
        if (!extra.TryGetValue(key, out var raw))
            return false;

        var value = raw?.ToString();
        if (TryParseStyle(value, out style))
            return true;

        Warn(scope, cmd, $"unknown color/style '{value}'");
        return false;
    }

    internal static string SgrOpen(string codes) => $"\u001b[{codes}m";

    internal static string SgrNamed(string name)
    {
        return TryNamedForegroundCode(name, out var code)
            ? SgrOpen(code.ToString(CultureInfo.InvariantCulture))
            : "";
    }

    internal static List<HighlightSpec>? GetHighlights(Dictionary<string, object?> extra, string cmd)
    {
        if (!extra.TryGetValue("highlight", out var raw) || raw is not List<object?> list || list.Count == 0)
            return null;

        var specs = new List<HighlightSpec>(list.Count);
        foreach (var item in list)
        {
            if (item is not Dictionary<object, object?> map) continue;
            var colorRaw = map.GetValueOrDefault("color")?.ToString();
            if (!TryParseStyle(colorRaw, out var colorOpen))
            {
                Warn("highlight", cmd, $"unknown color/style '{colorRaw}'");
                continue;
            }

            if (string.IsNullOrEmpty(colorOpen))
                continue;

            if (!map.TryGetValue("at", out var atRaw) || atRaw is null)
            {
                Warn("highlight", cmd, "missing 'at'");
                continue;
            }

            var ats = new List<string>();
            if (atRaw is List<object?> atList)
            {
                foreach (var a in atList)
                {
                    var s = a?.ToString();
                    if (!string.IsNullOrWhiteSpace(s)) ats.Add(s);
                }
            }
            else
            {
                var s = atRaw.ToString();
                if (!string.IsNullOrWhiteSpace(s)) ats.Add(s);
            }

            if (ats.Count == 0)
            {
                Warn("highlight", cmd, "missing 'at'");
                continue;
            }

            specs.Add(new HighlightSpec(colorOpen, ats));
        }

        return specs.Count > 0 ? specs : null;
    }

    internal static bool TryNamedForegroundCode(string? name, out int code)
    {
        code = name?.Trim().ToLowerInvariant() switch
        {
            "black" => 30,
            "red" => 31,
            "green" => 32,
            "yellow" => 33,
            "blue" => 34,
            "magenta" => 35,
            "cyan" => 36,
            "white" => 37,
            "bright-black" or "gray" or "grey" => 90,
            "bright-red" => 91,
            "bright-green" => 92,
            "bright-yellow" => 93,
            "bright-blue" => 94,
            "bright-magenta" => 95,
            "bright-cyan" => 96,
            "bright-white" => 97,
            _ => 0,
        };

        return code != 0;
    }

    internal static bool TryParseStyle(string? raw, out string styleOpen)
    {
        styleOpen = "";
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        var text = raw.Trim();
        if (text.Equals("none", StringComparison.OrdinalIgnoreCase) ||
            text.Equals("off", StringComparison.OrdinalIgnoreCase) ||
            text.Equals("default", StringComparison.OrdinalIgnoreCase) ||
            text.Equals("reset", StringComparison.OrdinalIgnoreCase))
        {
            styleOpen = "";
            return true;
        }

        if (TryNamedForegroundCode(text, out var namedCode))
        {
            styleOpen = SgrOpen(namedCode.ToString(CultureInfo.InvariantCulture));
            return true;
        }

        if (TryParseSgrLiteral(text, out var sgrCodes))
        {
            styleOpen = SgrOpen(sgrCodes);
            return true;
        }

        if (TryParseStyleWords(text, out var wordCodes))
        {
            styleOpen = SgrOpen(wordCodes);
            return true;
        }

        return false;
    }

    internal static bool TryParseSgrLiteral(string raw, out string codes)
    {
        codes = "";
        var text = raw.Trim();

        if (text.StartsWith("\\e[", StringComparison.OrdinalIgnoreCase))
            text = text[3..];
        else if (text.StartsWith("\\x1b[", StringComparison.OrdinalIgnoreCase))
            text = text[5..];
        else if (text.StartsWith("\\u001b[", StringComparison.OrdinalIgnoreCase))
            text = text[7..];
        else if (text.Length >= 2 && text[0] == '\u001b' && text[1] == '[')
            text = text[2..];
        else if (text.StartsWith("[", StringComparison.Ordinal))
            text = text[1..];

        if (text.EndsWith("m", StringComparison.OrdinalIgnoreCase))
            text = text[..^1];

        if (text.Length == 0)
            return false;

        if (text[0] == ';' || text[^1] == ';')
            return false;

        var parts = text.Split(';', StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
            return false;

        var normalized = new List<string>(parts.Length);
        for (var i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            if (!int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out var value))
                return false;

            if (value is 38 or 48 && i + 1 < parts.Length && parts[i + 1] == "5")
            {
                if (i + 2 >= parts.Length)
                    return false;

                if (!int.TryParse(parts[i + 2], NumberStyles.None, CultureInfo.InvariantCulture, out var colorIndex))
                    return false;

                if (colorIndex is < 0 or > 255)
                    return false;

                normalized.Add(value.ToString(CultureInfo.InvariantCulture));
                normalized.Add("5");
                normalized.Add(colorIndex.ToString(CultureInfo.InvariantCulture));
                i += 2;
                continue;
            }

            if (value is 38 or 48 && i + 1 < parts.Length && parts[i + 1] == "2")
            {
                if (i + 4 >= parts.Length)
                    return false;

                if (!int.TryParse(parts[i + 2], NumberStyles.None, CultureInfo.InvariantCulture, out var r) ||
                    !int.TryParse(parts[i + 3], NumberStyles.None, CultureInfo.InvariantCulture, out var g) ||
                    !int.TryParse(parts[i + 4], NumberStyles.None, CultureInfo.InvariantCulture, out var b))
                {
                    return false;
                }

                if (r is < 0 or > 255 || g is < 0 or > 255 || b is < 0 or > 255)
                    return false;

                normalized.Add(value.ToString(CultureInfo.InvariantCulture));
                normalized.Add("2");
                normalized.Add(r.ToString(CultureInfo.InvariantCulture));
                normalized.Add(g.ToString(CultureInfo.InvariantCulture));
                normalized.Add(b.ToString(CultureInfo.InvariantCulture));
                i += 4;
                continue;
            }

            if (value is < 0 or > 107)
                return false;

            normalized.Add(value.ToString(CultureInfo.InvariantCulture));
        }

        codes = string.Join(';', normalized);
        return true;
    }

    internal static bool TryParseStyleWords(string raw, out string codes)
    {
        codes = "";
        var tokens = raw
            .Split([' ', ',', '+'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(static t => t.Trim().ToLowerInvariant())
            .ToArray();

        if (tokens.Length == 0)
            return false;

        var bold = false;
        var underline = false;
        var intensityRequested = false;
        string? fgCodes = null;
        string? bgCodes = null;
        int simpleFgCode = 0;

        foreach (var token in tokens)
        {
            if (token is "bold")
            {
                bold = true;
                continue;
            }

            if (token is "underline")
            {
                underline = true;
                continue;
            }

            if (token is "bright")
            {
                intensityRequested = true;
                continue;
            }

            if (TryParseFgBgToken(token, out var isBackground, out var colorName))
            {
                if (isBackground)
                {
                    if (!TryParseStyleColor(colorName, isBackground: true, out bgCodes, out _))
                        return false;
                }
                else
                {
                    if (!TryParseStyleColor(colorName, isBackground: false, out fgCodes, out simpleFgCode))
                        return false;
                }

                continue;
            }

            if (TryNamedForegroundCode(token, out var directFg))
            {
                simpleFgCode = directFg;
                fgCodes = directFg.ToString(CultureInfo.InvariantCulture);
                continue;
            }

            return false;
        }

        if (intensityRequested)
        {
            if (simpleFgCode is >= 30 and <= 37)
                fgCodes = (simpleFgCode + 60).ToString(CultureInfo.InvariantCulture);
            else if (fgCodes is null)
                bold = true;
        }

        var list = new List<string>(4);
        if (bold) list.Add("1");
        if (underline) list.Add("4");
        if (fgCodes is not null) list.Add(fgCodes);
        if (bgCodes is not null) list.Add(bgCodes);
        if (list.Count == 0) return false;

        codes = string.Join(';', list);
        return true;
    }

    internal static bool TryParseStyleColor(string raw, bool isBackground, out string codes, out int simpleForegroundCode)
    {
        codes = "";
        simpleForegroundCode = 0;

        if (TryNamedForegroundCode(raw, out var fgCode))
        {
            simpleForegroundCode = isBackground ? 0 : fgCode;
            codes = (isBackground ? fgCode + 10 : fgCode).ToString(CultureInfo.InvariantCulture);
            return true;
        }

        if (TryParseTrueColorValue(raw, out var r, out var g, out var b))
        {
            codes = isBackground
                ? string.Create(CultureInfo.InvariantCulture, $"48;2;{r};{g};{b}")
                : string.Create(CultureInfo.InvariantCulture, $"38;2;{r};{g};{b}");
            return true;
        }

        if (!int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var colorIndex))
            return false;

        if (colorIndex is < 0 or > 255)
            return false;

        codes = isBackground
            ? $"48;5;{colorIndex.ToString(CultureInfo.InvariantCulture)}"
            : $"38;5;{colorIndex.ToString(CultureInfo.InvariantCulture)}";
        return true;
    }

    internal static bool TryParseTrueColorValue(string raw, out int r, out int g, out int b)
    {
        r = g = b = 0;
        var text = raw.Trim();
        if (text.Length == 0)
            return false;

        if (text[0] == '#')
        {
            var hex = text[1..];
            if (hex.Length == 3)
            {
                if (!TryParseHexComponent(hex[0], out var rNibble) ||
                    !TryParseHexComponent(hex[1], out var gNibble) ||
                    !TryParseHexComponent(hex[2], out var bNibble))
                {
                    return false;
                }

                r = rNibble * 17;
                g = gNibble * 17;
                b = bNibble * 17;
                return true;
            }

            if (hex.Length != 6)
                return false;

            if (!TryParseHexPair(hex[0], hex[1], out r) ||
                !TryParseHexPair(hex[2], hex[3], out g) ||
                !TryParseHexPair(hex[4], hex[5], out b))
            {
                return false;
            }

            return true;
        }

        var parts = text.Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length != 3)
            return false;

        if (!int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out r) ||
            !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out g) ||
            !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out b))
        {
            return false;
        }

        return r is >= 0 and <= 255 &&
               g is >= 0 and <= 255 &&
               b is >= 0 and <= 255;
    }

    internal static bool TryParseHexPair(char hi, char lo, out int value)
    {
        value = 0;
        if (!TryParseHexComponent(hi, out var high) || !TryParseHexComponent(lo, out var low))
            return false;

        value = high * 16 + low;
        return true;
    }

    internal static bool TryParseHexComponent(char c, out int value)
    {
        if (c is >= '0' and <= '9')
        {
            value = c - '0';
            return true;
        }

        if (c is >= 'a' and <= 'f')
        {
            value = c - 'a' + 10;
            return true;
        }

        if (c is >= 'A' and <= 'F')
        {
            value = c - 'A' + 10;
            return true;
        }

        value = 0;
        return false;
    }

    internal static bool TryParseFgBgToken(string token, out bool isBackground, out string colorName)
    {
        isBackground = false;
        colorName = "";
        if (token.StartsWith("fg:", StringComparison.Ordinal))
        {
            var value = token[3..].Trim();
            if (value.Length == 0)
                return false;

            isBackground = false;
            colorName = value;
            return true;
        }

        if (token.StartsWith("bg:", StringComparison.Ordinal))
        {
            var value = token[3..].Trim();
            if (value.Length == 0)
                return false;

            isBackground = true;
            colorName = value;
            return true;
        }

        return false;
    }

    internal static string ApplyHighlights(string output, List<HighlightSpec> specs, string cmd)
    {
        var text = NormalizeToLf(output);
        var lineCount = CountLines(text.AsSpan());
        var lineRangesArray = ArrayPool<int>.Shared.Rent(lineCount * 2);

        try
        {
            var lineRanges = lineRangesArray.AsSpan(0, lineCount * 2);
            FillLineRanges(text.AsSpan(), lineRanges);

            var paint = new ushort[lineCount][];
            var styleIds = new Dictionary<string, ushort>(StringComparer.Ordinal);
            var stylesList = new List<string> { "" };

            foreach (var spec in specs)
            {
                var styleId = GetOrAddStyleId(styleIds, stylesList, spec.ColorOpen);
                foreach (var at in spec.At)
                    ApplyAt(lineRanges, paint, at, styleId, cmd);
            }

            var styles = CollectionsMarshal.AsSpan(stylesList);
            var inside = EscapeSequences.Inside(text);
            var sb = new StringBuilder(text.Length + specs.Count * 16);
            ushort current = 0;
            for (var i = 0; i < lineCount; i++)
            {
                if (i > 0) sb.Append('\n');
                var start = lineRanges[i * 2];
                var length = lineRanges[i * 2 + 1];
                AppendPaintedLine(sb, text.AsSpan(start, length), paint[i], styles, inside.AsSpan(start, length + 1), ref current);
            }

            return sb.ToString();
        }
        finally
        {
            ArrayPool<int>.Shared.Return(lineRangesArray, clearArray: false);
        }
    }

    internal static string NormalizeToLf(string text)
    {
        if (text.IndexOf('\r') < 0)
            return text;

        var sb = new StringBuilder(text.Length);
        var span = text.AsSpan();
        for (var i = 0; i < span.Length; i++)
        {
            var ch = span[i];
            if (ch == '\r')
            {
                sb.Append('\n');
                if (i + 1 < span.Length && span[i + 1] == '\n')
                    i++;
                continue;
            }

            sb.Append(ch);
        }

        return sb.ToString();
    }

    internal static int CountLines(ReadOnlySpan<char> text)
    {
        var count = 1;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
                count++;
        }

        return count;
    }

    internal static void FillLineRanges(ReadOnlySpan<char> text, Span<int> lineRanges)
    {
        var start = 0;
        var line = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != '\n')
                continue;

            lineRanges[line * 2] = start;
            lineRanges[line * 2 + 1] = i - start;
            line++;
            start = i + 1;
        }

        lineRanges[line * 2] = start;
        lineRanges[line * 2 + 1] = text.Length - start;
    }

    internal static void ApplyAt(ReadOnlySpan<int> lines, ushort[][] paint, string at, ushort styleId, string cmd)
    {
        if (!TryParseAt(at, out var lineLo, out var lineHi, out var openLineEnd, out var colLo, out var colHi, out var openColEnd, out var hasCol))
        {
            Warn("highlight", cmd, $"invalid at '{at}'");
            return;
        }

        var lineCount = lines.Length / 2;
        if (lineLo > lineCount)
        {
            Warn("highlight", cmd, $"at '{at}' starts after output");
            return;
        }

        if (openLineEnd)
        {
            lineHi = lineCount;
        }
        else if (lineHi > lineCount)
        {
            Warn("highlight", cmd, $"at '{at}': lines {lineCount + 1}-{lineHi} are missing");
            lineHi = lineCount;
        }

        for (var line = lineLo; line <= lineHi; line++)
        {
            var li = line - 1;
            var contentLength = lines[li * 2 + 1];
            int start, end;
            if (hasCol)
            {
                start = colLo - 1;
                end = openColEnd ? contentLength : colHi;
                if (start >= contentLength)
                {
                    if (lineLo == lineHi)
                        Warn("highlight", cmd, $"at '{at}' starts after output");
                    continue;
                }
                if (end > contentLength) end = contentLength;
            }
            else
            {
                start = 0;
                end = contentLength;
            }

            if (start >= end) continue;
            var row = paint[li] ??= new ushort[contentLength];
            for (var c = start; c < end; c++)
                row[c] = styleId;
        }
    }

    /// <summary>
    /// Appends one line with its painted columns. <paramref name="inside"/> has one more place than the line, for its
    /// line break, and marks the places inside an escape sequence: a style written there would end the sequence early,
    /// so the change waits for the first place after it, on this line or a later one (<paramref name="current"/>).
    /// </summary>
    internal static void AppendPaintedLine(StringBuilder sb, ReadOnlySpan<char> line, ushort[]? row, ReadOnlySpan<string> styles, ReadOnlySpan<bool> inside, ref ushort current)
    {
        if (row is null && current == 0)
        {
            sb.Append(line);
            return;
        }

        for (var i = 0; i < line.Length; i++)
        {
            var next = row is null ? (ushort)0 : row[i];
            if (next != current && !inside[i])
            {
                if (current != 0) sb.Append(SgrReset);
                if (next != 0) sb.Append(styles[next]);
                current = next;
            }

            sb.Append(line[i]);
        }

        if (current != 0 && !inside[line.Length])
        {
            sb.Append(SgrReset);
            current = 0;
        }
    }

    internal static ushort GetOrAddStyleId(Dictionary<string, ushort> styleIds, List<string> styles, string style)
    {
        if (styleIds.TryGetValue(style, out var id))
            return id;

        if (styles.Count >= ushort.MaxValue)
            throw new InvalidOperationException("Too many unique highlight styles in one output.");

        id = (ushort)styles.Count;
        styles.Add(style);
        styleIds[style] = id;
        return id;
    }

    internal static bool TryParseAt(string at, out int lineLo, out int lineHi, out bool openLineEnd, out int colLo, out int colHi, out bool openColEnd, out bool hasCol)
    {
        lineLo = lineHi = colLo = colHi = 0;
        openLineEnd = openColEnd = hasCol = false;
        var span = at.AsSpan().Trim();
        if (span.IsEmpty) return false;

        var colon = span.IndexOf(':');
        if (!TryParseSpan(span[..(colon >= 0 ? colon : span.Length)], allowOpenEnd: true, out lineLo, out lineHi, out openLineEnd))
            return false;

        if (colon < 0) return true;
        hasCol = true;
        return TryParseColSpan(span[(colon + 1)..], out colLo, out colHi, out openColEnd);
    }

    internal static bool TryParseSpan(ReadOnlySpan<char> span, bool allowOpenEnd, out int lo, out int hi, out bool openEnd)
    {
        lo = hi = 0;
        openEnd = false;
        var dash = span.IndexOf('-');
        if (dash < 0)
            return TryPosInt(span, out lo) && (hi = lo) > 0;

        if (allowOpenEnd && dash == span.Length - 1)
        {
            if (!TryPosInt(span[..dash], out lo) || lo < 1)
                return false;

            hi = lo;
            openEnd = true;
            return true;
        }

        if (!TryPosInt(span[..dash], out lo) || !TryPosInt(span[(dash + 1)..], out hi) || lo < 1 || hi < 1)
            return false;

        if (lo > hi) (lo, hi) = (hi, lo);
        return true;
    }

    internal static bool TryParseColSpan(ReadOnlySpan<char> span, out int lo, out int hi, out bool openEnd)
    {
        lo = hi = 0;
        openEnd = false;
        if (span.EndsWith("-"))
        {
            openEnd = true;
            span = span[..^1];
            return TryPosInt(span, out lo) && lo > 0;
        }

        return TryParseSpan(span, allowOpenEnd: false, out lo, out hi, out _);
    }

    internal static bool TryPosInt(ReadOnlySpan<char> span, out int value)
    {
        value = 0;
        if (span.IsEmpty) return false;
        foreach (var ch in span)
        {
            if (ch is < '0' or > '9') return false;
            value = value * 10 + (ch - '0');
        }
        return value > 0;
    }

    internal static string ParseStyleOrFallback(string? raw, string fallbackStyle, string scope, string? cmd = null)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return fallbackStyle;

        if (TryParseStyle(raw, out var style))
            return style;

        Warn(scope, cmd, $"unknown color/style '{raw}'");
        return fallbackStyle;
    }

    internal static void Warn(string scope, string? cmd, string detail)
    {
        if (string.IsNullOrWhiteSpace(cmd))
            Console.Error.WriteLine($"Warning: {scope}: {detail}");
        else
            Console.Error.WriteLine($"Warning: {scope} ({cmd}): {detail}");
    }

    internal static void WarnStepExitCode(string? name, string cmd, int exitCode)
    {
        if (string.IsNullOrWhiteSpace(name))
            Console.Error.WriteLine($"Warning: step exited {exitCode}: {cmd}");
        else
            Console.Error.WriteLine($"Warning: step exited {exitCode} ({name}): {cmd}");
    }
}
