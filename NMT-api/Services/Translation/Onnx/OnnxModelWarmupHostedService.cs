using Microsoft.Extensions.Options;
using NMT_api.Services.Translation.Tokenization;

namespace NMT_api.Services.Translation.Onnx;

public sealed class OnnxModelWarmupHostedService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly NllbOnnxOptions _options;
    private readonly ILogger<OnnxModelWarmupHostedService> _logger;

    public OnnxModelWarmupHostedService(
        IServiceProvider serviceProvider,
        IOptions<NllbOnnxOptions> options,
        ILogger<OnnxModelWarmupHostedService> logger)
    {
        _serviceProvider = serviceProvider;
        _options = options.Value;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        IOnnxNllbRunner runner = _serviceProvider.GetRequiredService<IOnnxNllbRunner>();
        OnnxModelInfo info = runner.ModelInfo;

        if (!info.IsLoaded)
        {
            if (_options.ModelRequired)
            {
                throw new InvalidOperationException(info.Message ?? "ONNX model is required but could not be loaded.");
            }

            _logger.LogWarning(
                "ONNX model is unavailable and will be ignored because NllbOnnx:ModelRequired=false. Reason: {Reason}",
                info.Message);
            return Task.CompletedTask;
        }

        try
        {
            _ = _serviceProvider.GetRequiredService<INllbTokenizer>().EosTokenId;
        }
        catch (Exception ex)
        {
            if (_options.ModelRequired)
            {
                throw new InvalidOperationException("The ONNX tokenizer is required but could not be loaded.", ex);
            }

            _logger.LogWarning(
                ex,
                "ONNX tokenizer is unavailable and will be ignored because NllbOnnx:ModelRequired=false.");
            return Task.CompletedTask;
        }

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
