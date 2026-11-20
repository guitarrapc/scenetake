using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Scenetake;

/// <summary>
/// The kernel32 functions scenetake calls on Windows. The library is loaded with <see cref="NativeLibrary"/> and
/// each function is called through an unmanaged function pointer, so nothing is marshaled: strings and buffers
/// are passed as the UTF-16 <c>char*</c> and the <c>BOOL</c> results as <c>int</c> that Windows uses. The
/// library and the exports are looked up on first use, which only Windows code paths reach.
/// </summary>
internal static unsafe class Kernel32
{
    private static readonly nint Library = NativeLibrary.Load("kernel32.dll");

    private static readonly delegate* unmanaged<char*, uint, uint, nint, uint, uint, nint, nint> CreateFileWExport =
        (delegate* unmanaged<char*, uint, uint, nint, uint, uint, nint, nint>)Export("CreateFileW");

    private static readonly delegate* unmanaged<nint, WindowsFileInformation*, int> GetFileInformationByHandleExport =
        (delegate* unmanaged<nint, WindowsFileInformation*, int>)Export("GetFileInformationByHandle");

    private static readonly delegate* unmanaged<nint, char*, uint, uint, uint> GetFinalPathNameByHandleWExport =
        (delegate* unmanaged<nint, char*, uint, uint, uint>)Export("GetFinalPathNameByHandleW");

    private static readonly delegate* unmanaged<uint> GetOEMCPExport =
        (delegate* unmanaged<uint>)Export("GetOEMCP");

    private static readonly delegate* unmanaged<uint, byte, int> IsDBCSLeadByteExExport =
        (delegate* unmanaged<uint, byte, int>)Export("IsDBCSLeadByteEx");

    private static readonly delegate* unmanaged<uint, uint, byte*, int, char*, int, int> MultiByteToWideCharExport =
        (delegate* unmanaged<uint, uint, byte*, int, char*, int, int>)Export("MultiByteToWideChar");

    private static readonly delegate* unmanaged<nint, char*, nint> CreateJobObjectWExport =
        (delegate* unmanaged<nint, char*, nint>)Export("CreateJobObjectW");

    private static readonly delegate* unmanaged<nint, int, void*, uint, int> SetInformationJobObjectExport =
        (delegate* unmanaged<nint, int, void*, uint, int>)Export("SetInformationJobObject");

    private static readonly delegate* unmanaged<nint, nint, int> AssignProcessToJobObjectExport =
        (delegate* unmanaged<nint, nint, int>)Export("AssignProcessToJobObject");

    private static readonly delegate* unmanaged<nint, uint, int> TerminateJobObjectExport =
        (delegate* unmanaged<nint, uint, int>)Export("TerminateJobObject");

    private static readonly delegate* unmanaged<nint, int> CloseHandleExport =
        (delegate* unmanaged<nint, int>)Export("CloseHandle");

    private static nint Export(string name) => NativeLibrary.GetExport(Library, name);

    // JobObjectExtendedLimitInformation, and the limit that ends every process in the job when its last handle closes.
    private const int JobObjectExtendedLimitInformation = 9;
    private const uint JobObjectLimitKillOnJobClose = 0x2000;

    /// <summary>A job object that ends its processes when its handle closes, or 0 when one cannot be made.</summary>
    internal static nint CreateKillOnCloseJob()
    {
        var job = CreateJobObjectWExport(0, null);
        if (job == 0)
            return 0;

        var limits = new JobObjectExtendedLimits();
        limits.Basic.LimitFlags = JobObjectLimitKillOnJobClose;
        if (SetInformationJobObjectExport(job, JobObjectExtendedLimitInformation, &limits, (uint)sizeof(JobObjectExtendedLimits)) == 0)
        {
            CloseHandleExport(job);
            return 0;
        }

        return job;
    }

    internal static bool AssignProcessToJobObject(nint job, nint process) => AssignProcessToJobObjectExport(job, process) != 0;

    internal static bool TerminateJobObject(nint job, uint exitCode) => TerminateJobObjectExport(job, exitCode) != 0;

    internal static void CloseHandle(nint handle) => CloseHandleExport(handle);

    /// <summary>JOBOBJECT_EXTENDED_LIMIT_INFORMATION. SIZE_T and ULONG_PTR are pointer-sized, so nint gives their alignment.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct JobObjectExtendedLimits
    {
        public JobObjectBasicLimits Basic;
        public ulong ReadOperationCount, WriteOperationCount, OtherOperationCount, ReadTransferCount, WriteTransferCount, OtherTransferCount;
        public nint ProcessMemoryLimit, JobMemoryLimit, PeakProcessMemoryUsed, PeakJobMemoryUsed;
    }

    /// <summary>JOBOBJECT_BASIC_LIMIT_INFORMATION.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct JobObjectBasicLimits
    {
        public long PerProcessUserTimeLimit, PerJobUserTimeLimit;
        public uint LimitFlags;
        public nint MinimumWorkingSetSize, MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public nint Affinity;
        public uint PriorityClass, SchedulingClass;
    }

    /// <summary>Opens <paramref name="fileName"/>; the handle is invalid when it cannot be opened.</summary>
    internal static SafeFileHandle CreateFile(string fileName, uint desiredAccess, uint shareMode, uint creationDisposition, uint flagsAndAttributes)
    {
        // A .NET string is null-terminated in memory, as CreateFileW needs.
        fixed (char* name = fileName)
            return new SafeFileHandle(CreateFileWExport(name, desiredAccess, shareMode, 0, creationDisposition, flagsAndAttributes, 0), ownsHandle: true);
    }

    internal static bool GetFileInformationByHandle(SafeFileHandle file, out WindowsFileInformation information)
    {
        WindowsFileInformation result = default;
        var added = false;
        try
        {
            // The reference keeps the handle from being closed while Windows uses it.
            file.DangerousAddRef(ref added);
            var ok = GetFileInformationByHandleExport(file.DangerousGetHandle(), &result) != 0;
            information = result;
            return ok;
        }
        finally
        {
            if (added)
                file.DangerousRelease();
        }
    }

    /// <summary>
    /// Writes the final path of <paramref name="file"/> into <paramref name="path"/> and returns its length, or the
    /// length the buffer needs (terminating null included) when it does not fit, or 0 on failure.
    /// </summary>
    internal static uint GetFinalPathNameByHandle(SafeFileHandle file, Span<char> path, uint flags)
    {
        var added = false;
        try
        {
            file.DangerousAddRef(ref added);
            fixed (char* chars = path)
                return GetFinalPathNameByHandleWExport(file.DangerousGetHandle(), chars, (uint)path.Length, flags);
        }
        finally
        {
            if (added)
                file.DangerousRelease();
        }
    }

    /// <summary>The code page a new console starts with.</summary>
    internal static uint GetOEMCP() => GetOEMCPExport();

    internal static bool IsDBCSLeadByteEx(uint codePage, byte testChar) => IsDBCSLeadByteExExport(codePage, testChar) != 0;

    /// <summary>Converts <paramref name="bytes"/> in <paramref name="codePage"/> to UTF-16 and returns the chars written.</summary>
    internal static int MultiByteToWideChar(uint codePage, ReadOnlySpan<byte> bytes, Span<char> chars)
    {
        fixed (byte* source = bytes)
        fixed (char* destination = chars)
            return MultiByteToWideCharExport(codePage, 0, source, bytes.Length, destination, chars.Length);
    }
}

/// <summary>
/// The processes of one command on Windows, held in a job object, so that ending the job ends them all. Ending the
/// process tree misses a process whose parent has exited: Git Bash's fork emulation leaves one when the shell runs a
/// command in a child, as it does with an EXIT trap set, and that process kept the pipes open until it ended by
/// itself. A process started inside the job stays in it. The job ends its processes when it is disposed, and when
/// scenetake exits, even by Ctrl+C. Elsewhere, and when Windows refuses a job, <see cref="TryCreate"/> gives null and
/// the caller ends the process tree as before.
/// </summary>
internal sealed class ProcessJob : IDisposable
{
    private nint _handle;

    private ProcessJob(nint handle) => _handle = handle;

    /// <summary>A job that holds <paramref name="process"/> and what it starts, or null.</summary>
    internal static ProcessJob? TryCreate(System.Diagnostics.Process process)
    {
        if (!OperatingSystem.IsWindows())
            return null;

        var handle = Kernel32.CreateKillOnCloseJob();
        if (handle == 0)
            return null;

        // The command has just started: a shell reads its login files before it starts anything.
        if (!Kernel32.AssignProcessToJobObject(handle, process.SafeHandle.DangerousGetHandle()))
        {
            Kernel32.CloseHandle(handle);
            return null;
        }

        return new ProcessJob(handle);
    }

    /// <summary>Ends every process in the job.</summary>
    internal void Terminate()
    {
        if (_handle != 0)
            Kernel32.TerminateJobObject(_handle, 1);
    }

    public void Dispose()
    {
        var handle = Interlocked.Exchange(ref _handle, 0);
        if (handle != 0)
            Kernel32.CloseHandle(handle);
    }
}

/// <summary>The BY_HANDLE_FILE_INFORMATION that GetFileInformationByHandle fills. Each FILETIME is two 32-bit halves, so the 64-bit fields are packed to 4 bytes.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
internal struct WindowsFileInformation
{
    public uint FileAttributes;
    public long CreationTime;
    public long LastAccessTime;
    public long LastWriteTime;
    public uint VolumeSerialNumber;
    public uint FileSizeHigh;
    public uint FileSizeLow;
    public uint NumberOfLinks;
    public uint FileIndexHigh;
    public uint FileIndexLow;
}
