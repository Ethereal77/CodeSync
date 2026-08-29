using System.Collections.Concurrent;
using System.Diagnostics;

namespace CodeSync.Core;

/// <summary>
///   Scans the specified root directory within the given workspace,
///   returning a list of file snapshots that are not ignored.
/// </summary>
public sealed class FileScanner
{
    // Default progress interval for throttled reporting
    private static readonly TimeSpan ProgressInterval = TimeSpan.FromMilliseconds(250);


    /// <summary>
    ///   Scans the specified root directory within the given workspace,
    ///   returning a list of normalized file paths.
    /// </summary>
    /// <param name="rootDirectory">The root directory to scan.</param>
    /// <param name="workspace">The workspace containing the files to scan.</param>
    /// <exception cref="ArgumentException">
    ///   Thrown if <paramref name="rootDirectory"/> is <see langword="null"/>, empty,
    ///   or consists only of white-space characters.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    ///   Thrown if <paramref name="workspace"/> or <paramref name="rootDirectory"/> is <see langword="null"/>.
    /// </exception>
    public IReadOnlyList<string> Discover(string rootDirectory, IWorkspace workspace)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        ArgumentNullException.ThrowIfNull(workspace);

        return workspace.EnumerateFiles(rootDirectory)
            .Select(PathUtils.NormalizeFilePath)
            .ToArray();
    }

    /// <summary>
    ///   Scans the specified root directory within the given workspace,
    ///   returning a list of file snapshots that are not ignored.
    /// </summary>
    /// <param name="rootDirectory">The root directory to scan.</param>
    /// <param name="workspace">The workspace containing the files to scan.</param>
    /// <param name="ignoreMatcher">The ignore matcher used to filter out ignored files.</param>
    /// <returns>A list of file snapshots that are not ignored.</returns>
    /// <exception cref="ArgumentException">
    ///   Thrown if <paramref name="rootDirectory"/> is <see langword="null"/>, empty,
    ///   or consists only of white-space characters.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    ///   Thrown if <paramref name="workspace"/> or <paramref name="ignoreMatcher"/> is <see langword="null"/>.
    /// </exception>
    /// <remarks>
    ///   This method is synchronous and blocks until the scan is complete.
    /// </remarks>
    public IReadOnlyList<FileSnapshot> Scan(string rootDirectory,
                                            IWorkspace workspace,
                                            IIgnoreMatcher ignoreMatcher)
    {
        return ScanAsync(rootDirectory, workspace, ignoreMatcher).GetAwaiter().GetResult();
    }

    /// <summary>
    ///   Scans the specified root directory within the given workspace,
    ///   returning a list of file snapshots that are not ignored.
    /// </summary>
    /// <param name="rootDirectory">The root directory to scan.</param>
    /// <param name="workspace">The workspace containing the files to scan.</param>
    /// <param name="ignoreMatcher">The ignore matcher used to filter out ignored files.</param>
    /// <param name="progress">An optional progress reporter for scan progress updates.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A list of file snapshots that are not ignored.</returns>
    /// <exception cref="ArgumentException">
    ///   Thrown if <paramref name="rootDirectory"/> is <see langword="null"/>, empty,
    ///   or consists only of white-space characters.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    ///   Thrown if <paramref name="workspace"/> or <paramref name="ignoreMatcher"/> is <see langword="null"/>.
    /// </exception>
    public Task<IReadOnlyList<FileSnapshot>> ScanAsync(string rootDirectory,
                                                       IWorkspace workspace,
                                                       IIgnoreMatcher ignoreMatcher,
                                                       IProgress<ScanProgress>? progress = null,
                                                       CancellationToken cancellationToken = default)
    {
        var discoveredPaths = Discover(rootDirectory, workspace);

        return ScanAsync(rootDirectory, discoveredPaths, workspace, ignoreMatcher, progress, cancellationToken);
    }

    /// <summary>
    ///   Scans the specified root directory within the given workspace,
    ///   returning a list of file snapshots that are not ignored.
    /// </summary>
    /// <param name="rootDirectory">The root directory to scan.</param>
    /// <param name="discoveredPaths">The list of discovered normalized file paths to scan.</param>
    /// <param name="workspace">The workspace containing the files to scan.</param>
    /// <param name="ignoreMatcher">The ignore matcher used to filter out ignored files.</param>
    /// <param name="progress">An optional progress reporter for scan progress updates.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A list of file snapshots that are not ignored.</returns>
    /// <exception cref="ArgumentException">
    ///   Thrown if <paramref name="rootDirectory"/> is <see langword="null"/>, empty,
    ///   or consists only of white-space characters.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    ///   Thrown if <paramref name="workspace"/>, <paramref name="ignoreMatcher"/>, or <paramref name="discoveredPaths"/>
    ///   is <see langword="null"/>.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    ///   Thrown if the operation is canceled via the <paramref name="cancellationToken"/>.
    /// </exception>
    public async Task<IReadOnlyList<FileSnapshot>> ScanAsync(string rootDirectory,
                                                             IReadOnlyCollection<string> discoveredPaths,
                                                             IWorkspace workspace,
                                                             IIgnoreMatcher ignoreMatcher,
                                                             IProgress<ScanProgress>? progress = null,
                                                             CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        ArgumentNullException.ThrowIfNull(discoveredPaths);
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(ignoreMatcher);

        var stopwatch = Stopwatch.StartNew();

        // Report initial progress for the enumeration phase
        progress?.Report(new ScanProgress(ScanPhase.Enumerating, Total: discoveredPaths.Count, Completed: discoveredPaths.Count, Ignored: 0, Elapsed: stopwatch.Elapsed));

        cancellationToken.ThrowIfCancellationRequested();

        // Filter out ignored paths
        var candidates = discoveredPaths
            .Where(path => !ignoreMatcher.IsIgnored(path, isDirectory: false))
            .ToArray();

        var ignored = discoveredPaths.Count - candidates.Length;

        progress?.Report(new ScanProgress(ScanPhase.Filtering, Total: discoveredPaths.Count, Completed: candidates.Length, Ignored: ignored, Elapsed: stopwatch.Elapsed));

        cancellationToken.ThrowIfCancellationRequested();

        // Compute file snapshots for the candidate paths in parallel to overlap I/O and CPU-bound work
        var completed = 0;
        var snapshots = new ConcurrentBag<FileSnapshot>();
        // We use a throttled reporter to avoid excessive progress updates
        // TODO: Simplify ScanProgress reporting
        var reporter = new ThrottledReporter(progress, ProgressInterval, total: discoveredPaths.Count, candidateCount: candidates.Length, ignored: ignored);

        await Parallel.ForEachAsync(candidates,
                                    new ParallelOptions
                                    {
                                        MaxDegreeOfParallelism = Environment.ProcessorCount,
                                        CancellationToken = cancellationToken
                                    },
                                    async (path, token) =>
                                    {
                                        // Take the snapshot of the current file, including size, last modified time, and content hash
                                        var snapshot = await workspace.ReadSnapshotAsync(rootDirectory, path, token);
                                        snapshots.Add(snapshot);

                                        reporter.Report(Interlocked.Increment(ref completed));
                                    });

        reporter.Report(candidates.Length, force: true);
        progress?.Report(new ScanProgress(ScanPhase.Completed, Total: discoveredPaths.Count, Completed: candidates.Length, Ignored: ignored, Elapsed: stopwatch.Elapsed));

        // Return the snapshots ordered by their paths to ensure consistent ordering
        return snapshots.OrderBy(snapshot => snapshot.Path, StringComparer.Ordinal).ToArray();
    }
}
