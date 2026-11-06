using System.Net.Http.Json;
using KleinGeld.Contracts;

namespace KleinGeld.Api.Tests;

[Collection(KleinGeldAppCollection.Name)]
public sealed class PhoneLayoutTests(KleinGeldApp app)
{
    [Fact]
    public async Task The_tab_bar_has_Home_and_More_and_More_holds_the_profile()
    {
        using var member = await app.LoginAsNewMemberAsync();

        var tabBar = TabBar(await member.GetStringAsync("/Account/Manage"));

        Assert.Contains(">Home</a>", tabBar);
        var more = More(tabBar);
        Assert.Contains("<summary>More</summary>", more);
        Assert.Contains("Account/Manage", more);
        Assert.Contains("Account/Logout", more);
    }

    [Fact]
    public async Task More_holds_My_Artists_only_for_managing_Members()
    {
        using var member = await app.LoginAsNewMemberAsync();

        var before = More(TabBar(await member.GetStringAsync("/Account/Manage")));
        using var created = await member.PostAsJsonAsync("/api/artists", new ArtistForm { Name = "Op De Kaart" });
        created.EnsureSuccessStatusCode();
        var after = More(TabBar(await member.GetStringAsync("/Account/Manage")));

        Assert.DoesNotContain("My Artists", before);
        Assert.Contains("My Artists", after);
    }

    [Fact]
    public async Task More_holds_Admin_only_for_the_Admin()
    {
        using var admin = await app.LoginAsAdminAsync();
        using var member = await app.LoginAsNewMemberAsync();

        Assert.Contains("href=\"admin\"", More(TabBar(await admin.GetStringAsync("/Account/Manage"))));
        Assert.DoesNotContain("href=\"admin\"", More(TabBar(await member.GetStringAsync("/Account/Manage"))));
    }

    private static string TabBar(string page)
    {
        var start = page.IndexOf("<nav class=\"tab-bar\"", StringComparison.Ordinal);
        Assert.True(start >= 0, "The page has no tab bar.");
        return page[start..page.IndexOf("</nav>", start, StringComparison.Ordinal)];
    }

    private static string More(string tabBar)
    {
        var start = tabBar.IndexOf("<details class=\"tab-more\"", StringComparison.Ordinal);
        Assert.True(start >= 0, "The tab bar has no More.");
        return tabBar[start..];
    }
}
