using System.Security.Cryptography;

namespace CodeSync.Core;

/// <summary>
///   Computes SHA-256 digests in the profile's canonical representation.
/// </summary>
public sealed class Sha256HashProvider : IHashProvider
{
    /// <inheritdoc/>
    public string ComputeSha256(ReadOnlySpan<byte> content)
    {
        Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(content, hash);

        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <inheritdoc/>
    public string ComputeSha256(Stream content)
    {
        Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(content, hash);

        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <inheritdoc/>
    public async Task<string> ComputeSha256Async(Stream content, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        var hash = await SHA256.HashDataAsync(content, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
