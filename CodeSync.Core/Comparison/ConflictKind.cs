namespace CodeSync.Core;

/// <summary>
///   Classifies why a file mapping is a <see cref="Conflict"/> and needs human review.
/// </summary>
public enum ConflictKind
{
    /// <summary>
    ///   The source file exists without a corresponding destination file.
    /// </summary>
    SourceWithoutDestination,

    /// <summary>
    ///   The destination file exists without a corresponding source file.
    /// </summary>
    DestinationWithoutSource,

    /// <summary>
    ///   The mapping is ambiguous and cannot be automatically resolved.
    /// </summary>
    AmbiguousMatch,

    /// <summary>
    ///   The mapped file is missing in the destination.
    /// </summary>
    MissingMappedFile,

    /// <summary>
    ///   The mapping is duplicated and needs human review.
    /// </summary>
    DuplicateMapping
}
