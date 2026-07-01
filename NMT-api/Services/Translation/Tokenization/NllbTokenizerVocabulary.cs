using System.Text.Json;

namespace NMT_api.Services.Translation.Tokenization;

public static class NllbTokenizerVocabulary
{
    public static IReadOnlyDictionary<string, long> LoadTokenIds(string tokenizerPath)
    {
        Dictionary<string, long> tokenIds = new(StringComparer.Ordinal);

        using FileStream stream = File.OpenRead(tokenizerPath);
        using JsonDocument document = JsonDocument.Parse(stream);
        JsonElement root = document.RootElement;

        if (root.TryGetProperty("model", out JsonElement model)
            && model.TryGetProperty("vocab", out JsonElement vocab))
        {
            AddModelVocabTokens(tokenIds, vocab);
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

        if (tokenIds.Count == 0)
        {
            throw new InvalidOperationException($"No token id could be read from tokenizer file: {tokenizerPath}");
        }

        return tokenIds;
    }

    private static void AddModelVocabTokens(Dictionary<string, long> tokenIds, JsonElement vocab)
    {
        if (vocab.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty token in vocab.EnumerateObject())
            {
                tokenIds[token.Name] = token.Value.GetInt64();
            }

            return;
        }

        if (vocab.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        long inferredId = 0;
        foreach (JsonElement tokenEntry in vocab.EnumerateArray())
        {
            string? token = tokenEntry.ValueKind switch
            {
                JsonValueKind.String => tokenEntry.GetString(),
                JsonValueKind.Array when tokenEntry.GetArrayLength() > 0 => tokenEntry[0].GetString(),
                JsonValueKind.Object when tokenEntry.TryGetProperty("content", out JsonElement content) => content.GetString(),
                _ => null
            };

            long tokenId = tokenEntry.ValueKind == JsonValueKind.Object
                && tokenEntry.TryGetProperty("id", out JsonElement explicitId)
                    ? explicitId.GetInt64()
                    : inferredId;

            if (!string.IsNullOrEmpty(token))
            {
                tokenIds[token] = tokenId;
            }

            inferredId++;
        }
    }

    public static void ValidateRequiredTokens(
        IReadOnlyDictionary<string, long> tokenIds,
        IEnumerable<string> requiredTokens)
    {
        string[] missingTokens = requiredTokens
            .Distinct(StringComparer.Ordinal)
            .Where(token => !tokenIds.ContainsKey(token))
            .Order(StringComparer.Ordinal)
            .ToArray();

        if (missingTokens.Length > 0)
        {
            throw new InvalidOperationException(
                $"Tokenizer vocabulary is missing required NLLB language token(s): {string.Join(", ", missingTokens)}.");
        }
    }
}
