using Mgo2Server.Http.Middleware;

namespace Mgo2Server.Tests;

/// <summary>
/// Guards the canonicalisation every game path goes through before routing.
/// <para>
/// The region segment is the disc's, not the server's, so a route declared against
/// one region has to answer whichever one the client sent. These cases pin both
/// halves of that: the region is folded, and the client's repeated separators are
/// collapsed, while the API's own paths are left alone.
/// </para>
/// </summary>
[Trait("Category", "Http")]
public sealed class LegacyPathNormalizerTests
{
    /// <summary>A path already in the declared form is handed through untouched.</summary>
    [Theory]
    [InlineData("/jp/mgo2/rank/mgogetrank.html")]
    [InlineData("/jp/mgo2/policy/policy.txt")]
    [InlineData("/jp/mgo2/help/2_6.txt")]
    public void A_path_in_the_declared_region_is_unchanged(string path)
    {
        Assert.Equal(path, LegacyPathNormalizer.Normalize(path));
    }

    /// <summary>The region a disc names is folded onto the one the routes are declared in.</summary>
    [Theory]
    [InlineData("/us/mgo2/rank/mgogetrank.html", "/jp/mgo2/rank/mgogetrank.html")]
    [InlineData("/eu/mgo2/rank/mgogetrank.html", "/jp/mgo2/rank/mgogetrank.html")]
    [InlineData("/US/mgo2/rank/mgogetrank.html", "/jp/mgo2/rank/mgogetrank.html")]
    [InlineData("/us/mgo2/rank/mgogetrank_clan.html", "/jp/mgo2/rank/mgogetrank_clan.html")]
    [InlineData("/us/mgo2/policy/policy.txt", "/jp/mgo2/policy/policy.txt")]
    [InlineData("/eu/mgo2/help/0_0.txt", "/jp/mgo2/help/0_0.txt")]
    public void The_region_the_disc_names_is_folded_onto_the_declared_one(string path, string expected)
    {
        Assert.Equal(expected, LegacyPathNormalizer.Normalize(path));
    }

    /// <summary>The client repeats separators, which a route template cannot carry.</summary>
    [Theory]
    [InlineData("/jp/mgo2//patch//checkver.html", "/jp/mgo2/patch/checkver.html")]
    [InlineData("/us/mgo2//patch//checkver.html", "/jp/mgo2/patch/checkver.html")]
    [InlineData("/us/mgo2//patch/checkver.html", "/jp/mgo2/patch/checkver.html")]
    [InlineData("/jp/mgo2///rank//mgogetrank.html", "/jp/mgo2/rank/mgogetrank.html")]
    public void Repeated_separators_are_collapsed(string path, string expected)
    {
        Assert.Equal(expected, LegacyPathNormalizer.Normalize(path));
    }

    /// <summary>
    /// A path that is not the game's is returned unchanged, so folding the region
    /// cannot reach the API's own routes or a document served beside them.
    /// </summary>
    [Theory]
    [InlineData("/api")]
    [InlineData("/api/accounts")]
    [InlineData("/.well-known/openapi.json")]
    [InlineData("/Z4qIOLmQBOj4NQo0uHx3q0mE51Fe/")]
    [InlineData("/")]
    [InlineData("")]
    public void A_path_that_is_not_the_games_is_unchanged(string path)
    {
        Assert.Equal(path, LegacyPathNormalizer.Normalize(path));
    }
}
