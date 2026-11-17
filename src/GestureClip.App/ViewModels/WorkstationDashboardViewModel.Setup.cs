using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using GestureClip.Core.Abstractions;
using GestureClip.Core.Settings;
using GestureClip.Core.Workstation;

namespace GestureClip.App.ViewModels;

using Media = System.Windows.Media;

public sealed partial class WorkstationDashboardViewModel : INotifyPropertyChanged
{
    private void ApplyWorkTemplate(object? parameter)
    {
        if (parameter is not WorkTemplateOption template)
        {
            return;
        }

        WorkstationWorkStartTime = template.WorkStartTime;
        WorkstationWorkEndTime = template.WorkEndTime;
        WorkstationLunchStartTime = template.LunchStartTime;
        WorkstationLunchEndTime = template.LunchEndTime;
        WorkstationWorkdays = template.Workdays;
        SetupStartTime = template.WorkStartTime;
        SetupEndTime = template.WorkEndTime;
        LastMessage = $"已应用工作制模板：{template.Name}";
    }


    public void OpenWorkRules()
    {
        SettingsExpanded = true;
        LastMessage = "在下方补月薪和上下班时间即可，数据只保存在本机。";
        OnPropertyChanged(nameof(ShowEmptySalaryGuide));
    }

    public async Task CompleteSetupAsync()
    {
        if (_settingsService is null)
        {
            return;
        }

        if (!decimal.TryParse(SetupSalaryText.Trim(), out var salary) || salary <= 0)
        {
            LastMessage = "请先填写有效月薪，例如 10000。";
            return;
        }

        if (!TimeOnly.TryParse(SetupStartTime, out var start) ||
            !TimeOnly.TryParse(SetupEndTime, out var end) ||
            end <= start)
        {
            LastMessage = "请填写有效上下班时间（HH:mm），且下班晚于上班。";
            return;
        }

        await _settingsService.SetAsync(SettingKeys.WorkstationMonthlySalary, salary, CancellationToken.None);
        await _settingsService.SetAsync(SettingKeys.WorkstationWorkStartTime, SetupStartTime.Trim(), CancellationToken.None);
        await _settingsService.SetAsync(SettingKeys.WorkstationWorkEndTime, SetupEndTime.Trim(), CancellationToken.None);
        await _settingsService.SetAsync(SettingKeys.WorkBearSetupCompleted, true, CancellationToken.None);
        OnPropertyChanged(nameof(WorkstationMonthlySalary));
        OnPropertyChanged(nameof(WorkstationWorkStartTime));
        OnPropertyChanged(nameof(WorkstationWorkEndTime));
        OnPropertyChanged(nameof(NeedsSetup));
        OnPropertyChanged(nameof(ShowEmptySalaryGuide));
        LastMessage = "30 秒配置完成。小熊已按你的月薪和工时估算今日状态。";
        await RefreshAsync();
    }

    public async Task DismissSetupAsync()
    {
        if (_settingsService is null)
        {
            return;
        }

        await _settingsService.SetAsync(SettingKeys.WorkBearSetupCompleted, true, CancellationToken.None);
        OnPropertyChanged(nameof(NeedsSetup));
        OnPropertyChanged(nameof(ShowEmptySalaryGuide));
        LastMessage = "已跳过引导。可随时在下方「工作规则设置」里补月薪和上下班时间，或点上方引导卡片。";
    }

    private async Task SetSettingAsync<T>(string key, T value)
    {
        if (_settingsService is null)
        {
            return;
        }

        await _settingsService.SetAsync(key, value, CancellationToken.None);
        await RefreshAsync();
        ValidateWorkRuleMessage();
    }

    private void ValidateWorkRuleMessage()
    {
        if (TimeOnly.TryParse(WorkstationWorkStartTime, out var start) &&
            TimeOnly.TryParse(WorkstationWorkEndTime, out var end) &&
            end <= start)
        {
            LastMessage = "下班时间不能早于或等于上班时间，请调整工作规则。";
            return;
        }

        if (!TimeOnly.TryParse(WorkstationWorkStartTime, out _) ||
            !TimeOnly.TryParse(WorkstationWorkEndTime, out _))
        {
            LastMessage = "时间格式建议使用 HH:mm，例如 09:00。";
        }
    }
}
