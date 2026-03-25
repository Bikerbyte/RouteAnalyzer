namespace RouteAnalyzer.Models;

public sealed class DnsLookupDefinition
{
    public string Name { get; init; } = "DNS lookup";

    public required string Hostname { get; init; }
}
