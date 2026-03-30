namespace NMT_api.Services.Srt;

public class SrtBlock
{
    public int Index { get; set; }
    public string TimeRange { get; set; } = string.Empty;
    public List<string> Lines { get; set; } = [];
}
