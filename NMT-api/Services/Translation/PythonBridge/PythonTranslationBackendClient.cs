using System.Net.Http.Json;
using NMT_api.Contracts.Requests;
using NMT_api.Models.Translation;
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

    public async Task<PythonTranslateResponse> TranslateAsync(TranslateRequest request, CancellationToken cancellationToken)
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

    public async Task<string> TranslateFileAsync(TranslateFileRequest request, CancellationToken cancellationToken)
    {
        using MultipartFormDataContent form = BuildFileRequest(request.File, request.SourceLanguage, request.TargetLanguage, request.MaxNewTokens, request.NumBeams);
        using HttpResponseMessage response = await httpClient.PostAsync("/translate/file", form, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    public async Task<PythonTranslateResponse> TranslateFileJsonAsync(TranslateFileJsonRequest request, CancellationToken cancellationToken)
    {
        using MultipartFormDataContent form = BuildFileRequest(request.File, request.SourceLanguage, request.TargetLanguage, request.MaxNewTokens, request.NumBeams);
        using HttpResponseMessage response = await httpClient.PostAsync("/translate/file/json", form, cancellationToken);
        response.EnsureSuccessStatusCode();

        PythonTranslateResponse? payload = await response.Content.ReadFromJsonAsync<PythonTranslateResponse>(cancellationToken: cancellationToken);
        if (payload is null)
        {
            throw new InvalidOperationException("Python translation backend returned an empty payload.");
        }

        return payload;
    }

    public async Task<string> TranslateSrtAsync(TranslateSrtRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request.File);

        using MultipartFormDataContent form = new();
        using StreamContent fileContent = new(request.File.OpenReadStream());
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(request.File.ContentType ?? "application/octet-stream");

        form.Add(fileContent, "file", request.File.FileName);
        form.Add(new StringContent(request.SourceLanguage), "sourceLanguage");
        form.Add(new StringContent(request.TargetLanguage), "targetLanguage");
        form.Add(new StringContent(request.MaxNewTokens.ToString()), "maxNewTokens");
        form.Add(new StringContent(request.NumBeams.ToString()), "numBeams");

        using HttpResponseMessage response = await httpClient.PostAsync("/translate/srt", form, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    private static MultipartFormDataContent BuildFileRequest(
        IFormFile? file,
        string sourceLanguage,
        string targetLanguage,
        int maxNewTokens,
        int numBeams)
    {
        ArgumentNullException.ThrowIfNull(file);

        MultipartFormDataContent form = new();
        StreamContent fileContent = new(file.OpenReadStream());
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType ?? "application/octet-stream");

        form.Add(fileContent, "file", file.FileName);
        form.Add(new StringContent(sourceLanguage), "sourceLanguage");
        form.Add(new StringContent(targetLanguage), "targetLanguage");
        form.Add(new StringContent(maxNewTokens.ToString()), "maxNewTokens");
        form.Add(new StringContent(numBeams.ToString()), "numBeams");

        return form;
    }
}
