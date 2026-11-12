using System.Buffers;
using System.Diagnostics;
using System.Text;

internal readonly record struct PipeStreamChunk(TimeSpan Time, bool IsStderr, string Text);

internal static class PipeStreamCapture
{
    internal static async Task<IReadOnlyList<PipeStreamChunk>> ReadAsync(Process process, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var stdoutChunks = new List<PipeStreamChunk>();
        var stderrChunks = new List<PipeStreamChunk>();

        var stdoutTask = ReadStreamAsync(process.StandardOutput, stdoutChunks, isStderr: false, stopwatch, cancellationToken);
        var stderrTask = ReadStreamAsync(process.StandardError, stderrChunks, isStderr: true, stopwatch, cancellationToken);
        await Task.WhenAll(stdoutTask, stderrTask);

        return MergeChronologically(stdoutChunks, stderrChunks);
    }

    private static Task ReadStreamAsync(
        StreamReader reader,
        List<PipeStreamChunk> chunks,
        bool isStderr,
        Stopwatch stopwatch,
        CancellationToken cancellationToken) =>
        // On Windows the reader's encoding is the code page of the terminal that started scenetake, which is
        // neither what UTF-8 tools write nor the code page of the console the child has, so the bytes go
        // through PipeOutputDecoder. Elsewhere the reader's encoding is right and is kept.
        OperatingSystem.IsWindows()
            ? ReadBytesAsync(reader.BaseStream, new PipeOutputDecoder(new WindowsCodePageDecoder(WindowsCodePageDecoder.OemCodePage())), chunks, isStderr, stopwatch, cancellationToken)
            : ReadCharsAsync(reader, chunks, isStderr, stopwatch, cancellationToken);

    private static async Task ReadCharsAsync(
        StreamReader reader,
        List<PipeStreamChunk> chunks,
        bool isStderr,
        Stopwatch stopwatch,
        CancellationToken cancellationToken)
    {
        var buffer = new char[4096];
        while (!cancellationToken.IsCancellationRequested)
        {
            var read = await reader.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
            if (read == 0)
                break;

            chunks.Add(new PipeStreamChunk(stopwatch.Elapsed, isStderr, new string(buffer, 0, read)));
        }
    }

    private static async Task ReadBytesAsync(
        Stream stream,
        Decoder decoder,
        List<PipeStreamChunk> chunks,
        bool isStderr,
        Stopwatch stopwatch,
        CancellationToken cancellationToken)
    {
        var bytes = new byte[4096];
        // PipeOutputDecoder returns at most one char per byte, its held-back bytes included.
        var chars = new char[bytes.Length + PipeOutputDecoder.MaxPendingBytes];
        while (!cancellationToken.IsCancellationRequested)
        {
            var read = await stream.ReadAsync(bytes.AsMemory(0, bytes.Length), cancellationToken);
            var count = decoder.GetChars(bytes, 0, read, chars, 0, flush: read == 0);
            if (count > 0)
                chunks.Add(new PipeStreamChunk(stopwatch.Elapsed, isStderr, new string(chars, 0, count)));
            if (read == 0)
                break;
        }
    }

    private static List<PipeStreamChunk> MergeChronologically(
        IReadOnlyList<PipeStreamChunk> stdoutChunks,
        IReadOnlyList<PipeStreamChunk> stderrChunks)
    {
        if (stdoutChunks.Count == 0)
            return [.. stderrChunks];
        if (stderrChunks.Count == 0)
            return [.. stdoutChunks];

        var merged = new List<PipeStreamChunk>(stdoutChunks.Count + stderrChunks.Count);
        var stdoutIndex = 0;
        var stderrIndex = 0;
        while (stdoutIndex < stdoutChunks.Count && stderrIndex < stderrChunks.Count)
        {
            if (stdoutChunks[stdoutIndex].Time <= stderrChunks[stderrIndex].Time)
                merged.Add(stdoutChunks[stdoutIndex++]);
            else
                merged.Add(stderrChunks[stderrIndex++]);
        }

        while (stdoutIndex < stdoutChunks.Count)
            merged.Add(stdoutChunks[stdoutIndex++]);

        while (stderrIndex < stderrChunks.Count)
            merged.Add(stderrChunks[stderrIndex++]);

        return merged;
    }
}

/// <summary>Decodes bytes that are not UTF-8, in a legacy code page.</summary>
internal interface ILegacyTextDecoder
{
    /// <summary>
    /// Appends the text of <paramref name="bytes"/> and returns how many bytes it used. Unless
    /// <paramref name="final"/>, a character whose bytes have not all arrived is left for the next call.
    /// </summary>
    int Decode(ReadOnlySpan<byte> bytes, bool final, StringBuilder text);
}

/// <summary>
/// Decodes what a Windows child writes to a redirected stdout or stderr. A child gets a console of its own,
/// and its code page is the system's OEM code page (932 on Japanese Windows), whatever code page the
/// terminal that started scenetake uses. Some tools write UTF-8 regardless (git, bash and other MSYS tools,
/// and pwsh, which scenetake tells to); others convert to the console's code page (cmd, ping). Each line is
/// read as UTF-8 when it is valid UTF-8, and in the legacy code page when it is not, so both come out right.
/// Text in a legacy DBCS code page is almost never valid UTF-8, and ASCII reads the same either way.
/// </summary>
internal sealed class PipeOutputDecoder(ILegacyTextDecoder legacy) : Decoder
{
    /// <summary>The most bytes held back between calls: an unfinished UTF-8 sequence has at most 3.</summary>
    internal const int MaxPendingBytes = 3;

    private static ReadOnlySpan<byte> Utf8Bom => [0xEF, 0xBB, 0xBF];

    private readonly StringBuilder _text = new();
    private byte[] _pending = [];
    private bool _atStart = true;
    private char[] _utf16 = [];

    public override int GetCharCount(byte[] bytes, int index, int count) => GetCharCount(bytes, index, count, flush: false);

    public override int GetCharCount(byte[] bytes, int index, int count, bool flush) =>
        Decode(bytes.AsSpan(index, count), flush, commit: false).Length;

    public override int GetChars(byte[] bytes, int byteIndex, int byteCount, char[] chars, int charIndex) =>
        GetChars(bytes, byteIndex, byteCount, chars, charIndex, flush: false);

    public override int GetChars(byte[] bytes, int byteIndex, int byteCount, char[] chars, int charIndex, bool flush)
    {
        var text = Decode(bytes.AsSpan(byteIndex, byteCount), flush, commit: true);
        if (text.Length > chars.Length - charIndex)
            throw new ArgumentException("The output buffer is too small for the decoded text.", nameof(chars));
        text.CopyTo(chars.AsSpan(charIndex));
        return text.Length;
    }

    public override void Reset()
    {
        _pending = [];
        _atStart = true;
    }

    private string Decode(ReadOnlySpan<byte> input, bool flush, bool commit)
    {
        ReadOnlySpan<byte> combined = _pending.Length == 0 ? input : [.. _pending, .. input];
        var start = 0;
        if (_atStart)
        {
            // A BOM at the start of the stream is skipped, as the StreamReader this replaces did. Until three
            // bytes have arrived, bytes that may begin one are held back.
            if (!flush && combined.Length < Utf8Bom.Length && Utf8Bom.StartsWith(combined))
            {
                if (commit)
                    _pending = combined.ToArray();
                return "";
            }

            if (combined.StartsWith(Utf8Bom))
                start = Utf8Bom.Length;
        }

        _text.Clear();
        while (start < combined.Length)
        {
            var lineFeed = combined[start..].IndexOf((byte)'\n');
            var end = lineFeed < 0 ? combined.Length : start + lineFeed + 1;
            var line = combined[start..end];
            var used = DecodeLine(line, complete: lineFeed >= 0 || flush);
            start += used;
            if (used < line.Length)
                break;
        }

        var text = _text.ToString();
        if (commit)
        {
            _pending = combined[start..].ToArray();
            _atStart = false;
        }

        return text;
    }

    /// <summary>
    /// Decodes a line, or the start of one that has not ended yet, and returns how many bytes it used. Only an
    /// unfinished character at the end of an unfinished line is left over.
    /// </summary>
    private int DecodeLine(ReadOnlySpan<byte> line, bool complete)
    {
        // UTF-8 never needs more chars than bytes.
        if (_utf16.Length < line.Length)
            _utf16 = new char[Math.Max(line.Length, 256)];

        var status = System.Text.Unicode.Utf8.ToUtf16(line, _utf16, out var read, out var written, replaceInvalidSequences: false, isFinalBlock: complete);
        if (status is OperationStatus.Done or OperationStatus.NeedMoreData)
        {
            _text.Append(_utf16, 0, written);
            return read;
        }

        return legacy.Decode(line, complete, _text);
    }
}

/// <summary>Decodes a Windows code page with the OS, as a console does.</summary>
internal sealed class WindowsCodePageDecoder(uint codePage) : ILegacyTextDecoder
{
    private char[] _chars = [];

    /// <summary>The code page a new console starts with, which a child of scenetake gets.</summary>
    internal static uint OemCodePage() => Kernel32.GetOEMCP();

    public int Decode(ReadOnlySpan<byte> bytes, bool final, StringBuilder text)
    {
        var length = bytes.Length;
        if (!final)
        {
            // A trail byte can have the value of a lead byte, so only a walk from the start of the line finds
            // whether the last byte is a lead byte whose trail byte has not arrived.
            var index = 0;
            while (index < bytes.Length)
                index += Kernel32.IsDBCSLeadByteEx(codePage, bytes[index]) ? 2 : 1;
            if (index > bytes.Length)
                length--;
        }

        if (length == 0)
            return 0;

        // No Windows code page gives more UTF-16 chars than it has bytes.
        if (_chars.Length < length)
            _chars = new char[Math.Max(length, 256)];
        var written = Kernel32.MultiByteToWideChar(codePage, bytes[..length], _chars);
        text.Append(_chars, 0, written);
        return length;
    }
}
