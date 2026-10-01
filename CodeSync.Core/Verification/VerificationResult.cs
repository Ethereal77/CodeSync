namespace CodeSync.Core;

/// <summary>
///   Result of validating a synchronization profile against the current state
///   of the source and destination directories.
/// </summary>
public sealed record VerificationResult
{
    /// <summary>
    ///   Gets the list of conflicts found during verification.
    /// </summary>
    public IReadOnlyList<Conflict> Conflicts { get; }

    /// <summary>
    ///   Gets the verified profile without its conflicting mappings and with repeated mappings merged.
    /// </summary>
    public SyncProfile CleanedProfile { get; }

    /// <summary>
    ///   Gets the non-blocking warnings found during verification.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; }

    /// <summary>
    ///   Gets a value indicating whether the verification passed without any conflicts.
    /// </summary>
    public bool IsValid => Conflicts.Count == 0;


    /// <summary>
    ///   Initializes a new instance of the <see cref="VerificationResult"/> class.
    /// </summary>
    /// <param name="conflicts">The list of conflicts found during verification.</param>
    /// <param name="cleanedProfile">The profile without its conflicting mappings.</param>
    /// <param name="warnings">The non-blocking warnings found during verification.</param>
    /// <exception cref="ArgumentNullException">
    ///   Thrown if any of the parameters is null.
    /// </exception>
    public VerificationResult(IEnumerable<Conflict> conflicts,
                              SyncProfile cleanedProfile,
                              IEnumerable<string> warnings)
    {
        Conflicts = conflicts?.ToArray()
            ?? throw new ArgumentNullException(nameof(conflicts));

        CleanedProfile = cleanedProfile
            ?? throw new ArgumentNullException(nameof(cleanedProfile));

        Warnings = warnings?.ToArray()
            ?? throw new ArgumentNullException(nameof(warnings));
    }
}
