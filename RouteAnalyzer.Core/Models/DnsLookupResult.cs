namespace RouteAnalyzer.Models;

public sealed class DnsLookupResult
{
    public required string Name { get; init; }

    public required string Hostname { get; init; }

    public required bool Success { get; init; }

    public required long DurationMs { get; init; }

    public required IReadOnlyList<string> Addresses { get; init; }

    public string? ErrorMessage { get; init; }
}
