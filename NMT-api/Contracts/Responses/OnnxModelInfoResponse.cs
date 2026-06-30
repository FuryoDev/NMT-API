namespace NMT_api.Contracts.Responses;

public sealed class OnnxModelInfoResponse
{
    public string Provider { get; set; } = string.Empty;
    public string ModelPath { get; set; } = string.Empty;
    public string TokenizerPath { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool IsLoaded { get; set; }
    public bool IsRequired { get; set; }
    public DateTimeOffset? LoadedAt { get; set; }
    public int StartupMs { get; set; }
    public IReadOnlyCollection<string> InputNames { get; set; } = [];
    public IReadOnlyCollection<string> OutputNames { get; set; } = [];
    public string? Message { get; set; }
}
