using PageCompose.Core;

namespace PageCompose.Core.Tests;

public sealed class RecentFilesTests
{
    private static readonly DateTime T0 = new(2026, 10, 15, 9, 0, 0);

    [Fact]
    public void A_new_file_comes_first()
    {
        var list = RecentFiles.Add([new(@"C:\a.PageComposeProject", T0)], @"C:\b.PageComposeProject", T0.AddHours(1));
        Assert.Equal([@"C:\b.PageComposeProject", @"C:\a.PageComposeProject"], list.Select(r => r.Path));
    }

    [Fact]
    public void A_file_already_there_moves_first_once_whatever_its_case()
    {
        List<RecentFile> list = [new(@"C:\a.PageComposeProject", T0), new(@"C:\Jobs\b.PageComposeProject", T0)];
        list = RecentFiles.Add(list, @"c:\JOBS\..\Jobs\B.pagecomposeproject", T0.AddHours(1));
        Assert.Equal(2, list.Count);
        Assert.Equal(@"c:\JOBS\..\Jobs\B.pagecomposeproject", list[0].Path);
        Assert.Equal(T0.AddHours(1), list[0].Time);
        Assert.Equal(@"C:\a.PageComposeProject", list[1].Path);
    }

    [Fact]
    public void At_most_ten_the_oldest_left_out()
    {
        var list = new List<RecentFile>();
        for (int i = 0; i < 12; i++)
            list = RecentFiles.Add(list, $@"C:\{i}.PageComposeProject", T0.AddMinutes(i));
        Assert.Equal(RecentFiles.MaxCount, list.Count);
        Assert.Equal(@"C:\11.PageComposeProject", list[0].Path);
        Assert.Equal(@"C:\2.PageComposeProject", list[^1].Path);
    }

    [Fact]
    public void Remove_takes_the_file_out_whatever_its_case()
    {
        List<RecentFile> list = [new(@"C:\a.PageComposeProject", T0), new(@"C:\b.PageComposeProject", T0)];
        Assert.Equal([@"C:\b.PageComposeProject"], RecentFiles.Remove(list, @"C:\A.PAGECOMPOSEPROJECT").Select(r => r.Path));
    }
}
