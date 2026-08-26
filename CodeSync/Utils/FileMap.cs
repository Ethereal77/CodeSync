namespace CodeSync.Utils;

using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

/// <summary>
///   A dictionary mapping a file name with one or more relative file paths where that file name
///   was found.
/// </summary>
class FileMap : IDictionary<string, IFileDestination>
{
    private readonly Dictionary<string, IFileDestination> _fileMap;

    /// <summary>
    ///   Gets the number of files in the file map.
    /// </summary>
    public int Count { get; private set; } = 0;

    /// <summary>
    ///   Gets or sets the relative directory (either single -<see cref="SingleFileDestination"/>- or
    ///   more than one -<see cref="MultipleFileDestination"/>) for a file name.
    /// </summary>
    public IFileDestination this[string key]
    {
        get => _fileMap[key];
        set => _fileMap[key] = value;
    }


    /// <summary>
    ///   Initializes a new instance of the <see cref="FileMap"/> class.
    /// </summary>
    public FileMap()
    {
        _fileMap = new Dictionary<string, IFileDestination>(StringComparer.InvariantCultureIgnoreCase);
    }

    /// <summary>
    ///   Initializes a new instance of the <see cref="FileMap"/> class.
    /// </summary>
    /// <param name="capacity">The initial capacity for the file map.</param>
    public FileMap(int capacity)
    {
        _fileMap = new Dictionary<string, IFileDestination>(capacity, StringComparer.InvariantCultureIgnoreCase);
    }

    /// <summary>
    ///   Initializes a new instance of the <see cref="FileMap"/> class.
    /// </summary>
    /// <param name="capacity">The initial capacity for the file map.</param>
    public FileMap(IEnumerable<string> files)
    {
        if (!files.TryGetNonEnumeratedCount(out int fileCount))
            fileCount = files.Count();

        _fileMap = new Dictionary<string, IFileDestination>(capacity: fileCount, StringComparer.InvariantCultureIgnoreCase);

        foreach(string fileRelativePath in files)
        {
            Add(fileRelativePath);
        }
    }


    /// <summary>
    ///   Adds a file to the map.
    /// </summary>
    /// <param name="filePath">The path of the file.</param>
    public void Add(string filePath)
    {
        var fileName = Path.GetFileName(filePath);

        // If the file name already exists in the map
        if (_fileMap.TryGetValue(fileName, out var existingFileSource))
        {
            if (existingFileSource is SingleFileDestination singleFileSource)
            {
                // Single path --> Now more than one
                _fileMap[fileName] = new MultipleFileDestination
                {
                    singleFileSource.FilePath,
                    filePath
                };
            }
            else if (existingFileSource is MultipleFileDestination multipleFileSource)
            {
                // Multiple paths --> Add to the list
                multipleFileSource.Add(filePath);
            }
            else Debug.Fail("No es SingleFileSource ni MultipleFileSource.");
        }
        else
        {
            // File name unknown --> Add it to the map
            _fileMap.Add(fileName, new SingleFileDestination(filePath));
        }

        Count++;
    }

    /// <summary>
    ///   Removes a file from the map.
    /// </summary>
    /// <param name="fileName">The file name (not the path) to remove.</param>
    public bool Remove(string fileName)
    {
        if (_fileMap.TryGetValue(fileName, out IFileDestination? fileSource))
        {
            if (fileSource is SingleFileDestination)
                Count--;

            else if (fileSource is MultipleFileDestination fileList)
                Count -= fileList.Count;

            return _fileMap.Remove(fileName);
        }
        return false;
    }

    /// <summary>
    ///   Removes a file from the map.
    /// </summary>
    /// <param name="fileName">The file name (not the path) to remove.</param>
    /// <param name="filePathToRemove">The file path to remove.</param>
    internal void Remove(string fileName, string filePathToRemove)
    {
        if (_fileMap.TryGetValue(fileName, out IFileDestination? fileSource))
        {
            if (fileSource is SingleFileDestination)
            {
                _fileMap.Remove(fileName);
            }
            else if (fileSource is MultipleFileDestination fileList)
            {
                fileList.Remove(filePathToRemove);

                if (fileList.Count == 0)
                    _fileMap.Remove(fileName);
            }

            Count--;
        }
    }

    /// <summary>
    ///   Removes a file from the map.
    /// </summary>
    /// <param name="fileName">The file name (not the path) to remove.</param>
    /// <param name="fileList">The file list where the file to remove is.</param>
    /// <param name="filePathToRemove">The file path to remove.</param>
    internal void Remove(string fileName, MultipleFileDestination fileList, string filePathToRemove)
    {
        fileList.Remove(filePathToRemove);
        Count--;

        if (fileList.Count == 0)
            _fileMap.Remove(fileName);
    }

    /// <summary>
    ///   Gets the file sources associated with the specified file name.
    /// </summary>
    /// <param name="fileName">The file name (not the path) to get.</param>
    /// <param name="fileSource">
    ///   When this method returns, contains the file source associated with the specified file name,
    ///   if it was found; otherwise, <see langword="null"/>.
    ///   This parameter is passed uninitialized.
    /// </param>
    /// <returns>
    ///   <see langword="true"/> if the map contains a file source with the specified file name;
    ///   otherwise, <see langword="false"/>.
    /// </returns>
    public bool TryGetValue(string fileName, [MaybeNullWhen(false)] out IFileDestination fileSource)
    {
        return _fileMap.TryGetValue(fileName, out fileSource);
    }

    #region Interface implementations

    public ICollection<string> Keys => _fileMap.Keys;
    public ICollection<IFileDestination> Values => _fileMap.Values;

    public bool IsReadOnly => ((ICollection<KeyValuePair<string, IFileDestination>>)_fileMap).IsReadOnly;

    void IDictionary<string, IFileDestination>.Add(string key, IFileDestination value)
    {
        ((IDictionary<string, IFileDestination>)_fileMap).Add(key, value);
    }

    void ICollection<KeyValuePair<string, IFileDestination>>.Add(KeyValuePair<string, IFileDestination> item)
    {
        ((IDictionary<string, IFileDestination>)_fileMap).Add(item.Key, item.Value);
    }

    void ICollection<KeyValuePair<string, IFileDestination>>.Clear() => _fileMap.Clear();

    bool ICollection<KeyValuePair<string, IFileDestination>>.Contains(KeyValuePair<string, IFileDestination> item)
    {
        return ((ICollection<KeyValuePair<string, IFileDestination>>)_fileMap).Contains(item);
    }

    bool IDictionary<string, IFileDestination>.ContainsKey(string key) => _fileMap.ContainsKey(key);

    void ICollection<KeyValuePair<string, IFileDestination>>.CopyTo(KeyValuePair<string, IFileDestination>[] array, int arrayIndex)
    {
        ((ICollection<KeyValuePair<string, IFileDestination>>)_fileMap).CopyTo(array, arrayIndex);
    }

    bool ICollection<KeyValuePair<string, IFileDestination>>.Remove(KeyValuePair<string, IFileDestination> item)
    {
        return ((ICollection<KeyValuePair<string, IFileDestination>>)_fileMap).Remove(item);
    }

    public IEnumerator<KeyValuePair<string, IFileDestination>> GetEnumerator() => _fileMap.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => _fileMap.GetEnumerator();

    #endregion
}

#region File destination types

/// <summary>
///   Base interface for a file source (either single -<see cref="SingleFileDestination"/>- or
///   more than one -<see cref="MultipleFileDestination"/>) for a file name.
/// </summary>
interface IFileDestination { }

/// <summary>
///   A single file source for a file name.
/// </summary>
sealed record class SingleFileDestination(string FilePath) : IFileDestination
{
    public override string ToString() => FilePath;
    public static implicit operator string(SingleFileDestination sfd) => sfd.FilePath;
}

/// <summary>
///   A list of file sources for a file name.
/// </summary>
sealed class MultipleFileDestination : List<string>, IFileDestination
{
    /// <summary>
    ///   Searches for a file with the same relative path as the provided source file path,
    ///   and returns the first occurrence within the entire list.
    /// </summary>
    public string? Find(string sourceFilePath)
    {
        return Find(path => string.Compare(path, sourceFilePath, StringComparison.InvariantCultureIgnoreCase) == 0);
    }
}

#endregion
