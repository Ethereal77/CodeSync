using CodeSync.Core;

namespace CodeSync;

internal sealed partial class App
{
    /// <summary>
    ///   Loads the specified profile and updates it based on the current state of the source directory.
    /// </summary>
    /// <param name="args">The command-line arguments passed to the program.</param>
    /// <returns>The exit code indicating the result of the update operation.</returns>
    private async Task<int> Update(string[] args)
    {
        ReadOnlySpan<string> cliArgs = args.AsSpan(start: 1);

        if (cliArgs.Length != 1)
        {
            Console.Error.WriteLine("Uso: CodeSync update <profile.xml>");
            return ExitCodeInvalidArguments;
        }

        var profilePath = RequireFile(cliArgs[0], "profile");
        var profile = profileStore.Load(profilePath);

        // Load the previously skipped files for this profile
        var skippedPath = ProfileArtifacts.GetSkippedPath(profilePath);
        var skipped = skippedStore.Load(skippedPath);

        // Scan the source directory to get the current state of the files
        var scanner = new FileScanner(Workspace);
        var source = await ScanDirectory(scanner, profile.SourceDirectory, "source");

        // Update the profile based on the current state of the source directory and previously skipped files
        var result = new ProfileUpdater().Update(profile, skipped, source);

        foreach (var error in result.Errors)
            Console.Error.WriteLine($"Error: {error}");
        if (!result.Succeeded)
            return ExitCodeError;

        // Save the updated profile and clear the skipped files for this profile
        profileStore.Save(profilePath, result.UpdatedProfile);
        skippedStore.Save(skippedPath, sourcePaths: []);

        Console.WriteLine($"Actualizados {result.UpdatedSourcePaths.Count} archivos omitidos.");
        return ExitCodeSuccess;
    }
}
