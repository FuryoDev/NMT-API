namespace NMT_api.Models.Translation;

public class HealthResponse
{
    public string Status { get; set; } = "ok";
    public string Device { get; set; } = "cpu";
    public int StartupMs { get; set; }
    public string Model { get; set; } = string.Empty;
}
