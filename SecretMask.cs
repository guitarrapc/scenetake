using System.Buffers;
using System.Globalization;
using System.Text;

/// <summary>
/// Hides secrets in what a recording shows: the texts in <c>settings.mask</c>, and with <c>settings.mask-auto</c> (on
/// by default) known token formats, the values of <c>KEY=value</c> pairs whose key names a secret, passwords in URLs,
/// Bearer tokens, JSON Web Tokens, private key blocks, and the values of the environment's secret variables. Each
/// hidden character becomes <c>*</c>, two for a wide character, so every line keeps its width and a full-screen
/// program's layout stays in place.
/// </summary>
/// <remarks>
/// The masker reads a step's output as one stream, so a secret split across two reads is found. It skips color codes
/// (SGR) and OSC sequences, so a secret that a tool colors in parts is found too. Other control sequences, such as
/// cursor moves, end a run of text: text that a full-screen program draws in different places is not joined. A token
/// keeps its recognizable prefix (<c>ghp_****</c>), and a pair keeps its key, so a reader still sees what was hidden.
/// Patterns are matched by hand-written scanners: a regular expression engine would add about half a megabyte to the
/// Native AOT binary.
/// </remarks>
internal sealed class SecretMasker
{
    internal const char MaskChar = '*';

    /// <summary>Environment values shorter than this are not hidden: short values such as <c>1</c> or <c>true</c> are everywhere.</summary>
    internal const int MinEnvironmentValueLength = 8;

    // A value shorter than this is not taken as a secret in a KEY=value pair: `token_count: 5` stays.
    private const int MinPairValueLength = 4;

    // This file avoids LINQ, tuples in collections, and other generics over value types: Native AOT compiles a copy of
    // such code for each value type, and the first version of this masker cost 84 KB of binary.
    private string[] _literals;
    private string[] _literalLabels;
    private readonly bool _auto;
    private readonly Dictionary<string, int> _counts = new(StringComparer.Ordinal);

    private SecretMasker(List<string> literals, List<string> labels, bool auto)
    {
        SortLongestFirst(literals, labels);
        _literals = literals.ToArray();
        _literalLabels = labels.ToArray();
        _auto = auto;
    }

    // Longest first, so a listed text that contains another one is hidden as a whole. The lists are short.
    private static void SortLongestFirst(List<string> literals, List<string> labels)
    {
        for (var i = 1; i < literals.Count; i++)
        {
            for (var j = i; j > 0 && literals[j].Length > literals[j - 1].Length; j--)
            {
                (literals[j], literals[j - 1]) = (literals[j - 1], literals[j]);
                (labels[j], labels[j - 1]) = (labels[j - 1], labels[j]);
            }
        }
    }

    /// <summary>
    /// Adds the values of secret variables that a command exported for the steps after it (ShellState), so a later
    /// command that prints one shows stars, as for the variables scenetake started with. Only with mask-auto.
    /// </summary>
    internal void Learn(List<KeyValuePair<string, string?>>? environment)
    {
        if (!_auto || environment is null || environment.Count == 0)
            return;

        var literals = new List<string>(_literals);
        var labels = new List<string>(_literalLabels);
        var added = false;
        foreach (var (name, value) in environment)
        {
            if (value is { Length: >= MinEnvironmentValueLength } && IsSecretKey(name) && !IsNonSecretValue(value) && !literals.Contains(value))
            {
                literals.Add(value);
                labels.Add("environment value");
                added = true;
            }
        }

        if (!added)
            return;

        SortLongestFirst(literals, labels);
        _literals = literals.ToArray();
        _literalLabels = labels.ToArray();
    }

    /// <summary>
    /// The masker for a scenario, or null when it would hide nothing: no <c>settings.mask</c> texts and
    /// <c>settings.mask-auto: false</c>. <paramref name="environment"/> gives the values of secret variables to hide.
    /// The playground has none, because its outputs were masked when they were recorded.
    /// </summary>
    internal static SecretMasker? ForScenario(Scenario scenario, IEnumerable<KeyValuePair<string, string?>>? environment)
    {
        var settings = scenario.Settings;
        var auto = settings?.MaskAuto ?? true;
        var literals = new List<string>();
        var labels = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var text in settings?.Mask ?? [])
        {
            if (!string.IsNullOrEmpty(text) && seen.Add(text))
            {
                literals.Add(text);
                labels.Add("settings.mask");
            }
        }

        if (auto && environment is not null)
        {
            foreach (var (name, value) in environment)
            {
                if (value is { Length: >= MinEnvironmentValueLength } && IsSecretKey(name) && !IsNonSecretValue(value) && seen.Add(value))
                {
                    literals.Add(value);
                    labels.Add("environment value");
                }
            }
        }

        return literals.Count == 0 && !auto ? null : new SecretMasker(literals, labels, auto);
    }

    /// <summary>The process's environment variables, for <see cref="ForScenario"/>: the commands inherit them.</summary>
    internal static IEnumerable<KeyValuePair<string, string?>> ProcessEnvironment()
    {
        foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
            yield return new((string)entry.Key, entry.Value as string);
    }

    /// <summary>How many secrets were hidden, by kind, such as <c>GitHub token: 2</c>. Empty when nothing was.</summary>
    internal IReadOnlyDictionary<string, int> Counts => _counts;

    /// <summary>A line for stderr that says what was hidden, such as <c>Masked 3 secrets: 2 key=value secret, 1 GitHub token</c>, or null.</summary>
    internal string? Summary()
    {
        if (_counts.Count == 0)
            return null;

        var kinds = new string[_counts.Count];
        _counts.Keys.CopyTo(kinds, 0);
        var counts = _counts;
        Array.Sort(kinds, (a, b) => counts[a] != counts[b] ? counts[b].CompareTo(counts[a]) : string.CompareOrdinal(a, b));

        var total = 0;
        var text = new StringBuilder();
        foreach (var kind in kinds)
        {
            total += counts[kind];
            text.Append(text.Length == 0 ? "" : ", ").Append(counts[kind]).Append(' ').Append(kind);
        }

        return $"Masked {total} secret{(total == 1 ? "" : "s")}: {text}";
    }

    /// <summary>Text that may hold escape sequences, such as a typed command or a step's comment line.</summary>
    internal string MaskText(string text)
    {
        if (text.Length == 0)
            return text;

        var masked = MaskSegments([Encoding.UTF8.GetBytes(text)]);
        return masked is null ? text : Encoding.UTF8.GetString(masked[0]);
    }

    /// <summary><see cref="MaskText"/> for text the recording does not show, so <see cref="Counts"/> leaves it out.</summary>
    internal string MaskUncounted(string text)
    {
        if (text.Length == 0)
            return text;

        var masked = MaskSegments([Encoding.UTF8.GetBytes(text)], count: false);
        return masked is null ? text : Encoding.UTF8.GetString(masked[0]);
    }

    /// <summary>Whether masking would change <paramref name="text"/>. Text that was masked already has only stars left to hide.</summary>
    internal bool Hides(string text) => !string.Equals(MaskUncounted(text), text, StringComparison.Ordinal);

    /// <summary>A PTY step's output chunks, read as one stream. The same list when nothing is hidden.</summary>
    internal IReadOnlyList<TimedBytes> MaskPty(IReadOnlyList<TimedBytes> chunks)
    {
        var segments = new ReadOnlyMemory<byte>[chunks.Count];
        for (var i = 0; i < segments.Length; i++)
            segments[i] = chunks[i].Data;

        var masked = MaskSegments(segments);
        if (masked is null)
            return chunks;

        var result = new TimedBytes[chunks.Count];
        for (var i = 0; i < result.Length; i++)
            result[i] = new TimedBytes(chunks[i].Time, masked[i]);
        return result;
    }

    /// <summary>Output that is already timed, as an interactive step composes it.</summary>
    internal ComposedPtyOutput MaskComposed(ComposedPtyOutput output)
    {
        var segments = new ReadOnlyMemory<byte>[output.Chunks.Count];
        for (var i = 0; i < segments.Length; i++)
            segments[i] = output.Chunks[i].Data;

        var masked = MaskSegments(segments);
        if (masked is null)
            return output;

        var chunks = new (double Time, ReadOnlyMemory<byte> Data)[segments.Length];
        for (var i = 0; i < chunks.Length; i++)
            chunks[i] = (output.Chunks[i].Time, masked[i]);
        return new ComposedPtyOutput(chunks, output.End);
    }

    /// <summary>
    /// A pipe step's output chunks. stdout and stderr are two streams, each read as a whole: a secret is never split
    /// between them. The same list when nothing is hidden.
    /// </summary>
    internal IReadOnlyList<PipeStreamChunk> MaskPipe(IReadOnlyList<PipeStreamChunk> chunks)
    {
        PipeStreamChunk[]? result = null;
        for (var pass = 0; pass < 2; pass++)
        {
            var stderr = pass == 1;
            var indices = new List<int>();
            for (var i = 0; i < chunks.Count; i++)
            {
                if (chunks[i].IsStderr == stderr)
                    indices.Add(i);
            }

            if (indices.Count == 0)
                continue;

            var segments = new ReadOnlyMemory<byte>[indices.Count];
            for (var k = 0; k < segments.Length; k++)
                segments[k] = Encoding.UTF8.GetBytes(chunks[indices[k]].Text);

            var masked = MaskSegments(segments);
            if (masked is null)
                continue;

            if (result is null)
            {
                result = new PipeStreamChunk[chunks.Count];
                for (var i = 0; i < result.Length; i++)
                    result[i] = chunks[i];
            }

            for (var k = 0; k < indices.Count; k++)
            {
                var chunk = result[indices[k]];
                result[indices[k]] = new PipeStreamChunk(chunk.Time, chunk.IsStderr, Encoding.UTF8.GetString(masked[k]));
            }
        }

        return result ?? chunks;
    }

    /// <summary>
    /// The segments with each hidden character replaced, one array per segment, or null when nothing is hidden. A
    /// character whose bytes are split between two segments is replaced in the first.
    /// </summary>
    private byte[][]? MaskSegments(ReadOnlyMemory<byte>[] segments, bool count = true)
    {
        var total = 0;
        foreach (var segment in segments)
            total += segment.Length;
        if (total == 0)
            return null;

        var all = new byte[total];
        var segmentStarts = new int[segments.Length + 1];
        var offset = 0;
        for (var i = 0; i < segments.Length; i++)
        {
            segmentStarts[i] = offset;
            segments[i].Span.CopyTo(all.AsSpan(offset));
            offset += segments[i].Length;
        }

        segmentStarts[^1] = total;

        var text = VisibleText.Read(all);
        var hidden = Find(text.Text, count);
        if (hidden is null)
            return null;

        // The visible characters to replace, in byte order. Each has its first byte, its byte count, and how many *
        // stand for it. Stand-ins have no bytes, and the second half of a surrogate pair has its first half's bytes.
        var replaced = new List<int>();
        var lastStart = -1;
        for (var k = 0; k < hidden.Length; k++)
        {
            if (!hidden[k] || text.Starts[k] < 0 || text.Starts[k] == lastStart)
                continue;
            replaced.Add(k);
            lastStart = text.Starts[k];
        }

        if (replaced.Count == 0)
            return null;

        var result = new byte[segments.Length][];
        var next = 0;
        var skipUntil = 0;
        var writer = new ArrayBufferWriter<byte>();
        for (var s = 0; s < segments.Length; s++)
        {
            writer.Clear();
            for (var b = segmentStarts[s]; b < segmentStarts[s + 1]; b++)
            {
                if (b < skipUntil)
                    continue;

                if (next < replaced.Count && text.Starts[replaced[next]] == b)
                {
                    var k = replaced[next++];
                    var width = text.Widths[k];
                    writer.GetSpan(width)[..width].Fill((byte)MaskChar);
                    writer.Advance(width);
                    skipUntil = b + text.Lengths[k];
                    continue;
                }

                writer.GetSpan(1)[0] = all[b];
                writer.Advance(1);
            }

            result[s] = writer.WrittenSpan.ToArray();
        }

        return result;
    }

    /// <summary>Which characters of <paramref name="text"/> to hide, or null when none.</summary>
    private bool[]? Find(string text, bool count = true)
    {
        bool[]? hidden = null;

        // Line breaks and the spaces that stand for cursor moves have no bytes, so marking them changes nothing.
        void Hide(int start, int end, string label)
        {
            var added = false;
            for (var k = start; k < end; k++)
            {
                hidden ??= new bool[text.Length];
                if (!hidden[k])
                {
                    hidden[k] = true;
                    added = true;
                }
            }

            if (added && count)
                _counts[label] = _counts.GetValueOrDefault(label) + 1;
        }

        for (var i = 0; i < _literals.Length; i++)
        {
            var literal = _literals[i];
            for (var at = text.IndexOf(literal, StringComparison.Ordinal); at >= 0; at = text.IndexOf(literal, at + literal.Length, StringComparison.Ordinal))
                Hide(at, at + literal.Length, _literalLabels[i]);
        }

        if (_auto)
        {
            FindPrivateKeys(text, Hide);
            FindPrefixedTokens(text, Hide);
            FindJsonWebTokens(text, Hide);
            FindUrlPasswords(text, Hide);
            FindBearerTokens(text, Hide);
            FindPairs(text, Hide);
        }

        return hidden;
    }

    private delegate void HideRange(int start, int end, string label);

    /// <summary>The body of a PEM private key, between its BEGIN and END lines, which stay.</summary>
    private static void FindPrivateKeys(string text, HideRange hide)
    {
        for (var at = text.IndexOf("-----BEGIN ", StringComparison.Ordinal); at >= 0; at = text.IndexOf("-----BEGIN ", at + 1, StringComparison.Ordinal))
        {
            var lineEnd = text.IndexOf('\n', at);
            var header = lineEnd < 0 ? text[at..] : text[at..lineEnd];
            if (!header.Contains("PRIVATE KEY-----", StringComparison.Ordinal) || lineEnd < 0)
                continue;

            var end = text.IndexOf("-----END ", lineEnd, StringComparison.Ordinal);
            hide(lineEnd + 1, end < 0 ? text.Length : end, "private key");
        }
    }

    private sealed class TokenFormat(string prefix, string label, TokenChars chars, int minLength, int ExactLength = 0)
    {
        internal string Prefix { get; } = prefix;
        internal string Label { get; } = label;
        internal TokenChars Chars { get; } = chars;
        internal int MinLength { get; } = minLength;
        internal int ExactLength { get; } = ExactLength;
    }

    [Flags]
    private enum TokenChars
    {
        Upper = 1,
        Lower = 2,
        Digit = 4,
        Underscore = 8,
        Hyphen = 16,
        Alnum = Upper | Lower | Digit,
        Word = Alnum | Underscore,
        WordHyphen = Word | Hyphen,
        AlnumHyphen = Alnum | Hyphen,
    }

    // Formats that providers publish for their keys. The body after the prefix is hidden, and the prefix says what it was.
    private static readonly TokenFormat[] TokenFormats =
    [
        new("ghp_", "GitHub token", TokenChars.Alnum, 36),
        new("gho_", "GitHub token", TokenChars.Alnum, 36),
        new("ghu_", "GitHub token", TokenChars.Alnum, 36),
        new("ghs_", "GitHub token", TokenChars.Alnum, 36),
        new("ghr_", "GitHub token", TokenChars.Alnum, 36),
        new("github_pat_", "GitHub token", TokenChars.Word, 22),
        new("glpat-", "GitLab token", TokenChars.WordHyphen, 20),
        new("xoxb-", "Slack token", TokenChars.AlnumHyphen, 10),
        new("xoxp-", "Slack token", TokenChars.AlnumHyphen, 10),
        new("xoxa-", "Slack token", TokenChars.AlnumHyphen, 10),
        new("xoxr-", "Slack token", TokenChars.AlnumHyphen, 10),
        new("xoxs-", "Slack token", TokenChars.AlnumHyphen, 10),
        new("xoxe-", "Slack token", TokenChars.AlnumHyphen, 10),
        new("AKIA", "AWS access key", TokenChars.Upper | TokenChars.Digit, 16, ExactLength: 16),
        new("ASIA", "AWS access key", TokenChars.Upper | TokenChars.Digit, 16, ExactLength: 16),
        new("sk-", "API key", TokenChars.WordHyphen, 20),
        new("sk_live_", "Stripe key", TokenChars.Alnum, 16),
        new("sk_test_", "Stripe key", TokenChars.Alnum, 16),
        new("rk_live_", "Stripe key", TokenChars.Alnum, 16),
        new("rk_test_", "Stripe key", TokenChars.Alnum, 16),
        new("whsec_", "Stripe key", TokenChars.Alnum, 16),
        new("AIza", "Google API key", TokenChars.WordHyphen, 35, ExactLength: 35),
        new("npm_", "npm token", TokenChars.Alnum, 36),
        new("pypi-", "PyPI token", TokenChars.WordHyphen, 50),
        new("hf_", "Hugging Face token", TokenChars.Alnum, 30),
        new("dckr_pat_", "Docker token", TokenChars.WordHyphen, 20),
    ];

    private static void FindPrefixedTokens(string text, HideRange hide)
    {
        foreach (var format in TokenFormats)
        {
            for (var at = text.IndexOf(format.Prefix, StringComparison.Ordinal); at >= 0; at = text.IndexOf(format.Prefix, at + 1, StringComparison.Ordinal))
            {
                // The prefix must start a word: `task-...` does not hold an `sk-` key.
                if (at > 0 && IsWordChar(text[at - 1]))
                    continue;

                var start = at + format.Prefix.Length;
                var end = start;
                while (end < text.Length && Allows(format.Chars, text[end]))
                    end++;

                var length = end - start;
                if (length < format.MinLength || (format.ExactLength > 0 && (length != format.ExactLength || (end < text.Length && IsWordChar(text[end])))))
                    continue;

                hide(start, end, format.Label);
            }
        }
    }

    /// <summary>A JSON Web Token: three base64url parts, the first two JSON objects. The header part stays.</summary>
    private static void FindJsonWebTokens(string text, HideRange hide)
    {
        for (var at = text.IndexOf("eyJ", StringComparison.Ordinal); at >= 0; at = text.IndexOf("eyJ", at + 1, StringComparison.Ordinal))
        {
            if (at > 0 && IsBase64UrlChar(text[at - 1]))
                continue;

            var headerEnd = SkipBase64Url(text, at);
            if (headerEnd - at < 10 || headerEnd >= text.Length || text[headerEnd] != '.')
                continue;

            var payloadEnd = SkipBase64Url(text, headerEnd + 1);
            if (payloadEnd - headerEnd - 1 < 10 || payloadEnd >= text.Length || text[payloadEnd] != '.')
                continue;

            var signatureEnd = SkipBase64Url(text, payloadEnd + 1);
            if (signatureEnd - payloadEnd - 1 < 10)
                continue;

            hide(headerEnd + 1, signatureEnd, "JSON Web Token");
            at = signatureEnd - 1;
        }

        static int SkipBase64Url(string text, int i)
        {
            while (i < text.Length && IsBase64UrlChar(text[i]))
                i++;
            return i;
        }
    }

    /// <summary>The password in <c>scheme://user:password@host</c>.</summary>
    private static void FindUrlPasswords(string text, HideRange hide)
    {
        for (var at = text.IndexOf("://", StringComparison.Ordinal); at >= 0; at = text.IndexOf("://", at + 3, StringComparison.Ordinal))
        {
            if (at == 0 || !char.IsAsciiLetter(text[at - 1]))
                continue;

            var colon = -1;
            var i = at + 3;
            for (; i < text.Length; i++)
            {
                var c = text[i];
                if (c is '@' or '/' or '\n' or ' ' or '"' or '\'' or '<' or '>')
                    break;
                if (c == ':' && colon < 0)
                    colon = i;
            }

            if (i >= text.Length || text[i] != '@' || colon < 0 || colon == i - 1)
                continue;

            var password = text.AsSpan(colon + 1, i - colon - 1);
            if (IsNonSecretValue(password))
                continue;

            hide(colon + 1, i, "password in a URL");
        }
    }

    /// <summary>The token after <c>Bearer</c>, as an Authorization header or a curl option shows it.</summary>
    private static void FindBearerTokens(string text, HideRange hide)
    {
        for (var at = text.IndexOf("Bearer ", StringComparison.OrdinalIgnoreCase); at >= 0; at = text.IndexOf("Bearer ", at + 1, StringComparison.OrdinalIgnoreCase))
        {
            if (at > 0 && IsWordChar(text[at - 1]))
                continue;

            var start = at + "Bearer ".Length;
            while (start < text.Length && text[start] == ' ')
                start++;

            var end = start;
            while (end < text.Length && IsTokenValueChar(text[end]))
                end++;

            if (end - start >= 16 && !IsNonSecretValue(text.AsSpan(start, end - start)))
                hide(start, end, "Bearer token");
        }
    }

    /// <summary>
    /// The value in <c>KEY=value</c>, <c>key: value</c>, <c>"key": "value"</c>, or <c>--password=value</c>, when the
    /// key names a secret (<see cref="IsSecretKey"/>).
    /// </summary>
    private static void FindPairs(string text, HideRange hide)
    {
        for (var i = 1; i < text.Length; i++)
        {
            var separator = text[i];
            if (separator is not ('=' or ':'))
                continue;

            // ==, !=, <=, >=, ::, and :// are not pairs.
            var next = i + 1 < text.Length ? text[i + 1] : '\0';
            var previous = text[i - 1];
            if (next == separator || previous is '!' or '<' or '>' or '=' || (separator == ':' && (previous == ':' || next == '/')))
                continue;

            if (!TryReadKey(text, i, out var key) || !IsSecretKey(key))
                continue;

            if (TryReadValue(text, i + 1, out var start, out var end))
                hide(start, end, "key=value secret");
        }
    }

    private static bool TryReadKey(string text, int separator, out string key)
    {
        key = "";
        var j = separator - 1;
        while (j >= 0 && text[j] == ' ')
            j--;
        if (j < 0)
            return false;

        if (text[j] is '"' or '\'')
        {
            var quote = text[j];
            var open = j - 1;
            while (open >= 0 && text[open] != quote && text[open] != '\n' && j - open <= 64)
                open--;
            if (open < 0 || text[open] != quote || open == j - 1)
                return false;

            key = text[(open + 1)..j];
            return true;
        }

        var end = j + 1;
        while (j >= 0 && end - j <= 64 && IsKeyChar(text[j]))
            j--;
        if (j + 1 == end)
            return false;

        key = text[(j + 1)..end];
        return true;
    }

    private static bool TryReadValue(string text, int from, out int start, out int end)
    {
        start = from;
        while (start < text.Length && text[start] == ' ')
            start++;
        end = start;
        if (start >= text.Length || text[start] == '\n')
            return false;

        var quote = text[start] is '"' or '\'' or '`' ? text[start] : '\0';
        if (quote != '\0')
        {
            start++;
            end = start;
            while (end < text.Length && text[end] != quote && text[end] != '\n')
                end++;
            if (end >= text.Length || text[end] != quote)
                return false;
        }
        else
        {
            while (end < text.Length && !IsValueEnd(text[end]))
                end++;
        }

        // `Authorization: Bearer <token>`: the scheme stays, the credential after it is hidden.
        var value = text.AsSpan(start, end - start);
        var space = value.IndexOf(' ');
        var word = space < 0 ? value : value[..space];
        if (IsAuthScheme(word))
        {
            var credential = start + word.Length;
            while (credential < text.Length && text[credential] == ' ')
                credential++;
            var credentialEnd = credential;
            while (credentialEnd < text.Length && credentialEnd < (quote != '\0' ? end : text.Length) && !IsValueEnd(text[credentialEnd]))
                credentialEnd++;
            start = credential;
            end = credentialEnd;
        }
        else if (quote != '\0' && space >= 0)
        {
            // A quoted value with spaces is a sentence, such as "token": "not set yet".
            return false;
        }

        var secret = text.AsSpan(start, end - start);
        return secret.Length >= MinPairValueLength && !IsNonSecretValue(secret);
    }

    private static bool IsAuthScheme(ReadOnlySpan<char> word) =>
        word.Equals("Bearer", StringComparison.OrdinalIgnoreCase)
        || word.Equals("Basic", StringComparison.OrdinalIgnoreCase)
        || word.Equals("Token", StringComparison.OrdinalIgnoreCase)
        || word.Equals("Bot", StringComparison.OrdinalIgnoreCase)
        || word.Equals("Digest", StringComparison.OrdinalIgnoreCase);

    // The last word of a key that names a secret: GITHUB_TOKEN, db_password, clientSecret.
    private static readonly HashSet<string> SecretWords = new(StringComparer.Ordinal)
    {
        "password", "passwd", "passphrase", "secret", "token", "apikey", "credential", "credentials",
        "privatekey", "accesskey", "secretkey", "authorization",
    };

    // Words that make a following "key" a secret: API_KEY, AccountKey, x-api-key. PUBLIC_KEY and KEY alone are not.
    private static readonly HashSet<string> KeyQualifiers = new(StringComparer.Ordinal)
    {
        "api", "access", "private", "secret", "signing", "encryption", "account", "master", "client", "subscription",
    };

    /// <summary>
    /// Whether a variable or key name names a secret: its last word is one of <see cref="SecretWords"/>, or it ends in
    /// "key" after one of <see cref="KeyQualifiers"/>. Words split at <c>_</c>, <c>-</c>, <c>.</c>, and camelCase.
    /// <c>token_endpoint</c> and <c>PASSWORD_FILE</c> do not name one: their last word is not a secret's.
    /// </summary>
    internal static bool IsSecretKey(string name)
    {
        var words = SplitWords(name);
        if (words.Count == 0)
            return false;

        var last = words[^1];
        return SecretWords.Contains(last) || (last == "key" && words.Count > 1 && KeyQualifiers.Contains(words[^2]));
    }

    private static List<string> SplitWords(string name)
    {
        var words = new List<string>();
        var current = new StringBuilder();
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (!char.IsAsciiLetterOrDigit(c))
            {
                Flush();
                continue;
            }

            // fooBar and HTTPServer split before the upper case letter that starts a word.
            if (char.IsAsciiLetterUpper(c) && current.Length > 0
                && (char.IsAsciiLetterLower(name[i - 1]) || char.IsAsciiDigit(name[i - 1])
                    || (i + 1 < name.Length && char.IsAsciiLetterLower(name[i + 1]) && char.IsAsciiLetterUpper(name[i - 1]))))
            {
                Flush();
            }

            current.Append(char.ToLowerInvariant(c));
        }

        Flush();
        return words;

        void Flush()
        {
            if (current.Length > 0)
                words.Add(current.ToString());
            current.Clear();
        }
    }

    /// <summary>Values that are not secrets: placeholders, references to variables, and masks.</summary>
    private static bool IsNonSecretValue(ReadOnlySpan<char> value)
    {
        if (value.IsEmpty || value[0] is '$' or '%' or '{' or '<')
            return true;

        var allStars = true;
        var allDigits = true;
        foreach (var c in value)
        {
            allStars &= c == MaskChar;
            allDigits &= char.IsAsciiDigit(c);
        }

        if (allStars || allDigits)
            return true;

        return value.Equals("null", StringComparison.OrdinalIgnoreCase)
            || value.Equals("true", StringComparison.OrdinalIgnoreCase)
            || value.Equals("false", StringComparison.OrdinalIgnoreCase)
            || value.Equals("none", StringComparison.OrdinalIgnoreCase)
            || value.Equals("undefined", StringComparison.OrdinalIgnoreCase);
    }

    private static bool Allows(TokenChars chars, char c) =>
        (char.IsAsciiLetterUpper(c) && chars.HasFlag(TokenChars.Upper))
        || (char.IsAsciiLetterLower(c) && chars.HasFlag(TokenChars.Lower))
        || (char.IsAsciiDigit(c) && chars.HasFlag(TokenChars.Digit))
        || (c == '_' && chars.HasFlag(TokenChars.Underscore))
        || (c == '-' && chars.HasFlag(TokenChars.Hyphen));

    private static bool IsWordChar(char c) => char.IsAsciiLetterOrDigit(c) || c is '_' or '-';

    private static bool IsBase64UrlChar(char c) => char.IsAsciiLetterOrDigit(c) || c is '_' or '-';

    private static bool IsKeyChar(char c) => char.IsAsciiLetterOrDigit(c) || c is '_' or '-' or '.';

    private static bool IsTokenValueChar(char c) => char.IsAsciiLetterOrDigit(c) || c is '-' or '.' or '_' or '~' or '+' or '/' or '=';

    private static bool IsValueEnd(char c) => c is ' ' or '\n' or ',' or ';' or ')' or ']' or '}' or '>' or '"' or '\'' or '`';

    /// <summary>
    /// The text a stream shows, with each character's place in the bytes. Color codes and OSC sequences are left out.
    /// Line breaks and other control sequences become <c>\n</c>, and a cursor move to the right becomes spaces. Those
    /// stand-ins have no bytes of their own (start −1), so they are never replaced.
    /// </summary>
    private sealed class VisibleText
    {
        private readonly StringBuilder _text;
        private readonly List<int> _starts;
        private readonly List<int> _lengths;
        private readonly List<int> _widths;

        private VisibleText(int capacity)
        {
            _text = new StringBuilder(capacity);
            _starts = new List<int>(capacity);
            _lengths = new List<int>(capacity);
            _widths = new List<int>(capacity);
        }

        internal string Text { get; private set; } = "";
        internal int[] Starts { get; private set; } = [];
        internal int[] Lengths { get; private set; } = [];
        internal int[] Widths { get; private set; } = [];

        private bool _lastIsBreak;

        private void Add(char c, int start, int length, int width)
        {
            _text.Append(c);
            _starts.Add(start);
            _lengths.Add(length);
            _widths.Add(width);
            _lastIsBreak = false;
        }

        // One stand-in for a run of line breaks and control sequences.
        private void Break()
        {
            if (_lastIsBreak)
                return;
            Add('\n', -1, 0, 0);
            _lastIsBreak = true;
        }

        internal static VisibleText Read(byte[] bytes)
        {
            var visible = new VisibleText(bytes.Length);
            Span<char> chars = stackalloc char[2];
            var i = 0;
            while (i < bytes.Length)
            {
                var b = bytes[i];
                if (b == 0x1B)
                {
                    i = visible.SkipEscape(bytes, i);
                    continue;
                }

                if (b < 0x20)
                {
                    if (b == (byte)'\t')
                        visible.Add(' ', -1, 0, 0);
                    else
                        visible.Break();
                    i++;
                    continue;
                }

                if (b == 0x7F)
                {
                    i++;
                    continue;
                }

                if (b < 0x80)
                {
                    visible.Add((char)b, i, 1, 1);
                    i++;
                    continue;
                }

                if (Rune.DecodeFromUtf8(bytes.AsSpan(i), out var rune, out var consumed) != OperationStatus.Done)
                {
                    visible.Break();
                    i++;
                    continue;
                }

                if (rune.Value is >= 0x80 and <= 0x9F)
                {
                    visible.Break();
                }
                else
                {
                    var width = IsCombining(rune) ? 0 : ScreenBuffer.IsWideCodePoint(rune.Value) ? 2 : 1;
                    var count = rune.EncodeToUtf16(chars);
                    for (var k = 0; k < count; k++)
                        visible.Add(chars[k], i, consumed, width);
                }

                i += consumed;
            }

            visible.Text = visible._text.ToString();
            visible.Starts = [.. visible._starts];
            visible.Lengths = [.. visible._lengths];
            visible.Widths = [.. visible._widths];
            return visible;
        }

        private int SkipEscape(byte[] bytes, int i)
        {
            if (i + 1 >= bytes.Length)
                return bytes.Length;

            var kind = bytes[i + 1];
            if (kind == (byte)'[')
            {
                var j = i + 2;
                while (j < bytes.Length && bytes[j] is >= 0x20 and <= 0x3F)
                    j++;
                if (j >= bytes.Length)
                    return bytes.Length;

                var final = bytes[j];
                if (final == (byte)'C')
                {
                    // ConPTY writes a run of spaces as a cursor move to the right: `KEY:\e[1Cvalue`.
                    var count = ParseCount(bytes.AsSpan(i + 2, j - i - 2));
                    for (var k = 0; k < Math.Min(count, 512); k++)
                        Add(' ', -1, 0, 0);
                }
                else if (final is not ((byte)'m' or (byte)'X'))
                {
                    // Colors (m) and erased cells (X) leave the text joined. Other sequences move the cursor or
                    // change the screen.
                    Break();
                }

                return j + 1;
            }

            if (kind == (byte)']')
            {
                // OSC: titles, hyperlinks, colors. A hyperlink's text goes on after it, so it is skipped quietly.
                var j = i + 2;
                while (j < bytes.Length && bytes[j] != 0x07 && !(bytes[j] == 0x1B && j + 1 < bytes.Length && bytes[j + 1] == (byte)'\\'))
                    j++;
                return j >= bytes.Length ? bytes.Length : bytes[j] == 0x07 ? j + 1 : j + 2;
            }

            if (kind is (byte)'P' or (byte)'X' or (byte)'^' or (byte)'_')
            {
                // DCS, SOS, PM, APC, such as a soft font: not text.
                var j = i + 2;
                while (j < bytes.Length && !(bytes[j] == 0x1B && j + 1 < bytes.Length && bytes[j + 1] == (byte)'\\'))
                    j++;
                Break();
                return Math.Min(j + 2, bytes.Length);
            }

            if (kind is >= 0x20 and <= 0x2F)
            {
                // A character set choice such as ESC ( B, which curses writes inside text.
                var j = i + 1;
                while (j < bytes.Length && bytes[j] is >= 0x20 and <= 0x2F)
                    j++;
                return Math.Min(j + 1, bytes.Length);
            }

            Break();
            return i + 2;
        }

        private static int ParseCount(ReadOnlySpan<byte> parameters)
        {
            var count = 0;
            foreach (var b in parameters)
            {
                if (b is < (byte)'0' or > (byte)'9')
                    return 1;
                count = Math.Min(count * 10 + (b - '0'), 10_000);
            }

            return Math.Max(count, 1);
        }

        private static bool IsCombining(Rune rune) =>
            Rune.GetUnicodeCategory(rune) is UnicodeCategory.NonSpacingMark or UnicodeCategory.EnclosingMark or UnicodeCategory.SpacingCombiningMark;
    }
}
