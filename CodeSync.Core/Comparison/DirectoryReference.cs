namespace CodeSync.Core;

/// <summary>
///   Describes a source directory and its corresponding destination directory.
/// </summary>
public sealed record DirectoryReference
{
    /// <summary>
    ///   Gets the path of the source directory.
    /// </summary>
    public string SourcePath { get; }

    /// <summary>
    ///   Gets the path of the destination directory.
    /// </summary>
    public string DestinationPath { get; }


    /// <summary>
    ///   Initializes a new instance of the <see cref="DirectoryReference"/> class.
    /// </summary>
    /// <param name="sourcePath">The path of the source directory.</param>
    /// <param name="destinationPath">The path of the destination directory.</param>
    /// <exception cref="ArgumentNullException">
    ///   Thrown if either <paramref name="sourcePath"/> or <paramref name="destinationPath"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   Thrown if either <paramref name="sourcePath"/> or <paramref name="destinationPath"/> is an invalid path,
    ///   not relative to their respective root directories, or contains <c>"."</c> or <c>".."</c> segments.
    /// </exception>
    public DirectoryReference(string sourcePath, string destinationPath)
    {
        SourcePath = PathUtils.NormalizeDirectoryPath(sourcePath);
        DestinationPath = PathUtils.NormalizeDirectoryPath(destinationPath);
    }

    /// <summary>
    ///   Tries to map a source file path through the most specific applicable directory reference.
    /// </summary>
    /// <param name="sourcePath">The normalized source file path.</param>
    /// <param name="references">The directory references to inspect.</param>
    /// <returns>The mapped destination path, or <see langword="null"/> when no unique mapping applies.</returns>
    public static string? TryMapSourcePath(string sourcePath, IEnumerable<DirectoryReference> references)
    {
        var normalizedSourcePath = PathUtils.NormalizeFilePath(sourcePath);
        ArgumentNullException.ThrowIfNull(references);

        var candidates = references
            .Where(reference => PathUtils.IsPathUnder(normalizedSourcePath, reference.SourcePath))
            .GroupBy(reference => PathUtils.GetPathDepth(reference.SourcePath))
            .OrderByDescending(group => group.Key)
            .FirstOrDefault();

        if (candidates is null)
            return null;

        var destinationPaths = candidates
            .Select(reference => reference.DestinationPath)
            .Distinct(PathUtils.PathComparer)
            .ToArray();

        if (destinationPaths.Length != 1)
            return null;

        var reference = candidates.First();
        var suffix = PathUtils.GetRelativeSuffix(normalizedSourcePath, reference.SourcePath);

        return reference.DestinationPath.Length == 0
            ? suffix
            : $"{reference.DestinationPath}/{suffix}";
    }
}
