namespace RouteAnalyzer.Models;

public sealed class DiagnosticAssessment
{
    public required string ScenarioKey { get; init; }

    public required string OverallStatusLabel { get; init; }

    public required string FaultDomain { get; init; }

    public required string UserSummary { get; init; }

    public required string ItSummary { get; init; }

    public required IReadOnlyList<string> EvidenceHighlights { get; init; }

    public required IReadOnlyList<string> Recommendations { get; init; }
}
