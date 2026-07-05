using System.Windows;
using System.Windows.Threading;
using GestureClip.Core.Workstation;

namespace GestureClip.App;

public partial class OverworkReminderToastWindow : Window
{
    private readonly DispatcherTimer _autoCloseTimer;
    private bool _resultRaised;

    public OverworkReminderToastWindow()
    {
        InitializeComponent();
        _autoCloseTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(8) };
        _autoCloseTimer.Tick += (_, _) => Complete(OverworkReminderToastResult.Dismiss);
    }

    public event EventHandler<OverworkReminderToastResult>? Completed;

    public void Configure(OverworkReminderNotification notification)
    {
        DataContext = new OverworkReminderToastViewModel(notification);
        PositionBottomRight();
        _autoCloseTimer.Stop();
        _autoCloseTimer.Start();
    }

    protected override void OnClosed(EventArgs e)
    {
        _autoCloseTimer.Stop();
        if (!_resultRaised)
        {
            Completed?.Invoke(this, OverworkReminderToastResult.Dismiss);
            _resultRaised = true;
        }

        base.OnClosed(e);
    }

    private void DismissButton_Click(object sender, RoutedEventArgs e)
    {
        Complete(OverworkReminderToastResult.Dismiss);
    }

    private void SnoozeButton_Click(object sender, RoutedEventArgs e)
    {
        Complete(OverworkReminderToastResult.Snooze);
    }

    private void MuteTodayButton_Click(object sender, RoutedEventArgs e)
    {
        Complete(OverworkReminderToastResult.MuteToday);
    }

    private void Complete(OverworkReminderToastResult result)
    {
        if (_resultRaised)
        {
            return;
        }

        _resultRaised = true;
        _autoCloseTimer.Stop();
        Completed?.Invoke(this, result);
        Close();
    }

    private void PositionBottomRight()
    {
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Right - Width - 24;
        Top = workArea.Bottom - Height - 28;
    }

    private sealed class OverworkReminderToastViewModel
    {
        public OverworkReminderToastViewModel(OverworkReminderNotification notification)
        {
            Title = notification.Title;
            Message = notification.Message;
            Detail = notification.Detail;
            StageText = GetStageText(notification.Stage);
            SnoozeButtonVisibility = notification.CanSnooze ? Visibility.Visible : Visibility.Collapsed;
        }

        public string Title { get; }

        public string Message { get; }

        public string Detail { get; }

        public string StageText { get; }

        public Visibility SnoozeButtonVisibility { get; }

        private static string GetStageText(WorkTimeStage stage) => stage switch
        {
            WorkTimeStage.EarlyWork => "开工状态 · 轻提醒",
            WorkTimeStage.MidWork => "稳定输出 · 记得休息",
            WorkTimeStage.LateWork => "工作后段 · 注意补水",
            WorkTimeStage.Overtime => "加班中 · 建议收尾",
            WorkTimeStage.LunchBreak => "午休中 · 先吃饭",
            WorkTimeStage.RestDay => "休息日 · 放轻松",
            WorkTimeStage.BeforeWork => "未上班 · 慢慢来",
            _ => "已下班 · 今天赢了"
        };
    }
}
