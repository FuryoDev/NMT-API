using NMT_api.Services.Translation.PythonBridge.Contracts;

namespace NMT_api.Services.Translation.PythonBridge;

public interface IPythonTranslationBackendClient
{
    Task<PythonHealthResponse?> GetHealthAsync(CancellationToken cancellationToken);
    Task<PythonTranslateResponse> TranslateAsync(PythonTranslateRequest request, CancellationToken cancellationToken);
}
