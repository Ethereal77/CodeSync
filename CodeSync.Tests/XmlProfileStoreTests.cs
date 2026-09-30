using CodeSync.Core;
using CodeSync.Infrastructure;

namespace CodeSync.Tests;

public sealed class XmlProfileStoreTests
{
    private static readonly DateTime FixedTime = new(2026, 8, 27, 10, 30, 0, DateTimeKind.Utc);
    private const string Hash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";


    [Fact]
    public void Load_RejectsProfilePathMissingFromContentInventory()
    {
        var temporaryDirectory = Path.Combine(Path.GetTempPath(), "CodeSyncTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporaryDirectory);
        var profilePath = Path.Combine(temporaryDirectory, "profile.xml");

        try
        {
            var definition = new ProfileDefinition(
                "source",
                "destination",
                [],
                [new ProfileMapping("src/unknown.cs", "lib/known.cs")]);
            var content = new ProfileContent(
                "source",
                "destination",
                FixedTime,
                FixedTime,
                [new FileSnapshot("src/known.cs", 1, FixedTime, Hash)],
                [new FileSnapshot("lib/known.cs", 1, FixedTime, Hash)]);

            File.WriteAllText(profilePath,
                              XmlCodecs.SerializeProfile(new ProfileDocument(definition, FixedTime, FixedTime)));
            File.WriteAllText(ProfileArtifacts.GetContentPath(profilePath),
                              XmlCodecs.SerializeContent(content));

            var exception = Assert.Throws<InvalidDataException>(() => new XmlProfileStore().Load(profilePath));
            Assert.Contains("src/unknown.cs", exception.Message);
        }
        finally
        {
            if (Directory.Exists(temporaryDirectory))
                Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    [Fact]
    public void Load_AllowsIgnoredDestinationSuggestionMissingFromContentInventory()
    {
        var temporaryDirectory = Path.Combine(Path.GetTempPath(), "CodeSyncTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporaryDirectory);
        var profilePath = Path.Combine(temporaryDirectory, "profile.xml");

        try
        {
            var definition = new ProfileDefinition(
                sourceDirectory: "source",
                destinationDirectory: "destination",
                directoryReferences: [],
                fileMappings:
                [
                    new ProfileMapping(sourcePath: "src/ignored.cs",
                                       destinationPath: "lib/suggested.cs",
                                       isIgnored: true)
                ]);

            var content = new ProfileContent(
                sourceDirectory: "source",
                destinationDirectory: "destination",
                createdUtc: FixedTime,
                lastUpdatedUtc: FixedTime,
                sourceFiles: [new FileSnapshot("src/ignored.cs", size: 7, lastWriteTimeUtc: FixedTime, Hash)],
                destinationFiles: []);

            File.WriteAllText(profilePath,
                              XmlCodecs.SerializeProfile(new ProfileDocument(definition, FixedTime, FixedTime)));
            File.WriteAllText(ProfileArtifacts.GetContentPath(profilePath),
                              XmlCodecs.SerializeContent(content));

            var mapping = Assert.Single(new XmlProfileStore().Load(profilePath).FileMappings);
            Assert.True(mapping.IsIgnored);
            Assert.Null(mapping.Destination);
            Assert.Equal("lib/suggested.cs", mapping.DestinationPath);
        }
        finally
        {
            if (Directory.Exists(temporaryDirectory))
                Directory.Delete(temporaryDirectory, recursive: true);
        }
    }
}
