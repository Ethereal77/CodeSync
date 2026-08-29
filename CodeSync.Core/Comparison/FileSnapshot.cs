namespace CodeSync.Core;

/// <summary>
///   Records the state of one file at a point in time.
/// </summary>
public sealed record FileSnapshot
{
    /// <summary>
    ///   Gets the normalized file path of the snapshot.
    /// </summary>
    public string Path { get; }

    /// <summary>
    ///   Gets the size of the file in bytes.
    /// </summary>
    public long Size { get; }

    /// <summary>
    ///   Gets the last write time of the file in UTC.
    /// </summary>
    public DateTime LastWriteTimeUtc { get; }

    /// <summary>
    ///   Gets the SHA-256 hash of the file.
    /// </summary>
    public string Sha256 { get; }


    /// <summary>
    ///   Initializes a new instance of the <see cref="FileSnapshot"/> class.
    /// </summary>
    /// <param name="path">The file path.</param>
    /// <param name="size">The size of the file in bytes.</param>
    /// <param name="lastWriteTimeUtc">The last write time of the file in UTC.</param>
    /// <param name="sha256">The SHA-256 hash of the file.</param>
    /// <exception cref="ArgumentException">
    ///   Thrown when the <paramref name="path"/> is <see langword="null"/> or whitespace,
    ///   the <paramref name="sha256"/> hash is invalid,
    ///   or the <paramref name="lastWriteTimeUtc"/> is not UTC.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the <paramref name="size"/> is negative.</exception>
    public FileSnapshot(string path, long size, DateTime lastWriteTimeUtc, string sha256)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentOutOfRangeException.ThrowIfNegative(size);
        ArgumentException.ThrowIfNullOrWhiteSpace(sha256);

        Path = PathUtils.NormalizeFilePath(path);

        Size = size;

        LastWriteTimeUtc = lastWriteTimeUtc.Kind == DateTimeKind.Utc
            ? lastWriteTimeUtc
            : throw new ArgumentException("The file time must be UTC.", nameof(lastWriteTimeUtc));

        if (sha256.Length != 64 || sha256.Any(character => !Uri.IsHexDigit(character)))
            throw new ArgumentException("A SHA-256 hash must contain 64 hexadecimal characters.", nameof(sha256));

        Sha256 = sha256.ToLowerInvariant();
    }
}
