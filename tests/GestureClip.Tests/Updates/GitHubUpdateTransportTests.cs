using GestureClip.Infrastructure.Updates;
using Xunit;

namespace GestureClip.Tests.Updates;

public sealed class GitHubUpdateTransportTests
{
    public GitHubUpdateTransportTests()
    {
        GitHubUpdateTransport.ResetPreferredRouteForTests();
    }

    [Fact]
    public void BuildApiRoutes_starts_with_official_proxy_then_direct_then_mirrors()
    {
        var routes = GitHubUpdateTransport.BuildApiRoutes();

        Assert.True(routes.Count >= 4);
        Assert.Equal(GitHubUpdateTransport.OfficialLatestReleaseApi, routes[0].Url);
        Assert.True(routes[0].UseProxy);
        Assert.Equal(GitHubUpdateTransport.OfficialLatestReleaseApi, routes[1].Url);
        Assert.False(routes[1].UseProxy);
        Assert.Contains(routes, route => route.Url.Contains("ghfast.top", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(routes, route => route.Url.Contains("gh-proxy.com", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BuildDownloadRoutes_includes_official_and_mirrored_urls()
    {
        const string official = "https://github.com/hhuhuwang-gif/GestureClip.App/releases/download/v0.6.15-beta/GestureClip-v0.6.15-beta-win-x64.zip";

        var routes = GitHubUpdateTransport.BuildDownloadRoutes(official);

        Assert.Equal(official, routes[0].Url);
        Assert.True(routes[0].UseProxy);
        Assert.Equal(official, routes[1].Url);
        Assert.False(routes[1].UseProxy);
        Assert.Contains(routes, route =>
            route.Url.StartsWith("https://ghfast.top/" + official, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BuildApiRoutes_prefers_last_successful_route_next_time()
    {
        var first = GitHubUpdateTransport.BuildApiRoutes();
        var directOfficial = first.First(route =>
            route.Url == GitHubUpdateTransport.OfficialLatestReleaseApi && !route.UseProxy);

        GitHubUpdateTransport.RememberSuccessForTests(directOfficial);

        var second = GitHubUpdateTransport.BuildApiRoutes();
        Assert.Equal(directOfficial.Url, second[0].Url);
        Assert.False(second[0].UseProxy);
        Assert.Equal("direct|" + directOfficial.Url, GitHubUpdateTransport.PreferredRouteKey);
    }
}
