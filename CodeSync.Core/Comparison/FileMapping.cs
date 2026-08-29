namespace CodeSync.Core;

/// <summary>
///   Associates an optional source file with an optional destination file.
/// </summary>
public sealed record FileMapping
{
    /// <summary>
    ///   Gets the source file snapshot, or <see langword="null"/> if there is no source file.
    /// </summary>
    public FileSnapshot? Source { get; init; }

    /// <summary>
    ///   Gets the destination file snapshot, or <see langword="null"/> if there is no destination file.
    /// </summary>
    public FileSnapshot? Destination { get; init; }


    /// <summary>
    ///   Initializes a new instance of the <see cref="FileMapping"/> class.
    /// </summary>
    /// <param name="source">The source file snapshot, or <see langword="null"/> if there is no source file.</param>
    /// <param name="destination">The destination file snapshot, or <see langword="null"/> if there is no destination file.</param>
    /// <exception cref="ArgumentException">
    ///   Thrown if both the <paramref name="source"/> and <paramref name="destination"/> files are <see langword="null"/>.
    /// </exception>
    public FileMapping(FileSnapshot? source, FileSnapshot? destination)
    {
        if (source is null && destination is null)
            throw new ArgumentException("A mapping must contain a source or destination file.");

        Source = source;
        Destination = destination;
    }
}
