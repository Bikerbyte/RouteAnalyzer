using RouteAnalyzer.Models;

namespace RouteAnalyzer.Services;

public static class ConnectionSignalSummaryBuilder
{
    public static ConnectionSignalSummary Build(
        RouteDiagnosticReport route,
        IReadOnlyList<DnsLookupResult> dnsResults,
        IReadOnlyList<TcpEndpointResult> tcpResults)
    {
        var signals = new List<string>
        {
            $"Ping replies: {route.PingSummary.Received}/{route.PingSummary.Sent}; average latency: {route.PingSummary.AverageRoundTripMs?.ToString() ?? "-"} ms; packet loss: {route.PingSummary.PacketLossPercent}%."
        };

        if (route.Hops.Count > 0)
        {
            signals.Add($"Traceroute hops parsed: {route.Hops.Count}.");
        }
        else
        {
            signals.Add("Traceroute produced no parsable hops.");
        }

        var timeoutHops = route.Hops.Where(static hop => hop.IsTimeout).Select(static hop => hop.HopNumber).ToArray();
        if (timeoutHops.Length > 0)
        {
            signals.Add($"Traceroute timeout hops: {string.Join(", ", timeoutHops)}.");
        }

        var latencySteps = route.Hops
            .Where(static hop => hop.LatencyDeltaMs.HasValue && hop.LatencyDeltaMs.Value >= 25)
            .Select(static hop => $"hop {hop.HopNumber} +{hop.LatencyDeltaMs} ms")
            .ToArray();
        if (latencySteps.Length > 0)
        {
            signals.Add($"Latency step-ups observed: {string.Join(", ", latencySteps)}.");
        }

        if (dnsResults.Count > 0)
        {
            var passedDns = dnsResults.Count(static result => result.Success);
            signals.Add($"DNS checks passed: {passedDns}/{dnsResults.Count}.");

            var failedDns = dnsResults.Where(static result => !result.Success).Select(static result => result.Name).ToArray();
            if (failedDns.Length > 0)
            {
                signals.Add($"DNS failures: {string.Join(", ", failedDns)}.");
            }
        }

        if (tcpResults.Count > 0)
        {
            var passedTcp = tcpResults.Count(static result => result.Success);
            signals.Add($"TCP checks passed: {passedTcp}/{tcpResults.Count}.");

            var failedTcp = tcpResults.Where(static result => !result.Success).Select(static result => $"{result.Name} ({result.Host}:{result.Port})").ToArray();
            if (failedTcp.Length > 0)
            {
                signals.Add($"TCP failures: {string.Join(", ", failedTcp)}.");
            }
        }

        return new ConnectionSignalSummary
        {
            CaptureStatusLabel = "Captured",
            Overview = BuildOverview(route, dnsResults, tcpResults),
            Signals = signals
        };
    }

    private static string BuildOverview(
        RouteDiagnosticReport route,
        IReadOnlyList<DnsLookupResult> dnsResults,
        IReadOnlyList<TcpEndpointResult> tcpResults)
    {
        var dnsLabel = dnsResults.Count == 0 ? "DNS n/a" : $"DNS {dnsResults.Count(static result => result.Success)}/{dnsResults.Count}";
        var tcpLabel = tcpResults.Count == 0 ? "TCP n/a" : $"TCP {tcpResults.Count(static result => result.Success)}/{tcpResults.Count}";

        return $"Captured ping, traceroute, {dnsLabel}, {tcpLabel}. Average latency: {route.PingSummary.AverageRoundTripMs?.ToString() ?? "-"} ms; packet loss: {route.PingSummary.PacketLossPercent}%; hops parsed: {route.Hops.Count}.";
    }
}
