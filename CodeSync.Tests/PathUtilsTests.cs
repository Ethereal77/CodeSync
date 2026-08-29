using CodeSync.Core;

namespace CodeSync.Tests;

public sealed class PathUtilsTests
{
    [Fact]
    public void NormalizeFilePath_UsesForwardSlashesAndRemovesRepeatedSeparators()
    {
        var result = PathUtils.NormalizeFilePath(@"src\\models//User.cs");

        Assert.Equal("src/models/User.cs", result);
    }

    [Theory]
    [InlineData(@"C:\\source\\file.cs")]
    [InlineData(@"/source/file.cs")]
    [InlineData(@"source/../file.cs")]
    [InlineData(@"source/./file.cs")]
    public void NormalizeFilePath_RejectsRootedOrEscapingPaths(string path)
    {
        Assert.Throws<ArgumentException>(() => PathUtils.NormalizeFilePath(path));
    }

    [Fact]
    public void NormalizeDirectoryPath_AllowsTheRepositoryRoot()
    {
        Assert.Equal(string.Empty, PathUtils.NormalizeDirectoryPath(string.Empty));
    }
}
