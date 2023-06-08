namespace CodeSync;

/// <summary>
///   Specifies the configuration settings for the <see cref="FileSynchronizer"/>.
/// </summary>
readonly struct FileSynchronizerOptions
{
    /// <summary>
    ///   The input XML file describing which files to copy from a source directory to a destination directory.
    /// </summary>
    public required string InputXml { get; init; }

    /// <summary>
    ///   Indicates whether to compare the last modified time of files and discard the copying of files in the
    ///   source repository that are older than the last modified time of the XML file.
    /// </summary>
    public bool DoNotCopyFilesOlderThanTheXml { get; init; } = false;
    /// <summary>
    ///   Indicates whether to compare the last modified time of files and discard the copying of files in the
    ///   source repository that are older than the corresponding files in the destination repository.
    /// </summary>
    public bool DoNotCopyFilesOlderThanTheDestination { get; init; } = false;

    /// <summary>
    ///   Indicates whether to pretend to copy the files while not making any real file operation.
    /// </summary>
    public bool DryRun { get; init; } = false;

    public FileSynchronizerOptions() { }
}
