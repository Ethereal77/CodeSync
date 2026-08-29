namespace CodeSync.Core;

/// <summary>
///   Represents the overall result of a copy operation, including the updated profile,
///   the status of each file, and any skipped files.
/// </summary>
public sealed record CopyResult
{
    /// <summary>
    ///   The updated synchronization profile after the copy operation.
    /// </summary>
    public SyncProfile UpdatedProfile { get; }

    /// <summary>
    ///   The list of results for each file involved in the copy operation.
    /// </summary>
    public IReadOnlyList<CopyFileResult> Files { get; }

    /// <summary>
    ///   The list of source file paths that were skipped because they were unchanged.
    /// </summary>
    public IReadOnlyList<string> SkippedSourcePaths { get; }

    /// <summary>
    ///   Indicates whether the copy operation succeeded for all files.
    /// </summary>
    /// <value>
    ///   This property is <see langword="true"/> only if no file failed.
    ///   It does not necessarily mean that all files were successfully copied;
    ///   some may have been skipped or ignored.
    ///   If any file failed, it will be <see langword="false"/>.
    /// </value>
    public bool Succeeded
        => Files.All(file => file.Status is not CopyFileStatus.Failed);


    /// <summary>
    ///   Initializes a new instance of the <see cref="CopyResult"/> class.
    /// </summary>
    /// <param name="updatedProfile">The updated synchronization profile after the copy operation.</param>
    /// <param name="files">The list of results for each file involved in the copy operation.</param>
    /// <exception cref="ArgumentNullException">
    ///   Thrown if <paramref name="updatedProfile"/> or <paramref name="files"/> is <see langword="null"/>.
    /// </exception>
    public CopyResult(SyncProfile updatedProfile, IEnumerable<CopyFileResult> files)
    {
        UpdatedProfile = updatedProfile
            ?? throw new ArgumentNullException(nameof(updatedProfile));
        Files = files?.ToArray()
            ?? throw new ArgumentNullException(nameof(files));

        SkippedSourcePaths = Files
            .Where(file => file.Status == CopyFileStatus.SkippedUnchanged)
            .Select(file => file.SourcePath)
            .ToArray();
    }
}
