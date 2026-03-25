using System.ComponentModel.DataAnnotations;

namespace RouteAnalyzer.Options;

public sealed class RouteAnalyzerOptions
{
    public const string SectionName = "RouteAnalyzer";
    public const int MinPingCount = 3;
    public const int MaxPingCount = 10;
    public const int MinMaxHops = 4;
    public const int MaxMaxHops = 64;
    public const int MinPingTimeoutMs = 400;
    public const int MaxPingTimeoutMs = 5_000;
    public const int MinTracerouteProbeTimeoutMs = 300;
    public const int MaxTracerouteProbeTimeoutMs = 5_000;
    public const int MinProcessTimeoutSeconds = 5;
    public const int MaxProcessTimeoutSeconds = 120;

    [Required]
    public string DefaultTarget { get; init; } = "1.1.1.1";

    [Range(MinPingCount, MaxPingCount)]
    public int DefaultPingCount { get; init; } = 4;

    [Range(MinMaxHops, MaxMaxHops)]
    public int DefaultMaxHops { get; init; } = 24;

    public bool DefaultIncludeGeoDetails { get; init; } = true;

    [Range(MinPingTimeoutMs, MaxPingTimeoutMs)]
    public int PingTimeoutMs { get; init; } = 1_200;

    [Range(MinTracerouteProbeTimeoutMs, MaxTracerouteProbeTimeoutMs)]
    public int TracerouteProbeTimeoutMs { get; init; } = 900;

    [Range(MinProcessTimeoutSeconds, MaxProcessTimeoutSeconds)]
    public int TracerouteProcessTimeoutSeconds { get; init; } = 40;
}
