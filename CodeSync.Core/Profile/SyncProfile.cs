namespace CodeSync.Core;

/// <summary>
///   Represents the synchronization profile containing source and destination directories, directory references,
///   and file mappings used by CodeSync to synchronize files between the source and destination directories.
/// </summary>
public sealed record SyncProfile
{
    /// <summary>
    ///   Gets the absolute path to the source directory.
    /// </summary>
    public string SourceDirectory { get; }

    /// <summary>
    ///   Gets the absolute path to the destination directory.
    /// </summary>
    public string DestinationDirectory { get; }

    /// <summary>
    ///   Gets the list of directory references included in the profile.
    /// </summary>
    public IReadOnlyList<DirectoryReference> DirectoryReferences { get; }

    /// <summary>
    ///   Gets the list of file mappings included in the profile.
    /// </summary>
    public IReadOnlyList<FileMapping> FileMappings { get; }


    /// <summary>
    ///   Initializes a new instance of the <see cref="SyncProfile"/> class.
    /// </summary>
    /// <param name="sourceDirectory">The absolute path to the source directory.</param>
    /// <param name="destinationDirectory">The absolute path to the destination directory.</param>
    /// <param name="directoryReferences">The list of directory references included in the profile.</param>
    /// <param name="fileMappings">The list of file mappings included in the profile.</param>
    /// <exception cref="ArgumentNullException">
    ///   Thrown if any of the constructor parameters are <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   Thrown if either the source or destination directory is not a valid absolute path.
    /// </exception>
    public SyncProfile(string sourceDirectory,
                       string destinationDirectory,
                       IEnumerable<DirectoryReference> directoryReferences,
                       IEnumerable<FileMapping> fileMappings)
    {
        SourceDirectory = ValidateRoot(sourceDirectory, nameof(sourceDirectory));
        DestinationDirectory = ValidateRoot(destinationDirectory, nameof(destinationDirectory));

        DirectoryReferences = directoryReferences?.ToArray()
            ?? throw new ArgumentNullException(nameof(directoryReferences));
        FileMappings = fileMappings?.ToArray()
            ?? throw new ArgumentNullException(nameof(fileMappings));

        //
        // Ensure that the source and destination directories are absolute paths.
        //
        static string ValidateRoot(string root, string parameterName)
        {
            return string.IsNullOrWhiteSpace(root)
                ? throw new ArgumentException("A profile root directory is required.", parameterName)
                : Path.GetFullPath(root);
        }
    }
}
