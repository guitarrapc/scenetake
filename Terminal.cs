using System.Buffers;
using System.Buffers.Text;
using System.Globalization;
using System.Text;
using System.Text.Unicode;

internal readonly record struct TerminalTheme(string Foreground, string Background, string[] AnsiPalette)
{
    private static readonly string[] DefaultPalette =
    [
        "#151515", "#ac4142", "#7e8e50", "#e5b567", "#6c99bb", "#9f4e85", "#7dd6cf", "#d0d0d0",
        "#505050", "#ac4142", "#7e8e50", "#e5b567", "#6c99bb", "#9f4e85", "#7dd6cf", "#f5f5f5"
    ];

    internal static TerminalTheme FromResolved(ResolvedTheme theme)
    {
        var parts = theme.Palette.Split(':', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 16)
        {
            var palette = (string[])DefaultPalette.Clone();
            Array.Copy(parts, palette, parts.Length);
            return new TerminalTheme(theme.Fg, theme.Bg, palette);
        }

        return new TerminalTheme(theme.Fg, theme.Bg, parts[..16]);
    }
}

internal readonly record struct CellStyle(
    string Foreground, string Background, bool Bold, bool Italic, bool Underline,
    bool Reversed = false, bool Faint = false)
{
    internal static CellStyle Default(TerminalTheme theme) =>
        new(theme.Foreground, theme.Background, false, false, false);
}

internal readonly struct ScreenCell(string text, CellStyle style, bool isWide = false, bool isWideContinuation = false)
{
    internal string Text => text;
    internal CellStyle Style => style;
    internal string Foreground => style.Foreground;
    internal string Background => style.Background;
    internal bool Bold => style.Bold;
    internal bool Italic => style.Italic;
    internal bool Underline => style.Underline;
    internal bool Reversed => style.Reversed;
    internal bool Faint => style.Faint;
    internal bool IsWide => isWide;
    internal bool IsWideContinuation => isWideContinuation;
    internal CellStyle ToStyle() => style;
}

internal sealed class ScreenBuffer
{
    private static readonly string[] CharCache = CreateCharCache();
    private static readonly string Space = " ";

    private readonly TerminalTheme _theme;
    private ScreenCell[,] _mainCells;
    private ScreenCell[,] _altCells;
    private ScreenCell[,] _cells;
    private bool _isAltScreen;
    private int _savedRow;
    private int _savedCol;
    private int _savedMainRow;
    private int _savedMainCol;
    private bool _savedMainPendingWrap;
    private int _scrollTop;
    private int _scrollBottom;
    private bool _pendingWrap;

    internal ScreenBuffer(int width, int height, TerminalTheme theme)
    {
        Width = Math.Max(1, width);
        Height = Math.Max(1, height);
        _theme = theme;
        DefaultStyle = CellStyle.Default(theme);
        _mainCells = CreateBlankCells();
        _altCells = CreateBlankCells();
        _cells = _mainCells;
        _scrollTop = 0;
        _scrollBottom = Height - 1;
        CursorRow = 0;
        CursorCol = 0;
        CursorVisible = true;
    }

    internal int Width { get; private set; }
    internal int Height { get; private set; }
    internal int CursorRow { get; private set; }
    internal int CursorCol { get; private set; }
    internal bool CursorVisible { get; set; }
    internal CellStyle DefaultStyle { get; }

    internal ScreenCell GetCell(int row, int col)
    {
        if ((uint)row >= (uint)Height || (uint)col >= (uint)Width)
            return BlankCell();

        return _cells[row, col];
    }

    internal bool RowEquals(ScreenBuffer other, int row)
    {
        if (Width != other.Width)
            return false;

        var w = Width;
        for (var col = 0; col < w; col++)
        {
            ref readonly var a = ref _cells[row, col];
            ref readonly var b = ref other._cells[row, col];
            if (a.Text != b.Text || a.Style != b.Style || a.IsWide != b.IsWide || a.IsWideContinuation != b.IsWideContinuation)
                return false;
        }

        return true;
    }

    /// <summary>Same comparison as <see cref="RowEquals(ScreenBuffer, int)"/>, against a row copied with <see cref="CopyRowTo"/>.</summary>
    internal bool RowEquals(int row, ReadOnlySpan<ScreenCell> other)
    {
        if (Width != other.Length)
            return false;

        var w = Width;
        for (var col = 0; col < w; col++)
        {
            ref readonly var a = ref _cells[row, col];
            ref readonly var b = ref other[col];
            if (a.Text != b.Text || a.Style != b.Style || a.IsWide != b.IsWide || a.IsWideContinuation != b.IsWideContinuation)
                return false;
        }

        return true;
    }

    internal void CopyRowTo(int row, ScreenCell[] destination) =>
        System.Runtime.InteropServices.MemoryMarshal.CreateReadOnlySpan(ref _cells[row, 0], Width).CopyTo(destination);

    /// <summary>Same test as <see cref="IsRowBlank(int)"/>, for a row copied with <see cref="CopyRowTo"/>.</summary>
    internal static bool IsRowBlank(ReadOnlySpan<ScreenCell> cells, CellStyle defaultStyle)
    {
        foreach (ref readonly var cell in cells)
        {
            if (cell.Text != Space || cell.Style != defaultStyle || cell.IsWide || cell.IsWideContinuation)
                return false;
        }

        return true;
    }

    internal bool IsRowBlank(int row)
    {
        if ((uint)row >= (uint)Height)
            return true;

        for (var col = 0; col < Width; col++)
        {
            ref readonly var cell = ref _cells[row, col];
            if (cell.Text != Space || cell.Style != DefaultStyle || cell.IsWide || cell.IsWideContinuation)
                return false;
        }

        return true;
    }

    /// <summary>A hash of one row's cells (text, style, width), for finding rows with identical content.</summary>
    internal ulong RowHash(int row)
    {
        const ulong fnvOffset = 1469598103934665603UL;
        const ulong fnvPrime = 1099511628211UL;

        var hash = fnvOffset;
        for (var col = 0; col < Width; col++)
        {
            ref readonly var cell = ref _cells[row, col];
            var style = cell.Style;
            hash = MixString(hash, cell.Text.AsSpan(), fnvPrime);
            hash = MixString(hash, style.Foreground.AsSpan(), fnvPrime);
            hash = MixString(hash, style.Background.AsSpan(), fnvPrime);
            hash = MixByte(hash, PackStyleFlags(style, cell.IsWide, cell.IsWideContinuation), fnvPrime);
        }

        return hash;
    }

    internal ulong ComputeCanvasSignature(int canvasWidth, int canvasHeight)
    {
        const ulong fnvOffset = 1469598103934665603UL;
        const ulong fnvPrime = 1099511628211UL;

        var signature = fnvOffset;
        signature = MixInt(signature, CursorRow, fnvPrime);
        signature = MixInt(signature, CursorCol, fnvPrime);
        signature = MixInt(signature, CursorVisible ? 1 : 0, fnvPrime);

        var blank = BlankCell();
        for (var row = 0; row < canvasHeight; row++)
        {
            var inRow = (uint)row < (uint)Height;
            for (var col = 0; col < canvasWidth; col++)
            {
                var cell = inRow && (uint)col < (uint)Width ? _cells[row, col] : blank;
                var style = cell.Style;
                signature = MixString(signature, cell.Text.AsSpan(), fnvPrime);
                signature = MixString(signature, style.Foreground.AsSpan(), fnvPrime);
                signature = MixString(signature, style.Background.AsSpan(), fnvPrime);
                signature = MixByte(signature, PackStyleFlags(style, cell.IsWide, cell.IsWideContinuation), fnvPrime);
            }
        }

        return signature;
    }

    private static ulong MixString(ulong signature, ReadOnlySpan<char> value, ulong fnvPrime)
    {
        foreach (var ch in value)
        {
            signature ^= ch;
            signature *= fnvPrime;
        }

        signature ^= 0xFF;
        signature *= fnvPrime;
        return signature;
    }

    private static byte PackStyleFlags(CellStyle style, bool isWide, bool isWideContinuation)
    {
        byte flags = 0;
        if (style.Bold) flags |= 1;
        if (style.Italic) flags |= 2;
        if (style.Underline) flags |= 4;
        if (style.Reversed) flags |= 8;
        if (style.Faint) flags |= 16;
        if (isWide) flags |= 32;
        if (isWideContinuation) flags |= 64;
        return flags;
    }

    private static ulong MixByte(ulong signature, byte value, ulong fnvPrime)
    {
        signature ^= value;
        signature *= fnvPrime;
        return signature;
    }

    private static ulong MixInt(ulong signature, int value, ulong fnvPrime)
    {
        unchecked
        {
            signature ^= (ulong)(uint)value;
            signature *= fnvPrime;
        }

        return signature;
    }

    internal static string CharStr(char value) =>
        value < CharCache.Length ? CharCache[value] : value.ToString();

    private static string[] CreateCharCache()
    {
        var cache = new string[128];
        for (var i = 0; i < cache.Length; i++)
            cache[i] = ((char)i).ToString();
        return cache;
    }

    internal void Resize(int newWidth, int newHeight)
    {
        newWidth = Math.Max(1, newWidth);
        newHeight = Math.Max(1, newHeight);
        if (newWidth == Width && newHeight == Height)
            return;

        var resizedMain = ResizeCells(_mainCells, Width, Height, newWidth, newHeight, DefaultStyle);
        var resizedAlt = ResizeCells(_altCells, Width, Height, newWidth, newHeight, DefaultStyle);
        Width = newWidth;
        Height = newHeight;
        _mainCells = resizedMain;
        _altCells = resizedAlt;
        _cells = _isAltScreen ? _altCells : _mainCells;
        _scrollTop = 0;
        _scrollBottom = Height - 1;
        CursorRow = Math.Clamp(CursorRow, 0, Height - 1);
        CursorCol = Math.Clamp(CursorCol, 0, Width - 1);
        _pendingWrap = false;
    }

    internal ScreenBuffer Clone()
    {
        var cloned = new ScreenBuffer(Width, Height, _theme)
        {
            CursorRow = CursorRow,
            CursorCol = CursorCol,
            CursorVisible = CursorVisible,
            _savedRow = _savedRow,
            _savedCol = _savedCol,
            _savedMainRow = _savedMainRow,
            _savedMainCol = _savedMainCol,
            _savedMainPendingWrap = _savedMainPendingWrap,
            _isAltScreen = _isAltScreen,
            _scrollTop = _scrollTop,
            _scrollBottom = _scrollBottom,
            _pendingWrap = _pendingWrap,
            _mainCells = CloneCells(_mainCells),
            _altCells = CloneCells(_altCells),
        };
        cloned._cells = cloned._isAltScreen ? cloned._altCells : cloned._mainCells;
        return cloned;
    }

    internal void PutChar(char value, CellStyle style)
    {
        switch (value)
        {
            case '\n':
                LineFeed();
                return;
            case '\r':
                CarriageReturn();
                return;
            case '\b':
                Backspace();
                return;
            case '\t':
            {
                var nextStop = ((CursorCol / 8) + 1) * 8;
                var spaces = Math.Max(1, nextStop - CursorCol);
                for (var i = 0; i < spaces; i++)
                PutPrintable(Space, style);
                return;
            }
            default:
                if (char.IsControl(value))
                    return;
                PutPrintable(CharStr(value), style);
                return;
        }
    }

    internal void PutSurrogatePair(char high, char low, CellStyle style) =>
        PutCodePoint(char.ConvertToUtf32(high, low), string.Create(2, (high, low), static (s, t) => { s[0] = t.high; s[1] = t.low; }), style);

    internal void AppendToPreviousCell(string combining)
    {
        var col = CursorCol - 1;
        var row = CursorRow;
        if (col < 0)
        {
            if (row == 0)
                return;
            row--;
            col = Width - 1;
        }

        if (_cells[row, col].IsWideContinuation && col > 0)
            col--;

        var prev = _cells[row, col];
        if (prev.Text == " ")
            return;

        _cells[row, col] = new ScreenCell(
            prev.Text + combining,
            prev.ToStyle(),
            prev.IsWide,
            prev.IsWideContinuation);

        if (combining == "\uFE0F" && !prev.IsWide && !prev.IsWideContinuation)
            TryPromoteCellToWide(row, col);
    }

    internal void MoveCursorTo(int row, int col)
    {
        _pendingWrap = false;
        CursorRow = Clamp(row, 0, Height - 1);
        CursorCol = Clamp(col, 0, Width - 1);
    }

    internal void MoveCursorBy(int rowDelta, int colDelta) =>
        MoveCursorTo(CursorRow + rowDelta, CursorCol + colDelta);

    internal void SaveCursor()
    {
        _savedRow = CursorRow;
        _savedCol = CursorCol;
    }

    internal void RestoreCursor() => MoveCursorTo(_savedRow, _savedCol);

    internal void CarriageReturn()
    {
        _pendingWrap = false;
        CursorCol = 0;
    }

    internal void LineFeed() => Index();

    internal void Index()
    {
        _pendingWrap = false;
        if (CursorRow == _scrollBottom)
        {
            ScrollRegionUp(_scrollTop, _scrollBottom, 1);
            return;
        }

        CursorRow = Math.Min(Height - 1, CursorRow + 1);
    }

    internal void NextLine()
    {
        CarriageReturn();
        Index();
    }

    internal void ReverseIndex()
    {
        _pendingWrap = false;
        if (CursorRow == _scrollTop)
        {
            ScrollRegionDown(_scrollTop, _scrollBottom, 1);
            return;
        }

        CursorRow = Math.Max(0, CursorRow - 1);
    }

    internal void SetScrollRegion(int top, int bottom)
    {
        top = Clamp(top, 0, Height - 1);
        bottom = Clamp(bottom, 0, Height - 1);

        if (top >= bottom)
        {
            _scrollTop = 0;
            _scrollBottom = Height - 1;
        }
        else
        {
            _scrollTop = top;
            _scrollBottom = bottom;
        }

        MoveCursorTo(0, 0);
    }

    internal void Backspace() => CursorCol = Math.Max(0, CursorCol - 1);

    internal void ClearLine(int mode, CellStyle? style = null)
    {
        var eraseStyle = style ?? DefaultStyle;
        switch (mode)
        {
            case 1:
                for (var col = 0; col <= CursorCol; col++)
                    _cells[CursorRow, col] = new ScreenCell(" ", eraseStyle);
                return;
            case 2:
                for (var col = 0; col < Width; col++)
                    _cells[CursorRow, col] = new ScreenCell(" ", eraseStyle);
                return;
            default:
                for (var col = CursorCol; col < Width; col++)
                    _cells[CursorRow, col] = new ScreenCell(" ", eraseStyle);
                return;
        }
    }

    internal void DeleteCharacters(int count, CellStyle? style = null)
    {
        if (count <= 0)
            return;

        var eraseStyle = style ?? DefaultStyle;
        count = Math.Min(count, Width - CursorCol);

        for (var col = CursorCol; col < Width - count; col++)
            _cells[CursorRow, col] = _cells[CursorRow, col + count];

        for (var col = Width - count; col < Width; col++)
            _cells[CursorRow, col] = new ScreenCell(" ", eraseStyle);
    }

    internal void InsertBlankCharacters(int count, CellStyle? style = null)
    {
        if (count <= 0)
            return;

        var eraseStyle = style ?? DefaultStyle;
        count = Math.Min(count, Width - CursorCol);

        for (var col = Width - 1; col >= CursorCol + count; col--)
            _cells[CursorRow, col] = _cells[CursorRow, col - count];

        for (var col = CursorCol; col < CursorCol + count; col++)
            _cells[CursorRow, col] = new ScreenCell(" ", eraseStyle);
    }

    internal void InsertLines(int count)
    {
        if (count <= 0 || CursorRow < _scrollTop || CursorRow > _scrollBottom)
            return;

        count = Math.Min(count, _scrollBottom - CursorRow + 1);
        for (var row = _scrollBottom; row >= CursorRow + count; row--)
        {
            for (var col = 0; col < Width; col++)
                _cells[row, col] = _cells[row - count, col];
        }

        for (var row = CursorRow; row < CursorRow + count; row++)
            ClearRow(row);
    }

    internal void DeleteLines(int count)
    {
        if (count <= 0 || CursorRow < _scrollTop || CursorRow > _scrollBottom)
            return;

        count = Math.Min(count, _scrollBottom - CursorRow + 1);
        for (var row = CursorRow; row <= _scrollBottom - count; row++)
        {
            for (var col = 0; col < Width; col++)
                _cells[row, col] = _cells[row + count, col];
        }

        for (var row = _scrollBottom - count + 1; row <= _scrollBottom; row++)
            ClearRow(row);
    }

    internal void ScrollUpLines(int count) =>
        ScrollRegionUp(_scrollTop, _scrollBottom, count);

    internal void ScrollDownLines(int count) =>
        ScrollRegionDown(_scrollTop, _scrollBottom, count);

    internal void EraseChars(int count, CellStyle? style = null)
    {
        if (count <= 0)
            return;

        var eraseStyle = style ?? DefaultStyle;
        var endCol = Math.Min(Width - 1, CursorCol + count - 1);
        for (var col = CursorCol; col <= endCol; col++)
        {
            var cell = _cells[CursorRow, col];
            if (cell.IsWideContinuation && col > 0)
                _cells[CursorRow, col - 1] = new ScreenCell(" ", eraseStyle);

            if (cell.IsWide && col + 1 < Width)
                _cells[CursorRow, col + 1] = new ScreenCell(" ", eraseStyle);

            _cells[CursorRow, col] = new ScreenCell(" ", eraseStyle);
        }
    }

    internal void ClearDisplay(int mode, CellStyle? style = null)
    {
        var eraseStyle = style ?? DefaultStyle;
        switch (mode)
        {
            case 1:
                for (var row = 0; row <= CursorRow; row++)
                {
                    var end = row == CursorRow ? CursorCol : Width - 1;
                    for (var col = 0; col <= end; col++)
                        _cells[row, col] = new ScreenCell(" ", eraseStyle);
                }
                return;
            case 2:
                for (var row = 0; row < Height; row++)
                {
                    for (var col = 0; col < Width; col++)
                        _cells[row, col] = new ScreenCell(" ", eraseStyle);
                }
                return;
            default:
                for (var row = CursorRow; row < Height; row++)
                {
                    var start = row == CursorRow ? CursorCol : 0;
                    for (var col = start; col < Width; col++)
                        _cells[row, col] = new ScreenCell(" ", eraseStyle);
                }
                return;
        }
    }

    internal void SetAlternateScreen(bool enabled)
    {
        if (enabled)
        {
            if (_isAltScreen)
                return;

            _savedMainRow = CursorRow;
            _savedMainCol = CursorCol;
            _savedMainPendingWrap = _pendingWrap;
            _altCells = CreateBlankCells();
            _cells = _altCells;
            CursorRow = 0;
            CursorCol = 0;
            _pendingWrap = false;
            _scrollTop = 0;
            _scrollBottom = Height - 1;
            _isAltScreen = true;
            return;
        }

        if (!_isAltScreen)
            return;

        _cells = _mainCells;
        _isAltScreen = false;
        CursorRow = Clamp(_savedMainRow, 0, Height - 1);
        CursorCol = Clamp(_savedMainCol, 0, Width - 1);
        _pendingWrap = _savedMainPendingWrap;
        _scrollTop = 0;
        _scrollBottom = Height - 1;
    }

    private void TryPromoteCellToWide(int row, int col)
    {
        if (col + 1 >= Width)
            return;

        var next = _cells[row, col + 1];
        if (next.Text != " " || next.IsWideContinuation)
            return;

        var cell = _cells[row, col];
        _cells[row, col] = new ScreenCell(cell.Text, cell.ToStyle(), isWide: true);
        _cells[row, col + 1] = new ScreenCell(Space, cell.ToStyle(), isWideContinuation: true);

        if (CursorRow == row && CursorCol == col + 1)
        {
            CursorCol++;
            if (CursorCol >= Width)
            {
                _pendingWrap = true;
                CursorCol = Width - 1;
            }
        }
    }

    private void PutPrintable(string text, CellStyle style)
    {
        if (_pendingWrap)
        {
            _pendingWrap = false;
            CursorCol = 0;
            Index();
        }

        var cp = Utf16CodePoint(text);
        if (cp < 0)
            return;

        PutCodePoint(cp, text, style);
    }

    private void PutCodePoint(int cp, string text, CellStyle style)
    {
        var isWide = IsWideCodePoint(cp);
        if (isWide && CursorCol + 1 >= Width)
        {
            _cells[CursorRow, CursorCol] = BlankCell();
            CursorCol = 0;
            Index();
        }

        _cells[CursorRow, CursorCol] = new ScreenCell(text, style, isWide);
        CursorCol++;

        if (isWide && CursorCol < Width)
        {
            _cells[CursorRow, CursorCol] = new ScreenCell(Space, style, isWideContinuation: true);
            CursorCol++;
        }

        if (CursorCol >= Width)
        {
            _pendingWrap = true;
            CursorCol = Width - 1;
        }
    }

    private static int Utf16CodePoint(string text) =>
        text.Length switch
        {
            0 => -1,
            1 => text[0],
            2 when char.IsHighSurrogate(text[0]) && char.IsLowSurrogate(text[1]) => char.ConvertToUtf32(text[0], text[1]),
            _ => -1
        };

    private static bool IsWideCodePoint(int cp) => cp switch
    {
        0x2611 or 0x2705 or 0x274C or 0x2753 or 0x2754 or 0x2755 or 0x2757 => true,
        >= 0x1100 and <= 0x115F => true,
        >= 0x2E80 and <= 0x9FFF => true,
        >= 0xA000 and <= 0xA48C => true,
        >= 0xA960 and <= 0xA97F => true,
        >= 0xAC00 and <= 0xD7A3 => true,
        >= 0xF900 and <= 0xFAFF => true,
        >= 0xFE10 and <= 0xFE6F => true,
        >= 0xFF01 and <= 0xFF60 => true,
        >= 0xFFE0 and <= 0xFFE6 => true,
        >= 0x1B000 and <= 0x1B0FF => true,
        0x1F004 or 0x1F0CF => true,
        >= 0x1F200 and <= 0x1F64F => true,
        >= 0x1F680 and <= 0x1F6FF => true,
        >= 0x1F900 and <= 0x1F9FF => true,
        >= 0x20000 and <= 0x2FFFD => true,
        >= 0x30000 and <= 0x3FFFD => true,
        _ => false
    };

    // Rows scrolled off the top are dropped. Nothing renders scrollback, and keeping it would grow
    // replay memory with every line a recording scrolls.
    private void ScrollRegionUp(int top, int bottom, int count)
    {
        if (count <= 0 || top < 0 || bottom >= Height || top > bottom)
            return;

        count = Math.Min(count, bottom - top + 1);
        for (var i = 0; i < count; i++)
        {
            for (var row = top + 1; row <= bottom; row++)
            {
                for (var col = 0; col < Width; col++)
                    _cells[row - 1, col] = _cells[row, col];
            }

            ClearRow(bottom);
        }
    }

    private void ScrollRegionDown(int top, int bottom, int count)
    {
        if (count <= 0 || top < 0 || bottom >= Height || top > bottom)
            return;

        count = Math.Min(count, bottom - top + 1);
        for (var i = 0; i < count; i++)
        {
            for (var row = bottom - 1; row >= top; row--)
            {
                for (var col = 0; col < Width; col++)
                    _cells[row + 1, col] = _cells[row, col];
            }

            ClearRow(top);
        }
    }

    private void ClearRow(int row)
    {
        for (var col = 0; col < Width; col++)
            _cells[row, col] = BlankCell();
    }

    private ScreenCell BlankCell() => new(Space, DefaultStyle);

    private ScreenCell[,] CreateBlankCells()
    {
        var cells = new ScreenCell[Height, Width];
        for (var row = 0; row < Height; row++)
        {
            for (var col = 0; col < Width; col++)
                cells[row, col] = BlankCell();
        }

        return cells;
    }

    private static ScreenCell[,] CloneCells(ScreenCell[,] source)
    {
        var cloned = new ScreenCell[source.GetLength(0), source.GetLength(1)];
        Array.Copy(source, cloned, source.Length);
        return cloned;
    }

    private static ScreenCell[,] ResizeCells(
        ScreenCell[,] source,
        int oldWidth,
        int oldHeight,
        int newWidth,
        int newHeight,
        CellStyle blankStyle)
    {
        var blank = new ScreenCell(Space, blankStyle);
        var resized = new ScreenCell[newHeight, newWidth];
        for (var row = 0; row < newHeight; row++)
        {
            for (var col = 0; col < newWidth; col++)
            {
                resized[row, col] = row < oldHeight && col < oldWidth
                    ? source[row, col]
                    : blank;
            }
        }

        return resized;
    }

    private static int Clamp(int value, int min, int max)
    {
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }

    internal string GetTrimmedRowText(int row)
    {
        if ((uint)row >= (uint)Height)
            return "";

        var sb = new StringBuilder(Width);
        for (var col = 0; col < Width; col++)
        {
            var cell = GetCell(row, col);
            if (cell.IsWideContinuation)
                continue;
            sb.Append(cell.Text);
        }

        return sb.ToString().TrimEnd();
    }
}

internal sealed class AnsiParser
{
    private const int MissingParameter = int.MinValue;

    private readonly ScreenBuffer _buffer;
    private readonly TerminalTheme _theme;
    private readonly List<int> _csiParams = new(8);
    private CellStyle _style;
    private byte[] _buf = [];
    private int _pendingLength;

    internal AnsiParser(ScreenBuffer buffer, TerminalTheme theme)
    {
        _buffer = buffer;
        _theme = theme;
        _style = _buffer.DefaultStyle;
    }

    internal void Process(string text)
    {
        if (text.Length == 0)
            return;

        var byteCount = Encoding.UTF8.GetByteCount(text);
        EnsureBuffer(_pendingLength + byteCount);
        Encoding.UTF8.GetBytes(text, _buf.AsSpan(_pendingLength));
        var combined = _buf.AsSpan(0, _pendingLength + byteCount);
        _pendingLength = 0;
        ProcessCoreUtf8(combined);
    }

    internal void ProcessUtf8(ReadOnlySpan<byte> utf8)
    {
        if (utf8.IsEmpty)
            return;

        if (_pendingLength > 0)
        {
            EnsureBuffer(_pendingLength + utf8.Length);
            utf8.CopyTo(_buf.AsSpan(_pendingLength));
            var combined = _buf.AsSpan(0, _pendingLength + utf8.Length);
            _pendingLength = 0;
            ProcessCoreUtf8(combined);
            return;
        }

        ProcessCoreUtf8(utf8);
    }

    private void EnsureBuffer(int capacity)
    {
        if (_buf.Length >= capacity)
            return;

        var newBuf = new byte[Math.Max(capacity, _buf.Length < 16 ? 16 : _buf.Length * 2)];
        if (_pendingLength > 0)
            _buf.AsSpan(0, _pendingLength).CopyTo(newBuf);
        _buf = newBuf;
    }

    private void SavePending(ReadOnlySpan<byte> tail)
    {
        if (tail.IsEmpty)
            return;

        EnsureBuffer(tail.Length);
        tail.CopyTo(_buf);
        _pendingLength = tail.Length;
    }

    private void ProcessCoreUtf8(ReadOnlySpan<byte> input)
    {
        var i = 0;
        while (i < input.Length)
        {
            var b = input[i];
            if (b == 0x1B)
            {
                if (!TryHandleEscape(input, i, out var escapeEndIndex))
                {
                    SavePending(input[i..]);
                    return;
                }

                i = escapeEndIndex + 1;
                continue;
            }

            if (b == (byte)'^' && i + 2 < input.Length && input[i + 1] == (byte)'[' && input[i + 2] == (byte)']')
            {
                if (!TrySkipOsc(input, i + 3, out var oscEnd, caretTerminator: true))
                {
                    SavePending(input[i..]);
                    return;
                }

                i = oscEnd + 1;
                continue;
            }

            var status = Rune.DecodeFromUtf8(input[i..], out var rune, out var consumed);
            if (status == OperationStatus.NeedMoreData)
            {
                SavePending(input[i..]);
                return;
            }

            if (status != OperationStatus.Done)
            {
                ProcessDecodedRune(new Rune(0xFFFD));
                i += Math.Max(consumed, 1);
                continue;
            }

            i += consumed;
            ProcessDecodedRune(rune);
        }
    }

    private void ProcessDecodedRune(Rune rune)
    {
        if (rune.Utf16SequenceLength == 2)
        {
            Span<char> chars = stackalloc char[2];
            rune.TryEncodeToUtf16(chars, out _);
            _buffer.PutSurrogatePair(chars[0], chars[1], _style);
            return;
        }

        var ch = (char)rune.Value;
        if (IsVariationSelector(ch))
        {
            _buffer.AppendToPreviousCell(ScreenBuffer.CharStr(ch));
            return;
        }

        if (IsZeroWidthChar(ch))
            return;

        var category = CharUnicodeInfo.GetUnicodeCategory(ch);
        if (category is UnicodeCategory.NonSpacingMark
            or UnicodeCategory.SpacingCombiningMark
            or UnicodeCategory.EnclosingMark)
        {
            _buffer.AppendToPreviousCell(ScreenBuffer.CharStr(ch));
            return;
        }

        _buffer.PutChar(ch, _style);
    }

    private static bool IsZeroWidthChar(char ch) =>
        ch is '\u00AD' or '\u200B' or '\u200C' or '\u200D' or '\uFEFF';

    private static bool IsVariationSelector(char ch) => ch is >= '\uFE00' and <= '\uFE0F';

    private bool TryHandleEscape(ReadOnlySpan<byte> input, int index, out int endIndex)
    {
        endIndex = index;
        if (index + 1 >= input.Length)
            return false;

        switch (input[index + 1])
        {
            case (byte)'[':
                return TryHandleCsi(input, index + 2, out endIndex);
            case (byte)']':
                return TrySkipOsc(input, index + 2, out endIndex);
            case (byte)'P':
                return TrySkipOsc(input, index + 2, out endIndex);
            case (byte)'(' or (byte)')' or (byte)'*' or (byte)'+' or (byte)'-' or (byte)'.' or (byte)'/' or (byte)'#' or (byte)'%':
                if (index + 2 >= input.Length)
                    return false;
                endIndex = index + 2;
                return true;
            case (byte)'7':
                _buffer.SaveCursor();
                endIndex = index + 1;
                return true;
            case (byte)'8':
                _buffer.RestoreCursor();
                endIndex = index + 1;
                return true;
            case (byte)'D':
                _buffer.Index();
                endIndex = index + 1;
                return true;
            case (byte)'E':
                _buffer.NextLine();
                endIndex = index + 1;
                return true;
            case (byte)'M':
                _buffer.ReverseIndex();
                endIndex = index + 1;
                return true;
            case (byte)'c':
                _buffer.ClearDisplay(2);
                _buffer.MoveCursorTo(0, 0);
                _style = _buffer.DefaultStyle;
                endIndex = index + 1;
                return true;
            default:
                endIndex = index + 1;
                return true;
        }
    }

    private static bool TrySkipOsc(ReadOnlySpan<byte> input, int start, out int endIndex, bool caretTerminator = false)
    {
        endIndex = start;
        for (var i = start; i < input.Length; i++)
        {
            if (input[i] == 0x07)
            {
                endIndex = i;
                return true;
            }

            if (caretTerminator && input[i] == (byte)'^' && i + 2 < input.Length && input[i + 1] == (byte)'[' && input[i + 2] == (byte)'\\')
            {
                endIndex = i + 2;
                return true;
            }

            if (input[i] == 0x1B && i + 1 < input.Length && input[i + 1] == (byte)'\\')
            {
                endIndex = i + 1;
                return true;
            }
        }

        return false;
    }

    private bool TryHandleCsi(ReadOnlySpan<byte> input, int start, out int endIndex)
    {
        endIndex = input.Length - 1;
        char? privateMarker = null;
        var paramStart = start;
        if (start < input.Length && input[start] is (byte)'<' or (byte)'=' or (byte)'>' or (byte)'?')
        {
            privateMarker = (char)input[start];
            paramStart++;
            start++;
        }

        var i = start;
        while (i < input.Length)
        {
            var c = input[i];
            if (c is >= (byte)'@' and <= (byte)'~')
            {
                var parameterText = paramStart <= i ? input.Slice(paramStart, i - paramStart) : ReadOnlySpan<byte>.Empty;
                ApplyCsi(privateMarker, (char)c, parameterText);
                endIndex = i;
                return true;
            }

            i++;
        }

        return false;
    }

    private void ApplyCsi(char? privateMarker, char command, ReadOnlySpan<byte> parameterText)
    {
        ParseParameters(parameterText);
        var parameters = _csiParams;
        if (privateMarker == '?' && parameters.Count > 0)
        {
            if (parameters[0] == 1049 && command is 'h' or 'l')
            {
                _buffer.SetAlternateScreen(command == 'h');
                return;
            }

            if (parameters[0] == 25 && command is 'h' or 'l')
            {
                _buffer.CursorVisible = command == 'h';
                return;
            }
        }

        if (privateMarker is not null)
            return;

        switch (command)
        {
            case 'm':
                ApplySgr(parameters);
                return;
            case 'A':
                _buffer.MoveCursorBy(-Math.Max(1, GetParameter(parameters, 0, 1)), 0);
                return;
            case 'B' or 'e':
                _buffer.MoveCursorBy(Math.Max(1, GetParameter(parameters, 0, 1)), 0);
                return;
            case 'C' or 'a':
                _buffer.MoveCursorBy(0, Math.Max(1, GetParameter(parameters, 0, 1)));
                return;
            case 'D':
                _buffer.MoveCursorBy(0, -Math.Max(1, GetParameter(parameters, 0, 1)));
                return;
            case 'E':
                _buffer.MoveCursorBy(Math.Max(1, GetParameter(parameters, 0, 1)), 0);
                _buffer.CarriageReturn();
                return;
            case 'F':
                _buffer.MoveCursorBy(-Math.Max(1, GetParameter(parameters, 0, 1)), 0);
                _buffer.CarriageReturn();
                return;
            case 'G' or '`':
            {
                var col = Math.Max(1, GetParameter(parameters, 0, 1)) - 1;
                _buffer.MoveCursorTo(_buffer.CursorRow, col);
                return;
            }
            case 'H' or 'f':
            {
                var row = Math.Max(1, GetParameter(parameters, 0, 1)) - 1;
                var col = Math.Max(1, GetParameter(parameters, 1, 1)) - 1;
                _buffer.MoveCursorTo(row, col);
                return;
            }
            case 'd':
            {
                var row = Math.Max(1, GetParameter(parameters, 0, 1)) - 1;
                _buffer.MoveCursorTo(row, _buffer.CursorCol);
                return;
            }
            case 'J':
                _buffer.ClearDisplay(GetParameter(parameters, 0, 0), _style);
                return;
            case 'K':
                _buffer.ClearLine(GetParameter(parameters, 0, 0), _style);
                return;
            case 'P':
                _buffer.DeleteCharacters(Math.Max(1, GetParameter(parameters, 0, 1)), _style);
                return;
            case '@':
                _buffer.InsertBlankCharacters(Math.Max(1, GetParameter(parameters, 0, 1)), _style);
                return;
            case 'L':
                _buffer.InsertLines(Math.Max(1, GetParameter(parameters, 0, 1)));
                return;
            case 'M':
                _buffer.DeleteLines(Math.Max(1, GetParameter(parameters, 0, 1)));
                return;
            case 'S':
                _buffer.ScrollUpLines(Math.Max(1, GetParameter(parameters, 0, 1)));
                return;
            case 'T':
                _buffer.ScrollDownLines(Math.Max(1, GetParameter(parameters, 0, 1)));
                return;
            case 'r':
            {
                var top = Math.Max(1, GetParameter(parameters, 0, 1)) - 1;
                var bottom = Math.Max(1, GetParameter(parameters, 1, _buffer.Height)) - 1;
                _buffer.SetScrollRegion(top, bottom);
                return;
            }
            case 'X':
                _buffer.EraseChars(Math.Max(1, GetParameter(parameters, 0, 1)), _style);
                return;
            case 's':
                _buffer.SaveCursor();
                return;
            case 'u':
                _buffer.RestoreCursor();
                return;
        }
    }

    private void ApplySgr(List<int> parameters)
    {
        if (parameters.Count == 0)
            parameters.Add(0);

        for (var i = 0; i < parameters.Count; i++)
        {
            var code = GetParameter(parameters, i, 0);
            switch (code)
            {
                case 0:
                    _style = _buffer.DefaultStyle;
                    break;
                case 1:
                    _style = _style with { Bold = true, Faint = false };
                    break;
                case 2:
                    _style = _style with { Bold = false, Faint = true };
                    break;
                case 3:
                    _style = _style with { Italic = true };
                    break;
                case 4:
                    _style = _style with { Underline = true };
                    break;
                case 7:
                    _style = _style with { Reversed = true };
                    break;
                case 22:
                    _style = _style with { Bold = false, Faint = false };
                    break;
                case 23:
                    _style = _style with { Italic = false };
                    break;
                case 24:
                    _style = _style with { Underline = false };
                    break;
                case 27:
                    _style = _style with { Reversed = false };
                    break;
                case 39:
                    _style = _style with { Foreground = _buffer.DefaultStyle.Foreground };
                    break;
                case 49:
                    _style = _style with { Background = _buffer.DefaultStyle.Background };
                    break;
                default:
                    if (code is >= 30 and <= 37)
                        _style = _style with { Foreground = _theme.AnsiPalette[code - 30] };
                    else if (code is >= 40 and <= 47)
                        _style = _style with { Background = _theme.AnsiPalette[code - 40] };
                    else if (code is >= 90 and <= 97)
                        _style = _style with { Foreground = _theme.AnsiPalette[8 + (code - 90)] };
                    else if (code is >= 100 and <= 107)
                        _style = _style with { Background = _theme.AnsiPalette[8 + (code - 100)] };
                    else if ((code == 38 || code == 48) && i + 1 < parameters.Count)
                        TryApplyExtendedColor(parameters, ref i, code == 48);
                    break;
            }
        }
    }

    private void TryApplyExtendedColor(List<int> parameters, ref int index, bool isBackground)
    {
        var mode = GetParameter(parameters, index + 1, 0);
        if (mode == 5 && index + 2 < parameters.Count)
        {
            var color = FromAnsi256(GetParameter(parameters, index + 2, 0));
            _style = isBackground
                ? _style with { Background = color }
                : _style with { Foreground = color };
            index += 2;
            return;
        }

        if (mode == 2)
        {
            var rgbStart = index + 2;
            if (rgbStart < parameters.Count && parameters[rgbStart] == MissingParameter)
                rgbStart++;

            if (rgbStart + 2 >= parameters.Count)
                return;

            var r = Clamp(GetParameter(parameters, rgbStart, 0), 0, 255);
            var g = Clamp(GetParameter(parameters, rgbStart + 1, 0), 0, 255);
            var b = Clamp(GetParameter(parameters, rgbStart + 2, 0), 0, 255);
            var color = string.Create(CultureInfo.InvariantCulture, $"#{r:X2}{g:X2}{b:X2}");
            _style = isBackground
                ? _style with { Background = color }
                : _style with { Foreground = color };
            index = rgbStart + 2;
        }
    }

    private string FromAnsi256(int index)
    {
        index = Clamp(index, 0, 255);
        if (index < 16)
            return _theme.AnsiPalette[index];

        if (index >= 232)
        {
            var gray = Clamp(8 + ((index - 232) * 10), 0, 255);
            return string.Create(CultureInfo.InvariantCulture, $"#{gray:X2}{gray:X2}{gray:X2}");
        }

        var cube = index - 16;
        var r = cube / 36;
        var g = (cube % 36) / 6;
        var b = cube % 6;
        var rgbR = r == 0 ? 0 : 55 + r * 40;
        var rgbG = g == 0 ? 0 : 55 + g * 40;
        var rgbB = b == 0 ? 0 : 55 + b * 40;
        return string.Create(CultureInfo.InvariantCulture, $"#{rgbR:X2}{rgbG:X2}{rgbB:X2}");
    }

    private void ParseParameters(ReadOnlySpan<byte> parameterText)
    {
        _csiParams.Clear();
        if (parameterText.IsEmpty)
            return;

        var i = 0;
        while (i <= parameterText.Length)
        {
            var start = i;
            while (i < parameterText.Length && parameterText[i] is not (byte)';' and not (byte)':')
                i++;

            var part = TrimAsciiWhitespace(parameterText[start..i]);
            if (part.IsEmpty)
                _csiParams.Add(MissingParameter);
            else if (Utf8Parser.TryParse(part, out int value, out _))
                _csiParams.Add(value);
            else
                _csiParams.Add(MissingParameter);

            if (i >= parameterText.Length)
                break;

            i++;
        }
    }

    private static ReadOnlySpan<byte> TrimAsciiWhitespace(ReadOnlySpan<byte> bytes)
    {
        var start = 0;
        while (start < bytes.Length && IsAsciiWhitespaceByte(bytes[start]))
            start++;

        var end = bytes.Length;
        while (end > start && IsAsciiWhitespaceByte(bytes[end - 1]))
            end--;

        return bytes[start..end];
    }

    private static bool IsAsciiWhitespaceByte(byte b) =>
        b is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n';

    private static int GetParameter(List<int> parameters, int index, int defaultValue)
    {
        if ((uint)index >= (uint)parameters.Count)
            return defaultValue;

        var parameter = parameters[index];
        return parameter == MissingParameter ? defaultValue : parameter;
    }

    private static int Clamp(int value, int min, int max)
    {
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }
}

internal enum CastEventKind
{
    Output,
    Resize,
    Marker,
}

internal readonly record struct CastEvent(
    double Time,
    CastEventKind Kind,
    string Data,
    int ResizeWidth = 0,
    int ResizeHeight = 0,
    ReadOnlyMemory<byte> Utf8Output = default)
{
    internal bool HasUtf8Output => !Utf8Output.IsEmpty;

    internal static CastEvent Output(double time, string data) => new(time, CastEventKind.Output, data);

    internal static CastEvent OutputUtf8(double time, ReadOnlyMemory<byte> data) =>
        new(time, CastEventKind.Output, "", Utf8Output: data);

    internal static CastEvent Resize(double time, int width, int height) =>
        new(time, CastEventKind.Resize, $"{width}x{height}", width, height);

    internal static CastEvent Marker(double time, string label) => new(time, CastEventKind.Marker, label);
}

internal static class CastEventLayout
{
    internal static string NameCommentPrefixForLastEvent(IReadOnlyList<CastEvent> events)
    {
        if (events.Count == 0)
            return "";

        var last = events[^1];
        if (last.Kind != CastEventKind.Output)
            return "\r\n";

        const int suffixLimit = 4096;
        var suffixParts = new List<string>();
        var suffixLength = 0;
        for (var i = events.Count - 1; i >= 0 && events[i].Kind == CastEventKind.Output && suffixLength < suffixLimit; i--)
        {
            var output = events[i].HasUtf8Output
                ? Encoding.UTF8.GetString(events[i].Utf8Output.Span)
                : events[i].Data;
            if (output.Length == 0)
                continue;

            var remaining = suffixLimit - suffixLength;
            if (output.Length > remaining)
            {
                suffixParts.Add(output[^remaining..]);
                suffixLength = suffixLimit;
                break;
            }

            suffixParts.Add(output);
            suffixLength += output.Length;
        }

        if (suffixParts.Count == 0)
            return "";

        suffixParts.Reverse();
        return VisualOutputEndsWithNewline(string.Concat(suffixParts)) ? "" : "\r\n";
    }

    static bool VisualOutputEndsWithNewline(string output)
    {
        var endsWithNewline = false;
        for (var i = 0; i < output.Length;)
        {
            var ch = output[i];
            if (ch == '\u001b')
            {
                i = SkipAnsiEscape(output, i);
                continue;
            }

            if (ch == '\n')
            {
                endsWithNewline = true;
                i++;
                continue;
            }

            if (char.IsControl(ch))
            {
                i++;
                continue;
            }

            endsWithNewline = false;
            i += char.IsHighSurrogate(ch) && i + 1 < output.Length && char.IsLowSurrogate(output[i + 1]) ? 2 : 1;
        }

        return endsWithNewline;
    }

    static int SkipAnsiEscape(string output, int index)
    {
        if (index + 1 >= output.Length)
            return output.Length;

        var introducer = output[index + 1];
        if (introducer == '[')
            return SkipAnsiFinalByteSequence(output, index + 2);
        if (introducer == ']')
            return SkipAnsiStringSequence(output, index + 2);
        if (introducer is 'P' or '^' or '_')
            return SkipAnsiStringSequence(output, index + 2);
        if (introducer is '(' or ')' or '*' or '+' or '-' or '.' or '/')
            return Math.Min(index + 3, output.Length);

        return Math.Min(index + 2, output.Length);
    }

    static int SkipAnsiFinalByteSequence(string output, int index)
    {
        while (index < output.Length)
        {
            var ch = output[index++];
            if (ch >= '@' && ch <= '~')
                break;
        }

        return index;
    }

    static int SkipAnsiStringSequence(string output, int index)
    {
        while (index < output.Length)
        {
            var ch = output[index++];
            if (ch == '\a')
                break;
            if (ch == '\u001b' && index < output.Length && output[index] == '\\')
            {
                index++;
                break;
            }
        }

        return index;
    }
}

/// <summary>
/// One changed screen state on the replay timeline. Holds no screen contents, because keeping every
/// screen grows with events × screen size. <see cref="EventIndex"/> names the event after which it is
/// shown, and <see cref="TerminalReplay.ReplaySelected"/> replays up to it to draw the screen.
/// </summary>
internal sealed class ReplayFrame(double time, int viewportWidth, int viewportHeight, ulong signature, bool isBlank, int eventIndex)
{
    internal double Time { get; } = time;
    internal int ViewportWidth { get; } = viewportWidth;
    internal int ViewportHeight { get; } = viewportHeight;
    internal ulong Signature { get; } = signature;
    internal bool IsBlank { get; } = isBlank;

    /// <summary>The event after which this screen state is shown: -1 before any event, <c>events.Count</c> after all of them.</summary>
    internal int EventIndex { get; } = eventIndex;

    internal ReplayFrame WithTime(double time) =>
        new(time, ViewportWidth, ViewportHeight, Signature, IsBlank, EventIndex);
}

/// <summary>
/// A changed screen state during replay. <see cref="View"/> is canvas-sized and is only
/// valid until the enumeration advances: it may be the live emulator buffer.
/// </summary>
internal readonly record struct ReplayCapture(
    double Time, ScreenBuffer View, int ViewportWidth, int ViewportHeight, ulong Signature, int EventIndex);

/// <summary>
/// Steps the emulator through cast events by time, for fixed-rate sampling (mp4).
/// Applies events exactly as <see cref="TerminalReplay.EnumerateCaptures"/> does, so a
/// sampled screen matches the SVG frame shown at the same time.
/// </summary>
internal sealed class ReplayStepper
{
    private const double TimeEpsilon = 1e-9;
    private readonly IReadOnlyList<CastEvent> _events;
    private readonly ScreenBuffer _buffer;
    private readonly AnsiParser _parser;
    private readonly int _canvasWidth;
    private readonly int _canvasHeight;
    private readonly int _stopIndex;
    private int _next;
    private ulong _lastSignature;

    /// <param name="stopIndex">Events at or after this index are never applied (see <see cref="TerminalReplay.FindTrailingBlankCutoff"/>).</param>
    internal ReplayStepper(
        IReadOnlyList<CastEvent> events,
        int initialWidth,
        int initialHeight,
        int canvasWidth,
        int canvasHeight,
        TerminalTheme theme,
        int stopIndex)
    {
        _events = events;
        _buffer = new ScreenBuffer(initialWidth, initialHeight, theme);
        _parser = new AnsiParser(_buffer, theme);
        _canvasWidth = canvasWidth;
        _canvasHeight = canvasHeight;
        _stopIndex = Math.Min(stopIndex, events.Count);
    }

    /// <summary>
    /// False until the first event is applied. Nothing, not even the cursor, is drawn before that,
    /// as in the SVG. A recording with no events differs: its SVG shows the cursor throughout,
    /// while the mp4 shows only the background.
    /// </summary>
    internal bool Started { get; private set; }

    /// <summary>The canvas-sized screen. Valid until the next <see cref="AdvanceTo"/>.</summary>
    internal ScreenBuffer View => TerminalReplay.ViewForCanvas(_buffer, _canvasWidth, _canvasHeight);

    /// <summary>Applies every event with time ≤ <paramref name="time"/>. Returns true when the screen changed.</summary>
    internal bool AdvanceTo(double time)
    {
        var applied = false;
        while (_next < _stopIndex && _events[_next].Time <= time + TimeEpsilon)
        {
            var ev = _events[_next++];
            if (ev.Kind == CastEventKind.Marker)
                continue;

            if (ev.Kind == CastEventKind.Resize)
                _buffer.Resize(ev.ResizeWidth, ev.ResizeHeight);
            else if (ev.HasUtf8Output)
                _parser.ProcessUtf8(ev.Utf8Output.Span);
            else
                _parser.Process(ev.Data);
            applied = true;
        }

        if (!applied)
            return false;

        var signature = _buffer.ComputeCanvasSignature(_canvasWidth, _canvasHeight);
        if (Started && signature == _lastSignature)
            return false;

        Started = true;
        _lastSignature = signature;
        return true;
    }
}

internal static class TerminalReplay
{
    private const string Space = " ";
    internal const double CastExitIntervalSeconds = 0.05;

    /// <summary>
    /// Matches agg GIF loop period with <c>--last-frame-duration 0</c>: the final
    /// frame is held for one more idle gap (time since the previous output event).
    /// </summary>
    internal static double ComputeLoopDuration(IReadOnlyList<CastEvent> events)
    {
        if (events.Count == 0)
            return 0;

        var lastTime = events[^1].Time;
        for (var i = events.Count - 2; i >= 0; i--)
        {
            if (events[i].Kind != CastEventKind.Output)
                continue;

            return lastTime + Math.Max(0, lastTime - events[i].Time);
        }

        return lastTime + CastExitIntervalSeconds;
    }

    /// <summary>
    /// Builds the replay timeline: one frame per changed screen state, after trimming a
    /// trailing blank alternate-screen restore. Frames hold no screen contents, so memory
    /// stays proportional to the number of changes, not changes × screen size.
    /// </summary>
    internal static List<ReplayFrame> BuildFrames(
        IReadOnlyList<CastEvent> events,
        int initialWidth,
        int initialHeight,
        int canvasWidth,
        int canvasHeight,
        TerminalTheme theme)
    {
        var frames = new List<ReplayFrame>();
        foreach (var capture in EnumerateCaptures(events, initialWidth, initialHeight, canvasWidth, canvasHeight, theme))
        {
            frames.Add(new ReplayFrame(
                capture.Time, capture.ViewportWidth, capture.ViewportHeight, capture.Signature, IsBlankBuffer(capture.View), capture.EventIndex));
        }

        return TrimTrailingBlankRestore(frames, events);
    }

    /// <summary>
    /// Index of the first event whose screen change the SVG drops as a trailing blank
    /// alternate-screen restore, or <c>events.Count</c> when nothing is dropped. A
    /// fixed-rate renderer stops applying events there so it ends on the same screen.
    /// </summary>
    internal static int FindTrailingBlankCutoff(
        IReadOnlyList<CastEvent> events,
        int initialWidth,
        int initialHeight,
        int canvasWidth,
        int canvasHeight,
        TerminalTheme theme)
    {
        // Only a blank final screen can be trimmed. Checking that needs a parse-only replay,
        // which is much cheaper than hashing the screen after every event below.
        var final = new ScreenBuffer(initialWidth, initialHeight, theme);
        var finalParser = new AnsiParser(final, theme);
        foreach (var ev in events)
        {
            if (ev.Kind == CastEventKind.Marker)
                continue;
            if (ev.Kind == CastEventKind.Resize)
                final.Resize(ev.ResizeWidth, ev.ResizeHeight);
            else if (ev.HasUtf8Output)
                finalParser.ProcessUtf8(ev.Utf8Output.Span);
            else
                finalParser.Process(ev.Data);
        }

        if (!IsBlankBuffer(ViewForCanvas(final, canvasWidth, canvasHeight)))
            return events.Count;

        var frames = new List<ReplayFrame>();
        foreach (var capture in EnumerateCaptures(events, initialWidth, initialHeight, canvasWidth, canvasHeight, theme))
        {
            frames.Add(new ReplayFrame(
                capture.Time, capture.ViewportWidth, capture.ViewportHeight, capture.Signature, IsBlankBuffer(capture.View), capture.EventIndex));
        }

        var kept = TrimTrailingBlankRestore(frames, events).Count;
        return kept < frames.Count && frames[kept].EventIndex >= 0 ? frames[kept].EventIndex : events.Count;
    }

    /// <summary>
    /// Replays <paramref name="events"/> and yields each changed screen state in order.
    /// The first applied event always yields, because the screen starts with no signature.
    /// A recording with no output or resize events yields its final state once, at time 0.
    /// Markers are not applied, and the cast reader drops input events.
    /// </summary>
    internal static IEnumerable<ReplayCapture> EnumerateCaptures(
        IReadOnlyList<CastEvent> events,
        int initialWidth,
        int initialHeight,
        int canvasWidth,
        int canvasHeight,
        TerminalTheme theme)
    {
        var buffer = new ScreenBuffer(initialWidth, initialHeight, theme);
        var parser = new AnsiParser(buffer, theme);
        ulong? lastSignature = null;
        var yielded = false;

        if (!IsBlankBuffer(buffer))
        {
            var signature = buffer.ComputeCanvasSignature(canvasWidth, canvasHeight);
            lastSignature = signature;
            yielded = true;
            yield return new ReplayCapture(0, ViewForCanvas(buffer, canvasWidth, canvasHeight), buffer.Width, buffer.Height, signature, -1);
        }

        for (var eventIndex = 0; eventIndex < events.Count; eventIndex++)
        {
            var ev = events[eventIndex];
            if (ev.Kind == CastEventKind.Marker)
                continue;

            if (ev.Kind == CastEventKind.Resize)
                buffer.Resize(ev.ResizeWidth, ev.ResizeHeight);
            else if (ev.HasUtf8Output)
                parser.ProcessUtf8(ev.Utf8Output.Span);
            else
                parser.Process(ev.Data);

            var signature = buffer.ComputeCanvasSignature(canvasWidth, canvasHeight);
            if (lastSignature is ulong previous && previous == signature)
                continue;

            lastSignature = signature;
            yielded = true;
            yield return new ReplayCapture(ev.Time, ViewForCanvas(buffer, canvasWidth, canvasHeight), buffer.Width, buffer.Height, signature, eventIndex);
        }

        if (!yielded)
        {
            var view = ViewForCanvas(buffer, canvasWidth, canvasHeight);
            yield return new ReplayCapture(0, view, buffer.Width, buffer.Height, view.ComputeCanvasSignature(view.Width, view.Height), events.Count);
        }
    }

    /// <summary>
    /// Replays <paramref name="events"/> again to draw <paramref name="frames"/>, which came from
    /// <see cref="BuildFrames"/> with the same arguments (possibly a subset, at changed times). It
    /// stops at each frame's event and does not hash the screen, which only the first pass needs.
    /// </summary>
    internal static IEnumerable<(ReplayFrame Frame, ScreenBuffer View)> ReplaySelected(
        IReadOnlyList<CastEvent> events,
        int initialWidth,
        int initialHeight,
        int canvasWidth,
        int canvasHeight,
        TerminalTheme theme,
        IReadOnlyList<ReplayFrame> frames)
    {
        var buffer = new ScreenBuffer(initialWidth, initialHeight, theme);
        var parser = new AnsiParser(buffer, theme);
        var next = 0;
        while (next < frames.Count && frames[next].EventIndex < 0)
            yield return (frames[next++], ViewForCanvas(buffer, canvasWidth, canvasHeight));

        for (var eventIndex = 0; eventIndex < events.Count && next < frames.Count; eventIndex++)
        {
            var ev = events[eventIndex];
            if (ev.Kind == CastEventKind.Marker)
                continue;

            if (ev.Kind == CastEventKind.Resize)
                buffer.Resize(ev.ResizeWidth, ev.ResizeHeight);
            else if (ev.HasUtf8Output)
                parser.ProcessUtf8(ev.Utf8Output.Span);
            else
                parser.Process(ev.Data);

            if (frames[next].EventIndex == eventIndex)
                yield return (frames[next++], ViewForCanvas(buffer, canvasWidth, canvasHeight));
        }

        while (next < frames.Count && frames[next].EventIndex >= events.Count)
            yield return (frames[next++], ViewForCanvas(buffer, canvasWidth, canvasHeight));

        if (next != frames.Count)
            throw new InvalidOperationException($"replay reached {next} of {frames.Count} frames");
    }

    /// <summary>The live buffer when it already fills the canvas, otherwise a resized copy.</summary>
    internal static ScreenBuffer ViewForCanvas(ScreenBuffer source, int canvasWidth, int canvasHeight)
    {
        if (source.Width == canvasWidth && source.Height == canvasHeight)
            return source;

        var copy = source.Clone();
        copy.Resize(canvasWidth, canvasHeight);
        return copy;
    }

    internal static (int width, int height) ResolveCanvasSize(
        int initialWidth,
        int initialHeight,
        IReadOnlyList<CastEvent> events)
    {
        var maxWidth = initialWidth;
        var maxHeight = initialHeight;
        foreach (var ev in events)
        {
            if (ev.Kind != CastEventKind.Resize)
                continue;

            maxWidth = Math.Max(maxWidth, ev.ResizeWidth);
            maxHeight = Math.Max(maxHeight, ev.ResizeHeight);
        }

        return (maxWidth, maxHeight);
    }

    private static List<ReplayFrame> TrimTrailingBlankRestore(
        List<ReplayFrame> frames,
        IReadOnlyList<CastEvent> events)
    {
        if (frames.Count <= 1)
            return frames;

        var lastNonBlank = -1;
        for (var i = frames.Count - 1; i >= 0; i--)
        {
            if (!frames[i].IsBlank)
            {
                lastNonBlank = i;
                break;
            }
        }

        if (lastNonBlank < 0 || lastNonBlank == frames.Count - 1)
            return frames;

        if (!HasTrailingBlankIndicators(events, FindEventIndexAtOrAfter(events, frames[lastNonBlank + 1].Time)))
            return frames;

        return frames.Take(lastNonBlank + 1).ToList();
    }

    private static bool IsBlankBuffer(ScreenBuffer buffer)
    {
        for (var row = 0; row < buffer.Height; row++)
        {
            for (var col = 0; col < buffer.Width; col++)
            {
                var cell = buffer.GetCell(row, col);
                if (cell.Text != Space || cell.IsWide || cell.IsWideContinuation)
                    return false;

                if (cell.Style != buffer.DefaultStyle)
                    return false;
            }
        }

        return true;
    }

    private static int FindEventIndexAtOrAfter(IReadOnlyList<CastEvent> events, double time)
    {
        var index = 0;
        while (index < events.Count && events[index].Time < time)
            index++;

        return index;
    }

    private static bool OutputContainsAnsiBlankIndicator(CastEvent ev)
    {
        if (ev.HasUtf8Output)
        {
            var span = ev.Utf8Output.Span;
            return Utf8Contains(span, "\u001b[?1049l"u8)
                || Utf8Contains(span, "\u001b[?47l"u8)
                || Utf8Contains(span, "\u001b[?1047l"u8)
                || Utf8Contains(span, "\u001b[2J"u8)
                || Utf8Contains(span, "\u001b[J"u8)
                || Utf8Contains(span, "\u001b[H"u8)
                || Utf8Contains(span, "\u001b[;H"u8);
        }

        var data = ev.Data;
        return data.Contains("\u001b[?1049l", StringComparison.Ordinal)
            || data.Contains("\u001b[?47l", StringComparison.Ordinal)
            || data.Contains("\u001b[?1047l", StringComparison.Ordinal)
            || data.Contains("\u001b[2J", StringComparison.Ordinal)
            || data.Contains("\u001b[J", StringComparison.Ordinal)
            || data.Contains("\u001b[H", StringComparison.Ordinal)
            || data.Contains("\u001b[;H", StringComparison.Ordinal);
    }

    private static bool Utf8Contains(ReadOnlySpan<byte> haystack, ReadOnlySpan<byte> needle) =>
        haystack.IndexOf(needle) >= 0;

    private static bool HasTrailingBlankIndicators(IReadOnlyList<CastEvent> events, int startIndex)
    {
        for (var i = startIndex; i < events.Count; i++)
        {
            if (events[i].Kind != CastEventKind.Output)
                continue;

            if (OutputContainsAnsiBlankIndicator(events[i]))
            {
                return true;
            }
        }

        return false;
    }
}

internal static class InteractivePtyCastEmit
{
    private const double InputEchoGraceSeconds = 0.45;
    private const double InputEchoMergeGapSeconds = 0.12;

    internal static List<(double Start, double End)> BuildInputWindows(IReadOnlyList<double> inputTimes) =>
        BuildInputEchoWindows(inputTimes);

    internal static List<(double Start, double End)> BuildInputEchoWindows(IReadOnlyList<double> inputTimes)
    {
        if (inputTimes.Count == 0)
            return [];

        var sorted = inputTimes.ToArray();
        Array.Sort(sorted);

        var windows = new List<(double Start, double End)>();
        foreach (var t in sorted)
        {
            var end = t + InputEchoGraceSeconds;
            if (windows.Count > 0 && t <= windows[^1].End + InputEchoMergeGapSeconds)
                windows[^1] = (windows[^1].Start, Math.Max(windows[^1].End, end));
            else
                windows.Add((t, end));
        }

        return windows;
    }

    internal static bool IsInInputWindow(double seconds, IReadOnlyList<(double Start, double End)> windows)
    {
        for (var i = 0; i < windows.Count; i++)
        {
            var (start, end) = windows[i];
            if (seconds >= start && seconds <= end)
                return true;
        }

        return false;
    }

    internal static List<(double Time, double OriginalTime, ReadOnlyMemory<byte> Data)> NormalizeOutputChunks(
        IReadOnlyList<(double Time, ReadOnlyMemory<byte> Data)> outputChunks,
        IReadOnlyList<(double Start, double End)> inputWindows,
        double typingSpeed,
        int width,
        int height,
        IReadOnlyList<(double Time, ReadOnlyMemory<byte> Data)>? inputChunks = null)
    {
        _ = width;
        _ = height;
        if (outputChunks.Count == 0 || inputWindows.Count == 0)
        {
            return outputChunks
                .Select(c => (c.Time, c.Time, c.Data))
                .ToList();
        }

        var committedEcho = inputChunks is { Count: > 0 }
            ? BuildCommittedTypingEcho(inputChunks)
            : null;

        var result = new List<(double Time, double OriginalTime, ReadOnlyMemory<byte> Data)>(outputChunks.Count);
        var emitClock = 0.0;
        var inTypingBurst = false;
        var committedIndex = 0;

        foreach (var (chunkTime, data) in outputChunks)
        {
            if (ShouldPassthroughWallClock(chunkTime, data.Span, inputWindows))
            {
                inTypingBurst = false;
                var emitTime = result.Count > 0 ? Math.Max(result[^1].Time, chunkTime) : chunkTime;
                emitClock = Math.Max(emitClock, emitTime);
                result.Add((emitTime, chunkTime, data));
                continue;
            }

            if (committedEcho is not null)
            {
                if (IsBackspaceEcho(data.Span))
                    continue;

                if (committedIndex >= committedEcho.Count
                    || !EchoMatchesCommitted(data.Span, committedEcho[committedIndex].Data.Span))
                {
                    continue;
                }

                committedIndex++;
            }

            if (!inTypingBurst)
            {
                emitClock = Math.Max(emitClock, chunkTime);
                inTypingBurst = true;
            }

            result.Add((emitClock, chunkTime, data));
            emitClock += typingSpeed * CountTypingUnits(data.Span);
        }

        return result;
    }

    internal static void CollapseIdleAfterNormalize(
        List<(double Time, double OriginalTime, ReadOnlyMemory<byte> Data)> chunks,
        double silenceThreshold,
        double maxIdle)
    {
        if (chunks.Count <= 1 || silenceThreshold < 0 || maxIdle < 0)
            return;

        var count = chunks.Count;
        var emitTimes = new double[count];
        var originalTimes = new double[count];
        for (var i = 0; i < count; i++)
        {
            emitTimes[i] = chunks[i].Time;
            originalTimes[i] = chunks[i].OriginalTime;
        }

        CastTimingPipeline.CollapseIdleGapsWithOriginal(emitTimes, originalTimes, silenceThreshold, maxIdle);
        for (var i = 0; i < count; i++)
            chunks[i] = (emitTimes[i], chunks[i].OriginalTime, chunks[i].Data);
    }

    private static List<(double Time, ReadOnlyMemory<byte> Data)> BuildCommittedTypingEcho(
        IReadOnlyList<(double Time, ReadOnlyMemory<byte> Data)> inputChunks)
    {
        var committed = new List<(double Time, ReadOnlyMemory<byte> Data)>();
        var ordered = inputChunks.ToArray();
        Array.Sort(ordered, (a, b) => a.Time.CompareTo(b.Time));

        foreach (var (time, data) in ordered)
        {
            AppendInputKeys(data.Span, committed, time);
        }

        return committed;
    }

    private static void AppendInputKeys(
        ReadOnlySpan<byte> data,
        List<(double Time, ReadOnlyMemory<byte> Data)> committed,
        double time)
    {
        for (var i = 0; i < data.Length; i++)
        {
            var b = data[i];
            if (b == 0x1b)
            {
                i += SkipEscapeSequence(data, i + 1);
                continue;
            }

            if (b is 0x08 or 0x7f)
            {
                if (committed.Count > 0)
                    committed.RemoveAt(committed.Count - 1);
                continue;
            }

            if (b is 0x0d or 0x0a or 0x07 or 0x09)
                continue;

            if (b < 0x20)
                continue;

            if (b <= 0x7f)
            {
                committed.Add((time, new[] { b }.AsMemory()));
                continue;
            }

            var remaining = data.Slice(i);
            if (Rune.DecodeFromUtf8(remaining, out _, out var consumed) != OperationStatus.Done || consumed <= 0)
                continue;

            var bytes = new byte[consumed];
            remaining[..consumed].CopyTo(bytes);
            committed.Add((time, bytes.AsMemory()));
            i += consumed - 1;
        }
    }

    private static bool EchoMatchesCommitted(ReadOnlySpan<byte> echo, ReadOnlySpan<byte> committed) =>
        echo.Length == committed.Length && echo.SequenceEqual(committed);

    private static bool IsBackspaceEcho(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
            return false;

        var hasErase = false;
        foreach (var b in data)
        {
            if (b is 0x08 or 0x7f)
                hasErase = true;
            else if (b is not ((byte)' ' or 0x09))
                return false;
        }

        return hasErase;
    }

    private static int SkipEscapeSequence(ReadOnlySpan<byte> data, int index)
    {
        if ((uint)index >= (uint)data.Length)
            return 0;

        if (data[index] == (byte)'O' && index + 1 < data.Length)
            return 2;

        if (data[index] == (byte)'[')
        {
            var i = index + 1;
            while (i < data.Length && data[i] is >= (byte)0x30 and <= (byte)0x3f)
                i++;
            while (i < data.Length && data[i] is >= (byte)0x20 and <= (byte)0x2f)
                i++;
            if (i < data.Length)
                return i - index + 1;
        }

        return 1;
    }

    private static bool ShouldPassthroughWallClock(
        double chunkTime,
        ReadOnlySpan<byte> data,
        IReadOnlyList<(double Start, double End)> inputWindows) =>
        !IsInInputWindow(chunkTime, inputWindows)
        || ContainsEscape(data)
        || !IsLikelyTypingEcho(data);

    private static bool ContainsEscape(ReadOnlySpan<byte> data)
    {
        for (var i = 0; i < data.Length; i++)
        {
            if (data[i] == 0x1b)
                return true;
        }

        return false;
    }

    private static bool IsLikelyTypingEcho(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty || data.Length > 16)
            return false;

        for (var i = 0; i < data.Length; i++)
        {
            var b = data[i];
            if (b is 0x08 or 0x7f or 0x09)
                continue;
            if (b is 0x0a or 0x0d or 0x07)
                return false;
            if (b >= 0x20)
                continue;
            return false;
        }

        return true;
    }

    private static double CountTypingUnits(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
            return 0;

        var units = 0;
        var remaining = data;
        while (!remaining.IsEmpty)
        {
            if (remaining[0] <= 0x7F)
            {
                units++;
                remaining = remaining[1..];
                continue;
            }

            if (Rune.DecodeFromUtf8(remaining, out _, out var consumed) != OperationStatus.Done || consumed <= 0)
                break;

            units++;
            remaining = remaining[consumed..];
        }

        return units;
    }
}

/// <summary>
/// Resolves the colors a renderer draws for a cell: reverse video, bold and faint intensity,
/// and the matrix-rain tint. Shared by the SVG and mp4 renderers so both agree.
/// </summary>
internal static class CellColors
{
    internal static string ResolveForeground(ScreenCell cell, TerminalTheme theme) =>
        ApplyIntensity(cell.Reversed ? cell.Background : cell.Foreground, cell.Bold, cell.Faint);

    /// <summary>
    /// Foreground with the matrix-rain tint: ANSI bright white next to green on the same
    /// row draws as bright green (see spec_svg.md).
    /// </summary>
    internal static string ResolveTintedForeground(
        ScreenBuffer buffer, int row, int col, ScreenCell cell, TerminalTheme theme)
    {
        var fg = ResolveForeground(cell, theme);
        var white = theme.AnsiPalette[7];
        if (fg != white && !string.Equals(fg, white, StringComparison.OrdinalIgnoreCase))
            return fg;

        return IsNearGreen(buffer, row, col, theme) ? theme.AnsiPalette[10] : fg;
    }

    private static bool IsNearGreen(ScreenBuffer buffer, int row, int col, TerminalTheme theme)
    {
        var green = theme.AnsiPalette[2];
        var brightGreen = theme.AnsiPalette[10];

        bool GreenAt(int r, int c)
        {
            var neighbor = buffer.GetCell(r, c);
            var fg = neighbor.Reversed ? neighbor.Background : neighbor.Foreground;
            return fg == green || fg == brightGreen
                || string.Equals(fg, green, StringComparison.OrdinalIgnoreCase)
                || string.Equals(fg, brightGreen, StringComparison.OrdinalIgnoreCase);
        }

        var width = buffer.Width;
        return (col > 0 && GreenAt(row, col - 1))
            || (col + 1 < width && GreenAt(row, col + 1));
    }

    /// <summary>The cell background, or null when it is the theme background (nothing to draw).</summary>
    internal static string? ResolveBackground(ScreenCell cell, TerminalTheme theme)
    {
        var bg = cell.Reversed ? cell.Foreground : cell.Background;
        return string.Equals(bg, theme.Background, StringComparison.OrdinalIgnoreCase) ? null : bg;
    }

    internal static string ApplyIntensity(string color, bool bold, bool faint)
    {
        var factor = 1d;
        if (bold) factor *= 1.2d;
        if (faint) factor *= 0.75d;
        if (Math.Abs(factor - 1d) < 0.0001d)
            return color;

        if (!TryParseHexColor(color, out var r, out var g, out var b))
            return color;

        return string.Create(
            CultureInfo.InvariantCulture,
            $"#{(int)Math.Clamp(Math.Round(r * factor), 0, 255):x2}{(int)Math.Clamp(Math.Round(g * factor), 0, 255):x2}{(int)Math.Clamp(Math.Round(b * factor), 0, 255):x2}");
    }

    internal static bool TryParseHexColor(string color, out int r, out int g, out int b)
    {
        r = g = b = 0;
        if (color.Length != 7 || color[0] != '#'
            || !TryHexNibble(color[1], out var r0) || !TryHexNibble(color[2], out var r1)
            || !TryHexNibble(color[3], out var g0) || !TryHexNibble(color[4], out var g1)
            || !TryHexNibble(color[5], out var b0) || !TryHexNibble(color[6], out var b1))
        {
            return false;
        }

        r = (r0 << 4) | r1;
        g = (g0 << 4) | g1;
        b = (b0 << 4) | b1;
        return true;
    }

    private static bool TryHexNibble(char c, out int value)
    {
        value = c switch
        {
            >= '0' and <= '9' => c - '0',
            >= 'A' and <= 'F' => c - 'A' + 10,
            >= 'a' and <= 'f' => c - 'a' + 10,
            _ => -1
        };
        return value >= 0;
    }
}
