namespace CodeSync;

/// <summary>
///   Specifies the configuration settings for the <see cref="FileUpdater"/>.
/// </summary>
readonly struct FileUpdaterOptions
{
    /// <summary>
    ///   The input CodeSync XML file to update.
    /// </summary>
    public required string InputXml { get; init; }

    /// <summary>
    ///   Indicates whether to compare the contents of files with a hash to verify matches between files
    ///   in the source repository and files in the destination repository.
    /// </summary>
    public bool UseHashMatching { get; init; }= false;

    /// <summary>
    ///   Output path where to write the resulting CodeSync XML file, or <see langword="null"/> if no output XML
    ///   file should be generated.
    /// </summary>
    public string? OutputXmlFilePath { get; init; }= null;

    /// <summary>
    ///   Indicates whether to update the last modified time in the CodeSync XML file.
    /// </summary>
    public bool UpdateLastModifiedTime { get; init; }= true;

    public FileUpdaterOptions() { }
}
