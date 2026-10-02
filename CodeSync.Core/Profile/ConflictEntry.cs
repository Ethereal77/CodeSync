namespace CodeSync.Core;

/// <summary>
///   Describes one conflict using only its optional source and destination paths.
/// </summary>
/// <remarks>
///   This record only contains the paths and kind of the conflict, without any additional metadata.
///   <para/>
///   The difference between this record and the <see cref="Conflict"/> is that this one
///   is the model for the conflict report used in the <see cref="ConflictDocument"/>.
/// </remarks>
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
    /// <param name="kind">The kind of conflict.</param>
    /// <param name="sourcePath">The normalized source path, or <see langword="null"/> when absent.</param>
    /// <param name="destinationPath">The normalized destination path, or <see langword="null"/> when absent.</param>
    /// <exception cref="ArgumentException">
    ///   Thrown if neither a source nor destination path is provided.
    /// </exception>
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
                                 conflict.Mapping.DestinationPath);
    }
}
