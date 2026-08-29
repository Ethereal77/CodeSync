using CodeSync.Core;

namespace CodeSync.Tests;

public sealed class FileSnapshotTests
{
    [Fact]
    public void FileSnapshot_NormalizesHashAndKeepsUtcState()
    {
        var snapshot = new FileSnapshot(
            path: @"src\\User.cs",
            size: 12,
            lastWriteTimeUtc: new DateTime(2026, 8, 27, 10, 30, 0, DateTimeKind.Utc),
            sha256: "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA");

        Assert.Equal("src/User.cs", snapshot.Path);
        Assert.Equal(new string('a', 64), snapshot.Sha256);
        Assert.Equal(new DateTime(2026, 8, 27, 10, 30, 0, DateTimeKind.Utc), snapshot.LastWriteTimeUtc);
    }

    [Theory]
    [InlineData("short")]
    [InlineData("not-a-sha256-value")]
    public void FileSnapshot_RejectsInvalidHashes(string hash)
    {
        Assert.Throws<ArgumentException>(() => new FileSnapshot(
            path: "file.txt",
            size: 0,
            lastWriteTimeUtc: DateTime.UnixEpoch,
            sha256: hash));
    }
}
