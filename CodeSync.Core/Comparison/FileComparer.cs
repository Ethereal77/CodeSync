using System.Runtime.InteropServices;

namespace CodeSync.Core;

/// <summary>
///   Matches files by unique content and reports all unresolved pairs.
/// </summary>
public sealed class FileComparer
{
    /// <summary>
    ///   Compares the specified source and destination files and returns the comparison result.
    /// </summary>
    /// <param name="sourceFiles">The collection of source files to compare.</param>
    /// <param name="destinationFiles">The collection of destination files to compare against.</param>
    /// <returns>
    ///   The result of the comparison, including mappings, directory references, and conflicts.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    ///   Thrown if either <paramref name="sourceFiles"/> or <paramref name="destinationFiles"/> is <see langword="null"/>.
    /// </exception>
    public ComparisonResult Compare(IEnumerable<FileSnapshot> sourceFiles,
                                    IEnumerable<FileSnapshot> destinationFiles)
    {
        ArgumentNullException.ThrowIfNull(sourceFiles);
        ArgumentNullException.ThrowIfNull(destinationFiles);

        // Group source files by their size and SHA-256 hash
        var source = sourceFiles.ToArray();
        var sourceGroups = source
            .GroupBy(GetFileSnapshotKey)
            .ToDictionary(group => group.Key, group => group.ToArray());

        // Group destination files by their size and SHA-256 hash
        var destination = destinationFiles.ToArray();
        var destinationGroups = destination
            .GroupBy(GetFileSnapshotKey)
            .ToDictionary(group => group.Key, group => group.ToArray());

        var mappings = new List<FileMapping>();
        var conflicts = new List<Conflict>();
        var matchedSourcePaths = new HashSet<string>(StringComparer.Ordinal);
        var matchedDestPaths = new HashSet<string>(StringComparer.Ordinal);
        var conflictedSourcePaths = new HashSet<string>(StringComparer.Ordinal);
        var conflictedDestPaths = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (identity, sourceGroup) in sourceGroups)
        {
            // If there is no matching destination group for this source group,
            // mark all source files in this group as conflicts
            if (!destinationGroups.TryGetValue(identity, out var destinationGroup))
            {
                foreach (var sourceFile in sourceGroup)
                {
                    conflicts.Add(new Conflict(ConflictKind.SourceWithoutDestination, new FileMapping(sourceFile, destination: null)));
                    conflictedSourcePaths.Add(sourceFile.Path);
                }

                continue;
            }

            // If there is a matching destination group for the current source group,
            // and it is a one-to-one match, create a mapping for the file
            if (sourceGroup.Length == 1 && destinationGroup.Length == 1)
            {
                var mapping = new FileMapping(sourceGroup[0], destinationGroup[0]);

                mappings.Add(mapping);
                matchedSourcePaths.Add(sourceGroup[0].Path);
                matchedDestPaths.Add(destinationGroup[0].Path);
                continue;
            }

            // We arrived here because the one-to-one match condition was not met,
            // even though there is a matching destination group.
            // As a result, there are multiple source files and multiple destination files
            // with the same identity, it is considered an ambiguous match.
            foreach (var sourceFile in sourceGroup)
            {
                conflicts.Add(new Conflict(ConflictKind.AmbiguousMatch, new FileMapping(sourceFile, null)));
                conflictedSourcePaths.Add(sourceFile.Path);
            }
            foreach (var destFile in destinationGroup)
            {
                conflicts.Add(new Conflict(ConflictKind.AmbiguousMatch, new FileMapping(null, destFile)));
                conflictedDestPaths.Add(destFile.Path);
            }
        }

        // Handle any remaining source files that were not matched and not already in conflicts
        var remainingSourceFiles = source
            .Where(sourceFile => !matchedSourcePaths.Contains(sourceFile.Path)
                              && !conflictedSourcePaths.Contains(sourceFile.Path));

        foreach (var sourceFile in remainingSourceFiles)
        {
            conflicts.Add(new Conflict(ConflictKind.SourceWithoutDestination, new FileMapping(sourceFile, null)));
            conflictedSourcePaths.Add(sourceFile.Path);
        }

        // Handle any remaining destination files that were not matched and not already in conflicts
        var remainingDestFiles = destination
            .Where(destFile => !matchedDestPaths.Contains(destFile.Path)
                            && !conflictedDestPaths.Contains(destFile.Path));

        foreach (var destFile in remainingDestFiles)
        {
            conflicts.Add(new Conflict(ConflictKind.DestinationWithoutSource, new FileMapping(null, destFile)));
            conflictedDestPaths.Add(destFile.Path);
        }

        // Compose a list of possible directory concordances based on the file mappings
        var dirReferences = FindDirectoryReferences(mappings, source, destination);

        return new ComparisonResult(mappings, dirReferences, conflicts);

        //
        // Gets a unique identifier for the specified file snapshot based on its size and SHA-256 hash.
        //
        static (long Size, string Sha256) GetFileSnapshotKey(FileSnapshot file) => (file.Size, file.Sha256);
    }

    /// <summary>
    ///   Finds possible directory references based on the provided file mappings and source/destination files.
    /// </summary>
    /// <param name="mappings">The list of file mappings between source and destination files.</param>
    /// <param name="source">The list of source files.</param>
    /// <param name="destination">The list of destination files.</param>
    /// <returns>A list of possible directory references based on the file mappings.</returns>
    /// <remarks>
    ///   This method attempts to identify directories that have corresponding files in both the source and destination,
    ///   based on the provided file mappings. It helps in understanding the directory structure relationships.
    /// </remarks>
    private static IReadOnlyList<DirectoryReference> FindDirectoryReferences(
        IReadOnlyList<FileMapping> mappings,
        IReadOnlyList<FileSnapshot> source,
        IReadOnlyList<FileSnapshot> destination)
    {
        var sourceCounts = CountFilesByDirectory(source);
        var destinationCounts = CountFilesByDirectory(destination);

        var matchedCounts = new Dictionary<(string Source, string Destination), int>();

        foreach (var mapping in mappings)
        {
            if (mapping.Source is null || mapping.Destination is null)
                continue;

            foreach (var pair in EnumerateMatchingAncestorPairs(mapping.Source.Path, mapping.Destination.Path))
            {
                ref var countRef = ref CollectionsMarshal.GetValueRefOrAddDefault(matchedCounts, pair, out _);
                countRef++;
            }
        }

        // Select candidate directory references based on matched counts and file counts, i.e.,
        // those directory pairs where the number of matched files equals the total number
        // of files in both source and destination directories.
        var candidates = matchedCounts
            .Where(pair => sourceCounts.TryGetValue(pair.Key.Source, out var sourceCount)
                        && destinationCounts.TryGetValue(pair.Key.Destination, out var destinationCount)
                        && sourceCount == destinationCount
                        && pair.Value == sourceCount)
            .Select(pair => new DirectoryReference(pair.Key.Source, pair.Key.Destination))
            .ToList();

        // Filter out non-unique directory references by source and destination paths
        var uniqueBySource = candidates
            .GroupBy(reference => reference.SourcePath, StringComparer.Ordinal)
            .Where(group => group.Count() == 1)
            .Select(group => group.Single());

        var uniqueByDestination = uniqueBySource
            .GroupBy(reference => reference.DestinationPath, StringComparer.Ordinal)
            .Where(group => group.Count() == 1)
            .Select(group => group.Single());

        // Return the filtered unique directory references, sorted by source and destination paths
        // for consistency and readability.
        return uniqueByDestination
            .Where(reference => reference.SourcePath.Length > 0
                             || reference.DestinationPath.Length > 0)
            .OrderBy(reference => reference.SourcePath, StringComparer.Ordinal)
            .ThenBy(reference => reference.DestinationPath, StringComparer.Ordinal)
            .ToArray();

        //
        // Counts the number of files in each directory, including ancestor directories.
        //
        static Dictionary<string, int> CountFilesByDirectory(IEnumerable<FileSnapshot> files)
        {
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (var file in files)
            foreach (var directory in PathUtils.EnumerateAncestors(file.Path))
            {
                ref var countRef = ref CollectionsMarshal.GetValueRefOrAddDefault(counts, directory, out _);
                countRef++;
            }

            return counts;
        }

        //
        // Enumerates pairs of matching ancestor directories for the given source and destination paths.
        //
        static IEnumerable<(string Source, string Destination)> EnumerateMatchingAncestorPairs(
            string sourcePath,
            string destinationPath)
        {
            var sourceParts = sourcePath.Split('/');
            var destinationParts = destinationPath.Split('/');

            var maxSuffixLength = Math.Min(sourceParts.Length, destinationParts.Length);

            for (var suffixLength = 1; suffixLength <= maxSuffixLength; suffixLength++)
            {
                var equal = true;

                for (var offset = 1; offset <= suffixLength; offset++)
                {
                    if (!string.Equals(sourceParts[^offset], destinationParts[^offset], StringComparison.Ordinal))
                    {
                        equal = false;
                        break;
                    }
                }

                if (!equal)
                    continue;

                var sourceDirectory = string.Join('/', sourceParts[..^suffixLength]);
                var destinationDirectory = string.Join('/', destinationParts[..^suffixLength]);

                yield return (sourceDirectory, destinationDirectory);
            }
        }
    }
}
