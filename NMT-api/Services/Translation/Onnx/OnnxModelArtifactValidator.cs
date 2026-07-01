namespace NMT_api.Services.Translation.Onnx;

public static class OnnxModelArtifactValidator
{
    public static OnnxModelArtifactValidationResult Validate(NllbOnnxOptions options)
    {
        string modelPath = ResolvePath(options.ModelPath);
        string tokenizerPath = ResolvePath(options.TokenizerPath);

        if (!File.Exists(modelPath))
        {
            return new OnnxModelArtifactValidationResult(
                modelPath,
                tokenizerPath,
                OnnxModelStatus.Missing,
                $"The ONNX model file was not found: {modelPath}");
        }

        if (!File.Exists(tokenizerPath))
        {
            return new OnnxModelArtifactValidationResult(
                modelPath,
                tokenizerPath,
                OnnxModelStatus.Missing,
                $"The tokenizer file was not found: {tokenizerPath}");
        }

        return new OnnxModelArtifactValidationResult(
            modelPath,
            tokenizerPath,
            OnnxModelStatus.Loaded,
            "ONNX model and tokenizer artifacts were found.");
    }

    public static string ResolvePath(string configuredPath)
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

        string baseDirectoryCandidate = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, configuredPath));
        return File.Exists(baseDirectoryCandidate)
            ? baseDirectoryCandidate
            : contentRootCandidate;
    }
}
