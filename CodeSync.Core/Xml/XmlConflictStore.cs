using System.Text;

namespace CodeSync.Core.Xml;

/// <summary>
///   A store for versioned conflict reports associated with synchronization profiles
///   that persists them using XML.
/// </summary>
public sealed class XmlConflictStore : IConflictStore
{
    /// <inheritdoc/>
    public ConflictDocument? Load(string path)
    {
        return File.Exists(path)
            ? XmlConflicts.DeserializeConflicts(File.ReadAllText(path, Encoding.UTF8))
            : null;
    }

    /// <inheritdoc/>
    public void Save(string path, ConflictSet conflicts)
    {
        AtomicTextFile.Write(path, XmlConflicts.SerializeConflicts(conflicts));
    }
}
