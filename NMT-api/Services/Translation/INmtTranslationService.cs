namespace NMT_api.Services.Translation;

public interface INmtTranslationService
{
    string ModelName { get; }
    string Device { get; }
    int StartupMs { get; }
    TranslationResult Translate(
        string text,
        string sourceLanguage,
        string targetLanguage,
        int maxNewTokens = 256,
        int numBeams = 4);

    string NormalizeLanguage(string language);
}
