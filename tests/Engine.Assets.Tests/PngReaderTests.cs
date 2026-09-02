using System.IO.Compression;
using System.Text;
using Engine.Assets;

namespace Engine.Assets.Tests;

/// <summary>
/// Builds tiny test PNGs by hand rather than reusing Engine.Render's
/// PngWriter — keeps this test project independent of a second plugin,
/// and a from-scratch encoder here is also a second, independent
/// implementation of the format to check PngReader against, not the same
/// code testing itself.
/// </summary>
public class PngReaderTests
{
    [Fact]
    public void Read_Recovers_Exact_Pixel_Values_Through_A_Round_Trip()
    {
        // 2x2, four distinct colors — catches row-order and channel-order
        // mistakes that a single flat color would hide.
        byte[] pixels =
        [
            255, 0, 0, 255, 0, 255, 0, 255, // row 0: red, green
            0, 0, 255, 255, 255, 255, 0, 255, // row 1: blue, yellow
        ];

        var path = WriteTestPng(2, 2, pixels, filterType: 0);
        try
        {
            var (width, height, rgba) = PngReader.Read(path);

            Assert.Equal(2, width);
            Assert.Equal(2, height);
            Assert.Equal(pixels, rgba);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData((byte)0)] // None
    [InlineData((byte)1)] // Sub
    [InlineData((byte)2)] // Up
    [InlineData((byte)3)] // Average
    [InlineData((byte)4)] // Paeth
    public void Read_Correctly_Unfilters_Every_Filter_Type(byte filterType)
    {
        byte[] pixels =
        [
            10, 20, 30, 255, 40, 50, 60, 255,
            70, 80, 90, 255, 100, 110, 120, 255,
        ];

        var path = WriteTestPng(2, 2, pixels, filterType);
        try
        {
            var (_, _, rgba) = PngReader.Read(path);
            Assert.Equal(pixels, rgba);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Read_Throws_On_A_File_That_Is_Not_A_PNG()
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, "not a png");

        try
        {
            Assert.Throws<InvalidDataException>(() => PngReader.Read(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Read_Throws_On_An_Unsupported_Color_Type()
    {
        // Grayscale (color type 0) instead of RGBA (6) — the reader is
        // deliberately scoped to the one subset it actually supports.
        var path = WriteTestPng(1, 1, [128], filterType: 0, colorType: 0, bytesPerPixel: 1);

        try
        {
            Assert.Throws<NotSupportedException>(() => PngReader.Read(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string WriteTestPng(
        int width, int height, byte[] rgba, byte filterType, byte colorType = 6, int bytesPerPixel = 4)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.png");

        using var file = File.Create(path);
        file.Write((byte[]) [137, 80, 78, 71, 13, 10, 26, 10]);

        var ihdr = new byte[13];
        WriteUInt32BE(ihdr, 0, (uint)width);
        WriteUInt32BE(ihdr, 4, (uint)height);
        ihdr[8] = 8;         // bit depth
        ihdr[9] = colorType;
        ihdr[10] = 0;
        ihdr[11] = 0;
        ihdr[12] = 0;        // interlace: none
        WriteChunk(file, "IHDR", ihdr);

        var stride = width * bytesPerPixel;
        using var raw = new MemoryStream();
        for (var y = 0; y < height; y++)
        {
            var row = new byte[stride];
            Array.Copy(rgba, y * stride, row, 0, stride);
            var filtered = ApplyFilter(filterType, row, y > 0 ? Slice(rgba, (y - 1) * stride, stride) : null, bytesPerPixel);
            raw.WriteByte(filterType);
            raw.Write(filtered);
        }

        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
            zlib.Write(raw.ToArray());
        WriteChunk(file, "IDAT", compressed.ToArray());

        WriteChunk(file, "IEND", []);
        return path;
    }

    private static byte[] Slice(byte[] source, int offset, int length)
    {
        var slice = new byte[length];
        Array.Copy(source, offset, slice, 0, length);
        return slice;
    }

    private static byte[] ApplyFilter(byte filterType, byte[] row, byte[]? previousRow, int bytesPerPixel)
    {
        var result = new byte[row.Length];

        for (var x = 0; x < row.Length; x++)
        {
            int a = x >= bytesPerPixel ? row[x - bytesPerPixel] : 0;
            int b = previousRow?[x] ?? 0;
            int c = x >= bytesPerPixel ? previousRow?[x - bytesPerPixel] ?? 0 : 0;

            result[x] = filterType switch
            {
                0 => row[x],
                1 => (byte)(row[x] - a),
                2 => (byte)(row[x] - b),
                3 => (byte)(row[x] - (a + b) / 2),
                4 => (byte)(row[x] - Paeth(a, b, c)),
                _ => throw new ArgumentOutOfRangeException(nameof(filterType)),
            };
        }

        return result;
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

    private static void WriteChunk(Stream stream, string type, byte[] data)
    {
        var typeBytes = Encoding.ASCII.GetBytes(type);

        var length = new byte[4];
        WriteUInt32BE(length, 0, (uint)data.Length);
        stream.Write(length);
        stream.Write(typeBytes);
        stream.Write(data);

        var crc = new byte[4];
        WriteUInt32BE(crc, 0, Crc32(typeBytes, data));
        stream.Write(crc);
    }

    private static void WriteUInt32BE(byte[] buffer, int offset, uint value)
    {
        buffer[offset] = (byte)(value >> 24);
        buffer[offset + 1] = (byte)(value >> 16);
        buffer[offset + 2] = (byte)(value >> 8);
        buffer[offset + 3] = (byte)value;
    }

    private static readonly uint[] Crc32Table = BuildCrc32Table();

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
