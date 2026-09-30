using CodeSync.Core;
using CodeSync.Infrastructure;

namespace CodeSync.Tests;

public sealed class XmlProfileStoreTests
{
    private static readonly DateTime FixedTime = new(2026, 8, 27, 10, 30, 0, DateTimeKind.Utc);
    private const string Hash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";


    [Fact]
    public void Save_WithExistingProfile_CreatesNumberedBackupsForProfileAndContent()
    {
        var temporaryDirectory = Path.Combine(Path.GetTempPath(), "CodeSyncTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporaryDirectory);

        var profilePath = Path.Combine(temporaryDirectory, "profile.xml");
        var contentPath = ProfileArtifacts.GetContentPath(profilePath);

        var profile = new SyncProfile("source", "destination", directoryReferences: [], fileMappings: []);
        var store = new XmlProfileStore();

        try
        {
            store.SaveNew(profilePath, profile, sourceFiles: [], destinationFiles: []);

            for (var backupIndex = 0; backupIndex < 3; backupIndex++)
            {
                var previousProfile = File.ReadAllBytes(profilePath);
                var previousContent = File.ReadAllBytes(contentPath);

                store.Save(profilePath, profile);

                var suffix = backupIndex == 0 ? ".bak" : $".{backupIndex - 1}.bak";
                Assert.Equal(previousProfile, File.ReadAllBytes(profilePath + suffix));
                Assert.Equal(previousContent, File.ReadAllBytes(contentPath + suffix));
            }
        }
        finally
        {
            if (Directory.Exists(temporaryDirectory))
                Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    [Fact]
    public void SaveNew_WithNewProfile_DoesNotCreateBackups()
    {
        var temporaryDirectory = Path.Combine(Path.GetTempPath(), "CodeSyncTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporaryDirectory);

        var profilePath = Path.Combine(temporaryDirectory, "profile.xml");
        var profile = new SyncProfile("source", "destination", directoryReferences: [], fileMappings: []);

        try
        {
            new XmlProfileStore().SaveNew(profilePath, profile, sourceFiles: [], destinationFiles: []);

            Assert.False(File.Exists(profilePath + ".bak"));
            Assert.False(File.Exists(ProfileArtifacts.GetContentPath(profilePath) + ".bak"));
        }
        finally
        {
            if (Directory.Exists(temporaryDirectory))
                Directory.Delete(temporaryDirectory, recursive: true);
        }
    }


    [Fact]
    public void Load_ReportsAllProfilePathsMissingFromContentInventory()
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
                    new ProfileMapping("src/unknown.cs", "lib/known.cs"),
                    new ProfileMapping("src/also-unknown.cs", "lib/also-known.cs")
                ]);

            var content = new ProfileContent(
                sourceDirectory: "source",
                destinationDirectory: "destination",
                createdUtc: FixedTime,
                lastUpdatedUtc: FixedTime,
                sourceFiles: [new FileSnapshot("src/known.cs", size: 1, lastWriteTimeUtc: FixedTime, sha256: Hash)],
                destinationFiles:
                [
                    new FileSnapshot("lib/known.cs", size: 1, lastWriteTimeUtc: FixedTime, sha256: Hash),
                    new FileSnapshot("lib/also-known.cs", size: 1, lastWriteTimeUtc: FixedTime, sha256: Hash)
                ]);

            File.WriteAllText(profilePath,
                              XmlCodecs.SerializeProfile(new ProfileDocument(definition, FixedTime, FixedTime)));
            File.WriteAllText(ProfileArtifacts.GetContentPath(profilePath),
                              XmlCodecs.SerializeContent(content));

            var exception = Assert.Throws<ProfileLoadException>(() => new XmlProfileStore().Load(profilePath));
            Assert.Equal(2, exception.Errors.Count);
            Assert.Contains(exception.Errors, error => error.Contains("src/unknown.cs", StringComparison.Ordinal));
            Assert.Contains(exception.Errors, error => error.Contains("src/also-unknown.cs", StringComparison.Ordinal));
        }
        finally
        {
            if (Directory.Exists(temporaryDirectory))
                Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    [Fact]
    public void Load_ReportsErrorsFromProfileAndContentTogether()
    {
        var temporaryDirectory = Path.Combine(Path.GetTempPath(), "CodeSyncTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporaryDirectory);
        var profilePath = Path.Combine(temporaryDirectory, "profile.xml");

        try
        {
            const string profileXml = """
                <CodeSyncProfile schemaVersion="1">
                  <SourceDirectory>source</SourceDirectory>
                  <DestinationDirectory>destination</DestinationDirectory>
                  <CreatedUtc>2026-08-27T10:30:00.0000000Z</CreatedUtc>
                  <LastUpdatedUtc>2026-08-27T10:30:00.0000000Z</LastUpdatedUtc>
                  <FileMappings>
                    <FileMapping Source="../invalid.cs" Destination="lib/invalid.cs" />
                    <FileMapping Destination="lib/missing-source.cs" />
                  </FileMappings>
                </CodeSyncProfile>
                """;

            const string contentXml = """
                <CodeSyncContent schemaVersion="1">
                  <SourceDirectory>source</SourceDirectory>
                  <DestinationDirectory>destination</DestinationDirectory>
                  <CreatedUtc>2026-08-27T10:30:00.0000000Z</CreatedUtc>
                  <LastUpdatedUtc>2026-08-27T10:30:00.0000000Z</LastUpdatedUtc>
                  <SourceFiles>
                    <File Path="src/one.cs" Size="bad" LastWriteTimeUtc="2026-08-27T10:30:00.0000000Z" Sha256="aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa" />
                    <File Path="src/two.cs" Size="bad" LastWriteTimeUtc="2026-08-27T10:30:00.0000000Z" Sha256="aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa" />
                  </SourceFiles>
                </CodeSyncContent>
                """;

            File.WriteAllText(profilePath, profileXml);
            File.WriteAllText(ProfileArtifacts.GetContentPath(profilePath), contentXml);

            var exception = Assert.Throws<ProfileLoadException>(() => new XmlProfileStore().Load(profilePath));

            Assert.Equal(4, exception.Errors.Count);
            Assert.Contains(exception.Errors, error => error.StartsWith("Profile: Line ", StringComparison.Ordinal));
            Assert.Contains(exception.Errors, error => error.StartsWith("Content inventory: Line ", StringComparison.Ordinal));
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
                Assert.Throws<ProfileLoadException>(() => new XmlProfileStore().Load(profilePath));
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
