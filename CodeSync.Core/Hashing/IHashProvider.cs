namespace CodeSync.Core;

/// <summary>
///   Computes the canonical persisted digest for file content.
/// </summary>
public interface IHashProvider
{
    /// <summary>
    ///   Computes the SHA-256 hash of the specified content.
    /// </summary>
    /// <param name="content">The content to compute the hash for.</param>
    /// <returns>The SHA-256 hash of the specified content as a hexadecimal string.</returns>
    string ComputeSha256(ReadOnlySpan<byte> content);

    /// <summary>
    ///   Computes the SHA-256 hash of the specified content.
    /// </summary>
    /// <param name="content">The content stream to compute the hash for.</param>
    /// <returns>The SHA-256 hash of the specified content as a hexadecimal string.</returns>
    string ComputeSha256(Stream content);

    /// <summary>
    ///   Computes the SHA-256 hash of the specified content asynchronously.
    /// </summary>
    /// <param name="content">The content stream to hash.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The SHA-256 hash as a canonical lowercase hexadecimal string.</returns>
    Task<string> ComputeSha256Async(Stream content, CancellationToken cancellationToken = default);
}
