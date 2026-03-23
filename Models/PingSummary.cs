namespace RouteAnalyzer.Models;

public sealed class PingSummary
{
    public required int Sent { get; init; }

    public required int Received { get; init; }

    public required int PacketLossPercent { get; init; }

    public int? AverageRoundTripMs { get; init; }

    public int? MinimumRoundTripMs { get; init; }

    public int? MaximumRoundTripMs { get; init; }

    public int? JitterMs { get; init; }

    public int SuccessRatePercent => Sent <= 0
        ? 0
        : (int)Math.Round((double)Received / Sent * 100);
}
