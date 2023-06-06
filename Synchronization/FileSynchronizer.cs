namespace CodeSync;

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
        var xml = LoadXml(options.InputXml, out var sourceDir, out var destDir);

        LogMessageAndValue("Directorio de origen: ", sourceDir);
        LogMessageAndValue("Directorio de destino: ", destDir);
        WriteLine();

        int copiedFiles = 0;
        int errorFiles = 0;

        var filesToCopy = xml.Elements(CopyFileEntryTag);

        foreach (var fileToCopy in filesToCopy)
        {
            CopyFile(fileToCopy);
        }

        LogCopyResults(copiedFiles, errorFiles);

        if (errorFiles > 0)
            Environment.Exit(1);

        //
        // Loads the XML synchronization file.
        //
        static XElement LoadXml(string xmlFilePath, out string sourceDir, out string destDir)
        {
            using var xmlFile = File.OpenText(xmlFilePath);
            var xml = XDocument.Load(xmlFile);

            var codeSyncXml = xml.Element(RootTag);

            if (codeSyncXml is null)
            {
                // ❌ The XML file is not a valid CodeSync file
                LogError("El archivo XML especificado no es un archivo CodeSync válido.");
                sourceDir = null!;
                destDir = null!;
                return null!;
            }

            var xmlSourceDir = codeSyncXml.Element(SourceRepositoryDirectoryTag);
            var xmlDestDir = codeSyncXml.Element(DestinationRepositoryDirectoryTag);

            if (xmlSourceDir is null || xmlDestDir is null)
            {
                // ❌ The XML file has no valid source and / or destination directories specified
                LogError("El archivo XML especificado no especifica directorios de origen y destino.");
                sourceDir = null!;
                destDir = null!;
                return null!;
            }

            sourceDir = (string) xmlSourceDir;
            destDir = (string) xmlDestDir;
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
                File.Copy(sourcePath, destPath, overwrite: true);
                LogCopy(fileName, sourcePath, destPath);
                copiedFiles++;
            }
            catch
            {
                LogCopyError(fileName);
                errorFiles++;
            }
        }
    }
}
