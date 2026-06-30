namespace NMT_api.Services.Translation.Core;

public interface INmtTranslationService
{
    Task<TranslationResult> TranslateTextAsync(
        string text,
        TranslationRequestOptions options,
        CancellationToken cancellationToken);
}
