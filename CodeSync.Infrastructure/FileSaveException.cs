namespace CodeSync.Infrastructure;

/// <summary>
///   Represents an error while saving a persisted file.
/// </summary>
public sealed class FileSaveException : IOException
{
    /// <summary>
    ///   Gets the path of the file that could not be saved.
    /// </summary>
    public string Path { get; }


    /// <summary>
    ///   Initializes an exception for a failed file save.
    /// </summary>
    /// <param name="path">The path of the file that could not be saved.</param>
    /// <param name="innerException">The exception that caused the save to fail.</param>
    public FileSaveException(string path, Exception innerException)
        : base($"Could not save file '{path}'.", innerException)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(innerException);

        Path = path;
    }
}