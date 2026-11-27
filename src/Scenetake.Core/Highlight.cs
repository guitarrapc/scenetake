using System.Buffers;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace Scenetake.Core;

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

    /// <summary>What <c>{dir}</c> in the prompt becomes: the name of the folder a step starts in.</summary>
    internal const string DirPlaceholder = "{dir}";

    /// <summary>How <c>{dir}</c> shows the home folder, and any folder that is not known, such as in an old outputs file.</summary>
    internal const string HomeFolderName = "~";

    /// <summary>
    /// The prompt as <c>settings.prompt</c> gives it: text, or a list of parts, each text or <c>{text, color}</c>
    /// with a style as <c>highlight</c> takes one. An unknown style is a warning, and its part is drawn plain.
    /// <see cref="FindPromptError"/> checks the value before anything runs: what it refuses becomes the default here.
    /// </summary>
    internal static PromptTemplate ParsePrompt(object? value)
    {
        if (value is null)
            return new([new(DefaultPrompt, "")]);
        if (TextToType(value) is { } text)
            return new([new(text, "")]);
        if (value is not List<object?> list)
            return new([new(DefaultPrompt, "")]);

        var parts = new List<PromptPart>();
        foreach (var item in list)
        {
            if (TextToType(item) is { } partText)
            {
                parts.Add(new(partText, ""));
            }
            else if (item is Dictionary<object, object?> part && TextToType(part.GetValueOrDefault("text")) is { } styledText)
            {
                var style = "";
                if (part.GetValueOrDefault("color") is string color && !TryParseStyle(color, out style))
                {
                    Warn("settings.prompt", null, $"unknown color/style '{color}'");
                    style = "";
                }

                parts.Add(new(styledText, style));
            }
        }

        return new([.. parts]);
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
                    ApplyAt(text, lineRanges, paint, at, styleId, cmd);
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

    /// <summary>
    /// Highlights on a PTY step's output, which keeps its own line breaks: a line ends at a line feed, the carriage
    /// return before it is not one of its columns, and a carriage return alone, as a progress bar writes it, stays in
    /// its line. Columns count the characters as written, escape sequences included, as for a pipe step.
    /// <para>
    /// <paramref name="reads"/> are the output's parts as the cast times them, and each comes back with the styles that
    /// fall in it. A style is not closed at the end of a part, because the terminal that plays the cast keeps it from
    /// one event to the next: the parts joined are the same text however the reads fell, so recording again compares
    /// them as they are. A style is opened again after each color sequence the program writes inside the range, so the
    /// program's own colors and resets do not end the highlight, and the attributes the style does not set stay the
    /// program's. Where the range ends, the program's colors at that point come back after the reset, so the rest of
    /// the line keeps them.
    /// </para>
    /// </summary>
    internal static string[] ApplyPtyHighlights(IReadOnlyList<string> reads, List<HighlightSpec> specs, string cmd)
    {
        var results = new string[reads.Count];
        var text = string.Concat(reads);
        if (text.Length == 0)
        {
            for (var k = 0; k < reads.Count; k++)
                results[k] = reads[k];
            return results;
        }

        var lineCount = CountLines(text.AsSpan());
        var lineRangesArray = ArrayPool<int>.Shared.Rent(lineCount * 2);
        try
        {
            var lineRanges = lineRangesArray.AsSpan(0, lineCount * 2);
            FillLineRanges(text.AsSpan(), lineRanges);
            // Every line but the last ends at a line feed, and a carriage return just before it ends the line too.
            for (var li = 0; li < lineCount - 1; li++)
            {
                var length = lineRanges[li * 2 + 1];
                if (length > 0 && text[lineRanges[li * 2] + length - 1] == '\r')
                    lineRanges[li * 2 + 1] = length - 1;
            }

            var paint = new ushort[lineCount][];
            var styleIds = new Dictionary<string, ushort>(StringComparer.Ordinal);
            var stylesList = new List<string> { "" };
            foreach (var spec in specs)
            {
                var styleId = GetOrAddStyleId(styleIds, stylesList, spec.ColorOpen);
                foreach (var at in spec.At)
                    ApplyAt(text, lineRanges, paint, at, styleId, cmd);
            }

            var styles = CollectionsMarshal.AsSpan(stylesList);
            var inside = EscapeSequences.Inside(text);
            var sb = new StringBuilder(text.Length + specs.Count * 16);
            var read = 0;
            var readEnd = reads[0].Length;
            var line = 0;
            var lineStart = lineRanges[0];
            ushort current = 0;
            // Where the program's last color sequence ends: the highlight opens again there.
            var reopenAt = -1;
            // The program's color sequences since its last reset, which a reset that ends a highlight would undo, as
            // places in the text. Only the last few: a program that colors each character without a reset, as lolcat
            // does, would otherwise have every one of them written again at each place a highlight ends. A ring of start
            // and length pairs, oldest first: a Queue of value tuples was generic code of its own in the binary.
            var programStyle = new ProgramStyles(new int[ProgramStylesKept * 2]);
            for (var i = 0; i < text.Length; i++)
            {
                while (i >= readEnd && read < reads.Count - 1)
                {
                    results[read++] = sb.ToString();
                    sb.Clear();
                    readEnd += reads[read].Length;
                }

                var column = i - lineStart;
                var row = paint[line];
                var next = row is not null && column < lineRanges[line * 2 + 1] ? row[column] : (ushort)0;
                if (!inside[i])
                {
                    if (next != current)
                    {
                        if (current != 0) AppendResetAndProgramStyle(sb, text, programStyle);
                        if (next != 0) sb.Append(styles[next]);
                        current = next;
                    }
                    else if (i == reopenAt && current != 0)
                    {
                        sb.Append(styles[current]);
                    }

                    // The program's sequence counts after the change written before it, which restored the colors
                    // that came before this sequence.
                    if (text[i] == '\u001b')
                    {
                        var end = EscapeSequences.End(text, i);
                        if (HighlightSplitter.IsSgrSequence(text, i, end))
                        {
                            HighlightSplitter.ReadSgr(text, i, end, out var resets, out var sets);
                            if (resets)
                                programStyle.Count = 0;
                            if (sets)
                                programStyle.Add(i, end - i);

                            reopenAt = end;
                        }
                    }
                }

                sb.Append(text[i]);
                if (text[i] == '\n' && line < lineCount - 1)
                {
                    line++;
                    lineStart = lineRanges[line * 2];
                }
            }

            // A style still open at the end, after the last line, which has no line feed to close it before.
            if (current != 0 && !inside[text.Length])
                AppendResetAndProgramStyle(sb, text, programStyle);

            results[read] = sb.ToString();
            for (var k = read + 1; k < reads.Count; k++)
                results[k] = "";
            return results;
        }
        finally
        {
            ArrayPool<int>.Shared.Return(lineRangesArray, clearArray: false);
        }
    }

    /// <summary>
    /// How many of the program's color sequences since its last reset a PTY highlight writes again where it ends. A
    /// program sets a few attributes after a reset, such as bold and a color, and a later color replaces an earlier one.
    /// </summary>
    private const int ProgramStylesKept = 8;

    private static void AppendResetAndProgramStyle(StringBuilder sb, string text, ProgramStyles programStyle)
    {
        sb.Append(SgrReset);
        for (var k = 0; k < programStyle.Count; k++)
        {
            var slot = (programStyle.First + k) % ProgramStylesKept * 2;
            sb.Append(text, programStyle.Places[slot], programStyle.Places[slot + 1]);
        }
    }

    /// <summary>The last <see cref="ProgramStylesKept"/> color sequences of the program, as start and length pairs in <see cref="Places"/>, from <see cref="First"/>.</summary>
    private sealed class ProgramStyles(int[] places)
    {
        internal int[] Places { get; } = places;
        internal int First { get; private set; }
        internal int Count { get; set; }

        /// <summary>Adds a sequence, dropping the oldest when all places are taken.</summary>
        internal void Add(int start, int length)
        {
            if (Count == ProgramStylesKept)
            {
                First = (First + 1) % ProgramStylesKept;
                Count--;
            }

            var slot = (First + Count) % ProgramStylesKept * 2;
            Places[slot] = start;
            Places[slot + 1] = length;
            Count++;
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

    /// <summary>
    /// Paints the places that <paramref name="at"/> names in <paramref name="text"/>, whose lines <paramref name="lines"/>
    /// gives as start and length. Columns count the characters a person sees as one, extended grapheme clusters, as
    /// typing types them: an emoji, a flag, or a letter with its accent is one column, so a style never starts or ends
    /// inside one, where it would split a surrogate pair into two U+FFFD. A kanji is one column too, although it takes
    /// two cells on the screen. Escape sequences are counted as written.
    /// </summary>
    internal static void ApplyAt(ReadOnlySpan<char> text, ReadOnlySpan<int> lines, ushort[][] paint, string at, ushort styleId, string cmd)
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
                var content = text.Slice(lines[li * 2], contentLength);
                start = CharacterOffset(content, colLo - 1);
                if (start >= contentLength)
                {
                    if (lineLo == lineHi)
                        Warn("highlight", cmd, $"at '{at}' starts after output");
                    continue;
                }
                end = openColEnd ? contentLength : CharacterOffset(content, colHi);
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

    /// <summary>The place in <paramref name="line"/> after its first <paramref name="count"/> characters (grapheme clusters), or its length when it has fewer.</summary>
    private static int CharacterOffset(ReadOnlySpan<char> line, int count)
    {
        var offset = 0;
        for (var n = 0; n < count && offset < line.Length; n++)
            offset += StringInfo.GetNextTextElementLength(line[offset..]);
        return offset;
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

/// <summary>One part of the prompt and its SGR style, empty for none. A class, not a tuple: a generic over a new value type adds its own code to the AOT binary.</summary>
internal sealed class PromptPart(string text, string style)
{
    internal string Text { get; } = text;
    internal string Style { get; } = style;
}

/// <summary>The prompt's parts, each with its style (<see cref="ScenarioCast.ParsePrompt"/>).</summary>
internal sealed class PromptTemplate(PromptPart[] parts)
{
    /// <summary>Whether the prompt shows the folder, which the recording then keeps for each step.</summary>
    internal bool UsesDir { get; } = Array.Exists(parts, static part => part.Text.Contains(ScenarioCast.DirPlaceholder, StringComparison.Ordinal));

    /// <summary>The prompt with <c>{dir}</c> as <paramref name="dir"/>, each styled part closed by a reset.</summary>
    internal string Render(string dir)
    {
        var prompt = new StringBuilder();
        foreach (var part in parts)
        {
            var shown = part.Text.Replace(ScenarioCast.DirPlaceholder, dir, StringComparison.Ordinal);
            if (shown.Length == 0)
                continue;
            if (part.Style.Length == 0)
                prompt.Append(shown);
            else
                prompt.Append(part.Style).Append(shown).Append(ScenarioCast.SgrReset);
        }

        return prompt.ToString();
    }

    /// <summary>
    /// How <c>{dir}</c> shows a folder, as bash's <c>\W</c> does: its last name, <c>~</c> for the home folder, and
    /// <c>/</c> for the root. The rest of the path is never shown, so a recording names no user folder. Git Bash
    /// reports <c>/c/Users/name</c> for <c>C:\Users\name</c>, which is the home folder too.
    /// </summary>
    /// <param name="profile">A second folder that is home too, such as the user profile, which PowerShell's <c>~</c> names when HOME does not.</param>
    internal static string FolderName(string folder, string? home, string? profile = null)
    {
        var path = NativePath(folder);
        if ((home is { Length: > 0 } && SameFolder(path, home)) || (profile is { Length: > 0 } && SameFolder(path, profile)))
            return "~";

        var trimmed = path.TrimEnd('/', '\\');
        if (trimmed.Length == 0)
            return "/";
        // C: or C:\ is a drive's root.
        if (trimmed.Length == 2 && trimmed[1] == ':')
            return trimmed + "\\";

        var slash = trimmed.LastIndexOfAny(['/', '\\']);
        return slash < 0 ? trimmed : trimmed[(slash + 1)..];
    }

    /// <summary>Whether two paths name one folder, Git Bash's /c/Users/name and C:\Users\name included.</summary>
    /// <remarks>
    /// Case is ignored on Windows and on macOS, whose file systems ignore it unless set up otherwise: <c>cd /users/me</c>
    /// is the home folder <c>/Users/me</c> there, and the shell keeps the case it was typed in.
    /// </remarks>
    internal static bool SameFolder(string a, string b) =>
        string.Equals(NativePath(a).TrimEnd('/', '\\'), NativePath(b).TrimEnd('/', '\\'), OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    /// <summary>A Git Bash or Cygwin drive path on Windows (<c>/c/Users</c>, <c>/cygdrive/c/Users</c>) as Windows writes it.</summary>
    internal static string NativePath(string path)
    {
        if (!OperatingSystem.IsWindows())
            return path;

        var rest = path.StartsWith("/cygdrive/", StringComparison.Ordinal) ? path["/cygdrive".Length..] : path;
        if (rest.Length >= 2 && rest[0] == '/' && char.IsAsciiLetter(rest[1]) && (rest.Length == 2 || rest[2] == '/'))
            return char.ToUpperInvariant(rest[1]) + ":\\" + rest[Math.Min(rest.Length, 3)..].Replace('/', '\\');
        return path;
    }
}
