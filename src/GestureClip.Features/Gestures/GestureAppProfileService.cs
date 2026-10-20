using System.Collections.ObjectModel;
using System.Text.Json;
using GestureClip.Core.Abstractions;
using GestureClip.Core.Gestures;

namespace GestureClip.Features.Gestures;

public sealed class GestureAppProfileService : IGestureAppProfileService
{
    public const string SettingsKey = "gesture.appProfilesJson";
    private readonly ISettingsService _settings;
    private readonly SemaphoreSlim _saveLock = new(1, 1);
    private IReadOnlyList<GestureAppProfile> _profiles;

    public GestureAppProfileService(ISettingsService settings)
    {
        _settings = settings;
        var json = settings.Get(SettingsKey, "[]");
        var profiles = JsonSerializer.Deserialize<GestureAppProfile[]>(json)
            ?? throw new InvalidDataException("应用手势配置不能为 null。");
        _profiles = Validate(profiles);
    }

    public IReadOnlyList<GestureAppProfile> GetCurrent() => Volatile.Read(ref _profiles);

    public async Task SaveAsync(IReadOnlyList<GestureAppProfile> profiles, CancellationToken cancellationToken)
    {
        var validated = Validate(profiles);
        await _saveLock.WaitAsync(cancellationToken);
        try
        {
            await _settings.SetAsync(SettingsKey, JsonSerializer.Serialize(validated), cancellationToken);
            Volatile.Write(ref _profiles, validated);
        }
        finally
        {
            _saveLock.Release();
        }
    }

    public bool TryGetAction(GestureExecutionContext context, out BuiltInGestureAction action)
    {
        action = BuiltInGestureAction.None;
        if (string.IsNullOrWhiteSpace(context.TargetProcessName)) return false;
        var process = NormalizeProcessName(context.TargetProcessName);
        var profile = GetCurrent().FirstOrDefault(item => item.IsEnabled && item.ProcessName == process);
        var binding = profile?.Bindings.FirstOrDefault(item =>
            item.Pattern == context.Pattern && item.IsLeftButtonModified == context.IsLeftButtonModified);
        if (binding is null) return false;
        action = binding.Action;
        return true;
    }

    public static string NormalizeProcessName(string name)
    {
        var normalized = name.Trim();
        if (normalized.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) normalized = normalized[..^4];
        if (normalized.Length == 0 || normalized.IndexOfAny(['/', '\\', ':', '*', '?', '"', '<', '>', '|']) >= 0)
            throw new ArgumentException("请输入准确的进程名，例如 EXCEL.exe；不接受路径或通配符。");
        return normalized.ToLowerInvariant();
    }

    private static IReadOnlyList<GestureAppProfile> Validate(IReadOnlyList<GestureAppProfile> profiles)
    {
        var snapshot = profiles.Select(profile => new GestureAppProfile(
            NormalizeProcessName(profile.ProcessName), profile.IsEnabled, ValidateBindings(profile.Bindings))).ToArray();
        if (snapshot.Select(profile => profile.ProcessName).Distinct().Count() != snapshot.Length)
            throw new ArgumentException("同一进程只能有一份应用手势配置。");
        return Array.AsReadOnly(snapshot);
    }

    private static ReadOnlyCollection<GestureAppBinding> ValidateBindings(IReadOnlyList<GestureAppBinding> bindings)
    {
        foreach (var binding in bindings)
        {
            if (!IsValidPattern(binding.Pattern) || !Enum.IsDefined(binding.Action))
                throw new ArgumentException("应用手势包含无效的手势码或动作。");
        }
        if (bindings.Select(binding => (binding.Pattern, binding.IsLeftButtonModified)).Distinct().Count() != bindings.Count)
            throw new ArgumentException("同一绑定模式内，每个手势只能配置一次。");
        return Array.AsReadOnly(bindings.ToArray());
    }

    private static bool IsValidPattern(string pattern) =>
        pattern is "R+L" or "WHEEL_UP" or "WHEEL_DOWN" ||
        (!string.IsNullOrEmpty(pattern) && pattern.All(direction => "UDLR".Contains(direction)));
}
