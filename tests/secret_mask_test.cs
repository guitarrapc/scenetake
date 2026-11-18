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
#:include ../PlaygroundRender.cs

// Secret masking (.github/docs/spec_scenario.md → Masking secrets). Every secret here is put together at run time,
// so no file in the repository holds a whole token that a secret scanner would report.

using System.Text;
using static ScenarioCast;

var failures = 0;

failures += Run("ListedTextIsHidden", ListedTextIsHidden);
failures += Run("ListedTextSplitAcrossChunks", ListedTextSplitAcrossChunks);
failures += Run("ColorCodesDoNotSplitASecret", ColorCodesDoNotSplitASecret);
failures += Run("CursorMovesSeparateText", CursorMovesSeparateText);
failures += Run("ConPtyCursorForwardIsASpace", ConPtyCursorForwardIsASpace);
failures += Run("WideCharactersKeepTheirWidth", WideCharactersKeepTheirWidth);
failures += Run("KnownTokensKeepTheirPrefix", KnownTokensKeepTheirPrefix);
failures += Run("TokenLookalikesStay", TokenLookalikesStay);
failures += Run("SecretPairsHideTheValue", SecretPairsHideTheValue);
failures += Run("OtherPairsStay", OtherPairsStay);
failures += Run("UrlPasswordIsHidden", UrlPasswordIsHidden);
failures += Run("PrivateKeyBodyIsHidden", PrivateKeyBodyIsHidden);
failures += Run("JsonWebTokenKeepsItsHeader", JsonWebTokenKeepsItsHeader);
failures += Run("SecretEnvironmentValuesAreHidden", SecretEnvironmentValuesAreHidden);
failures += Run("MaskAutoFalseHidesListedTextOnly", MaskAutoFalseHidesListedTextOnly);
failures += Run("MaskingTwiceChangesNothing", MaskingTwiceChangesNothing);
failures += Run("PipeStreamsAreMaskedSeparately", PipeStreamsAreMaskedSeparately);
failures += Run("SummaryCountsByKind", SummaryCountsByKind);
failures += Run("SecretKeyNames", SecretKeyNames);
failures += Run("SettingsKeysParse", SettingsKeysParse);
failures += Run("RecordingHidesCommandCommentAndOutput", RecordingHidesCommandCommentAndOutput);

if (failures > 0)
{
    Console.Error.WriteLine($"Failed: {failures} secret mask test(s)");
    return 1;
}

Console.Error.WriteLine("all secret mask tests passed");
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

static bool Same(string label, string expected, string actual) =>
    Expect(expected == actual, $"{label}: expected {Show(expected)}, got {Show(actual)}");

static string Show(string text) => "\"" + text.Replace("\u001b", "\\e").Replace("\r", "\\r").Replace("\n", "\\n") + "\"";

static string Stars(int count) => new('*', count);

static string Repeat(string text, int length)
{
    var sb = new StringBuilder();
    while (sb.Length < length)
        sb.Append(text);
    return sb.ToString(0, length);
}

static SecretMasker Masker(string? yamlSettings = null, IEnumerable<KeyValuePair<string, string?>>? environment = null) =>
    SecretMasker.ForScenario(ParseScenario($"settings:\n{yamlSettings ?? "  prompt: \"$ \""}\nsteps:\n  - echo hi\n"), environment)
        ?? throw new InvalidOperationException("no masker");

static string MaskPty(SecretMasker masker, params string[] chunks)
{
    var input = chunks.Select((text, i) => new TimedBytes(TimeSpan.FromMilliseconds(i), Encoding.UTF8.GetBytes(text))).ToArray();
    var output = masker.MaskPty(input);
    return string.Join("|", output.Select(static chunk => Encoding.UTF8.GetString(chunk.Data.Span)));
}

static bool ListedTextIsHidden()
{
    var masker = Masker("  mask:\n    - \"s3cret value\"");
    return Same("listed", "the value is ************.\n", masker.MaskText("the value is s3cret value.\n"));
}

static bool ListedTextSplitAcrossChunks()
{
    var masker = Masker("  mask:\n    - abcdef");
    // Each chunk keeps its place: the stars stay in the chunk that held the characters.
    return Same("split", "x***|***y", MaskPty(masker, "xabc", "defy"));
}

static bool ColorCodesDoNotSplitASecret()
{
    var token = "gh" + "p_" + Repeat("aB3", 36);
    var colored = token[..10] + "\u001b[1m" + token[10..] + "\u001b[0m";
    var expected = "ghp_" + Stars(6) + "\u001b[1m" + Stars(30) + "\u001b[0m";
    return Same("colored", expected, Masker().MaskText(colored));
}

static bool CursorMovesSeparateText()
{
    // A full-screen program draws "abc" and "def" in different places: they are not one word.
    var masker = Masker("  mask:\n    - abcdef");
    return Same("cursor", "abc\u001b[5;1Hdef", masker.MaskText("abc\u001b[5;1Hdef"));
}

static bool ConPtyCursorForwardIsASpace()
{
    // ConPTY writes `TOKEN: value` with the space as a cursor move.
    return Same("cuf", "TOKEN:\u001b[1C" + Stars(10), Masker().MaskText("TOKEN:\u001b[1Cvalue12345"));
}

static bool WideCharactersKeepTheirWidth()
{
    var masker = Masker("  mask:\n    - 秘密の値");
    return Same("wide", "鍵は********です", masker.MaskText("鍵は秘密の値です"));
}

static bool KnownTokensKeepTheirPrefix()
{
    var masker = Masker();
    var cases = new (string Prefix, string Body)[]
    {
        ("gh" + "p_", Repeat("Ab1", 36)),
        ("github" + "_pat_", Repeat("A1_b", 82)),
        ("gl" + "pat-", Repeat("x-Y_9", 20)),
        ("xo" + "xb-", "1234-5678-" + Repeat("ab", 24)),
        ("AK" + "IA", Repeat("Q7W", 16)),
        ("sk" + "-", "proj-" + Repeat("aZ_9-", 40)),
        ("sk" + "_live_", Repeat("aZ9", 24)),
        ("AI" + "za", Repeat("Sy_-9", 35)),
        ("np" + "m_", Repeat("aZ9", 36)),
        ("hf" + "_", Repeat("aZ9", 34)),
    };

    var ok = true;
    foreach (var (prefix, body) in cases)
        ok &= Same(prefix, $"key: {prefix}{Stars(body.Length)} end", masker.MaskText($"key: {prefix}{body} end"));
    return ok;
}

static bool TokenLookalikesStay()
{
    var masker = Masker();
    var ok = true;
    foreach (var text in new[]
    {
        "gh" + "p_" + Repeat("Ab1", 20),               // too short
        "task" + "-" + Repeat("ab", 30),               // sk- inside a word
        "AK" + "IA" + Repeat("Q7W", 15),               // one short
        "AK" + "IA" + Repeat("Q7W", 17),               // one long
        "commit 1f3c5a7d9b2e4f6a8c0d1e3f5a7b9c2d4e6f8a0b",
        "eyJ" + "hbGc.short.x",
    })
    {
        ok &= Same(text, text, masker.MaskText(text));
    }

    return ok;
}

static bool SecretPairsHideTheValue()
{
    var masker = Masker();
    var cases = new (string Input, string Expected)[]
    {
        ("API_KEY=abcd1234", "API_KEY=" + Stars(8)),
        ("export GITHUB_TOKEN=xyz12345", "export GITHUB_TOKEN=" + Stars(8)),
        ("\"password\": \"hunter2x\",", "\"password\": \"" + Stars(8) + "\","),
        ("password: hunter2x", "password: " + Stars(8)),
        ("db.password = 'p@ss-word'", "db.password = '" + Stars(9) + "'"),
        ("mysql --password=abc12345 -u root", "mysql --password=" + Stars(8) + " -u root"),
        ("Authorization: Bearer abcdef0123456789", "Authorization: Bearer " + Stars(16)),
        ("\"Authorization\": \"Basic dXNlcjpwYXNz\"", "\"Authorization\": \"Basic " + Stars(12) + "\""),
        ("aws_secret_access_key = abcdEFGH1234", "aws_secret_access_key = " + Stars(12)),
        ("x-api-key: k-123456", "x-api-key: " + Stars(8)),
        ("clientSecret:\"zz99yy88\"", "clientSecret:\"" + Stars(8) + "\""),
        ("AccountName=demo;AccountKey=abc+def/123==;", "AccountName=demo;AccountKey=" + Stars(13) + ";"),
    };

    var ok = true;
    foreach (var (input, expected) in cases)
        ok &= Same(input, expected, masker.MaskText(input));
    return ok;
}

static bool OtherPairsStay()
{
    var masker = Masker();
    var ok = true;
    foreach (var text in new[]
    {
        "token_endpoint: https://example.com/oauth/token",
        "PASSWORD_FILE=/run/secrets/db",
        "max_tokens: 100",
        "token_count: 5",
        "TOKEN=$GITHUB_TOKEN",
        "if token == expected:",
        "\"token\": null",
        "secretKeyRef: name",
        "PUBLIC_KEY=ssh-ed25519",
        "password: ********",
        "\"token\": \"not set yet\"",
        "Password: ",
        "KEY=value123",
    })
    {
        ok &= Same(text, text, masker.MaskText(text));
    }

    return ok;
}

static bool UrlPasswordIsHidden()
{
    var masker = Masker();
    return Same("url", "DATABASE_URL=postgres://app:" + Stars(7) + "@db:5432/app", masker.MaskText("DATABASE_URL=postgres://app:hunter2@db:5432/app"))
        & Same("no password", "https://example.com:8080/path@x", masker.MaskText("https://example.com:8080/path@x"))
        & Same("ssh", "git@github.com:org/repo.git", masker.MaskText("git@github.com:org/repo.git"));
}

static bool PrivateKeyBodyIsHidden()
{
    var begin = "-----BEGIN OPENSSH " + "PRIVATE KEY-----";
    var end = "-----END OPENSSH " + "PRIVATE KEY-----";
    var input = $"{begin}\r\nb3BlbnNzaC1rZXk\r\nAAAAABG5vbmU=\r\n{end}\r\n";
    var expected = $"{begin}\r\n{Stars(15)}\r\n{Stars(13)}\r\n{end}\r\n";
    return Same("pem", expected, Masker().MaskText(input));
}

static bool JsonWebTokenKeepsItsHeader()
{
    var header = "ey" + "JhbGciOiJIUzI1NiJ9";
    var payload = "ey" + "JzdWIiOiIxMjM0NTY3ODkwIn0";
    var signature = "dozjgNryP4J3jVmNHl0w5N_XgL0n3I9PlFUP0THsR8U";
    var token = $"{header}.{payload}.{signature}";
    return Same("jwt", $"id={header}.{Stars(payload.Length + 1 + signature.Length)} ok", Masker().MaskText($"id={token} ok"));
}

static bool SecretEnvironmentValuesAreHidden()
{
    var environment = new Dictionary<string, string?>
    {
        ["MY_SERVICE_TOKEN"] = "opaque-value-123",
        ["HOME"] = "/home/demo-user",
        ["SHORT_SECRET"] = "abc",
        ["PORT_PASSWORD"] = "12345678",
    };
    var masker = Masker(environment: environment);
    return Same("token value", "got " + Stars(16) + " back", masker.MaskText("got opaque-value-123 back"))
        & Same("home", "cd /home/demo-user", masker.MaskText("cd /home/demo-user"))
        & Same("short", "abc", masker.MaskText("abc"))
        & Same("digits", "12345678", masker.MaskText("12345678"));
}

static bool MaskAutoFalseHidesListedTextOnly()
{
    var masker = Masker("  mask:\n    - hello\n  mask-auto: false", new Dictionary<string, string?> { ["API_TOKEN"] = "zzzzzzzzzz" });
    return Same("listed", "*****", masker.MaskText("hello"))
        & Same("auto off", "API_KEY=abcd1234", masker.MaskText("API_KEY=abcd1234"))
        & Same("environment off", "zzzzzzzzzz", masker.MaskText("zzzzzzzzzz"))
        & Expect(SecretMasker.ForScenario(ParseScenario("settings:\n  mask-auto: false\nsteps:\n  - echo hi\n"), null) is null,
            "mask-auto: false without mask texts gives no masker");
}

static bool MaskingTwiceChangesNothing()
{
    var masker = Masker();
    var chunks = new[] { new TimedBytes(TimeSpan.Zero, Encoding.UTF8.GetBytes("API_KEY=abcd1234\r\n")) };
    var once = masker.MaskPty(chunks);
    var twice = Masker().MaskPty(once);
    return Expect(!ReferenceEquals(once, chunks), "the first pass masks")
        & Expect(ReferenceEquals(once, twice), "the second pass returns the same chunks");
}

static bool PipeStreamsAreMaskedSeparately()
{
    var masker = Masker("  mask:\n    - abcdef");
    var chunks = new[]
    {
        new PipeStreamChunk(TimeSpan.Zero, false, "abc"),
        new PipeStreamChunk(TimeSpan.FromMilliseconds(1), true, "zzz"),
        new PipeStreamChunk(TimeSpan.FromMilliseconds(2), false, "def\n"),
        new PipeStreamChunk(TimeSpan.FromMilliseconds(3), true, "abcdef\n"),
    };
    var masked = masker.MaskPipe(chunks);
    return Same("stdout", "***|***\n", masked[0].Text + "|" + masked[2].Text)
        & Same("stderr", "zzz|******\n", masked[1].Text + "|" + masked[3].Text)
        & Expect(masked.Select(static chunk => chunk.Time).SequenceEqual(chunks.Select(static chunk => chunk.Time)), "times stay");
}

static bool SummaryCountsByKind()
{
    var masker = Masker("  mask:\n    - hello");
    masker.MaskText("hello API_KEY=abcd1234 and API_TOKEN=efgh5678 hello");
    return Same("summary", "Masked 4 secrets: 2 key=value secret, 2 settings.mask", masker.Summary() ?? "(none)")
        & Expect(Masker().Summary() is null, "nothing hidden gives no summary");
}

static bool SecretKeyNames()
{
    var ok = true;
    foreach (var name in new[] { "GITHUB_TOKEN", "TOKEN", "db_password", "clientSecret", "x-api-key", "AWS_SECRET_ACCESS_KEY", "AccountKey", "APIKey", "--password", "Authorization", "MYSQL_ROOT_PASSWORD" })
        ok &= Expect(SecretMasker.IsSecretKey(name), $"{name} names a secret");
    foreach (var name in new[] { "token_endpoint", "PASSWORD_FILE", "PUBLIC_KEY", "KEY", "secretKeyRef", "max_tokens", "TOKENIZERS_PARALLELISM", "author", "SSH_AUTH_SOCK", "PWD" })
        ok &= Expect(!SecretMasker.IsSecretKey(name), $"{name} does not name a secret");
    return ok;
}

static bool SettingsKeysParse()
{
    var scenario = ParseScenario("settings:\n  mask:\n    - one\n    - two\n  mask-auto: false\nsteps:\n  - echo hi\n");
    var unknown = FindUnknownScenarioKeys("settings:\n  mask: [one]\n  mask-auto: true\nsteps:\n  - echo hi\n");
    return Expect(scenario.Settings?.Mask is ["one", "two"], "mask is read as a list")
        & Expect(scenario.Settings?.MaskAuto == false, "mask-auto is read")
        & Expect(unknown.Count == 0, $"no unknown keys: {string.Join("; ", unknown)}")
        & Same("type error", "'settings.mask' must be a list, not 'abc'.", FindValueTypeError("settings:\n  mask: abc\nsteps:\n  - echo hi\n") ?? "(none)");
}

static bool RecordingHidesCommandCommentAndOutput()
{
    var scenario = ParseScenario(
        "settings:\n  mask:\n    - hunter2x\nsteps:\n  - name: log in with hunter2x\n    run: login --password hunter2x\n");
    var recorded = new List<RecordedStep>();
    var masker = SecretMasker.ForScenario(scenario, null);
    var options = new GenerateOptions { LogRunning = false, Record = recorded, Masker = masker };
    var (events, _) = GenerateAsync(scenario, 1, options, step => Task.FromResult(CommandExecution.FromPipeStream(
        "welcome, hunter2x\n", "", 0, [new PipeStreamChunk(TimeSpan.Zero, false, "welcome, hunter2x\n")]))).GetAwaiter().GetResult();

    var shown = string.Concat(events.Select(static ev => ev.Kind == CastEventKind.Marker ? $"[{ev.Data}]" : ev.HasUtf8Output ? Encoding.UTF8.GetString(ev.Utf8Output.Span) : ev.Data));
    using var stream = new MemoryStream();
    new StepOutputs { Scenetake = "test", Os = "linux", Shell = "bash", Width = 80, Height = 24, Steps = recorded }.Write(stream);
    var outputsJson = Encoding.UTF8.GetString(stream.ToArray());
    // The outputs file keeps the step's run text as the YAML has it: that is how a replay finds the step.
    return Expect(!shown.Contains("hunter2x", StringComparison.Ordinal), $"the cast shows the secret: {Show(shown)}")
        & Expect(shown.Contains("[log in with ********]", StringComparison.Ordinal), $"the marker is masked: {Show(shown)}")
        & Expect(shown.Contains("welcome, ********", StringComparison.Ordinal), $"the output is masked: {Show(shown)}")
        & Expect(!outputsJson.Contains("welcome, hunter2x", StringComparison.Ordinal) && outputsJson.Contains("welcome, ********", StringComparison.Ordinal),
            $"the outputs file holds the masked output: {outputsJson}");
}
