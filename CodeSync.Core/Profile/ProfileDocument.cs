namespace CodeSync.Core;

/// <summary>
///   Contains an editable profile and its persisted document timestamps.
/// </summary>
public sealed record ProfileDocument
{
    /// <summary>
    ///   Gets the editable profile definition.
    /// </summary>
    public ProfileDefinition Profile { get; }

    /// <summary>
    ///   Gets the UTC creation time.
    /// </summary>
    public DateTimeOffset CreatedUtc { get; }

    /// <summary>
    ///   Gets the UTC last-update time.
    /// </summary>
    public DateTimeOffset LastUpdatedUtc { get; }


    /// <summary>
    ///   Initializes a profile document.
    /// </summary>
    public ProfileDocument(ProfileDefinition profile, DateTimeOffset createdUtc, DateTimeOffset lastUpdatedUtc)
    {
        Profile = profile ?? throw new ArgumentNullException(nameof(profile));

        CreatedUtc = createdUtc;
        LastUpdatedUtc = lastUpdatedUtc;
    }
}
