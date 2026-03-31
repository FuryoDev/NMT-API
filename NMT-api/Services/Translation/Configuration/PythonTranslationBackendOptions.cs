namespace NMT_api.Services.Translation.Configuration;

public sealed class PythonTranslationBackendOptions
{
    public const string SectionName = "Translation:PythonBackend";

    public string BaseUrl { get; set; } = "http://127.0.0.1:8000";
    public int TimeoutSeconds { get; set; } = 120;
    public int StartupHealthCheckTimeoutSeconds { get; set; } = 10;
    public string FallbackModelName { get; set; } = "facebook/nllb-200-distilled-600M";
    public string FallbackDevice { get; set; } = "python-backend";
    public bool AutoStartInDevelopment { get; set; } = false;
    public string AutoStartWorkingDirectory { get; set; } = "../python projet";
    public string AutoStartCommand { get; set; } = "python";
    public string AutoStartArguments { get; set; } = "-m uvicorn api:app --host 127.0.0.1 --port 8000";
    public int AutoStartWarmupDelaySeconds { get; set; } = 2;
}
