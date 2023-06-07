namespace CodeSync;

/// <summary>
///   Specifies the configuration settings for the <see cref="FileVerifier"/>.
/// </summary>
struct FileVerifierOptions
{
    /// <summary>
    ///   The input CodeSync XML file to verify.
    /// </summary>
    public required string InputXml { get; init; }

    /// <summary>
    ///   Indicates whether to check if the Copy or Ignore entries are repeated.
    /// </summary>
    public bool DiscardRepeatedEntries = true;
    /// <summary>
    ///   Indicates whether to check if the referenced files do still exist.
    /// </summary>
    public CheckExistingEntryOption DiscardMissingFiles = CheckExistingEntryOption.None;

    /// <summary>
    ///   Output path where to write the verified and reorganized CodeSync XML file,
    ///   or <see langword="null"/> if no output XML file should be generated.
    /// </summary>
    public string? OutputXmlFilePath = null;


    public FileVerifierOptions() { }
}

/// <summary>
///   Determines what file entries to verify for the Verify command when looking for files
///   that no longer exist.
/// </summary>
[Flags]
enum CheckExistingEntryOption
{
    None = 0,

    CheckCopyEntries,
    CheckIgnoreEntries,

    CheckAll = CheckIgnoreEntries | CheckCopyEntries
}
