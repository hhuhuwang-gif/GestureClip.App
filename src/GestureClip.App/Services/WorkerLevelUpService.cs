using System.Windows;
using GestureClip.App.ViewModels;
using GestureClip.Core.Abstractions;
using GestureClip.Core.Settings;
using GestureClip.Core.WorkerLevel;
using Microsoft.Extensions.DependencyInjection;

namespace GestureClip.App.Services;

public sealed class WorkerLevelUpService : IWorkerLevelUpService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ISettingsService _settingsService;
    private DateTimeOffset _lastShownAt = DateTimeOffset.MinValue;
    private WorkerLevelUpWindow? _window;

    public WorkerLevelUpService(IServiceProvider serviceProvider, ISettingsService settingsService)
    {
        _serviceProvider = serviceProvider;
        _settingsService = settingsService;
    }

    public async Task ShowLevelUpAsync(WorkerLevelSnapshot snapshot, CancellationToken cancellationToken)
    {
        if (!snapshot.LeveledUp || !_settingsService.Get(SettingKeys.WorkerLevelShowLevelUpPopup, true))
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        if ((now - _lastShownAt).TotalSeconds < 2)
        {
            return;
        }

        _lastShownAt = now;
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            return;
        }

        await dispatcher.InvokeAsync(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            _window?.Close();
            _window = _serviceProvider.GetRequiredService<WorkerLevelUpWindow>();
            _window.DataContext = new WorkerLevelUpViewModel(snapshot.LevelText, snapshot.XpText, snapshot.CurrentLevel.Title);
            PositionWindow(_window);
            _window.Show();
            _ = HideLaterAsync(_window);
        });
    }

    private static void PositionWindow(Window window)
    {
        var bounds = new Rect(
            SystemParameters.WorkArea.Left,
            SystemParameters.WorkArea.Top,
            SystemParameters.WorkArea.Width,
            SystemParameters.WorkArea.Height);
        window.Left = bounds.Right - window.Width - 32;
        window.Top = bounds.Bottom - window.Height - 64;
    }

    private async Task HideLaterAsync(WorkerLevelUpWindow window)
    {
        await Task.Delay(TimeSpan.FromSeconds(3));
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            return;
        }

        await dispatcher.InvokeAsync(() =>
        {
            if (ReferenceEquals(_window, window))
            {
                window.Hide();
            }
        });
    }
}

