namespace NMT_api.Services.Translation.Onnx;

public sealed record OnnxModelArtifactValidationResult(
    string ModelPath,
    string TokenizerPath,
    OnnxModelStatus Status,
    string? Message)
{
    public bool IsValid => Status == OnnxModelStatus.Loaded;
}
