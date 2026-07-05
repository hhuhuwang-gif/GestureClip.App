using Xunit;

namespace GestureClip.Tests.Performance;

public sealed class Stage10UiThreadGuardTests
{
    [Fact]
    public void App_feature_and_infrastructure_sources_do_not_add_blocking_ui_thread_patterns()
    {
        var root = FindRepositoryRoot();
        var sourceRoots = new[]
        {
            Path.Combine(root, "src", "GestureClip.App"),
            Path.Combine(root, "src", "GestureClip.Features"),
            Path.Combine(root, "src", "GestureClip.Infrastructure")
        };

        var violations = sourceRoots
            .SelectMany(directory => Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories))
            .SelectMany(file => FindBlockingPatterns(root, file))
            .Where(violation => !IsAllowedShutdownFlush(violation))
            .ToArray();

        Assert.Empty(violations);
    }

    private static IEnumerable<SourceViolation> FindBlockingPatterns(string root, string file)
    {
        var lines = File.ReadAllLines(file);
        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            if (line.Contains(".Result", StringComparison.Ordinal) ||
                line.Contains(".Wait(", StringComparison.Ordinal) ||
                line.Contains("Thread.Sleep", StringComparison.Ordinal) ||
                line.Contains("Dispatcher.Invoke(", StringComparison.Ordinal))
            {
                yield return new SourceViolation(
                    Path.GetRelativePath(root, file),
                    index + 1,
                    line.Trim());
            }
        }
    }

    private static bool IsAllowedShutdownFlush(SourceViolation violation)
    {
        return violation.File == Path.Combine("src", "GestureClip.Infrastructure", "Logging", "LoggingServiceCollectionExtensions.cs") &&
            violation.LineText.Contains("_writerTask.Wait(TimeSpan.FromSeconds(2))", StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "GestureClip.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }

    private sealed record SourceViolation(string File, int LineNumber, string LineText)
    {
        public override string ToString()
        {
            return $"{File}:{LineNumber}: {LineText}";
        }
    }
}
