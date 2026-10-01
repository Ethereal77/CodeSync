namespace CodeSync.Core;

/// <summary>
///   Checks the coverage of a synchronization profile of the source and destination directories
///   without comparing file contents, and separates conflicting mappings from the clean ones.
/// </summary>
public sealed class ProfileVerifier
{
    /// <summary>
    ///   Verifies the synchronization profile against the provided source and destination files, identifying any conflicts.
    /// </summary>
    /// <param name="profile">The synchronization profile to verify.</param>
    /// <param name="sourceFiles">The list of source files to check against the profile.</param>
    /// <param name="destinationFiles">The list of destination files to check against the profile.</param>
    /// <returns>
    ///   A <see cref="VerificationResult"/> containing any conflicts found during verification,
    ///   the profile without the conflicting mappings, and any warnings.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    ///   Thrown if any of the parameters are <see langword="null"/>.
    /// </exception>
    public VerificationResult Verify(SyncProfile profile,
                                     IEnumerable<FileSnapshot> sourceFiles,
                                     IEnumerable<FileSnapshot> destinationFiles)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(sourceFiles);
        ArgumentNullException.ThrowIfNull(destinationFiles);

        var source = sourceFiles.ToArray();
        var sourceByPath = source.ToDictionary(file => file.Path, PathUtils.PathComparer);

        var destination = destinationFiles.ToArray();
        var destinationByPath = destination.ToDictionary(file => file.Path, PathUtils.PathComparer);

        var conflicts = new List<Conflict>();
        var warnings = new List<string>();
        var cleanMappings = new List<FileMapping>();
        var mappedSources = new HashSet<string>(PathUtils.PathComparer);
        var mappedDestinations = new HashSet<string>(PathUtils.PathComparer);

        // Exact repetitions are harmless: keep the first one and warn
        var distinctMappings = new List<FileMapping>();
        var seenMappings = new HashSet<string>(PathUtils.PathComparer);

        foreach (var mapping in profile.FileMappings)
        {
            var key = $"{mapping.Source?.Path}\0{mapping.DestinationPath}\0{mapping.IsIgnored}";

            if (seenMappings.Add(key))
                distinctMappings.Add(mapping);
            else
            {
                var sourcePath = mapping.Source?.Path ?? "(no source)";
                var destinationPath = mapping.DestinationPath ?? "(no destination)";

                warnings.Add($"The mapping '{sourcePath}' -> '{destinationPath}' is repeated and was merged.");
            }
        }

        var sourceCounts = CountPaths(distinctMappings.Select(mapping => mapping.Source?.Path));
        var destinationCounts = CountPaths(distinctMappings.Select(mapping => mapping.DestinationPath));

        // Verify each file mapping in the profile against the source and destination files
        foreach (var mapping in distinctMappings)
        {
            var sourcePath = mapping.Source?.Path;
            var destinationPath = mapping.DestinationPath;

            if (sourcePath is not null)
                mappedSources.Add(sourcePath);
            if (destinationPath is not null)
                mappedDestinations.Add(destinationPath);

            // Every mapping sharing a source or destination is ambiguous, so none of them is kept
            var isDuplicate = (sourcePath is not null && sourceCounts[sourcePath] > 1) ||
                              (destinationPath is not null && destinationCounts[destinationPath] > 1);

            if (isDuplicate)
            {
                conflicts.Add(new Conflict(ConflictKind.DuplicateMapping, mapping));
                continue;
            }

            // A mapping is considered missing if its source or destination file is not present in the corresponding inventory
            var isMissing = !mapping.IsIgnored &&
                            ((sourcePath is not null && !sourceByPath.ContainsKey(sourcePath)) ||
                             (mapping.Destination is not null && destinationPath is not null &&
                              !destinationByPath.ContainsKey(destinationPath)));

            if (isMissing)
            {
                conflicts.Add(new Conflict(ConflictKind.MissingMappedFile, mapping));
                continue;
            }

            cleanMappings.Add(mapping);
        }

        // Check for source files without corresponding destination mappings
        foreach (var sourceFile in source.Where(file => !mappedSources.Contains(file.Path)))
        {
            var suggestedDestination = DirectoryReference.TryMapSourcePath(sourceFile.Path,
                                                                           profile.DirectoryReferences);
            var fileMapping = new FileMapping(sourceFile,
                                              destination: null,
                                              destinationPath: suggestedDestination);

            var conflict = new Conflict(ConflictKind.SourceWithoutDestination, fileMapping);

            conflicts.Add(conflict);
        }

        // Check for destination files without corresponding source mappings
        foreach (var destFile in destination.Where(file => !mappedDestinations.Contains(file.Path)))
        {
            var fileMapping = new FileMapping(source: null, destFile);

            var conflict = new Conflict(ConflictKind.DestinationWithoutSource, fileMapping);

            conflicts.Add(conflict);
        }

        // Cleaned profile with only the valid mappings and no duplicates
        var cleanedProfile = cleanMappings.Count == profile.FileMappings.Count
            ? profile
            : new SyncProfile(profile.SourceDirectory,
                              profile.DestinationDirectory,
                              profile.DirectoryReferences,
                              cleanMappings);

        return new VerificationResult(conflicts, cleanedProfile, warnings);
    }

    /// <summary>
    ///   Counts how many times each non-null path appears, using the platform path comparison.
    /// </summary>
    private static Dictionary<string, int> CountPaths(IEnumerable<string?> paths)
    {
        var counts = new Dictionary<string, int>(PathUtils.PathComparer);

        foreach (var path in paths)
        {
            if (path is not null)
                counts[path] = counts.GetValueOrDefault(path) + 1;
        }

        return counts;
    }
}
