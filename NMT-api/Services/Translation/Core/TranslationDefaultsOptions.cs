namespace NMT_api.Services.Translation.Core;

public sealed class TranslationDefaultsOptions
{
    public string SourceLanguage { get; set; } = "fr";
    public string TargetLanguage { get; set; } = "en";
    public int MaxNewTokens { get; set; } = 512;
    public int MaxTextInputChars { get; set; } = 200_000;
    public int SrtMaxLineLength { get; set; } = 42;
}
