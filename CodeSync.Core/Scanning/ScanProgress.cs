namespace CodeSync.Core;

/// <summary>
///   Represents the progress of a directory scan.
/// </summary>
/// <param name="Phase">The current phase of the scan.</param>
/// <param name="Total">The total number of items to be scanned.</param>
/// <param name="Completed">The number of items that have been completed so far.</param>
/// <param name="Ignored">The number of items that have been ignored.</param>
/// <param name="Elapsed">The elapsed time since the scan started.</param>
public readonly record struct ScanProgress(ScanPhase Phase,
                                           int Total,
                                           int Completed,
                                           int Ignored,
                                           TimeSpan Elapsed);
