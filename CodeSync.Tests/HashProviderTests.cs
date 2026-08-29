using CodeSync.Core;

namespace CodeSync.Tests;

public sealed class HashProviderTests
{
    private readonly Sha256HashProvider _hashProvider = new();

    [Fact]
    public void Sha256HashProvider_ReturnsCanonicalLowercaseDigest()
    {
        var hash = _hashProvider.ComputeSha256("hello"u8);

        Assert.Equal("2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824", hash);
    }

    [Fact]
    public async Task Sha256HashProvider_ReturnsSameDigestAsynchronously()
    {
        await using var content = new MemoryStream("hello"u8.ToArray());

        var hash = await _hashProvider.ComputeSha256Async(content);

        Assert.Equal("2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824", hash);
    }
}
