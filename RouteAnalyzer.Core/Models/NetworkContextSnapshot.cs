namespace RouteAnalyzer.Models;

public sealed class NetworkContextSnapshot
{
    public required string ConnectionType { get; init; }

    public required string ActiveAdapterName { get; init; }

    public required string DefaultGateway { get; init; }

    public required IReadOnlyList<string> DnsServers { get; init; }
}
