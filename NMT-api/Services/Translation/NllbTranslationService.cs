using System.Diagnostics;
using NMT_api.Services.Translation.Configuration;
using NMT_api.Services.Translation.PythonBridge;
using NMT_api.Services.Translation.PythonBridge.Contracts;

namespace NMT_api.Services.Translation;

public class NllbTranslationService : INmtTranslationService
{
    private static readonly IReadOnlyDictionary<string, string> LanguageMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["fr"] = "fra_Latn",
        ["en"] = "eng_Latn",
        ["nl"] = "nld_Latn",
        ["ar"] = "arb_Arab",
        ["de"] = "deu_Latn",
        ["es"] = "spa_Latn",
        ["it"] = "ita_Latn"
    };

    private readonly IPythonTranslationBackendClient _pythonClient;

    public string ModelName { get; }
    public string Device { get; }
    public int StartupMs { get; }

    public NllbTranslationService(
        IConfiguration configuration,
        IPythonTranslationBackendClient pythonClient,
        ILogger<NllbTranslationService> logger)
    {
        Stopwatch sw = Stopwatch.StartNew();
        _pythonClient = pythonClient;

        PythonTranslationBackendOptions options = configuration
            .GetSection(PythonTranslationBackendOptions.SectionName)
            .Get<PythonTranslationBackendOptions>()
            ?? new PythonTranslationBackendOptions();

        using CancellationTokenSource healthTimeout = new(TimeSpan.FromSeconds(options.StartupHealthCheckTimeoutSeconds));
        try
        {
            PythonHealthResponse? health = _pythonClient.GetHealthAsync(healthTimeout.Token).GetAwaiter().GetResult();

            ModelName = !string.IsNullOrWhiteSpace(health?.Model) ? health.Model : options.FallbackModelName;
            Device = !string.IsNullOrWhiteSpace(health?.Device) ? health.Device : options.FallbackDevice;
            StartupMs = health?.StartupMs > 0 ? health.StartupMs : (int)sw.ElapsedMilliseconds;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Python backend health check failed at startup. Fallback monitoring values are used.");
            ModelName = options.FallbackModelName;
            Device = options.FallbackDevice;
            StartupMs = (int)sw.ElapsedMilliseconds;
        }
    }

    public string NormalizeLanguage(string language)
    {
        string key = (language ?? string.Empty).Trim();
        return LanguageMap.TryGetValue(key, out string? value) ? value : key;
    }

    public TranslationResult Translate(
        string text,
        string sourceLanguage,
        string targetLanguage,
        int maxNewTokens = 256,
        int numBeams = 4)
    {
        Stopwatch sw = Stopwatch.StartNew();

        string normalizedText = (text ?? string.Empty).Trim();
        string source = NormalizeLanguage(sourceLanguage);
        string target = NormalizeLanguage(targetLanguage);

        if (string.IsNullOrWhiteSpace(normalizedText))
        {
            return new TranslationResult
            {
                TranslatedText = string.Empty,
                SourceLanguage = source,
                TargetLanguage = target,
                Device = Device,
                DurationMs = (int)sw.ElapsedMilliseconds
            };
        }

        PythonTranslateResponse response = _pythonClient.TranslateAsync(
            new PythonTranslateRequest
            {
                Text = normalizedText,
                SourceLanguage = source,
                TargetLanguage = target,
                MaxNewTokens = maxNewTokens,
                NumBeams = numBeams,
                Chunking = false,
                Preprocess = false
            },
            CancellationToken.None).GetAwaiter().GetResult();

        return new TranslationResult
        {
            TranslatedText = response.TranslatedText,
            SourceLanguage = source,
            TargetLanguage = target,
            Device = string.IsNullOrWhiteSpace(response.Device) ? Device : response.Device,
            DurationMs = response.DurationMs > 0 ? response.DurationMs : (int)sw.ElapsedMilliseconds
        };
    }
}
