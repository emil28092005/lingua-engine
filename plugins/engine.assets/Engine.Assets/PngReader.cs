using System.IO.Compression;

namespace Engine.Assets;

/// <summary>
/// Minimal PNG decoder — the inverse of Engine.Render's PngWriter, and
/// written for the same reason: SixLabors.ImageSharp's license isn't
/// MIT/Apache, so it's not something an MIT engine should depend on for
/// its own texture loading. Same deliberate subset as the encoder: 8-bit
/// RGBA (color type 6), no interlacing. Unlike the encoder, this decodes
/// all five PNG filter types (None/Sub/Up/Average/Paeth), not just
/// None — a real image tool exporting a texture won't necessarily pick
/// the same filter this codebase's own writer does, and rejecting
/// anything but self-produced files would defeat the point of loading
/// textures at all.
///
/// Not shared with Engine.Render's PngWriter as a common library: the two
/// plugins would gain a dependency on each other (or on a third one) for
/// a couple hundred lines neither owns conceptually — texture loading and
/// screenshot writing are different concerns that happen to touch the
/// same file format. Small, deliberate duplication over a premature
/// shared abstraction for two consumers.
/// </summary>
internal static class PngReader
{
    private static readonly byte[] Signature = [137, 80, 78, 71, 13, 10, 26, 10];
    private static readonly uint[] Crc32Table = BuildCrc32Table();

    public static (int Width, int Height, byte[] Rgba) Read(string path)
    {
        using var stream = File.OpenRead(path);

        Span<byte> signature = stackalloc byte[8];
        stream.ReadExactly(signature);
        if (!signature.SequenceEqual(Signature))
            throw new InvalidDataException($"'{path}' is not a PNG file.");

        var width = 0;
        var height = 0;
        var sawIhdr = false;
        using var idat = new MemoryStream();

        // Allocated once, outside the loop, and reused every iteration —
        // a PNG can carry an unbounded number of chunks (multiple IDATs
        // are routine for a large image), and a stackalloc that re-runs
        // per iteration doesn't free the previous one until this whole
        // method returns. The compiler's own CA2014 caught this — a real
        // stack-overflow risk on a large enough file, not a style nit.
        Span<byte> lengthBytes = stackalloc byte[4];
        Span<byte> typeBytes = stackalloc byte[4];
        Span<byte> crcBytes = stackalloc byte[4];

        while (true)
        {
            stream.ReadExactly(lengthBytes);
            var length = ReadUInt32BE(lengthBytes);

            stream.ReadExactly(typeBytes);
            var type = System.Text.Encoding.ASCII.GetString(typeBytes);

            var data = new byte[length];
            stream.ReadExactly(data);

            stream.ReadExactly(crcBytes);
            var expectedCrc = ReadUInt32BE(crcBytes);
            var actualCrc = Crc32(typeBytes, data);
            if (actualCrc != expectedCrc)
                throw new InvalidDataException($"'{path}': corrupt {type} chunk (CRC mismatch).");

            switch (type)
            {
                case "IHDR":
                    width = (int)ReadUInt32BE(data.AsSpan(0, 4));
                    height = (int)ReadUInt32BE(data.AsSpan(4, 4));
                    var bitDepth = data[8];
                    var colorType = data[9];
                    var interlace = data[12];
                    if (bitDepth != 8 || colorType != 6 || interlace != 0)
                    {
                        throw new NotSupportedException(
                            $"'{path}': only 8-bit non-interlaced RGBA PNGs are supported " +
                            $"(got bit depth {bitDepth}, color type {colorType}, interlace {interlace}).");
                    }

                    sawIhdr = true;
                    break;

                case "IDAT":
                    idat.Write(data);
                    break;

                case "IEND":
                    goto doneReadingChunks;
            }
        }

        doneReadingChunks:
        if (!sawIhdr)
            throw new InvalidDataException($"'{path}': no IHDR chunk found.");

        idat.Position = 0;
        using var zlib = new ZLibStream(idat, CompressionMode.Decompress);
        using var raw = new MemoryStream();
        zlib.CopyTo(raw);
        var scanlines = raw.ToArray();

        var rgba = Unfilter(scanlines, width, height);
        return (width, height, rgba);
    }

    private static byte[] Unfilter(byte[] scanlines, int width, int height)
    {
        const int bytesPerPixel = 4; // fixed: 8-bit RGBA, see the color type check above
        var stride = width * bytesPerPixel;
        var rawStride = stride + 1; // +1 filter-type byte per row
        var rgba = new byte[stride * height];

        for (var y = 0; y < height; y++)
        {
            var filterType = scanlines[y * rawStride];
            var rowStart = y * rawStride + 1;

            for (var x = 0; x < stride; x++)
            {
                var filtered = scanlines[rowStart + x];

                int a = x >= bytesPerPixel ? rgba[y * stride + x - bytesPerPixel] : 0;
                int b = y > 0 ? rgba[(y - 1) * stride + x] : 0;
                int c = x >= bytesPerPixel && y > 0 ? rgba[(y - 1) * stride + x - bytesPerPixel] : 0;

                int reconstructed = filterType switch
                {
                    0 => filtered,
                    1 => filtered + a,
                    2 => filtered + b,
                    3 => filtered + (a + b) / 2,
                    4 => filtered + Paeth(a, b, c),
                    _ => throw new NotSupportedException($"Unknown PNG filter type {filterType}."),
                };

                rgba[y * stride + x] = (byte)reconstructed;
            }
        }

        return rgba;
    }

    private static int Paeth(int a, int b, int c)
    {
        var p = a + b - c;
        var pa = Math.Abs(p - a);
        var pb = Math.Abs(p - b);
        var pc = Math.Abs(p - c);

        if (pa <= pb && pa <= pc)
            return a;

        return pb <= pc ? b : c;
    }

    private static uint ReadUInt32BE(ReadOnlySpan<byte> bytes) =>
        ((uint)bytes[0] << 24) | ((uint)bytes[1] << 16) | ((uint)bytes[2] << 8) | bytes[3];

    private static uint[] BuildCrc32Table()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            var c = n;
            for (var k = 0; k < 8; k++)
                c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
            table[n] = c;
        }

        return table;
    }

    private static uint Crc32(ReadOnlySpan<byte> type, byte[] data)
    {
        var crc = 0xFFFFFFFFu;

        foreach (var b in type)
            crc = Crc32Table[(crc ^ b) & 0xFF] ^ (crc >> 8);
        foreach (var b in data)
            crc = Crc32Table[(crc ^ b) & 0xFF] ^ (crc >> 8);

        return crc ^ 0xFFFFFFFFu;
    }
}
