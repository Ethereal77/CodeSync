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
    public async Task ScanAsync_WhenFinalHashWasAlreadyReported_DoesNotReportItAgain()
    {
        var fakeSnapshot = CreateFakeSnapshot("file.cs");

        var workspace = new SyntheticWorkspace(
            snapshotDelay: TimeSpan.FromMilliseconds(300),
            snapshots: fakeSnapshot);

        var progress = new RecordingProgress();

        await new FileScanner().ScanAsync(
            rootDirectory: "source",
            workspace: workspace,
            ignoreMatcher: new GitIgnoreMatcher(Array.Empty<string>()),
            progress: progress);

        Assert.Equal(1, progress.Values.Count(value => value.Phase == ScanPhase.Hashing));
        Assert.Contains(progress.Values, value => value.Phase == ScanPhase.Hashing && value.Completed == 1);
    }

    [Fact]
    public void ScanProgressRenderer_InInteractiveMode_RewritesHashingLine()
    {
        var writer = new StringWriter { NewLine = "\n" };
        var renderer = new ScanProgressRenderer(writer, interactive: true);

        renderer.Report(new ScanProgress(ScanPhase.Hashing, Total: 120, Completed: 100, Ignored: 0,
                                         Elapsed: TimeSpan.FromSeconds(123)));
        renderer.Report(new ScanProgress(ScanPhase.Hashing, Total: 120, Completed: 1, Ignored: 0,
                                         Elapsed: TimeSpan.FromSeconds(1)));
        renderer.Report(new ScanProgress(ScanPhase.Completed, Total: 120, Completed: 1, Ignored: 0,
                                         Elapsed: TimeSpan.FromSeconds(3)));

        var output = writer.ToString();

        Assert.Contains("\r  Comparando", output);
        Assert.Contains("s)    \r  Completado", output);
        Assert.Contains("\r  Completado:", output);
        Assert.DoesNotContain("\n  Completado:", output);

        Assert.Equal(1, output.Split("Completado:", StringSplitOptions.None).Length - 1);
    }

    [Fact]
    public void ScanProgressRenderer_InRedirectedMode_WritesEachUpdateOnItsOwnLine()
    {
        var writer = new StringWriter { NewLine = "\n" };
        var renderer = new ScanProgressRenderer(writer, interactive: false);

        renderer.Report(new ScanProgress(ScanPhase.Hashing, Total: 12, Completed: 10, Ignored: 0,
                                         Elapsed: TimeSpan.FromSeconds(1)));
        renderer.Report(new ScanProgress(ScanPhase.Hashing, Total: 12, Completed: 12, Ignored: 0,
                                         Elapsed: TimeSpan.FromSeconds(2)));
        renderer.Report(new ScanProgress(ScanPhase.Completed, Total: 12, Completed: 12, Ignored: 0,
                                         Elapsed: TimeSpan.FromSeconds(3)));

        var output = writer.ToString();

        Assert.DoesNotContain('\r', output);
        Assert.Equal(2, output.Split("Comparando", StringSplitOptions.None).Length - 1);
        Assert.Contains("\n  Completado:", output);
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

        private readonly TimeSpan _snapshotDelay = TimeSpan.Zero;

        public SyntheticWorkspace(TimeSpan snapshotDelay, params FileSnapshot[] snapshots)
            : this(snapshots)
        {
            _snapshotDelay = snapshotDelay;
        }

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

        public async Task<FileSnapshot> ReadSnapshotAsync(string rootDirectory,
                                                           string relativePath,
                                                           CancellationToken cancellationToken = default)
        {
            if (_snapshotDelay > TimeSpan.Zero)
                await Task.Delay(_snapshotDelay, cancellationToken);

            return ReadSnapshot(rootDirectory, relativePath);
        }

        public byte[] ReadAllBytes(string rootDirectory, string relativePath) => [];

        public void WriteAllBytes(string rootDirectory, string relativePath, ReadOnlySpan<byte> content) { }

        public void EnsureParentDirectory(string rootDirectory, string relativePath) { }
    }


    private sealed class RecordingProgress : IProgress<ScanProgress>
    {
        public List<ScanProgress> Values { get; } = [];

        public void Report(ScanProgress value) => Values.Add(value);
    }
}
