using Microsoft.Extensions.Options;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System.Diagnostics;

namespace NMT_api.Services.Translation.Onnx;

public class OnnxNllbRunner : IOnnxNllbRunner, IDisposable
{
    private readonly InferenceSession _session;
    private readonly NllbOnnxOptions _options;
    private readonly string _modelPath;
    private readonly DateTimeOffset _loadedAt;
    private readonly int _startupMs;

    public OnnxNllbRunner(IOptions<NllbOnnxOptions> options)
    {
        _options = options.Value;
        _modelPath = ResolvePath(_options.ModelPath);

        if (!File.Exists(_modelPath))
        {
            throw new FileNotFoundException($"The model file {_modelPath} does not exist.");
        }

        Stopwatch stopwatch = Stopwatch.StartNew();
        _session = new InferenceSession(_modelPath);
        stopwatch.Stop();

        _loadedAt = DateTimeOffset.UtcNow;
        _startupMs = (int)stopwatch.ElapsedMilliseconds;
    }

    public OnnxModelInfo ModelInfo => new(
        "ONNX Runtime",
        _modelPath,
        IsLoaded: true,
        _loadedAt,
        _startupMs,
        _session.InputMetadata.Keys.Order().ToArray(),
        _session.OutputMetadata.Keys.Order().ToArray());

    public GreedyGenerationResult Generate(GreedyGenerationRequest request)
    {
        if (request.InputIds.Length == 0)
            throw new ArgumentException("InputIds cannot be empty.");

        if (request.AttentionMask.Length == 0)
            throw new ArgumentException("AttentionMask cannot be empty.");

        if (request.InputIds.Length != request.AttentionMask.Length)
            throw new ArgumentException("InputIds and AttentionMask must have the same length.");

        int maxNewTokens = request.MaxNewTokens ?? _options.MaxNewTokens;

        long eosTokenId = _options.EosTokenId;
        long targetLanguageTokenId = request.TargetLanguageTokenId ?? _options.TargetLanguageTokenId;

        List<long> decoderTokens = [eosTokenId];

        for (int step = 0; step < maxNewTokens; step++)
        {
            using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> outputs =
                RunModel(request.InputIds, request.AttentionMask, decoderTokens.ToArray());

            Tensor<float> logits = outputs.First().AsTensor<float>();

            long nextTokenId = step == 0
                ? targetLanguageTokenId
                : ArgMaxLastToken(logits);

            decoderTokens.Add(nextTokenId);

            if (nextTokenId == eosTokenId)
            {
                break;
            }
        }

        return new GreedyGenerationResult
        {
            GeneratedTokenIds = decoderTokens
        };
    }

    private IDisposableReadOnlyCollection<DisposableNamedOnnxValue> RunModel(
        long[] inputIds,
        long[] attentionMask,
        long[] decoderInputIds)
    {
        DenseTensor<long> inputIdsTensor = new(inputIds, [1, inputIds.Length]);
        DenseTensor<long> attentionMaskTensor = new(attentionMask, [1, attentionMask.Length]);
        DenseTensor<long> decoderInputIdsTensor = new(decoderInputIds, [1, decoderInputIds.Length]);

        List<NamedOnnxValue> inputs =
        [
            NamedOnnxValue.CreateFromTensor("input_ids", inputIdsTensor),
            NamedOnnxValue.CreateFromTensor("attention_mask", attentionMaskTensor),
            NamedOnnxValue.CreateFromTensor("decoder_input_ids", decoderInputIdsTensor)
        ];

        return _session.Run(inputs);
    }

    private static long ArgMaxLastToken(Tensor<float> logits)
    {
        int batchSize = logits.Dimensions[0];
        int sequenceLength = logits.Dimensions[1];
        int vocabSize = logits.Dimensions[2];

        if (batchSize != 1)
            throw new NotSupportedException("Only batch size 1 is supported for this test runner.");

        int lastTokenIndex = sequenceLength - 1;

        float bestValue = float.NegativeInfinity;
        int bestIndex = 0;

        for (int vocabIndex = 0; vocabIndex < vocabSize; vocabIndex++)
        {
            float value = logits[0, lastTokenIndex, vocabIndex];

            if (value > bestValue)
            {
                bestValue = value;
                bestIndex = vocabIndex;
            }
        }

        return bestIndex;
    }

    private static string ResolvePath(string configuredPath)
    {
        if (Path.IsPathRooted(configuredPath))
        {
            return configuredPath;
        }

        string contentRootCandidate = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), configuredPath));
        if (File.Exists(contentRootCandidate))
        {
            return contentRootCandidate;
        }

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, configuredPath));
    }

    public void Dispose()
    {
        _session.Dispose();
    }
}
