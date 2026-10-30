using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using StbTrueTypeSharp;

/// <summary>
/// Pixel layout of a raster frame: the SVG cell metrics rounded to whole pixels, plus the
/// SVG inner padding. With window chrome, the SVG title bar sits above the terminal.
/// Width and height are even because yuv420p video requires it.
/// </summary>
internal readonly record struct RasterLayout(
    int Cols, int Rows, int CellWidth, int CellHeight, int Baseline, int PadX, int PadY, int Width, int Height, int FontSize,
    WindowStyle Window, int TitleBarHeight)
{
    internal static RasterLayout Create(int fontSize, int cols, int rows, WindowStyle window = WindowStyle.None)
    {
        var cellWidth = Math.Max(1, Round(fontSize * SvgFrameRenderer.CharWidthFactor));
        var cellHeight = Math.Max(1, Round(fontSize * SvgFrameRenderer.LineHeightFactor));
        var baseline = Math.Clamp(Round(fontSize * SvgFrameRenderer.BaselineFactor), 1, cellHeight);
        var (padH, padV) = SvgFrameRenderer.ResolveInnerPadding(fontSize);
        var padX = Round(padH);
        var padY = Round(padV);
        var titleBar = window == WindowStyle.None ? 0 : Round(SvgFrameRenderer.ChromeTitleBarHeight);
        var width = cols * cellWidth + padX * 2;
        var height = titleBar + rows * cellHeight + padY * 2;
        width += width & 1;
        height += height & 1;

        // ffmpeg rejects a raw input frame unless (width + 128) × (height + 128) is below 2^28, about
        // 16000x16000. Large accepted sizes reach that, for example 512×512 cells from font size 37.
        // Checking it here also keeps the RGB24 frame within one array and int arithmetic.
        if ((width + 128L) * (height + 128L) >= 1L << 28)
            throw new FrameTooLargeException($"an mp4 frame of {width}x{height} pixels is too large for ffmpeg; use a smaller --font-size or terminal size");

        return new RasterLayout(cols, rows, cellWidth, cellHeight, baseline, padX, padY, width, height, fontSize, window, titleBar);
    }

    internal int CellX(int col) => PadX + col * CellWidth;

    internal int RowY(int row) => TitleBarHeight + PadY + row * CellHeight;

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
                $"no installed font matches font-family '{fontFamily}' or the built-in fallbacks; install a monospace font or pass --font-file");
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
    private (int Row, int Col)? _cursor;

    internal FrameRasterizer(RasterLayout layout, TerminalTheme theme, GlyphSource glyphs)
    {
        _layout = layout;
        _theme = theme;
        _glyphs = glyphs;
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

    /// <summary>Fills the frame with the theme background, draws the window chrome if any, and forgets what was drawn.</summary>
    internal void Clear()
    {
        FillRect(0, 0, _layout.Width, _layout.Height, _background);
        if (_layout.Window != WindowStyle.None)
            DrawChrome();
        Array.Clear(_rowValid);
        _cursor = null;
    }

    /// <summary>
    /// The SVG title bar and buttons, with the same palette and fixed-pixel geometry. Video has
    /// no transparency, so the SVG's outer margin, drop shadow, rounded corners, and outer border
    /// are left out and the title bar runs flush with the top edge of the frame.
    /// </summary>
    private void DrawChrome()
    {
        var palette = SvgFrameRenderer.ResolveChromePalette(_layout.Window, _theme.Background);
        var bar = _layout.TitleBarHeight;
        FillRect(0, 0, _layout.Width, bar - 1, ParseColor(palette.TitleBarBg));
        FillRect(0, bar - 1, _layout.Width, 1, ParseColor(palette.Border));

        if (_layout.Window == WindowStyle.Macos)
        {
            var radius = SvgFrameRenderer.MacButtonDiameter / 2;
            var cx = SvgFrameRenderer.ChromeSidePadding + radius;
            var cy = SvgFrameRenderer.ChromeTopPadding + radius;
            foreach (var color in SvgFrameRenderer.MacButtonColors)
            {
                var centerX = cx;
                FillShape(centerX - radius, cy - radius, centerX + radius, cy + radius, ParseColor(color),
                    (x, y) => (x - centerX) * (x - centerX) + (y - cy) * (y - cy) <= radius * radius);
                cx += SvgFrameRenderer.MacButtonDiameter + SvgFrameRenderer.MacButtonGap;
            }

            return;
        }

        var size = SvgFrameRenderer.WinButtonSize;
        var corner = SvgFrameRenderer.WinButtonCornerRadius;
        var top = SvgFrameRenderer.ChromeTopPadding;
        var left = _layout.Width - SvgFrameRenderer.ChromeSidePadding - size * 3 - SvgFrameRenderer.WinButtonGap * 2;
        var buttonColor = ParseColor(palette.WinButton);
        for (var i = 0; i < 3; i++)
        {
            var x0 = left;
            FillShape(x0, top, x0 + size, top + size, buttonColor, (x, y) =>
            {
                // Inside the rounded square: clamp to the inner rectangle, then compare with the corner radius.
                var dx = Math.Max(Math.Max(x0 + corner - x, x - (x0 + size - corner)), 0);
                var dy = Math.Max(Math.Max(top + corner - y, y - (top + size - corner)), 0);
                return dx * dx + dy * dy <= corner * corner;
            });
            left += size + SvgFrameRenderer.WinButtonGap;
        }
    }

    /// <summary>Fills the pixels of a shape inside the given bounds, anti-aliased by 4×4 supersampling.</summary>
    private void FillShape(double left, double top, double right, double bottom, (byte R, byte G, byte B) color, Func<double, double, bool> inside)
    {
        for (var y = Math.Max(0, (int)Math.Floor(top)); y < Math.Min(_layout.Height, (int)Math.Ceiling(bottom)); y++)
        {
            for (var x = Math.Max(0, (int)Math.Floor(left)); x < Math.Min(_layout.Width, (int)Math.Ceiling(right)); x++)
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

                if (hits != 0)
                    BlendPixel((y * _layout.Width + x) * 3, color, hits * 255 / 16);
            }
        }
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
        FillRect(0, top, _layout.Width, height, _background);

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
            var glyph = _glyphs.Get(cell.Text, cell.Bold, cell.Italic, span);
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
