using System.Text;

using CodeSync.Core;

namespace CodeSync.Infrastructure;

/// <summary>
///   A store for versioned conflict reports associated with synchronization profiles
///   that persists them using XML.
/// </summary>
public sealed class XmlConflictStore : IConflictStore
{
    /// <inheritdoc/>
    public ConflictSet? Load(string path)
    {
        return File.Exists(path)
            ? XmlCodecs.DeserializeConflicts(File.ReadAllText(path, Encoding.UTF8))
            : null;
    }

    /// <inheritdoc/>
    public void Save(string path, ConflictSet conflicts)
    {
        AtomicTextFile.Write(path, XmlCodecs.SerializeConflicts(conflicts));
    }
}
