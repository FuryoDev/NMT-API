namespace NMT_api.Services.Translation.Core;

public sealed record TranslationResult(
    string TranslatedText,
    string SourceLanguage,
    string TargetLanguage,
    string SourceNllbLanguage,
    string TargetNllbLanguage,
    string? Device,
    int DurationMs,
    int? ChunkCount);
