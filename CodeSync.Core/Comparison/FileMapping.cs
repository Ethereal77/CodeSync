namespace CodeSync.Core;

/// <summary>
///   Associates an optional source file with an optional destination file.
/// </summary>
public sealed record FileMapping
{
    /// <summary>
    ///   Gets a value indicating whether this mapping is explicitly ignored.
    /// </summary>
    public bool IsIgnored { get; }

    /// <summary>
    ///   Gets the source file snapshot, or <see langword="null"/> if there is no source file.
    /// </summary>
    public FileSnapshot? Source { get; init; }

    /// <summary>
    ///   Gets the destination file snapshot, or <see langword="null"/> if there is no destination file.
    /// </summary>
    public FileSnapshot? Destination { get; init; }

    /// <summary>
    ///   Gets the destination path, including a suggested path without a destination snapshot.
    /// </summary>
    public string? DestinationPath => Destination?.Path ?? field;


    /// <summary>
    ///   Initializes a new instance of the <see cref="FileMapping"/> class.
    /// </summary>
    /// <param name="source">The source file snapshot, or <see langword="null"/> if there is no source file.</param>
    /// <param name="destination">The destination file snapshot, or <see langword="null"/> if there is no destination file.</param>
    /// <param name="isIgnored">Indicates whether the mapping is an explicit ignore decision.</param>
    /// <param name="destinationPath">An optional destination path suggestion without a snapshot.</param>
    /// <exception cref="ArgumentException">
    ///   Thrown if neither a source, destination, nor destination path is provided,
    ///   or if a destination path disagrees with the destination snapshot.
    /// </exception>
    public FileMapping(FileSnapshot? source,
                       FileSnapshot? destination,
                       string? destinationPath = null,
                       bool isIgnored = false)
    {
        if (source is null && destination is null && destinationPath is null)
            throw new ArgumentException("A mapping must contain a source or destination file.");

        var normalizedDestinationPath = destinationPath is null
            ? null
            : PathUtils.NormalizeFilePath(destinationPath);

        if (destination is not null && normalizedDestinationPath is not null &&
            !string.Equals(destination.Path, normalizedDestinationPath, StringComparison.Ordinal))
        {
            throw new ArgumentException("The destination snapshot and path must identify the same file.", nameof(destinationPath));
        }

        IsIgnored = isIgnored;
        Source = source;
        Destination = destination;
        DestinationPath = normalizedDestinationPath;
    }
}
