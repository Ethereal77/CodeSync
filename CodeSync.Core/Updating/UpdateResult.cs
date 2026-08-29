namespace CodeSync.Core;

/// <summary>
///   Result of refreshing a synchronization profile by scanning the source directory
///   and considering previously skipped files.
/// </summary>
public sealed record UpdateResult
{
    /// <summary>
    ///   Gets the updated synchronization profile.
    /// </summary>
    public SyncProfile UpdatedProfile { get; }

    /// <summary>
    ///   Gets the list of source file paths that were updated.
    /// </summary>
    public IReadOnlyList<string> UpdatedSourcePaths { get; }

    /// <summary>
    ///   Gets the list of errors encountered during the update process.
    /// </summary>
    public IReadOnlyList<string> Errors { get; }

    /// <summary>
    ///   Gets a value indicating whether the update operation succeeded (i.e., no errors were encountered).
    /// </summary>
    public bool Succeeded => Errors is [];


    /// <summary>
    ///   Initializes a new instance of the <see cref="UpdateResult"/> class.
    /// </summary>
    /// <param name="updatedProfile">The updated synchronization profile.</param>
    /// <param name="updatedSourcePaths">The list of source file paths that were updated.</param>
    /// <param name="errors">The list of errors encountered during the update process.</param>
    /// <exception cref="ArgumentNullException">
    ///   Thrown if any of the constructor arguments are <see langword="null"/>.
    /// </exception>
    public UpdateResult(SyncProfile updatedProfile,
                        IEnumerable<string> updatedSourcePaths,
                        IEnumerable<string> errors)
    {
        UpdatedProfile = updatedProfile
            ?? throw new ArgumentNullException(nameof(updatedProfile));
        UpdatedSourcePaths = updatedSourcePaths?.ToArray()
            ?? throw new ArgumentNullException(nameof(updatedSourcePaths));
        Errors = errors?.ToArray()
            ?? throw new ArgumentNullException(nameof(errors));
    }
}
