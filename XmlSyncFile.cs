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

        _text.WriteLine();
        _xml.WriteComment("""
            
            The following files are in the source, but have no matching file in the destination.
            Maybe they got deleted or renamed.
        
        """);
        _text.WriteLine();

        foreach (var filePath in orphanSourceFiles)
        {
            _xml.WriteStartElement("Copy");
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
    public void OutputFilesInSourceAmbiguousDestination(IEnumerable<(string, MultipleFileSource)> ambiguousFiles)
    {
        if (!ambiguousFiles.Any())
            return;

        _text.WriteLine();
        _xml.WriteComment("""
            
            The following files are in the source, but have multiple candidates in the destination.
        
        """);
        _text.WriteLine();

        foreach (var (filePath, candidates) in ambiguousFiles)
        {
            _xml.WriteStartElement("Copy");
            _xml.WriteElementString("Source", filePath);

            foreach (var destPath in candidates)
                _xml.WriteComment($"<Destination>{destPath}</Destination>");

            _xml.WriteEndElement();
        }
    }

    /// <summary>
    ///    Writes to the output XML the files in the destination repository that have no matching file
    ///    in the source repository (i.e. they are new?).
    /// </summary>
    /// <param name="orphanDestinationFiles">The collection of destination files not found in the source directory.</param>
    /// <param name="orphanCount">When this method returns, contains the number of orphan files found in the destination directory.</param>
    public void WriteDestinationOrphanFiles(IEnumerable<IFileSource> orphanDestinationFiles, out int orphanCount)
    {
        orphanCount = 0;

        if (!orphanDestinationFiles.Any())
            return;

        _text.WriteLine();
        _xml.WriteComment("""
            
            The following files are in the destination, but not in the source.
            Maybe they are new additions, or renamed files.
        
        """);
        _text.WriteLine();
        
        foreach (var destFileSource in orphanDestinationFiles)
        {
            if (destFileSource is SingleFileSource singleFile)
            {
                _xml.WriteComment(singleFile.FilePath);
                orphanCount++;
            }
            else if (destFileSource is MultipleFileSource multiFile)
            {
                orphanCount += multiFile.Count;

                foreach (var fileName in multiFile)
                    _xml.WriteComment(fileName);
            }
        }
    }
}
