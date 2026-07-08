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

    [Fact]
    public void BuildAttentionMask_IncludesZeroTokenBeforeEndToken()
    {
        var tokenIds = new long[ClipTokenizer.ContextLength];
        tokenIds[0] = ClipTokenizer.StartOfTextId;
        tokenIds[1] = 0;
        tokenIds[2] = ClipTokenizer.EndOfTextId;

        var mask = ClipEmbeddingProvider.BuildAttentionMask(tokenIds);

        Assert.Equal(1, mask[0]);
        Assert.Equal(1, mask[1]);
        Assert.Equal(1, mask[2]);
        Assert.Equal(0, mask[3]);
    }

    [Fact]
    public void CreateSessionPair_WhenVisionCreationFails_DisposesTextSession()
    {
        var textSession = new FakeSession();

        Assert.Throws<InvalidOperationException>(() =>
            ClipEmbeddingProvider.CreateSessionPair(
                new object(),
                _ => textSession,
                _ => throw new InvalidOperationException("vision failed"),
                session => session.Dispose()));

        Assert.True(textSession.Disposed);
    }

    private sealed class FakeSession : IDisposable
    {
        public bool Disposed { get; private set; }

        public void Dispose()
            => Disposed = true;
    }
}
