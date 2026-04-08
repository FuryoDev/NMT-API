namespace NMT_api.Services.Translation.Configuration;

public sealed class PythonTranslationBackendOptions
{
    public const string SectionName = "Translation:PythonBackend";

    public string BaseUrl { get; set; } = "http://127.0.0.1:8000";
    public int TimeoutSeconds { get; set; } = 120;
}
