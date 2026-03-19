namespace RouteAnalyzer.Models;

public sealed class PingSummary
{
    public required int Sent { get; init; }

    public required int Received { get; init; }

    public required int PacketLossPercent { get; init; }

    public int? AverageRoundTripMs { get; init; }

    public int? MinimumRoundTripMs { get; init; }

    public int? MaximumRoundTripMs { get; init; }
}
