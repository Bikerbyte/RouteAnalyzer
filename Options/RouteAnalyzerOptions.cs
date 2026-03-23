using System.ComponentModel.DataAnnotations;

namespace RouteAnalyzer.Options;

public sealed class RouteAnalyzerOptions
{
    public const string SectionName = "RouteAnalyzer";
    public const int MinPingCount = 3;
    public const int MaxPingCount = 10;

    [Required]
    public string DefaultTarget { get; init; } = "1.1.1.1";

    [Range(MinPingCount, MaxPingCount)]
    public int DefaultPingCount { get; init; } = 4;
}
