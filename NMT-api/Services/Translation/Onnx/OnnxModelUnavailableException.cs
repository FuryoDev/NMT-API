namespace NMT_api.Services.Translation.Onnx;

public sealed class OnnxModelUnavailableException : InvalidOperationException
{
    public OnnxModelUnavailableException(string message)
        : base(message)
    {
    }
}
