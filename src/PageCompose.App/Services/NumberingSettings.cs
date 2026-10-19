using System.Text.Json.Serialization;
using PageCompose.Core.Pdf;

namespace PageCompose.App.Services;

/// <summary>
/// The numbering of the visuals (asked by the author: raffle tickets, numbered cards…), in the settings and the presets:
/// the first number, its digits and the text around it, the order, one or two places in the visual (millimetres from
/// its top left corner, each its own way), the font, its height, its colour and a background if wanted.
/// </summary>
public sealed class NumberingSettings
{
    public const string DefaultFont = "Segoe UI";

    public bool Enabled { get; set; }
    public int First { get; set; } = 1;
    public int Digits { get; set; } = 3;
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

    public NumberingSettings Copy() => (NumberingSettings)MemberwiseClone();
}
