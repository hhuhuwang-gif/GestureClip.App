using System.Text.Json;
using GestureClip.Core.Abstractions;
using GestureClip.Core.Privacy;
using GestureClip.Core.Settings;
using Microsoft.Extensions.Logging;

namespace GestureClip.Features.Privacy;

public sealed class AppSmartPasteRuleService : IAppSmartPasteRuleService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNameCaseInsensitive = true
    };

    private readonly ISettingsService _settingsService;
    private readonly ILogger<AppSmartPasteRuleService> _logger;
    private readonly object _sync = new();
    private Dictionary<string, AppSmartPasteRule> _rules = new(StringComparer.OrdinalIgnoreCase);
    private bool _loaded;

    public AppSmartPasteRuleService(ISettingsService settingsService, ILogger<AppSmartPasteRuleService> logger)
    {
        _settingsService = settingsService;
        _logger = logger;
    }

    public async Task<IReadOnlyList<AppSmartPasteRule>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await RefreshAsync(cancellationToken);
        lock (_sync)
        {
            return _rules.Values
                .OrderBy(r => r.ProcessName, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
    }

    public async Task SetAsync(string processName, string strategy, string? note = null, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeProcessName(processName);
        if (normalized is null)
        {
            throw new ArgumentException("Process name cannot be empty.", nameof(processName));
        }

        strategy = NormalizeStrategy(strategy);
        await RefreshAsync(cancellationToken);
        lock (_sync)
        {
            _rules[normalized] = new AppSmartPasteRule(normalized, strategy, note);
        }

        await PersistAsync(cancellationToken);
    }

    public async Task DeleteAsync(string processName, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeProcessName(processName);
        if (normalized is null)
        {
            return;
        }

        await RefreshAsync(cancellationToken);
        lock (_sync)
        {
            _rules.Remove(normalized);
        }

        await PersistAsync(cancellationToken);
    }

    public string? TryGetStrategy(string? processName)
    {
        EnsureLoadedSync();
        var normalized = NormalizeProcessName(processName);
        if (normalized is null)
        {
            return null;
        }

        lock (_sync)
        {
            return _rules.TryGetValue(normalized, out var rule) ? rule.Strategy : null;
        }
    }

    public Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var json = _settingsService.Get(SettingKeys.AppSmartPasteRulesJson, "");
            var map = new Dictionary<string, AppSmartPasteRule>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(json))
            {
                var list = JsonSerializer.Deserialize<List<AppSmartPasteRuleDto>>(json, JsonOptions);
                if (list is not null)
                {
                    foreach (var dto in list)
                    {
                        var name = NormalizeProcessName(dto.ProcessName);
                        if (name is null || string.IsNullOrWhiteSpace(dto.Strategy))
                        {
                            continue;
                        }

                        map[name] = new AppSmartPasteRule(name, NormalizeStrategy(dto.Strategy), dto.Note);
                    }
                }
            }

            lock (_sync)
            {
                _rules = map;
                _loaded = true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load app smart paste rules.");
            lock (_sync)
            {
                _rules = new Dictionary<string, AppSmartPasteRule>(StringComparer.OrdinalIgnoreCase);
                _loaded = true;
            }
        }

        return Task.CompletedTask;
    }

    private void EnsureLoadedSync()
    {
        if (_loaded)
        {
            return;
        }

        RefreshAsync().GetAwaiter().GetResult();
    }

    private async Task PersistAsync(CancellationToken cancellationToken)
    {
        List<AppSmartPasteRuleDto> list;
        lock (_sync)
        {
            list = _rules.Values
                .Select(r => new AppSmartPasteRuleDto
                {
                    ProcessName = r.ProcessName,
                    Strategy = r.Strategy,
                    Note = r.Note
                })
                .OrderBy(r => r.ProcessName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        var json = JsonSerializer.Serialize(list, JsonOptions);
        await _settingsService.SetAsync(SettingKeys.AppSmartPasteRulesJson, json, cancellationToken);
    }

    private static string? NormalizeProcessName(string? processName)
    {
        var normalized = processName?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        if (!normalized.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            normalized += ".exe";
        }

        return normalized;
    }

    private static string NormalizeStrategy(string strategy)
    {
        strategy = strategy.Trim();
        return strategy.ToLowerInvariant() switch
        {
            "plain" or "plaintext" or "plaintextpaste" => "PlainTextPaste",
            "clean" or "cleantext" or "cleantextpaste" => "CleanTextPaste",
            "normal" or "normalpaste" => "NormalPaste",
            _ => strategy
        };
    }

    private sealed class AppSmartPasteRuleDto
    {
        public string ProcessName { get; set; } = "";
        public string Strategy { get; set; } = "NormalPaste";
        public string? Note { get; set; }
    }
}
