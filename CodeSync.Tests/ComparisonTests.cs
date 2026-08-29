using CodeSync.Core;

namespace CodeSync.Tests;

public sealed class ComparisonTests
{
    [Fact]
    public void Compare_MatchesUniqueContentEvenWhenNamesAndDirectoriesDiffer()
    {
        var source = CreateFakeSnapshot(path: "src/old-name.cs", content: "same content");
        var destination = CreateFakeSnapshot(path: "lib/new-name.cs", content: "same content");

        var result = new FileComparer().Compare(sourceFiles: [source], destinationFiles: [destination]);

        var mapping = Assert.Single(result.FileMappings);
        Assert.Equal(source.Path, mapping.Source!.Path);
        Assert.Equal(destination.Path, mapping.Destination!.Path);

        Assert.Empty(result.Conflicts);
    }

    [Fact]
    public void Compare_ReportsDuplicateContentAsAmbiguous()
    {
        FileSnapshot[] source =
        [
            CreateFakeSnapshot(path: "src/one.cs", content: "same"),
            CreateFakeSnapshot(path: "src/two.cs", content: "same")
        ];
        FileSnapshot[] destination =
        [
            CreateFakeSnapshot(path: "lib/one.cs", content: "same"),
            CreateFakeSnapshot(path: "lib/two.cs", content: "same")
        ];

        var result = new FileComparer().Compare(source, destination);

        Assert.Empty(result.FileMappings);

        Assert.Equal(4, result.Conflicts.Count);
        Assert.All(result.Conflicts, conflict => Assert.Equal(ConflictKind.AmbiguousMatch, conflict.Kind));
    }

    [Fact]
    public void Compare_ReportsFilesWithNoContentCounterpartOnTheirOwnSide()
    {
        var result = new FileComparer().Compare(
            sourceFiles: [CreateFakeSnapshot(path: "src/new.cs", content: "source")],
            destinationFiles: [CreateFakeSnapshot(path: "lib/old.cs", content: "destination")]);

        Assert.Equal(2, result.Conflicts.Count);
        Assert.Contains(result.Conflicts, conflict => conflict.Kind == ConflictKind.SourceWithoutDestination);
        Assert.Contains(result.Conflicts, conflict => conflict.Kind == ConflictKind.DestinationWithoutSource);
    }

    [Fact]
    public void Compare_DerivesDirectoryReferenceFromIdenticalRelativeTrees()
    {
        FileSnapshot[] source =
        [
            CreateFakeSnapshot(path: "src/one.cs", content: "one"),
            CreateFakeSnapshot(path: "src/sub/two.cs", content: "two")
        ];
        FileSnapshot[] destination =
        [
            CreateFakeSnapshot(path: "lib/one.cs", content: "one"),
            CreateFakeSnapshot(path: "lib/sub/two.cs", content: "two")
        ];

        var result = new FileComparer().Compare(source, destination);

        Assert.Contains(result.DirectoryReferences,
            reference => reference.SourcePath == "src"
                      && reference.DestinationPath == "lib");
    }


    private static FileSnapshot CreateFakeSnapshot(string path, string content)
    {
        var hashProvider = new Sha256HashProvider();
        var fixedTime = new DateTime(2026, 8, 27, 10, 30, 0, DateTimeKind.Utc);

        var contentBytes = System.Text.Encoding.UTF8.GetBytes(content);
        var contentHash = hashProvider.ComputeSha256(contentBytes);

        return new FileSnapshot(path, contentBytes.Length, fixedTime, contentHash);
    }
}
