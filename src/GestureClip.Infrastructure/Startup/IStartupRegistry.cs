namespace GestureClip.Infrastructure.Startup;

public interface IStartupRegistry
{
    string? GetValue(string name);

    void SetValue(string name, string value);

    void DeleteValue(string name);
}
