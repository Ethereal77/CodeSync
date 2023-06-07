namespace CodeSync;

using CodeSync.Xml;
using CodeSync.Utils;

using static System.Console;
using static CodeSync.Utils.ConsoleLog;
using static CodeSync.Utils.FileEnumerator;

using static FileAnalyzer;

static class FileUpdater
{
    //
    // Reads a CodeSync XML file and verifies its validity, optionally updating it and writing a new XML file.
    //
    public static void Update(in FileUpdaterOptions options)
    {
        var inputXml = LoadXmlInput(options.InputXml);

        if (inputXml is null)
            return;

        var sourceDir = inputXml.SourceDirectory;
        var destDir = inputXml.DestinationDirectory;

        LogMessageAndValue("Directorio de origen: ", sourceDir);
        LogMessageAndValue("Directorio de destino: ", destDir);
        WriteLine();

        // Create a list of partial entries (those where one of the paths or both are invalid)
        var filesToCopy = new List<CopyFileEntry>();
        var partialEntries = new List<CopyFilePartialEntry>(inputXml.PartialEntries);

        var knownSources = new HashSet<string>();
        var knownDests = new HashSet<string>();

        var statInvalidSourceFiles = 0;
        var statInvalidDestFiles = 0;

        // Read the XML entries and categorize in valid / invalid
        foreach (var copyFileEnty in inputXml.FilesToCopy)
        {
            bool invalid = false;

            if (!FileExists(sourceDir, copyFileEnty.SourcePath))
            {
                LogWarning("El archivo de origen ya no existe.", copyFileEnty.SourcePath);
                statInvalidSourceFiles++;
                invalid = true;
            }
            if (!FileExists(destDir, copyFileEnty.DestPath))
            {
                LogWarning("El archivo de destino ya no existe.", copyFileEnty.DestPath);
                statInvalidDestFiles++;
                invalid = true;
            }

            if (invalid)
                // ❌ Invalid entry; the source and / or the destination is missing
                partialEntries.Add(new CopyFilePartialEntry(copyFileEnty.SourcePath, copyFileEnty.DestPath));
            else
            {
                // ✅ Valid entry
                filesToCopy.Add(copyFileEnty);

                knownSources.Add(copyFileEnty.SourcePath);
                knownDests.Add(copyFileEnty.DestPath);
            }
        }

        // Read the XML entries of files to ignore
        var filesInSourceToIgnore = inputXml.IgnoreSourceEntries.Select(f => f.SourcePath).ToHashSet();
        var statSourceFilesIgnored = 0;

        var filesInDestToIgnore = inputXml.IgnoreDestinationEntries.Select(f => f.DestPath).ToHashSet();
        var statDestFilesIgnored = 0;

        // Enumerate the source and destination directories, looking for files not enumerated by the XML

        // Analyze the source directory, converting to relative paths, and applying exclusion rules
        WriteLine($"Buscando en el directorio de origen `{sourceDir}`...");

        var sourceFiles = EnumerateFiles(sourceDir);
        var sourceFileQueue = new Queue<string>();

        foreach (var sourceFilePath in sourceFiles)
        {
            if (filesInSourceToIgnore.Contains(sourceFilePath))
            {
                statSourceFilesIgnored++;
                continue;
            }
            if (knownSources.Contains(sourceFilePath))
                continue;

            sourceFileQueue.Enqueue(sourceFilePath);
        }

        WriteLine($"Se han encontrado {sourceFileQueue.Count} archivos nuevos.");
        WriteLine($"Se han encontrado {statInvalidSourceFiles} archivos que ya no existen.");
        WriteLine($"Se han ignorado {statSourceFilesIgnored} archivos.");
        WriteLine();

        // Analyze the destination directory, converting to relative paths, and applying exclusion rules
        WriteLine($"Buscando en el directorio de destino `{destDir}`...");

        var destFiles = EnumerateFiles(destDir);
        var destFileMap = new FileMap();
        var newDestFiles = 0;

        foreach (var destFilePath in destFiles)
        {
            if (filesInDestToIgnore.Contains(destFilePath))
            {
                statDestFilesIgnored++;
                continue;
            }
            if (!knownDests.Contains(destFilePath))
                newDestFiles++;

            destFileMap.Add(destFilePath);
        }

        WriteLine($"Se han encontrado {destFileMap.Count} archivos ({newDestFiles} nuevos).");
        WriteLine($"Se han encontrado {statInvalidDestFiles} archivos que ya no existen.");
        WriteLine($"Se han ignorado {statDestFilesIgnored} archivos.");
        WriteLine();

        // Create the XML output if specified, and write the still valid entries there
        using var outputXml = StartXmlOutput(options, sourceDir, destDir);

        outputXml?.WritePreviousMatches(filesToCopy);
        outputXml?.WritePreviousPartialEntries(partialEntries);
        outputXml?.WritePreviousSourceFilesToIgnore(inputXml.IgnoreSourceEntries);
        outputXml?.WritePreviousDestinationFilesToIgnore(inputXml.IgnoreDestinationEntries);

        // Run the analyzer to find matches
        var analyzerOptions = new FileAnalyzerOptions
        {
            SourceDirectory = sourceDir,
            SourceFilesQueue = sourceFileQueue,

            DestinationDirectory = destDir,
            DestinationFilesMap = destFileMap,
            DestinationFilesAlreadyKnown = knownDests,

            OutputXmlFilePath = options.OutputXmlFilePath,

            UseHashMatching = options.UseHashMatching,
            DiscardOldFiles = options.DiscardOldFiles
        };
        Analyze(analyzerOptions, outputXml);


        //
        // Loads the XML synchronization file.
        //
        static XmlSyncInputFile? LoadXmlInput(string xmlFilePath)
        {
            try
            {
                var xml = new XmlSyncInputFile(xmlFilePath);
                return xml;
            }
            catch (Exception ex)
            {
                LogError(ex.Message);
                return null;
            }
        }

        //
        // Creates the output XML file if specified.
        //
        static XmlSyncOutputFile? StartXmlOutput(in FileUpdaterOptions options, string sourceDir, string destDir)
        {
            if (string.IsNullOrWhiteSpace(options.OutputXmlFilePath))
                return null;

            return new XmlSyncOutputFile(options.OutputXmlFilePath, sourceDir, destDir);
        }

        //
        // Checks that a file specified by a path exists.
        //
        static bool FileExists(string baseDir, string path)
        {
            var fullPath = Path.Combine(baseDir, path);

            return File.Exists(fullPath);
        }
    }
}
