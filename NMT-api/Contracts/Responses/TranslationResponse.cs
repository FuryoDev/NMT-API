namespace NMT_api.Contracts.Responses;

public sealed class TranslationResponse
{
    public string TranslatedText { get; set; } = string.Empty;
    public string SourceLanguage { get; set; } = string.Empty;
    public string TargetLanguage { get; set; } = string.Empty;
    public string SourceNllbLanguage { get; set; } = string.Empty;
    public string TargetNllbLanguage { get; set; } = string.Empty;
    public string? Device { get; set; }
    public int DurationMs { get; set; }
    public int? ChunkCount { get; set; }
}
