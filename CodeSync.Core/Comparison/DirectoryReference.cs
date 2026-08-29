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
}
