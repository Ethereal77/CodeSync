namespace CodeSync.Xml;

using System.Xml;

using CodeSync.Utils;

using static XmlSyncFormat;

/// <summary>
///   A XML CodeSync file where the results of the analysis made by <see cref="FileAnalyzer"/> or <see cref="FileUpdater"/>
///   can be written.
/// </summary>
sealed class XmlSyncOutputFile : IDisposable
{
    private readonly XmlWriter _xml;
    private readonly TextWriter _text;

    private bool startedCopyEntries = false;

    /// <summary>
    ///   Initializes a new instance of the <see cref="XmlSyncOutputFile"/> class.
    /// </summary>
    /// <param name="path">The path of the XML file to create.</param>
    /// <param name="sourceDir">The directory where the source repository is located.</param>
    /// <param name="destDir">The directory where the destination repository is located.</param>
    public XmlSyncOutputFile(string path, string sourceDir, string destDir)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        _text = new StreamWriter(path, append: false, DefaultEncoding);

        var xmlSettings = new XmlWriterSettings
        {
            Indent = true,
            IndentChars = DefaultIndent,
            Encoding = DefaultEncoding,
            NewLineChars = DefaultNewLine
        };

        _xml = XmlWriter.Create(_text, xmlSettings);

        _xml.WriteStartDocument();
        _xml.WriteStartElement(RootTag);

        _xml.WriteElementString(SourceRepositoryDirectoryTag, sourceDir);
        _xml.WriteElementString(DestinationRepositoryDirectoryTag, destDir);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        EndXmlOutput();

        _xml.Dispose();
        _text.Dispose();
    }


    //
    // Ends and closes the output XML file where the analysis is to be written.
    //
    private void EndXmlOutput()
    {
        _xml.WriteEndElement();
        _xml.WriteEndDocument();

        _xml.Close();
        _xml.Flush();
    }

    /// <summary>
    ///   Writes to the output XML a match between a source file and a destination file.
    /// </summary>
    /// <param name="sourceFilePath">The source file path.</param>
    /// <param name="destFilePath">The destination file path.</param>
    public void WriteMatch(string sourceFilePath, string destFilePath)
    {
        if (!startedCopyEntries)
            StartCurrentMatchesSection();

        _xml.WriteStartElement(CopyFileEntryTag);
        _xml.WriteElementString(FileEntrySourceTag, sourceFilePath);
        _xml.WriteElementString(FileEntryDestinationTag, destFilePath);
        _xml.WriteEndElement();
    }

    //
    // Writes a header that separates the XML in sections.
    //
    private void WriteSectionHeader(string headerText)
    {
        _xml.Flush();
        _text.WriteLine();

        _xml.WriteComment(headerText);

        _xml.Flush();
        _text.WriteLine();
    }

    /// <summary>
    ///   Starts the section where the current matches are to be written.
    /// </summary>
    public void StartCurrentMatchesSection()
    {
        WriteSectionHeader("""

            The following entries are new matches detected between files in the source and destination repositories.

        """);

        startedCopyEntries = true;
    }

    /// <summary>
    ///   Writes to the output XML a collection of matches imported from another XML file.
    /// </summary>
    /// <param name="fileCopyEntries">The collection of entries to copy.</param>
    public void WritePreviousMatches(IEnumerable<CopyFileEntry> fileCopyEntries)
    {
        if (!fileCopyEntries.Any())
            return;

        WriteSectionHeader("""

            The following files to copy were copied from a previous CodeSync XML file that was updated.

        """);

        foreach (var entry in fileCopyEntries)
        {
            _xml.WriteStartElement(CopyFileEntryTag);
            _xml.WriteElementString(FileEntrySourceTag, entry.SourcePath);
            _xml.WriteElementString(FileEntryDestinationTag, entry.DestPath);
            _xml.WriteEndElement();
        }
    }

    /// <summary>
    ///   Writes to the output XML a collection of partial entries imported from another XML file.
    /// </summary>
    /// <param name="partialEntries">The collection of partial entries to copy.</param>
    public void WritePreviousPartialEntries(IEnumerable<CopyFilePartialEntry> partialEntries)
    {
        if (!partialEntries.Any())
            return;

        WriteSectionHeader("""

            The following entries were partially specified in a previous CodeSync XML file that was updated.
            Some reference a missing source file, some reference a missing destination.

        """);

        foreach (var entry in partialEntries)
        {
            _xml.WriteStartElement(IgnoreFileEntryTag);
            _xml.WriteElementString(FileEntrySourceTag, entry.SourcePath ?? string.Empty);
            _xml.WriteElementString(FileEntryDestinationTag, entry.DestPath ?? string.Empty);
            _xml.WriteEndElement();
        }
    }

    /// <summary>
    ///   Writes to the output XML a collection of files to ignore imported from another XML file.
    /// </summary>
    /// <param name="sourceFilesToIgnore">The collection of source files to ignore.</param>
    public void WritePreviousSourceFilesToIgnore(IEnumerable<IgnoreSourceFileEntry> sourceFilesToIgnore)
    {
        if (!sourceFilesToIgnore.Any())
            return;

        WriteSectionHeader("""

            The following entries are files to ignore in the source repository.
            They were imported from a previous CodeSync XML file that was updated.

        """);

        foreach (var entry in sourceFilesToIgnore)
        {
            _xml.WriteStartElement(IgnoreFileEntryTag);
            _xml.WriteElementString(FileEntrySourceTag, entry.SourcePath);
            _xml.WriteEndElement();
        }
    }

    /// <summary>
    ///   Writes to the output XML a collection of files to ignore imported from another XML file.
    /// </summary>
    /// <param name="destFilesToIgnore">The collection of destination files to ignore.</param>
    public void WritePreviousDestinationFilesToIgnore(IEnumerable<IgnoreDestFileEntry> destFilesToIgnore)
    {
        if (!destFilesToIgnore.Any())
            return;

        WriteSectionHeader("""

            The following entries are files to ignore in the destination repository.
            They were imported from a previous CodeSync XML file that was updated.

        """);

        foreach (var entry in destFilesToIgnore)
        {
            _xml.WriteStartElement(IgnoreFileEntryTag);
            _xml.WriteElementString(FileEntryDestinationTag, entry.DestPath);
            _xml.WriteEndElement();
        }
    }

    /// <summary>
    ///   Writes to the output XML the files in the source repository that have no matching file
    ///   in the destination repository (i.e. they got deleted?).
    /// </summary>
    /// <param name="orphanSourceFiles">The collection of source files not found in the destination directory.</param>
    public void WriteSourceOrphanFiles(IEnumerable<string> orphanSourceFiles)
    {
        if (!orphanSourceFiles.Any())
            return;

        WriteSectionHeader($"""

            The following files are in the source, but have no matching file in the destination.

            Maybe they got deleted or renamed.

            You can change the <{IgnoreFileEntryTag}> tag to a <{CopyFileEntryTag}> tag and specify a valid destination to copy the file.

        """);

        foreach (var filePath in orphanSourceFiles)
        {
            _xml.WriteStartElement(IgnoreFileEntryTag);
            _xml.WriteElementString(FileEntrySourceTag, filePath);
            _xml.WriteComment($"<{FileEntryDestinationTag}></{FileEntryDestinationTag}>");
            _xml.WriteEndElement();
        }
    }

    /// <summary>
    ///   Writes to the output XML the files in the source repository that have many matching files
    ///   in the destination repository, so the destination is ambiguous.
    /// </summary>
    /// <param name="ambiguousFiles">The collection of source files and their associated multiple candidates in the destination directory.</param>
    /// <param name="ambiguousFilesCount">When this method returns, contains the number of source files that have more than one candidate destination file.</param>
    public void WriteSourceFilesWithAmbiguousDestination(IEnumerable<(string, MultipleFileDestination)> ambiguousFiles,
                                                         out int ambiguousFilesCount)
    {
        ambiguousFilesCount = 0;

        if (ambiguousFiles.TryGetNonEnumeratedCount(out int count) && count == 0 ||
            !ambiguousFiles.Any())
            return;

        WriteSectionHeader($"""

            The following files are in the source, but have multiple candidates in the destination.

            You can change the <{IgnoreFileEntryTag}> tag to a <{CopyFileEntryTag}> tag and select just one of the possible
            destinations (or duplicate the entry for each) to copy the file.

        """);

        foreach (var (filePath, candidates) in ambiguousFiles)
        {
            _xml.WriteStartElement(IgnoreFileEntryTag);
            _xml.WriteElementString(FileEntrySourceTag, filePath);

            foreach (var destPath in candidates)
                _xml.WriteComment($"<{FileEntryDestinationTag}>{destPath}</{FileEntryDestinationTag}>");

            _xml.WriteEndElement();

            ambiguousFilesCount++;
        }
    }

    /// <summary>
    ///   Writes to the output XML the files in the source repository that had many matching files
    ///   in the destination repository, but by discarding the other ones, are now just with one
    ///   potentially incorrect destination left.
    /// </summary>
    /// <param name="potentiallyIncorrectFiles">The collection of source files and their associated candidate in the destination directory.</param>
    /// <param name="potentiallyIncorrectFilesCount">When this method returns, contains the number of source files that have one potentially incorrect candidate destination file.</param>
    public void WritesSourceFilesWithOneAmbiguousDestinationLeft(IEnumerable<(string, string)>? potentiallyIncorrectFiles,
                                                                 out int potentiallyIncorrectFilesCount)
    {
        potentiallyIncorrectFilesCount = 0;

        if (potentiallyIncorrectFiles is null)
            return;
        if (potentiallyIncorrectFiles.TryGetNonEnumeratedCount(out int count) && count == 0 ||
            !potentiallyIncorrectFiles.Any())
            return;

        WriteSectionHeader($"""

            The following files are in the source, but have a single candidate destination that
            is left after having discarded the others it had.

            The result can be incorrect.

            You can change the <{CopyFileEntryTag}> tag to an <{IgnoreFileEntryTag}> tag if the entry is incorrect to ignore it.

        """);

        foreach (var (filePath, candidate) in potentiallyIncorrectFiles)
        {
            _xml.WriteStartElement(CopyFileEntryTag);
            _xml.WriteElementString(FileEntrySourceTag, filePath);
            _xml.WriteElementString(FileEntryDestinationTag, candidate);
            _xml.WriteEndElement();

            potentiallyIncorrectFilesCount++;
        }
    }

    /// <summary>
    ///    Writes to the output XML the files in the destination repository that have no matching file
    ///    in the source repository (i.e. they are new?).
    /// </summary>
    /// <param name="orphanDestinationFiles">The collection of destination files not found in the source directory.</param>
    /// <param name="orphanCount">When this method returns, contains the number of orphan files found in the destination directory.</param>
    public void WriteDestinationOrphanFiles(IEnumerable<IFileDestination> orphanDestinationFiles, out int orphanCount)
    {
        orphanCount = 0;

        if (!orphanDestinationFiles.Any())
            return;

        WriteSectionHeader("""

            The following files are in the destination, but not in the source.
            Also, they could not be matched by size or content.

            Maybe they are new additions, or renamed files.

            You can check these files against the source files that have no destination to
            complete those if needed.

        """);

        foreach (var destFileSource in orphanDestinationFiles)
        {
            if (destFileSource is SingleFileDestination singleFile)
            {
                _xml.WriteComment(singleFile.FilePath);
                orphanCount++;
            }
            else if (destFileSource is MultipleFileDestination multiFile)
            {
                orphanCount += multiFile.Count;

                foreach (var fileName in multiFile)
                    _xml.WriteComment(fileName);
            }
        }
    }
}
