namespace CodeSync.Core;

/// <summary>
///   Represents the result of attempting to copy a file from the source to the destination.
/// </summary>
/// <param name="SourcePath">The path of the source file.</param>
/// <param name="DestinationPath">The path of the destination file, if applicable.</param>
/// <param name="Status">The status of the copy operation.</param>
/// <param name="Error">An optional error message if the copy failed.</param>
public sealed record CopyFileResult(string SourcePath,
                                    string? DestinationPath,
                                    CopyFileStatus Status,
                                    string? Error = null);
