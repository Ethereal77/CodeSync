namespace CodeSync;

/// <summary>
///   Specifies the configuration settings for the <see cref="FileSynchronizer"/>.
/// </summary>
struct FileSynchronizerOptions
{
    /// <summary>
    ///   The input XML file describing which files to copy from a source directory to a destination directory.
    /// </summary>
    public required string InputXml { get; init; }

    public FileSynchronizerOptions() { }
}
