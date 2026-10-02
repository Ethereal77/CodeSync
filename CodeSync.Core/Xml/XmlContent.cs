using System.Globalization;
using System.Xml.Linq;

using static CodeSync.Core.Xml.XmlHelpers;

namespace CodeSync.Core.Xml;

/// <summary>
///   Serializes and parses versioned CodeSync XML content inventory documents.
/// </summary>
/// <seealso cref="ProfileContent"/>
public static class XmlContent
{
    private const string ContentRoot = "CodeSyncContent";
    private const string SchemaVersion = "1";


    /// <summary>
    ///   Serializes the content inventory that stores file metadata separately from the profile.
    /// </summary>
    /// <param name="content">The content inventory.</param>
    /// <returns>A human-readable XML content inventory.</returns>
    public static string SerializeContent(ProfileContent content)
    {
        ArgumentNullException.ThrowIfNull(content);

        var writer = new FormattedXmlWriter();

        writer.WriteDeclaration();
        writer.WriteComment($"""
                              CodeSync Content v{SchemaVersion}

                                Source:      {content.SourceDirectory}
                                Destination: {content.DestinationDirectory}

                                Created:     {FormatHeaderTimestamp(content.CreatedUtc)}
                                Last update: {FormatHeaderTimestamp(content.LastUpdatedUtc)}
                            """);

        writer.WriteStartElement(ContentRoot, [("schemaVersion", SchemaVersion)]);
        writer.WriteBlankLine();

        writer.WriteElement("SourceDirectory", content.SourceDirectory, indent: 2);
        writer.WriteElement("DestinationDirectory", content.DestinationDirectory, indent: 2);
        writer.WriteBlankLine();

        writer.WriteElement("CreatedUtc", FormatTimestamp(content.CreatedUtc), indent: 2);
        writer.WriteElement("LastUpdatedUtc", FormatTimestamp(content.LastUpdatedUtc), indent: 2);
        writer.WriteBlankLine();

        WriteSnapshotSection(writer,
                             sectionName: "SourceFiles",
                             description: "The following file metadata was discovered in the source directory.",
                             content.SourceFiles);
        writer.WriteBlankLine();

        WriteSnapshotSection(writer,
                             sectionName: "DestinationFiles",
                             description: "The following file metadata was discovered in the destination directory.",
                             content.DestinationFiles);
        writer.WriteBlankLine();

        writer.WriteEndElement(ContentRoot);

        return writer.ToString();

        //
        // Writes a section of the XML containing file snapshots.
        //
        static void WriteSnapshotSection(FormattedXmlWriter writer,
                                         string sectionName,
                                         string description,
                                         IEnumerable<FileSnapshot> snapshots)
        {
            writer.WriteStartElement(sectionName, attributes: [], indent: 2);

            writer.WriteComment(description, indent: 4);

            bool first = true;

            foreach (var snapshot in snapshots.OrderBy(file => file.Path, StringComparer.Ordinal))
            {
                if (!first)
                    writer.WriteBlankLine();

                writer.WriteEmptyElement("File",
                                         [
                                             ("Path", snapshot.Path),
                                             ("Size", snapshot.Size.ToString(CultureInfo.InvariantCulture)),
                                             ("LastWriteTimeUtc", FormatTimestamp(snapshot.LastWriteTimeUtc)),
                                             ("Sha256", snapshot.Sha256)
                                         ],
                                         indent: 4);
                first = false;
            }

            writer.WriteBlankLine();
            writer.WriteEndElement(sectionName, indent: 2);
        }
    }

    /// <summary>
    ///   Deserializes a content inventory.
    /// </summary>
    /// <param name="xml">The XML content inventory to parse.</param>
    /// <returns>The parsed content inventory.</returns>
    public static ProfileContent DeserializeContent(string xml)
    {
        var errors = new List<string>();

        var root = LoadRootFromXmlDocumentString(xml, ContentRoot, SchemaVersion);

        var sourceDirectory = TryRequiredRoot(root, "SourceDirectory", errors);
        var destinationDirectory = TryRequiredRoot(root, "DestinationDirectory", errors);
        var createdUtc = TryRequiredUtc(root, "CreatedUtc", errors);
        var lastUpdatedUtc = TryRequiredUtc(root, "LastUpdatedUtc", errors);

        var sourceFiles = ParseSnapshots(root, "SourceFiles", errors);
        var destinationFiles = ParseSnapshots(root, "DestinationFiles", errors);

        if (errors.Count > 0)
            throw new ProfileLoadException(errors);

        return new ProfileContent(sourceDirectory!,
                                  destinationDirectory!,
                                  createdUtc!.Value,
                                  lastUpdatedUtc!.Value,
                                  sourceFiles,
                                  destinationFiles);

        //
        // Parses a collection of file snapshots from the specified XML section.
        //
        static IReadOnlyList<FileSnapshot> ParseSnapshots(XElement root, string sectionName, List<string> errors)
        {
            var snapshots = new List<FileSnapshot>();
            var paths = new HashSet<string>(PathUtils.PathComparer);

            foreach (var element in root.Element(sectionName)?.Elements("File") ?? [])
            {
                var snapshot = TryParseElement(element, errors, () => ParseSnapshot(element));
                if (snapshot is null)
                    continue;

                if (!paths.Add(snapshot.Path))
                {
                    var message = FormatElementError(
                        element,
                        $"The {sectionName} section contains duplicate path '{snapshot.Path}'.");

                    errors.Add(message);
                }

                snapshots.Add(snapshot);
            }

            return snapshots;
        }

        //
        // Parses an individual file snapshot from its XML representation.
        //
        static FileSnapshot ParseSnapshot(XElement element)
        {
            var sizeXml = RequiredAttribute(element, "Size");

            if (!long.TryParse(sizeXml, NumberStyles.None, CultureInfo.InvariantCulture, out var size))
                throw new InvalidDataException("A file size must be an invariant integer.");

            return new FileSnapshot(
                RequiredAttribute(element, "Path"),
                size,
                RequiredUtcAttribute(element, "LastWriteTimeUtc"),
                RequiredAttribute(element, "Sha256"));
        }
    }
}
