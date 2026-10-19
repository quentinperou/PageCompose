using PageCompose.Core.Layout;
using PageCompose.Core.Pdf;
using PageCompose.Core.Visuals;
using PdfSharp.Pdf;
using PdfSharp.Pdf.Advanced;
using PdfSharp.Pdf.IO;

namespace PageCompose.Core.Tests;

public sealed class CaptionTests : IDisposable
{
    private readonly TestFiles _files = new();

    private static SheetLayout Polaroids() => LayoutEngine.Compute(new LayoutOptions(SheetFormats.Sra3, ItemFormats.Polaroid.Size,
        Margins: ItemFormats.Polaroid.Margins));

    [Fact]
    public void A_polaroid_writes_in_its_bottom_band()
    {
        var cell = Polaroids().Cells[0];

        var at = Caption.BandOf(cell);

        Assert.Equal(Caption.Side.Bottom, at?.Side);
        Assert.Equal(22, at!.Value.Band.Height, 9);
        Assert.Equal(cell.Trim.Bottom, at.Value.Band.Bottom, 9);
    }

    [Fact]
    public void A_frame_laid_a_quarter_turn_has_its_band_on_the_left()
    {
        var turned = new LayoutCell(0, 0, new RectMm(0, 0, 107, 88), new RectMm(0, 0, 107, 88), ItemFormats.Polaroid.Margins.RotatedClockwise, true);

        var at = Caption.BandOf(turned);

        Assert.Equal(Caption.Side.Left, at?.Side);
        Assert.Equal(22, at!.Value.Band.Width, 9);
    }

    [Fact]
    public void The_text_stands_as_its_frame_whatever_side_its_band_is_on()
    {
        var thickTop = new MarginsMm(22, 4.5, 6, 4.5);
        var upright = new LayoutCell(0, 0, new RectMm(0, 0, 88, 107), new RectMm(0, 0, 88, 107), thickTop);
        var turned = new LayoutCell(0, 0, new RectMm(0, 0, 107, 88), new RectMm(0, 0, 107, 88), thickTop.RotatedClockwise, true);

        Assert.Equal(Caption.Side.Top, Caption.BandOf(upright)?.Side);
        Assert.Equal(0, Caption.AngleOf(upright));
        Assert.Equal(Caption.Side.Right, Caption.BandOf(turned)?.Side);
        Assert.Equal(90, Caption.AngleOf(turned));
    }

    [Fact]
    public void Segoe_ui_chosen_for_the_captions_keeps_the_bold_of_the_app()
    {
        string file = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "segoeui.ttf");
        string family = WindowsFonts.Register("Segoe UI", file);
        var fonts = new WindowsFonts();

        Assert.NotEqual("Segoe UI", family);
        Assert.Equal(file, fonts.ResolveTypeface(family, false, false)?.FaceName);
        Assert.Equal("segoeuib", fonts.ResolveTypeface("Segoe UI", true, false)?.FaceName);
        Assert.Equal("segoeui", fonts.ResolveTypeface("Segoe UI", false, false)?.FaceName);
    }

    [Fact]
    public void A_thin_border_holds_no_text()
    {
        var layout = LayoutEngine.Compute(new LayoutOptions(SheetFormats.Sra3, ItemFormats.Square10Border.Size, Margins: ItemFormats.Square10Border.Margins));

        Assert.Null(Caption.BandOf(layout.Cells[0]));
    }

    [Fact]
    public void The_caption_is_written_with_its_font_embedded()
    {
        var photo = VisualLoader.Load(_files.Jpeg(800, 800));
        string font = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "segoeui.ttf");
        var job = ImpositionJob.Repeat(Polaroids(), photo with { Caption = "Léa & Tom · 2026" }, 1)
            with { Caption = new CaptionStyle("Caption test font", font, CaptionColor.Navy) };

        string withText = Content(job), without = Content(job with { Caption = null });

        Assert.Contains("BT", withText);   // text written
        Assert.DoesNotContain("BT", without);
    }

    private string Content(ImpositionJob job)
    {
        var path = _files.Path($"legendes-{Guid.NewGuid():N}.pdf");
        ImpositionPdfWriter.Write(job, path);
        using var doc = PdfReader.Open(path, PdfDocumentOpenMode.Import);
        return string.Concat(doc.Pages[0].Contents.Elements.Items.Select(item =>
            (item as PdfReference)?.Value is PdfDictionary d && d.Stream is { } s ? System.Text.Encoding.ASCII.GetString(s.UnfilteredValue) : ""));
    }

    public void Dispose() => _files.Dispose();
}
