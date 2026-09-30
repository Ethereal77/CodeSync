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
    /// <param name="backupExisting">Whether to back up an existing file before replacing it.</param>
    public static void Write(string path, string content, bool backupExisting = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var fullPath = Path.GetFullPath(path);
        var parent = Path.GetDirectoryName(fullPath);
        var temporaryPath = fullPath + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            if (!string.IsNullOrEmpty(parent))
                Directory.CreateDirectory(parent);

            // Use UTF-8 encoding without BOM for writing the file
            var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

            File.WriteAllText(temporaryPath, content, encoding);

            if (backupExisting && File.Exists(fullPath))
                CreateBackup(fullPath);

            // Move and overwrite the original file with the temporary file instead of writing directly to it
            // so that the write operation is atomic and the original file is not corrupted in case of an error.
            File.Move(temporaryPath, fullPath, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new FileSaveException(fullPath, exception);
        }
        finally
        {
            try
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
            catch (IOException)
            {
                // Ignore exceptions during cleanup of the temporary file
            }
            catch (UnauthorizedAccessException)
            {
                // Ignore exceptions during cleanup of the temporary file
            }
        }
    }

    /// <summary>
    ///   Creates a backup of the specified file by appending a numbered suffix
    ///   to the backup file name if necessary.
    /// </summary>
    /// <param name="path">The path to the file to back up.</param>
    private static void CreateBackup(string path)
    {
        var backupPath = path + ".bak";

        for (var suffix = 0; File.Exists(backupPath); suffix++)
            backupPath = $"{path}.{suffix}.bak";

        File.Copy(path, backupPath);
    }
}
