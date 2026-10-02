using CodeSync.Core;

namespace CodeSync;

internal sealed partial class App
{
    /// <summary>
    ///   Loads a valid synchronization profile (with no unresolved conflicts) and performs
    ///   the copy operation from the source to the destination directory as defined in the profile.
    /// </summary>
    /// <param name="args">The command-line arguments passed to the program.</param>
    /// <returns>The exit code indicating the result of the copy operation.</returns>
    private int Copy(string[] args)
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
        var conflicts = conflictStore.Load(ProfileArtifacts.GetConflictsPath(profilePath));

        // If there are unresolved conflicts, cancel the copy operation
        if (conflicts is not null && conflicts.Conflicts.Count > 0)
        {
            Console.Error.WriteLine($"La copia se cancela: hay {conflicts.Conflicts.Count} conflictos sin resolver.");
            return ExitCodeError;
        }

        var profile = profileStore.Load(profilePath);

        // Perform the copy operation using the loaded profile and the specified options
        var result = new FileCopier().Copy(profile, Workspace, new CopyOptions(dryRun));

        if (!dryRun)
        {
            // Save the updated profile and any skipped files if the copy operation was not a dry run
            profileStore.Save(profilePath, result.UpdatedProfile);
            skippedStore.Save(ProfileArtifacts.GetSkippedPath(profilePath), result.SkippedSourcePaths);
        }

        // Display the status of each file involved in the copy operation.
        foreach (var file in result.Files)
            Console.WriteLine($"{file.Status}: {file.SourcePath} -> {file.DestinationPath ?? "(ignored)"}");

        return result.Succeeded ? ExitCodeSuccess : ExitCodeError;
    }
}
