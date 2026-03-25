namespace RouteAnalyzer.Api;

public sealed class RouteAnalyzeApiRequest
{
    public string TargetHost { get; init; } = string.Empty;

    public int? PingCount { get; init; }

    public int? MaxHops { get; init; }

    public bool? IncludeGeoDetails { get; init; }
}

public sealed class RouteAnalyzerApiInfoResponse
{
    public required string Name { get; init; }

    public required string Version { get; init; }

    public required string Runtime { get; init; }

    public required string OperatingSystem { get; init; }

    public required string EnvironmentName { get; init; }

    public required string[] Endpoints { get; init; }

    public required bool SupportsTraceroute { get; init; }
}
