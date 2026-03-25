namespace RouteAnalyzer.Models;

public sealed class DiagnosticProfile
{
    public string ProfileName { get; init; } = "Remote Support";

    public string? CompanyName { get; init; }

    public string? Description { get; init; }

    public string PreferredLanguage { get; init; } = ReportLanguage.English;

    public required string TargetHost { get; init; }

    public int PingCount { get; init; } = 4;

    public int MaxHops { get; init; } = 24;

    public bool IncludeGeoDetails { get; init; } = true;

    public IReadOnlyList<DnsLookupDefinition> DnsLookups { get; init; } = [];

    public IReadOnlyList<TcpEndpointDefinition> TcpEndpoints { get; init; } = [];
}
