namespace NMT_api.Services.Translation.Language;

public interface ITranslationLanguageService
{
    IReadOnlyCollection<SupportedLanguage> GetSupportedLanguages();
    SupportedLanguage Resolve(string languageCode);
    bool IsSupported(string languageCode);
}
