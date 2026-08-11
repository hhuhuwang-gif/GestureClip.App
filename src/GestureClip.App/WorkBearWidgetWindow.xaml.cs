using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using GestureClip.Core.Abstractions;
using GestureClip.Core.Settings;
using GestureClip.Core.Workstation;

namespace GestureClip.App;

/// <summary>
/// Always-on-top mini widget: live earnings + off-work countdown pill with an
/// optional custom desktop pet image (animated GIF supported natively).
/// </summary>
public partial class WorkBearWidgetWindow : Window
{
    private readonly IWorkstationDashboardService _dashboardService;
    private readonly ISettingsService _settingsService;
    private readonly IAppLifecycleService _appLifecycleService;
    private readonly DispatcherTimer _refreshTimer;
    private readonly DispatcherTimer _tickTimer;
    private readonly DispatcherTimer _petTimer;
    private WorkstationDashboardSnapshot? _snapshot;
    private DateTimeOffset _snapshotAt = DateTimeOffset.Now;
    private IReadOnlyList<BitmapSource> _petFrames = [];
    private int _petFrameIndex;

    public WorkBearWidgetWindow(
        IWorkstationDashboardService dashboardService,
        ISettingsService settingsService,
        IAppLifecycleService appLifecycleService)
    {
        _dashboardService = dashboardService;
        _settingsService = settingsService;
        _appLifecycleService = appLifecycleService;
        InitializeComponent();
        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _refreshTimer.Tick += async (_, _) => await RefreshSnapshotAsync();
        _tickTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _tickTimer.Tick += (_, _) => UpdateDisplay();
        _petTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
        _petTimer.Tick += (_, _) => AdvancePetFrame();
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        RestorePosition();
        LoadPet(_settingsService.Get(SettingKeys.WorkBearWidgetPetPath, string.Empty));
        await RefreshSnapshotAsync();
        _refreshTimer.Start();
        _tickTimer.Start();
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        _refreshTimer.Stop();
        _tickTimer.Stop();
        _petTimer.Stop();
        SavePosition();
    }

    private async Task RefreshSnapshotAsync()
    {
        try
        {
            _snapshot = await _dashboardService.GetSnapshotAsync(DateTimeOffset.Now, CancellationToken.None);
            _snapshotAt = DateTimeOffset.Now;
            UpdateDisplay();
        }
        catch
        {
            // Widget is best-effort; keep last values on transient failures.
        }
    }

    private void UpdateDisplay()
    {
        if (_snapshot is null)
        {
            return;
        }

        var elapsed = DateTimeOffset.Now - _snapshotAt;
        if (elapsed < TimeSpan.Zero)
        {
            elapsed = TimeSpan.Zero;
        }

        var isEarning = _snapshot.WorkTimeStage
            is WorkTimeStage.EarlyWork or WorkTimeStage.MidWork or WorkTimeStage.LateWork;
        var earned = isEarning && _snapshot.MinuteValue > 0
            ? _snapshot.TodayEarned + (decimal)elapsed.TotalMinutes * _snapshot.MinuteValue
            : _snapshot.TodayEarned;
        EarnedText.Text = $"¥{earned:0.00}";

        var remaining = _snapshot.TimeUntilOffWork > TimeSpan.Zero
            ? _snapshot.TimeUntilOffWork - TimeSpan.FromSeconds(Math.Floor(elapsed.TotalSeconds))
            : _snapshot.TimeUntilOffWork;
        CountdownText.Text = remaining <= TimeSpan.Zero
            ? (_snapshot.WorkTimeStage == WorkTimeStage.RestDay ? "休息日" : "下班了")
            : $"{(int)remaining.TotalHours:D2}:{remaining.Minutes:D2}:{remaining.Seconds:D2}";
    }

    private void LoadPet(string path)
    {
        _petTimer.Stop();
        _petFrames = [];
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            PetImage.Visibility = Visibility.Collapsed;
            PetEmoji.Visibility = Visibility.Visible;
            return;
        }

        try
        {
            if (path.EndsWith(".gif", StringComparison.OrdinalIgnoreCase))
            {
                var decoder = new GifBitmapDecoder(
                    new Uri(path),
                    BitmapCreateOptions.PreservePixelFormat,
                    BitmapCacheOption.OnLoad);
                var frames = decoder.Frames.Select(frame => (BitmapSource)frame.GetAsFrozen()).ToList();
                if (frames.Count == 0)
                {
                    throw new InvalidOperationException("GIF 没有可用帧");
                }

                _petFrames = frames;
                _petFrameIndex = 0;
                PetImage.Source = frames[0];
                if (frames.Count > 1)
                {
                    _petTimer.Start();
                }
            }
            else
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.UriSource = new Uri(path);
                image.EndInit();
                image.Freeze();
                PetImage.Source = image;
            }

            PetImage.Visibility = Visibility.Visible;
            PetEmoji.Visibility = Visibility.Collapsed;
        }
        catch
        {
            PetImage.Visibility = Visibility.Collapsed;
            PetEmoji.Visibility = Visibility.Visible;
        }
    }

    private void AdvancePetFrame()
    {
        if (_petFrames.Count < 2)
        {
            return;
        }

        _petFrameIndex = (_petFrameIndex + 1) % _petFrames.Count;
        PetImage.Source = _petFrames[_petFrameIndex];
    }

    private void RestorePosition()
    {
        var left = _settingsService.Get(SettingKeys.WorkBearWidgetLeft, double.NaN);
        var top = _settingsService.Get(SettingKeys.WorkBearWidgetTop, double.NaN);
        var workArea = SystemParameters.WorkArea;
        if (!double.IsNaN(left) && !double.IsNaN(top) &&
            left >= workArea.Left - 40 && left < workArea.Right - 40 &&
            top >= workArea.Top - 10 && top < workArea.Bottom - 20)
        {
            Left = left;
            Top = top;
            return;
        }

        Left = workArea.Right - 280;
        Top = workArea.Bottom - 70;
    }

    private void SavePosition()
    {
        _ = _settingsService.SetAsync(SettingKeys.WorkBearWidgetLeft, Left, CancellationToken.None);
        _ = _settingsService.SetAsync(SettingKeys.WorkBearWidgetTop, Top, CancellationToken.None);
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        SavePosition();
    }

    private void Window_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        _appLifecycleService.ShowWorkstationDashboardWindow();
    }

    private void OpenHubMenuItem_Click(object sender, RoutedEventArgs e)
    {
        _appLifecycleService.ShowWorkstationDashboardWindow();
    }

    private void ChoosePetMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "选择桌宠图片（GIF 会动）",
            Filter = "图片 (*.gif;*.png;*.jpg;*.jpeg)|*.gif;*.png;*.jpg;*.jpeg"
        };
        if (dialog.ShowDialog() == true)
        {
            _ = _settingsService.SetAsync(SettingKeys.WorkBearWidgetPetPath, dialog.FileName, CancellationToken.None);
            LoadPet(dialog.FileName);
        }
    }

    private void RemovePetMenuItem_Click(object sender, RoutedEventArgs e)
    {
        _ = _settingsService.SetAsync(SettingKeys.WorkBearWidgetPetPath, string.Empty, CancellationToken.None);
        LoadPet(string.Empty);
    }

    private void CloseWidgetMenuItem_Click(object sender, RoutedEventArgs e)
    {
        _ = _settingsService.SetAsync(SettingKeys.WorkBearWidgetEnabled, false, CancellationToken.None);
        Close();
    }
}
