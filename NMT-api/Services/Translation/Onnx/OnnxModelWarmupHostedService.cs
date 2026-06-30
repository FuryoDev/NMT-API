using NMT_api.Services.Translation.Tokenization;

namespace NMT_api.Services.Translation.Onnx;

public sealed class OnnxModelWarmupHostedService : IHostedService
{
    private readonly IOnnxNllbRunner _runner;
    private readonly INllbTokenizer _tokenizer;
    private readonly ILogger<OnnxModelWarmupHostedService> _logger;

    public OnnxModelWarmupHostedService(
        IOnnxNllbRunner runner,
        INllbTokenizer tokenizer,
        ILogger<OnnxModelWarmupHostedService> logger)
    {
        _runner = runner;
        _tokenizer = tokenizer;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        OnnxModelInfo info = _runner.ModelInfo;
        _ = _tokenizer.EosTokenId;

        _logger.LogInformation(
            "ONNX model loaded at startup from {ModelPath} in {StartupMs} ms. Inputs: {Inputs}. Outputs: {Outputs}.",
            info.ModelPath,
            info.StartupMs,
            string.Join(", ", info.InputNames),
            string.Join(", ", info.OutputNames));

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
