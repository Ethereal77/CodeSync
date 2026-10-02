using CodeSync.Core;

namespace CodeSync;

internal sealed partial class App
{
    /// <summary>
    ///   Compares the source and destination directories and generates a synchronization profile.
    /// </summary>
    /// <param name="args">The command-line arguments passed to the program.</param>
    /// <returns>The exit code indicating the result of the comparison.</returns>
    private async Task<int> Compare(string[] args)
    {
        ReadOnlySpan<string> cliArgs = args.AsSpan(start: 1);

        if (cliArgs.Length != 3)
        {
            Console.Error.WriteLine("Uso: CodeSync compare <source-directory> <destination-directory> <profile.xml>");
            return ExitCodeInvalidArguments;
        }

        var sourceDir = RequireDirectory(cliArgs[0], "source");
        var destDir = RequireDirectory(cliArgs[1], "destination");

        var profilePath = Path.GetFullPath(cliArgs[2]);

        Console.WriteLine($"Comparando...");
        Console.WriteLine($"  Origen: {sourceDir}");
        Console.WriteLine($"  Destino: {destDir}");

        var scanner = new FileScanner(Workspace);

        var sourceFiles = await ScanDirectory(scanner, sourceDir, "source");
        var destFiles = await ScanDirectory(scanner, destDir, "destination");

        // Compare the scanned files, determining matches, conflicts, and missing files
        var comparer = new FileComparer();
        var result = comparer.Compare(sourceFiles, destFiles);

        // Compose the synchronization profile and save it
        var profile = new SyncProfile(sourceDir, destDir,
                                      result.DirectoryReferences, result.FileMappings);

        profileStore.SaveNew(profilePath, profile, sourceFiles, destFiles);

        // Save also the found conflicts to the conflict store
        var conflicts = new ConflictSet(sourceDir, destDir, result.Conflicts);

        conflictStore.Save(ProfileArtifacts.GetConflictsPath(profilePath), conflicts);

        Console.WriteLine();
        Console.WriteLine($"Se han encontrado {result.FileMappings.Count} coincidencias ({result.Conflicts.Count} conflictos pendientes).");
        Console.WriteLine("--------------------------------------------------");
        Console.WriteLine($"Perfil de sincronización guardado en: {profilePath}");

        return result.Conflicts.Count == 0 ? ExitCodeSuccess : ExitCodeError;
    }
}
