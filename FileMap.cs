using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

/// <summary>
///   A dictionary mapping a file name with one or more relative file paths where that file name
///   was found.
/// </summary>
class FileMap : IDictionary<string, IFileSource>
{
    private readonly Dictionary<string, IFileSource> _fileMap;

    /// <summary>
    ///   Gets the number of files in the file map.
    /// </summary>
    public int Count { get; private set; } = 0;

    /// <summary>
    ///   Gets or sets the relative directory (either single -<see cref="SingleFileSource"/>- or
    ///   more than one -<see cref="MultipleFileSource"/>) for a file name.
    /// </summary>
    public IFileSource this[string key] 
    { 
        get => _fileMap[key]; 
        set => _fileMap[key] = value; 
    }  


    /// <summary>
    ///   Initializes a new instance of the <see cref="FileMap"/> class.
    /// </summary>
    /// <param name="capacity">The initial capacity for the file map.</param>
    public FileMap(int capacity)
    {
        _fileMap = new Dictionary<string, IFileSource>(capacity);
    }

    /// <summary>
    ///   Initializes a new instance of the <see cref="FileMap"/> class.
    /// </summary>
    /// <param name="capacity">The initial capacity for the file map.</param>
    public FileMap(IEnumerable<string> files)
    {
        if (!files.TryGetNonEnumeratedCount(out int fileCount))
            fileCount = files.Count();

        _fileMap = new Dictionary<string, IFileSource>(capacity: fileCount, StringComparer.InvariantCultureIgnoreCase);

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
            if (existingFileSource is SingleFileSource singleFileSource)
            {
                // Single path --> Now more than one
                _fileMap[fileName] = new MultipleFileSource
                {
                    singleFileSource.FilePath,
                    filePath
                };
            }
            else if (existingFileSource is MultipleFileSource multipleFileSource)
            {
                // Multiple paths --> Add to the list
                multipleFileSource.Add(filePath);
            }
            else Debug.Fail("No es SingleFileSource ni MultipleFileSource.");
        }
        else
        {
            // File name unknown --> Add it to the map
            _fileMap.Add(fileName, new SingleFileSource(filePath));
        }

        Count++;
    }

    /// <summary>
    ///   Removes a file from the map.
    /// </summary>
    /// <param name="fileName">The file name (not the path) to remove.</param>
    public bool Remove(string fileName)
    {
        if (_fileMap.TryGetValue(fileName, out IFileSource? fileSource))
        {
            if (fileSource is SingleFileSource)
                Count--;

            else if (fileSource is MultipleFileSource fileList)
                Count -= fileList.Count;

            return _fileMap.Remove(fileName);
        }
        return false;
    }

    /// <summary>
    ///   Removes a file from the map.
    /// </summary>
    /// <param name="fileName">The file name (not the path) to remove.</param>
    /// <param name="fileList">The file list where the file to remove is.</param>
    /// <param name="filePathToRemove">The file path to remove.</param>
    internal void Remove(string fileName, MultipleFileSource fileList, string filePathToRemove)
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
    public bool TryGetValue(string fileName, [MaybeNullWhen(false)] out IFileSource fileSource)
    {
        return _fileMap.TryGetValue(fileName, out fileSource);
    }

    #region Implementaciones de interfaz

    public ICollection<string> Keys => _fileMap.Keys;
    public ICollection<IFileSource> Values => _fileMap.Values;

    public bool IsReadOnly => ((ICollection<KeyValuePair<string, IFileSource>>)_fileMap).IsReadOnly;

    void IDictionary<string, IFileSource>.Add(string key, IFileSource value)
    {
        ((IDictionary<string, IFileSource>)_fileMap).Add(key, value);
    }

    void ICollection<KeyValuePair<string, IFileSource>>.Add(KeyValuePair<string, IFileSource> item)
    {
        ((IDictionary<string, IFileSource>)_fileMap).Add(item.Key, item.Value);
    }

    void ICollection<KeyValuePair<string, IFileSource>>.Clear() => _fileMap.Clear();

    bool ICollection<KeyValuePair<string, IFileSource>>.Contains(KeyValuePair<string, IFileSource> item)
    {
        return ((ICollection<KeyValuePair<string, IFileSource>>)_fileMap).Contains(item);
    }

    bool IDictionary<string, IFileSource>.ContainsKey(string key) => _fileMap.ContainsKey(key);

    void ICollection<KeyValuePair<string, IFileSource>>.CopyTo(KeyValuePair<string, IFileSource>[] array, int arrayIndex)
    {
        ((ICollection<KeyValuePair<string, IFileSource>>)_fileMap).CopyTo(array, arrayIndex);
    }

    bool ICollection<KeyValuePair<string, IFileSource>>.Remove(KeyValuePair<string, IFileSource> item)
    {
        return ((ICollection<KeyValuePair<string, IFileSource>>)_fileMap).Remove(item);
    }

    public IEnumerator<KeyValuePair<string, IFileSource>> GetEnumerator() => _fileMap.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => _fileMap.GetEnumerator();

    #endregion
}

#region File source types

/// <summary>
///   Base interface for a file source (either single -<see cref="SingleFileSource"/>- or
///   more than one -<see cref="MultipleFileSource"/>) for a file name.
/// </summary>
interface IFileSource { }

/// <summary>
///   A single file source for a file name.
/// </summary>
sealed record class SingleFileSource(string FilePath) : IFileSource;

/// <summary>
///   A list of file sources for a file name.
/// </summary>
sealed class MultipleFileSource : List<string>, IFileSource { }

#endregion
