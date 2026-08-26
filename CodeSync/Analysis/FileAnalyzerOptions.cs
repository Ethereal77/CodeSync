namespace CodeSync;

using CodeSync.Utils;

/// <summary>
///   Specifies the configuration settings for the <see cref="FileAnalyzer"/>.
/// </summary>
readonly struct FileAnalyzerOptions
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
    ///   An optional set of file paths already known from a prior <see cref="FileAnalyzer"/> run.
    /// </summary>
    public IEnumerable<string>? DestinationFilesAlreadyKnown { get; init; }

    /// <summary>
    ///   Indicates whether to compare the contents of files with a hash to verify matches between files
    ///   in the source repository and files in the destination repository.
    /// </summary>
    public bool UseHashMatching { get; init; } = false;

    /// <summary>
    ///   Output path where to write the resulting CodeSync XML file, or <see langword="null"/> if no output XML
    ///   file should be generated.
    /// </summary>
    public string? OutputXmlFilePath { get; init; } = null;

    public FileAnalyzerOptions() { }
}
