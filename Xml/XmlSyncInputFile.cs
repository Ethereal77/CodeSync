namespace CodeSync.Xml;

using System.Xml.Linq;

using static XmlSyncFormat;

/// <summary>
///   A XML CodeSync file from where to read the results of a previous analysis to be updated by <see cref="FileUpdater"/>.
/// </summary>
sealed class XmlSyncInputFile
{
    private readonly IEnumerable<XElement> _copyEntries;
    private readonly IEnumerable<XElement> _ignoreEntries;

    /// <summary>
    ///   Gets the directory where the source repository is located (i.e. where the files to copy are).
    /// </summary>
    public string SourceDirectory { get; private set; }
    /// <summary>
    ///   Gets the directory where the destination repository is located (i.e. the directory to copy the files to).
    /// </summary>
    public string DestinationDirectory { get; private set; }

    /// <summary>
    ///   Gets the files to copy.
    /// </summary>
    /// <remarks>
    ///   The paths of each file to copy (both source and destination) are relative to the base directories
    ///   specified by <see cref="SourceDirectory"/> and <see cref="DestinationDirectory"/>.
    /// </remarks>
    public IEnumerable<CopyFileEntry> FilesToCopy
    {
        get
        {
            foreach (var xmlEntry in _copyEntries)
            {
                var source = (string?) xmlEntry.Element(FileEntrySourceTag);
                var dest = (string?) xmlEntry.Element(FileEntryDestinationTag);

                if (source is null || dest is null)
                    continue;

                yield return new CopyFileEntry(source, dest);
            }
        }
    }

    /// <summary>
    ///   Gets the copy entries in the XML that have no source or destination specified
    ///   (maybe it was a destination file without source, or a source file without destination).
    /// </summary>
    /// <remarks>
    ///   The paths of each entry (both source and destination) are relative to the base directories
    ///   specified by <see cref="SourceDirectory"/> and <see cref="DestinationDirectory"/>.
    /// </remarks>
    public IEnumerable<CopyFilePartialEntry> PartialEntries
    {
        get
        {
            foreach (var xmlEntry in _copyEntries)
            {
                var source = (string?) xmlEntry.Element(FileEntrySourceTag);
                var dest = (string?) xmlEntry.Element(FileEntryDestinationTag);

                if (source is not null && dest is not null)
                    continue;

                yield return new CopyFilePartialEntry(source, dest);
            }
        }
    }

    /// <summary>
    ///   Gets the files to ignore in the <see cref="SourceDirectory"/>.
    /// </summary>
    /// <remarks>
    ///   The paths of each file to ignore are relative to the base directory specified by <see cref="SourceDirectory"/>.
    /// </remarks>
    public IEnumerable<IgnoreSourceFileEntry> IgnoreSourceEntries
    {
        get
        {
            foreach (var xmlEntry in _ignoreEntries)
            {
                var source = (string?) xmlEntry.Element(FileEntrySourceTag);

                if (source is null)
                    continue;

                yield return new IgnoreSourceFileEntry(source);
            }
        }
    }

    /// <summary>
    ///   Gets the files to ignore in the <see cref="DestinationDirectory"/>.
    /// </summary>
    /// <remarks>
    ///   The paths of each file to ignore are relative to the base directory specified by <see cref="DestinationDirectory"/>.
    /// </remarks>
    public IEnumerable<IgnoreDestFileEntry> IgnoreDestinationEntries
    {
        get
        {
            foreach (var xmlEntry in _ignoreEntries)
            {
                var dest = (string?) xmlEntry.Element(FileEntryDestinationTag);

                if (dest is null)
                    continue;

                yield return new IgnoreDestFileEntry(dest);
            }
        }
    }


    /// <summary>
    ///   Initializes a new instance of the <see cref="XmlSyncInputFile"/> class.
    /// </summary>
    /// <param name="path">The path of the XML file to read.</param>
    public XmlSyncInputFile(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        using var xmlFile = File.OpenText(path);
        var xml = XDocument.Load(xmlFile);

        var codeSyncXml = xml.Element(RootTag) ??
                          throw new InvalidDataException("El archivo XML especificado no es un archivo CodeSync válido.");

        var xmlSourceDir = codeSyncXml.Element(SourceRepositoryDirectoryTag) ??
                           throw new InvalidDataException("El archivo XML especificado no especifica el directorio de origen.");

        var xmlDestDir = codeSyncXml.Element(DestinationRepositoryDirectoryTag) ??
                         throw new InvalidDataException("El archivo XML especificado no especifica directorios de destino.");

        SourceDirectory = (string) xmlSourceDir;
        DestinationDirectory = (string) xmlDestDir;

        _copyEntries = codeSyncXml.Elements(CopyFileEntryTag);
        _ignoreEntries = codeSyncXml.Elements(IgnoreFileEntryTag);
    }
}
