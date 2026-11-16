using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

internal enum RecordedStepKind
{
    Pipe,
    Pty,
    Interactive,
}

/// <summary>
/// One step's output in an outputs file: for a pipe step its stdout and stderr chunks, for a PTY step its bytes
/// after <see cref="ScenarioCast.RemovePtyLeadingInit"/>, and for an interactive step its output as timed in the
/// cast. Times are exact, so a replay composes the cast the run wrote.
/// </summary>
internal sealed class RecordedStep
{
    internal required string Run { get; init; }
    internal required RecordedStepKind Kind { get; init; }
    internal int ExitCode { get; init; }

    /// <summary>A non-interactive PTY step on Windows: the 1-based cursor that ConPTY started at.</summary>
    internal (int Row, int Column)? Cursor { get; init; }

    internal IReadOnlyList<PipeStreamChunk> PipeChunks { get; init; } = [];
    internal IReadOnlyList<TimedBytes> PtyChunks { get; init; } = [];
    internal ComposedPtyOutput? Composed { get; init; }

    internal static RecordedStep Pipe(string run, int exitCode, IReadOnlyList<PipeStreamChunk> chunks) =>
        new() { Run = run, Kind = RecordedStepKind.Pipe, ExitCode = exitCode, PipeChunks = chunks };

    internal static RecordedStep Pty(string run, int exitCode, (int Row, int Column)? cursor, IReadOnlyList<TimedBytes> chunks) =>
        new() { Run = run, Kind = RecordedStepKind.Pty, ExitCode = exitCode, Cursor = cursor, PtyChunks = chunks };

    internal static RecordedStep Interactive(string run, int exitCode, ComposedPtyOutput output) =>
        new() { Run = run, Kind = RecordedStepKind.Interactive, ExitCode = exitCode, Composed = output };

    internal static string KindName(RecordedStepKind kind) => kind switch
    {
        RecordedStepKind.Pty => "pty",
        RecordedStepKind.Interactive => "interactive",
        _ => "pipe",
    };
}

/// <summary>
/// The outputs file (<c>samples/&lt;name&gt;.outputs.json</c>): what each step printed during the run that wrote the
/// cast. The CLI writes it when <c>SCENETAKE_OUTPUTS</c> names a file; the playground replays it. It is committed
/// and shared like the cast, so OSC text that the cast leaves out is masked (<see cref="OscMasker"/>), and an
/// interactive step keeps no typed keys.
/// </summary>
internal sealed class StepOutputs
{
    internal const int FormatVersion = 1;
    internal const string EnvironmentVariable = "SCENETAKE_OUTPUTS";

    internal required string Scenetake { get; init; }
    internal required string Os { get; init; }
    internal required string Shell { get; init; }
    internal int Width { get; init; }
    internal int Height { get; init; }
    internal required IReadOnlyList<RecordedStep> Steps { get; init; }

    internal static string CurrentOs =>
        OperatingSystem.IsWindows() ? "windows" : OperatingSystem.IsMacOS() ? "macos" : "linux";

    internal void WriteFile(string path)
    {
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        Write(stream);
    }

    internal void Write(Stream stream)
    {
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
        {
            Indented = true,
            NewLine = "\n",
            // Command output is mostly ASCII, but a sample may print Japanese or emoji, which stay readable.
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        });
        writer.WriteStartObject();
        writer.WriteNumber("version", FormatVersion);
        writer.WriteString("scenetake", Scenetake);
        writer.WriteString("os", Os);
        writer.WriteString("shell", Shell);
        writer.WriteNumber("width", Width);
        writer.WriteNumber("height", Height);
        writer.WriteStartArray("steps");
        foreach (var step in Steps)
            WriteStep(writer, step);
        writer.WriteEndArray();
        writer.WriteEndObject();
        writer.Flush();
        stream.WriteByte((byte)'\n');
    }

    private static void WriteStep(Utf8JsonWriter writer, RecordedStep step)
    {
        writer.WriteStartObject();
        writer.WriteString("run", step.Run);
        writer.WriteString("kind", RecordedStep.KindName(step.Kind));
        writer.WriteNumber("exitCode", step.ExitCode);
        if (step.Cursor is { } cursor)
        {
            writer.WriteStartObject("cursor");
            writer.WriteNumber("row", cursor.Row);
            writer.WriteNumber("column", cursor.Column);
            writer.WriteEndObject();
        }

        switch (step.Kind)
        {
            case RecordedStepKind.Pipe:
            {
                // stdout and stderr are separate streams, so each has its own OSC state.
                var stdoutMask = new OscMasker();
                var stderrMask = new OscMasker();
                writer.WriteStartArray("chunks");
                foreach (var chunk in step.PipeChunks)
                {
                    writer.WriteStartObject();
                    writer.WriteNumber("ticks", chunk.Time.Ticks);
                    if (chunk.IsStderr)
                        writer.WriteString("err", stderrMask.Mask(chunk.Text));
                    else
                        writer.WriteString("out", stdoutMask.Mask(chunk.Text));
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
                break;
            }

            case RecordedStepKind.Pty:
            {
                var mask = new OscMasker();
                writer.WriteStartArray("chunks");
                foreach (var chunk in step.PtyChunks)
                {
                    writer.WriteStartObject();
                    writer.WriteNumber("ticks", chunk.Time.Ticks);
                    writer.WriteBase64String("data", mask.Mask(chunk.Data.Span));
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
                break;
            }

            case RecordedStepKind.Interactive:
            {
                var mask = new OscMasker();
                var composed = step.Composed ?? new ComposedPtyOutput([], 0);
                writer.WriteNumber("end", composed.End);
                writer.WriteStartArray("chunks");
                foreach (var (time, data) in composed.Chunks)
                {
                    writer.WriteStartObject();
                    writer.WriteNumber("time", time);
                    writer.WriteBase64String("data", mask.Mask(data.Span));
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
                break;
            }
        }

        writer.WriteEndObject();
    }

    /// <summary>Reads an outputs file. Throws <see cref="FormatException"/> when it is not one.</summary>
    internal static StepOutputs Parse(string json)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new FormatException($"outputs file is not JSON: {ex.Message}", ex);
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw new FormatException("outputs file must be a JSON object");
            var version = RequireInt(root, "version");
            if (version != FormatVersion)
                throw new FormatException($"outputs file version {version} is not supported (expected {FormatVersion})");

            var steps = new List<RecordedStep>();
            if (!root.TryGetProperty("steps", out var stepsElement) || stepsElement.ValueKind != JsonValueKind.Array)
                throw new FormatException("outputs file has no steps array");
            foreach (var element in stepsElement.EnumerateArray())
                steps.Add(ParseStep(element));

            return new StepOutputs
            {
                Scenetake = OptionalString(root, "scenetake") ?? "",
                Os = OptionalString(root, "os") ?? "",
                Shell = OptionalString(root, "shell") ?? "",
                Width = RequireInt(root, "width"),
                Height = RequireInt(root, "height"),
                Steps = steps,
            };
        }
    }

    private static RecordedStep ParseStep(JsonElement element)
    {
        var run = OptionalString(element, "run") ?? throw new FormatException("an outputs file step has no run");
        var kind = OptionalString(element, "kind") switch
        {
            "pipe" => RecordedStepKind.Pipe,
            "pty" => RecordedStepKind.Pty,
            "interactive" => RecordedStepKind.Interactive,
            var other => throw new FormatException($"an outputs file step has an unknown kind '{other}'"),
        };
        var exitCode = element.TryGetProperty("exitCode", out var exitElement) ? exitElement.GetInt32() : 0;
        (int Row, int Column)? cursor = element.TryGetProperty("cursor", out var cursorElement)
            ? (RequireInt(cursorElement, "row"), RequireInt(cursorElement, "column"))
            : null;
        var chunks = element.TryGetProperty("chunks", out var chunksElement) && chunksElement.ValueKind == JsonValueKind.Array
            ? chunksElement
            : default;

        switch (kind)
        {
            case RecordedStepKind.Pipe:
            {
                var pipeChunks = new List<PipeStreamChunk>();
                if (chunks.ValueKind == JsonValueKind.Array)
                {
                    foreach (var chunk in chunks.EnumerateArray())
                    {
                        var time = TimeSpan.FromTicks(chunk.GetProperty("ticks").GetInt64());
                        if (chunk.TryGetProperty("err", out var err))
                            pipeChunks.Add(new PipeStreamChunk(time, true, err.GetString() ?? ""));
                        else
                            pipeChunks.Add(new PipeStreamChunk(time, false, OptionalString(chunk, "out") ?? ""));
                    }
                }

                return RecordedStep.Pipe(run, exitCode, pipeChunks);
            }

            case RecordedStepKind.Pty:
            {
                var ptyChunks = new List<TimedBytes>();
                if (chunks.ValueKind == JsonValueKind.Array)
                {
                    foreach (var chunk in chunks.EnumerateArray())
                        ptyChunks.Add(new TimedBytes(TimeSpan.FromTicks(chunk.GetProperty("ticks").GetInt64()), chunk.GetProperty("data").GetBytesFromBase64()));
                }

                return RecordedStep.Pty(run, exitCode, cursor, ptyChunks);
            }

            default:
            {
                var output = new List<(double Time, ReadOnlyMemory<byte> Data)>();
                if (chunks.ValueKind == JsonValueKind.Array)
                {
                    foreach (var chunk in chunks.EnumerateArray())
                        output.Add((chunk.GetProperty("time").GetDouble(), chunk.GetProperty("data").GetBytesFromBase64()));
                }

                var end = element.TryGetProperty("end", out var endElement) ? endElement.GetDouble() : 0;
                return RecordedStep.Interactive(run, exitCode, new ComposedPtyOutput(output, end));
            }
        }
    }

    private static int RequireInt(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)
            ? number
            : throw new FormatException($"outputs file has no integer '{name}'");

    private static string? OptionalString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}

/// <summary>
/// Masks the text of the OSC sequences that <see cref="CastOscFilter"/> leaves out of a cast: window titles,
/// working folders, hyperlink targets, and every other OSC that is not a color change. Each masked byte or char
/// becomes <c>x</c>, and the introducer, code, and terminator stay. Every chunk therefore keeps its length and
/// the bytes the filter decides on, and the filter drops the same text from a replay as from the run, where a
/// chunk that held only an OSC became an empty event and was removed.
/// </summary>
/// <remarks>The scan follows <see cref="CastOscFilter"/>'s states, and carries them from one chunk to the next.</remarks>
internal sealed class OscMasker
{
    private enum State
    {
        Ground,
        Escape,
        Code,
        Keep,
        KeepEscape,
        Drop,
        DropEscape,
    }

    private const char MaskChar = 'x';

    private State _state;
    private int _code;
    private int _digits;

    internal byte[] Mask(ReadOnlySpan<byte> input)
    {
        var output = input.ToArray();
        for (var i = 0; i < output.Length; i++)
        {
            if (IsMasked(output[i]))
                output[i] = (byte)MaskChar;
        }

        return output;
    }

    internal string Mask(string input)
    {
        if (_state == State.Ground && input.IndexOf('\u001b') < 0)
            return input;

        var output = input.ToCharArray();
        for (var i = 0; i < output.Length; i++)
        {
            if (IsMasked(output[i]))
                output[i] = MaskChar;
        }

        return new string(output);
    }

    /// <summary>Advances the scan over one byte or char; true when it is text of an OSC that the cast leaves out.</summary>
    private bool IsMasked(int c)
    {
        while (true)
        {
            switch (_state)
            {
                case State.Ground:
                    if (c == 0x1B)
                        _state = State.Escape;
                    return false;

                case State.Escape:
                    if (c == ']')
                    {
                        _state = State.Code;
                        _code = 0;
                        _digits = 0;
                        return false;
                    }

                    _state = State.Ground;
                    continue;

                case State.Code:
                    if (c is >= '0' and <= '9' && _digits < 6)
                    {
                        _code = _code * 10 + (c - '0');
                        _digits++;
                        return false;
                    }

                    _state = _digits > 0 && CastOscFilter.IsColorCode(_code) ? State.Keep : State.Drop;
                    continue;

                case State.Keep:
                    if (c == 0x1B)
                        _state = State.KeepEscape;
                    else if (c is 0x07 or 0x18 or 0x1A)
                        _state = State.Ground;
                    return false;

                case State.KeepEscape:
                    if (c == '\\')
                    {
                        _state = State.Ground;
                        return false;
                    }

                    _state = State.Escape;
                    continue;

                case State.Drop:
                    if (c == 0x1B)
                    {
                        _state = State.DropEscape;
                        return false;
                    }

                    if (c is 0x07 or 0x18 or 0x1A)
                    {
                        _state = State.Ground;
                        return false;
                    }

                    return true;

                default: // DropEscape
                    if (c == '\\')
                    {
                        _state = State.Ground;
                        return false;
                    }

                    _state = State.Escape;
                    continue;
            }
        }
    }
}

/// <summary>A step of a replay: whether the outputs file had output for it, and what the reader should know.</summary>
internal sealed record ReplayedStep(int Index, string Run, string Kind, bool Recorded, IReadOnlyList<string> Notices);

/// <summary>
/// Gives each step the output that the outputs file recorded for it, for <see cref="ScenarioCast.GenerateAsync"/>.
/// A step matches a recorded step with the same run text and kind; the n-th such step takes the n-th recorded one,
/// and steps after the last take the last. A step without a match gets <see cref="NotRecordedLine"/>.
/// </summary>
internal sealed class StepOutputsReplay
{
    internal const string NotRecordedLine = "\u001b[2m# no recorded output in the playground\u001b[0m\n";

    private readonly StepOutputs _outputs;
    private readonly Dictionary<(string Run, RecordedStepKind Kind), List<RecordedStep>> _recorded = [];
    private readonly Dictionary<(string Run, RecordedStepKind Kind), int> _taken = [];

    internal StepOutputsReplay(StepOutputs outputs)
    {
        _outputs = outputs;
        foreach (var step in outputs.Steps)
        {
            var key = (step.Run, step.Kind);
            if (!_recorded.TryGetValue(key, out var list))
                _recorded[key] = list = [];
            list.Add(step);
        }
    }

    /// <summary>Whether a PTY step gets the recording's cursor, as ConPTY did when the outputs file was recorded.</summary>
    internal bool TrackPtyCursor => _outputs.Os == "windows";

    internal List<ReplayedStep> Steps { get; } = [];

    internal Task<CommandExecution> RunAsync(StepStart step) => Task.FromResult(Run(step));

    internal CommandExecution Run(StepStart step)
    {
        var kind = step.UseInteractive ? RecordedStepKind.Interactive : step.UsePty ? RecordedStepKind.Pty : RecordedStepKind.Pipe;
        var key = (step.Command.Cmd, kind);
        if (!_recorded.TryGetValue(key, out var recorded))
        {
            Steps.Add(new ReplayedStep(step.Index, step.Command.Cmd, RecordedStep.KindName(kind), false, []));
            return new CommandExecution(0, false, "", "", null, null, []) { Placeholder = NotRecordedLine };
        }

        var taken = _taken.GetValueOrDefault(key);
        _taken[key] = taken + 1;
        var source = recorded[Math.Min(taken, recorded.Count - 1)];

        var notices = new List<string>();
        if (kind != RecordedStepKind.Pipe && (step.Width != _outputs.Width || step.Height != _outputs.Height))
            notices.Add($"recorded in a {_outputs.Width}x{_outputs.Height} terminal; this one is {step.Width}x{step.Height}, so the output may not fit");
        if (kind == RecordedStepKind.Pty && source.Cursor is { } recordedCursor && step.PtyStartCursor is { } cursor && cursor != recordedCursor)
            notices.Add($"recorded with the cursor at row {recordedCursor.Row}, column {recordedCursor.Column}; here it starts at row {cursor.Row}, column {cursor.Column}, so the output may draw on other rows");
        Steps.Add(new ReplayedStep(step.Index, step.Command.Cmd, RecordedStep.KindName(kind), true, notices));

        return kind switch
        {
            RecordedStepKind.Pipe => new CommandExecution(source.ExitCode, false, "", "", null, null, source.PipeChunks),
            RecordedStepKind.Pty => new CommandExecution(source.ExitCode, true, "", "", source.PtyChunks, null, null) { PtyLeadingInitRemoved = true },
            _ => new CommandExecution(source.ExitCode, true, "", "", null, null, null) { ComposedOutput = source.Composed },
        };
    }
}
