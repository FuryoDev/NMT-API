namespace NMT_api.Services.Translation.Onnx;

public interface IOnnxNllbRunner
{
    OnnxModelInfo ModelInfo { get; }
    GreedyGenerationResult Generate(GreedyGenerationRequest request);
}
