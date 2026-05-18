using RouteAnalyzer.Models;

namespace RouteAnalyzer.App.Diagnostics;

public sealed class DiagnosticRunRequest
{
    public string TargetHost { get; init; } = string.Empty;

    public string? ProfileName { get; init; }

    public string? DestinationName { get; init; }

    public string Language { get; init; } = ReportLanguage.TraditionalChinese;

    public int PingCount { get; init; } = 4;

    public int MaxHops { get; init; } = 24;

    public bool IncludeGeoDetails { get; init; } = true;

    public bool IncludeDnsCheck { get; init; } = true;

    public bool IncludeHttpsCheck { get; init; } = true;

    public IReadOnlyList<TcpEndpointInput> TcpEndpoints { get; init; } = [];
}

public sealed class TcpEndpointInput
{
    public string? Name { get; init; }

    public string Host { get; init; } = string.Empty;

    public int Port { get; init; } = 443;
}

public sealed class DiagnosticRunResponse
{
    public required string ExecutionId { get; init; }

    public required string ReportUrl { get; init; }

    public required string ReportDirectory { get; init; }

    public required DiagnosticSummaryView Summary { get; init; }
}

public sealed class DiagnosticSummaryView
{
    public required string CaptureStatus { get; init; }

    public required string Overview { get; init; }

    public required string CopyText { get; init; }

    public required MetricView Latency { get; init; }

    public required MetricView PacketLoss { get; init; }

    public required MetricView Dns { get; init; }

    public required MetricView Tcp { get; init; }

    public required IReadOnlyList<string> Signals { get; init; }

    public required IReadOnlyList<RouteHopView> Hops { get; init; }

    public required NetworkContextSnapshot NetworkContext { get; init; }
}

public sealed class MetricView
{
    public required string Label { get; init; }

    public required string Value { get; init; }

    public required string Tone { get; init; }
}

public sealed class RouteHopView
{
    public required int HopNumber { get; init; }

    public required string Address { get; init; }

    public int? AverageLatencyMs { get; init; }

    public int? LatencyDeltaMs { get; init; }

    public required bool IsTimeout { get; init; }

    public required bool SuspectedSpike { get; init; }

    public required string ScopeLabel { get; init; }
}
