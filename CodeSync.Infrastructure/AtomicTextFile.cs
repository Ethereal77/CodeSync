using System.Text;

namespace CodeSync.Infrastructure;

/// <summary>
///   Provides atomic write operations for text files, ensuring that the original file
///   is not corrupted in case of an error.
/// </summary>
internal static class AtomicTextFile
{
    /// <summary>
    ///   Writes the specified content to the specified path atomically, ensuring that the original file
    ///   is not corrupted in case of an error.
    /// </summary>
    /// <param name="path">The path to the file to write.</param>
    /// <param name="content">The content to write to the file.</param>
    public static void Write(string path, string content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var fullPath = Path.GetFullPath(path);
        var parent = Path.GetDirectoryName(fullPath);

        if (!string.IsNullOrEmpty(parent))
            Directory.CreateDirectory(parent);

        var temporaryPath = fullPath + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            // Use UTF-8 encoding without BOM for writing the file
            var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

            File.WriteAllText(temporaryPath, content, encoding);

            // Move and overwrite the original file with the temporary file instead of writing directly to it
            // so that the write operation is atomic and the original file is not corrupted in case of an error.
            File.Move(temporaryPath, fullPath, overwrite: true);
        }
        finally
        {
            // Clean up the temporary file if it still exists, ensuring no leftover files remain
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }
}
