using System.Globalization;
using System.Xml.Linq;

namespace CodeSync.Core;

/// <summary>
///   Serializes and parses versioned CodeSync XML documents.
/// </summary>
public static class XmlCodecs
{
    private const string SchemaVersion = "1";

    private static readonly ConflictKind[] ConflictDisplayOrder =
    [
        ConflictKind.DestinationWithoutSource,
        ConflictKind.SourceWithoutDestination,
        ConflictKind.AmbiguousMatch,
        ConflictKind.MissingMappedFile,
        ConflictKind.DuplicateMapping
    ];

    private const string ProfileRoot = "CodeSyncProfile";
    private const string ContentRoot = "CodeSyncContent";
    private const string ConflictsRoot = "CodeSyncConflicts";
    private const string SkippedRoot = "CodeSyncSkipped";


    #region Serialization / Deserialization helpers

    /// <summary>
    ///   Converts an XML element to an XML string.
    /// </summary>
    /// <param name="root">The root XML element to convert.</param>
    /// <param name="header">The header comment to include in the XML document.</param>
    /// <returns>An XML string representing the XML element.</returns>
    private static string ToXmlDocumentString(XElement root, string header)
    {
        var xmlDeclaration = new XDeclaration("1.0", "utf-8", standalone: null);
        var xmlDocument = new XDocument(xmlDeclaration, new XComment($"\n{header}\n"), root);

        return xmlDocument.ToString(SaveOptions.None);
    }

    /// <summary>
    ///   Loads the root XML element from an XML string and validates it
    ///   against the expected root name and schema version.
    /// </summary>
    /// <param name="xml">The XML string to parse.</param>
    /// <param name="expectedRoot">The expected root element name.</param>
    /// <returns>The root XML element.</returns>
    /// <exception cref="ArgumentException">
    ///   Thrown if the <paramref name="xml"/> is <see langword="null"/>, empty, or consists only of whitespace.
    /// </exception>
    /// <exception cref="InvalidDataException">
    ///   Thrown if the XML is invalid or does not match the expected root and schema version.
    /// </exception>
    private static XElement LoadRootFromXmlDocumentString(string xml, string expectedRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);

        var root = XDocument.Parse(xml, LoadOptions.PreserveWhitespace).Root
            ?? throw new InvalidDataException("The XML document has no root element.");

        if (root.Name.LocalName != expectedRoot ||
            (string?) root.Attribute("schemaVersion") != SchemaVersion)
        {
            throw new InvalidDataException($"The document is not a CodeSync {expectedRoot} schema version {SchemaVersion}.");
        }

        return root;
    }

    /// <summary>
    ///   Helper method to retrieve the required text content of an XML element.
    /// </summary>
    /// <param name="root">The XML element containing the required text.</param>
    /// <param name="name">The name of the child element whose text content is required.</param>
    /// <returns>The text content of the specified child element.</returns>
    /// <exception cref="InvalidDataException">
    ///   Thrown if the specified child element is missing or its text content is <see langword="null"/>,
    ///   empty, or consists only of whitespace.
    /// </exception>
    private static string RequiredText(XElement root, string name)
    {
        var value = (string?) root.Element(name);

        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidDataException($"The XML document requires '{name}'.")
            : value;
    }

    /// <summary>
    ///   Helper method to retrieve the required attribute value of an XML element.
    /// </summary>
    /// <param name="element">The XML element containing the required attribute.</param>
    /// <param name="name">The name of the required attribute.</param>
    /// <returns>The value of the specified attribute.</returns>
    /// <exception cref="InvalidDataException">
    ///   Thrown if the specified attribute is missing or its value is <see langword="null"/>,
    ///   empty, or consists only of whitespace.
    /// </exception>
    private static string RequiredAttribute(XElement element, string name)
    {
        var value = (string?) element.Attribute(name);

        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidDataException($"The XML element '{element.Name.LocalName}' requires '{name}'.")
            : value;
    }

    /// <summary>
    ///   Helper method to retrieve the required UTC timestamp from an XML element's text content.
    /// </summary>
    /// <param name="root">The XML element containing the required timestamp.</param>
    /// <param name="name">The name of the child element whose text content is required to be a UTC timestamp.</param>
    /// <returns>The UTC timestamp represented by the specified child element's text content.</returns>
    /// <exception cref="InvalidDataException">
    ///   Thrown if the specified child element is missing, its text content is <see langword="null"/>,
    ///   empty, consists only of whitespace, or does not represent a valid UTC timestamp.
    /// </exception>
    private static DateTimeOffset RequiredUtc(XElement root, string name)
    {
        var value = RequiredText(root, name);

        var valid = DateTimeOffset.TryParse(value,
                                            CultureInfo.InvariantCulture,
                                            DateTimeStyles.RoundtripKind,
                                            out var result);

        return !valid
            ? throw new InvalidDataException($"The XML element '{name}' must contain a UTC round-trip timestamp.")
            : result;
    }

    /// <summary>
    ///   Helper method to retrieve the required UTC timestamp from an XML element's attribute.
    /// </summary>
    /// <param name="element">The XML element containing the required attribute.</param>
    /// <param name="name">The name of the attribute whose value is required to be a UTC timestamp.</param>
    /// <returns>The UTC timestamp represented by the specified attribute's value.</returns>
    /// <exception cref="InvalidDataException">
    ///   Thrown if the specified attribute is missing, its value is <see langword="null"/>,
    ///   empty, consists only of whitespace, or does not represent a valid UTC timestamp.
    /// </exception>
    private static DateTimeOffset RequiredUtcAttribute(XElement element, string name)
    {
        var value = RequiredAttribute(element, name);

        var valid = DateTimeOffset.TryParse(value,
                                            CultureInfo.InvariantCulture,
                                            DateTimeStyles.RoundtripKind,
                                            out var result);

        return !valid
            ? throw new InvalidDataException($"The XML attribute '{name}' must contain a UTC round-trip timestamp.")
            : result;
    }

    /// <summary>
    ///   Formats a DateTime value as an ISO 8601 round-trip timestamp.
    /// </summary>
    /// <param name="value">The DateTime value to format.</param>
    /// <returns>The formatted ISO 8601 round-trip timestamp.</returns>
    private static string FormatTimestamp(DateTimeOffset value)
        => value.ToString("O", CultureInfo.InvariantCulture);

    /// <summary>
    ///   Formats a DateTime value for display in the profile header.
    /// </summary>
    /// <param name="value">The DateTime value to format.</param>
    /// <returns>The formatted timestamp for the profile header.</returns>
    private static string FormatHeaderTimestamp(DateTimeOffset value)
        => value.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    #endregion


    #region Profile Serialization and Deserialization

    /// <summary>
    ///   Serializes an editable profile document without file metadata.
    /// </summary>
    /// <param name="document">The editable profile document.</param>
    /// <returns>A human-readable XML profile.</returns>
    public static string SerializeProfile(ProfileDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var profile = document.Profile;

        var root = new XElement(ProfileRoot,
            new XAttribute("schemaVersion", SchemaVersion),
            new XElement("SourceDirectory", profile.SourceDirectory),
            new XElement("DestinationDirectory", profile.DestinationDirectory),
            new XElement("CreatedUtc", FormatTimestamp(document.CreatedUtc)),
            new XElement("LastUpdatedUtc", FormatTimestamp(document.LastUpdatedUtc)),
            new XElement("DirectoryReferences",
                new XComment("\n    Known source-to-destination directory relationships.\n  "),
                profile.DirectoryReferences.Select(SerializeDirectoryReference)),
            new XElement("FileMappings",
                new XComment("\n    Editable file relationships. A mapping with only Source is intentionally ignored.\n  "),
                profile.FileMappings.Select(SerializeProfileMapping)));

        var header = $"""
                        CodeSync Profile v{SchemaVersion}

                          Source:      {profile.SourceDirectory}
                          Destination: {profile.DestinationDirectory}

                          Created:     {FormatHeaderTimestamp(document.CreatedUtc)}
                          Last update: {FormatHeaderTimestamp(document.LastUpdatedUtc)}
                      """;

        return ToXmlDocumentString(root, header);
    }

    /// <summary>
    ///   Serializes a hydrated profile and its timestamps as an editable profile document.
    /// </summary>
    /// <param name="profile">The hydrated synchronization profile.</param>
    /// <param name="createdUtc">The UTC creation time.</param>
    /// <param name="lastUpdatedUtc">The UTC last-update time.</param>
    /// <returns>A human-readable XML profile.</returns>
    public static string SerializeProfile(SyncProfile profile, DateTime createdUtc, DateTime lastUpdatedUtc)
    {
        ArgumentNullException.ThrowIfNull(profile);

        return SerializeProfile(new ProfileDocument(ProfileDefinition.FromProfile(profile), createdUtc, lastUpdatedUtc));
    }

    /// <summary>
    ///   Deserializes an editable profile document.
    /// </summary>
    /// <param name="xml">The XML profile to parse.</param>
    /// <returns>The parsed profile definition and timestamps.</returns>
    public static ProfileDocument DeserializeProfile(string xml)
    {
        var root = LoadRootFromXmlDocumentString(xml, ProfileRoot);

        var sourceDirectory = RequiredText(root, "SourceDirectory");
        var destinationDirectory = RequiredText(root, "DestinationDirectory");

        var directoryReferences = root.Element("DirectoryReferences")?
            .Elements("Directory")
            .Select(ParseDirectoryReference) ?? [];

        var fileMappings = root.Element("FileMappings")?
            .Elements("FileMapping")
            .Select(ParseProfileMapping) ?? [];

        var profile = new ProfileDefinition(sourceDirectory,
                                             destinationDirectory,
                                             directoryReferences,
                                             fileMappings);

        return new ProfileDocument(profile,
                                   RequiredUtc(root, "CreatedUtc"),
                                   RequiredUtc(root, "LastUpdatedUtc"));
    }

    #endregion

    #region Content Serialization and Deserialization

    /// <summary>
    ///   Serializes the content inventory that stores file metadata separately from the profile.
    /// </summary>
    /// <param name="content">The content inventory.</param>
    /// <returns>A human-readable XML content inventory.</returns>
    public static string SerializeContent(ProfileContent content)
    {
        ArgumentNullException.ThrowIfNull(content);

        var root = new XElement(ContentRoot,
            new XAttribute("schemaVersion", SchemaVersion),
            new XElement("SourceDirectory", content.SourceDirectory),
            new XElement("DestinationDirectory", content.DestinationDirectory),
            new XElement("CreatedUtc", FormatTimestamp(content.CreatedUtc)),
            new XElement("LastUpdatedUtc", FormatTimestamp(content.LastUpdatedUtc)),
            new XElement("SourceFiles",
                new XComment("\n    File metadata discovered in the source directory.\n  "),
                content.SourceFiles.OrderBy(file => file.Path, StringComparer.Ordinal).Select(SerializeSnapshot)),
            new XElement("DestinationFiles",
                new XComment("\n    File metadata discovered in the destination directory.\n  "),
                content.DestinationFiles.OrderBy(file => file.Path, StringComparer.Ordinal).Select(SerializeSnapshot)));

        var header = $"""
                        CodeSync Content v{SchemaVersion}

                          Source:      {content.SourceDirectory}
                          Destination: {content.DestinationDirectory}

                          Created:     {FormatHeaderTimestamp(content.CreatedUtc)}
                          Last update: {FormatHeaderTimestamp(content.LastUpdatedUtc)}
                      """;

        return ToXmlDocumentString(root, header);
    }

    /// <summary>
    ///   Deserializes a content inventory.
    /// </summary>
    /// <param name="xml">The XML content inventory to parse.</param>
    /// <returns>The parsed content inventory.</returns>
    public static ProfileContent DeserializeContent(string xml)
    {
        var root = LoadRootFromXmlDocumentString(xml, ContentRoot);

        var sourceFiles = root.Element("SourceFiles")?
            .Elements("File")
            .Select(ParseSnapshot) ?? [];

        var destinationFiles = root.Element("DestinationFiles")?
            .Elements("File")
            .Select(ParseSnapshot) ?? [];

        return new ProfileContent(
            RequiredText(root, "SourceDirectory"),
            RequiredText(root, "DestinationDirectory"),
            RequiredUtc(root, "CreatedUtc"),
            RequiredUtc(root, "LastUpdatedUtc"),
            sourceFiles,
            destinationFiles);
    }

    #endregion

    #region ConflictSet Serialization and Deserialization

    /// <summary>
    ///   Serializes conflicts as path-only, human-readable sections.
    /// </summary>
    /// <param name="conflictSet">The conflicts to serialize.</param>
    /// <returns>A human-readable XML conflict report.</returns>
    public static string SerializeConflicts(ConflictSet conflictSet)
    {
        ArgumentNullException.ThrowIfNull(conflictSet);

        var document = new ConflictDocument(
            conflictSet.SourceDirectory,
            conflictSet.DestinationDirectory,
            conflictSet.Conflicts.Select(ConflictEntry.FromConflict));

        return SerializeConflicts(document);
    }

    /// <summary>
    ///   Serializes a path-only conflict document.
    /// </summary>
    /// <param name="document">The conflict document to serialize.</param>
    /// <returns>A human-readable XML conflict report.</returns>
    public static string SerializeConflicts(ConflictDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var root = new XElement(ConflictsRoot,
            new XAttribute("schemaVersion", SchemaVersion),
            new XElement("SourceDirectory", document.SourceDirectory),
            new XElement("DestinationDirectory", document.DestinationDirectory),
            ConflictSection(document, ConflictKind.DestinationWithoutSource),
            ConflictSection(document, ConflictKind.SourceWithoutDestination),
            ConflictSection(document, ConflictKind.AmbiguousMatch),
            ConflictSection(document, ConflictKind.MissingMappedFile),
            ConflictSection(document, ConflictKind.DuplicateMapping));

        var header = $"""
                        CodeSync Conflicts v{SchemaVersion}

                          Source:      {document.SourceDirectory}
                          Destination: {document.DestinationDirectory}

                          Review the sections below and update the profile before copying.
                      """;

        return ToXmlDocumentString(root, header);

        //
        // Serializes a conflict section for the specified conflict kind.
        //
        static XElement ConflictSection(ConflictDocument source, ConflictKind kind)
        {
            var entries = source.Conflicts
                .Where(conflict => conflict.Kind == kind)
                .OrderBy(conflict => conflict.SourcePath ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(conflict => conflict.DestinationPath ?? string.Empty, StringComparer.Ordinal)
                .Select(SerializeConflictEntry);

            return new XElement(kind.ToString(),
                new XComment($"\n    {ConflictDescription(kind)}\n  "),
                entries);
        }

        //
        // Serializes an individual conflict entry.
        //
        static XElement SerializeConflictEntry(ConflictEntry entry)
        {
            var attributes = new List<object>(capacity: 2);

            if (entry.SourcePath is not null)
                attributes.Add(new XAttribute("Source", entry.SourcePath));
            if (entry.DestinationPath is not null)
                attributes.Add(new XAttribute("Destination", entry.DestinationPath));

            return new XElement("FileMapping", attributes);
        }
    }

    /// <summary>
    ///   Deserializes a path-only conflict document.
    /// </summary>
    /// <param name="xml">The XML conflict report to parse.</param>
    /// <returns>The parsed conflict document.</returns>
    public static ConflictDocument DeserializeConflicts(string xml)
    {
        var root = LoadRootFromXmlDocumentString(xml, ConflictsRoot);

        var conflicts = ConflictDisplayOrder
            .SelectMany(kind => ParseConflictSection(root, kind))
            .ToArray();

        return new ConflictDocument(RequiredText(root, "SourceDirectory"),
                                    RequiredText(root, "DestinationDirectory"),
                                    conflicts);
    }

    #endregion

    #region Skipped files Serialization and Deserialization

    /// <summary>
    ///   Serializes a collection of skipped source paths.
    /// </summary>
    /// <param name="sourcePaths">The skipped source paths.</param>
    /// <returns>A human-readable XML skipped-file report.</returns>
    public static string SerializeSkipped(IEnumerable<string> sourcePaths)
    {
        ArgumentNullException.ThrowIfNull(sourcePaths);

        var root = new XElement(SkippedRoot,
            new XAttribute("schemaVersion", SchemaVersion),
            new XElement("Files",
                new XComment("\n    Source files that were unchanged during the last copy.\n  "),
                sourcePaths.Select(path => new XElement("File",
                    new XAttribute("Source", PathUtils.NormalizeFilePath(path))))));

        return ToXmlDocumentString(root, $"  CodeSync Skipped Files v{SchemaVersion}");
    }

    /// <summary>
    ///   Deserializes skipped source paths.
    /// </summary>
    /// <param name="xml">The XML skipped-file report to parse.</param>
    /// <returns>The normalized skipped source paths.</returns>
    public static IReadOnlyList<string> DeserializeSkipped(string xml)
    {
        var root = LoadRootFromXmlDocumentString(xml, SkippedRoot);

        var skippedFilesXml = root.Element("Files");

        return skippedFilesXml?.Elements("File")
            .Select(element => RequiredAttribute(element, "Source"))
            .Select(PathUtils.NormalizeFilePath)
            .ToArray() ?? [];
    }

    #endregion


    #region Mapping and snapshot helpers

    /// <summary>
    ///   Serializes an individual directory reference into its XML representation.
    /// </summary>
    /// <param name="reference">The directory reference to serialize.</param>
    /// <returns>The XML element representing the directory reference.</returns>
    private static XElement SerializeDirectoryReference(DirectoryReference reference)
    {
        return new XElement("Directory",
            new XAttribute("Source", reference.SourcePath),
            new XAttribute("Destination", reference.DestinationPath));
    }

    /// <summary>
    ///   Parses an individual directory reference from its XML representation.
    /// </summary>
    /// <param name="element">The XML element representing the directory reference.</param>
    /// <returns>The parsed <see cref="DirectoryReference"/> instance.</returns>
    private static DirectoryReference ParseDirectoryReference(XElement element)
    {
        return new DirectoryReference(
            RequiredAttribute(element, "Source"),
            RequiredAttribute(element, "Destination"));
    }

    /// <summary>
    ///   Serializes an individual profile mapping into its XML representation.
    /// </summary>
    /// <param name="mapping">The profile mapping to serialize.</param>
    /// <returns>The XML element representing the profile mapping.</returns>
    private static XElement SerializeProfileMapping(ProfileMapping mapping)
    {
        var attributes = new List<object>(capacity: 2);

        if (mapping.SourcePath is not null)
            attributes.Add(new XAttribute("Source", mapping.SourcePath));
        if (mapping.DestinationPath is not null)
            attributes.Add(new XAttribute("Destination", mapping.DestinationPath));

        return new XElement("FileMapping", attributes);
    }

    /// <summary>
    ///   Parses an individual profile mapping from its XML representation.
    /// </summary>
    /// <param name="element">The XML element representing the profile mapping.</param>
    /// <returns>The parsed <see cref="ProfileMapping"/> instance.</returns>
    private static ProfileMapping ParseProfileMapping(XElement element)
    {
        return new ProfileMapping(
            OptionalAttribute(element, "Source"),
            OptionalAttribute(element, "Destination"));
    }

    /// <summary>
    ///   Parses the conflict section of the XML document for a specific conflict kind.
    /// </summary>
    /// <param name="root">The root XML element containing the conflict section.</param>
    /// <param name="kind">The kind of conflict to parse.</param>
    /// <returns>
    ///   A collection of <see cref="ConflictEntry"/> instances representing the conflicts of the specified kind.
    /// </returns>
    private static IEnumerable<ConflictEntry> ParseConflictSection(XElement root, ConflictKind kind)
    {
        return root.Element(kind.ToString())?.Elements("FileMapping")
            .Select(element => new ConflictEntry(
                kind,
                OptionalAttribute(element, "Source"),
                OptionalAttribute(element, "Destination"))) ?? [];
    }

    /// <summary>
    ///   Retrieves the value of an optional XML attribute.
    /// </summary>
    /// <param name="element">The XML element containing the attribute.</param>
    /// <param name="name">The name of the attribute.</param>
    /// <returns>The value of the attribute, or <c>null</c> if it is not present or empty.</returns>
    private static string? OptionalAttribute(XElement element, string name)
    {
        var value = (string?) element.Attribute(name);

        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    /// <summary>
    ///   Serializes an individual file snapshot to its XML representation.
    /// </summary>
    /// <param name="snapshot">The file snapshot to serialize.</param>
    /// <returns>The XML element representing the file snapshot.</returns>
    private static XElement SerializeSnapshot(FileSnapshot snapshot)
    {
        return new XElement("File",
            new XAttribute("Path", snapshot.Path),
            new XAttribute("Size", snapshot.Size.ToString(CultureInfo.InvariantCulture)),
            new XAttribute("LastWriteTimeUtc", FormatTimestamp(snapshot.LastWriteTimeUtc)),
            new XAttribute("Sha256", snapshot.Sha256));
    }

    /// <summary>
    ///   Parses an individual file snapshot from its XML representation.
    /// </summary>
    /// <param name="element">The XML element representing the file snapshot.</param>
    /// <returns>The parsed <see cref="FileSnapshot"/> instance.</returns>
    /// <exception cref="InvalidDataException">Thrown if the file size is not a valid invariant integer.</exception>
    private static FileSnapshot ParseSnapshot(XElement element)
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

    /// <summary>
    ///   Returns a human-readable description for the specified conflict kind.
    /// </summary>
    /// <param name="kind">The conflict kind to describe.</param>
    /// <returns>A human-readable description of the conflict kind.</returns>
    private static string ConflictDescription(ConflictKind kind)
        => kind switch
        {
            ConflictKind.DestinationWithoutSource => "Files found only in the destination.",
            ConflictKind.SourceWithoutDestination => "Files found only in the source.",
            ConflictKind.AmbiguousMatch => "Files whose content match is not unique.",
            ConflictKind.MissingMappedFile => "Mappings that refer to files no longer present.",
            ConflictKind.DuplicateMapping => "Mappings that use a source or destination more than once.",

            _ => "Files requiring review."
        };

    #endregion
}
