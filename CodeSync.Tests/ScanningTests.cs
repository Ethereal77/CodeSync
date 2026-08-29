using CodeSync.Core;

namespace CodeSync.Tests;

public sealed class ScanningTests
{
    [Fact]
    public void Scan_FiltersIgnoredFilesBeforeReadingSnapshots()
    {
        var workspace = new SyntheticWorkspace(
            CreateFakeSnapshot("src/keep.cs"),
            CreateFakeSnapshot("obj/generated.cs"),
            CreateFakeSnapshot("src/readme.md"));

        var ignoreMatcher = new GitIgnoreMatcher(["obj/", "*.md"]);

        var snapshots = new FileScanner().Scan("source", workspace, ignoreMatcher);

        Assert.Equal(["src/keep.cs"], snapshots.Select(snapshot => snapshot.Path));
        Assert.Equal(["src/keep.cs"], workspace.ReadPaths);
    }

    [Fact]
    public async Task ScanAsync_ReportsPhasesAndReturnsSortedSnapshots()
    {
        var workspace = new SyntheticWorkspace(
            CreateFakeSnapshot("z-last.cs"),
            CreateFakeSnapshot("a-first.cs"),
            CreateFakeSnapshot("ignored.tmp"));

        var progress = new List<ScanProgress>();

        var snapshots = await new FileScanner().ScanAsync(
            rootDirectory: "source",
            workspace: workspace,
            ignoreMatcher: new GitIgnoreMatcher(["*.tmp"]),
            progress: new Progress<ScanProgress>(progress.Add));

        Assert.Equal(["a-first.cs", "z-last.cs"], snapshots.Select(snapshot => snapshot.Path));

        Assert.Contains(progress, value => value.Phase == ScanPhase.Enumerating);
        Assert.Contains(progress, value => value.Phase == ScanPhase.Filtering && value.Ignored == 1);
        Assert.Contains(progress, value => value.Phase == ScanPhase.Completed && value.Completed == 2);

        Assert.Equal(["a-first.cs", "z-last.cs"], workspace.ReadPaths.OrderBy(path => path));
    }

    [Fact]
    public async Task DiscoverThenScanAsync_ReusesTheDiscoveredPathList()
    {
        var workspace = new SyntheticWorkspace(CreateFakeSnapshot("file.cs"));
        var scanner = new FileScanner();
        var paths = scanner.Discover("source", workspace);

        await scanner.ScanAsync("source", paths, workspace, new GitIgnoreMatcher(Array.Empty<string>()));

        Assert.Equal(1, workspace.EnumerationCount);
    }


    private static FileSnapshot CreateFakeSnapshot(string path)
    {
        return new FileSnapshot(path, 1, DateTime.UnixEpoch, new string('a', 64));
    }

    private sealed class SyntheticWorkspace(params FileSnapshot[] snapshots) : IWorkspace
    {
        private readonly Dictionary<string, FileSnapshot> _snapshots = snapshots.ToDictionary(snapshot => snapshot.Path, StringComparer.Ordinal);

        public List<string> ReadPaths { get; } = [];
        public int EnumerationCount { get; private set; }


        public IEnumerable<string> EnumerateFiles(string rootDirectory)
        {
            EnumerationCount++;
            return _snapshots.Keys.ToArray();
        }

        public FileSnapshot ReadSnapshot(string rootDirectory, string relativePath)
        {
            ReadPaths.Add(relativePath);
            return _snapshots[relativePath];
        }

        public byte[] ReadAllBytes(string rootDirectory, string relativePath) => [];

        public void WriteAllBytes(string rootDirectory, string relativePath, ReadOnlySpan<byte> content) { }

        public void EnsureParentDirectory(string rootDirectory, string relativePath) { }
    }
}
