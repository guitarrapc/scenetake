using System.Globalization;

internal sealed class AnsiParser
{
    private const int MissingParameter = int.MinValue;

    private readonly ScreenBuffer _buffer;
    private readonly TerminalTheme _theme;
    private CellStyle _style;
    private string _pendingEscapeSequence = "";
    private string _pendingCaretSequence = "";

    internal AnsiParser(ScreenBuffer buffer, TerminalTheme theme)
    {
        _buffer = buffer;
        _theme = theme;
        _style = _buffer.DefaultStyle;
    }

    internal void Process(string text)
    {
        if (string.IsNullOrEmpty(text))
            return;

        if (_pendingEscapeSequence.Length > 0)
        {
            text = _pendingEscapeSequence + text;
            _pendingEscapeSequence = "";
        }

        if (_pendingCaretSequence.Length > 0)
        {
            text = _pendingCaretSequence + text;
            _pendingCaretSequence = "";
        }

        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (ch == '\u001b')
            {
                if (!TryHandleEscape(text, i, out var escapeEndIndex))
                {
                    _pendingEscapeSequence = text[i..];
                    break;
                }

                i = escapeEndIndex;
                continue;
            }

            if (ch == '^' && i + 2 < text.Length && text[i + 1] == '[' && text[i + 2] == ']')
            {
                if (!TrySkipCaretOsc(text, i + 3, out var oscEnd))
                {
                    _pendingCaretSequence = text[i..];
                    break;
                }

                i = oscEnd;
                continue;
            }

            if (char.IsHighSurrogate(ch) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                _buffer.PutSurrogatePair(text.Substring(i, 2), _style);
                i++;
                continue;
            }

            if (char.IsLowSurrogate(ch))
                continue;

            if (IsVariationSelector(ch))
            {
                _buffer.AppendToPreviousCell(ch.ToString());
                continue;
            }

            if (IsZeroWidthChar(ch))
                continue;

            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category is UnicodeCategory.NonSpacingMark
                or UnicodeCategory.SpacingCombiningMark
                or UnicodeCategory.EnclosingMark)
            {
                _buffer.AppendToPreviousCell(ch.ToString());
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
                return TrySkipDcs(text, index + 2, out endIndex);
            case '(' or ')' or '*' or '+' or '-' or '.' or '/' or '#' or '%':
                return TrySkipEscSingleFinal(text, index + 2, out endIndex);
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

    private static bool TrySkipEscSingleFinal(string text, int start, out int endIndex)
    {
        endIndex = start;
        if (start >= text.Length)
            return false;

        endIndex = start;
        return true;
    }

    private static bool TrySkipOsc(string text, int start, out int endIndex)
    {
        endIndex = text.Length - 1;
        for (var i = start; i < text.Length; i++)
        {
            if (text[i] == '\a')
            {
                endIndex = i;
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

    private static bool TrySkipCaretOsc(string text, int start, out int endIndex)
    {
        endIndex = start;
        for (var i = start; i < text.Length; i++)
        {
            if (text[i] == '\a')
            {
                endIndex = i;
                return true;
            }

            if (text[i] == '^' && i + 2 < text.Length && text[i + 1] == '[' && text[i + 2] == '\\')
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

    private static bool TrySkipDcs(string text, int start, out int endIndex) =>
        TrySkipOsc(text, start, out endIndex);

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
                var parameterText = paramStart <= i ? text[paramStart..i] : "";
                ApplyCsi(privateMarker, c, ParseParameters(parameterText));
                endIndex = i;
                return true;
            }

            i++;
        }

        return false;
    }

    private void ApplyCsi(char? privateMarker, char command, List<int> parameters)
    {
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

    private static List<int> ParseParameters(string parameterText)
    {
        var result = new List<int>();
        if (string.IsNullOrEmpty(parameterText))
            return result;

        foreach (var part in parameterText.Split([';', ':']))
        {
            if (string.IsNullOrWhiteSpace(part))
            {
                result.Add(MissingParameter);
                continue;
            }

            if (int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
                result.Add(value);
            else
                result.Add(MissingParameter);
        }

        return result;
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
