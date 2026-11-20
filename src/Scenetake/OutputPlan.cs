namespace Scenetake;

/// <summary>
/// A file a recording writes besides the cast: the <c>--format</c> output, or an entry of the scenario's
/// <c>outputs</c> list. <see cref="Settings"/> is its look for the checks before anything runs. An entry's
/// <see cref="Overrides"/> are applied again to the settings read back from the cast, which the file is drawn with.
/// </summary>
/// <remarks>A class, not a record: nothing compares plans, and a record's generated members add to the AOT binary.</remarks>
sealed class OutputPlan(
    string label,
    OutputFormat format,
    string path,
    ResolvedRenderSettings settings,
    Mp4Options mp4,
    double? stillAt,
    OutputOverrides? overrides)
{
    /// <summary>How errors name it: empty for <c>--format</c>, "outputs entry 2" for an entry.</summary>
    internal string Label { get; } = label;
    internal OutputFormat Format { get; } = format;
    internal string Path { get; } = path;
    internal ResolvedRenderSettings Settings { get; } = settings;
    internal Mp4Options Mp4 { get; } = mp4;
    internal double? StillAt { get; } = stillAt;
    internal OutputOverrides? Overrides { get; } = overrides;

    /// <summary>The ffmpeg that draws it, found by the checks before anything runs. Null for an SVG.</summary>
    internal string? FfmpegPath { get; set; }
}

/// <summary>The look an outputs entry sets for its file, on top of the scenario's, as the svg command's options do.</summary>
sealed class OutputOverrides(int? fontSize, string? fontFamily, string? theme, WindowStyle? window, int? maxFps, StyleOptions style)
{
    /// <summary>
    /// A theme replaces the scenario's theme with its fg, bg, and palette, as <c>scenetake svg --theme</c> does: a
    /// light copy of a dark recording takes the light theme's own colors.
    /// </summary>
    internal ResolvedRenderSettings Apply(ResolvedRenderSettings settings) =>
        style.ToOverrides().Apply(RenderSettingsResolver.ApplySvgOverrides(settings, fontSize, fontFamily, theme, window, maxFps, out _));
}
