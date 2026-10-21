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
    private bool _isEnabled;
    private string _status = "新配置默认关闭，保存后生效。";

    public GestureProfilesViewModel(IGestureAppProfileService service)
    {
        _service = service;
        Profiles = new(service.GetCurrent());
        SelectedProfile = Profiles.FirstOrDefault();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public ObservableCollection<GestureAppProfile> Profiles { get; }
    public ObservableCollection<GestureProfileBindingRow> Bindings { get; } = [];
    public IReadOnlyList<GestureActionOptionViewModel> Actions => GestureActionCatalog.DefaultOptions;
    public string ProcessName { get => _processName; set { _processName = value; Notify(); } }
    public bool IsEnabled { get => _isEnabled; set { _isEnabled = value; Notify(); } }
    public string Status { get => _status; private set { _status = value; Notify(); } }
    public bool HasProfiles => Profiles.Count > 0;
    public bool HasBindings => Bindings.Count > 0;
    public bool HasSelectedProfile => SelectedProfile is not null;
    public string EditorTitle => SelectedProfile is null ? "新建应用配置" : $"编辑 {SelectedProfile.ProcessName}";

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
            Notify(nameof(HasSelectedProfile));
            Notify(nameof(EditorTitle));
            NotifyBindingsState();
        }
    }

    public void NewProfile()
    {
        SelectedProfile = null;
        Status = "已建立空白草稿；选择模板或手动添加绑定。";
    }

    public void DuplicateSelectedProfile()
    {
        var source = SelectedProfile;
        if (source is null) return;
        var rows = source.Bindings.Select(binding => new GestureProfileBindingRow(binding)).ToArray();
        SelectedProfile = null;
        foreach (var row in rows) Bindings.Add(row);
        IsEnabled = false;
        NotifyBindingsState();
        Status = $"已复制 {source.ProcessName} 的绑定；请填写新进程名后保存。";
    }

    public GestureProfileBindingRow AddBinding()
    {
        var row = new GestureProfileBindingRow();
        Bindings.Add(row);
        NotifyBindingsState();
        Status = "已添加一条覆盖，可直接编辑手势码与动作。";
        return row;
    }

    public void RemoveBinding(GestureProfileBindingRow? row)
    {
        if (row is null || !Bindings.Remove(row)) return;
        NotifyBindingsState();
        Status = "已移除选中覆盖，保存后生效。";
    }

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
        NotifyBindingsState();
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
        Notify(nameof(HasProfiles));
    }

    private void NotifyBindingsState() => Notify(nameof(HasBindings));

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
