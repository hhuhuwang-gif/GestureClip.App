using System.Diagnostics;
using GestureClip.Core.Clipboard;
using GestureClip.Core.SystemInfo;
using Microsoft.Extensions.Logging;

namespace GestureClip.Features.Clipboard;

public sealed partial class ClipboardService
{
    private async Task CaptureImageAsync(string pngBase64, DateTimeOffset changedAt, CancellationToken cancellationToken)
    {
        if (!IsCaptureEnabled)
        {
            _logger.LogInformation("Clipboard image capture skipped: disabled.");
            return;
        }

        var fitted = await FitImageForHistoryAsync(pngBase64, cancellationToken);
        if (fitted is null)
        {
            return;
        }

        var hashWatch = Stopwatch.StartNew();
        var hash = _clipboardHashService.ComputeHash(fitted);
        hashWatch.Stop();
        LogPerf("HashMs", hashWatch.ElapsedMilliseconds, ("ContentType", "image/png"));

        if (ShouldSkipAsInternalEcho(textHash: null, imageHash: hash))
        {
            DebugPerf("CAPTURE_SUPPRESSED_INTERNAL", "ContentType=image/png");
            return;
        }

        var foreground = _foregroundAppService.GetCurrent();
        if (await _appBlacklistService.IsClipboardBlockedAsync(foreground.ProcessName, cancellationToken))
        {
            _logger.LogInformation("Clipboard image capture skipped: source process is blacklisted.");
            return;
        }

        var dedupWatch = Stopwatch.StartNew();
        var duplicate = await _clipboardRepository.FindByHashAsync(hash, cancellationToken);
        dedupWatch.Stop();
        LogPerf("DedupQueryMs", dedupWatch.ElapsedMilliseconds, ("ContentType", "image/png"));
        if (duplicate is not null)
        {
            await _clipboardRepository.TouchAsync(duplicate.Id, cancellationToken);
            MarkHistoryChanged();
            _logger.LogInformation("Clipboard image capture skipped: duplicate hash; item refreshed.");
            return;
        }

        await StoreImageCaptureAsync((fitted, hash), foreground, cancellationToken);
    }

    private async Task StoreImageCaptureAsync(
        (string PngBase64, string Hash) image,
        ForegroundAppInfo foreground,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var insertWatch = Stopwatch.StartNew();
        var thumbnailWatch = Stopwatch.StartNew();
        var thumbnailContent = await Task.Run(
            () => GestureClip.Infrastructure.Clipboard.ClipboardImageFactory.TryCreateThumbnailPngBase64(image.PngBase64, 128),
            cancellationToken);
        thumbnailWatch.Stop();
        LogPerf("ThumbnailDecodeDurationMs", thumbnailWatch.ElapsedMilliseconds, ("ContentType", "image/png"));

        var itemId = Guid.NewGuid();
        var ocrText = await RecognizeImageTextAsync(
            string.IsNullOrWhiteSpace(thumbnailContent) ? image.PngBase64 : thumbnailContent,
            cancellationToken);

        var preview = string.IsNullOrWhiteSpace(ocrText)
            ? "图片"
            : (ocrText.Length > 80 ? ocrText[..80] + "…" : ocrText);

        await _clipboardRepository.InsertAsync(
            new ClipboardItem(
                itemId,
                "image/png",
                image.PngBase64,
                preview,
                image.Hash,
                null,
                foreground.WindowTitle,
                foreground.ProcessName,
                false,
                false,
                false,
                0,
                now,
                now,
                null,
                thumbnailContent,
                ocrText),
            cancellationToken);
        MarkHistoryChanged();
        insertWatch.Stop();
        LogPerf("InsertMs", insertWatch.ElapsedMilliseconds, ("ContentType", "image/png"));
        RecordCopyInBackground(now);
        DebugPerf("CAPTURE_SAVED", "ContentType=image/png");
        _logger.LogInformation("Clipboard image item captured. OcrChars={OcrChars}", ocrText?.Length ?? 0);
    }


    private async Task<string?> FitImageForHistoryAsync(string pngBase64, CancellationToken cancellationToken)
    {
        var imageBytes = EstimateBase64DecodedByteCount(pngBase64);
        var maxImageBytes = GetMaxImageBytes();
        if (imageBytes > maxImageBytes)
        {
            var resizeWatch = Stopwatch.StartNew();
            var fitted = await Task.Run(
                () => GestureClip.Infrastructure.Clipboard.ClipboardImageFactory.TryFitPngBase64(pngBase64, maxImageBytes),
                cancellationToken);
            resizeWatch.Stop();
            if (fitted is null)
            {
                _logger.LogInformation(
                    "Clipboard image capture skipped: image could not be fitted to the configured limit. ImageBytes={ImageBytes}, MaxImageBytes={MaxImageBytes}",
                    imageBytes,
                    maxImageBytes);
                return null;
            }

            var fittedBytes = EstimateBase64DecodedByteCount(fitted);
            LogPerf(
                "ImageFitDurationMs",
                resizeWatch.ElapsedMilliseconds,
                ("OriginalBytes", imageBytes),
                ("FittedBytes", fittedBytes),
                ("MaxImageBytes", maxImageBytes));
            _logger.LogInformation(
                "Clipboard image resized for history. OriginalBytes={OriginalBytes}, FittedBytes={FittedBytes}",
                imageBytes,
                fittedBytes);
            return fitted;
        }

        return pngBase64;
    }

    private async Task<string?> RecognizeImageTextAsync(string pngBase64, CancellationToken cancellationToken)
    {
        string? ocrText = null;
        if (_imageOcrService is not null)
        {
            try
            {
                var ocrWatch = Stopwatch.StartNew();
                ocrText = await _imageOcrService.RecognizePngBase64Async(
                    pngBase64,
                    cancellationToken);
                ocrWatch.Stop();
                LogPerf("OcrMs", ocrWatch.ElapsedMilliseconds, ("ContentType", "image/png"), ("OcrLen", ocrText?.Length ?? 0));
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Image OCR skipped.");
            }
        }

        return ocrText;
    }
}
