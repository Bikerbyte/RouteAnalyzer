using System.Net;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using RouteAnalyzer.Models;

namespace RouteAnalyzer.Services;

public static class SupportDiagnosticExportFormatter
{
    // Summary sections stay intentionally short so the first screen is still usable.
    private const int MaxSummaryEvidenceItems = 4;
    private const int MaxSummaryRecommendationItems = 3;
    private const int MaxSummarySignalItems = 4;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static string ToJson(SupportDiagnosticReport report)
    {
        return JsonSerializer.Serialize(report, JsonOptions);
    }

    public static string ToText(SupportDiagnosticReport report)
    {
        // summary.txt is meant to be a quick handoff artifact, not the full raw dump.
        var language = ReportLanguage.Normalize(report.Profile.PreferredLanguage);
        var assessment = SupportReportLocalizer.GetAssessmentView(report, language);
        var route = SupportReportLocalizer.GetRouteView(report.PrimaryRoute, language);
        var summaryEvidence = TakeSummaryItems(assessment.EvidenceHighlights, MaxSummaryEvidenceItems);
        var summaryRecommendations = TakeSummaryItems(assessment.Recommendations, MaxSummaryRecommendationItems);
        var summarySignals = TakeSummaryItems(BuildHighlightedSignals(report, language), MaxSummarySignalItems);
        var builder = new StringBuilder();
        builder.AppendLine(SupportReportLocalizer.Text("ReportTitle", language));
        builder.AppendLine("============================");
        builder.AppendLine($"{SupportReportLocalizer.Text("ExecutionId", language),-12} : {report.ExecutionId}");
        builder.AppendLine($"{SupportReportLocalizer.Text("Generated", language),-12} : {report.GeneratedAtUtc:yyyy-MM-dd HH:mm:ss 'UTC'}");
        builder.AppendLine($"{SupportReportLocalizer.Text("Machine", language),-12} : {report.MachineName}");
        builder.AppendLine($"{SupportReportLocalizer.Text("ConnectionType", language),-12} : {report.NetworkContext.ConnectionType}");
        builder.AppendLine($"{SupportReportLocalizer.Text("ActiveAdapter", language),-12} : {report.NetworkContext.ActiveAdapterName}");
        builder.AppendLine($"{SupportReportLocalizer.Text("DefaultGateway", language),-12} : {report.NetworkContext.DefaultGateway}");
        builder.AppendLine($"{SupportReportLocalizer.Text("DnsServers", language),-12} : {string.Join(", ", report.NetworkContext.DnsServers)}");
        builder.AppendLine($"Profile      : {report.Profile.ProfileName}");
        builder.AppendLine($"{SupportReportLocalizer.Text("Company", language),-12} : {report.Profile.CompanyName ?? "-"}");
        builder.AppendLine($"{SupportReportLocalizer.Text("Target", language),-12} : {report.Profile.TargetHost}");
        builder.AppendLine($"{SupportReportLocalizer.Text("Status", language),-12} : {assessment.OverallStatusLabel}");
        builder.AppendLine($"{SupportReportLocalizer.Text("PossibleFaultDomain", language),-12} : {assessment.FaultDomain}");
        builder.AppendLine($"{SupportReportLocalizer.Text("Duration", language),-12} : {report.DurationMs} ms");
        builder.AppendLine();
        builder.AppendLine(SupportReportLocalizer.Text("OverallFinding", language));
        builder.AppendLine("------------");
        builder.AppendLine(assessment.UserSummary);
        builder.AppendLine();
        builder.AppendLine(SupportReportLocalizer.Text("SuspiciousSignals", language));
        builder.AppendLine("-------------------");
        if (summarySignals.Count == 0)
        {
            builder.AppendLine($"- {SupportReportLocalizer.Text("NoSuspiciousSignals", language)}");
        }
        else
        {
            foreach (var line in summarySignals)
            {
                builder.AppendLine($"- {line}");
            }
        }

        builder.AppendLine();
        builder.AppendLine(SupportReportLocalizer.Text("Observations", language));
        builder.AppendLine("--------");
        foreach (var line in summaryEvidence)
        {
            builder.AppendLine($"- {line}");
        }

        builder.AppendLine();
        builder.AppendLine(SupportReportLocalizer.Text("Interpretation", language));
        builder.AppendLine("--------------");
        builder.AppendLine(assessment.ItSummary);
        builder.AppendLine();
        builder.AppendLine(SupportReportLocalizer.Text("NextSteps", language));
        builder.AppendLine("---------------");
        foreach (var line in summaryRecommendations)
        {
            builder.AppendLine($"- {line}");
        }

        builder.AppendLine();
        builder.AppendLine(SupportReportLocalizer.Text("RouteSummary", language));
        builder.AppendLine("-------------");
        builder.AppendLine($"{SupportReportLocalizer.Text("Status", language),-12} : {route.StatusLabel}");
        builder.AppendLine($"{SupportReportLocalizer.Text("Summary", language),-12} : {route.StatusSummary}");
        builder.AppendLine($"{SupportReportLocalizer.Text("Narrative", language),-12} : {route.Narrative}");
        builder.AppendLine($"{SupportReportLocalizer.Text("AverageLatency", language),-12} : {report.PrimaryRoute.PingSummary.AverageRoundTripMs?.ToString() ?? "-"} ms");
        builder.AppendLine($"{SupportReportLocalizer.Text("Loss", language),-12} : {report.PrimaryRoute.PingSummary.PacketLossPercent}%");
        builder.AppendLine($"{SupportReportLocalizer.Text("Jitter", language),-12} : {report.PrimaryRoute.PingSummary.JitterMs?.ToString() ?? "-"} ms");

        if (report.DnsResults.Count > 0 || report.TcpResults.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine(SupportReportLocalizer.Text("ChecksOverview", language));
            builder.AppendLine("----------------");
            builder.AppendLine(BuildChecksOverviewLine(report, language));
        }

        builder.AppendLine();
        builder.AppendLine(SupportReportLocalizer.Text("DetailHint", language));

        return builder.ToString();
    }

    public static string ToHtml(SupportDiagnosticReport report)
    {
        return SupportDiagnosticHtmlFormatter.Render(report);
    }

    public static ReportArtifactBundle WriteBundle(SupportDiagnosticReport report, string directoryPath)
    {
        // Keep the bundle predictable so support can zip or forward it without extra cleanup.
        var fullDirectoryPath = Path.GetFullPath(directoryPath);
        Directory.CreateDirectory(fullDirectoryPath);

        var summaryPath = Path.Combine(fullDirectoryPath, "summary.txt");
        var jsonPath = Path.Combine(fullDirectoryPath, "report.json");
        var htmlPath = Path.Combine(fullDirectoryPath, "report.html");
        var routeCsvPath = Path.Combine(fullDirectoryPath, "route-hops.csv");

        File.WriteAllText(summaryPath, ToText(report), Encoding.UTF8);
        File.WriteAllText(jsonPath, ToJson(report), Encoding.UTF8);
        File.WriteAllText(htmlPath, ToHtml(report), Encoding.UTF8);
        File.WriteAllText(routeCsvPath, RouteDiagnosticExportFormatter.ToCsv(report.PrimaryRoute), Encoding.UTF8);

        return new ReportArtifactBundle
        {
            DirectoryPath = fullDirectoryPath,
            SummaryPath = summaryPath,
            JsonPath = jsonPath,
            HtmlPath = htmlPath,
            RouteCsvPath = routeCsvPath
        };
    }

    public static string BuildDefaultDirectoryName(SupportDiagnosticReport report)
    {
        var safeTarget = SanitizeFileName(report.Profile.TargetHost);
        return $"report-{report.GeneratedAtUtc:yyyyMMdd-HHmmss}-{safeTarget}";
    }

    private static string MetricCard(string englishLabel, string chineseLabel, string value)
    {
        return $"<div class=\"card\"><span class=\"label\">{Bilingual(englishLabel, chineseLabel)}</span><strong class=\"big\">{Encode(value)}</strong></div>";
    }

    private static IReadOnlyList<string> TakeSummaryItems(IReadOnlyList<string> items, int maxCount)
    {
        return items
            .Where(static item => !string.IsNullOrWhiteSpace(item))
            .Take(maxCount)
            .ToArray();
    }

    private static IReadOnlyList<string> BuildHighlightedSignals(SupportDiagnosticReport report, string language)
    {
        var signals = new List<string>();
        var route = report.PrimaryRoute;
        var firstSpike = route.Hops.FirstOrDefault(static hop => hop.SuspectedSpike);
        var timeoutHops = route.Hops.Where(static hop => hop.IsTimeout).Select(static hop => hop.HopNumber).ToArray();
        var failedDns = report.DnsResults.Where(static result => !result.Success).ToArray();
        var failedTcp = report.TcpResults.Where(static result => !result.Success).ToArray();
        var zh = ReportLanguage.IsTraditionalChinese(language);

        if (route.PingSummary.PacketLossPercent > 0)
        {
            signals.Add(zh
                ? $"封包遺失為 {route.PingSummary.PacketLossPercent}%。"
                : $"Packet loss was observed at {route.PingSummary.PacketLossPercent}%.");
        }

        if (firstSpike is not null)
        {
            var scopeLabel = zh
                ? SupportReportLocalizer.GetHopScopeLabel(firstSpike, ReportLanguage.TraditionalChinese)
                : firstSpike.ScopeLabel;

            signals.Add(zh
                ? $"延遲階梯從第 {firstSpike.HopNumber} 跳開始，位置接近 {scopeLabel}。"
                : $"Latency begins stepping up around hop {firstSpike.HopNumber} near {scopeLabel}.");
        }

        if (timeoutHops.Length > 0)
        {
            var hopList = string.Join(", ", timeoutHops);
            signals.Add(zh
                ? $"這次 traceroute 在 hop {hopList} 出現 timeout。"
                : $"Traceroute timeouts were observed at hop {hopList}.");
        }

        if (failedDns.Length > 0)
        {
            var failedNames = string.Join(", ", failedDns.Select(static result => result.Name));
            signals.Add(zh
                ? $"DNS 檢查失敗項目: {failedNames}。"
                : $"DNS check failures were observed for: {failedNames}.");
        }

        if (failedTcp.Length > 0)
        {
            var failedEndpoints = string.Join(", ", failedTcp.Select(static result => $"{result.Name} ({result.Host}:{result.Port})"));
            signals.Add(zh
                ? $"TCP 端點失敗: {failedEndpoints}。"
                : $"TCP endpoint failures were observed for: {failedEndpoints}.");
        }

        return signals;
    }

    private static string BuildChecksOverviewLine(SupportDiagnosticReport report, string language)
    {
        var dnsSummary = BuildDnsSummary(report);
        var tcpSummary = BuildTcpSummary(report);

        return ReportLanguage.IsTraditionalChinese(language)
            ? $"DNS：{dnsSummary} | TCP：{tcpSummary}"
            : $"DNS: {dnsSummary} | TCP: {tcpSummary}";
    }

    private static string BuildDnsSummary(SupportDiagnosticReport report)
    {
        if (report.DnsResults.Count == 0)
        {
            return "n/a";
        }

        var passed = report.DnsResults.Count(static result => result.Success);
        return $"{passed}/{report.DnsResults.Count}";
    }

    private static string BuildTcpSummary(SupportDiagnosticReport report)
    {
        if (report.TcpResults.Count == 0)
        {
            return "n/a";
        }

        var passed = report.TcpResults.Count(static result => result.Success);
        return $"{passed}/{report.TcpResults.Count}";
    }

    private static bool IsRouteDetailWorthOpening(SupportDiagnosticReport report)
    {
        // Auto-expand route detail only when it is likely to explain the current result.
        return !string.Equals(report.Assessment.OverallStatusLabel, "Healthy", StringComparison.OrdinalIgnoreCase)
            || report.PrimaryRoute.PingSummary.PacketLossPercent > 0
            || report.PrimaryRoute.Hops.Any(static hop => hop.SuspectedSpike || hop.IsTimeout);
    }

    private static string Bilingual(string english, string chinese)
    {
        return $"{LocalizedSpan(ReportLanguage.English, english)}{LocalizedSpan(ReportLanguage.TraditionalChinese, chinese)}";
    }

    private static string BilingualLabelValue(string englishLabel, string englishValue, string chineseLabel, string chineseValue, bool codeValue = false)
    {
        var englishContent = codeValue ? $"<code>{Encode(englishValue)}</code>" : Encode(englishValue);
        var chineseContent = codeValue ? $"<code>{Encode(chineseValue)}</code>" : Encode(chineseValue);

        return
            $"{LocalizedSpanRaw(ReportLanguage.English, $"<strong>{Encode(englishLabel)}:</strong> {englishContent}")}" +
            $"{LocalizedSpanRaw(ReportLanguage.TraditionalChinese, $"<strong>{Encode(chineseLabel)}:</strong> {chineseContent}")}";
    }

    private static string LocalizedSpan(string language, string value)
    {
        return $"<span data-lang=\"{Encode(language)}\">{Encode(value)}</span>";
    }

    private static string LocalizedSpanRaw(string language, string value)
    {
        return $"<span data-lang=\"{Encode(language)}\">{value}</span>";
    }

    private static string Encode(string value)
    {
        return WebUtility.HtmlEncode(value);
    }

    private static string GetStatusClass(string statusLabel)
    {
        return statusLabel switch
        {
            "Healthy" => "status-healthy",
            "Action Needed" => "status-action-needed",
            _ => "status-warning"
        };
    }

    private static string SanitizeFileName(string value)
    {
        return string.Join("-", value.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries)).Trim('-');
    }
}
