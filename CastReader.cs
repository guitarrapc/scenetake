using System.Globalization;
using System.Text;
using System.Text.Json;

internal sealed class CastReadException : Exception
{
    public CastReadException(string message) : base(message) { }
}

internal readonly record struct CastRecording(
    int Width,
    int Height,
    ResolvedRenderSettings RenderSettings,
    List<CastEvent> Events,
    double LoopDuration);

internal static class CastReader
{
    internal static CastRecording Read(string castPath)
    {
        var lines = File.ReadAllLines(castPath, Encoding.UTF8);
        if (lines.Length == 0)
            throw new CastReadException("cast file is empty");

        var headerLineIndex = -1;
        string headerLine = "";
        for (var i = 0; i < lines.Length; i++)
        {
            var candidate = lines[i].Trim();
            if (candidate.Length == 0 || candidate[0] == '#')
                continue;

            headerLineIndex = i;
            headerLine = candidate;
            break;
        }

        if (headerLineIndex < 0)
            throw new CastReadException("cast header is missing");

        using var headerDoc = ParseJsonOrThrow(headerLine, headerLineIndex + 1);
        var header = headerDoc.RootElement;

        if (!header.TryGetProperty("version", out var versionElement) ||
            !versionElement.TryGetInt32(out var version) ||
            version is not (2 or 3))
        {
            throw new CastReadException("cast version must be 2 or 3");
        }

        var fontSize = RenderSettingsResolver.DefaultFontSize;
        var fontFamily = RenderSettingsResolver.DefaultFontFamily;
        var window = WindowStyle.None;
        JsonElement theme = default;
        int width;
        int height;

        if (version == 3)
        {
            if (!header.TryGetProperty("term", out var term) || term.ValueKind != JsonValueKind.Object ||
                !TryReadPositiveInt(term, "cols", out width) ||
                !TryReadPositiveInt(term, "rows", out height))
            {
                throw new CastReadException("cast header is missing terminal size");
            }

            term.TryGetProperty("theme", out theme);
            if (header.TryGetProperty("tags", out var tags) && tags.ValueKind == JsonValueKind.Array)
                ReadRenderTags(tags, ref fontSize, ref fontFamily, ref window);
        }
        else
        {
            if (!TryReadPositiveInt(header, "width", out width) ||
                !TryReadPositiveInt(header, "height", out height))
            {
                throw new CastReadException("cast header is missing terminal size");
            }

            header.TryGetProperty("theme", out theme);
        }

        if (!RenderSettingsResolver.IsValidTerminalSize(width, height))
        {
            throw new CastReadException(
                $"cast terminal size must be {RenderSettingsResolver.MinTerminalCols}–{RenderSettingsResolver.MaxTerminalCols}");
        }

        var (fg, bg, palette) = ParseTheme(theme);
        var renderSettings = new ResolvedRenderSettings(fontSize, fontFamily, new ResolvedTheme(fg, bg, palette), window);
        var events = new List<CastEvent>();
        var warnedCodes = new HashSet<string>(StringComparer.Ordinal);
        var usesRelativeTime = version == 3;
        var absoluteTime = 0.0;

        for (var i = headerLineIndex + 1; i < lines.Length; i++)
        {
            var line = lines[i].AsSpan().Trim();
            if (line.IsEmpty || line[0] == '#')
                continue;

            if (!TryParseEventLine(line, i + 1, warnedCodes, out var ev, out var relativeDelay))
                throw new CastReadException($"invalid cast event at line {i + 1}");

            if (usesRelativeTime)
                absoluteTime += relativeDelay;

            if (!ev.HasValue)
                continue;

            events.Add(usesRelativeTime ? ev.Value with { Time = absoluteTime } : ev.Value);
        }

        var loopDuration = TerminalReplay.ComputeLoopDuration(events);

        return new CastRecording(width, height, renderSettings, events, loopDuration);
    }

    private static void ReadRenderTags(JsonElement tags, ref int fontSize, ref string fontFamily, ref WindowStyle window)
    {
        var warnedSize = false;
        var warnedFamily = false;
        var warnedWindow = false;
        foreach (var tag in tags.EnumerateArray())
        {
            if (tag.ValueKind != JsonValueKind.String)
                continue;
            var value = tag.GetString();
            if (value is null)
                continue;
            if (value.StartsWith(RenderSettingsResolver.FontSizeTagPrefix, StringComparison.Ordinal))
                ApplyFontSize(ref fontSize, value[RenderSettingsResolver.FontSizeTagPrefix.Length..], ref warnedSize);
            else if (value.StartsWith(RenderSettingsResolver.FontFamilyTagPrefix, StringComparison.Ordinal))
                ApplyFontFamily(ref fontFamily, value[RenderSettingsResolver.FontFamilyTagPrefix.Length..], ref warnedFamily);
            else if (value.StartsWith(RenderSettingsResolver.WindowTagPrefix, StringComparison.Ordinal))
                ApplyWindow(ref window, value[RenderSettingsResolver.WindowTagPrefix.Length..], ref warnedWindow);
        }
    }

    private static void ApplyFontSize(ref int fontSize, string sizeText, ref bool warned)
    {
        if (RenderSettingsResolver.TryParseFontSize(sizeText, out var parsed, out _))
        {
            fontSize = parsed;
            return;
        }

        if (!warned)
        {
            warned = true;
            Console.Error.WriteLine($"Warning: svg: invalid font-size '{sizeText}'; using default font size");
        }
    }

    private static void ApplyFontFamily(ref string fontFamily, string familyText, ref bool warned)
    {
        if (RenderSettingsResolver.TryParseFontFamily(familyText, out var parsed, out _))
        {
            fontFamily = parsed;
            return;
        }

        if (!warned)
        {
            warned = true;
            Console.Error.WriteLine($"Warning: svg: invalid font-family '{familyText}'; using default font-family");
        }
    }

    private static void ApplyWindow(ref WindowStyle window, string windowText, ref bool warned)
    {
        if (RenderSettingsResolver.TryParseWindow(windowText, out var parsed, out _))
        {
            window = parsed;
            return;
        }

        if (!warned)
        {
            warned = true;
            Console.Error.WriteLine($"Warning: svg: invalid window '{windowText}'; using no window chrome");
        }
    }

    private static (string fg, string bg, string palette) ParseTheme(JsonElement theme)
    {
        var fg = RenderSettingsResolver.DefaultFg;
        var bg = RenderSettingsResolver.DefaultBg;
        var palette = RenderSettingsResolver.DefaultPalette;
        if (theme.ValueKind != JsonValueKind.Object)
            return (fg, bg, palette);

        if (theme.TryGetProperty("fg", out var fgElement) &&
            fgElement.ValueKind == JsonValueKind.String &&
            TryParseHexColor(fgElement.GetString(), out var parsedFg))
        {
            fg = parsedFg;
        }

        if (theme.TryGetProperty("bg", out var bgElement) &&
            bgElement.ValueKind == JsonValueKind.String &&
            TryParseHexColor(bgElement.GetString(), out var parsedBg))
        {
            bg = parsedBg;
        }

        if (theme.TryGetProperty("palette", out var paletteElement) &&
            paletteElement.ValueKind == JsonValueKind.String &&
            TryParsePalette(paletteElement.GetString(), out var parsedPalette))
        {
            palette = parsedPalette;
        }

        return (fg, bg, palette);
    }

    private static bool TryReadPositiveInt(JsonElement header, string name, out int value)
    {
        value = 0;
        if (!header.TryGetProperty(name, out var element) || element.ValueKind != JsonValueKind.Number)
            return false;

        return element.TryGetInt32(out value) && value > 0;
    }

    private static JsonDocument ParseJsonOrThrow(ReadOnlySpan<char> json, int lineNumber)
    {
        try
        {
            return JsonDocument.Parse(json.ToString());
        }
        catch (JsonException ex)
        {
            throw new CastReadException($"invalid JSON at line {lineNumber}: {ex.Message}");
        }
    }

    private static bool TryParseHexColor(string? value, out string color)
    {
        color = "";
        if (string.IsNullOrWhiteSpace(value))
            return false;

        value = value.Trim();
        if (value.Length is not (4 or 7) || value[0] != '#')
            return false;

        for (var i = 1; i < value.Length; i++)
        {
            if (!IsHexDigit(value[i]))
                return false;
        }

        color = value;
        return true;
    }

    private static bool TryParsePalette(string? value, out string palette)
    {
        palette = "";
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var parts = value.Split(':', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 16)
            return false;

        var colors = new string[16];
        for (var i = 0; i < 16; i++)
        {
            if (!TryParseHexColor(parts[i], out colors[i]))
                return false;
        }

        palette = string.Join(':', colors);
        return true;
    }

    private static bool IsHexDigit(char c) =>
        c is (>= '0' and <= '9') or (>= 'a' and <= 'f') or (>= 'A' and <= 'F');

    private static bool TryParseEventLine(
        ReadOnlySpan<char> line,
        int lineNumber,
        HashSet<string> warnedCodes,
        out CastEvent? ev,
        out double relativeDelay)
    {
        ev = null;
        relativeDelay = 0;

        using var doc = ParseJsonOrThrow(line, lineNumber);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() < 3 ||
            root[0].ValueKind != JsonValueKind.Number ||
            root[1].ValueKind != JsonValueKind.String ||
            root[2].ValueKind != JsonValueKind.String)
        {
            return false;
        }

        var time = root[0].GetDouble();
        relativeDelay = time;
        var data = root[2].GetString() ?? "";

        var codeText = root[1].GetRawText();

        if (codeText.Length == 3 && codeText[0] == '"' && codeText[2] == '"')
        {
            switch (codeText[1])
            {
                case 'o':
                    ev = CastEvent.Output(time, data);
                    return true;
                case 'r':
                    if (!TryParseResizeData(data, out var resizeWidth, out var resizeHeight))
                    {
                        if (warnedCodes.Add("invalid-resize"))
                            Console.Error.WriteLine("Warning: svg: invalid resize event data; skipping");

                        return true;
                    }

                    ev = CastEvent.Resize(time, resizeWidth, resizeHeight);
                    return true;
                case 'm':
                case 'x':
                case 'i':
                    return true;
                default:
                {
                    var unknown = codeText[1].ToString();
                    if (warnedCodes.Add(unknown))
                        Console.Error.WriteLine($"Warning: svg: unsupported cast event code '{unknown}'; skipping");

                    return true;
                }
            }
        }

        var code = codeText.Length >= 2 && codeText[0] == '"' && codeText[^1] == '"'
            ? codeText[1..^1].ToString()
            : root[1].GetString() ?? "";
        if (warnedCodes.Add(code))
            Console.Error.WriteLine($"Warning: svg: unsupported cast event code '{code}'; skipping");

        return true;
    }

    private static bool TryParseResizeData(string data, out int width, out int height)
    {
        width = 0;
        height = 0;
        var separator = data.IndexOf('x');
        if (separator <= 0 || separator >= data.Length - 1)
            return false;

        if (!int.TryParse(data.AsSpan(0, separator), NumberStyles.Integer, CultureInfo.InvariantCulture, out width))
            return false;

        if (!int.TryParse(data.AsSpan(separator + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out height))
            return false;

        return RenderSettingsResolver.IsValidTerminalSize(width, height);
    }
}
