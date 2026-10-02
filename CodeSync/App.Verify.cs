using System.Diagnostics;

using CodeSync.Core;

namespace CodeSync;

internal sealed partial class App
{
    /// <summary>
    ///   Verifies a synchronization profile against the current state of the source and destination directories.
    /// </summary>
    /// <param name="args">The command-line arguments passed to the program.</param>
    /// <returns>The exit code indicating the result of the verification.</returns>
    private async Task<int> Verify(string[] args)
    {
        ReadOnlySpan<string> cliArgs = args.AsSpan(start: 1);

        if (cliArgs.Length != 1)
        {
            Console.Error.WriteLine("Uso: CodeSync verify <profile.xml>");
            return ExitCodeInvalidArguments;
        }

        var profilePath = RequireFile(cliArgs[0], "profile");
        var existingConflicts = conflictStore.Load(ProfileArtifacts.GetConflictsPath(profilePath));

        // Check if there are existing conflicts before proceeding with verification
        if (existingConflicts is not null && existingConflicts.Conflicts.Count > 0)
        {
            Console.Error.WriteLine($"La verificación se cancela: hay {existingConflicts.Conflicts.Count} conflictos sin resolver.");
            return ExitCodeError;
        }

        // Load the synchronization profile for verification
        var profile = profileStore.Load(profilePath);

        // Scan the source and destination directories based on the loaded profile
        var scanner = new FileScanner(Workspace);

        var source = await ScanDirectory(scanner, profile.SourceDirectory, "source");
        var destination = await ScanDirectory(scanner,  profile.DestinationDirectory, "destination");

        Console.WriteLine();

        // Verify the profile against the scanned directories
        var result = new ProfileVerifier().Verify(profile, source, destination);

        // Save the found conflicts to the conflict store
        var conflicts = new ConflictSet(profile.SourceDirectory, profile.DestinationDirectory, result.Conflicts);

        conflictStore.Save(ProfileArtifacts.GetConflictsPath(profilePath), conflicts);

        // Conflicts are saved first so that a failed profile write never loses the removed mappings
        var removedMappings = profile.FileMappings.Count - result.CleanedProfile.FileMappings.Count;

        if (removedMappings > 0)
            profileStore.Save(profilePath, result.CleanedProfile);

        profileStore.RefreshContent(profilePath, source, destination);

        foreach (var warning in result.Warnings)
            Console.Error.WriteLine($"Aviso: {warning}");

        if (removedMappings > 0)
        {
            Console.WriteLine($"⚠️ Se han retirado {removedMappings} entradas del perfil.");
            Console.WriteLine($" - Conflictos: {result.Conflicts.Count}");
            Console.WriteLine($" - Repeticiones: {removedMappings - result.Conflicts.Count}");
            Console.WriteLine();
        }

        if (!result.IsValid)
        {
            Console.Error.WriteLine("🚫 Conflictos encontrados:");
            Console.Error.WriteLine();

            foreach (var conflict in result.Conflicts)
            {
                var sourcePath = conflict.Mapping.Source?.Path ?? "(sin origen)";
                var destinationPath = conflict.Mapping.DestinationPath ?? "(sin destino)";

                Console.Error.WriteLine($"❗{GetConflictKindDescription(conflict.Kind)}");
                Console.Error.WriteLine($"  Origen: {sourcePath}");
                Console.Error.WriteLine($"  Destino: {destinationPath}");
                Console.Error.WriteLine();
            }
        }

        Console.WriteLine(result.IsValid
            ? "👍 Verificación correcta: no se han encontrado conflictos."
            : $"❌ Verificación fallida: {result.Conflicts.Count} conflictos pendientes.");

        return result.IsValid ? ExitCodeSuccess : ExitCodeError;

        //
        // Returns a human-readable description for a given conflict kind.
        //
        static string GetConflictKindDescription(ConflictKind kind)
        {
            return kind switch
            {
                ConflictKind.DuplicateMapping => "Entrada duplicada",
                ConflictKind.AmbiguousMatch => "Coincidencia ambigua",
                ConflictKind.DestinationWithoutSource => "Destino sin origen",
                ConflictKind.SourceWithoutDestination => "Origen sin destino",
                ConflictKind.MissingMappedFile => "Archivo mapeado ausente",

                // This case should never be reached because all conflict kinds are handled explicitly
                _ => throw new UnreachableException()
            };
        }
    }
}
