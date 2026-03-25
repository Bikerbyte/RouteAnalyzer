using RouteAnalyzer.Models;
using RouteAnalyzer.Services;

namespace RouteAnalyzer.Tests;

public class RouteDiagnosticExportFormatterTests
{
    [Fact]
    public void ToText_IncludesStatusSummaryAndIssue()
    {
        var report = CreateReport();

        var output = RouteDiagnosticExportFormatter.ToText(report);

        Assert.Contains("Status    : Investigate", output);
        Assert.Contains("Summary   : Latency rises after the ISP edge.", output);
        Assert.Contains("Suspected issue: Elevated latency begins near hop 2", output);
    }

    [Fact]
    public void ToCsv_IncludesHopValues()
    {
        var report = CreateReport();

        var output = RouteDiagnosticExportFormatter.ToCsv(report);

        Assert.Contains("Hop,Address,Scope", output);
        Assert.Contains("\"203.0.113.10\"", output);
        Assert.Contains("\"Transit hop\"", output);
    }

    [Fact]
    public void BuildReportFileName_SanitizesInvalidCharacters()
    {
        var fileName = RouteDiagnosticExportFormatter.BuildReportFileName("https://vpn.example.com:443", "json");

        Assert.Equal("route-report-https-vpn.example.com-443.json", fileName);
    }

    private static RouteDiagnosticReport CreateReport()
    {
        return new RouteDiagnosticReport
        {
            TargetHost = "vpn.example.com",
            MaxHops = 16,
            GeoDetailsEnabled = true,
            ExecutionId = "abc123def456",
            GeneratedAtUtc = new DateTimeOffset(2026, 3, 25, 8, 0, 0, TimeSpan.Zero),
            DurationMs = 1500,
            PingSummary = new PingSummary
            {
                Sent = 4,
                Received = 3,
                PacketLossPercent = 25,
                AverageRoundTripMs = 88,
                MinimumRoundTripMs = 70,
                MaximumRoundTripMs = 110,
                JitterMs = 16
            },
            Hops =
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
                    Samples = ["70 ms", "85 ms", "90 ms"],
                    AverageLatencyMs = 82,
                    LatencyDeltaMs = 81,
                    IsTimeout = false,
                    SuspectedSpike = true,
                    ScopeLabel = "Transit hop",
                    ScopeDetail = "Intermediate public network node, often upstream or backbone transit.",
                    ReverseDns = "isp-edge.example.net",
                    GeoDetails = new IpGeoDetails
                    {
                        Country = "Taiwan",
                        Region = "Taipei",
                        City = "Taipei",
                        Isp = "Example ISP",
                        Asn = "64500",
                        Latitude = 25.03,
                        Longitude = 121.56,
                        Timezone = "Asia/Taipei"
                    },
                    Note = "Latency increases by 81 ms compared with the previous hop."
                }
            ],
            Narrative = "Average ping is 88 ms and the primary signal is: Elevated latency begins near hop 2.",
            StatusLabel = "Investigate",
            StatusSummary = "Latency rises after the ISP edge.",
            RuntimeSummary = "Windows | .NET 10.0.0",
            DiagnosticMode = "ICMP ping + Windows tracert",
            TracerouteCommand = "tracert -d -w 900 -h 16 vpn.example.com",
            GeoDataProvider = "ipwho.is",
            SuspectedIssue = "Elevated latency begins near hop 2",
            RawTracerouteLines = ["  1    <1 ms    <1 ms    <1 ms  192.168.1.1"]
        };
    }
}
