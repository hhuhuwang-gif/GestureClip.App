using System.IO;
using GestureClip.Infrastructure.Paths;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GestureClip.Infrastructure.Logging;

public static class LoggingServiceCollectionExtensions
{
    public static IServiceCollection AddGestureClipLogging(
        this IServiceCollection services,
        AppPathProvider paths)
    {
        paths.EnsureDirectories();

        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.SetMinimumLevel(LogLevel.Information);
            builder.AddProvider(new FileLoggerProvider(paths.LogDirectory));
        });

        return services;
    }

    private sealed class FileLoggerProvider : ILoggerProvider
    {
        private readonly string _logDirectory;

        public FileLoggerProvider(string logDirectory)
        {
            _logDirectory = logDirectory;
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new FileLogger(_logDirectory, categoryName);
        }

        public void Dispose()
        {
        }
    }

    private sealed class FileLogger : ILogger
    {
        private static readonly object SyncRoot = new();
        private readonly string _categoryName;
        private readonly string _logDirectory;

        public FileLogger(string logDirectory, string categoryName)
        {
            _logDirectory = logDirectory;
            _categoryName = categoryName;
        }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel >= LogLevel.Information;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var path = Path.Combine(_logDirectory, $"{DateTimeOffset.Now:yyyy-MM-dd}.log");
            var message = formatter(state, exception);
            var line = $"{DateTimeOffset.Now:O} [{logLevel}] {_categoryName}: {message}";

            if (exception is not null)
            {
                line += Environment.NewLine + exception;
            }

            lock (SyncRoot)
            {
                Directory.CreateDirectory(_logDirectory);
                File.AppendAllText(path, line + Environment.NewLine);
            }
        }
    }
}
