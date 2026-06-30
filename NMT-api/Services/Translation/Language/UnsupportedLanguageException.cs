namespace NMT_api.Services.Translation.Language;

public sealed class UnsupportedLanguageException : ArgumentException
{
    public UnsupportedLanguageException(string languageCode, IEnumerable<string> supportedLanguages)
        : base($"Unsupported language '{languageCode}'. Supported languages are: {string.Join(", ", supportedLanguages.Order())}.")
    {
        LanguageCode = languageCode;
        SupportedLanguages = supportedLanguages.Order().ToArray();
    }

    public string LanguageCode { get; }
    public IReadOnlyCollection<string> SupportedLanguages { get; }
}
