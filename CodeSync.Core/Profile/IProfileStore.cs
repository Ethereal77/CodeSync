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

    /// <summary>
    ///   Creates a synchronization profile and a complete content inventory from a comparison scan.
    /// </summary>
    /// <param name="path">The path to the profile file.</param>
    /// <param name="profile">The synchronization profile to save.</param>
    /// <param name="sourceFiles">The source file snapshots.</param>
    /// <param name="destinationFiles">The destination file snapshots.</param>
    public void SaveNew(string path,
                        SyncProfile profile,
                        IEnumerable<FileSnapshot> sourceFiles,
                        IEnumerable<FileSnapshot> destinationFiles);

    /// <summary>
    ///   Refreshes the content inventory with the latest scan while preserving historical snapshots.
    /// </summary>
    /// <param name="path">The path to the profile file.</param>
    /// <param name="sourceFiles">The source file snapshots.</param>
    /// <param name="destinationFiles">The destination file snapshots.</param>
    public void RefreshContent(string path,
                               IEnumerable<FileSnapshot> sourceFiles,
                               IEnumerable<FileSnapshot> destinationFiles);
}
