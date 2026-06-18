internal readonly record struct TerminalTheme(string Foreground, string Background, string[] AnsiPalette)
{
    internal static TerminalTheme FromResolved(ResolvedTheme theme)
    {
        var palette = ParsePalette(theme.Palette);
        return new TerminalTheme(theme.Fg, theme.Bg, palette);
    }

    private static string[] ParsePalette(string palette)
    {
        var parts = palette.Split(':', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 16)
            return parts[..16];

        var defaults = TerminalEmulatorPalette.DefaultColors();
        for (var i = 0; i < parts.Length && i < defaults.Length; i++)
            defaults[i] = parts[i];
        return defaults;
    }
}

internal static class TerminalEmulatorPalette
{
    internal static string[] DefaultColors() =>
    [
        "#151515", "#ac4142", "#7e8e50", "#e5b567", "#6c99bb", "#9f4e85", "#7dd6cf", "#d0d0d0",
        "#505050", "#ac4142", "#7e8e50", "#e5b567", "#6c99bb", "#9f4e85", "#7dd6cf", "#f5f5f5"
    ];
}
