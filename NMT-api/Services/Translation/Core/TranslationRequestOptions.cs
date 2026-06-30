namespace NMT_api.Services.Translation.Core;

public sealed record TranslationRequestOptions(
    string SourceLanguage,
    string TargetLanguage,
    int MaxNewTokens);
