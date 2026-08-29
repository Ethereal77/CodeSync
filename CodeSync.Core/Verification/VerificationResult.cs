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
    ///   Gets a value indicating whether the verification passed without any conflicts.
    /// </summary>
    public bool IsValid => Conflicts.Count == 0;


    /// <summary>
    ///   Initializes a new instance of the <see cref="VerificationResult"/> class.
    /// </summary>
    /// <param name="conflicts">The list of conflicts found during verification.</param>
    /// <exception cref="ArgumentNullException">
    ///   Thrown if the <paramref name="conflicts"/> parameter is null.
    /// </exception>
    public VerificationResult(IEnumerable<Conflict> conflicts)
    {
        Conflicts = conflicts?.ToArray()
            ?? throw new ArgumentNullException(nameof(conflicts));
    }
}
