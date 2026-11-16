using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using StbTrueTypeSharp;

/// <summary>
/// Pixel layout of a raster frame: the SVG cell metrics rounded to whole pixels, plus the
/// SVG inner padding. With window chrome, the SVG title bar sits above the terminal.
/// <see cref="PadX"/> and <see cref="PadY"/> are the padding left of and above the cells; the frame's
/// remaining width and height are padding on the other sides.
/// Width and height are even because yuv420p video requires it.
/// </summary>
internal readonly record struct RasterLayout(
    int Cols, int Rows, int CellWidth, int CellHeight, int Baseline, int PadX, int PadY, int Width, int Height, int FontSize,
    WindowStyle Window, int TitleBarHeight, int WindowX = 0, int WindowY = 0, int WindowWidth = 0, int WindowHeight = 0, bool Desktop = false)
{
    /// <param name="padding">Pixels between the terminal's edge and its text, or null for the default from the font size.</param>
    /// <param name="margin">Pixels of background around the window when <paramref name="desktop"/> is set, or null for the default.</param>
    /// <param name="desktop">
    /// Whether a background surrounds the window. Without one the window is the whole frame, as video has no
    /// transparency to show around it, and <paramref name="margin"/> does not apply.
    /// </param>
    /// <param name="png">Whether the frame is a PNG, which names the output in the error for a frame too large.</param>
    internal static RasterLayout Create(int fontSize, int cols, int rows, WindowStyle window = WindowStyle.None, int? padding = null, int? margin = null, bool desktop = false, bool png = false)
    {
        var cellWidth = Math.Max(1, Round(fontSize * SvgFrameRenderer.CharWidthFactor));
        var cellHeight = Math.Max(1, Round(fontSize * SvgFrameRenderer.LineHeightFactor));
        var baseline = Math.Clamp(Round(fontSize * SvgFrameRenderer.BaselineFactor), 1, cellHeight);
        var (padH, padV) = SvgFrameRenderer.ResolveInnerPadding(fontSize, padding);
        var padX = Round(padH);
        var padY = Round(padV);
        var titleBar = window == WindowStyle.None ? 0 : Round(SvgFrameRenderer.ChromeTitleBarHeight);
        var windowWidth = cols * cellWidth + padX * 2;
        var windowHeight = titleBar + rows * cellHeight + padY * 2;
        if (!desktop)
        {
            var width = windowWidth + (windowWidth & 1);
            var height = windowHeight + (windowHeight & 1);
            CheckRawFrame(width, height, "use a smaller --font-size or terminal size", png);
            return new RasterLayout(cols, rows, cellWidth, cellHeight, baseline, padX, padY, width, height, fontSize, window, titleBar, 0, 0, width, height);
        }

        var outer = Round(SvgFrameRenderer.ResolveMargin(margin, hasBackground: true, styleDefault: 0));
        var frameWidth = windowWidth + outer * 2;
        var frameHeight = windowHeight + outer * 2;
        frameWidth += frameWidth & 1;
        frameHeight += frameHeight & 1;
        CheckRawFrame(frameWidth, frameHeight, "use a smaller --font-size, terminal size, or --margin", png);
        return new RasterLayout(cols, rows, cellWidth, cellHeight, baseline, padX, padY, frameWidth, frameHeight, fontSize, window, titleBar,
            outer, outer, windowWidth, windowHeight, Desktop: true);
    }

    /// <summary>The window's corner radius: the chrome's, or square for a terminal without chrome.</summary>
    internal int CornerRadius => Window switch
    {
        WindowStyle.Macos => Round(SvgFrameRenderer.ChromeMacCornerRadius),
        WindowStyle.Windows => Round(SvgFrameRenderer.ChromeWindowsCornerRadius),
        _ => 0,
    };

    /// <summary>
    /// This layout inside a <c>--size</c> frame. A terminal that fits is centered and the rest is padding
    /// in the theme background; an odd pixel goes right or below. A terminal larger than the frame gets
    /// padding to the frame's shape instead, and the encoder scales the result down to the frame size.
    /// The title bar stays at the top edge and spans the frame; the padding goes around the terminal below it.
    /// </summary>
    internal RasterLayout FitFrame(int frameWidth, int frameHeight)
    {
        // The terminal before Create rounds it to even sizes. With a background, the window and its margin: the
        // larger frame shows more background, and the window stays centered.
        long terminalWidth = Desktop ? Width : Cols * CellWidth + PadX * 2;
        long terminalHeight = Desktop ? Height : TitleBarHeight + Rows * CellHeight + PadY * 2;
        long width = frameWidth, height = frameHeight;
        if (terminalWidth > frameWidth || terminalHeight > frameHeight)
        {
            // The side with the smaller ratio frame / terminal sets the scale and keeps its size.
            if (frameWidth * terminalHeight <= frameHeight * terminalWidth)
            {
                width = terminalWidth;
                height = (terminalWidth * frameHeight + frameWidth - 1) / frameWidth;
            }
            else
            {
                height = terminalHeight;
                width = (terminalHeight * frameWidth + frameHeight - 1) / frameHeight;
            }

            width += width & 1;
            height += height & 1;
        }

        CheckRawFrame(width, height,
            $"--size {frameWidth}x{frameHeight} pads the {terminalWidth}x{terminalHeight} terminal to its shape before scaling: use a --size closer to the terminal's shape");
        if (Desktop)
        {
            return this with
            {
                WindowX = WindowX + (int)((width - terminalWidth) / 2),
                WindowY = WindowY + (int)((height - terminalHeight) / 2),
                Width = (int)width,
                Height = (int)height,
            };
        }

        return this with
        {
            PadX = PadX + (int)((width - terminalWidth) / 2),
            PadY = PadY + (int)((height - terminalHeight) / 2),
            Width = (int)width,
            Height = (int)height,
            WindowWidth = (int)width,
            WindowHeight = (int)height,
        };
    }

    /// <summary>
    /// ffmpeg rejects a raw input frame unless (width + 128) × (height + 128) is below 2^28, about
    /// 16000x16000. Large accepted sizes reach that, for example 512×512 cells from font size 37.
    /// Checking it here also keeps the RGB24 frame within one array and int arithmetic. A PNG needs no
    /// ffmpeg, but the same limit keeps its frame, about 800MB at most, within one array too.
    /// </summary>
    private static void CheckRawFrame(long width, long height, string advice, bool png = false)
    {
        if ((width + 128) * (height + 128) < 1L << 28)
            return;

        throw new FrameTooLargeException(png
            ? $"a PNG of {width}x{height} pixels is too large to draw: {advice}"
            : $"an mp4 frame of {width}x{height} pixels is too large for ffmpeg: {advice}");
    }

    internal int CellX(int col) => WindowX + PadX + col * CellWidth;

    internal int RowY(int row) => WindowY + TitleBarHeight + PadY + row * CellHeight;

    private static int Round(double value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);
}

/// <summary>An alpha mask placed relative to a cell: <see cref="OffsetX"/> from the cell's left edge, <see cref="OffsetY"/> from the baseline.</summary>
internal sealed record RasterGlyph(int Width, int Height, int OffsetX, int OffsetY, byte[] Alpha)
{
    internal static readonly RasterGlyph Empty = new(0, 0, 0, 0, []);
}

/// <summary>One face of an installed font file, read from its headers only.</summary>
internal sealed record FontFaceInfo(
    string Path, int Index, string Family, string? TypographicFamily, string? PostScriptName,
    int Weight, bool Italic, bool FixedPitch, bool Variable)
{
    internal bool Matches(string family) =>
        string.Equals(Family, family, StringComparison.OrdinalIgnoreCase)
        || string.Equals(TypographicFamily, family, StringComparison.OrdinalIgnoreCase)
        || string.Equals(PostScriptName, family, StringComparison.OrdinalIgnoreCase);
}

/// <summary>Finds installed fonts by reading only each file's headers: the table directory, the name, OS/2, and post tables, and the cmap encoding records.</summary>
internal static class FontCatalog
{
    private static List<FontFaceInfo>? s_installed;

    internal static IReadOnlyList<FontFaceInfo> Installed => s_installed ??= Scan(FontDirectories());

    /// <summary>Drops the scan, so the next use of <see cref="Installed"/> sees fonts installed since.</summary>
    internal static void Rescan() => s_installed = null;

    internal static IEnumerable<string> FontDirectories()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (OperatingSystem.IsWindows())
        {
            yield return Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
            yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "Fonts");
        }
        else if (OperatingSystem.IsMacOS())
        {
            yield return "/System/Library/Fonts";
            yield return "/Library/Fonts";
            yield return Path.Combine(home, "Library", "Fonts");
        }
        else
        {
            var dataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
            yield return Path.Combine(string.IsNullOrEmpty(dataHome) ? Path.Combine(home, ".local", "share") : dataHome, "fonts");
            yield return Path.Combine(home, ".fonts");
            yield return "/usr/local/share/fonts";
            yield return "/usr/share/fonts";
        }
    }

    internal static List<FontFaceInfo> Scan(IEnumerable<string> directories)
    {
        var faces = new List<FontFaceInfo>();
        var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true };
        foreach (var directory in directories)
        {
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
                continue;

            foreach (var path in Directory.EnumerateFiles(directory, "*", options))
            {
                var extension = Path.GetExtension(path);
                if (!extension.Equals(".ttf", StringComparison.OrdinalIgnoreCase)
                    && !extension.Equals(".otf", StringComparison.OrdinalIgnoreCase)
                    && !extension.Equals(".ttc", StringComparison.OrdinalIgnoreCase)
                    && !extension.Equals(".otc", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                try
                {
                    faces.AddRange(ReadFaces(path));
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentOutOfRangeException)
                {
                    // Files that cannot be read, or whose headers are malformed, are skipped.
                }
            }
        }

        return faces;
    }

    internal static List<FontFaceInfo> ReadFaces(string path)
    {
        using var stream = File.OpenRead(path);
        Span<byte> header = stackalloc byte[12];
        stream.ReadExactly(header);
        var faceOffsets = new List<long>();
        if (header[..4].SequenceEqual("ttcf"u8))
        {
            var count = BinaryPrimitives.ReadUInt32BigEndian(header[8..]);
            if (count is 0 or > 256)
                throw new InvalidDataException("bad font collection");
            var offsets = new byte[count * 4];
            stream.ReadExactly(offsets);
            for (var i = 0; i < count; i++)
                faceOffsets.Add(BinaryPrimitives.ReadUInt32BigEndian(offsets.AsSpan(i * 4)));
        }
        else
        {
            faceOffsets.Add(0);
        }

        var faces = new List<FontFaceInfo>(faceOffsets.Count);
        for (var index = 0; index < faceOffsets.Count; index++)
        {
            var tables = ReadTableDirectory(stream, faceOffsets[index]);
            if (!tables.TryGetValue("name", out var nameTable))
                continue;
            if (!(tables.ContainsKey("glyf") && tables.ContainsKey("loca")) && !tables.ContainsKey("CFF "))
                continue; // no outlines stb_truetype can draw (bitmap-only or CFF2)
            if (!tables.ContainsKey("head") || !tables.ContainsKey("hhea") || !tables.ContainsKey("hmtx"))
                continue; // stb_truetype cannot load the face without them
            if (!tables.TryGetValue("cmap", out var cmapTable) || !HasUnicodeCmap(stream, cmapTable.Offset, cmapTable.Length))
                continue; // symbol fonts (Symbol, Wingdings) have only a symbol cmap subtable, which stb_truetype cannot load

            var names = ReadNames(stream, nameTable.Offset, nameTable.Length);
            var family = names.Family ?? names.TypographicFamily;
            if (family is null)
                continue;

            var weight = 400;
            var italic = false;
            if (tables.TryGetValue("OS/2", out var os2) && os2.Length >= 64)
            {
                weight = ReadUInt16(stream, os2.Offset + 4);
                var selection = ReadUInt16(stream, os2.Offset + 62);
                italic = (selection & 0x0201) != 0; // ITALIC or OBLIQUE
            }

            var fixedPitch = tables.TryGetValue("post", out var post) && post.Length >= 16 && ReadUInt32(stream, post.Offset + 12) != 0;
            faces.Add(new FontFaceInfo(path, index, family, names.TypographicFamily, names.PostScriptName, weight, italic, fixedPitch, tables.ContainsKey("fvar")));
        }

        return faces;
    }

    private static Dictionary<string, (long Offset, int Length)> ReadTableDirectory(FileStream stream, long faceOffset)
    {
        stream.Position = faceOffset;
        Span<byte> header = stackalloc byte[12];
        stream.ReadExactly(header);
        var count = BinaryPrimitives.ReadUInt16BigEndian(header[4..]);
        var records = new byte[count * 16];
        stream.ReadExactly(records);
        var tables = new Dictionary<string, (long, int)>(count, StringComparer.Ordinal);
        for (var i = 0; i < count; i++)
        {
            var record = records.AsSpan(i * 16, 16);
            var offset = (long)BinaryPrimitives.ReadUInt32BigEndian(record[8..]);
            var length = (long)BinaryPrimitives.ReadUInt32BigEndian(record[12..]);
            // stb_truetype reads tables through raw pointers without bounds checks, so a table past the
            // end of the file would crash the process later instead of failing here.
            if (offset + length > stream.Length || length > int.MaxValue)
                throw new InvalidDataException("font table outside the file");
            tables[Encoding.ASCII.GetString(record[..4])] = (offset, (int)length);
        }

        return tables;
    }

    /// <summary>Whether the cmap has a Unicode subtable (platform 0, or Windows platform 3 with encoding 1 or 10), the ones stb_truetype reads.</summary>
    private static bool HasUnicodeCmap(FileStream stream, long offset, int length)
    {
        if (length < 4)
            return false;

        var count = ReadUInt16(stream, offset + 2);
        if (4 + count * 8L > length)
            return false;

        var records = new byte[count * 8];
        stream.Position = offset + 4;
        stream.ReadExactly(records);
        for (var i = 0; i < count; i++)
        {
            var platform = BinaryPrimitives.ReadUInt16BigEndian(records.AsSpan(i * 8));
            var encoding = BinaryPrimitives.ReadUInt16BigEndian(records.AsSpan(i * 8 + 2));
            if (platform == 0 || (platform == 3 && encoding is 1 or 10))
                return true;
        }

        return false;
    }

    private static (string? Family, string? TypographicFamily, string? PostScriptName) ReadNames(FileStream stream, long offset, int length)
    {
        if (length is < 6 or > 1 << 20)
            throw new InvalidDataException("bad name table");

        var table = new byte[length];
        stream.Position = offset;
        stream.ReadExactly(table);
        var count = BinaryPrimitives.ReadUInt16BigEndian(table.AsSpan(2));
        var stringsStart = BinaryPrimitives.ReadUInt16BigEndian(table.AsSpan(4));

        string? Find(int nameId)
        {
            string? fallback = null;
            for (var i = 0; i < count && 6 + (i + 1) * 12 <= table.Length; i++)
            {
                var record = table.AsSpan(6 + i * 12, 12);
                var platform = BinaryPrimitives.ReadUInt16BigEndian(record);
                var language = BinaryPrimitives.ReadUInt16BigEndian(record[4..]);
                if (BinaryPrimitives.ReadUInt16BigEndian(record[6..]) != nameId)
                    continue;

                var start = stringsStart + BinaryPrimitives.ReadUInt16BigEndian(record[10..]);
                var size = BinaryPrimitives.ReadUInt16BigEndian(record[8..]);
                if (start + size > table.Length)
                    continue;

                var bytes = table.AsSpan(start, size);
                if (platform == 3 && language == 0x409)
                    return Encoding.BigEndianUnicode.GetString(bytes);
                if (platform is 0 or 3)
                    fallback ??= Encoding.BigEndianUnicode.GetString(bytes);
                else if (platform == 1)
                    fallback ??= Encoding.Latin1.GetString(bytes);
            }

            return fallback;
        }

        return (Find(1), Find(16), Find(6));
    }

    private static int ReadUInt16(FileStream stream, long at)
    {
        Span<byte> bytes = stackalloc byte[2];
        stream.Position = at;
        stream.ReadExactly(bytes);
        return BinaryPrimitives.ReadUInt16BigEndian(bytes);
    }

    private static uint ReadUInt32(FileStream stream, long at)
    {
        Span<byte> bytes = stackalloc byte[4];
        stream.Position = at;
        stream.ReadExactly(bytes);
        return BinaryPrimitives.ReadUInt32BigEndian(bytes);
    }
}

internal sealed class FontNotFoundException(string message) : Exception(message);

internal sealed class FrameTooLargeException(string message) : Exception(message);

/// <summary>
/// A face loaded for drawing. The file is memory-mapped rather than read, because a glyph that no
/// font covers is looked up in every fallback family, and reading each whole file would keep them
/// all in memory. A face that <see cref="TryLoad"/> keeps stays mapped until <see cref="ReleaseAll"/>.
/// </summary>
internal sealed unsafe class LoadedFace
{
    private static readonly Dictionary<(string Path, int Index), LoadedFace?> s_loaded = [];
    private readonly System.IO.MemoryMappedFiles.MemoryMappedFile _file;
    private readonly System.IO.MemoryMappedFiles.MemoryMappedViewAccessor? _view;
    private readonly int _glyphCount;

    private LoadedFace(FontFaceInfo info)
    {
        Info = info;

        // The size comes from the opened file, because FileInfo reports a symbolic link's own size.
        var stream = new FileStream(info.Path, FileMode.Open, FileAccess.Read, FileShare.Read);
        long size;
        try
        {
            size = stream.Length;
            _file = System.IO.MemoryMappedFiles.MemoryMappedFile.CreateFromFile(
                stream, mapName: null, capacity: 0, System.IO.MemoryMappedFiles.MemoryMappedFileAccess.Read,
                HandleInheritability.None, leaveOpen: false);
        }
        catch
        {
            stream.Dispose();
            throw;
        }

        var acquired = false;
        try
        {
            _view = _file.CreateViewAccessor(0, 0, System.IO.MemoryMappedFiles.MemoryMappedFileAccess.Read);
            byte* bytes = null;
            _view.SafeMemoryMappedViewHandle.AcquirePointer(ref bytes);
            acquired = true;
            bytes += _view.PointerOffset;
            var offset = StbTrueType.stbtt_GetFontOffsetForIndex(bytes, info.Index);
            if (offset < 0 || !FontTables.TryCheck(bytes, size, offset, out _glyphCount))
                throw new InvalidDataException($"cannot load font {info.Path}");

            Font = new StbTrueType.stbtt_fontinfo();
            if (StbTrueType.stbtt_InitFont(Font, bytes, offset) == 0)
                throw new InvalidDataException($"cannot load font {info.Path}");
        }
        catch
        {
            if (acquired)
                _view!.SafeMemoryMappedViewHandle.ReleasePointer();
            _view?.Dispose();
            _file.Dispose();
            throw;
        }
    }

    internal FontFaceInfo Info { get; }

    internal StbTrueType.stbtt_fontinfo Font { get; } = null!;

    /// <summary>
    /// The loaded face, or null when the file cannot be opened, its tables fail <see cref="FontTables"/>,
    /// or stb_truetype rejects it. The header scan does not check everything loading needs.
    /// </summary>
    internal static LoadedFace? TryLoad(FontFaceInfo info)
    {
        if (!s_loaded.TryGetValue((info.Path, info.Index), out var face))
        {
            face = Open(info);
            s_loaded[(info.Path, info.Index)] = face;
        }

        return face;
    }

    /// <summary>
    /// Whether the face loads, without keeping its file mapped. A check before <c>pre</c> runs uses
    /// this, because a mapped file cannot be replaced on Windows, and <c>pre</c> may replace the font.
    /// </summary>
    internal static bool CanLoad(FontFaceInfo info)
    {
        if (s_loaded.TryGetValue((info.Path, info.Index), out var loaded))
            return loaded is not null;

        var face = Open(info);
        face?.Close();
        return face is not null;
    }

    /// <summary>
    /// Releases every face <see cref="TryLoad"/> kept. A renderer calls it when it is done drawing:
    /// Windows cannot replace or delete a mapped file, and <c>post</c> may do either to a font.
    /// </summary>
    internal static void ReleaseAll()
    {
        foreach (var face in s_loaded.Values)
            face?.Close();
        s_loaded.Clear();
    }

    /// <summary>The glyph for <paramref name="codepoint"/>, or 0 when the font has none.</summary>
    internal int GlyphIndex(int codepoint)
    {
        // A cmap entry past the glyph count would read hmtx beyond what FontTables checked. A format 12
        // cmap holds 32-bit glyph ids, which come back negative from 0x80000000 up.
        var index = StbTrueType.stbtt_FindGlyphIndex(Font, codepoint);
        return (uint)index < (uint)_glyphCount ? index : 0;
    }

    private static LoadedFace? Open(FontFaceInfo info)
    {
        try
        {
            return new LoadedFace(info);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // StbTrueTypeSharp throws a plain Exception for some unsupported fonts.
            return null;
        }
    }

    private void Close()
    {
        Font.Dispose();
        _view!.SafeMemoryMappedViewHandle.ReleasePointer();
        _view.Dispose();
        _file.Dispose();
    }
}

/// <summary>
/// Checks the tables stb_truetype reads at fixed offsets or by glyph index. It reads them through raw
/// pointers without bounds checks, so a table that is too short, or a loca entry past the end of glyf,
/// would crash the process instead of failing to load. The glyph outlines and the cmap subtables
/// themselves are not checked.
/// </summary>
internal static unsafe class FontTables
{
    internal static bool TryCheck(byte* data, long size, int faceOffset, out int glyphCount)
    {
        glyphCount = 0;
        if (faceOffset + 12L > size)
            return false;

        var tableCount = U16(data + faceOffset + 4);
        if (faceOffset + 12L + tableCount * 16L > size)
            return false;

        if (!Find(data, size, faceOffset, tableCount, "head"u8, out var head, out var headLength) || headLength < 54
            || !Find(data, size, faceOffset, tableCount, "hhea"u8, out var hhea, out var hheaLength) || hheaLength < 36
            || !Find(data, size, faceOffset, tableCount, "maxp"u8, out var maxp, out var maxpLength) || maxpLength < 6
            || !Find(data, size, faceOffset, tableCount, "hmtx"u8, out var hmtx, out var hmtxLength))
        {
            return false;
        }

        glyphCount = U16(data + maxp + 4);
        var longMetrics = U16(data + hhea + 34);
        if (longMetrics == 0 || hmtxLength < 4L * Math.Min(longMetrics, glyphCount) + 2L * Math.Max(0, glyphCount - longMetrics))
            return false;

        if (!Find(data, size, faceOffset, tableCount, "glyf"u8, out _, out var glyfLength))
            return true; // CFF outlines, which are not checked here

        if (!Find(data, size, faceOffset, tableCount, "loca"u8, out var loca, out var locaLength))
            return false;

        var longOffsets = U16(data + head + 50) != 0;
        if (locaLength < (glyphCount + 1L) * (longOffsets ? 4 : 2))
            return false;

        for (var i = 0; i <= glyphCount; i++)
        {
            var at = longOffsets ? U32(data + loca + i * 4L) : U16(data + loca + i * 2L) * 2L;
            if (at > glyfLength)
                return false;
        }

        return true;
    }

    private static bool Find(byte* data, long size, int faceOffset, int tableCount, ReadOnlySpan<byte> tag, out long offset, out long length)
    {
        for (var i = 0; i < tableCount; i++)
        {
            var record = data + faceOffset + 12 + i * 16;
            if (!new ReadOnlySpan<byte>(record, 4).SequenceEqual(tag))
                continue;

            offset = U32(record + 8);
            length = U32(record + 12);
            return offset + length <= size;
        }

        offset = 0;
        length = 0;
        return false;
    }

    private static int U16(byte* at) => BinaryPrimitives.ReadUInt16BigEndian(new ReadOnlySpan<byte>(at, 2));

    private static long U32(byte* at) => BinaryPrimitives.ReadUInt32BigEndian(new ReadOnlySpan<byte>(at, 4));
}

/// <summary>The faces of one family, and how each style is drawn: a real face, or a synthetic bold/italic.</summary>
internal sealed class FontFamilyFaces
{
    private readonly FontFaceInfo _regular;
    private readonly FontFaceInfo? _bold;
    private readonly FontFaceInfo? _italic;
    private readonly FontFaceInfo? _boldItalic;

    private FontFamilyFaces(string name, FontFaceInfo regular, FontFaceInfo? bold, FontFaceInfo? italic, FontFaceInfo? boldItalic)
    {
        Name = name;
        _regular = regular;
        _bold = bold;
        _italic = italic;
        _boldItalic = boldItalic;
    }

    internal string Name { get; }

    internal FontFaceInfo Regular => _regular;

    /// <summary>Every face this family draws with: regular, and bold, italic, or bold-italic where the family has them.</summary>
    internal IEnumerable<FontFaceInfo> Faces => new[] { _regular, _bold, _italic, _boldItalic }.OfType<FontFaceInfo>().Distinct();

    /// <summary>Picks regular, bold, italic, and bold-italic faces from <paramref name="faces"/>, or null when none is upright or italic.</summary>
    internal static FontFamilyFaces? FromFaces(string name, IReadOnlyList<FontFaceInfo> faces)
    {
        if (faces.Count == 0)
            return null;

        var regular = Closest(faces, italic: false, 400) ?? Closest(faces, italic: true, 400);
        if (regular is null)
            return null;

        // stb_truetype draws only a variable font's default instance, so bold comes from emboldening.
        var bold = regular.Variable ? null : Closest(faces, italic: false, 700, minWeight: 600);
        var italic = Closest(faces, italic: true, 400, maxWeight: 599);
        var boldItalic = regular.Variable ? null : Closest(faces, italic: true, 700, minWeight: 600);
        return new FontFamilyFaces(name, regular, bold, italic, boldItalic);
    }

    /// <summary>The face for a style, and which effects must be synthesized on top of it.</summary>
    internal (FontFaceInfo Face, bool SyntheticBold, bool SyntheticItalic) Resolve(bool bold, bool italic) =>
        (bold, italic) switch
        {
            (false, false) => (_regular, false, false),
            (true, false) => _bold is not null ? (_bold, false, false) : (_regular, true, false),
            (false, true) => _italic is not null ? (_italic, false, false) : (_regular, false, !_regular.Italic),
            _ => _boldItalic is not null ? (_boldItalic, false, false)
                : _italic is not null ? (_italic, true, false)
                : _bold is not null ? (_bold, false, true)
                : (_regular, true, !_regular.Italic),
        };

    private static FontFaceInfo? Closest(IReadOnlyList<FontFaceInfo> faces, bool italic, int weight, int minWeight = 0, int maxWeight = 1000)
    {
        FontFaceInfo? best = null;
        foreach (var face in faces)
        {
            if (face.Italic != italic || face.Weight < minWeight || face.Weight > maxWeight)
                continue;
            if (best is null || Math.Abs(face.Weight - weight) < Math.Abs(best.Weight - weight))
                best = face;
        }

        return best;
    }
}

/// <summary>
/// Turns cell text into alpha masks: box drawing, blocks, and braille are drawn
/// procedurally. Everything else comes from the first font in the chain that has the glyph.
/// </summary>
internal sealed unsafe class GlyphSource
{
    private const float ItalicSlant = 0.2f;
    private readonly List<FontFamilyFaces> _chain;
    private readonly RasterLayout _layout;
    private readonly Dictionary<(string Text, int Style, int Span), RasterGlyph> _cache = new();
    private readonly Dictionary<SoftGlyph, RasterGlyph> _softCache = new();

    private GlyphSource(List<FontFamilyFaces> chain, RasterLayout layout)
    {
        _chain = chain;
        _layout = layout;
    }

    /// <summary>
    /// The regular face of the first family in the chain whose regular face loads, or of the first
    /// family when none does, for log messages. A family whose regular file fails the load checks draws
    /// no regular text, so naming it would point at the wrong font.
    /// </summary>
    internal FontFaceInfo Primary =>
        _chain.Select(family => family.Regular).FirstOrDefault(face => LoadedFace.TryLoad(face) is not null) ?? _chain[0].Regular;

    /// <summary>
    /// Builds the font chain: <paramref name="fontFile"/> if given, then the families in the
    /// CSS <paramref name="fontFamily"/> list that are installed, then per-OS fallbacks
    /// for CJK, symbols, and emoji. Generic <c>monospace</c> and <c>ui-monospace</c> map to OS defaults.
    /// </summary>
    internal static GlyphSource Create(string fontFamily, string? fontFile, RasterLayout layout)
    {
        var chain = new List<FontFamilyFaces>();
        if (fontFile is not null)
        {
            if (!File.Exists(fontFile))
                throw new FontNotFoundException($"font file not found: {fontFile}");

            List<FontFaceInfo> faces;
            try
            {
                faces = FontCatalog.ReadFaces(fontFile);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentOutOfRangeException)
            {
                throw new FontNotFoundException($"cannot read font file {fontFile}: {ex.Message}");
            }

            var family = FontFamilyFaces.FromFaces(Path.GetFileName(fontFile), faces)
                ?? throw new FontNotFoundException($"no usable outline font in {fontFile}");

            // The header scan cannot see everything loading needs. Without this check a face that
            // fails to load would be skipped like any fallback, and another font drawn instead.
            if (!family.Faces.All(LoadedFace.CanLoad))
                throw new FontNotFoundException($"cannot load font file {fontFile}: its tables are malformed or unsupported");
            chain.Add(family);
        }

        var names = new List<string>();
        foreach (var name in ParseFamilyList(fontFamily))
        {
            if (name.Equals("monospace", StringComparison.OrdinalIgnoreCase) || name.Equals("ui-monospace", StringComparison.OrdinalIgnoreCase))
                names.AddRange(DefaultMonospaceFamilies());
            else
                names.Add(name);
        }

        names.AddRange(FallbackFamilies());
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in names)
        {
            if (!seen.Add(name))
                continue;

            var faces = FontCatalog.Installed.Where(face => face.Matches(name)).ToList();
            if (FontFamilyFaces.FromFaces(name, faces) is { } family)
                chain.Add(family);
        }

        if (chain.Count == 0)
        {
            throw new FontNotFoundException(
                $"no installed font matches font-family '{fontFamily}' or the built-in fallbacks: install a monospace font or pass --font-file");
        }

        return new GlyphSource(chain, layout);
    }

    /// <summary>Splits a CSS font-family list into family names without quotes.</summary>
    internal static List<string> ParseFamilyList(string css)
    {
        var names = new List<string>();
        foreach (var part in css.Split(','))
        {
            var name = part.Trim().Trim('"', '\'').Trim();
            if (name.Length != 0)
                names.Add(name);
        }

        return names;
    }

    internal static string[] DefaultMonospaceFamilies() =>
        OperatingSystem.IsWindows() ? ["Cascadia Mono", "Consolas", "Lucida Console", "Courier New"]
        : OperatingSystem.IsMacOS() ? ["SF Mono", "Menlo", "Monaco", "Courier"]
        : ["DejaVu Sans Mono", "Noto Sans Mono", "Liberation Mono", "Ubuntu Mono", "FreeMono"];

    internal static string[] FallbackFamilies() =>
        OperatingSystem.IsWindows()
            ? ["Cascadia Mono", "Consolas", "BIZ UDGothic", "MS Gothic", "Yu Gothic", "Meiryo", "Malgun Gothic", "Microsoft YaHei", "Microsoft JhengHei", "Segoe UI Symbol", "Segoe UI Emoji", "Segoe UI"]
        : OperatingSystem.IsMacOS()
            ? ["Menlo", "Hiragino Sans", "Hiragino Kaku Gothic ProN", "PingFang SC", "Apple SD Gothic Neo", "Apple Symbols", "Apple Braille", "Arial Unicode MS"]
            : ["DejaVu Sans Mono", "Noto Sans Mono", "Noto Sans Mono CJK JP", "Noto Sans CJK JP", "Droid Sans Fallback", "WenQuanYi Micro Hei", "DejaVu Sans", "Noto Sans Symbols 2", "Noto Sans Symbols", "Symbola", "Noto Emoji"];

    /// <summary>The mask for <paramref name="text"/> in a cell <paramref name="span"/> columns wide. Empty when nothing should be drawn.</summary>
    internal RasterGlyph Get(string text, bool bold, bool italic, int span)
    {
        var style = (bold ? 1 : 0) | (italic ? 2 : 0);
        if (_cache.TryGetValue((text, style, span), out var cached))
            return cached;

        var glyph = Build(text, bold, italic, span);
        _cache[(text, style, span)] = glyph;
        return glyph;
    }

    /// <summary>
    /// The mask of <paramref name="text"/>'s first scalar and any combining marks, drawn at the glyph's own
    /// advance with its origin at <paramref name="pen"/> pixels, and that advance: for proportional text such as
    /// the window title. A character no font has is a box one cell wide, or two for a wide one.
    /// </summary>
    internal (RasterGlyph Glyph, float Advance) GetProportional(string text, bool bold, float pen)
    {
        if (text.Length == 0 || !Rune.TryGetRuneAt(text, 0, out var first) || Rune.IsControl(first))
            return (RasterGlyph.Empty, 0);

        foreach (var family in _chain)
        {
            var (info, syntheticBold, _) = family.Resolve(bold, italic: false);
            if (LoadedFace.TryLoad(info) is not { } face || face.GlyphIndex(first.Value) == 0)
                continue;

            var font = face.Font;
            var glyphIndex = face.GlyphIndex(first.Value);
            int advance, bearing;
            StbTrueType.stbtt_GetGlyphHMetrics(font, glyphIndex, &advance, &bearing);
            var scale = StbTrueType.stbtt_ScaleForMappingEmToPixels(font, _layout.FontSize);
            var advancePx = advance * scale;
            if (Rune.IsWhiteSpace(first))
                return (RasterGlyph.Empty, advancePx);

            var glyph = RasterizeAt(font, glyphIndex, scale, pen);
            for (var i = first.Utf16SequenceLength; i < text.Length;)
            {
                if (!Rune.TryGetRuneAt(text, i, out var rune))
                    break;
                i += rune.Utf16SequenceLength;
                if (Rune.GetUnicodeCategory(rune) is not (UnicodeCategory.NonSpacingMark or UnicodeCategory.EnclosingMark))
                    continue;
                var markIndex = face.GlyphIndex(rune.Value);
                if (markIndex != 0)
                    glyph = Union(glyph, RasterizeAt(font, markIndex, scale, pen + advancePx));
            }

            if (syntheticBold)
                glyph = Embolden(glyph, Math.Max(1, (int)Math.Round(_layout.FontSize / 16.0)));
            return (glyph, advancePx);
        }

        var span = ScreenBuffer.IsWideCodePoint(first.Value) ? 2 : 1;
        if (Rune.IsWhiteSpace(first))
            return (RasterGlyph.Empty, _layout.CellWidth * span);
        var tofu = CellGlyphs.Tofu(_layout, span);
        return (tofu with { OffsetX = tofu.OffsetX + (int)MathF.Round(pen) }, _layout.CellWidth * span);
    }

    /// <summary>The mask for a soft font glyph: its bitmap scaled to the cell (see <see cref="CellGlyphs.DrawSoft"/>).</summary>
    internal RasterGlyph GetSoft(SoftGlyph glyph)
    {
        if (!_softCache.TryGetValue(glyph, out var mask))
        {
            mask = CellGlyphs.DrawSoft(glyph, _layout);
            _softCache[glyph] = mask;
        }

        return mask;
    }

    private RasterGlyph Build(string text, bool bold, bool italic, int span)
    {
        if (text.Length == 0 || !Rune.TryGetRuneAt(text, 0, out var first) || Rune.IsWhiteSpace(first) || Rune.IsControl(first))
            return RasterGlyph.Empty;

        if (CellGlyphs.TryDraw(first.Value, _layout, span, out var procedural))
            return procedural;

        foreach (var family in _chain)
        {
            var (info, syntheticBold, syntheticItalic) = family.Resolve(bold, italic);
            if (LoadedFace.TryLoad(info) is not { } face || face.GlyphIndex(first.Value) == 0)
                continue;

            var glyph = Rasterize(face, text, span);
            if (syntheticBold)
                glyph = Embolden(glyph, Math.Max(1, (int)Math.Round(_layout.FontSize / 16.0)));
            if (syntheticItalic)
                glyph = Slant(glyph);
            return glyph;
        }

        return CellGlyphs.Tofu(_layout, span);
    }

    /// <summary>
    /// Draws the first scalar of <paramref name="text"/> centered on its advance in the cell
    /// span (shrunk to fit when wider), plus any combining marks over it.
    /// </summary>
    private RasterGlyph Rasterize(LoadedFace face, string text, int span)
    {
        var font = face.Font;
        var first = Rune.GetRuneAt(text, 0);
        var glyphIndex = face.GlyphIndex(first.Value);
        int advance, bearing;
        StbTrueType.stbtt_GetGlyphHMetrics(font, glyphIndex, &advance, &bearing);
        var scale = StbTrueType.stbtt_ScaleForMappingEmToPixels(font, _layout.FontSize);
        var box = (float)(_layout.CellWidth * span);
        var advancePx = advance * scale;
        if (advancePx > box && advancePx > 0)
        {
            scale *= box / advancePx;
            advancePx = box;
        }

        var pen = (box - advancePx) / 2f;
        var glyph = RasterizeAt(font, glyphIndex, scale, pen);

        // Combining marks are drawn at the end of the base glyph's advance, where fonts expect them.
        for (var i = first.Utf16SequenceLength; i < text.Length;)
        {
            if (!Rune.TryGetRuneAt(text, i, out var rune))
                break;
            i += rune.Utf16SequenceLength;
            var category = Rune.GetUnicodeCategory(rune);
            if (category is not (UnicodeCategory.NonSpacingMark or UnicodeCategory.EnclosingMark))
            {
                if (rune.Value == 0x200D)
                    break; // ZWJ sequence: only the first emoji is drawn
                continue; // variation selectors and other format characters
            }

            var markIndex = face.GlyphIndex(rune.Value);
            if (markIndex != 0)
                glyph = Union(glyph, RasterizeAt(font, markIndex, scale, pen + advancePx));
        }

        return glyph;
    }

    private static RasterGlyph RasterizeAt(StbTrueType.stbtt_fontinfo font, int glyphIndex, float scale, float pen)
    {
        var whole = (int)MathF.Floor(pen);
        int width, height, xOffset, yOffset;
        var bitmap = StbTrueType.stbtt_GetGlyphBitmapSubpixel(font, scale, scale, pen - whole, 0f, glyphIndex, &width, &height, &xOffset, &yOffset);
        if (bitmap == null)
            return RasterGlyph.Empty;

        try
        {
            if (width <= 0 || height <= 0)
                return RasterGlyph.Empty;

            var alpha = new ReadOnlySpan<byte>(bitmap, width * height).ToArray();
            return new RasterGlyph(width, height, whole + xOffset, yOffset, alpha);
        }
        finally
        {
            StbTrueType.stbtt_FreeBitmap(bitmap, null);
        }
    }

    private static RasterGlyph Union(RasterGlyph a, RasterGlyph b)
    {
        if (b.Width == 0)
            return a;
        if (a.Width == 0)
            return b;

        var left = Math.Min(a.OffsetX, b.OffsetX);
        var top = Math.Min(a.OffsetY, b.OffsetY);
        var right = Math.Max(a.OffsetX + a.Width, b.OffsetX + b.Width);
        var bottom = Math.Max(a.OffsetY + a.Height, b.OffsetY + b.Height);
        var result = new RasterGlyph(right - left, bottom - top, left, top, new byte[(right - left) * (bottom - top)]);
        foreach (var source in (ReadOnlySpan<RasterGlyph>)[a, b])
        {
            for (var y = 0; y < source.Height; y++)
            {
                var dest = (source.OffsetY - top + y) * result.Width + (source.OffsetX - left);
                for (var x = 0; x < source.Width; x++)
                    result.Alpha[dest + x] = Math.Max(result.Alpha[dest + x], source.Alpha[y * source.Width + x]);
            }
        }

        return result;
    }

    /// <summary>Synthetic bold: widens strokes by <paramref name="strength"/> pixels to the right.</summary>
    internal static RasterGlyph Embolden(RasterGlyph glyph, int strength)
    {
        if (glyph.Width == 0)
            return glyph;

        var width = glyph.Width + strength;
        var alpha = new byte[width * glyph.Height];
        for (var y = 0; y < glyph.Height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                byte value = 0;
                for (var k = 0; k <= strength; k++)
                {
                    var sx = x - k;
                    if (sx >= 0 && sx < glyph.Width)
                        value = Math.Max(value, glyph.Alpha[y * glyph.Width + sx]);
                }

                alpha[y * width + x] = value;
            }
        }

        return glyph with { Width = width, Alpha = alpha };
    }

    /// <summary>Synthetic italic: shears rows right in proportion to their height above the baseline.</summary>
    internal static RasterGlyph Slant(RasterGlyph glyph)
    {
        if (glyph.Width == 0)
            return glyph;

        // Row y (relative to the baseline) shifts by -(y + 0.5) * slant pixels.
        var top = glyph.OffsetY;
        var bottom = glyph.OffsetY + glyph.Height;
        var minShift = -(bottom - 0.5f) * ItalicSlant;
        var maxShift = -(top + 0.5f) * ItalicSlant;
        var left = (int)MathF.Floor(minShift);
        var width = glyph.Width + (int)MathF.Ceiling(maxShift) - left + 1;
        var alpha = new byte[width * glyph.Height];
        for (var row = 0; row < glyph.Height; row++)
        {
            var shift = -(top + row + 0.5f) * ItalicSlant - left;
            var whole = (int)MathF.Floor(shift);
            var fraction = shift - whole;
            for (var x = 0; x < glyph.Width; x++)
            {
                var value = glyph.Alpha[row * glyph.Width + x];
                if (value == 0)
                    continue;

                var at = row * width + x + whole;
                alpha[at] = (byte)Math.Min(255, alpha[at] + value * (1 - fraction));
                alpha[at + 1] = (byte)Math.Min(255, alpha[at + 1] + value * fraction);
            }
        }

        return glyph with { Width = width, OffsetX = glyph.OffsetX + left, Alpha = alpha };
    }
}

/// <summary>
/// Procedural cell glyphs. Fonts leave gaps between rows at 1.25 line height, which breaks
/// TUI borders and block art, so these are drawn edge to edge instead.
/// </summary>
internal static class CellGlyphs
{
    // Arms per box-drawing code point U+2500–U+257F as "UDLR" weights: 0 none, 1 light, 2 heavy, 3 double.
    // A suffix ":n" makes the line dashed with n segments, and ":a" draws a rounded corner.
    // Null entries (diagonals U+2571–U+2573) come from the font.
    private static readonly string?[] BoxArms =
    [
        "0011", "0022", "1100", "2200", "0011:3", "0022:3", "1100:3", "2200:3", "0011:4", "0022:4", "1100:4", "2200:4", "0101", "0102", "0201", "0202",
        "0110", "0120", "0210", "0220", "1001", "1002", "2001", "2002", "1010", "1020", "2010", "2020", "1101", "1102", "2101", "1201",
        "2201", "2102", "1202", "2202", "1110", "1120", "2110", "1210", "2210", "2120", "1220", "2220", "0111", "0121", "0112", "0122",
        "0211", "0221", "0212", "0222", "1011", "1021", "1012", "1022", "2011", "2021", "2012", "2022", "1111", "1121", "1112", "1122",
        "2111", "1211", "2211", "2121", "2112", "1221", "1212", "2122", "1222", "2221", "2212", "2222", "0011:2", "0022:2", "1100:2", "2200:2",
        "0033", "3300", "0103", "0301", "0303", "0130", "0310", "0330", "1003", "3001", "3003", "1030", "3010", "3030", "1103", "3301",
        "3303", "1130", "3310", "3330", "0133", "0311", "0333", "1033", "3011", "3033", "1133", "3311", "3333", "0101:a", "0110:a", "1010:a",
        "1001:a", null, null, null, "0010", "1000", "0001", "0100", "0020", "2000", "0002", "0200", "0012", "1200", "0021", "2100",
    ];

    internal static bool TryDraw(int codepoint, RasterLayout layout, int span, out RasterGlyph glyph)
    {
        glyph = RasterGlyph.Empty;
        if (span != 1)
            return false;

        var width = layout.CellWidth;
        var height = layout.CellHeight;
        var alpha = new byte[width * height];
        if (codepoint is >= 0x2500 and <= 0x257F && BoxArms[codepoint - 0x2500] is { } arms)
            DrawBox(alpha, width, height, arms, layout.FontSize);
        else if (codepoint is >= 0x2580 and <= 0x259F)
            DrawBlock(alpha, width, height, codepoint);
        else if (codepoint is >= 0x2800 and <= 0x28FF)
            DrawBraille(alpha, width, height, codepoint - 0x2800);
        else
            return false;

        glyph = new RasterGlyph(width, height, 0, -layout.Baseline, alpha);
        return true;
    }

    /// <summary>
    /// A soft font (DECDLD) glyph scaled to fill the cell, as terminals draw it whatever the matrix size.
    /// Each pixel's alpha is the share of its area the glyph's set pixels cover, so a 12×24 matrix
    /// in a 10×20 cell keeps every stroke instead of dropping whole rows and columns.
    /// </summary>
    internal static RasterGlyph DrawSoft(SoftGlyph glyph, RasterLayout layout)
    {
        var width = layout.CellWidth;
        var height = layout.CellHeight;
        var alpha = new byte[width * height];
        var scaleX = (double)glyph.Width / width;
        var scaleY = (double)glyph.Height / height;
        var area = scaleX * scaleY;
        for (var y = 0; y < height; y++)
        {
            var top = y * scaleY;
            var bottom = top + scaleY;
            for (var x = 0; x < width; x++)
            {
                var left = x * scaleX;
                var right = left + scaleX;
                var covered = 0d;
                for (var gy = (int)top; gy < Math.Min(glyph.Height, (int)Math.Ceiling(bottom)); gy++)
                {
                    var overlapY = Math.Min(bottom, gy + 1) - Math.Max(top, gy);
                    for (var gx = (int)left; gx < Math.Min(glyph.Width, (int)Math.Ceiling(right)); gx++)
                    {
                        if (glyph.IsSet(gx, gy))
                            covered += overlapY * (Math.Min(right, gx + 1) - Math.Max(left, gx));
                    }
                }

                alpha[y * width + x] = (byte)Math.Clamp((int)Math.Round(covered / area * 255), 0, 255);
            }
        }

        return new RasterGlyph(width, height, 0, -layout.Baseline, alpha);
    }

    /// <summary>An outlined box for characters no font has.</summary>
    internal static RasterGlyph Tofu(RasterLayout layout, int span)
    {
        var width = layout.CellWidth * span;
        var height = layout.CellHeight;
        var alpha = new byte[width * height];
        int x0 = Math.Max(1, width / 8), x1 = width - 1 - x0, y0 = Math.Max(1, height / 6), y1 = height - 1 - y0;
        for (var x = x0; x <= x1; x++)
            alpha[y0 * width + x] = alpha[y1 * width + x] = 255;
        for (var y = y0; y <= y1; y++)
            alpha[y * width + x0] = alpha[y * width + x1] = 255;
        return new RasterGlyph(width, height, 0, -layout.Baseline, alpha);
    }

    private static void DrawBox(byte[] alpha, int width, int height, string arms, int fontSize)
    {
        var light = Math.Max(1, (int)Math.Round(fontSize / 14.0, MidpointRounding.AwayFromZero));
        var heavy = Math.Max(light + 1, light * 2);
        int Thickness(char weight) => weight switch { '1' => light, '2' => heavy, _ => 0 };
        int up = Thickness(arms[0]), down = Thickness(arms[1]), left = Thickness(arms[2]), right = Thickness(arms[3]);
        var dashes = arms.Length > 5 && char.IsDigit(arms[5]) ? arms[5] - '0' : 0;
        var arc = arms.EndsWith(":a", StringComparison.Ordinal);

        if (arc)
        {
            DrawArc(alpha, width, height, light, up != 0, left != 0);
            return;
        }

        if (arms.AsSpan(0, 4).Contains('3'))
        {
            DrawDouble(alpha, width, height, arms[0] - '0', arms[1] - '0', arms[2] - '0', arms[3] - '0', light);
            return;
        }

        // Arms overlap across the center by the thickest line, so joins have no notches.
        var widest = Math.Max(Math.Max(up, down), Math.Max(left, right));
        var centerLeft = (width - widest) / 2;
        var centerTop = (height - widest) / 2;
        if (left != 0)
            HorizontalLine(alpha, width, height, 0, centerLeft + widest, left, dashes);
        if (right != 0)
            HorizontalLine(alpha, width, height, centerLeft, width, right, dashes);
        if (up != 0)
            VerticalLine(alpha, width, height, 0, centerTop + widest, up, dashes);
        if (down != 0)
            VerticalLine(alpha, width, height, centerTop, height, down, dashes);
    }

    /// <summary>
    /// Double lines (U+2550–U+256C) and their single-line mixes. Each of the two parallel
    /// lines ends where the Unicode name says it joins. In ╔ the outer line turns at the
    /// outer corner and the inner line at the inner corner. In ╬ no line crosses the center.
    /// Arms are 0 none, 1 single, 3 double.
    /// </summary>
    private static void DrawDouble(byte[] alpha, int width, int height, int up, int down, int left, int right, int thickness)
    {
        var gap = thickness + 1;
        var span = thickness * 2 + gap;
        int vl = (width - span) / 2, vr = vl + thickness + gap, vc = (width - thickness) / 2;   // left, right, center columns
        int ht = (height - span) / 2, hb = ht + thickness + gap, hc = (height - thickness) / 2; // top, bottom, center rows
        var t = thickness;

        void Band(int x0, int x1, int y0, int y1) => Fill(alpha, width, Math.Max(0, x0), Math.Max(0, y0), Math.Min(width, x1), Math.Min(height, y1), 255);
        static int Pick(params ReadOnlySpan<(bool When, int Value)> rules)
        {
            foreach (var (when, value) in rules)
            {
                if (when)
                    return value;
            }

            return rules[^1].Value;
        }

        // Double horizontal arms: top line (ht) and bottom line (hb).
        if (right == 3)
        {
            Band(Pick((up == 3, vr), (up == 1, vc), (left == 3, 0), (down == 3, vl), (true, vc)), width, ht, ht + t);
            Band(Pick((down == 3, vr), (down == 1, vc), (left == 3, 0), (up == 3, vl), (true, vc)), width, hb, hb + t);
        }

        if (left == 3)
        {
            Band(0, Pick((up == 3, vl + t), (up == 1, vc + t), (right == 3, width), (down == 3, vr + t), (true, vc + t)), ht, ht + t);
            Band(0, Pick((down == 3, vl + t), (down == 1, vc + t), (right == 3, width), (up == 3, vr + t), (true, vc + t)), hb, hb + t);
        }

        // Double vertical arms: left line (vl) and right line (vr).
        if (down == 3)
        {
            Band(vl, vl + t, Pick((left == 3, hb), (left == 1, hc), (up == 3, 0), (right == 3, ht), (true, hc)), height);
            Band(vr, vr + t, Pick((right == 3, hb), (right == 1, hc), (up == 3, 0), (left == 3, ht), (true, hc)), height);
        }

        if (up == 3)
        {
            Band(vl, vl + t, 0, Pick((left == 3, ht + t), (left == 1, hc + t), (down == 3, height), (right == 3, hb + t), (true, hc + t)));
            Band(vr, vr + t, 0, Pick((right == 3, ht + t), (right == 1, hc + t), (down == 3, height), (left == 3, hb + t), (true, hc + t)));
        }

        // Single arms meeting double lines. Opposite single arms form one straight line.
        var verticalDouble = (up == 3 ? 1 : 0) + (down == 3 ? 1 : 0);
        var horizontalDouble = (left == 3 ? 1 : 0) + (right == 3 ? 1 : 0);
        if (left == 1 && right == 1)
            Band(0, width, hc, hc + t);
        else if (right == 1)
            Band(verticalDouble == 2 ? vr : verticalDouble == 1 ? vl : vc, width, hc, hc + t);
        else if (left == 1)
            Band(0, verticalDouble == 2 ? vl + t : verticalDouble == 1 ? vr + t : vc + t, hc, hc + t);

        if (up == 1 && down == 1)
            Band(vc, vc + t, 0, height);
        else if (down == 1)
            Band(vc, vc + t, horizontalDouble == 2 ? hb : horizontalDouble == 1 ? ht : hc, height);
        else if (up == 1)
            Band(vc, vc + t, 0, horizontalDouble == 2 ? ht + t : horizontalDouble == 1 ? hb + t : hc + t);
    }

    // Lines and fills are clipped to the cell, because at the smallest font sizes a line is wider than
    // the cell: a heavy line at font sizes 1–2, and a double line up to font size 5.
    private static void HorizontalLine(byte[] alpha, int width, int height, int x0, int x1, int thickness, int dashes)
    {
        var y0 = Math.Max(0, (height - thickness) / 2);
        var y1 = Math.Min(height, y0 + thickness);
        for (var x = Math.Max(0, x0); x < Math.Min(width, x1); x++)
        {
            if (dashes > 0 && InDashGap(x, width, dashes))
                continue;
            for (var y = y0; y < y1; y++)
                alpha[y * width + x] = 255;
        }
    }

    private static void VerticalLine(byte[] alpha, int width, int height, int y0, int y1, int thickness, int dashes)
    {
        var x0 = Math.Max(0, (width - thickness) / 2);
        var x1 = Math.Min(width, x0 + thickness);
        for (var y = Math.Max(0, y0); y < Math.Min(height, y1); y++)
        {
            if (dashes > 0 && InDashGap(y, height, dashes))
                continue;
            for (var x = x0; x < x1; x++)
                alpha[y * width + x] = 255;
        }
    }

    // Each of n segments is followed by a gap of about a third of its length.
    private static bool InDashGap(int position, int length, int dashes)
    {
        var segment = (double)length / dashes;
        var within = position + 0.5 - Math.Floor((position + 0.5) / segment) * segment;
        return within > segment * 2 / 3;
    }

    /// <summary>Quarter-circle corner (╭╮╯╰) joining the vertical and horizontal center lines, anti-aliased.</summary>
    private static void DrawArc(byte[] alpha, int width, int height, int thickness, bool up, bool left)
    {
        var lineX = (width - thickness) / 2 + thickness / 2.0; // center lines, in pixel units
        var lineY = (height - thickness) / 2 + thickness / 2.0;
        var radius = Math.Min(width, height) / 2.0;
        var towardX = left ? -1 : 1; // direction of the horizontal arm
        var towardY = up ? -1 : 1;   // direction of the vertical arm
        var cx = lineX + towardX * radius;
        var cy = lineY + towardY * radius;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                double px = x + 0.5, py = y + 0.5;
                var dx = (px - cx) * towardX; // > 0: past the arc, along the horizontal arm
                var dy = (py - cy) * towardY; // > 0: past the arc, along the vertical arm
                var distance = double.MaxValue;
                if (dx < 0 && dy < 0)
                    distance = Math.Abs(Math.Sqrt((px - cx) * (px - cx) + (py - cy) * (py - cy)) - radius);
                if (dy >= 0)
                    distance = Math.Min(distance, Math.Abs(px - lineX));
                if (dx >= 0)
                    distance = Math.Min(distance, Math.Abs(py - lineY));

                var coverage = Math.Clamp(thickness / 2.0 + 0.5 - distance, 0, 1);
                if (coverage > 0)
                    alpha[y * width + x] = Math.Max(alpha[y * width + x], (byte)Math.Round(coverage * 255));
            }
        }
    }

    private static void DrawBlock(byte[] alpha, int width, int height, int codepoint)
    {
        int Eighth(int length, int n) => (int)Math.Round(length * n / 8.0, MidpointRounding.AwayFromZero);
        var halfX = width / 2;
        var halfY = height / 2;
        switch (codepoint)
        {
            case 0x2580: Fill(alpha, width, 0, 0, width, halfY, 255); break;
            case >= 0x2581 and <= 0x2588: Fill(alpha, width, 0, height - Eighth(height, codepoint - 0x2580), width, height, 255); break;
            case >= 0x2589 and <= 0x258F: Fill(alpha, width, 0, 0, Eighth(width, 0x2590 - codepoint), height, 255); break;
            case 0x2590: Fill(alpha, width, halfX, 0, width, height, 255); break;
            case 0x2591: Fill(alpha, width, 0, 0, width, height, 64); break;
            case 0x2592: Fill(alpha, width, 0, 0, width, height, 128); break;
            case 0x2593: Fill(alpha, width, 0, 0, width, height, 191); break;
            case 0x2594: Fill(alpha, width, 0, 0, width, Eighth(height, 1), 255); break;
            case 0x2595: Fill(alpha, width, width - Eighth(width, 1), 0, width, height, 255); break;
            default:
                // Quadrants U+2596–U+259F: bits 1 upper-left, 2 upper-right, 4 lower-left, 8 lower-right.
                ReadOnlySpan<byte> quadrants = [4, 8, 1, 1 | 4 | 8, 1 | 8, 1 | 2 | 4, 1 | 2 | 8, 2, 2 | 4, 2 | 4 | 8];
                var bits = quadrants[codepoint - 0x2596];
                if ((bits & 1) != 0) Fill(alpha, width, 0, 0, halfX, halfY, 255);
                if ((bits & 2) != 0) Fill(alpha, width, halfX, 0, width, halfY, 255);
                if ((bits & 4) != 0) Fill(alpha, width, 0, halfY, halfX, height, 255);
                if ((bits & 8) != 0) Fill(alpha, width, halfX, halfY, width, height, 255);
                break;
        }
    }

    /// <summary>Braille: dots 1–3 and 7 down the left column, 4–6 and 8 down the right (bit n = dot n+1).</summary>
    private static void DrawBraille(byte[] alpha, int width, int height, int pattern)
    {
        ReadOnlySpan<(int Col, int Row)> dots = [(0, 0), (0, 1), (0, 2), (1, 0), (1, 1), (1, 2), (0, 3), (1, 3)];
        var size = Math.Max(1, (int)Math.Round(Math.Min(width / 2.0, height / 4.0) * 0.6));
        for (var bit = 0; bit < 8; bit++)
        {
            if ((pattern & (1 << bit)) == 0)
                continue;

            var centerX = width * (dots[bit].Col * 2 + 1) / 4.0;
            var centerY = height * (dots[bit].Row * 2 + 1) / 8.0;
            var x0 = (int)Math.Round(centerX - size / 2.0);
            var y0 = (int)Math.Round(centerY - size / 2.0);
            Fill(alpha, width, Math.Max(0, x0), Math.Max(0, y0), Math.Min(width, x0 + size), Math.Min(height, y0 + size), 255);
        }
    }

    private static void Fill(byte[] alpha, int stride, int x0, int y0, int x1, int y1, byte value)
    {
        x0 = Math.Clamp(x0, 0, stride);
        x1 = Math.Clamp(x1, x0, stride);
        for (var y = Math.Max(0, y0); y < Math.Min(alpha.Length / stride, y1); y++)
            alpha.AsSpan(y * stride + x0, x1 - x0).Fill(value);
    }
}

/// <summary>
/// Draws terminal screens into an RGB24 frame. Rows are redrawn only when their cells or
/// the cursor changed, so mostly-static recordings stay cheap at large sizes.
/// </summary>
internal sealed class FrameRasterizer
{
    private const int CursorAlpha = 128; // SVG: theme foreground at 50% opacity
    private readonly RasterLayout _layout;
    private readonly TerminalTheme _theme;
    private readonly GlyphSource _glyphs;
    private readonly (byte R, byte G, byte B) _background;
    private readonly Dictionary<string, (byte R, byte G, byte B)> _colors = new(StringComparer.OrdinalIgnoreCase);
    private readonly ScreenCell[][] _drawn;
    private readonly bool[] _rowValid;
    private readonly byte[]? _desktop;
    private readonly GlyphSource? _titleGlyphs;
    private readonly string? _title;

    // How much of the window's own fill covers the background, 0-255; 255 is opaque.
    private readonly int _windowAlpha;

    // The standard deviation, in pixels, of the blur on the background seen through a translucent window; 0 is none.
    private readonly int _blur;

    // The frame as Clear left it, when the window is translucent: a row is redrawn from it, not filled.
    private byte[]? _cleared;
    private (int Row, int Col)? _cursor;

    /// <param name="desktop">The background around the window as RGB24 pixels of the whole frame, when the layout has one.</param>
    /// <param name="titleGlyphs">Glyphs of the title's font at its size, when <paramref name="title"/> shows.</param>
    /// <param name="opacity">
    /// How much of the terminal's default background and the title bar covers <paramref name="desktop"/>, 0-1.
    /// Without a desktop the window is opaque.
    /// </param>
    /// <param name="blur">The blur in pixels on the desktop seen through a window with <paramref name="opacity"/> below 1.</param>
    internal FrameRasterizer(
        RasterLayout layout,
        TerminalTheme theme,
        GlyphSource glyphs,
        byte[]? desktop = null,
        GlyphSource? titleGlyphs = null,
        string? title = null,
        double opacity = 1,
        int blur = 0)
    {
        _layout = layout;
        _theme = theme;
        _glyphs = glyphs;
        _desktop = desktop;
        _titleGlyphs = titleGlyphs;
        _title = title;
        _windowAlpha = desktop is null ? 255 : (int)Math.Round(Math.Clamp(opacity, 0, 1) * 255, MidpointRounding.AwayFromZero);
        _blur = _windowAlpha < 255 ? Math.Max(blur, 0) : 0;
        _background = ParseColor(theme.Background);
        Pixels = new byte[layout.Width * layout.Height * 3];
        _drawn = new ScreenCell[layout.Rows][];
        for (var row = 0; row < layout.Rows; row++)
            _drawn[row] = new ScreenCell[layout.Cols];
        _rowValid = new bool[layout.Rows];
        Clear();
    }

    /// <summary>RGB24 pixels, row-major, <see cref="RasterLayout.Width"/> × <see cref="RasterLayout.Height"/>.</summary>
    internal byte[] Pixels { get; }

    /// <summary>
    /// Fills the frame with the theme background, or draws the background and the window on it, then the window
    /// chrome if any, and forgets what was drawn.
    /// </summary>
    internal void Clear()
    {
        if (_desktop is not null)
        {
            _desktop.CopyTo(Pixels, 0);
            if (_layout.Window != WindowStyle.None)
                DrawWindowShadow();
            DrawWindowBody();
        }
        else
        {
            FillRect(0, 0, _layout.Width, _layout.Height, _background);
        }

        if (_layout.Window != WindowStyle.None)
        {
            DrawChrome();
            if (_desktop is not null)
                DrawEdgeHighlight();
        }

        // On a background, a row is drawn again from this, so the edge line, the rounded corners, and a translucent
        // window's background come back with it.
        if (_desktop is not null)
            _cleared = (byte[])Pixels.Clone();
        Array.Clear(_rowValid);
        _cursor = null;
    }

    /// <summary>
    /// The window on the background: the theme background inside, and with chrome a 1-pixel border and rounded
    /// corners, as in the SVG. A translucent window blends the theme background over the background, blurred
    /// first if asked; the border stays opaque, and <see cref="DrawChrome"/> draws the title bar.
    /// </summary>
    private void DrawWindowBody()
    {
        var (x, y, width, height) = (_layout.WindowX, _layout.WindowY, _layout.WindowWidth, _layout.WindowHeight);
        var translucent = _windowAlpha < 255;
        if (_layout.Window == WindowStyle.None)
        {
            if (!translucent)
            {
                FillRect(x, y, width, height, _background);
                return;
            }

            var square = RoundedRect(x, y, width, height, 0, roundTop: false, roundBottom: false);
            if (_blur > 0)
                CopyShape(BlurDesktop(), x, y, x + width, y + height, square);
            FillShape(x, y, x + width, y + height, _background, square, _windowAlpha);
            return;
        }

        var palette = SvgFrameRenderer.ResolveChromePalette(_layout.Window, _theme.Background);
        var radius = _layout.CornerRadius;
        var innerRadius = Math.Max(radius - 1, 0);
        if (!translucent)
        {
            FillRoundedRect(x, y, width, height, radius, ParseColor(palette.Border), roundBottom: true);
            FillRoundedRect(x + 1, y + 1, width - 2, height - 2, innerRadius, _background, roundBottom: true);
            return;
        }

        var outer = RoundedRect(x, y, width, height, radius, roundTop: true, roundBottom: true);
        var inner = RoundedRect(x + 1, y + 1, width - 2, height - 2, innerRadius, roundTop: true, roundBottom: true);
        if (_blur > 0)
            CopyShape(BlurDesktop(), x + 1, y + 1, x + width - 1, y + height - 1, inner);

        // The terminal below the title bar and its line.
        var bar = _layout.TitleBarHeight;
        FillShape(x + 1, y + bar, x + width - 1, y + height - 1, _background,
            RoundedRect(x + 1, y + bar, width - 2, height - 1 - bar, innerRadius, roundTop: false, roundBottom: true), _windowAlpha);
        FillShape(x, y, x + width, y + height, ParseColor(palette.Border), (px, py) => outer(px, py) && !inner(px, py));
    }

    /// <summary>
    /// The window's shadow on the background, as the SVG draws it: the window's shape moved down by
    /// <see cref="SvgFrameRenderer.WindowShadowOffsetY"/>, blurred by a Gaussian of
    /// <see cref="SvgFrameRenderer.WindowShadowDeviation"/>, and darkening the background by up to
    /// <see cref="SvgFrameRenderer.WindowShadowOpacity"/>. It stays outside the window, as the SVG's mask keeps a
    /// translucent window's shadow; an opaque window covers it anyway. Drawn once per video.
    /// </summary>
    private void DrawWindowShadow()
    {
        var (x, y, width, height) = (_layout.WindowX, _layout.WindowY, _layout.WindowWidth, _layout.WindowHeight);
        var radius = _layout.CornerRadius;
        var offset = (int)Math.Round(SvgFrameRenderer.WindowShadowOffsetY);
        var kernel = GaussianKernel(SvgFrameRenderer.WindowShadowDeviation);
        var reach = kernel.Length / 2;

        // The moved shape's coverage over the area its blur reaches, which may run past the frame.
        var left = x - reach;
        var top = y + offset - reach;
        var areaWidth = width + reach * 2;
        var areaHeight = height + reach * 2;
        var shape = RoundedRect(x, y + offset, width, height, radius, roundTop: true, roundBottom: true);
        var coverage = new float[areaWidth * areaHeight];
        for (var row = reach; row < reach + height; row++)
        {
            for (var col = reach; col < reach + width; col++)
                coverage[row * areaWidth + col] = ShapeCoverage(left + col, top + row, x, y + offset, width, height, radius, shape);
        }

        var across = new float[coverage.Length];
        for (var row = reach; row < reach + height; row++)
        {
            for (var col = 0; col < areaWidth; col++)
            {
                var sum = 0f;
                for (var k = 0; k < kernel.Length; k++)
                {
                    var source = col + k - reach;
                    if ((uint)source < (uint)areaWidth)
                        sum += coverage[row * areaWidth + source] * kernel[k];
                }

                across[row * areaWidth + col] = sum;
            }
        }

        var window = RoundedRect(x, y, width, height, radius, roundTop: true, roundBottom: true);
        var opacity = (float)SvgFrameRenderer.WindowShadowOpacity;
        for (var row = 0; row < areaHeight; row++)
        {
            var frameY = top + row;
            if ((uint)frameY >= (uint)_layout.Height)
                continue;

            for (var col = 0; col < areaWidth; col++)
            {
                var frameX = left + col;
                if ((uint)frameX >= (uint)_layout.Width)
                    continue;

                var sum = 0f;
                for (var k = 0; k < kernel.Length; k++)
                {
                    var source = row + k - reach;
                    if ((uint)source < (uint)areaHeight)
                        sum += across[source * areaWidth + col] * kernel[k];
                }

                var inside = ShapeCoverage(frameX, frameY, x, y, width, height, radius, window);
                var shade = opacity * sum * (1 - inside);
                if (shade <= 0)
                    continue;

                var offsetPixel = (frameY * _layout.Width + frameX) * 3;
                for (var c = 0; c < 3; c++)
                    Pixels[offsetPixel + c] = (byte)MathF.Round(Pixels[offsetPixel + c] * (1 - shade));
            }
        }
    }

    /// <summary>
    /// How much of the pixel at (<paramref name="px"/>, <paramref name="py"/>) a rounded rectangle with whole-pixel
    /// edges covers, 0-1: whole inside its straight edges, sampled as <see cref="FillShape"/> samples near a corner.
    /// </summary>
    private static float ShapeCoverage(int px, int py, int x, int y, int width, int height, int radius, Func<double, double, bool> inside)
    {
        if (px < x || px >= x + width || py < y || py >= y + height)
            return 0;
        var nearX = px < x + radius || px >= x + width - radius;
        var nearY = py < y + radius || py >= y + height - radius;
        return nearX && nearY ? Coverage(px, py, inside) / 16f : 1;
    }

    /// <summary>
    /// The faint white line just inside the window's dark border, over the title bar and the terminal, when the
    /// chrome palette has one (<see cref="SvgFrameRenderer.ChromePalette.EdgeHighlight"/>).
    /// </summary>
    private void DrawEdgeHighlight()
    {
        var palette = SvgFrameRenderer.ResolveChromePalette(_layout.Window, _theme.Background);
        if (palette.EdgeHighlight <= 0)
            return;

        var (x, y, width, height) = (_layout.WindowX, _layout.WindowY, _layout.WindowWidth, _layout.WindowHeight);
        var radius = _layout.CornerRadius;
        var outer = RoundedRect(x + 1, y + 1, width - 2, height - 2, Math.Max(radius - 1, 0), roundTop: true, roundBottom: true);
        var inner = RoundedRect(x + 2, y + 2, width - 4, height - 4, Math.Max(radius - 2, 0), roundTop: true, roundBottom: true);
        var alpha = (int)Math.Round(palette.EdgeHighlight * 255, MidpointRounding.AwayFromZero);
        FillShape(x + 1, y + 1, x + width - 1, y + height - 1, ((byte)255, (byte)255, (byte)255), (px, py) => outer(px, py) && !inner(px, py), alpha);
    }

    /// <summary>A normalized Gaussian kernel of standard deviation <paramref name="sigma"/>, three deviations each side.</summary>
    private static float[] GaussianKernel(double sigma)
    {
        var radius = (int)Math.Ceiling(sigma * 3);
        var kernel = new float[radius * 2 + 1];
        var total = 0f;
        for (var k = -radius; k <= radius; k++)
            total += kernel[k + radius] = (float)Math.Exp(-(k * k) / (2 * sigma * sigma));
        for (var k = 0; k < kernel.Length; k++)
            kernel[k] /= total;
        return kernel;
    }

    /// <summary>
    /// The desktop with the window's box blurred: a Gaussian blur in two passes, with the frame's edge pixels
    /// repeated outward, as the SVG's feGaussianBlur with edgeMode="duplicate" draws it. Values blur as stored,
    /// in sRGB, as the SVG's filter does with color-interpolation-filters="sRGB".
    /// </summary>
    private byte[] BlurDesktop()
    {
        var source = _desktop!;
        var (width, height) = (_layout.Width, _layout.Height);
        var x0 = Math.Max(0, _layout.WindowX);
        var y0 = Math.Max(0, _layout.WindowY);
        var x1 = Math.Min(width, _layout.WindowX + _layout.WindowWidth);
        var y1 = Math.Min(height, _layout.WindowY + _layout.WindowHeight);
        var result = (byte[])source.Clone();
        if (x1 <= x0 || y1 <= y0)
            return result;

        var kernel = GaussianKernel(_blur);
        var radius = kernel.Length / 2;

        // Across, for the rows the pass down reads.
        var boxWidth = x1 - x0;
        var top = Math.Max(0, y0 - radius);
        var bottom = Math.Min(height, y1 + radius);
        var across = new float[(bottom - top) * boxWidth * 3];
        for (var y = top; y < bottom; y++)
        {
            var row = y * width;
            for (var x = x0; x < x1; x++)
            {
                float r = 0, g = 0, b = 0;
                for (var k = 0; k < kernel.Length; k++)
                {
                    var offset = (row + Math.Clamp(x + k - radius, 0, width - 1)) * 3;
                    var weight = kernel[k];
                    r += source[offset] * weight;
                    g += source[offset + 1] * weight;
                    b += source[offset + 2] * weight;
                }

                var target = ((y - top) * boxWidth + x - x0) * 3;
                across[target] = r;
                across[target + 1] = g;
                across[target + 2] = b;
            }
        }

        for (var y = y0; y < y1; y++)
        {
            for (var x = x0; x < x1; x++)
            {
                float r = 0, g = 0, b = 0;
                for (var k = 0; k < kernel.Length; k++)
                {
                    var offset = ((Math.Clamp(y + k - radius, 0, height - 1) - top) * boxWidth + x - x0) * 3;
                    var weight = kernel[k];
                    r += across[offset] * weight;
                    g += across[offset + 1] * weight;
                    b += across[offset + 2] * weight;
                }

                var target = (y * width + x) * 3;
                result[target] = (byte)Math.Clamp((int)MathF.Round(r), 0, 255);
                result[target + 1] = (byte)Math.Clamp((int)MathF.Round(g), 0, 255);
                result[target + 2] = (byte)Math.Clamp((int)MathF.Round(b), 0, 255);
            }
        }

        return result;
    }

    /// <summary>
    /// The SVG title bar with its title and buttons, or on Windows its tab row, with the same palette and
    /// fixed-pixel geometry. On a background the bar has the window's rounded top corners inside its border.
    /// Without one, video has no transparency, so the SVG's outer margin, drop shadow, rounded corners, and outer
    /// border are left out and the bar runs flush with the top edge of the frame.
    /// </summary>
    private void DrawChrome()
    {
        var palette = SvgFrameRenderer.ResolveChromePalette(_layout.Window, _theme.Background);
        var (x, y, width) = (_layout.WindowX, _layout.WindowY, _layout.WindowWidth);
        var bar = _layout.TitleBarHeight;

        // A macOS bar ends in a line; a Windows tab row meets the terminal, its active tab in the terminal's color.
        var windows = _layout.Window == WindowStyle.Windows;
        if (_layout.Desktop)
        {
            FillRoundedRect(x + 1, y + 1, width - 2, windows ? bar - 1 : bar - 2, Math.Max(_layout.CornerRadius - 1, 0), ParseColor(palette.TitleBarBg), roundBottom: false, _windowAlpha);
            if (!windows)
                FillRect(x + 1, y + bar - 1, width - 2, 1, ParseColor(palette.Border));
        }
        else
        {
            FillRect(x, y, width, windows ? bar : bar - 1, ParseColor(palette.TitleBarBg));
            if (!windows)
                FillRect(x, y + bar - 1, width, 1, ParseColor(palette.Border));
        }

        if (windows)
        {
            DrawWinTabRow(palette);
            return;
        }

        if (SvgFrameRenderer.FitWindowTitle(_title, _layout.Window, width) is { } fitted)
            DrawTitle(fitted.Text, x + fitted.X, fitted.Centered, y + SvgFrameRenderer.ChromeTitleBaseline, bold: true, ParseColor(palette.TitleText));

        var radius = SvgFrameRenderer.MacButtonDiameter / 2;
        var cx = x + SvgFrameRenderer.ChromeSidePadding + radius;
        var cy = y + SvgFrameRenderer.ChromeTopPadding + radius;
        foreach (var color in SvgFrameRenderer.MacButtonColors)
        {
            var centerX = cx;
            FillShape(centerX - radius, cy - radius, centerX + radius, cy + radius, ParseColor(color),
                (x, y) => (x - centerX) * (x - centerX) + (y - cy) * (y - cy) <= radius * radius);
            cx += SvgFrameRenderer.MacButtonDiameter + SvgFrameRenderer.MacButtonGap;
        }
    }

    /// <summary>
    /// The Windows Terminal tab row, laid out as the SVG lays it out (<see cref="SvgFrameRenderer.LayoutWinTabRow"/>):
    /// the active tab in the terminal's color with the outlined terminal icon, the title, and a close glyph; the
    /// new-tab and dropdown glyphs; and the minimize, maximize, and close glyphs at the right.
    /// </summary>
    private void DrawWinTabRow(SvgFrameRenderer.ChromePalette palette)
    {
        var (x, y, width) = (_layout.WindowX, _layout.WindowY, _layout.WindowWidth);
        var color = ParseColor(palette.TitleText);
        var row = SvgFrameRenderer.LayoutWinTabRow(_title, width);
        if (row.HasTab)
        {
            double left = x + row.TabLeft, right = x + row.TabRight, top = y + SvgFrameRenderer.WinTabTop, bottom = y + _layout.TitleBarHeight;
            FillShape(left, top, right, bottom, _background,
                RoundedRect(left, top, right - left, bottom - top, SvgFrameRenderer.WinTabCornerRadius, roundTop: true, roundBottom: false), _windowAlpha);

            var cy = y + SvgFrameRenderer.WinTabCenterY;
            var ix = left + 10;
            var iy = cy - 6.5;
            StrokeRoundedRect(ix + 0.5, iy + 0.5, 14, 11, 2, color);
            StrokeLine(ix + 4, iy + 4, ix + 6.5, iy + 6.5, color);
            StrokeLine(ix + 6.5, iy + 6.5, ix + 4, iy + 9, color);
            StrokeLine(ix + 8, iy + 9, ix + 11, iy + 9, color);

            if (row.Title is { } title)
                DrawTitle(title, x + row.TitleX, centered: false, y + SvgFrameRenderer.WinTitleBaseline, bold: false, color);

            var close = right - 16;
            StrokeLine(close - 4, cy - 4, close + 4, cy + 4, color);
            StrokeLine(close + 4, cy - 4, close - 4, cy + 4, color);

            if (row.Extras)
            {
                var plus = right + 20;
                var down = right + 46;
                StrokeLine(plus - 5, cy, plus + 5, cy, color);
                StrokeLine(plus, cy - 5, plus, cy + 5, color);
                StrokeLine(down - 4, cy - 2, down, cy + 2, color);
                StrokeLine(down, cy + 2, down + 4, cy - 2, color);
            }
        }

        var edge = x + width;
        var captionY = y + SvgFrameRenderer.WinCaptionCenterY;
        var minimize = edge - SvgFrameRenderer.WinCaptionWidth * 2.5;
        var maximize = edge - SvgFrameRenderer.WinCaptionWidth * 1.5;
        var closeWindow = edge - SvgFrameRenderer.WinCaptionWidth * 0.5;
        StrokeLine(minimize - 5, captionY, minimize + 5, captionY, color);
        StrokeRoundedRect(maximize - 4.5, captionY - 4.5, 9, 9, 1, color);
        StrokeLine(closeWindow - 5, captionY - 5, closeWindow + 5, captionY + 5, color);
        StrokeLine(closeWindow + 5, captionY - 5, closeWindow - 5, captionY + 5, color);
    }

    /// <summary>
    /// A window title in its sans-serif at each glyph's own width, starting at <paramref name="x"/> or, with
    /// <paramref name="centered"/>, centered on it by the width it draws, as the SVG's text-anchor centers it.
    /// </summary>
    private void DrawTitle(string text, double x, bool centered, double baseline, bool bold, (byte R, byte G, byte B) color)
    {
        if (_titleGlyphs is null)
            return;

        var elements = new List<string>();
        var enumerator = StringInfo.GetTextElementEnumerator(text);
        while (enumerator.MoveNext())
            elements.Add(enumerator.GetTextElement());

        var width = 0f;
        foreach (var element in elements)
            width += _titleGlyphs.GetProportional(element, bold, 0).Advance;

        var pen = (float)(x - (centered ? width / 2 : 0));
        var baselineY = (int)Math.Round(baseline, MidpointRounding.AwayFromZero);
        foreach (var element in elements)
        {
            var (glyph, advance) = _titleGlyphs.GetProportional(element, bold, pen);
            if (glyph.Width != 0)
                Blit(glyph, 0, baselineY, _layout.WindowY, _layout.WindowY + _layout.TitleBarHeight, color);
            pen += advance;
        }
    }

    /// <summary>A 1-pixel line from one point to another, anti-aliased, with round ends.</summary>
    private void StrokeLine(double x0, double y0, double x1, double y1, (byte R, byte G, byte B) color)
    {
        const double half = 0.5;
        var (dx, dy) = (x1 - x0, y1 - y0);
        var lengthSquared = dx * dx + dy * dy;
        FillShape(Math.Min(x0, x1) - half, Math.Min(y0, y1) - half, Math.Max(x0, x1) + half, Math.Max(y0, y1) + half, color, (px, py) =>
        {
            var t = lengthSquared == 0 ? 0 : Math.Clamp(((px - x0) * dx + (py - y0) * dy) / lengthSquared, 0, 1);
            var (nx, ny) = (x0 + t * dx - px, y0 + t * dy - py);
            return nx * nx + ny * ny <= half * half;
        });
    }

    /// <summary>The 1-pixel outline of a rounded rectangle, centered on its edges as an SVG stroke is.</summary>
    private void StrokeRoundedRect(double x, double y, double width, double height, double radius, (byte R, byte G, byte B) color)
    {
        var outer = RoundedRect(x - 0.5, y - 0.5, width + 1, height + 1, radius + 0.5, roundTop: true, roundBottom: true);
        var inner = RoundedRect(x + 0.5, y + 0.5, width - 1, height - 1, Math.Max(radius - 0.5, 0), roundTop: true, roundBottom: true);
        FillShape(x - 0.5, y - 0.5, x + width + 0.5, y + height + 0.5, color, (px, py) => outer(px, py) && !inner(px, py));
    }

    /// <summary>
    /// A rectangle with rounded corners, anti-aliased; <paramref name="roundBottom"/> false keeps the bottom corners
    /// square. <paramref name="alpha"/> below 255 blends it over what is there.
    /// </summary>
    private void FillRoundedRect(int x, int y, int width, int height, int radius, (byte R, byte G, byte B) color, bool roundBottom, int alpha = 255)
    {
        if (width <= 0 || height <= 0)
            return;
        if (radius <= 0 && alpha == 255)
        {
            FillRect(x, y, width, height, color);
            return;
        }

        FillShape(x, y, x + width, y + height, color, RoundedRect(x, y, width, height, radius, roundTop: true, roundBottom), alpha);
    }

    /// <summary>Whether a point is inside a rectangle with the given corners rounded by <paramref name="radius"/>.</summary>
    private static Func<double, double, bool> RoundedRect(double x, double y, double width, double height, double radius, bool roundTop, bool roundBottom)
    {
        double left = x, top = y, right = x + width, bottom = y + height, r = Math.Max(radius, 0);
        return (px, py) =>
        {
            if (px < left || px >= right || py < top || py >= bottom)
                return false;

            var nearTop = roundTop && py < top + r;
            var nearBottom = roundBottom && py > bottom - r;
            if (!nearTop && !nearBottom)
                return true;

            var cx = Math.Clamp(px, left + r, right - r);
            var cy = nearTop ? top + r : bottom - r;
            return (px - cx) * (px - cx) + (py - cy) * (py - cy) <= r * r;
        };
    }

    /// <summary>
    /// Fills the pixels of a shape inside the given bounds, anti-aliased by 4×4 supersampling. <paramref name="alpha"/>
    /// below 255 blends the whole shape over what is there.
    /// </summary>
    private void FillShape(double left, double top, double right, double bottom, (byte R, byte G, byte B) color, Func<double, double, bool> inside, int alpha = 255)
    {
        for (var y = Math.Max(0, (int)Math.Floor(top)); y < Math.Min(_layout.Height, (int)Math.Ceiling(bottom)); y++)
        {
            for (var x = Math.Max(0, (int)Math.Floor(left)); x < Math.Min(_layout.Width, (int)Math.Ceiling(right)); x++)
            {
                var hits = Coverage(x, y, inside);
                if (hits != 0)
                    BlendPixel((y * _layout.Width + x) * 3, color, hits * 255 / 16 * alpha / 255);
            }
        }
    }

    /// <summary>Draws the pixels of a shape from <paramref name="source"/>, a frame of the same size, anti-aliased as <see cref="FillShape"/> is.</summary>
    private void CopyShape(byte[] source, double left, double top, double right, double bottom, Func<double, double, bool> inside)
    {
        for (var y = Math.Max(0, (int)Math.Floor(top)); y < Math.Min(_layout.Height, (int)Math.Ceiling(bottom)); y++)
        {
            for (var x = Math.Max(0, (int)Math.Floor(left)); x < Math.Min(_layout.Width, (int)Math.Ceiling(right)); x++)
            {
                var hits = Coverage(x, y, inside);
                if (hits == 0)
                    continue;

                var offset = (y * _layout.Width + x) * 3;
                BlendPixel(offset, (source[offset], source[offset + 1], source[offset + 2]), hits * 255 / 16);
            }
        }
    }

    /// <summary>How many of 4×4 sample points in the pixel at (<paramref name="x"/>, <paramref name="y"/>) are inside the shape, 0-16.</summary>
    private static int Coverage(int x, int y, Func<double, double, bool> inside)
    {
        var hits = 0;
        for (var sy = 0; sy < 4; sy++)
        {
            for (var sx = 0; sx < 4; sx++)
            {
                if (inside(x + (sx + 0.5) / 4, y + (sy + 0.5) / 4))
                    hits++;
            }
        }

        return hits;
    }

    /// <summary>Draws <paramref name="view"/> (canvas-sized), redrawing only rows that changed.</summary>
    internal void Draw(ScreenBuffer view)
    {
        (int Row, int Col)? cursor = view.CursorVisible && view.CursorRow < _layout.Rows && view.CursorCol < _layout.Cols
            ? (view.CursorRow, view.CursorCol)
            : null;
        for (var row = 0; row < _layout.Rows; row++)
        {
            var cursorHere = cursor?.Row == row;
            var cursorWasHere = _cursor?.Row == row;
            if (_rowValid[row] && !cursorHere && !cursorWasHere && view.RowEquals(row, _drawn[row]))
                continue;

            DrawRow(view, row, cursorHere ? cursor!.Value.Col : -1);
            view.CopyRowTo(row, _drawn[row]);
            _rowValid[row] = true;
        }

        _cursor = cursor;
    }

    private void DrawRow(ScreenBuffer view, int row, int cursorCol)
    {
        var top = _layout.RowY(row);
        var height = _layout.CellHeight;
        // Inside the window, and inside its border when it stands on a background. On a background the row comes
        // back as Clear drew it: the edge line, the corners, and a translucent window's background with it.
        var inset = _layout.Desktop && _layout.Window != WindowStyle.None ? 1 : 0;
        if (_cleared is null)
            FillRect(_layout.WindowX + inset, top, _layout.WindowWidth - inset * 2, height, _background);
        else
            CopyRect(_cleared, _layout.WindowX + inset, top, _layout.WindowWidth - inset * 2, height);

        // Backgrounds first, then glyphs, as in the SVG: a glyph may overhang its neighbor.
        var lastUnderlined = -1;
        for (var col = 0; col < _layout.Cols; col++)
        {
            var cell = view.GetCell(row, col);
            if (cell.IsWideContinuation)
                continue;

            if (CellColors.ResolveBackground(cell, _theme) is { } bg)
                FillRect(_layout.CellX(col), top, _layout.CellWidth * (cell.IsWide ? 2 : 1), height, Color(bg));
            if (cell.Underline && cell.Text != " ")
                lastUnderlined = col;
        }

        var underlineY = top + _layout.Baseline + Math.Max(1, (int)Math.Round(_layout.FontSize * 0.1));
        var underlineThickness = Math.Max(1, (int)Math.Round(_layout.FontSize / 16.0));
        for (var col = 0; col < _layout.Cols; col++)
        {
            var cell = view.GetCell(row, col);
            if (cell.IsWideContinuation)
                continue;

            var span = cell.IsWide ? 2 : 1;
            var x = _layout.CellX(col);
            var glyph = cell.Glyph is { } soft ? _glyphs.GetSoft(soft) : _glyphs.Get(cell.Text, cell.Bold, cell.Italic, span);
            var fg = glyph.Width != 0 || (cell.Underline && col <= lastUnderlined)
                ? Color(CellColors.ResolveTintedForeground(view, row, col, cell, _theme))
                : default;
            if (glyph.Width != 0)
                Blit(glyph, x, top + _layout.Baseline, top, top + height, fg);
            if (cell.Underline && col <= lastUnderlined)
                FillRect(x, Math.Min(underlineY, top + height - underlineThickness), _layout.CellWidth * span, underlineThickness, fg);
        }

        if (cursorCol >= 0)
            BlendRect(_layout.CellX(cursorCol), top, _layout.CellWidth, height, ParseColor(_theme.Foreground), CursorAlpha);
    }

    private (byte R, byte G, byte B) Color(string hex)
    {
        if (!_colors.TryGetValue(hex, out var color))
        {
            color = CellColors.TryParseHexColor(hex, out _, out _, out _) ? ParseColor(hex) : ParseColor(_theme.Foreground);
            _colors[hex] = color;
        }

        return color;
    }

    private static (byte R, byte G, byte B) ParseColor(string hex) =>
        CellColors.TryParseHexColor(hex, out var r, out var g, out var b) ? ((byte)r, (byte)g, (byte)b) : ((byte)208, (byte)208, (byte)208);

    private void FillRect(int x, int y, int width, int height, (byte R, byte G, byte B) color)
    {
        var x0 = Math.Max(0, x);
        var x1 = Math.Min(_layout.Width, x + width);
        if (x1 <= x0)
            return;

        for (var yy = Math.Max(0, y); yy < Math.Min(_layout.Height, y + height); yy++)
        {
            var line = Pixels.AsSpan((yy * _layout.Width + x0) * 3, (x1 - x0) * 3);
            for (var i = 0; i < line.Length; i += 3)
            {
                line[i] = color.R;
                line[i + 1] = color.G;
                line[i + 2] = color.B;
            }
        }
    }

    /// <summary>Copies a rectangle from <paramref name="source"/>, a frame of the same size.</summary>
    private void CopyRect(byte[] source, int x, int y, int width, int height)
    {
        var x0 = Math.Max(0, x);
        var x1 = Math.Min(_layout.Width, x + width);
        if (x1 <= x0)
            return;

        for (var yy = Math.Max(0, y); yy < Math.Min(_layout.Height, y + height); yy++)
        {
            var start = (yy * _layout.Width + x0) * 3;
            source.AsSpan(start, (x1 - x0) * 3).CopyTo(Pixels.AsSpan(start));
        }
    }

    private void BlendRect(int x, int y, int width, int height, (byte R, byte G, byte B) color, int alpha)
    {
        for (var yy = Math.Max(0, y); yy < Math.Min(_layout.Height, y + height); yy++)
        {
            for (var xx = Math.Max(0, x); xx < Math.Min(_layout.Width, x + width); xx++)
                BlendPixel((yy * _layout.Width + xx) * 3, color, alpha);
        }
    }

    /// <summary>Draws <paramref name="glyph"/> with its baseline at <paramref name="baselineY"/>, clipped to the row band.</summary>
    private void Blit(RasterGlyph glyph, int cellX, int baselineY, int clipTop, int clipBottom, (byte R, byte G, byte B) color)
    {
        var x0 = cellX + glyph.OffsetX;
        var y0 = baselineY + glyph.OffsetY;
        for (var gy = 0; gy < glyph.Height; gy++)
        {
            var y = y0 + gy;
            if (y < clipTop || y >= clipBottom)
                continue;

            for (var gx = 0; gx < glyph.Width; gx++)
            {
                var x = x0 + gx;
                if ((uint)x >= (uint)_layout.Width)
                    continue;

                int a = glyph.Alpha[gy * glyph.Width + gx];
                if (a != 0)
                    BlendPixel((y * _layout.Width + x) * 3, color, a);
            }
        }
    }

    private void BlendPixel(int offset, (byte R, byte G, byte B) color, int alpha)
    {
        var pixels = Pixels;
        pixels[offset] = (byte)(pixels[offset] + (color.R - pixels[offset]) * alpha / 255);
        pixels[offset + 1] = (byte)(pixels[offset + 1] + (color.G - pixels[offset + 1]) * alpha / 255);
        pixels[offset + 2] = (byte)(pixels[offset + 2] + (color.B - pixels[offset + 2]) * alpha / 255);
    }
}

/// <summary>
/// The background around the window as RGB24 pixels of the whole frame, drawn once per video: one color, or a
/// gradient whose stops are spread from the top-left corner to the bottom-right one, as the SVG's linear gradient
/// in bounding-box units draws it, or an image decoded to the frame size.
/// </summary>
internal static class DesktopRaster
{
    /// <param name="decodeImage">Decodes the image to RGB24 pixels of the given width and height, covering the frame and cropped to it.</param>
    internal static byte[] Render(RasterLayout layout, BackgroundSpec background, Func<int, int, byte[]> decodeImage)
    {
        var (width, height) = (layout.Width, layout.Height);
        if (background.ImagePath is not null)
            return decodeImage(width, height);

        var pixels = new byte[width * height * 3];
        var stops = background.Colors.Select(static color =>
            CellColors.TryParseHexColor(color, out var r, out var g, out var b) ? (R: (double)r, G: (double)g, B: (double)b) : (R: 0d, G: 0d, B: 0d)).ToArray();
        if (stops.Length == 1)
        {
            for (var i = 0; i < pixels.Length; i += 3)
            {
                pixels[i] = (byte)stops[0].R;
                pixels[i + 1] = (byte)stops[0].G;
                pixels[i + 2] = (byte)stops[0].B;
            }

            return pixels;
        }

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                // In bounding-box units the gradient line runs from (0,0) to (1,1), so a point's offset is (u + v) / 2.
                var t = ((x + 0.5) / width + (y + 0.5) / height) / 2 * (stops.Length - 1);
                var index = Math.Min((int)t, stops.Length - 2);
                var f = t - index;
                var (a, b) = (stops[index], stops[index + 1]);
                var offset = (y * width + x) * 3;
                pixels[offset] = (byte)Math.Round(a.R + (b.R - a.R) * f);
                pixels[offset + 1] = (byte)Math.Round(a.G + (b.G - a.G) * f);
                pixels[offset + 2] = (byte)Math.Round(a.B + (b.B - a.B) * f);
            }
        }

        return pixels;
    }
}
