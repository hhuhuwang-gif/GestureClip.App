using System.Net.Http;

namespace GestureClip.Infrastructure.Updates;

public static class UpdateHttpClientFactory
{
    public static HttpClient CreateDirectClient()
    {
        var handler = new HttpClientHandler
        {
            UseProxy = false
        };
        var client = new HttpClient(handler, disposeHandler: true);
        GitHubReleaseUpdateCheckService.EnsureUserAgent(client);
        return client;
    }
}
