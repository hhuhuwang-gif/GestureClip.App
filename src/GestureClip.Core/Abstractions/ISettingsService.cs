namespace GestureClip.Core.Abstractions;

public interface ISettingsService
{
    T Get<T>(string key, T defaultValue);

    Task SetAsync<T>(string key, T value, CancellationToken cancellationToken);
}
