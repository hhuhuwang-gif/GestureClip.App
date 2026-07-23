using GestureClip.Core.Gestures;

namespace GestureClip.Core.Abstractions;

public interface IGestureConfigTransferService
{
    string ExportToJson(
        GesturePreset preset,
        IReadOnlyDictionary<string, BuiltInGestureAction> bindings,
        IReadOnlyDictionary<string, BuiltInGestureAction> leftButtonEnhanced);

    GestureConfigImportResult ImportFromJson(string json);
}

public sealed record GestureConfigImportResult(
    bool Success,
    string Message,
    GesturePreset Preset,
    IReadOnlyDictionary<string, BuiltInGestureAction> Bindings,
    IReadOnlyDictionary<string, BuiltInGestureAction> LeftButtonEnhanced);
