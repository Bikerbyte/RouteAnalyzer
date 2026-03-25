using RouteAnalyzer.Models;
using RouteAnalyzer.Services;

namespace RouteAnalyzer.Tests;

public class SupportDiagnosticExportFormatterTests
{
    [Fact]
    public void ToHtml_ContainsUserAndItSections()
    {
        var html = SupportDiagnosticExportFormatter.ToHtml(CreateReport());

        Assert.Contains("User Summary", html);
        Assert.Contains("IT Summary", html);
        Assert.Contains("DNS Checks", html);
        Assert.Contains("TCP Checks", html);
    }

    [Fact]
    public void WriteBundle_WritesAllExpectedArtifacts()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        try
        {
            var bundle = SupportDiagnosticExportFormatter.WriteBundle(CreateReport(), tempDirectory);

            Assert.True(File.Exists(bundle.SummaryPath));
            Assert.True(File.Exists(bundle.JsonPath));
            Assert.True(File.Exists(bundle.HtmlPath));
            Assert.True(File.Exists(bundle.RouteCsvPath));
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }

    private static SupportDiagnosticReport CreateReport()
    {
        var route = new RouteDiagnosticReport
        {
            TargetHost = "vpn.example.com",
            MaxHops = 16,
            GeoDetailsEnabled = true,
            ExecutionId = "route12345678",
            GeneratedAtUtc = new DateTimeOffset(2026, 3, 25, 10, 0, 0, TimeSpan.Zero),
            DurationMs = 1400,
            PingSummary = new PingSummary
            {
                Sent = 4,
                Received = 4,
                PacketLossPercent = 0,
                AverageRoundTripMs = 48,
                MinimumRoundTripMs = 40,
                MaximumRoundTripMs = 58,
                JitterMs = 6
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
                }
            ],
            Narrative = "The path does not show a strong network fault.",
            StatusLabel = "Stable",
            StatusSummary = "The current path looks consistent.",
            RuntimeSummary = "Windows | .NET 10",
            DiagnosticMode = "ICMP ping + Windows tracert",
            TracerouteCommand = "tracert -d vpn.example.com",
            GeoDataProvider = "ipwho.is",
            SuspectedIssue = null,
            RawTracerouteLines = ["trace output"]
        };

        return new SupportDiagnosticReport
        {
            ExecutionId = "support123456",
            GeneratedAtUtc = new DateTimeOffset(2026, 3, 25, 10, 0, 0, TimeSpan.Zero),
            DurationMs = 2200,
            MachineName = "CLIENT-01",
            RuntimeSummary = "Windows | .NET 10",
            Profile = new DiagnosticProfile
            {
                ProfileName = "Remote Support - VPN",
                CompanyName = "Contoso",
                TargetHost = "vpn.example.com",
                PingCount = 4,
                MaxHops = 24,
                DnsLookups =
                [
                    new DnsLookupDefinition
                    {
                        Name = "VPN DNS",
                        Hostname = "vpn.example.com"
                    }
                ],
                TcpEndpoints =
                [
                    new TcpEndpointDefinition
                    {
                        Name = "VPN 443",
                        Host = "vpn.example.com",
                        Port = 443
                    }
                ]
            },
            Assessment = new DiagnosticAssessment
            {
                OverallStatusLabel = "Healthy",
                FaultDomain = "No clear network fault detected",
                ConfidenceLabel = "Medium",
                UserSummary = "The current network path looks healthy.",
                ItSummary = "No strong network-side issue stands out in this run.",
                EvidenceHighlights =
                [
                    "Ping success rate: 100% with average latency 48 ms."
                ],
                Recommendations =
                [
                    "Collect another run if the slowdown returns."
                ]
            },
            PrimaryRoute = route,
            DnsResults =
            [
                new DnsLookupResult
                {
                    Name = "VPN DNS",
                    Hostname = "vpn.example.com",
                    Success = true,
                    DurationMs = 18,
                    Addresses = ["203.0.113.10"]
                }
            ],
            TcpResults =
            [
                new TcpEndpointResult
                {
                    Name = "VPN 443",
                    Host = "vpn.example.com",
                    Port = 443,
                    Success = true,
                    DurationMs = 47
                }
            ]
        };
    }
}
