namespace CodeSync.Core;

/// <summary>
///   Represents one or more errors found while loading a persisted profile.
/// </summary>
public sealed class ProfileLoadException : Exception
{
    /// <summary>
    ///   Gets the errors found while loading the profile.
    /// </summary>
    public IReadOnlyList<string> Errors { get; }


    /// <summary>
    ///   Initializes an exception containing all profile loading errors.
    /// </summary>
    /// <param name="errors">The errors found while loading the profile.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="errors"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="errors"/> is empty.</exception>
    public ProfileLoadException(IEnumerable<string> errors)
        : this(errors?.ToArray() ?? throw new ArgumentNullException(nameof(errors)))
    {
    }

    private ProfileLoadException(string[] errors)
        : base(CreateMessage(errors))
    {
        Errors = errors;
    }

    private static string CreateMessage(IReadOnlyList<string> errors)
    {
        if (errors.Count == 0)
            throw new ArgumentException("At least one profile loading error is required.", nameof(errors));

        return $"The profile contains {errors.Count} error(s):{Environment.NewLine}" +
               string.Join(Environment.NewLine, errors.Select(error => $"- {error}"));
    }
}
