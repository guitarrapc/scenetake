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
    List<CastEvent> Events);

internal static class CastReader
{
    internal static CastRecording Read(string castPath)
    {
        var lines = File.ReadAllLines(castPath, Encoding.UTF8);
        if (lines.Length == 0)
            throw new CastReadException("cast file is empty");

        var headerLine = lines[0].Trim();
        if (headerLine.Length == 0)
            throw new CastReadException("cast header is missing");

        using var headerDoc = JsonDocument.Parse(headerLine);
        var header = headerDoc.RootElement;

        if (!header.TryGetProperty("version", out var versionElement) ||
            versionElement.ValueKind != JsonValueKind.Number ||
            versionElement.GetInt32() != 2)
        {
            throw new CastReadException("cast version must be 2");
        }

        if (!TryReadPositiveInt(header, "width", out var width))
            throw new CastReadException("cast header is missing width");

        if (!TryReadPositiveInt(header, "height", out var height))
            throw new CastReadException("cast header is missing height");

        var renderSettings = CastReader.ResolveFromCastHeader(header);
        var events = new List<CastEvent>();
        var warnedCodes = new HashSet<string>(StringComparer.Ordinal);

        for (var i = 1; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (line.Length == 0)
                continue;

            if (!TryParseEventLine(line, i + 1, warnedCodes, out var ev))
                throw new CastReadException($"invalid cast event at line {i + 1}");

            if (ev is not null)
                events.Add(ev);
        }

        return new CastRecording(width, height, renderSettings, events);
    }

    internal static ResolvedRenderSettings ResolveFromCastHeader(JsonElement header)
    {
        var fontSize = RenderSettingsResolver.DefaultFontSize;
        if (header.TryGetProperty("scenario2cast", out var extension) &&
            extension.ValueKind == JsonValueKind.Object &&
            extension.TryGetProperty("font-size", out var fontSizeElement) &&
            fontSizeElement.ValueKind == JsonValueKind.Number)
        {
            var parsed = fontSizeElement.GetInt32();
            if (parsed > 0)
                fontSize = parsed;
        }

        string fg = RenderSettingsResolver.DefaultFg;
        string bg = RenderSettingsResolver.DefaultBg;
        string palette = RenderSettingsResolver.DefaultPalette;

        if (header.TryGetProperty("theme", out var theme) && theme.ValueKind == JsonValueKind.Object)
        {
            if (theme.TryGetProperty("fg", out var fgElement) &&
                fgElement.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(fgElement.GetString()))
            {
                fg = fgElement.GetString()!;
            }

            if (theme.TryGetProperty("bg", out var bgElement) &&
                bgElement.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(bgElement.GetString()))
            {
                bg = bgElement.GetString()!;
            }

            if (theme.TryGetProperty("palette", out var paletteElement) &&
                paletteElement.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(paletteElement.GetString()))
            {
                palette = paletteElement.GetString()!;
            }
        }

        return new ResolvedRenderSettings(fontSize, new ResolvedTheme(fg, bg, palette));
    }

    private static bool TryReadPositiveInt(JsonElement header, string name, out int value)
    {
        value = 0;
        if (!header.TryGetProperty(name, out var element) || element.ValueKind != JsonValueKind.Number)
            return false;

        value = element.GetInt32();
        return value > 0;
    }

    private static bool TryParseEventLine(
        string line,
        int lineNumber,
        HashSet<string> warnedCodes,
        out CastEvent? ev)
    {
        ev = null;

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(line);
        }
        catch (JsonException ex)
        {
            throw new CastReadException($"invalid JSON at line {lineNumber}: {ex.Message}");
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() < 3)
                return false;

            if (root[0].ValueKind != JsonValueKind.Number)
                return false;

            if (root[1].ValueKind != JsonValueKind.String)
                return false;

            if (root[2].ValueKind != JsonValueKind.String)
                return false;

            var code = root[1].GetString() ?? "";
            if (!string.Equals(code, "o", StringComparison.Ordinal))
            {
                if (warnedCodes.Add(code))
                    Console.Error.WriteLine($"Warning: svg: unsupported cast event code '{code}'; skipping");

                return true;
            }

            var time = root[0].GetDouble();
            var data = root[2].GetString() ?? "";
            ev = new CastEvent(time, data);
            return true;
        }
    }
}
