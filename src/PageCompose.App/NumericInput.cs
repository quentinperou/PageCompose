using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PageCompose.App;

/// <summary>
/// Up and Down arrows change the number of a text box by <c>Step</c> (Shift: ten times more), as in the Windows 11
/// NumberBox, without going under <c>Minimum</c>. The new value reaches the binding at once. A "." or a "," typed or
/// pasted becomes the decimal separator of the PC: "0.1" and "0,1" both work in France.
/// </summary>
public static class NumericInput
{
    public static readonly DependencyProperty StepProperty = DependencyProperty.RegisterAttached(
        "Step", typeof(double), typeof(NumericInput), new PropertyMetadata(0.0, OnStepChanged));

    public static readonly DependencyProperty MinimumProperty = DependencyProperty.RegisterAttached(
        "Minimum", typeof(double), typeof(NumericInput), new PropertyMetadata(0.0));

    public static double GetStep(DependencyObject element) => (double)element.GetValue(StepProperty);
    public static void SetStep(DependencyObject element, double value) => element.SetValue(StepProperty, value);
    public static double GetMinimum(DependencyObject element) => (double)element.GetValue(MinimumProperty);
    public static void SetMinimum(DependencyObject element, double value) => element.SetValue(MinimumProperty, value);

    private static void OnStepChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBox box) return;
        box.PreviewKeyDown -= OnPreviewKeyDown;
        box.PreviewTextInput -= OnPreviewTextInput;
        DataObject.RemovePastingHandler(box, OnPasting);
        if ((double)e.NewValue <= 0) return;
        box.PreviewKeyDown += OnPreviewKeyDown;
        box.PreviewTextInput += OnPreviewTextInput;
        DataObject.AddPastingHandler(box, OnPasting);
    }

    private static string DecimalSeparator(TextBox box) => box.Language.GetSpecificCulture().NumberFormat.NumberDecimalSeparator;

    private static void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (sender is not TextBox box || e.Text is not ("." or ",")) return;
        string separator = DecimalSeparator(box);
        if (e.Text == separator) return;

        // Typed in place of the selection, as the key itself would.
        int start = box.SelectionStart;
        box.Text = box.Text.Remove(start, box.SelectionLength).Insert(start, separator);
        box.CaretIndex = start + separator.Length;
        e.Handled = true;
    }

    private static void OnPasting(object sender, DataObjectPastingEventArgs e)
    {
        if (sender is not TextBox box || e.SourceDataObject.GetData(DataFormats.UnicodeText) is not string text) return;
        string separator = DecimalSeparator(box);
        string fixedText = text.Trim().Replace(".", separator).Replace(",", separator);
        if (fixedText == text) return;

        var data = new DataObject();
        data.SetData(DataFormats.UnicodeText, fixedText);
        e.DataObject = data;
    }

    private static void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Up or Key.Down) || sender is not TextBox box) return;

        // Same culture as the binding (decimal comma in France).
        var culture = box.Language.GetSpecificCulture();
        double min = GetMinimum(box);
        if (!double.TryParse(box.Text, NumberStyles.Float, culture, out double value)) value = min;

        double step = GetStep(box) * (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? 10 : 1);
        value = Math.Round(Math.Max(min, value + (e.Key == Key.Up ? step : -step)), 2);

        box.Text = value.ToString(culture);
        box.CaretIndex = box.Text.Length;
        box.GetBindingExpression(TextBox.TextProperty)?.UpdateSource(); // without the typing delay
        e.Handled = true;
    }
}
