using System.Diagnostics;
using NMT_api.Services.Translation.Configuration;
using NMT_api.Services.Translation.Onnx;

namespace NMT_api.Services.Translation;

public class NllbTranslationService : INmtTranslationService
{
    private static readonly IReadOnlyDictionary<string, string> LanguageMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["fr"] = "fra_Latn",
        ["en"] = "eng_Latn",
        ["nl"] = "nld_Latn",
        ["ar"] = "arb_Arab",
        ["de"] = "deu_Latn",
        ["es"] = "spa_Latn",
        ["it"] = "ita_Latn"
    };

    private readonly ITranslationTokenizer _tokenizer;
    private readonly IOnnxNllbRunner _onnxRunner;
    private readonly NllbOnnxOptions _options;

    public string ModelName { get; }
    public string Device { get; }
    public int StartupMs { get; }

    public NllbTranslationService(
        IConfiguration configuration,
        ITranslationTokenizer tokenizer,
        IOnnxNllbRunner onnxRunner)
    {
        Stopwatch sw = Stopwatch.StartNew();

        _options = configuration.GetSection(NllbOnnxOptions.SectionName).Get<NllbOnnxOptions>() ?? new NllbOnnxOptions();
        _tokenizer = tokenizer;
        _onnxRunner = onnxRunner;

        ModelName = _options.ModelName;
        Device = _onnxRunner.RuntimeDevice;
        StartupMs = (int)sw.ElapsedMilliseconds;
    }

    public string NormalizeLanguage(string language)
    {
        string key = (language ?? string.Empty).Trim();
        return LanguageMap.TryGetValue(key, out string? value) ? value : key;
    }

    public TranslationResult Translate(
        string text,
        string sourceLanguage,
        string targetLanguage,
        int maxNewTokens = 256,
        int numBeams = 4)
    {
        Stopwatch sw = Stopwatch.StartNew();

        string source = NormalizeLanguage(sourceLanguage);
        string target = NormalizeLanguage(targetLanguage);
        string cleanText = (text ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(cleanText))
        {
            return new TranslationResult
            {
                TranslatedText = string.Empty,
                SourceLanguage = source,
                TargetLanguage = target,
                Device = Device,
                DurationMs = (int)sw.ElapsedMilliseconds
            };
        }

        int[] inputIds = _tokenizer.Encode(cleanText, source, _options.MaxInputTokens);
        int forcedBosTokenId = _tokenizer.ResolveForcedBosTokenId(target);

        IReadOnlyList<int> outputIds = _onnxRunner.Generate(
            inputIds,
            forcedBosTokenId,
            maxNewTokens > 0 ? maxNewTokens : _options.DefaultMaxNewTokens,
            numBeams > 0 ? numBeams : _options.DefaultNumBeams,
            _options.NoRepeatNgramSize,
            _options.RepetitionPenalty);

        string translated = _tokenizer.Decode(outputIds);

        return new TranslationResult
        {
            TranslatedText = string.IsNullOrWhiteSpace(translated) ? cleanText : translated,
            SourceLanguage = source,
            TargetLanguage = target,
            Device = Device,
            DurationMs = (int)sw.ElapsedMilliseconds
        };
    }
}
