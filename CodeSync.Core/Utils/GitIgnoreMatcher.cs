using Ignore;

using static CodeSync.Core.PathUtils;

namespace CodeSync.Core;

/// <summary>
///   Evaluates <c>.gitignore</c>-formatted rules determining whether files or directories should be ignored.
/// </summary>
public sealed class GitIgnoreMatcher : IIgnoreMatcher
{
    private readonly IReadOnlyList<CompiledRuleSet> _ruleSets;


    /// <summary>
    ///   Initializes a new instance of the <see cref="GitIgnoreMatcher"/> class.
    /// </summary>
    /// <param name="rules">The collection of ignore rules to be used by the matcher.</param>
    public GitIgnoreMatcher(IEnumerable<string> rules)
        : this([new IgnoreRuleSet(string.Empty, rules)])
    { }

    /// <summary>
    ///   Initializes a new instance of the <see cref="GitIgnoreMatcher"/> class.
    /// </summary>
    /// <param name="ruleSets">The collection of ignore rule sets to be used by the matcher.</param>
    public GitIgnoreMatcher(IEnumerable<IgnoreRuleSet> ruleSets)
    {
        ArgumentNullException.ThrowIfNull(ruleSets);

        _ruleSets = ruleSets
            .OrderBy(ruleSet => GetPathDepth(ruleSet.BasePath))
            .Select(ruleSet =>
            {
                var rules = ruleSet.Rules
                    .Select(rule => new IgnoreRule(rule))
                    .ToArray();

                return new CompiledRuleSet(ruleSet.BasePath, rules);
            })
            .ToArray();
    }


    /// <summary>
    ///   Determines whether the specified file or directory should be ignored based on the configured ignore rules.
    /// </summary>
    /// <param name="relativePath">The relative path of the file or directory to check.</param>
    /// <param name="isDirectory">Indicates whether the path represents a directory.</param>
    /// <returns>
    ///   <see langword="true"/> if the specified file or directory should be ignored;
    ///   otherwise, <see langword="false"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    ///   Thrown if <paramref name="relativePath"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   Thrown if <paramref name="relativePath"/> is not a relative path, or it contains '.' or '..' segments.
    /// </exception>
    public bool IsIgnored(string relativePath, bool isDirectory)
    {
        relativePath = NormalizeFilePath(relativePath);

        var ignored = false;

        foreach (var ruleSet in _ruleSets)
        {
            // Skip rule sets that are not applicable to the current path
            if (!IsPathUnder(relativePath, ruleSet.BasePath))
                continue;

            var localPath = GetRelativeSuffix(relativePath, ruleSet.BasePath);
            var pathForMatcher = isDirectory ? localPath + "/" : localPath;

            foreach (var rule in ruleSet.Rules)
                if (rule.IsMatch(pathForMatcher))
                    ignored = !rule.Negate;
        }

        return ignored;
    }


    /// <summary>
    ///   Represents a compiled set of ignore rules for a specific base path.
    /// </summary>
    /// <param name="BasePath">The base path to which the ignore rules apply.</param>
    /// <param name="Rules">The collection of compiled ignore rules for the base path.</param>
    private sealed record CompiledRuleSet(string BasePath, IReadOnlyList<IgnoreRule> Rules);
}
