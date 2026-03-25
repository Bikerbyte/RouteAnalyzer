namespace RouteAnalyzer.Models;

public sealed class TcpEndpointResult
{
    public required string Name { get; init; }

    public required string Host { get; init; }

    public required int Port { get; init; }

    public required bool Success { get; init; }

    public required long DurationMs { get; init; }

    public string? ErrorMessage { get; init; }
}
