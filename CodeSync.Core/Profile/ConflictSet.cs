namespace CodeSync.Core;

/// <summary>
///   Represents a set of conflicts for a specific synchronization profile,
///   including the source and destination directories.
/// </summary>
public sealed record ConflictSet
{
    /// <summary>
    ///   Gets the full path to the source directory for the conflict set.
    /// </summary>
    public string SourceDirectory { get; }

    /// <summary>
    ///   Gets the full path to the destination directory for the conflict set.
    /// </summary>
    public string DestinationDirectory { get; }

    /// <summary>
    ///   Gets the list of conflicts for the conflict set.
    /// </summary>
    public IReadOnlyList<Conflict> Conflicts { get; }


    /// <summary>
    ///   Initializes a new instance of the <see cref="ConflictSet"/> class with.
    /// </summary>
    /// <param name="sourceDirectory">The full path to the source directory for the conflict set.</param>
    /// <param name="destinationDirectory">The full path to the destination directory for the conflict set.</param>
    /// <param name="conflicts">The list of conflicts for the conflict set.</param>
    /// <exception cref="ArgumentException">
    ///   Thrown if the source or destination directory is <see langword="null"/>, empty,
    ///   or consists only of white-space characters.
    /// </exception>
    /// <exception cref="ArgumentNullException">Thrown if the conflicts list is <see langword="null"/>.</exception>
    public ConflictSet(string sourceDirectory, string destinationDirectory, IEnumerable<Conflict> conflicts)
    {
        SourceDirectory = string.IsNullOrWhiteSpace(sourceDirectory)
            ? throw new ArgumentException("A conflict source directory is required.", nameof(sourceDirectory))
            : Path.GetFullPath(sourceDirectory);

        DestinationDirectory = string.IsNullOrWhiteSpace(destinationDirectory)
            ? throw new ArgumentException("A conflict destination directory is required.", nameof(destinationDirectory))
            : Path.GetFullPath(destinationDirectory);

        Conflicts = conflicts?.ToArray() ?? throw new ArgumentNullException(nameof(conflicts));
    }
}
