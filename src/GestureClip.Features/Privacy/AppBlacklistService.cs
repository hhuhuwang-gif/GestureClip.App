using Dapper;
using GestureClip.Core.Abstractions;
using GestureClip.Core.Privacy;
using GestureClip.Infrastructure.Database;
using Microsoft.Extensions.Logging;

namespace GestureClip.Features.Privacy;

public sealed class AppBlacklistService : IAppBlacklistService
{
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
