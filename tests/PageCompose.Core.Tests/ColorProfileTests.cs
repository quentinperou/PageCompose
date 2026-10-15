using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using PageCompose.Core.Visuals;

namespace PageCompose.Core.Tests;

public sealed class ColorProfileTests : IDisposable
{
    private readonly TestFiles _files = new();

    private static readonly (double, double, double)[] Srgb = [(0.4361, 0.2225, 0.0139), (0.3851, 0.7169, 0.0971), (0.1431, 0.0606, 0.7141)];
    private static readonly (double, double, double)[] AdobeRgb = [(0.6097, 0.3111, 0.0195), (0.2053, 0.6257, 0.0609), (0.1492, 0.0632, 0.7446)];
    private static readonly (double, double, double)[] DisplayP3 = [(0.5151, 0.2412, -0.0011), (0.2920, 0.6922, 0.0419), (0.1571, 0.0666, 0.7841)];

    [Theory]
    [InlineData("sRGB IEC61966-2.1", "RGB ", "srgb", false, true)]
    [InlineData("c2", "RGB ", "srgb", false, true)]                         // the small sRGB profiles of the web
    [InlineData("Adobe RGB (1998)", "RGB ", "adobe", false, false)]
    [InlineData("Display P3", "RGB ", "p3", true, false)]                   // ICC v4: a multi-language name
    [InlineData("U.S. Web Coated (SWOP) v2", "CMYK", null, false, false)]
    [InlineData("sRGB built-in", "RGB ", null, false, true)]                // no primaries: the name tells
    public void A_profile_is_sRGB_by_its_primaries(string name, string space, string? primaries, bool v4, bool srgb)
    {
        var p = ColorProfile.Parse(Icc(name, space, primaries switch { "srgb" => Srgb, "adobe" => AdobeRgb, "p3" => DisplayP3, _ => null }, v4));

        Assert.NotNull(p);
        Assert.Equal(name, p.Name);
        Assert.Equal(srgb, p.IsSrgb);
    }

    [Fact]
    public void Bytes_that_are_no_profile_give_none() =>
        Assert.Null(ColorProfile.Parse(Encoding.ASCII.GetBytes(new string('x', 200))));

    [Fact]
    public void The_profile_of_a_JPEG_is_read_from_its_header()
    {
        var plain = VisualLoader.Load(_files.Jpeg(300, 200, name: "plain.jpg"));
        var adobe = VisualLoader.Load(JpegWithProfile(Icc("Adobe RGB (1998)", "RGB ", AdobeRgb), "adobe.jpg", segments: 1));
        var split = VisualLoader.Load(JpegWithProfile(Icc("Display P3", "RGB ", DisplayP3, v4: true), "p3.jpg", segments: 2));
        var srgb = VisualLoader.Load(JpegWithProfile(Icc("sRGB IEC61966-2.1", "RGB ", Srgb), "srgb.jpg", segments: 1));

        Assert.Null(plain.Profile);
        Assert.False(plain.HasForeignProfile);
        Assert.Equal("Adobe RGB (1998)", adobe.Profile?.Name);
        Assert.True(adobe.HasForeignProfile);
        Assert.Equal(300, adobe.FullWidth); // the size is still read past the profile
        Assert.Equal("Display P3", split.Profile?.Name); // a profile over two segments
        Assert.True(srgb.Profile?.IsSrgb);
        Assert.False(srgb.HasForeignProfile);
        // Converted at import, it no longer counts as foreign.
        Assert.False((adobe with { ProfileConverted = true }).HasForeignProfile);
    }

    [Fact]
    public void The_profile_of_a_PNG_is_read_from_its_iCCP_or_sRGB_chunk()
    {
        var adobe = VisualLoader.Load(PngWithChunk("iCCP", IccpData("Adobe RGB (1998)", Icc("Adobe RGB (1998)", "RGB ", AdobeRgb)), "adobe.png"));
        var srgb = VisualLoader.Load(PngWithChunk("sRGB", [0], "srgb.png"));
        var plain = VisualLoader.Load(_files.Png(80, 60, name: "plain.png"));

        Assert.Equal("Adobe RGB (1998)", adobe.Profile?.Name);
        Assert.True(adobe.HasForeignProfile);
        Assert.Equal(80, adobe.FullWidth);
        Assert.True(srgb.Profile?.IsSrgb);
        Assert.False(plain.HasForeignProfile); // none, or the sRGB chunk WPF writes
    }

    // ---- Test files ----

    /// <summary>A small ICC profile: a description (v2 text or v4 multi-language) and, when given, the three primaries.</summary>
    private static byte[] Icc(string name, string space, (double X, double Y, double Z)[]? primaries, bool v4 = false)
    {
        var tags = new List<(string Signature, byte[] Data)> { ("desc", v4 ? Mluc(name) : TextDescription(name)) };
        if (primaries is not null)
            foreach (var (signature, xyz) in new[] { "rXYZ", "gXYZ", "bXYZ" }.Zip(primaries))
                tags.Add((signature, Xyz(xyz)));

        int offset = 128 + 4 + tags.Count * 12;
        int size = offset + tags.Sum(t => (t.Data.Length + 3) / 4 * 4);
        var icc = new byte[size];
        BinaryPrimitives.WriteUInt32BigEndian(icc.AsSpan(0), (uint)size);
        BinaryPrimitives.WriteUInt32BigEndian(icc.AsSpan(8), v4 ? 0x04300000u : 0x02100000u);
        Encoding.ASCII.GetBytes("mntr").CopyTo(icc, 12);
        Encoding.ASCII.GetBytes(space).CopyTo(icc, 16);
        Encoding.ASCII.GetBytes("XYZ ").CopyTo(icc, 20);
        Encoding.ASCII.GetBytes("acsp").CopyTo(icc, 36);
        BinaryPrimitives.WriteUInt32BigEndian(icc.AsSpan(128), (uint)tags.Count);
        for (int i = 0; i < tags.Count; i++)
        {
            int entry = 132 + i * 12;
            Encoding.ASCII.GetBytes(tags[i].Signature).CopyTo(icc, entry);
            BinaryPrimitives.WriteUInt32BigEndian(icc.AsSpan(entry + 4), (uint)offset);
            BinaryPrimitives.WriteUInt32BigEndian(icc.AsSpan(entry + 8), (uint)tags[i].Data.Length);
            tags[i].Data.CopyTo(icc, offset);
            offset += (tags[i].Data.Length + 3) / 4 * 4;
        }
        return icc;
    }

    private static byte[] TextDescription(string name)
    {
        var data = new byte[12 + name.Length + 1];
        Encoding.ASCII.GetBytes("desc").CopyTo(data, 0);
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(8), (uint)(name.Length + 1));
        Encoding.ASCII.GetBytes(name).CopyTo(data, 12);
        return data;
    }

    private static byte[] Mluc(string name)
    {
        byte[] text = Encoding.BigEndianUnicode.GetBytes(name);
        var data = new byte[28 + text.Length];
        Encoding.ASCII.GetBytes("mluc").CopyTo(data, 0);
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(8), 1);
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(12), 12);
        Encoding.ASCII.GetBytes("enUS").CopyTo(data, 16);
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(20), (uint)text.Length);
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(24), 28);
        text.CopyTo(data, 28);
        return data;
    }

    private static byte[] Xyz((double X, double Y, double Z) v)
    {
        var data = new byte[20];
        Encoding.ASCII.GetBytes("XYZ ").CopyTo(data, 0);
        BinaryPrimitives.WriteInt32BigEndian(data.AsSpan(8), (int)Math.Round(v.X * 65536));
        BinaryPrimitives.WriteInt32BigEndian(data.AsSpan(12), (int)Math.Round(v.Y * 65536));
        BinaryPrimitives.WriteInt32BigEndian(data.AsSpan(16), (int)Math.Round(v.Z * 65536));
        return data;
    }

    /// <summary>A test JPEG with the profile in APP2 "ICC_PROFILE" segments right after SOI.</summary>
    private string JpegWithProfile(byte[] icc, string name, int segments)
    {
        byte[] jpeg = File.ReadAllBytes(_files.Jpeg(300, 200, name: "source-" + name));
        var app2 = new List<byte>();
        int part = (icc.Length + segments - 1) / segments;
        for (int n = 0; n < segments; n++)
        {
            byte[] data = icc.Skip(n * part).Take(part).ToArray();
            int length = 2 + 12 + 2 + data.Length;
            app2.AddRange([0xFF, 0xE2, (byte)(length >> 8), (byte)length]);
            app2.AddRange("ICC_PROFILE\0"u8.ToArray());
            app2.AddRange([(byte)(n + 1), (byte)segments]);
            app2.AddRange(data);
        }
        string path = _files.Path(name);
        File.WriteAllBytes(path, [.. jpeg.Take(2), .. app2, .. jpeg.Skip(2)]);
        return path;
    }

    private static byte[] IccpData(string name, byte[] icc)
    {
        using var compressed = new MemoryStream();
        using (var z = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
            z.Write(icc);
        return [.. Encoding.Latin1.GetBytes(name), 0, 0, .. compressed.ToArray()];
    }

    /// <summary>A test PNG with one more chunk right after IHDR.</summary>
    private string PngWithChunk(string type, byte[] data, string name)
    {
        byte[] png = File.ReadAllBytes(_files.Png(80, 60, name: "source-" + name));
        const int afterIhdr = 8 + 4 + 4 + 13 + 4;
        var chunk = new byte[12 + data.Length];
        BinaryPrimitives.WriteUInt32BigEndian(chunk, (uint)data.Length);
        Encoding.ASCII.GetBytes(type).CopyTo(chunk, 4);
        data.CopyTo(chunk, 8);
        BinaryPrimitives.WriteUInt32BigEndian(chunk.AsSpan(8 + data.Length), Crc32(chunk.AsSpan(4, 4 + data.Length)));
        string path = _files.Path(name);
        File.WriteAllBytes(path, [.. png.Take(afterIhdr), .. chunk, .. png.Skip(afterIhdr)]);
        return path;
    }

    private static uint Crc32(ReadOnlySpan<byte> bytes)
    {
        uint crc = 0xFFFFFFFF;
        foreach (byte b in bytes)
        {
            crc ^= b;
            for (int k = 0; k < 8; k++)
                crc = (crc & 1) != 0 ? 0xEDB88320 ^ (crc >> 1) : crc >> 1;
        }
        return ~crc;
    }

    public void Dispose() => _files.Dispose();
}
