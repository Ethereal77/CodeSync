# CodeSync

CodeSync is a command-line tool for carrying file changes from one directory tree to another when
the two trees have diverged in layout or file names.

It compares complete file contents, records the confirmed source-to-destination relationships
in an XML profile, and then uses that profile to copy later source changes. It is intended for
synchronizing files between related repositories or long-lived branches.

## How it works

CodeSync works with two directory trees:

- **Source** is the tree whose changes you want to transfer.
- **Destination** is the tree that receives copied files.

The initial comparison identifies files with the same size and SHA-256 hash.
* A content match is accepted only when it is unique on both sides
* Duplicate identical files are reported as ambiguous rather than guessed.
* The comparison also records directory references inferred from complete, uniquely matched directories.

The resulting profile is a reviewed, persistent mapping. Later copies use it even if a source file's
contents have changed.

```text
Source directory + Destination directory
                 |
                 v
              compare
                 |
                 +--> Profile.xml           : Confirmed file mappings
                 +--> Profile.content.xml   : Source and destination snapshots
                 +--> Profile.conflicts.xml : Items needing review
                 |
                 v
       review / complete the profile
                 |
                 v
               verify
                 |
                 v
            copy [--dry-run]
                 |
                 +--> Profile.skipped.xml : Unchanged source files
                 |
                 v
               update
```

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download)

## Build and run

Build the solution from the repository root:

```powershell
dotnet build CodeSync.slnx
```

Run the CLI during development with `dotnet run`:

```text
dotnet run --project CodeSync -- compare <source-directory> <destination-directory> <profile.xml>
```

For example:

```powershell
dotnet run --project CodeSync -- compare 'C:\work\upstream' 'C:\work\product' '.\sync-profile.xml'
```

You can also build a distributable executable with `dotnet publish`; run the produced `CodeSync`
executable with the same commands shown below.

## Commands

### `compare`

```text
CodeSync compare <source-directory> <destination-directory> <profile.xml>
```

Scans both directory trees, applies their `.gitignore` rules, calculates SHA-256 hashes, and creates:

- `<profile>.xml`, containing confirmed file mappings and inferred directory references;
- `<profile>.content.xml`, containing the size, UTC timestamp, and SHA-256 snapshots used by copy;
- `<profile>.conflicts.xml`, containing unmatched, missing, or ambiguous files that need human review.

The command exits with code `0` when there are no conflicts, or `1` when it created a profile but review
is still required.

Example:

```powershell
CodeSync compare 'C:\repos\source' 'C:\repos\destination' 'C:\profiles\source-to-destination.xml'
```

Files ignored by `.gitignore` are excluded. CodeSync automatically ignores `.git`;
it loads `.gitignore` files found throughout each tree, so nested rules are respected too.

### `verify`

```text
CodeSync verify <profile.xml>
```

Checks that the profile still covers the current source and destination trees. It reports:
* missing mapped files,
* duplicate mappings, and
* files that are not covered by the profile,

then writes the current conflict report beside the profile.

The content inventory is refreshed with the files found by the verification scan, including files
that are newly reported as conflicts.

`verify` stops immediately if its existing conflict report is not empty.
Resolve and clear that report first, then verify again.

Example:

```powershell
CodeSync verify 'C:\profiles\source-to-destination.xml'
```

Verification checks the mapping and file coverage, not whether the source and destination contents
still match. This is deliberate: a changed source file is what `copy` is intended to transfer.

### `copy`

```text
CodeSync copy <profile.xml> [--dry-run]
```

Copies every changed mapped source file to its mapped destination, creating destination
parent directories when necessary. Destination files are overwritten.

Before copying, CodeSync refuses to run if the profile has unresolved conflicts.

For each mapped source file, CodeSync compares the stored snapshot from `<profile>.content.xml` with the current source size,
UTC last-write time, and SHA-256 hash:

- A changed source is copied and its snapshots in the content sidecar are refreshed.
- An unchanged source is skipped and recorded in `<profile>.skipped.xml`.
- A source-only mapping (no destination) is treated as intentionally ignored.

Use `--dry-run` to see the operations that would be performed without writing destination files,
updating the profile, or producing a skipped-file report.

```powershell
CodeSync copy 'C:\profiles\source-to-destination.xml' --dry-run
CodeSync copy 'C:\profiles\source-to-destination.xml'
```

### `update`

```text
CodeSync update <profile.xml>
```

Refreshes the source snapshots for files recorded in `<profile>.skipped.xml` after a copy.
This is useful when a file was not copied because it had not changed, but you later want
that file's current source state to become the profile baseline.
On success, it clears the skipped-file report.

```powershell
CodeSync update 'C:\profiles\source-to-destination.xml'
```

## Resolving conflicts

CodeSync never guesses when a match is missing or ambiguous. Inspect the generated `*.conflicts.xml`
report and decide how the files should relate. The current workflow supports manual profile editing:

1. Add or correct the required `<FileMapping>` entries in the profile.

2. A mapping with both `Source` and `Destination` means “copy this source file to this destination file.”

3. A mapping containing only `Source` or only `Destination` marks that file as intentionally unpaired;
   source-only mappings are ignored by `copy`.

4. Clear the resolved entries from the conflict report, then run `verify` again.
   `copy` is enabled only when the report is empty and verification succeeds.

Profiles store absolute root directories and normalized relative file paths. File metadata is kept
separately in `<profile>.content.xml`; a manually edited mapping must refer to paths already present
in that inventory. Unknown paths are rejected because CodeSync cannot verify or copy them safely.

A typical generated profile has this shape:

```xml
<?xml version="1.0" encoding="utf-8"?>
<!--
  CodeSync Profile v1

    Source:      C:\repos\source
    Destination: C:\repos\destination
-->
<CodeSyncProfile schemaVersion="1">
  <SourceDirectory>C:\repos\source</SourceDirectory>
  <DestinationDirectory>C:\repos\destination</DestinationDirectory>
  <CreatedUtc>2026-08-31T10:00:00.0000000Z</CreatedUtc>
  <LastUpdatedUtc>2026-08-31T10:00:00.0000000Z</LastUpdatedUtc>
  <DirectoryReferences>
    <Directory Source="src" Destination="lib" />
  </DirectoryReferences>
  <FileMappings>
    <FileMapping Source="src/Widget.cs" Destination="lib/Widget.cs" />
    <FileMapping Source="src/IntentionallyIgnored.cs" />
  </FileMappings>
</CodeSyncProfile>
```

The metadata sidecar contains the corresponding technical state:

```xml
<CodeSyncContent schemaVersion="1">
  <SourceFiles>
    <File Path="src/Widget.cs" Size="1234"
          LastWriteTimeUtc="2026-08-31T10:00:00.0000000Z"
          Sha256="aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa" />
  </SourceFiles>
  <DestinationFiles>
    <File Path="lib/Widget.cs" Size="1234"
          LastWriteTimeUtc="2026-08-31T10:00:00.0000000Z"
          Sha256="aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa" />
  </DestinationFiles>
</CodeSyncContent>
```

Treat a profile as a mapping plan: review it before running a non-dry copy, especially when it was
edited by hand.

## Exit codes

| Code | Meaning |
| --- | --- |
| `0` | The command completed successfully. |
| `1` | The operation failed or conflicts remain. |
| `2` | The command line was invalid or the command was not recognized. |

## Development

Run the test suite with:

```powershell
dotnet test CodeSync.slnx
```

The solution is organized into four projects:

- `CodeSync` — command-line application.
- `CodeSync.Core` — comparison, profile, verification, copy, and update logic.
- `CodeSync.Infrastructure` — physical filesystem and XML persistence.
- `CodeSync.Tests` — automated tests.
