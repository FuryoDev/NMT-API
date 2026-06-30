namespace NMT_api.Contracts.Requests;

public sealed class TranslateTextRequest
{
    public string Text { get; set; } = string.Empty;
    public string SourceLanguage { get; set; } = "fr";
    public string TargetLanguage { get; set; } = "en";
    public int MaxNewTokens { get; set; } = 512;
}
