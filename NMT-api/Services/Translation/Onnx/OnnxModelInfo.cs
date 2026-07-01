namespace NMT_api.Services.Translation.Onnx;

public sealed record OnnxModelInfo(
    string Provider,
    string ModelPath,
    string TokenizerPath,
    OnnxModelStatus Status,
    bool IsLoaded,
    bool IsRequired,
    DateTimeOffset? LoadedAt,
    int StartupMs,
    DecodingMode DecodingMode,
    int NumBeams,
    double LengthPenalty,
    int NoRepeatNgramSize,
    double RepetitionPenalty,
    IReadOnlyCollection<string> InputNames,
    IReadOnlyCollection<string> OutputNames,
    string? Message);
