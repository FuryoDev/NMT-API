namespace NMT_api.Services.Translation.Srt;

public sealed record SrtBlock(int Index, string TimeRange, IReadOnlyList<string> Lines);
