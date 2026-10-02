using CodeSync.Core;
using CodeSync.Core.Xml;

namespace CodeSync.Tests;

public sealed class XmlSidecarStoreTests
{
    [Fact]
    public void Save_WhenDestinationIsDirectory_ThrowsFileSaveExceptionWithPath()
    {
        var temporaryDirectory = Path.Combine(Path.GetTempPath(), "CodeSyncTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporaryDirectory);

        try
        {
            var exception = Assert.Throws<FileSaveException>(
                () =>
                {
                    var conflictSet = new ConflictSet("source", "destination", conflicts: []);
                    new XmlConflictStore().Save(temporaryDirectory, conflictSet);
                });

            Assert.Equal(temporaryDirectory, exception.Path);
            Assert.NotNull(exception.InnerException);
        }
        finally
        {
            if (Directory.Exists(temporaryDirectory))
                Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    [Fact]
    public void Save_ConflictAndSkippedSidecarsDoNotCreateBackups()
    {
        var temporaryDirectory = Path.Combine(Path.GetTempPath(), "CodeSyncTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporaryDirectory);

        var conflictsPath = Path.Combine(temporaryDirectory, "profile.conflicts.xml");
        var skippedPath = Path.Combine(temporaryDirectory, "profile.skipped.xml");

        try
        {
            var conflictStore = new XmlConflictStore();
            var conflicts = new ConflictSet("source", "destination", []);
            conflictStore.Save(conflictsPath, conflicts);
            conflictStore.Save(conflictsPath, conflicts);

            var skippedStore = new XmlSkippedStore();
            skippedStore.Save(skippedPath, sourcePaths: []);
            skippedStore.Save(skippedPath, sourcePaths: []);

            Assert.False(File.Exists(conflictsPath + ".bak"));
            Assert.False(File.Exists(skippedPath + ".bak"));
        }
        finally
        {
            if (Directory.Exists(temporaryDirectory))
                Directory.Delete(temporaryDirectory, recursive: true);
        }
    }
}
