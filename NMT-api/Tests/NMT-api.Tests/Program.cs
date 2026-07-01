using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.DataProtection;
using NMT_api.Services.Security;
using NMT_api.Services.Translation.Core;
using NMT_api.Services.Translation.Language;
using NMT_api.Services.Translation.Onnx;
using NMT_api.Services.Translation.Srt;
using NMT_api.Services.Translation.Tokenization;

List<(string Name, Action Test)> tests =
[
    ("language aliases resolve to expected NLLB codes", LanguageAliasesResolve),
    ("tokenizer vocabulary resolves configured language tokens", TokenizerVocabularyResolvesTokens),
    ("tokenizer vocabulary reports missing language tokens", TokenizerVocabularyReportsMissingTokens),
    ("API token service validates issued tokens", ApiTokenServiceValidatesIssuedTokens),
    ("translation resolves target token through tokenizer", TranslationResolvesTargetTokenThroughTokenizer),
    ("SRT parse and build preserve indexes and timecodes", SrtParseAndBuildPreserveStructure),
    ("missing ONNX artifacts report explicit validation errors", MissingOnnxArtifactsReportValidationErrors)
];

int failed = 0;

foreach ((string name, Action test) in tests)
{
    try
    {
        test();
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception ex)
    {
        failed++;
        Console.WriteLine($"FAIL {name}: {ex.Message}");
    }
}

if (failed > 0)
{
    Environment.ExitCode = 1;
}

static void LanguageAliasesResolve()
{
    TranslationLanguageService service = new(Options.Create(new TranslationLanguageOptions()));

    AssertEqual("nob_Latn", service.Resolve("no").NllbCode);
    AssertEqual("nob_Latn", service.Resolve("NO").NllbCode);
    AssertEqual("nob_Latn", service.Resolve("nb").NllbCode);
    AssertEqual("nob_Latn", service.Resolve("no-NO").NllbCode);
    AssertEqual("nno_Latn", service.Resolve("nn").NllbCode);
    AssertEqual("nno_Latn", service.Resolve("nn-NO").NllbCode);
    AssertEqual("zho_Hans", service.Resolve("zh").NllbCode);
    AssertEqual("zho_Hans", service.Resolve("zh-Hans").NllbCode);
    AssertEqual("zho_Hans", service.Resolve("ZH-HANS").NllbCode);
    AssertEqual("zho_Hans", service.Resolve("zh-CN").NllbCode);
    AssertEqual("zho_Hant", service.Resolve("zh-Hant").NllbCode);
    AssertEqual("zho_Hant", service.Resolve("zh-TW").NllbCode);
    AssertEqual("zho_Hant", service.Resolve("zh-HK").NllbCode);
    AssertEqual("fra_Latn", service.Resolve("FR").NllbCode);
}

static void TokenizerVocabularyResolvesTokens()
{
    string tokenizerPath = WriteTokenizerJson("fra_Latn", "eng_Latn", "nob_Latn", "nno_Latn", "zho_Hans", "zho_Hant");
    IReadOnlyDictionary<string, long> tokenIds = NllbTokenizerVocabulary.LoadTokenIds(tokenizerPath);

    AssertTrue(tokenIds.ContainsKey("fra_Latn"), "fra_Latn should be present.");
    AssertTrue(tokenIds.ContainsKey("zho_Hant"), "zho_Hant should be present.");

    NllbTokenizerVocabulary.ValidateRequiredTokens(tokenIds, ["fra_Latn", "zho_Hant"]);
}

static void TokenizerVocabularyReportsMissingTokens()
{
    string tokenizerPath = WriteTokenizerJson("fra_Latn");
    IReadOnlyDictionary<string, long> tokenIds = NllbTokenizerVocabulary.LoadTokenIds(tokenizerPath);

    AssertThrows<InvalidOperationException>(
        () => NllbTokenizerVocabulary.ValidateRequiredTokens(tokenIds, ["fra_Latn", "eng_Latn"]),
        "eng_Latn");
}

static void ApiTokenServiceValidatesIssuedTokens()
{
    string keyRingPath = Path.Combine(Path.GetTempPath(), $"nmt-token-keys-{Guid.NewGuid():N}");
    DirectoryInfo keyRingDirectory = Directory.CreateDirectory(keyRingPath);
    ApiTokenService service = new(
        DataProtectionProvider.Create(keyRingDirectory),
        Options.Create(new ApiTokenOptions { ExpirationMinutes = 5 }));

    ApiTokenIssueResult token = service.Issue();
    ApiTokenValidationResult valid = service.Validate(token.Token);
    ApiTokenValidationResult invalid = service.Validate("not-a-valid-token");

    AssertTrue(valid.IsValid, "Issued token should be valid.");
    AssertTrue(valid.ExpiresAt is not null, "Valid token should expose its expiration.");
    AssertTrue(!invalid.IsValid, "Malformed token should be rejected.");
}

static void TranslationResolvesTargetTokenThroughTokenizer()
{
    TranslationLanguageService languageService = new(Options.Create(new TranslationLanguageOptions()));
    FakeOnnxRunner runner = new();
    FakeTokenizer tokenizer = new();
    NmtTranslationService service = new(
        runner,
        new SingleServiceProvider(tokenizer),
        languageService,
        Options.Create(new TranslationDefaultsOptions()));

    TranslationResult result = service
        .TranslateTextAsync(
            "Bonjour",
            new TranslationRequestOptions("fr", "zh-Hant", 9),
            CancellationToken.None)
        .GetAwaiter()
        .GetResult();

    AssertEqual("translated", result.TranslatedText);
    AssertTrue(runner.LastRequest is not null, "Runner should receive a generation request.");
    AssertEqual(456L, runner.LastRequest!.TargetLanguageTokenId);
    AssertEqual(9, runner.LastRequest.MaxNewTokens);
    AssertEqual("fra_Latn", tokenizer.LastSourceLanguage);
}

static void SrtParseAndBuildPreserveStructure()
{
    const string srt = """
1
00:00:01,000 --> 00:00:03,000
Bonjour le monde

2
00:00:04,000 --> 00:00:05,500
Deuxieme ligne
""";

    SrtService service = new();
    SrtDocument document = service.Parse(srt);

    AssertEqual(2, document.Blocks.Count);
    AssertEqual(1, document.Blocks[0].Index);
    AssertEqual("00:00:01,000 --> 00:00:03,000", document.Blocks[0].TimeRange);
    AssertEqual(2, document.Blocks[1].Index);
    AssertEqual("00:00:04,000 --> 00:00:05,500", document.Blocks[1].TimeRange);

    string rebuilt = service.Build(document);
    AssertTrue(rebuilt.Contains("00:00:01,000 --> 00:00:03,000"), "First timecode should be preserved.");
    AssertTrue(rebuilt.Contains("00:00:04,000 --> 00:00:05,500"), "Second timecode should be preserved.");
}

static void MissingOnnxArtifactsReportValidationErrors()
{
    NllbOnnxOptions options = new()
    {
        ModelPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing.onnx"),
        TokenizerPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "tokenizer.json"),
        ModelRequired = false
    };

    OnnxModelArtifactValidationResult result = OnnxModelArtifactValidator.Validate(options);

    AssertEqual(OnnxModelStatus.Missing, result.Status);
    AssertTrue(!result.IsValid, "Artifacts should be invalid.");
    AssertTrue(result.Message?.Contains("model file", StringComparison.OrdinalIgnoreCase) == true, "Missing model error should be explicit.");
}

static string WriteTokenizerJson(params string[] tokens)
{
    string path = Path.Combine(Path.GetTempPath(), $"nmt-tokenizer-{Guid.NewGuid():N}.json");
    List<string> entries = [];
    long id = 100;

    foreach (string token in tokens)
    {
        entries.Add($$"""
        { "id": {{id++}}, "content": "{{token}}" }
        """);
    }

    string json = $$"""
    {
      "model": {
        "vocab": {
          "<unk>": 0
        }
      },
      "added_tokens": [
        {{string.Join(",\n        ", entries)}}
      ]
    }
    """;

    File.WriteAllText(path, json);
    return path;
}

static void AssertEqual<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"Expected '{expected}', got '{actual}'.");
    }
}

static void AssertTrue(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static void AssertThrows<TException>(Action action, string expectedMessagePart)
    where TException : Exception
{
    try
    {
        action();
    }
    catch (TException ex)
    {
        if (!ex.Message.Contains(expectedMessagePart, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Expected exception message to contain '{expectedMessagePart}', got '{ex.Message}'.");
        }

        return;
    }

    throw new InvalidOperationException($"Expected exception {typeof(TException).Name} was not thrown.");
}

sealed class FakeOnnxRunner : IOnnxNllbRunner
{
    public GreedyGenerationRequest? LastRequest { get; private set; }

    public OnnxModelInfo ModelInfo { get; } = new(
        "fake",
        "model.onnx",
        "tokenizer.json",
        OnnxModelStatus.Loaded,
        IsLoaded: true,
        IsRequired: true,
        LoadedAt: DateTimeOffset.UtcNow,
        StartupMs: 1,
        DecodingMode.Greedy,
        NumBeams: 1,
        LengthPenalty: 1.0,
        NoRepeatNgramSize: 0,
        RepetitionPenalty: 1.0,
        InputNames: ["input_ids", "attention_mask", "decoder_input_ids"],
        OutputNames: ["logits"],
        Message: "loaded");

    public GreedyGenerationResult Generate(GreedyGenerationRequest request)
    {
        LastRequest = request;
        return new GreedyGenerationResult
        {
            GeneratedTokenIds = [2, request.TargetLanguageTokenId]
        };
    }
}

sealed class FakeTokenizer : INllbTokenizer
{
    public string? LastSourceLanguage { get; private set; }

    public long EosTokenId => 2;

    public long[] Encode(string text, string sourceLanguage)
    {
        LastSourceLanguage = sourceLanguage;
        return [11, 12];
    }

    public string Decode(IEnumerable<long> tokenIds)
    {
        return "translated";
    }

    public long GetTokenId(string token)
    {
        return token switch
        {
            "zho_Hant" => 456,
            _ => throw new InvalidOperationException($"Unexpected token requested: {token}")
        };
    }
}

sealed class SingleServiceProvider(object service) : IServiceProvider
{
    public object? GetService(Type serviceType)
    {
        return serviceType.IsInstanceOfType(service)
            ? service
            : null;
    }
}
