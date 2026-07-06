using System.Windows;
using GestureClip.App.Services;
using GestureClip.Core.Abstractions;

namespace GestureClip.App;

public partial class OnboardingWindow : Window
{
    private readonly IFirstRunOnboardingService _onboardingService;
    private readonly AppLifecycleService _appLifecycleService;

    public OnboardingWindow(
        IFirstRunOnboardingService onboardingService,
        AppLifecycleService appLifecycleService)
    {
        _onboardingService = onboardingService;
        _appLifecycleService = appLifecycleService;
        InitializeComponent();
    }

    private async void StartButton_Click(object sender, RoutedEventArgs e)
    {
        await CompleteAndOpenSettingsAsync();
    }

    private async void SkipButton_Click(object sender, RoutedEventArgs e)
    {
        await CompleteAndOpenSettingsAsync();
    }

    private async Task CompleteAndOpenSettingsAsync()
    {
        await _onboardingService.CompleteAsync(CancellationToken.None);
        Close();
        _appLifecycleService.ShowSettingsWindow();
    }
}
