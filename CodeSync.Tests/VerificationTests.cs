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
    public void Verify_WithExactRepeatedMapping_WarnsAndKeepsOneCopy()
    {
        var source = CreateFakeFileSnapshot(path: "src/file.cs", content: "source");
        var destination = CreateFakeFileSnapshot(path: "lib/file.cs", content: "destination");
        var mapping = new FileMapping(source, destination);

        var result = new ProfileVerifier().Verify(CreateFakeProfile(mapping, mapping), [source], [destination]);

        Assert.True(result.IsValid);
        Assert.Single(result.Warnings);
        Assert.Single(result.CleanedProfile.FileMappings);
    }

    [Fact]
    public void Verify_WithSharedDestination_RemovesAllInvolvedMappingsFromCleanedProfile()
    {
        var firstSource = CreateFakeFileSnapshot(path: "src/one.cs", content: "one");
        var secondSource = CreateFakeFileSnapshot(path: "src/two.cs", content: "two");
        var otherSource = CreateFakeFileSnapshot(path: "src/other.cs", content: "other");
        var destination = CreateFakeFileSnapshot(path: "lib/file.cs", content: "destination");
        var otherDestination = CreateFakeFileSnapshot(path: "lib/other.cs", content: "other");

        var fakeProfile = CreateFakeProfile(new FileMapping(firstSource, destination),
                                            new FileMapping(secondSource, destination),
                                            new FileMapping(otherSource, otherDestination));

        var result = new ProfileVerifier().Verify(
            fakeProfile,
            sourceFiles: [firstSource, secondSource, otherSource],
            destinationFiles: [destination, otherDestination]);

        Assert.Equal(2, result.Conflicts.Count);
        Assert.All(result.Conflicts, conflict => Assert.Equal(ConflictKind.DuplicateMapping, conflict.Kind));

        var remaining = Assert.Single(result.CleanedProfile.FileMappings);
        Assert.Equal("src/other.cs", remaining.Source!.Path);
    }

    [Fact]
    public void Verify_WithSharedSource_DoesNotReportRemovedFilesAsUncovered()
    {
        var source = CreateFakeFileSnapshot(path: "src/file.cs", content: "source");
        var firstDestination = CreateFakeFileSnapshot(path: "lib/one.cs", content: "one");
        var secondDestination = CreateFakeFileSnapshot(path: "lib/two.cs", content: "two");

        var fakeProfile = CreateFakeProfile(new FileMapping(source, firstDestination),
                                            new FileMapping(source, secondDestination));

        var result = new ProfileVerifier().Verify(
            fakeProfile,
            sourceFiles: [source],
            destinationFiles: [firstDestination, secondDestination]);

        Assert.Equal(2, result.Conflicts.Count);
        Assert.All(result.Conflicts, conflict => Assert.Equal(ConflictKind.DuplicateMapping, conflict.Kind));
        Assert.Empty(result.CleanedProfile.FileMappings);
    }

    [Fact]
    public void Verify_WithMissingMappedFile_RemovesMappingFromCleanedProfile()
    {
        var source = CreateFakeFileSnapshot(path: "src/file.cs", content: "source");
        var destination = CreateFakeFileSnapshot(path: "lib/file.cs", content: "destination");
        var mapping = new FileMapping(source, destination);

        var fakeProfile = CreateFakeProfile(mapping);

        var result = new ProfileVerifier().Verify(fakeProfile, sourceFiles: [source], destinationFiles: []);

        Assert.Empty(result.CleanedProfile.FileMappings);
    }

    [Fact]
    public void Verify_WithValidProfile_ReturnsSameProfileInstance()
    {
        var source = CreateFakeFileSnapshot(path: "src/file.cs", content: "source");
        var destination = CreateFakeFileSnapshot(path: "lib/file.cs", content: "destination");
        var mapping = new FileMapping(source, destination);

        var fakeProfile = CreateFakeProfile(mapping);

        var result = new ProfileVerifier().Verify(fakeProfile, sourceFiles: [source], destinationFiles: [destination]);

        Assert.Same(fakeProfile, result.CleanedProfile);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Verify_SuggestsDestinationFromTheMostSpecificDirectoryReference()
    {
        var knownSource = CreateFakeFileSnapshot(path: "src/known.cs", content: "known");
        var knownDestination = CreateFakeFileSnapshot(path: "lib/known.cs", content: "known");
        var newSource = CreateFakeFileSnapshot(path: "src/new.cs", content: "new");

        var profile = new SyncProfile(
            sourceDirectory: "source",
            destinationDirectory: "destination",
            directoryReferences: [new DirectoryReference("src", "lib")],
            fileMappings: [new FileMapping(knownSource, knownDestination)]);

        var result = new ProfileVerifier().Verify(profile: profile,
                                                  sourceFiles: [knownSource, newSource],
                                                  destinationFiles: [knownDestination]);

        var conflict = Assert.Single(result.Conflicts);
        Assert.Equal("lib/new.cs", conflict.Mapping.DestinationPath);
    }

    [Fact]
    public void Verify_IgnoreCoversSourceWithSuggestedDestinationThatIsNotPresent()
    {
        var source = CreateFakeFileSnapshot(path: "src/ignored.cs", content: "ignored");
        var ignored = new FileMapping(source,
                                      destination: null,
                                      isIgnored: true,
                                      destinationPath: "lib/ignored.cs");

        var result = new ProfileVerifier().Verify(
            profile: new SyncProfile("source", "destination", directoryReferences: [], fileMappings: [ignored]),
            sourceFiles: [source],
            destinationFiles: []);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Verify_IgnoreDestinationUsesPlatformPathComparison()
    {
        var destination = CreateFakeFileSnapshot(path: "lib/file.cs", content: "destination");
        var ignored = new FileMapping(source: null,
                                      destination: null,
                                      destinationPath: "LIB/FILE.cs",
                                      isIgnored: true);

        var result = new ProfileVerifier().Verify(
            profile: new SyncProfile("source", "destination", directoryReferences: [], fileMappings: [ignored]),
            sourceFiles: [],
            destinationFiles: [destination]);

        var isCaseInsensitive = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS();

        Assert.Equal(isCaseInsensitive, result.IsValid);
        Assert.Equal(isCaseInsensitive, result.Conflicts.Count == 0);
    }

    [Fact]
    public void Verify_AcceptsNewDestinationPathWithoutDestinationSnapshot()
    {
        var source = CreateFakeFileSnapshot(path: "src/new.cs", content: "new");
        var mapping = new FileMapping(source,
                                      destination: null,
                                      destinationPath: "lib/new.cs");

        var result = new ProfileVerifier().Verify(
            profile: new SyncProfile("source", "destination", directoryReferences: [], fileMappings: [mapping]),
            sourceFiles: [source],
            destinationFiles: []);

        Assert.True(result.IsValid);
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
