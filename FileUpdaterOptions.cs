/// <summary>
///   Specifies the configuration settings for the <see cref="FileUpdater"/>.
/// </summary>
struct FileUpdaterOptions
{
    /// <summary>
    ///   The input CodeSync XML file to update.
    /// </summary>
    public required string InputXml { get; init; }

    /// <summary>
    ///   Indicates whether to compare the contents of files with a hash to verify matches between files
    ///   in the source repository and files in the destination repository.
    /// </summary>
    public bool UseHashMatching = false;
    /// <summary>
    ///   Indicates whether to compare the last modified time of files and discard the copying of files in the
    ///   source repository that are older than the corresponding files in the destination repository.
    /// </summary>
    public bool DiscardOldFiles = false;

    /// <summary>
    ///   Output path where to write the resulting CodeSync XML file, or <see langword="null"/> if no output XML
    ///   file should be generated.
    /// </summary>
    public string? OutputXmlFilePath = null;

    public FileUpdaterOptions() { }
}
