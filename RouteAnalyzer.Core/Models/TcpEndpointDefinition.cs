namespace RouteAnalyzer.Models;

public sealed class TcpEndpointDefinition
{
    public string Name { get; init; } = "TCP endpoint";

    public required string Host { get; init; }

    public required int Port { get; init; }

    public int? TimeoutMs { get; init; }
}
