using System.Text;

using CodeSync.Core;

namespace CodeSync.Infrastructure;

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
            ? XmlCodecs.DeserializeSkipped(File.ReadAllText(path, Encoding.UTF8))
            : [];
    }

    /// <inheritdoc/>
    public void Save(string path, IEnumerable<string> sourcePaths)
    {
        AtomicTextFile.Write(path, XmlCodecs.SerializeSkipped(sourcePaths));
    }
}
