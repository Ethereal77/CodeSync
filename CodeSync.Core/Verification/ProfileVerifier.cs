namespace CodeSync.Core;

/// <summary>
///   Checks the coverage of a synchronization profile of the source and destination directories
///   without comparing or modifying file contents.
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
    ///   A <see cref="VerificationResult"/> containing any conflicts found during verification.
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
        var sourceByPath = source.ToDictionary(file => file.Path, StringComparer.Ordinal);

        var destination = destinationFiles.ToArray();
        var destinationByPath = destination.ToDictionary(file => file.Path, StringComparer.Ordinal);

        var conflicts = new List<Conflict>();
        var mappedSources = new HashSet<string>(StringComparer.Ordinal);
        var mappedDestinations = new HashSet<string>(StringComparer.Ordinal);

        // Verify each file mapping in the profile against the source and destination files
        foreach (var mapping in profile.FileMappings)
        {
            if (mapping.Source is not null)
            {
                // Check for duplicate source mappings in the profile
                if (!mappedSources.Add(mapping.Source.Path))
                    conflicts.Add(new Conflict(ConflictKind.DuplicateMapping, mapping));
                // Check for missing source files in the profile
                else if (!sourceByPath.ContainsKey(mapping.Source.Path))
                    conflicts.Add(new Conflict(ConflictKind.MissingMappedFile, mapping));
            }

            if (mapping.Destination is not null)
            {
                // Check for duplicate destination mappings in the profile
                if (!mappedDestinations.Add(mapping.Destination.Path))
                    conflicts.Add(new Conflict(ConflictKind.DuplicateMapping, mapping));
                // Check for missing destination files in the profile
                else if (!destinationByPath.ContainsKey(mapping.Destination.Path))
                    conflicts.Add(new Conflict(ConflictKind.MissingMappedFile, mapping));
            }
        }

        // Check for source files without corresponding destination mappings
        foreach (var sourceFile in source.Where(file => !mappedSources.Contains(file.Path)))
            conflicts.Add(new Conflict(ConflictKind.SourceWithoutDestination, new FileMapping(sourceFile, null)));

        // Check for destination files without corresponding source mappings
        foreach (var destFile in destination.Where(file => !mappedDestinations.Contains(file.Path)))
            conflicts.Add(new Conflict(ConflictKind.DestinationWithoutSource, new FileMapping(null, destFile)));

        return new VerificationResult(conflicts);
    }
}
