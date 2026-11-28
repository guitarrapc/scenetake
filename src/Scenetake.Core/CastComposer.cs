using System.Buffers;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

namespace Scenetake.Core;

/// <summary>
/// Composition of a step's typing and output into cast events, and the cast writer. Shared by the CLI and
/// the playground.
/// </summary>
internal static partial class ScenarioCast
{
    /// <summary>What a <c>clear: true</c> step writes: the cursor home, then the screen erased, as <c>clear</c> writes for xterm.</summary>
    internal const string ClearScreen = "\u001b[H\u001b[2J";

    private const string HideCursor = "\u001b[?25l";
    private const string ShowCursor = "\u001b[?25h";

    /// <summary>
    /// What a <c>card</c> step writes: the cursor hidden, the screen cleared, and each line centered in
    /// <paramref name="window"/> (<see cref="CardWindow"/>), with its <c>[style]</c> prefix as its color. An empty
    /// line keeps its row. The text is masked as the recording's other text is, which keeps its width. With
    /// <paramref name="markCorner"/>, the window's last cell gets a bold space, which shows nothing but is not a blank
    /// cell, so fit keeps the blank rows and columns around the text and a still of the card stays centered.
    /// </summary>
    /// <summary>What a card writes after the cursor move to its window's last cell when it marks the corner.</summary>
    internal const string CardCornerMark = "\u001b[1m " + SgrReset;

    internal static string ComposeCard(IReadOnlyList<string> lines, CanvasWindow window, SecretMasker? masker, bool markCorner)
    {
        var card = new StringBuilder(HideCursor).Append(ClearScreen);
        var top = window.Top + Math.Max(0, (window.Height - lines.Count) / 2);
        var bottom = window.Top + window.Height;
        var right = window.Left + window.Width;
        // The mark's cell must hold no text: below the card, or right of its last line, which a window as tall as the
        // card still has when it keeps a blank column on each side.
        var lastWidth = lines.Count == 0 ? 0 : DisplayWidth(SplitCardStyle(lines[^1]).Text);
        var lastEnd = window.Left + Math.Max(0, (window.Width - lastWidth) / 2) + lastWidth;
        if (markCorner && (top + lines.Count < bottom || lastEnd < right))
            card.Append("\u001b[").Append(bottom).Append(';').Append(right).Append('H').Append(CardCornerMark);
        for (var i = 0; i < lines.Count; i++)
        {
            var (styleName, raw) = SplitCardStyle(lines[i]);
            if (raw.Length == 0)
                continue;

            var text = masker?.MaskText(raw) ?? raw;
            var style = "";
            if (styleName is not null && !TryParseStyle(styleName, out style))
            {
                Warn("card", $"line {i + 1}", $"unknown color/style '{styleName}'");
                style = "";
            }

            var column = window.Left + Math.Max(0, (window.Width - DisplayWidth(text)) / 2);
            card.Append("\u001b[").Append(top + i + 1).Append(';').Append(column + 1).Append('H');
            card.Append(style).Append(text);
            if (style.Length > 0)
                card.Append(SgrReset);
        }

        return card.ToString();
    }

    /// <summary>
    /// The error for a card that the crop would cut: more lines than the rows it leaves, or a line wider than its
    /// columns. The crop comes from render settings and the command line, so this runs once they are known, before
    /// anything runs. Null when every card fits.
    /// </summary>
    internal static string? FindCardCropError(Scenario scenario, CropSpec crop)
    {
        if (crop.IsEmpty)
            return null;

        var rows = (scenario.Height ?? RenderSettingsResolver.DefaultTerminalRows) - crop.Top - crop.Bottom;
        var columns = (scenario.Width ?? RenderSettingsResolver.DefaultTerminalCols) - crop.Left - crop.Right;
        // A crop that leaves nothing has an error of its own.
        if (rows < 1 || columns < 1)
            return null;

        var steps = scenario.Steps ?? [];
        for (var i = 0; i < steps.Count; i++)
        {
            if (ParseCommand(steps[i]).Card is not { } lines)
                continue;
            if (lines.Count > rows)
                return $"Step {i + 1} card has {lines.Count} lines, more than the {rows} rows the crop leaves. Leave lines out, or crop less.";
            for (var j = 0; j < lines.Count; j++)
            {
                if (DisplayWidth(SplitCardStyle(lines[j]).Text) is var width && width > columns)
                    return $"Step {i + 1} card line {j + 1} is {width} columns wide, wider than the {columns} columns the crop leaves. Shorten it, or crop less.";
            }
        }

        return null;
    }

    /// <summary>A step's <c>id</c>, which <c>--at</c> names, or null.</summary>
    private static string? StepIdOf(CommandEntry command) =>
        command.Extra.TryGetValue("id", out var id) ? id?.ToString() : null;

    /// <summary>A card as the time per step names it: <c>card: </c> and its first line with text, as the card shows it.</summary>
    private static string CardLabel(List<string> lines, SecretMasker? masker)
    {
        foreach (var line in lines)
        {
            var text = SplitCardStyle(line).Text.Trim();
            if (text.Length > 0)
                return "card: " + Rerecording.Shorten(masker?.MaskUncounted(text) ?? text);
        }

        return "card";
    }

    /// <summary>
    /// The part of the terminal a card is centered in: the window the rest of the recording shows with the scenario's
    /// fit and crop, found as the render finds it, so a card neither sits in cut cells nor grows the canvas that fit
    /// keeps. Where fit moves an edge in, the window keeps a blank row or column on each side of the largest card. The
    /// events hold each card as a cleared screen, which shows nothing.
    /// </summary>
    private static CanvasWindow CardWindow(List<CastEvent> events, int tallest, int widest, int width, int height, FitMode fit, CropSpec crop)
    {
        var croppedWidth = width - crop.Left - crop.Right;
        var croppedHeight = height - crop.Top - crop.Bottom;
        // The crop is checked before anything runs. A crop that leaves nothing never gets here from the CLI.
        if (croppedWidth < 1 || croppedHeight < 1)
            return CanvasWindow.Whole(width, height);

        var shown = TerminalReplay.ResolveCanvas(events, width, height, crop, fit, TerminalTheme.ForMeasuring());
        var rows = fit is FitMode.Height or FitMode.Both ? Math.Min(Math.Max(shown.Height, tallest + 2), croppedHeight) : croppedHeight;
        var columns = fit is FitMode.Width or FitMode.Both ? Math.Min(Math.Max(shown.Width, widest + 2), croppedWidth) : croppedWidth;
        return new CanvasWindow(crop.Left, crop.Top, columns, rows);
    }

    /// <summary>
    /// Records the scenario's steps as cast events. <paramref name="run"/> gives each step's output: the CLI
    /// runs the command, and the playground takes the output recorded in an outputs file.
    /// </summary>
    internal static async Task<(List<CastEvent> Events, FailedStep? StrictFailure)> GenerateAsync(
        Scenario scenario,
        GenerateOptions options,
        StepRunner run)
    {
        var settings = scenario.Settings ?? new ScenarioSettings();
        var promptTemplate = ParsePrompt(settings.Prompt);
        // The folder a step starts in, as {dir} shows it: from the shell's state on the CLI, from the outputs file in a
        // replay. Null when the prompt does not show it, so the outputs file keeps no folder names it does not need.
        string? StepFolder(string run, bool pty, bool interactive) =>
            !promptTemplate.UsesDir ? null
            : options.Replay is { } replay ? replay.Folder(run, pty, interactive) ?? HomeFolderName
            : MaskFolder(options.PromptFolder?.Invoke() ?? HomeFolderName);
        // The outputs file keeps the folder name masked, as the prompt shows it: a secret that masking finds stays on
        // this machine. An outputs file already holds it masked.
        string MaskFolder(string folder) => options.Masker is { } folderMasker ? folderMasker.MaskUncounted(folder) : folder;
        // A prompt drawn at every step: masked without counting, so a folder name is not counted once per step.
        string Prompt(string? folder) =>
            promptTemplate.Render(folder ?? "") is var prompt && options.Masker is { } promptMasker ? promptMasker.MaskUncounted(prompt) : prompt;
        // settings.shell-prompt: the prompt the shell drew for the next command, as the cast shows it, before masking.
        // From the shell's state on the CLI, from the outputs file in a replay. Null shows settings.prompt.
        var shellPrompt = settings.ShellPrompt == true;
        string? ShellPrompt(string run, bool pty, bool interactive) =>
            !shellPrompt ? null
            : options.Replay is { } replay ? replay.Prompt(run, pty, interactive)
            : options.ShellPrompt?.Invoke() is { } drawn ? PromptTemplate.ShellPrompt(drawn) : null;
        // A drawn prompt is masked and counted as output is, unlike settings.prompt: the Masked line then names a computer
        // name that only the prompt shows. A value counts once, however many prompts show it.
        string? MaskShellPrompt(string? drawn, bool count) =>
            drawn is null || options.Masker is not { } shellPromptMasker ? drawn
            : count ? shellPromptMasker.MaskText(drawn)
            : shellPromptMasker.MaskUncounted(drawn);
        var speed     = settings.TypingSpeed ?? DefaultSpeed;
        var jitter    = settings.TypingJitter ?? DefaultJitter;
        var preDelay  = settings.PreDelay ?? DefaultPreDelay;
        var postDelay = settings.PostDelay ?? DefaultPostDelay;
        var defaultExecutionDuration = settings.ExecutionDuration ?? DefaultExecutionDuration;
        var defaultStderrStyle = ParseStyleOrFallback(settings.StderrColor, SgrNamed(DefaultStderrColorSpec), "settings.stderr-color");
        var events = new List<CastEvent>();
        // Each step types from a random sequence of its own, seeded by the step itself (TypingRandom), so an edit to a
        // comment, the look, or another step leaves its typing as it was.
        var typingSeen = new Dictionary<ulong, int>();
        Random TypingRandom(bool pty, bool interactive, StepShow show, string maskedRun)
        {
            var id = Rerecording.StepId(pty, interactive, show, maskedRun);
            var occurrence = typingSeen.GetValueOrDefault(id);
            typingSeen[id] = occurrence + 1;
            return new Random(TypingSeed(id, occurrence));
        }

        double t   = 0.5;
        // The pauses of the last step shown, which set how long the final screen stays.
        var lastPost = 0.0;
        var lastPre = 0.0;
        CastCursorTracker? cursorTracker = null;
        // Whether a step has been shown yet. It does not depend on what the step printed, so a step that printed
        // nothing the cast keeps lays out the steps after it as in any other recording of the scenario.
        var anyStepShown = false;
        // A card stays on the screen, with the cursor hidden, until the next shown step clears it. Its event is drawn
        // after the last step, when the window it is centered in is known (CardWindow).
        var cardOnScreen = false;
        // Two lists rather than a list of tuples: a generic over a new value type adds its own code to the AOT binary.
        var cardEvents = new List<int>();
        var cardLines = new List<List<string>>();

        var steps = scenario.Steps ?? [];
        for (var i = 0; i < steps.Count; i++)
        {
            // Each step starts on a whole millisecond. The cast stores intervals between times rounded to milliseconds, so
            // a step that types longer than before would otherwise move the rounding of every step after it by 1ms.
            t = CeilingToMillisecond(t);
            // Where this step begins in the time per step: the first shown step holds the moment before it too.
            var stepStart = options.Times is { Count: > 0 } ? t : 0;
            var command = ParseCommand(steps[i]);
            if (command.Card is { } card)
            {
                // A card shows lines centered on a cleared screen, such as a title before the demo and an install
                // line after it, without a command: the same on every OS and in the playground, which runs nothing.
                // pre-delay passes before the card shows, and the card stays for its post-delay.
                t += GetDouble(command.Extra, preDelay, "pre-delay");
                anyStepShown = true;
                cardEvents.Add(events.Count);
                cardLines.Add(card);
                events.Add(CastEvent.Output(Math.Round(t, 6), HideCursor + ClearScreen));
                cardOnScreen = true;
                t += GetDouble(command.Extra, CardPostDelay, "post-delay");
                options.Times?.Add(new StepTime(i + 1, CardLabel(card, options.Masker), stepStart) { End = t, Id = StepIdOf(command), At = events[^1].Time });
                continue;
            }

            if (command.Clear)
            {
                // A clear step clears the screen as `clear` does, without a command: the same in a terminal
                // without TERM, on every OS, and in the playground, which runs nothing. pre-delay passes before it clears.
                lastPre = GetDouble(command.Extra, preDelay, "pre-delay");
                t += lastPre;
                anyStepShown = true;
                events.Add(CastEvent.Output(Math.Round(t, 6), cardOnScreen ? ClearScreen + ShowCursor : ClearScreen));
                cardOnScreen = false;
                if (TryFormatNameComment(command.Name, "clear", out var clearNameLine, out var clearNameText))
                {
                    // The cursor is home, so the comment needs no line break before it.
                    var clearMasker = options.Masker;
                    events.Add(CastEvent.Marker(Math.Round(t, 6), clearMasker?.MaskText(clearNameText) ?? clearNameText));
                    events.Add(CastEvent.Output(Math.Round(t, 6), clearMasker?.MaskText(clearNameLine) ?? clearNameLine));
                    t += 0.05;
                }

                options.Times?.Add(new StepTime(i + 1, clearNameText.Length > 0 ? Rerecording.Shorten(options.Masker?.MaskUncounted(clearNameText) ?? clearNameText) : "clear", stepStart) { End = t, Id = StepIdOf(command), At = t });
                lastPost = GetDouble(command.Extra, postDelay, "post-delay");
                t += lastPost;
                continue;
            }

            if (string.IsNullOrWhiteSpace(command.Cmd)) continue;

            if (IsStepFlagSet(command.Extra, "background"))
            {
                // The command starts and keeps running while the next steps record, such as a server that a later
                // step calls, and is stopped before post. With wait, the next step starts when its output shows the
                // text. The recording shows nothing of it, and the playground skips it.
                if (options.LogRunning)
                    Console.Error.WriteLine($"  starting (background): {command.Cmd}");
                var (waitFor, waitLimit) = GetBackgroundWait(command.Extra);
                var started = await run(new StepStart(
                    i, command, false, false,
                    scenario.Width ?? RenderSettingsResolver.DefaultTerminalCols,
                    scenario.Height ?? RenderSettingsResolver.DefaultTerminalRows,
                    null)
                {
                    Show = StepShow.None,
                    Background = true,
                    WaitFor = waitFor,
                    WaitLimit = waitLimit,
                });
                if (started.BackgroundProblem is { } problem)
                {
                    // The steps after it call what it starts, so they would fail in the recording without saying why.
                    if (!GetBool(command.Extra, false, "continue-on-error"))
                        return (events, new FailedStep(i + 1, command.Cmd, started.ExitCode, FailureOutputTail(started)) { Background = true, Problem = problem });
                    Console.Error.WriteLine($"Warning: background step {i + 1} {problem}, and the steps go on (continue-on-error): {command.Cmd}");
                }

                continue;
            }

            var show = GetStepShow(command.Extra);
            if (show == StepShow.None)
            {
                // The command runs between the steps around it, as pre runs before them, and the recording shows
                // nothing of it. Nothing it does reaches the cast or the outputs file, so the playground skips it.
                if (options.LogRunning)
                    Console.Error.WriteLine($"  running (show: none): {command.Cmd}");
                var hiddenPty = GetBool(command.Extra, false, "pty");
                var hiddenInteractive = GetBool(command.Extra, false, "interactive");
                var hiddenSpeed = GetDouble(command.Extra, speed, "typing-speed");
                var hiddenRandom = TypingRandom(hiddenPty, hiddenInteractive, StepShow.None, options.Masker?.MaskUncounted(command.Cmd) ?? command.Cmd);
                var hidden = await run(new StepStart(
                    i, command, hiddenPty, hiddenInteractive,
                    scenario.Width ?? RenderSettingsResolver.DefaultTerminalCols,
                    scenario.Height ?? RenderSettingsResolver.DefaultTerminalRows,
                    null)
                {
                    Keys = BuildKeyActions(command.Extra, hiddenSpeed, GetDouble(command.Extra, jitter, "typing-jitter"), hiddenRandom),
                    Timeout = GetStepTimeout(command.Extra),
                    Show = StepShow.None,
                });
                if (hiddenInteractive && hidden.PtyInputChunks is { } hiddenInput)
                    options.SavedKeys?.Add(new SavedStepKeys(i + 1, command.Cmd, hiddenSpeed, hiddenInput, hidden.HiddenInputTimes ?? []));
                // The steps after it rely on what it prepares, and the recording would not show why they fail, so a
                // failure stops the recording, with or without --strict, as a failing pre command does.
                if (hidden.ExitCode != 0 && !GetBool(command.Extra, false, "continue-on-error"))
                    return (events, new FailedStep(i + 1, command.Cmd, hidden.ExitCode, FailureOutputTail(hidden)) { Hidden = true });
                continue;
            }

            var cmdSpeed  = GetDouble(command.Extra, speed, "typing-speed");
            var cmdJitter = GetDouble(command.Extra, jitter, "typing-jitter");
            var cmdPre    = GetDouble(command.Extra, preDelay, "pre-delay");
            var cmdPost   = GetDouble(command.Extra, postDelay, "post-delay");
            var cmdExecutionDuration = GetDouble(command.Extra, defaultExecutionDuration, "execution-duration");
            var cmdCastTiming = ResolveCastTiming(settings, command.Extra);
            var cmdStderrStyle = GetOverrideStyle(command.Extra, "stderr-color", defaultStderrStyle, "stderr-color", command.Cmd);
            var cmdHighlights = GetHighlights(command.Extra, command.Cmd);
            var hasRunHighlight = TryGetStepStyle(command.Extra, "run-highlight", "run-highlight", command.Cmd, out var runHighlightStyle);
            var usePty = GetBool(command.Extra, false, "pty");
            var useInteractive = GetBool(command.Extra, false, "interactive");
            var normalizeTyping = GetBool(command.Extra, false, "normalize-typing");
            var continueOnError = GetBool(command.Extra, false, "continue-on-error");
            // Read once, so a value that is not true or false warns once, also when the earlier output cannot be reused.
            var keepOutput = options.Rerecorder is not null && GetBool(command.Extra, false, "keep-output");
            // A terminal gets stderr and stdout as one stream, and a person draws an interactive step's screen, so these
            // keys have nothing to color there. They used to do nothing without a word.
            if (usePty && command.Extra.ContainsKey("stderr-color"))
                Warn("stderr-color", command.Cmd, "does nothing with pty: true, where stderr and stdout are one stream: color it with the program's own options");
            if (useInteractive && cmdHighlights is not null)
            {
                Warn("highlight", command.Cmd, "does nothing with interactive: true, whose screen a person draws");
                cmdHighlights = null;
            }

            anyStepShown = true;
            lastPre = cmdPre;
            lastPost = cmdPost;

            // After a card the demo goes on from a cleared screen, as after a clear step, with the cursor shown again.
            var afterCard = cardOnScreen;
            // Taken before the command runs: a cd in it moves the next step's prompt, not its own, and so does a PS1.
            var stepFolder = StepFolder(command.Cmd, usePty, useInteractive);
            var stepShellPrompt = ShellPrompt(command.Cmd, usePty, useInteractive);
            var recordedPrompt = MaskShellPrompt(stepShellPrompt, count: false);
            if (cardOnScreen)
            {
                events.Add(CastEvent.Output(Math.Round(t, 6), ClearScreen + ShowCursor));
                cardOnScreen = false;
            }

            // Secrets are hidden in everything the recording shows: the comment line, the typed command, and the
            // output below. The outputs file gets the hidden output too, so a replay never sees the secret.
            var masker = options.Masker;
            if (TryFormatNameComment(command.Name, command.Cmd, out var nameLine, out var nameDisplayText))
            {
                // The cursor is home after a card's clear. The line break check looks past escape sequences, and
                // would see the card's text.
                var prefix = afterCard ? "" : CastEventLayout.NameCommentPrefixForLastEvent(events);
                events.Add(CastEvent.Marker(Math.Round(t, 6), masker?.MaskText(nameDisplayText) ?? nameDisplayText));
                events.Add(CastEvent.Output(Math.Round(t, 6), prefix + (masker?.MaskText(nameLine) ?? nameLine)));
                t += 0.05;
            }

            // show: output leaves out the prompt and the typing: the output appears as if the program drew it by itself.
            // A replay types the run text as the outputs file holds it, masked by the recording, which knew the
            // secrets that pre and earlier steps exported. It masks it again for what the YAML has added.
            var typed = options.Replay?.MaskedRun(command.Cmd, usePty, useInteractive) ?? command.Cmd;
            // Seeded by the text as the cast shows it, after masking: a seed from a secret would let the typing confirm
            // a guess of it.
            var stepRandom = TypingRandom(usePty, useInteractive, show, masker?.MaskUncounted(typed) ?? typed);
            // pre-delay passes with the prompt on the screen, as a person pauses before typing, or before the output
            // of a step that types nothing. The prompt itself shows as soon as the step before has paused for its
            // post-delay, as a shell draws it when the command ends.
            var typingFrom = t + cmdPre + 0.05;
            if (show == StepShow.All)
                EmitTypedCommand(events, ref t, MaskShellPrompt(stepShellPrompt, count: true) ?? Prompt(stepFolder), cmdPre, masker?.MaskText(typed) ?? typed, cmdSpeed, cmdJitter, stepRandom, hasRunHighlight, runHighlightStyle);
            else
                t += cmdPre;
            var stepTyping = show == StepShow.All ? t - typingFrom : 0;
            // The step as the time per step names it: by its name, or by its run text as the cast shows it.
            string StepLabel(string run) =>
                Rerecording.Shorten(nameDisplayText.Length > 0 ? masker?.MaskUncounted(nameDisplayText) ?? nameDisplayText : run);

            var width = scenario.Width ?? RenderSettingsResolver.DefaultTerminalCols;
            var height = scenario.Height ?? RenderSettingsResolver.DefaultTerminalRows;
            // ConPTY addresses rows from where it believes its cursor starts. Telling it where the recording's
            // cursor is keeps a redrawn line on its own row instead of row 1. A Unix PTY has no screen model and
            // passes `\r` through. An interactive step draws on the host terminal too, whose cursor is elsewhere.
            var ptyStartCursor = usePty && !useInteractive && options.TrackPtyCursor
                ? (cursorTracker ??= new CastCursorTracker(width, height)).Advance(events)
                : ((int Row, int Column)?)null;

            // rerun: false shows the earlier cast's output without running the command: a slow step, one that costs
            // money or changes something, or an interactive step recorded once by hand. A replay runs nothing anyway,
            // and --rerun runs every step.
            if (!GetBool(command.Extra, true, "rerun") && options.Replay is null && !options.RerunAll && options.Rerecorder is { } reuser)
            {
                var reusedRun = masker?.MaskUncounted(command.Cmd) ?? command.Cmd;
                if (reuser.Reuse(
                    events,
                    i + 1,
                    reusedRun,
                    Rerecording.StepId(usePty, useInteractive, show, reusedRun),
                    keepOutput,
                    t,
                    t + cmdExecutionDuration,
                    ptyStartCursor,
                    width,
                    height) is double reusedEnd)
                {
                    if (options.LogRunning)
                        Console.Error.WriteLine($"  not run (rerun: false): {command.Cmd}");
                    options.Times?.Add(new StepTime(i + 1, StepLabel(reusedRun), stepStart) { Typing = stepTyping, Output = reusedEnd - t, End = reusedEnd, Id = StepIdOf(command), At = reusedEnd });
                    t = reusedEnd + cmdPost;
                    continue;
                }
            }

            if (options.LogRunning)
                Console.Error.WriteLine($"  running: {command.Cmd}");
            // The keys' typing delays come from the step's own sequence, after its typed command. The playground ignores
            // the keys, and the steps after them type the same way in both, as each has a sequence of its own.
            var keys = BuildKeyActions(command.Extra, cmdSpeed, cmdJitter, stepRandom);
            var execution = await run(new StepStart(i, command, usePty, useInteractive, width, height, ptyStartCursor)
            {
                Keys = keys,
                Timeout = GetStepTimeout(command.Extra),
                Show = show,
            });
            // Before the exit code is looked at: a --strict stop at this step keeps what the person typed too.
            if (useInteractive && execution.PtyInputChunks is { } typedInput)
                options.SavedKeys?.Add(new SavedStepKeys(i + 1, command.Cmd, cmdSpeed, typedInput, execution.HiddenInputTimes ?? []));
            // A step that is meant to fail (a demo of an error) says so with continue-on-error: no warning, and
            // --strict goes on. Any other failure is a warning, or with --strict the end of the recording: later
            // steps may depend on this one, and they run real commands.
            if (execution.ExitCode != 0 && !continueOnError)
            {
                if (options.Strict)
                    return (events, new FailedStep(i + 1, command.Cmd, execution.ExitCode, FailureOutputTail(execution)));
                WarnStepExitCode(command.Name, command.Cmd, execution.ExitCode);
            }

            var commandStart = t;
            var outputFirst = events.Count;
            // A replay starts from the text the recording masked, with what only the recording knew, such as an
            // exported secret, and masks it again for what the YAML has added since.
            var unmaskedRun = execution.MaskedRun ?? command.Cmd;
            var maskedRun = masker?.MaskUncounted(unmaskedRun) ?? unmaskedRun;
            if (execution.Placeholder is { } placeholder)
            {
                // The playground has no output for this command. The line stands where its output would be,
                // as plain output that the step's highlight and stderr-color do not touch.
                events.Add(CastEvent.Output(Math.Round(commandStart + cmdExecutionDuration, 6), NormalizeNewlines(placeholder)));
                t = commandStart + cmdExecutionDuration;
            }
            else if (execution.ComposedOutput is { } replayedOutput)
            {
                t = EmitComposedPtyOutput(events, commandStart, masker?.MaskComposed(replayedOutput) ?? replayedOutput);
            }
            else if (execution.IsPty && useInteractive)
            {
                var composed = ComposeInteractivePtyOutput(
                    execution.PtyByteChunks!,
                    execution.PtyInputChunks,
                    normalizeTyping,
                    cmdSpeed,
                    cmdCastTiming,
                    width,
                    height,
                    options.Verbose);
                composed = masker?.MaskComposed(composed) ?? composed;
                options.Record?.Add(RecordedStep.Interactive(command.Cmd, execution.ExitCode, composed, maskedRun, stepFolder, recordedPrompt));
                t = EmitComposedPtyOutput(events, commandStart, composed);
            }
            else if (execution.IsPty)
            {
                var chunks = execution.PtyLeadingInitRemoved
                    ? execution.PtyByteChunks!
                    : RemovePtyLeadingInit(execution.PtyByteChunks!, options.Verbose);
                chunks = masker?.MaskPty(chunks) ?? chunks;
                options.Record?.Add(RecordedStep.Pty(command.Cmd, execution.ExitCode, ptyStartCursor, chunks, maskedRun, stepFolder, recordedPrompt));
                t = EmitPtyStepOutput(
                    events,
                    commandStart,
                    chunks,
                    cmdExecutionDuration,
                    cmdCastTiming,
                    options.Verbose,
                    cmdHighlights,
                    command.Cmd);
            }
            else
            {
                var cuts = masker is not null && cmdCastTiming.UsesStreamEmission()
                    ? StreamedPipeCuts(execution.PipeStreamChunks!, cmdStderrStyle)
                    : null;
                var pipeChunks = masker?.MaskPipe(execution.PipeStreamChunks!, cuts) ?? execution.PipeStreamChunks!;
                options.Record?.Add(RecordedStep.Pipe(command.Cmd, execution.ExitCode, pipeChunks, maskedRun, stepFolder, recordedPrompt));
                t = EmitPipeStepOutput(
                    events,
                    commandStart,
                    pipeChunks,
                    cmdStderrStyle,
                    cmdHighlights,
                    command.Cmd,
                    cmdExecutionDuration,
                    cmdCastTiming);
            }

            CastOscFilter.RemovePrivateSequencesFrom(events, outputFirst);

            // Output timed by the wall clock (PTY, interactive, stream emit) ends at a time whose fraction of a
            // millisecond differs from run to run. The cast stores each interval as the difference of absolute
            // times rounded to milliseconds, so that fraction moved later typed intervals by 1ms, and a cast
            // regenerated from the same scenario differed on lines after the step. Going on from a whole
            // millisecond keeps them the same; only the output and the first interval after it still differ.
            var clock = execution.IsPty || cmdCastTiming.UsesStreamEmission();
            if (clock)
                t = CeilingToMillisecond(t);

            // Recording again to the same file keeps a step that prints the same, with its timing, so only the steps
            // whose output changed differ. It happens here, so the steps after it are laid out on what the cast shows.
            if (options.Rerecorder is { } rerecorder)
            {
                // Streamed pipe output is colored one read at a time, so its colors depend on how the reads fell.
                var colorsFollowReads = clock && !execution.IsPty;
                t = rerecorder.Finish(
                    events,
                    new StepSlot(
                        i + 1,
                        maskedRun,
                        Rerecording.StepId(usePty, useInteractive, show, maskedRun),
                        Rerecording.TimingHash(
                            cmdExecutionDuration,
                            cmdCastTiming,
                            normalizeTyping,
                            GetStepTimeout(command.Extra),
                            useInteractive || keys.Count > 0,
                            cmdSpeed,
                            cmdJitter,
                            command.Extra.GetValueOrDefault("keys"),
                            masker,
                            colorsFollowReads ? Rerecording.DescribeColors(cmdStderrStyle, cmdHighlights) : null),
                        clock,
                        keepOutput,
                        outputFirst,
                        events.Count - outputFirst,
                        commandStart,
                        t,
                        commandStart + cmdExecutionDuration),
                    ptyStartCursor,
                    width,
                    height,
                    colorsFollowReads);
            }

            options.Times?.Add(new StepTime(i + 1, StepLabel(maskedRun), stepStart) { Typing = stepTyping, Output = t - commandStart, End = t, Id = StepIdOf(command), At = t });
            t += cmdPost;
        }

        // The closing prompt shows the folder the last step left, which the outputs file keeps for a replay, also when
        // the recording ends without the prompt: a replay may draw it. A closing card stays to the end, without a prompt
        // over it: an event that changes nothing ends the recording after its post-delay, or the card would show only
        // for the hold after the last event.
        if (promptTemplate.UsesDir)
            options.EndFolder = options.Replay is { } endReplay ? endReplay.EndFolder ?? HomeFolderName : MaskFolder(options.PromptFolder?.Invoke() ?? HomeFolderName);
        var endShellPrompt = !shellPrompt ? null
            : options.Replay is { } endPromptReplay ? endPromptReplay.EndPrompt
            : options.ShellPrompt?.Invoke() is { } endDrawn ? PromptTemplate.ShellPrompt(endDrawn) : null;
        options.EndShellPrompt = MaskShellPrompt(endShellPrompt, count: false);
        if (cardOnScreen)
        {
            events.Add(CastEvent.Output(Math.Round(t, 6), HideCursor));
        }
        else if (anyStepShown && settings.EndPrompt == true)
        {
            // end-prompt: the closing prompt shows after the last step's post-delay, as each step's prompt does. The
            // final screen stays 2 x (post-delay + pre-delay) after the last output: an event that changes nothing ends
            // the recording, and the render holds the screen as long again. An end-hold leaves the event out
            // (TerminalReplay.WithoutHoldEvent).
            events.Add(CastEvent.Output(Math.Round(t, 6), MaskShellPrompt(endShellPrompt, count: true) ?? Prompt(options.EndFolder)));
            var closingHold = lastPost / 2 + lastPre;
            if (closingHold > 0)
                events.Add(CastEvent.Output(Math.Round(t + closingHold, 6), SgrReset));
        }
        else if (anyStepShown)
        {
            // The recording ends on the last output: the cursor goes as soon as it ends, so the line below does not
            // look like a command that is still running, and the final screen stays 2 x (post-delay + pre-delay), as
            // with the prompt. The render holds it as long again as the gap to the event that ends the recording.
            var outputEnd = t - lastPost;
            events.Add(CastEvent.Output(Math.Round(outputEnd, 6), HideCursor));
            var finalHold = lastPost + lastPre;
            if (finalHold > 0)
                events.Add(CastEvent.Output(Math.Round(outputEnd + finalHold, 6), SgrReset));
        }

        if (cardEvents.Count > 0)
        {
            var cardWidth = scenario.Width ?? RenderSettingsResolver.DefaultTerminalCols;
            var cardHeight = scenario.Height ?? RenderSettingsResolver.DefaultTerminalRows;
            var tallest = 0;
            var widest = 0;
            foreach (var lines in cardLines)
            {
                tallest = Math.Max(tallest, lines.Count);
                foreach (var line in lines)
                    widest = Math.Max(widest, DisplayWidth(SplitCardStyle(line).Text));
            }

            var window = CardWindow(events, tallest, widest, cardWidth, cardHeight, options.Fit, options.Crop);
            for (var c = 0; c < cardEvents.Count; c++)
                events[cardEvents[c]] = CastEvent.Output(events[cardEvents[c]].Time, ComposeCard(cardLines[c], window, options.Masker, options.Fit != FitMode.None));
        }

        return (events, null);
    }

    /// <summary>
    /// The next whole millisecond at or after <paramref name="seconds"/>. Rounding to a microsecond first keeps
    /// a value such as 12.345, which is 12345.000000000002 ms as a double, at 12.345.
    /// </summary>
    internal static double CeilingToMillisecond(double seconds) =>
        Math.Ceiling(Math.Round(seconds * 1000, 3)) / 1000;

    internal static CastTimingSettings ResolveCastTiming(ScenarioSettings settings, Dictionary<string, object?>? stepExtra)
    {
        var silenceThreshold = settings.SilenceThreshold ?? DefaultSilenceThreshold;
        var maxIdle = settings.MaxIdle ?? DefaultMaxIdle;
        double? maxDuration = settings.MaxDuration;
        var streamOutput = settings.StreamOutput ?? false;
        if (stepExtra is null)
            return new CastTimingSettings(silenceThreshold, maxIdle, maxDuration, streamOutput);

        return new CastTimingSettings(
            GetDouble(stepExtra, silenceThreshold, "silence-threshold"),
            GetDouble(stepExtra, maxIdle, "max-idle"),
            GetOptionalDouble(stepExtra, maxDuration, "max-duration"),
            GetBool(stepExtra, streamOutput, "stream-output"));
    }

    internal static double EmitPipeStepOutput(
        List<CastEvent> events,
        double commandStart,
        IReadOnlyList<PipeStreamChunk> chunks,
        string stderrStyle,
        List<HighlightSpec>? highlights,
        string cmd,
        double executionDuration,
        CastTimingSettings timing)
    {
        if (chunks.Count == 0)
            return commandStart + executionDuration;

        if (!timing.UsesStreamEmission())
            return EmitPipeStepOutputInstant(events, commandStart, chunks, stderrStyle, highlights, cmd, executionDuration);

        var timedChunks = BuildStartupAdjustedPipeChunks(chunks, executionDuration, stderrStyle);
        ApplyBurstMergeToChunks(timedChunks, chunks);
        ApplyCastTimingToChunks(timedChunks, timing);
        var coalesced = CastTimingPipeline.CoalesceTimedText(timedChunks);
        EmitTimedTextCastEvents(events, commandStart, coalesced, highlights, cmd);
        return Math.Max(commandStart + executionDuration, commandStart + coalesced[^1].Time);
    }

    internal static double EmitPipeStepOutputInstant(
        List<CastEvent> events,
        double commandStart,
        IReadOnlyList<PipeStreamChunk> chunks,
        string stderrStyle,
        List<HighlightSpec>? highlights,
        string cmd,
        double executionDuration)
    {
        var (stdout, stderr) = CollectPipeOutputs(chunks);
        var merged = MergePipeOutput(stdout, stderr, stderrStyle);
        if (string.IsNullOrEmpty(merged))
            return commandStart + executionDuration;

        var output = highlights is not null
            ? ApplyHighlights(merged, highlights, cmd)
            : merged;
        events.Add(CastEvent.Output(
            Math.Round(commandStart + executionDuration, 6),
            NormalizeNewlines(output)));
        return commandStart + executionDuration;
    }

    /// <summary>
    /// A pipe step's stdout, then its stderr in <paramref name="stderrStyle"/>. Output with a NUL in it stays: a terminal
    /// draws nothing for a NUL, as the cast's player and the renderers do, so <c>find -print0</c> shows as it does there,
    /// and as a streamed step shows it.
    /// </summary>
    internal static string MergePipeOutput(string stdout, string stderr, string stderrStyle)
    {
        if (string.IsNullOrEmpty(stdout))
        {
            if (string.IsNullOrEmpty(stderr) || string.IsNullOrEmpty(stderrStyle) || ContainsAnsiSgr(stderr))
                return stderr;

            return WrapWithStylePreserveTrailingNewlines(stderr, stderrStyle);
        }

        if (string.IsNullOrEmpty(stderr))
            return stdout;

        if (string.IsNullOrEmpty(stderrStyle) || ContainsAnsiSgr(stderr))
            return string.Concat(stdout, stderr);

        return string.Concat(stdout, WrapWithStylePreserveTrailingNewlines(stderr, stderrStyle));
    }

    internal static (string Stdout, string Stderr) CollectPipeOutputs(IReadOnlyList<PipeStreamChunk> chunks)
    {
        if (chunks.Count == 0)
            return ("", "");

        StringBuilder? stdoutBuilder = null;
        StringBuilder? stderrBuilder = null;
        foreach (var chunk in chunks)
        {
            if (chunk.IsStderr)
            {
                stderrBuilder ??= new StringBuilder();
                stderrBuilder.Append(chunk.Text);
                continue;
            }

            stdoutBuilder ??= new StringBuilder();
            stdoutBuilder.Append(chunk.Text);
        }

        return (stdoutBuilder?.ToString() ?? "", stderrBuilder?.ToString() ?? "");
    }

    internal static void ApplyBurstMergeToChunks(
        List<(double Time, string Text)> chunks,
        IReadOnlyList<PipeStreamChunk> rawChunks)
    {
        if (chunks.Count <= 1)
            return;

        var count = rawChunks.Count;
        var useStack = count <= 256;
        double[]? rentedOriginal = null;
        double[]? rentedTimes = null;
        Span<double> originalSeconds = useStack
            ? stackalloc double[count]
            : (rentedOriginal = ArrayPool<double>.Shared.Rent(count)).AsSpan(0, count);
        Span<double> times = useStack
            ? stackalloc double[count]
            : (rentedTimes = ArrayPool<double>.Shared.Rent(count)).AsSpan(0, count);
        try
        {
            for (var i = 0; i < rawChunks.Count; i++)
            {
                originalSeconds[i] = rawChunks[i].Time.TotalSeconds;
                times[i] = chunks[i].Time;
            }

            CastTimingPipeline.MergeBurstGaps(times, originalSeconds, DefaultBurstThreshold);
            for (var i = 0; i < chunks.Count; i++)
                chunks[i] = (times[i], chunks[i].Text);
        }
        finally
        {
            if (rentedOriginal is not null)
                ArrayPool<double>.Shared.Return(rentedOriginal, clearArray: false);
            if (rentedTimes is not null)
                ArrayPool<double>.Shared.Return(rentedTimes, clearArray: false);
        }
    }

    /// <summary>
    /// A PTY step's output without the terminal setup that a shell or ConPTY writes before the command's own
    /// output (<see cref="PtyLeadingInitFilter"/>). Each chunk keeps its time. The outputs file stores this
    /// list, so the playground starts from it without the filter.
    /// </summary>
    internal static List<TimedBytes> RemovePtyLeadingInit(IReadOnlyList<TimedBytes> chunks, bool verbose)
    {
        var ptyFilter = new PtyLeadingInitFilter();
        var result = new List<TimedBytes>(chunks.Count);
        foreach (var chunk in chunks)
        {
            var data = ptyFilter.Process(chunk.Data);
            if (!data.IsEmpty)
                result.Add(new TimedBytes(chunk.Time, data));
        }

        var tail = ptyFilter.Complete();
        if (!tail.IsEmpty)
            result.Add(new TimedBytes(chunks.Count > 0 ? chunks[^1].Time : TimeSpan.Zero, tail));

        if (verbose)
            Console.Error.WriteLine($"scenetake: pty cleanup: stripped {ptyFilter.StrippedByteCount} bytes");
        return result;
    }

    /// <summary>
    /// Adds a PTY step's output (<see cref="RemovePtyLeadingInit"/>) to the cast, timed by the step's settings, with the
    /// step's <paramref name="highlights"/> (<see cref="ScenarioCast.ApplyPtyHighlights"/>). The output file keeps the
    /// chunks without them, so a replay colors them as the scenario says then.
    /// </summary>
    internal static double EmitPtyStepOutput(
        List<CastEvent> events,
        double commandStart,
        IReadOnlyList<TimedBytes> chunks,
        double executionDuration,
        CastTimingSettings timing,
        bool verbose,
        List<HighlightSpec>? highlights = null,
        string cmd = "")
    {
        if (chunks.Count == 0)
            return commandStart + executionDuration;

        var startupOffset = PtyCastTiming.ComputeStartupOffset(chunks[0].Time.TotalSeconds, executionDuration);
        if (verbose && startupOffset > 0)
            Console.Error.WriteLine($"scenetake: pty startup compress: {startupOffset:F3}s");

        var timedChunks = new List<(double Time, double OriginalTime, ReadOnlyMemory<byte> Data)>(chunks.Count);
        foreach (var chunk in chunks)
        {
            var chunkSeconds = chunk.Time.TotalSeconds;
            timedChunks.Add((PtyCastTiming.AdjustChunkSeconds(chunkSeconds, startupOffset), chunkSeconds, chunk.Data));
        }

        if (timing.UsesStreamEmission())
        {
            ApplyBurstMergeToPtyChunks(timedChunks);
            ApplyCastTimingToPtyChunks(timedChunks, timing);
        }

        var coalescedInput = new List<(double Time, ReadOnlyMemory<byte> Data)>(timedChunks.Count);
        for (var i = 0; i < timedChunks.Count; i++)
            coalescedInput.Add((timedChunks[i].Time, timedChunks[i].Data));
        var coalesced = CastTimingPipeline.CoalesceTimedUtf8(coalescedInput);
        if (highlights is null)
        {
            foreach (var (time, data) in coalesced)
            {
                events.Add(CastEvent.OutputUtf8(
                    Math.Round(commandStart + time, 6),
                    data));
            }
        }
        else
        {
            // Each chunk ends on a whole character (CoalesceTimedUtf8), so each decodes by itself.
            var reads = new string[coalesced.Count];
            for (var i = 0; i < coalesced.Count; i++)
                reads[i] = Encoding.UTF8.GetString(coalesced[i].Data);
            var painted = ApplyPtyHighlights(reads, highlights, cmd);
            for (var i = 0; i < coalesced.Count; i++)
            {
                if (painted[i].Length > 0)
                    events.Add(CastEvent.Output(Math.Round(commandStart + coalesced[i].Time, 6), painted[i]));
            }
        }

        return Math.Max(commandStart + executionDuration, commandStart + coalesced[^1].Time);
    }

    /// <summary>
    /// An interactive step's output, timed as the cast has it: times since the command started, and where the
    /// step ends. The outputs file stores this, not the input log, which on Windows holds every key typed.
    /// </summary>
    internal static ComposedPtyOutput ComposeInteractivePtyOutput(
        IReadOnlyList<TimedBytes> chunks,
        IReadOnlyList<TimedBytes>? inputChunks,
        bool normalizeTyping,
        double typingSpeed,
        CastTimingSettings castTiming,
        int width,
        int height,
        bool verbose)
    {
        var ptyFilter = new PtyLeadingInitFilter();
        var timedChunks = new List<(double Time, ReadOnlyMemory<byte> Data)>();

        foreach (var chunk in chunks)
        {
            var data = ptyFilter.Process(chunk.Data);
            if (data.IsEmpty)
                continue;

            timedChunks.Add((chunk.Time.TotalSeconds, data));
        }

        var tail = ptyFilter.Complete();
        if (!tail.IsEmpty)
        {
            var tailSeconds = chunks.Count > 0
                ? chunks[^1].Time.TotalSeconds
                : 0.0;
            timedChunks.Add((tailSeconds, tail));
        }

        if (verbose)
        {
            var inputCount = inputChunks?.Count ?? 0;
            Console.Error.WriteLine(
                $"scenetake: interactive pty cleanup: stripped {ptyFilter.StrippedByteCount} bytes, input={inputCount}, normalize-typing={normalizeTyping}");
        }

        if (timedChunks.Count == 0)
        {
            var lastSeconds = 0.0;
            if (chunks.Count > 0)
                lastSeconds = Math.Max(lastSeconds, chunks[^1].Time.TotalSeconds);
            if (inputChunks is { Count: > 0 })
                lastSeconds = Math.Max(lastSeconds, inputChunks[^1].Time.TotalSeconds);
            return new ComposedPtyOutput([], lastSeconds);
        }

        List<(double Time, ReadOnlyMemory<byte> Data)> emitChunks;

        if (normalizeTyping && inputChunks is { Count: > 0 })
        {
            var inputTimes = new double[inputChunks.Count];
            var inputBytes = new (double Time, ReadOnlyMemory<byte> Data)[inputChunks.Count];
            for (var i = 0; i < inputChunks.Count; i++)
            {
                inputTimes[i] = inputChunks[i].Time.TotalSeconds;
                inputBytes[i] = (inputChunks[i].Time.TotalSeconds, inputChunks[i].Data);
            }

            var windows = InteractivePtyCastEmit.BuildInputEchoWindows(inputTimes);
            var normalized = InteractivePtyCastEmit.NormalizeOutputChunks(
                timedChunks,
                windows,
                typingSpeed,
                width,
                height,
                inputBytes);
            InteractivePtyCastEmit.CollapseIdleAfterNormalize(
                normalized,
                castTiming.SilenceThreshold,
                castTiming.MaxIdle);
            emitChunks = new List<(double Time, ReadOnlyMemory<byte> Data)>(normalized.Count);
            foreach (var chunk in normalized)
                emitChunks.Add((chunk.Time, chunk.Data));
        }
        else
        {
            if (normalizeTyping && verbose)
                Console.Error.WriteLine("scenetake: normalize-typing: no input chunks, using wall-clock output");
            emitChunks = timedChunks;
        }

        var coalesced = CastTimingPipeline.CoalesceTimedUtf8(emitChunks);
        var output = new (double Time, ReadOnlyMemory<byte> Data)[coalesced.Count];
        for (var i = 0; i < coalesced.Count; i++)
            output[i] = (coalesced[i].Time, coalesced[i].Data);
        return new ComposedPtyOutput(output, coalesced[^1].Time);
    }

    /// <summary>Adds output that is already timed (<see cref="ComposeInteractivePtyOutput"/>) to the cast.</summary>
    internal static double EmitComposedPtyOutput(List<CastEvent> events, double commandStart, ComposedPtyOutput output)
    {
        foreach (var (time, data) in output.Chunks)
        {
            events.Add(CastEvent.OutputUtf8(
                Math.Round(commandStart + time, 6),
                data));
        }

        return commandStart + output.End;
    }

    internal static List<(double Time, string Text)> BuildStartupAdjustedPipeChunks(
        IReadOnlyList<PipeStreamChunk> chunks,
        double executionDuration,
        string stderrStyle)
    {
        var startupOffset = PtyCastTiming.ComputeStartupOffset(chunks[0].Time.TotalSeconds, executionDuration);
        var timedChunks = new List<(double Time, string Text)>(chunks.Count);
        for (var i = 0; i < chunks.Count; i++)
        {
            var chunk = WithoutSplitCr(chunks, i);
            var adjusted = PtyCastTiming.AdjustChunkSeconds(chunk.Time.TotalSeconds, startupOffset);
            timedChunks.Add((adjusted, FormatPipeChunkText(chunk, stderrStyle)));
        }

        return timedChunks;
    }

    // A CRLF split between two reads of a stream is one line break: each half alone would become one. The color of a
    // stderr read would hide the line feed from a check made later. So the CR leaves the first read.
    private static PipeStreamChunk WithoutSplitCr(IReadOnlyList<PipeStreamChunk> chunks, int index)
    {
        var chunk = chunks[index];
        if (!chunk.Text.EndsWith('\r'))
            return chunk;

        for (var i = index + 1; i < chunks.Count; i++)
        {
            if (chunks[i].IsStderr == chunk.IsStderr)
                return chunks[i].Text.StartsWith('\n') ? chunk with { Text = chunk.Text[..^1] } : chunk;
        }

        return chunk;
    }

    /// <summary>
    /// For each read of a streamed pipe step, where the cast writes a byte that ends a sequence the read's stream left
    /// open. The masker reads each stream as one, so it is told where. A place that ends nothing is left out: the
    /// masker would read the rest of a string as text and join it to the text after.
    /// </summary>
    internal static PipeReadCuts[] StreamedPipeCuts(IReadOnlyList<PipeStreamChunk> chunks, string stderrStyle)
    {
        var cuts = new PipeReadCuts[chunks.Count];
        // For stdout [0] and stderr [1]: the escape sequence that the stream has left open so far, which can span
        // reads, and whether the other stream's reads since the last one end it in the cast.
        var pending = new[] { "", "" };
        var open = new OpenSequence[2];
        var started = new bool[2];
        var ended = new bool[2];
        for (var i = 0; i < chunks.Count; i++)
        {
            var chunk = WithoutSplitCr(chunks, i);
            var own = chunk.IsStderr ? 1 : 0;
            var colored = IsColoredStderr(chunk, stderrStyle);
            if (colored)
                cuts[i] = PipeReadCuts.Before | PipeReadCuts.AfterText;
            else if (started[own] && ended[own])
                cuts[i] = PipeReadCuts.Before;

            var other = 1 - own;
            if (started[other] && !ended[other])
                ended[other] = colored || Ends(open[other], chunk.Text);
            started[own] = true;
            ended[own] = false;
            // The color around a colored read ends whatever it leaves open.
            var text = cuts[i] == PipeReadCuts.None ? pending[own] + chunk.Text : chunk.Text;
            open[own] = colored ? OpenSequence.None : OpenAtEnd(text);
            pending[own] = open[own] == OpenSequence.None ? "" : text[text.LastIndexOf('\u001b')..];
        }

        return cuts;
    }

    internal static string FormatPipeChunkText(PipeStreamChunk chunk, string stderrStyle) =>
        IsColoredStderr(chunk, stderrStyle) ? WrapWithStylePreserveTrailingNewlines(chunk.Text, stderrStyle) : chunk.Text;

    // What a stream leaves open for the bytes after it. A control string counts as None: the bytes that end one (ESC,
    // BEL, CAN, SUB) change nothing in the ground state, so a cut there costs nothing.
    private enum OpenSequence : byte
    {
        None,
        LoneEscape,
        Escape, // with intermediates
        Csi,
    }

    private static OpenSequence OpenAtEnd(string text)
    {
        var i = text.LastIndexOf('\u001b') + 1;
        if (i == 0)
            return OpenSequence.None;
        if (i == text.Length)
            return OpenSequence.LoneEscape;

        var csi = text[i] == '[';
        if (!csi && text[i] is not (>= ' ' and <= '/'))
            return OpenSequence.None;

        var last = csi ? '?' : '/';
        i++;
        while (i < text.Length && text[i] >= ' ' && text[i] <= last)
            i++;
        return i < text.Length ? OpenSequence.None : csi ? OpenSequence.Csi : OpenSequence.Escape;
    }

    // Whether text, written between two reads of a stream, ends what the first one left open, or takes the place of the
    // byte after a lone ESC. Either way the stream's next read no longer goes on as it would alone. ESC, CAN, and SUB
    // end anything. Other C0 controls run inside an escape without ending it. An escape with intermediates goes on
    // through more of them, and a CSI goes on through its parameter bytes.
    private static bool Ends(OpenSequence open, string text)
    {
        foreach (var c in text)
        {
            if (c is '\u001b' or '\u0018' or '\u001a')
                return true;
            var ends = open switch
            {
                OpenSequence.None => c == '\u0007',
                OpenSequence.LoneEscape => c >= ' ' && c != '\u007f',
                OpenSequence.Escape => c >= '0' && c != '\u007f',
                _ => c >= '@' && c != '\u007f',
            };
            if (ends)
                return true;
        }

        return false;
    }

    // A stderr read without colors of its own gets the step's stderr color.
    private static bool IsColoredStderr(PipeStreamChunk chunk, string stderrStyle) =>
        chunk.IsStderr && !string.IsNullOrEmpty(stderrStyle) && chunk.Text.Length > 0 && !ContainsAnsiSgr(chunk.Text);

    internal static void ApplyCastTimingToChunks(List<(double Time, string Text)> chunks, CastTimingSettings timing)
    {
        if (chunks.Count == 0)
            return;

        var count = chunks.Count;
        var useStack = count <= 256;
        double[]? rentedTimes = null;
        Span<double> times = useStack
            ? stackalloc double[count]
            : (rentedTimes = ArrayPool<double>.Shared.Rent(count)).AsSpan(0, count);
        try
        {
            for (var i = 0; i < count; i++)
                times[i] = chunks[i].Time;

            CastTimingPipeline.AdjustTimelineInPlace(times, timing);
            for (var i = 0; i < chunks.Count; i++)
                chunks[i] = (times[i], chunks[i].Text);
        }
        finally
        {
            if (rentedTimes is not null)
                ArrayPool<double>.Shared.Return(rentedTimes, clearArray: false);
        }
    }

    internal static void ApplyCastTimingToPtyChunks(
        List<(double Time, double OriginalTime, ReadOnlyMemory<byte> Data)> chunks,
        CastTimingSettings timing)
    {
        if (chunks.Count == 0)
            return;

        var count = chunks.Count;
        var useStack = count <= 256;
        double[]? rentedTimes = null;
        Span<double> times = useStack
            ? stackalloc double[count]
            : (rentedTimes = ArrayPool<double>.Shared.Rent(count)).AsSpan(0, count);
        try
        {
            for (var i = 0; i < count; i++)
                times[i] = chunks[i].Time;

            CastTimingPipeline.AdjustTimelineInPlace(times, timing);
            for (var i = 0; i < chunks.Count; i++)
                chunks[i] = (times[i], chunks[i].OriginalTime, chunks[i].Data);
        }
        finally
        {
            if (rentedTimes is not null)
                ArrayPool<double>.Shared.Return(rentedTimes, clearArray: false);
        }
    }

    internal static void ApplyBurstMergeToPtyChunks(List<(double Time, double OriginalTime, ReadOnlyMemory<byte> Data)> chunks)
    {
        if (chunks.Count <= 1)
            return;

        var count = chunks.Count;
        var useStack = count <= 256;
        double[]? rentedOriginal = null;
        double[]? rentedTimes = null;
        Span<double> originalSeconds = useStack
            ? stackalloc double[count]
            : (rentedOriginal = ArrayPool<double>.Shared.Rent(count)).AsSpan(0, count);
        Span<double> times = useStack
            ? stackalloc double[count]
            : (rentedTimes = ArrayPool<double>.Shared.Rent(count)).AsSpan(0, count);
        try
        {
            for (var i = 0; i < count; i++)
            {
                originalSeconds[i] = chunks[i].OriginalTime;
                times[i] = chunks[i].Time;
            }

            CastTimingPipeline.MergeBurstGaps(times, originalSeconds, DefaultBurstThreshold);
            for (var i = 0; i < chunks.Count; i++)
                chunks[i] = (times[i], chunks[i].OriginalTime, chunks[i].Data);
        }
        finally
        {
            if (rentedOriginal is not null)
                ArrayPool<double>.Shared.Return(rentedOriginal, clearArray: false);
            if (rentedTimes is not null)
                ArrayPool<double>.Shared.Return(rentedTimes, clearArray: false);
        }
    }

    internal static void EmitTimedTextCastEvents(
        List<CastEvent> events,
        double commandStart,
        IReadOnlyList<(double Time, string Text)> coalesced,
        List<HighlightSpec>? highlights,
        string cmd)
    {
        if (coalesced.Count == 0)
            return;

        if (highlights is null)
        {
            // Each read is normalized on its own, so a CRLF that two reads split is one line break here as in the
            // joined output: the CR that ends a read became CRLF already, and the LF that starts the next is dropped.
            var afterCr = false;
            foreach (var (time, text) in coalesced)
            {
                if (text.Length == 0)
                    continue;

                var part = afterCr && text[0] == '\n' ? text[1..] : text;
                afterCr = text[^1] == '\r';
                if (part.Length > 0)
                    events.Add(CastEvent.Output(Math.Round(commandStart + time, 6), NormalizeNewlines(part)));
            }

            return;
        }

        var mergedBuilder = new StringBuilder();
        var rawSegments = new List<string>(coalesced.Count);
        for (var i = 0; i < coalesced.Count; i++)
        {
            var text = coalesced[i].Text;
            mergedBuilder.Append(text);
            rawSegments.Add(text);
        }

        var merged = mergedBuilder.ToString();
        var highlighted = ApplyHighlights(merged, highlights, cmd);
        var split = HighlightSplitter.SplitByRawSegments(highlighted, rawSegments);
        for (var i = 0; i < coalesced.Count; i++)
        {
            if (split[i].Length > 0)
            {
                events.Add(CastEvent.Output(
                    Math.Round(commandStart + coalesced[i].Time, 6),
                    NormalizeNewlines(split[i])));
            }
        }
    }

    internal static void EmitTypedCommand(
        List<CastEvent> events,
        ref double t,
        string prompt,
        double pause,
        string cmd,
        double speed,
        double jitter,
        Random rng,
        bool hasRunHighlight,
        string runHighlightStyle)
    {
        // The prompt, then the pause before typing (pre-delay).
        events.Add(CastEvent.Output(Math.Round(t, 6), prompt));
        t += pause + 0.05;

        if (hasRunHighlight && !string.IsNullOrEmpty(runHighlightStyle))
            events.Add(CastEvent.Output(Math.Round(t, 6), runHighlightStyle));

        // One keystroke per user-perceived character (extended grapheme cluster). Typing UTF-16 code units
        // split an emoji's surrogate pair into two events, which the cast writer stores as two U+FFFD.
        for (var i = 0; i < cmd.Length;)
        {
            var length = StringInfo.GetNextTextElementLength(cmd.AsSpan(i));
            events.Add(CastEvent.Output(Math.Round(t, 6), length == 1 ? TypingChars.Get(cmd[i]) : cmd.Substring(i, length)));
            var delay = speed + rng.NextDouble() * 2 * jitter - jitter;
            t += Math.Max(delay, 0.005);
            i += length;
        }

        if (hasRunHighlight && !string.IsNullOrEmpty(runHighlightStyle))
            events.Add(CastEvent.Output(Math.Round(t, 6), SgrReset));

        events.Add(CastEvent.Output(Math.Round(t, 6), "\r\n"));
        t += 0.15;
    }

    /// <summary>
    /// The seed of a step's typing: the step as recording again finds it (<see cref="Rerecording.StepId"/>, from the
    /// run text after masking, how it runs, and what it shows) and how many steps before it are the same, so two steps
    /// that type one command still type it differently.
    /// </summary>
    internal static int TypingSeed(ulong stepId, int occurrence)
    {
        unchecked
        {
            // SplitMix64's finalizer: ids that differ in one bit give unrelated seeds.
            var mixed = stepId + (ulong)occurrence * 0x9E3779B97F4A7C15UL;
            mixed = (mixed ^ (mixed >> 30)) * 0xBF58476D1CE4E5B9UL;
            mixed = (mixed ^ (mixed >> 27)) * 0x94D049BB133111EBUL;
            return (int)(mixed ^ (mixed >> 31));
        }
    }

    /// <summary>
    /// The shell for the cast header's env.SHELL: its file name, without the folder and without ".exe". The
    /// full path told every viewer of a committed cast where the shell was installed, which on Windows is
    /// often under C:\Users\&lt;name&gt; (scoop, winget user installs). A player does not use the value.
    /// </summary>
    internal static string ShellEnvName(string fileName)
    {
        var name = Path.GetFileName(fileName);
        return name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? name[..^4] : name;
    }

    internal static void WriteCast(
        Scenario scenario,
        List<CastEvent> events,
        string outputPath,
        string shellName,
        long timestamp,
        ResolvedRenderSettings renderSettings,
        IReadOnlyList<StepSlot>? slots = null,
        IReadOnlyList<StepTime>? times = null)
    {
        using var writer = new StreamWriter(outputPath, append: false, encoding: new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        WriteCast(writer, scenario, events, shellName, timestamp, renderSettings, slots, times);
    }

    /// <summary>
    /// Writes the cast to <paramref name="writer"/>; <paramref name="shellName"/> is the header's env.SHELL (<see cref="ShellEnvName"/>).
    /// <paramref name="slots"/> become the st:steps tag, which the next recording to the same file reads (<see cref="Rerecording"/>).
    /// The ids of <paramref name="times"/> become the st:ids tag, which <c>--at</c> with an id reads (<see cref="StepIds"/>).
    /// </summary>
    internal static void WriteCast(
        TextWriter writer,
        Scenario scenario,
        List<CastEvent> events,
        string shellName,
        long timestamp,
        ResolvedRenderSettings renderSettings,
        IReadOnlyList<StepSlot>? slots = null,
        IReadOnlyList<StepTime>? times = null)
    {
        writer.NewLine = "\n";

        var width = scenario.Width ?? RenderSettingsResolver.DefaultTerminalCols;
        var height = scenario.Height ?? RenderSettingsResolver.DefaultTerminalRows;
        var title = scenario.Title ?? "";
        var fontSize = renderSettings.FontSize.ToString(CultureInfo.InvariantCulture);
        var fontFamilyTag = RenderSettingsResolver.FontFamilyTagPrefix + renderSettings.FontFamily;
        var tags = new List<string>
        {
            RenderSettingsResolver.FontSizeTagPrefix + fontSize,
            fontFamilyTag,
        };
        if (renderSettings.Window != WindowStyle.None)
            tags.Add(RenderSettingsResolver.WindowTagPrefix + RenderSettingsResolver.WindowToTagValue(renderSettings.Window));
        // Without window-title the reader takes the cast's title. An image background is left out: a cast names
        // no file of the computer it was recorded on, so the scenario path passes the image to the renderer.
        if (renderSettings.WindowTitle is { } windowTitle)
            tags.Add(RenderSettingsResolver.WindowTitleTagPrefix + windowTitle);
        if (renderSettings.Background is { Colors.Count: > 0 } background)
            tags.Add(RenderSettingsResolver.BackgroundTagPrefix + background.TagValue);
        if (renderSettings.Margin is int margin)
            tags.Add(RenderSettingsResolver.MarginTagPrefix + margin.ToString(CultureInfo.InvariantCulture));
        if (renderSettings.Padding is int padding)
            tags.Add(RenderSettingsResolver.PaddingTagPrefix + padding.ToString(CultureInfo.InvariantCulture));
        if (renderSettings.Opacity is double opacity)
            tags.Add(RenderSettingsResolver.OpacityTagPrefix + RenderSettingsResolver.FormatOpacity(opacity));
        if (renderSettings.Blur is int blur)
            tags.Add(RenderSettingsResolver.BlurTagPrefix + blur.ToString(CultureInfo.InvariantCulture));
        if (renderSettings.Loop is bool loop)
            tags.Add(RenderSettingsResolver.LoopTagPrefix + (loop ? "true" : "false"));
        if (renderSettings.EndHold is double endHold)
            tags.Add(RenderSettingsResolver.EndHoldTagPrefix + RenderSettingsResolver.FormatSeconds(endHold));
        if (renderSettings.FadeOut is double fadeOut)
            tags.Add(RenderSettingsResolver.FadeOutTagPrefix + RenderSettingsResolver.FormatSeconds(fadeOut));
        // The events keep the recorded times, which other players play. The tag says how fast scenetake renders them.
        if (renderSettings.Speed is double speed && speed != 1)
            tags.Add(RenderSettingsResolver.SpeedTagPrefix + RenderSettingsResolver.FormatSeconds(speed));
        // As with speed, the events keep the recorded times, and the tag says how long scenetake renders them at most.
        if (renderSettings.Length is double length)
            tags.Add(RenderSettingsResolver.LengthTagPrefix + RenderSettingsResolver.FormatSeconds(length));
        // A cast without st:fit is read with the default fit, so only another fit, none included, needs the tag.
        if (renderSettings.Fit != RenderSettingsResolver.DefaultFit)
            tags.Add(RenderSettingsResolver.FitTagPrefix + RenderSettingsResolver.FitToTagValue(renderSettings.Fit));
        if (renderSettings.Crop.Top > 0)
            tags.Add(RenderSettingsResolver.CropTopTagPrefix + renderSettings.Crop.Top.ToString(CultureInfo.InvariantCulture));
        if (renderSettings.Crop.Bottom > 0)
            tags.Add(RenderSettingsResolver.CropBottomTagPrefix + renderSettings.Crop.Bottom.ToString(CultureInfo.InvariantCulture));
        if (renderSettings.Crop.Left > 0)
            tags.Add(RenderSettingsResolver.CropLeftTagPrefix + renderSettings.Crop.Left.ToString(CultureInfo.InvariantCulture));
        if (renderSettings.Crop.Right > 0)
            tags.Add(RenderSettingsResolver.CropRightTagPrefix + renderSettings.Crop.Right.ToString(CultureInfo.InvariantCulture));
        if (StepIds.FormatTag(times) is { } idsTag)
            tags.Add(idsTag);
        if (slots is { Count: > 0 })
            tags.Add(Rerecording.FormatTag(slots, events.Count, WrittenMilliseconds(events)));
        var tagsJson = string.Join(",", tags.ConvertAll(static tag => JsonEscape.JsonString(tag)));
        writer.WriteLine(
            $"{{\"version\":3,\"term\":{{\"cols\":{width},\"rows\":{height}" +
            $",\"type\":\"xterm-256color\"" +
            $",\"theme\":{{\"fg\":{JsonEscape.JsonString(renderSettings.Theme.Fg)},\"bg\":{JsonEscape.JsonString(renderSettings.Theme.Bg)},\"palette\":{JsonEscape.JsonString(renderSettings.Theme.Palette)}}}}}" +
            $",\"timestamp\":{timestamp},\"title\":{JsonEscape.JsonString(title)}" +
            $",\"env\":{{\"SHELL\":{JsonEscape.JsonString(shellName)}}}" +
            $",\"tags\":[{tagsJson}]}}");

        var intervalError = 0.0;
        var previousAbs = 0.0;
        for (var i = 0; i < events.Count; i++)
        {
            var ev = events[i];
            var exact = i == 0 ? ev.Time : ev.Time - previousAbs;
            previousAbs = ev.Time;
            var code = ev.Kind switch
            {
                CastEventKind.Resize => 'r',
                CastEventKind.Marker => 'm',
                _ => 'o',
            };
            writer.Write('[');
            WriteCastInterval(writer, exact, ref intervalError);
            writer.Write(',');
            writer.Write('"');
            writer.Write(code);
            writer.Write("\",");
            if (ev.HasUtf8Output)
                JsonEscape.WriteJsonUtf8String(writer, ev.Utf8Output.Span);
            else
                JsonEscape.WriteJsonString(writer, ev.Data);
            writer.WriteLine(']');
        }

        writer.Write('[');
        WriteCastInterval(writer, events.Count > 0 ? 0.05 : 0, ref intervalError);
        writer.WriteLine(",\"x\",\"0\"]");
    }

    /// <summary>Where the last event is in the file: the sum of the intervals <see cref="WriteCastInterval"/> writes for the events.</summary>
    private static long WrittenMilliseconds(List<CastEvent> events)
    {
        var error = 0.0;
        var previous = 0.0;
        var total = 0L;
        foreach (var ev in events)
        {
            total += (long)RoundIntervalToMilliseconds(ev.Time - previous, ref error);
            previous = ev.Time;
        }

        return total;
    }

    // Carries the rounding error to the next interval, so the times do not drift from the exact ones. Event times are
    // whole microseconds, so a time can sit exactly on half a millisecond. Without the first rounding, floating-point
    // noise from the previous event's time would decide its side, and halves go up so that no interval is negative.
    private static double RoundIntervalToMilliseconds(double exact, ref double error)
    {
        var scaled = Math.Round(exact * 1000.0 + error, 6);
        var quantized = Math.Floor(scaled + 0.5);
        error = scaled - quantized;
        return quantized;
    }

    internal static void WriteCastInterval(TextWriter writer, double exact, ref double error)
    {
        const double scale = 1000.0;
        var quantized = RoundIntervalToMilliseconds(exact, ref error);
        var seconds = quantized / scale;
        if (seconds == 0)
        {
            writer.Write("0.000");
            return;
        }

        Span<char> buf = stackalloc char[16];
        seconds.TryFormat(buf, out var n, "0.000", CultureInfo.InvariantCulture);
        writer.Write(buf[..n]);
    }

    /// <summary>
    /// The last <paramref name="maxLines"/> lines a step printed, as plain text: escape sequences and other
    /// control characters removed, and a line redrawn with <c>\r</c> reduced to what was drawn last. PTY
    /// output is the terminal byte stream, so this is only an approximation of its screen.
    /// </summary>
    internal static IReadOnlyList<string> FailureOutputTail(CommandExecution execution, int maxLines = 20)
    {
        string text;
        if (execution.IsPty && execution.PtyByteChunks is { } chunks)
        {
            var bytes = new ArrayBufferWriter<byte>();
            foreach (var chunk in chunks)
                bytes.Write(chunk.Data.Span);
            text = Encoding.UTF8.GetString(bytes.WrittenSpan);
        }
        else
        {
            text = execution.Output.Length == 0 || execution.Output.EndsWith('\n')
                ? execution.Output + execution.Stderr
                : execution.Output + "\n" + execution.Stderr;
        }

        var lines = new List<string>();
        foreach (var rawLine in StripTerminalControls(text).Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            lines.Add(line[(line.LastIndexOf('\r') + 1)..].TrimEnd());
        }

        while (lines.Count > 0 && lines[^1].Length == 0)
            lines.RemoveAt(lines.Count - 1);
        var start = Math.Max(0, lines.Count - maxLines);
        while (start < lines.Count && lines[start].Length == 0)
            start++;
        return lines.GetRange(start, lines.Count - start);
    }

    /// <summary>Text without escape sequences (CSI, OSC and other strings, two-character escapes) and without control characters other than tab and line breaks.</summary>
    internal static string StripTerminalControls(string text)
    {
        var builder = new StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '\u001b' && i + 1 < text.Length)
            {
                var next = text[i + 1];
                i += 2;
                if (next == '[')
                {
                    // Parameters and intermediates, then the final byte, which the loop's i++ steps over.
                    while (i < text.Length && text[i] is < '@' or > '~')
                        i++;
                }
                else if (next is ']' or 'P' or 'X' or '^' or '_')
                {
                    // A string ended by BEL or ESC \.
                    while (i < text.Length && text[i] != '\a' && !(text[i] == '\u001b' && i + 1 < text.Length && text[i + 1] == '\\'))
                        i++;
                    if (i < text.Length && text[i] == '\u001b')
                        i++;
                }
                else if (next is >= ' ' and <= '/')
                {
                    // Intermediates, then the final byte: ESC ( B.
                    while (i < text.Length && text[i] is >= ' ' and <= '/')
                        i++;
                }
                else
                {
                    // A two-character escape such as ESC 7: the loop's i++ moves past the next character.
                    i -= 1;
                }

                continue;
            }

            if (c is '\n' or '\r' or '\t' || (c >= ' ' && c != '\u007f'))
                builder.Append(c);
        }

        return builder.ToString();
    }
}

internal static class PtyCastTiming
{
    internal static double ComputeStartupOffset(double firstEmittedChunkSeconds, double executionDuration) =>
        Math.Max(0.0, firstEmittedChunkSeconds - Math.Max(0.0, executionDuration));

    internal static double AdjustChunkSeconds(double chunkSeconds, double startupOffset) =>
        chunkSeconds - startupOffset;
}

static class TypingChars
{
    private static readonly string[] Cache = CreateCache();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string Get(char c) => (uint)c < 128 ? Cache[c] : c.ToString();

    private static string[] CreateCache()
    {
        var cache = new string[128];
        for (var i = 0; i < cache.Length; i++)
            cache[i] = ((char)i).ToString();
        return cache;
    }
}

/// <summary>What <see cref="ScenarioCast.GenerateAsync"/> does besides composing the cast.</summary>
internal sealed class GenerateOptions
{
    internal bool Verbose { get; init; }

    /// <summary>Whether the first step that exits non-zero without <c>continue-on-error</c> stops the recording.</summary>
    internal bool Strict { get; init; }

    /// <summary>
    /// Whether a non-interactive PTY step gets the recording's cursor (<see cref="StepStart.PtyStartCursor"/>):
    /// on the CLI when ConPTY runs it, in the playground when the outputs file comes from Windows.
    /// </summary>
    internal bool TrackPtyCursor { get; init; }

    /// <summary>Whether each step writes a <c>running:</c> line to stderr.</summary>
    internal bool LogRunning { get; init; } = true;

    /// <summary>
    /// The scenario's <c>render.fit</c> and crop, with the command line's on top: a card is centered in the part of the
    /// terminal they show (spec_scenario.md → Card steps).
    /// </summary>
    internal FitMode Fit { get; init; } = FitMode.Height;

    /// <inheritdoc cref="Fit"/>
    internal CropSpec Crop { get; init; }

    /// <summary>
    /// The name of the folder the next command starts in, as the prompt's <c>{dir}</c> shows it
    /// (<see cref="PromptTemplate.FolderName"/>). Called before each shown step and once at the end. Null shows
    /// <c>~</c>. A replay takes the folders from the outputs file instead.
    /// </summary>
    internal Func<string>? PromptFolder { get; init; }

    /// <summary>The folder the closing prompt showed, when the prompt shows one: the outputs file keeps it.</summary>
    internal string? EndFolder { get; set; }

    /// <summary>
    /// The prompt the shell drew for the next command (settings.shell-prompt), as the shell wrote it, or null while the
    /// shell draws none. Called before each shown step and once at the end. A replay takes the prompts from
    /// the outputs file instead.
    /// </summary>
    internal Func<string?>? ShellPrompt { get; init; }

    /// <summary>The prompt the shell drew for the closing prompt, masked, when it drew one: the outputs file keeps it.</summary>
    internal string? EndShellPrompt { get; set; }

    /// <summary>Receives each step's output for the outputs file. Null records nothing.</summary>
    internal List<RecordedStep>? Record { get; init; }

    /// <summary>
    /// Hides secrets in each step's typed command, comment line, and output, before they reach the cast or the
    /// outputs file (<see cref="SecretMasker"/>). Null hides nothing.
    /// </summary>
    internal SecretMasker? Masker { get; init; }

    /// <summary>Receives the keys typed in each interactive step, for --save-keys. Null keeps none.</summary>
    internal List<SavedStepKeys>? SavedKeys { get; init; }

    /// <summary>Receives each shown step's time in the cast, for the table after the cast's Written line. Null keeps none.</summary>
    internal List<StepTime>? Times { get; init; }

    /// <summary>Compares each step with the earlier cast, and keeps where its output is for the st:steps tag. Null does neither.</summary>
    internal Rerecorder? Rerecorder { get; init; }

    /// <summary>--rerun: run every step, also one with <c>rerun: false</c> whose output the earlier cast has.</summary>
    internal bool RerunAll { get; init; }

    /// <summary>
    /// A replay of an outputs file, which gives each typed command as the file holds it, masked by the recording. Null
    /// types the YAML's run text.
    /// </summary>
    internal StepOutputsReplay? Replay { get; init; }
}

/// <summary>
/// The keys a person typed in one interactive step, for --save-keys (RecordedKeys turns them into YAML).
/// <paramref name="Number"/> is the step's 1-based position. <paramref name="HiddenInput"/> holds when each key that
/// was not recorded, because the program read a password, was typed.
/// </summary>
internal sealed record SavedStepKeys(int Number, string Run, double TypingSpeed, IReadOnlyList<TimedBytes> Input, IReadOnlyList<TimeSpan> HiddenInput);

/// <summary>Runs one step, or finds its recorded output, and returns what the command printed.</summary>
internal delegate Task<CommandExecution> StepRunner(StepStart step);

/// <summary>
/// A step that is about to run. <paramref name="Index"/> is its 0-based position in <c>steps</c>;
/// <paramref name="PtyStartCursor"/> is the 1-based cursor for ConPTY when <see cref="GenerateOptions.TrackPtyCursor"/> is set.
/// </summary>
internal readonly record struct StepStart(
    int Index,
    CommandEntry Command,
    bool UsePty,
    bool UseInteractive,
    int Width,
    int Height,
    (int Row, int Column)? PtyStartCursor)
{
    /// <summary>What the step's <c>keys</c> write to its PTY, and when. Empty when it sets none.</summary>
    internal IReadOnlyList<ScenarioCast.KeyAction> Keys { get; init; } = [];

    /// <summary>Seconds after which the step's command is stopped (<c>timeout</c>), or null.</summary>
    internal double? Timeout { get; init; }

    /// <summary>What the step shows (<c>show</c>). A step with <see cref="StepShow.None"/> is run and not recorded: the playground skips it.</summary>
    internal StepShow Show { get; init; }

    /// <summary>A <c>background: true</c> step: its command keeps running after the runner returns, until it is stopped before post.</summary>
    internal bool Background { get; init; }

    /// <summary>For a background step, the text its output shows when it is ready (<c>wait</c>), or null to go on at once.</summary>
    internal string? WaitFor { get; init; }

    /// <summary>For a background step, the seconds it waits for <see cref="WaitFor"/> (<c>wait-timeout</c>).</summary>
    internal double WaitLimit { get; init; }
}

/// <summary>
/// Where the cast of a streamed pipe step writes a byte that ends a sequence the stream left open, around one read.
/// </summary>
[Flags]
internal enum PipeReadCuts : byte
{
    None = 0,

    /// <summary>Before the read: its color, or reads of the other stream that end the sequence.</summary>
    Before = 1,

    /// <summary>After the read's text, before its last line breaks: the end of its color.</summary>
    AfterText = 2,
}

/// <summary>Output that is already timed: each chunk's time since the command started, and where the step ends.</summary>
internal sealed record ComposedPtyOutput(IReadOnlyList<(double Time, ReadOnlyMemory<byte> Data)> Chunks, double End);

internal readonly record struct CommandExecution(
    int ExitCode,
    bool IsPty,
    string Output,
    string Stderr,
    IReadOnlyList<TimedBytes>? PtyByteChunks,
    IReadOnlyList<TimedBytes>? PtyInputChunks,
    IReadOnlyList<PipeStreamChunk>? PipeStreamChunks)
{
    /// <summary>Playground: <see cref="PtyByteChunks"/> already went through <see cref="ScenarioCast.RemovePtyLeadingInit"/>.</summary>
    internal bool PtyLeadingInitRemoved { get; init; }

    /// <summary>Playground: an interactive step's output as the outputs file stores it, added to the cast as it is.</summary>
    internal ComposedPtyOutput? ComposedOutput { get; init; }

    /// <summary>Playground: the line that stands for output the outputs file does not have.</summary>
    internal string? Placeholder { get; init; }

    /// <summary>A replay: the step's run text as the recording masked it, when masking changed it.</summary>
    internal string? MaskedRun { get; init; }

    /// <summary>
    /// A background step that did not get ready: what went wrong, such as <c>did not print "Listening" within 30 s</c>.
    /// Null when it started, and printed its <c>wait</c> text when it has one.
    /// </summary>
    internal string? BackgroundProblem { get; init; }

    /// <summary>
    /// Interactive steps: when each key that <see cref="PtyInputChunks"/> leaves out was typed, because the program
    /// read a password. The keys themselves are never kept.
    /// </summary>
    internal IReadOnlyList<TimeSpan>? HiddenInputTimes { get; init; }

    public static CommandExecution FromPipeStream(
        string stdout,
        string stderr,
        int exitCode,
        IReadOnlyList<PipeStreamChunk> chunks) =>
        new(exitCode, false, stdout, stderr, null, null, chunks);

    public static CommandExecution FromPipe(string stdout, string stderr, int exitCode) =>
        new(exitCode, false, stdout, stderr, null, null, []);
}
/// <summary>
/// The step that stopped a --strict run: it exited non-zero without <c>continue-on-error</c>. <paramref name="Number"/>
/// is 1-based, as in other step messages; <paramref name="OutputTail"/> holds its last output lines as plain text.
/// </summary>
record FailedStep(int Number, string Cmd, int ExitCode, IReadOnlyList<string> OutputTail)
{
    /// <summary>A <c>show: none</c> step, which stops the recording when it fails, with or without --strict.</summary>
    internal bool Hidden { get; init; }

    /// <summary>A <c>background: true</c> step that did not get ready, which stops the recording too. <see cref="Problem"/> says why.</summary>
    internal bool Background { get; init; }

    /// <summary>For <see cref="Background"/>: what went wrong, such as <c>exited 1 before it printed "Listening"</c>.</summary>
    internal string? Problem { get; init; }
}
