using Dapper;
using GestureClip.Core.Abstractions;
using GestureClip.Core.Privacy;
using GestureClip.Infrastructure.Database;
using Microsoft.Extensions.Logging;

namespace GestureClip.Features.Privacy;

public sealed class AppBlacklistService : IAppBlacklistService
{
    private static readonly DefaultBlacklistItem[] DefaultBlacklistItems =
    [
        new("1password.exe", "默认隐私保护：密码管理器", true, false),
        new("bitwarden.exe", "默认隐私保护：密码管理器", true, false),
        new("keepass.exe", "默认隐私保护：密码管理器", true, false),
        new("keepassxc.exe", "默认隐私保护：密码管理器", true, false),
        new("lastpass.exe", "默认隐私保护：密码管理器", true, false),
        new("authy.exe", "默认隐私保护：双因素验证工具", true, false),
        new("msrdc.exe", "默认隐私保护：远程桌面", true, true),
        new("mstsc.exe", "默认隐私保护：远程桌面", true, true),
        new("anydesk.exe", "默认隐私保护：远程控制", true, true),
        new("teamviewer.exe", "默认隐私保护：远程控制", true, true)
    ];

    private readonly ISqliteConnectionFactory _connectionFactory;
    private readonly ILogger<AppBlacklistService> _logger;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private IReadOnlyList<AppBlacklistItem> _items = [];
    private HashSet<string> _clipboardBlocked = new(StringComparer.OrdinalIgnoreCase);
    private HashSet<string> _gestureBlocked = new(StringComparer.OrdinalIgnoreCase);
    private bool _loaded;

    public AppBlacklistService(
        ISqliteConnectionFactory connectionFactory,
        ILogger<AppBlacklistService> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task<IReadOnlyList<AppBlacklistItem>> GetAllAsync(CancellationToken cancellationToken)
    {
        await EnsureLoadedAsync(cancellationToken);
        return _items;
    }

    public async Task AddAsync(string processName, bool blockClipboard, bool blockGesture, CancellationToken cancellationToken)
    {
        var normalized = NormalizeProcessName(processName);
        if (normalized is null)
        {
            throw new ArgumentException("Process name cannot be empty.", nameof(processName));
        }

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow.ToString("O");
        var existing = await connection.ExecuteScalarAsync<int>(
            """
SELECT COUNT(*)
FROM AppBlacklist
WHERE lower(ProcessName) = lower(@ProcessName);
""",
            new { ProcessName = normalized });

        if (existing == 0)
        {
            await connection.ExecuteAsync(
                """
INSERT INTO AppBlacklist (Id, ProcessName, Reason, BlockClipboard, BlockGesture, CreatedAt, UpdatedAt)
VALUES (@Id, @ProcessName, @Reason, @BlockClipboard, @BlockGesture, @CreatedAt, @UpdatedAt);
""",
                new
                {
                    Id = Guid.NewGuid().ToString(),
                    ProcessName = normalized,
                    Reason = (string?)null,
                    BlockClipboard = blockClipboard ? 1 : 0,
                    BlockGesture = blockGesture ? 1 : 0,
                    CreatedAt = now,
                    UpdatedAt = now
                });
        }

        await RefreshAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(
            "DELETE FROM AppBlacklist WHERE Id = @Id;",
            new { Id = id.ToString() });
        await RefreshAsync(cancellationToken);
    }

    public async Task UpdateAsync(Guid id, bool blockClipboard, bool blockGesture, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(
            """
UPDATE AppBlacklist
SET BlockClipboard = @BlockClipboard,
    BlockGesture = @BlockGesture,
    UpdatedAt = @UpdatedAt
WHERE Id = @Id;
""",
            new
            {
                Id = id.ToString(),
                BlockClipboard = blockClipboard ? 1 : 0,
                BlockGesture = blockGesture ? 1 : 0,
                UpdatedAt = DateTimeOffset.UtcNow.ToString("O")
            });
        await RefreshAsync(cancellationToken);
    }

    public async Task<bool> IsClipboardBlockedAsync(string? processName, CancellationToken cancellationToken)
    {
        await EnsureLoadedAsync(cancellationToken);
        var normalized = NormalizeProcessName(processName);
        return normalized is not null && _clipboardBlocked.Contains(normalized);
    }

    public async Task<bool> IsGestureBlockedAsync(string? processName, CancellationToken cancellationToken)
    {
        await EnsureLoadedAsync(cancellationToken);
        return IsGestureBlockedCached(processName);
    }

    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
            await SeedDefaultBlacklistAsync(connection);
            var rows = await connection.QueryAsync<AppBlacklistRow>(
                """
SELECT Id, ProcessName, Reason, BlockClipboard, BlockGesture
FROM AppBlacklist
ORDER BY ProcessName COLLATE NOCASE;
""");

            var items = rows.Select(row => row.ToModel()).ToArray();
            _items = items;
            _clipboardBlocked = items
                .Where(item => item.BlockClipboard)
                .Select(item => item.ProcessName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            _gestureBlocked = items
                .Where(item => item.BlockGesture)
                .Select(item => item.ProcessName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            _loaded = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh app blacklist.");
            throw;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    public bool IsGestureBlockedCached(string? processName)
    {
        var normalized = NormalizeProcessName(processName);
        return normalized is not null && _gestureBlocked.Contains(normalized);
    }

    private async Task EnsureLoadedAsync(CancellationToken cancellationToken)
    {
        if (_loaded)
        {
            return;
        }

        await RefreshAsync(cancellationToken);
    }

    private async Task SeedDefaultBlacklistAsync(System.Data.IDbConnection connection)
    {
        var now = DateTimeOffset.UtcNow.ToString("O");
        foreach (var item in DefaultBlacklistItems)
        {
            await connection.ExecuteAsync(
                """
INSERT OR IGNORE INTO AppBlacklist (Id, ProcessName, Reason, BlockClipboard, BlockGesture, CreatedAt, UpdatedAt)
VALUES (@Id, @ProcessName, @Reason, @BlockClipboard, @BlockGesture, @CreatedAt, @UpdatedAt);
""",
                new
                {
                    Id = Guid.NewGuid().ToString(),
                    item.ProcessName,
                    item.Reason,
                    BlockClipboard = item.BlockClipboard ? 1 : 0,
                    BlockGesture = item.BlockGesture ? 1 : 0,
                    CreatedAt = now,
                    UpdatedAt = now
                });
        }
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

    private sealed record DefaultBlacklistItem(
        string ProcessName,
        string Reason,
        bool BlockClipboard,
        bool BlockGesture);

    private sealed class AppBlacklistRow
    {
        public string Id { get; set; } = "";
        public string ProcessName { get; set; } = "";
        public string? Reason { get; set; }
        public int BlockClipboard { get; set; }
        public int BlockGesture { get; set; }

        public AppBlacklistItem ToModel()
        {
            return new AppBlacklistItem(
                Guid.Parse(Id),
                ProcessName,
                BlockClipboard == 1,
                BlockGesture == 1,
                Reason);
        }
    }
}
