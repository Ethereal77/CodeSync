using CodeSync.Core;

namespace CodeSync.Tests;

public sealed class PipelineTests
{
    private static readonly DateTime FixedTime = new(2026, 8, 27, 10, 30, 0, DateTimeKind.Utc);
    private static readonly Sha256HashProvider HashProvider = new();


    [Fact]
    public void CompareVerifyCopyAndUpdate_WorkAsOneSyntheticPipeline()
    {
        var initialSource = CreateFakeFileSnapshot(path: "src/changed.cs", content: "old");
        var unchangedSource = CreateFakeFileSnapshot(path: "src/unchanged.cs", content: "same");

        var initialDestination = CreateFakeFileSnapshot(path: "lib/changed.cs", content: "old");
        var unchangedDestination = CreateFakeFileSnapshot(path: "lib/unchanged.cs", content: "same");

        var comparison = new FileComparer().Compare(
            sourceFiles: [initialSource, unchangedSource],
            destinationFiles: [initialDestination, unchangedDestination]);

        var profile = new SyncProfile("source", "destination",
                                      comparison.DirectoryReferences, comparison.FileMappings);

        Assert.Empty(comparison.Conflicts);

        var serializedProfile = XmlCodecs.SerializeProfile(profile);
        profile = XmlCodecs.DeserializeProfile(serializedProfile);

        var currentSource = CreateFakeFileSnapshot(path: "src/changed.cs", content: "new");

        var workspace = new SyntheticWorkspace(
            (currentSource, Content: "new"),
            (unchangedSource, Content: "same"),
            (initialDestination, Content: "old"),
            (unchangedDestination, Content: "same"));

        var verification = new ProfileVerifier().Verify(
            profile,
            sourceFiles: [currentSource, unchangedSource],
            destinationFiles: [initialDestination, unchangedDestination]);

        Assert.True(verification.IsValid);

        var copyResults = new FileCopier().Copy(profile, workspace);

        Assert.Equal(2, copyResults.Files.Count);
        Assert.Contains(copyResults.Files, file => file.Status == CopyFileStatus.Copied);
        Assert.Contains(copyResults.Files, file => file.Status == CopyFileStatus.SkippedUnchanged);

        serializedProfile = XmlCodecs.SerializeProfile(copyResults.UpdatedProfile);
        var persistedProfile = XmlCodecs.DeserializeProfile(serializedProfile);

        var updateResults = new ProfileUpdater().Update(
            persistedProfile,
            skippedSourcePaths: copyResults.SkippedSourcePaths,
            currentSourceFiles: [currentSource, unchangedSource]);

        Assert.True(updateResults.Succeeded);
        Assert.Single(updateResults.UpdatedSourcePaths);

        var singleUpdatedMapping = updateResults.UpdatedProfile.FileMappings.Single(mapping => mapping.Source!.Path == "src/changed.cs");
        Assert.Equal(currentSource.Sha256,
                     singleUpdatedMapping.Source!.Sha256);
    }


    private FileSnapshot CreateFakeFileSnapshot(string path, string content)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);

        return new FileSnapshot(path,
                                size: bytes.Length,
                                lastWriteTimeUtc: FixedTime,
                                sha256: HashProvider.ComputeSha256(bytes));
    }


    private sealed class SyntheticWorkspace : IWorkspace
    {
        private readonly Dictionary<(string Root, string Path), (FileSnapshot Snapshot, byte[] Content)> _files = [];


        public SyntheticWorkspace(params (FileSnapshot Snapshot, string Content)[] files)
        {
            foreach (var (snapshot, content) in files)
            {
                var root = snapshot.Path.StartsWith("src/", StringComparison.Ordinal)
                    ? "source"
                    : "destination";

                _files[(root, snapshot.Path)] = (snapshot, System.Text.Encoding.UTF8.GetBytes(content));
            }
        }

        public IEnumerable<string> EnumerateFiles(string rootDirectory)
        {
            var root = Root(rootDirectory);

            return _files.Keys
                .Where(key => key.Root == root)
                .Select(key => key.Path)
                .ToArray();
        }

        public FileSnapshot ReadSnapshot(string rootDirectory, string relativePath)
        {
            return _files[(Root(rootDirectory), relativePath)].Snapshot;
        }

        public byte[] ReadAllBytes(string rootDirectory, string relativePath)
        {
            return _files[(Root(rootDirectory), relativePath)].Content;
        }

        public void WriteAllBytes(string rootDirectory, string relativePath, ReadOnlySpan<byte> content)
        {
            var root = Root(rootDirectory);
            var bytes = content.ToArray();

            var oldSnapshot = _files[(root, relativePath)].Snapshot;
            var newSnapshot = new FileSnapshot(relativePath,
                                               size: bytes.Length,
                                               lastWriteTimeUtc: oldSnapshot.LastWriteTimeUtc,
                                               sha256: HashProvider.ComputeSha256(bytes));

            _files[(root, relativePath)] = (newSnapshot, bytes);
        }

        public void EnsureParentDirectory(string rootDirectory, string relativePath)
        {
        }


        private static string Root(string rootDirectory)
        {
            return rootDirectory.EndsWith("source", StringComparison.OrdinalIgnoreCase)
                ? "source"
                : "destination";
        }
    }
}
