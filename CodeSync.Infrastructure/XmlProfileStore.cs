using System.Text;

using CodeSync.Core;

namespace CodeSync.Infrastructure;

/// <summary>
///   A store for versioned synchronization profiles that persists them using XML.
/// </summary>
public sealed class XmlProfileStore : IProfileStore
{
    /// <inheritdoc/>
    public SyncProfile Load(string path)
    {
        var xml = File.ReadAllText(path, Encoding.UTF8);
        return XmlCodecs.DeserializeProfile(xml);
    }

    /// <inheritdoc/>
    public void Save(string path, SyncProfile profile)
    {
        var xml = XmlCodecs.SerializeProfile(profile);
        AtomicTextFile.Write(path, xml);
    }
}
