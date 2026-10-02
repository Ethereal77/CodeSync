using static CodeSync.Core.Xml.XmlHelpers;

namespace CodeSync.Core.Xml;

/// <summary>
///   Serializes and parses versioned CodeSync XML skipped-file reports.
/// </summary>
public static class XmlSkipped
{
    private const string SkippedRoot = "CodeSyncSkipped";
    private const string SchemaVersion = "1";


    /// <summary>
    ///   Serializes a collection of skipped source paths.
    /// </summary>
    /// <param name="sourcePaths">The skipped source paths.</param>
    /// <returns>A human-readable XML skipped-file report.</returns>
    public static string SerializeSkipped(IEnumerable<string> sourcePaths)
    {
        ArgumentNullException.ThrowIfNull(sourcePaths);

        var writer = new FormattedXmlWriter();

        writer.WriteDeclaration();
        writer.WriteComment($"  CodeSync Skipped Files v{SchemaVersion}");
        writer.WriteBlankLine();

        writer.WriteStartElement(SkippedRoot, [("schemaVersion", SchemaVersion)]);
        writer.WriteBlankLine();

        writer.WriteStartElement("Files", [], indent: 2);

        writer.WriteComment("Source files that were unchanged during the last copy.", indent: 4);

        bool first = true;

        foreach (var sourcePath in sourcePaths)
        {
            if (first)
                writer.WriteBlankLine();

            writer.WriteEmptyElement("File",
                                     [("Source", PathUtils.NormalizeFilePath(sourcePath))],
                                     indent: 4);

            first = false;
        }

        writer.WriteBlankLine();
        writer.WriteEndElement("Files", indent: 2);

        writer.WriteBlankLine();
        writer.WriteEndElement(SkippedRoot);

        return writer.ToString();
    }

    /// <summary>
    ///   Deserializes skipped source paths.
    /// </summary>
    /// <param name="xml">The XML skipped-file report to parse.</param>
    /// <returns>The normalized skipped source paths.</returns>
    public static IReadOnlyList<string> DeserializeSkipped(string xml)
    {
        var errors = new List<string>();
        var sourcePaths = new List<string>();

        var root = LoadRootFromXmlDocumentString(xml, SkippedRoot, SchemaVersion);

        var skippedFilesXml = root.Element("Files");

        foreach (var element in skippedFilesXml?.Elements("File") ?? [])
        {
            var sourcePath = TryParseElement(element,
                                             errors,
                                             () => PathUtils.NormalizeFilePath(RequiredAttribute(element, "Source")));

            if (sourcePath is not null)
                sourcePaths.Add(sourcePath);
        }

        if (errors.Count > 0)
            throw new ProfileLoadException(errors);

        return sourcePaths;
    }
}
