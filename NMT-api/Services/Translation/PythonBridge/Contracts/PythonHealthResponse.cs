namespace NMT_api.Services.Translation.PythonBridge.Contracts;

public sealed class PythonHealthResponse
{
    public string Status { get; init; } = string.Empty;
    public string Device { get; init; } = string.Empty;
    public int StartupMs { get; init; }
    public string Model { get; init; } = string.Empty;
}
