namespace CodeSync.Core;

/// <summary>
///   Evaluates one <c>.gitignore</c> rule set against normalized relative paths.
/// </summary>
public interface IIgnoreMatcher
{
    /// <summary>
    ///   Determines whether the specified file or directory should be ignored based on the configured ignore rules.
    /// </summary>
    /// <param name="relativePath">The relative path of the file or directory to check.</param>
    /// <param name="isDirectory">Indicates whether the path represents a directory.</param>
    /// <returns>
    ///   <see langword="true"/> if the specified file or directory should be ignored;
    ///   otherwise, <see langword="false"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    ///   Thrown if <paramref name="relativePath"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   Thrown if <paramref name="relativePath"/> is not a relative path, or it contains '.' or '..' segments.
    /// </exception>
    bool IsIgnored(string relativePath, bool isDirectory);
}
