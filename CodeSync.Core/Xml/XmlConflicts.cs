using System.Diagnostics;

using static CodeSync.Core.Xml.XmlHelpers;

namespace CodeSync.Core.Xml;

/// <summary>
///   Serializes and parses versioned CodeSync XML conflict documents.
/// </summary>
/// <seealso cref="ConflictSet"/>
/// <seealso cref="ConflictDocument"/>
public static class XmlConflicts
{
    private const string ConflictsRoot = "CodeSyncConflicts";
    private const string SchemaVersion = "1";

    private static readonly ConflictKind[] ConflictDisplayOrder =
    [
        ConflictKind.DestinationWithoutSource,
        ConflictKind.SourceWithoutDestination,
        ConflictKind.AmbiguousMatch,
        ConflictKind.MissingMappedFile,
        ConflictKind.DuplicateMapping
    ];


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

        var root = LoadRootFromXmlDocumentString(xml, ConflictsRoot, SchemaVersion);

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
}
