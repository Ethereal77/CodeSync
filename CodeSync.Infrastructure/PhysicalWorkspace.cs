using CodeSync.Core;

namespace CodeSync.Infrastructure;

/// <summary>
///   Workspace implementation backed by ordinary directories and files.
/// </summary>
/// <param name="hashProvider">
///   The hash provider used to compute file hashes.
///   If <see langword="null"/>, a default SHA-256 hash provider is used.
/// </param>
public sealed class PhysicalWorkspace(IHashProvider? hashProvider = null) : IWorkspace
{
    private readonly IHashProvider _hashProvider = hashProvider ?? new Sha256HashProvider();


    /// <summary>
    ///   Enumerates all files within the specified root directory, including files in subdirectories.
    /// </summary>
    /// <param name="rootDirectory">The root directory to enumerate files from.</param>
    /// <returns>A collection of relative and normalized file paths within the root directory.</returns>
    public IEnumerable<string> EnumerateFiles(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);

        // Use a stack to perform a depth-first traversal of the directory tree
        var pendingDirectories = new Stack<string>([rootDirectory]);

        while (pendingDirectories.Count > 0)
        {
            var directory = pendingDirectories.Pop();

            // First, enumerate all files in the current directory
            foreach (var path in Directory.EnumerateFiles(directory))
                yield return PathUtils.NormalizeFilePath(Path.GetRelativePath(rootDirectory, path));

            // Then, enumerate all subdirectories in the current directory
            foreach (var child in Directory.EnumerateDirectories(directory))
            {
                // Special case: skip the .git directory
                if (string.Equals(Path.GetFileName(child), ".git", StringComparison.OrdinalIgnoreCase))
                    continue;

                // Skip reparse points (e.g., symbolic links) to avoid potential infinite loops
                if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) != 0)
                    continue;

                // Add the subdirectory to the stack for further traversal
                pendingDirectories.Push(child);
            }
        }
    }

    /// <summary>
    ///   Reads a snapshot of the specified file within the root directory.
    /// </summary>
    /// <param name="rootDirectory">The root directory containing the file.</param>
    /// <param name="relativePath">The relative path to the file within the root directory.</param>
    /// <returns>
    ///   A snapshot of the specified file, including its relative path, size, last write time, and content hash.
    /// </returns>
    public FileSnapshot ReadSnapshot(string rootDirectory, string relativePath)
    {
        relativePath = PathUtils.NormalizeFilePath(relativePath);
        var fullPath = Path.Combine(rootDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));

        using var content = OpenForSequentialRead(fullPath, asynchronous: false);
        var length = content.Length;
        var lastWriteTimeUtc = File.GetLastWriteTimeUtc(content.SafeFileHandle);

        var contentHash = _hashProvider.ComputeSha256(content);

        return new FileSnapshot(relativePath, length, lastWriteTimeUtc, contentHash);
    }

    /// <summary>
    ///   Reads a snapshot of the specified file within the root directory.
    /// </summary>
    /// <param name="rootDirectory">The root directory containing the file.</param>
    /// <param name="relativePath">The relative path to the file within the root directory.</param>
    /// <param name="cancellationToken">A token used to check if the operation is to be canceled.</param>
    /// <returns>
    ///   A snapshot of the specified file, including its relative path, size, last write time, and content hash.
    /// </returns>
    public async Task<FileSnapshot> ReadSnapshotAsync(string rootDirectory,
                                                       string relativePath,
                                                       CancellationToken cancellationToken = default)
    {
        relativePath = PathUtils.NormalizeFilePath(relativePath);
        var fullPath = Path.Combine(rootDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));

        await using var content = OpenForSequentialRead(fullPath, asynchronous: true);
        var length = content.Length;
        var lastWriteTimeUtc = File.GetLastWriteTimeUtc(content.SafeFileHandle);

        var contentHash = await _hashProvider.ComputeSha256Async(content, cancellationToken);

        return new FileSnapshot(relativePath, length, lastWriteTimeUtc, contentHash);
    }

    /// <summary>
    ///   Reads all bytes from the specified file within the root directory.
    /// </summary>
    /// <param name="rootDirectory">The root directory containing the file.</param>
    /// <param name="relativePath">The relative path to the file within the root directory.</param>
    /// <returns>The content of the specified file as a byte array.</returns>
    public byte[] ReadAllBytes(string rootDirectory, string relativePath)
    {
        var fullPath = GetFullPath(rootDirectory, relativePath);

        return File.ReadAllBytes(fullPath);
    }

    /// <summary>
    ///   Writes the specified content to the specified file within the root directory.
    /// </summary>
    /// <param name="rootDirectory">The root directory containing the file.</param>
    /// <param name="relativePath">The relative path to the file within the root directory.</param>
    /// <param name="content">The content to write to the file.</param>
    public void WriteAllBytes(string rootDirectory, string relativePath, ReadOnlySpan<byte> content)
    {
        var fullPath = GetFullPath(rootDirectory, relativePath);

        File.WriteAllBytes(fullPath, content);
    }

    /// <summary>
    ///   Ensures that the parent directory of the specified file within the root directory exists.
    /// </summary>
    /// <param name="rootDirectory">The root directory containing the file.</param>
    /// <param name="relativePath">The relative path to the file within the root directory.</param>
    /// <remarks>
    ///   If the parent directory does not exist, it will be created.
    /// </remarks>
    public void EnsureParentDirectory(string rootDirectory, string relativePath)
    {
        var parent = Path.GetDirectoryName(GetFullPath(rootDirectory, relativePath));
        if (!string.IsNullOrEmpty(parent))
            Directory.CreateDirectory(parent);
    }


    /// <summary>
    ///   Gets the full path to the specified file within the root directory.
    /// </summary>
    /// <param name="rootDirectory">The root directory containing the file.</param>
    /// <param name="relativePath">The relative path to the file within the root directory.</param>
    /// <returns>The full path to the specified file within the root directory.</returns>
    private static string GetFullPath(string rootDirectory, string relativePath)
    {
        var normalizedPath = PathUtils.NormalizeFilePath(relativePath);

        return Path.Combine(rootDirectory, normalizedPath.Replace('/', Path.DirectorySeparatorChar));
    }

    /// <summary>
    ///   Opens the specified file for sequential reading, optionally using asynchronous I/O.
    /// </summary>
    /// <param name="fullPath">The full path to the file to open.</param>
    /// <param name="asynchronous">Indicates whether to use asynchronous I/O.</param>
    /// <returns>A <see cref="FileStream"/> for reading the specified file sequentially.</returns>
    private static FileStream OpenForSequentialRead(string fullPath, bool asynchronous)
    {
        // Open the file for sequential read, optionally using asynchronous I/O
        var fileOptions = FileOptions.SequentialScan | (asynchronous ? FileOptions.Asynchronous : FileOptions.None);
        var streamOptions = new FileStreamOptions
        {
            Mode = FileMode.Open,
            Access = FileAccess.Read,
            Share = FileShare.Read,
            BufferSize = 1024 * 1024, // 1 MB buffer for efficient sequential reading
            Options = fileOptions
        };

        return new FileStream(fullPath, streamOptions);
    }
}
