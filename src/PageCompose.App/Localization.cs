using System.Globalization;
using System.Resources;
using System.Windows.Markup;

namespace PageCompose.App;

/// <summary>
/// UI strings from Properties/Strings.resx (English, the default) and Strings.fr.resx (French).
/// The language follows the Windows display language (CultureInfo.CurrentUICulture) unless chosen in Settings.
/// </summary>
public static class Strings
{
    private static readonly ResourceManager Manager = new("PageCompose.App.Properties.Strings", typeof(Strings).Assembly);

    public static string Get(string key) => Manager.GetString(key, CultureInfo.CurrentUICulture) ?? $"[{key}]";

    public static string Format(string key, params object[] args) =>
        string.Format(CultureInfo.CurrentCulture, Get(key), args);

    /// <summary>"{0} sheet" / "{0} sheets": picks <paramref name="key"/>One or <paramref name="key"/>Many.</summary>
    public static string Count(string key, int count) => Format(count == 1 ? key + "One" : key + "Many", count);
}

/// <summary>In XAML, <c>Text="{local:Tr ExportButton}"</c> shows the "ExportButton" string in the UI language.</summary>
[MarkupExtensionReturnType(typeof(string))]
public sealed class TrExtension(string key) : MarkupExtension
{
    public string Key { get; set; } = key;

    public override object ProvideValue(IServiceProvider serviceProvider) => Strings.Get(Key);
}
