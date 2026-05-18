namespace RouteAnalyzer.Models;

public sealed class ConnectionSignalSummary
{
    public required string CaptureStatusLabel { get; init; }

    public required string Overview { get; init; }

    public required IReadOnlyList<string> Signals { get; init; }
}
