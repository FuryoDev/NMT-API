using Microsoft.Extensions.Options;
using NMT_api.Services.Translation.Onnx;
using System.Text.Json;
using Tokenizers.HuggingFace.Tokenizer;

namespace NMT_api.Services.Translation.Tokenization;

public sealed class NllbTokenizer : INllbTokenizer, IDisposable
{
    private readonly Tokenizer _tokenizer;
    private readonly IReadOnlyDictionary<string, long> _tokenIds;

    public long EosTokenId => 2;

    public NllbTokenizer(IOptions<NllbOnnxOptions> options)
    {
        string tokenizerPath = ResolvePath(options.Value.TokenizerPath);

        if (!File.Exists(tokenizerPath))
        {
            throw new FileNotFoundException($"Tokenizer file not found: {tokenizerPath}");
        }

        _tokenizer = Tokenizer.FromFile(tokenizerPath);
        _tokenIds = LoadTokenIds(tokenizerPath);
    }

    public long[] Encode(string text, string sourceLanguage)
    {
        var encoding = _tokenizer.Encode(text ?? string.Empty, addSpecialTokens: true).First();
        List<long> ids = encoding.Ids.Select(id => (long)id).ToList();

        if (_tokenIds.TryGetValue(sourceLanguage, out long sourceLanguageTokenId)
            && (ids.Count == 0 || ids[0] != sourceLanguageTokenId))
        {
            ids.Insert(0, sourceLanguageTokenId);
        }

        return ids.ToArray();
    }

    public string Decode(IEnumerable<long> tokenIds)
    {
        IReadOnlyList<uint> ids = tokenIds.Select(id => (uint)id).ToArray();

        return _tokenizer.Decode(ids, skipSpecialTokens: true);
    }

    public long GetTokenId(string token)
    {
        if (!_tokenIds.TryGetValue(token, out long id))
        {
            throw new InvalidOperationException($"Token not found in tokenizer vocab: {token}");
        }

        return id;
    }

    private static IReadOnlyDictionary<string, long> LoadTokenIds(string tokenizerPath)
    {
        Dictionary<string, long> tokenIds = new(StringComparer.Ordinal);

        using FileStream stream = File.OpenRead(tokenizerPath);
        using JsonDocument document = JsonDocument.Parse(stream);
        JsonElement root = document.RootElement;

        if (root.TryGetProperty("model", out JsonElement model)
            && model.TryGetProperty("vocab", out JsonElement vocab)
            && vocab.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty token in vocab.EnumerateObject())
            {
                tokenIds[token.Name] = token.Value.GetInt64();
            }
        }

        if (root.TryGetProperty("added_tokens", out JsonElement addedTokens)
            && addedTokens.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement addedToken in addedTokens.EnumerateArray())
            {
                if (addedToken.TryGetProperty("content", out JsonElement content)
                    && addedToken.TryGetProperty("id", out JsonElement tokenId))
                {
                    string? token = content.GetString();
                    if (!string.IsNullOrEmpty(token))
                    {
                        tokenIds[token] = tokenId.GetInt64();
                    }
                }
            }
        }

        return tokenIds;
    }

    private static string ResolvePath(string configuredPath)
    {
        if (Path.IsPathRooted(configuredPath))
        {
            return configuredPath;
        }

        string contentRootCandidate = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), configuredPath));
        if (File.Exists(contentRootCandidate))
        {
            return contentRootCandidate;
        }

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, configuredPath));
    }

    public void Dispose()
    {
        _tokenizer.Dispose();
    }
}
