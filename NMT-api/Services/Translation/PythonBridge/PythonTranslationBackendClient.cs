using System.Net.Http.Json;
using NMT_api.Services.Translation.PythonBridge.Contracts;

namespace NMT_api.Services.Translation.PythonBridge;

public sealed class PythonTranslationBackendClient(HttpClient httpClient) : IPythonTranslationBackendClient
{
    public async Task<PythonHealthResponse?> GetHealthAsync(CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await httpClient.GetAsync("/health", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<PythonHealthResponse>(cancellationToken: cancellationToken);
    }

    public async Task<PythonTranslateResponse> TranslateAsync(PythonTranslateRequest request, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await httpClient.PostAsJsonAsync("/translate", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        PythonTranslateResponse? payload = await response.Content.ReadFromJsonAsync<PythonTranslateResponse>(cancellationToken: cancellationToken);
        if (payload is null)
        {
            throw new InvalidOperationException("Python translation backend returned an empty payload.");
        }

        return payload;
    }
}
