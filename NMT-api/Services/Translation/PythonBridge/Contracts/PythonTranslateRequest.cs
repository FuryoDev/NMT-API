namespace NMT_api.Services.Translation.PythonBridge.Contracts;

public sealed class PythonTranslateRequest
{
    public string Text { get; init; } = string.Empty;
    public string SourceLanguage { get; init; } = "fr";
    public string TargetLanguage { get; init; } = "en";
    public int MaxNewTokens { get; init; } = 256;
    public int NumBeams { get; init; } = 4;
    public bool Chunking { get; init; } = false;
    public bool Preprocess { get; init; } = false;
}
