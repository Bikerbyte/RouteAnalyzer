using RouteAnalyzer.Models;
using RouteAnalyzer.Services;

namespace RouteAnalyzer.Tests;

public class SupportDiagnosticExportFormatterTests
{
    [Fact]
    public void ToHtml_ContainsMinimalTriageSections()
    {
        var html = SupportDiagnosticExportFormatter.ToHtml(CreateReport());

        Assert.Contains("Captured signals", html);
        Assert.Contains("Route Summary", html);
        Assert.Contains("Copy capture summary", html);
    }

    [Fact]
    public void ToHtml_IncludesLanguageToggleAndTraditionalChineseCopy()
    {
        var html = SupportDiagnosticExportFormatter.ToHtml(CreateReport(preferredLanguage: ReportLanguage.TraditionalChinese));

        Assert.Contains("class=\"lang-zh\"", html);
        Assert.Contains("\u6AA2\u6E2C\u8A0A\u865F", html);
        Assert.Contains("\u8907\u88FD\u6AA2\u6E2C\u6458\u8981", html);
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

    [Fact]
    public void WriteBundle_PreservesTraditionalChineseTracerouteOutput()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        try
        {
            var chineseLine = "\u900F\u904E\u6700\u591A 24 \u500B\u8E8D\u9EDE\u8FFD\u8E64\u5230 172.17.70.36 \u7684\u8DEF\u7531";
            var report = CreateReport(rawTracerouteLines: [chineseLine]);
            var bundle = SupportDiagnosticExportFormatter.WriteBundle(report, tempDirectory);
            var html = File.ReadAllText(bundle.HtmlPath);

            Assert.Contains(chineseLine, html);
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }

    private static SupportDiagnosticReport CreateReport(
        string? preferredLanguage = null,
        IReadOnlyList<string>? rawTracerouteLines = null)
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
            RuntimeSummary = "Windows | .NET 10",
            DiagnosticMode = "ICMP ping + Windows tracert",
            TracerouteCommand = "tracert -d vpn.example.com",
            GeoDataProvider = "ipwho.is",
            RawTracerouteLines = rawTracerouteLines ?? ["trace output"]
        };

        return new SupportDiagnosticReport
        {
            ExecutionId = "support123456",
            GeneratedAtUtc = new DateTimeOffset(2026, 3, 25, 10, 0, 0, TimeSpan.Zero),
            DurationMs = 2200,
            MachineName = "CLIENT-01",
            RuntimeSummary = "Windows | .NET 10",
            NetworkContext = new NetworkContextSnapshot
            {
                ConnectionType = "Wi-Fi",
                ActiveAdapterName = "Intel Wi-Fi 6",
                DefaultGateway = "192.168.1.1",
                DnsServers = ["192.168.1.1", "1.1.1.1"]
            },
            Profile = new DiagnosticProfile
            {
                ProfileName = "Remote Support - VPN",
                DestinationName = "Contoso",
                PreferredLanguage = preferredLanguage ?? ReportLanguage.English,
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
            SignalSummary = new ConnectionSignalSummary
            {
                CaptureStatusLabel = "Captured",
                Overview = "Captured ping, traceroute, DNS 1/1, and TCP 1/1. Average latency: 48 ms; packet loss: 0%; hops parsed: 1.",
                Signals =
                [
                    "Ping replies: 4/4; average latency: 48 ms; packet loss: 0%.",
                    "Traceroute hops parsed: 1.",
                    "DNS checks passed: 1/1.",
                    "TCP checks passed: 1/1."
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
