namespace RouteAnalyzer.Models;

public sealed class ReportArtifactBundle
{
    public required string DirectoryPath { get; init; }

    public required string SummaryPath { get; init; }

    public required string JsonPath { get; init; }

    public required string HtmlPath { get; init; }

    public required string RouteCsvPath { get; init; }
}
