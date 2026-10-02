using CodeSync.Core;

namespace CodeSync;

internal sealed partial class App
{
    /// <summary>
    ///   Loads the <c>.gitignore</c> matcher for the specified root directory.
    /// </summary>
    /// <param name="rootDirectory">The root directory for which to load the <c>.gitignore</c> matcher.</param>
    /// <returns>
    ///   An instance of <see cref="GitIgnoreMatcher"/> configured with the rules from
    ///   the <c>.gitignore</c> files in the specified root directory and its subdirectories.
    /// </returns>
    private static GitIgnoreMatcher LoadIgnoreMatcher(string rootDirectory, IEnumerable<string> discoveredPaths)
    {
        List<IgnoreRuleSet> ruleSets =
        [
            // By default, ignore the .git directory
            new(basePath: string.Empty, rules: [".git/"])
        ];

        // Compile all the .gitignore rules into the rule sets
        foreach (var relativePath in discoveredPaths.Where(path =>
                     string.Equals(Path.GetFileName(path), ".gitignore", StringComparison.OrdinalIgnoreCase)))
        {
            var normalizedPath = PathUtils.NormalizeFilePath(relativePath);
            var fullPath = Path.Combine(rootDirectory, normalizedPath.Replace('/', Path.DirectorySeparatorChar));
            var basePath = PathUtils.NormalizeDirectoryPath(Path.GetDirectoryName(normalizedPath) ?? string.Empty);

            ruleSets.Add(new IgnoreRuleSet(basePath, File.ReadLines(fullPath)));
        }

        // Create a matcher with the compiled rule sets
        return new GitIgnoreMatcher(ruleSets);
    }
}
