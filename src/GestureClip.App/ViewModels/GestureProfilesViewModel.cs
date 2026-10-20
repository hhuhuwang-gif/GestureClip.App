using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using GestureClip.Core.Abstractions;
using GestureClip.Core.Gestures;
using GestureClip.Features.Gestures;

namespace GestureClip.App.ViewModels;

public sealed class GestureProfilesViewModel : INotifyPropertyChanged
{
    private readonly IGestureAppProfileService _service;
    private GestureAppProfile? _selectedProfile;
    private string _processName = "";
    private string _status = "新配置默认关闭，保存后生效。";

    public GestureProfilesViewModel(IGestureAppProfileService service)
    {
        _service = service;
        Profiles = new(service.GetCurrent());
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public ObservableCollection<GestureAppProfile> Profiles { get; }
    public ObservableCollection<GestureProfileBindingRow> Bindings { get; } = [];
    public IReadOnlyList<GestureActionOptionViewModel> Actions => GestureActionCatalog.DefaultOptions;
    public string ProcessName { get => _processName; set { _processName = value; Notify(); } }
    public bool IsEnabled { get; set; }
    public string Status { get => _status; private set { _status = value; Notify(); } }

    public GestureAppProfile? SelectedProfile
    {
        get => _selectedProfile;
        set
        {
            _selectedProfile = value;
            ProcessName = value?.ProcessName ?? "";
            IsEnabled = value?.IsEnabled ?? false;
            Bindings.Clear();
            foreach (var binding in value?.Bindings ?? []) Bindings.Add(new(binding));
            Notify();
            Notify(nameof(ProcessName));
            Notify(nameof(IsEnabled));
        }
    }

    public void NewProfile() => SelectedProfile = null;

    public void ApplyTemplate(string template)
    {
        if (SelectedProfile is not null || Bindings.Count > 0)
            throw new InvalidOperationException("请先点击“新增配置”，模板只填入空白草稿，不覆盖现有绑定。");
        var process = template switch { "Excel" => "excel", "浏览器" => "msedge", _ => "code" };
        ProcessName = process;
        var actions = template == "浏览器"
            ? new[] { BuiltInGestureAction.PreviousTab, BuiltInGestureAction.NextTab, BuiltInGestureAction.ReopenClosedTab }
            : new[] { BuiltInGestureAction.Copy, BuiltInGestureAction.PastePlainText, BuiltInGestureAction.Undo };
        var patterns = new[] { "U", "D", "RL" };
        for (var index = 0; index < patterns.Length; index++)
            Bindings.Add(new(new(patterns[index], false, actions[index])));
        Notify(nameof(ProcessName));
        Status = "模板已填入草稿，未启用；请确认进程和动作后保存。";
    }

    public async Task SaveAsync()
    {
        var profile = new GestureAppProfile(GestureAppProfileService.NormalizeProcessName(ProcessName),
            IsEnabled, Bindings.Select(row => row.ToBinding()).ToArray());
        var profiles = Profiles.Where(item => item != SelectedProfile).Append(profile).ToArray();
        await _service.SaveAsync(profiles, CancellationToken.None);
        ReplaceProfiles(profiles);
        SelectedProfile = profile;
        Status = "应用手势已保存；未覆盖的手势继续使用全局绑定。";
    }

    public async Task DeleteAsync()
    {
        if (SelectedProfile is null) return;
        var profiles = Profiles.Where(item => item != SelectedProfile).ToArray();
        await _service.SaveAsync(profiles, CancellationToken.None);
        ReplaceProfiles(profiles);
        NewProfile();
        Status = "配置已删除，该应用使用全局绑定。";
    }

    private void ReplaceProfiles(IEnumerable<GestureAppProfile> profiles)
    {
        Profiles.Clear();
        foreach (var profile in profiles) Profiles.Add(profile);
    }

    private void Notify([CallerMemberName] string? property = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
}

public sealed class GestureProfileBindingRow
{
    public GestureProfileBindingRow() { }

    public GestureProfileBindingRow(GestureAppBinding binding)
    {
        Pattern = binding.Pattern;
        IsLeftButtonModified = binding.IsLeftButtonModified;
        Action = binding.Action;
    }

    public string Pattern { get; set; } = "D";
    public bool IsLeftButtonModified { get; set; }
    public BuiltInGestureAction Action { get; set; } = BuiltInGestureAction.PastePlainText;

    public GestureAppBinding ToBinding() => new(Pattern.Trim().ToUpperInvariant(), IsLeftButtonModified, Action);
}
