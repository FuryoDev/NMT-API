namespace NMT_api.Services.Translation.Onnx;

public class GreedyGenerationRequest
{
    public long[] InputIds { get; set; } = [];
    public long[] AttentionMask { get; set; } = [];
    public long? TargetLanguageTokenId { get; set; }
    public int? MaxNewTokens { get; set; }
}
