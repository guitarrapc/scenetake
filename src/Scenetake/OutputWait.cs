using System.Text;

namespace Scenetake;

internal static class Delays
{
    /// <summary>
    /// A wait in seconds as a delay that Task.Delay and CancellationTokenSource accept: about 24 days at most, which a
    /// recording never reaches. A longer timeout or wait-timeout would make them throw instead of waiting.
    /// </summary>
    internal static TimeSpan Of(double seconds) =>
        seconds * 1000 >= int.MaxValue ? TimeSpan.FromMilliseconds(int.MaxValue) : TimeSpan.FromSeconds(Math.Max(seconds, 0));
}

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
    private char[] decoded = [];
    // How much of the text the pending wait has looked through. Text only grows at its end until a wait takes it or the
    // oldest is dropped, so the next look starts just before here: a match that ends in the new output can start at
    // most the needle's length back. Searching all of it after every read, up to MaxText characters, was slow while a
    // program drew a lot.
    private int searched;
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
            var count = decoder.GetCharCount(data, flush: false);
            if (decoded.Length < count)
                decoded = new char[Math.Max(count, 4096)];
            count = decoder.GetChars(data, decoded, flush: false);
            AppendLocked(decoded.AsSpan(0, count));
        }
    }

    /// <summary>
    /// Adds output that its stream's own decoder read, such as a background command's, which on Windows may be in the
    /// console's code page rather than UTF-8 (<see cref="PipeOutputDecoder"/>).
    /// </summary>
    internal void Append(ReadOnlySpan<char> chars)
    {
        lock (gate)
            AppendLocked(chars);
    }

    private void AppendLocked(ReadOnlySpan<char> chars)
    {
        foreach (var c in chars)
            Feed(c);

        if (text.Length > MaxText)
        {
            var dropped = text.Length - MaxText / 2;
            text.Remove(0, dropped);
            searched = Math.Max(0, searched - dropped);
        }

        if (pending is not null && TryTake(pending))
        {
            found!.TrySetResult();
            pending = null;
            found = null;
        }
    }

    /// <summary>Forgets the output so far: a key is about to be written, and a later wait looks for the answer to it.</summary>
    internal void Clear()
    {
        lock (gate)
        {
            text.Clear();
            searched = 0;
        }
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
            // A new text: none of the output has been looked through for it yet.
            searched = 0;
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
        if (needle.Length == 0)
            return true;

        // The first match starts at or after `from`: one that starts before it ends inside the text already looked
        // through, which held none.
        var from = Math.Max(0, searched - needle.Length + 1);
        var index = from >= text.Length ? -1 : text.ToString(from, text.Length - from).IndexOf(needle, StringComparison.Ordinal);
        if (index < 0)
        {
            searched = text.Length;
            return false;
        }

        text.Remove(0, from + index + needle.Length);
        searched = 0;
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
