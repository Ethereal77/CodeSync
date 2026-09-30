using CodeSync.Core;

namespace CodeSync.Tests;

public sealed class XmlCodecTests
{
    private const string Hash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private static readonly DateTime FixedTime = new(2026, 8, 27, 10, 30, 0, DateTimeKind.Utc);


    [Fact]
    public void ProfileXml_RoundTripsPathsAndLeavesMetadataOutOfTheEditableDocument()
    {
        var definition = new ProfileDefinition(
            sourceDirectory: @"C:\synthetic\source",
            destinationDirectory: @"C:\synthetic\destination",
            directoryReferences: [new DirectoryReference("src", "lib")],
            fileMappings: [new ProfileMapping("src/old.cs", "lib/new.cs"),
                           new ProfileMapping("src/ignored.cs", null)]);

        var document = new ProfileDocument(definition,
                                           createdUtc: FixedTime,
                                           lastUpdatedUtc: FixedTime.AddMinutes(5));

        var serialized = XmlCodecs.SerializeProfile(document);
        var restored = XmlCodecs.DeserializeProfile(serialized);

        Assert.Equal(definition.SourceDirectory, restored.Profile.SourceDirectory);
        Assert.Equal(definition.DestinationDirectory, restored.Profile.DestinationDirectory);
        Assert.Equal(FixedTime, restored.CreatedUtc);
        Assert.Equal(FixedTime.AddMinutes(5), restored.LastUpdatedUtc);
        Assert.Equal("src", Assert.Single(restored.Profile.DirectoryReferences).SourcePath);
        Assert.Equal("src/old.cs", restored.Profile.FileMappings.First().SourcePath);
        Assert.Contains("Source=\"src/old.cs\"", serialized);
        Assert.DoesNotContain("Size=", serialized);
        Assert.DoesNotContain("Sha256=", serialized);
        Assert.Contains("CodeSync Profile v1", serialized);

        Assert.StartsWith("<?xml version=\"1.0\" encoding=\"utf-8\"?>", serialized);
        Assert.Contains("<!--", serialized);
        Assert.Contains("-->", serialized);
        Assert.Contains("<CodeSyncProfile schemaVersion=\"1\">", serialized);
        Assert.Contains("    <Directory Source=\"src\"", serialized);
        Assert.Contains("Destination=\"lib\" />", serialized);
    }

    [Fact]
    public void ContentXml_RoundTripsSnapshotsWithPascalCaseMetadata()
    {
        var source = new FileSnapshot("src/old.cs", 3, FixedTime, Hash);
        var destination = new FileSnapshot("lib/new.cs", 3, FixedTime, Hash);

        var content = new ProfileContent("source", "destination", FixedTime, FixedTime, [source], [destination]);

        var serialized = XmlCodecs.SerializeContent(content);
        var restored = XmlCodecs.DeserializeContent(serialized);

        Assert.Equal(source, Assert.Single(restored.SourceFiles));
        Assert.Equal(destination, Assert.Single(restored.DestinationFiles));
        Assert.Contains("Path=\"src/old.cs\"", serialized);
        Assert.Contains("Size=\"3\"", serialized);
        Assert.Contains("LastWriteTimeUtc=", serialized);
        Assert.Contains("Sha256=", serialized);
        Assert.DoesNotContain("size=", serialized);
        Assert.DoesNotContain("sha256=", serialized);

        Assert.Contains("    <File Path=\"src/old.cs\"\r\n          Size=\"3\"", serialized);
        Assert.Contains("    <!--\r\n      The following file metadata was discovered in the source directory.\r\n    -->", serialized);
    }

    [Fact]
    public void ConflictXml_UsesPathOnlySectionsInDisplayOrder()
    {
        var destination = new FileSnapshot("lib/extra.cs", 3, FixedTime, Hash);
        var source = new FileSnapshot("src/new.cs", 3, FixedTime, Hash);
        var ambiguous = new FileSnapshot("src/ambiguous.cs", 3, FixedTime, Hash);

        var conflicts = new ConflictSet(
            sourceDirectory: "source",
            destinationDirectory: "destination",
            conflicts:
            [
                new Conflict(ConflictKind.AmbiguousMatch, new FileMapping(ambiguous, null)),
                new Conflict(ConflictKind.SourceWithoutDestination, new FileMapping(source, null)),
                new Conflict(ConflictKind.DestinationWithoutSource, new FileMapping(null, destination))
            ]);

        var serialized = XmlCodecs.SerializeConflicts(conflicts);
        var restored = XmlCodecs.DeserializeConflicts(serialized);

        Assert.Equal([ConflictKind.DestinationWithoutSource,
                      ConflictKind.SourceWithoutDestination,
                      ConflictKind.AmbiguousMatch],
                     restored.Conflicts.Select(conflict => conflict.Kind));
        Assert.Contains("<DestinationWithoutSource>", serialized);
        Assert.Contains("<SourceWithoutDestination>", serialized);
        Assert.Contains("<AmbiguousMatch>", serialized);
        Assert.DoesNotContain("kind=", serialized);
        Assert.DoesNotContain("Size=", serialized);
        Assert.Contains("Destination=\"lib/extra.cs\"", serialized);

        Assert.Contains("    <!--\r\n      ", serialized);
        Assert.Contains("\r\n    -->", serialized);
    }

    [Fact]
    public void SkippedXml_NormalizesAndRoundTripsSourcePaths()
    {
        var serialized = XmlCodecs.SerializeSkipped(sourcePaths: [@"src\\unchanged.cs", "src/other.cs"]);
        var restored = XmlCodecs.DeserializeSkipped(serialized);

        Assert.Equal(["src/unchanged.cs", "src/other.cs"], restored);
        Assert.Contains("Source=\"src/unchanged.cs\"", serialized);
    }

    [Fact]
    public void ProfileXml_RejectsLegacyRootAndUnsupportedVersion()
    {
        Assert.Throws<InvalidDataException>(() => XmlCodecs.DeserializeProfile(
            "<CodeSync><SourceDirectory>source</SourceDirectory><DestinationDirectory>destination</DestinationDirectory></CodeSync>"));
        Assert.Throws<InvalidDataException>(() => XmlCodecs.DeserializeProfile(
            "<CodeSyncProfile schemaVersion=\"2\" />"));
    }

    [Fact]
    public void ProfileXml_RejectsMetadataEmbeddedInEditableMappings()
    {
        const string xml = """
            <CodeSyncProfile schemaVersion="1">
              <SourceDirectory>source</SourceDirectory>
              <DestinationDirectory>destination</DestinationDirectory>
              <CreatedUtc>2026-08-27T10:30:00.0000000Z</CreatedUtc>
              <LastUpdatedUtc>2026-08-27T10:30:00.0000000Z</LastUpdatedUtc>
              <FileMappings>
                <FileMapping><Source path="file.txt" /></FileMapping>
              </FileMappings>
            </CodeSyncProfile>
            """;

        Assert.Throws<ArgumentException>(() => XmlCodecs.DeserializeProfile(xml));
    }
}
