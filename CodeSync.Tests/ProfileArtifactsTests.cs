using CodeSync.Core;

namespace CodeSync.Tests;

public sealed class ProfileArtifactsTests
{
    [Fact]
    public void ProfileArtifacts_DeriveSidecarsFromTheProfileFileName()
    {
        Assert.EndsWith("profile.conflicts.xml", ProfileArtifacts.GetConflictsPath(@"profiles\\profile.xml"));
        Assert.EndsWith("profile.skipped.xml", ProfileArtifacts.GetSkippedPath(@"profiles\\profile.xml"));
    }
}
