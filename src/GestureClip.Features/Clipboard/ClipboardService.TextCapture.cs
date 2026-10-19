using System.Diagnostics;
using GestureClip.Core.Clipboard;
using GestureClip.Core.Settings;
using Microsoft.Extensions.Logging;

namespace GestureClip.Features.Clipboard;

public sealed partial class ClipboardService
{
    public async Task CaptureTextAsync(ClipboardCapture capture, CancellationToken cancellationToken)
    {
        if (!IsCaptureEnabled)
        {
            _logger.LogInformation("Clipboard capture skipped: disabled.");
            return;
        }

        if (string.IsNullOrWhiteSpace(capture.Text))
        {
            _logger.LogInformation("Clipboard capture skipped: empty text.");
            return;
        }

        var hashWatch = Stopwatch.StartNew();
        var hash = _clipboardHashService.ComputeHash(capture.Text);
        var plainTextHash = _clipboardHashService.ComputePlainTextHash(capture.Text);
        hashWatch.Stop();
        LogPerf("HashMs", hashWatch.ElapsedMilliseconds, ("ContentType", "text"));

        if (ShouldSkipAsInternalEcho(hash, imageHash: null))
        {
            DebugPerf("CAPTURE_SUPPRESSED_INTERNAL", "ContentType=text");
            return;
        }

        if (await _appBlacklistService.IsClipboardBlockedAsync(capture.SourceProcess, cancellationToken))
        {
            _logger.LogInformation("Clipboard capture skipped: source process is blacklisted.");
            return;
        }

        if (_sensitiveCaptureGate?.ShouldSkipCapture(capture.SourceProcess, capture.SourceApp) == true)
        {
            _logger.LogInformation("Clipboard capture skipped: sensitive UI context (password/login).");
            return;
        }

        var dedupWatch = Stopwatch.StartNew();
        var existing = await _clipboardRepository.FindByHashAsync(hash, cancellationToken);
        dedupWatch.Stop();
        LogPerf("DedupQueryMs", dedupWatch.ElapsedMilliseconds, ("ContentType", "text"));
        if (existing is not null)
        {
            await _clipboardRepository.TouchAsync(existing.Id, cancellationToken);
            MarkHistoryChanged();
            RecordCopyInBackground(DateTimeOffset.UtcNow);
            _logger.LogInformation("Clipboard duplicate item refreshed.");
            return;
        }

        await StoreTextCaptureAsync(capture, (hash, plainTextHash), cancellationToken);
    }

    private async Task StoreTextCaptureAsync(
        ClipboardCapture capture,
        (string Hash, string? PlainTextHash) hashes,
        CancellationToken cancellationToken)
    {
        var isSensitive = _sensitiveContentDetector.LooksSensitive(capture.Text);
        if (isSensitive && _settingsService.Get(SettingKeys.PrivacySuppressSensitive, true))
        {
            _logger.LogInformation("Clipboard capture skipped: sensitive text detected.");
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var item = new ClipboardItem(
            Guid.NewGuid(),
            "text",
            capture.Text,
            CreatePreview(capture.Text),
            hashes.Hash,
            hashes.PlainTextHash,
            capture.SourceApp,
            capture.SourceProcess,
            false,
            false,
            isSensitive,
            0,
            now,
            now,
            null);

        var insertWatch = Stopwatch.StartNew();
        await _clipboardRepository.InsertAsync(item, cancellationToken);
        MarkHistoryChanged();
        insertWatch.Stop();
        LogPerf("InsertMs", insertWatch.ElapsedMilliseconds, ("ContentType", "text"));
        RecordCopyInBackground(now);
        DebugPerf("CAPTURE_SAVED", "ContentType=text");
        _logger.LogInformation("Clipboard text item captured.");
    }

}
