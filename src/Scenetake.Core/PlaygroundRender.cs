using System.Text;
using static Scenetake.Core.ScenarioCast;

namespace Scenetake.Core;

/// <summary>
/// The render options of the playground. They are the CLI flags <c>--theme</c>, <c>--window</c>, <c>--font-size</c>,
/// <c>--font-family</c>, <c>--max-fps</c>, <c>--window-title</c>, <c>--background</c>, <c>--margin</c>,
/// <c>--padding</c>, <c>--opacity</c>, <c>--blur</c>, <c>--loop</c> / <c>--no-loop</c>, <c>--end-hold</c>,
/// <c>--fade-out</c>, and <c>--fit</c>, with the same values and ranges. Null leaves a setting as the scenario or the
/// cast sets it.
/// </summary>
/// <param name="Images">
/// The images that a background, in the YAML or in <paramref name="Background"/>, names. The playground reads no
/// files, so the page gives it each image with a name, and a name stands where the CLI takes a path.
/// </param>
internal sealed record PlaygroundOptions(
    string? Theme = null,
    string? Window = null,
    int? FontSize = null,
    string? FontFamily = null,
    int? MaxFps = null,
    string? WindowTitle = null,
    string? Background = null,
    int? Margin = null,
    int? Padding = null,
    IReadOnlyDictionary<string, byte[]>? Images = null,
    double? Opacity = null,
    int? Blur = null,
    bool? Loop = null,
    double? EndHold = null,
    double? FadeOut = null,
    string? Fit = null)
{
    internal BackgroundImageSource ImageSource => BackgroundImageSource.FromImages(Images ?? new Dictionary<string, byte[]>());
}

/// <summary>
/// What a render gives the playground. <see cref="Error"/> is set when the CLI would stop before recording, and then
/// <see cref="Cast"/> and <see cref="Svg"/> are null. <see cref="Warnings"/> are the CLI's stderr warnings without
/// their "Warning: " prefix.
/// </summary>
internal sealed record PlaygroundResult(
    byte[]? Cast,
    byte[]? Svg,
    IReadOnlyList<ReplayedStep> Steps,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> NotApplied,
    string? Error);

/// <summary>
/// The playground's scenario and cast paths. They follow the CLI's scenario path and <c>svg</c> command step by step,
/// with the commands replaced by an outputs file (<see cref="StepOutputsReplay"/>), so a sample's unmodified YAML
/// gives the cast and SVG that the CLI wrote.
/// </summary>
internal static class PlaygroundRender
{
    internal static PlaygroundResult RenderScenario(string yaml, StepOutputs outputs, PlaygroundOptions options, bool renderSvg = true)
    {
        var warnings = new List<string>();
        var replay = new StepOutputsReplay(outputs);
        string? error = null;
        byte[]? cast = null;
        byte[]? svg = null;
        IReadOnlyList<string> notApplied = [];

        CaptureWarnings(warnings, () =>
        {
            Scenario scenario;
            try
            {
                scenario = ParseScenario(yaml);
            }
            catch (Exception ex)
            {
                error = $"cannot read the scenario: {ex.Message}";
                return;
            }

            foreach (var unknownKey in FindUnknownScenarioKeys(yaml))
                Console.Error.WriteLine($"Warning: {unknownKey}");

            if (!TryParseOptions(options, out var themePreset, out var window, out var fontSize, out var fontFamily, out var maxFps, out var style, out error))
                return;

            if (!RenderSettingsResolver.TryResolve(scenario, themePreset, out var renderSettings, out var themeError, options.ImageSource))
            {
                error = themeError;
                return;
            }

            var width = scenario.Width ?? RenderSettingsResolver.DefaultTerminalCols;
            var height = scenario.Height ?? RenderSettingsResolver.DefaultTerminalRows;
            if (!RenderSettingsResolver.IsValidTerminalSize(width, height))
            {
                error = $"width must be between {RenderSettingsResolver.MinTerminalCols} and {RenderSettingsResolver.MaxTerminalCols}, and height between {RenderSettingsResolver.MinTerminalRows} and {RenderSettingsResolver.MaxTerminalRows}: {width}x{height}";
                return;
            }

            if (FindStepKeyError(scenario) is { } stepKeyError)
            {
                error = stepKeyError;
                return;
            }

            notApplied = FindNotApplied(scenario, outputs);

            if (fontSize is int fontSizeValue)
                renderSettings = renderSettings with { FontSize = fontSizeValue };
            if (fontFamily is string fontFamilyValue)
                renderSettings = renderSettings with { FontFamily = fontFamilyValue };
            if (window is WindowStyle windowValue)
                renderSettings = renderSettings with { Window = windowValue };
            if (maxFps is int maxFpsValue)
                renderSettings = renderSettings with { MaxFps = maxFpsValue };
            renderSettings = style.Apply(renderSettings);
            if (FindCardCropError(scenario, renderSettings.Crop) is { } cardCropError)
            {
                error = cardCropError;
                return;
            }

            foreach (var note in RenderSettingsResolver.FindIneffectiveStyle(renderSettings))
                Console.Error.WriteLine($"Warning: {note}");

            var seed = ComputeDeterministicSeed(yaml);
            var timestamp = ComputeDeterministicTimestamp(seed);
            var rerecorder = new Rerecorder(previous: null, masker: null);
            var generateOptions = new GenerateOptions
            {
                TrackPtyCursor = replay.TrackPtyCursor,
                Fit = renderSettings.Fit,
                Crop = renderSettings.Crop,
                LogRunning = false,
                // The outputs were masked when they were recorded, with the recording machine's environment, which the
                // browser does not have. Masking again hides only what the YAML adds, such as a new settings.mask text.
                Masker = SecretMasker.ForScenario(scenario, environment: null),
                Rerecorder = rerecorder,
                Replay = replay,
            };

            // Every step's output is in memory, so the task has already completed.
            var (events, _) = GenerateAsync(scenario, seed, generateOptions, replay.RunAsync).GetAwaiter().GetResult();
            var filterMap = new int[events.Count + 1];
            events = CastOscFilter.RemovePrivateSequences(events, filterMap);

            // The same st:steps tag as the CLI writes, so a cast from here gives the CLI's next recording its steps.
            using var castWriter = new StringWriter();
            WriteCast(castWriter, scenario, events, outputs.Shell, timestamp, renderSettings, Rerecording.MapIndices(rerecorder.Slots, filterMap));
            var castText = castWriter.ToString();
            cast = Encoding.UTF8.GetBytes(castText);

            if (renderSvg)
            {
                // As on the CLI, the SVG comes from the cast as written, with --max-fps applied again.
                var written = CastReader.ReadText(castText);
                svg = SvgRender.RenderToUtf8(written.Events, written.Width, written.Height, RenderSettingsResolver.WithImageBackground(written.RenderSettings, renderSettings) with { MaxFps = renderSettings.MaxFps }, written.LoopDuration);
                if (SvgRender.SizeWarning(svg.LongLength, renderSettings.MaxFps, fromCast: false) is { } sizeWarning)
                    Console.Error.WriteLine($"Warning: {sizeWarning}");
            }
        }, ref error);

        return new PlaygroundResult(cast, svg, replay.Steps, warnings, notApplied, error);
    }

    internal static PlaygroundResult RenderCast(string castText, PlaygroundOptions options)
    {
        var warnings = new List<string>();
        string? error = null;
        byte[]? svg = null;

        CaptureWarnings(warnings, () =>
        {
            if (!TryParseOptions(options, out var themePreset, out var window, out var fontSize, out var fontFamily, out var maxFps, out var style, out error))
                return;

            CastRecording recording;
            try
            {
                recording = CastReader.ReadText(castText);
            }
            catch (CastReadException ex)
            {
                error = ex.Message;
                return;
            }

            var settings = RenderSettingsResolver.ApplySvgOverrides(recording.RenderSettings, fontSize, fontFamily, themePreset, window, maxFps, out var overrideError);
            if (overrideError.Length != 0)
            {
                error = overrideError;
                return;
            }

            settings = style.Apply(settings);
            foreach (var note in RenderSettingsResolver.FindIneffectiveStyle(settings))
                Console.Error.WriteLine($"Warning: {note}");
            svg = SvgRender.RenderToUtf8(recording.Events, recording.Width, recording.Height, settings, recording.LoopDuration);
            if (SvgRender.SizeWarning(svg.LongLength, settings.MaxFps, fromCast: true) is { } sizeWarning)
                Console.Error.WriteLine($"Warning: {sizeWarning}");
        }, ref error);

        return new PlaygroundResult(null, svg, [], warnings, [], error);
    }

    /// <summary>The scenario keys that set something the playground cannot do, with why, for the UI.</summary>
    internal static List<string> FindNotApplied(Scenario scenario, StepOutputs outputs)
    {
        var notes = new List<string>();
        // Every sample sets the shell it was recorded with, so only another shell is worth a note.
        if (!string.IsNullOrWhiteSpace(scenario.Shell) && !string.Equals(ShellEnvName(scenario.Shell.Trim()), outputs.Shell, StringComparison.OrdinalIgnoreCase))
            notes.Add($"shell: the output comes from the recording, made with {outputs.Shell}");
        if (!string.IsNullOrWhiteSpace(scenario.Cwd))
            notes.Add("cwd: no command runs in the browser");
        if (scenario.Pre is { Count: > 0 } || scenario.Post is { Count: > 0 })
            notes.Add("pre, post: no command runs in the browser, and their output is never in a cast");
        if ((scenario.Steps ?? []).Select(ParseCommand).Any(static command => IsStepFlagSet(command.Extra, "keep-output")))
            notes.Add("keep-output: there is no earlier cast in the browser, so each step shows the recorded output");
        if (scenario.Outputs is { Count: > 0 })
            notes.Add("outputs: the browser shows one SVG with the render options above, and the CLI writes the files listed");
        return notes;
    }

    private static bool TryParseOptions(
        PlaygroundOptions options,
        out string? themePreset,
        out WindowStyle? window,
        out int? fontSize,
        out string? fontFamily,
        out int? maxFps,
        out RenderStyleOverrides style,
        out string? error)
    {
        themePreset = null;
        window = null;
        fontSize = null;
        fontFamily = null;
        maxFps = null;
        style = RenderStyleOverrides.None;
        error = null;
        string message;

        if (!string.IsNullOrWhiteSpace(options.Theme))
        {
            if (!RenderSettingsResolver.TryParsePreset(options.Theme, out var preset, out message))
            {
                error = message;
                return false;
            }

            themePreset = preset;
        }

        if (!string.IsNullOrWhiteSpace(options.Window))
        {
            if (!RenderSettingsResolver.TryParseWindow(options.Window, out var parsedWindow, out message))
            {
                error = message;
                return false;
            }

            window = parsedWindow;
        }

        if (options.FontSize is int size)
        {
            if (!RenderSettingsResolver.TryParseFontSize(size.ToString(System.Globalization.CultureInfo.InvariantCulture), out var parsedSize, out message))
            {
                error = message;
                return false;
            }

            fontSize = parsedSize;
        }

        if (options.FontFamily is not null)
        {
            if (!RenderSettingsResolver.TryParseFontFamily(options.FontFamily, out var parsedFamily, out message))
            {
                error = message;
                return false;
            }

            fontFamily = parsedFamily;
        }

        if (options.MaxFps is int fps)
        {
            if (!RenderSettingsResolver.TryParseMaxFps(fps.ToString(System.Globalization.CultureInfo.InvariantCulture), out var parsedFps, out message))
            {
                error = message;
                return false;
            }

            maxFps = parsedFps;
        }

        BackgroundSpec? background = null;
        if (!string.IsNullOrWhiteSpace(options.Background))
        {
            if (!RenderSettingsResolver.TryParseBackground(options.Background, options.ImageSource, out var parsedBackground, out message))
            {
                error = message;
                return false;
            }

            background = parsedBackground;
        }

        if (options.Margin is int margin && !RenderSettingsResolver.IsValidMargin(margin))
        {
            error = $"margin must be between {RenderSettingsResolver.MinMargin} and {RenderSettingsResolver.MaxMargin} pixels: {margin}";
            return false;
        }

        if (options.Padding is int padding && !RenderSettingsResolver.IsValidPadding(padding))
        {
            error = $"padding must be between {RenderSettingsResolver.MinPadding} and {RenderSettingsResolver.MaxPadding} pixels: {padding}";
            return false;
        }

        if (options.Opacity is double opacity && !RenderSettingsResolver.IsValidOpacity(opacity))
        {
            error = $"opacity must be between {RenderSettingsResolver.MinOpacity} and {RenderSettingsResolver.MaxOpacity}: {RenderSettingsResolver.FormatOpacity(opacity)}";
            return false;
        }

        if (options.Blur is int blur && !RenderSettingsResolver.IsValidBlur(blur))
        {
            error = $"blur must be between {RenderSettingsResolver.MinBlur} and {RenderSettingsResolver.MaxBlur} pixels: {blur}";
            return false;
        }

        if (options.EndHold is double endHold
            && !RenderSettingsResolver.TryParseEndHold(endHold.ToString(System.Globalization.CultureInfo.InvariantCulture), out _, out message))
        {
            error = message;
            return false;
        }

        if (options.FadeOut is double fadeOut
            && !RenderSettingsResolver.TryParseFadeOut(fadeOut.ToString(System.Globalization.CultureInfo.InvariantCulture), out _, out message))
        {
            error = message;
            return false;
        }

        FitMode? fit = null;
        if (!string.IsNullOrWhiteSpace(options.Fit))
        {
            if (!RenderSettingsResolver.TryParseFit(options.Fit, out var parsedFit, out message))
            {
                error = message;
                return false;
            }

            fit = parsedFit;
        }

        style = new RenderStyleOverrides(
            options.WindowTitle is null ? null : RenderSettingsResolver.NormalizeWindowTitle(options.WindowTitle),
            background,
            options.Margin,
            options.Padding,
            options.Opacity,
            options.Blur,
            options.Loop,
            options.EndHold,
            options.FadeOut,
            fit);
        return true;
    }

    /// <summary>
    /// Runs <paramref name="action"/> with stderr captured: the shared code warns on stderr, as the CLI prints it, and
    /// the playground lists the warnings instead. An exception becomes <paramref name="error"/>, because one that
    /// crosses the WASM boundary stops the runtime.
    /// </summary>
    private static void CaptureWarnings(List<string> warnings, Action action, ref string? error)
    {
        var previous = Console.Error;
        using var captured = new StringWriter();
        Console.SetError(captured);
        try
        {
            action();
        }
        catch (Exception ex)
        {
            error ??= $"render failed: {ex.Message}";
        }
        finally
        {
            Console.SetError(previous);
        }

        foreach (var line in captured.ToString().Split('\n'))
        {
            var text = line.TrimEnd('\r');
            if (text.Length == 0)
                continue;
            warnings.Add(text.StartsWith("Warning: ", StringComparison.Ordinal) ? text["Warning: ".Length..] : text);
        }
    }
}
