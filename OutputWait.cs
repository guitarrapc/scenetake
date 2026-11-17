using System.Text;

/// <summary>
/// A PTY step's output as plain text, for the <c>wait</c> entries of its <c>keys</c>. Escape sequences are left
/// out. Line breaks, cursor moves, and runs of spaces all count as one space, because a program can move the
/// cursor where it could print spaces (ConPTY does), and a line can wrap anywhere. A wait looks for its text in the
/// output that arrived after the previous entry, and takes everything up to the end of the match, so the next wait
/// looks further on, as expect does.
/// </summary>
internal sealed class PtyOutputWatcher
{
    /// <summary>
    /// The most text kept for a wait. Older output is dropped, so a program that draws for a long time without
    /// printing the text does not grow it without end.
    /// </summary>
    private const int MaxText = 65536;

    private readonly Lock gate = new();
    private readonly Decoder decoder = Encoding.UTF8.GetDecoder();
    private readonly StringBuilder text = new();
    private State state;
    private string? pending;
    private TaskCompletionSource? found;

    private enum State
    {
        Ground,
        Escape,
        Csi,
        EscapeIntermediate,
        String,
        StringEscape,
    }

    /// <summary>Adds output from the PTY. Ends a wait whose text it completes.</summary>
    internal void Append(ReadOnlySpan<byte> data)
    {
        lock (gate)
        {
            var chars = new char[decoder.GetCharCount(data, flush: false)];
            decoder.GetChars(data, chars, flush: false);
            foreach (var c in chars)
                Feed(c);

            if (text.Length > MaxText)
                text.Remove(0, text.Length - MaxText / 2);

            if (pending is not null && TryTake(pending))
            {
                found!.TrySetResult();
                pending = null;
                found = null;
            }
        }
    }

    /// <summary>Forgets the output so far: a key is about to be written, and a later wait looks for the answer to it.</summary>
    internal void Clear()
    {
        lock (gate)
            text.Clear();
    }

    /// <summary>
    /// Completes when <paramref name="needle"/> is in the output, at once when it already is. Spaces at its ends do
    /// not count, and runs of spaces inside it match as one, as in the output.
    /// </summary>
    internal Task WaitAsync(string needle)
    {
        var normalized = Normalize(needle);
        lock (gate)
        {
            if (TryTake(normalized))
                return Task.CompletedTask;

            pending = normalized;
            found = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            return found.Task;
        }
    }

    /// <summary>Gives up the wait in progress.</summary>
    internal void StopWaiting()
    {
        lock (gate)
        {
            pending = null;
            found = null;
        }
    }

    /// <summary>The output as plain text so far, for tests.</summary>
    internal string Text
    {
        get
        {
            lock (gate)
                return text.ToString();
        }
    }

    /// <summary><paramref name="needle"/> as the output is kept: runs of white space as one space, none at the ends.</summary>
    internal static string Normalize(string needle)
    {
        var builder = new StringBuilder(needle.Length);
        foreach (var c in needle)
        {
            if (char.IsWhiteSpace(c) || char.IsControl(c))
            {
                if (builder.Length > 0 && builder[^1] != ' ')
                    builder.Append(' ');
            }
            else
            {
                builder.Append(c);
            }
        }

        return builder.ToString().TrimEnd(' ');
    }

    private bool TryTake(string needle)
    {
        var index = text.ToString().IndexOf(needle, StringComparison.Ordinal);
        if (index < 0)
            return false;

        text.Remove(0, index + needle.Length);
        return true;
    }

    private void Feed(char c)
    {
        switch (state)
        {
            case State.Ground:
                if (c == '\u001b')
                    state = State.Escape;
                else if (char.IsWhiteSpace(c))
                    AddSpace();
                else if (!char.IsControl(c))
                    text.Append(c);
                // Other control characters, such as BEL or backspace, print nothing.
                break;

            case State.Escape:
                state = c switch
                {
                    '[' => State.Csi,
                    ']' or 'P' or 'X' or '^' or '_' => State.String,
                    >= ' ' and <= '/' => State.EscapeIntermediate,
                    _ => State.Ground,
                };
                // Index, next line, and reverse index move the cursor to another line.
                if (c is 'D' or 'E' or 'M')
                    AddSpace();
                break;

            case State.Csi:
                if (c == '\u001b')
                {
                    state = State.Escape;
                }
                else if (c is >= '@' and <= '~')
                {
                    // Cursor moves part what is on either side, as a line break does. Forward (C) stands for spaces.
                    if (c is 'A' or 'B' or 'C' or 'D' or 'E' or 'F' or 'G' or 'H' or 'd' or 'f')
                        AddSpace();
                    state = State.Ground;
                }

                break;

            case State.EscapeIntermediate:
                if (c is >= '0' and <= '~')
                    state = State.Ground;
                break;

            case State.String:
                if (c == '\u0007')
                    state = State.Ground;
                else if (c == '\u001b')
                    state = State.StringEscape;
                break;

            case State.StringEscape:
                // ST (ESC \) ends the string. Any other ESC ends it too and starts a sequence, as on a terminal.
                if (c == '\\')
                {
                    state = State.Ground;
                }
                else
                {
                    state = State.Escape;
                    Feed(c);
                }

                break;
        }
    }

    private void AddSpace()
    {
        if (text.Length > 0 && text[^1] != ' ')
            text.Append(' ');
    }
}
