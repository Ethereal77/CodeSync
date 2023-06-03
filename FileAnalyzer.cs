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
        var filesInSourceManyInDest = new List<(string, MultipleFileDestination)>();

        FileHashMap? fileHashes = null;

        var statFilesMatched = 0;
        var statFilesMatchedByHash = 0;
        var statFilesInSourceNotInDest = 0;
        var statFilesInSourceMultiInDest = 0;
        var statFilesInSourceOneAmbiguousInDest = 0;
        var statFilesInDestNotInSource = 0;

        while (sourceFiles.Count > 0)
        {
            var sourceFilePath = sourceFiles.Dequeue();
            var sourceFileName = Path.GetFileName(sourceFilePath);

            bool possibleCoincidence = destFileMap.TryGetValue(sourceFileName, out IFileDestination? destFileSource);
            if (possibleCoincidence == false)
            {
                // ❌ The source file name is not found in the destination
                LogWarning($"El archivo no tiene ninguna coincidencia en el destino.", sourceFilePath);
                filesInSourceNotInDest.Add(sourceFilePath);
                statFilesInSourceNotInDest++;
                continue;
            }

            if (destFileSource is SingleFileDestination singleDestination)
            {
                // ✅ Match: A source file has a corresponding destination file
                OutputMatch(sourceFileName, sourceFilePath, singleDestination);
                statFilesMatched++;
                destFileMap.Remove(sourceFileName);
            }
            else if (destFileSource is MultipleFileDestination multipleDestinations)
            {
                var foundDestPath = multipleDestinations.Find(sourceFilePath);
                if (foundDestPath is not null)
                {
                    // ✅ Match: A source file has a corresponding destination file
                    OutputMatch(sourceFileName, sourceFilePath, foundDestPath);
                    statFilesMatched++;

                    destFileMap.Remove(sourceFileName, multipleDestinations, foundDestPath);
                }
                else
                {
                    // No destination matches the source file path

                    // Order the candidates by partial matching based on the path
                    var destPathsRanked = OrderByPathSimilarity(sourceFilePath, multipleDestinations);

                    // Try to match by size / match
                    bool hasMatchedByHash = false;
                    if (options.UseHashMatching)
                    {
                        // ⚠️ Possible match, but use size / content matching to verify
                        hasMatchedByHash = HashMatchSourceFileToFileList(sourceFileName, options.SourceDirectory, sourceFilePath,
                                                                         options.DestinationDirectory, multipleDestinations, destPathsRanked);
                    }

                    // If after hash matching there's still candidate destinations for a source file name...
                    if (multipleDestinations.Count > 0 && !hasMatchedByHash)
                    {
                        // ❌ The source file name is found in several places in the destination
                        filesInSourceManyInDest.Add((sourceFilePath, multipleDestinations));
                    }
                }
            }
        }

        List<(string, string)>? filesInSourceWithOnlyOneDest = null;

        // Check the ambiguous files that have discarded others and now have a single candidate
        if (filesInSourceManyInDest.Count > 0)
        {
            var filesWithNoCandidates = filesInSourceManyInDest.Where(f => f.Item2.Count == 0).Select(f => f.Item1);

            filesInSourceNotInDest.AddRange(filesWithNoCandidates);

            var filesWithAnyCandidate = filesInSourceManyInDest.Where(f => f.Item2.Count > 0);
            var filesWithOnlyOneCandidateLeft = filesWithAnyCandidate.Where(f => f.Item2.Count == 1);

            filesInSourceWithOnlyOneDest = filesWithOnlyOneCandidateLeft.Select(f => (f.Item1, f.Item2[0])).ToList();
            filesInSourceManyInDest = filesWithAnyCandidate.Except(filesWithOnlyOneCandidateLeft).ToList();
        }

        // If there are unmatched files not matched by name, try by contents
        if (options.UseHashMatching &&
            statFilesInSourceNotInDest > 0 && destFileMap.Count > 0)
        {
            HashMatchOrphanFiles(options.SourceDirectory, options.DestinationDirectory, filesInSourceNotInDest);
        }

        OutputFilesInSourceWithOneDestinationByDiscarding();
        OutputFilesInSourceAmbiguousDestination();
        OutputFilesInSourceNotInDestination();
        OutputFilesInDestinationNotInSource();

        xml?.Dispose();

        LogStatistics(statFilesMatched, statFilesMatchedByHash, statFilesInSourceOneAmbiguousInDest,
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

            xml?.OutputFilesInSourceAmbiguousDestination(filesInSourceManyInDest, out statFilesInSourceMultiInDest);
        }

        //
        // Writes to the output XML the files in the source repository that had many matching files
        // in the destination repository, but by discarding others, are now with just one potentially
        // incorrect destination candidate.
        //
        void OutputFilesInSourceWithOneDestinationByDiscarding()
        {
            if ((filesInSourceWithOnlyOneDest?.Count ?? 0) == 0)
                return;

            xml?.OutputFilesInSourceOneAmbiguousDestinationLeft(filesInSourceWithOnlyOneDest, out statFilesInSourceOneAmbiguousInDest);
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
        // Ranks and orders a list of candidate destination paths according to their similarity to a
        // source path.
        //
        IEnumerable<string> OrderByPathSimilarity(string sourcePath, IList<string> destinationPaths)
        {
            if (destinationPaths.Count < 2)
                return destinationPaths;

            // Divide the source path in directories and filenames
            var sourcePathParts = sourcePath.Split(Path.DirectorySeparatorChar);
            Array.Reverse(sourcePathParts);

            var rankedDestPaths = new (string destination, int rank)[destinationPaths.Count];

            for (int i = 0; i < destinationPaths.Count; i++)
            {
                var destPath = destinationPaths[i];

                // Divide the destination path in directories and filenames
                var destPathParts = destPath.Split(Path.DirectorySeparatorChar);
                Array.Reverse(destPathParts);

                // Rank the path based on how many parts match
                var currentRank = 0;
                for (int pathPartIndex = 0; pathPartIndex < sourcePathParts.Length && pathPartIndex < destPathParts.Length; pathPartIndex++)
                {
                    var sourcePart = sourcePathParts[pathPartIndex];
                    var destPart = destPathParts[pathPartIndex];

                    if (string.Compare(sourcePart, destPart, ignoreCase: true) == 0)
                        currentRank--;
                    else
                        currentRank++;
                }

                rankedDestPaths[i] = (destPath, currentRank);
            }

            // Order the paths by similarity (rank). More similar paths will become before more dissimilar
            return rankedDestPaths.OrderBy(p => p.rank).Select(p => p.destination);
        }

        //
        // Tries to match a source file to one destination file out of a candidate list based
        // on its size and / or contents hash.
        //
        bool HashMatchSourceFileToFileList(string sourceFileName, string sourceDirectory, string sourceFilePath,
                                           string destDirectory, MultipleFileDestination destFiles, IEnumerable<string> rankedDestFiles)
        {
            var sourcePath = Path.Combine(sourceDirectory, sourceFilePath);

            using var sourceHandle = File.OpenHandle(sourcePath);
            var sourceLength = RandomAccess.GetLength(sourceHandle);

            int? sourceHash = null;

            // We assume the list is ranked by similarity, so the first match would be the most similar
            foreach (var candidate in rankedDestFiles)
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
                    return true;
                }
            }

            // None matches
            return false;
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
                if (destFile is SingleFileDestination single)
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
                else if (destFile is MultipleFileDestination multi)
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
