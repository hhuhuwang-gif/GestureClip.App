using System.IO;
using System.Threading.Channels;
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
            builder.Services.AddSingleton<ILoggerProvider>(_ => new FileLoggerProvider(paths.LogDirectory));
        });

        return services;
    }

    private sealed class FileLoggerProvider : ILoggerProvider
    {
        private const int QueueCapacity = 4096;
        private const long MaxLogFileBytes = 10 * 1024 * 1024;
        private readonly string _logDirectory;
        private readonly Channel<string> _channel;
        private readonly CancellationTokenSource _disposeCts = new();
        private readonly Task _writerTask;

        public FileLoggerProvider(string logDirectory)
        {
            _logDirectory = logDirectory;
            _channel = Channel.CreateBounded<string>(new BoundedChannelOptions(QueueCapacity)
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.DropWrite
            });
            _writerTask = Task.Run(ProcessQueueAsync);
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new FileLogger(this, categoryName);
        }

        public void Dispose()
        {
            _channel.Writer.TryComplete();
            if (!_writerTask.Wait(TimeSpan.FromSeconds(2)))
            {
                _disposeCts.Cancel();
            }

            _disposeCts.Dispose();
        }

        public void Enqueue(string line)
        {
            _channel.Writer.TryWrite(line);
        }

        private async Task ProcessQueueAsync()
        {
            try
            {
                await foreach (var line in _channel.Reader.ReadAllAsync(_disposeCts.Token).ConfigureAwait(false))
                {
                    WriteLine(line);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private void WriteLine(string line)
        {
            try
            {
                Directory.CreateDirectory(_logDirectory);
                var path = Path.Combine(_logDirectory, $"{DateTimeOffset.Now:yyyy-MM-dd}.log");
                RotateIfNeeded(path);
                using var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                using var writer = new StreamWriter(stream);
                writer.WriteLine(line);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private static void RotateIfNeeded(string path)
        {
            var file = new FileInfo(path);
            if (!file.Exists || file.Length < MaxLogFileBytes)
            {
                return;
            }

            var rotatedPath = Path.Combine(
                file.DirectoryName ?? "",
                $"{Path.GetFileNameWithoutExtension(file.Name)}.{DateTimeOffset.Now:HHmmss}.old{file.Extension}");
            File.Move(path, rotatedPath, overwrite: true);
        }
    }

    private sealed class FileLogger : ILogger
    {
        private readonly string _categoryName;
        private readonly FileLoggerProvider _provider;

        public FileLogger(FileLoggerProvider provider, string categoryName)
        {
            _provider = provider;
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

            var message = formatter(state, exception);
            var line = $"{DateTimeOffset.Now:O} [{logLevel}] {_categoryName}: {message}";

            if (exception is not null)
            {
                line += Environment.NewLine + exception;
            }

            _provider.Enqueue(line);
        }
    }
}
