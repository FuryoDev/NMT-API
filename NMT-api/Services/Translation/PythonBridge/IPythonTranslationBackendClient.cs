using NMT_api.Contracts.Requests;
using NMT_api.Models.Translation;
using NMT_api.Services.Translation.PythonBridge.Contracts;

namespace NMT_api.Services.Translation.PythonBridge;

public interface IPythonTranslationBackendClient
{
    Task<PythonHealthResponse?> GetHealthAsync(CancellationToken cancellationToken);
    Task<PythonTranslateResponse> TranslateAsync(TranslateRequest request, CancellationToken cancellationToken);
    Task<string> TranslateFileAsync(TranslateFileRequest request, CancellationToken cancellationToken);
    Task<PythonTranslateResponse> TranslateFileJsonAsync(TranslateFileJsonRequest request, CancellationToken cancellationToken);
    Task<string> TranslateSrtAsync(TranslateSrtRequest request, CancellationToken cancellationToken);
}
