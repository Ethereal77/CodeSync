using System.Text;

using CodeSync.Core;

namespace CodeSync.Infrastructure;

/// <summary>
///   A store for versioned synchronization profiles that persists them using XML.
/// </summary>
public sealed class XmlProfileStore : IProfileStore
{
    private readonly TimeProvider _timeProvider;


    /// <summary>
    ///   Initializes a profile store using the current UTC clock.
    /// </summary>
    /// <param name="utcNow">Optional UTC clock used when writing document timestamps.</param>
    public XmlProfileStore(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }


    /// <inheritdoc/>
    public SyncProfile Load(string path)
    {
        var document = LoadProfileDocument(path);
        var content = LoadContent(path);

        ValidateRoots(document.Profile, content);

        var mappings = document.Profile.FileMappings.Select(CreateFileMapping);

        return new SyncProfile(document.Profile.SourceDirectory,
                               document.Profile.DestinationDirectory,
                               document.Profile.DirectoryReferences,
                               mappings);

        //
        // Maps profile mappings to file mappings using the resolved content.
        //
        FileMapping CreateFileMapping(ProfileMapping mapping)
        {
            var source = mapping.SourcePath is null
                ? null
                : mapping.IsIgnored
                    ? content.FindSource(mapping.SourcePath)
                    : ResolveSource(content, mapping.SourcePath);

            var destination = mapping.DestinationPath is null
                ? null
                : content.FindDestination(mapping.DestinationPath);

            return new FileMapping(
                source,
                destination,
                mapping.DestinationPath,
                mapping.IsIgnored);
        }

        //
        // Resolves a source file snapshot from the content inventory.
        // Throws an exception if the file is not found.
        //
        static FileSnapshot ResolveSource(ProfileContent content, string path)
        {
            return content.FindSource(path)
                ?? throw new InvalidDataException($"The source path '{path}' is missing from the content inventory.");
        }
    }

    /// <inheritdoc/>
    public void Save(string path, SyncProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var document = LoadProfileDocument(path);
        var content = LoadContent(path);

        var now = _timeProvider.GetUtcNow();

        var mergedContent = MergeContent(content, profile, document.CreatedUtc, now);

        SaveDocuments(path, ProfileDefinition.FromProfile(profile), mergedContent, document.CreatedUtc, now);
    }

    /// <summary>
    ///   Creates a profile and a complete content inventory from a comparison scan.
    /// </summary>
    /// <param name="path">The path to the profile file.</param>
    /// <param name="profile">The synchronization profile to save.</param>
    /// <param name="sourceFiles">The source file snapshots.</param>
    /// <param name="destinationFiles">The destination file snapshots.</param>
    public void SaveNew(string path,
                        SyncProfile profile,
                        IEnumerable<FileSnapshot> sourceFiles,
                        IEnumerable<FileSnapshot> destinationFiles)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(sourceFiles);
        ArgumentNullException.ThrowIfNull(destinationFiles);

        var now = _timeProvider.GetUtcNow();

        var content = new ProfileContent(profile.SourceDirectory,
                                         profile.DestinationDirectory,
                                         now,
                                         now,
                                         sourceFiles,
                                         destinationFiles);

        SaveDocuments(path, ProfileDefinition.FromProfile(profile), content, now, now);
    }

    /// <summary>
    ///   Refreshes the content inventory with the latest scan while preserving historical snapshots.
    /// </summary>
    /// <param name="path">The path to the profile file.</param>
    /// <param name="sourceFiles">The source file snapshots.</param>
    /// <param name="destinationFiles">The destination file snapshots.</param>
    public void RefreshContent(string path,
                               IEnumerable<FileSnapshot> sourceFiles,
                               IEnumerable<FileSnapshot> destinationFiles)
    {
        ArgumentNullException.ThrowIfNull(sourceFiles);
        ArgumentNullException.ThrowIfNull(destinationFiles);

        var document = LoadProfileDocument(path);
        var content = LoadContent(path);

        var now = _timeProvider.GetUtcNow();

        var refreshedContent = new ProfileContent(
            content.SourceDirectory,
            content.DestinationDirectory,
            document.CreatedUtc,
            now,
            MergeSnapshots(content.SourceFiles, sourceFiles),
            MergeSnapshots(content.DestinationFiles, destinationFiles));

        SaveDocuments(path, document.Profile, refreshedContent, document.CreatedUtc, now);

        //
        // Merges the existing and current file snapshots, giving precedence to the current snapshots.
        //
        static IReadOnlyList<FileSnapshot> MergeSnapshots(IEnumerable<FileSnapshot> existing,
                                                          IEnumerable<FileSnapshot> current)
        {
            var snapshots = existing.ToDictionary(file => file.Path, StringComparer.Ordinal);

            foreach (var snapshot in current)
                snapshots[snapshot.Path] = snapshot;

            return snapshots.Values.ToArray();
        }
    }


    /// <summary>
    ///   Loads the profile document from the specified path.
    /// </summary>
    /// <param name="path">The path to the profile file.</param>
    /// <returns>The loaded profile document.</returns>
    private static ProfileDocument LoadProfileDocument(string path)
    {
        var xml = File.ReadAllText(path, Encoding.UTF8);

        return XmlCodecs.DeserializeProfile(xml);
    }

    /// <summary>
    ///   Loads the profile content from the specified path.
    /// </summary>
    /// <param name="path">The path to the profile file.</param>
    /// <returns>The loaded profile content.</returns>
    private static ProfileContent LoadContent(string path)
    {
        var contentPath = ProfileArtifacts.GetContentPath(path);

        if (!File.Exists(contentPath))
            throw new InvalidDataException($"The profile content inventory is missing: {contentPath}");

        return XmlCodecs.DeserializeContent(File.ReadAllText(contentPath, Encoding.UTF8));
    }

    /// <summary>
    ///   Validates that the roots of the profile and content inventory match.
    /// </summary>
    /// <param name="profile">The profile definition.</param>
    /// <param name="content">The profile content.</param>
    private static void ValidateRoots(ProfileDefinition profile, ProfileContent content)
    {
        if (!string.Equals(profile.SourceDirectory, content.SourceDirectory, StringComparison.Ordinal) ||
            !string.Equals(profile.DestinationDirectory, content.DestinationDirectory, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The profile and content inventory use different directory roots.");
        }
    }

    /// <summary>
    ///   Merges the existing profile content with the current profile.
    /// </summary>
    /// <param name="existing">The existing profile content.</param>
    /// <param name="profile">The current profile.</param>
    /// <param name="createdUtc">The creation timestamp.</param>
    /// <param name="lastUpdatedUtc">The last updated timestamp.</param>
    /// <returns>The merged profile content.</returns>
    private static ProfileContent MergeContent(ProfileContent existing,
                                               SyncProfile profile,
                                               DateTimeOffset createdUtc,
                                               DateTimeOffset lastUpdatedUtc)
    {
        ValidateRoots(ProfileDefinition.FromProfile(profile), existing);

        var sourceFiles = existing.SourceFiles.ToDictionary(file => file.Path, StringComparer.Ordinal);
        var destinationFiles = existing.DestinationFiles.ToDictionary(file => file.Path, StringComparer.Ordinal);

        foreach (var mapping in profile.FileMappings)
        {
            if (mapping.Source is not null)
                sourceFiles[mapping.Source.Path] = mapping.Source;
            if (mapping.Destination is not null)
                destinationFiles[mapping.Destination.Path] = mapping.Destination;
        }

        return new ProfileContent(existing.SourceDirectory,
                                  existing.DestinationDirectory,
                                  createdUtc,
                                  lastUpdatedUtc,
                                  sourceFiles.Values,
                                  destinationFiles.Values);
    }

    /// <summary>
    ///   Saves the profile and content documents to the specified path.
    /// </summary>
    /// <param name="path">The path to save the documents to.</param>
    /// <param name="profile">The profile definition.</param>
    /// <param name="content">The profile content.</param>
    /// <param name="createdUtc">The creation timestamp.</param>
    /// <param name="lastUpdatedUtc">The last updated timestamp.</param>
    private static void SaveDocuments(string path,
                                      ProfileDefinition profile,
                                      ProfileContent content,
                                      DateTimeOffset createdUtc,
                                      DateTimeOffset lastUpdatedUtc)
    {
        ValidateRoots(profile, content);

        var document = new ProfileDocument(profile, createdUtc, lastUpdatedUtc);

        AtomicTextFile.Write(path, XmlCodecs.SerializeProfile(document));
        AtomicTextFile.Write(ProfileArtifacts.GetContentPath(path), XmlCodecs.SerializeContent(content));
    }
}
