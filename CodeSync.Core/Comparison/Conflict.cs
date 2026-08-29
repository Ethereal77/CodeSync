namespace CodeSync.Core;

/// <summary>
///   Represents a conflict between source and destination files, i.e., a situation where
///   the action of automatically synchronizing the files cannot be completed successfully and
///   human review is required.
/// </summary>
/// <param name="Kind">The kind of conflict.</param>
/// <param name="Mapping">The file mapping associated with the conflict.</param>
public sealed record Conflict(ConflictKind Kind, FileMapping Mapping);
