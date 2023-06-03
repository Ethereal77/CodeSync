using System.Xml.Linq;

using static ConsoleLog;

static class FileSynchronizer
{
    //
    // Analyzes the source files and the destination file map to determine possible file matches.
    //
    public static void Synchronize(in FileSynchronizerOptions options)
    {
        var xml = LoadXml(options.InputXml, out var sourceDir, out var destDir);

        var filesToCopy = xml.Elements("Copy");

        Console.WriteLine($"Directorio de origen: {sourceDir}");
        Console.WriteLine($"Directorio de destino: {destDir}");
        Console.WriteLine();

        int copiedFiles = 0;

        foreach (var fileToCopy in filesToCopy)
        {
            CopyFile(fileToCopy);
        }

        Console.WriteLine(copiedFiles == 1 ? $"{copiedFiles} archivo copiado." : $"{copiedFiles} archivos copiados.");
        Console.WriteLine();

        //
        // Loads the XML synchronization file.
        //
        static XElement LoadXml(string xmlFilePath, out string sourceDir, out string destDir)
        {
            using var xmlFile = File.OpenText(xmlFilePath); // <<<< Close?
            var xml = XDocument.Load(xmlFile);

            var codeSyncXml = xml.Element("CodeSync");

            if (codeSyncXml is null)
            {
                // ❌ The XML file is not a valid CodeSync file
                LogError("El archivo XML especificado no es un archivo CodeSync válido.");
                sourceDir = null!;
                destDir = null!;
                return null!;
            }

            var xmlSourceDir = codeSyncXml.Element("SourceDirectory");
            var xmlDestDir = codeSyncXml.Element("DestDirectory");

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
            var source = (string?) xmlCopy.Element("Source");
            var dest = (string?) xmlCopy.Element("Destination");

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
            }
            catch
            {
                LogCopyError(fileName);
            }
        }
    }
}
