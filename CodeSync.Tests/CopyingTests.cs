using CodeSync.Core;

namespace CodeSync.Tests;

public sealed class CopyingTests
{
    private readonly Sha256HashProvider _hashProvider = new();


    [Fact]
    public void Copy_SkipsUnchangedSourceAndRecordsIt()
    {
        var source = CreateFakeFileSnapshot(path: "src/file.cs", content: "same");
        var destination = CreateFakeFileSnapshot(path: "lib/file.cs", content: "old");
        var workspace = new MemoryWorkspace((source, "same"), (destination, "old"));

        var fakeMapping = new FileMapping(source, destination);
        var fakeProfile = CreateFakeProfile(fakeMapping);

        var result = new FileCopier().Copy(fakeProfile, workspace);

        Assert.Equal(CopyFileStatus.SkippedUnchanged, Assert.Single(result.Files).Status);
        Assert.Equal(["src/file.cs"], result.SkippedSourcePaths);

        Assert.Empty(workspace.Writes);
    }

    [Fact]
    public void Copy_WritesChangedSourceAndUpdatesBothSnapshots()
    {
        var previousSource = CreateFakeFileSnapshot(path: "src/file.cs", content: "old");
        var currentSource = CreateFakeFileSnapshot(path: "src/file.cs", content: "new");

        var destination = CreateFakeFileSnapshot(path: "lib/file.cs", content: "old");

        var workspace = new MemoryWorkspace((currentSource, "new"), (destination, "old"));

        var fakeMapping = new FileMapping(previousSource, destination);
        var fakeProfile = CreateFakeProfile(fakeMapping);

        var result = new FileCopier().Copy(fakeProfile, workspace);

        Assert.Equal(CopyFileStatus.Copied, Assert.Single(result.Files).Status);
        Assert.Single(workspace.Writes);

        var updated = Assert.Single(result.UpdatedProfile.FileMappings);
        Assert.Equal(currentSource.Sha256, updated.Source!.Sha256);
        Assert.Equal(currentSource.Sha256, updated.Destination!.Sha256);
    }

    [Fact]
    public void Copy_IgnoresMappingsWithoutDestination()
    {
        var source = CreateFakeFileSnapshot(path: "src/ignored.cs", content: "ignored");

        var workspace = new MemoryWorkspace((source, "ignored"));

        var fakeMapping = new FileMapping(source, destination: null, isIgnored: true);
        var fakeProfile = CreateFakeProfile(fakeMapping);

        var result = new FileCopier().Copy(fakeProfile, workspace);

        Assert.Equal(CopyFileStatus.Ignored, Assert.Single(result.Files).Status);
    }

    [Fact]
    public void Copy_IgnoresMappingWithSuggestedDestinationWithoutReadingIt()
    {
        var source = CreateFakeFileSnapshot(path: "src/ignored.cs", content: "ignored");
        var workspace = new MemoryWorkspace((source, "ignored"));
        var mapping = new FileMapping(source,
                                      destination: null,
                                      isIgnored: true,
                                      destinationPath: "lib/ignored.cs");

        var result = new FileCopier().Copy(CreateFakeProfile(mapping), workspace);

        var file = Assert.Single(result.Files);
        Assert.Equal(CopyFileStatus.Ignored, file.Status);
        Assert.Equal("lib/ignored.cs", file.DestinationPath);
        Assert.Empty(workspace.Writes);
    }

    [Fact]
    public void Copy_CreatesDestinationWhenMappingHasNoDestinationSnapshot()
    {
        var source = CreateFakeFileSnapshot(path: "src/new.cs", content: "new");
        var destination = CreateFakeFileSnapshot(path: "lib/new.cs", content: "old");
        var workspace = new MemoryWorkspace((source, "new"), (destination, "old"));
        var mapping = new FileMapping(source,
                                      destination: null,
                                      destinationPath: "lib/new.cs");

        var result = new FileCopier().Copy(CreateFakeProfile(mapping), workspace);

        Assert.Equal(CopyFileStatus.Copied, Assert.Single(result.Files).Status);
        Assert.Single(workspace.Writes);
        Assert.Equal("lib/new.cs", workspace.Writes[0].Path);
    }

    [Fact]
    public void Copy_RejectsNonIgnoredMappingWithoutSource()
    {
        var destination = CreateFakeFileSnapshot(path: "lib/file.cs", content: "destination");
        var profile = CreateFakeProfile(new FileMapping(source: null, destination));

        var exception = Assert.Throws<InvalidOperationException>(
            () => new FileCopier().Copy(profile, new MemoryWorkspace((destination, "destination"))));

        Assert.Contains("source", exception.Message);
    }

    [Fact]
    public void Copy_RejectsNonIgnoredMappingWithoutDestinationPath()
    {
        var source = CreateFakeFileSnapshot(path: "src/file.cs", content: "source");
        var profile = CreateFakeProfile(new FileMapping(source, destination: null));

        var exception = Assert.Throws<InvalidOperationException>(
            () => new FileCopier().Copy(profile, new MemoryWorkspace((source, "source"))));

        Assert.Contains("destination path", exception.Message);
    }

    [Fact]
    public void Copy_DryRunDoesNotWriteOrUpdateProfile()
    {
        var previous = CreateFakeFileSnapshot(path: "src/file.cs", content: "old");
        var current = CreateFakeFileSnapshot(path: "src/file.cs", content: "new");

        var destination = CreateFakeFileSnapshot(path: "lib/file.cs", content: "old");

        var workspace = new MemoryWorkspace((current, "new"), (destination, "old"));

        var fakeMapping = new FileMapping(previous, destination);
        var fakeProfile = CreateFakeProfile(fakeMapping);
        var result = new FileCopier().Copy(fakeProfile, workspace, new CopyOptions(DryRun: true));

        Assert.Equal(CopyFileStatus.Copied, Assert.Single(result.Files).Status);
        Assert.Empty(workspace.Writes);
        Assert.Equal(previous.Sha256, Assert.Single(result.UpdatedProfile.FileMappings).Source!.Sha256);
    }


    private SyncProfile CreateFakeProfile(params FileMapping[] mappings)
    {
        return new SyncProfile(sourceDirectory: "source",
                               destinationDirectory: "destination",
                               directoryReferences: [],
                               mappings);
    }

    private FileSnapshot CreateFakeFileSnapshot(string path, string content)
    {
        var fixedTime = new DateTime(2026, 8, 27, 10, 30, 0, DateTimeKind.Utc);

        var bytes = System.Text.Encoding.UTF8.GetBytes(content);

        return new FileSnapshot(path, bytes.Length, fixedTime, _hashProvider.ComputeSha256(bytes));
    }


    private sealed class MemoryWorkspace : IWorkspace
    {
        private readonly Dictionary<(string Root, string Path), (FileSnapshot Snapshot, byte[] Content)> _files = [];

        public List<(string Root, string Path, byte[] Content)> Writes { get; } = [];


        public MemoryWorkspace(params (FileSnapshot Snapshot, string Content)[] files)
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
            var root = RootKey(rootDirectory);

            return _files.Keys
                .Where(key => key.Root == root)
                .Select(key => key.Path)
                .ToArray();
        }

        public FileSnapshot ReadSnapshot(string rootDirectory, string relativePath)
        {
            return _files[(RootKey(rootDirectory), relativePath)].Snapshot;
        }

        public byte[] ReadAllBytes(string rootDirectory, string relativePath)
        {
            return _files[(RootKey(rootDirectory), relativePath)].Content;
        }

        public void WriteAllBytes(string rootDirectory, string relativePath, ReadOnlySpan<byte> content)
        {
            var bytes = content.ToArray();
            var root = RootKey(rootDirectory);

            var prevSnapshot = _files[(root, relativePath)].Snapshot;
            var newSnapshot = new FileSnapshot(relativePath,
                                               size: bytes.Length,
                                               lastWriteTimeUtc: prevSnapshot.LastWriteTimeUtc,
                                               sha256: new Sha256HashProvider().ComputeSha256(bytes));

            _files[(root, relativePath)] = (newSnapshot, bytes);
            Writes.Add((root, relativePath, bytes));
        }

        public void EnsureParentDirectory(string rootDirectory, string relativePath)
        {
        }


        private static string RootKey(string rootDirectory)
        {
            return rootDirectory.EndsWith("source", StringComparison.OrdinalIgnoreCase)
                ? "source"
                : rootDirectory.EndsWith("destination", StringComparison.OrdinalIgnoreCase)
                    ? "destination"
                    : rootDirectory;
        }
    }
}
