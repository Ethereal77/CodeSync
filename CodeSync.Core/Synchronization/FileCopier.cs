namespace CodeSync.Core;

/// <summary>
///   Copies mapped files from a source to a destination directories as indicated by a synchronization profile.
/// </summary>
public sealed class FileCopier
{
    /// <summary>
    ///   Copies the files as specified by the synchronization profile, updating the profile state as necessary.
    /// </summary>
    /// <param name="profile">The synchronization profile containing the file mappings.</param>
    /// <param name="workspace">The workspace used to read and write files.</param>
    /// <param name="options">Optional copy options, such as dry run mode.</param>
    /// <returns>A <see cref="CopyResult"/> containing the results of the copy operation.</returns>
    /// <exception cref="ArgumentNullException">
    ///   Thrown if <paramref name="profile"/> or <paramref name="workspace"/> is <see langword="null"/>.
    /// </exception>
    public CopyResult Copy(SyncProfile profile, IWorkspace workspace, CopyOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(workspace);

        options ??= new CopyOptions();

        var results = new List<CopyFileResult>();
        var updatedMappings = new List<FileMapping>(profile.FileMappings.Count);

        // Iterate through each file mapping in the profile and process the copy operation accordingly
        foreach (var mapping in profile.FileMappings)
        {
            // If there is no source file to copy, skip this mapping.
            // It may be a newly added file in the destination directory.
            if (mapping.Source is null)
            {
                updatedMappings.Add(mapping);
                continue;
            }

            // If there is no destination file specified, this mapping is ignored
            if (mapping.Destination is null)
            {
                updatedMappings.Add(mapping);
                results.Add(new CopyFileResult(mapping.Source.Path, DestinationPath: null, CopyFileStatus.Ignored));
                continue;
            }

            try
            {
                // We have source and destination, proceed with checking for changes and copying if necessary

                // Read the current state of the source file from the workspace
                var currentSource = workspace.ReadSnapshot(profile.SourceDirectory, mapping.Source.Path);

                if (IsUnchanged(mapping.Source, currentSource))
                {
                    // The source file has not changed since the last snapshot, so we skip copying it
                    updatedMappings.Add(mapping);
                    results.Add(new CopyFileResult(currentSource.Path,
                                                   mapping.Destination.Path,
                                                   CopyFileStatus.SkippedUnchanged));
                    continue;
                }

                if (!options.DryRun)
                {
                    // Read the content of the current source file from the workspace
                    var content = workspace.ReadAllBytes(profile.SourceDirectory, currentSource.Path);

                    // Ensure the parent directory exists before writing the file
                    workspace.EnsureParentDirectory(profile.DestinationDirectory, mapping.Destination.Path);
                    // Write the content of the source file to the destination path
                    workspace.WriteAllBytes(profile.DestinationDirectory, mapping.Destination.Path, content);

                    // Read the current state of the destination file from the workspace after writing
                    var currentDestination = workspace.ReadSnapshot(profile.DestinationDirectory, mapping.Destination.Path);
                    updatedMappings.Add(new FileMapping(currentSource, currentDestination));
                }
                else
                {
                    // In dry run mode, we do not perform any actual copying, but we still record the mapping as updated
                    updatedMappings.Add(mapping);
                }

                results.Add(new CopyFileResult(currentSource.Path, mapping.Destination.Path, CopyFileStatus.Copied));
            }
            catch (Exception ex)
            {
                // An error occurred while processing this file mapping, mark it as failed
                updatedMappings.Add(mapping);

                results.Add(new CopyFileResult(mapping.Source.Path,
                                               mapping.Destination.Path,
                                               CopyFileStatus.Failed,
                                               ex.Message));
            }
        }

        // After processing all file mappings, create an updated sync profile and return the copy results
        var updatedProfile = new SyncProfile(profile.SourceDirectory,
                                             profile.DestinationDirectory,
                                             profile.DirectoryReferences,
                                             updatedMappings);

        return new CopyResult(updatedProfile, results);

        //
        // Determines if two file snapshots are unchanged based on size, last write time, and SHA-256 hash.
        //
        static bool IsUnchanged(FileSnapshot previous, FileSnapshot current)
        {
            return previous.Size == current.Size
                && previous.LastWriteTimeUtc == current.LastWriteTimeUtc
                && string.Equals(previous.Sha256, current.Sha256, StringComparison.Ordinal);
        }
    }
}
