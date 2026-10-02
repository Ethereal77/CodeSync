namespace CodeSync.Core;

/// <summary>
///   Represents a human-readable conflict report without file metadata.
/// </summary>
/// <remarks>
///   This record represents a conflict report that lists conflicts without including any file metadata.
///   When persisted to a XML file, for example, this would be the <c>[Profile].conflicts.xml</c> file.
/// </remarks>
public sealed record ConflictDocument
{
    /// <summary>
    ///   Gets the absolute source directory.
    /// </summary>
    public string SourceDirectory { get; }

    /// <summary>
    ///   Gets the absolute destination directory.
    /// </summary>
    public string DestinationDirectory { get; }

    /// <summary>
    ///   Gets the conflicts in their persisted presentation order.
    /// </summary>
    public IReadOnlyList<ConflictEntry> Conflicts { get; }


    /// <summary>
    ///   Initializes a conflict document.
    /// </summary>
    public ConflictDocument(string sourceDirectory,
                            string destinationDirectory,
                            IEnumerable<ConflictEntry> conflicts)
    {
        SourceDirectory = ValidateRoot(sourceDirectory, nameof(sourceDirectory));
        DestinationDirectory = ValidateRoot(destinationDirectory, nameof(destinationDirectory));

        Conflicts = conflicts?.ToArray() ?? throw new ArgumentNullException(nameof(conflicts));

        //
        // Validates that a root directory is specified and returns its full path.
        //
        static string ValidateRoot(string root, string parameterName)
        {
            return string.IsNullOrWhiteSpace(root)
                ? throw new ArgumentException("A conflict root directory is required.", parameterName)
                : Path.GetFullPath(root);
        }
    }
}
