using System.Diagnostics;

namespace NMT_api.Services.Translation;

public class NllbTranslationService : INmtTranslationService
{
    public const string DefaultModelName = "facebook/nllb-200-distilled-600M";

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

    public string ModelName { get; }
    public string Device { get; }
    public int StartupMs { get; }

    public NllbTranslationService()
    {
        Stopwatch sw = Stopwatch.StartNew();

        ModelName = DefaultModelName;
        Device = "cpu";

        // TODO: brancher ici une implémentation .NET du modèle NLLB (ONNX/Transformers).
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
        _ = maxNewTokens;
        _ = numBeams;

        Stopwatch sw = Stopwatch.StartNew();
        string source = NormalizeLanguage(sourceLanguage);
        string target = NormalizeLanguage(targetLanguage);

        // TODO: remplacer ce fallback par une vraie traduction du modèle.
        string translated = (text ?? string.Empty).Trim();

        return new TranslationResult
        {
            TranslatedText = translated,
            SourceLanguage = source,
            TargetLanguage = target,
            Device = Device,
            DurationMs = (int)sw.ElapsedMilliseconds
        };
    }
}
