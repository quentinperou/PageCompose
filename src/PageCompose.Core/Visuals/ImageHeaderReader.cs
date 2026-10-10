using System.Buffers.Binary;

namespace PageCompose.Core.Visuals;

/// <summary>Minimal reading of JPEG and PNG headers (size, EXIF orientation), without decoding the image.</summary>
internal static class ImageHeaderReader
{
    public readonly record struct ImageHeader(int Width, int Height, int ExifOrientation);

    public static ImageHeader ReadJpeg(Stream s)
    {
        var br = new BinaryReader(s);
        if (br.ReadByte() != 0xFF || br.ReadByte() != 0xD8)
            throw new InvalidDataException("Signature JPEG absente.");

        int width = 0, height = 0, orientation = 1;
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

            s.Position = next;
        }

        if (width == 0 || height == 0)
            throw new InvalidDataException("Dimensions JPEG introuvables.");
        return new ImageHeader(width, height, orientation is >= 1 and <= 8 ? orientation : 1);
    }

    public static ImageHeader ReadPng(Stream s)
    {
        Span<byte> head = stackalloc byte[24];
        s.ReadExactly(head);
        if (!head[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }) ||
            !head.Slice(12, 4).SequenceEqual("IHDR"u8))
            throw new InvalidDataException("Signature PNG absente.");
        return new ImageHeader(
            (int)BinaryPrimitives.ReadUInt32BigEndian(head.Slice(16, 4)),
            (int)BinaryPrimitives.ReadUInt32BigEndian(head.Slice(20, 4)),
            1);
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
