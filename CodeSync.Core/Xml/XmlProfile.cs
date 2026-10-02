using System.Xml.Linq;

using static CodeSync.Core.Xml.XmlHelpers;

namespace CodeSync.Core.Xml;

/// <summary>
///   Serializes and parses versioned CodeSync XML profile documents.
/// </summary>
/// <seealso cref="ProfileDocument"/>
public static class XmlProfile
{
    private const string ProfileRoot = "CodeSyncProfile";
    private const string SchemaVersion = "1";


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

        var root = LoadRootFromXmlDocumentString(xml, ProfileRoot, SchemaVersion);

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
}
