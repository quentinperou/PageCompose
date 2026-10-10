using PageCompose.Core.Visuals;
using PdfSharp.Drawing;

namespace PageCompose.Core.Tests;

public sealed class VisualLoaderTests : IDisposable
{
    private readonly TestFiles _files = new();

    [Fact]
    public void Jpeg_dimensions_are_read_from_header()
    {
        var v = VisualLoader.Load(_files.Jpeg(640, 480));

        Assert.Equal(VisualKind.Image, v.Kind);
        Assert.Equal((640.0, 480.0), (v.FullWidth, v.FullHeight));
        Assert.Equal(0, v.BaseRotation);
        Assert.False(v.HasOwnBleed);
    }

    [Theory]
    [InlineData(1, 0, false)]
    [InlineData(3, 180, false)]
    [InlineData(6, 90, false)]
    [InlineData(8, 270, false)]
    [InlineData(2, 0, true)]
    [InlineData(5, 270, true)]
    public void Jpeg_exif_orientation_is_applied(int exif, int rotation, bool mirror)
    {
        var v = VisualLoader.Load(_files.Jpeg(400, 300, exif, $"exif{exif}.jpg"));

        Assert.Equal(rotation, v.BaseRotation);
        Assert.Equal(mirror, v.BaseMirror);
        var expectedUpright = rotation % 180 == 90 ? (300.0, 400.0) : (400.0, 300.0);
        Assert.Equal(expectedUpright, v.UprightContentSize);
    }

    [Fact]
    public void Png_dimensions_are_read_from_header()
    {
        var v = VisualLoader.Load(_files.Png(320, 200));

        Assert.Equal((320.0, 200.0), (v.FullWidth, v.FullHeight));
    }

    [Fact]
    public void Pdf_page_can_be_chosen()
    {
        var path = _files.Pdf(85, 55, name: "recto-verso.pdf", pages: 2);

        var first = VisualLoader.Load(path);
        var second = VisualLoader.Load(path, 2);

        Assert.Equal((1, 2), (first.PageNumber, first.PageCount));
        Assert.Equal((2, 2), (second.PageNumber, second.PageCount));
        Assert.Equal(85, Units.PtToMm(first.FullWidth), 1);
        Assert.Equal(86, Units.PtToMm(second.FullWidth), 1); // the test file makes page 2 one millimetre wider
        Assert.Equal(VisualLoadError.NoPages, Assert.Throws<VisualLoadException>(() => VisualLoader.Load(path, 3)).Error);
        Assert.Equal(1, VisualLoader.Load(_files.Jpeg(100, 100)).PageCount);
    }

    [Fact]
    public void Pdf_without_trimbox_uses_whole_page()
    {
        var v = VisualLoader.Load(_files.Pdf(85, 55));

        Assert.Equal(VisualKind.Pdf, v.Kind);
        Assert.Equal(XUnit.FromMillimeter(85).Point, v.FullWidth, 3);
        Assert.False(v.HasOwnBleed);
    }

    [Fact]
    public void Pdf_with_trimbox_exposes_its_own_bleed()
    {
        var v = VisualLoader.Load(_files.Pdf(91, 61, trimInsetMm: 3));

        Assert.True(v.HasOwnBleed);
        double inset = XUnit.FromMillimeter(3).Point;
        Assert.Equal(inset, v.Content.X, tolerance: 0.01);
        Assert.Equal(inset, v.Content.Y, tolerance: 0.01);
        Assert.Equal(XUnit.FromMillimeter(85).Point, v.Content.Width, tolerance: 0.01);
        Assert.Equal(XUnit.FromMillimeter(55).Point, v.Content.Height, tolerance: 0.01);
    }

    [Fact]
    public void Pdf_page_rotation_becomes_base_rotation()
    {
        var v = VisualLoader.Load(_files.Pdf(85, 55, rotate: 90));

        Assert.Equal(90, v.BaseRotation);
    }

    [Fact]
    public void Unusable_files_raise_an_error_code_for_the_ui()
    {
        var txt = _files.Path("notes.txt");
        File.WriteAllText(txt, "x");
        var fake = _files.Path("fake.jpg");
        File.WriteAllText(fake, "not a jpeg");

        var unsupported = Assert.Throws<VisualLoadException>(() => VisualLoader.Load(txt));
        Assert.Equal(VisualLoadError.Unsupported, unsupported.Error);
        Assert.Equal(".txt", unsupported.Detail);
        Assert.Equal(VisualLoadError.NotFound, Assert.Throws<VisualLoadException>(() => VisualLoader.Load(_files.Path("missing.jpg"))).Error);
        var unreadable = Assert.Throws<VisualLoadException>(() => VisualLoader.Load(fake));
        Assert.Equal(VisualLoadError.Unreadable, unreadable.Error);
        Assert.Equal("fake.jpg", unreadable.FileName);
    }

    public void Dispose() => _files.Dispose();
}
