using Microsoft.Extensions.Options;
using NMT_api.Services.Translation.Onnx;
using NMT_api.Services.Translation.Language;
using Tokenizers.HuggingFace.Tokenizer;

namespace NMT_api.Services.Translation.Tokenization;

public sealed class NllbTokenizer : INllbTokenizer, IDisposable
{
    private readonly Tokenizer _tokenizer;
    private readonly IReadOnlyDictionary<string, long> _tokenIds;
    private readonly long _eosTokenId;

    public long EosTokenId => _eosTokenId;

    public NllbTokenizer(
        IOptions<NllbOnnxOptions> options,
        ITranslationLanguageService languageService)
    {
        NllbOnnxOptions nllbOptions = options.Value;
        _eosTokenId = nllbOptions.EosTokenId;
        string tokenizerPath = ResolvePath(nllbOptions.TokenizerPath);

        if (!File.Exists(tokenizerPath))
        {
            throw new FileNotFoundException($"Tokenizer file not found: {tokenizerPath}");
        }

        _tokenizer = Tokenizer.FromFile(tokenizerPath);
        _tokenIds = NllbTokenizerVocabulary.LoadTokenIds(tokenizerPath);
        NllbTokenizerVocabulary.ValidateRequiredTokens(
            _tokenIds,
            languageService.GetSupportedLanguages().Select(language => language.NllbCode));
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

    private static string ResolvePath(string configuredPath)
    {
        return OnnxModelArtifactValidator.ResolvePath(configuredPath);
    }

    public void Dispose()
    {
        _tokenizer.Dispose();
    }
}
