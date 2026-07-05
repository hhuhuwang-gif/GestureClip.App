using System.Windows;
using GestureClip.Core.Abstractions;
using GestureClip.Core.Workstation;
using Microsoft.Extensions.DependencyInjection;

namespace GestureClip.App.Services;

public sealed class OverworkReminderToastService : IOverworkReminderToastService
{
    private readonly IServiceProvider _serviceProvider;
    private OverworkReminderToastWindow? _currentWindow;

    public OverworkReminderToastService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task<OverworkReminderToastResult> ShowAsync(
        OverworkReminderNotification notification,
        CancellationToken cancellationToken)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            return OverworkReminderToastResult.Dismiss;
        }

        return await dispatcher.InvokeAsync(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var tcs = new TaskCompletionSource<OverworkReminderToastResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            _currentWindow?.Close();
            var window = _serviceProvider.GetRequiredService<OverworkReminderToastWindow>();
            _currentWindow = window;
            window.Completed += (_, result) =>
            {
                if (ReferenceEquals(_currentWindow, window))
                {
                    _currentWindow = null;
                }

                tcs.TrySetResult(result);
            };
            window.Configure(notification);
            window.Show();
            if (cancellationToken.CanBeCanceled)
            {
                cancellationToken.Register(() =>
                {
                    dispatcher.InvokeAsync(() =>
                    {
                        if (ReferenceEquals(_currentWindow, window))
                        {
                            window.Close();
                        }
                    });
                    tcs.TrySetCanceled(cancellationToken);
                });
            }

            return tcs.Task;
        }).Task.Unwrap();
    }
}
