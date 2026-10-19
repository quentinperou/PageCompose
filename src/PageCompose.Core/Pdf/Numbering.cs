using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using PageCompose.Core.Visuals;

namespace PageCompose.Core.Pdf;

/// <summary>
/// Order of the numbers: down the sheets cell by cell, so that the sheets stacked and cut give piles already in order
/// (cell 1 of every sheet, then cell 2…), or sheet after sheet in reading order.
/// </summary>
public enum NumberingOrder { Pile, BySheet }

/// <summary>Which way a number reads: across, up (from the bottom, as the spine of a book), upside down, down.</summary>
public enum NumberDirection { Horizontal, Vertical, HorizontalInverted, VerticalInverted }

/// <summary>Colours of the numbers and of the background behind them.</summary>
public enum NumberColor { Black, White, Grey, Red, Blue }

/// <summary>What each visual gets: a number that follows on, a code made by the app, or a code of a list read from a file.</summary>
public enum NumberingContent { Numbers, GeneratedCodes, CodeList }

/// <summary>The characters of the codes made by the app: none of those read alike (0 O, 1 I L).</summary>
public enum CodeAlphabet { LettersAndDigits, Digits, Letters }

/// <summary>
/// A place of the number in a visual: the top left corner of the number as it lies, from the top left corner of the
/// visual (millimetres), and the way it reads.
/// </summary>
public sealed record NumberPlace(double X, double Y, NumberDirection Direction = NumberDirection.Horizontal);

/// <summary>
/// A QR code of each visual's number or code (asked by the author with the codes): its top left corner from that of the
/// visual and its size (millimetres, its white margin included), and an address to put the code in ("{code}" is
/// replaced by it); null: the code alone.
/// </summary>
public sealed record QrStyle(double X, double Y, double SizeMm, string? Address = null)
{
    public const string CodeField = "{code}";

    public string Content(string value) => Address is { Length: > 0 } address ? address.Replace(CodeField, value) : value;
}

/// <summary>
/// How the numbers (or codes) of the visuals are written (asked by the author: raffle tickets, numbered cards, unique
/// promo codes…): the text around the value, its places in the visual, a TrueType font of the PC (its file, embedded in
/// the PDF), its height and colour, a background behind it if asked (to keep it readable on a busy visual), and a QR
/// code if asked.
/// </summary>
public sealed record NumberingStyle(string Prefix, string Suffix, IReadOnlyList<NumberPlace> Places,
    string Family, string FontFile, double SizeMm, NumberColor Color, NumberColor? Background = null, QrStyle? Qr = null)
{
    /// <summary>White space around the number on its background, in millimetres.</summary>
    public const double BackgroundPaddingMm = 0.8;

    /// <summary>"N° 007", "Code : 7F3K-9QXA": the value between the prefix and the suffix.</summary>
    public string Text(string value) => Prefix + value + Suffix;
}

public static class Numbering
{
    /// <summary>
    /// The rank of each visual laid, sheet by sheet as in <paramref name="sheets"/> (null: an empty cell), from
    /// <paramref name="first"/>. In a pile, the ranks go down the sheets in each cell, cell after cell: with the last
    /// sheet only partly filled, the piles of its empty cells are one shorter, and the ranks still follow on.
    /// </summary>
    public static int?[][] Assign(IReadOnlyList<IReadOnlyList<VisualInfo?>> sheets, NumberingOrder order, int first)
    {
        ArgumentNullException.ThrowIfNull(sheets);
        var numbers = sheets.Select(s => new int?[s.Count]).ToArray();
        int next = Math.Max(0, first);
        if (order == NumberingOrder.BySheet)
        {
            for (int s = 0; s < sheets.Count; s++)
                for (int c = 0; c < sheets[s].Count; c++)
                    if (sheets[s][c] is not null) numbers[s][c] = next++;
            return numbers;
        }

        int cells = sheets.Count == 0 ? 0 : sheets.Max(s => s.Count);
        for (int c = 0; c < cells; c++)
            for (int s = 0; s < sheets.Count; s++)
                if (c < sheets[s].Count && sheets[s][c] is not null) numbers[s][c] = next++;
        return numbers;
    }

    /// <summary>The value of each cell from its rank (<see cref="Assign"/>): a number, or a code; null: none.</summary>
    public static string?[][] Values(int?[][] ranks, Func<int, string?> valueOf) =>
        [.. ranks.Select(sheet => sheet.Select(rank => rank is { } r ? valueOf(r) : null).ToArray())];

    /// <summary>"007": at least <paramref name="digits"/> digits.</summary>
    public static string Number(int number, int digits) => number.ToString("D" + Math.Clamp(digits, 0, 9), CultureInfo.InvariantCulture);

    public static (byte R, byte G, byte B) Rgb(this NumberColor color) => color switch
    {
        NumberColor.White => (255, 255, 255),
        NumberColor.Grey => (110, 110, 110),
        NumberColor.Red => (200, 30, 30),
        NumberColor.Blue => (25, 60, 160),
        _ => (0, 0, 0),
    };

    // ---- Codes

    private const string Letters = "ABCDEFGHJKMNPQRSTUVWXYZ", Digits = "23456789";

    public static string Characters(this CodeAlphabet alphabet) => alphabet switch
    {
        CodeAlphabet.Digits => Digits,
        CodeAlphabet.Letters => Letters,
        _ => Digits + Letters,
    };

    /// <summary>
    /// <paramref name="count"/> codes not in <paramref name="taken"/> nor among themselves, of <paramref name="length"/>
    /// characters drawn by the random generator of cryptography (a code must not be guessed from another), in groups of
    /// <paramref name="group"/> joined by "-" (0: no group).
    /// </summary>
    /// <exception cref="InvalidOperationException">There are not that many codes of this length.</exception>
    public static List<string> NewCodes(int count, int length, int group, CodeAlphabet alphabet, IEnumerable<string>? taken = null)
    {
        length = Math.Clamp(length, 1, 32);
        string characters = alphabet.Characters();
        if (Math.Pow(characters.Length, length) < (count + (taken?.Count() ?? 0)) * 2.0)
            throw new InvalidOperationException("Too many codes for their length.");
        var used = new HashSet<string>(taken ?? [], StringComparer.OrdinalIgnoreCase);
        var codes = new List<string>(count);
        var text = new StringBuilder();
        while (codes.Count < count)
        {
            text.Clear();
            for (int i = 0; i < length; i++)
            {
                if (group > 0 && i > 0 && i % group == 0) text.Append('-');
                text.Append(characters[RandomNumberGenerator.GetInt32(characters.Length)]);
            }
            string code = text.ToString();
            if (used.Add(code)) codes.Add(code);
        }
        return codes;
    }

    /// <summary>The codes of a list made elsewhere (the till, a website), and how many came twice (left out).</summary>
    public sealed record CodeListRead(IReadOnlyList<string> Codes, int Duplicates);

    /// <summary>
    /// The codes of a text or CSV file: one a line, the first column (";", "," or a tab), empty lines and a first line
    /// "code" left out, a code found twice kept once.
    /// </summary>
    public static CodeListRead ReadCodes(string text)
    {
        var codes = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int duplicates = 0;
        bool first = true;
        foreach (var raw in text.Split('\n'))
        {
            string code = raw.Split(';', ',', '\t')[0].Trim().Trim('"').Trim();
            bool header = first && code.Equals("code", StringComparison.OrdinalIgnoreCase);
            first = false;
            if (code.Length == 0 || header) continue;
            if (seen.Add(code)) codes.Add(code);
            else duplicates++;
        }
        return new CodeListRead(codes, duplicates);
    }

    /// <summary>
    /// The codes laid, one a line in the order of their ranks (that of the piles, or of the sheets): the sheet and the cell
    /// of each (from 1, the cells in reading order) and its code, for the till or the website that takes them. The header
    /// line first; a field holding the separator or a quote is quoted.
    /// </summary>
    public static string CodesCsv(int?[][] ranks, string?[][] values, IReadOnlyList<string> header, string separator)
    {
        var rows = new List<(int Rank, int Sheet, int Cell, string Code)>();
        for (int s = 0; s < ranks.Length && s < values.Length; s++)
            for (int c = 0; c < ranks[s].Length && c < values[s].Length; c++)
                if (ranks[s][c] is { } rank && values[s][c] is { } code) rows.Add((rank, s + 1, c + 1, code));
        var text = new StringBuilder();
        text.AppendJoin(separator, header.Select(h => Field(h, separator))).Append("\r\n");
        foreach (var row in rows.OrderBy(r => r.Rank))
            text.Append(row.Sheet.ToString(CultureInfo.InvariantCulture)).Append(separator)
                .Append(row.Cell.ToString(CultureInfo.InvariantCulture)).Append(separator)
                .Append(Field(row.Code, separator)).Append("\r\n");
        return text.ToString();
    }

    private static string Field(string value, string separator) =>
        value.Contains(separator) || value.Contains('"') || value.Contains('\n')
            ? "\"" + value.Replace("\"", "\"\"") + "\""
            : value;
}
