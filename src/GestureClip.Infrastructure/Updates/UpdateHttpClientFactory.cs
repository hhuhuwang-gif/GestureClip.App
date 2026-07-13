using System.Net.Http;
using System.Net.Http.Headers;

namespace GestureClip.Infrastructure.Updates;

public static class UpdateHttpClientFactory
{
    public static HttpClient CreateDefaultClient()
    {
        return CreateClient(useProxy: true, timeout: TimeSpan.FromSeconds(45));
    }

    public static HttpClient CreateDirectClient()
    {
        return CreateClient(useProxy: false, timeout: TimeSpan.FromSeconds(45));
    }

    public static HttpClient CreateClient(bool useProxy, TimeSpan timeout)
    {
        var handler = new HttpClientHandler
        {
            UseProxy = useProxy,
            // Allow following redirects used by some mirrors and GitHub asset CDNs.
            AllowAutoRedirect = true,
            AutomaticDecompression = System.Net.DecompressionMethods.All
        };

        var client = new HttpClient(handler, disposeHandler: true)
        {
            Timeout = timeout
        };
        ConfigureHeaders(client);
        return client;
    }

    public static void ConfigureHeaders(HttpClient httpClient)
    {
        if (!httpClient.DefaultRequestHeaders.UserAgent.Any())
        {
            // GitHub requires a User-Agent; identify app for support / rate-limit diagnosis.
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("GestureClip-Updater/0.6 (+https://github.com/hhuhuwang-gif/GestureClip.App)");
        }

        if (!httpClient.DefaultRequestHeaders.Accept.Any())
        {
            httpClient.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            httpClient.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/octet-stream"));
            httpClient.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("*/*"));
        }

        if (!httpClient.DefaultRequestHeaders.Contains("X-GitHub-Api-Version"))
        {
            httpClient.DefaultRequestHeaders.TryAddWithoutValidation("X-GitHub-Api-Version", "2022-11-28");
        }
    }
}
