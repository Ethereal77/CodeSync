namespace CodeSync.Xml;

using System.Text;

static class XmlSyncFormat
{
    // <CodeSync>
    //   ...
    // </CodeSync>
    internal const string RootTag = "CodeSync";

    // <CodeSync>
    //   <SourceDirectory>path/to/source/repository</SourceDirectory>
    //   <DestDirectory>path/to/dest/repository</DestDirectory>
    //   <ModifiedTime></ModifiedTime>
    //   ...
    internal const string SourceRepositoryDirectoryTag = "SourceDirectory";
    internal const string DestinationRepositoryDirectoryTag = "DestDirectory";
    internal const string ModifiedTimeTag = "ModifiedTime";

    // <Copy>
    //   <Source>path/to/source/repository</Source>
    //   <Destination>path/to/dest/repository</Destination>
    // </Copy>
    internal const string CopyFileEntryTag = "Copy";
    internal const string FileEntrySourceTag = "Source";
    internal const string FileEntryDestinationTag = "Destination";

    // <Ignore>
    //   <Source>path/to/source/repository</Source>
    //   <Destination>path/to/dest/repository</Destination>
    // </Ignore>
    internal const string IgnoreFileEntryTag = "Ignore";

    // Encoding and text format preferences
    internal const string DefaultIndent = "  ";
    internal const string DefaultNewLine = "\r\n";
    internal static readonly Encoding DefaultEncoding = Encoding.UTF8;
}

#region XML data entries

/// <summary>
///   A XML entry that specifies a file to copy from the source repository to the destination repository.
/// </summary>
/// <param name="SourcePath">The path of the file to copy, relative to the base directory of the source repository.</param>
/// <param name="DestPath">The path of the destination file, relative to the base directory of the destination repository.</param>
record struct CopyFileEntry(string SourcePath, string DestPath);

/// <summary>
///   A partial XML entry of a file to copy. Like <see cref="CopyFileEntry"/> but some of the paths are unspecified.
/// </summary>
/// <param name="SourcePath">The path of the file to copy, relative to the base directory of the source repository.</param>
/// <param name="DestPath">The path of the destination file, relative to the base directory of the destination repository.</param>
record struct CopyFilePartialEntry(string? SourcePath, string? DestPath);

/// <summary>
///   A XML entry that specifies a file to ignore in the source repository.
/// </summary>
/// <param name="SourcePath">The path of the file to copy, relative to the base directory of the source repository.</param>
record struct IgnoreSourceFileEntry(string SourcePath);

/// <summary>
///   A XML entry that specifies a file to ignore in the destination repository.
/// </summary>
/// <param name="DestPath">The path of the destination file, relative to the base directory of the destination repository.</param>
record struct IgnoreDestFileEntry(string DestPath);

#endregion
