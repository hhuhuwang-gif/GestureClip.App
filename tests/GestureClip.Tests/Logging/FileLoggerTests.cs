using GestureClip.Infrastructure.Logging;
using GestureClip.Infrastructure.Paths;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace GestureClip.Tests.Logging;

public sealed class FileLoggerTests
{
    [Fact]
    public void FileLogger_does_not_write_to_file_synchronously_on_log_call()
    {
        var source = File.ReadAllText(FindRepositoryFile("src", "GestureClip.Infrastructure", "Logging", "LoggingServiceCollectionExtensions.cs"));

        Assert.DoesNotContain("File.AppendAllText", source);
        Assert.Contains("Channel", source);
    }

    [Fact]
    public void FileLogger_flushes_queued_messages_when_provider_is_disposed()
    {
        var localAppData = Path.Combine(Path.GetTempPath(), $"gestureclip-logging-{Guid.NewGuid():N}");
        var paths = new AppPathProvider(localAppData);
        using (var services = new ServiceCollection()
                   .AddGestureClipLogging(paths)
                   .BuildServiceProvider())
        {
            var logger = services.GetRequiredService<ILogger<FileLoggerTests>>();
            logger.LogInformation("flush-test-message");
        }

        var logPath = Directory.GetFiles(paths.LogDirectory, "*.log").Single();
        var log = File.ReadAllText(logPath);
        Assert.Contains("flush-test-message", log);
    }

    private static string FindRepositoryFile(params string[] segments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(segments).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate repository file.", Path.Combine(segments));
    }
}
