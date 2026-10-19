using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace PageCompose.App;

/// <summary>
/// Up and Down arrows change the number of a text box by <c>Step</c> (Shift: ten times more), as in the Windows 11
/// NumberBox, without going under <c>Minimum</c>. The new value reaches the binding at once. A "." or a "," typed or
/// pasted becomes the decimal separator of the PC: "0.1" and "0,1" both work in France. The label of a box can change
/// its number too, dragged sideways (<c>Scrubs</c>).
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

    // ---- Dragging a label (asked by the author, as in Photoshop or Figma) ----
    // Pressed on the label of a number and dragged sideways: a step of its box every few pixels, to the right more, to
    // the left less (Shift: ten times more; Alt: a tenth, in a box of decimals). The cursor shows ↔ while the box can
    // change. A click without dragging puts the cursor in the box, its number selected; a check box (Bleed, Space)
    // keeps its own click.

    /// <summary>The text box whose number this label changes when dragged sideways.</summary>
    public static readonly DependencyProperty ScrubsProperty = DependencyProperty.RegisterAttached(
        "Scrubs", typeof(TextBox), typeof(NumericInput), new PropertyMetadata(null, OnScrubsChanged));

    public static TextBox? GetScrubs(DependencyObject element) => (TextBox?)element.GetValue(ScrubsProperty);
    public static void SetScrubs(DependencyObject element, TextBox? value) => element.SetValue(ScrubsProperty, value);

    private const double PixelsPerStep = 4, DragThreshold = 3;

    private sealed class Drag
    {
        public double StartX;
        public int Steps;
        public bool Moving;
    }

    private static readonly DependencyProperty DragProperty = DependencyProperty.RegisterAttached(
        "Drag", typeof(Drag), typeof(NumericInput), new PropertyMetadata(null));

    private static void OnScrubsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement label) return;
        label.MouseEnter -= Label_MouseEnter;
        label.PreviewMouseLeftButtonDown -= Label_Down;
        label.PreviewMouseMove -= Label_Move;
        label.PreviewMouseLeftButtonUp -= Label_Up;
        label.LostMouseCapture -= Label_LostCapture;
        if (e.NewValue is null) return;
        // A text block takes the mouse on its letters only: with a background, all of it does.
        if (label is TextBlock { Background: null } block) block.Background = Brushes.Transparent;
        label.MouseEnter += Label_MouseEnter;
        label.PreviewMouseLeftButtonDown += Label_Down;
        label.PreviewMouseMove += Label_Move;
        label.PreviewMouseLeftButtonUp += Label_Up;
        label.LostMouseCapture += Label_LostCapture;
    }

    // Its box can change now: shown, enabled (the bleed box is not while the bleed is off).
    private static TextBox? LiveBox(DependencyObject label) => GetScrubs(label) is { IsVisible: true, IsEnabled: true } box ? box : null;

    private static void Label_MouseEnter(object sender, MouseEventArgs e)
    {
        if (sender is FrameworkElement label)
            label.Cursor = LiveBox(label) is null ? null : Cursors.SizeWE;
    }

    private static void Label_Down(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement label || LiveBox(label) is null) return;
        label.SetValue(DragProperty, new Drag { StartX = e.GetPosition(label).X });
    }

    private static void Label_Move(object sender, MouseEventArgs e)
    {
        if (sender is not FrameworkElement label || label.GetValue(DragProperty) is not Drag drag) return;
        if (e.LeftButton != MouseButtonState.Pressed || LiveBox(label) is not { } box)
        {
            label.ClearValue(DragProperty);
            return;
        }

        double dx = e.GetPosition(label).X - drag.StartX;
        if (!drag.Moving)
        {
            if (Math.Abs(dx) < DragThreshold) return;
            drag.Moving = true;
            label.CaptureMouse(); // kept past the edges of the label; a check box under the mouse loses its click
        }

        // Step by step from where the mouse is: Shift or Alt pressed during the drag change the steps from then on.
        int steps = (int)Math.Round(dx / PixelsPerStep);
        if (steps != drag.Steps)
        {
            double step = GetStep(box) * (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? 10 : 1)
                          * (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt) && AcceptsDecimals(box) ? 0.1 : 1);
            SetNumber(box, ValueOf(box) + (steps - drag.Steps) * step);
            drag.Steps = steps;
        }
        e.Handled = true;
    }

    private static void Label_Up(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement label || label.GetValue(DragProperty) is not Drag drag) return;
        label.ClearValue(DragProperty);
        if (drag.Moving)
        {
            label.ReleaseMouseCapture();
            e.Handled = true; // a check box keeps its state
        }
        else if (label is not CheckBox && LiveBox(label) is { } box)
        {
            box.Focus();
            box.SelectAll();
        }
    }

    private static void Label_LostCapture(object sender, MouseEventArgs e)
    {
        if (sender is FrameworkElement label && label.GetValue(DragProperty) is Drag { Moving: true })
            label.ClearValue(DragProperty);
    }

    // Its number as typed (the culture of the binding: decimal comma in France), else its minimum.
    private static double ValueOf(TextBox box) =>
        double.TryParse(box.Text, NumberStyles.Float, box.Language.GetSpecificCulture(), out double value) ? value : GetMinimum(box);

    private static void SetNumber(TextBox box, double value)
    {
        value = Math.Round(Math.Max(GetMinimum(box), value), 2);
        box.Text = value.ToString(box.Language.GetSpecificCulture());
        box.GetBindingExpression(TextBox.TextProperty)?.UpdateSource(); // without the typing delay
    }

    // The box is bound to a number with decimals (millimetres), not to a whole one (copies, digits).
    private static bool AcceptsDecimals(TextBox box)
    {
        var binding = box.GetBindingExpression(TextBox.TextProperty);
        var type = binding?.ResolvedSource?.GetType().GetProperty(binding.ResolvedSourcePropertyName ?? "")?.PropertyType;
        return type == typeof(double) || type == typeof(double?);
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
