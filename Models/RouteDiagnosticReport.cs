namespace RouteAnalyzer.Models;

public sealed class RouteDiagnosticReport
{
    public required string TargetHost { get; init; }

    public required string ExecutionId { get; init; }

    public required DateTimeOffset GeneratedAtUtc { get; init; }

    public required long DurationMs { get; init; }

    public required PingSummary PingSummary { get; init; }

    public required IReadOnlyList<RouteHop> Hops { get; init; }

    public required string Narrative { get; init; }

    public required string StatusLabel { get; init; }

    public required string StatusSummary { get; init; }

    public required string RuntimeSummary { get; init; }

    public required string DiagnosticMode { get; init; }

    public required string GeoDataProvider { get; init; }

    public string? SuspectedIssue { get; init; }

    public required IReadOnlyList<string> RawTracerouteLines { get; init; }
}
