using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

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

    private static nint Export(string name) => NativeLibrary.GetExport(Library, name);

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
