namespace CodeSync.Core;

/// <summary>
///   Describes the correspondence of source and destination paths in a synchronization profile.
/// </summary>
/// <remarks>
///   This record represents the user-editable definition of a synchronization profile,
///   including the root directories, known directory references, and file mappings.
///   <para/>
///   It will later be used to execute and verify a synchronization profile.
/// </remarks>
public sealed record ProfileDefinition
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
    ///   Gets the known directory references.
    /// </summary>
    public IReadOnlyList<DirectoryReference> DirectoryReferences { get; }

    /// <summary>
    ///   Gets the user-editable file mappings.
    /// </summary>
    public IReadOnlyList<ProfileMapping> FileMappings { get; }


    /// <summary>
    ///   Initializes a profile definition.
    /// </summary>
    public ProfileDefinition(string sourceDirectory,
                             string destinationDirectory,
                             IEnumerable<DirectoryReference> directoryReferences,
                             IEnumerable<ProfileMapping> fileMappings)
    {
        SourceDirectory = ValidateRoot(sourceDirectory, nameof(sourceDirectory));
        DestinationDirectory = ValidateRoot(destinationDirectory, nameof(destinationDirectory));

        DirectoryReferences = directoryReferences?.ToArray()
            ?? throw new ArgumentNullException(nameof(directoryReferences));

        FileMappings = fileMappings?.ToArray()
            ?? throw new ArgumentNullException(nameof(fileMappings));

        //
        // Validates that the root directories are not null or whitespace and are converted to full paths.
        //
        static string ValidateRoot(string root, string parameterName)
        {
            return string.IsNullOrWhiteSpace(root)
                ? throw new ArgumentException("A profile root directory is required.", parameterName)
                : Path.GetFullPath(root);
        }
    }

    /// <summary>
    ///   Creates a path-only definition from a hydrated synchronization profile.
    /// </summary>
    public static ProfileDefinition FromProfile(SyncProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        return new ProfileDefinition(
            profile.SourceDirectory,
            profile.DestinationDirectory,
            profile.DirectoryReferences,
            profile.FileMappings.Select(mapping => new ProfileMapping(
                mapping.Source?.Path,
                mapping.DestinationPath,
                mapping.IsIgnored)));
    }
}
