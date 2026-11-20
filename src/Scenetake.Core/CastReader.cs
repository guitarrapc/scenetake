using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Scenetake.Core;

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
    internal static CastRecording Read(string castPath) =>
        ReadLines(File.ReadAllLines(castPath, Encoding.UTF8));

    /// <summary>Reads a cast held in memory, split into lines as <see cref="File.ReadAllLines(string)"/> splits a file.</summary>
    internal static CastRecording ReadText(string text)
    {
        var lines = new List<string>();
        using var reader = new StringReader(text.Length > 0 && text[0] == '﻿' ? text[1..] : text);
        while (reader.ReadLine() is { } line)
            lines.Add(line);
        return ReadLines([.. lines]);
    }

    private static CastRecording ReadLines(string[] lines)
    {
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
        var style = new CastStyle();
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
                ReadRenderTags(tags, ref fontSize, ref fontFamily, ref window, style);
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
                $"cast terminal size must be between {RenderSettingsResolver.MinTerminalCols} and {RenderSettingsResolver.MaxTerminalCols}");
        }

        var (fg, bg, palette) = ParseTheme(theme);
        // The title bar shows st:window-title when the cast has one, and the cast's own title otherwise.
        var castTitle = header.TryGetProperty("title", out var titleElement) && titleElement.ValueKind == JsonValueKind.String
            ? RenderSettingsResolver.NormalizeWindowTitle(titleElement.GetString() ?? "")
            : null;
        var renderSettings = new ResolvedRenderSettings(fontSize, fontFamily, new ResolvedTheme(fg, bg, palette), window)
        {
            WindowTitle = style.WindowTitle ?? castTitle,
            Background = style.Background,
            Margin = style.Margin,
            Padding = style.Padding,
            Opacity = style.Opacity,
            Blur = style.Blur,
            Loop = style.Loop,
            EndHold = style.EndHold,
            FadeOut = style.FadeOut,
            Fit = style.Fit,
            Crop = new CropSpec(style.CropTop, style.CropBottom, style.CropLeft, style.CropRight),
        };
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

    /// <summary>
    /// The window title, background, margin, padding, opacity, blur, loop, end hold, and fade-out a cast's tags set,
    /// null for each it does not. Fit and crop are none and zero when unset.
    /// </summary>
    private sealed class CastStyle
    {
        internal string? WindowTitle;
        internal BackgroundSpec? Background;
        internal int? Margin;
        internal int? Padding;
        internal double? Opacity;
        internal int? Blur;
        internal bool? Loop;
        internal double? EndHold;
        internal double? FadeOut;
        internal FitMode Fit = RenderSettingsResolver.DefaultFit;
        internal int CropTop;
        internal int CropBottom;
        internal int CropLeft;
        internal int CropRight;
    }

    private static void ReadRenderTags(JsonElement tags, ref int fontSize, ref string fontFamily, ref WindowStyle window, CastStyle style)
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
            // st:window-title= is checked before st:window=, which is its prefix.
            if (value.StartsWith(RenderSettingsResolver.FontSizeTagPrefix, StringComparison.Ordinal))
                ApplyFontSize(ref fontSize, value[RenderSettingsResolver.FontSizeTagPrefix.Length..], ref warnedSize);
            else if (value.StartsWith(RenderSettingsResolver.FontFamilyTagPrefix, StringComparison.Ordinal))
                ApplyFontFamily(ref fontFamily, value[RenderSettingsResolver.FontFamilyTagPrefix.Length..], ref warnedFamily);
            else if (value.StartsWith(RenderSettingsResolver.WindowTitleTagPrefix, StringComparison.Ordinal))
                style.WindowTitle = RenderSettingsResolver.NormalizeWindowTitle(value[RenderSettingsResolver.WindowTitleTagPrefix.Length..]);
            else if (value.StartsWith(RenderSettingsResolver.WindowTagPrefix, StringComparison.Ordinal))
                ApplyWindow(ref window, value[RenderSettingsResolver.WindowTagPrefix.Length..], ref warnedWindow);
            else if (value.StartsWith(RenderSettingsResolver.BackgroundTagPrefix, StringComparison.Ordinal))
            {
                if (ReadTag(value, RenderSettingsResolver.BackgroundTagPrefix, "background", "no background",
                    static (string text, out BackgroundSpec parsed, out string error) => RenderSettingsResolver.TryParseBackground(text, images: null, out parsed, out error),
                    out var background))
                    style.Background = background;
            }
            else if (value.StartsWith(RenderSettingsResolver.MarginTagPrefix, StringComparison.Ordinal))
            {
                if (ReadTag<int>(value, RenderSettingsResolver.MarginTagPrefix, "margin", "the default margin", RenderSettingsResolver.TryParseMargin, out var margin))
                    style.Margin = margin;
            }
            else if (value.StartsWith(RenderSettingsResolver.PaddingTagPrefix, StringComparison.Ordinal))
            {
                if (ReadTag<int>(value, RenderSettingsResolver.PaddingTagPrefix, "padding", "the default padding", RenderSettingsResolver.TryParsePadding, out var padding))
                    style.Padding = padding;
            }
            else if (value.StartsWith(RenderSettingsResolver.OpacityTagPrefix, StringComparison.Ordinal))
            {
                if (ReadTag<double>(value, RenderSettingsResolver.OpacityTagPrefix, "opacity", "an opaque window", RenderSettingsResolver.TryParseOpacity, out var opacity))
                    style.Opacity = opacity;
            }
            else if (value.StartsWith(RenderSettingsResolver.BlurTagPrefix, StringComparison.Ordinal))
            {
                if (ReadTag<int>(value, RenderSettingsResolver.BlurTagPrefix, "blur", "no blur", RenderSettingsResolver.TryParseBlur, out var blur))
                    style.Blur = blur;
            }
            else if (value.StartsWith(RenderSettingsResolver.LoopTagPrefix, StringComparison.Ordinal))
            {
                if (ReadTag<bool>(value, RenderSettingsResolver.LoopTagPrefix, "loop", "a looping SVG", RenderSettingsResolver.TryParseLoop, out var loop))
                    style.Loop = loop;
            }
            else if (value.StartsWith(RenderSettingsResolver.EndHoldTagPrefix, StringComparison.Ordinal))
            {
                if (ReadTag<double>(value, RenderSettingsResolver.EndHoldTagPrefix, "end-hold", "the default hold", RenderSettingsResolver.TryParseEndHold, out var endHold))
                    style.EndHold = endHold;
            }
            else if (value.StartsWith(RenderSettingsResolver.FadeOutTagPrefix, StringComparison.Ordinal))
            {
                if (ReadTag<double>(value, RenderSettingsResolver.FadeOutTagPrefix, "fade-out", "no fade", RenderSettingsResolver.TryParseFadeOut, out var fadeOut))
                    style.FadeOut = fadeOut;
            }
            else if (value.StartsWith(RenderSettingsResolver.FitTagPrefix, StringComparison.Ordinal))
            {
                if (ReadTag<FitMode>(value, RenderSettingsResolver.FitTagPrefix, "fit", "the default fit (height)", RenderSettingsResolver.TryParseFit, out var fit))
                    style.Fit = fit;
            }
            else if (value.StartsWith(RenderSettingsResolver.CropTopTagPrefix, StringComparison.Ordinal))
                style.CropTop = ReadCropTag(value, RenderSettingsResolver.CropTopTagPrefix, "crop-top");
            else if (value.StartsWith(RenderSettingsResolver.CropBottomTagPrefix, StringComparison.Ordinal))
                style.CropBottom = ReadCropTag(value, RenderSettingsResolver.CropBottomTagPrefix, "crop-bottom");
            else if (value.StartsWith(RenderSettingsResolver.CropLeftTagPrefix, StringComparison.Ordinal))
                style.CropLeft = ReadCropTag(value, RenderSettingsResolver.CropLeftTagPrefix, "crop-left");
            else if (value.StartsWith(RenderSettingsResolver.CropRightTagPrefix, StringComparison.Ordinal))
                style.CropRight = ReadCropTag(value, RenderSettingsResolver.CropRightTagPrefix, "crop-right");
        }
    }

    private static int ReadCropTag(string tag, string prefix, string name)
    {
        var text = tag[prefix.Length..];
        if (RenderSettingsResolver.TryParseCrop(text, name, out var cells, out _))
            return cells;

        Console.Error.WriteLine($"Warning: cast: invalid {name} '{text}', using no crop");
        return 0;
    }

    private delegate bool TagParser<T>(string text, out T parsed, out string error);

    /// <summary>Reads a tag's value. One that scenetake does not accept is warned about and left out.</summary>
    private static bool ReadTag<T>(string tag, string prefix, string name, string fallback, TagParser<T> parse, out T value)
    {
        var text = tag[prefix.Length..];
        if (parse(text, out value, out _))
            return true;

        Console.Error.WriteLine($"Warning: cast: invalid {name} '{text}', using {fallback}");
        return false;
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
            Console.Error.WriteLine($"Warning: cast: invalid font-size '{sizeText}', using default font size");
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
            Console.Error.WriteLine($"Warning: cast: invalid font-family '{familyText}', using default font-family");
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
            Console.Error.WriteLine($"Warning: cast: invalid window '{windowText}', using no window chrome");
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
            // System.Text.Json ends its message with "LineNumber: 0 | BytePositionInLine: N." for the one
            // line it was given, which contradicted the cast line number in front of it.
            var message = ex.Message;
            var path = message.IndexOf(" Path:", StringComparison.Ordinal);
            var position = message.IndexOf(" LineNumber:", StringComparison.Ordinal);
            if (path > 0 && (position < 0 || path < position))
                position = path;
            if (position > 0)
                message = message[..position];
            throw new CastReadException($"invalid JSON at line {lineNumber}: {message}");
        }
    }

    internal static bool TryParseHexColor(string? value, out string color)
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

    internal static bool TryParsePalette(string? value, out string palette)
    {
        palette = "";
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var parts = value.Split(':', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 16)
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
                            Console.Error.WriteLine("Warning: cast: invalid resize event data, skipping");

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
                        Console.Error.WriteLine($"Warning: cast: unsupported cast event code '{unknown}', skipping");

                    return true;
                }
            }
        }

        var code = codeText.Length >= 2 && codeText[0] == '"' && codeText[^1] == '"'
            ? codeText[1..^1].ToString()
            : root[1].GetString() ?? "";
        if (warnedCodes.Add(code))
            Console.Error.WriteLine($"Warning: cast: unsupported cast event code '{code}', skipping");

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
