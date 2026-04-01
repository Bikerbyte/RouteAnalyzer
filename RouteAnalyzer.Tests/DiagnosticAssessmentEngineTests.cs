using RouteAnalyzer.Models;
using RouteAnalyzer.Services;

namespace RouteAnalyzer.Tests;

public class DiagnosticAssessmentEngineTests
{
    [Fact]
    public void Assess_WhenRouteIsStableButTcpFails_PointsToCompanySide()
    {
        var profile = CreateProfile();
        var route = CreateRouteReport(statusLabel: "Stable", packetLossPercent: 0, hops: CreateHealthyHops());
        var dnsResults = new[]
        {
            new DnsLookupResult
            {
                Name = "VPN DNS",
                Hostname = "vpn.example.com",
                Success = true,
                DurationMs = 20,
                Addresses = ["203.0.113.10"]
            }
        };
        var tcpResults = new[]
        {
            new TcpEndpointResult
            {
                Name = "VPN 443",
                Host = "vpn.example.com",
                Port = 443,
                Success = false,
                DurationMs = 3000,
                ErrorMessage = "Timed out after 3000 ms."
            }
        };

        var assessment = DiagnosticAssessmentEngine.Assess(profile, route, dnsResults, tcpResults);

        Assert.Equal("Action Needed", assessment.OverallStatusLabel);
        Assert.Equal("Destination edge or destination service", assessment.FaultDomain);
        Assert.Contains("more consistent", assessment.ItSummary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Assess_WhenFirstHopIsDegraded_PointsToLocalNetwork()
    {
        var profile = CreateProfile();
        var degradedHops = new[]
        {
            new RouteHop
            {
                HopNumber = 1,
                DisplayAddress = "192.168.1.1",
                Samples = ["40 ms", "42 ms", "39 ms"],
                AverageLatencyMs = 40,
                LatencyDeltaMs = null,
                IsTimeout = false,
                SuspectedSpike = true,
                ScopeLabel = "LAN / Gateway",
                ScopeDetail = "Usually the local router or first-hop gateway.",
                ReverseDns = null,
                GeoDetails = null,
                Note = "Latency increases by 39 ms compared with the device baseline."
            }
        };
        var route = CreateRouteReport(statusLabel: "Investigate", packetLossPercent: 32, hops: degradedHops);

        var assessment = DiagnosticAssessmentEngine.Assess(profile, route, [], []);

        Assert.Equal("Action Needed", assessment.OverallStatusLabel);
        Assert.Equal("Local network or Wi-Fi", assessment.FaultDomain);
    }

    [Fact]
    public void Assess_WhenOnlyTimeoutObservationAndChecksPass_RemainsHealthy()
    {
        var profile = CreateProfile();
        var route = CreateRouteReport(
            statusLabel: "Observe",
            packetLossPercent: 0,
            hops:
            [
                new RouteHop
                {
                    HopNumber = 1,
                    DisplayAddress = "192.168.1.1",
                    Samples = ["1 ms", "1 ms", "1 ms"],
                    AverageLatencyMs = 1,
                    LatencyDeltaMs = null,
                    IsTimeout = false,
                    SuspectedSpike = false,
                    ScopeLabel = "LAN / Gateway",
                    ScopeDetail = "Usually the local router or first-hop gateway.",
                    ReverseDns = null,
                    GeoDetails = null,
                    Note = "No obvious step-up is visible at this hop."
                },
                new RouteHop
                {
                    HopNumber = 2,
                    DisplayAddress = "203.0.113.1",
                    Samples = ["12 ms", "12 ms", "13 ms"],
                    AverageLatencyMs = 12,
                    LatencyDeltaMs = 11,
                    IsTimeout = false,
                    SuspectedSpike = false,
                    ScopeLabel = "Access / ISP edge",
                    ScopeDetail = "Usually near the local network boundary or ISP access edge.",
                    ReverseDns = null,
                    GeoDetails = null,
                    Note = "No obvious step-up is visible at this hop."
                },
                new RouteHop
                {
                    HopNumber = 3,
                    DisplayAddress = "203.0.113.2",
                    Samples = ["13 ms", "12 ms", "12 ms"],
                    AverageLatencyMs = 12,
                    LatencyDeltaMs = 0,
                    IsTimeout = false,
                    SuspectedSpike = false,
                    ScopeLabel = "Public hop",
                    ScopeDetail = "Transit is still responding normally before the timeout hop.",
                    ReverseDns = null,
                    GeoDetails = null,
                    Note = "No obvious step-up is visible at this hop."
                },
                new RouteHop
                {
                    HopNumber = 4,
                    DisplayAddress = "ed",
                    Samples = ["*", "*", "*"],
                    AverageLatencyMs = null,
                    LatencyDeltaMs = null,
                    IsTimeout = true,
                    SuspectedSpike = false,
                    ScopeLabel = "No reply",
                    ScopeDetail = "This hop did not reply to ICMP probes.",
                    ReverseDns = null,
                    GeoDetails = null,
                    Note = "This hop did not reply to ICMP. That alone does not prove a failure."
                },
                new RouteHop
                {
                    HopNumber = 5,
                    DisplayAddress = "203.0.113.10",
                    Samples = ["15 ms", "14 ms", "14 ms"],
                    AverageLatencyMs = 14,
                    LatencyDeltaMs = null,
                    IsTimeout = false,
                    SuspectedSpike = false,
                    ScopeLabel = "Public hop",
                    ScopeDetail = "Public network transit resumed after the timeout hop.",
                    ReverseDns = null,
                    GeoDetails = null,
                    Note = "No obvious step-up is visible at this hop."
                }
            ],
            suspectedIssue: "One or more hops timed out, but timeout-only signals are inconclusive");
        var dnsResults = new[]
        {
            new DnsLookupResult
            {
                Name = "VPN DNS",
                Hostname = "vpn.example.com",
                Success = true,
                DurationMs = 14,
                Addresses = ["203.0.113.10"]
            }
        };
        var tcpResults = new[]
        {
            new TcpEndpointResult
            {
                Name = "VPN 443",
                Host = "vpn.example.com",
                Port = 443,
                Success = true,
                DurationMs = 48
            }
        };

        var assessment = DiagnosticAssessmentEngine.Assess(profile, route, dnsResults, tcpResults);

        Assert.Equal("Healthy", assessment.OverallStatusLabel);
        Assert.Equal("No clear network fault detected", assessment.FaultDomain);
        Assert.Contains("timed out", assessment.UserSummary, StringComparison.OrdinalIgnoreCase);
    }

    private static DiagnosticProfile CreateProfile()
    {
        return new DiagnosticProfile
        {
            ProfileName = "Remote Support",
            TargetHost = "vpn.example.com",
            PingCount = 4,
            MaxHops = 24
        };
    }

    private static RouteDiagnosticReport CreateRouteReport(
        string statusLabel,
        int packetLossPercent,
        IReadOnlyList<RouteHop> hops,
        string? suspectedIssue = null)
    {
        return new RouteDiagnosticReport
        {
            TargetHost = "vpn.example.com",
            MaxHops = 24,
            GeoDetailsEnabled = true,
            ExecutionId = "route12345678",
            GeneratedAtUtc = new DateTimeOffset(2026, 3, 25, 9, 0, 0, TimeSpan.Zero),
            DurationMs = 1200,
            PingSummary = new PingSummary
            {
                Sent = 4,
                Received = Math.Max(0, 4 - (packetLossPercent / 25)),
                PacketLossPercent = packetLossPercent,
                AverageRoundTripMs = 44,
                MinimumRoundTripMs = 20,
                MaximumRoundTripMs = 75,
                JitterMs = 12
            },
            Hops = hops,
            Narrative = "Route test narrative.",
            StatusLabel = statusLabel,
            StatusSummary = "Route status summary.",
            RuntimeSummary = "Windows | .NET 10",
            DiagnosticMode = "ICMP ping + Windows tracert",
            TracerouteCommand = "tracert -d vpn.example.com",
            GeoDataProvider = "ipwho.is",
            SuspectedIssue = suspectedIssue,
            RawTracerouteLines = ["trace output"]
        };
    }

    private static IReadOnlyList<RouteHop> CreateHealthyHops()
    {
        return
        [
            new RouteHop
            {
                HopNumber = 1,
                DisplayAddress = "192.168.1.1",
                Samples = ["1 ms", "1 ms", "2 ms"],
                AverageLatencyMs = 1,
                LatencyDeltaMs = null,
                IsTimeout = false,
                SuspectedSpike = false,
                ScopeLabel = "LAN / Gateway",
                ScopeDetail = "Usually the local router or first-hop gateway.",
                ReverseDns = null,
                GeoDetails = null,
                Note = "No obvious step-up is visible at this hop."
            },
            new RouteHop
            {
                HopNumber = 2,
                DisplayAddress = "203.0.113.10",
                Samples = ["12 ms", "11 ms", "12 ms"],
                AverageLatencyMs = 12,
                LatencyDeltaMs = 11,
                IsTimeout = false,
                SuspectedSpike = false,
                ScopeLabel = "Access / ISP edge",
                ScopeDetail = "Usually near the local network boundary or ISP access edge.",
                ReverseDns = null,
                GeoDetails = null,
                Note = "No obvious step-up is visible at this hop."
            }
        ];
    }
}
