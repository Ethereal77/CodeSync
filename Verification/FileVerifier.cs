namespace CodeSync;

using CodeSync.Xml;
using CodeSync.Utils;

using static System.Console;
using static CodeSync.Utils.ConsoleLog;
using static CodeSync.Utils.FileEnumerator;

using static FileAnalyzer;

static class FileVerifier
{
    //
    // Reads a CodeSync XML file and verifies its validity, reorganizing the entries in the file, and
    // discarding the incorrect ones.
    //
    public static void Verify(in FileVerifierOptions options)
    {
        var inputXml = LoadXmlInput(options.InputXml);

        if (inputXml is null)
            return;

        var sourceDir = inputXml.SourceDirectory;
        var destDir = inputXml.DestinationDirectory;

        LogMessageAndValue("Directorio de origen: ", sourceDir);
        LogMessageAndValue("Directorio de destino: ", destDir);
        WriteLine();

        // Verify the directories are valid
        if (!Directory.Exists(sourceDir))
            LogWarning("El directorio de origen no existe.", sourceDir);

        if (!Directory.Exists(destDir))
            LogWarning("El directorio de destino no existe.", destDir);

        // Read the XML entries of files to ignore
        var setFilesInSourceToIgnore = new HashSet<string>();
        var filesInSourceToIgnore = new List<string>();
        var dupIgnoreSourceFiles = 0;
        var missingIgnoreSourceFiles = 0;

        var setFilesInDestToIgnore = new HashSet<string>();
        var filesInDestToIgnore = new List<string>();
        var dupIgnoreDestFiles = 0;
        var missingIgnoreDestFiles = 0;

        // Check repeated ignore entries
        foreach (var file in inputXml.IgnoreSourceEntries)
        {
            bool alreadyKnown = !setFilesInSourceToIgnore.Add(file.SourcePath);

            if (alreadyKnown && options.DiscardRepeatedEntries)
            {
                LogWarning("El mismo archivo a ignorar en el directorio de origen aparece varias veces.", file.SourcePath);
                dupIgnoreSourceFiles++;
                continue;
            }

            filesInSourceToIgnore.Add(file.SourcePath);
        }
        foreach (var file in inputXml.IgnoreDestinationEntries)
        {
            bool alreadyKnown = !setFilesInDestToIgnore.Add(file.DestPath);

            if (alreadyKnown && options.DiscardRepeatedEntries)
            {
                LogWarning("El mismo archivo a ignorar en el directorio de destino aparece varias veces.", file.DestPath);
                dupIgnoreDestFiles++;
                continue;
            }

            filesInDestToIgnore.Add(file.DestPath);
        }

        // Read the XML entries of files to copy
        var setFileCopyEntries = new HashSet<CopyFileEntry>();
        var fileCopyEntries = new List<CopyFileEntry>();

        var dupFileCopies = 0;
        var ignoredFileCopies = 0;

        foreach (var fileEntry in inputXml.FilesToCopy)
        {
            bool alreadyKnown = !setFileCopyEntries.Add(fileEntry);
            bool discardEntry = false;
            bool ignoreEntry = false;

            // Check if the same entry has already appeared before
            if (alreadyKnown && options.DiscardRepeatedEntries)
            {
                LogDuplicatedCopy(fileEntry);
                dupFileCopies++;
                discardEntry = true;
            }

            // Check if the entry is ignored by one of the Ignore entries
            if (filesInSourceToIgnore.Contains(fileEntry.SourcePath))
            {
                LogWarning("El archivo de origen a copiar está también entre los archivos a ignorar.", fileEntry.SourcePath);
                ignoreEntry = true;
            }
            if (filesInDestToIgnore.Contains(fileEntry.DestPath))
            {
                LogWarning("El archivo de destino a copiar está también entre los archivos a ignorar.", fileEntry.DestPath);
                ignoreEntry = true;
            }

            if (ignoreEntry)
            {
                LogIgnoredCopy(fileEntry);
                ignoredFileCopies++;
                setFileCopyEntries.Remove(fileEntry);
            }
            else if (!discardEntry)
                fileCopyEntries.Add(fileEntry);
        }

        // Report invalid or malformed entries
        var malformedCopyEntries = 0;
        foreach (var fileEntry in inputXml.PartialEntries)
        {
            LogMalformedCopy(fileEntry);
            malformedCopyEntries++;

            if (fileEntry.SourcePath is not null && filesInSourceToIgnore.Contains(fileEntry.SourcePath))
            {
                LogWarning("El archivo de origen a copiar está también entre los archivos a ignorar.", fileEntry.SourcePath);
            }
            if (fileEntry.DestPath is not null && filesInDestToIgnore.Contains(fileEntry.DestPath))
            {
                LogWarning("El archivo de destino a copiar está también entre los archivos a ignorar.", fileEntry.DestPath);
            }
        }

        // Check the existence of the files specified by Copy entries
        var missingSourceToCopy = 0;
        var missingDestToCopy = 0;

        if (options.DiscardMissingFiles.HasFlag(CheckExistingEntryOption.CheckCopyEntries))
        {
            var fileEntriesToCheck = new List<CopyFileEntry>(fileCopyEntries);
            fileCopyEntries.Clear();

            foreach (var fileEntry in fileEntriesToCheck)
            {
                bool invalid = false;

                if (!FileExists(sourceDir, fileEntry.SourcePath))
                {
                    LogWarning("El archivo de origen ya no existe.", fileEntry.SourcePath);
                    missingSourceToCopy++;
                    invalid = true;
                }
                if (!FileExists(destDir, fileEntry.DestPath))
                {
                    LogWarning("El archivo de destino ya no existe.", fileEntry.DestPath);
                    missingDestToCopy++;
                    invalid = true;
                }

                if (!invalid)
                    fileCopyEntries.Add(fileEntry);
            }
        }
        // Check the existence of the files specified by Ignore entries
        if (options.DiscardMissingFiles.HasFlag(CheckExistingEntryOption.CheckIgnoreEntries))
        {
            var sourceFilesIgnoredToCheck = new List<string>(filesInSourceToIgnore);
            filesInSourceToIgnore.Clear();

            foreach (var file in sourceFilesIgnoredToCheck)
            {
                if (!FileExists(sourceDir, file))
                {
                    LogWarning("El archivo de origen ya no existe.", file);
                    missingIgnoreSourceFiles++;
                }
                else filesInSourceToIgnore.Add(file);
            }

            var destFilesIgnoredToCheck = new List<string>(filesInDestToIgnore);
            filesInDestToIgnore.Clear();

            foreach (var file in destFilesIgnoredToCheck)
            {
                if (!FileExists(destDir, file))
                {
                    LogWarning("El archivo de destino ya no existe.", file);
                    missingIgnoreDestFiles++;
                }
                else filesInDestToIgnore.Add(file);
            }
        }

        // Create the XML output if specified, and write the still valid entries there
        using var outputXml = StartXmlOutput(options, sourceDir, destDir);

        if (outputXml is not null)
        {
            var copyEntries = fileCopyEntries.OrderBy(x => x.SourcePath);

            foreach (var copyEntry in copyEntries)
                outputXml.WriteMatch(copyEntry.SourcePath, copyEntry.DestPath);

            var sourceIgnoreEntries = filesInSourceToIgnore.OrderBy(x => x);
            var destIgnoreEntries = filesInDestToIgnore.OrderBy(x => x);

            foreach (var ignoreSource in sourceIgnoreEntries)
                outputXml.WriteIgnore(ignoreSource, null);
            foreach (var ignoreDest in destIgnoreEntries)
                outputXml.WriteIgnore(null, ignoreDest);
        }

        LogVerifyStats(fileCopyEntries.Count, dupFileCopies, missingSourceToCopy, missingDestToCopy, malformedCopyEntries, ignoredFileCopies,
                       filesInSourceToIgnore.Count, dupIgnoreSourceFiles, missingIgnoreSourceFiles,
                       filesInDestToIgnore.Count, dupIgnoreDestFiles, missingIgnoreDestFiles);


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
        static XmlSyncOutputFile? StartXmlOutput(in FileVerifierOptions options, string sourceDir, string destDir)
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
