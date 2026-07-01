using Microsoft.Extensions.Options;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System.Diagnostics;

namespace NMT_api.Services.Translation.Onnx;

public class OnnxNllbRunner : IOnnxNllbRunner, IDisposable
{
    private readonly InferenceSession? _session;
    private readonly NllbOnnxOptions _options;
    private readonly string _modelPath;
    private readonly string _tokenizerPath;
    private readonly DateTimeOffset? _loadedAt;
    private readonly int _startupMs;
    private readonly OnnxModelStatus _status;
    private readonly string? _message;
    private static readonly string[] RequiredInputNames = ["input_ids", "attention_mask", "decoder_input_ids"];

    public OnnxNllbRunner(IOptions<NllbOnnxOptions> options)
    {
        _options = options.Value;
        OnnxModelArtifactValidationResult artifactValidation = OnnxModelArtifactValidator.Validate(_options);
        _modelPath = artifactValidation.ModelPath;
        _tokenizerPath = artifactValidation.TokenizerPath;

        if (!artifactValidation.IsValid)
        {
            _status = artifactValidation.Status;
            _message = artifactValidation.Message;
            return;
        }

        try
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            _session = new InferenceSession(_modelPath);
            stopwatch.Stop();

            string? compatibilityError = ValidateModelCompatibility(_session);
            if (compatibilityError is not null)
            {
                _session.Dispose();
                _session = null;
                _status = OnnxModelStatus.Failed;
                _message = compatibilityError;
                return;
            }

            _loadedAt = DateTimeOffset.UtcNow;
            _startupMs = (int)stopwatch.ElapsedMilliseconds;
            _status = OnnxModelStatus.Loaded;
            _message = "ONNX model loaded.";
        }
        catch (Exception ex)
        {
            _status = OnnxModelStatus.Failed;
            _message = $"ONNX model loading failed: {ex.Message}";
        }
    }

    public OnnxModelInfo ModelInfo => new(
        "ONNX Runtime",
        _modelPath,
        _tokenizerPath,
        _status,
        IsLoaded: _session is not null,
        IsRequired: _options.ModelRequired,
        _loadedAt,
        _startupMs,
        _options.DecodingMode,
        _options.NumBeams,
        _options.LengthPenalty,
        _options.NoRepeatNgramSize,
        _options.RepetitionPenalty,
        _session?.InputMetadata.Keys.Order().ToArray() ?? [],
        _session?.OutputMetadata.Keys.Order().ToArray() ?? [],
        _message);

    public GreedyGenerationResult Generate(GreedyGenerationRequest request)
    {
        if (_session is null)
        {
            throw new OnnxModelUnavailableException(_message ?? "ONNX model is not available.");
        }

        if (_options.DecodingMode == DecodingMode.BeamSearch)
        {
            throw new NotSupportedException("BeamSearch decoding is configured but not implemented yet. Use DecodingMode=Greedy until beam search is added to the ONNX runner.");
        }

        if (request.InputIds.Length == 0)
            throw new ArgumentException("InputIds cannot be empty.");

        if (request.AttentionMask.Length == 0)
            throw new ArgumentException("AttentionMask cannot be empty.");

        if (request.InputIds.Length != request.AttentionMask.Length)
            throw new ArgumentException("InputIds and AttentionMask must have the same length.");

        int maxNewTokens = request.MaxNewTokens ?? _options.MaxNewTokens;

        long eosTokenId = _options.EosTokenId;
        long targetLanguageTokenId = request.TargetLanguageTokenId;

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
        if (_session is null)
        {
            throw new OnnxModelUnavailableException(_message ?? "ONNX model is not available.");
        }

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

    private static string? ValidateModelCompatibility(InferenceSession session)
    {
        string[] missingInputs = RequiredInputNames
            .Where(inputName => !session.InputMetadata.ContainsKey(inputName))
            .ToArray();

        if (missingInputs.Length > 0)
        {
            return $"The ONNX model is not compatible with this runner. Missing input(s): {string.Join(", ", missingInputs)}. Expected inputs: {string.Join(", ", RequiredInputNames)}.";
        }

        if (session.OutputMetadata.Count == 0)
        {
            return "The ONNX model is not compatible with this runner. No output was found; expected logits output.";
        }

        NodeMetadata output = session.OutputMetadata.First().Value;
        if (output.ElementType != typeof(float))
        {
            return $"The ONNX model output is not compatible with this runner. Expected float logits, got {output.ElementType.Name}.";
        }

        if (output.Dimensions.Length != 3)
        {
            return $"The ONNX model output is not compatible with this runner. Expected logits tensor rank 3 [batch, sequence, vocab], got rank {output.Dimensions.Length}.";
        }

        return null;
    }

    private static long ArgMaxLastToken(Tensor<float> logits)
    {
        int batchSize = logits.Dimensions[0];
        int sequenceLength = logits.Dimensions[1];
        int vocabSize = logits.Dimensions[2];

        if (batchSize != 1)
            throw new NotSupportedException("Only batch size 1 is supported for this runner.");

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

    public void Dispose()
    {
        _session?.Dispose();
    }
}
