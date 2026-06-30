using System.IO;

namespace GestureClip.Infrastructure.Paths;

public sealed class AppPathProvider
{
    public AppPathProvider()
        : this(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData))
    {
    }

    public AppPathProvider(string localAppDataPath)
    {
        RootDirectory = Path.Combine(localAppDataPath, "GestureClip");
        LogDirectory = Path.Combine(RootDirectory, "logs");
        DatabasePath = Path.Combine(RootDirectory, "gestureclip.db");
    }

    public string RootDirectory { get; }

    public string DatabasePath { get; }

    public string LogDirectory { get; }

    public void EnsureDirectories()
    {
        Directory.CreateDirectory(RootDirectory);
        Directory.CreateDirectory(LogDirectory);
    }
}
