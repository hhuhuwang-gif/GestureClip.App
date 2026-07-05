namespace GestureClip.Infrastructure.Clipboard;

public static class ClipboardRetryPolicy
{
    public static async Task RunAsync(
        Func<Task> action,
        int retryCount,
        TimeSpan delay,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(retryCount);

        for (var attempt = 0; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await action();
                return;
            }
            catch when (attempt < retryCount)
            {
                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay, cancellationToken);
                }
            }
        }
    }
}
