using System.Collections;
using System.Diagnostics.CodeAnalysis;

/// <summary>
///   A dictionary mapping a hash code to the informatioon of the files with that hash.
/// </summary>
class FileHashMap : IDictionary<int, IList<FileHashInfo>>
{
    private readonly Dictionary<int, IList<FileHashInfo>> _fileHashMap;

    /// <summary>
    ///   Gets the number of files in the file hashes map.
    /// </summary>
    public int Count { get; private set; } = 0;

    /// <summary>
    ///   Gets or sets the collection of files that share a specific hash code.
    /// </summary>
    public IList<FileHashInfo> this[int hash] 
    { 
        get => _fileHashMap[hash]; 
        set => _fileHashMap[hash] = value; 
    }  


    /// <summary>
    ///   Initializes a new instance of the <see cref="FileHashMap"/> class.
    /// </summary>
    /// <param name="capacity">The initial capacity for the file hashes map.</param>
    public FileHashMap(int capacity)
    {
        _fileHashMap = new Dictionary<int, IList<FileHashInfo>>(capacity);
    }


    /// <summary>
    ///   Adds a file to the map.
    /// </summary>
    /// <param name="fileHash">The hash code of the file.</param>
    /// <param name="fileInfo">The information of the file.</param>
    public void Add(int fileHash, FileHashInfo fileInfo)
    {
        if (_fileHashMap.TryGetValue(fileHash, out var fileList))
            fileList.Add(fileInfo);

        else
            _fileHashMap.Add(fileHash, new List<FileHashInfo> { fileInfo });

        Count++;
    }

    /// <summary>
    ///   Gets a collection of files associated with the specified hash code.
    /// </summary>
    /// <param name="fileHash">The file hash code.</param>
    /// <param name="files">
    ///   When this method returns, contains the files associated with the specified hash code,
    ///   if it was found; otherwise, <see langword="null"/>.
    ///   This parameter is passed uninitialized.
    /// </param>
    /// <returns>
    ///   <see langword="true"/> if the map contains a the specified file hash code;
    ///   otherwise, <see langword="false"/>.
    /// </returns>
    public bool TryGetValue(int fileHash, [MaybeNullWhen(false)] out IList<FileHashInfo> files)
    {
        return _fileHashMap.TryGetValue(fileHash, out files);
    }

    #region Implementaciones de interfaz

    public ICollection<int> Keys => _fileHashMap.Keys;
    public ICollection<IList<FileHashInfo>> Values => _fileHashMap.Values;

    public bool IsReadOnly => ((ICollection<KeyValuePair<int, IList<FileHashInfo>>>)_fileHashMap).IsReadOnly;

    void IDictionary<int, IList<FileHashInfo>>.Add(int key, IList<FileHashInfo> value)
    {
        ((IDictionary<int, IList<FileHashInfo>>)_fileHashMap).Add(key, value);
    }

    void ICollection<KeyValuePair<int, IList<FileHashInfo>>>.Add(KeyValuePair<int, IList<FileHashInfo>> item)
    {
        ((IDictionary<int, IList<FileHashInfo>>)_fileHashMap).Add(item.Key, item.Value);
    }

    void ICollection<KeyValuePair<int, IList<FileHashInfo>>>.Clear() => _fileHashMap.Clear();

    bool ICollection<KeyValuePair<int, IList<FileHashInfo>>>.Contains(KeyValuePair<int, IList<FileHashInfo>> item)
    {
        return ((ICollection<KeyValuePair<int, IList<FileHashInfo>>>)_fileHashMap).Contains(item);
    }

    bool IDictionary<int, IList<FileHashInfo>>.ContainsKey(int key) => _fileHashMap.ContainsKey(key);

    void ICollection<KeyValuePair<int, IList<FileHashInfo>>>.CopyTo(KeyValuePair<int, IList<FileHashInfo>>[] array, int arrayIndex)
    {
        ((ICollection<KeyValuePair<int, IList<FileHashInfo>>>)_fileHashMap).CopyTo(array, arrayIndex);
    }

    bool ICollection<KeyValuePair<int, IList<FileHashInfo>>>.Remove(KeyValuePair<int, IList<FileHashInfo>> item)
    {
        return ((ICollection<KeyValuePair<int, IList<FileHashInfo>>>)_fileHashMap).Remove(item);
    }

    public IEnumerator<KeyValuePair<int, IList<FileHashInfo>>> GetEnumerator() => _fileHashMap.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => _fileHashMap.GetEnumerator();

    bool IDictionary<int, IList<FileHashInfo>>.Remove(int key) => _fileHashMap.Remove(key);

    #endregion
}

#region File hashing information

/// <summary>
///   Contains information about a hashed file.
/// </summary>
/// <param name="Matched">A value indicating whether the file has been matched to another file with the same hash.</param>
/// <param name="FilePath">The file path relative to its origin directory.</param>
/// <param name="Length">The file length in bytes.</param>
record struct FileHashInfo (bool Matched, string FilePath, long Length);

#endregion
