using System.Buffers.Binary;
using System.Text;

namespace PageCompose.Core.Visuals;

/// <summary>
/// The ICC colour profile embedded in a photo: its name as the profile gives it ("Adobe RGB (1998)") and whether it is
/// sRGB. A photo without a profile is read as sRGB, as screens and printers do. With another profile its colours are
/// only right once converted: a PDF declares a JPEG as plain RGB, so readers, RIPs and Windows ignore the profile inside
/// it (an Adobe RGB photo then prints dull). The app converts such photos to sRGB at import (an option of its settings).
/// </summary>
public sealed record ColorProfile(string Name, bool IsSrgb)
{
    /// <summary>The primaries of sRGB, adapted to D50 as an ICC profile stores them (rXYZ, gXYZ, bXYZ).</summary>
    private static readonly (double X, double Y, double Z)[] SrgbPrimaries =
        [(0.4361, 0.2225, 0.0139), (0.3851, 0.7169, 0.0971), (0.1431, 0.0606, 0.7141)];

    /// <summary>Tolerance on each value of the primaries: the sRGB profiles of cameras and phones differ a little.</summary>
    private const double PrimaryTolerance = 0.01;

    /// <summary>A PNG with an sRGB chunk: sRGB without a profile of its own.</summary>
    public static ColorProfile Srgb { get; } = new("sRGB", true);

    /// <summary>
    /// Reads an ICC profile. It is sRGB when it is an RGB profile with the sRGB primaries (the small "c2" profiles of
    /// the web included), or, without primaries, when its name says so. Null when the bytes are not an ICC profile.
    /// </summary>
    public static ColorProfile? Parse(ReadOnlySpan<byte> icc)
    {
        if (icc.Length < 132 || !icc.Slice(36, 4).SequenceEqual("acsp"u8))
            return null;

        string space = Encoding.ASCII.GetString(icc.Slice(16, 4)).Trim();
        long count = BinaryPrimitives.ReadUInt32BigEndian(icc.Slice(128, 4));
        string name = "";
        var primaries = new Dictionary<string, (double X, double Y, double Z)>();
        for (long i = 0; i < count && 132 + (i + 1) * 12 <= icc.Length; i++)
        {
            int entry = (int)(132 + i * 12);
            string signature = Encoding.ASCII.GetString(icc.Slice(entry, 4));
            long offset = BinaryPrimitives.ReadUInt32BigEndian(icc.Slice(entry + 4, 4));
            long size = BinaryPrimitives.ReadUInt32BigEndian(icc.Slice(entry + 8, 4));
            if (size < 8 || offset + size > icc.Length)
                continue;

            var tag = icc.Slice((int)offset, (int)size);
            if (signature == "desc")
                name = ReadText(tag);
            else if (signature is "rXYZ" or "gXYZ" or "bXYZ" && tag.Length >= 20 && tag[..4].SequenceEqual("XYZ "u8))
                primaries[signature] = (Fixed(tag, 8), Fixed(tag, 12), Fixed(tag, 16));
        }

        bool srgb = space == "RGB" && (primaries.Count == 3
            ? Close(primaries["rXYZ"], SrgbPrimaries[0]) && Close(primaries["gXYZ"], SrgbPrimaries[1]) && Close(primaries["bXYZ"], SrgbPrimaries[2])
            : name.Contains("sRGB", StringComparison.OrdinalIgnoreCase));
        name = name.Trim();
        if (name.Length == 0)
            name = srgb ? "sRGB" : space;
        return new ColorProfile(name, srgb);
    }

    /// <summary>The text of a "desc" tag: ICC v2 (textDescriptionType), v4 (multiLocalizedUnicodeType) or plain text.</summary>
    private static string ReadText(ReadOnlySpan<byte> tag)
    {
        if (tag.Length < 12)
            return "";

        if (tag[..4].SequenceEqual("desc"u8))
        {
            int length = (int)Math.Min(BinaryPrimitives.ReadUInt32BigEndian(tag.Slice(8, 4)), (uint)(tag.Length - 12));
            return Encoding.ASCII.GetString(tag.Slice(12, length)).TrimEnd('\0');
        }

        if (tag[..4].SequenceEqual("mluc"u8) && tag.Length >= 28)
        {
            // Records of 12 bytes: language, country, length, offset (from the start of the tag); English first.
            int records = (int)BinaryPrimitives.ReadUInt32BigEndian(tag.Slice(8, 4));
            int recordSize = (int)BinaryPrimitives.ReadUInt32BigEndian(tag.Slice(12, 4));
            string first = "";
            for (int r = 0; r < records && 16 + (r + 1) * recordSize <= tag.Length && recordSize >= 12; r++)
            {
                int at = 16 + r * recordSize;
                int length = (int)BinaryPrimitives.ReadUInt32BigEndian(tag.Slice(at + 4, 4));
                int offset = (int)BinaryPrimitives.ReadUInt32BigEndian(tag.Slice(at + 8, 4));
                if (offset < 0 || length < 0 || offset + length > tag.Length)
                    continue;
                string text = Encoding.BigEndianUnicode.GetString(tag.Slice(offset, length)).TrimEnd('\0');
                if (tag.Slice(at, 2).SequenceEqual("en"u8))
                    return text;
                if (first.Length == 0)
                    first = text;
            }

            return first;
        }

        if (tag[..4].SequenceEqual("text"u8))
            return Encoding.ASCII.GetString(tag[8..]).TrimEnd('\0');
        return "";
    }

    /// <summary>An s15Fixed16Number.</summary>
    private static double Fixed(ReadOnlySpan<byte> b, int offset) => BinaryPrimitives.ReadInt32BigEndian(b.Slice(offset, 4)) / 65536.0;

    private static bool Close((double X, double Y, double Z) a, (double X, double Y, double Z) b) =>
        Math.Abs(a.X - b.X) <= PrimaryTolerance && Math.Abs(a.Y - b.Y) <= PrimaryTolerance && Math.Abs(a.Z - b.Z) <= PrimaryTolerance;
}
