using CodeSync.Core;

namespace CodeSync.Tests;

public sealed class XmlCodecTests
{
    private const string Hash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private static readonly DateTime FixedTime = new(2026, 8, 27, 10, 30, 0, DateTimeKind.Utc);


    [Fact]
    public void ProfileXml_RoundTripsRootsReferencesAndFileStates()
    {
        var source = new FileSnapshot(path: "src/old.cs", size: 3, lastWriteTimeUtc: FixedTime, sha256: Hash);
        var destination = new FileSnapshot(path: "lib/new.cs", size: 3, lastWriteTimeUtc: FixedTime, sha256: Hash);

        var profile = new SyncProfile(
            sourceDirectory: @"C:\\synthetic\\source",
            destinationDirectory: @"C:\\synthetic\\destination",
            directoryReferences: [new DirectoryReference("src", "lib")],
            fileMappings: [new FileMapping(source, destination)]);

        var serialized = XmlCodecs.SerializeProfile(profile);
        var restored = XmlCodecs.DeserializeProfile(serialized);

        Assert.Equal(profile.SourceDirectory, restored.SourceDirectory);
        Assert.Equal(profile.DestinationDirectory, restored.DestinationDirectory);
        Assert.Equal("src", Assert.Single(restored.DirectoryReferences).SourcePath);
        Assert.Equal(source, Assert.Single(restored.FileMappings).Source);
    }

    [Fact]
    public void ConflictXml_RoundTripsReasonAndPartialMapping()
    {
        var source = new FileSnapshot(path: "src/new.cs", size: 3, lastWriteTimeUtc: FixedTime, sha256: Hash);
        var mapping = new FileMapping(source, destination: null);

        var conflicts = new ConflictSet(
            sourceDirectory: "source",
            destinationDirectory: "destination",
            conflicts: [new Conflict(ConflictKind.SourceWithoutDestination, mapping)]);

        var serialized = XmlCodecs.SerializeConflicts(conflicts);
        var restored = XmlCodecs.DeserializeConflicts(serialized);

        var conflict = Assert.Single(restored.Conflicts);
        Assert.Equal(ConflictKind.SourceWithoutDestination, conflict.Kind);
        Assert.Equal("src/new.cs", conflict.Mapping.Source!.Path);
        Assert.Null(conflict.Mapping.Destination);
    }

    [Fact]
    public void SkippedXml_NormalizesAndRoundTripsSourcePaths()
    {
        var serialized = XmlCodecs.SerializeSkipped(sourcePaths: [@"src\\unchanged.cs", "src/other.cs"]);
        var restored = XmlCodecs.DeserializeSkipped(serialized);

        Assert.Equal(["src/unchanged.cs", "src/other.cs"], restored);
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
    public void ProfileXml_RejectsIncompleteSnapshots()
    {
        const string xml = """
            <CodeSyncProfile schemaVersion="1">
              <SourceDirectory>source</SourceDirectory>
              <DestinationDirectory>destination</DestinationDirectory>
              <FileMappings>
                <FileMapping><Source path="file.txt" /></FileMapping>
              </FileMappings>
            </CodeSyncProfile>
            """;

        Assert.Throws<InvalidDataException>(() => XmlCodecs.DeserializeProfile(xml));
    }
}
