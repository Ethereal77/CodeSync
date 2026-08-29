using CodeSync.Core;

namespace CodeSync.Tests;

public sealed class SyncProfileTests
{
    [Fact]
    public void SyncProfile_MaterializesItsCollections()
    {
        var mapping = new FileMapping(
            new FileSnapshot(
                path: "source.txt",
                size: 1,
                lastWriteTimeUtc: DateTime.UnixEpoch,
                sha256: new string('a', 64)),
            destination: null);

        DirectoryReference[] references = [new DirectoryReference("src", "lib")];

        var profile = new SyncProfile("source", "destination", references, [mapping]);

        Assert.Equal(Path.GetFullPath("source"), profile.SourceDirectory);
        Assert.Single(profile.DirectoryReferences);
        Assert.Single(profile.FileMappings);
    }
}
