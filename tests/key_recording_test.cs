#:sdk Microsoft.NET.Sdk
#:property TargetFramework=net10.0
#:property Nullable=enable
#:property ImplicitUsings=enable
#:package VYaml@1.6.1
#:include ../Terminal.cs
#:include ../CastTimingPipeline.cs
#:include ../CastReader.cs
#:include ../Svg.cs
#:include ../JsonEscape.cs
#:include ../PtyLeadingInitFilter.cs
#:include ../ScenarioKeys.cs
#:include ../Scenario.cs
#:include ../Highlight.cs
#:include ../CastComposer.cs
#:include ../Rerecord.cs
#:include ../SecretMask.cs
#:include ../StepChunks.cs
#:include ../StepOutputs.cs
#:include ../KeyRecording.cs
#:include ../OutputWait.cs

// --save-keys turns the keys typed in an interactive step into `keys` entries, and `wait` entries match a PTY's
// output as text (.github/docs/spec_pty.md → Saving the keys of an interactive step, Keys and timeout).

using System.Text;
using static ScenarioCast;
using Kind = RecordedKeys.PressKind;

var failures = 0;
failures += Run("DecodeReadsXtermKeys", DecodeReadsXtermKeys);
failures += Run("DecodeLeavesOutTerminalAnswers", DecodeLeavesOutTerminalAnswers);
failures += Run("DecodeJoinsWhatAReadCutOff", DecodeJoinsWhatAReadCutOff);
failures += Run("DecodeReadsWin32InputMode", DecodeReadsWin32InputMode);
failures += Run("DecodePlacesHiddenPasswordKeys", DecodePlacesHiddenPasswordKeys);
failures += Run("EntriesGroupTypingAndKeepPauses", EntriesGroupTypingAndKeepPauses);
failures += Run("EntriesNoteWhatIsNotSaved", EntriesNoteWhatIsNotSaved);
failures += Run("SavedKeysReplayTheTypedBytes", SavedKeysReplayTheTypedBytes);
failures += Run("SavedKeysFileSaysWhatItLacks", SavedKeysFileSaysWhatItLacks);
failures += Run("WatcherFindsTextWithoutEscapes", WatcherFindsTextWithoutEscapes);
failures += Run("WatcherTakesEachMatchOnce", WatcherTakesEachMatchOnce);
failures += Run("WatcherWaitsForLaterOutput", WatcherWaitsForLaterOutput);

if (failures > 0)
{
    Console.Error.WriteLine($"Failed: {failures} key recording test(s)");
    return 1;
}

Console.Error.WriteLine("all key recording tests passed");
return 0;

static int Run(string name, Func<bool> test)
{
    try
    {
        if (test())
        {
            Console.Error.WriteLine($"ok {name}");
            return 0;
        }
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"  {ex}");
    }

    Console.Error.WriteLine($"FAIL {name}");
    return 1;
}

static bool Expect(bool condition, string message)
{
    if (!condition)
        Console.Error.WriteLine($"  {message}");
    return condition;
}

static TimedBytes At(double seconds, string text) => new(TimeSpan.FromSeconds(seconds), Encoding.UTF8.GetBytes(text));

static TimedBytes Bytes(double seconds, params byte[] data) => new(TimeSpan.FromSeconds(seconds), data);

static string ShowPresses(IEnumerable<RecordedKeys.Press> presses) =>
    string.Join(", ", presses.Select(static p => $"{p.Time}:{p.Kind}:{Show(p.Value)}"));

static bool ExpectPresses(IReadOnlyList<RecordedKeys.Press> actual, params (Kind Kind, string Value)[] expected)
{
    var ok = Expect(actual.Count == expected.Length, $"expected {expected.Length} presses, got {actual.Count}: {ShowPresses(actual)}");
    for (var i = 0; ok && i < expected.Length; i++)
    {
        ok &= Expect(actual[i].Kind == expected[i].Kind && actual[i].Value == expected[i].Value,
            $"press {i + 1}: got {actual[i].Kind}:{Show(actual[i].Value)}, expected {expected[i].Kind}:{Show(expected[i].Value)}");
    }

    return ok;
}

static bool DecodeReadsXtermKeys()
{
    var presses = RecordedKeys.Decode(
    [
        At(1.0, "l"), At(1.1, "s"), At(1.2, "\r"),
        At(2.0, "\u001b[A"), At(2.1, "\u001bOB"), At(2.2, "\u001b[5~"), At(2.3, "\u001b[3~"),
        At(2.4, "\u001bOP"), At(2.5, "\u001b[15~"), At(2.6, "\u001b[1~"), At(2.7, "\u001b[F"),
        At(3.0, "\u0003"), At(3.1, "\u007f"), At(3.2, "\t"), At(3.3, "\u001b"), At(3.4, "\u001bx"), At(3.5, "\u0008"),
        // A paste arrives in one read: each grapheme is a character, a line break is Enter.
        At(4.0, "あ👩‍💻\r"),
    ]);
    return ExpectPresses(presses,
        (Kind.Text, "l"), (Kind.Text, "s"), (Kind.Key, "enter"),
        (Kind.Key, "up"), (Kind.Key, "down"), (Kind.Key, "page-up"), (Kind.Key, "delete"),
        (Kind.Key, "f1"), (Kind.Key, "f5"), (Kind.Key, "home"), (Kind.Key, "end"),
        (Kind.Key, "ctrl+c"), (Kind.Key, "backspace"), (Kind.Key, "tab"), (Kind.Key, "escape"), (Kind.Key, "alt+x"), (Kind.Key, "ctrl+h"),
        (Kind.Text, "あ"), (Kind.Text, "👩‍💻"), (Kind.Key, "enter"))
        && Expect(presses[0].Time == 1.0 && presses[^1].Time == 4.0, $"times: {ShowPresses(presses)}");
}

static bool DecodeLeavesOutTerminalAnswers()
{
    var presses = RecordedKeys.Decode(
    [
        // Answers the host terminal sends: cursor position, device attributes, a mode, a color, focus.
        At(1.0, "\u001b[12;40R"), At(1.1, "\u001b[?62;22c"), At(1.2, "\u001b[?2004;1$y"),
        At(1.3, "\u001b]11;rgb:0000/0000/0000\u001b\\"), At(1.4, "\u001b]10;rgb:ffff/ffff/ffff\u0007"), At(1.5, "\u001b[I"),
        // Bracketed paste markers around pasted text.
        At(2.0, "\u001b[200~ab\u001b[201~"),
        // Mouse reports and keys with modifiers have no key name.
        At(3.0, "\u001b[<0;10;5M"), At(3.1, "\u001b[M !!"), At(3.2, "\u001b[1;5A"), At(3.3, "\u001b[Z"), At(3.4, "\u001b[3;2~"),
        // Alt+] without the end of a string sequence is a key.
        At(4.0, "\u001b]"),
    ]);
    return ExpectPresses(presses,
        (Kind.Text, "a"), (Kind.Text, "b"),
        (Kind.Skipped, "mouse input"), (Kind.Skipped, "mouse input"),
        (Kind.Skipped, "ESC [1;5A, which has no key name"), (Kind.Skipped, "shift+tab, which has no key name"),
        (Kind.Skipped, "ESC [3;2~, which has no key name"),
        (Kind.Key, "alt+]"));
}

static bool DecodeJoinsWhatAReadCutOff()
{
    var hiragana = Encoding.UTF8.GetBytes("あ");
    var presses = RecordedKeys.Decode(
    [
        // A sequence cut between two reads is one key, at the time of its first part.
        At(1.0, "\u001b["), At(1.01, "A"),
        // So is a character cut between its UTF-8 bytes, at the time its last byte arrived.
        Bytes(2.0, hiragana[0], hiragana[1]), Bytes(2.01, hiragana[2]),
        // A lone ESC at the end of a read is Escape, and the next read starts afresh.
        At(3.0, "\u001b"), At(3.5, "[x"),
    ]);
    return ExpectPresses(presses, (Kind.Key, "up"), (Kind.Text, "あ"), (Kind.Key, "escape"), (Kind.Text, "["), (Kind.Text, "x"))
        && Expect(presses[0].Time == 1.0 && Math.Abs(presses[1].Time - 2.01) < 1e-6, $"times: {ShowPresses(presses)}");
}

static bool DecodeReadsWin32InputMode()
{
    // ESC [ Vk ; Sc ; Uc ; Kd ; Cs ; Rc _ for each key press and release, as ConPTY asks the host terminal to send.
    var presses = RecordedKeys.Decode(
    [
        At(1.0, "\u001b[16;42;0;1;16;1_\u001b[65;30;65;1;16;1_\u001b[65;30;65;0;16;1_\u001b[16;42;0;0;0;1_"),
        At(1.1, "\u001b[38;72;0;1;0;1_\u001b[38;72;0;0;0;1_"),
        At(1.2, "\u001b[38;72;0;1;8;1_"),
        At(1.3, "\u001b[13;28;13;1;0;1_"),
        At(1.4, "\u001b[8;14;8;1;0;1_"),
        At(1.5, "\u001b[27;1;27;1;0;1_"),
        At(1.6, "\u001b[67;46;3;1;8;1_"),
        At(1.7, "\u001b[88;45;120;1;2;1_"),
        At(1.8, "\u001b[116;63;0;1;0;1_"),
        At(1.9, "\u001b[9;15;9;1;16;1_"),
        // AltGr (Ctrl and Alt) types the character itself.
        At(2.0, "\u001b[81;16;64;1;10;1_"),
        // A held key repeats.
        At(2.1, "\u001b[66;48;98;1;0;3_"),
        // A record without a virtual key carries one character of a sequence: here a cursor position report.
        At(2.2, "\u001b[0;0;27;1;0;1_\u001b[0;0;91;1;0;1_\u001b[0;0;53;1;0;1_\u001b[0;0;59;1;0;1_\u001b[0;0;49;1;0;1_\u001b[0;0;82;1;0;1_"),
        // Fields at the end can be left out.
        At(2.3, "\u001b[67;46;99;1_"),
    ]);
    return ExpectPresses(presses,
        (Kind.Text, "A"), (Kind.Key, "up"), (Kind.Skipped, "ESC [1;5A, which has no key name"), (Kind.Key, "enter"),
        (Kind.Key, "backspace"), (Kind.Key, "escape"), (Kind.Key, "ctrl+c"), (Kind.Key, "alt+x"), (Kind.Key, "f5"),
        (Kind.Skipped, "shift+tab, which has no key name"), (Kind.Text, "@"),
        (Kind.Text, "b"), (Kind.Text, "b"), (Kind.Text, "b"), (Kind.Text, "c"));
}

static bool DecodePlacesHiddenPasswordKeys()
{
    var presses = RecordedKeys.Decode(
        [At(1.0, "s"), At(1.1, "u"), At(5.0, "l")],
        [TimeSpan.FromSeconds(3.0), TimeSpan.FromSeconds(3.2), TimeSpan.FromSeconds(3.4)]);
    return ExpectPresses(presses,
        (Kind.Text, "s"), (Kind.Text, "u"), (Kind.Hidden, ""), (Kind.Hidden, ""), (Kind.Hidden, ""), (Kind.Text, "l"))
        && Expect(presses[2].Time == 3.0 && presses[4].Time == 3.4, $"times: {ShowPresses(presses)}");
}

static RecordedKeys.Press P(double time, Kind kind, string value = "") => new(time, kind, value);

static string ShowEntries(IEnumerable<RecordedKeys.Entry> entries) =>
    string.Join(" | ", entries.Select(static e =>
        $"{(e.Comments.Count > 0 ? "#" + string.Join("#", e.Comments) + " " : "")}sleep={e.Sleep?.ToString() ?? "-"} type={Show(e.Type)} key={e.Key ?? "-"}"));

static bool ExpectEntries(IReadOnlyList<RecordedKeys.Entry> actual, params (double? Sleep, string? Type, string? Key)[] expected)
{
    var ok = Expect(actual.Count == expected.Length, $"expected {expected.Length} entries, got {actual.Count}: {ShowEntries(actual)}");
    for (var i = 0; ok && i < expected.Length; i++)
    {
        ok &= Expect(Nullable.Equals(actual[i].Sleep, expected[i].Sleep) && actual[i].Type == expected[i].Type && actual[i].Key == expected[i].Key,
            $"entry {i + 1}: got {ShowEntries([actual[i]])}, expected sleep={expected[i].Sleep?.ToString() ?? "-"} type={Show(expected[i].Type)} key={expected[i].Key ?? "-"}");
    }

    return ok;
}

static bool EntriesGroupTypingAndKeepPauses()
{
    // vim: i, a pause, two characters, Escape at once, a pause, :wq and Enter.
    var entries = RecordedKeys.ToEntries(
    [
        P(1.23, Kind.Text, "i"),
        P(2.0, Kind.Text, "H"), P(2.2, Kind.Text, "i"),
        P(2.25, Kind.Key, "escape"),
        P(3.15, Kind.Text, ":"), P(3.3, Kind.Text, "w"), P(3.45, Kind.Text, "q"), P(3.6, Kind.Key, "enter"),
        // Enter ends the typed line: what comes next is an entry of its own, however soon.
        P(3.7, Kind.Text, "y"),
        // A pause of half a second or more inside typing starts an entry.
        P(4.35, Kind.Text, "z"),
        // Enter after a pause, or after a key, is a key of its own.
        P(5.0, Kind.Key, "enter"),
        P(5.1, Kind.Key, "down"), P(5.13, Kind.Key, "enter"),
    ], typingSpeed: 0.05);
    // The sleep before typed text keeps the person's pause. After typed text the replay already waits one
    // typing delay, so the sleep is that much shorter: 2.0 - 1.23 - 0.05 = 0.72.
    return ExpectEntries(entries,
        (1.2, "i", null),
        (0.7, "Hi", null),
        (null, null, "escape"),
        (0.9, ":wq\n", null),
        (0.1, "y", null),
        (0.6, "z", null),
        (0.6, null, "enter"),
        (0.1, null, "down"),
        (null, null, "enter"));
}

static bool EntriesNoteWhatIsNotSaved()
{
    var entries = RecordedKeys.ToEntries(
    [
        P(1.0, Kind.Text, "a"),
        P(1.2, Kind.Skipped, "mouse input"), P(1.3, Kind.Skipped, "mouse input"),
        P(1.55, Kind.Text, "b"),
        P(2.0, Kind.Hidden), P(2.1, Kind.Hidden), P(2.2, Kind.Hidden),
        P(3.0, Kind.Key, "enter"),
        P(4.0, Kind.Skipped, "ESC [1;5A, which has no key name"),
    ], typingSpeed: 0.05);
    var ok = ExpectEntries(entries,
        (1.0, "a", null),
        (0.5, "b", null),
        // The pause after a password counts from its last key.
        (0.8, null, "enter"),
        (null, null, null));
    ok &= Expect(entries.Count == 4 && entries[1].Comments.SequenceEqual(["not saved: mouse input"]),
        $"many mouse reports are one note before the next entry: {ShowEntries(entries)}");
    ok &= Expect(entries.Count == 4 && entries[2].Comments.Count == 1 && entries[2].Comments[0].Contains("password", StringComparison.Ordinal),
        $"the password keys are one note: {ShowEntries(entries)}");
    ok &= Expect(entries.Count == 4 && entries[3].Comments.SequenceEqual(["not saved: ESC [1;5A, which has no key name"]),
        $"a note after the last key ends the list: {ShowEntries(entries)}");
    return ok;
}

// The YAML the file holds reads back as the same keys: each step parses without an error or an unknown key, and
// its keys write the bytes the person typed (Enter as CR, the arrows as xterm sends them).
static bool SavedKeysReplayTheTypedBytes()
{
    var run = "printf 'say \"hi\"\\n' \\\n  && vim 'a b.txt'";
    var yaml = RecordedKeys.FormatFile("demo.yaml",
    [
        new SavedStepKeys(3, run, 0.05,
        [
            At(1.2, "i"), At(2.0, "H"), At(2.1, "\""), At(2.2, "\\"), At(2.3, "\u001b"),
            At(3.0, "\u001bOA"), At(3.5, ":"), At(3.6, "w"), At(3.7, "q"), At(3.8, "\r"),
        ], []),
        new SavedStepKeys(5, "top", 0.1, [At(2.0, "q")], []),
    ], everyKeySaved: false);

    var scenario = ParseScenario(yaml);
    var ok = Expect(FindStepKeyError(scenario) is null, $"error: {FindStepKeyError(scenario)}\n{yaml}");
    ok &= Expect(FindUnknownScenarioKeys(yaml).Count == 0, $"unknown keys: {string.Join("; ", FindUnknownScenarioKeys(yaml))}\n{yaml}");
    if (!ok)
        return false;

    var vim = ParseCommand(scenario.Steps![0]);
    ok &= Expect(vim.Cmd == run, $"run read back as {Show(vim.Cmd)}");
    var written = string.Concat(BuildKeyActions(vim.Extra, 0.05, 0, new Random(1)).Select(static action => action.Text));
    ok &= Expect(written == "iH\"\\\u001b\u001b[A:wq\r", $"keys write {Show(written)}\n{yaml}");

    var top = ParseCommand(scenario.Steps![1]);
    var topKeys = BuildKeyActions(top.Extra, 0.1, 0, new Random(1));
    ok &= Expect(topKeys.Count == 1 && topKeys[0].Text == "q" && Math.Abs(topKeys[0].Delay - 2.0) < 1e-9,
        $"top keys: {string.Join(", ", topKeys.Select(static k => $"{k.Delay}:{Show(k.Text)}"))}");
    ok &= Expect(yaml.Contains("  # step 3\n  - run: ", StringComparison.Ordinal) && yaml.Contains("  # step 5\n", StringComparison.Ordinal),
        $"each step says which it is:\n{yaml}");
    ok &= Expect(!yaml.Contains("Windows", StringComparison.Ordinal), $"the password warning is for Windows only:\n{yaml}");
    return ok;
}

static bool SavedKeysFileSaysWhatItLacks()
{
    var yaml = RecordedKeys.FormatFile("demo.yaml",
    [
        new SavedStepKeys(1, "sudo true", 0.05, [At(1.0, "\u001b[<0;3;4M"), At(4.0, "\u001b[1;5C")], [TimeSpan.FromSeconds(2.0), TimeSpan.FromSeconds(2.5)]),
        new SavedStepKeys(2, "sleep 1", 0.05, [], []),
    ], everyKeySaved: true);

    var ok = Expect(FindStepKeyError(ParseScenario(yaml)) is null, $"error: {FindStepKeyError(ParseScenario(yaml))}\n{yaml}");
    ok &= Expect(yaml.Contains("      # not saved: mouse input\n", StringComparison.Ordinal), $"no mouse note:\n{yaml}");
    ok &= Expect(yaml.Contains("      # not saved: keys typed while the program read a password.", StringComparison.Ordinal), $"no password note:\n{yaml}");
    ok &= Expect(yaml.Contains("      # not saved: ESC [1;5C, which has no key name\n", StringComparison.Ordinal), $"no note for ctrl+right:\n{yaml}");
    ok &= Expect(yaml.Contains("    # no keys were typed in this step\n", StringComparison.Ordinal), $"a step without keys says so:\n{yaml}");
    ok &= Expect(yaml.Contains("a password too", StringComparison.Ordinal), $"Windows saves passwords and the file must say so:\n{yaml}");
    // Only comments under keys: an empty list, which a replay accepts.
    var steps = ParseScenario(yaml).Steps!;
    ok &= Expect(BuildKeyActions(ParseCommand(steps[0]).Extra, 0.05, 0, new Random(1)).Count == 0, "a step with only notes has keys to write");
    return ok;
}

static bool WatcherFindsTextWithoutEscapes()
{
    var cases = new (string Output, string Wait)[]
    {
        ("\u001b[1;32mName?\u001b[0m ", "Name? "),             // colors, and a space at the end that does not count
        ("hello\u001b[3Cworld", "hello world"),               // ConPTY moves the cursor where spaces would be
        ("hello\u001b[2;1Hworld", "hello world"),             // a cursor move parts words as a line break does
        ("wrapped\r\nline", "wrapped line"),
        ("a   b\tc", "a b c"),                                // runs of spaces are one
        ("\u001b]0;secret title\u0007ready", "ready"),        // OSC ends with BEL
        ("\u001bPq#0;2;0;0;0\u001b\\done", "done"),           // DCS ends with ST
        ("\u001b(Bplain\u001b=", "plain"),
        ("[y/N]", "[y/N]"),
    };

    var ok = true;
    foreach (var (output, wait) in cases)
    {
        var watcher = new PtyOutputWatcher();
        watcher.Append(Encoding.UTF8.GetBytes(output));
        ok &= Expect(watcher.WaitAsync(wait).IsCompleted, $"{Show(wait)} not found in {Show(output)}, read as {Show(watcher.Text)}");
    }

    var titled = new PtyOutputWatcher();
    titled.Append(Encoding.UTF8.GetBytes("\u001b]0;secret title\u0007\u001b[31mred"));
    ok &= Expect(titled.Text == "red", $"escape sequences left text: {Show(titled.Text)}");
    ok &= Expect(PtyOutputWatcher.Normalize("  a \n\t b  ") == "a b", $"Normalize gave {Show(PtyOutputWatcher.Normalize("  a \n\t b  "))}");
    return ok;
}

static bool WatcherTakesEachMatchOnce()
{
    var watcher = new PtyOutputWatcher();
    watcher.Append(Encoding.UTF8.GetBytes("next? a\r\ngot:a\r\nnext? "));
    var ok = Expect(watcher.WaitAsync("next?").IsCompleted, "the first prompt was not found");
    ok &= Expect(watcher.WaitAsync("next?").IsCompleted, "the second prompt was not found");
    ok &= Expect(!watcher.WaitAsync("next?").IsCompleted, "a third prompt was found in two");
    watcher.StopWaiting();

    // A key clears what came before it: the next wait looks for the answer to the key.
    watcher.Append(Encoding.UTF8.GetBytes("old prompt"));
    watcher.Clear();
    ok &= Expect(!watcher.WaitAsync("old prompt").IsCompleted, "output from before the key was found");
    watcher.StopWaiting();
    return ok;
}

static bool WatcherWaitsForLaterOutput()
{
    var watcher = new PtyOutputWatcher();
    var wait = watcher.WaitAsync("完了 ok");
    var ok = Expect(!wait.IsCompleted, "found before any output");

    // The text arrives in pieces: a character cut between its UTF-8 bytes, and a color sequence cut in two.
    var bytes = Encoding.UTF8.GetBytes("完了");
    watcher.Append(bytes.AsSpan(0, 2));
    watcher.Append(bytes.AsSpan(2));
    watcher.Append(Encoding.UTF8.GetBytes("\u001b[3"));
    ok &= Expect(!wait.IsCompleted, "found before the whole text arrived");
    watcher.Append(Encoding.UTF8.GetBytes("2m ok"));
    ok &= Expect(wait.IsCompleted, $"not found after it arrived, read as {Show(watcher.Text)}");

    // A long output that never has the text is cut to its end, so it does not grow without end.
    var waitForEnd = watcher.WaitAsync("the end");
    var line = Encoding.UTF8.GetBytes(new string('x', 1000) + "\r\n");
    for (var i = 0; i < 200; i++)
        watcher.Append(line);
    ok &= Expect(watcher.Text.Length <= 65536, $"kept {watcher.Text.Length} characters");
    watcher.Append(Encoding.UTF8.GetBytes("the end"));
    ok &= Expect(waitForEnd.IsCompleted, "the text after a long output was not found");
    return ok;
}

// A string with its control characters escaped, for messages.
static string Show(string? text) =>
    text is null ? "null" : "\"" + string.Concat(text.Select(static c => char.IsControl(c) ? $"\\u{(int)c:x4}" : c.ToString())) + "\"";
