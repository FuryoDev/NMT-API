using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NMT_api.Services.Translation.Language;
using NMT_api.Services.Translation.Onnx;
using NMT_api.Services.Translation.Tokenization;
using System.Diagnostics;

namespace NMT_api.Services.Translation.Core;

public sealed class NmtTranslationService : INmtTranslationService
{
    private readonly IOnnxNllbRunner _runner;
    private readonly IServiceProvider _serviceProvider;
    private readonly ITranslationLanguageService _languageService;
    private readonly TranslationDefaultsOptions _defaults;

    public NmtTranslationService(
        IOnnxNllbRunner runner,
        IServiceProvider serviceProvider,
        ITranslationLanguageService languageService,
        IOptions<TranslationDefaultsOptions> defaults)
    {
        _runner = runner;
        _serviceProvider = serviceProvider;
        _languageService = languageService;
        _defaults = defaults.Value;
    }

    public Task<TranslationResult> TranslateTextAsync(
        string text,
        TranslationRequestOptions options,
        CancellationToken cancellationToken)
    {
        string sourceText = text ?? string.Empty;
        if (string.IsNullOrWhiteSpace(sourceText))
        {
            throw new ArgumentException("Text cannot be empty.", nameof(text));
        }

        if (sourceText.Length > _defaults.MaxTextInputChars)
        {
            throw new ArgumentException($"Text is too large. Limit is {_defaults.MaxTextInputChars} characters.", nameof(text));
        }

        if (options.MaxNewTokens < 1)
        {
            throw new ArgumentException("MaxNewTokens must be greater than or equal to 1.", nameof(options));
        }

        cancellationToken.ThrowIfCancellationRequested();

        SupportedLanguage source = _languageService.Resolve(options.SourceLanguage);
        SupportedLanguage target = _languageService.Resolve(options.TargetLanguage);

        if (!_runner.ModelInfo.IsLoaded)
        {
            throw new OnnxModelUnavailableException(_runner.ModelInfo.Message ?? "ONNX model is not loaded.");
        }

        INllbTokenizer tokenizer;
        try
        {
            tokenizer = _serviceProvider.GetRequiredService<INllbTokenizer>();
        }
        catch (Exception ex)
        {
            throw new OnnxModelUnavailableException($"Tokenizer is not available: {ex.Message}");
        }

        long[] inputIds = tokenizer.Encode(sourceText, source.NllbCode);
        long[] attentionMask = inputIds.Select(_ => 1L).ToArray();
        long targetLanguageTokenId = tokenizer.GetTokenId(target.NllbCode);

        Stopwatch stopwatch = Stopwatch.StartNew();
        GreedyGenerationResult generation = _runner.Generate(new GreedyGenerationRequest
        {
            InputIds = inputIds,
            AttentionMask = attentionMask,
            TargetLanguageTokenId = targetLanguageTokenId,
            MaxNewTokens = options.MaxNewTokens
        });
        stopwatch.Stop();

        cancellationToken.ThrowIfCancellationRequested();

        string translatedText = tokenizer.Decode(generation.GeneratedTokenIds);

        TranslationResult result = new(
            translatedText,
            source.Code,
            target.Code,
            source.NllbCode,
            target.NllbCode,
            _runner.ModelInfo.Provider,
            (int)stopwatch.ElapsedMilliseconds,
            ChunkCount: null);

        return Task.FromResult(result);
    }
}
