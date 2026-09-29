namespace CodeSync.Core;

/// <summary>
///   Stores the file snapshots used to execute and verify a synchronization profile.
/// </summary>
public sealed record ProfileContent
{
    /// <summary>
    ///   Gets the absolute source directory represented by this inventory.
    /// </summary>
    public string SourceDirectory { get; }

    /// <summary>
    ///   Gets the absolute destination directory represented by this inventory.
    /// </summary>
    public string DestinationDirectory { get; }

    /// <summary>
    ///   Gets the UTC creation time of the inventory.
    /// </summary>
    public DateTimeOffset CreatedUtc { get; }

    /// <summary>
    ///   Gets the UTC last-update time of the inventory.
    /// </summary>
    public DateTimeOffset LastUpdatedUtc { get; }

    /// <summary>
    ///   Gets snapshots found in the source directory.
    /// </summary>
    public IReadOnlyList<FileSnapshot> SourceFiles { get; }

    /// <summary>
    ///   Gets snapshots found in the destination directory.
    /// </summary>
    public IReadOnlyList<FileSnapshot> DestinationFiles { get; }


    /// <summary>
    ///   Initializes a content inventory.
    /// </summary>
    public ProfileContent(string sourceDirectory,
                          string destinationDirectory,
                          DateTimeOffset createdUtc,
                          DateTimeOffset lastUpdatedUtc,
                          IEnumerable<FileSnapshot> sourceFiles,
                          IEnumerable<FileSnapshot> destinationFiles)
    {
        SourceDirectory = ValidateRoot(sourceDirectory, nameof(sourceDirectory));
        DestinationDirectory = ValidateRoot(destinationDirectory, nameof(destinationDirectory));
        CreatedUtc = createdUtc;
        LastUpdatedUtc = lastUpdatedUtc;
        SourceFiles = MaterializeUnique(sourceFiles, nameof(sourceFiles));
        DestinationFiles = MaterializeUnique(destinationFiles, nameof(destinationFiles));

        //
        // Validates that a root directory is specified and returns its full path.
        //
        static string ValidateRoot(string root, string parameterName)
        {
            return string.IsNullOrWhiteSpace(root)
                ? throw new ArgumentException("A content root directory is required.", parameterName)
                : Path.GetFullPath(root);
        }

        //
        // Materializes a unique list of file snapshots, ensuring no nulls or duplicates.
        // Throws an exception if the input is invalid.
        //
        static IReadOnlyList<FileSnapshot> MaterializeUnique(IEnumerable<FileSnapshot> files,
                                                             string parameterName)
        {
            ArgumentNullException.ThrowIfNull(files, parameterName);

            var materialized = files.ToArray();
            if (materialized.Any(file => file is null))
                throw new ArgumentException("A content inventory cannot contain a null snapshot.", parameterName);

            if (materialized.Select(file => file.Path).Distinct(StringComparer.Ordinal).Count() != materialized.Length)
                throw new ArgumentException("A content inventory cannot contain duplicate paths.", parameterName);

            return materialized;
        }
    }


    /// <summary>
    ///   Returns the source snapshot for a normalized path, when known.
    /// </summary>
    public FileSnapshot? FindSource(string path)
        => Find(SourceFiles, path);

    /// <summary>
    ///   Returns the destination snapshot for a normalized path, when known.
    /// </summary>
    public FileSnapshot? FindDestination(string path)
        => Find(DestinationFiles, path);

    /// <summary>
    ///   Returns the source snapshot for a normalized path, when known.
    /// </summary>
    /// <param name="files">The collection of file snapshots to search.</param>
    /// <param name="path">The normalized path to look for.</param>
    /// <returns>The matching file snapshot, or <see langword="null"/> if not found.</returns>
    private static FileSnapshot? Find(IEnumerable<FileSnapshot> files, string path)
    {
        var normalizedPath = PathUtils.NormalizeFilePath(path);

        return files.SingleOrDefault(file => file.Path == normalizedPath);
    }
}
