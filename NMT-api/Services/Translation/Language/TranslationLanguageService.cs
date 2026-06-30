using Microsoft.Extensions.Options;

namespace NMT_api.Services.Translation.Language;

public sealed class TranslationLanguageService : ITranslationLanguageService
{
    private readonly IReadOnlyDictionary<string, string> _languages;

    public TranslationLanguageService(IOptions<TranslationLanguageOptions> options)
    {
        _languages = new Dictionary<string, string>(
            options.Value.SupportedLanguages,
            StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyCollection<SupportedLanguage> GetSupportedLanguages()
    {
        return _languages
            .OrderBy(language => language.Key, StringComparer.OrdinalIgnoreCase)
            .Select(language => new SupportedLanguage(language.Key, language.Value))
            .ToArray();
    }

    public SupportedLanguage Resolve(string languageCode)
    {
        string normalized = Normalize(languageCode);

        if (!_languages.TryGetValue(normalized, out string? nllbCode))
        {
            throw new UnsupportedLanguageException(normalized, _languages.Keys);
        }

        return new SupportedLanguage(normalized, nllbCode);
    }

    public bool IsSupported(string languageCode)
    {
        return _languages.ContainsKey(Normalize(languageCode));
    }

    private static string Normalize(string languageCode)
    {
        return (languageCode ?? string.Empty).Trim().ToLowerInvariant();
    }
}
