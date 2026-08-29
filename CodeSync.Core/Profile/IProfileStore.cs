namespace CodeSync.Core;

/// <summary>
///   A store for versioned synchronization profiles.
/// </summary>
public interface IProfileStore
{
    /// <summary>
    ///   Loads a synchronization profile from a path.
    /// </summary>
    /// <param name="path">The file path from which to load the profile.</param>
    /// <returns>The loaded synchronization profile.</returns>
    SyncProfile Load(string path);

    /// <summary>
    ///   Saves a synchronization profile to a path.
    /// </summary>
    /// <param name="path">The file path to which to save the profile.</param>
    /// <param name="profile">The synchronization profile to save.</param>
    void Save(string path, SyncProfile profile);
}
