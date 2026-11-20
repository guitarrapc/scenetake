using System.Globalization;
using System.Text;

namespace Scenetake;

/// <summary>
/// Turns the bytes a person typed in an interactive step into the <c>keys</c> entries that replay it, and writes
/// them as YAML for the scenario (--save-keys). The bytes are what the host terminal sent: xterm sequences on Unix,
/// and on Windows the same or win32-input-mode records, which ConPTY asks the host terminal for.
/// </summary>
internal static class RecordedKeys
{
    /// <summary>A pause between typed characters at least this long starts a new entry, with a sleep before it.</summary>
    internal const double PauseSeconds = 0.5;

    internal enum PressKind
    {
        /// <summary>A typed character (one grapheme cluster).</summary>
        Text,

        /// <summary>A key with a name that <see cref="ScenarioCast.KeyText"/> reads back, such as enter or ctrl+c.</summary>
        Key,

        /// <summary>Input that no key name stands for, such as a mouse click. It is not saved, and a comment says so.</summary>
        Skipped,

        /// <summary>Keys typed while the program read a password. They were never recorded.</summary>
        Hidden,
    }

    /// <summary>One thing the person did, <paramref name="Time"/> seconds after the command started.</summary>
    /// <remarks>A class, not a struct: the native binary shares one copy of List code for all classes.</remarks>
    internal sealed record Press(double Time, PressKind Kind, string Value);

    /// <summary>One <c>keys</c> entry, with the comments that go before it.</summary>
    internal sealed record Entry(double? Sleep, string? Type, string? Key, IReadOnlyList<string> Comments);

    /// <summary>
    /// What the person did, in order. Terminal answers that pass through the input, such as a cursor position
    /// report or focus events, are no keys and are left out without a note.
    /// </summary>
    internal static List<Press> Decode(IReadOnlyList<TimedBytes> input, IReadOnlyList<TimeSpan>? hidden = null)
    {
        var presses = new List<Press>();
        var decoder = Encoding.UTF8.GetDecoder();
        var carry = "";
        var carryTime = 0.0;
        foreach (var chunk in input)
        {
            var time = chunk.Time.TotalSeconds;
            var chars = new char[decoder.GetCharCount(chunk.Data.Span, flush: false)];
            decoder.GetChars(chunk.Data.Span, chars, flush: false);
            var text = carry + new string(chars);
            var start = carry.Length > 0 ? carryTime : time;

            // A sequence cut at the end of a read is finished by the next one. A lone ESC at the end is the Escape key.
            var tail = IncompleteTailStart(text);
            carry = text[tail..];
            carryTime = start;
            ParseVt(FromWin32InputMode(text[..tail]), start, presses);
        }

        if (carry.Length > 0)
            ParseVt(FromWin32InputMode(carry), carryTime, presses);

        if (hidden is not { Count: > 0 })
            return presses;

        // Both are in the order they were typed, so they merge in one pass.
        var merged = new List<Press>(presses.Count + hidden.Count);
        var next = 0;
        foreach (var press in presses)
        {
            while (next < hidden.Count && hidden[next].TotalSeconds < press.Time)
                merged.Add(new Press(hidden[next++].TotalSeconds, PressKind.Hidden, ""));
            merged.Add(press);
        }

        while (next < hidden.Count)
            merged.Add(new Press(hidden[next++].TotalSeconds, PressKind.Hidden, ""));
        return merged;
    }

    /// <summary>
    /// The <c>keys</c> entries that replay <paramref name="presses"/>. Characters typed without a pause of
    /// <see cref="PauseSeconds"/> become one <c>type</c>, and Enter right after them ends it as a line break. Each
    /// other key is an entry of its own. A sleep keeps each pause as the person made it, to a tenth of a second:
    /// the replay types at <paramref name="typingSpeed"/>, and a key after typed text already waits one typing delay.
    /// </summary>
    internal static List<Entry> ToEntries(IReadOnlyList<Press> presses, double typingSpeed)
    {
        var entries = new List<Entry>();
        var comments = new List<string>();
        var lastEnd = 0.0;
        var lastWasType = false;
        StringBuilder? typed = null;
        double? typedSleep = null;
        var typedEnd = 0.0;

        foreach (var press in presses)
        {
            switch (press.Kind)
            {
                case PressKind.Key when press.Value == "enter" && typed is not null && press.Time - typedEnd < PauseSeconds:
                    // Enter ends the typed line. What the person typed next came after the program's answer, so it
                    // is an entry of its own, with the pause they made.
                    typed.Append('\n');
                    typedEnd = press.Time;
                    FlushTyped();
                    break;

                case PressKind.Text:
                    if (typed is not null && press.Time - typedEnd < PauseSeconds)
                    {
                        typed.Append(press.Value);
                        typedEnd = press.Time;
                        break;
                    }

                    FlushTyped();
                    typedSleep = SleepBefore(press.Time);
                    typed = new StringBuilder(press.Value);
                    typedEnd = press.Time;
                    break;

                case PressKind.Key:
                    FlushTyped();
                    entries.Add(new Entry(SleepBefore(press.Time), null, press.Value, TakeComments()));
                    lastEnd = press.Time;
                    lastWasType = false;
                    break;

                case PressKind.Skipped:
                    FlushTyped();
                    AddComment($"not saved: {press.Value}");
                    break;

                case PressKind.Hidden:
                    FlushTyped();
                    // Typed before the prompt, a password would reach the terminal while it still echoes, so the
                    // note asks for a wait first.
                    AddComment("not saved: keys typed while the program read a password. To replay the step, add a 'wait' for its prompt and a 'type' with it here.");
                    // The password stands where the person typed it, so the pause after it counts from there.
                    lastEnd = press.Time;
                    lastWasType = false;
                    break;
            }
        }

        FlushTyped();
        if (comments.Count > 0)
            entries.Add(new Entry(null, null, null, TakeComments()));
        return entries;

        double? SleepBefore(double time)
        {
            var gap = time - lastEnd - (lastWasType ? typingSpeed : 0);
            var rounded = Math.Round(Math.Max(gap, 0), 1, MidpointRounding.AwayFromZero);
            return rounded > 0 ? rounded : null;
        }

        void FlushTyped()
        {
            if (typed is null)
                return;
            entries.Add(new Entry(typedSleep, typed.ToString(), null, TakeComments()));
            lastEnd = typedEnd;
            lastWasType = true;
            typed = null;
            typedSleep = null;
        }

        // Many mouse moves in a row are one note.
        void AddComment(string comment)
        {
            if (comments.Count == 0 || comments[^1] != comment)
                comments.Add(comment);
        }

        IReadOnlyList<string> TakeComments()
        {
            if (comments.Count == 0)
                return [];
            var taken = comments.ToArray();
            comments.Clear();
            return taken;
        }
    }

    /// <summary>
    /// The --save-keys file: for each interactive step, its <c>run</c> and the <c>keys</c> that replay what the person
    /// typed, as a <c>steps</c> list, so the file is valid YAML and each block reads like the step it belongs to.
    /// </summary>
    internal static string FormatFile(string scenarioName, IReadOnlyList<SavedStepKeys> steps, bool everyKeySaved)
    {
        var builder = new StringBuilder();
        builder.Append("# Keys typed in the interactive steps of ").Append(scenarioName).Append(", saved by scenetake --save-keys.\n");
        builder.Append("# To replay a step without a person, replace 'interactive: true' in that step with its 'keys'\n");
        builder.Append("# below, and remove 'normalize-typing'. Each 'type' replays at the step's typing-speed.\n");
        builder.Append("# Where a program takes a varying time, a 'wait' for text it prints is steadier than a 'sleep'.\n");
        if (everyKeySaved)
            builder.Append("# Every key was saved, a password too: Windows does not tell scenetake when a program reads one.\n");
        builder.Append("\nsteps:\n");
        foreach (var step in steps)
        {
            builder.Append("  # step ").Append(step.Number.ToString(CultureInfo.InvariantCulture)).Append('\n');
            builder.Append("  - run: ").Append(Quote(step.Run)).Append('\n');
            builder.Append("    pty: true\n");
            var entries = ToEntries(Decode(step.Input, step.HiddenInput), step.TypingSpeed);
            if (entries.Count == 0)
            {
                builder.Append("    # no keys were typed in this step\n");
                continue;
            }

            builder.Append("    keys:\n");
            foreach (var entry in entries)
                AppendEntry(builder, entry);
        }

        return builder.ToString();
    }

    private static void AppendEntry(StringBuilder builder, Entry entry)
    {
        foreach (var comment in entry.Comments)
            builder.Append("      # ").Append(comment).Append('\n');

        var lines = new List<string>(2);
        if (entry.Sleep is { } sleep)
            lines.Add("sleep: " + sleep.ToString("0.0", CultureInfo.InvariantCulture));
        if (entry.Type is { } type)
            lines.Add("type: " + Quote(type));
        if (entry.Key is { } key)
            lines.Add("key: " + key);

        for (var i = 0; i < lines.Count; i++)
            builder.Append(i == 0 ? "      - " : "        ").Append(lines[i]).Append('\n');
    }

    /// <summary>
    /// A YAML double-quoted scalar. Line breaks and tabs keep their short escapes, other control characters
    /// become <c>\u</c> escapes, and everything else is written as it is.
    /// </summary>
    internal static string Quote(string text)
    {
        var builder = new StringBuilder(text.Length + 2).Append('"');
        foreach (var c in text)
        {
            switch (c)
            {
                case '\\': builder.Append(@"\\"); break;
                case '"': builder.Append("\\\""); break;
                case '\n': builder.Append(@"\n"); break;
                case '\r': builder.Append(@"\r"); break;
                case '\t': builder.Append(@"\t"); break;
                case < ' ' or '\u007f' or (>= '\u0080' and <= '\u009f'):
                    builder.Append(@"\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    break;
                default: builder.Append(c); break;
            }
        }

        return builder.Append('"').ToString();
    }

    /// <summary>
    /// Where a control sequence that the end of <paramref name="text"/> cuts off starts (<c>ESC [</c> and parameters
    /// without their final byte), or the length of the text when nothing is cut off. A lone ESC at the end is the
    /// Escape key, not the start of a sequence.
    /// </summary>
    private static int IncompleteTailStart(string text)
    {
        var esc = text.LastIndexOf('\u001b');
        if (esc < 0 || esc + 1 >= text.Length || text[esc + 1] != '[')
            return text.Length;

        for (var i = esc + 2; i < text.Length; i++)
        {
            if (text[i] is < ' ' or > '?')
                return text.Length;
        }

        return esc;
    }

    /// <summary>
    /// <paramref name="text"/> with each win32-input-mode record, <c>ESC [ Vk ; Sc ; Uc ; Kd ; Cs ; Rc _</c>, replaced
    /// by the bytes an xterm sends for that key press. Key releases and keys that write nothing, such as Shift alone,
    /// give nothing. A record without a virtual key carries one character of a sequence the host terminal sent,
    /// such as an answer to a query, and gives that character.
    /// </summary>
    internal static string FromWin32InputMode(string text)
    {
        if (!text.Contains('_'))
            return text;

        var builder = new StringBuilder(text.Length);
        var i = 0;
        while (i < text.Length)
        {
            if (TryReadWin32Record(text, i, out var length, out var vt))
            {
                builder.Append(vt);
                i += length;
                continue;
            }

            builder.Append(text[i]);
            i++;
        }

        return builder.ToString();
    }

    private static bool TryReadWin32Record(string text, int start, out int length, out string vt)
    {
        length = 0;
        vt = "";
        if (start + 2 >= text.Length || text[start] != '\u001b' || text[start + 1] != '[')
            return false;

        var end = start + 2;
        while (end < text.Length && (char.IsAsciiDigit(text[end]) || text[end] == ';'))
            end++;
        if (end >= text.Length || text[end] != '_')
            return false;

        length = end - start + 1;
        // Fields left out take their defaults: Vk, Sc, Uc, Kd, and Cs 0, Rc 1. Each holds digits only.
        Span<int> fields = [0, 0, 0, 0, 0, 1];
        var field = 0;
        var number = -1;
        for (var i = start + 2; i <= end && field < fields.Length; i++)
        {
            if (i < end && text[i] != ';')
            {
                number = (Math.Max(number, 0) * 10) + (text[i] - '0');
                if (number > 0x10FFFF)
                    number = 0x10FFFF + 1;
                continue;
            }

            if (number >= 0)
                fields[field] = number;
            field++;
            number = -1;
        }

        var (virtualKey, unicode, keyDown, control, repeat) = (fields[0], fields[2], fields[3], fields[4], fields[5]);
        if (keyDown == 0)
            return true;

        var shift = (control & 0x10) != 0;
        var alt = (control & 0x03) != 0;
        var ctrl = (control & 0x0C) != 0;
        string press;
        if (virtualKey == 0)
            press = unicode is > 0 and <= 0x10FFFF and not (>= 0xD800 and <= 0xDFFF) ? char.ConvertFromUtf32(unicode) : "";
        else if (NamedVirtualKey(virtualKey, shift, alt, ctrl) is { } named)
            press = named;
        else if (unicode is > 0 and <= 0x10FFFF and not (>= 0xD800 and <= 0xDFFF))
            // AltGr is Ctrl and Alt together and types the character itself.
            press = (alt && !ctrl ? "\u001b" : "") + char.ConvertFromUtf32(unicode);
        else
            press = "";

        if (press.Length == 0 || repeat <= 1)
        {
            vt = press;
            return true;
        }

        // A key held down repeats.
        var repeated = new StringBuilder(press.Length * Math.Min(repeat, 64));
        for (var i = 0; i < Math.Min(repeat, 64); i++)
            repeated.Append(press);
        vt = repeated.ToString();
        return true;
    }

    /// <summary>The xterm bytes for a Windows virtual key that has no character of its own, or null for any other key.</summary>
    private static string? NamedVirtualKey(int virtualKey, bool shift, bool alt, bool ctrl)
    {
        // xterm's modifier parameter: 1, plus 1 for Shift, 2 for Alt, 4 for Ctrl.
        var modifier = 1 + (shift ? 1 : 0) + (alt ? 2 : 0) + (ctrl ? 4 : 0);
        string Cursor(char final) => modifier == 1 ? $"\u001b[{final}" : $"\u001b[1;{modifier}{final}";
        string Tilde(int code) => modifier == 1 ? $"\u001b[{code}~" : $"\u001b[{code};{modifier}~";
        string Function(char final) => modifier == 1 ? $"\u001bO{final}" : $"\u001b[1;{modifier}{final}";

        return virtualKey switch
        {
            0x08 => ctrl ? "\b" : alt ? "\u001b\u007f" : "\u007f",
            0x09 => shift ? "\u001b[Z" : "\t",
            0x0D => alt ? "\u001b\r" : "\r",
            0x1B => "\u001b",
            0x21 => Tilde(5),
            0x22 => Tilde(6),
            0x23 => Cursor('F'),
            0x24 => Cursor('H'),
            0x25 => Cursor('D'),
            0x26 => Cursor('A'),
            0x27 => Cursor('C'),
            0x28 => Cursor('B'),
            0x2D => Tilde(2),
            0x2E => Tilde(3),
            0x70 => Function('P'),
            0x71 => Function('Q'),
            0x72 => Function('R'),
            0x73 => Function('S'),
            >= 0x74 and <= 0x7B => Tilde(FunctionKeyCodes[virtualKey - 0x74]),
            _ => null,
        };
    }

    /// <summary>The <c>CSI n ~</c> codes of F5 to F12.</summary>
    private static readonly int[] FunctionKeyCodes = [15, 17, 18, 19, 20, 21, 23, 24];

    /// <summary>Key names for the <c>CSI n ~</c> codes a keyboard sends.</summary>
    private static readonly Dictionary<string, string> TildeKeys = new(StringComparer.Ordinal)
    {
        ["1"] = "home", ["2"] = "insert", ["3"] = "delete", ["4"] = "end", ["5"] = "page-up", ["6"] = "page-down",
        ["7"] = "home", ["8"] = "end", ["11"] = "f1", ["12"] = "f2", ["13"] = "f3", ["14"] = "f4",
        ["15"] = "f5", ["17"] = "f6", ["18"] = "f7", ["19"] = "f8", ["20"] = "f9", ["21"] = "f10", ["23"] = "f11", ["24"] = "f12",
    };

    /// <summary>Reads xterm input bytes as presses, all at <paramref name="time"/>.</summary>
    private static void ParseVt(string text, double time, List<Press> presses)
    {
        var i = 0;
        while (i < text.Length)
        {
            var c = text[i];
            if (c == '\u001b')
            {
                i = ParseEscape(text, i, time, presses);
                continue;
            }

            if (c == '\r')
                presses.Add(new Press(time, PressKind.Key, "enter"));
            else if (c == '\t')
                presses.Add(new Press(time, PressKind.Key, "tab"));
            else if (c == '\u007f')
                presses.Add(new Press(time, PressKind.Key, "backspace"));
            else if (c is >= '\u0001' and <= '\u001a')
                presses.Add(new Press(time, PressKind.Key, "ctrl+" + (char)('a' + c - 1)));
            else if (c < ' ' || c is >= '\u0080' and <= '\u009f')
                presses.Add(new Press(time, PressKind.Skipped, Describe(c.ToString()) + ", which has no key name"));
            else
            {
                var length = StringInfo.GetNextTextElementLength(text.AsSpan(i));
                presses.Add(new Press(time, PressKind.Text, text.Substring(i, length)));
                i += length;
                continue;
            }

            i++;
        }
    }

    /// <summary>Reads the sequence that starts with the ESC at <paramref name="start"/>, and returns where the next one starts.</summary>
    private static int ParseEscape(string text, int start, double time, List<Press> presses)
    {
        if (start + 1 >= text.Length)
        {
            presses.Add(new Press(time, PressKind.Key, "escape"));
            return start + 1;
        }

        var next = text[start + 1];
        switch (next)
        {
            case '[':
                return ParseCsi(text, start, time, presses);

            case 'O' when start + 2 < text.Length:
            {
                // SS3: the arrows of application cursor mode, which vim sets, and F1 to F4.
                var name = text[start + 2] switch
                {
                    'A' => "up", 'B' => "down", 'C' => "right", 'D' => "left", 'H' => "home", 'F' => "end",
                    'P' => "f1", 'Q' => "f2", 'R' => "f3", 'S' => "f4",
                    _ => null,
                };
                presses.Add(name is null
                    ? new Press(time, PressKind.Skipped, Describe(text.Substring(start, 3)) + ", which has no key name")
                    : new Press(time, PressKind.Key, name));
                return start + 3;
            }

            case ']' or 'P' or '_' or '^' or 'X' when StringSequenceEnd(text, start) is { } end:
                // A string sequence, such as a color the host terminal reports, is no key. Without its end in the
                // same read it is Alt and the character, as a person types it.
                return end;

            case '\u001b':
                // Escape pressed twice, or Escape before a sequence: the second ESC starts again.
                presses.Add(new Press(time, PressKind.Key, "escape"));
                return start + 1;

            case > ' ' and < '\u007f':
                presses.Add(new Press(time, PressKind.Key, "alt+" + next));
                return start + 2;

            default:
                presses.Add(new Press(time, PressKind.Skipped, "alt+" + Describe(next.ToString()) + ", which has no key name"));
                return start + 2;
        }
    }

    /// <summary>Where the string sequence (OSC, DCS, APC, PM, SOS) at <paramref name="start"/> ends: after BEL or ST. Null when it does not end.</summary>
    private static int? StringSequenceEnd(string text, int start)
    {
        for (var i = start + 2; i < text.Length; i++)
        {
            if (text[i] == '\u0007')
                return i + 1;
            if (text[i] == '\u001b')
                return i + 1 < text.Length && text[i + 1] == '\\' ? i + 2 : null;
        }

        return null;
    }

    private static int ParseCsi(string text, int start, double time, List<Press> presses)
    {
        var end = start + 2;
        while (end < text.Length && text[end] is >= '0' and <= '?')
            end++;
        var parametersEnd = end;
        while (end < text.Length && text[end] is >= ' ' and <= '/')
            end++;
        if (end >= text.Length)
        {
            presses.Add(new Press(time, PressKind.Skipped, Describe(text[start..]) + ", which has no key name"));
            return text.Length;
        }

        var final = text[end];
        var parameters = text[(start + 2)..parametersEnd];
        var next = end + 1;
        var sequence = text[start..next];

        // X10 mouse: CSI M and three bytes for the button and the cell.
        if (final == 'M' && parameters.Length == 0 && parametersEnd == end)
        {
            presses.Add(new Press(time, PressKind.Skipped, "mouse input"));
            return Math.Min(next + 3, text.Length);
        }

        if (parameters.StartsWith('<') && final is 'M' or 'm')
        {
            presses.Add(new Press(time, PressKind.Skipped, "mouse input"));
            return next;
        }

        // Answers to the program's queries (device attributes, modes, cursor position, window size), and focus
        // events: the host terminal sends them, not the person, and a replay has no one to ask.
        if (parametersEnd != end || parameters.Length > 0 && parameters[0] is '<' or '=' or '>' or '?'
            || final is 'R' or 'c' or 'n' or 't' or 'y' or 'x'
            || final is 'I' or 'O' && parameters.Length == 0)
            return next;

        var name = final switch
        {
            // The modifier parameter 1 means no modifier. Any other has no key name.
            'A' or 'B' or 'C' or 'D' or 'H' or 'F' when parameters is "" or "1" or "1;1" => final switch
            {
                'A' => "up", 'B' => "down", 'C' => "right", 'D' => "left", 'H' => "home", _ => "end",
            },
            '~' => TildeName(parameters),
            _ => null,
        };

        // Bracketed paste wraps pasted text in CSI 200 ~ and CSI 201 ~. The text between is typed as it is.
        if (final == '~' && parameters is "200" or "201")
            return next;

        presses.Add(name is null
            ? new Press(time, PressKind.Skipped, (final == 'Z' && parameters.Length == 0 ? "shift+tab" : Describe(sequence)) + ", which has no key name")
            : new Press(time, PressKind.Key, name));
        return next;

        static string? TildeName(string parameters)
        {
            var semicolon = parameters.IndexOf(';');
            if (semicolon >= 0 && parameters[(semicolon + 1)..] != "1")
                return null;
            var code = semicolon >= 0 ? parameters[..semicolon] : parameters;
            return TildeKeys.TryGetValue(code, out var name) ? name : null;
        }
    }

    /// <summary>Input bytes as a reader sees them in a comment: ESC as <c>ESC</c>, other control characters as <c>^X</c>.</summary>
    private static string Describe(string text)
    {
        var builder = new StringBuilder();
        foreach (var c in text)
        {
            if (c == '\u001b')
                builder.Append("ESC ");
            else if (c < ' ')
                builder.Append('^').Append((char)(c + '@'));
            else if (c == '\u007f')
                builder.Append("^?");
            else if (c is >= '\u0080' and <= '\u009f')
                builder.Append(@"\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
            else
                builder.Append(c);
        }

        return builder.ToString().TrimEnd();
    }
}
