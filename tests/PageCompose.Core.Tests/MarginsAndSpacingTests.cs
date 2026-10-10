using PageCompose.Core.Layout;
using PageCompose.Core.Visuals;

namespace PageCompose.Core.Tests;

public class MarginsAndSpacingTests
{
    private static VisualInfo Image(double w, double h) => new()
    {
        Path = "x.jpg",
        Kind = VisualKind.Image,
        FullWidth = w,
        FullHeight = h,
        Content = new RawBox(0, 0, w, h),
    };

    // ---- Spacing ----

    [Fact]
    public void Spacing_separates_the_trims_of_neighbouring_visuals()
    {
        var layout = LayoutEngine.Compute(new LayoutOptions(SheetFormats.Sra3, ItemFormats.BusinessCard.Size, SpacingMm: 5));

        // 311.8 x 441.8 mm printable: 3 columns of 85 and 7 rows of 55, 5 mm apart.
        Assert.Equal(21, layout.PerSheet);
        Assert.False(layout.ItemRotated);
        var row0 = layout.Cells.Where(c => c.Row == 0).OrderBy(c => c.Column).ToList();
        Assert.Equal(5, row0[1].Trim.X - row0[0].Trim.Right, 6);
        Assert.Equal(2 * layout.Columns, layout.CutsX.Count); // two cuts per visual
    }

    [Theory]
    [InlineData(5, 2, 5)]  // the bleed fits in the space
    [InlineData(3, 2, 4)]  // the bleed needs more room than the space
    [InlineData(0, 2, 4)]
    [InlineData(5, 0, 5)]
    public void Gap_is_the_spacing_but_never_less_than_the_bleeds(double spacing, double bleed, double expected)
    {
        var options = new LayoutOptions(SheetFormats.Sra3, ItemFormats.BusinessCard.Size, bleed, SpacingMm: spacing);
        var layout = LayoutEngine.Compute(options);

        Assert.Equal(expected, options.Gap, 6);
        Assert.Equal(expected, layout.Cells[1].Trim.X - layout.Cells[0].Trim.Right, 6);
    }

    [Fact]
    public void Negative_spacing_is_rejected() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            LayoutEngine.Compute(new LayoutOptions(SheetFormats.Sra3, ItemFormats.BusinessCard.Size, SpacingMm: -1)));

    // ---- Margins ----

    [Fact]
    public void Margins_turn_clockwise_and_mirror()
    {
        var m = new MarginsMm(1, 2, 3, 4);

        Assert.Equal(new MarginsMm(4, 1, 2, 3), m.RotatedClockwise);
        Assert.Equal(m, m.RotatedClockwise.RotatedClockwise.RotatedClockwise.RotatedClockwise);
        Assert.Equal(new MarginsMm(1, 4, 3, 2), m.MirrorX);
        Assert.Equal(new MarginsMm(3, 2, 1, 4), m.MirrorY);
        Assert.True(m.IsAsymmetric);
        Assert.False(new MarginsMm(5, 3, 5, 3).IsAsymmetric);
        Assert.True(MarginsMm.None.IsEmpty);
    }

    [Theory]
    [InlineData("Polaroid", 79, 79, true)]
    [InlineData("InstaxMini", 46, 62, true)]
    [InlineData("InstaxSquare", 62, 62, true)]
    [InlineData("InstaxWide", 99, 62, true)]
    [InlineData("Square10Border", 90, 90, false)]
    public void Framed_sizes_leave_the_expected_image_area(string id, double imageWidth, double imageHeight, bool thickBottom)
    {
        var format = ItemFormats.All.Single(f => f.Id == id);
        var image = new RectMm(0, 0, format.Width, format.Height).Deflate(format.Margins);

        Assert.Equal(imageWidth, image.Width, 6);
        Assert.Equal(imageHeight, image.Height, 6);
        Assert.Equal(thickBottom, format.Margins.Bottom > format.Margins.Top);
    }

    [Fact]
    public void Margins_turn_with_a_rotated_layout()
    {
        var wide = ItemFormats.InstaxWide;
        var layout = LayoutEngine.Compute(new LayoutOptions(SheetFormats.Sra3, wide.Size, Margins: wide.Margins));

        // 12 portrait (86 x 108) instead of 10 landscape: the frame is laid a quarter turn clockwise.
        Assert.Equal(12, layout.PerSheet);
        Assert.True(layout.ItemRotated);
        var cell = layout.Cells[0];
        Assert.True(cell.Rotated);
        Assert.Equal(wide.Margins.RotatedClockwise, cell.Margins);
        Assert.Equal(18, cell.Margins.Left, 6); // the thick bottom border is now on the left
        Assert.Equal(62, cell.ImageTrim.Width, 6);
        Assert.Equal(99, cell.ImageTrim.Height, 6);
    }

    [Theory]
    [InlineData(30, 0, 25, 0)]  // 55 mm of margins on a 55 mm high card
    [InlineData(0, 45, 0, 40)]  // 85 mm on a 85 mm wide card
    [InlineData(-1, 0, 0, 0)]
    public void Margins_that_leave_no_image_are_rejected(double top, double right, double bottom, double left) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => LayoutEngine.Compute(
            new LayoutOptions(SheetFormats.Sra3, ItemFormats.BusinessCard.Size, Margins: new MarginsMm(top, right, bottom, left))));

    [Fact]
    public void Image_goes_inside_the_margins_and_skips_the_bleed_there()
    {
        // 85 x 55 card, 2 mm bleed, white band of 10 mm at the bottom only.
        var cell = new LayoutCell(0, 0, new RectMm(10, 10, 85, 55), new RectMm(8, 8, 89, 59), new MarginsMm(0, 0, 10, 0));

        var p = Placement.Compute(Image(1200, 800), cell, FitMode.Fill);

        Assert.Equal(new RectMm(8, 8, 89, 47), p.Clip); // bleed on three sides, stops at the white band
        Assert.Equal(cell.ImageBleed, p.Clip);
        Assert.True(p.DrawRect.Width >= 89 - 1e-9 && p.DrawRect.Height >= 47 - 1e-9);
        Assert.Equal(31.5, p.CenterY, 6);
    }

    [Fact]
    public void Polaroid_photo_stays_upright_in_its_frame()
    {
        var polaroid = ItemFormats.Polaroid;
        var upright = new LayoutCell(0, 0, new RectMm(10, 10, 88, 107), new RectMm(10, 10, 88, 107), polaroid.Margins);
        var turned = new LayoutCell(0, 0, new RectMm(10, 10, 107, 88), new RectMm(10, 10, 107, 88),
            polaroid.Margins.RotatedClockwise, Rotated: true);

        var p = Placement.Compute(Image(1200, 800), upright, FitMode.Fill);
        Assert.Equal(0, p.AngleDeg);
        Assert.Equal(new RectMm(14.5, 16, 79, 79), p.Clip); // the square image area, no bleed into the frame

        // Laid a quarter turn, the square image area fits a landscape photo either way: it turns with the frame,
        // its bottom towards the thick border.
        var q = Placement.Compute(Image(1200, 800), turned, FitMode.Fill);
        Assert.Equal(90, q.AngleDeg);
        Assert.Equal(new RectMm(10 + 22, 10 + 4.5, 79, 79), q.Clip);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(0.1)]
    [InlineData(0.5)]
    [InlineData(1)]
    public void A_square_image_area_turns_photos_the_same_way_in_every_frame(double spacing)
    {
        // The edges of each cell are sums: rounding leaves the 79 x 79 image area of a Polaroid a hair wider or taller
        // depending on where the cell lies. A photo must not turn in some frames and not in others.
        var polaroid = ItemFormats.Polaroid;
        var layout = LayoutEngine.Compute(new LayoutOptions(SheetFormats.Sra3, polaroid.Size, 0, true, spacing, polaroid.Margins));

        foreach (var photo in new[] { Image(1200, 800), Image(800, 1200) })
            Assert.Single(layout.Cells.Select(cell => Placement.Compute(photo, cell, FitMode.Fill).AngleDeg).Distinct());
    }

    [Fact]
    public void A_photo_turns_by_its_proportions_in_a_frame_too()
    {
        // No exception for frames (the author's choice): a landscape photo turns to fill the upright 46 x 62 image
        // area of an Instax Mini, a portrait one does not. A photo the wrong way is turned by hand.
        var mini = ItemFormats.InstaxMini;
        var cell = new LayoutCell(0, 0, new RectMm(10, 10, 54, 86), new RectMm(10, 10, 54, 86), mini.Margins);

        Assert.Equal(90, Placement.Compute(Image(1200, 800), cell, FitMode.Fill).AngleDeg);
        Assert.Equal(0, Placement.Compute(Image(800, 1200), cell, FitMode.Fill).AngleDeg);
    }

    [Theory]
    [InlineData(152, 102, 0, 0)]     // landscape photo, landscape cell: no automatic turn
    [InlineData(152, 102, 90, 90)]   // turned by hand: as shown
    [InlineData(102, 152, 0, 90)]    // portrait cell: the automatic quarter turn
    [InlineData(102, 152, 90, 180)]  // and the turn by hand on top of it
    [InlineData(102, 152, 270, 0)]
    public void A_turn_by_hand_comes_on_top_of_the_automatic_one(double cellWidth, double cellHeight, int byHand, int expected)
    {
        // The context menu of a file line turns the visual as the sheet shows it: the automatic turn never undoes it.
        var cell = new LayoutCell(0, 0, new RectMm(10, 10, cellWidth, cellHeight), new RectMm(10, 10, cellWidth, cellHeight));

        var p = Placement.Compute(Image(1200, 800) with { UserRotation = byHand }, cell, FitMode.Fill);

        Assert.Equal(expected, p.AngleDeg);
    }

    [Fact]
    public void Symmetric_margins_keep_the_usual_orientation()
    {
        var cell = new LayoutCell(0, 0, new RectMm(10, 10, 152, 102), new RectMm(10, 10, 152, 102), new MarginsMm(5, 5, 5, 5), Rotated: true);

        Assert.Equal(0, Placement.Compute(Image(1200, 800), cell, FitMode.Fill).AngleDeg);
        Assert.Equal(0, Placement.Compute(Image(1000, 1000), cell, FitMode.Fill).AngleDeg);
    }

    [Fact]
    public void Back_ignores_the_margins_of_the_front()
    {
        var cell = new LayoutCell(0, 0, new RectMm(10, 10, 88, 107), new RectMm(8, 8, 92, 111), ItemFormats.Polaroid.Margins);

        var p = Placement.Compute(Image(800, 1200), cell, FitMode.Fill, new SideOptions(true));

        Assert.Equal(cell.Bleed, p.Clip);
    }

    [Theory]
    [InlineData(DuplexFlip.LongEdge, 0)]
    [InlineData(DuplexFlip.ShortEdge, 180)]
    public void Back_can_keep_the_margins_with_the_thick_border_under_the_visual(DuplexFlip flip, int angle)
    {
        var sheet = new SizeMm(320, 450);
        var front = new LayoutCell(0, 0, new RectMm(10, 10, 88, 107), new RectMm(10, 10, 88, 107), ItemFormats.Polaroid.Margins);
        var back = Placement.MirrorForVerso(front, sheet, flip);

        var p = Placement.Compute(Image(1200, 800), back, FitMode.Fill, new SideOptions(true, flip, VersoMargins: true));

        Assert.Equal(back.ImageBleed, p.Clip);
        Assert.Equal(angle, p.AngleDeg);
        // Long edge: the back is upright, its thick border at the bottom. Short edge: the back is upside down on the
        // page, so its thick border is at the top of the page, still under the visual.
        Assert.Equal(flip == DuplexFlip.LongEdge ? 22 : 6, back.Margins.Bottom, 6);
    }

    [Fact]
    public void Back_cell_mirrors_its_margins_with_the_flip()
    {
        var sheet = new SizeMm(320, 450);
        var cell = new LayoutCell(0, 0, new RectMm(20, 30, 85, 55), new RectMm(20, 30, 85, 55), new MarginsMm(1, 2, 3, 4));

        Assert.Equal(new MarginsMm(1, 4, 3, 2), Placement.MirrorForVerso(cell, sheet, DuplexFlip.LongEdge).Margins);
        Assert.Equal(new MarginsMm(3, 2, 1, 4), Placement.MirrorForVerso(cell, sheet, DuplexFlip.ShortEdge).Margins);
    }
}
