using System.IO.Compression;
using System.Text;

namespace Engine.Render;

/// <summary>
/// Minimal PNG encoder: 8-bit RGBA, uncompressed-filter scanlines (filter
/// type 0, "None"), one IDAT chunk. No external dependency —
/// SixLabors.ImageSharp was considered and rejected: its license isn't
/// MIT or Apache (it's revenue-gated), and pulling that into an MIT
/// engine's own screenshot tooling would be exactly the kind of surprise
/// a downstream user shouldn't have to discover later. <see
/// cref="ZLibStream"/> (BCL, since .NET 6) does the one genuinely hard
/// part — a correctly zlib-wrapped DEFLATE stream — so what's left here
/// (chunk framing, CRC32) is small enough to get right and to verify by
/// actually opening a file this writes.
/// </summary>
internal static class PngWriter
{
    private static readonly byte[] Signature = [137, 80, 78, 71, 13, 10, 26, 10];
    private static readonly uint[] Crc32Table = BuildCrc32Table();

    /// <summary>
    /// <paramref name="topDownRgba"/> is <paramref name="width"/> *
    /// <paramref name="height"/> * 4 bytes, row 0 first (top of the
    /// image). OpenGL's ReadPixels returns rows bottom-first — flip
    /// before calling this, not after; this writer has no opinion about
    /// where the bytes came from.
    /// </summary>
    public static void Write(string path, int width, int height, ReadOnlySpan<byte> topDownRgba)
    {
        using var file = File.Create(path);
        file.Write(Signature);
        WriteChunk(file, "IHDR", BuildIhdr(width, height));
        WriteChunk(file, "IDAT", BuildIdat(width, height, topDownRgba));
        WriteChunk(file, "IEND", []);
    }

    private static byte[] BuildIhdr(int width, int height)
    {
        var ihdr = new byte[13];
        WriteUInt32BE(ihdr, 0, (uint)width);
        WriteUInt32BE(ihdr, 4, (uint)height);
        ihdr[8] = 8;  // bit depth
        ihdr[9] = 6;  // color type: RGBA
        ihdr[10] = 0; // compression method: deflate (the only one PNG defines)
        ihdr[11] = 0; // filter method: adaptive (per-scanline filter byte)
        ihdr[12] = 0; // interlace method: none
        return ihdr;
    }

    private static byte[] BuildIdat(int width, int height, ReadOnlySpan<byte> rgba)
    {
        var stride = width * 4;
        using var compressed = new MemoryStream();

        using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
        {
            var filterByte = new byte[1]; // 0 = "None" — every scanline, unfiltered
            for (var y = 0; y < height; y++)
            {
                zlib.Write(filterByte);
                zlib.Write(rgba.Slice(y * stride, stride));
            }
        }

        return compressed.ToArray();
    }

    private static void WriteChunk(Stream stream, string type, byte[] data)
    {
        var typeBytes = Encoding.ASCII.GetBytes(type);

        Span<byte> length = stackalloc byte[4];
        WriteUInt32BE(length, 0, (uint)data.Length);
        stream.Write(length);

        stream.Write(typeBytes);
        stream.Write(data);

        Span<byte> crc = stackalloc byte[4];
        WriteUInt32BE(crc, 0, Crc32(typeBytes, data));
        stream.Write(crc);
    }

    private static void WriteUInt32BE(Span<byte> buffer, int offset, uint value)
    {
        buffer[offset] = (byte)(value >> 24);
        buffer[offset + 1] = (byte)(value >> 16);
        buffer[offset + 2] = (byte)(value >> 8);
        buffer[offset + 3] = (byte)value;
    }

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

    private static uint Crc32(byte[] type, byte[] data)
    {
        var crc = 0xFFFFFFFFu;

        foreach (var b in type)
            crc = Crc32Table[(crc ^ b) & 0xFF] ^ (crc >> 8);
        foreach (var b in data)
            crc = Crc32Table[(crc ^ b) & 0xFF] ^ (crc >> 8);

        return crc ^ 0xFFFFFFFFu;
    }
}
