using CodeSync.Core;

namespace CodeSync.Tests;

public sealed class FileMappingTests
{
    [Fact]
    public void FileMapping_RequiresAtLeastOneSide()
    {
        Assert.Throws<ArgumentException>(() => new FileMapping(source: null, destination: null));
    }
}
