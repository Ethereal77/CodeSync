namespace CodeSync.Core;

/// <summary>
///   Defines the possible outcomes for each file when attempting
///   to copy it from the source to the destination.
/// </summary>
public enum CopyFileStatus
{
    /// <summary>
    ///   The file was successfully copied from the source to the destination.
    /// </summary>
    Copied,

    /// <summary>
    ///   The file was skipped because it was unchanged.
    /// </summary>
    SkippedUnchanged,

    /// <summary>
    ///   The file was ignored and not copied.
    /// </summary>
    Ignored,

    /// <summary>
    ///   The file failed to copy due to an error.
    /// </summary>
    Failed
}
