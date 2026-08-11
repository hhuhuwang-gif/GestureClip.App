using GestureClip.Core.Abstractions;
using GestureClip.Core.Assistant;
using GestureClip.Core.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GestureClip.Features.Assistant;

public sealed class AssistantActionExecutor : IAssistantActionExecutor
{
    private static readonly TimeSpan CaptureSuppressWindow = TimeSpan.FromMilliseconds(800);

    private readonly IAssistantActionCatalog _catalog;
    private readonly IClipboardService _clipboardService;
    private readonly IClipboardTextReader _clipboardTextReader;
    private readonly IClipboardWriter _clipboardWriter;
    private readonly IAppLifecycleService _appLifecycleService;
    // Resolve diagnostics on demand to avoid DI cycle:
    // MouseGesture -> GestureExecutor -> Assistant -> Diagnostics -> MouseGesture.
    private readonly IServiceProvider _serviceProvider;
    private readonly IWorkerLevelService? _workerLevelService;
    private readonly ISettingsService? _settingsService;
    private readonly ILogger<AssistantActionExecutor> _logger;

    public AssistantActionExecutor(
        IAssistantActionCatalog catalog,
        IClipboardService clipboardService,
        IClipboardTextReader clipboardTextReader,
        IClipboardWriter clipboardWriter,
        IAppLifecycleService appLifecycleService,
        IServiceProvider serviceProvider,
        ILogger<AssistantActionExecutor> logger,
        IWorkerLevelService? workerLevelService = null,
        ISettingsService? settingsService = null)
    {
        _catalog = catalog;
        _clipboardService = clipboardService;
        _clipboardTextReader = clipboardTextReader;
        _clipboardWriter = clipboardWriter;
        _appLifecycleService = appLifecycleService;
        _serviceProvider = serviceProvider;
        _logger = logger;
        _workerLevelService = workerLevelService;
        _settingsService = settingsService;
    }

    public async Task<AssistantActionResult> ExecuteAsync(AssistantActionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var definition = _catalog.GetById(request.ActionId);
        if (definition is null)
        {
            return Fail("unknown_action", "找不到这个动作。");
        }

        if (definition.PrivacyLevel != AssistantPrivacyLevel.LocalOnly)
        {
            return Fail("network_action_disabled", "联网/AI 动作默认关闭。");
        }

        try
        {
            var result = definition.Id switch
            {
                BuiltInAssistantActionCatalog.OpenSettingsId => ExecuteOpenSettings(),
                BuiltInAssistantActionCatalog.ExportDiagnosticsId => await ExecuteExportDiagnosticsAsync(cancellationToken),
                _ => await ExecuteTextActionAsync(definition, request, cancellationToken)
            };

            if (result.Success)
            {
                await TryAwardAssistantXpAsync(cancellationToken);
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Never log raw clipboard text.
            _logger.LogWarning(
                ex,
                "Assistant action failed. ActionId={ActionId} ErrorClass={ErrorClass}",
                definition.Id,
                "executor_exception");
            return Fail("executor_exception", "执行失败，请重试。");
        }
    }

    private AssistantActionResult ExecuteOpenSettings()
    {
        _appLifecycleService.ShowSettingsWindow();
        return new AssistantActionResult(true, Message: "已打开设置。");
    }

    private async Task<AssistantActionResult> ExecuteExportDiagnosticsAsync(CancellationToken cancellationToken)
    {
        var diagnostics = _serviceProvider.GetRequiredService<IDiagnosticsService>();
        var path = await diagnostics.ExportPackageAsync(cancellationToken);
        return new AssistantActionResult(true, Message: $"诊断包已导出：{path}");
    }

    private async Task<AssistantActionResult> ExecuteTextActionAsync(
        AssistantActionDefinition definition,
        AssistantActionRequest request,
        CancellationToken cancellationToken)
    {
        var input = request.InputText;
        if (string.IsNullOrEmpty(input))
        {
            input = _clipboardTextReader.TryReadText();
        }

        if (string.IsNullOrEmpty(input))
        {
            return Fail("empty_input", "剪贴板里没有可用文本。先复制一段文字再试。", 0);
        }

        if (!TryTransform(definition.Id, input, out var output, out var errorClass, out var errorMessage))
        {
            return Fail(errorClass ?? "transform_failed", errorMessage ?? "处理失败。", input.Length);
        }

        var outputKind = request.OutputOverride ?? definition.DefaultOutput;
        if (outputKind is AssistantOutputKind.Clipboard or AssistantOutputKind.Paste)
        {
            _clipboardService.SuppressCaptureFor(CaptureSuppressWindow);
            await _clipboardWriter.SetTextAsync(output, cancellationToken);
            if (outputKind == AssistantOutputKind.Paste)
            {
                await Task.Delay(70, cancellationToken);
                await _clipboardWriter.SendPasteHotkeyAsync(cancellationToken);
            }
        }

        var message = outputKind switch
        {
            AssistantOutputKind.Paste => "已处理并粘贴。",
            AssistantOutputKind.Clipboard => "已处理并复制到系统剪贴板。",
            _ => "已生成预览。"
        };

        _logger.LogInformation(
            "Assistant action completed. ActionId={ActionId} InputLength={InputLength} OutputLength={OutputLength} Output={Output}",
            definition.Id,
            input.Length,
            output.Length,
            outputKind);

        return new AssistantActionResult(
            Success: true,
            PreviewText: TruncatePreview(output),
            Message: message,
            InputLength: input.Length,
            OutputLength: output.Length);
    }

    private static bool TryTransform(
        string actionId,
        string input,
        out string output,
        out string? errorClass,
        out string? errorMessage)
    {
        errorClass = null;
        errorMessage = null;
        output = input;

        switch (actionId)
        {
            case BuiltInAssistantActionCatalog.TrimId:
                output = LocalTextTransforms.Trim(input);
                return true;
            case BuiltInAssistantActionCatalog.NormalizeWhitespaceId:
                output = LocalTextTransforms.NormalizeWhitespace(input);
                return true;
            case BuiltInAssistantActionCatalog.CollapseBlankLinesId:
                output = LocalTextTransforms.CollapseBlankLines(input);
                return true;
            case BuiltInAssistantActionCatalog.UpperId:
                output = LocalTextTransforms.ToUpper(input);
                return true;
            case BuiltInAssistantActionCatalog.LowerId:
                output = LocalTextTransforms.ToLower(input);
                return true;
            case BuiltInAssistantActionCatalog.TitleCaseId:
                output = LocalTextTransforms.ToTitleCase(input);
                return true;
            case BuiltInAssistantActionCatalog.JsonFormatId:
                if (!LocalTextTransforms.TryFormatJson(input, out output, out errorClass))
                {
                    errorMessage = "不是有效 JSON，无法美化。";
                    return false;
                }

                return true;
            case BuiltInAssistantActionCatalog.JsonMinifyId:
                if (!LocalTextTransforms.TryMinifyJson(input, out output, out errorClass))
                {
                    errorMessage = "不是有效 JSON，无法压缩。";
                    return false;
                }

                return true;
            case BuiltInAssistantActionCatalog.UrlEncodeId:
                output = LocalTextTransforms.UrlEncode(input);
                return true;
            case BuiltInAssistantActionCatalog.UrlDecodeId:
                output = LocalTextTransforms.UrlDecode(input);
                return true;
            case BuiltInAssistantActionCatalog.QuoteId:
                output = LocalTextTransforms.Quote(input);
                return true;
            case BuiltInAssistantActionCatalog.UnquoteId:
                output = LocalTextTransforms.Unquote(input);
                return true;
            case BuiltInAssistantActionCatalog.PlainTextId:
                output = LocalTextTransforms.ToPlainText(input);
                return true;
            case BuiltInAssistantActionCatalog.HtmlToTextId:
                output = LocalTextTransforms.HtmlToPlainText(input);
                return true;
            case BuiltInAssistantActionCatalog.ToMarkdownId:
                output = LocalTextTransforms.ToMarkdownLite(input);
                return true;
            case BuiltInAssistantActionCatalog.CleanUrlId:
                output = LocalTextTransforms.CleanTrackingUrls(input);
                return true;
            default:
                errorClass = "unknown_action";
                errorMessage = "找不到这个动作。";
                return false;
        }
    }

    private static AssistantActionResult Fail(string errorClass, string message, int inputLength = 0)
    {
        return new AssistantActionResult(
            Success: false,
            Message: message,
            ErrorClass: errorClass,
            InputLength: inputLength);
    }

    private static string TruncatePreview(string text)
    {
        const int max = 4000;
        if (text.Length <= max)
        {
            return text;
        }

        return text[..max] + "\n…(预览已截断)";
    }

    private async Task TryAwardAssistantXpAsync(CancellationToken cancellationToken)
    {
        if (_workerLevelService is null)
        {
            return;
        }

        if (_settingsService?.Get(SettingKeys.WorkBearGestureXpBonusEnabled, true) == false)
        {
            return;
        }

        try
        {
            // Small local XP for deterministic assistant actions (no network).
            await _workerLevelService.RecordBonusXpAsync(3, DateTimeOffset.Now, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Assistant XP award skipped.");
        }
    }
}
