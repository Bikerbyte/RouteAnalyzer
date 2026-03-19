namespace RouteAnalyzer.Models;

public sealed class RouteHop
{
    public required int HopNumber { get; init; }

    public required string DisplayAddress { get; init; }

    public required IReadOnlyList<string> Samples { get; init; }

    public int? AverageLatencyMs { get; init; }

    public bool IsTimeout { get; init; }

    public bool SuspectedSpike { get; init; }

    public required string Note { get; init; }
}
