using PageCompose.Core;
using PageCompose.Core.Layout;
using PageCompose.Core.Pdf;
using PageCompose.Core.Visuals;

namespace PageCompose.Core.Tests;

public sealed class SafeFileTests : IDisposable
{
    private readonly TestFiles _files = new();

    [Fact]
    public void A_write_replaces_the_file_and_leaves_nothing_else()
    {
        var path = _files.Path("export.pdf");
        File.WriteAllText(path, "old");

        SafeFile.Write(path, s => s.Write("new"u8));

        Assert.Equal("new", File.ReadAllText(path));
        Assert.Single(Directory.GetFiles(Path.GetDirectoryName(path)!, "export.pdf*"));
    }

    [Fact]
    public void A_write_that_fails_keeps_the_file_that_was_there()
    {
        var path = _files.Path("export.pdf");
        File.WriteAllText(path, "good export");

        Assert.Throws<IOException>(() => SafeFile.Write(path, s =>
        {
            s.Write("half"u8);
            throw new IOException("a photo is gone");
        }));

        Assert.Equal("good export", File.ReadAllText(path));
        Assert.Single(Directory.GetFiles(Path.GetDirectoryName(path)!, "export.pdf*"));
    }

    [Fact]
    public void A_temporary_file_left_by_a_stopped_run_does_not_stop_the_next_write()
    {
        var path = _files.Path("job.PageComposeProject");
        File.WriteAllText(path + ".tmp", "left by a save that failed");

        SafeFile.Write(path, s => s.Write("project"u8));

        Assert.Equal("project", File.ReadAllText(path));
    }

    [Fact]
    public void A_pdf_export_that_fails_keeps_the_last_good_one()
    {
        // The photo of a reopened job is no longer there (a USB drive taken out): the export fails, the PDF of the
        // last export stays as it was instead of being emptied.
        var photo = _files.Jpeg(600, 400, name: "photo.jpg");
        var visual = VisualLoader.Load(photo);
        var layout = LayoutEngine.Compute(new LayoutOptions(SheetFormats.Sra3, ItemFormats.Photo10x15.Size));
        var path = _files.Path("sheets.pdf");
        ImpositionPdfWriter.Write(ImpositionJob.Repeat(layout, visual, 1), path);
        var good = File.ReadAllBytes(path);

        File.Delete(photo);
        Assert.ThrowsAny<Exception>(() => ImpositionPdfWriter.Write(ImpositionJob.Repeat(layout, visual, 1), path));

        Assert.Equal(good, File.ReadAllBytes(path));
    }

    public void Dispose() => _files.Dispose();
}
