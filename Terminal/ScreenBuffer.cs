internal readonly record struct CellStyle(
    string Foreground,
    string Background,
    bool Bold,
    bool Italic,
    bool Underline,
    bool Reversed = false,
    bool Faint = false)
{
    internal static CellStyle Default(TerminalTheme theme) =>
        new(theme.Foreground, theme.Background, false, false, false);
}

internal readonly struct ScreenCell
{
    internal ScreenCell(
        string text,
        CellStyle style,
        bool isWide = false,
        bool isWideContinuation = false)
    {
        Text = text;
        Foreground = style.Foreground;
        Background = style.Background;
        Bold = style.Bold;
        Italic = style.Italic;
        Underline = style.Underline;
        Reversed = style.Reversed;
        Faint = style.Faint;
        IsWide = isWide;
        IsWideContinuation = isWideContinuation;
    }

    internal string Text { get; }
    internal string Foreground { get; }
    internal string Background { get; }
    internal bool Bold { get; }
    internal bool Italic { get; }
    internal bool Underline { get; }
    internal bool Reversed { get; }
    internal bool Faint { get; }
    internal bool IsWide { get; }
    internal bool IsWideContinuation { get; }

    internal CellStyle ToStyle() =>
        new(Foreground, Background, Bold, Italic, Underline, Reversed, Faint);
}

internal sealed class ScreenBuffer
{
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
        if (row < 0 || row >= Height || col < 0 || col >= Width)
            return BlankCell();

        return _cells[row, col];
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
                    PutPrintable(" ", style);
                return;
            }
            default:
                if (char.IsControl(value))
                    return;
                PutPrintable(value.ToString(), style);
                return;
        }
    }

    internal void PutSurrogatePair(string cluster, CellStyle style) => PutPrintable(cluster, style);

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
        _cells[row, col + 1] = new ScreenCell(" ", cell.ToStyle(), isWideContinuation: true);

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

        var isWide = IsWideCharacter(text);

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
            _cells[CursorRow, CursorCol] = new ScreenCell(" ", style, false, true);
            CursorCol++;
        }

        if (CursorCol >= Width)
        {
            _pendingWrap = true;
            CursorCol = Width - 1;
        }
    }

    private static bool IsWideCharacter(string text)
    {
        if (string.IsNullOrEmpty(text))
            return false;

        int codePoint;
        if (text.Length >= 2 && char.IsHighSurrogate(text[0]) && char.IsLowSurrogate(text[1]))
            codePoint = char.ConvertToUtf32(text[0], text[1]);
        else if (text.Length == 1)
            codePoint = text[0];
        else
            return false;

        return IsEastAsianWide(codePoint) || IsBmpEmojiWide(codePoint);
    }

    private static bool IsBmpEmojiWide(int cp) =>
        cp is 0x2611 or 0x2705 or 0x274C or 0x2753 or 0x2754 or 0x2755 or 0x2757;

    private static bool IsEastAsianWide(int cp) =>
        cp is (>= 0x1100 and <= 0x115F)
            or (>= 0x2E80 and <= 0x2FFD)
            or (>= 0x3000 and <= 0x303F)
            or (>= 0x3040 and <= 0x33FF)
            or (>= 0x3400 and <= 0x4DBF)
            or (>= 0x4E00 and <= 0x9FFF)
            or (>= 0xA000 and <= 0xA48C)
            or (>= 0xA960 and <= 0xA97F)
            or (>= 0xAC00 and <= 0xD7A3)
            or (>= 0xF900 and <= 0xFAFF)
            or (>= 0xFE10 and <= 0xFE1F)
            or (>= 0xFE30 and <= 0xFE6F)
            or (>= 0xFF01 and <= 0xFF60)
            or (>= 0xFFE0 and <= 0xFFE6)
            or (>= 0x1B000 and <= 0x1B0FF)
            or (>= 0x1F004 and <= 0x1F004)
            or (>= 0x1F0CF and <= 0x1F0CF)
            or (>= 0x1F200 and <= 0x1F2FF)
            or (>= 0x1F300 and <= 0x1F64F)
            or (>= 0x1F680 and <= 0x1F6FF)
            or (>= 0x1F900 and <= 0x1F9FF)
            or (>= 0x20000 and <= 0x2FFFD)
            or (>= 0x30000 and <= 0x3FFFD);

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

    private ScreenCell BlankCell() => new(" ", DefaultStyle);

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
        for (var row = 0; row < source.GetLength(0); row++)
        {
            for (var col = 0; col < source.GetLength(1); col++)
                cloned[row, col] = source[row, col];
        }

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
        var blank = new ScreenCell(" ", blankStyle);
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
