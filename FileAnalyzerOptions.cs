/// <summary>
///   Specifies the configuration settings for the <see cref="FileAnalyzer"/>.
/// </summary>
struct FileAnalyzerOptions
{
    /// <summary>
    ///   The directory where the source repository is located.
    /// </summary>
    public required string SourceDirectory { get; init; }
    /// <summary>
    ///   The queue of files in the source repository to analyze, as enumerated by the <see cref="FileEnumerator"/>.
    /// </summary>
    public required Queue<string> SourceFilesQueue { get; init; }

    /// <summary>
    ///   The directory where the destination repository is located.
    /// </summary>
    public required string DestinationDirectory { get; init; }
    /// <summary>
    ///   The map of file names to files in the destination repository to analyze, as enumerated by the <see cref="FileEnumerator"/>.
    /// </summary>
    public required FileMap DestinationFilesMap { get; init; }

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
    ///   Specifies the configuration settings for the <see cref="FileAnalyzer"/>.
    /// </summary>
    public string? OutputXmlFilePath = null;

    public FileAnalyzerOptions() { }
}
