using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Images.Services;

public sealed class ClipTokenizer
{
    public const int ContextLength = 77;
    public const int StartOfTextId = 49406;
    public const int EndOfTextId = 49407;

    private readonly Dictionary<string, int> _vocab;
    private readonly List<(string, string)> _merges;
    private readonly Dictionary<(string, string), int> _mergeRanks;
    private readonly Regex _pattern;

    private ClipTokenizer(Dictionary<string, int> vocab, List<(string, string)> merges)
    {
        _vocab = vocab;
        _merges = merges;
        _mergeRanks = new Dictionary<(string, string), int>(merges.Count);
        for (var i = 0; i < merges.Count; i++)
            _mergeRanks[merges[i]] = i;

        _pattern = new Regex(
            @"<\|startoftext\|>|<\|endoftext\|>|'s|'t|'re|'ve|'m|'ll|'d|[\p{L}]+|[\p{N}]|[^\s\p{L}\p{N}]+",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
    }

    public static ClipTokenizer Load(string tokenizerJsonPath)
    {
        var json = File.ReadAllText(tokenizerJsonPath, Encoding.UTF8);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var vocab = new Dictionary<string, int>();
        var merges = new List<(string, string)>();

        if (root.TryGetProperty("model", out var model))
        {
            if (model.TryGetProperty("vocab", out var vocabObj))
            {
                foreach (var prop in vocabObj.EnumerateObject())
                    vocab[prop.Name] = prop.Value.GetInt32();
            }

            if (model.TryGetProperty("merges", out var mergesArr))
            {
                foreach (var m in mergesArr.EnumerateArray())
                {
                    var parts = m.GetString()!.Split(' ', 2);
                    if (parts.Length == 2)
                        merges.Add((parts[0], parts[1]));
                }
            }
        }
        else
        {
            if (root.TryGetProperty("vocab", out var vocabObj))
            {
                foreach (var prop in vocabObj.EnumerateObject())
                    vocab[prop.Name] = prop.Value.GetInt32();
            }

            if (root.TryGetProperty("merges", out var mergesArr))
            {
                foreach (var m in mergesArr.EnumerateArray())
                {
                    var parts = m.GetString()!.Split(' ', 2);
                    if (parts.Length == 2)
                        merges.Add((parts[0], parts[1]));
                }
            }
        }

        if (vocab.Count == 0)
            throw new InvalidOperationException("Tokenizer vocabulary is empty.");

        if (!vocab.ContainsKey("<|startoftext|>"))
            vocab["<|startoftext|>"] = StartOfTextId;
        if (!vocab.ContainsKey("<|endoftext|>"))
            vocab["<|endoftext|>"] = EndOfTextId;

        return new ClipTokenizer(vocab, merges);
    }

    public long[] Encode(string text)
    {
        var tokens = new List<int> { StartOfTextId };

        var cleaned = text.ToLowerInvariant().Trim();
        if (string.IsNullOrEmpty(cleaned))
        {
            tokens.Add(EndOfTextId);
            return Pad(tokens);
        }

        foreach (Match match in _pattern.Matches(cleaned))
        {
            var word = match.Value;
            if (word == "<|startoftext|>" || word == "<|endoftext|>")
            {
                if (_vocab.TryGetValue(word, out var specialId))
                    tokens.Add(specialId);
                continue;
            }

            var bpeTokens = Bpe(word);
            foreach (var token in bpeTokens)
            {
                if (_vocab.TryGetValue(token, out var id))
                    tokens.Add(id);
            }
        }

        if (tokens.Count >= ContextLength)
        {
            tokens = tokens.Take(ContextLength - 1).ToList();
        }

        tokens.Add(EndOfTextId);
        return Pad(tokens);
    }

    private List<string> Bpe(string word)
    {
        if (word.Length == 0) return [];

        var wordParts = new List<string>();
        for (var i = 0; i < word.Length - 1; i++)
            wordParts.Add(word[i].ToString());
        wordParts.Add(word[^1] + "</w>");

        while (wordParts.Count > 1)
        {
            var bestPair = ((string, string)?)null;
            var bestRank = int.MaxValue;

            for (var i = 0; i < wordParts.Count - 1; i++)
            {
                var pair = (wordParts[i], wordParts[i + 1]);
                if (_mergeRanks.TryGetValue(pair, out var rank) && rank < bestRank)
                {
                    bestRank = rank;
                    bestPair = pair;
                }
            }

            if (bestPair is null) break;

            var (first, second) = bestPair.Value;
            var merged = first + second;
            var newParts = new List<string>(wordParts.Count);
            var j = 0;
            while (j < wordParts.Count)
            {
                if (j < wordParts.Count - 1 &&
                    wordParts[j] == first &&
                    wordParts[j + 1] == second)
                {
                    newParts.Add(merged);
                    j += 2;
                }
                else
                {
                    newParts.Add(wordParts[j]);
                    j++;
                }
            }
            wordParts = newParts;
        }

        return wordParts;
    }

    private static long[] Pad(List<int> tokens)
    {
        var result = new long[ContextLength];
        for (var i = 0; i < Math.Min(tokens.Count, ContextLength); i++)
            result[i] = tokens[i];
        return result;
    }
}
