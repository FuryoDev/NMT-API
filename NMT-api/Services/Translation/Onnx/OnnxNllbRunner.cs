using Microsoft.ML.OnnxRuntime;
using NMT_api.Services.Translation.Configuration;

namespace NMT_api.Services.Translation.Onnx;

public sealed class OnnxNllbRunner : IOnnxNllbRunner, IDisposable
{
    private readonly InferenceSession? _session;

    public bool IsLoaded => _session is not null;
    public string RuntimeDevice { get; }
    public string RuntimeDetails { get; }

    public OnnxNllbRunner(IConfiguration configuration)
    {
        NllbOnnxOptions options = configuration.GetSection(NllbOnnxOptions.SectionName).Get<NllbOnnxOptions>() ?? new NllbOnnxOptions();
        RuntimeDevice = options.Device;

        if (!options.EnableOnnxRuntime)
        {
            RuntimeDetails = "ONNX runtime disabled in configuration.";
            return;
        }

        string modelPath = Path.GetFullPath(options.ModelPath, AppContext.BaseDirectory);
        if (!File.Exists(modelPath))
        {
            RuntimeDetails = $"ONNX model not found at '{modelPath}'.";
            return;
        }

        SessionOptions sessionOptions = new();
        _session = new InferenceSession(modelPath, sessionOptions);
        RuntimeDetails = $"ONNX model loaded from '{modelPath}'.";
    }

    public IReadOnlyList<int> Generate(
        IReadOnlyList<int> inputTokenIds,
        int forcedBosTokenId,
        int maxNewTokens,
        int numBeams,
        int noRepeatNgramSize,
        float repetitionPenalty)
    {
        _ = forcedBosTokenId;
        _ = numBeams;
        _ = noRepeatNgramSize;
        _ = repetitionPenalty;

        if (inputTokenIds.Count == 0)
        {
            return [];
        }

        // Current minimal implementation keeps endpoint compatibility while ONNX seq2seq decoder loop
        // is wired progressively. Once encoder/decoder graphs are exported, this method will run the full generation loop.
        return inputTokenIds.Take(maxNewTokens).ToArray();
    }

    public void Dispose()
    {
        _session?.Dispose();
    }
}
