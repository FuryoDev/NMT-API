namespace NMT_api.Models.Translation;

public class TranslateRequest
{
    public string Text { get; set; } = string.Empty;
    public string SourceLanguage { get; set; } = "fr";
    public string TargetLanguage { get; set; } = "en";
    public int MaxNewTokens { get; set; } = 256;
    public int NumBeams { get; set; } = 4;
    public bool Preprocess { get; set; } = true;
}
