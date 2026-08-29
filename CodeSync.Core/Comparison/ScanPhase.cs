namespace CodeSync.Core;

/// <summary>
///   Defines the different stages of a directory scan.
/// </summary>
public enum ScanPhase
{
    /// <summary>
    ///   The phase where the directory contents are being enumerated.
    /// </summary>
    Enumerating,

    /// <summary>
    ///   The phase where ignored paths are being filtered out.
    /// </summary>
    Filtering,

    /// <summary>
    ///   The phase where file hashes are being computed.
    /// </summary>
    Hashing,

    /// <summary>
    ///   The phase indicating that the scan has completed.
    /// </summary>
    Completed
}
