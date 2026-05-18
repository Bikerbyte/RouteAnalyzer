using Microsoft.Extensions.Options;
using RouteAnalyzer.Models;
using RouteAnalyzer.Options;
using RouteAnalyzer.Services;

namespace RouteAnalyzer.App.Diagnostics;

public sealed class AppDiagnosticRunner
{
    private readonly SupportDiagnosticService _diagnosticService;
    private readonly IWebHostEnvironment _environment;
    private readonly RouteAnalyzerOptions _options;

    public AppDiagnosticRunner(
        SupportDiagnosticService diagnosticService,
        IWebHostEnvironment environment,
        IOptions<RouteAnalyzerOptions> options)
    {
        _diagnosticService = diagnosticService;
        _environment = environment;
        _options = options.Value;
    }

    public async Task<DiagnosticRunResponse> RunAsync(DiagnosticRunRequest request, CancellationToken cancellationToken)
    {
        var profile = BuildProfile(request);
        var report = await _diagnosticService.RunAsync(profile, cancellationToken);
        var reportRoot = GetReportRoot(_environment);
        var directoryName = SupportDiagnosticExportFormatter.BuildDefaultDirectoryName(report);
        var bundle = SupportDiagnosticExportFormatter.WriteBundle(report, Path.Combine(reportRoot, directoryName));
        var language = ReportLanguage.Normalize(profile.PreferredLanguage);

        return new DiagnosticRunResponse
        {
            ExecutionId = report.ExecutionId,
            ReportUrl = $"/reports/app/{Uri.EscapeDataString(directoryName)}/report.html",
            ReportDirectory = bundle.DirectoryPath,
            Summary = BuildSummary(report, language)
        };
    }

    public static string GetReportRoot(IWebHostEnvironment environment)
    {
        return Path.GetFullPath(Path.Combine(environment.ContentRootPath, "..", "reports", "app"));
    }

    private DiagnosticProfile BuildProfile(DiagnosticRunRequest request)
    {
        if (!TargetHostParser.TryNormalize(request.TargetHost, out var normalizedTarget))
        {
            throw new DiagnosticProfileException("請輸入有效的網址、主機名稱或 IP。");
        }

        var dnsLookups = request.IncludeDnsCheck
            ? new[]
            {
                new DnsLookupDefinition
                {
                    Name = "Primary DNS",
                    Hostname = normalizedTarget
                }
            }
            : [];

        var tcpEndpoints = BuildTcpEndpoints(request, normalizedTarget);

        var profile = new DiagnosticProfile
        {
            ProfileName = string.IsNullOrWhiteSpace(request.ProfileName) ? "Interactive Route Check" : request.ProfileName.Trim(),
            DestinationName = string.IsNullOrWhiteSpace(request.DestinationName) ? normalizedTarget : request.DestinationName.Trim(),
            Description = "Local interactive scan from Route Analyzer App.",
            PreferredLanguage = ReportLanguage.Normalize(request.Language),
            TargetHost = normalizedTarget,
            PingCount = Math.Clamp(request.PingCount, RouteAnalyzerOptions.MinPingCount, RouteAnalyzerOptions.MaxPingCount),
            MaxHops = Math.Clamp(request.MaxHops, RouteAnalyzerOptions.MinMaxHops, RouteAnalyzerOptions.MaxMaxHops),
            IncludeGeoDetails = request.IncludeGeoDetails,
            DnsLookups = dnsLookups,
            TcpEndpoints = tcpEndpoints
        };

        return DiagnosticProfileLoader.Normalize(profile);
    }

    private IReadOnlyList<TcpEndpointDefinition> BuildTcpEndpoints(DiagnosticRunRequest request, string normalizedTarget)
    {
        var endpoints = new List<TcpEndpointDefinition>();

        if (request.IncludeHttpsCheck)
        {
            endpoints.Add(new TcpEndpointDefinition
            {
                Name = "HTTPS",
                Host = normalizedTarget,
                Port = 443
            });
        }

        foreach (var endpoint in request.TcpEndpoints)
        {
            if (!TargetHostParser.TryNormalize(endpoint.Host, out var normalizedHost))
            {
                continue;
            }

            if (endpoint.Port is <= 0 or > 65535)
            {
                continue;
            }

            endpoints.Add(new TcpEndpointDefinition
            {
                Name = string.IsNullOrWhiteSpace(endpoint.Name) ? $"{normalizedHost}:{endpoint.Port}" : endpoint.Name.Trim(),
                Host = normalizedHost,
                Port = endpoint.Port
            });
        }

        return endpoints
            .GroupBy(endpoint => $"{endpoint.Host}:{endpoint.Port}", StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .ToArray();
    }

    private static DiagnosticSummaryView BuildSummary(SupportDiagnosticReport report, string language)
    {
        var latency = report.PrimaryRoute.PingSummary.AverageRoundTripMs;
        var packetLoss = report.PrimaryRoute.PingSummary.PacketLossPercent;
        var dnsPassed = report.DnsResults.Count(static result => result.Success);
        var tcpPassed = report.TcpResults.Count(static result => result.Success);

        return new DiagnosticSummaryView
        {
            CaptureStatus = ReportLanguage.IsTraditionalChinese(language) ? "已完成檢測" : report.SignalSummary.CaptureStatusLabel,
            Overview = SupportDiagnosticExportFormatter.LocalizeOverview(report.SignalSummary.Overview, language),
            CopyText = BuildCopyText(report, language),
            Latency = new MetricView
            {
                Label = ReportLanguage.IsTraditionalChinese(language) ? "平均延遲" : "Average latency",
                Value = latency.HasValue ? $"{latency.Value} ms" : "-",
                Tone = latency is null ? "muted" : latency <= 80 ? "good" : latency <= 180 ? "warn" : "bad"
            },
            PacketLoss = new MetricView
            {
                Label = ReportLanguage.IsTraditionalChinese(language) ? "封包遺失" : "Packet loss",
                Value = $"{packetLoss}%",
                Tone = packetLoss == 0 ? "good" : packetLoss < 15 ? "warn" : "bad"
            },
            Dns = new MetricView
            {
                Label = "DNS",
                Value = report.DnsResults.Count == 0 ? "n/a" : $"{dnsPassed}/{report.DnsResults.Count}",
                Tone = report.DnsResults.Count == 0 ? "muted" : dnsPassed == report.DnsResults.Count ? "good" : "bad"
            },
            Tcp = new MetricView
            {
                Label = "TCP",
                Value = report.TcpResults.Count == 0 ? "n/a" : $"{tcpPassed}/{report.TcpResults.Count}",
                Tone = report.TcpResults.Count == 0 ? "muted" : tcpPassed == report.TcpResults.Count ? "good" : "bad"
            },
            Signals = report.SignalSummary.Signals
                .Select(signal => SupportDiagnosticExportFormatter.LocalizeSignal(signal, language))
                .Take(8)
                .ToArray(),
            Hops = report.PrimaryRoute.Hops.Select(static hop => new RouteHopView
            {
                HopNumber = hop.HopNumber,
                Address = hop.DisplayAddress,
                AverageLatencyMs = hop.AverageLatencyMs,
                LatencyDeltaMs = hop.LatencyDeltaMs,
                IsTimeout = hop.IsTimeout,
                SuspectedSpike = hop.SuspectedSpike,
                ScopeLabel = hop.ScopeLabel
            }).ToArray(),
            NetworkContext = report.NetworkContext
        };
    }

    private static string BuildCopyText(
        SupportDiagnosticReport report,
        string language)
    {
        var zh = ReportLanguage.IsTraditionalChinese(language);
        var builder = new System.Text.StringBuilder();
        builder.AppendLine(zh ? $"檢測編號: {report.ExecutionId}" : $"Capture: {report.ExecutionId}");
        builder.AppendLine(zh ? $"目標: {report.Profile.TargetHost}" : $"Target: {report.Profile.TargetHost}");
        builder.AppendLine(zh
            ? $"摘要: {SupportDiagnosticExportFormatter.LocalizeOverview(report.SignalSummary.Overview, language)}"
            : $"Overview: {report.SignalSummary.Overview}");

        foreach (var signal in report.SignalSummary.Signals.Take(8))
        {
            builder.AppendLine($"- {SupportDiagnosticExportFormatter.LocalizeSignal(signal, language)}");
        }

        return builder.ToString().TrimEnd();
    }
}
