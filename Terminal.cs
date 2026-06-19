using System.Globalization;

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
    private int _scrollTop;
    private int _scrollBottom;
    private bool _pendingWrap;
    private readonly List<ScreenCell[]> _scrollbackRows = [];

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
    internal int ScrollbackCount => _scrollbackRows.Count;

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
            ScrollRegionUp(
                _scrollTop,
                _scrollBottom,
                1,
                includeScrollback: !_isAltScreen && _scrollTop == 0 && _scrollBottom == Height - 1);
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
        ScrollRegionUp(
            _scrollTop,
            _scrollBottom,
            count,
            includeScrollback: !_isAltScreen && _scrollTop == 0 && _scrollBottom == Height - 1);

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
            _altCells = CreateBlankCells();
            _cells = _altCells;
            CursorRow = 0;
            CursorCol = 0;
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

    private void ScrollRegionUp(int top, int bottom, int count, bool includeScrollback)
    {
        if (count <= 0 || top < 0 || bottom >= Height || top > bottom)
            return;

        count = Math.Min(count, bottom - top + 1);
        for (var i = 0; i < count; i++)
        {
            if (includeScrollback && top == 0)
            {
                var topRow = new ScreenCell[Width];
                for (var col = 0; col < Width; col++)
                    topRow[col] = _cells[0, col];
                _scrollbackRows.Add(topRow);
            }

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
}

internal sealed class AnsiParser
{
    private const int MissingParameter = int.MinValue;

    private readonly ScreenBuffer _buffer;
    private readonly TerminalTheme _theme;
    private readonly List<int> _csiParams = new(8);
    private CellStyle _style;
    private string _pendingEscape = "";
    private string _pendingCaret = "";
    [ThreadStatic] private static char[]? t_pendingBuf;

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

        if (_pendingEscape.Length > 0)
        {
            text = MergePending(_pendingEscape, text);
            _pendingEscape = "";
        }

        if (_pendingCaret.Length > 0)
        {
            text = MergePending(_pendingCaret, text);
            _pendingCaret = "";
        }

        ProcessCore(text);
    }

    private static string MergePending(string pending, string text)
    {
        var len = pending.Length + text.Length;
        var buf = t_pendingBuf ??= new char[256];
        if (len > buf.Length)
            buf = t_pendingBuf = new char[len];
        pending.AsSpan().CopyTo(buf);
        text.AsSpan().CopyTo(buf.AsSpan(pending.Length));
        return new string(buf, 0, len);
    }

    private void ProcessCore(string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (ch == '\u001b')
            {
                if (!TryHandleEscape(text, i, out var escapeEndIndex))
                {
                    _pendingEscape = text[i..];
                    break;
                }

                i = escapeEndIndex;
                continue;
            }

            if (ch == '^' && i + 2 < text.Length && text[i + 1] == '[' && text[i + 2] == ']')
            {
                if (!TrySkipOsc(text, i + 3, out var oscEnd, caretTerminator: true))
                {
                    _pendingCaret = text[i..];
                    break;
                }

                i = oscEnd;
                continue;
            }

            if (char.IsHighSurrogate(ch) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                _buffer.PutSurrogatePair(text[i], text[i + 1], _style);
                i++;
                continue;
            }

            if (char.IsLowSurrogate(ch))
                continue;

            if (IsVariationSelector(ch))
            {
                _buffer.AppendToPreviousCell(ScreenBuffer.CharStr(ch));
                continue;
            }

            if (IsZeroWidthChar(ch))
                continue;

            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category is UnicodeCategory.NonSpacingMark
                or UnicodeCategory.SpacingCombiningMark
                or UnicodeCategory.EnclosingMark)
            {
                _buffer.AppendToPreviousCell(ScreenBuffer.CharStr(ch));
                continue;
            }

            _buffer.PutChar(ch, _style);
        }
    }

    private static bool IsZeroWidthChar(char ch) =>
        ch is '\u00AD' or '\u200B' or '\u200C' or '\u200D' or '\uFEFF';

    private static bool IsVariationSelector(char ch) => ch is >= '\uFE00' and <= '\uFE0F';

    private bool TryHandleEscape(string text, int index, out int endIndex)
    {
        endIndex = index;
        if (index + 1 >= text.Length)
            return false;

        switch (text[index + 1])
        {
            case '[':
                return TryHandleCsi(text, index + 2, out endIndex);
            case ']':
                return TrySkipOsc(text, index + 2, out endIndex);
            case 'P':
                return TrySkipOsc(text, index + 2, out endIndex);
            case '(' or ')' or '*' or '+' or '-' or '.' or '/' or '#' or '%':
                if (index + 2 >= text.Length)
                    return false;
                endIndex = index + 2;
                return true;
            case '7':
                _buffer.SaveCursor();
                endIndex = index + 1;
                return true;
            case '8':
                _buffer.RestoreCursor();
                endIndex = index + 1;
                return true;
            case 'D':
                _buffer.Index();
                endIndex = index + 1;
                return true;
            case 'E':
                _buffer.NextLine();
                endIndex = index + 1;
                return true;
            case 'M':
                _buffer.ReverseIndex();
                endIndex = index + 1;
                return true;
            case 'c':
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

    private static bool TrySkipOsc(string text, int start, out int endIndex, bool caretTerminator = false)
    {
        endIndex = start;
        for (var i = start; i < text.Length; i++)
        {
            if (text[i] == '\a')
            {
                endIndex = i;
                return true;
            }

            if (caretTerminator && text[i] == '^' && i + 2 < text.Length && text[i + 1] == '[' && text[i + 2] == '\\')
            {
                endIndex = i + 2;
                return true;
            }

            if (text[i] == '\u001b' && i + 1 < text.Length && text[i + 1] == '\\')
            {
                endIndex = i + 1;
                return true;
            }
        }

        return false;
    }

    private bool TryHandleCsi(string text, int start, out int endIndex)
    {
        endIndex = text.Length - 1;
        char? privateMarker = null;
        var paramStart = start;
        if (start < text.Length && text[start] is '<' or '=' or '>' or '?')
        {
            privateMarker = text[start];
            paramStart++;
            start++;
        }

        var i = start;
        while (i < text.Length)
        {
            var c = text[i];
            if (c is >= '@' and <= '~')
            {
                var parameterText = paramStart <= i ? text.AsSpan(paramStart, i - paramStart) : ReadOnlySpan<char>.Empty;
                ApplyCsi(privateMarker, c, parameterText);
                endIndex = i;
                return true;
            }

            i++;
        }

        return false;
    }

    private void ApplyCsi(char? privateMarker, char command, ReadOnlySpan<char> parameterText)
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

    private void ParseParameters(ReadOnlySpan<char> parameterText)
    {
        _csiParams.Clear();
        if (parameterText.IsEmpty)
            return;

        var i = 0;
        while (i <= parameterText.Length)
        {
            var start = i;
            while (i < parameterText.Length && parameterText[i] is not ';' and not ':')
                i++;

            var part = parameterText[start..i];
            if (part.IsEmpty || part.IsWhiteSpace())
                _csiParams.Add(MissingParameter);
            else if (int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
                _csiParams.Add(value);
            else
                _csiParams.Add(MissingParameter);

            if (i >= parameterText.Length)
                break;

            i++;
        }
    }

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
    int ResizeHeight = 0)
{
    internal static CastEvent Output(double time, string data) => new(time, CastEventKind.Output, data);

    internal static CastEvent Resize(double time, int width, int height) =>
        new(time, CastEventKind.Resize, $"{width}x{height}", width, height);

    internal static CastEvent Marker(double time, string label) => new(time, CastEventKind.Marker, label);
}

internal sealed class ReplayFrame
{
    internal ReplayFrame(double time, ScreenBuffer buffer, int viewportWidth, int viewportHeight, ulong? signature = null)
    {
        Time = time;
        Buffer = buffer;
        ViewportWidth = viewportWidth;
        ViewportHeight = viewportHeight;
        Signature = signature ?? buffer.ComputeCanvasSignature(buffer.Width, buffer.Height);
    }

    internal double Time { get; }
    internal ScreenBuffer Buffer { get; }
    internal int ViewportWidth { get; }
    internal int ViewportHeight { get; }
    internal ulong Signature { get; }
}

internal static class TerminalReplay
{
    private const string Space = " ";
    internal const double CastExitIntervalSeconds = 0.05;

    internal static double ComputeLoopDuration(IReadOnlyList<CastEvent> events)
    {
        if (events.Count == 0)
            return 0;

        return events[^1].Time + CastExitIntervalSeconds;
    }

    internal static List<ReplayFrame> BuildFrames(
        IReadOnlyList<CastEvent> events,
        int initialWidth,
        int initialHeight,
        int canvasWidth,
        int canvasHeight,
        TerminalTheme theme)
    {
        var buffer = new ScreenBuffer(initialWidth, initialHeight, theme);
        var parser = new AnsiParser(buffer, theme);
        var frames = new List<ReplayFrame>();
        ulong? lastSignature = null;

        void CaptureIfChanged(double time)
        {
            var signature = buffer.ComputeCanvasSignature(canvasWidth, canvasHeight);
            if (lastSignature is ulong previous && previous == signature)
                return;

            frames.Add(new ReplayFrame(time, SnapshotForCanvas(buffer, canvasWidth, canvasHeight), buffer.Width, buffer.Height, signature));
            lastSignature = signature;
        }

        if (!IsBlankBuffer(buffer))
            CaptureIfChanged(0);

        foreach (var ev in events)
        {
            if (ev.Kind == CastEventKind.Marker)
                continue;

            if (ev.Kind == CastEventKind.Resize)
            {
                buffer.Resize(ev.ResizeWidth, ev.ResizeHeight);
                CaptureIfChanged(ev.Time);
                continue;
            }

            parser.Process(ev.Data);
            CaptureIfChanged(ev.Time);
        }

        if (frames.Count == 0)
            frames.Add(new ReplayFrame(0, SnapshotForCanvas(buffer, canvasWidth, canvasHeight), buffer.Width, buffer.Height));

        return TrimTrailingBlankRestore(frames, events);
    }

    private static ScreenBuffer SnapshotForCanvas(ScreenBuffer source, int canvasWidth, int canvasHeight)
    {
        var copy = source.Clone();
        if (copy.Width != canvasWidth || copy.Height != canvasHeight)
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
            if (!IsBlankBuffer(frames[i].Buffer))
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

    private static bool HasTrailingBlankIndicators(IReadOnlyList<CastEvent> events, int startIndex)
    {
        for (var i = startIndex; i < events.Count; i++)
        {
            if (events[i].Kind != CastEventKind.Output)
                continue;

            var data = events[i].Data;
            if (data.Contains("\u001b[?1049l", StringComparison.Ordinal)
                || data.Contains("\u001b[?47l", StringComparison.Ordinal)
                || data.Contains("\u001b[?1047l", StringComparison.Ordinal)
                || data.Contains("\u001b[2J", StringComparison.Ordinal)
                || data.Contains("\u001b[J", StringComparison.Ordinal)
                || data.Contains("\u001b[H", StringComparison.Ordinal)
                || data.Contains("\u001b[;H", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}

