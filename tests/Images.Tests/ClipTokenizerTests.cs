using System.IO;
using System.Text.Json;
using Images.Services;

namespace Images.Tests;

public sealed class ClipTokenizerTests
{
    [Fact]
    public void Load_ReadsVocabAndMergesFromHuggingFaceFormat()
    {
        using var temp = TestDirectory.Create();
        var tokenizerPath = WriteTestTokenizer(temp, useModelKey: true);

        var tokenizer = ClipTokenizer.Load(tokenizerPath);

        Assert.NotNull(tokenizer);
    }

    [Fact]
    public void Load_ReadsVocabAndMergesFromFlatFormat()
    {
        using var temp = TestDirectory.Create();
        var tokenizerPath = WriteTestTokenizer(temp, useModelKey: false);

        var tokenizer = ClipTokenizer.Load(tokenizerPath);

        Assert.NotNull(tokenizer);
    }

    [Fact]
    public void Encode_EmptyString_ReturnsStartAndEndTokens()
    {
        using var temp = TestDirectory.Create();
        var tokenizerPath = WriteTestTokenizer(temp, useModelKey: true);
        var tokenizer = ClipTokenizer.Load(tokenizerPath);

        var tokens = tokenizer.Encode("");

        Assert.Equal(ClipTokenizer.ContextLength, tokens.Length);
        Assert.Equal(ClipTokenizer.StartOfTextId, tokens[0]);
        Assert.Equal(ClipTokenizer.EndOfTextId, tokens[1]);
        Assert.Equal(0, tokens[2]);
    }

    [Fact]
    public void Encode_OutputIsPaddedToContextLength()
    {
        using var temp = TestDirectory.Create();
        var tokenizerPath = WriteTestTokenizer(temp, useModelKey: true);
        var tokenizer = ClipTokenizer.Load(tokenizerPath);

        var tokens = tokenizer.Encode("hello");

        Assert.Equal(ClipTokenizer.ContextLength, tokens.Length);
        Assert.Equal(ClipTokenizer.StartOfTextId, tokens[0]);
    }

    [Fact]
    public void Encode_AlwaysStartsWithStartToken()
    {
        using var temp = TestDirectory.Create();
        var tokenizerPath = WriteTestTokenizer(temp, useModelKey: true);
        var tokenizer = ClipTokenizer.Load(tokenizerPath);

        var tokens = tokenizer.Encode("a photo of a dog");

        Assert.Equal(ClipTokenizer.StartOfTextId, tokens[0]);
    }

    [Fact]
    public void Encode_ContainsEndToken()
    {
        using var temp = TestDirectory.Create();
        var tokenizerPath = WriteTestTokenizer(temp, useModelKey: true);
        var tokenizer = ClipTokenizer.Load(tokenizerPath);

        var tokens = tokenizer.Encode("test");

        var endIdx = Array.IndexOf(tokens, (long)ClipTokenizer.EndOfTextId);
        Assert.True(endIdx > 0, "End-of-text token should appear after start token");
    }

    [Fact]
    public void Load_ThrowsOnEmptyVocab()
    {
        using var temp = TestDirectory.Create();
        var path = temp.WriteFile("empty.json", """{"model": {"vocab": {}, "merges": []}}""");

        Assert.Throws<InvalidOperationException>(() => ClipTokenizer.Load(path));
    }

    private static string WriteTestTokenizer(TestDirectory temp, bool useModelKey)
    {
        var vocab = new Dictionary<string, int>
        {
            ["<|startoftext|>"] = 49406,
            ["<|endoftext|>"] = 49407,
            ["h"] = 0, ["e"] = 1, ["l"] = 2, ["o"] = 3,
            ["he</w>"] = 4, ["ll</w>"] = 5, ["lo</w>"] = 6,
            ["hello</w>"] = 7,
            ["a</w>"] = 8, ["t"] = 9, ["s"] = 10,
            ["te</w>"] = 11, ["st</w>"] = 12, ["test</w>"] = 13,
            ["p"] = 14, ["ho</w>"] = 15, ["to</w>"] = 16,
            ["photo</w>"] = 17, ["of</w>"] = 18,
            ["d"] = 19, ["g</w>"] = 20, ["do</w>"] = 21, ["dog</w>"] = 22,
        };

        var merges = new[]
        {
            "h e</w>", "l l</w>", "l o</w>", "he ll</w>",
            "t e</w>", "s t</w>", "te st</w>",
            "h o</w>", "t o</w>", "p ho</w>",
            "d o</w>", "do g</w>",
        };

        object jsonObj;
        if (useModelKey)
        {
            jsonObj = new { model = new { vocab, merges } };
        }
        else
        {
            jsonObj = new { vocab, merges };
        }

        var json = JsonSerializer.Serialize(jsonObj);
        return temp.WriteFile("tokenizer.json", json);
    }
}
