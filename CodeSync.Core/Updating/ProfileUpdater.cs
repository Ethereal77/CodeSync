namespace CodeSync.Core;

/// <summary>
///   Provides functionality to update a synchronization profile by refreshing
///   source snapshots that were previously skipped.
/// </summary>
public sealed class ProfileUpdater
{
    /// <summary>
    ///   Updates the synchronization profile by refreshing the source snapshots
    ///   that were previously skipped.
    /// </summary>
    /// <param name="profile">The synchronization profile to update.</param>
    /// <param name="skippedSourcePaths">The list of source file paths that were previously skipped.</param>
    /// <param name="currentSourceFiles">The current state of the source files.</param>
    /// <returns>An <see cref="UpdateResult"/> representing the outcome of the update operation.</returns>
    /// <exception cref="ArgumentNullException">
    ///   Thrown if any of the method arguments are <see langword="null"/>.
    /// </exception>
    public UpdateResult Update(SyncProfile profile,
                               IEnumerable<string> skippedSourcePaths,
                               IEnumerable<FileSnapshot> currentSourceFiles)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(skippedSourcePaths);
        ArgumentNullException.ThrowIfNull(currentSourceFiles);

        var currentByPath = currentSourceFiles
            .ToDictionary(file => file.Path, StringComparer.Ordinal);

        var mappingsByPath = profile.FileMappings
            .Where(mapping => mapping.Source is not null)
            .ToDictionary(mapping => mapping.Source!.Path, StringComparer.Ordinal);

        var updatedPaths = new List<string>();
        var errors = new List<string>();
        var updatedMappings = profile.FileMappings.ToArray();

        // Normalize the skipped source paths to ensure consistent comparison
        // with the current source files and profile mappings.
        var normalizedSkippedSourcePaths = skippedSourcePaths
            .Select(PathUtils.NormalizeFilePath)
            .Distinct(StringComparer.Ordinal);

        // Iterate over the normalized skipped source paths and attempt to update the corresponding mappings
        foreach (var path in normalizedSkippedSourcePaths)
        {
            // Skip any empty or invalid paths
            if (!mappingsByPath.TryGetValue(path, out var mapping))
            {
                errors.Add($"The skipped source '{path}' is not present in the profile.");
                continue;
            }

            // If the mapping exists but the source path is empty or invalid, skip it
            if (!currentByPath.TryGetValue(path, out var current))
            {
                errors.Add($"The skipped source '{path}' no longer exists.");
                continue;
            }

            // Update the mapping with the current source file snapshot and record the updated path
            var index = Array.IndexOf(updatedMappings, mapping);
            updatedMappings[index] = new FileMapping(current, mapping.Destination);
            updatedPaths.Add(path);
        }

        // Return the updated profile along with the list of updated paths and any errors encountered
        var updatedProfile = new SyncProfile(profile.SourceDirectory,
                                             profile.DestinationDirectory,
                                             profile.DirectoryReferences,
                                             updatedMappings);

        return new UpdateResult(updatedProfile, updatedPaths, errors);
    }
}
