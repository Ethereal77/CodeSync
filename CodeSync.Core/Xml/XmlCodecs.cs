using System.Globalization;
using System.Xml.Linq;

namespace CodeSync.Core;

/// <summary>
///   Serializes and parses versioned CodeSync XML documents.
/// </summary>
public static class XmlCodecs
{
    private const string SchemaVersion = "1";

    private const string ProfileRoot = "CodeSyncProfile";
    private const string ConflictsRoot = "CodeSyncConflicts";
    private const string SkippedRoot = "CodeSyncSkipped";


    #region Serialization / Deserialization helpers

    /// <summary>
    ///   Converts an XML element to an XML string.
    /// </summary>
    /// <param name="root">The root XML element to convert.</param>
    /// <returns>An XML string representing the XML element.</returns>
    private static string ToXmlDocumentString(XElement root)
    {
        var xmlDeclaration = new XDeclaration("1.0", "utf-8", standalone: null);
        var xmlDocument = new XDocument(xmlDeclaration, root);

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
            (string?)root.Attribute("schemaVersion") != SchemaVersion)
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
        var value = (string?)root.Element(name);

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
        var value = (string?)element.Attribute(name);

        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidDataException($"The XML element '{element.Name.LocalName}' requires '{name}'.")
            : value;
    }

    #endregion


    #region Profile Serialization and Deserialization

    /// <summary>
    ///   Serializes a synchronization profile to an XML string.
    /// </summary>
    /// <param name="profile">The synchronization profile to serialize.</param>
    /// <returns>An XML string representing the synchronization profile.</returns>
    /// <exception cref="ArgumentNullException">
    ///   Thrown if the <paramref name="profile"/> is <see langword="null"/>.
    /// </exception>
    public static string SerializeProfile(SyncProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var root = new XElement(ProfileRoot,
            new XAttribute("schemaVersion", SchemaVersion),
            new XElement("SourceDirectory", profile.SourceDirectory),
            new XElement("DestinationDirectory", profile.DestinationDirectory),
            new XElement("DirectoryReferences", profile.DirectoryReferences.Select(SerializeDirectoryReference)),
            new XElement("FileMappings", profile.FileMappings.Select(SerializeMapping)));

        return ToXmlDocumentString(root);
    }

    /// <summary>
    ///   Deserializes a synchronization profile from an XML string.
    /// </summary>
    /// <param name="xml">The XML string representing the synchronization profile.</param>
    /// <returns>The deserialized synchronization profile.</returns>
    public static SyncProfile DeserializeProfile(string xml)
    {
        var root = LoadRootFromXmlDocumentString(xml, ProfileRoot);

        var sourceDirectory = RequiredText(root, "SourceDirectory");
        var destinationDirectory = RequiredText(root, "DestinationDirectory");

        var directoryReferences = root.Element("DirectoryReferences")?
            .Elements("Directory")
            .Select(ParseDirectoryReference) ?? [];

        var fileMappings = root.Element("FileMappings")?
            .Elements("FileMapping")
            .Select(ParseMapping) ?? [];

        return new SyncProfile(sourceDirectory, destinationDirectory, directoryReferences, fileMappings);
    }

    #endregion

    #region ConflictSet Serialization and Deserialization

    /// <summary>
    ///   Serializes a set of conflicts to an XML string.
    /// </summary>
    /// <param name="conflictSet">The set of conflicts to serialize.</param>
    /// <returns>An XML string representing the set of conflicts.</returns>
    public static string SerializeConflicts(ConflictSet conflictSet)
    {
        ArgumentNullException.ThrowIfNull(conflictSet);

        var conflictsXml = conflictSet.Conflicts.Select(SerializeConflict);

        var root = new XElement(ConflictsRoot,
            new XAttribute("schemaVersion", SchemaVersion),
            new XElement("SourceDirectory", conflictSet.SourceDirectory),
            new XElement("DestinationDirectory", conflictSet.DestinationDirectory),
            new XElement("Conflicts", conflictsXml));

        return ToXmlDocumentString(root);

        //
        // Serialize a single conflict.
        //
        static XElement SerializeConflict(Conflict conflict)
        {
            var conflictChildrenXml = SerializeMappingChildren(conflict.Mapping);

            return new XElement("FileMapping", new XAttribute("kind", conflict.Kind), conflictChildrenXml);
        }
    }

    /// <summary>
    ///   Deserializes a set of conflicts from an XML string.
    /// </summary>
    /// <param name="xml">The XML string representing the set of conflicts.</param>
    /// <returns>The deserialized set of conflicts.</returns>
    /// <exception cref="InvalidDataException">
    ///   Thrown if the XML is missing required attributes or contains unknown conflict kinds.
    /// </exception>
    public static ConflictSet DeserializeConflicts(string xml)
    {
        var root = LoadRootFromXmlDocumentString(xml, ConflictsRoot);
        var conflictsXml = root.Element("Conflicts");

        var conflicts = conflictsXml?.Elements("FileMapping").Select(ParseConflict) ?? [];

        return new ConflictSet(
            RequiredText(root, "SourceDirectory"),
            RequiredText(root, "DestinationDirectory"),
            conflicts);

        //
        // Parse a single conflict.
        //
        static Conflict ParseConflict(XElement element)
        {
            var kindText = (string?) element.Attribute("kind")
                ?? throw new InvalidDataException("A conflict kind is required.");

            if (!Enum.TryParse<ConflictKind>(kindText, ignoreCase: false, out var kind))
                throw new InvalidDataException($"Unknown conflict kind '{kindText}'.");

            return new Conflict(kind, ParseMapping(element));
        }
    }

    #endregion

    #region Skipped files Serialization and Deserialization

    /// <summary>
    ///   Serializes a collection of skipped file paths to an XML string.
    /// </summary>
    /// <param name="sourcePaths">The collection of skipped file paths to serialize.</param>
    /// <returns>An XML string representing the collection of skipped file paths.</returns>
    public static string SerializeSkipped(IEnumerable<string> sourcePaths)
    {
        ArgumentNullException.ThrowIfNull(sourcePaths);

        var root = new XElement(SkippedRoot,
            new XAttribute("schemaVersion", SchemaVersion),
            new XElement("Files", sourcePaths.Select(SerializeSkippedFile)));

        return ToXmlDocumentString(root);

        //
        // Serialize a single skipped file.
        //
        static XElement SerializeSkippedFile(string sourcePath)
        {
            var normalizedSourcePath = PathUtils.NormalizeFilePath(sourcePath);

            return new XElement("File", new XAttribute("source", normalizedSourcePath));
        }
    }

    /// <summary>
    ///   Deserializes an XML string into a collection of skipped file paths.
    /// </summary>
    /// <param name="xml">The XML string representing the collection of skipped file paths.</param>
    /// <returns>A collection of skipped file paths parsed from the XML string.</returns>
    public static IReadOnlyList<string> DeserializeSkipped(string xml)
    {
        var root = LoadRootFromXmlDocumentString(xml, SkippedRoot);
        var skippedFilesXml = root.Element("Files");

        return skippedFilesXml?.Elements("File")
            .Select(ParseSkippedFile)
            .Select(PathUtils.NormalizeFilePath)
            .ToArray() ?? [];

        //
        // Parse a single skipped file.
        //
        static string ParseSkippedFile(XElement element)
        {
            return (string?) element.Attribute("source")
                ?? throw new InvalidDataException("A skipped file source path is required.");
        }
    }

    #endregion

    #region Directory Reference Serialization and Deserialization

    /// <summary>
    ///   Serializes a <see cref="DirectoryReference"/> to an XML element.
    /// </summary>
    /// <param name="reference">The directory reference to serialize.</param>
    /// <returns>An XML element representing the directory reference.</returns>
    private static XElement SerializeDirectoryReference(DirectoryReference reference)
    {
        return new XElement("Directory",
            new XAttribute("source", reference.SourcePath),
            new XAttribute("destination", reference.DestinationPath));
    }

    /// <summary>
    ///   Parses an XML element into a <see cref="DirectoryReference"/>.
    /// </summary>
    /// <param name="element">The XML element representing the directory reference.</param>
    /// <returns>A <see cref="DirectoryReference"/> object parsed from the XML element.</returns>
    private static DirectoryReference ParseDirectoryReference(XElement element)
    {
        return new DirectoryReference(
            RequiredAttribute(element, "source"),
            RequiredAttribute(element, "destination"));
    }

    #endregion

    #region File Mapping Serialization and Deserialization

    /// <summary>
    ///   Serializes a <see cref="FileMapping"/> to an XML element.
    /// </summary>
    /// <param name="mapping">The file mapping to serialize.</param>
    /// <returns>An XML element representing the file mapping.</returns>
    private static XElement SerializeMapping(FileMapping mapping)
    {
        return new XElement("FileMapping", SerializeMappingChildren(mapping));
    }

    /// <summary>
    ///   Serializes the children of a <see cref="FileMapping"/> to an array of XML objects.
    /// </summary>
    /// <param name="mapping">The file mapping whose children are to be serialized.</param>
    /// <returns>An array of XML objects representing the children of the file mapping.</returns>
    private static object[] SerializeMappingChildren(FileMapping mapping)
    {
        var children = new List<object>(capacity: 2);

        if (mapping.Source is not null)
            children.Add(SerializeSnapshot("Source", mapping.Source));
        if (mapping.Destination is not null)
            children.Add(SerializeSnapshot("Destination", mapping.Destination));

        return children.ToArray();
    }

    /// <summary>
    ///   Parses an XML element into a <see cref="FileMapping"/>.
    /// </summary>
    /// <param name="element">The XML element representing the file mapping.</param>
    /// <returns>A <see cref="FileMapping"/> object parsed from the XML element.</returns>
    private static FileMapping ParseMapping(XElement element)
    {
        var source = element.Element("Source") is { } sourceElement
            ? ParseSnapshot(sourceElement)
            : null;

        var destination = element.Element("Destination") is { } destinationElement
            ? ParseSnapshot(destinationElement)
            : null;

        return new FileMapping(source, destination);
    }

    #endregion

    #region File Snapshot Serialization and Deserialization

    /// <summary>
    ///   Serializes a <see cref="FileSnapshot"/> to an XML element.
    /// </summary>
    /// <param name="elementName">The name of the XML element to create.</param>
    /// <param name="snapshot">The file snapshot to serialize.</param>
    /// <returns>An XML element representing the file snapshot.</returns>
    private static XElement SerializeSnapshot(string elementName, FileSnapshot snapshot)
    {
        return new XElement(elementName,
            new XAttribute("path", snapshot.Path),
            new XAttribute("size", snapshot.Size.ToString(CultureInfo.InvariantCulture)),
            new XAttribute("lastWriteTimeUtc", snapshot.LastWriteTimeUtc.ToString("O", CultureInfo.InvariantCulture)),
            new XAttribute("sha256", snapshot.Sha256));
    }

    /// <summary>
    ///   Parses an XML element into a <see cref="FileSnapshot"/>.
    /// </summary>
    /// <param name="element">The XML element representing the file snapshot.</param>
    /// <returns>A <see cref="FileSnapshot"/> object parsed from the XML element.</returns>
    /// <exception cref="InvalidDataException">
    ///   Thrown if the XML element contains invalid file snapshot data.
    /// </exception>
    private static FileSnapshot ParseSnapshot(XElement element)
    {
        var sizeXml = RequiredAttribute(element, "size");

        if (!long.TryParse(sizeXml, NumberStyles.None, CultureInfo.InvariantCulture, out var size))
            throw new InvalidDataException("A file size must be an invariant integer.");

        var lastWriteTimeUtcXml = RequiredAttribute(element, "lastWriteTimeUtc");
        var validDate = DateTime.TryParse(lastWriteTimeUtcXml, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var time);

        if (!validDate || time.Kind != DateTimeKind.Utc)
            throw new InvalidDataException("A file timestamp must be UTC and round-trip parseable.");

        var path = RequiredAttribute(element, "path");
        var sha256 = RequiredAttribute(element, "sha256");

        return new FileSnapshot(path, size, time, sha256);
    }

    #endregion
}
