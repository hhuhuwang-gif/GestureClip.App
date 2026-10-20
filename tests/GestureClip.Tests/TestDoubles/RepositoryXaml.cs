using System.Xml.Linq;

namespace GestureClip.Tests.TestDoubles;

internal static class RepositoryXaml
{
    private const string HubNamespace = "clr-namespace:GestureClip.App.Controls.Hub";
    private static readonly XNamespace XamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";

    public static string Read(string path) => string.Join(Environment.NewLine,
        LoadDocuments(path).Select(document => document.ToString()));

    public static IReadOnlyList<XDocument> LoadDocuments(string path)
    {
        var documents = new Dictionary<string, XDocument>(StringComparer.OrdinalIgnoreCase);
        LoadReferencedDocuments(Path.GetFullPath(path), FindAppDirectory(path), documents);
        return documents.Values.ToArray();
    }

    private static void LoadReferencedDocuments(string path, string appDirectory,
        Dictionary<string, XDocument> documents)
    {
        if (documents.ContainsKey(path)) return;
        var document = XDocument.Load(path);
        documents.Add(path, document);
        foreach (var reference in FindReferences(document, path, appDirectory))
        {
            LoadReferencedDocuments(reference, appDirectory, documents);
        }
    }

    private static IEnumerable<string> FindReferences(XDocument document, string path, string appDirectory)
    {
        foreach (var element in document.Descendants())
        {
            if (element.Name.NamespaceName == HubNamespace)
            {
                var controlPath = Path.Combine(appDirectory, "Controls", "Hub", element.Name.LocalName + ".xaml");
                var control = XDocument.Load(controlPath);
                var expectedClass = "GestureClip.App.Controls.Hub." + element.Name.LocalName;
                if ((string?)control.Root?.Attribute(XamlNamespace + "Class") != expectedClass)
                    throw new InvalidDataException($"XAML class does not match its reference: {controlPath}");
                yield return controlPath;
            }

            if (element.Name.LocalName != "ResourceDictionary" ||
                element.Parent?.Name.LocalName != "ResourceDictionary.MergedDictionaries") continue;
            var source = (string?)element.Attribute("Source");
            if (source is null) continue;
            yield return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, source));
        }
    }

    private static string FindAppDirectory(string path)
    {
        for (var directory = new DirectoryInfo(Path.GetDirectoryName(Path.GetFullPath(path))!);
             directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "GestureClip.App.csproj")))
                return directory.FullName;
        }

        throw new DirectoryNotFoundException($"GestureClip.App directory not found for {path}");
    }
}
