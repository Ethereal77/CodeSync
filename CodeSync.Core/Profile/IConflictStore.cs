namespace CodeSync.Core;

/// <summary>
///   A store that keeps track of conflicts associated with a synchronization profile.
/// </summary>
public interface IConflictStore
{
    /// <summary>
    ///   Loads the conflicts report for a synchronization profile from a path.
    /// </summary>
    /// <param name="path">The file path from which to load the conflicts report.</param>
    /// <returns>The set of conflicts, or null if no conflicts were found.</returns>
    ConflictSet? Load(string path);

    /// <summary>
    ///   Saves the conflicts report for a synchronization profile to a path.
    /// </summary>
    /// <param name="path">The file path to which to save the conflicts report.</param>
    /// <param name="conflicts">The set of conflicts to save.</param>
    void Save(string path, ConflictSet conflicts);
}
