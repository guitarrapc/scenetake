using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

public readonly record struct CommandOutput(string Stdout, string Stderr, int ExitCode, bool IsTerminalOutput = false, List<CommandOutputChunk>? TerminalChunks = null)
{
    public List<CommandOutputChunk> Chunks => TerminalChunks ?? [];
}

public readonly record struct CommandOutputChunk(double Time, string Data);

public readonly record struct PtyLaunchContext(string Shell, int Columns, int Rows, string? Cwd);

public sealed record PtyCaptureOptions
{
    public Encoding OutputEncoding { get; init; } = Encoding.UTF8;
}

static class PtyDiagnostics
{
    public static void Fail(string step, Exception exception, PtyLaunchContext context)
    {
        var message = exception switch
        {
            Win32Exception win32 => $"{win32.Message} ({FormatOsError(win32.NativeErrorCode)})",
            ExternalException external when external.ErrorCode != 0
                => $"{external.Message} (HRESULT 0x{unchecked((uint)external.ErrorCode):X8})",
            _ => exception.Message,
        };
        Console.Error.WriteLine($"scenetake: pty error: {step} failed: {message}");
        Console.Error.WriteLine($"scenetake: pty context: shell={context.Shell} cols={context.Columns} rows={context.Rows}");
        if (!string.IsNullOrWhiteSpace(context.Cwd))
            Console.Error.WriteLine($"scenetake: pty context: cwd={context.Cwd}");
    }

    public static void ThrowForHResult(string step, int hr, PtyLaunchContext context)
    {
        Console.Error.WriteLine($"scenetake: pty error: {step} failed: HRESULT 0x{unchecked((uint)hr):X8}");
        Console.Error.WriteLine($"scenetake: pty context: shell={context.Shell} cols={context.Columns} rows={context.Rows}");
        if (!string.IsNullOrWhiteSpace(context.Cwd))
            Console.Error.WriteLine($"scenetake: pty context: cwd={context.Cwd}");
        Marshal.ThrowExceptionForHR(hr);
    }

    public static void VerboseLog(
        PtyLaunchContext context,
        string fileName,
        string[] arguments,
        int processId,
        int chunkCount,
        int totalChars)
    {
        Console.Error.WriteLine($"scenetake: pty verbose: shell={context.Shell} cols={context.Columns} rows={context.Rows}");
        if (!string.IsNullOrWhiteSpace(context.Cwd))
            Console.Error.WriteLine($"scenetake: pty verbose: cwd={context.Cwd}");
        Console.Error.WriteLine($"scenetake: pty verbose: executable={fileName}");
        Console.Error.WriteLine($"scenetake: pty verbose: arguments={string.Join(' ', arguments)}");
        Console.Error.WriteLine($"scenetake: pty verbose: pid={processId} chunks={chunkCount} chars={totalChars}");
    }

    private static string FormatOsError(int code) =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? $"Win32 {code}"
            : $"errno {code}";
}

/// <summary>
/// A running pseudo-terminal child session. Dispose kills the child if it is still running,
/// then releases ConPTY / pipe / process handles.
/// </summary>
public sealed class PseudoTerminalSession : IAsyncDisposable, IDisposable
{
    private readonly IPtySessionBackend _backend;
    private bool _disposed;

    internal PseudoTerminalSession(IPtySessionBackend backend) => _backend = backend;

    public int ProcessId => _backend.ProcessId;

    public bool HasExited => _backend.HasExited;

    public void WriteInput(string input) => _backend.WriteInput(input);

    /// <summary>Signals that no further stdin bytes will be sent.</summary>
    /// <remarks>
    /// Windows: closes the ConPTY input pipe write end (true EOF to the child).
    /// Unix: writes EOT (0x04, Ctrl-D) to the PTY master — PTY fds are not sockets and cannot be half-closed with <c>shutdown(SHUT_WR)</c>.
    /// </remarks>
    public void CloseInput() => _backend.CloseInput();

    public void Kill() => _backend.Kill();

    public Task<int> WaitForExitAsync(CancellationToken cancellationToken = default) =>
        _backend.WaitForExitAsync(cancellationToken);

    public CommandOutput Complete(bool verbose = false) => _backend.Complete(verbose);

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _backend.Dispose();
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}

interface IPtySessionBackend : IDisposable
{
    int ProcessId { get; }
    bool HasExited { get; }
    void WriteInput(string input);
    void CloseInput();
    void Kill();
    Task<int> WaitForExitAsync(CancellationToken cancellationToken);
    CommandOutput Complete(bool verbose);
}

public static class PseudoTerminal
{
    public static PseudoTerminalSession Start(
        string fileName,
        string[] arguments,
        string? cwd,
        int width,
        int height,
        PtyLaunchContext context,
        PtyCaptureOptions? options = null)
    {
        width = Math.Clamp(width, 1, 512);
        height = Math.Clamp(height, 1, 512);
        context = context with { Columns = width, Rows = height };
        options ??= new PtyCaptureOptions();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return new PseudoTerminalSession(WindowsPseudoTerminal.Start(fileName, arguments, cwd, width, height, context, options));

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ||
            RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ||
            RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD))
            return new PseudoTerminalSession(UnixPseudoTerminal.Start(fileName, arguments, cwd, width, height, context, options));

        throw new PlatformNotSupportedException("PTY recording is not supported on this operating system.");
    }

    public static CommandOutput Run(
        string fileName,
        string[] arguments,
        string? cwd,
        int width,
        int height,
        PtyLaunchContext context,
        string? input = null,
        bool verbose = false,
        CancellationToken cancellationToken = default,
        PtyCaptureOptions? options = null)
    {
        using var session = Start(fileName, arguments, cwd, width, height, context, options);
        if (input is not null)
        {
            if (input.Length > 0)
                session.WriteInput(input);
            session.CloseInput();
        }
        session.WaitForExitAsync(cancellationToken).GetAwaiter().GetResult();
        return session.Complete(verbose);
    }
}

static partial class WindowsPseudoTerminal
{
    private const int PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE = 0x00020016;
    private const uint EXTENDED_STARTUPINFO_PRESENT = 0x00080000;
    private const uint STARTF_USESTDHANDLES = 0x00000100;
    private static readonly IntPtr InvalidHandleValue = new(-1);
    private const uint WaitObject0 = 0;
    private const uint WaitTimeout = 0x00000102;
    private const uint WaitFailed = 0xFFFFFFFF;
    private const uint WaitPollMs = 100;

    public static IPtySessionBackend Start(
        string fileName,
        string[] arguments,
        string? cwd,
        int width,
        int height,
        PtyLaunchContext context,
        PtyCaptureOptions options)
    {
        CreateConPtyPipes(out var inputRead, out var inputWrite, out var outputRead, out var outputWrite);
        var inputWriteHandle = inputWrite;
        var outputReadHandle = outputRead;

        var size = new COORD((short)width, (short)height);
        var hr = CreatePseudoConsole(size, inputRead, outputWrite, 0, out var pseudoConsoleHandle);
        if (hr < 0)
            PtyDiagnostics.ThrowForHResult("CreatePseudoConsole", hr, context);

        var hpc = new SafePseudoConsoleHandle(pseudoConsoleHandle);
        inputRead.Dispose();
        outputWrite.Dispose();

        var attrList = IntPtr.Zero;
        var processInfo = new PROCESS_INFORMATION();
        try
        {
            nuint attrListSize = 0;
            _ = InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref attrListSize);
            if (attrListSize == 0)
                throw new Win32Exception(Marshal.GetLastPInvokeError(), "InitializeProcThreadAttributeList size query failed");

            attrList = Marshal.AllocHGlobal((IntPtr)(nint)attrListSize);
            if (!InitializeProcThreadAttributeList(attrList, 1, 0, ref attrListSize))
                throw new Win32Exception(Marshal.GetLastPInvokeError(), "InitializeProcThreadAttributeList failed");

            if (!UpdateProcThreadAttribute(attrList, 0, (IntPtr)PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE, hpc.DangerousGetHandle(), (IntPtr)IntPtr.Size, IntPtr.Zero, IntPtr.Zero))
                throw new Win32Exception(Marshal.GetLastPInvokeError(), "UpdateProcThreadAttribute failed");

            var startupInfo = new STARTUPINFOEX
            {
                StartupInfo =
                {
                    cb = Marshal.SizeOf<STARTUPINFOEX>(),
                    dwFlags = (int)STARTF_USESTDHANDLES,
                    hStdInput = InvalidHandleValue,
                    hStdOutput = InvalidHandleValue,
                    hStdError = InvalidHandleValue,
                },
                lpAttributeList = attrList,
            };

            var commandLineText = arguments.Length == 0
                ? QuoteArg(fileName)
                : QuoteArg(fileName) + " " + string.Join(" ", arguments.Select(QuoteArg));
            var commandLine = (commandLineText + '\0').ToCharArray();
            if (!CreateProcessW(
                    null,
                    commandLine,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    false,
                    EXTENDED_STARTUPINFO_PRESENT,
                    IntPtr.Zero,
                    string.IsNullOrWhiteSpace(cwd) ? null : cwd,
                    ref startupInfo,
                    out processInfo))
            {
                var error = new Win32Exception(Marshal.GetLastPInvokeError(), "CreateProcess failed");
                PtyDiagnostics.Fail("CreateProcess", error, context);
                throw error;
            }

            var stopwatch = Stopwatch.StartNew();
            var outputEncoding = options.OutputEncoding;
            var outputTask = Task.Run(() => ReadChunks(outputReadHandle, stopwatch, outputEncoding));
            return new WindowsPtySession(
                inputWriteHandle,
                outputReadHandle,
                hpc,
                attrList,
                processInfo,
                outputTask,
                stopwatch,
                fileName,
                arguments,
                context);
        }
        catch
        {
            if (processInfo.hThread != IntPtr.Zero)
                CloseHandle(processInfo.hThread);
            if (processInfo.hProcess != IntPtr.Zero)
                CloseHandle(processInfo.hProcess);
            if (attrList != IntPtr.Zero)
            {
                DeleteProcThreadAttributeList(attrList);
                Marshal.FreeHGlobal(attrList);
            }
            hpc.Dispose();
            inputWriteHandle.Dispose();
            outputReadHandle.Dispose();
            throw;
        }
    }

    private sealed class WindowsPtySession : IPtySessionBackend
    {
        private readonly SafeFileHandle _inputWriteHandle;
        private readonly SafeFileHandle _outputReadHandle;
        private readonly SafePseudoConsoleHandle _hpc;
        private readonly IntPtr _attrList;
        private readonly PROCESS_INFORMATION _processInfo;
        private readonly Task<List<CommandOutputChunk>> _outputTask;
        private readonly Stopwatch _stopwatch;
        private readonly string _fileName;
        private readonly string[] _arguments;
        private readonly PtyLaunchContext _context;
        private bool _inputClosed;
        private bool _hpcClosed;
        private bool _exited;
        private int _exitCode;
        private bool _disposed;

        public WindowsPtySession(
            SafeFileHandle inputWriteHandle,
            SafeFileHandle outputReadHandle,
            SafePseudoConsoleHandle hpc,
            IntPtr attrList,
            PROCESS_INFORMATION processInfo,
            Task<List<CommandOutputChunk>> outputTask,
            Stopwatch stopwatch,
            string fileName,
            string[] arguments,
            PtyLaunchContext context)
        {
            _inputWriteHandle = inputWriteHandle;
            _outputReadHandle = outputReadHandle;
            _hpc = hpc;
            _attrList = attrList;
            _processInfo = processInfo;
            _outputTask = outputTask;
            _stopwatch = stopwatch;
            _fileName = fileName;
            _arguments = arguments;
            _context = context;
        }

        public int ProcessId => _processInfo.dwProcessId;

        public bool HasExited => _exited;

        public void WriteInput(string input)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_exited || _inputClosed || input.Length == 0)
                return;

            WriteAll(_inputWriteHandle, Encoding.UTF8.GetBytes(input));
        }

        public void CloseInput()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_exited || _inputClosed)
                return;

            // ConPTY may not have wired child stdin yet right after CreateProcess.
            Thread.Sleep(100);
            FlushFileBuffers(_inputWriteHandle);
            _inputWriteHandle.Dispose();
            _inputClosed = true;
        }

        public void Kill()
        {
            if (_disposed || _exited || _processInfo.hProcess == IntPtr.Zero)
                return;

            TerminateProcess(_processInfo.hProcess, 1);
        }

        public async Task<int> WaitForExitAsync(CancellationToken cancellationToken)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_exited)
                return _exitCode;

            using var registration = cancellationToken.Register(static state =>
            {
                var session = (WindowsPtySession)state!;
                session.Kill();
            }, this);

            while (!_exited)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var waitResult = WaitForSingleObject(_processInfo.hProcess, WaitPollMs);
                if (waitResult == WaitObject0)
                {
                    if (!GetExitCodeProcess(_processInfo.hProcess, out var exitCode))
                        throw new Win32Exception(Marshal.GetLastPInvokeError(), "GetExitCodeProcess failed");

                    _exitCode = unchecked((int)exitCode);
                    _exited = true;
                    CloseTransport();
                    break;
                }

                if (waitResult == WaitTimeout)
                {
                    await Task.Yield();
                    continue;
                }

                if (waitResult == WaitFailed)
                    throw new Win32Exception(Marshal.GetLastPInvokeError(), "WaitForSingleObject failed");

                throw new InvalidOperationException($"WaitForSingleObject returned unexpected code 0x{waitResult:X8}");
            }

            cancellationToken.ThrowIfCancellationRequested();
            return _exitCode;
        }

        public CommandOutput Complete(bool verbose)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_exited)
                throw new InvalidOperationException("The PTY child process has not exited yet.");

            var chunks = _outputTask.GetAwaiter().GetResult();
            var totalChars = chunks.Sum(static x => x.Data.Length);
            if (verbose)
                PtyDiagnostics.VerboseLog(_context, _fileName, _arguments, _processInfo.dwProcessId, chunks.Count, totalChars);

            return new CommandOutput(string.Concat(chunks.Select(static x => x.Data)), "", _exitCode, true, chunks);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            if (!_exited)
                Kill();

            CloseTransport();
            DrainOutputTask();

            if (_processInfo.hThread != IntPtr.Zero)
                CloseHandle(_processInfo.hThread);
            if (_processInfo.hProcess != IntPtr.Zero)
                CloseHandle(_processInfo.hProcess);
            if (_attrList != IntPtr.Zero)
            {
                DeleteProcThreadAttributeList(_attrList);
                Marshal.FreeHGlobal(_attrList);
            }

            _inputWriteHandle.Dispose();
            _outputReadHandle.Dispose();
        }

        private void CloseTransport()
        {
            if (!_inputClosed)
            {
                _inputWriteHandle.Dispose();
                _inputClosed = true;
            }

            if (!_hpcClosed)
            {
                _hpc.Dispose();
                _hpcClosed = true;
            }
        }

        private void DrainOutputTask()
        {
            try
            {
                _ = _outputTask.GetAwaiter().GetResult();
            }
            catch
            {
            }
        }
    }

    private static unsafe void WriteAll(SafeFileHandle handle, byte[] bytes)
    {
        fixed (byte* ptr = bytes)
        {
            var offset = 0;
            while (offset < bytes.Length)
            {
                var remaining = (uint)(bytes.Length - offset);
                if (!WriteFile(handle, ptr + offset, remaining, out var written, IntPtr.Zero))
                    throw new Win32Exception(Marshal.GetLastPInvokeError(), "WriteFile failed");
                if (written == 0)
                    throw new IOException("WriteFile wrote 0 bytes");
                offset += (int)written;
            }
        }
    }

    private static List<CommandOutputChunk> ReadChunks(SafeFileHandle handle, Stopwatch stopwatch, Encoding outputEncoding)
    {
        using var stream = new FileStream(handle, FileAccess.Read, 4096, false);
        return TerminalChunkReader.Read(stream, stopwatch, outputEncoding);
    }

    private const uint HANDLE_FLAG_INHERIT = 0x00000001;

    private static void CreateConPtyPipes(
        out SafeFileHandle inputRead,
        out SafeFileHandle inputWrite,
        out SafeFileHandle outputRead,
        out SafeFileHandle outputWrite)
    {
        var securityAttributes = new SECURITY_ATTRIBUTES
        {
            nLength = Marshal.SizeOf<SECURITY_ATTRIBUTES>(),
            bInheritHandle = true,
        };
        var attrPtr = Marshal.AllocHGlobal(securityAttributes.nLength);
        try
        {
            Marshal.StructureToPtr(securityAttributes, attrPtr, false);
            if (!CreatePipe(out inputRead, out inputWrite, attrPtr, 0))
                throw new Win32Exception(Marshal.GetLastPInvokeError(), "CreatePipe failed");
            if (!CreatePipe(out outputRead, out outputWrite, attrPtr, 0))
                throw new Win32Exception(Marshal.GetLastPInvokeError(), "CreatePipe failed");
        }
        finally
        {
            Marshal.FreeHGlobal(attrPtr);
        }
        if (!SetHandleInformation(inputWrite, HANDLE_FLAG_INHERIT, 0))
            throw new Win32Exception(Marshal.GetLastPInvokeError(), "SetHandleInformation failed");
        if (!SetHandleInformation(outputRead, HANDLE_FLAG_INHERIT, 0))
            throw new Win32Exception(Marshal.GetLastPInvokeError(), "SetHandleInformation failed");
    }

    private static string QuoteArg(string arg)
    {
        if (arg.Length == 0)
            return "\"\"";
        if (!arg.Any(static c => char.IsWhiteSpace(c) || c is '"' or '\\'))
            return arg;

        var sb = new StringBuilder(arg.Length + 2);
        sb.Append('"');
        var backslashes = 0;
        foreach (var c in arg)
        {
            if (c == '\\')
            {
                backslashes++;
                continue;
            }

            if (c == '"')
            {
                sb.Append('\\', backslashes * 2 + 1);
                sb.Append('"');
                backslashes = 0;
                continue;
            }

            sb.Append('\\', backslashes);
            backslashes = 0;
            sb.Append(c);
        }
        sb.Append('\\', backslashes * 2);
        sb.Append('"');
        return sb.ToString();
    }

    private sealed class SafePseudoConsoleHandle : SafeHandle
    {
        public SafePseudoConsoleHandle(IntPtr value) : base(IntPtr.Zero, ownsHandle: true) => SetHandle(value);

        public override bool IsInvalid => handle == IntPtr.Zero;

        protected override bool ReleaseHandle()
        {
            ClosePseudoConsole(handle);
            return true;
        }
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CreatePipe(out SafeFileHandle hReadPipe, out SafeFileHandle hWritePipe, IntPtr lpPipeAttributes, uint nSize);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetHandleInformation(SafeFileHandle hObject, uint dwMask, uint dwFlags);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial int CreatePseudoConsole(COORD size, SafeFileHandle hInput, SafeFileHandle hOutput, uint dwFlags, out IntPtr phPC);

    [LibraryImport("kernel32.dll")]
    private static partial void ClosePseudoConsole(IntPtr hPC);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool InitializeProcThreadAttributeList(IntPtr lpAttributeList, int dwAttributeCount, int dwFlags, ref nuint lpSize);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool UpdateProcThreadAttribute(IntPtr lpAttributeList, uint dwFlags, IntPtr attribute, IntPtr lpValue, IntPtr cbSize, IntPtr lpPreviousValue, IntPtr lpReturnSize);

    [LibraryImport("kernel32.dll")]
    private static partial void DeleteProcThreadAttributeList(IntPtr lpAttributeList);

    [LibraryImport("kernel32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CreateProcessW(
        string? lpApplicationName,
        char[] lpCommandLine,
        IntPtr lpProcessAttributes,
        IntPtr lpThreadAttributes,
        [MarshalAs(UnmanagedType.Bool)] bool bInheritHandles,
        uint dwCreationFlags,
        IntPtr lpEnvironment,
        string? lpCurrentDirectory,
        ref STARTUPINFOEX lpStartupInfo,
        out PROCESS_INFORMATION lpProcessInformation);

    [LibraryImport("kernel32.dll")]
    private static partial uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetExitCodeProcess(IntPtr hProcess, out uint lpExitCode);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool TerminateProcess(IntPtr hProcess, uint uExitCode);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool FlushFileBuffers(SafeFileHandle hFile);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static unsafe partial bool WriteFile(SafeFileHandle hFile, byte* lpBuffer, uint nNumberOfBytesToWrite, out uint lpNumberOfBytesWritten, IntPtr lpOverlapped);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseHandle(IntPtr hObject);

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct COORD(short x, short y)
    {
        public readonly short X = x;
        public readonly short Y = y;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct STARTUPINFO
    {
        public int cb;
        public IntPtr lpReserved;
        public IntPtr lpDesktop;
        public IntPtr lpTitle;
        public int dwX;
        public int dwY;
        public int dwXSize;
        public int dwYSize;
        public int dwXCountChars;
        public int dwYCountChars;
        public int dwFillAttribute;
        public int dwFlags;
        public short wShowWindow;
        public short cbReserved2;
        public IntPtr lpReserved2;
        public IntPtr hStdInput;
        public IntPtr hStdOutput;
        public IntPtr hStdError;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct STARTUPINFOEX
    {
        public STARTUPINFO StartupInfo;
        public IntPtr lpAttributeList;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_INFORMATION
    {
        public IntPtr hProcess;
        public IntPtr hThread;
        public int dwProcessId;
        public int dwThreadId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SECURITY_ATTRIBUTES
    {
        public int nLength;
        public IntPtr lpSecurityDescriptor;
        [MarshalAs(UnmanagedType.Bool)]
        public bool bInheritHandle;
    }
}

static partial class UnixPseudoTerminal
{
    private const byte InputEot = 0x04;
    private const int WaitNoHang = 1;
    private const int SigKill = 9;
    private const int WaitPollMs = 100;
    private const int EINTR = 4;

    public static IPtySessionBackend Start(
        string fileName,
        string[] arguments,
        string? cwd,
        int width,
        int height,
        PtyLaunchContext context,
        PtyCaptureOptions options)
    {
        var winsize = new Winsize { ws_col = (ushort)width, ws_row = (ushort)height };
        if (OpenPty(out var master, out var slave, ref winsize) != 0)
        {
            var error = new Win32Exception(Marshal.GetLastPInvokeError(), "openpty failed");
            PtyDiagnostics.Fail("openpty", error, context);
            throw error;
        }

        var tiocSetCtty = TiocSetCtty();
        var exec = UnixExecPayload.Create(fileName, arguments, cwd);
        try
        {
            var pid = fork();
            if (pid < 0)
            {
                close(master);
                close(slave);
                var error = new Win32Exception(Marshal.GetLastPInvokeError(), "fork failed");
                PtyDiagnostics.Fail("fork", error, context);
                throw error;
            }

            if (pid == 0)
                ChildMainAfterFork(master, slave, tiocSetCtty, exec.Executable, exec.Argv, exec.WorkingDirectory);

            close(slave);
            var stopwatch = Stopwatch.StartNew();
            var outputEncoding = options.OutputEncoding;
            var outputTask = Task.Run(() => ReadChunks(master, stopwatch, outputEncoding));
            return new UnixPtySession(master, pid, outputTask, stopwatch, fileName, arguments, context);
        }
        finally
        {
            exec.Dispose();
        }
    }

    /// <summary>
    /// Child path after <c>fork()</c>. Only async-signal-safe libc calls — no managed allocation or runtime APIs.
    /// </summary>
    private static unsafe void ChildMainAfterFork(
        int master,
        int slave,
        ulong tiocSetCtty,
        IntPtr executable,
        IntPtr argv,
        IntPtr workingDirectory)
    {
        close(master);
        setsid();
        ioctl(slave, tiocSetCtty, 0);
        dup2(slave, 0);
        dup2(slave, 1);
        dup2(slave, 2);
        if (slave > 2)
            close(slave);
        if (workingDirectory != IntPtr.Zero)
            chdir((byte*)workingDirectory);
        execvp((byte*)executable, (byte**)argv);
        _exit(127);
    }

    private sealed class UnixExecPayload : IDisposable
    {
        private readonly List<IntPtr> _owned;

        private UnixExecPayload(List<IntPtr> owned, IntPtr executable, IntPtr argv, IntPtr workingDirectory)
        {
            _owned = owned;
            Executable = executable;
            Argv = argv;
            WorkingDirectory = workingDirectory;
        }

        public IntPtr Executable { get; }
        public IntPtr Argv { get; }
        public IntPtr WorkingDirectory { get; }

        public static unsafe UnixExecPayload Create(string fileName, string[] arguments, string? cwd)
        {
            var owned = new List<IntPtr>();
            try
            {
                var executable = AllocUtf8CString(fileName, owned);
                var argv = AllocUtf8Argv(fileName, arguments, owned);
                IntPtr workingDirectory = IntPtr.Zero;
                if (!string.IsNullOrWhiteSpace(cwd))
                    workingDirectory = (IntPtr)AllocUtf8CString(cwd, owned);
                return new UnixExecPayload(owned, (IntPtr)executable, (IntPtr)argv, workingDirectory);
            }
            catch
            {
                FreeUtf8Allocations(owned);
                throw;
            }
        }

        public void Dispose() => FreeUtf8Allocations(_owned);
    }

    private sealed class UnixPtySession : IPtySessionBackend
    {
        private readonly int _master;
        private readonly int _pid;
        private readonly Task<List<CommandOutputChunk>> _outputTask;
        private readonly Stopwatch _stopwatch;
        private readonly string _fileName;
        private readonly string[] _arguments;
        private readonly PtyLaunchContext _context;
        private bool _inputEofSignaled;
        private bool _exited;
        private int _exitCode;
        private bool _disposed;

        public UnixPtySession(
            int master,
            int pid,
            Task<List<CommandOutputChunk>> outputTask,
            Stopwatch stopwatch,
            string fileName,
            string[] arguments,
            PtyLaunchContext context)
        {
            _master = master;
            _pid = pid;
            _outputTask = outputTask;
            _stopwatch = stopwatch;
            _fileName = fileName;
            _arguments = arguments;
            _context = context;
        }

        public int ProcessId => _pid;

        public bool HasExited => _exited;

        public void WriteInput(string input)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_exited || _inputEofSignaled || input.Length == 0)
                return;

            WriteAll(_master, Encoding.UTF8.GetBytes(input));
        }

        public void CloseInput()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            SignalInputEof();
        }

        public void Kill()
        {
            if (_disposed || _exited)
                return;

            kill(_pid, SigKill);
        }

        public async Task<int> WaitForExitAsync(CancellationToken cancellationToken)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_exited)
                return _exitCode;

            using var registration = cancellationToken.Register(static state =>
            {
                var session = (UnixPtySession)state!;
                session.Kill();
            }, this);

            while (!_exited)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!TryWaitPid(_pid, WaitNoHang, out var status, out var result))
                    throw new Win32Exception(Marshal.GetLastPInvokeError(), "waitpid failed");

                if (result == _pid)
                {
                    _exitCode = MapWaitStatusToExitCode(status);
                    _exited = true;
                    break;
                }

                await Task.Delay(WaitPollMs, cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
            return _exitCode;
        }

        public CommandOutput Complete(bool verbose)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_exited)
                throw new InvalidOperationException("The PTY child process has not exited yet.");

            var output = _outputTask.GetAwaiter().GetResult();
            var totalChars = output.Sum(static x => x.Data.Length);
            if (verbose)
                PtyDiagnostics.VerboseLog(_context, _fileName, _arguments, _pid, output.Count, totalChars);

            return new CommandOutput(string.Concat(output.Select(static x => x.Data)), "", _exitCode, true, output);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            if (!_exited)
                Kill();

            DrainOutputTask();
            close(_master);
        }

        private void SignalInputEof()
        {
            if (_inputEofSignaled || _exited)
                return;

            WriteAll(_master, [InputEot]);
            _inputEofSignaled = true;
        }

        private void DrainOutputTask()
        {
            try
            {
                _ = _outputTask.GetAwaiter().GetResult();
            }
            catch
            {
            }
        }
    }

    private static unsafe byte** AllocUtf8Argv(string fileName, string[] arguments, List<IntPtr> owned)
    {
        var argc = arguments.Length + 1;
        var argv = (byte**)NativeMemory.Alloc((nuint)(argc + 1) * (nuint)IntPtr.Size);
        owned.Add((IntPtr)argv);

        argv[0] = AllocUtf8CString(fileName, owned);
        for (var i = 0; i < arguments.Length; i++)
            argv[i + 1] = AllocUtf8CString(arguments[i], owned);
        argv[argc] = null;
        return argv;
    }

    private static unsafe byte* AllocUtf8CString(string value, List<IntPtr> owned)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        var ptr = (byte*)NativeMemory.Alloc((nuint)bytes.Length + 1);
        owned.Add((IntPtr)ptr);
        bytes.AsSpan().CopyTo(new Span<byte>(ptr, bytes.Length));
        ptr[bytes.Length] = 0;
        return ptr;
    }

    private static unsafe void FreeUtf8Allocations(List<IntPtr> owned)
    {
        foreach (var ptr in owned)
            NativeMemory.Free((void*)ptr);
        owned.Clear();
    }

    private static void WriteAll(int fd, byte[] bytes)
    {
        var offset = 0;
        while (offset < bytes.Length)
        {
            var buffer = offset == 0 ? bytes : bytes[offset..];
            var written = write(fd, buffer, (nuint)buffer.Length);
            if (written <= 0)
                throw new Win32Exception(Marshal.GetLastPInvokeError(), "write failed");
            offset += written;
        }
    }

    private static List<CommandOutputChunk> ReadChunks(int fd, Stopwatch stopwatch, Encoding outputEncoding)
    {
        using var handle = new SafeFileHandle((IntPtr)fd, ownsHandle: false);
        using var stream = new FileStream(handle, FileAccess.Read, 4096, false);
        return TerminalChunkReader.Read(stream, stopwatch, outputEncoding);
    }

    private static bool WIFEXITED(int status) => (status & 0x7f) == 0;
    private static int WEXITSTATUS(int status) => (status >> 8) & 0xff;
    private static bool WIFSIGNALED(int status) => (((status & 0x7f) + 1) >> 1) > 0;
    private static int WTERMSIG(int status) => status & 0x7f;

    private static int MapWaitStatusToExitCode(int status)
    {
        if (WIFEXITED(status))
            return WEXITSTATUS(status);
        if (WIFSIGNALED(status))
            return 128 + WTERMSIG(status);
        return 1;
    }

    private static bool TryWaitPid(int pid, int options, out int status, out int result)
    {
        while (true)
        {
            result = waitpid(pid, out status, options);
            if (result >= 0)
                return true;
            if (Marshal.GetLastPInvokeError() == EINTR)
                continue;
            return false;
        }
    }

    private static int OpenPty(out int master, out int slave, ref Winsize winsize)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return LinuxOpenPty(out master, out slave, IntPtr.Zero, IntPtr.Zero, ref winsize);
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return MacOSOpenPty(out master, out slave, IntPtr.Zero, IntPtr.Zero, ref winsize);
        if (RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD))
            return FreeBSDOpenPty(out master, out slave, IntPtr.Zero, IntPtr.Zero, ref winsize);

        throw new PlatformNotSupportedException("PTY recording is not supported on this Unix operating system.");
    }

    private static ulong TiocSetCtty()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return Linux.TIOCSCTTY;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return MacOS.TIOCSCTTY;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD))
            return FreeBSD.TIOCSCTTY;

        throw new PlatformNotSupportedException("PTY recording is not supported on this Unix operating system.");
    }

    private static class Linux
    {
        internal const ulong TIOCSCTTY = 0x540E;
    }

    private static class MacOS
    {
        internal const ulong TIOCSCTTY = 0x20007461;
    }

    private static class FreeBSD
    {
        internal const ulong TIOCSCTTY = 0x20007461;
    }

    [LibraryImport("libc", SetLastError = true)]
    private static partial int LinuxOpenPty(out int amaster, out int aslave, IntPtr name, IntPtr termp, ref Winsize winp);

    [LibraryImport("libutil", SetLastError = true)]
    private static partial int MacOSOpenPty(out int amaster, out int aslave, IntPtr name, IntPtr termp, ref Winsize winp);

    [LibraryImport("libutil", SetLastError = true)]
    private static partial int FreeBSDOpenPty(out int amaster, out int aslave, IntPtr name, IntPtr termp, ref Winsize winp);

    [LibraryImport("libc", SetLastError = true)]
    private static partial int fork();

    [LibraryImport("libc", SetLastError = true)]
    private static partial int setsid();

    [LibraryImport("libc", SetLastError = true)]
    private static partial int ioctl(int fd, ulong request, int arg);

    [LibraryImport("libc", SetLastError = true)]
    private static partial int dup2(int oldfd, int newfd);

    [LibraryImport("libc", SetLastError = true)]
    private static unsafe partial int chdir(byte* path);

    [LibraryImport("libc", SetLastError = true)]
    private static unsafe partial int execvp(byte* file, byte** argv);

    [LibraryImport("libc", SetLastError = true)]
    private static partial int close(int fd);

    [LibraryImport("libc", SetLastError = true)]
    private static partial int waitpid(int pid, out int status, int options);

    [LibraryImport("libc", SetLastError = true)]
    private static partial int kill(int pid, int sig);

    [LibraryImport("libc", SetLastError = true)]
    private static partial int write(int fd, byte[] buf, nuint count);

    [LibraryImport("libc")]
    private static partial void _exit(int status);

    [StructLayout(LayoutKind.Sequential)]
    private struct Winsize
    {
        public ushort ws_row;
        public ushort ws_col;
        public ushort ws_xpixel;
        public ushort ws_ypixel;
    }
}

static class TerminalChunkReader
{
    public static List<CommandOutputChunk> Read(Stream stream, Stopwatch stopwatch, Encoding outputEncoding, Action<long>? onChunk = null)
    {
        var chunks = new List<CommandOutputChunk>();
        var bytes = new byte[4096];
        var chars = new char[outputEncoding.GetMaxCharCount(bytes.Length)];
        var decoder = outputEncoding.GetDecoder();

        while (true)
        {
            var read = stream.Read(bytes, 0, bytes.Length);
            if (read <= 0)
                break;

            var charCount = decoder.GetChars(bytes, 0, read, chars, 0, flush: false);
            if (charCount > 0)
            {
                onChunk?.Invoke(stopwatch.ElapsedMilliseconds);
                chunks.Add(new CommandOutputChunk(stopwatch.Elapsed.TotalSeconds, new string(chars, 0, charCount)));
            }
        }

        var trailing = decoder.GetChars(Array.Empty<byte>(), 0, 0, chars, 0, flush: true);
        if (trailing > 0)
        {
            onChunk?.Invoke(stopwatch.ElapsedMilliseconds);
            chunks.Add(new CommandOutputChunk(stopwatch.Elapsed.TotalSeconds, new string(chars, 0, trailing)));
        }

        return chunks;
    }
}
