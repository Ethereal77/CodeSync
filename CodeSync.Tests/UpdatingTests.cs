using CodeSync.Core;

namespace CodeSync.Tests;

public sealed class UpdatingTests
{
    [Fact]
    public void Update_RefreshesSnapshotsListedAsSkipped()
    {
        var previous = CreateFakeFileSnapshot(path: "src/file.cs", size: 1);
        var current = CreateFakeFileSnapshot(path: "src/file.cs", size: 2);
        var destination = CreateFakeFileSnapshot(path: "lib/file.cs", size: 1);

        var fakeMapping = new FileMapping(previous, destination); 
        var fakeProfile = CreateFakeProfile(fakeMapping); 

        var result = new ProfileUpdater().Update(
            fakeProfile,
            skippedSourcePaths: ["src/file.cs"],
            currentSourceFiles: [current]);

        Assert.True(result.Succeeded);

        Assert.Equal(["src/file.cs"], result.UpdatedSourcePaths);
        var singleUpdatedMapping = Assert.Single(result.UpdatedProfile.FileMappings);
        Assert.Equal(2, singleUpdatedMapping.Source!.Size);
    }

    [Fact]
    public void Update_IsIdempotentForDuplicateSkippedPaths()
    {
        var source = CreateFakeFileSnapshot(path: "src/file.cs", size: 1);

        var fakeMapping = new FileMapping(source, null);
        var fakeProfile = CreateFakeProfile(fakeMapping);

        var result = new ProfileUpdater().Update(
            fakeProfile,
            skippedSourcePaths: [@"src\\file.cs", "src/file.cs"],
            currentSourceFiles: [source]);

        Assert.True(result.Succeeded);
        Assert.Single(result.UpdatedSourcePaths);
    }

    [Fact]
    public void Update_ReportsUnknownOrMissingSkippedSources()
    {
        var source = CreateFakeFileSnapshot(path: "src/file.cs", size: 1);

        var fakeMapping = new FileMapping(source, null);
        var fakeProfile = CreateFakeProfile(fakeMapping);

        var result = new ProfileUpdater().Update(
            fakeProfile,
            skippedSourcePaths: ["src/unknown.cs", "src/missing.cs"],
            currentSourceFiles: [source]);

        Assert.False(result.Succeeded);
        Assert.Equal(2, result.Errors.Count);
    }


    private static SyncProfile CreateFakeProfile(params FileMapping[] mappings)
    {
        return new SyncProfile("source", "destination", Array.Empty<DirectoryReference>(), mappings);
    }

    private static FileSnapshot CreateFakeFileSnapshot(string path, long size)
    {
        const string Hash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        var fixedTime = new DateTime(2026, 8, 27, 10, 30, 0, DateTimeKind.Utc);

        return new FileSnapshot(path, size, fixedTime, Hash);
    }
}
