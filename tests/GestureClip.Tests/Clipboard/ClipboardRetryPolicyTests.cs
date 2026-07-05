using GestureClip.Infrastructure.Clipboard;
using Xunit;

namespace GestureClip.Tests.Clipboard;

public sealed class ClipboardRetryPolicyTests
{
    [Fact]
    public async Task RunAsync_retries_transient_clipboard_failures()
    {
        var attempts = 0;

        await ClipboardRetryPolicy.RunAsync(
            () =>
            {
                attempts++;
                if (attempts < 3)
                {
                    throw new InvalidOperationException("clipboard busy");
                }

                return Task.CompletedTask;
            },
            retryCount: 3,
            delay: TimeSpan.Zero,
            CancellationToken.None);

        Assert.Equal(3, attempts);
    }

    [Fact]
    public async Task RunAsync_stops_after_retry_limit()
    {
        var attempts = 0;

        await Assert.ThrowsAsync<InvalidOperationException>(() => ClipboardRetryPolicy.RunAsync(
            () =>
            {
                attempts++;
                throw new InvalidOperationException("clipboard busy");
            },
            retryCount: 2,
            delay: TimeSpan.Zero,
            CancellationToken.None));

        Assert.Equal(3, attempts);
    }
}
