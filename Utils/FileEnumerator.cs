namespace CodeSync.Utils;

/// <summary>
///   A helper class that enumerates the source directories for files.
/// </summary>
static class FileEnumerator
{
    private static string[] excludedBaseDirs = null!;
    private static string[] excludedSubDirs = null!;

    /// <summary>
    ///   Gets or sets a list of directories to exclude from the search.
    /// </summary>
    public static string[]? DirectoriesToExclude
    {
        get => excludedBaseDirs?.Clone() as string[];
        set
        {
            if (value is null)
            {
                excludedBaseDirs = excludedSubDirs = Array.Empty<string>();
            }
            else
            {
                excludedBaseDirs = (string[]) value.Clone();
                excludedSubDirs = excludedBaseDirs.Select(dir => $@"\{dir}\").ToArray();
            }
        }
    }


    static FileEnumerator()
    {
        // Default excluded directories
        DirectoriesToExclude = new[] { "obj", "bin", ".vs", ".vscode", ".git" };
    }


    /// <summary>
    ///   Enumerates the files in a directory and its subdirectories, excluding some from the
    ///   resulting enumeration (<c>obj/</c>, <c>bin/</c>, etc).
    /// </summary>
    public static IEnumerable<string> EnumerateFiles(string directory)
    {
        var options = new EnumerationOptions
        {
            IgnoreInaccessible = true,
            ReturnSpecialDirectories = false,
            RecurseSubdirectories = true
        };

        var files = Directory.EnumerateFiles(directory, "*", options)
            .Select(path => Path.GetRelativePath(relativeTo: directory, path))
            .Where(path => !excludedBaseDirs.Any(exclusion => path.ToLowerInvariant().StartsWith(exclusion + Path.DirectorySeparatorChar)))
            .Where(path => !excludedSubDirs.Any(exclusion => path.ToLowerInvariant().Contains(exclusion)));

        return files;
    }
}
