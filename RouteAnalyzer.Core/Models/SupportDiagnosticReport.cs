namespace RouteAnalyzer.Models;

public sealed class SupportDiagnosticReport
{
    public required string ExecutionId { get; init; }

    public required DateTimeOffset GeneratedAtUtc { get; init; }

    public required long DurationMs { get; init; }

    public required string MachineName { get; init; }

    public required string RuntimeSummary { get; init; }

    public required NetworkContextSnapshot NetworkContext { get; init; }

    public required DiagnosticProfile Profile { get; init; }

    public required DiagnosticAssessment Assessment { get; init; }

    public required RouteDiagnosticReport PrimaryRoute { get; init; }

    public required IReadOnlyList<DnsLookupResult> DnsResults { get; init; }

    public required IReadOnlyList<TcpEndpointResult> TcpResults { get; init; }
}
