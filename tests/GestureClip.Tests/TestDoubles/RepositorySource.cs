namespace GestureClip.Tests.TestDoubles;

public static class RepositorySource
{
    public static string ReadClassSources(string rootFile)
    {
        var directory = Path.GetDirectoryName(rootFile)
            ?? throw new ArgumentException("Source file must have a directory.", nameof(rootFile));
        var prefix = Path.GetFileNameWithoutExtension(rootFile);
        const string XamlSuffix = ".xaml";
        if (prefix.EndsWith(XamlSuffix, StringComparison.Ordinal))
        {
            prefix = prefix[..^XamlSuffix.Length];
        }
        var sourceFiles = Directory.GetFiles(directory, $"{prefix}*.cs");
        if (sourceFiles.Length == 0)
        {
            throw new FileNotFoundException("No class source files found.", rootFile);
        }
        return string.Join(Environment.NewLine, sourceFiles.OrderBy(path => path).Select(File.ReadAllText));
    }
}
