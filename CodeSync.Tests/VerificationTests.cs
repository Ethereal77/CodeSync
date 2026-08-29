using CodeSync.Core;

namespace CodeSync.Tests;

public sealed class VerificationTests
{
    [Fact]
    public void Verify_AcceptsCompleteProfileEvenWhenContentChanged()
    {
        var source = CreateFakeFileSnapshot(path: "src/file.cs", content: "new source content");

        var destination = CreateFakeFileSnapshot(path: "lib/file.cs", content: "different destination content");

        var profile = CreateFakeProfile(new FileMapping(
            CreateFakeFileSnapshot(path: "src/file.cs", content: "old source content"),
            CreateFakeFileSnapshot(path: "lib/file.cs", content: "old destination content")));

        var result = new ProfileVerifier().Verify(profile, [source], [destination]);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Verify_ReportsMissingMappedFiles()
    {
        var source = CreateFakeFileSnapshot(path: "src/file.cs", content: "source");

        var mapping = new FileMapping(source, CreateFakeFileSnapshot(path: "lib/file.cs", content: "destination"));

        var result = new ProfileVerifier().Verify(CreateFakeProfile(mapping), [source], []);

        var conflict = Assert.Single(result.Conflicts);
        Assert.Equal(ConflictKind.MissingMappedFile, conflict.Kind);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Verify_ReportsUncoveredFilesOnBothSides()
    {
        var knownSource = CreateFakeFileSnapshot(path: "src/known.cs", content: "known");
        var knownDestination = CreateFakeFileSnapshot(path: "lib/known.cs", content: "known");
        var extraSource = CreateFakeFileSnapshot(path: "src/new.cs", content: "new");
        var extraDestination = CreateFakeFileSnapshot(path: "lib/local.cs", content: "local");

        var result = new ProfileVerifier().Verify(
            CreateFakeProfile(new FileMapping(knownSource, knownDestination)),
            [knownSource, extraSource],
            [knownDestination, extraDestination]);

        Assert.Equal(2, result.Conflicts.Count);
        Assert.Contains(result.Conflicts, conflict => conflict.Kind == ConflictKind.SourceWithoutDestination);
        Assert.Contains(result.Conflicts, conflict => conflict.Kind == ConflictKind.DestinationWithoutSource);
    }

    [Fact]
    public void Verify_ReportsDuplicateMappings()
    {
        var source = CreateFakeFileSnapshot(path: "src/file.cs", content: "source");
        var destination = CreateFakeFileSnapshot(path: "lib/file.cs", content: "destination");
        var mapping = new FileMapping(source, destination);

        var result = new ProfileVerifier().Verify(CreateFakeProfile(mapping, mapping), [source], [destination]);

        Assert.Contains(result.Conflicts, conflict => conflict.Kind == ConflictKind.DuplicateMapping);
    }


    private static SyncProfile CreateFakeProfile(params FileMapping[] mappings)
    {
        return new SyncProfile(sourceDirectory: "source",
                               destinationDirectory: "destination",
                               directoryReferences: [],
                               fileMappings: mappings);
    }

    private static FileSnapshot CreateFakeFileSnapshot(string path, string content)
    {
        const string Hash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        DateTime fixedTime = new(2026, 8, 27, 10, 30, 0, DateTimeKind.Utc);

        return new FileSnapshot(path, content.Length, fixedTime, Hash);
    }
}
