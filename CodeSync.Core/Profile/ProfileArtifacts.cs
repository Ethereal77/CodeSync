namespace CodeSync.Core;

/// <summary>
///   Helper class for determining the sidecar paths associated with a profile path.
/// </summary>
public static class ProfileArtifacts
{
    /// <summary>
    ///   Returns the full path to the conflicts sidecar file associated with the specified profile path.
    /// </summary>
    /// <param name="profilePath">The full path to the profile file.</param>
    /// <returns>The full path to the conflicts sidecar file associated with the specified profile path.</returns>
    public static string GetConflictsPath(string profilePath)
        => WithSuffix(profilePath, ".conflicts.xml");

    /// <summary>
    ///   Returns the full path to the skipped sidecar file associated with the specified profile path.
    /// </summary>
    /// <param name="profilePath">The full path to the profile file.</param>
    /// <returns>The full path to the skipped sidecar file associated with the specified profile path.</returns>
    public static string GetSkippedPath(string profilePath)
        => WithSuffix(profilePath, ".skipped.xml");


    /// <summary>
    ///   Returns the full path to the sidecar file with the specified suffix.
    /// </summary>
    /// <param name="profilePath">The full path to the profile file.</param>
    /// <param name="suffix">The suffix to append to the profile file name for the sidecar file.</param>
    /// <returns>The full path to the sidecar file with the specified suffix.</returns>
    private static string WithSuffix(string profilePath, string suffix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profilePath);

        var fullPath = Path.GetFullPath(profilePath);
        var directory = Path.GetDirectoryName(fullPath) ?? string.Empty;
        var fileName = Path.GetFileNameWithoutExtension(fullPath);

        return Path.Combine(directory, fileName + suffix);
    }
}
