using System.Diagnostics;

using CodeSync.Core;

namespace CodeSync;

/// <summary>
///   Represents the main application class for CodeSync, responsible for handling
///   command-line arguments and coordinating the synchronization process between
///   source and destination directories.
/// </summary>
internal sealed partial class App
{
    private const int ExitCodeSuccess = 0;
    private const int ExitCodeError = 1;
    private const int ExitCodeInvalidArguments = 2;

    /// <summary>
    ///   Gets the workspace associated with the application.
    /// </summary>
    public IWorkspace Workspace { get; }

    private readonly IProfileStore profileStore;
    private readonly IConflictStore conflictStore;
    private readonly ISkippedStore skippedStore;


    /// <summary>
    ///   Initializes a new instance of the <see cref="App"/> class.
    /// </summary>
    /// <param name="workspace">The workspace to be used by the application.</param>
    /// <param name="profileStore">The profile store for managing synchronization profiles.</param>
    /// <param name="conflictStore">The conflict store for managing synchronization conflicts.</param>
    /// <param name="skippedStore">The skipped store for managing skipped files.</param>
    public App(IWorkspace workspace, IProfileStore profileStore, IConflictStore conflictStore, ISkippedStore skippedStore)
    {
        Workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));

        this.profileStore = profileStore ?? throw new ArgumentNullException(nameof(profileStore));
        this.conflictStore = conflictStore ?? throw new ArgumentNullException(nameof(conflictStore));
        this.skippedStore = skippedStore ?? throw new ArgumentNullException(nameof(skippedStore));
    }

    /// <summary>
    ///   The main entry point for the CodeSync application.
    /// </summary>
    /// <param name="args">The command-line arguments passed to the application.</param>
    /// <returns>The exit code indicating the result of the application's execution.</returns>
    public async Task<int> Run(string[] args)
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
        catch (ProfileLoadException ex)
        {
            Console.Error.WriteLine($"Error: se encontraron {ex.Errors.Count} errores al cargar el perfil:");
            foreach (var error in ex.Errors)
                Console.Error.WriteLine($"  - {error}");

            return ExitCodeError;
        }
        catch (FileSaveException ex)
        {
            Console.Error.WriteLine($"Error: No se ha podido guardar el archivo {ex.Path}. ¿Es posible que esté en uso por otro proceso?");
            return ExitCodeError;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return ExitCodeError;
        }
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


    /// <summary>
    ///   Discovers paths, loads their ignore rules and scans the accepted files,
    ///   returning a list of file snapshots.
    /// </summary>
    private async Task<IReadOnlyList<FileSnapshot>> ScanDirectory(FileScanner scanner, string rootDirectory, string label)
    {
        Console.WriteLine();
        Console.WriteLine($"Escaneando directorio ({label}): {rootDirectory}");

        var timer = Stopwatch.StartNew();

        var paths = scanner.Discover(rootDirectory);
        var discoveryElapsed = timer.Elapsed;

        Console.WriteLine($"  Se han encontrado {paths.Count} archivos en {discoveryElapsed.TotalSeconds:F1}s.");

        timer.Restart();

        var matcher = LoadIgnoreMatcher(rootDirectory, paths);
        var ignoreElapsed = timer.Elapsed;

        Console.WriteLine($"  Se han cargado las reglas .gitignore en {ignoreElapsed.TotalSeconds:F1}s.");

        var renderer = new ScanProgressRenderer(Console.Out, interactive: !Console.IsOutputRedirected);

        try
        {
            return await scanner.ScanAsync(rootDirectory, paths, matcher, renderer);
        }
        finally
        {
            renderer.FinishDynamicLine();
        }
    }
}
