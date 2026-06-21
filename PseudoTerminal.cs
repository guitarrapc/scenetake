using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

readonly record struct CommandOutput(string Stdout, string Stderr, int ExitCode, bool IsTerminalOutput = false, List<CommandOutputChunk>? TerminalChunks = null)
{
    public List<CommandOutputChunk> Chunks => TerminalChunks ?? [];
}

readonly record struct CommandOutputChunk(double Time, string Data);

readonly record struct PtyLaunchContext(string Shell, int Columns, int Rows, string? Cwd);

static class PtyDiagnostics
{
    public static void Fail(string step, Exception exception, PtyLaunchContext context)
    {
        var message = exception switch
        {
            Win32Exception win32 => $"{win32.Message} ({FormatOsError(win32.NativeErrorCode)})",
            _ => exception.Message,
        };
        Console.Error.WriteLine($"scenetake: pty error: {step} failed: {message}");
        Console.Error.WriteLine($"scenetake: pty context: shell={context.Shell} cols={context.Columns} rows={context.Rows}");
        if (!string.IsNullOrWhiteSpace(context.Cwd))
            Console.Error.WriteLine($"scenetake: pty context: cwd={context.Cwd}");
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

static class PseudoTerminal
{
    public static CommandOutput Run(
        string fileName,
        string[] arguments,
        string? cwd,
        int width,
        int height,
        PtyLaunchContext context,
        string? input = null,
        bool verbose = false)
    {
        width = Math.Clamp(width, 1, 512);
        height = Math.Clamp(height, 1, 512);
        context = context with { Columns = width, Rows = height };

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return WindowsPseudoTerminal.Run(fileName, arguments, cwd, width, height, context, input, verbose);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ||
            RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ||
            RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD))
            return UnixPseudoTerminal.Run(fileName, arguments, cwd, width, height, context, input, verbose);

        throw new PlatformNotSupportedException("PTY recording is not supported on this operating system.");
    }
}

static partial class WindowsPseudoTerminal
{
    private const int PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE = 0x00020016;
    private const uint EXTENDED_STARTUPINFO_PRESENT = 0x00080000;
    private const uint STARTF_USESTDHANDLES = 0x00000100;
    private static readonly IntPtr InvalidHandleValue = new(-1);
    private const uint INFINITE = 0xFFFFFFFF;

    public static CommandOutput Run(
        string fileName,
        string[] arguments,
        string? cwd,
        int width,
        int height,
        PtyLaunchContext context,
        string? input,
        bool verbose)
    {
        CreateConPtyPipes(out var inputRead, out var inputWrite, out var outputRead, out var outputWrite);
        using var inputWriteHandle = inputWrite;
        using var outputReadHandle = outputRead;

        var size = new COORD((short)width, (short)height);
        var hr = CreatePseudoConsole(size, inputRead, outputWrite, 0, out var hpc);
        if (hr != 0)
        {
            var error = new Win32Exception(hr, "CreatePseudoConsole failed");
            PtyDiagnostics.Fail("CreatePseudoConsole", error, context);
            throw error;
        }

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

            if (!UpdateProcThreadAttribute(attrList, 0, (IntPtr)PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE, hpc, (IntPtr)IntPtr.Size, IntPtr.Zero, IntPtr.Zero))
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
            var outputTask = Task.Run(() => ReadChunks(outputReadHandle, stopwatch));
            if (!string.IsNullOrEmpty(input))
                WriteAll(inputWriteHandle, Encoding.UTF8.GetBytes(input));

            WaitForSingleObject(processInfo.hProcess, INFINITE);
            inputWriteHandle.Dispose();
            if (!GetExitCodeProcess(processInfo.hProcess, out var exitCode))
                throw new Win32Exception(Marshal.GetLastPInvokeError(), "GetExitCodeProcess failed");

            ClosePseudoConsole(hpc);
            hpc = IntPtr.Zero;
            var chunks = outputTask.GetAwaiter().GetResult();
            var totalChars = chunks.Sum(static x => x.Data.Length);
            if (verbose)
                PtyDiagnostics.VerboseLog(context, fileName, arguments, processInfo.dwProcessId, chunks.Count, totalChars);

            return new CommandOutput(string.Concat(chunks.Select(static x => x.Data)), "", unchecked((int)exitCode), true, chunks);
        }
        finally
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
            if (hpc != IntPtr.Zero)
                ClosePseudoConsole(hpc);
        }
    }

    private static void WriteAll(SafeFileHandle handle, byte[] bytes)
    {
        using var stream = new FileStream(handle, FileAccess.Write, 4096, false);
        stream.Write(bytes, 0, bytes.Length);
        stream.Flush();
    }

    private static List<CommandOutputChunk> ReadChunks(SafeFileHandle handle, Stopwatch stopwatch)
    {
        using var stream = new FileStream(handle, FileAccess.Read, 4096, false);
        return TerminalChunkReader.Read(stream, stopwatch);
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
    private const int ShutWrite = 1;

    public static CommandOutput Run(
        string fileName,
        string[] arguments,
        string? cwd,
        int width,
        int height,
        PtyLaunchContext context,
        string? input,
        bool verbose)
    {
        var winsize = new Winsize { ws_col = (ushort)width, ws_row = (ushort)height };
        if (openpty(out var master, out var slave, IntPtr.Zero, IntPtr.Zero, ref winsize) != 0)
        {
            var error = new Win32Exception(Marshal.GetLastPInvokeError(), "openpty failed");
            PtyDiagnostics.Fail("openpty", error, context);
            throw error;
        }

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
        {
            close(master);
            setsid();
            ioctl(slave, TIOCSCTTY, 0);
            dup2(slave, 0);
            dup2(slave, 1);
            dup2(slave, 2);
            if (slave > 2)
                close(slave);
            if (!string.IsNullOrWhiteSpace(cwd))
                chdir(cwd);
            ExecvpOrExit(fileName, arguments);
        }

        close(slave);
        var stopwatch = Stopwatch.StartNew();
        var outputTask = Task.Run(() => ReadChunks(master, stopwatch));
        if (!string.IsNullOrEmpty(input))
            WriteAll(master, Encoding.UTF8.GetBytes(input));
        else
            shutdown(master, ShutWrite);

        waitpid(pid, out var status, 0);
        var output = outputTask.GetAwaiter().GetResult();
        close(master);
        var exitCode = WIFEXITED(status) ? WEXITSTATUS(status) : 1;
        var totalChars = output.Sum(static x => x.Data.Length);
        if (verbose)
            PtyDiagnostics.VerboseLog(context, fileName, arguments, pid, output.Count, totalChars);

        return new CommandOutput(string.Concat(output.Select(static x => x.Data)), "", exitCode, true, output);
    }

    private static unsafe void ExecvpOrExit(string fileName, string[] arguments)
    {
        var owned = new List<IntPtr>();
        try
        {
            var argv = AllocUtf8Argv(fileName, arguments, owned);
            execvp(argv[0], argv);
        }
        finally
        {
            FreeUtf8Allocations(owned);
        }

        _exit(127);
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

    private static List<CommandOutputChunk> ReadChunks(int fd, Stopwatch stopwatch)
    {
        using var handle = new SafeFileHandle((IntPtr)fd, ownsHandle: false);
        using var stream = new FileStream(handle, FileAccess.Read, 4096, false);
        return TerminalChunkReader.Read(stream, stopwatch);
    }

    private static bool WIFEXITED(int status) => (status & 0x7f) == 0;
    private static int WEXITSTATUS(int status) => (status >> 8) & 0xff;

    private const ulong TIOCSCTTY = 0x540E;

    [LibraryImport("libc", SetLastError = true)]
    private static partial int openpty(out int amaster, out int aslave, IntPtr name, IntPtr termp, ref Winsize winp);

    [LibraryImport("libc", SetLastError = true)]
    private static partial int fork();

    [LibraryImport("libc", SetLastError = true)]
    private static partial int setsid();

    [LibraryImport("libc", SetLastError = true)]
    private static partial int ioctl(int fd, ulong request, int arg);

    [LibraryImport("libc", SetLastError = true)]
    private static partial int dup2(int oldfd, int newfd);

    [LibraryImport("libc", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    private static partial int chdir(string path);

    [LibraryImport("libc", SetLastError = true)]
    private static unsafe partial int execvp(byte* file, byte** argv);

    [LibraryImport("libc", SetLastError = true)]
    private static partial int close(int fd);

    [LibraryImport("libc", SetLastError = true)]
    private static partial int shutdown(int fd, int how);

    [LibraryImport("libc", SetLastError = true)]
    private static partial int waitpid(int pid, out int status, int options);

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
    public static List<CommandOutputChunk> Read(Stream stream, Stopwatch stopwatch, Action<long>? onChunk = null)
    {
        var chunks = new List<CommandOutputChunk>();
        var bytes = new byte[4096];
        var chars = new char[Console.OutputEncoding.GetMaxCharCount(bytes.Length)];
        var decoder = Console.OutputEncoding.GetDecoder();

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
