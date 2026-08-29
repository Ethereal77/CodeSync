namespace CodeSync.Core;

/// <summary>
///   Provides canonical validation and formatting for profile-relative paths.
/// </summary>
public static class PathUtils
{
    /// <summary>
    ///   Normalizes a profile-relative file path, rejecting paths that can escape its profile root.
    /// </summary>
    /// <param name="path">The file path to normalize.</param>
    /// <returns>The normalized <paramref name="path"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    ///   <paramref name="path"/> is invalid, i.e., it is rooted, or it contains '.' or '..' segments.
    /// </exception>
    public static string NormalizeFilePath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (Path.IsPathRooted(path))
            throw new ArgumentException("A relative path must not be rooted.", nameof(path));

        var candidate = path.Replace('\\', '/');
        var parts = candidate.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 0 || parts.Any(part => part is "." or ".."))
            throw new ArgumentException("A relative path must not contain '.' or '..' segments.", nameof(path));

        return string.Join('/', parts);
    }

    /// <summary>
    ///   Normalizes a profile-relative directory path.
    /// </summary>
    /// <param name="path">
    ///   The directory path to normalize; an empty path represents the root directory.
    /// </param>
    /// <returns>The normalized <paramref name="path"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    ///   <paramref name="path"/> is invalid, i.e., it is rooted, or it contains '.' or '..' segments.
    /// </exception>
    public static string NormalizeDirectoryPath(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        if (path.Length == 0)
            return string.Empty;

        if (path is "." or "./" or @".\")
            return string.Empty;

        return NormalizeFilePath(path);
    }

    /// <summary>
    ///   Enumerates all ancestor directories of the specified path, starting from the root.
    /// </summary>
    /// <param name="path">The path for which to enumerate ancestor directories.</param>
    /// <returns>An enumerable of ancestor directory paths, starting from the root.</returns>
    public static IEnumerable<string> EnumerateAncestors(string path)
    {
        var parts = path.Split('/');

        for (var index = 0; index < parts.Length; index++)
            yield return string.Join('/', parts.Take(index));
    }

    /// <summary>
    ///   Calculates the depth of the specified path, defined as the number of directory levels it contains.
    /// </summary>
    /// <param name="path">The path whose depth is to be calculated.</param>
    /// <returns>
    ///   The depth of the specified path, defined as the number of directory levels it contains.
    /// </returns>
    public static int GetPathDepth(string path)
    {
        return path.Length == 0 ? 0 : path.Count(ch => ch == '/') + 1;
    }

    /// <summary>
    ///   Determines whether the specified path is under the given directory.
    /// </summary>
    /// <param name="path">The path of the file or directory to check.</param>
    /// <param name="directory">The directory to check against.</param>
    /// <returns>
    ///   <see langword="true"/> if the specified path is under the given directory;
    ///   otherwise, <see langword="false"/>.
    /// </returns>
    public static bool IsPathUnder(string path, string directory)
    {
        return directory.Length == 0
            || path == directory
            || path.StartsWith(directory + "/", StringComparison.Ordinal);
    }

    /// <summary>
    ///   Gets the relative path of the specified file or directory with respect to the given directory
    ///   (for example, if the path is "src/file.txt" and the directory is "src", the relative path would be "file.txt").
    /// </summary>
    /// <param name="path">The path of the file or directory.</param>
    /// <param name="directory">The base directory to which the relative path is calculated.</param>
    /// <returns>The relative path of the specified file or directory with respect to the given directory.</returns>
    public static string GetRelativeSuffix(string path, string directory)
    {
        return directory.Length == 0
            ? path
            : path[(directory.Length + 1)..];
    }
}
