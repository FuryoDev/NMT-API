namespace NMT_api.Models.Translation;

public class TranslateResponse
{
    public string TranslatedText { get; set; } = string.Empty;
    public string Device { get; set; } = "cpu";
    public int DurationMs { get; set; }
    public List<string>? Preprocessing { get; set; }
}
