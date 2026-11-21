using System.Globalization;
using System.Text;

namespace Scenetake;

record ShellLaunch(string FileName, string[] Arguments, string DisplayName);

enum OutputFormat
{
    Cast,
    Svg,
    Mp4,
    Png,
    Gif,
}

/// <summary>
/// <c>--window-title</c>, <c>--background</c>, <c>--margin</c>, and <c>--padding</c>, which the scenario path and the
/// svg and mp4 commands share. Each replaces the scenario's or the cast's value.
/// </summary>
sealed class StyleOptions
{
    internal string? WindowTitle { get; set; }
    internal BackgroundSpec? Background { get; set; }
    internal int? Margin { get; set; }
    internal int? Padding { get; set; }
    internal double? Opacity { get; set; }
    internal int? Blur { get; set; }
    internal bool? Loop { get; set; }
    internal double? EndHold { get; set; }
    internal double? FadeOut { get; set; }
    internal double? Speed { get; set; }
    internal FitMode? Fit { get; set; }
    internal int? CropTop { get; set; }
    internal int? CropBottom { get; set; }
    internal int? CropLeft { get; set; }
    internal int? CropRight { get; set; }

    internal int? Crop(string edge) => edge switch
    {
        "top" => CropTop,
        "bottom" => CropBottom,
        "left" => CropLeft,
        _ => CropRight,
    };

    internal void SetCrop(string edge, int cells)
    {
        switch (edge)
        {
            case "top": CropTop = cells; break;
            case "bottom": CropBottom = cells; break;
            case "left": CropLeft = cells; break;
            default: CropRight = cells; break;
        }
    }

    internal RenderStyleOverrides ToOverrides() =>
        new(WindowTitle, Background, Margin, Padding, Opacity, Blur, Loop, EndHold, FadeOut, Fit, CropTop, CropBottom, CropLeft, CropRight, Speed);
}

/// <summary>
/// <c>scenetake capture</c>: one command recorded without a YAML file. It becomes the scenario that a file with one
/// step would hold, and the scenario path records that as it records a file: the typed command, its output, masking,
/// and every output format. The command runs in a PTY unless <c>--pipe</c> is given, so tools color their output
/// as they do in a terminal.
/// </summary>
sealed class CaptureRequest
{
    /// <summary>The command as one argument, as a step's <c>run</c> holds it.</summary>
    internal string? Command { get; set; }

    /// <summary>The command's words after <c>--</c>, which the caller's shell already split.</summary>
    internal string[]? Words { get; set; }

    internal int? Width { get; set; }
    internal int? Height { get; set; }
    internal string? Shell { get; set; }
    internal double? Timeout { get; set; }
    internal bool Pipe { get; set; }

    /// <summary>The command line that the recording types and the shell runs.</summary>
    internal string CommandLine { get; private set; } = "";

    /// <summary>The scenario as YAML. Its text seeds the typing, as a file's does, so the same capture types the same way.</summary>
    internal string Yaml { get; private set; } = "";

    /// <summary>
    /// <c>--save-scenario</c>: where to write <see cref="Yaml"/>, so the capture goes on as a scenario that can take
    /// more steps. The recording is then named after it, as running that file would name it.
    /// </summary>
    internal string? SavePath { get; set; }

    /// <summary>
    /// Builds the scenario, and the path a scenario file for it would have: the files are written next to it, in the
    /// current folder, named after the command (<c>git status</c> writes <c>git.cast</c> and <c>git.svg</c>), or the
    /// <c>--save-scenario</c> file. The style options become its <c>render</c> keys, so the saved file records the
    /// same look without them.
    /// </summary>
    internal bool TryFinish(int? fontSize, string? fontFamily, string? theme, WindowStyle? window, StyleOptions style, out string scenarioPath, out string error)
    {
        scenarioPath = "";
        error = "";
        if (Command is not null && Words is not null)
        {
            error = $"capture takes one command: give it as one argument or after --, not both: {Command}";
            return false;
        }

        CommandLine = Words is not null ? JoinWords(Words) : Command ?? "";
        if (string.IsNullOrWhiteSpace(CommandLine))
        {
            error = "capture needs a command: scenetake capture [options] -- <command>";
            return false;
        }

        scenarioPath = SavePath is not null
            ? Path.GetFullPath(SavePath)
            : Path.Combine(Directory.GetCurrentDirectory(), OutputName() + ".yaml");

        var yaml = new StringBuilder();
        // A saved scenario is edited next, so it names the editor schema as init's does. The line seeds no typing, so it
        // changes no recording.
        if (SavePath is not null)
            yaml.Append(ScenarioSchema.ModeLine);
        yaml.Append("title: ").Append(QuoteYaml(CommandLine)).Append('\n');
        if (Width is int width)
            yaml.Append("width: ").Append(width.ToString(CultureInfo.InvariantCulture)).Append('\n');
        if (Height is int height)
            yaml.Append("height: ").Append(height.ToString(CultureInfo.InvariantCulture)).Append('\n');
        if (Shell is not null)
            yaml.Append("shell: ").Append(QuoteYaml(Shell)).Append('\n');
        AppendRender(yaml, fontSize, fontFamily, theme, window, style, Path.GetDirectoryName(scenarioPath)!);
        yaml.Append("steps:\n  - run: ").Append(QuoteYaml(CommandLine)).Append('\n');
        if (!Pipe)
            yaml.Append("    pty: true\n");
        if (Timeout is double timeout)
            yaml.Append("    timeout: ").Append(timeout.ToString("R", CultureInfo.InvariantCulture)).Append('\n');
        Yaml = yaml.ToString();
        return true;
    }

    /// <summary>
    /// The style options as <c>render</c> keys, in the order the README lists them, or nothing without any. An image
    /// background is written relative to <paramref name="folder"/>, where the scenario reads it from.
    /// </summary>
    private static void AppendRender(StringBuilder yaml, int? fontSize, string? fontFamily, string? theme, WindowStyle? window, StyleOptions style, string folder)
    {
        var start = yaml.Length;
        void Key(string name, string value) => yaml.Append("  ").Append(name).Append(": ").Append(value).Append('\n');
        static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);
        static string Whole(int value) => value.ToString(CultureInfo.InvariantCulture);

        if (fontSize is int size)
            Key("font-size", Whole(size));
        if (fontFamily is not null)
            Key("font-family", QuoteYaml(fontFamily));
        if (window is WindowStyle chrome)
            Key("window", chrome switch { WindowStyle.Macos => "macos", WindowStyle.Windows => "windows", _ => "none" });
        if (style.WindowTitle is not null)
            Key("window-title", QuoteYaml(style.WindowTitle));
        if (style.Background is { } background)
        {
            Key("background", QuoteYaml(background.ImagePath is { } image
                ? Path.GetRelativePath(folder, image).Replace('\\', '/')
                : string.Join(' ', background.Colors)));
        }

        if (style.Margin is int margin)
            Key("margin", Whole(margin));
        if (style.Padding is int padding)
            Key("padding", Whole(padding));
        if (style.Opacity is double opacity)
            Key("opacity", Number(opacity));
        if (style.Blur is int blur)
            Key("blur", Whole(blur));
        if (style.Loop is bool loop)
            Key("loop", loop ? "true" : "false");
        if (style.EndHold is double endHold)
            Key("end-hold", Number(endHold));
        if (style.FadeOut is double fadeOut)
            Key("fade-out", Number(fadeOut));
        if (style.Speed is double speed)
            Key("speed", Number(speed));
        if (style.Fit is FitMode fit)
            Key("fit", fit switch { FitMode.None => "none", FitMode.Width => "width", FitMode.Both => "both", _ => "height" });
        foreach (var edge in (ReadOnlySpan<string>)["top", "bottom", "left", "right"])
        {
            if (style.Crop(edge) is int cells)
                Key("crop-" + edge, Whole(cells));
        }

        if (theme is not null)
            yaml.Append("  theme:\n    preset: ").Append(QuoteYaml(theme)).Append('\n');
        if (yaml.Length != start)
            yaml.Insert(start, "render:\n");
    }

    /// <summary>
    /// The words as one command line for the shell that runs it. A word with a space, a quote, or another character a
    /// shell reads is quoted for that shell, so it reaches the command as the caller's shell passed it.
    /// </summary>
    private string JoinWords(string[] words)
    {
        var shell = Shell ?? (OperatingSystem.IsWindows() ? "pwsh" : Environment.GetEnvironmentVariable("SHELL") ?? "bash");
        var name = Path.GetFileNameWithoutExtension(shell.Trim()).ToLowerInvariant();
        var line = new StringBuilder();
        foreach (var word in words)
        {
            if (line.Length > 0)
                line.Append(' ');
            if (word.Length > 0 && IsPlainWord(word))
                line.Append(word);
            else if (name is "pwsh" or "powershell")
                line.Append('\'').Append(word.Replace("'", "''")).Append('\'');
            else if (name is "cmd")
                line.Append('"').Append(word.Replace("\"", "\"\"")).Append('"');
            else
                line.Append('\'').Append(word.Replace("'", "'\\''")).Append('\'');
        }

        return line.ToString();

        // Letters, digits, and the punctuation that no shell reads specially in a word.
        static bool IsPlainWord(string word)
        {
            foreach (var c in word)
            {
                if (!char.IsAsciiLetterOrDigit(c) && "-_./=:,+@%^".IndexOf(c) < 0)
                    return false;
            }

            return true;
        }
    }

    /// <summary>The command's name without its folder and extension, such as <c>git</c> or <c>build</c>, or <c>capture</c>.</summary>
    private string OutputName()
    {
        var first = Words is { Length: > 0 } ? Words[0] : CommandLine.TrimStart().Split([' ', '\t'], 2)[0].Trim('"', '\'');
        var file = Path.GetFileNameWithoutExtension(first.Replace('\\', '/').Split('/')[^1]);
        var name = new StringBuilder();
        foreach (var c in file)
        {
            if (char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.')
                name.Append(c);
        }

        var trimmed = name.ToString().Trim('.');
        return trimmed.Length > 0 ? trimmed : "capture";
    }

    /// <summary>A YAML double-quoted scalar.</summary>
    private static string QuoteYaml(string text)
    {
        var quoted = new StringBuilder("\"");
        foreach (var c in text)
        {
            quoted.Append(c switch
            {
                '\\' => "\\\\",
                '"' => "\\\"",
                '\n' => "\\n",
                '\r' => "\\r",
                '\t' => "\\t",
                < ' ' => $"\\x{(int)c:x2}",
                _ => c.ToString(),
            });
        }

        return quoted.Append('"').ToString();
    }
}

/// <summary>mp4 options from the command line. Unset values take the <see cref="Mp4Settings"/> defaults.</summary>
sealed class Mp4Options
{
    internal int? Fps { get; set; }
    internal int? Crf { get; set; }
    internal (int Width, int Height)? MaxSize { get; set; }
    internal (int Width, int Height)? Size { get; set; }
    internal string? FontFile { get; set; }

    /// <summary>The first mp4 option given, for errors when mp4 output is not selected.</summary>
    internal string? FirstOption { get; set; }

    /// <summary>The first of them that only video takes: every one but <c>--font-file</c>, which a PNG takes too.</summary>
    internal string? FirstVideoOption { get; set; }

    /// <summary><c>--crf</c> when given: H.264's quality, which only mp4 takes.</summary>
    internal string? FirstCrfOption { get; set; }

    /// <summary>The first of <c>--fps</c>, <c>--max-size</c>, and <c>--size</c>, which mp4 and GIF take.</summary>
    internal string? FirstFrameOption { get; set; }

    /// <summary>
    /// The settings a GIF uses: those of mp4, with the GIF's own default frame rate, raised with
    /// <paramref name="speed"/> as an mp4's is (<see cref="Mp4Settings.DefaultFpsAt"/>).
    /// </summary>
    internal Mp4Settings ToGifSettings(double speed) =>
        ToSettings(speed) with { Fps = Fps ?? Mp4Settings.DefaultFpsAt(GifRender.DefaultFps, GifRender.MaxFps, speed) };

    /// <summary>The mp4 settings. Without <c>--fps</c>, the frame rate follows the playback <paramref name="speed"/>.</summary>
    internal Mp4Settings ToSettings(double speed) => new(
        Fps ?? Mp4Settings.DefaultFpsAt(Mp4Settings.DefaultFps, Mp4Settings.MaxFps, speed),
        Crf ?? Mp4Settings.DefaultCrf,
        MaxSize?.Width ?? 0,
        MaxSize?.Height ?? 0,
        Size?.Width ?? 0,
        Size?.Height ?? 0,
        FontFile);
}
