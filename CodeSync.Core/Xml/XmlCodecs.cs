using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Xml;
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

        var root = XDocument.Parse(xml, LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo).Root
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
    ///   Attempts to retrieve the required text content of an XML element,
    ///   adding any errors to the provided list.
    /// </summary>
    /// <param name="root">The XML element containing the required text.</param>
    /// <param name="name">The name of the child element whose text content is required.</param>
    /// <param name="errors">The list of errors to which any encountered errors will be added.</param>
    /// <returns>The text content of the specified child element, or <c>null</c> if an error occurred.</returns>
    private static string? TryRequiredText(XElement root, string name, List<string> errors)
    {
        try
        {
            return RequiredText(root, name);
        }
        catch (InvalidDataException exception)
        {
            errors.Add(FormatElementError(root.Element(name) ?? root, exception.Message));
            return null;
        }
    }

    /// <summary>
    ///   Attempts to retrieve the required UTC timestamp from an XML element,
    ///   adding any errors to the provided list.
    /// </summary>
    /// <param name="root">The XML element containing the required timestamp.</param>
    /// <param name="name">The name of the child element whose timestamp is required.</param>
    /// <param name="errors">The list of errors to which any encountered errors will be added.</param>
    /// <returns>The UTC timestamp of the specified child element, or <c>null</c> if an error occurred.</returns>
    private static DateTimeOffset? TryRequiredUtc(XElement root, string name, List<string> errors)
    {
        try
        {
            return RequiredUtc(root, name);
        }
        catch (InvalidDataException exception)
        {
            errors.Add(FormatElementError(root.Element(name) ?? root, exception.Message));
            return null;
        }
    }

    /// <summary>
    ///   Attempts to retrieve the required root directory path from an XML element,
    ///   adding any errors to the provided list.
    /// </summary>
    /// <param name="root">The XML element containing the required root directory path.</param>
    /// <param name="name">The name of the child element whose root directory path is required.</param>
    /// <param name="errors">The list of errors to which any encountered errors will be added.</param>
    /// <returns>The full path of the specified child element, or <c>null</c> if an error occurred.</returns>
    private static string? TryRequiredRoot(XElement root, string name, List<string> errors)
    {
        var path = TryRequiredText(root, name, errors);
        if (path is null)
            return null;

        try
        {
            return Path.GetFullPath(path);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            var message = FormatElementError(
                root.Element(name) ?? root,
                $"The XML element '{name}' is not a valid directory path: {exception.Message}");

            errors.Add(message);
            return null;
        }
    }

    /// <summary>
    ///   Attempts to parse an XML element using the provided parsing function,
    ///   adding any errors to the provided list.
    /// </summary>
    /// <typeparam name="T">The type of the parsed result.</typeparam>
    /// <param name="element">The XML element to parse.</param>
    /// <param name="errors">The list of errors to which any encountered errors will be added.</param>
    /// <param name="parse">The function that performs the parsing.</param>
    /// <returns>The parsed result, or <c>null</c> if an error occurred.</returns>
    private static T? TryParseElement<T>(XElement element, List<string> errors, Func<T> parse)
        where T : class
    {
        try
        {
            return parse();
        }
        catch (Exception exception) when (exception is InvalidDataException or ArgumentException or FormatException or OverflowException)
        {
            var message = FormatElementError(element, exception.Message);

            errors.Add(message);
            return null;
        }
    }

    /// <summary>
    ///   Formats an error message for an XML element, including line information if available.
    /// </summary>
    /// <param name="element">The XML element associated with the error.</param>
    /// <param name="message">The error message.</param>
    /// <returns>The formatted error message, including line information if available.</returns>
    private static string FormatElementError(XElement element, string message)
    {
        var lineInfo = (IXmlLineInfo) element;

        return lineInfo.HasLineInfo()
            ? $"Line {lineInfo.LineNumber}: {message}"
            : message;
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
        var writer = new FormattedXmlWriter();

        writer.WriteDeclaration();
        writer.WriteComment($"""
                              CodeSync Profile v{SchemaVersion}

                                Source:      {profile.SourceDirectory}
                                Destination: {profile.DestinationDirectory}

                                Created:     {FormatHeaderTimestamp(document.CreatedUtc)}
                                Last update: {FormatHeaderTimestamp(document.LastUpdatedUtc)}
                            """);

        writer.WriteStartElement(ProfileRoot, [("schemaVersion", SchemaVersion)]);
        writer.WriteBlankLine();

        writer.WriteElement("SourceDirectory", profile.SourceDirectory, indent: 2);
        writer.WriteElement("DestinationDirectory", profile.DestinationDirectory, indent: 2);
        writer.WriteBlankLine();

        writer.WriteElement("CreatedUtc", FormatTimestamp(document.CreatedUtc), indent: 2);
        writer.WriteElement("LastUpdatedUtc", FormatTimestamp(document.LastUpdatedUtc), indent: 2);
        writer.WriteBlankLine();

        SerializeDirectoryReference(writer, profile.DirectoryReferences);
        writer.WriteBlankLine();

        SerializeFileMappings(writer, profile.FileMappings);
        writer.WriteBlankLine();

        writer.WriteEndElement(ProfileRoot);

        return writer.ToString();

        //
        // Serializes the directory references section of the profile.
        //
        static void SerializeDirectoryReference(FormattedXmlWriter writer, IEnumerable<DirectoryReference> directoryReferences)
        {
            writer.WriteStartElement("DirectoryReferences", attributes: [], indent: 2);

            writer.WriteComment("""
                              The following directories are known to match between source and destination.
                              Any unknown file found under one of these directories will be automatically
                              placed in the corresponding destination directory, albeit as a conflict for
                              the user to check and correct.
                            """,
                            indent: 4);

            bool first = true;

            foreach (var reference in directoryReferences)
            {
                if (!first)
                    writer.WriteBlankLine();

                writer.WriteEmptyElement("Directory",
                                         attributes: [("Source", reference.SourcePath), ("Destination", reference.DestinationPath)],
                                         indent: 4);
                first = false;
            }

            writer.WriteBlankLine();
            writer.WriteEndElement("DirectoryReferences", indent: 2);
        }

        //
        // Serializes the file mappings section of the profile.
        //
        static void SerializeFileMappings(FormattedXmlWriter writer, IEnumerable<ProfileMapping> fileMappings)
        {
            writer.WriteStartElement("FileMappings", attributes: [], indent: 2);

            writer.WriteComment("""
                            The following files are either copied from a previous version of this CodeSync profile,
                            or new matching files found that will be copied.

                            FileMapping entries describe files that will be copied.
                            Ignore entries are reviewed decisions for files that must remain unpaired.
                          """,
                          indent: 4);

            bool first = true;

            foreach (var mapping in fileMappings)
            {
                Span<(string Name, string Value)> attributes =
                [
                    ("Source", mapping.SourcePath ?? ""),
                    ("Destination", mapping.DestinationPath ?? "")
                ];

                if (!first)
                    writer.WriteBlankLine();

                writer.WriteEmptyElement(mapping.IsIgnored ? "Ignore" : "FileMapping",
                                         attributes,
                                         indent: 4);

                first = false;
            }

            writer.WriteBlankLine();
            writer.WriteEndElement("FileMappings", indent: 2);
        }
    }

    /// <summary>
    ///   Deserializes an editable profile document.
    /// </summary>
    /// <param name="xml">The XML profile to parse.</param>
    /// <returns>The parsed profile definition and timestamps.</returns>
    public static ProfileDocument DeserializeProfile(string xml)
    {
        var errors = new List<string>();

        var root = LoadRootFromXmlDocumentString(xml, ProfileRoot);

        var sourceDirectory = TryRequiredRoot(root, "SourceDirectory", errors);
        var destinationDirectory = TryRequiredRoot(root, "DestinationDirectory", errors);

        var directoryReferences = root.Element("DirectoryReferences")?
            .Elements("Directory")
            .Select(element => TryParseElement(element,
                                               errors,
                                               () => ParseDirectoryReference(element)))
            .OfType<DirectoryReference>()
            .ToArray() ?? [];

        var fileMappings = root.Element("FileMappings")?
            .Elements()
            .Where(element => element.Name.LocalName is "FileMapping" or "Ignore")
            .Select(element => TryParseElement(element,
                                               errors,
                                               () => ParseProfileMapping(element)))
            .OfType<ProfileMapping>()
            .ToArray() ?? [];

        var createdUtc = TryRequiredUtc(root, "CreatedUtc", errors);
        var lastUpdatedUtc = TryRequiredUtc(root, "LastUpdatedUtc", errors);

        if (errors.Count > 0)
            throw new ProfileLoadException(errors);

        var profile = new ProfileDefinition(sourceDirectory!,
                                            destinationDirectory!,
                                            directoryReferences,
                                            fileMappings);

        return new ProfileDocument(profile,
                                   createdUtc!.Value,
                                   lastUpdatedUtc!.Value);

        //
        // Parses an individual directory reference from its XML representation.
        //
        static DirectoryReference ParseDirectoryReference(XElement element)
        {
            return new DirectoryReference(
                RequiredAttribute(element, "Source"),
                RequiredAttribute(element, "Destination"));
        }

        //
        // Parses an individual profile mapping from its XML representation.
        //
        static ProfileMapping ParseProfileMapping(XElement element)
        {
            return new ProfileMapping(
                OptionalAttribute(element, "Source"),
                OptionalAttribute(element, "Destination"),
                element.Name.LocalName == "Ignore");
        }
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

        var root = LoadRootFromXmlDocumentString(xml, ContentRoot);

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

        var writer = new FormattedXmlWriter();

        writer.WriteDeclaration();
        writer.WriteComment($"""
                              CodeSync Conflicts v{SchemaVersion}

                                Source:      {document.SourceDirectory}
                                Destination: {document.DestinationDirectory}

                                Review the sections below and update the profile before copying.
                            """);

        writer.WriteStartElement(ConflictsRoot, [("schemaVersion", SchemaVersion)]);
        writer.WriteBlankLine();

        writer.WriteElement("SourceDirectory", document.SourceDirectory, indent: 2);
        writer.WriteElement("DestinationDirectory", document.DestinationDirectory, indent: 2);

        foreach (ConflictKind conflictKind in ConflictDisplayOrder)
        {
            writer.WriteBlankLine();
            WriteConflictSection(writer, document, conflictKind);
        }

        writer.WriteBlankLine();
        writer.WriteEndElement(ConflictsRoot);

        return writer.ToString();

        //
        // Writes a section of the XML containing conflicts of a specific kind.
        //
        static void WriteConflictSection(FormattedXmlWriter writer,
                                         ConflictDocument document,
                                         ConflictKind kind)
        {
            writer.WriteStartElement(kind.ToString(), attributes: [], indent: 2);

            writer.WriteComment(ConflictDescription(kind), indent: 4);

            var conflictsOfKind = document.Conflicts
                .Where(conflict => conflict.Kind == kind)
                .OrderBy(conflict => conflict.SourcePath ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(conflict => conflict.DestinationPath ?? string.Empty, StringComparer.Ordinal);

            bool first = true;

            foreach (var conflict in conflictsOfKind)
            {
                Span<(string Name, string Value)> attributes =
                [
                    ("Source", conflict?.SourcePath ?? ""),
                    ("Destination", conflict?.DestinationPath ?? "")
                ];

                if (!first)
                    writer.WriteBlankLine();

                writer.WriteEmptyElement("FileMapping", attributes, indent: 4);

                first = false;
            }

            if (first)
            {
                // If no conflicts of this kind were found, write a comment indicating that
                writer.WriteBlankLine();
                writer.WriteComment("No files to see here. Move on.", indent: 4);
            }

            writer.WriteBlankLine();
            writer.WriteEndElement(kind.ToString(), indent: 2);
        }

        //
        // Returns a human-readable description for the specified conflict kind.
        //
        static string ConflictDescription(ConflictKind kind)
            => kind switch
            {
                ConflictKind.DestinationWithoutSource => """
                      The following files are in the destination, but not in the source.
                      Also, they could not be matched by size or content.

                      Maybe they are new additions, or renamed files.

                      You can check these files against the source files that have no destination to
                      complete those if needed.
                    """,

                ConflictKind.SourceWithoutDestination => """
                      The following files are found only in the source. They do not exist in the destination.

                      This may be because these files are new additions in the source, or they have been renamed.

                      Review these files to determine if they need to be added to the destination.
                    """,

                ConflictKind.AmbiguousMatch => """
                      The following files have ambiguous content matches, i.e., multiple files
                      have identical content.

                      Review these files to determine the correct source-destination mapping.
                    """,

                ConflictKind.MissingMappedFile => """
                      The following mappings refer to files that are no longer present.

                      These mappings need to be reviewed and possibly removed.
                    """,

                ConflictKind.DuplicateMapping => """
                      The following files have duplicate mappings, i.e., the same source or destination
                      is referenced multiple times.

                      Review these mappings to resolve the duplicates.
                    """,

                // This case should never be reached because all conflict kinds are handled explicitly
                _ => throw new UnreachableException()
            };
    }

    /// <summary>
    ///   Deserializes a path-only conflict document.
    /// </summary>
    /// <param name="xml">The XML conflict report to parse.</param>
    /// <returns>The parsed conflict document.</returns>
    public static ConflictDocument DeserializeConflicts(string xml)
    {
        var errors = new List<string>();

        var root = LoadRootFromXmlDocumentString(xml, ConflictsRoot);

        var sourceDirectory = TryRequiredRoot(root, "SourceDirectory", errors);
        var destinationDirectory = TryRequiredRoot(root, "DestinationDirectory", errors);

        var conflicts = new List<ConflictEntry>();

        foreach (var kind in ConflictDisplayOrder)
        {
            var mappingsOfKind = root.Element(kind.ToString())?.Elements("FileMapping") ?? [];

            foreach (var element in mappingsOfKind)
            {
                var conflict = TryParseElement(
                    element,
                    errors,
                    () => new ConflictEntry(kind,
                                            OptionalAttribute(element, "Source"),
                                            OptionalAttribute(element, "Destination")));

                if (conflict is not null)
                    conflicts.Add(conflict);
            }
        }

        if (errors.Count > 0)
            throw new ProfileLoadException(errors);

        return new ConflictDocument(sourceDirectory!,
                                    destinationDirectory!,
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

        var root = LoadRootFromXmlDocumentString(xml, SkippedRoot);

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

    #endregion

    #region Helper: FormattedXmlWriter

    /// <summary>
    ///   A helper class for writing formatted XML and generating a human-readable XML string.
    /// </summary>
    private sealed class FormattedXmlWriter
    {
        private readonly StringBuilder _builder = new();


        /// <summary>
        ///   Writes the XML declaration at the beginning of the document.
        /// </summary>
        public void WriteDeclaration()
        {
            _builder.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        }

        /// <summary>
        ///   Writes an XML comment with the specified text and indentation.
        /// </summary>
        /// <param name="text">The text of the comment.</param>
        /// <param name="indent">The number of spaces to indent the comment.</param>
        public void WriteComment(string text, int indent = 0)
        {
            var span = text.AsSpan();

            AppendIndentation(indent);
            _builder.AppendLine("<!--");

            foreach (var line in span.EnumerateLines())
            {
                var trimmedLine = line.Trim();
                if (trimmedLine.IsEmpty)
                {
                    _builder.AppendLine();
                }
                else
                {
                    AppendIndentation(indent + 2);
                    _builder.Append(trimmedLine).AppendLine();
                }
            }

            AppendIndentation(indent);
            _builder.AppendLine("-->");
        }

        /// <summary>
        ///   Writes the start tag of an XML element with the specified name, attributes, and indentation.
        /// </summary>
        /// <param name="name">The name of the element.</param>
        /// <param name="attributes">The attributes of the element.</param>
        /// <param name="indent">The number of spaces to indent the element.</param>
        public void WriteStartElement(string name,
                                      ReadOnlySpan<(string Name, string Value)> attributes,
                                      int indent = 0)
        {
            AppendIndentation(indent);

            if (attributes.IsEmpty)
            {
                _builder.AppendLine($"<{name}>");
            }
            else
            {
                _builder.Append($"<{name} ");

                bool first = true;

                foreach (var (attrName, attrValue) in attributes)
                {
                    if (!first)
                        _builder.Append(' ');

                    _builder.Append($"{attrName}=\"{EscapeAttribute(attrValue)}\"");

                    first = false;
                }

                _builder.AppendLine(">");
            }
        }

        /// <summary>
        ///   Writes an empty XML element with the specified name, attributes, and indentation.
        /// </summary>
        /// <param name="name">The name of the element.</param>
        /// <param name="attributes">The attributes of the element.</param>
        /// <param name="indent">The number of spaces to indent the element.</param>
        public void WriteEmptyElement(string name,
                                      ReadOnlySpan<(string Name, string Value)> attributes,
                                      int indent = 0)
        {
            if (attributes.IsEmpty)
            {
                AppendIndentation(indent);
                _builder.AppendLine($"<{name} />");
                return;
            }

            if (attributes.Length == 1)
            {
                var (attrName, attrValue) = attributes[0];

                AppendIndentation(indent);
                _builder.AppendLine($"<{name} {attrName}=\"{EscapeAttribute(attrValue)}\" />");
                return;
            }

            var continuationIndent = indent + name.Length + 2;

            for (var index = 0; index < attributes.Length; index++)
            {
                var (attrName, attrValue) = attributes[index];

                if (index == 0)
                {
                    AppendIndentation(indent);
                    _builder.Append($"<{name} ");
                }
                else
                {
                    AppendIndentation(continuationIndent);
                }

                _builder.Append($"{attrName}=\"{EscapeAttribute(attrValue)}\"");

                if (index < attributes.Length - 1)
                {
                    _builder.AppendLine();
                }
            }

            _builder.AppendLine(" />");
        }

        /// <summary>
        ///   Writes an XML element with the specified name, value, and indentation.
        /// </summary>
        /// <param name="name">The name of the element.</param>
        /// <param name="value">The text content of the element.</param>
        /// <param name="indent">The number of spaces to indent the element.</param>
        public void WriteElement(string name, string value, int indent = 0)
        {
            AppendIndentation(indent);
            _builder.AppendLine($"<{name}>{EscapeText(value)}</{name}>");
        }

        /// <summary>
        ///   Writes the end tag of an XML element with the specified name and indentation.
        /// </summary>
        /// <param name="name">The name of the element.</param>
        /// <param name="indent">The number of spaces to indent the element.</param>
        public void WriteEndElement(string name, int indent = 0)
        {
            AppendIndentation(indent);
            _builder.AppendLine($"</{name}>");
        }

        /// <summary>
        ///   Writes indentation to the XML output based on the specified number of spaces.
        /// </summary>
        /// <param name="indent">The number of spaces to indent.</param>
        public void AppendIndentation(int indent = 0)
        {
            if (indent > 0)
            {
                _builder.Append(' ', repeatCount: indent);
            }
        }

        /// <summary>
        ///   Writes a blank line to the XML output.
        /// </summary>
        public void WriteBlankLine()
        {
            _builder.AppendLine();
        }

        /// <summary>
        ///   Returns the current XML content as a string.
        /// </summary>
        /// <returns>The current XML content as a string.</returns>
        public override string ToString()
        {
            return _builder.ToString();
        }

        /// <summary>
        ///   Escapes special characters in the text content of an XML element.
        /// </summary>
        /// <param name="value">The text content to escape.</param>
        /// <returns>The escaped text content.</returns>
        private static string EscapeText(string value)
        {
            return value.Replace("&", "&amp;", StringComparison.Ordinal)
                .Replace("<", "&lt;", StringComparison.Ordinal)
                .Replace(">", "&gt;", StringComparison.Ordinal);
        }

        /// <summary>
        ///   Escapes special characters in the value of an XML attribute.
        /// </summary>
        /// <param name="value">The attribute value to escape.</param>
        /// <returns>The escaped attribute value.</returns>
        private static string EscapeAttribute(string value)
        {
            return EscapeText(value)
                .Replace("\"", "&quot;", StringComparison.Ordinal)
                .Replace("'", "&apos;", StringComparison.Ordinal);
        }
    }

    #endregion
}
