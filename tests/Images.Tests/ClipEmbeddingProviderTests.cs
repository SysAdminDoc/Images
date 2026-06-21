using Images.Services;

namespace Images.Tests;

public sealed class ClipEmbeddingProviderTests
{
    [Fact]
    public void Constants_AreNonEmpty()
    {
        Assert.False(string.IsNullOrEmpty(ClipEmbeddingProvider.TextModelId));
        Assert.False(string.IsNullOrEmpty(ClipEmbeddingProvider.VisionModelId));
        Assert.False(string.IsNullOrEmpty(ClipEmbeddingProvider.TokenizerId));
        Assert.False(string.IsNullOrEmpty(ClipEmbeddingProvider.PreprocessorId));
    }
}
