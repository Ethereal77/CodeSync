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
    public void Load_ResolvesProfilePathsUsingPlatformComparison()
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
                fileMappings: [new ProfileMapping("src/Assets/file.cs", "LIB/file.cs")]);

            var content = new ProfileContent(
                sourceDirectory: "SOURCE",
                destinationDirectory: "destination",
                createdUtc: FixedTime,
                lastUpdatedUtc: FixedTime,
                sourceFiles: [new FileSnapshot("src/assets/file.cs", 1, FixedTime, Hash)],
                destinationFiles: [new FileSnapshot("lib/file.cs", 1, FixedTime, Hash)]);

            File.WriteAllText(profilePath,
                              XmlCodecs.SerializeProfile(new ProfileDocument(definition, FixedTime, FixedTime)));

            File.WriteAllText(ProfileArtifacts.GetContentPath(profilePath),
                              XmlCodecs.SerializeContent(content));

            if (OperatingSystem.IsWindows() || OperatingSystem.IsMacOS())
            {
                var mapping = Assert.Single(new XmlProfileStore().Load(profilePath).FileMappings);
                Assert.Equal("src/assets/file.cs", mapping.Source!.Path);
                Assert.Equal("lib/file.cs", mapping.Destination!.Path);
            }
            else
            {
                Assert.Throws<InvalidDataException>(() => new XmlProfileStore().Load(profilePath));
            }
        }
        finally
        {
            if (Directory.Exists(temporaryDirectory))
                Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    [Fact]
    public void Load_AllowsIgnoredSourceMissingFromContentInventoryWhenDestinationExists()
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
                                       destinationPath: "lib/ignored.cs",
                                       isIgnored: true)
                ]);
            var content = new ProfileContent(
                sourceDirectory: "source",
                destinationDirectory: "destination",
                createdUtc: FixedTime,
                lastUpdatedUtc: FixedTime,
                sourceFiles: [],
                destinationFiles: [new FileSnapshot("lib/ignored.cs", 7, FixedTime, Hash)]);

            File.WriteAllText(profilePath,
                              XmlCodecs.SerializeProfile(new ProfileDocument(definition, FixedTime, FixedTime)));
            File.WriteAllText(ProfileArtifacts.GetContentPath(profilePath),
                              XmlCodecs.SerializeContent(content));

            var mapping = Assert.Single(new XmlProfileStore().Load(profilePath).FileMappings);
            Assert.True(mapping.IsIgnored);
            Assert.Null(mapping.Source);
            Assert.Equal("lib/ignored.cs", mapping.DestinationPath);
            Assert.NotNull(mapping.Destination);
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
