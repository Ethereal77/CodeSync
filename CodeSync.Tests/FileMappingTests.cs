using CodeSync.Core;

namespace CodeSync.Tests;

public sealed class FileMappingTests
{
    [Fact]
    public void FileMapping_RequiresAtLeastOneSide()
    {
        Assert.Throws<ArgumentException>(() => new FileMapping(source: null, destination: null));
    }

    [Fact]
    public void FileMapping_ValidatesDestinationPathUsingPlatformComparison()
    {
        const string Path = "LIB/FILE.cs";
        const string Hash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

        var destination = new FileSnapshot("lib/file.cs", size: 1, lastWriteTimeUtc: DateTimeOffset.UnixEpoch, sha256: Hash);

        if (OperatingSystem.IsWindows() || OperatingSystem.IsMacOS())
        {
            var mapping = new FileMapping(source: null, destination, destinationPath: Path);
            Assert.Equal(destination.Path, mapping.DestinationPath);
        }
        else
        {
            Assert.Throws<ArgumentException>(() => new FileMapping(source: null, destination, destinationPath: Path));
        }
    }
}
