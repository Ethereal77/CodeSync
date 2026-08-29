namespace CodeSync.Core;

/// <summary>
///   A store that keeps track of skipped files, i.e.,
///   files that were not copied because their source was unchanged.
/// </summary>
public interface ISkippedStore
{
    /// <summary>
    ///   Loads the skipped files report for a synchronization profile from a path.
    /// </summary>
    /// <param name="path">The file path from which to load the skipped files report.</param>
    /// <returns>The list of source paths that were skipped.</returns>
    IReadOnlyList<string> Load(string path);

    /// <summary>
    ///   Saves the skipped files report for a synchronization profile to a path.
    /// </summary>
    /// <param name="path">The file path to which to save the skipped files report.</param>
    /// <param name="sourcePaths">The list of source paths that were skipped.</param>
    void Save(string path, IEnumerable<string> sourcePaths);
}
