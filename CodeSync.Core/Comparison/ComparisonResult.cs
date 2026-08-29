namespace CodeSync.Core;

/// <summary>
///   Result of comparing two already-filtered workspace inventories.
/// </summary>
public sealed record ComparisonResult
{
    /// <summary>
    ///   Gets the list of file mappings.
    /// </summary>
    public IReadOnlyList<FileMapping> FileMappings { get; }

    /// <summary>
    ///   Gets the list of directory references.
    /// </summary>
    public IReadOnlyList<DirectoryReference> DirectoryReferences { get; }

    /// <summary>
    ///   Gets the list of conflicts.
    /// </summary>
    public IReadOnlyList<Conflict> Conflicts { get; }


    /// <summary>
    ///   Initializes a new instance of the <see cref="ComparisonResult"/> class.
    /// </summary>
    /// <param name="fileMappings">The list of file mappings.</param>
    /// <param name="directoryReferences">The list of directory references.</param>
    /// <param name="conflicts">The list of conflicts.</param>
    /// <exception cref="ArgumentNullException">
    ///   Thrown if any of the parameters are <see langword="null"/>.
    /// </exception>
    public ComparisonResult(IEnumerable<FileMapping> fileMappings,
                            IEnumerable<DirectoryReference> directoryReferences,
                            IEnumerable<Conflict> conflicts)
    {
        FileMappings = fileMappings?.ToArray()
            ?? throw new ArgumentNullException(nameof(fileMappings));
        DirectoryReferences = directoryReferences?.ToArray()
            ?? throw new ArgumentNullException(nameof(directoryReferences));
        Conflicts = conflicts?.ToArray()
            ?? throw new ArgumentNullException(nameof(conflicts));
    }
}
