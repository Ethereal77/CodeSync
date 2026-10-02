namespace CodeSync.Core;

/// <summary>
///   Reads and writes file data without exposing a concrete file-system implementation.
/// </summary>
public interface IWorkspace
{
    /// <summary>
    ///   Enumerates all files within the specified root directory, including files in subdirectories.
    /// </summary>
    /// <param name="rootDirectory">The root directory to enumerate files from.</param>
    /// <returns>A collection of relative file paths within the root directory.</returns>
    IEnumerable<string> EnumerateFiles(string rootDirectory);

    /// <summary>
    ///   Reads a snapshot of the specified file within the root directory.
    /// </summary>
    /// <param name="rootDirectory">The root directory containing the file.</param>
    /// <param name="relativePath">The relative path to the file within the root directory.</param>
    /// <returns>
    ///   A snapshot of the specified file, including its relative path, size, last write time, and content hash.
    /// </returns>
    FileSnapshot ReadSnapshot(string rootDirectory, string relativePath);

    /// <summary>
    ///   Reads a snapshot of the specified file within the root directory.
    /// </summary>
    /// <param name="rootDirectory">The root directory containing the file.</param>
    /// <param name="relativePath">The relative path to the file within the root directory.</param>
    /// <param name="cancellationToken">A token used to check if the operation is to be canceled.</param>
    /// <returns>A task that produces the file snapshot.</returns>
    /// <remarks>
    ///   The default implementation simply calls the synchronous <see cref="ReadSnapshot"/> method
    ///   and wraps the result in a completed task. Implementations may override this method to provide
    ///   true asynchronous behavior.
    /// </remarks>
    Task<FileSnapshot> ReadSnapshotAsync(string rootDirectory,
                                         string relativePath,
                                         CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ReadSnapshot(rootDirectory, relativePath));
    }

    /// <summary>
    ///   Reads all bytes from the specified file within the root directory.
    /// </summary>
    /// <param name="rootDirectory">The root directory containing the file.</param>
    /// <param name="relativePath">The relative path to the file within the root directory.</param>
    /// <returns>The content of the specified file as a byte array.</returns>
    byte[] ReadAllBytes(string rootDirectory, string relativePath);

    /// <summary>
    ///   Writes the specified content to the file within the root directory.
    /// </summary>
    /// <param name="rootDirectory">The root directory containing the file.</param>
    /// <param name="relativePath">The relative path to the file within the root directory.</param>
    /// <param name="content">The content to write to the file.</param>
    void WriteAllBytes(string rootDirectory, string relativePath, ReadOnlySpan<byte> content);

    /// <summary>
    ///   Ensures that the parent directory of the specified file exists within the root directory.
    /// </summary>
    /// <param name="rootDirectory">The root directory containing the file.</param>
    /// <param name="relativePath">The relative path to the file within the root directory.</param>
    void EnsureParentDirectory(string rootDirectory, string relativePath);
}
