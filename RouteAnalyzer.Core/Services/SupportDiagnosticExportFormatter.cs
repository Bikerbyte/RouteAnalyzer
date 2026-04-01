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
        var builder = new StringBuilder();
        builder.AppendLine(SupportReportLocalizer.Text("ReportTitle", language));
        builder.AppendLine("============================");
        builder.AppendLine($"{SupportReportLocalizer.Text("ExecutionId", language),-12} : {report.ExecutionId}");
        builder.AppendLine($"{SupportReportLocalizer.Text("Generated", language),-12} : {report.GeneratedAtUtc:yyyy-MM-dd HH:mm:ss 'UTC'}");
        builder.AppendLine($"{SupportReportLocalizer.Text("Machine", language),-12} : {report.MachineName}");
        builder.AppendLine($"Profile      : {report.Profile.ProfileName}");
        builder.AppendLine($"{SupportReportLocalizer.Text("Company", language),-12} : {report.Profile.CompanyName ?? "-"}");
        builder.AppendLine($"{SupportReportLocalizer.Text("Target", language),-12} : {report.Profile.TargetHost}");
        builder.AppendLine($"{SupportReportLocalizer.Text("Status", language),-12} : {assessment.OverallStatusLabel}");
        builder.AppendLine($"{SupportReportLocalizer.Text("FaultDomain", language),-12} : {assessment.FaultDomain}");
        builder.AppendLine($"{SupportReportLocalizer.Text("Confidence", language),-12} : {assessment.ConfidenceLabel}");
        builder.AppendLine($"{SupportReportLocalizer.Text("Duration", language),-12} : {report.DurationMs} ms");
        builder.AppendLine();
        builder.AppendLine(SupportReportLocalizer.Text("UserSummary", language));
        builder.AppendLine("------------");
        builder.AppendLine(assessment.UserSummary);
        builder.AppendLine();
        builder.AppendLine(SupportReportLocalizer.Text("ItSummary", language));
        builder.AppendLine("----------");
        builder.AppendLine(assessment.ItSummary);
        builder.AppendLine();
        builder.AppendLine(SupportReportLocalizer.Text("Evidence", language));
        builder.AppendLine("--------");
        foreach (var line in summaryEvidence)
        {
            builder.AppendLine($"- {line}");
        }

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
        // HTML is the main artifact for humans, so keep the summary visible
        // and move raw detail into collapsible sections.
        var builder = new StringBuilder();
        var defaultLanguage = ReportLanguage.Normalize(report.Profile.PreferredLanguage);
        var htmlLanguageClass = ReportLanguage.IsTraditionalChinese(defaultLanguage) ? "lang-zh" : "lang-en";
        var assessmentEn = SupportReportLocalizer.GetAssessmentView(report, ReportLanguage.English);
        var assessmentZh = SupportReportLocalizer.GetAssessmentView(report, ReportLanguage.TraditionalChinese);
        var routeEn = SupportReportLocalizer.GetRouteView(report.PrimaryRoute, ReportLanguage.English);
        var routeZh = SupportReportLocalizer.GetRouteView(report.PrimaryRoute, ReportLanguage.TraditionalChinese);
        var summaryRecommendationsEn = TakeSummaryItems(assessmentEn.Recommendations, MaxSummaryRecommendationItems);
        var summaryRecommendationsZh = TakeSummaryItems(assessmentZh.Recommendations, MaxSummaryRecommendationItems);
        var summaryEvidenceEn = TakeSummaryItems(assessmentEn.EvidenceHighlights, MaxSummaryEvidenceItems);
        var summaryEvidenceZh = TakeSummaryItems(assessmentZh.EvidenceHighlights, MaxSummaryEvidenceItems);
        var statusClass = GetStatusClass(report.Assessment.OverallStatusLabel);
        var dnsPassed = report.DnsResults.Count(static result => result.Success);
        var tcpPassed = report.TcpResults.Count(static result => result.Success);
        var pingAverage = report.PrimaryRoute.PingSummary.AverageRoundTripMs?.ToString() ?? "-";
        var jitter = report.PrimaryRoute.PingSummary.JitterMs?.ToString() ?? "-";
        var generatedDisplay = report.GeneratedAtUtc.ToString("yyyy-MM-dd HH:mm:ss 'UTC'");
        var dnsMetric = report.DnsResults.Count == 0 ? "n/a" : $"{dnsPassed}/{report.DnsResults.Count} pass";
        var tcpMetric = report.TcpResults.Count == 0 ? "n/a" : $"{tcpPassed}/{report.TcpResults.Count} pass";

        builder.AppendLine("<!DOCTYPE html>");
        builder.AppendLine($"<html lang=\"en\" class=\"{htmlLanguageClass}\" data-report-language=\"{Encode(defaultLanguage)}\">");
        builder.AppendLine("<head>");
        builder.AppendLine("  <meta charset=\"utf-8\">");
        builder.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        builder.AppendLine($"  <title>{Encode(report.Profile.ProfileName)} - Route Analyzer Report</title>");
        builder.AppendLine("  <style>");
        builder.AppendLine("    :root { color-scheme: light; --bg: #f4efe6; --panel: #fffdf8; --ink: #1c1b18; --muted: #6a655c; --line: #d7cfbf; --healthy: #2b7a4b; --warning: #b7791f; --action: #b23a2b; --accent: #0d5c63; font-family: 'Segoe UI', Tahoma, sans-serif; }");
        builder.AppendLine("    body { margin: 0; background: radial-gradient(circle at top, #fff7ec 0%, var(--bg) 48%, #ece6db 100%); color: var(--ink); }");
        builder.AppendLine("    .page { max-width: 1100px; margin: 0 auto; padding: 32px 20px 60px; }");
        builder.AppendLine("    .hero { background: rgba(43, 122, 75, 0.08); border-radius: 24px; padding: 28px; box-shadow: 0 18px 42px rgba(28, 27, 24, .16); }");
        builder.AppendLine("    .eyebrow { text-transform: uppercase; letter-spacing: .16em; font-size: 12px; opacity: .8; }");
        builder.AppendLine("    h1, h2, h3 { margin: 0; }");
        builder.AppendLine("    .hero-grid, .meta-grid, .summary-grid, .two-col { display: grid; gap: 16px; }");
        builder.AppendLine("    .hero-grid { grid-template-columns: 2fr 1fr; margin-top: 18px; }");
        builder.AppendLine("    .summary-grid { grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); margin-top: 18px; }");
        builder.AppendLine("    .two-col { grid-template-columns: repeat(auto-fit, minmax(280px, 1fr)); margin-top: 24px; }");
        builder.AppendLine("    .panel { background: var(--panel); border: 1px solid rgba(215,207,191,.85); border-radius: 20px; padding: 20px; box-shadow: 0 10px 30px rgba(64,52,36,.08); margin-top: 20px; }");
        builder.AppendLine("    details.panel { padding: 0; overflow: hidden; }");
        builder.AppendLine("    details.panel summary { list-style: none; cursor: pointer; padding: 18px 20px; font-weight: 700; display: flex; justify-content: space-between; align-items: center; gap: 16px; }");
        builder.AppendLine("    details.panel summary::-webkit-details-marker { display: none; }");
        builder.AppendLine("    details.panel summary::after { content: '+'; color: var(--accent); font-size: 24px; line-height: 1; }");
        builder.AppendLine("    details.panel[open] summary::after { content: '−'; }");
        builder.AppendLine("    .detail-body { padding: 0 20px 20px; }");
        builder.AppendLine("    .summary-note { color: var(--muted); font-weight: 500; font-size: 14px; }");
        builder.AppendLine("    .status-pill { display: inline-block; padding: 8px 14px; border-radius: 999px; font-weight: 700; font-size: 14px; }");
        builder.AppendLine("    .status-healthy { background: rgba(43,122,75,.14); color: var(--healthy); }");
        builder.AppendLine("    .status-warning { background: rgba(183,121,31,.14); color: var(--warning); }");
        builder.AppendLine("    .status-action-needed { background: rgba(178,58,43,.14); color: var(--action); }");
        builder.AppendLine("    .card { background: rgba(255,255,255,.85); border: 1px solid var(--line); border-radius: 18px; padding: 16px; }");
        builder.AppendLine("    .label { display: block; font-size: 12px; text-transform: uppercase; letter-spacing: .12em; color: var(--muted); margin-bottom: 8px; }");
        builder.AppendLine("    strong.big { font-size: 28px; line-height: 1.1; }");
        builder.AppendLine("    p { margin: 0; line-height: 1.6; }");
        builder.AppendLine("    ul { margin: 12px 0 0; padding-left: 18px; }");
        builder.AppendLine("    li { margin-top: 6px; }");
        builder.AppendLine("    table { width: 100%; border-collapse: collapse; margin-top: 14px; font-size: 14px; }");
        builder.AppendLine("    th, td { padding: 10px 12px; border-bottom: 1px solid rgba(215,207,191,.9); text-align: left; vertical-align: top; }");
        builder.AppendLine("    th { font-size: 12px; text-transform: uppercase; letter-spacing: .1em; color: var(--muted); }");
        builder.AppendLine("    .muted { color: var(--muted); }");
        builder.AppendLine("    code { font-family: Consolas, 'Courier New', monospace; font-size: 12px; }");
        builder.AppendLine("    pre { white-space: pre-wrap; word-break: break-word; background: #1e1f23; color: #f6f3ed; border-radius: 16px; padding: 16px; overflow: auto; }");
        builder.AppendLine("    .top-row { display: flex; justify-content: space-between; align-items: flex-start; gap: 16px; flex-wrap: wrap; }");
        builder.AppendLine("    .lang-switch { display: inline-flex; align-items: center; gap: 8px; background: rgba(255,255,255,.76); padding: 8px; border-radius: 999px; border: 1px solid var(--line); }");
        builder.AppendLine("    .lang-btn { border: 0; border-radius: 999px; padding: 8px 12px; cursor: pointer; background: transparent; color: var(--muted); font-weight: 700; }");
        builder.AppendLine("    .lang-btn[aria-pressed='true'] { background: #fff; color: var(--ink); box-shadow: 0 2px 8px rgba(28, 27, 24, .08); }");
        builder.AppendLine("    [data-lang='en'], [data-lang='zh-TW'] { display: inline; }");
        builder.AppendLine("    html.lang-en [data-lang='zh-TW'] { display: none !important; }");
        builder.AppendLine("    html.lang-zh [data-lang='en'] { display: none !important; }");
        builder.AppendLine("    @media (max-width: 720px) { .hero-grid { grid-template-columns: 1fr; } .page { padding: 18px 14px 36px; } .hero { padding: 22px; } }");
        builder.AppendLine("  </style>");
        builder.AppendLine("</head>");
        builder.AppendLine("<body>");
        builder.AppendLine("  <div class=\"page\">");
        builder.AppendLine("    <section class=\"hero\">");
        builder.AppendLine("      <div class=\"top-row\">");
        builder.AppendLine("        <div>");
        builder.AppendLine($"          <span class=\"eyebrow\">{Bilingual(SupportReportLocalizer.Text("ReportTitle", ReportLanguage.English), SupportReportLocalizer.Text("ReportTitle", ReportLanguage.TraditionalChinese))}</span>");
        builder.AppendLine($"          <h1>{Encode(report.Profile.ProfileName)}</h1>");
        builder.AppendLine("        </div>");
        builder.AppendLine("        <div class=\"lang-switch\" role=\"group\" aria-label=\"Language switch\">");
        builder.AppendLine($"          <span class=\"muted\">{Bilingual(SupportReportLocalizer.Text("Language", ReportLanguage.English), SupportReportLocalizer.Text("Language", ReportLanguage.TraditionalChinese))}</span>");
        builder.AppendLine($"          <button type=\"button\" class=\"lang-btn\" data-switch-language=\"en\">{Encode(SupportReportLocalizer.Text("English", ReportLanguage.English))}</button>");
        builder.AppendLine($"          <button type=\"button\" class=\"lang-btn\" data-switch-language=\"zh-TW\">{Encode(SupportReportLocalizer.Text("TraditionalChinese", ReportLanguage.TraditionalChinese))}</button>");
        builder.AppendLine("        </div>");
        builder.AppendLine("      </div>");
        builder.AppendLine("      <div class=\"hero-grid\">");
        builder.AppendLine("        <div>");
        builder.AppendLine($"          <span class=\"status-pill {statusClass}\">{Bilingual(assessmentEn.OverallStatusLabel, assessmentZh.OverallStatusLabel)}</span>");
        builder.AppendLine($"          <p style=\"margin-top:14px;font-size:18px;\">{Bilingual(assessmentEn.UserSummary, assessmentZh.UserSummary)}</p>");
        builder.AppendLine($"          <p class=\"muted\" style=\"margin-top:12px;\">{Bilingual($"Fault domain: {assessmentEn.FaultDomain} | Confidence: {assessmentEn.ConfidenceLabel}", $"故障域: {assessmentZh.FaultDomain} | 信心: {assessmentZh.ConfidenceLabel}")}</p>");
        builder.AppendLine("        </div>");
        builder.AppendLine("        <div class=\"card\">");
        builder.AppendLine($"          <span class=\"label\">{Bilingual(SupportReportLocalizer.Text("RunDetails", ReportLanguage.English), SupportReportLocalizer.Text("RunDetails", ReportLanguage.TraditionalChinese))}</span>");
        builder.AppendLine($"          <p>{BilingualLabelValue(SupportReportLocalizer.Text("Company", ReportLanguage.English), report.Profile.CompanyName ?? "-", SupportReportLocalizer.Text("Company", ReportLanguage.TraditionalChinese), report.Profile.CompanyName ?? "-")}</p>");
        builder.AppendLine($"          <p>{BilingualLabelValue(SupportReportLocalizer.Text("Machine", ReportLanguage.English), report.MachineName, SupportReportLocalizer.Text("Machine", ReportLanguage.TraditionalChinese), report.MachineName)}</p>");
        builder.AppendLine($"          <p>{BilingualLabelValue(SupportReportLocalizer.Text("Target", ReportLanguage.English), report.Profile.TargetHost, SupportReportLocalizer.Text("Target", ReportLanguage.TraditionalChinese), report.Profile.TargetHost)}</p>");
        builder.AppendLine($"          <p>{BilingualLabelValue(SupportReportLocalizer.Text("Generated", ReportLanguage.English), generatedDisplay, SupportReportLocalizer.Text("Generated", ReportLanguage.TraditionalChinese), generatedDisplay)}</p>");
        builder.AppendLine($"          <p>{BilingualLabelValue(SupportReportLocalizer.Text("ExecutionId", ReportLanguage.English), report.ExecutionId, SupportReportLocalizer.Text("ExecutionId", ReportLanguage.TraditionalChinese), report.ExecutionId, codeValue: true)}</p>");
        builder.AppendLine("        </div>");
        builder.AppendLine("      </div>");
        builder.AppendLine("      <div class=\"summary-grid\">");
        builder.AppendLine("        " + MetricCard(
            SupportReportLocalizer.Text("AverageLatency", ReportLanguage.English),
            SupportReportLocalizer.Text("AverageLatency", ReportLanguage.TraditionalChinese),
            pingAverage + " ms"));
        builder.AppendLine("        " + MetricCard(
            SupportReportLocalizer.Text("PacketLoss", ReportLanguage.English),
            SupportReportLocalizer.Text("PacketLoss", ReportLanguage.TraditionalChinese),
            report.PrimaryRoute.PingSummary.PacketLossPercent + "%"));
        builder.AppendLine("        " + MetricCard(
            SupportReportLocalizer.Text("Jitter", ReportLanguage.English),
            SupportReportLocalizer.Text("Jitter", ReportLanguage.TraditionalChinese),
            jitter + " ms"));
        builder.AppendLine("        " + MetricCard(
            SupportReportLocalizer.Text("DnsChecks", ReportLanguage.English),
            SupportReportLocalizer.Text("DnsChecks", ReportLanguage.TraditionalChinese),
            dnsMetric));
        builder.AppendLine("        " + MetricCard(
            SupportReportLocalizer.Text("TcpChecks", ReportLanguage.English),
            SupportReportLocalizer.Text("TcpChecks", ReportLanguage.TraditionalChinese),
            tcpMetric));
        builder.AppendLine("        " + MetricCard(
            SupportReportLocalizer.Text("Duration", ReportLanguage.English),
            SupportReportLocalizer.Text("Duration", ReportLanguage.TraditionalChinese),
            report.DurationMs + " ms"));
        builder.AppendLine("      </div>");
        builder.AppendLine("    </section>");
        builder.AppendLine("    <section class=\"two-col\">");
        builder.AppendLine("      <article class=\"panel\">");
        builder.AppendLine($"        <h2>{Bilingual(SupportReportLocalizer.Text("UserSummary", ReportLanguage.English), SupportReportLocalizer.Text("UserSummary", ReportLanguage.TraditionalChinese))}</h2>");
        builder.AppendLine($"        <p style=\"margin-top:12px;\">{Bilingual(assessmentEn.UserSummary, assessmentZh.UserSummary)}</p>");
        builder.AppendLine("        <ul>");
        for (var index = 0; index < Math.Max(summaryRecommendationsEn.Count, summaryRecommendationsZh.Count); index++)
        {
            var englishRecommendation = index < summaryRecommendationsEn.Count ? summaryRecommendationsEn[index] : string.Empty;
            var chineseRecommendation = index < summaryRecommendationsZh.Count ? summaryRecommendationsZh[index] : string.Empty;
            builder.AppendLine($"          <li>{Bilingual(englishRecommendation, chineseRecommendation)}</li>");
        }
        builder.AppendLine("        </ul>");
        builder.AppendLine("      </article>");
        builder.AppendLine("      <article class=\"panel\">");
        builder.AppendLine($"        <h2>{Bilingual(SupportReportLocalizer.Text("ItSummary", ReportLanguage.English), SupportReportLocalizer.Text("ItSummary", ReportLanguage.TraditionalChinese))}</h2>");
        builder.AppendLine($"        <p style=\"margin-top:12px;\">{Bilingual(assessmentEn.ItSummary, assessmentZh.ItSummary)}</p>");
        builder.AppendLine("        <ul>");
        for (var index = 0; index < Math.Max(summaryEvidenceEn.Count, summaryEvidenceZh.Count); index++)
        {
            var englishEvidence = index < summaryEvidenceEn.Count ? summaryEvidenceEn[index] : string.Empty;
            var chineseEvidence = index < summaryEvidenceZh.Count ? summaryEvidenceZh[index] : string.Empty;
            builder.AppendLine($"          <li>{Bilingual(englishEvidence, chineseEvidence)}</li>");
        }
        builder.AppendLine("        </ul>");
        builder.AppendLine("      </article>");
        builder.AppendLine("    </section>");
        // DNS / TCP / route / raw output are still available,
        // but they should not dominate the first screen.
        builder.AppendLine("    <details class=\"panel\">");
        builder.AppendLine($"      <summary><span>{Bilingual(SupportReportLocalizer.Text("DnsChecks", ReportLanguage.English), SupportReportLocalizer.Text("DnsChecks", ReportLanguage.TraditionalChinese))}</span><span class=\"summary-note\">{Encode(BuildDnsSummary(report))}</span></summary>");
        builder.AppendLine("      <div class=\"detail-body\">");
        builder.AppendLine($"      <table><thead><tr><th>{Bilingual(SupportReportLocalizer.Text("Name", ReportLanguage.English), SupportReportLocalizer.Text("Name", ReportLanguage.TraditionalChinese))}</th><th>{Bilingual(SupportReportLocalizer.Text("Hostname", ReportLanguage.English), SupportReportLocalizer.Text("Hostname", ReportLanguage.TraditionalChinese))}</th><th>{Bilingual(SupportReportLocalizer.Text("Status", ReportLanguage.English), SupportReportLocalizer.Text("Status", ReportLanguage.TraditionalChinese))}</th><th>{Bilingual(SupportReportLocalizer.Text("Duration", ReportLanguage.English), SupportReportLocalizer.Text("Duration", ReportLanguage.TraditionalChinese))}</th><th>{Bilingual(SupportReportLocalizer.Text("Detail", ReportLanguage.English), SupportReportLocalizer.Text("Detail", ReportLanguage.TraditionalChinese))}</th></tr></thead><tbody>");
        if (report.DnsResults.Count == 0)
        {
            builder.AppendLine($"        <tr><td colspan=\"5\">{Bilingual(SupportReportLocalizer.Text("NoDnsChecks", ReportLanguage.English), SupportReportLocalizer.Text("NoDnsChecks", ReportLanguage.TraditionalChinese))}</td></tr>");
        }
        else
        {
            foreach (var result in report.DnsResults)
            {
                var detail = result.Success ? string.Join(", ", result.Addresses) : result.ErrorMessage ?? "Lookup failed.";
                var englishStatus = result.Success ? SupportReportLocalizer.Text("Pass", ReportLanguage.English) : SupportReportLocalizer.Text("Fail", ReportLanguage.English);
                var chineseStatus = result.Success ? SupportReportLocalizer.Text("Pass", ReportLanguage.TraditionalChinese) : SupportReportLocalizer.Text("Fail", ReportLanguage.TraditionalChinese);
                builder.AppendLine($"        <tr><td>{Encode(result.Name)}</td><td>{Encode(result.Hostname)}</td><td>{Bilingual(englishStatus, chineseStatus)}</td><td>{result.DurationMs} ms</td><td>{Encode(detail)}</td></tr>");
            }
        }
        builder.AppendLine("      </tbody></table>");
        builder.AppendLine("      </div>");
        builder.AppendLine("    </details>");
        builder.AppendLine("    <details class=\"panel\">");
        builder.AppendLine($"      <summary><span>{Bilingual(SupportReportLocalizer.Text("TcpChecks", ReportLanguage.English), SupportReportLocalizer.Text("TcpChecks", ReportLanguage.TraditionalChinese))}</span><span class=\"summary-note\">{Encode(BuildTcpSummary(report))}</span></summary>");
        builder.AppendLine("      <div class=\"detail-body\">");
        builder.AppendLine($"      <table><thead><tr><th>{Bilingual(SupportReportLocalizer.Text("Name", ReportLanguage.English), SupportReportLocalizer.Text("Name", ReportLanguage.TraditionalChinese))}</th><th>{Bilingual(SupportReportLocalizer.Text("Endpoint", ReportLanguage.English), SupportReportLocalizer.Text("Endpoint", ReportLanguage.TraditionalChinese))}</th><th>{Bilingual(SupportReportLocalizer.Text("Status", ReportLanguage.English), SupportReportLocalizer.Text("Status", ReportLanguage.TraditionalChinese))}</th><th>{Bilingual(SupportReportLocalizer.Text("Duration", ReportLanguage.English), SupportReportLocalizer.Text("Duration", ReportLanguage.TraditionalChinese))}</th><th>{Bilingual(SupportReportLocalizer.Text("Detail", ReportLanguage.English), SupportReportLocalizer.Text("Detail", ReportLanguage.TraditionalChinese))}</th></tr></thead><tbody>");
        if (report.TcpResults.Count == 0)
        {
            builder.AppendLine($"        <tr><td colspan=\"5\">{Bilingual(SupportReportLocalizer.Text("NoTcpChecks", ReportLanguage.English), SupportReportLocalizer.Text("NoTcpChecks", ReportLanguage.TraditionalChinese))}</td></tr>");
        }
        else
        {
            foreach (var result in report.TcpResults)
            {
                var endpointLabel = $"{result.Host}:{result.Port}";
                var englishStatus = result.Success ? SupportReportLocalizer.Text("Pass", ReportLanguage.English) : SupportReportLocalizer.Text("Fail", ReportLanguage.English);
                var chineseStatus = result.Success ? SupportReportLocalizer.Text("Pass", ReportLanguage.TraditionalChinese) : SupportReportLocalizer.Text("Fail", ReportLanguage.TraditionalChinese);
                var englishDetail = result.Success ? "Connection established." : result.ErrorMessage ?? "Connection failed.";
                var chineseDetail = result.Success ? "\u9023\u7dda\u5DF2\u5EFA\u7ACB\u3002" : result.ErrorMessage ?? "\u9023\u7DDA\u5931\u6557\u3002";
                builder.AppendLine($"        <tr><td>{Encode(result.Name)}</td><td>{Encode(endpointLabel)}</td><td>{Bilingual(englishStatus, chineseStatus)}</td><td>{result.DurationMs} ms</td><td>{Bilingual(englishDetail, chineseDetail)}</td></tr>");
            }
        }
        builder.AppendLine("      </tbody></table>");
        builder.AppendLine("      </div>");
        builder.AppendLine("    </details>");
        builder.AppendLine($"    <details class=\"panel\"{(IsRouteDetailWorthOpening(report) ? " open" : string.Empty)}>");
        builder.AppendLine($"      <summary><span>{Bilingual(SupportReportLocalizer.Text("RouteDetail", ReportLanguage.English), SupportReportLocalizer.Text("RouteDetail", ReportLanguage.TraditionalChinese))}</span><span class=\"summary-note\">{Bilingual(routeEn.StatusSummary, routeZh.StatusSummary)}</span></summary>");
        builder.AppendLine("      <div class=\"detail-body\">");
        builder.AppendLine($"      <table><thead><tr><th>{Bilingual(SupportReportLocalizer.Text("Hop", ReportLanguage.English), SupportReportLocalizer.Text("Hop", ReportLanguage.TraditionalChinese))}</th><th>{Bilingual(SupportReportLocalizer.Text("Address", ReportLanguage.English), SupportReportLocalizer.Text("Address", ReportLanguage.TraditionalChinese))}</th><th>{Bilingual(SupportReportLocalizer.Text("Avg", ReportLanguage.English), SupportReportLocalizer.Text("Avg", ReportLanguage.TraditionalChinese))}</th><th>{Bilingual(SupportReportLocalizer.Text("Delta", ReportLanguage.English), SupportReportLocalizer.Text("Delta", ReportLanguage.TraditionalChinese))}</th><th>{Bilingual(SupportReportLocalizer.Text("Scope", ReportLanguage.English), SupportReportLocalizer.Text("Scope", ReportLanguage.TraditionalChinese))}</th><th>{Bilingual(SupportReportLocalizer.Text("Note", ReportLanguage.English), SupportReportLocalizer.Text("Note", ReportLanguage.TraditionalChinese))}</th></tr></thead><tbody>");
        if (report.PrimaryRoute.Hops.Count == 0)
        {
            builder.AppendLine($"        <tr><td colspan=\"6\">{Bilingual(SupportReportLocalizer.Text("NoParsableHops", ReportLanguage.English), SupportReportLocalizer.Text("NoParsableHops", ReportLanguage.TraditionalChinese))}</td></tr>");
        }
        else
        {
            foreach (var hop in report.PrimaryRoute.Hops)
            {
                builder.AppendLine($"        <tr><td>{hop.HopNumber}</td><td>{Encode(hop.DisplayAddress)}</td><td>{Encode(hop.AverageLatencyMs?.ToString() ?? "*")} ms</td><td>{Encode(hop.LatencyDeltaMs?.ToString() ?? "-")} ms</td><td>{Bilingual(hop.ScopeLabel, SupportReportLocalizer.GetHopScopeLabel(hop, ReportLanguage.TraditionalChinese))}</td><td>{Bilingual(hop.Note, SupportReportLocalizer.GetHopNote(hop, ReportLanguage.TraditionalChinese))}</td></tr>");
            }
        }
        builder.AppendLine("      </tbody></table>");
        builder.AppendLine("      </div>");
        builder.AppendLine("    </details>");
        builder.AppendLine("    <details class=\"panel\">");
        builder.AppendLine($"      <summary><span>{Bilingual(SupportReportLocalizer.Text("RawTracerouteOutput", ReportLanguage.English), SupportReportLocalizer.Text("RawTracerouteOutput", ReportLanguage.TraditionalChinese))}</span><span class=\"summary-note\">{Bilingual("Expand only if you need the raw command output.", "只有需要看原始輸出時再展開。")}</span></summary>");
        builder.AppendLine("      <div class=\"detail-body\">");
        builder.AppendLine($"      <pre>{Encode(string.Join(Environment.NewLine, report.PrimaryRoute.RawTracerouteLines))}</pre>");
        builder.AppendLine("      </div>");
        builder.AppendLine("    </details>");
        builder.AppendLine("  </div>");
        builder.AppendLine("  <script>");
        builder.AppendLine("    (() => {");
        builder.AppendLine("      const root = document.documentElement;");
        builder.AppendLine("      const buttons = document.querySelectorAll('[data-switch-language]');");
        builder.AppendLine("      const applyLanguage = (language) => {");
        builder.AppendLine("        root.classList.toggle('lang-en', language === 'en');");
        builder.AppendLine("        root.classList.toggle('lang-zh', language === 'zh-TW');");
        builder.AppendLine("        buttons.forEach((button) => button.setAttribute('aria-pressed', button.dataset.switchLanguage === language ? 'true' : 'false'));");
        builder.AppendLine("      };");
        builder.AppendLine("      buttons.forEach((button) => button.addEventListener('click', () => applyLanguage(button.dataset.switchLanguage)));");
        builder.AppendLine("      applyLanguage(root.dataset.reportLanguage === 'zh-TW' ? 'zh-TW' : 'en');");
        builder.AppendLine("    })();");
        builder.AppendLine("  </script>");
        builder.AppendLine("</body>");
        builder.AppendLine("</html>");

        return builder.ToString();
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
        return $"{passed}/{report.DnsResults.Count} pass";
    }

    private static string BuildTcpSummary(SupportDiagnosticReport report)
    {
        if (report.TcpResults.Count == 0)
        {
            return "n/a";
        }

        var passed = report.TcpResults.Count(static result => result.Success);
        return $"{passed}/{report.TcpResults.Count} pass";
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
