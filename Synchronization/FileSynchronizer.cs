namespace CodeSync;

using System.Diagnostics;
using System.Xml.Linq;

using static System.Console;
using static CodeSync.Utils.ConsoleLog;

using static CodeSync.Xml.XmlSyncFormat;

static class FileSynchronizer
{
    //
    // Executes the copy operations described in a CodeSync XML file.
    //
    public static void Synchronize(in FileSynchronizerOptions options)
    {
        var xml = LoadXml(options.InputXml, out var sourceDir, out var destDir, out var lastModifiedXml);

        LogMessageAndValue("Directorio de origen: ", sourceDir);
        LogMessageAndValue("Directorio de destino: ", destDir);
        WriteLine();

        var dryRun = options.DryRun;
        var ignoreOlderThanXml = options.DoNotCopyFilesOlderThanTheXml;
        var ignoreOlderThanDest = options.DoNotCopyFilesOlderThanTheDestination;

        if (lastModifiedXml is not null)
        {
            LogMessageAndValue("Modificado por última vez: ", lastModifiedXml.Value.ToLongDateString());
            WriteLine();
        }
        else ignoreOlderThanXml = false;

        int copiedFiles = 0, ignoredFiles = 0, errorFiles = 0;

        var filesToCopy = xml.Elements(CopyFileEntryTag);

        foreach (var fileToCopy in filesToCopy)
        {
            CopyFile(fileToCopy);
        }

        LogCopyResults(copiedFiles, errorFiles, ignoredFiles, dryRun);

        if (errorFiles > 0)
            Environment.Exit(1);

        //
        // Loads the XML synchronization file.
        //
        static XElement LoadXml(string xmlFilePath, out string sourceDir, out string destDir, out DateTime? lastModifiedTime)
        {
            (sourceDir, destDir, lastModifiedTime) = (null!, null!, null);

            using var xmlFile = File.OpenText(xmlFilePath);
            var xml = XDocument.Load(xmlFile);

            var codeSyncXml = xml.Element(RootTag);

            if (codeSyncXml is null)
            {
                // ❌ The XML file is not a valid CodeSync file
                LogError("El archivo XML especificado no es un archivo CodeSync válido.");
                return null!;
            }

            var xmlSourceDir = codeSyncXml.Element(SourceRepositoryDirectoryTag);
            var xmlDestDir = codeSyncXml.Element(DestinationRepositoryDirectoryTag);

            if (xmlSourceDir is null || xmlDestDir is null)
            {
                // ❌ The XML file has no valid source and / or destination directories specified
                LogError("El archivo XML especificado no especifica directorios de origen y destino.");
                return null!;
            }

            sourceDir = (string) xmlSourceDir;
            destDir = (string) xmlDestDir;

            var xmlLastModifiedTime = codeSyncXml.Element(ModifiedTimeTag);
            lastModifiedTime = (DateTime?) xmlLastModifiedTime;

            return codeSyncXml;
        }

        //
        // Copies a file from the source to the destination directory.
        //
        void CopyFile(XElement xmlCopy)
        {
            var source = (string?) xmlCopy.Element(FileEntrySourceTag);
            var dest = (string?) xmlCopy.Element(FileEntryDestinationTag);

            if (source is null || dest is null)
            {
                // ❌ Invalid source or destination path
                LogError("No se especifica correctamente origen o destino para la copia.");
                return;
            }

            var fileName = Path.GetFileName(source);

            var sourcePath = Path.Combine(sourceDir, source);
            var destPath = Path.Combine(destDir, dest);

            try
            {
                bool ignoreFileCopy = false;
                if (ignoreOlderThanDest || ignoreOlderThanXml)
                {
                    var sourceFileTime = File.GetLastWriteTimeUtc(sourcePath);

                    // Ignore files older than the last modification time of the XML
                    bool isOlderThanXml = ignoreOlderThanXml && sourceFileTime < lastModifiedXml;

                    // Ignore files older than the destination file
                    var destFileTime = ignoreOlderThanDest ? File.GetLastWriteTimeUtc(destPath) : default;
                    var isOlderThanDest = ignoreOlderThanDest && destFileTime > sourceFileTime;

                    if (isOlderThanXml || isOlderThanDest)
                    {
                        Debug.Assert(lastModifiedXml is not null);

                        LogCopyIgnored(fileName, sourcePath, destPath, sourceFileTime, 
                                       isOlderThanXml, lastModifiedXml.Value, 
                                       isOlderThanDest, destFileTime);

                        ignoredFiles++;
                        ignoreFileCopy = true;
                    }
                }

                if (!ignoreFileCopy)
                {
                    if (!dryRun)
                        File.Copy(sourcePath, destPath, overwrite: true);

                    LogCopy(fileName, sourcePath, destPath);
                    copiedFiles++;
                }
            }
            catch
            {
                LogCopyError(fileName);
                errorFiles++;
            }
        }
    }
}
