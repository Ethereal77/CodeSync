namespace CodeSync.Core;

/// <summary>
///   Describes the source and destination paths in one editable profile mapping.
/// </summary>
/// <remarks>
///   This record represents a single mapping between a source and destination path
///   in an editable synchronization profile.
///   It may also indicate whether the mapping is explicitly ignored.
///   <para/>
///   The difference between this record and <see cref="FileMapping"/> is that this one
///   is the model used for persisting profile mappings.
/// </remarks>
public sealed record ProfileMapping
{
    /// <summary>
    ///   Gets a value indicating whether this mapping is an explicit ignore decision.
    /// </summary>
    public bool IsIgnored { get; }

    /// <summary>
    ///   Gets the normalized source path, or <see langword="null"/> when absent.
    /// </summary>
    public string? SourcePath { get; }

    /// <summary>
    ///   Gets the normalized destination path, or <see langword="null"/> when absent.
    /// </summary>
    public string? DestinationPath { get; }


    /// <summary>
    ///   Initializes a profile mapping with at least one side.
    /// </summary>
    /// <param name="sourcePath">The source path, or <see langword="null"/> if absent.</param>
    /// <param name="destinationPath">The destination path, or <see langword="null"/> if absent.</param>
    /// <param name="isIgnored">Indicates whether this mapping is an explicit ignore decision.</param>
    /// <exception cref="ArgumentException">
    ///   Thrown when both source and destination paths are absent, or when a non-ignored mapping lacks either path.
    /// </exception>
    public ProfileMapping(string? sourcePath, string? destinationPath, bool isIgnored = false)
    {
        if (sourcePath is null && destinationPath is null)
            throw new ArgumentException("A profile mapping must contain a source or destination path.");

        if (!isIgnored && (sourcePath is null || destinationPath is null))
            throw new ArgumentException("A non-ignored profile mapping must contain both source and destination paths.");

        IsIgnored = isIgnored;
        SourcePath = sourcePath is null ? null : PathUtils.NormalizeFilePath(sourcePath);
        DestinationPath = destinationPath is null ? null : PathUtils.NormalizeFilePath(destinationPath);
    }
}
