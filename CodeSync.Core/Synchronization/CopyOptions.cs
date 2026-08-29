namespace CodeSync.Core;

/// <summary>
///   Represents the options that control the behavior of a copy operation.
/// </summary>
/// <param name="DryRun">
///   Indicates whether the copy operation should be simulated without making actual changes.
/// </param>
public sealed record CopyOptions(bool DryRun = false);
