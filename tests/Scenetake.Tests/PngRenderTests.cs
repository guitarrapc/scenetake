using System.Buffers.Binary;
using System.IO.Compression;
using Scenetake.TestSupport;

namespace Scenetake.Tests;

// Still images: the PNG encoder (decoded again here, so the tests compare pixels, not compressed bytes), the PNG of
// one screen, which must match the mp4 frame at the same time, and the still SVG. Tests that draw need one installed
// font. They skip when none is found.

public sealed class PngRenderTests
{
    [Test]
    public async Task Crc32MatchesTheStandardCheckValue() => await Assert.That(Skipping.Run(() => PngRenderCases.Crc32MatchesTheStandardCheckValue())).IsTrue();

    [Test]
    public async Task FewColorsUseAPaletteAtTheSmallestDepth() => await Assert.That(Skipping.Run(() => PngRenderCases.FewColorsUseAPaletteAtTheSmallestDepth())).IsTrue();

    [Test]
    public async Task PaletteRowsPackAtOddWidths() => await Assert.That(Skipping.Run(() => PngRenderCases.PaletteRowsPackAtOddWidths())).IsTrue();

    [Test]
    public async Task ManyColorsUseFilteredRgb() => await Assert.That(Skipping.Run(() => PngRenderCases.ManyColorsUseFilteredRgb())).IsTrue();

    [Test]
    public async Task EncoderRejectsAWrongPixelCount() => await Assert.That(Skipping.Run(() => PngRenderCases.EncoderRejectsAWrongPixelCount())).IsTrue();

    [Test]
    public async Task StillTimeParsing() => await Assert.That(Skipping.Run(() => PngRenderCases.StillTimeParsing())).IsTrue();

    [Test]
    public async Task StillSelectsTheLastScreenShownByThen() => await Assert.That(Skipping.Run(() => PngRenderCases.StillSelectsTheLastScreenShownByThen())).IsTrue();

    [Test]
    public async Task StillSvgHasNoAnimation() => await Assert.That(Skipping.Run(() => PngRenderCases.StillSvgHasNoAnimation())).IsTrue();

    [Test]
    public async Task StillSvgShowsTheScreenAtItsTime() => await Assert.That(Skipping.Run(() => PngRenderCases.StillSvgShowsTheScreenAtItsTime())).IsTrue();

    [Test]
    public async Task StillSvgBeforeTheFirstScreenIsEmpty() => await Assert.That(Skipping.Run(() => PngRenderCases.StillSvgBeforeTheFirstScreenIsEmpty())).IsTrue();

    [Test]
    public async Task PngMatchesTheMp4FrameAtTheSameTime() => await Assert.That(Skipping.Run(() => PngRenderCases.PngMatchesTheMp4FrameAtTheSameTime())).IsTrue();

    [Test]
    public async Task PngBeforeTheFirstScreenIsAnEmptyTerminal() => await Assert.That(Skipping.Run(() => PngRenderCases.PngBeforeTheFirstScreenIsAnEmptyTerminal())).IsTrue();

    [Test]
    public async Task PngDrawsWindowAndBackgroundLikeMp4() => await Assert.That(Skipping.Run(() => PngRenderCases.PngDrawsWindowAndBackgroundLikeMp4())).IsTrue();

    [Test]
    public async Task HugePngIsReportedAsAPng() => await Assert.That(Skipping.Run(() => PngRenderCases.HugePngIsReportedAsAPng())).IsTrue();

    [Test]
    public async Task FailedPngWritesNoFile() => await Assert.That(Skipping.Run(() => PngRenderCases.FailedPngWritesNoFile())).IsTrue();
}

internal static class PngRenderCases
{

    internal static bool Crc32MatchesTheStandardCheckValue() =>
        PngEncoder.Crc32("123456789"u8) == 0xCBF43926u && PngEncoder.Crc32([]) == 0;

    internal static bool FewColorsUseAPaletteAtTheSmallestDepth()
    {
        // 2, 4, 16 and 200 colors take 1, 2, 4 and 8 bits per pixel.
        var ok = true;
        foreach (var (colors, depth) in new[] { (2, 1), (4, 2), (16, 4), (200, 8) })
        {
            var rgb = Pixels(13, 17, (x, y) => ((x * 17 + y) % colors) * 41 % 256 << 16 | ((x * 17 + y) % colors));
            var png = PngEncoder.Encode(rgb, 13, 17, out var counted);
            var decoded = Decode(png);
            if (counted != colors || decoded.ColorType != 3 || decoded.BitDepth != depth || decoded.PaletteSize != colors || !decoded.Rgb.SequenceEqual(rgb))
            {
                Console.Error.WriteLine($"  {colors} colors: counted {counted}, type {decoded.ColorType}, depth {decoded.BitDepth}, palette {decoded.PaletteSize}, same {decoded.Rgb.SequenceEqual(rgb)}");
                ok = false;
            }
        }

        return ok;
    }

    internal static bool PaletteRowsPackAtOddWidths()
    {
        // A width that leaves part of the last byte of each row unused, at every packed depth.
        var ok = true;
        foreach (var width in new[] { 1, 3, 7, 9, 15 })
        {
            foreach (var colors in new[] { 2, 3, 11 })
            {
                var rgb = Pixels(width, 5, (x, y) => ((x + 2 * y) % colors) * 0x102030);
                var decoded = Decode(PngEncoder.Encode(rgb, width, 5, out _));
                if (!decoded.Rgb.SequenceEqual(rgb))
                {
                    Console.Error.WriteLine($"  width {width}, {colors} colors: pixels differ");
                    ok = false;
                }
            }
        }

        return ok;
    }

    internal static bool ManyColorsUseFilteredRgb()
    {
        // 64×64 with 4096 colors: RGB, and each row's filter is one of the five.
        var rgb = Pixels(64, 64, (x, y) => (x * 4) << 16 | (y * 4) << 8 | ((x ^ y) * 4 & 0xFF));
        var png = PngEncoder.Encode(rgb, 64, 64, out var colors);
        var decoded = Decode(png);
        return colors == 0 && decoded.ColorType == 2 && decoded.BitDepth == 8 && decoded.Rgb.SequenceEqual(rgb)
            && decoded.Filters.All(static f => f <= 4) && decoded.Filters.Distinct().Count() > 1;
    }

    internal static bool EncoderRejectsAWrongPixelCount()
    {
        try
        {
            PngEncoder.Encode(new byte[10], 2, 2, out _);
            return false;
        }
        catch (ArgumentException)
        {
            return true;
        }
    }

    internal static bool StillTimeParsing()
    {
        bool Parses(string text, double expected) => RenderSettingsResolver.TryParseStillAt(text, out var at, out _) && at.Equals(expected);
        bool Fails(string text) => !RenderSettingsResolver.TryParseStillAt(text, out _, out var error) && error.Contains("--at", StringComparison.Ordinal);
        return Parses("end", double.PositiveInfinity) && Parses("END", double.PositiveInfinity) && Parses("2.5", 2.5) && Parses("0", 0)
            && Parses(" 3 ", 3) && Parses("120", 120)
            && Fails("-1") && Fails("1e3") && Fails("abc") && Fails("") && Fails("NaN") && Fails("Infinity") && Fails("1,5")
            && RenderSettingsResolver.FormatStillAt(double.PositiveInfinity) == "final screen" && RenderSettingsResolver.FormatStillAt(2.5) == "at 2.5s";
    }

    internal static bool StillSelectsTheLastScreenShownByThen()
    {
        var frames = new List<ReplayFrame> { new(0.5, 4, 1, 1, false, 0), new(1.0, 4, 1, 2, false, 1), new(2.0, 4, 1, 3, false, 2) };
        return TerminalReplay.SelectStill(frames, 0.4) is null
            && TerminalReplay.SelectStill(frames, 0.5)?.EventIndex == 0
            && TerminalReplay.SelectStill(frames, 1.99)?.EventIndex == 1
            && TerminalReplay.SelectStill(frames, 2.0)?.EventIndex == 2
            && TerminalReplay.SelectStill(frames, 50)?.EventIndex == 2
            && TerminalReplay.SelectStill(frames, double.PositiveInfinity)?.EventIndex == 2
            && TerminalReplay.SelectStill([], double.PositiveInfinity) is null;
    }

    internal static bool StillSvgHasNoAnimation()
    {
        // Settings that shape the animation, a fade and playing once, change nothing in a still.
        var recording = Recording("[0.5,\"o\",\"one\\r\\n\"]\n[1.0,\"o\",\"two\\r\\n\"]\n[2.0,\"o\",\"three\"]");
        var render = recording.RenderSettings with { FadeOut = 2, Loop = false, EndHold = 5 };
        var svg = SvgRender.RenderToString(recording.Events, recording.Width, recording.Height, render, recording.LoopDuration, stillAt: double.PositiveInfinity);
        var animated = SvgRender.RenderToString(recording.Events, recording.Width, recording.Height, render, recording.LoopDuration);
        return !svg.Contains("animation", StringComparison.Ordinal) && !svg.Contains("@keyframes", StringComparison.Ordinal)
            && !svg.Contains("visibility", StringComparison.Ordinal) && !svg.Contains("class=\"fade\"", StringComparison.Ordinal)
            && svg.Contains(">one<", StringComparison.Ordinal) && svg.Contains(">two<", StringComparison.Ordinal) && svg.Contains(">three<", StringComparison.Ordinal)
            && animated.Contains("animation", StringComparison.Ordinal) && svg.Length < animated.Length;
    }

    internal static bool StillSvgShowsTheScreenAtItsTime()
    {
        // At 1.5 s the screen has "one" and "two"; "three" comes at 2 s.
        var recording = Recording("[0.5,\"o\",\"one\\r\\n\"]\n[1.0,\"o\",\"two\\r\\n\"]\n[2.0,\"o\",\"three\"]");
        var svg = SvgRender.RenderToString(recording.Events, recording.Width, recording.Height, recording.RenderSettings, recording.LoopDuration, stillAt: 1.5);
        return svg.Contains(">one<", StringComparison.Ordinal) && svg.Contains(">two<", StringComparison.Ordinal) && !svg.Contains("three", StringComparison.Ordinal);
    }

    internal static bool StillSvgBeforeTheFirstScreenIsEmpty()
    {
        var recording = Recording("[0.5,\"o\",\"one\"]");
        var svg = SvgRender.RenderToString(recording.Events, recording.Width, recording.Height, recording.RenderSettings, recording.LoopDuration, stillAt: 0.2);
        return !svg.Contains("one", StringComparison.Ordinal) && svg.StartsWith("<?xml", StringComparison.Ordinal);
    }

    internal static bool PngMatchesTheMp4FrameAtTheSameTime()
    {
        // mp4 frame k shows the events up to k / fps, so at 10 fps frame 10 is the screen at 1 s and the last frame the
        // final screen. Both are drawn by the same rasterizer from the same cells.
        var recording = Recording("[0.5,\"o\",\"\\u001b[31mone\\u001b[0m\\r\\n\"]\n[1.0,\"o\",\"two\\r\\n\"]\n[2.0,\"o\",\"three\"]");
        var frames = new List<byte[]>();
        var renderer = new Mp4FrameRenderer(recording.Events, recording.Width, recording.Height, recording.RenderSettings,
            Mp4Settings.Default with { Fps = 10 }, RenderSettingsResolver.ResolveLoopDuration(recording.LoopDuration, recording.Events, recording.RenderSettings));
        try
        {
            renderer.Render(pixels => frames.Add((byte[])pixels.Clone()));
            var (atOne, oneResult) = PngRender.Render(recording.Events, recording.Width, recording.Height, recording.RenderSettings, 1.0);
            var (final, finalResult) = PngRender.Render(recording.Events, recording.Width, recording.Height, recording.RenderSettings, double.PositiveInfinity);
            var layout = renderer.Layout;
            var ok = oneResult.Width == layout.Width && oneResult.Height == layout.Height
                && Decode(atOne).Rgb.SequenceEqual(frames[10]) && Decode(final).Rgb.SequenceEqual(frames[^1])
                && !Decode(atOne).Rgb.SequenceEqual(Decode(final).Rgb);
            if (!ok)
                Console.Error.WriteLine($"  png {oneResult.Width}x{oneResult.Height}, mp4 {layout.Width}x{layout.Height}, frames {frames.Count}");
            return ok;
        }
        finally
        {
            LoadedFace.ReleaseAll();
        }
    }

    internal static bool PngBeforeTheFirstScreenIsAnEmptyTerminal()
    {
        // Nothing is drawn before the first output, not even the cursor: one color, the theme background.
        var recording = Recording("[0.5,\"o\",\"one\"]");
        try
        {
            var (png, result) = PngRender.Render(recording.Events, recording.Width, recording.Height, recording.RenderSettings, 0.2);
            var decoded = Decode(png);
            return result.Colors == 1 && decoded.Rgb[0] == 0x28 && decoded.Rgb[1] == 0x2c && decoded.Rgb[2] == 0x34;
        }
        finally
        {
            LoadedFace.ReleaseAll();
        }
    }

    internal static bool PngDrawsWindowAndBackgroundLikeMp4()
    {
        // macOS chrome on a gradient: the PNG is the mp4's last frame, with the window inside the margin.
        var recording = Recording("[0.5,\"o\",\"hello\"]", "\"st:window=macos\",\"st:background=#1e3c72 #2a5298\"");
        var render = recording.RenderSettings;
        var frames = new List<byte[]>();
        var renderer = new Mp4FrameRenderer(recording.Events, recording.Width, recording.Height, render,
            Mp4Settings.Default with { Fps = 10 }, RenderSettingsResolver.ResolveLoopDuration(recording.LoopDuration, recording.Events, render));
        try
        {
            renderer.Render(pixels => frames.Add((byte[])pixels.Clone()));
            var (png, result) = PngRender.Render(recording.Events, recording.Width, recording.Height, render, double.PositiveInfinity);
            return render.Background is not null && render.Window == WindowStyle.Macos && renderer.Layout.Desktop
                && result.Width == renderer.Layout.Width && Decode(png).Rgb.SequenceEqual(frames[^1]);
        }
        finally
        {
            LoadedFace.ReleaseAll();
        }
    }

    internal static bool HugePngIsReportedAsAPng()
    {
        try
        {
            PngRender.CreateLayout(new ResolvedRenderSettings(128, "monospace", default), 512, 512);
            return false;
        }
        catch (FrameTooLargeException ex)
        {
            return ex.Message.StartsWith("a PNG of ", StringComparison.Ordinal) && !ex.Message.Contains("ffmpeg", StringComparison.Ordinal);
        }
    }

    internal static bool FailedPngWritesNoFile()
    {
        // A font file that does not exist fails before anything is drawn, and no PNG is left behind.
        var recording = Recording("[0.5,\"o\",\"one\"]");
        var path = Path.Combine(Path.GetTempPath(), $"scenetake-png-{Guid.NewGuid():N}.png");
        try
        {
            PngRender.WritePng(recording.Events, recording.Width, recording.Height, recording.RenderSettings, double.PositiveInfinity, path,
                fontFile: Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.ttf"));
            return false;
        }
        catch (Exception ex) when (ex is FontNotFoundException or IOException)
        {
            return !File.Exists(path);
        }
        finally
        {
            File.Delete(path);
        }
    }

    internal static CastRecording Recording(string events, string? tags = null)
    {
        var path = Path.Combine(Path.GetTempPath(), $"scenetake-png-{Guid.NewGuid():N}.cast");
        try
        {
            // These tests check the drawing at the terminal's full size, so they turn off the default fit (canvas_fit_test
            // checks fit).
            var tagsJson = $",\"tags\":[\"st:fit=none\"{(tags is null ? "" : "," + tags)}]";
            File.WriteAllText(path, "{\"version\":3,\"term\":{\"cols\":12,\"rows\":3,\"type\":\"xterm-256color\"}" + tagsJson + "}\n" + events + "\n");
            return CastReader.Read(path);
        }
        finally
        {
            File.Delete(path);
        }
    }

    internal static byte[] Pixels(int width, int height, Func<int, int, int> color)
    {
        var rgb = new byte[width * height * 3];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var c = color(x, y);
                var i = (y * width + x) * 3;
                rgb[i] = (byte)(c >> 16);
                rgb[i + 1] = (byte)(c >> 8);
                rgb[i + 2] = (byte)c;
            }
        }

        return rgb;
    }

    // A PNG decoder for the encoder's output: checks the signature and every chunk's CRC, inflates IDAT, reverses the
    // row filters, and expands palette indices to RGB.
    internal static (byte[] Rgb, int Width, int Height, int ColorType, int BitDepth, int PaletteSize, List<byte> Filters) Decode(byte[] png)
    {
        byte[] signature = [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A];
        if (!png.AsSpan(0, 8).SequenceEqual(signature))
            throw new InvalidDataException("bad signature");

        int width = 0, height = 0, bitDepth = 0, colorType = 0;
        byte[] palette = [];
        using var idat = new MemoryStream();
        var position = 8;
        var sawEnd = false;
        while (position < png.Length)
        {
            var length = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(position));
            var type = System.Text.Encoding.ASCII.GetString(png, position + 4, 4);
            var data = png.AsSpan(position + 8, length);
            var crc = BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(position + 8 + length));
            if (crc != PngEncoder.Crc32(png.AsSpan(position + 4, length + 4)))
                throw new InvalidDataException($"bad CRC in {type}");

            switch (type)
            {
                case "IHDR":
                    width = BinaryPrimitives.ReadInt32BigEndian(data);
                    height = BinaryPrimitives.ReadInt32BigEndian(data[4..]);
                    bitDepth = data[8];
                    colorType = data[9];
                    break;
                case "PLTE":
                    palette = data.ToArray();
                    break;
                case "IDAT":
                    idat.Write(data);
                    break;
                case "IEND":
                    sawEnd = true;
                    break;
            }

            position += 12 + length;
        }

        if (!sawEnd)
            throw new InvalidDataException("no IEND");

        idat.Position = 0;
        using var inflated = new MemoryStream();
        using (var zlib = new ZLibStream(idat, CompressionMode.Decompress))
            zlib.CopyTo(inflated);
        var raw = inflated.ToArray();

        var bpp = colorType == 2 ? 3 : 1;
        var stride = colorType == 2 ? width * 3 : (width * bitDepth + 7) / 8;
        var rows = new byte[height * stride];
        var filters = new List<byte>();
        for (var y = 0; y < height; y++)
        {
            var filter = raw[y * (stride + 1)];
            filters.Add(filter);
            for (var i = 0; i < stride; i++)
            {
                int value = raw[y * (stride + 1) + 1 + i];
                var left = i >= bpp ? rows[y * stride + i - bpp] : 0;
                var above = y > 0 ? rows[(y - 1) * stride + i] : 0;
                var upperLeft = i >= bpp && y > 0 ? rows[(y - 1) * stride + i - bpp] : 0;
                value += filter switch
                {
                    0 => 0,
                    1 => left,
                    2 => above,
                    3 => (left + above) >> 1,
                    4 => PaethPredictor(left, above, upperLeft),
                    _ => throw new InvalidDataException($"filter {filter}"),
                };
                rows[y * stride + i] = (byte)value;
            }
        }

        if (colorType == 2)
            return (rows, width, height, colorType, bitDepth, 0, filters);

        var rgb = new byte[width * height * 3];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var bit = x * bitDepth;
                var index = (rows[y * stride + bit / 8] >> (8 - bitDepth - bit % 8)) & ((1 << bitDepth) - 1);
                palette.AsSpan(index * 3, 3).CopyTo(rgb.AsSpan((y * width + x) * 3));
            }
        }

        return (rgb, width, height, colorType, bitDepth, palette.Length / 3, filters);
    }

    internal static int PaethPredictor(int a, int b, int c)
    {
        var p = a + b - c;
        var pa = Math.Abs(p - a);
        var pb = Math.Abs(p - b);
        var pc = Math.Abs(p - c);
        return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
    }
}
