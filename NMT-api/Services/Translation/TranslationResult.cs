namespace NMT_api.Services.Translation;

public class TranslationResult
{
    public string TranslatedText { get; init; } = string.Empty;
    public string SourceLanguage { get; init; } = string.Empty;
    public string TargetLanguage { get; init; } = string.Empty;
    public string Device { get; init; } = "cpu";
    public int DurationMs { get; init; }
}
