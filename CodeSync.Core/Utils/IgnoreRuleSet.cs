namespace CodeSync.Core;

/// <summary>
///   Associates <c>.gitignore</c>-formatted rules with the directory that owns them.
/// </summary>
public sealed record IgnoreRuleSet
{
    /// <summary>
    ///   Gets the base directory path for the ignore rules.
    /// </summary>
    public string BasePath { get; }

    /// <summary>
    ///   Gets the collection of ignore rules associated with the base path.
    /// </summary>
    public IReadOnlyList<string> Rules { get; }


    /// <summary>
    ///   Initializes a new instance of the <see cref="IgnoreRuleSet"/> class.
    /// </summary>
    /// <param name="basePath">The base directory path for the ignore rules.</param>
    /// <param name="rules">The collection of ignore rules associated with the base path.</param>
    /// <exception cref="ArgumentNullException">
    ///   Thrown if the <paramref name="rules"/> parameter is <see langword="null"/>.
    /// </exception>
    public IgnoreRuleSet(string basePath, IEnumerable<string> rules)
    {
        BasePath = PathUtils.NormalizeDirectoryPath(basePath);

        Rules = rules?.ToArray() ?? throw new ArgumentNullException(nameof(rules));
    }
}
