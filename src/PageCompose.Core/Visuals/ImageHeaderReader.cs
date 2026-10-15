using System.Buffers.Binary;
using System.IO.Compression;

namespace PageCompose.Core.Visuals;

/// <summary>Minimal reading of JPEG and PNG headers (size, EXIF orientation, ICC profile), without decoding the image.</summary>
internal static class ImageHeaderReader
{
    /// <param name="Profile">The embedded colour profile; null when there is none (read as sRGB).</param>
    public readonly record struct ImageHeader(int Width, int Height, int ExifOrientation, ColorProfile? Profile = null);

    public static ImageHeader ReadJpeg(Stream s)
    {
        var br = new BinaryReader(s);
        if (br.ReadByte() != 0xFF || br.ReadByte() != 0xD8)
            throw new InvalidDataException("Signature JPEG absente.");

        int width = 0, height = 0, orientation = 1;
        // A profile larger than a segment is split over several APP2 "ICC_PROFILE" segments, numbered from 1.
        var iccParts = new SortedDictionary<int, byte[]>();
        while (s.Position < s.Length)
        {
            if (br.ReadByte() != 0xFF) continue;
            byte marker = br.ReadByte();
            while (marker == 0xFF) marker = br.ReadByte();
            if (marker is 0xD8 or 0x01 or (>= 0xD0 and <= 0xD7)) continue; // markers without a length
            if (marker is 0xD9 or 0xDA) break; // end of image / start of the compressed data

            int length = BinaryPrimitives.ReadUInt16BigEndian(br.ReadBytes(2));
            if (length < 2) throw new InvalidDataException("Segment JPEG invalide.");
            long next = s.Position + length - 2;

            bool isSof = marker is >= 0xC0 and <= 0xCF and not (0xC4 or 0xC8 or 0xCC);
            if (isSof && width == 0)
            {
                br.ReadByte(); // precision
                height = BinaryPrimitives.ReadUInt16BigEndian(br.ReadBytes(2));
                width = BinaryPrimitives.ReadUInt16BigEndian(br.ReadBytes(2));
            }
            else if (marker == 0xE1 && length > 14)
            {
                var data = br.ReadBytes(length - 2);
                if (data.AsSpan(0, 6).SequenceEqual("Exif\0\0"u8))
                    orientation = ReadExifOrientation(data.AsSpan(6)) ?? orientation;
            }
            else if (marker == 0xE2 && length > 16)
            {
                var data = br.ReadBytes(length - 2);
                if (data.AsSpan(0, 12).SequenceEqual("ICC_PROFILE\0"u8))
                    iccParts[data[12]] = data[14..];
            }

            s.Position = next;
        }

        if (width == 0 || height == 0)
            throw new InvalidDataException("Dimensions JPEG introuvables.");
        var profile = iccParts.Count > 0 ? ColorProfile.Parse(iccParts.Values.SelectMany(p => p).ToArray()) : null;
        return new ImageHeader(width, height, orientation is >= 1 and <= 8 ? orientation : 1, profile);
    }

    public static ImageHeader ReadPng(Stream s)
    {
        Span<byte> head = stackalloc byte[24];
        s.ReadExactly(head);
        if (!head[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }) ||
            !head.Slice(12, 4).SequenceEqual("IHDR"u8))
            throw new InvalidDataException("Signature PNG absente.");
        int width = (int)BinaryPrimitives.ReadUInt32BigEndian(head.Slice(16, 4));
        int height = (int)BinaryPrimitives.ReadUInt32BigEndian(head.Slice(20, 4));

        // The colour chunks come before the image data: iCCP (a profile, compressed) or sRGB. Each chunk: its length,
        // its type, its data, a CRC.
        ColorProfile? profile = null;
        long position = 8 + 4 + 4 + 13 + 4; // past the signature and IHDR
        var chunk = new byte[8];
        while (position + 8 <= s.Length)
        {
            s.Position = position;
            s.ReadExactly(chunk);
            long length = BinaryPrimitives.ReadUInt32BigEndian(chunk.AsSpan(0, 4));
            var type = chunk.AsSpan(4, 4);
            if (type.SequenceEqual("IDAT"u8) || type.SequenceEqual("IEND"u8) || position + 12 + length > s.Length)
                break;
            // Both should not be there; when they are, the profile wins, as in the PNG decoders that read it.
            if (type.SequenceEqual("sRGB"u8))
                profile ??= ColorProfile.Srgb;
            else if (type.SequenceEqual("iCCP"u8))
                profile = ReadPngProfile(s, (int)length) ?? profile;
            position += 12 + length;
        }

        return new ImageHeader(width, height, 1, profile);
    }

    /// <summary>iCCP: a name (Latin-1, up to 79 bytes, then 0), the compression method (0), the profile compressed by zlib.</summary>
    private static ColorProfile? ReadPngProfile(Stream s, int length)
    {
        var data = new byte[length];
        s.ReadExactly(data);
        int zero = Array.IndexOf(data, (byte)0);
        if (zero < 0 || zero + 2 > data.Length || data[zero + 1] != 0)
            return null;
        try
        {
            using var z = new ZLibStream(new MemoryStream(data, zero + 2, data.Length - zero - 2), CompressionMode.Decompress);
            using var icc = new MemoryStream();
            z.CopyTo(icc);
            return ColorProfile.Parse(icc.ToArray());
        }
        catch (InvalidDataException)
        {
            return null;
        }
    }

    /// <summary>Reads the Orientation tag (0x0112) from IFD0 of a TIFF/EXIF block.</summary>
    private static int? ReadExifOrientation(ReadOnlySpan<byte> tiff)
    {
        if (tiff.Length < 8) return null;
        bool little = tiff[0] == (byte)'I' && tiff[1] == (byte)'I';
        bool big = tiff[0] == (byte)'M' && tiff[1] == (byte)'M';
        if (!little && !big) return null;

        long ifd = U32(tiff, 4, little);
        if (ifd + 2 > tiff.Length) return null;
        int count = U16(tiff, (int)ifd, little);
        for (int i = 0; i < count; i++)
        {
            int entry = (int)ifd + 2 + i * 12;
            if (entry + 12 > tiff.Length) return null;
            if (U16(tiff, entry, little) == 0x0112)
                return U16(tiff, entry + 8, little);
        }

        return null;
    }

    private static ushort U16(ReadOnlySpan<byte> b, int offset, bool little) =>
        little ? BinaryPrimitives.ReadUInt16LittleEndian(b[offset..]) : BinaryPrimitives.ReadUInt16BigEndian(b[offset..]);

    private static uint U32(ReadOnlySpan<byte> b, int offset, bool little) =>
        little ? BinaryPrimitives.ReadUInt32LittleEndian(b[offset..]) : BinaryPrimitives.ReadUInt32BigEndian(b[offset..]);
}
