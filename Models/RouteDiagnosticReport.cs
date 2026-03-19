namespace RouteAnalyzer.Models;

public sealed class RouteDiagnosticReport
{
    public required string TargetHost { get; init; }

    public required PingSummary PingSummary { get; init; }

    public required IReadOnlyList<RouteHop> Hops { get; init; }

    public required string Narrative { get; init; }

    public string? SuspectedIssue { get; init; }

    public required IReadOnlyList<string> RawTracerouteLines { get; init; }
}
