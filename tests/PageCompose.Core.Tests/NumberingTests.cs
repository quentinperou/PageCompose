using PageCompose.Core.Layout;
using PageCompose.Core.Pdf;
using PageCompose.Core.Visuals;
using PdfSharp.Pdf;
using PdfSharp.Pdf.Advanced;
using PdfSharp.Pdf.IO;

namespace PageCompose.Core.Tests;

public sealed class NumberingTests : IDisposable
{
    private readonly TestFiles _files = new();

    private static readonly VisualInfo Ticket = new()
    {
        Path = "ticket.png", Kind = VisualKind.Image, FullWidth = 1500, FullHeight = 500, Content = new RawBox(0, 0, 1500, 500),
    };

    private static List<VisualInfo?[]> Sheets(int sheets, int cells, int lastFilled) =>
        [.. Enumerable.Range(0, sheets).Select(s => Enumerable.Range(0, cells)
            .Select(c => s < sheets - 1 || c < lastFilled ? Ticket : null).ToArray())];

    [Fact]
    public void In_a_pile_each_cell_goes_down_the_sheets()
    {
        // 3 sheets of 4: cell 1 holds 1, 2, 3 down the pile, cell 2 holds 4, 5, 6…
        var numbers = Numbering.Assign(Sheets(3, 4, 4), NumberingOrder.Pile, 1);

        Assert.Equal([1, 4, 7, 10], numbers[0].Select(n => n!.Value));
        Assert.Equal([2, 5, 8, 11], numbers[1].Select(n => n!.Value));
        Assert.Equal([3, 6, 9, 12], numbers[2].Select(n => n!.Value));
    }

    [Fact]
    public void By_sheet_the_numbers_follow_the_reading_order()
    {
        var numbers = Numbering.Assign(Sheets(2, 4, 4), NumberingOrder.BySheet, 101);

        Assert.Equal([101, 102, 103, 104], numbers[0].Select(n => n!.Value));
        Assert.Equal([105, 106, 107, 108], numbers[1].Select(n => n!.Value));
    }

    [Fact]
    public void A_last_sheet_partly_filled_gives_shorter_piles_and_no_gap()
    {
        // 10 visuals, 4 a sheet: the last sheet holds 2. The piles of cells 1 and 2 have 3 tickets, the others 2.
        var numbers = Numbering.Assign(Sheets(3, 4, 2), NumberingOrder.Pile, 1);

        Assert.Equal([1, 4, 7, 9], numbers[0].Select(n => n!.Value));
        Assert.Equal([2, 5, 8, 10], numbers[1].Select(n => n!.Value));
        Assert.Equal([3, 6, null, null], numbers[2]);
        Assert.Equal(Enumerable.Range(1, 10), numbers.SelectMany(s => s).OfType<int>().Order());
    }

    [Theory]
    [InlineData(3, "N° ", "", 7, "N° 007")]
    [InlineData(0, "", " / 500", 42, "42 / 500")]
    [InlineData(2, "A-", "", 123, "A-123")]
    public void The_text_of_a_number(int digits, string prefix, string suffix, int number, string expected)
    {
        var style = new NumberingStyle(prefix, digits, suffix, [new NumberPlace(5, 5)], "Segoe UI", "segoeui.ttf", 4, NumberColor.Black);

        Assert.Equal(expected, style.Text(number));
    }

    [Theory]
    [InlineData(NumberDirection.Horizontal, null)]
    [InlineData(NumberDirection.Vertical, NumberColor.White)]
    [InlineData(NumberDirection.HorizontalInverted, null)]
    [InlineData(NumberDirection.VerticalInverted, NumberColor.Black)]
    public void Numbers_are_written_on_the_front_only(NumberDirection direction, NumberColor? background)
    {
        var visual = VisualLoader.Load(_files.Png(600, 200, name: "ticket.png"));
        var verso = VisualLoader.Load(_files.Png(600, 200, name: "verso.png"));
        var layout = LayoutEngine.Compute(new LayoutOptions(SheetFormats.Sra3, new SizeMm(150, 50), CropMarks: true));
        var job = ImpositionJob.Repeat(layout, visual, 2, verso);
        string font = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "segoeui.ttf");
        var style = new NumberingStyle("N° ", 3, "", [new NumberPlace(117, 6, direction), new NumberPlace(6, 6, direction)],
            "Segoe UI", font, 4.4, NumberColor.Red, background);
        var numbered = job with { Numbering = style, Numbers = Numbering.Assign(job.Sheets, NumberingOrder.Pile, 1) };

        using var plain = WriteAndOpen(job);
        using var withNumbers = WriteAndOpen(numbered);

        Assert.True(ContentLength(withNumbers.Pages[0]) > ContentLength(plain.Pages[0]));   // the front of sheet 1
        Assert.Equal(ContentLength(plain.Pages[1]), ContentLength(withNumbers.Pages[1]));   // its back: nothing
        Assert.True(ContentLength(withNumbers.Pages[2]) > ContentLength(plain.Pages[2]));   // the front of sheet 2
    }

    [Fact]
    public void A_size_laid_a_quarter_turn_takes_its_numbers_along()
    {
        // ID photos laid turned on a 10 x 15 print (eight of them): written without fault, inside their cells.
        var visual = VisualLoader.Load(_files.Png(350, 450, name: "id.png"));
        var layout = LayoutEngine.Compute(new LayoutOptions(SheetFormats.Lab10x15, ItemFormats.IdPhoto.Size));
        Assert.Contains(layout.Cells, c => c.Rotated);
        var job = ImpositionJob.Repeat(layout, visual, 1);
        string font = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "segoeui.ttf");
        var style = new NumberingStyle("", 3, "", [new NumberPlace(20, 38)], "Segoe UI", font, 4, NumberColor.Black);

        using var doc = WriteAndOpen(job with { Numbering = style, Numbers = Numbering.Assign(job.Sheets, NumberingOrder.BySheet, 1) });

        Assert.Equal(1, doc.PageCount);
    }

    private static int ContentLength(PdfPage page) =>
        page.Contents.Elements.Items.Sum(item => (item as PdfReference)?.Value is PdfDictionary d ? d.Stream?.Value.Length ?? 0 : 0);

    private PdfDocument WriteAndOpen(ImpositionJob job)
    {
        var path = _files.Path($"sortie-{Guid.NewGuid():N}.pdf");
        ImpositionPdfWriter.Write(job, path);
        return PdfReader.Open(path, PdfDocumentOpenMode.Import);
    }

    public void Dispose() => _files.Dispose();
}
