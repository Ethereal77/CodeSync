using System.Diagnostics;

using CodeSync.Core;
using CodeSync.Infrastructure;

namespace CodeSync;

internal static class Program
{
    private const int ExitCodeSuccess = 0;
    private const int ExitCodeError = 1;
    private const int ExitCodeInvalidArguments = 2;

    private static readonly PhysicalWorkspace Workspace = new();

    private static readonly FileScanner Scanner = new();
    private static readonly XmlProfileStore ProfileStore = new();
    private static readonly XmlConflictStore ConflictStore = new();
    private static readonly XmlSkippedStore SkippedStore = new();


    /// <summary>
    ///   The main entry point for the CodeSync application.
    /// </summary>
    /// <param name="args">The command-line arguments passed to the application.</param>
    /// <returns>The exit code indicating the result of the application's execution.</returns>
    private static async Task<int> Main(string[] args)
    {
        Console.WriteLine("CodeSync - Sincroniza archivos entre dos directorios");
        Console.WriteLine("© Infinisis 2026");
        Console.WriteLine();

        if (args.Length == 0)
        {
            PrintUsage();
            return ExitCodeInvalidArguments;
        }

        try
        {
            // Determine which command to execute based on the first argument
            var command = args[0].ToLowerInvariant();

            return command switch
            {
                "compare" => await Compare(args),
                "verify" => await Verify(args),
                "copy" => Copy(args),
                "update" => await Update(args),

                _ => UnknownCommand(command)
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return ExitCodeError;
        }
    }

    /// <summary>
    ///   Compares the source and destination directories and generates a synchronization profile.
    /// </summary>
    /// <param name="args">The command-line arguments passed to the program.</param>
    /// <returns>The exit code indicating the result of the comparison.</returns>
    private static async Task<int> Compare(string[] args)
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

        var sourceFiles = await ScanDirectory(sourceDir, "source");
        var destFiles = await ScanDirectory(destDir, "destination");

        // Compare the scanned files, determining matches, conflicts, and missing files
        var result = new FileComparer().Compare(sourceFiles, destFiles);

        // Compose the synchronization profile and save it
        var profile = new SyncProfile(sourceDir, destDir,
                                      result.DirectoryReferences, result.FileMappings);

        ProfileStore.Save(profilePath, profile);

        // Save also the found conflicts to the conflict store
        var conflicts = new ConflictSet(sourceDir, destDir, result.Conflicts);

        ConflictStore.Save(ProfileArtifacts.GetConflictsPath(profilePath), conflicts);

        Console.WriteLine();
        Console.WriteLine($"Se han encontrado {result.FileMappings.Count} coincidencias ({result.Conflicts.Count} conflictos pendientes).");
        Console.WriteLine("--------------------------------------------------");
        Console.WriteLine($"Perfil de sincronización guardado en: {profilePath}");

        return result.Conflicts.Count == 0 ? ExitCodeSuccess : ExitCodeError;
    }

    /// <summary>
    ///   Verifies a synchronization profile against the current state of the source and destination directories.
    /// </summary>
    /// <param name="args">The command-line arguments passed to the program.</param>
    /// <returns>The exit code indicating the result of the verification.</returns>
    private static async Task<int> Verify(string[] args)
    {
        ReadOnlySpan<string> cliArgs = args.AsSpan(start: 1);

        if (cliArgs.Length != 1)
        {
            Console.Error.WriteLine("Uso: CodeSync verify <profile.xml>");
            return ExitCodeInvalidArguments;
        }

        var profilePath = RequireFile(cliArgs[0], "profile");
        var existingConflicts = ConflictStore.Load(ProfileArtifacts.GetConflictsPath(profilePath));

        // Check if there are existing conflicts before proceeding with verification
        if (existingConflicts is not null && existingConflicts.Conflicts.Count > 0)
        {
            Console.Error.WriteLine($"La verificación se cancela: hay {existingConflicts.Conflicts.Count} conflictos sin resolver.");
            return ExitCodeError;
        }

        // Load the synchronization profile for verification
        var profile = ProfileStore.Load(profilePath);

        // Scan the source and destination directories based on the loaded profile
        var source = await ScanDirectory(profile.SourceDirectory, "source");
        var destination = await ScanDirectory(profile.DestinationDirectory, "destination");

        // Verify the profile against the scanned directories
        var result = new ProfileVerifier().Verify(profile, source, destination);

        // Save the found conflicts to the conflict store
        var conflicts = new ConflictSet(profile.SourceDirectory, profile.DestinationDirectory, result.Conflicts);

        ConflictStore.Save(ProfileArtifacts.GetConflictsPath(profilePath), conflicts);

        Console.WriteLine(result.IsValid
            ? "Verificación correcta: no se han encontrado conflictos."
            : $"Verificación fallida: {result.Conflicts.Count} conflictos pendientes.");

        return result.IsValid ? ExitCodeSuccess : ExitCodeError;
    }

    /// <summary>
    ///   Loads a valid synchronization profile (with no unresolved conflicts) and performs
    ///   the copy operation from the source to the destination directory as defined in the profile.
    /// </summary>
    /// <param name="args">The command-line arguments passed to the program.</param>
    /// <returns>The exit code indicating the result of the copy operation.</returns>
    private static int Copy(string[] args)
    {
        ReadOnlySpan<string> cliArgs = args.AsSpan(start: 1);

        // Dry-run flag indicates whether the copy operation should be simulated without making actual changes
        var dryRun = cliArgs.Length > 1 && cliArgs[1] is "-d" or "--dry-run";

        if (cliArgs.Length < 1 || (dryRun && cliArgs.Length < 2))
        {
            Console.Error.WriteLine("Uso: CodeSync copy <profile.xml> [--dry-run]");
            return ExitCodeInvalidArguments;
        }

        var profilePath = RequireFile(cliArgs[0], "profile");
        var conflicts = ConflictStore.Load(ProfileArtifacts.GetConflictsPath(profilePath));

        // If there are unresolved conflicts, cancel the copy operation
        if (conflicts is not null && conflicts.Conflicts.Count > 0)
        {
            Console.Error.WriteLine($"La copia se cancela: hay {conflicts.Conflicts.Count} conflictos sin resolver.");
            return ExitCodeError;
        }

        var profile = ProfileStore.Load(profilePath);

        // Perform the copy operation using the loaded profile and the specified options
        var result = new FileCopier().Copy(profile, Workspace, new CopyOptions(dryRun));

        if (!dryRun)
        {
            // Save the updated profile and any skipped files if the copy operation was not a dry run
            ProfileStore.Save(profilePath, result.UpdatedProfile);
            SkippedStore.Save(ProfileArtifacts.GetSkippedPath(profilePath), result.SkippedSourcePaths);
        }

        // Display the status of each file involved in the copy operation.
        foreach (var file in result.Files)
            Console.WriteLine($"{file.Status}: {file.SourcePath} -> {file.DestinationPath ?? "(ignored)"}");

        return result.Succeeded ? ExitCodeSuccess : ExitCodeError;
    }

    /// <summary>
    ///   Loads the specified profile and updates it based on the current state of the source directory.
    /// </summary>
    /// <param name="args">The command-line arguments passed to the program.</param>
    /// <returns>The exit code indicating the result of the update operation.</returns>
    private static async Task<int> Update(string[] args)
    {
        ReadOnlySpan<string> cliArgs = args.AsSpan(start: 1);

        if (cliArgs.Length != 1)
        {
            Console.Error.WriteLine("Uso: CodeSync update <profile.xml>");
            return ExitCodeInvalidArguments;
        }

        var profilePath = RequireFile(cliArgs[0], "profile");
        var profile = ProfileStore.Load(profilePath);

        // Load the previously skipped files for this profile
        var skippedPath = ProfileArtifacts.GetSkippedPath(profilePath);
        var skipped = SkippedStore.Load(skippedPath);

        // Scan the source directory to get the current state of the files
        var source = await ScanDirectory(profile.SourceDirectory, "source");

        // Update the profile based on the current state of the source directory and previously skipped files
        var result = new ProfileUpdater().Update(profile, skipped, source);

        foreach (var error in result.Errors)
            Console.Error.WriteLine($"Error: {error}");
        if (!result.Succeeded)
            return ExitCodeError;

        // Save the updated profile and clear the skipped files for this profile
        ProfileStore.Save(profilePath, result.UpdatedProfile);
        SkippedStore.Save(skippedPath, []);

        Console.WriteLine($"Actualizados {result.UpdatedSourcePaths.Count} archivos omitidos.");
        return ExitCodeSuccess;
    }


    /// <summary>
    ///   Discovers paths, loads their ignore rules and scans the accepted files,
    ///   returning a list of file snapshots.
    /// </summary>
    private static async Task<IReadOnlyList<FileSnapshot>> ScanDirectory(string rootDirectory, string label)
    {
        Console.WriteLine();
        Console.WriteLine($"Escaneando directorio ({label}): {rootDirectory}");

        var timer = Stopwatch.StartNew();

        var paths = Scanner.Discover(rootDirectory, Workspace);
        var discoveryElapsed = timer.Elapsed;

        Console.WriteLine($"  Se han encontrado {paths.Count} archivos en {discoveryElapsed.TotalSeconds:F1}s.");

        timer.Restart();

        var matcher = LoadIgnoreMatcher(rootDirectory, paths);
        var ignoreElapsed = timer.Elapsed;

        Console.WriteLine($"  Se han cargado las reglas .gitignore en {ignoreElapsed.TotalSeconds:F1}s.");

        var renderer = new ScanProgressRenderer(Console.Out, interactive: !Console.IsOutputRedirected);

        try
        {
            return await Scanner.ScanAsync(rootDirectory, paths, Workspace, matcher, renderer);
        }
        finally
        {
            renderer.FinishDynamicLine();
        }
    }

    /// <summary>
    ///   Loads the <c>.gitignore</c> matcher for the specified root directory.
    /// </summary>
    /// <param name="rootDirectory">The root directory for which to load the <c>.gitignore</c> matcher.</param>
    /// <returns>
    ///   An instance of <see cref="GitIgnoreMatcher"/> configured with the rules from
    ///   the <c>.gitignore</c> files in the specified root directory and its subdirectories.
    /// </returns>
    private static GitIgnoreMatcher LoadIgnoreMatcher(string rootDirectory, IEnumerable<string> discoveredPaths)
    {
        List<IgnoreRuleSet> ruleSets =
        [
            // By default, ignore the .git directory
            new(basePath: string.Empty, rules: [".git/"])
        ];

        // Compile all the .gitignore rules into the rule sets
        foreach (var relativePath in discoveredPaths.Where(path =>
                     string.Equals(Path.GetFileName(path), ".gitignore", StringComparison.OrdinalIgnoreCase)))
        {
            var normalizedPath = PathUtils.NormalizeFilePath(relativePath);
            var fullPath = Path.Combine(rootDirectory, normalizedPath.Replace('/', Path.DirectorySeparatorChar));
            var basePath = PathUtils.NormalizeDirectoryPath(Path.GetDirectoryName(normalizedPath) ?? string.Empty);

            ruleSets.Add(new IgnoreRuleSet(basePath, File.ReadLines(fullPath)));
        }

        // Create a matcher with the compiled rule sets
        return new GitIgnoreMatcher(ruleSets);
    }


    /// <summary>
    ///   Ensures that the specified path is an existing directory.
    /// </summary>
    /// <param name="path">The path to the directory.</param>
    /// <param name="label">A label used in error messages.</param>
    /// <returns>The full path to the existing directory.</returns>
    /// <exception cref="DirectoryNotFoundException">Thrown if the specified directory does not exist.</exception>
    private static string RequireDirectory(string path, string label)
    {
        var fullPath = Path.GetFullPath(path);

        if (!Directory.Exists(fullPath))
            throw new DirectoryNotFoundException($"El directorio {label} no existe: {fullPath}");

        return fullPath;
    }

    /// <summary>
    ///   Ensures that the specified path is an existing file.
    /// </summary>
    /// <param name="path">The path to the file.</param>
    /// <param name="label">A label used in error messages.</param>
    /// <returns>The full path to the existing file.</returns>
    /// <exception cref="FileNotFoundException">Thrown if the specified file does not exist.</exception>
    private static string RequireFile(string path, string label)
    {
        var fullPath = Path.GetFullPath(path);

        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"El archivo {label} no existe: {fullPath}");

        return fullPath;
    }

    /// <summary>
    ///   Handles the case when an unknown command is encountered.
    /// </summary>
    /// <param name="command">The unknown command that was encountered.</param>
    /// <returns>The exit code indicating an unrecognized command.</returns>
    private static int UnknownCommand(string command)
    {
        Console.Error.WriteLine($"Comando no reconocido: {command}");
        PrintUsage();
        return 2;
    }

    /// <summary>
    ///   Prints the usage information for the CodeSync command-line tool,
    ///   printing the usage instructions to the console, showing the available commands and their expected arguments.
    /// </summary>
    private static void PrintUsage()
    {
        Console.WriteLine("Uso:");
        Console.WriteLine("  CodeSync compare <source-directory> <destination-directory> <profile.xml>");
        Console.WriteLine("  CodeSync verify <profile.xml>");
        Console.WriteLine("  CodeSync copy <profile.xml> [--dry-run]");
        Console.WriteLine("  CodeSync update <profile.xml>");
    }
}
