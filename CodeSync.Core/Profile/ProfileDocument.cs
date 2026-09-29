namespace CodeSync.Core;

/// <summary>
///   Describes the user-editable paths in a synchronization profile.
/// </summary>
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
                mapping.Destination?.Path)));
    }
}

/// <summary>
///   Describes the source and destination paths in one editable profile mapping.
/// </summary>
public sealed record ProfileMapping
{
    /// <summary>
    ///   Gets the normalized source path, or <see langword="null"/> when absent.
    /// </summary>
    public string? SourcePath { get; }

    /// <summary>
    ///   Gets the normalized destination path, or <see langword="null"/> when absent.
    /// </summary>
    public string? DestinationPath { get; }


    /// <summary>
    ///   Initializes a profile mapping with at least one side.
    /// </summary>
    public ProfileMapping(string? sourcePath, string? destinationPath)
    {
        if (sourcePath is null && destinationPath is null)
            throw new ArgumentException("A profile mapping must contain a source or destination path.");

        SourcePath = sourcePath is null ? null : PathUtils.NormalizeFilePath(sourcePath);
        DestinationPath = destinationPath is null ? null : PathUtils.NormalizeFilePath(destinationPath);
    }
}

/// <summary>
///   Contains an editable profile and its persisted document timestamps.
/// </summary>
public sealed record ProfileDocument
{
    /// <summary>
    ///   Gets the editable profile definition.
    /// </summary>
    public ProfileDefinition Profile { get; }

    /// <summary>
    ///   Gets the UTC creation time.
    /// </summary>
    public DateTimeOffset CreatedUtc { get; }

    /// <summary>
    ///   Gets the UTC last-update time.
    /// </summary>
    public DateTimeOffset LastUpdatedUtc { get; }


    /// <summary>
    ///   Initializes a profile document.
    /// </summary>
    public ProfileDocument(ProfileDefinition profile, DateTimeOffset createdUtc, DateTimeOffset lastUpdatedUtc)
    {
        Profile = profile ?? throw new ArgumentNullException(nameof(profile));

        CreatedUtc = createdUtc;
        LastUpdatedUtc = lastUpdatedUtc;
    }
}
