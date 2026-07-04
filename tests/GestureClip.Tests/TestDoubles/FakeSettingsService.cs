using GestureClip.Core.Abstractions;

namespace GestureClip.Tests.TestDoubles;

public sealed class FakeSettingsService : ISettingsService
{
    public Dictionary<string, object?> Values { get; } = [];

    public T Get<T>(string key, T defaultValue)
    {
        if (!Values.TryGetValue(key, out var value) || value is null)
        {
            return defaultValue;
        }

        if (value is T typed)
        {
            return typed;
        }

        try
        {
            return (T)Convert.ChangeType(value, typeof(T));
        }
        catch
        {
            return defaultValue;
        }
    }

    public Task SetAsync<T>(string key, T value, CancellationToken cancellationToken)
    {
        Values[key] = value;
        return Task.CompletedTask;
    }
}
