using System.Text;

namespace CodeSync.Core.Xml;

/// <summary>
///   A store for skipped source paths associated with synchronization profiles
///   that persists them using XML.
/// </summary>
public sealed class XmlSkippedStore : ISkippedStore
{
    /// <inheritdoc/>
    public IReadOnlyList<string> Load(string path)
    {
        return File.Exists(path)
            ? XmlSkipped.DeserializeSkipped(File.ReadAllText(path, Encoding.UTF8))
            : [];
    }

    /// <inheritdoc/>
    public void Save(string path, IEnumerable<string> sourcePaths)
    {
        AtomicTextFile.Write(path, XmlSkipped.SerializeSkipped(sourcePaths));
    }
}
