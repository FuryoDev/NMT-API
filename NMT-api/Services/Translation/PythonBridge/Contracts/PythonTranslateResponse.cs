namespace NMT_api.Services.Translation.PythonBridge.Contracts;

public sealed class PythonTranslateResponse
{
    public string TranslatedText { get; init; } = string.Empty;
    public string Device { get; init; } = string.Empty;
    public int DurationMs { get; init; }
}
