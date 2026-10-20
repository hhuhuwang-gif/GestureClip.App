using System.Xml.Linq;

namespace GestureClip.Tests.TestDoubles;

internal static class RepositoryXaml
{
    public static IReadOnlyList<XDocument> LoadDocuments(string path)
    {
        var documents = new Dictionary<string, XDocument>(StringComparer.OrdinalIgnoreCase);
        ReadReferencedDictionaries(Path.GetFullPath(path), documents);
        return documents.Values.ToArray();
    }

    private static void ReadReferencedDictionaries(string path, Dictionary<string, XDocument> documents)
    {
        if (documents.ContainsKey(path)) return;
        var document = XDocument.Load(path);
        documents.Add(path, document);
        var sources = document.Descendants().Where(element =>
            element.Name.LocalName == "ResourceDictionary" &&
            element.Parent?.Name.LocalName == "ResourceDictionary.MergedDictionaries")
            .Select(element => (string?)element.Attribute("Source"));
        foreach (var source in sources)
        {
            if (source is null) continue;
            var referencedPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, source));
            ReadReferencedDictionaries(referencedPath, documents);
        }
    }
}
