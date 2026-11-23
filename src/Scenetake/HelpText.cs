namespace Scenetake;

/// <summary>
/// The text of --help for the scenario path and the svg, mp4 and init commands. Each option says what it
/// does, its range and its default, so the README is not needed to use one. ASCII only, as error messages
/// are, so every console code page shows it.
/// </summary>
static class HelpText
{
    internal const string Intro = """
        Records a YAML scenario as an asciinema v3 cast, and optionally as an animated SVG, an mp4,
        a GIF, or a PNG of one screen. The steps run for real: ordinary steps are shown with
        simulated typing, pty steps run in a pseudo-terminal.

        """;

    internal const string Commands = """
        Usage:
          scenetake [options] <scenario.yaml> [output]   Run a scenario and record it
          scenetake [options] <scenario.yaml>...         Record several scenarios, one after another
          scenetake svg [options] <input.cast> [output]  Convert a cast file to an animated SVG
          scenetake mp4 [options] <input.cast> [output]  Convert a cast file to an mp4 (needs ffmpeg)
          scenetake png [options] <input.cast> [output]  Draw one screen of a cast file as a PNG
          scenetake gif [options] <input.cast> [output]  Convert a cast file to a GIF (needs ffmpeg)
          scenetake capture [options] -- <command>       Record one command without a scenario file
          scenetake init [--shell NAME] [scenario.yaml]  Write a commented starter scenario
          scenetake --version                            Print the version
          scenetake svg|mp4|png|gif|capture|init --help  Show the options of that command

        [output] names the files without an extension: 'out' writes out.cast and out.svg, and an
        extension given is replaced. Without it, the files are named after the scenario, next to it.

        Several scenarios, or a wildcard in the file name such as 'docs/*.yaml', are recorded one
        after another with the same options, each next to its YAML. One that fails does not stop
        the others, and the exit code is 1 when one was not recorded.

        """;

    internal const string CaptureIntro = """
        Usage: scenetake capture [options] -- <command>
               scenetake capture [options] "<command>"

        Records one command without a scenario file, as a scenario with that one step would: the
        command is typed, then its output follows. Without --format it writes an SVG, and the cast
        next to it. The files go to the current folder, named after the command ('git status'
        writes git.cast and git.svg), unless -o names them. --save-scenario keeps it as a scenario
        file, to add steps to and record again.

        """;

    internal const string CaptureOptions = """
        Capture:
          --width N                Terminal columns, 1-512 (default 80)
          --height N               Terminal rows, 1-512 (default 24)
          --shell NAME             Shell that runs the command: bash, pwsh, powershell, or a path
                                   (default: $SHELL or bash, pwsh on Windows)
          --pipe                   Capture the output through pipes instead of a PTY. In a PTY, the
                                   default, tools see a terminal and color their output.
          --timeout N              Stop the command after N seconds, as a step's timeout does, for a
                                   command that does not end by itself (default: none)
          -o, --output NAME        Name the files without an extension: '-o demo' writes demo.cast
                                   and demo.svg
          --save-scenario FILE     Write the command as a scenario to FILE first, with the style
                                   options as its render keys, and name the files after FILE.
                                   'scenetake --format svg FILE' then records it again. FILE must
                                   not exist.

        """;

    /// <summary>
    /// The recording options. <c>capture</c> writes an SVG when no format is given, a scenario only the cast. A
    /// capture has no steps of its own to set <c>continue-on-error</c> on, no <c>post</c>, and no interactive step,
    /// so its <c>--strict</c> and <c>--verbose</c> say only what applies to one command.
    /// </summary>
    internal static string Recording(bool capture = false) =>
        capture
            ? """
                Recording:
                  --format cast|svg|mp4|png|gif
                                           Write the cast and an SVG (svg, the default), an mp4, a PNG, or a
                                           GIF rendered from it, or only the cast (cast). One format a run:
                                           convert the cast for others, such as 'scenetake gif git.cast'.
                  --strict                 Write nothing, and exit 1, if the command exits non-zero (default:
                                           record it, with a warning). A --save-scenario file stays.
                  --verbose                Show phase markers, and the type and stack of an error that stops
                                           the run

                """
            : """
                Recording:
                  --format cast|svg|mp4|png|gif
                                           Write only the cast (cast), or the cast and an SVG, an mp4, a
                                           PNG, or a GIF rendered from it. Without --format, the cast and
                                           the files the scenario lists under 'outputs:', each with its
                                           own options. One format a run here: list several under
                                           'outputs:', or convert the cast, such as 'scenetake gif demo.cast'.
                  --save-keys FILE         Write the keys typed in the 'interactive: true' steps to FILE as
                                           'keys' entries, which replay them without a person. Written even
                                           when the run stops at a later step. On Windows a password typed
                                           in a step is saved too.
                  --save-outputs FILE      Write what each step printed to FILE (JSON), for --replay.
                                           Secrets are hidden as in the cast.
                  --replay FILE            Run no command: take each step's output from FILE, written by
                                           --save-outputs. Changes to typing, comments, highlights, timing,
                                           and render apply. A step whose run changed needs a new recording.
                                           pre and post do not run.
                  --rerun                  Run every step, also those that set 'rerun: false' and show the
                                           earlier cast's output without running
                  --strict                Stop at the first step that exits non-zero unless it sets
                                           'continue-on-error: true'. Write no output but the --save-keys
                                           file, and run post. Exit 1. If a post command fails, return that
                                           command's exit code.
                  --verbose                Show pre and post labels and phase markers, and the type and stack
                                           of an error that stops the run

                """;

    /// <summary>
    /// The rendering options. On a scenario they override its <c>render</c> settings, the theme only as a
    /// preset under the scenario's own colors. On a cast they replace what the cast header stores.
    /// </summary>
    /// <param name="animation">Whether to list the options of an animation, which a PNG does not take.</param>
    internal static string Render(bool scenario, bool animation = true) =>
        (scenario
            ? "Rendering for SVG, mp4, and PNG (overrides the scenario's render settings, and is stored in the cast):\n"
            : "Rendering (overrides what the cast header stores, and the default applies when neither sets it):\n")
        + """
              --font-size N            Font size, 1-128 (default 16)
              --font-family FAMILIES   CSS font-family list, such as "'JetBrains Mono', monospace"

            """
        + (scenario
            ? """
                  --theme NAME             Color theme preset (default dark). The scenario's render.theme fg,
                                           bg and palette still apply on top of it.

                """
            : """
                  --theme NAME             Color theme, replacing the colors the cast stores (default dark)

                """)
        + """
                                       Names: dark, light, macos-dark, macos-light, dracula, nord,
                                       one-dark, gruvbox-dark, catppuccin-mocha, tokyo-night,
                                       solarized-dark, solarized-light, github-dark, github-light

            """
        + """
              --window none|macos|windows
                                       Draw a window title bar around the terminal (default none)
              --window-title TEXT      Text in the title bar (default: the title the scenario or cast has).
                                       "" shows none.
              --background VALUE       Fill around the terminal: a color such as #1e3c72, 2-4 colors for a
                                       gradient from the top-left corner ("#1e3c72 #2a5298"), or a .png,
                                       .jpg, .gif or .webp file that covers it. A cast keeps colors only.
              --margin N               Pixels around the terminal or window, 0-200 (default 8, 12 with a
                                       window, 32 with a background, and mp4 has one only with a background)
              --padding N              Pixels between the terminal's edge and its text, 0-100 (default from
                                       the font size)
              --opacity N              How much of the window's own fill covers the background, 0-1
                                       (default 1). 0.85 lets the background show through the terminal's
                                       default background and the title bar, and text stays opaque. Needs
                                       --background.
              --blur N                 Blur the background seen through a translucent window, 0-50 pixels
                                       (default 0). Needs --opacity below 1.
              --fit none|height|width|both
                                       Move the bottom edge (height), the right edge (width), or both in to
                                       the last row and column the recording shows, the cursor included
                                       (default height: no empty rows below the content). none draws the
                                       whole terminal. The cast keeps the terminal's size.
              --crop-top N, --crop-bottom N, --crop-left N, --crop-right N
                                       Cut N rows or columns from that edge before --fit, 0-511 (default 0)

            """
        + (animation
            ? """
                  --end-hold N             Seconds the final screen stays before the SVG starts again or the
                                           mp4 ends, 0-60 (default: as long as the pause before the last output)
                  --fade-out N             Fade the terminal's contents out over N seconds after the end hold,
                                           0-10 (default 0). The window and the background stay.
                  --speed N                Play the SVG, mp4, or GIF N times as fast as recorded, 0.25-4
                                           (default 1), such as 1.5. Typing, pauses, and output all speed up.
                                           --end-hold and --fade-out stay seconds of the output.
                  --length N               Play the SVG, mp4, or GIF in at most N seconds, 1-600: waits in
                                           the output shrink first, then pauses, then typing, each only down
                                           to what stays readable. A shorter recording stays as it is. Not
                                           with --speed.

                """
            : "");

    internal const string SvgOptions = """
        SVG (--format svg, or the svg command):
          --max-fps N              Cap the animation frame rate, 1-120. 0 keeps every frame (default).
          --no-loop                Play the animation once and stop on the final screen (default: loop)
          --loop                   Loop, also when the scenario or the cast says not to

        """;

    internal const string StillOptions = """
        Still image (--format png, the png command, or --at with SVG):
          --at TIME                Draw the screen shown TIME seconds into the recording, such as 2.5,
                                   end for the final screen, or a step's id for the screen right after
                                   that step's output. A PNG shows the final screen without it.
                                   With SVG, --at makes a still SVG without animation, which --max-fps,
                                   --no-loop, --end-hold, --fade-out and --speed do not apply to. TIME
                                   is the recording's, as the cast's line shows it, at any --speed.

        """;

    internal const string PngOptions = """
        PNG (the png command, or --format png):
          --at TIME                Draw the screen shown TIME seconds into the recording, such as 2.5,
                                   end for the final screen (the default), or a step's id for the
                                   screen right after that step's output
          --font-file PATH         A font file (TTF, OTF, TTC) to use before the --font-family fonts

        The PNG is the frame an mp4 shows at that time, at the terminal's own size, with a palette
        when it has 256 colors or fewer. It needs no ffmpeg, except to decode a --background image.

        """;

    internal const string PngIntro = """
        Usage: scenetake png [options] <input.cast> [output]

        Draws one screen of an asciinema v2 or v3 cast file as a PNG. [output] names the PNG. Without
        it, the PNG is written next to the cast with the same name and .png.

        """;

    internal const string Mp4Options = """
        mp4 (--format mp4 or the mp4 command, needs ffmpeg with libx264):
          --fps N                  Frame rate, 1-60 (default 30, times --speed above 1: 60 at 2x)
          --crf N                  H.264 quality, 1-51, where a higher value makes a smaller file (default 23)
          --max-size WxH           Scale down to fit in WxH, keeping the shape and never scaling up.
                                   1280x1024 fits X (Twitter) uploads.
          --size WxH               Make the video exactly WxH (both even): the terminal is centered on
                                   the theme background, and a larger one is scaled down to fit.
                                   Not with --max-size.
          --size W:H               Pad the video to the shape W:H, such as 16:9, at the terminal's own
                                   size, with the terminal centered. --max-size can scale it down.
                                   Without --size, a video wider than 3:1, the widest X takes, gets
                                   space above and below up to 3:1.
          --font-file PATH         A font file (TTF, OTF, TTC) to use before the --font-family fonts.
                                   PNG and GIF take it too.

        """;

    internal const string GifOptions = """
        GIF (--format gif or the gif command, needs ffmpeg):
          --fps N                  Frame rate, 1-50 (default 20, times --speed above 1: 40 at 2x). A GIF
                                   counts each frame's delay in hundredths of a second. A lower rate
                                   makes a full-screen program smaller, and changes little for typing.
          --max-size WxH           Scale down to fit in WxH, keeping the shape and never scaling up.
                                   Scaling blurs text and can make a GIF larger. For a smaller GIF,
                                   such as Zenn's 3MB, use a smaller --font-size.
          --size WxH               Make the GIF exactly WxH (both even), as for mp4. Not with --max-size.
          --size W:H               Pad the GIF to the shape W:H, as for mp4. A GIF without --size keeps
                                   the terminal's shape.
          --no-loop                Play the GIF once and stop on the final screen (default: loop)
          --loop                   Loop, also when the scenario or the cast says not to
          --font-file PATH         A font file (TTF, OTF, TTC) to use before the --font-family fonts

        The frames are those of an mp4, with one palette of 256 colors for the whole GIF.

        """;

    internal const string GifIntro = """
        Usage: scenetake gif [options] <input.cast> [output]

        Converts an asciinema v2 or v3 cast file to an animated GIF with ffmpeg (any build). [output]
        names the GIF. Without it, the GIF is written next to the cast with the same name and .gif.

        """;

    internal const string Environment = """
        Environment:
          SCENETAKE_FFMPEG         The ffmpeg to use for mp4 and GIF, and for a PNG's background image,
                                   instead of the one on PATH

        """;

    internal const string More = """
        Scenario format, samples and output modes: https://github.com/guitarrapc/scenetake

        """;

    internal const string SvgIntro = """
        Usage: scenetake svg [options] <input.cast> [output]

        Converts an asciinema v2 or v3 cast file to an animated SVG. [output] names the SVG. Without
        it, the SVG is written next to the cast with the same name and .svg.

        """;

    internal const string Mp4Intro = """
        Usage: scenetake mp4 [options] <input.cast> [output]

        Converts an asciinema v2 or v3 cast file to an H.264 mp4 with ffmpeg (libx264). [output]
        names the mp4. Without it, the mp4 is written next to the cast with the same name and .mp4.
        Install ffmpeg with 'winget install Gyan.FFmpeg' or 'scoop install ffmpeg' (Windows),
        'brew install ffmpeg' (macOS), or 'sudo apt install ffmpeg' (Debian, Ubuntu).

        """;

    internal const string Init = """
        Usage: scenetake init [--shell bash|pwsh|powershell] [scenario.yaml]

        Writes a commented starter scenario to scenario.yaml in the current folder, or to the path
        given. An existing file is not overwritten.

          --shell bash|pwsh|powershell
                                   Write the steps for this shell and set 'shell:' in the file.
                                   Without it, the steps suit the default shell: pwsh on Windows,
                                   $SHELL elsewhere.

        """;
}
