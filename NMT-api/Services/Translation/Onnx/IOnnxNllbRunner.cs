namespace NMT_api.Services.Translation.Onnx;

public interface IOnnxNllbRunner
{
    bool IsLoaded { get; }
    string RuntimeDevice { get; }
    string RuntimeDetails { get; }
    IReadOnlyList<int> Generate(
        IReadOnlyList<int> inputTokenIds,
        int forcedBosTokenId,
        int maxNewTokens,
        int numBeams,
        int noRepeatNgramSize,
        float repetitionPenalty);
}
