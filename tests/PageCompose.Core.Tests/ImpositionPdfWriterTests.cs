using PageCompose.Core.Layout;
using PageCompose.Core.Pdf;
using PageCompose.Core.Visuals;
using PdfSharp.Pdf;
using PdfSharp.Pdf.Advanced;
using PdfSharp.Pdf.Annotations;
using PdfSharp.Pdf.Content;
using PdfSharp.Pdf.Content.Objects;
using PdfSharp.Pdf.IO;

namespace PageCompose.Core.Tests;

public sealed class ImpositionPdfWriterTests : IDisposable
{
    private readonly TestFiles _files = new();

    private static SheetLayout CardsOnSra3(bool marks = true) =>
        LayoutEngine.Compute(new LayoutOptions(SheetFormats.Sra3, ItemFormats.BusinessCard.Size, 2, marks));

    [Fact]
    public void One_page_per_sheet_at_the_exact_sheet_size()
    {
        var jpg = VisualLoader.Load(_files.Jpeg(1063, 650));

        using var doc = WriteAndOpen(ImpositionJob.Repeat(CardsOnSra3(), jpg, 3));

        Assert.Equal(3, doc.PageCount);
        foreach (var page in doc.Pages)
        {
            Assert.Equal(320, page.Width.Millimeter, 2);
            Assert.Equal(450, page.Height.Millimeter, 2);
        }
    }

    [Fact]
    public void Jpeg_is_stored_once_and_not_recompressed()
    {
        var path = _files.Jpeg(1063, 650);
        var jpg = VisualLoader.Load(path);

        using var doc = WriteAndOpen(ImpositionJob.Repeat(CardsOnSra3(), jpg, 10));

        var images = ImageXObjects(doc);
        var image = Assert.Single(images);
        Assert.Equal("/DCTDecode", image.Elements.GetName("/Filter"));
        Assert.Equal(new FileInfo(path).Length, image.Stream.Length);
    }

    [Fact]
    public void Verso_adds_an_alternating_page_and_a_second_image()
    {
        var recto = VisualLoader.Load(_files.Jpeg(1063, 650, name: "recto.jpg"));
        var verso = VisualLoader.Load(_files.Jpeg(650, 1063, name: "verso.jpg"));

        using var doc = WriteAndOpen(ImpositionJob.Repeat(CardsOnSra3(), recto, 2, verso));

        Assert.Equal(4, doc.PageCount);
        Assert.Equal(2, ImageXObjects(doc).Count);
    }

    [Fact]
    public void Png_and_pdf_sources_are_supported()
    {
        var png = VisualLoader.Load(_files.Png(600, 400));
        var pdf = VisualLoader.Load(_files.Pdf(91, 61, trimInsetMm: 3));

        using var fromPng = WriteAndOpen(ImpositionJob.Repeat(CardsOnSra3(), png, 1));
        using var fromPdf1 = WriteAndOpen(ImpositionJob.Repeat(CardsOnSra3(false), pdf, 1));
        using var fromPdf5 = WriteAndOpen(ImpositionJob.Repeat(CardsOnSra3(false), pdf, 5));

        Assert.Single(ImageXObjects(fromPng));
        Assert.Equal(5, fromPdf5.PageCount);
        // The source page is imported once, whatever the number of sheets.
        Assert.NotEmpty(XObjects(fromPdf1, "/Form"));
        Assert.Equal(XObjects(fromPdf1, "/Form").Count, XObjects(fromPdf5, "/Form").Count);
    }

    [Fact]
    public void Two_page_pdf_gives_the_front_and_the_back()
    {
        var path = _files.Pdf(85, 55, name: "carte.pdf", pages: 2);
        var front = VisualLoader.Load(path);
        var back = VisualLoader.Load(path, 2);

        using var frontOnly = WriteAndOpen(ImpositionJob.Repeat(CardsOnSra3(false), front, 1));
        using var both = WriteAndOpen(ImpositionJob.Repeat(CardsOnSra3(false), front, 1, back));

        Assert.Equal(2, both.PageCount);
        // Page 2 of the file is imported as well: one more form than with the front only.
        Assert.True(XObjects(both, "/Form").Count > XObjects(frontOnly, "/Form").Count);
    }

    [Fact]
    public void Empty_layout_is_refused()
    {
        var jpg = VisualLoader.Load(_files.Jpeg(100, 100));
        var empty = LayoutEngine.Compute(new LayoutOptions(SheetFormats.A4, ItemFormats.A4.Size));

        Assert.Throws<InvalidOperationException>(() =>
            ImpositionPdfWriter.Write(ImpositionJob.Repeat(empty, jpg, 1), Stream.Null));
    }

    [Fact]
    public void Several_visuals_share_the_sheets_and_each_image_is_stored_once()
    {
        var a = VisualLoader.Load(_files.Jpeg(1063, 650, name: "a.jpg"));
        var b = VisualLoader.Load(_files.Jpeg(1000, 640, name: "b.jpg")); // content differs from a: PDFsharp merges identical images
        var c = VisualLoader.Load(_files.Png(600, 400, name: "c.png"));
        var layout = CardsOnSra3();
        var plan = SheetFiller.Fill([new(a, 30), new(b, 10), new(c, 1)], layout.PerSheet, completeLastSheet: false);

        using var doc = WriteAndOpen(new ImpositionJob(layout, plan.Sheets));

        Assert.Equal(2, doc.PageCount);          // 41 cards at 21 per sheet
        Assert.Equal(3, ImageXObjects(doc).Count);
    }

    [Fact]
    public void Verso_is_only_printed_behind_filled_cells()
    {
        var recto = VisualLoader.Load(_files.Jpeg(1063, 650, name: "recto.jpg"));
        var verso = VisualLoader.Load(_files.Jpeg(1063, 650, name: "verso.jpg"));
        var layout = CardsOnSra3();
        var plan = SheetFiller.Fill([new(recto, 3)], layout.PerSheet, completeLastSheet: false);

        var path = _files.Path("partiel.pdf");
        ImpositionPdfWriter.Write(new ImpositionJob(layout, plan.Sheets, verso), path);
        using var doc = PdfReader.Open(path, PdfDocumentOpenMode.Import);

        Assert.Equal(2, doc.PageCount);
        // The back only draws 3 cells: its content is much shorter than a full page.
        Assert.True(ContentLength(doc.Pages[1]) < ContentLength(doc.Pages[0]));
    }

    [Fact]
    public void Visuals_with_their_own_back_get_it_and_the_others_the_shared_one()
    {
        var a = VisualLoader.Load(_files.Jpeg(1063, 650, name: "a.jpg"));
        var b = VisualLoader.Load(_files.Jpeg(1000, 640, name: "b.jpg"));
        var backOfA = VisualLoader.Load(_files.Jpeg(900, 600, name: "a-verso.jpg"));
        var shared = VisualLoader.Load(_files.Png(600, 400, name: "shared.png"));
        var layout = CardsOnSra3();
        var plan = SheetFiller.Fill([new(a, 30), new(b, 10)], layout.PerSheet, completeLastSheet: false);
        var backs = plan.Sheets.Select(s => s.Select(v => v == a ? backOfA : null).ToArray()).ToList();
        var job = new ImpositionJob(layout, plan.Sheets, Backs: backs);

        using var ownOnly = WriteAndOpen(job);
        using var withShared = WriteAndOpen(job with { Verso = shared });

        Assert.True(job.HasBack);
        Assert.Same(backOfA, job.BackOf(0, 0));
        Assert.Null(job.BackOf(1, plan.Sheets[1].Length - 1));
        Assert.Equal(4, ownOnly.PageCount);           // 40 cards at 21 per sheet, each with its back page
        Assert.Equal(3, ImageXObjects(ownOnly).Count);
        Assert.Equal(4, ImageXObjects(withShared).Count);
        // Sheet 2 holds 9 a and 10 b: without a shared back, only the 9 a have one.
        Assert.True(ContentLength(ownOnly.Pages[3]) < ContentLength(withShared.Pages[3]));
    }

    [Fact]
    public void Backs_without_any_back_leave_the_sheets_single_sided()
    {
        var a = VisualLoader.Load(_files.Jpeg(1063, 650));
        var layout = CardsOnSra3();
        var job = ImpositionJob.Repeat(layout, a, 2) with { Backs = [new VisualInfo?[layout.PerSheet], new VisualInfo?[layout.PerSheet]] };

        using var doc = WriteAndOpen(job);

        Assert.False(job.HasBack);
        Assert.Equal(2, doc.PageCount);
    }

    [Fact]
    public void Backs_must_match_the_sheets()
    {
        var a = VisualLoader.Load(_files.Jpeg(1063, 650));
        var job = ImpositionJob.Repeat(CardsOnSra3(), a, 2) with { Backs = [new VisualInfo?[3]] };

        Assert.Throws<ArgumentException>(() => ImpositionPdfWriter.Write(job, Stream.Null));
    }

    [Fact]
    public void The_mark_of_each_sheet_is_written_on_its_front_only()
    {
        var recto = VisualLoader.Load(_files.Jpeg(1063, 650, name: "recto.jpg"));
        var verso = VisualLoader.Load(_files.Jpeg(1000, 640, name: "verso.jpg"));
        var job = ImpositionJob.Repeat(CardsOnSra3(), recto, 2, verso);

        using var plain = WriteAndOpen(job);
        using var marked = WriteAndOpen(job with { Marks = ["dupont · 11/10/2026 · sheet 1/2", "dupont · 11/10/2026 · sheet 2/2"] });

        Assert.True(ContentLength(marked.Pages[0]) > ContentLength(plain.Pages[0]));
        Assert.Equal(ContentLength(plain.Pages[1]), ContentLength(marked.Pages[1]));
        Assert.Throws<ArgumentException>(() => ImpositionPdfWriter.Write(job with { Marks = ["one mark"] }, Stream.Null));
    }

    [Fact]
    public void Cut_lines_are_drawn_on_the_front_only()
    {
        var recto = VisualLoader.Load(_files.Jpeg(1063, 650, name: "recto.jpg"));
        var verso = VisualLoader.Load(_files.Jpeg(1000, 640, name: "verso.jpg"));
        var job = ImpositionJob.Repeat(CardsOnSra3(marks: false), recto, 1, verso);

        using var plain = WriteAndOpen(job);
        using var lined = WriteAndOpen(job with { CutLines = true });

        Assert.True(ContentLength(lined.Pages[0]) > ContentLength(plain.Pages[0]));
        Assert.Equal(ContentLength(plain.Pages[1]), ContentLength(lined.Pages[1]));
    }

    [Fact]
    public void A_space_wider_than_the_bleed_is_painted_white_on_both_sides()
    {
        var card = VisualLoader.Load(_files.Jpeg(1063, 650, name: "recto.jpg"));
        var verso = VisualLoader.Load(_files.Jpeg(1000, 640, name: "verso.jpg"));
        var spaced = LayoutEngine.Compute(new LayoutOptions(SheetFormats.Sra3, ItemFormats.BusinessCard.Size, 0, true, 0.1));
        var bled = LayoutEngine.Compute(new LayoutOptions(SheetFormats.Sra3, ItemFormats.BusinessCard.Size, 2, true, 3));

        using var doc = WriteAndOpen(ImpositionJob.Repeat(spaced, card, 1, verso));
        using var covered = WriteAndOpen(ImpositionJob.Repeat(bled, card, 1));

        // One band between two columns, one between two rows (Windows would round a 0.1 mm space away).
        int bands = spaced.Columns - 1 + spaced.Rows - 1;
        Assert.Equal(bands, WhiteRectangles(doc.Pages[0]));
        Assert.Equal(bands, WhiteRectangles(doc.Pages[1]));
        // 3 mm between visuals with 2 mm of bleed: the bleeds fill the whole space.
        Assert.Equal(0, WhiteRectangles(covered.Pages[0]));
    }

    [Fact]
    public void Flat_pdf_has_one_picture_per_page_covering_the_sheet()
    {
        var front = _files.Jpeg(945, 1329, name: "front.jpg");
        var back = _files.Jpeg(944, 1328, name: "back.jpg"); // differs from the front: PDFsharp merges identical images
        var path = _files.Path("flat.pdf");

        using (var fs = File.Create(path))
            FlatPdfWriter.Write([front, back], SheetFormats.Sra3.Size, fs);
        using var doc = PdfReader.Open(path, PdfDocumentOpenMode.Import);

        Assert.Equal(2, doc.PageCount);
        Assert.All(doc.Pages.Cast<PdfPage>(), p =>
        {
            Assert.Equal(320, p.Width.Millimeter, 2);
            Assert.Equal(450, p.Height.Millimeter, 2);
        });
        var images = ImageXObjects(doc);
        Assert.Equal(2, images.Count);
        Assert.All(images, i => Assert.Equal("/DCTDecode", i.Elements.GetName("/Filter"))); // not recompressed
        Assert.Throws<ArgumentException>(() => FlatPdfWriter.Write([], SheetFormats.Sra3.Size, Stream.Null));
    }

    [Fact]
    public void Mixed_sheets_each_have_their_own_layout()
    {
        var photo = VisualLoader.Load(_files.Jpeg(1200, 1800, name: "photo.jpg"));
        var card = VisualLoader.Load(_files.Jpeg(1063, 650, name: "card.jpg"));
        var verso = VisualLoader.Load(_files.Jpeg(1000, 640, name: "verso.jpg"));
        var plan = MixedPacker.Pack(SheetFormats.Sra3,
            [new MixedItem(photo, ItemFormats.Photo15x20.Size, 3), new MixedItem(card, ItemFormats.BusinessCard.Size, 12)], 0, 3);
        Assert.Equal(2, plan.SheetCount);

        using var doc = WriteAndOpen(ImpositionJob.Mixed(plan) with { Verso = verso });
        using var second = WriteAndOpen(ImpositionJob.Mixed(plan, [1]));

        Assert.Equal(4, doc.PageCount);              // front and back of both sheets
        Assert.Equal(3, ImageXObjects(doc).Count);   // each picture once
        Assert.Equal(1, second.PageCount);
        // The spaces of each sheet are painted white, on the back too.
        Assert.Equal(plan.Sheets[0].Layout.Spaces.Count, WhiteRectangles(doc.Pages[0]));
        Assert.Equal(plan.Sheets[0].Layout.Spaces.Count, WhiteRectangles(doc.Pages[1]));
        Assert.Equal(plan.Sheets[1].Layout.Spaces.Count, WhiteRectangles(doc.Pages[2]));
    }

    [Fact]
    public void The_same_photo_in_two_sizes_is_stored_once()
    {
        // A line duplicated in the Mixed sizes mode: the same visual, another size.
        var photo = VisualLoader.Load(_files.Jpeg(1200, 1800, name: "photo.jpg"));
        var plan = MixedPacker.Pack(SheetFormats.Sra3,
            [new MixedItem(photo, ItemFormats.Photo10x15.Size, 2), new MixedItem(photo, ItemFormats.Photo20x30.Size, 1)]);

        using var doc = WriteAndOpen(ImpositionJob.Mixed(plan));

        Assert.Equal(3, plan.Printed);
        Assert.Contains(plan.Sheets.SelectMany(s => s.Layout.Cells), c => Math.Abs(c.Trim.Width * c.Trim.Height - 203.2 * 304.8) < 1e-6);
        Assert.Single(ImageXObjects(doc));
    }

    [Fact]
    public void Mixed_cut_lines_are_drawn_on_the_front_only()
    {
        var photo = VisualLoader.Load(_files.Jpeg(1200, 1800, name: "photo.jpg"));
        var verso = VisualLoader.Load(_files.Jpeg(1000, 640, name: "verso.jpg"));
        var plan = MixedPacker.Pack(SheetFormats.Sra3, [new MixedItem(photo, ItemFormats.Photo13x18.Size, 4)]);
        var job = ImpositionJob.Mixed(plan) with { Verso = verso };

        using var lined = WriteAndOpen(job);
        using var plain = WriteAndOpen(job with { CutLines = false });

        Assert.True(ContentLength(lined.Pages[0]) > ContentLength(plain.Pages[0]));
        Assert.Equal(ContentLength(plain.Pages[1]), ContentLength(lined.Pages[1]));
    }

    [Fact]
    public void Cutting_plan_has_one_A4_page_per_sheet()
    {
        var photo = VisualLoader.Load(_files.Jpeg(1200, 1800, name: "photo.jpg"));
        var card = VisualLoader.Load(_files.Jpeg(1063, 650, name: "card.jpg"));
        var plan = MixedPacker.Pack(SheetFormats.Sra3,
            [new MixedItem(photo, ItemFormats.Photo15x20.Size, 3), new MixedItem(card, ItemFormats.BusinessCard.Size, 12)], 0, 3);
        var texts = new CuttingPlanTexts("Cutting plan", "Sheet {0} / {1} · {2}", "Cut in the order of the numbers.",
            "{0} mm from the top", "{0} mm from the bottom", "{0} mm from the left", "{0} mm from the right", "From the edge named.");
        var path = _files.Path("plan.pdf");

        using (var fs = File.Create(path))
            CuttingPlanWriter.Write(plan, ["Photo 15 × 20", "Business card"], "SRA3", "job", texts, fs,
                logoPng: File.ReadAllBytes(_files.Png(64, 64, name: "logo.png")));
        using var doc = PdfReader.Open(path, PdfDocumentOpenMode.Import);

        Assert.Equal(plan.SheetCount, doc.PageCount);
        Assert.All(doc.Pages.Cast<PdfPage>(), p =>
        {
            Assert.Equal(210, p.Width.Millimeter, 1);
            Assert.Equal(297, p.Height.Millimeter, 1);
            // At the foot of the page: a link to the GitHub page of the app.
            var link = Assert.Single(p.Annotations.Cast<PdfAnnotation>());
            Assert.Equal("https://github.com/quentinperou/PageCompose", link.Elements.GetDictionary("/A")!.Elements.GetString("/URI"));
            Assert.InRange(link.Rectangle.Y1, 0, Units.MmToPt(20));
        });
        Assert.Equal(3, ImageXObjects(doc).Count); // the visuals and the icon, each once
        Assert.Throws<ArgumentException>(() => CuttingPlanWriter.Write(MixedPlan.Empty, [], "SRA3", null, texts, Stream.Null));
    }

    [Fact]
    public void Sheets_must_match_the_layout()
    {
        var jpg = VisualLoader.Load(_files.Jpeg(100, 100));
        var layout = CardsOnSra3();

        Assert.Throws<ArgumentException>(() =>
            ImpositionPdfWriter.Write(new ImpositionJob(layout, [new VisualInfo?[3]]), Stream.Null));
        Assert.Throws<ArgumentException>(() =>
            ImpositionPdfWriter.Write(new ImpositionJob(layout, []), Stream.Null));
        // Each sheet of a mixed job has its own layout.
        Assert.Throws<ArgumentException>(() =>
            ImpositionPdfWriter.Write(new ImpositionJob(layout, [new VisualInfo?[layout.PerSheet]], SheetLayouts: []), Stream.Null));
    }

    // Rectangles filled ("re" then "f") while the fill colour is white ("1 1 1 rg").
    private static int WhiteRectangles(PdfPage page)
    {
        var ops = ContentReader.ReadContent(page).OfType<COperator>().ToList();
        bool white = false;
        int count = 0;
        for (int i = 0; i < ops.Count; i++)
        {
            switch (ops[i].OpCode.Name)
            {
                case "rg":
                    white = ops[i].Operands.Count == 3 && ops[i].Operands.All(o => o is CReal { Value: 1 } or CInteger { Value: 1 });
                    break;
                case "g" or "k" or "sc" or "scn":
                    white = false;
                    break;
                case "re" when white && i + 1 < ops.Count && ops[i + 1].OpCode.Name is "f" or "f*":
                    count++;
                    break;
            }
        }
        return count;
    }

    private static int ContentLength(PdfPage page) =>
        page.Contents.Elements.Items.Sum(item => (item as PdfReference)?.Value is PdfDictionary d ? d.Stream?.Value.Length ?? 0 : 0);

    private PdfDocument WriteAndOpen(ImpositionJob job)
    {
        var path = _files.Path($"sortie-{Guid.NewGuid():N}.pdf");
        ImpositionPdfWriter.Write(job, path);
        return PdfReader.Open(path, PdfDocumentOpenMode.Import);
    }

    private static List<PdfDictionary> ImageXObjects(PdfDocument doc) => XObjects(doc, "/Image");

    private static List<PdfDictionary> XObjects(PdfDocument doc, string subtype) =>
        doc.Internals.GetAllObjects()
            .OfType<PdfDictionary>()
            .Where(d => d.Elements.GetName("/Subtype") == subtype)
            .ToList();

    public void Dispose() => _files.Dispose();
}
