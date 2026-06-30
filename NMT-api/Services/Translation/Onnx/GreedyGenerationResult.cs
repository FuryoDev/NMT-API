namespace NMT_api.Services.Translation.Onnx;

public class GreedyGenerationResult
{
    public List<long> GeneratedTokenIds { get; set; } = [];
}