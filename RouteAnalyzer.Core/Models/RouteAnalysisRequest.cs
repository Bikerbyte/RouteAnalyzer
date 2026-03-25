namespace RouteAnalyzer.Models;

public sealed class RouteAnalysisRequest
{
    public required string TargetHost { get; init; }

    public int PingCount { get; init; }

    public int MaxHops { get; init; }

    public bool IncludeGeoDetails { get; init; } = true;
}
