namespace NMT_api.Services.Translation.Onnx;

public sealed record OnnxModelInfo(
    string Provider,
    string ModelPath,
    bool IsLoaded,
    DateTimeOffset LoadedAt,
    int StartupMs,
    IReadOnlyCollection<string> InputNames,
    IReadOnlyCollection<string> OutputNames);
