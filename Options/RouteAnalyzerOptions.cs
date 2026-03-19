namespace RouteAnalyzer.Options;

public sealed class RouteAnalyzerOptions
{
    public const string SectionName = "RouteAnalyzer";

    public string DefaultTarget { get; init; } = "1.1.1.1";

    public int DefaultPingCount { get; init; } = 4;
}
