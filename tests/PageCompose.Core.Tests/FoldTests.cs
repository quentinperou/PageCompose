using PageCompose.Core.Layout;
using PageCompose.Core.Pdf;
using PageCompose.Core.Visuals;
using PdfSharp.Pdf;
using PdfSharp.Pdf.Advanced;
using PdfSharp.Pdf.IO;

namespace PageCompose.Core.Tests;

public sealed class FoldTests : IDisposable
{
    private readonly TestFiles _files = new();

    private static SheetLayout Lay(PaperFormat format, bool foldMarks = true, bool cropMarks = false) =>
        LayoutEngine.Compute(new LayoutOptions(SheetFormats.Sra3, format.Size, 0, cropMarks, Folds: format.Folds, FoldMarks: foldMarks));

    [Fact]
    public void A_folded_a6_is_marked_in_the_middle_of_each_card()
    {
        var layout = Lay(ItemFormats.FoldedA6); // four A5 open, portrait

        Assert.Equal(4, layout.PerSheet);
        Assert.False(layout.ItemRotated);
        var folds = layout.Cells.Select(c => c.Trim.Y + 105).Distinct().ToList();
        Assert.Equal(4, layout.FoldMarks.Count);          // two rows, a mark on each side
        Assert.All(layout.FoldMarks, m => Assert.Contains(folds, y => Math.Abs(m.Y1 - y) < 1e-9 && Math.Abs(m.Y2 - y) < 1e-9));
        Assert.All(layout.FoldMarks, m => Assert.True(m.X2 < layout.Block!.Value.X || m.X2 > layout.Block!.Value.Right));
    }

    [Fact]
    public void The_folds_turn_with_the_card()
    {
        var layout = Lay(ItemFormats.FoldedSquare14); // 140 x 280 laid 280 x 140: three in a column

        Assert.True(layout.ItemRotated);
        double fold = layout.Cells[0].Trim.X + 140;
        Assert.Equal(2, layout.FoldMarks.Count);           // above and below the block
        Assert.All(layout.FoldMarks, m => Assert.Equal(fold, m.X1, 9));
    }

    [Fact]
    public void A_trifold_leaflet_has_its_narrower_flap()
    {
        var layout = Lay(ItemFormats.TrifoldA4);

        var x = layout.FoldMarks.Select(m => m.X1 - layout.Cells[0].Trim.X).Distinct().Order().ToList();
        Assert.Equal(new[] { 97.0, 197.0 }, x.Select(v => Math.Round(v, 6)));
    }

    [Fact]
    public void Without_fold_marks_there_is_none_and_no_room_kept()
    {
        var plain = Lay(ItemFormats.FoldedA6, foldMarks: false);
        var marked = Lay(ItemFormats.FoldedA6);

        Assert.Empty(plain.FoldMarks);
        Assert.True(plain.PrintableArea.Width > 0);
        Assert.True(plain.Block!.Value.X < marked.Block!.Value.X + 1e-9); // same centred block, room or not
        Assert.Empty(Lay(ItemFormats.Photo10x15).FoldMarks);              // a flat size has no fold
    }

    [Fact]
    public void Fold_marks_are_dashed_on_the_front()
    {
        var pdf = VisualLoader.Load(_files.Pdf(148, 210, name: "carte.pdf"));
        var job = ImpositionJob.Repeat(Lay(ItemFormats.FoldedA6), pdf, 1);
        var path = _files.Path("pliees.pdf");
        ImpositionPdfWriter.Write(job, path);

        using var doc = PdfReader.Open(path, PdfDocumentOpenMode.Import);
        string content = string.Concat(doc.Pages[0].Contents.Elements.Items.Select(item =>
            (item as PdfReference)?.Value is PdfDictionary d && d.Stream is { } s ? System.Text.Encoding.ASCII.GetString(s.UnfilteredValue) : ""));
        Assert.Matches(@"\[[\d. ]+\]\s*0 d", content);
    }

    public void Dispose() => _files.Dispose();
}
