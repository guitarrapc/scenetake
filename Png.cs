using System.Buffers.Binary;
using System.IO.Compression;

internal sealed record PngResult(int Width, int Height, int Colors, long Bytes, FontFaceInfo Font);

/// <summary>
/// A still PNG of one screen of a recording: the screen an animated SVG shows at a given time, drawn by the mp4's
/// rasterizer, so it looks like that frame of an mp4. It needs no ffmpeg, except to decode a background image.
/// </summary>
internal static class PngRender
{
    /// <summary>The drawn frame: the same as an mp4 frame without <c>--size</c>.</summary>
    internal static RasterLayout CreateLayout(ResolvedRenderSettings render, int cols, int rows) =>
        RasterLayout.Create(render.FontSize, cols, rows, render.Window, render.Padding, render.Margin, render.Background is not null, png: true);

    /// <summary>
    /// Renders the screen shown at <paramref name="at"/> seconds (<see cref="double.PositiveInfinity"/> for the final
    /// screen) and writes it to <paramref name="outputPath"/>. The file is written only after the image is encoded,
    /// so a failure leaves none behind. Every font file it loaded is released when it returns or throws, as for mp4.
    /// </summary>
    /// <param name="ffmpegPath">ffmpeg, which decodes a background image. Needed only when the background is an image.</param>
    internal static PngResult WritePng(
        IReadOnlyList<CastEvent> events,
        int width,
        int height,
        ResolvedRenderSettings render,
        double at,
        string outputPath,
        string? fontFile = null,
        string? ffmpegPath = null)
    {
        try
        {
            var (png, result) = Render(events, width, height, render, at, fontFile, ffmpegPath);
            File.WriteAllBytes(outputPath, png);
            return result;
        }
        finally
        {
            LoadedFace.ReleaseAll();
        }
    }

    /// <summary>The encoded PNG and its line for the log. The caller releases the fonts.</summary>
    internal static (byte[] Png, PngResult Result) Render(
        IReadOnlyList<CastEvent> events,
        int width,
        int height,
        ResolvedRenderSettings render,
        double at,
        string? fontFile = null,
        string? ffmpegPath = null)
    {
        var (cols, rows) = TerminalReplay.ResolveCanvasSize(width, height, events);
        var theme = TerminalTheme.FromResolved(render.Theme);
        var layout = CreateLayout(render, cols, rows);
        var glyphs = GlyphSource.Create(render.FontFamily, fontFile, layout);
        var desktop = render.Background is { } background
            ? DesktopRaster.Render(layout, background, (frameWidth, frameHeight) => Ffmpeg.DecodeImage(
                ffmpegPath ?? throw new InvalidOperationException("a background image needs ffmpeg to decode it"), background.ImagePath!, frameWidth, frameHeight))
            : null;
        var titleGlyphs = layout.Window != WindowStyle.None && !string.IsNullOrEmpty(render.WindowTitle)
            ? Mp4FrameRenderer.CreateTitleGlyphs(layout.Window, render.FontFamily, fontFile)
            : null;
        var raster = new FrameRasterizer(layout, theme, glyphs, desktop, titleGlyphs, render.WindowTitle, render.WindowOpacity, render.WindowBlur);

        // The same screen a still SVG shows. Before the first screen the terminal is empty, without a cursor.
        var frames = TerminalReplay.BuildFrames(events, width, height, cols, rows, theme, lastOfSameTime: true);
        var view = TerminalReplay.SelectStill(frames, at) is { } frame
            ? TerminalReplay.ReplayStill(events, width, height, cols, rows, theme, frame)
            : new ScreenBuffer(cols, rows, theme) { CursorVisible = false };
        raster.Draw(view);

        var png = PngEncoder.Encode(raster.Pixels, layout.Width, layout.Height, out var colors);
        return (png, new PngResult(layout.Width, layout.Height, colors, png.Length, glyphs.Primary));
    }
}

/// <summary>
/// Writes RGB24 pixels as a PNG. A terminal screen has few colors: text in a handful of theme colors, with the shades
/// that antialiasing adds. An image of 256 colors or fewer gets a palette, which is about a third of the size of RGB;
/// others are RGB, with the filter chosen per row. zlib comes from .NET.
/// </summary>
internal static class PngEncoder
{
    private const int MaxPaletteColors = 256;
    private static readonly uint[] CrcTable = BuildCrcTable();

    /// <param name="colors">The number of colors in the image, or 0 when it has more than 256.</param>
    internal static byte[] Encode(ReadOnlySpan<byte> rgb, int width, int height, out int colors)
    {
        if (width <= 0 || height <= 0 || rgb.Length != (long)width * height * 3)
            throw new ArgumentException($"expected {width}x{height} RGB24 pixels, got {rgb.Length} bytes");

        using var output = new MemoryStream();
        output.Write([0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A]);

        var palette = TryIndex(rgb, out var indices);
        colors = palette?.Count ?? 0;
        var bitDepth = palette is null ? 8 : palette.Count switch { <= 2 => 1, <= 4 => 2, <= 16 => 4, _ => 8 };

        Span<byte> header = stackalloc byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, width);
        BinaryPrimitives.WriteInt32BigEndian(header[4..], height);
        header[8] = (byte)bitDepth;
        header[9] = palette is null ? (byte)2 : (byte)3; // RGB, or palette indices
        // header[10..13]: deflate, adaptive filtering, no interlace
        WriteChunk(output, "IHDR"u8, header);

        if (palette is not null)
        {
            var entries = new byte[palette.Count * 3];
            for (var i = 0; i < palette.Count; i++)
            {
                entries[i * 3] = (byte)(palette[i] >> 16);
                entries[i * 3 + 1] = (byte)(palette[i] >> 8);
                entries[i * 3 + 2] = (byte)palette[i];
            }

            WriteChunk(output, "PLTE"u8, entries);
        }

        using (var data = new MemoryStream())
        {
            using (var zlib = new ZLibStream(data, CompressionLevel.SmallestSize, leaveOpen: true))
            {
                if (palette is null)
                    WriteFilteredRgbRows(zlib, rgb, width, height);
                else
                    WritePackedRows(zlib, indices!, width, height, bitDepth);
            }

            WriteChunk(output, "IDAT"u8, data.GetBuffer().AsSpan(0, (int)data.Length));
        }

        WriteChunk(output, "IEND"u8, []);
        return output.ToArray();
    }

    /// <summary>
    /// Each pixel's index in the palette, in the order colors first appear, or null when there are more than 256
    /// colors. The order makes the output the same for the same pixels.
    /// </summary>
    private static List<int>? TryIndex(ReadOnlySpan<byte> rgb, out byte[]? indices)
    {
        var palette = new List<int>();
        var lookup = new Dictionary<int, byte>();
        indices = new byte[rgb.Length / 3];
        var last = -1;
        byte lastIndex = 0;
        for (int i = 0, p = 0; i < rgb.Length; i += 3, p++)
        {
            var color = (rgb[i] << 16) | (rgb[i + 1] << 8) | rgb[i + 2];
            if (color != last)
            {
                if (!lookup.TryGetValue(color, out lastIndex))
                {
                    if (palette.Count == MaxPaletteColors)
                    {
                        indices = null;
                        return null;
                    }

                    lastIndex = (byte)palette.Count;
                    lookup.Add(color, lastIndex);
                    palette.Add(color);
                }

                last = color;
            }

            indices[p] = lastIndex;
        }

        return palette;
    }

    /// <summary>Palette rows packed at <paramref name="bitDepth"/>, without filters, as the PNG specification advises for palette images.</summary>
    private static void WritePackedRows(Stream zlib, byte[] indices, int width, int height, int bitDepth)
    {
        var row = new byte[1 + (width * bitDepth + 7) / 8];
        var perByte = 8 / bitDepth;
        for (var y = 0; y < height; y++)
        {
            Array.Clear(row);
            var source = indices.AsSpan(y * width, width);
            for (var x = 0; x < width; x++)
            {
                var shift = 8 - bitDepth * (x % perByte + 1);
                row[1 + x / perByte] |= (byte)(source[x] << shift);
            }

            zlib.Write(row);
        }
    }

    /// <summary>
    /// RGB rows, each with the filter whose output has the smallest sum of absolute values: the usual heuristic, which
    /// suits flat backgrounds and runs of text.
    /// </summary>
    private static void WriteFilteredRgbRows(Stream zlib, ReadOnlySpan<byte> rgb, int width, int height)
    {
        const int Bpp = 3;
        var stride = width * Bpp;
        var candidates = new byte[5][];
        for (var f = 0; f < candidates.Length; f++)
            candidates[f] = new byte[1 + stride];

        var zeros = new byte[stride];
        for (var y = 0; y < height; y++)
        {
            var row = rgb.Slice(y * stride, stride);
            ReadOnlySpan<byte> up = y == 0 ? zeros : rgb.Slice((y - 1) * stride, stride);
            var best = 0;
            var bestSum = long.MaxValue;
            for (var filter = 0; filter < candidates.Length; filter++)
            {
                var output = candidates[filter];
                output[0] = (byte)filter;
                long sum = 0;
                for (var i = 0; i < stride; i++)
                {
                    var left = i >= Bpp ? row[i - Bpp] : 0;
                    var above = up[i];
                    var upperLeft = i >= Bpp ? up[i - Bpp] : 0;
                    var predicted = filter switch
                    {
                        1 => left,
                        2 => above,
                        3 => (left + above) >> 1,
                        4 => Paeth(left, above, upperLeft),
                        _ => 0,
                    };
                    var value = (byte)(row[i] - predicted);
                    output[1 + i] = value;
                    sum += value < 128 ? value : 256 - value;
                }

                if (sum < bestSum)
                {
                    bestSum = sum;
                    best = filter;
                }
            }

            zlib.Write(candidates[best]);
        }
    }

    private static int Paeth(int left, int above, int upperLeft)
    {
        var estimate = left + above - upperLeft;
        var toLeft = Math.Abs(estimate - left);
        var toAbove = Math.Abs(estimate - above);
        var toUpperLeft = Math.Abs(estimate - upperLeft);
        if (toLeft <= toAbove && toLeft <= toUpperLeft)
            return left;
        return toAbove <= toUpperLeft ? above : upperLeft;
    }

    private static void WriteChunk(Stream output, ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        Span<byte> number = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(number, data.Length);
        output.Write(number);
        output.Write(type);
        output.Write(data);
        var crc = UpdateCrc(UpdateCrc(0xFFFFFFFFu, type), data) ^ 0xFFFFFFFFu;
        BinaryPrimitives.WriteUInt32BigEndian(number, crc);
        output.Write(number);
    }

    internal static uint Crc32(ReadOnlySpan<byte> data) => UpdateCrc(0xFFFFFFFFu, data) ^ 0xFFFFFFFFu;

    private static uint UpdateCrc(uint crc, ReadOnlySpan<byte> data)
    {
        foreach (var b in data)
            crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        return crc;
    }

    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            var c = n;
            for (var k = 0; k < 8; k++)
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            table[n] = c;
        }

        return table;
    }
}
