using System.Diagnostics;
using System.Buffers;
using System.IO.Hashing;
using Microsoft.Win32.SafeHandles;

using static ConsoleLog;

static class FileAnalyzer
{
    //
    // Analyzes the source files and the destination file map to determine possible file matches.
    //
    public static void Analyze(in FileAnalyzerOptions options)
    {
        var sourceFiles = options.SourceFilesQueue;
        var destFileMap = options.DestinationFilesMap;

        XmlSyncFile? xml = options.OutputXmlFilePath is not null ? new XmlSyncFile(options) : null;

        var filesInSourceNotInDest = new List<string>();
        var filesInSourceManyInDest = new List<(string, MultipleFileSource)>();

        FileHashMap? fileHashes = null;

        var statFilesMatched = 0;
        var statFilesMatchedByHash = 0;
        var statFilesInSourceNotInDest = 0;
        var statFilesInSourceMultiInDest = 0;
        var statFilesInDestNotInSource = 0;

        while (sourceFiles.Count > 0)
        {
            var sourceFilePath = sourceFiles.Dequeue();
            var sourceFileName = Path.GetFileName(sourceFilePath);

            bool possibleCoincidence = destFileMap.TryGetValue(sourceFileName, out IFileSource? destFileSource);
            if (possibleCoincidence == false)
            {
                // ❌ The source file name is not found in the destination
                LogWarning($"El archivo no tiene ninguna coincidencia en el destino.", sourceFilePath);
                filesInSourceNotInDest.Add(sourceFilePath);
                statFilesInSourceNotInDest++;
                continue;
            }

            if (destFileSource is SingleFileSource destSingleFile)
            {
                // ✅ Match: A source file has a corresponding destination file
                OutputMatch(sourceFileName, sourceFilePath, destSingleFile.FilePath);
                statFilesMatched++;
                destFileMap.Remove(sourceFileName);
            }
            else if (destFileSource is MultipleFileSource destMultiFile)
            {
                var foundDestPath = destMultiFile.Find(path => string.Compare(path, sourceFilePath, StringComparison.InvariantCultureIgnoreCase) == 0);

                if (foundDestPath is null)
                {
                    // Try to match by size / match
                    if (options.UseHashMatching)
                    {
                        // ⚠️ Possible match, but use size / content matching to verify
                        HashMatchSourceFileToFileList(sourceFileName, options.SourceDirectory, sourceFilePath, 
                                                      options.DestinationDirectory, destMultiFile);
                    }

                    if (destMultiFile.Count > 0)
                    {
                        // ❌ The source file name is found in several places in the destination
                        LogWarningAmbiguous(sourceFilePath, destMultiFile);
                        statFilesInSourceMultiInDest++;
                        filesInSourceManyInDest.Add((sourceFilePath, destMultiFile));
                    }
                    continue;
                }

                // ✅ Match: A source file has a corresponding destination file
                OutputMatch(sourceFileName, sourceFilePath, foundDestPath);
                statFilesMatched++;

                destFileMap.Remove(sourceFileName, destMultiFile, foundDestPath);
            }
        }
        
        // If there are unmatched files not matched by name, try by contents
        if (options.UseHashMatching &&
            statFilesInSourceNotInDest > 0 && destFileMap.Count > 0)
        {
            HashMatchOrphanFiles(options.SourceDirectory, options.DestinationDirectory, filesInSourceNotInDest);
        }

        OutputFilesInSourceNotInDestination();
        OutputFilesInSourceAmbiguousDestination();
        OutputFilesInDestinationNotInSource();

        xml?.Dispose();

        LogStatistics(statFilesMatched, statFilesMatchedByHash,
                      statFilesInSourceNotInDest, statFilesInSourceMultiInDest,
                      statFilesInDestNotInSource);

        //
        // Writes to the output XML a match between a source file and a destination file.
        //
        void OutputMatch(string sourceFileName, string sourceFilePath, string destFilePath, bool hashMatch = false)
        {
            LogMatch(sourceFileName, sourceFilePath, destFilePath, hashMatch);

            xml?.WriteMatch(sourceFilePath, destFilePath);
        }

        //
        // Writes to the output XML the files in the source repository that have no matching file
        // in the destination repository (i.e. they got deleted?).
        //
        void OutputFilesInSourceNotInDestination()
        {
            if (filesInSourceNotInDest.Count == 0)
                return;
            
            xml?.WriteSourceOrphanFiles(filesInSourceNotInDest);
        }

        //
        // Writes to the output XML the files in the source repository that have many matching files
        // in the destination repository, so the destination is ambiguous.
        //
        void OutputFilesInSourceAmbiguousDestination()
        {
            if (filesInSourceManyInDest.Count == 0)
                return;

            xml?.OutputFilesInSourceAmbiguousDestination(filesInSourceManyInDest);
        }

        //
        // Writes to the output XML the files in the destination repository that have no matching file
        // in the source repository (i.e. they are new?).
        //
        void OutputFilesInDestinationNotInSource()
        {
            if (destFileMap.Count == 0)
                return;

            LogWarningFileMapFilesRemaining(destFileMap);

            xml?.WriteDestinationOrphanFiles(destFileMap.Values, out statFilesInDestNotInSource);
        }

        //
        // Tries to match a source file to one destination file out of a candidate list based
        // on its size and / or contents hash.
        //
        void HashMatchSourceFileToFileList(string sourceFileName, string sourceDirectory, string sourceFilePath,
                                           string destDirectory, MultipleFileSource destFiles)
        {
            var sourcePath = Path.Combine(sourceDirectory, sourceFilePath);

            using var sourceHandle = File.OpenHandle(sourcePath);
            var sourceLength = RandomAccess.GetLength(sourceHandle);

            int? sourceHash = null;

            var filesToMatch = new List<string>(destFiles);
            filesToMatch.Reverse();

            foreach (var candidate in filesToMatch)
            {
                var destPath = Path.Combine(destDirectory, candidate);

                using var destHandle = File.OpenHandle(destPath);
                var destLength = RandomAccess.GetLength(destHandle);

                if (destLength != sourceLength)
                    continue;
                
                sourceHash ??= ComputeHash(sourceHandle, sourceLength);

                var destHash = ComputeHash(destHandle, destLength);
                if (destHash == sourceHash)
                {
                    // ✅ Match: A source file has a corresponding destination file
                    OutputMatch(sourceFileName, sourceFilePath, candidate, hashMatch: true);
                    statFilesMatchedByHash++;

                    destFileMap.Remove(sourceFileName, destFiles, candidate);
                }
            }
        }

        //
        // Computes a hash of the contents of a file.
        //
        static int ComputeHash(SafeFileHandle fileHandle, long lengthToHash)
        {
            const int BufferSize = 4 * 1024; // 4 kiB

            var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);

            var crc = new Crc32();
            long totalRead = 0;
            while (totalRead < lengthToHash)
            {
                var bytesRead = RandomAccess.Read(fileHandle, buffer, totalRead);
                crc.Append(buffer.AsSpan(0, bytesRead));
                totalRead += bytesRead;
            }

            ArrayPool<byte>.Shared.Return(buffer);

            return BitConverter.ToInt32(crc.GetHashAndReset());
        }

        //
        // Computes a hash of the contents of a file.
        //
        static (long Length, int Hash) ComputeHashAndLength(string filePath)
        {
            using var fileHandle = File.OpenHandle(filePath);
            var length = RandomAccess.GetLength(fileHandle);

            const int BufferSize = 4 * 1024; // 4 kiB

            var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);

            var crc = new Crc32();
            long totalRead = 0;
            while (totalRead < length)
            {
                var bytesRead = RandomAccess.Read(fileHandle, buffer, totalRead);
                crc.Append(buffer.AsSpan(0, bytesRead));
                totalRead += bytesRead;
            }

            ArrayPool<byte>.Shared.Return(buffer);

            var hash = BitConverter.ToInt32(crc.GetHashAndReset());

            return (length, hash);
        }

        //
        // Tries to match the remaining files that have no match in either side by comparing
        // their contents.
        //
        void HashMatchOrphanFiles(string sourceDirectory, string destDirectory,
                                  ICollection<string> orphanSourceFiles)
        {
            fileHashes ??= new FileHashMap(capacity: statFilesInSourceNotInDest + statFilesInDestNotInSource);

            // Compute the hash of all orphan files in the source directory
            foreach (var sourceFile in orphanSourceFiles)
            {
                var (length, hash) = ComputeFileHash(sourceFile, sourceDirectory);
                fileHashes.Add(hash, new FileHashInfo(Matched: false, sourceFile, length));
            }

            // Check the orphan files in the destination directory for matches
            var orphanDestFiles = destFileMap.Values.ToArray();
            foreach (var destFile in orphanDestFiles)
            {
                if (destFile is SingleFileSource single)
                {
                    var matched = CheckFileHash(single.FilePath, destDirectory);
                    if (matched)
                    {
                        // ✅ Match: A source file has a corresponding destination file
                        destFileMap.Remove(Path.GetFileName(single.FilePath));
                        statFilesMatched++;
                        statFilesMatchedByHash++;
                    }
                }
                else if (destFile is MultipleFileSource multi)
                {
                    foreach (var file in multi)
                    {
                        var matched = CheckFileHash(file, destDirectory);
                        if (matched)
                        {
                            // ✅ Match: A source file has a corresponding destination file
                            destFileMap.Remove(Path.GetFileName(file), multi, file);
                            statFilesMatched++;
                            statFilesMatchedByHash++;
                        }
                    }
                }
                else Debug.Fail("Not a single nor a multiple file source!");
            }

            // Recount the unmatched files in source without an associated destination file
            statFilesInSourceNotInDest = fileHashes.SelectMany(kvp => kvp.Value).Count(f => f.Matched is false);

            //
            // Computes the hash for a file.
            //
            (long Length, int Hash) ComputeFileHash(string filePath, string baseDirectory)
            {
                var fullPath = Path.Combine(baseDirectory, filePath);
                return ComputeHashAndLength(fullPath);
            }

            //
            // Checks the hash of a file for matches with any of the orphan source files.
            //
            bool CheckFileHash(string filePath, string baseDirectory)
            {
                var (length, hash) = ComputeFileHash(filePath, baseDirectory);

                if (fileHashes.TryGetValue(hash, out var files))
                {
                    int foundIndex = -1;
                    for (int i = 0; i < files.Count && foundIndex < 0; i++)
                        if (files[i].Length == length)
                            foundIndex = i;

                    if (foundIndex != -1)
                    {
                        var sourceFile = files[foundIndex];
                        var sourceFilePath = sourceFile.FilePath;
                        var sourceFileName = Path.GetFileName(sourceFile.FilePath);

                        OutputMatch(sourceFileName, sourceFilePath, filePath, hashMatch: true);

                        files[foundIndex] = sourceFile with { Matched = true };
                        orphanSourceFiles.Remove(sourceFile.FilePath);
                        return true;
                    }
                }

                return false;
            }
        }
    }
}

/// <summary>
///   Specifies the configuration settings for the <see cref="FileAnalyzer"/>.
/// </summary>
struct FileAnalyzerOptions
{
    public required string SourceDirectory { get; init; } 
    public required Queue<string> SourceFilesQueue { get; init; }
    
    public required string DestinationDirectory { get; init; }
    public required FileMap DestinationFilesMap { get; init; } 

    public bool UseHashMatching = false;

    public string? OutputXmlFilePath = null;

    public FileAnalyzerOptions() { }
}
