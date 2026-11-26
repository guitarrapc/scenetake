using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Scenetake;

/// <summary>
/// The commands of <c>background: true</c> steps (spec_scenario.md → Background steps). Each starts with the shell
/// state so far, scenetake waits until its output shows the step's <c>wait</c> text, and it keeps running while the
/// next steps record, such as a server that a later step calls, until <see cref="StopAll"/> ends it before post.
/// Nothing of it is recorded.
/// </summary>
internal sealed class BackgroundSteps : IDisposable
{
    private readonly List<Running> _running = [];
    private readonly ConsoleCancelEventHandler _onCancel;

    internal BackgroundSteps()
    {
        // Ctrl+C ends scenetake. On Windows a background command has a console of its own, which gets no Ctrl+C, so it
        // would outlive scenetake.
        _onCancel = (_, _) => StopAll(quiet: true);
        Console.CancelKeyPress += _onCancel;
    }

    /// <summary>
    /// Starts <paramref name="psi"/>, and waits until its output shows <paramref name="waitFor"/>, for at most
    /// <paramref name="waitLimit"/> seconds. The result has <see cref="CommandExecution.BackgroundProblem"/> set when the
    /// command exits before the text, or the text does not come in time; the command keeps running in the latter case
    /// until <see cref="StopAll"/>.
    /// </summary>
    internal async Task<CommandExecution> StartAsync(int number, string cmd, bool continueOnError, ProcessStartInfo psi, string? waitFor, double waitLimit)
    {
        psi.RedirectStandardInput = true;
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;
        var process = new Process { StartInfo = psi };
        process.Start();
        // On Windows the job holds every process the command starts, so the stop ends them all (ProcessJob).
        var job = ProcessJob.TryCreate(process);
        // A background command reads no input: the terminal belongs to the steps.
        process.StandardInput.Close();

        var running = new Running(number, cmd, continueOnError, process, job);
        lock (_running)
            _running.Add(running);

        if (waitFor is null)
            return CommandExecution.FromPipe("", "", 0);

        var stopwatch = Stopwatch.StartNew();
        var found = running.WaitAsync(waitFor);
        var exited = process.WaitForExitAsync();
        var first = await Task.WhenAny(found, exited, Task.Delay(Delays.Of(waitLimit)));
        if (first == exited)
        {
            // The text may have come just before the exit: what the command printed is read to its end first.
            await running.DrainAsync(TimeSpan.FromSeconds(2));
            first = found.IsCompleted ? found : exited;
        }

        if (first == found)
        {
            running.StopWaiting();
            Console.Error.WriteLine($"  ready after {stopwatch.Elapsed.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture)} s: \"{waitFor}\"");
            return CommandExecution.FromPipe("", "", 0);
        }

        running.StopWaiting();
        var problem = first == exited
            ? $"exited {process.ExitCode} before it printed \"{waitFor}\""
            : $"did not print \"{waitFor}\" within {waitLimit.ToString("0.###", CultureInfo.InvariantCulture)} s";
        return CommandExecution.FromPipe(running.Tail(), "", first == exited ? process.ExitCode : 1) with { BackgroundProblem = problem };
    }

    /// <summary>
    /// Ends every background command that still runs, with its process tree, as a pipe step's <c>timeout</c> does. One
    /// that already ended with a non-zero exit code gets a warning, unless its step sets <c>continue-on-error</c>.
    /// </summary>
    internal void StopAll(bool quiet = false)
    {
        Running[] running;
        lock (_running)
        {
            running = [.. _running];
            _running.Clear();
        }

        foreach (var item in running)
            item.Stop(quiet);
    }

    public void Dispose()
    {
        Console.CancelKeyPress -= _onCancel;
        StopAll();
    }

    /// <summary>One background command, with its output read as it comes, so a full pipe never stops it.</summary>
    private sealed class Running
    {
        // The most output kept for the error message of a step that did not get ready.
        private const int MaxTail = 16384;

        private readonly int _number;
        private readonly string _cmd;
        private readonly bool _continueOnError;
        private readonly Process _process;
        private readonly ProcessJob? _job;
        // One watcher per stream: the bytes of a character never come half from stdout and half from stderr.
        private readonly PtyOutputWatcher _stdout = new();
        private readonly PtyOutputWatcher _stderr = new();
        private readonly StringBuilder _tail = new();
        private readonly Task _pumps;

        internal Running(int number, string cmd, bool continueOnError, Process process, ProcessJob? job)
        {
            _number = number;
            _cmd = cmd;
            _continueOnError = continueOnError;
            _process = process;
            _job = job;
            _pumps = Task.WhenAll(
                PumpAsync(process.StandardOutput.BaseStream, _stdout),
                PumpAsync(process.StandardError.BaseStream, _stderr));
        }

        /// <summary>Completes when either stream shows <paramref name="text"/>.</summary>
        internal Task WaitAsync(string text) => Task.WhenAny(_stdout.WaitAsync(text), _stderr.WaitAsync(text));

        internal void StopWaiting()
        {
            _stdout.StopWaiting();
            _stderr.StopWaiting();
        }

        internal Task DrainAsync(TimeSpan limit) => Task.WhenAny(_pumps, Task.Delay(limit));

        /// <summary>The last output, both streams in the order it came, for <see cref="ScenarioCast.FailureOutputTail"/>.</summary>
        internal string Tail()
        {
            lock (_tail)
                return _tail.ToString();
        }

        internal void Stop(bool quiet)
        {
            try
            {
                if (!_process.HasExited)
                {
                    if (_job is not null)
                        _job.Terminate();
                    else
                        _process.Kill(entireProcessTree: true);
                    _process.WaitForExit(5000);
                    if (!quiet)
                        Console.Error.WriteLine($"  stopped (background): {_cmd}");
                }
                else if (_process.ExitCode != 0 && !_continueOnError && !quiet)
                {
                    Console.Error.WriteLine($"Warning: background step {_number} exited {_process.ExitCode} before the recording ended: {_cmd}");
                }
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                // It exited while being stopped.
            }

            // Whatever the command left running ends with the job, even when the command itself had exited.
            _job?.Dispose();
            // The pipes close when the processes end. A descendant outside the tree, on Linux or macOS, may hold them open: it is not waited for.
            _pumps.Wait(TimeSpan.FromSeconds(2));
            _process.Dispose();
        }

        private async Task PumpAsync(Stream stream, PtyOutputWatcher watcher)
        {
            // Read as a pipe step's output is (PipeStreamCapture): on Windows a command such as cmd writes the code
            // page of its console unless it writes UTF-8, so a wait for non-ASCII text would never match as UTF-8.
            // One decoder per stream serves both the wait and the tail.
            Decoder decoder = OperatingSystem.IsWindows()
                ? new PipeOutputDecoder(new WindowsCodePageDecoder(WindowsCodePageDecoder.OemCodePage()))
                : Encoding.UTF8.GetDecoder();
            var buffer = new byte[4096];
            // UTF-8 gives at most one char per byte plus one, and PipeOutputDecoder one per byte with its held-back bytes.
            var chars = new char[buffer.Length + PipeOutputDecoder.MaxPendingBytes];
            try
            {
                int read;
                while ((read = await stream.ReadAsync(buffer).ConfigureAwait(false)) > 0)
                {
                    var count = decoder.GetChars(buffer, 0, read, chars, 0, flush: false);
                    watcher.Append(chars.AsSpan(0, count));
                    lock (_tail)
                    {
                        _tail.Append(chars, 0, count);
                        if (_tail.Length > MaxTail)
                            _tail.Remove(0, _tail.Length - MaxTail / 2);
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException)
            {
                // The process was stopped while its output was read.
            }
        }
    }
}
