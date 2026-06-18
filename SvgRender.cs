using System.Globalization;
using System.Text;

internal static class SvgRender
{
    internal static void WriteSvg(
        IReadOnlyList<CastEvent> events,
        int width,
        int height,
        ResolvedRenderSettings render,
        string outputPath)
    {
        var (canvasWidth, canvasHeight) = TerminalReplay.ResolveCanvasSize(width, height, events);
        var theme = TerminalTheme.FromResolved(render.Theme);
        var frames = TerminalReplay.BuildFrames(events, width, height, canvasWidth, canvasHeight, theme);
        var svg = SvgFrameRenderer.Render(frames, render, canvasWidth, canvasHeight);
        File.WriteAllText(outputPath, svg, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }
}

internal readonly record struct ResolvedRenderSettings(int FontSize, string FontFamily, ResolvedTheme Theme)
{
    public string DefaultFg => Theme.Fg;
}

internal readonly record struct ResolvedTheme(string Fg, string Bg, string Palette);

internal static class ThemePresets
{
    internal const string DarkName = "dark";
    internal const string LightName = "light";
    internal static string ExpectedPresetNames => $"{DarkName}|{LightName}";

    private static readonly ResolvedTheme DarkTheme = new(
        "#d0d0d0",
        "#282c34",
        "#151515:#ac4142:#7e8e50:#e5b567:#6c99bb:#9f4e85:#7dd6cf:#d0d0d0:#505050:#ac4142:#7e8e50:#e5b567:#6c99bb:#9f4e85:#7dd6cf:#f5f5f5");

    private static readonly ResolvedTheme LightTheme = new(
        "#383838",
        "#fafafa",
        "#383838:#c82828:#548b2e:#a88800:#2871aa:#9a4a96:#008787:#585858:#686868:#e74c3c:#69a845:#d4a017:#3498db:#c678dd:#20b2aa:#fafafa");

    internal static ResolvedTheme Dark => DarkTheme;
    internal static ResolvedTheme Light => LightTheme;

    internal static bool TryGet(string name, out ResolvedTheme theme)
    {
        switch (name.Trim().ToLowerInvariant())
        {
            case DarkName:
                theme = DarkTheme;
                return true;
            case LightName:
                theme = LightTheme;
                return true;
            default:
                theme = default;
                return false;
        }
    }

    internal static bool TryParse(string text, out string presetName, out string error)
    {
        presetName = text.Trim();
        if (presetName.Length == 0)
        {
            error = "--theme requires a value";
            return false;
        }

        if (!TryGet(presetName, out _))
        {
            error = $"unknown theme preset: {presetName} (expected: {ExpectedPresetNames})";
            return false;
        }

        error = "";
        return true;
    }
}

internal static class RenderSettingsResolver
{
    internal const int MinFontSize = 1;
    internal const int MaxFontSize = 128;
    internal const int MinTerminalCols = 1;
    internal const int MaxTerminalCols = 512;
    internal const int MinTerminalRows = 1;
    internal const int MaxTerminalRows = 512;

    internal static bool IsValidTerminalSize(int cols, int rows) =>
        cols is >= MinTerminalCols and <= MaxTerminalCols &&
        rows is >= MinTerminalRows and <= MaxTerminalRows;

    internal const int DefaultFontSize = 16;
    internal const int MaxFontFamilyLength = 256;
    internal const int MaxFontFamilyCount = 10;
    internal const string FontSizeTagPrefix = "s2c:font-size=";
    internal const string FontFamilyTagPrefix = "s2c:font-family=";
    internal const string DefaultFontFamily =
        "ui-monospace, \"Cascadia Mono\", \"Cascadia Code\", \"JetBrains Mono\", \"Noto Sans Mono\", SFMono-Regular, Menlo, Monaco, Consolas, \"Liberation Mono\", monospace";
    internal static string DefaultFg => ThemePresets.Dark.Fg;
    internal static string DefaultBg => ThemePresets.Dark.Bg;
    internal static string DefaultPalette => ThemePresets.Dark.Palette;

    internal static bool TryParseFontFamily(string text, out string fontFamily, out string error)
    {
        fontFamily = "";
        error = "";
        if (string.IsNullOrWhiteSpace(text))
        {
            error = "font-family must not be empty";
            return false;
        }

        text = text.Trim();
        if (text.Length > MaxFontFamilyLength)
        {
            error = $"font-family must be at most {MaxFontFamilyLength} characters";
            return false;
        }

        if (!TrySplitFontFamilies(text, out var families, out error))
            return false;

        var hasMono = families.Any(f =>
            f.Equals("monospace", StringComparison.OrdinalIgnoreCase) ||
            f.Equals("ui-monospace", StringComparison.OrdinalIgnoreCase));
        var maxAllowed = hasMono ? MaxFontFamilyCount : MaxFontFamilyCount - 1;
        if (families.Count > maxAllowed)
        {
            error = $"font-family must have at most {maxAllowed} families";
            return false;
        }

        fontFamily = EnsureMonospaceFallback(FormatFontFamilies(families));
        if (fontFamily.Length > MaxFontFamilyLength)
        {
            error = $"font-family must be at most {MaxFontFamilyLength} characters";
            return false;
        }

        return true;
    }

    internal static bool TryParseFontSize(string text, out int fontSize, out string error)
    {
        fontSize = 0;
        error = "";
        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out fontSize))
        {
            error = $"invalid --font-size value: {text}";
            return false;
        }

        if (fontSize is < MinFontSize or > MaxFontSize)
        {
            error = $"--font-size must be between {MinFontSize} and {MaxFontSize}: {fontSize}";
            return false;
        }

        return true;
    }

    internal static bool TryResolve(
        Scenario scenario,
        string? cliThemePreset,
        out ResolvedRenderSettings settings,
        out string error)
    {
        settings = default;
        error = "";
        var render = scenario.Render;
        var fontSize = render?.FontSize ?? DefaultFontSize;
        if (fontSize is < MinFontSize or > MaxFontSize)
            fontSize = DefaultFontSize;

        if (!TryResolveTheme(render?.Theme, cliThemePreset, out var theme, out error))
            return false;

        var fontFamily = DefaultFontFamily;
        if (!string.IsNullOrWhiteSpace(render?.FontFamily))
        {
            if (!TryParseFontFamily(render!.FontFamily!, out fontFamily, out error))
            {
                error = $"invalid render.font-family: {error}";
                return false;
            }
        }

        settings = new ResolvedRenderSettings(fontSize, fontFamily, theme);
        return true;
    }

    internal static bool TryResolveTheme(
        ScenarioTheme? theme,
        string? cliThemePreset,
        out ResolvedTheme resolved,
        out string error)
    {
        resolved = default;
        error = "";

        var presetName = cliThemePreset ?? theme?.Preset;
        if (string.IsNullOrWhiteSpace(presetName))
            presetName = ThemePresets.DarkName;

        if (!ThemePresets.TryGet(presetName, out var baseTheme))
        {
            error = $"unknown theme preset: {presetName} (expected: {ThemePresets.ExpectedPresetNames})";
            return false;
        }

        resolved = MergeTheme(baseTheme, theme);
        return true;
    }

    internal static ResolvedRenderSettings ApplySvgOverrides(
        ResolvedRenderSettings settings,
        int? fontSizeOverride,
        string? fontFamilyOverride,
        string? themePresetOverride,
        out string error)
    {
        error = "";
        if (themePresetOverride is not null)
        {
            if (!ThemePresets.TryGet(themePresetOverride, out var theme))
            {
                error = $"unknown theme preset: {themePresetOverride} (expected: {ThemePresets.ExpectedPresetNames})";
                return settings;
            }

            settings = settings with { Theme = theme };
        }

        if (fontSizeOverride is int fontSize)
            settings = settings with { FontSize = fontSize };

        if (fontFamilyOverride is string fontFamily)
            settings = settings with { FontFamily = fontFamily };

        return settings;
    }

    private static bool TrySplitFontFamilies(string text, out List<string> families, out string error)
    {
        families = [];
        error = "";
        var index = 0;
        while (index < text.Length)
        {
            while (index < text.Length && char.IsWhiteSpace(text[index]))
                index++;

            if (index >= text.Length)
                break;

            if (!TryReadFontFamilyName(text, ref index, out var family, out error))
                return false;

            families.Add(family);

            while (index < text.Length && char.IsWhiteSpace(text[index]))
                index++;

            if (index >= text.Length)
                break;

            if (text[index] != ',')
            {
                error = "font-family: expected comma between family names";
                return false;
            }

            index++;
            if (index >= text.Length)
            {
                error = "font-family: trailing comma";
                return false;
            }
        }

        if (families.Count == 0)
        {
            error = "font-family must not be empty";
            return false;
        }

        return true;
    }

    private static bool TryReadFontFamilyName(string text, ref int index, out string family, out string error)
    {
        family = "";
        error = "";
        if (index >= text.Length)
        {
            error = "font-family: unexpected end of value";
            return false;
        }

        if (text[index] is '"' or '\'')
        {
            var quote = text[index++];
            var start = index;
            while (index < text.Length)
            {
                if (text[index] == '\\' && index + 1 < text.Length)
                {
                    index += 2;
                    continue;
                }

                if (text[index] == quote)
                {
                    family = text[start..index];
                    index++;
                    return ValidateFontFamilyName(family, out error);
                }

                index++;
            }

            error = "font-family: unterminated quoted family name";
            return false;
        }

        var unquotedStart = index;
        while (index < text.Length && text[index] != ',')
            index++;

        family = text[unquotedStart..index].Trim();
        if (family.Length == 0)
        {
            error = "font-family: empty family name";
            return false;
        }

        if (family.Any(char.IsWhiteSpace))
        {
            error = $"font-family: unquoted family name contains spaces: {family}";
            return false;
        }

        return ValidateFontFamilyName(family, out error);
    }

    private static bool ValidateFontFamilyName(string family, out string error)
    {
        error = "";
        if (family.Length == 0)
        {
            error = "font-family: empty family name";
            return false;
        }

        foreach (var c in family)
        {
            if (char.IsControl(c) || c is ';' or '{' or '}' or '<' or '>')
            {
                error = $"font-family: invalid character '{c}' in family name";
                return false;
            }
        }

        return true;
    }

    private static string FormatFontFamilies(IReadOnlyList<string> families)
    {
        var parts = new string[families.Count];
        for (var i = 0; i < families.Count; i++)
            parts[i] = FormatFontFamilyForCss(families[i]);

        return string.Join(", ", parts);
    }

    private static string FormatFontFamilyForCss(string family)
    {
        var isSafeIdent = family.Length > 0 &&
            (char.IsAsciiLetter(family[0]) || family[0] is '_' or '-') &&
            (family[0] != '-' || family.Length == 1 || !char.IsAsciiDigit(family[1])) &&
            family.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');
        if (isSafeIdent)
            return family;

        var escaped = family
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
        return $"\"{escaped}\"";
    }

    private static string EnsureMonospaceFallback(string fontFamily)
    {
        if (HasMonospaceGeneric(fontFamily))
            return fontFamily;

        return $"{fontFamily}, monospace";
    }

    private static bool HasMonospaceGeneric(string fontFamily)
    {
        if (!TrySplitFontFamilies(fontFamily, out var families, out _))
            return false;

        foreach (var family in families)
        {
            if (family.Equals("monospace", StringComparison.OrdinalIgnoreCase) ||
                family.Equals("ui-monospace", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static ResolvedTheme MergeTheme(ResolvedTheme baseTheme, ScenarioTheme? overrides)
    {
        if (overrides is null)
            return baseTheme;

        return new ResolvedTheme(
            string.IsNullOrWhiteSpace(overrides.Fg) ? baseTheme.Fg : overrides.Fg!,
            string.IsNullOrWhiteSpace(overrides.Bg) ? baseTheme.Bg : overrides.Bg!,
            string.IsNullOrWhiteSpace(overrides.Palette) ? baseTheme.Palette : overrides.Palette!);
    }

    internal static ResolvedRenderSettings Resolve(Scenario scenario)
    {
        if (!TryResolve(scenario, cliThemePreset: null, out var settings, out var error))
            throw new InvalidOperationException(error);

        return settings;
    }

    internal static string ResolveCastSvgOutputPath(string castPath, string? outputArg)
    {
        if (outputArg is not null)
        {
            var full = Path.GetFullPath(outputArg);
            return Path.Combine(
                Path.GetDirectoryName(full) ?? ".",
                Path.GetFileNameWithoutExtension(full) + ".svg");
        }

        return Path.ChangeExtension(Path.GetFullPath(castPath), ".svg")!;
    }

    internal static string ResolveOutputStem(string scenarioPath, string? outputArg)
    {
        if (outputArg is not null)
        {
            var full = Path.GetFullPath(outputArg);
            return Path.Combine(Path.GetDirectoryName(full) ?? ".", Path.GetFileNameWithoutExtension(full));
        }

        return Path.ChangeExtension(Path.GetFullPath(scenarioPath), null)!;
    }
}
