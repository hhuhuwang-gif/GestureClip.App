using System.Net.Http;
using System.Net.Http.Headers;

namespace GestureClip.Infrastructure.Updates;

public static class UpdateHttpClientFactory
{
    public static HttpClient CreateDefaultClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(45)
        };
        ConfigureHeaders(client);
        return client;
    }

    public static HttpClient CreateDirectClient()
    {
        var handler = new HttpClientHandler
        {
            UseProxy = false
        };
        var client = new HttpClient(handler, disposeHandler: true)
        {
            Timeout = TimeSpan.FromSeconds(45)
        };
        ConfigureHeaders(client);
        return client;
    }

    public static void ConfigureHeaders(HttpClient httpClient)
    {
        if (!httpClient.DefaultRequestHeaders.UserAgent.Any())
        {
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("GestureClip-Updater");
        }

        if (!httpClient.DefaultRequestHeaders.Accept.Any())
        {
            httpClient.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        }

        if (!httpClient.DefaultRequestHeaders.Contains("X-GitHub-Api-Version"))
        {
            httpClient.DefaultRequestHeaders.TryAddWithoutValidation("X-GitHub-Api-Version", "2022-11-28");
        }
    }
}
