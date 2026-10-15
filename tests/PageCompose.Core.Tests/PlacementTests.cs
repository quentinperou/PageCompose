using PageCompose.Core.Layout;
using PageCompose.Core.Visuals;

namespace PageCompose.Core.Tests;

public class PlacementTests
{
    private static readonly LayoutCell LandscapeCard = new(0, 0, new RectMm(10, 10, 85, 55), new RectMm(10, 10, 85, 55));

    private static VisualInfo Image(double w, double h, int rotation = 0, bool mirror = false) => new()
    {
        Path = "x.jpg",
        Kind = VisualKind.Image,
        FullWidth = w,
        FullHeight = h,
        Content = new RawBox(0, 0, w, h),
        BaseRotation = rotation,
        BaseMirror = mirror,
    };

    [Fact]
    public void Landscape_image_in_landscape_cell_is_not_rotated_and_covers_the_cell()
    {
        var p = Placement.Compute(Image(1200, 800), LandscapeCard, FitMode.Fill);

        Assert.Equal(0, p.AngleDeg);
        double k = Math.Max(85.0 / 1200, 55.0 / 800);
        Assert.Equal(1200 * k, p.DrawRect.Width, 6);
        Assert.Equal(-600 * k, p.DrawRect.X, 6);
        Assert.Equal(LandscapeCard.Trim.CenterX, p.CenterX, 6);
        Assert.Equal(25.4 / k, p.EffectiveDpi, 6);
        Assert.True(p.DrawRect.Width >= 85 - 1e-9 && p.DrawRect.Height >= 55 - 1e-9);
    }

    [Fact]
    public void A_visual_with_its_own_fit_ignores_the_layout_s()
    {
        var photo = Image(1200, 800); // 3:2 in an 85 x 55 cell: cropped when filled, white bands when fitted

        var filled = Placement.Compute(photo, LandscapeCard, FitMode.Fill);
        var fitted = Placement.Compute(photo with { Fit = FitMode.Fit }, LandscapeCard, FitMode.Fill);
        var filledAnyway = Placement.Compute(photo with { Fit = FitMode.Fill }, LandscapeCard, FitMode.Fit);

        Assert.Equal(55, fitted.DrawRect.Height, 6);       // whole: its height fits the cell, bands left and right
        Assert.True(fitted.DrawRect.Width < 85);
        Assert.True(filled.DrawRect.Height > 55);          // the layout's Fill
        Assert.Equal(filled.DrawRect, filledAnyway.DrawRect);
        Assert.Equal(Placement.Dragged(photo with { Fit = FitMode.Fit }, LandscapeCard, FitMode.Fill, SideOptions.Recto, 5, 0),
            Placement.Dragged(photo, LandscapeCard, FitMode.Fit, SideOptions.Recto, 5, 0));
    }

    // ---- Mirrored bleed ----

    private static readonly LayoutCell CardWithBleed = new(0, 0, new RectMm(10, 10, 85, 55), new RectMm(9.5, 9.5, 86, 56));
    private static readonly SideOptions MirrorSide = SideOptions.Recto with { MirrorBleed = true };

    [Fact]
    public void Mirrored_bleed_keeps_the_photo_to_its_trim_and_mirrors_the_sides_it_does_not_cover()
    {
        // 3:2 in 85 x 55: filled, it overflows the trim by 0.8 mm above and below (more than the 0.5 mm bleed), and
        // its sides end on the trim.
        var p = Placement.Compute(Image(1200, 800), CardWithBleed, FitMode.Fill, MirrorSide);

        Assert.Equal(85, p.DrawRect.Width, 6);
        Assert.Equal(CardWithBleed.Bleed, p.Clip);
        Assert.Equal(new[] { (-1, 0), (1, 0) }, p.MirrorCopies);
        Assert.Equal(25.4 / (85.0 / 1200), p.EffectiveDpi, 6);
    }

    [Fact]
    public void Mirrored_copies_follow_the_turn_of_the_photo()
    {
        // A portrait photo turned a quarter: its own sides, above and below in its axes, lie left and right on the sheet.
        var p = Placement.Compute(Image(800, 1200), CardWithBleed, FitMode.Fill, MirrorSide);

        Assert.Equal(90, p.AngleDeg % 180);
        Assert.Equal(new[] { (0, -1), (0, 1) }, p.MirrorCopies);
    }

    [Fact]
    public void Without_the_option_or_when_fitted_nothing_is_mirrored()
    {
        var enlarged = Placement.Compute(Image(1200, 800), CardWithBleed, FitMode.Fill);
        var fitted = Placement.Compute(Image(1200, 800), CardWithBleed, FitMode.Fit, MirrorSide);

        Assert.Empty(enlarged.MirrorCopies);
        Assert.True(enlarged.DrawRect.Width > 85.9); // enlarged to cover the bleed
        Assert.Empty(fitted.MirrorCopies);
    }

    // ---- The part kept (dragged on the preview) ----

    private static readonly LayoutCell PortraitPhoto = new(0, 0, new RectMm(10, 10, 100, 150), new RectMm(10, 10, 100, 150));

    [Theory]
    [InlineData(0.0, -50)]  // its left edge on the left of the cell
    [InlineData(0.5, -60)]  // centred, as before
    [InlineData(1.0, -70)]  // its right edge on the right of the cell
    public void The_part_kept_slides_the_visual_along_what_overflows(double cropX, double drawX)
    {
        // An upright 4:5 photo in an upright 2:3 cell (Fill): 120 mm wide for a cell of 100.
        var p = Placement.Compute(Image(800, 1000) with { CropX = cropX, CropY = 0 }, PortraitPhoto, FitMode.Fill);

        Assert.Equal(0, p.AngleDeg);
        Assert.Equal(120, p.DrawRect.Width, 6);
        Assert.Equal(drawX, p.DrawRect.X, 6);
        Assert.Equal(-75, p.DrawRect.Y, 6); // nothing overflows down: CropY changes nothing
    }

    [Fact]
    public void In_fit_mode_the_part_kept_aligns_the_visual()
    {
        var p = Placement.Compute(Image(1000, 1000) with { CropX = 0 }, LandscapeCard, FitMode.Fit);

        // 55 mm wide in a cell of 85: against its left edge.
        Assert.Equal(-85 / 2.0, p.DrawRect.X, 6);
    }

    [Theory]
    [InlineData(0, false, 30, 0)]
    [InlineData(0, false, 0, 30)]
    [InlineData(90, false, 30, 0)]
    [InlineData(90, false, 0, -30)]
    [InlineData(0, true, -30, 0)]
    [InlineData(270, true, 0, 30)]
    public void A_drag_moves_the_visual_with_the_mouse(int rotation, bool mirror, double dx, double dy)
    {
        // Whatever the turn (EXIF, automatic) and the mirror, the visual follows the drag on the sheet, along what
        // overflows its cell.
        foreach (var photo in new[] { Image(1200, 800, rotation, mirror), Image(800, 1000, rotation, mirror) })
        foreach (var cell in new[] { PortraitPhoto, LandscapeCard })
        {
            var before = Placement.Compute(photo, cell, FitMode.Fill);
            var (x, y) = Placement.Dragged(photo, cell, FitMode.Fill, SideOptions.Recto, dx, dy);
            var after = Placement.Compute(photo with { CropX = x, CropY = y }, cell, FitMode.Fill);

            var (bx, by) = CentreOnSheet(before);
            var (ax, ay) = CentreOnSheet(after);
            // Only along the side that overflows, and no further than it allows (from the centre: half of it).
            double movable = OverflowOnSheet(before, cell) / 2;
            Assert.Equal(OverflowsAcross(before, cell) ? Math.Clamp(dx, -movable, movable) : 0, ax - bx, 6);
            Assert.Equal(OverflowsDown(before, cell) ? Math.Clamp(dy, -movable, movable) : 0, ay - by, 6);
        }
    }

    [Fact]
    public void A_drag_never_goes_past_the_edges_of_the_visual()
    {
        var photo = Image(800, 1000);

        Assert.Equal((1.0, 0.5), Placement.Dragged(photo, PortraitPhoto, FitMode.Fill, SideOptions.Recto, -500, 0));
        Assert.Equal((0.0, 0.5), Placement.Dragged(photo, PortraitPhoto, FitMode.Fill, SideOptions.Recto, 500, 0));
        Assert.Equal((0.25, 0.5), Placement.Dragged(photo, PortraitPhoto, FitMode.Fill, SideOptions.Recto, 5, 0)); // 5 of 20 mm
    }

    // Where the centre of the visual lands on the sheet: its draw rectangle mirrored, turned, then moved.
    private static (double X, double Y) CentreOnSheet(VisualPlacement p)
    {
        double x = p.DrawRect.X + p.DrawRect.Width / 2, y = p.DrawRect.Y + p.DrawRect.Height / 2;
        if (p.Mirror) x = -x;
        double a = p.AngleDeg * Math.PI / 180;
        return (p.CenterX + x * Math.Cos(a) - y * Math.Sin(a), p.CenterY + x * Math.Sin(a) + y * Math.Cos(a));
    }

    private static (double W, double H) SizeOnSheet(VisualPlacement p) =>
        p.AngleDeg % 180 == 90 ? (p.DrawRect.Height, p.DrawRect.Width) : (p.DrawRect.Width, p.DrawRect.Height);

    private static bool OverflowsAcross(VisualPlacement p, LayoutCell cell) => SizeOnSheet(p).W > cell.Trim.Width + 1e-6;
    private static bool OverflowsDown(VisualPlacement p, LayoutCell cell) => SizeOnSheet(p).H > cell.Trim.Height + 1e-6;

    private static double OverflowOnSheet(VisualPlacement p, LayoutCell cell) =>
        Math.Max(SizeOnSheet(p).W - cell.Trim.Width, SizeOnSheet(p).H - cell.Trim.Height);

    [Fact]
    public void Fit_mode_keeps_the_whole_image_inside_the_cell()
    {
        var p = Placement.Compute(Image(1000, 1000), LandscapeCard, FitMode.Fit);

        Assert.Equal(55, p.DrawRect.Width, 6);
        Assert.Equal(55, p.DrawRect.Height, 6);
    }

    [Fact]
    public void Portrait_image_is_turned_a_quarter_in_a_landscape_cell()
    {
        var p = Placement.Compute(Image(800, 1200), LandscapeCard, FitMode.Fill);

        Assert.Equal(90, p.AngleDeg);
        // After the quarter turn, 1200 px cover 85 mm and 800 px cover 55 mm.
        double k = Math.Max(85.0 / 1200, 55.0 / 800);
        Assert.Equal(800 * k, p.DrawRect.Width, 6);
    }

    [Fact]
    public void Exif_rotation_is_combined_with_the_sheet_rotation()
    {
        // Stored landscape but EXIF 6: the photo is actually portrait.
        var p = Placement.Compute(Image(1200, 800, rotation: 90), LandscapeCard, FitMode.Fill);

        Assert.Equal(180, p.AngleDeg); // 90 (EXIF) + 90 (portrait in a landscape cell)
    }

    [Theory]
    [InlineData(800, 1200, DuplexFlip.LongEdge, false, 270)]
    [InlineData(1200, 800, DuplexFlip.LongEdge, false, 0)]
    [InlineData(1200, 800, DuplexFlip.ShortEdge, false, 180)]
    [InlineData(800, 1200, DuplexFlip.ShortEdge, false, 90)]
    [InlineData(1200, 800, DuplexFlip.LongEdge, true, 180)]
    public void Verso_orientation_follows_the_flip(double w, double h, DuplexFlip flip, bool rotate180, int expected)
    {
        var p = Placement.Compute(Image(w, h), LandscapeCard, FitMode.Fill, new SideOptions(true, flip, rotate180));

        Assert.Equal(expected, p.AngleDeg);
    }

    [Fact]
    public void Verso_cells_are_mirrored_across_the_flip_axis()
    {
        var sheet = new SizeMm(320, 450);
        var cell = new LayoutCell(0, 0, new RectMm(20, 30, 85, 55), new RectMm(18, 28, 89, 59));

        var longEdge = Placement.MirrorForVerso(cell, sheet, DuplexFlip.LongEdge);
        var shortEdge = Placement.MirrorForVerso(cell, sheet, DuplexFlip.ShortEdge);

        Assert.Equal(320 - 20 - 85, longEdge.Trim.X, 6);
        Assert.Equal(30, longEdge.Trim.Y, 6);
        Assert.Equal(450 - 30 - 55, shortEdge.Trim.Y, 6);
        Assert.Equal(20, shortEdge.Trim.X, 6);
    }

    [Fact]
    public void Pdf_with_own_bleed_is_scaled_on_its_trim_box_and_clipped_to_the_cell_bleed()
    {
        // 91 x 61 mm PDF whose 85 x 55 TrimBox is inset by 3 mm; layout with 2 mm bleed.
        double pt = 72 / 25.4;
        var pdf = new VisualInfo
        {
            Path = "x.pdf",
            Kind = VisualKind.Pdf,
            FullWidth = 91 * pt,
            FullHeight = 61 * pt,
            Content = new RawBox(3 * pt, 3 * pt, 85 * pt, 55 * pt),
        };
        var cell = new LayoutCell(0, 0, new RectMm(10, 10, 85, 55), new RectMm(8, 8, 89, 59));

        var p = Placement.Compute(pdf, cell, FitMode.Fill);

        Assert.Equal(91, p.DrawRect.Width, 6);   // 100 % scale: the PDF trim lands on the layout trim
        Assert.Equal(-45.5, p.DrawRect.X, 6);
        Assert.Equal(cell.Bleed, p.Clip);
        Assert.True(double.IsNaN(p.EffectiveDpi));
    }

    [Fact]
    public void Image_without_bleed_is_enlarged_to_cover_the_bleed_area()
    {
        var cell = new LayoutCell(0, 0, new RectMm(10, 10, 85, 55), new RectMm(8, 8, 89, 59));

        var p = Placement.Compute(Image(850, 550), cell, FitMode.Fill);

        Assert.True(p.DrawRect.Width >= 89 - 1e-9 && p.DrawRect.Height >= 59 - 1e-9);
        Assert.Equal(cell.Bleed.CenterX, p.CenterX, 6);
    }

    // ---- Zoom into the photo (the slider of its line) ----

    [Fact]
    public void Zoom_enlarges_the_photo_around_the_part_kept_and_lowers_its_resolution()
    {
        var photo = Image(1200, 800);
        var asLaid = Placement.Compute(photo, LandscapeCard, FitMode.Fill);
        var zoomed = Placement.Compute(photo with { Zoom = 2 }, LandscapeCard, FitMode.Fill);

        Assert.Equal(asLaid.DrawRect.Width * 2, zoomed.DrawRect.Width, 6);
        Assert.Equal(asLaid.DrawRect.Height * 2, zoomed.DrawRect.Height, 6);
        Assert.Equal(0, zoomed.DrawRect.X + zoomed.DrawRect.Width / 2, 6); // still centred
        Assert.Equal(asLaid.EffectiveDpi / 2, zoomed.EffectiveDpi, 6);
        Assert.Equal(asLaid.Clip, zoomed.Clip);                            // still in its cell
    }

    [Fact]
    public void A_zoomed_photo_keeps_the_part_chosen_and_follows_a_drag()
    {
        var photo = Image(1200, 800) with { Zoom = 2 };

        // Its left edge kept: on the left edge of the 85 mm cell, drawn around its centre.
        Assert.Equal(-85 / 2.0, Placement.Compute(photo with { CropX = 0 }, LandscapeCard, FitMode.Fill).DrawRect.X, 6);

        var before = Placement.Compute(photo, LandscapeCard, FitMode.Fill);
        var (x, _) = Placement.Dragged(photo, LandscapeCard, FitMode.Fill, SideOptions.Recto, -10, 0);
        var after = Placement.Compute(photo with { CropX = x }, LandscapeCard, FitMode.Fill);
        Assert.Equal(before.DrawRect.X - 10, after.DrawRect.X, 6);
    }

    [Fact]
    public void A_fitted_photo_zoomed_grows_from_its_whole_size()
    {
        var photo = Image(1200, 800) with { Fit = FitMode.Fit };
        var whole = Placement.Compute(photo, LandscapeCard, FitMode.Fill);
        var zoomed = Placement.Compute(photo with { Zoom = 1.5 }, LandscapeCard, FitMode.Fill);

        Assert.Equal(whole.DrawRect.Width * 1.5, zoomed.DrawRect.Width, 6);
        Assert.Equal(LandscapeCard.Bleed, zoomed.Clip); // what goes beyond its cell is cut
    }

    [Fact]
    public void Zoom_stays_between_1_and_3()
    {
        var photo = Image(1200, 800);
        Assert.Equal(Placement.Compute(photo, LandscapeCard, FitMode.Fill).DrawRect,
            Placement.Compute(photo with { Zoom = 0.5 }, LandscapeCard, FitMode.Fill).DrawRect);
        Assert.Equal(Placement.Compute(photo with { Zoom = 3 }, LandscapeCard, FitMode.Fill).DrawRect,
            Placement.Compute(photo with { Zoom = 7 }, LandscapeCard, FitMode.Fill).DrawRect);
    }
}
