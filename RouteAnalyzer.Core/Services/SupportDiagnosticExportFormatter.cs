using System.Net;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using RouteAnalyzer.Models;

namespace RouteAnalyzer.Services;

public static class SupportDiagnosticExportFormatter
{
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
        var builder = new StringBuilder();
        builder.AppendLine("Route Analyzer Support Report");
        builder.AppendLine("============================");
        builder.AppendLine($"Execution ID : {report.ExecutionId}");
        builder.AppendLine($"Generated    : {report.GeneratedAtUtc:yyyy-MM-dd HH:mm:ss 'UTC'}");
        builder.AppendLine($"Machine      : {report.MachineName}");
        builder.AppendLine($"Profile      : {report.Profile.ProfileName}");
        builder.AppendLine($"Company      : {report.Profile.CompanyName ?? "-"}");
        builder.AppendLine($"Target       : {report.Profile.TargetHost}");
        builder.AppendLine($"Status       : {report.Assessment.OverallStatusLabel}");
        builder.AppendLine($"Fault Domain : {report.Assessment.FaultDomain}");
        builder.AppendLine($"Confidence   : {report.Assessment.ConfidenceLabel}");
        builder.AppendLine($"Duration     : {report.DurationMs} ms");
        builder.AppendLine();
        builder.AppendLine("User Summary");
        builder.AppendLine("------------");
        builder.AppendLine(report.Assessment.UserSummary);
        builder.AppendLine();
        builder.AppendLine("IT Summary");
        builder.AppendLine("----------");
        builder.AppendLine(report.Assessment.ItSummary);
        builder.AppendLine();
        builder.AppendLine("Evidence");
        builder.AppendLine("--------");
        foreach (var line in report.Assessment.EvidenceHighlights)
        {
            builder.AppendLine($"- {line}");
        }

        builder.AppendLine();
        builder.AppendLine("Recommendations");
        builder.AppendLine("---------------");
        foreach (var line in report.Assessment.Recommendations)
        {
            builder.AppendLine($"- {line}");
        }

        if (report.DnsResults.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("DNS Checks");
            builder.AppendLine("----------");
            foreach (var result in report.DnsResults)
            {
                var status = result.Success ? "PASS" : "FAIL";
                var detail = result.Success
                    ? string.Join(", ", result.Addresses)
                    : result.ErrorMessage ?? "Lookup failed.";
                builder.AppendLine($"- [{status}] {result.Name} -> {result.Hostname} ({result.DurationMs} ms) :: {detail}");
            }
        }

        if (report.TcpResults.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("TCP Checks");
            builder.AppendLine("----------");
            foreach (var result in report.TcpResults)
            {
                var status = result.Success ? "PASS" : "FAIL";
                var detail = result.Success ? "Connection established." : result.ErrorMessage ?? "Connection failed.";
                builder.AppendLine($"- [{status}] {result.Name} -> {result.Host}:{result.Port} ({result.DurationMs} ms) :: {detail}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("Route Summary");
        builder.AppendLine("-------------");
        builder.AppendLine($"Status    : {report.PrimaryRoute.StatusLabel}");
        builder.AppendLine($"Summary   : {report.PrimaryRoute.StatusSummary}");
        builder.AppendLine($"Narrative : {report.PrimaryRoute.Narrative}");
        builder.AppendLine($"Ping Avg  : {report.PrimaryRoute.PingSummary.AverageRoundTripMs?.ToString() ?? "-"} ms");
        builder.AppendLine($"Loss      : {report.PrimaryRoute.PingSummary.PacketLossPercent}%");
        builder.AppendLine($"Jitter    : {report.PrimaryRoute.PingSummary.JitterMs?.ToString() ?? "-"} ms");
        builder.AppendLine();
        builder.AppendLine("Hops");
        builder.AppendLine("----");

        foreach (var hop in report.PrimaryRoute.Hops)
        {
            builder.AppendLine(
                $"#{hop.HopNumber,2} {hop.DisplayAddress,-40} {(hop.AverageLatencyMs?.ToString() ?? "*"),5} ms  {(hop.ScopeLabel),-18}  {hop.Note}");
        }

        return builder.ToString();
    }

    public static string ToHtml(SupportDiagnosticReport report)
    {
        var builder = new StringBuilder();
        var statusClass = GetStatusClass(report.Assessment.OverallStatusLabel);
        var dnsPassed = report.DnsResults.Count(static result => result.Success);
        var tcpPassed = report.TcpResults.Count(static result => result.Success);
        var pingAverage = report.PrimaryRoute.PingSummary.AverageRoundTripMs?.ToString() ?? "-";
        var jitter = report.PrimaryRoute.PingSummary.JitterMs?.ToString() ?? "-";
        var generatedDisplay = report.GeneratedAtUtc.ToString("yyyy-MM-dd HH:mm:ss 'UTC'");
        var dnsMetric = report.DnsResults.Count == 0 ? "n/a" : $"{dnsPassed}/{report.DnsResults.Count} pass";
        var tcpMetric = report.TcpResults.Count == 0 ? "n/a" : $"{tcpPassed}/{report.TcpResults.Count} pass";

        builder.AppendLine("<!DOCTYPE html>");
        builder.AppendLine("<html lang=\"en\">");
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
        builder.AppendLine("    @media (max-width: 720px) { .hero-grid { grid-template-columns: 1fr; } .page { padding: 18px 14px 36px; } .hero { padding: 22px; } }");
        builder.AppendLine("  </style>");
        builder.AppendLine("</head>");
        builder.AppendLine("<body>");
        builder.AppendLine("  <div class=\"page\">");
        builder.AppendLine("    <section class=\"hero\">");
        builder.AppendLine("      <span class=\"eyebrow\">Route Analyzer Support Report</span>");
        builder.AppendLine($"      <h1>{Encode(report.Profile.ProfileName)}</h1>");
        builder.AppendLine("      <div class=\"hero-grid\">");
        builder.AppendLine("        <div>");
        builder.AppendLine($"          <span class=\"status-pill {statusClass}\">{Encode(report.Assessment.OverallStatusLabel)}</span>");
        builder.AppendLine($"          <p style=\"margin-top:14px;font-size:18px;\">{Encode(report.Assessment.UserSummary)}</p>");
        builder.AppendLine($"          <p class=\"muted\" style=\"margin-top:12px;\">Fault domain: {Encode(report.Assessment.FaultDomain)} | Confidence: {Encode(report.Assessment.ConfidenceLabel)}</p>");
        builder.AppendLine("        </div>");
        builder.AppendLine("        <div class=\"card\">");
        builder.AppendLine("          <span class=\"label\">Run Details</span>");
        builder.AppendLine($"          <p><strong>Company:</strong> {Encode(report.Profile.CompanyName ?? "-")}</p>");
        builder.AppendLine($"          <p><strong>Machine:</strong> {Encode(report.MachineName)}</p>");
        builder.AppendLine($"          <p><strong>Target:</strong> {Encode(report.Profile.TargetHost)}</p>");
        builder.AppendLine($"          <p><strong>Generated:</strong> {Encode(generatedDisplay)}</p>");
        builder.AppendLine($"          <p><strong>Execution ID:</strong> <code>{Encode(report.ExecutionId)}</code></p>");
        builder.AppendLine("        </div>");
        builder.AppendLine("      </div>");
        builder.AppendLine("      <div class=\"summary-grid\">");
        builder.AppendLine("        " + MetricCard("Average latency", pingAverage + " ms"));
        builder.AppendLine("        " + MetricCard("Packet loss", report.PrimaryRoute.PingSummary.PacketLossPercent + "%"));
        builder.AppendLine("        " + MetricCard("Jitter", jitter + " ms"));
        builder.AppendLine("        " + MetricCard("DNS checks", dnsMetric));
        builder.AppendLine("        " + MetricCard("TCP checks", tcpMetric));
        builder.AppendLine("        " + MetricCard("Duration", report.DurationMs + " ms"));
        builder.AppendLine("      </div>");
        builder.AppendLine("    </section>");
        builder.AppendLine("    <section class=\"two-col\">");
        builder.AppendLine("      <article class=\"panel\">");
        builder.AppendLine("        <h2>User Summary</h2>");
        builder.AppendLine($"        <p style=\"margin-top:12px;\">{Encode(report.Assessment.UserSummary)}</p>");
        builder.AppendLine("        <ul>");
        foreach (var recommendation in report.Assessment.Recommendations)
        {
            builder.AppendLine($"          <li>{Encode(recommendation)}</li>");
        }
        builder.AppendLine("        </ul>");
        builder.AppendLine("      </article>");
        builder.AppendLine("      <article class=\"panel\">");
        builder.AppendLine("        <h2>IT Summary</h2>");
        builder.AppendLine($"        <p style=\"margin-top:12px;\">{Encode(report.Assessment.ItSummary)}</p>");
        builder.AppendLine("        <ul>");
        foreach (var evidence in report.Assessment.EvidenceHighlights)
        {
            builder.AppendLine($"          <li>{Encode(evidence)}</li>");
        }
        builder.AppendLine("        </ul>");
        builder.AppendLine("      </article>");
        builder.AppendLine("    </section>");
        builder.AppendLine("    <section class=\"panel\">");
        builder.AppendLine("      <h2>DNS Checks</h2>");
        builder.AppendLine("      <table><thead><tr><th>Name</th><th>Hostname</th><th>Status</th><th>Duration</th><th>Detail</th></tr></thead><tbody>");
        if (report.DnsResults.Count == 0)
        {
            builder.AppendLine("        <tr><td colspan=\"5\">No DNS checks were configured.</td></tr>");
        }
        else
        {
            foreach (var result in report.DnsResults)
            {
                var detail = result.Success ? string.Join(", ", result.Addresses) : result.ErrorMessage ?? "Lookup failed.";
                builder.AppendLine($"        <tr><td>{Encode(result.Name)}</td><td>{Encode(result.Hostname)}</td><td>{Encode(result.Success ? "Pass" : "Fail")}</td><td>{result.DurationMs} ms</td><td>{Encode(detail)}</td></tr>");
            }
        }
        builder.AppendLine("      </tbody></table>");
        builder.AppendLine("    </section>");
        builder.AppendLine("    <section class=\"panel\">");
        builder.AppendLine("      <h2>TCP Checks</h2>");
        builder.AppendLine("      <table><thead><tr><th>Name</th><th>Endpoint</th><th>Status</th><th>Duration</th><th>Detail</th></tr></thead><tbody>");
        if (report.TcpResults.Count == 0)
        {
            builder.AppendLine("        <tr><td colspan=\"5\">No TCP endpoints were configured.</td></tr>");
        }
        else
        {
            foreach (var result in report.TcpResults)
            {
                var endpointLabel = $"{result.Host}:{result.Port}";
                builder.AppendLine($"        <tr><td>{Encode(result.Name)}</td><td>{Encode(endpointLabel)}</td><td>{Encode(result.Success ? "Pass" : "Fail")}</td><td>{result.DurationMs} ms</td><td>{Encode(result.Success ? "Connection established." : result.ErrorMessage ?? "Connection failed.")}</td></tr>");
            }
        }
        builder.AppendLine("      </tbody></table>");
        builder.AppendLine("    </section>");
        builder.AppendLine("    <section class=\"panel\">");
        builder.AppendLine("      <h2>Route Detail</h2>");
        builder.AppendLine($"      <p class=\"muted\" style=\"margin-top:8px;\">{Encode(report.PrimaryRoute.StatusSummary)}</p>");
        builder.AppendLine("      <table><thead><tr><th>Hop</th><th>Address</th><th>Avg</th><th>Delta</th><th>Scope</th><th>Note</th></tr></thead><tbody>");
        if (report.PrimaryRoute.Hops.Count == 0)
        {
            builder.AppendLine("        <tr><td colspan=\"6\">No parsable hops were captured.</td></tr>");
        }
        else
        {
            foreach (var hop in report.PrimaryRoute.Hops)
            {
                builder.AppendLine($"        <tr><td>{hop.HopNumber}</td><td>{Encode(hop.DisplayAddress)}</td><td>{Encode(hop.AverageLatencyMs?.ToString() ?? "*")} ms</td><td>{Encode(hop.LatencyDeltaMs?.ToString() ?? "-")} ms</td><td>{Encode(hop.ScopeLabel)}</td><td>{Encode(hop.Note)}</td></tr>");
            }
        }
        builder.AppendLine("      </tbody></table>");
        builder.AppendLine("    </section>");
        builder.AppendLine("    <section class=\"panel\">");
        builder.AppendLine("      <h2>Raw Traceroute Output</h2>");

        // 2026/3/25: There's encoding problem below  
        // �b�W�� 24 ���D�I�W�l�� 172.17.70.36 ������
        // 1    <1 ms    <1 ms    <1 ms  172.17.68.253
        // 2     1 ms     1 ms     1 ms  172.17.255.244
        // 3     1 ms     1 ms     1 ms  172.17.70.36
        // �l�ܧ����C
        builder.AppendLine($"      <pre>{Encode(string.Join(Environment.NewLine, report.PrimaryRoute.RawTracerouteLines))}</pre>");
        builder.AppendLine("    </section>");
        builder.AppendLine("  </div>");
        builder.AppendLine("</body>");
        builder.AppendLine("</html>");

        return builder.ToString();
    }

    public static ReportArtifactBundle WriteBundle(SupportDiagnosticReport report, string directoryPath)
    {
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

    private static string MetricCard(string label, string value)
    {
        return $"<div class=\"card\"><span class=\"label\">{Encode(label)}</span><strong class=\"big\">{Encode(value)}</strong></div>";
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
