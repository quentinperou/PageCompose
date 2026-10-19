using System.Text.Json.Serialization;
using PageCompose.Core.Pdf;

namespace PageCompose.App.Services;

/// <summary>
/// The numbering of the visuals (asked by the author: raffle tickets, numbered cards, unique promo codes…), in the
/// settings and the presets: what each visual gets (a number, a code made by the app, a code of a list), the first
/// number and its digits or how the codes are made, the text around it, the order, one or two places in the visual
/// (millimetres from its top left corner, each its own way), the font, its height, its colour, a background if wanted,
/// and a QR code if wanted. The codes themselves belong to the job (its project), never to the settings.
/// </summary>
public sealed class NumberingSettings
{
    public const string DefaultFont = "Segoe UI";

    public bool Enabled { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter<NumberingContent>))]
    public NumberingContent Content { get; set; } = NumberingContent.Numbers;

    public int First { get; set; } = 1;
    public int Digits { get; set; } = 3;

    /// <summary>The codes made by the app: their characters, in groups joined by "-" (0: none).</summary>
    public int CodeLength { get; set; } = 8;
    public int CodeGroup { get; set; } = 4;

    [JsonConverter(typeof(JsonStringEnumConverter<CodeAlphabet>))]
    public CodeAlphabet CodeAlphabet { get; set; } = CodeAlphabet.LettersAndDigits;

    public string Prefix { get; set; } = "N° ";
    public string Suffix { get; set; } = "";

    /// <summary>By sheet by default (the author's choice); in a pile for the guillotine.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter<NumberingOrder>))]
    public NumberingOrder Order { get; set; } = NumberingOrder.BySheet;

    public double X1 { get; set; } = 5;
    public double Y1 { get; set; } = 5;

    [JsonConverter(typeof(JsonStringEnumConverter<NumberDirection>))]
    public NumberDirection Direction1 { get; set; }

    public bool Second { get; set; }
    public double X2 { get; set; } = 5;
    public double Y2 { get; set; } = 15;

    [JsonConverter(typeof(JsonStringEnumConverter<NumberDirection>))]
    public NumberDirection Direction2 { get; set; }

    public string Font { get; set; } = DefaultFont;
    public double SizeMm { get; set; } = 4;

    [JsonConverter(typeof(JsonStringEnumConverter<NumberColor>))]
    public NumberColor Color { get; set; } = NumberColor.Black;

    /// <summary>A background behind the number, to keep it readable on a busy visual (none by default).</summary>
    public bool HasBackground { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter<NumberColor>))]
    public NumberColor Background { get; set; } = NumberColor.White;

    /// <summary>A QR code of each number or code, alone or in an address (<see cref="QrStyle.CodeField"/>).</summary>
    public bool Qr { get; set; }
    public bool QrAddressMode { get; set; }
    public string QrAddress { get; set; } = "https://";
    public double QrX { get; set; } = 5;
    public double QrY { get; set; } = 5;
    public double QrSizeMm { get; set; } = 20;

    public NumberingSettings Copy() => (NumberingSettings)MemberwiseClone();
}
