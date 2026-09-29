namespace CodeSync.Core;

/// <summary>
///   Represents a human-readable conflict report without file metadata.
/// </summary>
public sealed record ConflictDocument
{
    /// <summary>
    ///   Gets the absolute source directory.
    /// </summary>
    public string SourceDirectory { get; }

    /// <summary>
    ///   Gets the absolute destination directory.
    /// </summary>
    public string DestinationDirectory { get; }

    /// <summary>
    ///   Gets the conflicts in their persisted presentation order.
    /// </summary>
    public IReadOnlyList<ConflictEntry> Conflicts { get; }


    /// <summary>
    ///   Initializes a conflict document.
    /// </summary>
    public ConflictDocument(string sourceDirectory,
                            string destinationDirectory,
                            IEnumerable<ConflictEntry> conflicts)
    {
        SourceDirectory = ValidateRoot(sourceDirectory, nameof(sourceDirectory));
        DestinationDirectory = ValidateRoot(destinationDirectory, nameof(destinationDirectory));

        Conflicts = conflicts?.ToArray() ?? throw new ArgumentNullException(nameof(conflicts));

        //
        // Validates that a root directory is specified and returns its full path.
        //
        static string ValidateRoot(string root, string parameterName)
        {
            return string.IsNullOrWhiteSpace(root)
                ? throw new ArgumentException("A conflict root directory is required.", parameterName)
                : Path.GetFullPath(root);
        }
    }
}

/// <summary>
///   Describes one conflict using only its optional source and destination paths.
/// </summary>
public sealed record ConflictEntry
{
    /// <summary>
    ///   Gets the conflict kind.
    /// </summary>
    public ConflictKind Kind { get; }

    /// <summary>
    ///   Gets the normalized source path, or <see langword="null"/> when absent.
    /// </summary>
    public string? SourcePath { get; }

    /// <summary>
    ///   Gets the normalized destination path, or <see langword="null"/> when absent.
    /// </summary>
    public string? DestinationPath { get; }


    /// <summary>
    ///   Initializes a conflict entry with at least one path.
    /// </summary>
    public ConflictEntry(ConflictKind kind, string? sourcePath, string? destinationPath)
    {
        if (sourcePath is null && destinationPath is null)
            throw new ArgumentException("A conflict must contain a source or destination path.");

        Kind = kind;
        SourcePath = sourcePath is null ? null : PathUtils.NormalizeFilePath(sourcePath);
        DestinationPath = destinationPath is null ? null : PathUtils.NormalizeFilePath(destinationPath);
    }

    /// <summary>
    ///   Creates a path-only entry from a domain conflict.
    /// </summary>
    public static ConflictEntry FromConflict(Conflict conflict)
    {
        ArgumentNullException.ThrowIfNull(conflict);

        return new ConflictEntry(conflict.Kind,
                                 conflict.Mapping.Source?.Path,
                                 conflict.Mapping.Destination?.Path);
    }
}
