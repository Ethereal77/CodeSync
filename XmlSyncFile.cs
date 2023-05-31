using System.Text;
using System.Xml;

sealed class XmlSyncFile : IDisposable
{
    private readonly XmlWriter _xml;
    private readonly TextWriter _text;

    /// <summary>
    ///   Initializes a new instance of the <see cref="XmlSyncFile"/> class.
    /// </summary>
    /// <param name="options">The configuration settings for the analysis.</param>
    public XmlSyncFile(in FileAnalyzerOptions options)
    {
        ArgumentException.ThrowIfNullOrEmpty(options.OutputXmlFilePath);
        
        _text = new StreamWriter(options.OutputXmlFilePath, append: false, Encoding.UTF8);

        var xmlSettings = new XmlWriterSettings
        {
            Indent = true,
            IndentChars = "  ",
            Encoding = Encoding.UTF8,
            NewLineChars = Environment.NewLine
        };

        _xml = XmlWriter.Create(_text, xmlSettings);

        _xml.WriteStartDocument();
        _xml.WriteStartElement("CodeSync");

        _xml.WriteElementString("SourceDirectory", options.SourceDirectory);
        _xml.WriteElementString("DestDirectory", options.DestinationDirectory);

        _xml.Flush();
        _text.WriteLine();
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
        _xml.WriteStartElement("Copy");
        _xml.WriteElementString("Source", sourceFilePath);
        _xml.WriteElementString("Destination", destFilePath);
        _xml.WriteEndElement();
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

        _xml.Flush();

        _text.WriteLine();
        _xml.WriteComment("""
            
            The following files are in the source, but have no matching file in the destination.

            Maybe they got deleted or renamed.

            You can change the <Ignore> tag to a <Copy> tag and specify a valid destination to copy the file.
        
        """);
        _xml.Flush();
        _text.WriteLine();

        foreach (var filePath in orphanSourceFiles)
        {
            _xml.WriteStartElement("Ignore");
            _xml.WriteElementString("Source", filePath);
            _xml.WriteComment($"<Destination></Destination>");
            _xml.WriteEndElement();
        }
    }

    /// <summary>
    ///   Writes to the output XML the files in the source repository that have many matching files
    ///   in the destination repository, so the destination is ambiguous.
    /// </summary>
    /// <param name="ambiguousFiles">The collection of source files and their associated multiple candidates in the destination directory.</param>
    /// <param name="ambiguousFilesCount">When this method returns, contains the number of source files that have more than one candidate destination file.</param>
    public void OutputFilesInSourceAmbiguousDestination(IEnumerable<(string, MultipleFileDestination)> ambiguousFiles,
                                                        out int ambiguousFilesCount)
    {
        ambiguousFilesCount = 0;

        if (ambiguousFiles.TryGetNonEnumeratedCount(out int count) && count == 0 ||
            !ambiguousFiles.Any())
            return;

        _xml.Flush();

        _text.WriteLine();
        _xml.WriteComment("""
            
            The following files are in the source, but have multiple candidates in the destination.

            You can change the <Ignore> tag to a <Copy> tag and select just one of the possible
            destinations (or duplicate the entry for each) to copy the file.
        
        """);
        _xml.Flush();
        _text.WriteLine();

        foreach (var (filePath, candidates) in ambiguousFiles)
        {
            _xml.WriteStartElement("Ignore");
            _xml.WriteElementString("Source", filePath);

            foreach (var destPath in candidates)
                _xml.WriteComment($"<Destination>{destPath}</Destination>");

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
    public void OutputFilesInSourceOneAmbiguousDestinationLeft(IEnumerable<(string, string)>? potentiallyIncorrectFiles,
                                                               out int potentiallyIncorrectFilesCount)
    {
        potentiallyIncorrectFilesCount = 0;

        if (potentiallyIncorrectFiles is null)
            return;
        if (potentiallyIncorrectFiles.TryGetNonEnumeratedCount(out int count) && count == 0 ||
            !potentiallyIncorrectFiles.Any())
            return;

        _xml.Flush();

        _text.WriteLine();
        _xml.WriteComment("""
            
            The following files are in the source, but have a single candidate destination that
            is left after having discarded the others it had.

            The result can be incorrect.

            You can change the <Copy> tag to an <Ignore> tag if the entry is incorrect to ignore it.
        
        """);
        _xml.Flush();
        _text.WriteLine();

        foreach (var (filePath, candidate) in potentiallyIncorrectFiles)
        {
            _xml.WriteStartElement("Copy");
            _xml.WriteElementString("Source", filePath);
            _xml.WriteElementString("Destination", candidate);
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

        _xml.Flush();

        _text.WriteLine();
        _xml.WriteComment("""
            
            The following files are in the destination, but not in the source.

            Maybe they are new additions, or renamed files.

            You can check these files against the source files that have no destination to
            complete those if needed.
        
        """);
        _xml.Flush();
        _text.WriteLine();
        
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
