using PageCompose.Core.Visuals;

namespace PageCompose.Core.Tests;

public sealed class BackPairsTests
{
    [Fact]
    public void Recto_and_verso_of_the_same_name_are_paired()
    {
        var pairs = BackPairs.Find([@"C:\job\carte-verso.pdf", @"C:\job\photo.jpg", @"C:\job\carte-recto.pdf"]);

        var pair = Assert.Single(pairs);
        Assert.Equal(@"C:\job\carte-recto.pdf", pair.Front);
        Assert.Equal(@"C:\job\carte-verso.pdf", pair.Back);
    }

    [Theory]
    [InlineData("Flyer front.jpg", "Flyer back.jpg")]
    [InlineData("RECTO_menu.png", "VERSO_menu.png")]
    [InlineData("recto.jpg", "verso.jpg")]
    [InlineData("invitation recto 2.pdf", "invitation verso 2.jpg")] // the extension may differ
    public void Names_and_cases_that_pair(string front, string back)
    {
        var pairs = BackPairs.Find([@"C:\job\" + back, @"C:\job\" + front]);

        Assert.Equal(new[] { (@"C:\job\" + front, @"C:\job\" + back) }, pairs);
    }

    [Theory]
    [InlineData(@"C:\job\carte-recto.pdf", @"C:\job\autre-verso.pdf")]        // another name
    [InlineData(@"C:\job\carte-recto.pdf", @"C:\other\carte-verso.pdf")]      // another folder
    [InlineData(@"C:\job\backstage-recto.jpg", @"C:\job\backstage-front.jpg")] // two fronts ("backstage" is not "back")
    [InlineData(@"C:\job\versolo.jpg", @"C:\job\rectolo.jpg")]                // not a word of its own
    [InlineData(@"C:\job\carte recto-verso.pdf", @"C:\job\carte.pdf")]        // both words: neither side
    public void Names_that_do_not_pair(string first, string second)
    {
        Assert.Empty(BackPairs.Find([first, second]));
    }

    [Fact]
    public void A_name_on_two_fronts_is_left_alone()
    {
        var pairs = BackPairs.Find([@"C:\job\carte-recto.pdf", @"C:\job\carte-recto.jpg", @"C:\job\carte-verso.pdf"]);

        Assert.Empty(pairs);
    }

    [Fact]
    public void Several_pairs_keep_the_order_of_their_fronts()
    {
        var pairs = BackPairs.Find([@"C:\b-front.jpg", @"C:\a-back.jpg", @"C:\a-front.jpg", @"C:\b-back.jpg"]);

        Assert.Equal(new[] { @"C:\b-front.jpg", @"C:\a-front.jpg" }, pairs.Select(p => p.Front));
    }
}
