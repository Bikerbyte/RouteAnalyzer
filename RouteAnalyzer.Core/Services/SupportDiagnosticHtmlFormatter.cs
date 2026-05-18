using System.Text;
using RouteAnalyzer.Models;

namespace RouteAnalyzer.Services;

internal static class SupportDiagnosticHtmlFormatter
{
    public static string Render(SupportDiagnosticReport report)
    {
        var language = ReportLanguage.Normalize(report.Profile.PreferredLanguage);
        var htmlClass = ReportLanguage.IsTraditionalChinese(language) ? "lang-zh" : "lang-en";
        var dnsPassed = report.DnsResults.Count(static result => result.Success);
        var tcpPassed = report.TcpResults.Count(static result => result.Success);
        var latency = report.PrimaryRoute.PingSummary.AverageRoundTripMs?.ToString() ?? "-";
        var loss = report.PrimaryRoute.PingSummary.PacketLossPercent.ToString();
        var copyText = BuildHandoffText(report, language);

        return $$"""
<!DOCTYPE html>
<html lang="en" class="{{htmlClass}}">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>{{SupportDiagnosticExportFormatter.Encode(report.Profile.ProfileName)}} - Route Analyzer</title>
  <style>
    :root { color-scheme: light; --bg:#f4f1ea; --panel:#fffdf8; --ink:#181713; --muted:#68635a; --line:#d9d1c2; --blue:#0f5c80; --green:#166534; --amber:#9a5b00; --red:#a32622; font-family: Aptos, Segoe UI, sans-serif; }
    * { box-sizing: border-box; }
    body { margin:0; background:var(--bg); color:var(--ink); }
    .page { max-width:1120px; margin:0 auto; padding:28px 18px 56px; }
    .top { display:flex; justify-content:space-between; gap:16px; align-items:flex-start; flex-wrap:wrap; margin-bottom:18px; }
    h1,h2,h3,p { margin:0; }
    h1 { font-family: Georgia, Times New Roman, serif; font-size:42px; line-height:1; }
    h2 { font-size:18px; }
    .eyebrow { color:var(--blue); font-size:12px; font-weight:900; letter-spacing:.14em; text-transform:uppercase; margin-bottom:8px; }
    .panel { background:var(--panel); border:1px solid var(--line); border-radius:8px; padding:16px; margin-top:12px; }
    .hero { border:2px solid var(--ink); }
    .overview { margin-top:12px; color:var(--muted); line-height:1.6; max-width:780px; }
    .metrics { display:grid; grid-template-columns:repeat(4,1fr); gap:12px; margin-top:14px; }
    .metric { border:1px solid var(--line); border-radius:8px; padding:14px; background:#fffaf1; }
    .metric span { display:block; color:var(--muted); font-size:12px; font-weight:800; text-transform:uppercase; letter-spacing:.08em; }
    .metric strong { display:block; margin-top:8px; font-family:Georgia, Times New Roman, serif; font-size:32px; }
    .grid { display:grid; grid-template-columns:1fr 1fr; gap:12px; }
    ul { margin:10px 0 0; padding-left:20px; line-height:1.65; color:var(--muted); }
    table { width:100%; border-collapse:collapse; margin-top:10px; font-size:14px; }
    th,td { padding:10px 8px; border-bottom:1px solid var(--line); text-align:left; vertical-align:top; }
    th { color:var(--muted); font-size:12px; text-transform:uppercase; letter-spacing:.08em; }
    .bars { display:grid; grid-auto-flow:column; grid-auto-columns:minmax(18px,1fr); align-items:end; gap:6px; min-height:180px; border-left:2px solid var(--ink); border-bottom:2px solid var(--ink); padding:12px 6px 0 12px; margin-top:12px; background:linear-gradient(rgba(24,23,19,.08) 1px, transparent 1px) 0 0/100% 36px; }
    .bar { min-width:16px; border:2px solid var(--ink); border-bottom:0; border-radius:8px 8px 0 0; background:#dceff5; }
    .bar.timeout { background:#fff0cf; }
    .bar.step { background:#fde7e2; }
    pre, textarea { width:100%; white-space:pre-wrap; word-break:break-word; border:1px solid var(--line); border-radius:8px; background:#fffaf1; padding:12px; color:var(--ink); line-height:1.55; }
    .btn { display:inline-block; border:2px solid var(--ink); border-radius:8px; background:var(--panel); color:var(--ink); padding:10px 12px; text-decoration:none; cursor:pointer; }
    .row { display:flex; gap:10px; flex-wrap:wrap; margin-top:12px; }
    @media (max-width:800px) { .metrics,.grid { grid-template-columns:1fr; } h1 { font-size:34px; } }
  </style>
</head>
<body>
  <main class="page">
    <header class="top">
      <div>
        <p class="eyebrow">{{SupportReportLocalizer.Text("ReportTitle", language)}}</p>
        <h1>{{SupportDiagnosticExportFormatter.Encode(report.Profile.ProfileName)}}</h1>
        <p class="overview">{{SupportDiagnosticExportFormatter.Encode(SupportDiagnosticExportFormatter.LocalizeOverview(report.SignalSummary.Overview, language))}}</p>
      </div>
      <div class="panel">
        <strong>{{SupportReportLocalizer.Text("ExecutionId", language)}}:</strong> {{SupportDiagnosticExportFormatter.Encode(report.ExecutionId)}}<br>
        <strong>{{SupportReportLocalizer.Text("Generated", language)}}:</strong> {{report.GeneratedAtUtc:yyyy-MM-dd HH:mm:ss}} UTC<br>
        <strong>{{SupportReportLocalizer.Text("Target", language)}}:</strong> {{SupportDiagnosticExportFormatter.Encode(report.Profile.TargetHost)}}
      </div>
    </header>

    <section class="panel hero">
      <div class="metrics">
        {{Metric(SupportReportLocalizer.Text("Latency", language), latency + " ms")}}
        {{Metric(SupportReportLocalizer.Text("PacketLoss", language), loss + "%")}}
        {{Metric("DNS", report.DnsResults.Count == 0 ? "n/a" : $"{dnsPassed}/{report.DnsResults.Count}")}}
        {{Metric("TCP", report.TcpResults.Count == 0 ? "n/a" : $"{tcpPassed}/{report.TcpResults.Count}")}}
      </div>
    </section>

    <div class="grid">
      <section class="panel">
        <h2>{{SupportReportLocalizer.Text("Signals", language)}}</h2>
        <ul>
          {{RenderSignals(report, language)}}
        </ul>
      </section>
      <section class="panel">
        <h2>{{SupportReportLocalizer.Text("RunDetails", language)}}</h2>
        <ul>
          <li>{{SupportReportLocalizer.Text("Machine", language)}}: {{SupportDiagnosticExportFormatter.Encode(report.MachineName)}}</li>
          <li>{{SupportReportLocalizer.Text("ConnectionType", language)}}: {{SupportDiagnosticExportFormatter.Encode(report.NetworkContext.ConnectionType)}}</li>
          <li>{{SupportReportLocalizer.Text("ActiveAdapter", language)}}: {{SupportDiagnosticExportFormatter.Encode(report.NetworkContext.ActiveAdapterName)}}</li>
          <li>{{SupportReportLocalizer.Text("DefaultGateway", language)}}: {{SupportDiagnosticExportFormatter.Encode(report.NetworkContext.DefaultGateway)}}</li>
          <li>{{SupportReportLocalizer.Text("DnsServers", language)}}: {{SupportDiagnosticExportFormatter.Encode(string.Join(", ", report.NetworkContext.DnsServers))}}</li>
        </ul>
      </section>
    </div>

    <section class="panel">
      <h2>{{SupportReportLocalizer.Text("RouteSummary", language)}}</h2>
      <div class="bars">{{RenderBars(report)}}</div>
      {{RenderRouteTable(report, language)}}
    </section>

    <div class="grid">
      <section class="panel">
        <h2>{{SupportReportLocalizer.Text("DnsChecks", language)}}</h2>
        {{RenderDnsTable(report, language)}}
      </section>
      <section class="panel">
        <h2>{{SupportReportLocalizer.Text("TcpChecks", language)}}</h2>
        {{RenderTcpTable(report, language)}}
      </section>
    </div>

    <section class="panel">
      <h2>{{SupportReportLocalizer.Text("CopyHandoff", language)}}</h2>
      <textarea id="copy-text" rows="8" readonly>{{SupportDiagnosticExportFormatter.Encode(copyText)}}</textarea>
      <div class="row"><button class="btn" type="button" id="copy-button">{{SupportReportLocalizer.Text("CopyHandoff", language)}}</button></div>
    </section>

    <section class="panel">
      <h2>{{SupportReportLocalizer.Text("RawTracerouteOutput", language)}}</h2>
      <pre>{{SupportDiagnosticExportFormatter.Encode(string.Join(Environment.NewLine, report.PrimaryRoute.RawTracerouteLines))}}</pre>
    </section>
  </main>
  <script>
    document.querySelector('#copy-button')?.addEventListener('click', async () => {
      await navigator.clipboard.writeText(document.querySelector('#copy-text').value);
      document.querySelector('#copy-button').textContent = '{{SupportReportLocalizer.Text("Copied", language)}}';
    });
  </script>
</body>
</html>
""";
    }

    private static string Metric(string label, string value)
    {
        return $"<div class=\"metric\"><span>{SupportDiagnosticExportFormatter.Encode(label)}</span><strong>{SupportDiagnosticExportFormatter.Encode(value)}</strong></div>";
    }

    private static string RenderSignals(SupportDiagnosticReport report, string language)
    {
        return string.Join(Environment.NewLine, report.SignalSummary.Signals.Select(signal => $"<li>{SupportDiagnosticExportFormatter.Encode(SupportDiagnosticExportFormatter.LocalizeSignal(signal, language))}</li>"));
    }

    private static string RenderBars(SupportDiagnosticReport report)
    {
        var hops = report.PrimaryRoute.Hops.Take(36).ToArray();
        if (hops.Length == 0)
        {
            return string.Empty;
        }

        var maxLatency = Math.Max(hops.Max(static hop => hop.AverageLatencyMs ?? 0), 20);
        return string.Join(Environment.NewLine, hops.Select(hop =>
        {
            var latency = hop.AverageLatencyMs ?? 0;
            var height = hop.IsTimeout ? 24 : Math.Max(18, (int)Math.Round(latency / (double)maxLatency * 150));
            var css = hop.IsTimeout ? " timeout" : hop.LatencyDeltaMs >= 25 ? " step" : string.Empty;
            return $"<div class=\"bar{css}\" style=\"height:{height}px\" title=\"hop {hop.HopNumber}: {SupportDiagnosticExportFormatter.Encode(hop.DisplayAddress)}\"></div>";
        }));
    }

    private static string RenderRouteTable(SupportDiagnosticReport report, string language)
    {
        if (report.PrimaryRoute.Hops.Count == 0)
        {
            return $"<p>{SupportReportLocalizer.Text("NoParsableHops", language)}</p>";
        }

        var builder = new StringBuilder();
        builder.AppendLine("<table><thead><tr>");
        builder.AppendLine($"<th>{SupportReportLocalizer.Text("Hop", language)}</th><th>{SupportReportLocalizer.Text("Address", language)}</th><th>{SupportReportLocalizer.Text("Avg", language)}</th><th>{SupportReportLocalizer.Text("Delta", language)}</th><th>{SupportReportLocalizer.Text("Scope", language)}</th><th>{SupportReportLocalizer.Text("Note", language)}</th>");
        builder.AppendLine("</tr></thead><tbody>");

        foreach (var hop in report.PrimaryRoute.Hops)
        {
            builder.AppendLine("<tr>");
            builder.AppendLine($"<td>{hop.HopNumber}</td><td>{SupportDiagnosticExportFormatter.Encode(hop.DisplayAddress)}</td><td>{hop.AverageLatencyMs?.ToString() ?? "*"}</td><td>{hop.LatencyDeltaMs?.ToString() ?? "-"}</td><td>{SupportDiagnosticExportFormatter.Encode(SupportReportLocalizer.GetHopScopeLabel(hop, language))}</td><td>{SupportDiagnosticExportFormatter.Encode(SupportReportLocalizer.GetHopNote(hop, language))}</td>");
            builder.AppendLine("</tr>");
        }

        builder.AppendLine("</tbody></table>");
        return builder.ToString();
    }

    private static string RenderDnsTable(SupportDiagnosticReport report, string language)
    {
        if (report.DnsResults.Count == 0)
        {
            return $"<p>{SupportReportLocalizer.Text("NoDnsChecks", language)}</p>";
        }

        var builder = new StringBuilder();
        builder.AppendLine("<table><thead><tr>");
        builder.AppendLine($"<th>{SupportReportLocalizer.Text("Name", language)}</th><th>{SupportReportLocalizer.Text("Hostname", language)}</th><th>{SupportReportLocalizer.Text("Status", language)}</th><th>{SupportReportLocalizer.Text("Detail", language)}</th>");
        builder.AppendLine("</tr></thead><tbody>");

        foreach (var result in report.DnsResults)
        {
            builder.AppendLine($"<tr><td>{SupportDiagnosticExportFormatter.Encode(result.Name)}</td><td>{SupportDiagnosticExportFormatter.Encode(result.Hostname)}</td><td>{(result.Success ? SupportReportLocalizer.Text("Pass", language) : SupportReportLocalizer.Text("Fail", language))}</td><td>{SupportDiagnosticExportFormatter.Encode(result.Success ? string.Join(", ", result.Addresses) : result.ErrorMessage ?? string.Empty)}</td></tr>");
        }

        builder.AppendLine("</tbody></table>");
        return builder.ToString();
    }

    private static string RenderTcpTable(SupportDiagnosticReport report, string language)
    {
        if (report.TcpResults.Count == 0)
        {
            return $"<p>{SupportReportLocalizer.Text("NoTcpChecks", language)}</p>";
        }

        var builder = new StringBuilder();
        builder.AppendLine("<table><thead><tr>");
        builder.AppendLine($"<th>{SupportReportLocalizer.Text("Name", language)}</th><th>{SupportReportLocalizer.Text("Endpoint", language)}</th><th>{SupportReportLocalizer.Text("Status", language)}</th><th>{SupportReportLocalizer.Text("Duration", language)}</th>");
        builder.AppendLine("</tr></thead><tbody>");

        foreach (var result in report.TcpResults)
        {
            builder.AppendLine($"<tr><td>{SupportDiagnosticExportFormatter.Encode(result.Name)}</td><td>{SupportDiagnosticExportFormatter.Encode($"{result.Host}:{result.Port}")}</td><td>{(result.Success ? SupportReportLocalizer.Text("Pass", language) : SupportReportLocalizer.Text("Fail", language))}</td><td>{result.DurationMs} ms</td></tr>");
        }

        builder.AppendLine("</tbody></table>");
        return builder.ToString();
    }

    private static string BuildHandoffText(SupportDiagnosticReport report, string language)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"{SupportReportLocalizer.Text("ExecutionId", language)}: {report.ExecutionId}");
        builder.AppendLine($"{SupportReportLocalizer.Text("Target", language)}: {report.Profile.TargetHost}");
        builder.AppendLine($"{SupportReportLocalizer.Text("Overview", language)}: {SupportDiagnosticExportFormatter.LocalizeOverview(report.SignalSummary.Overview, language)}");
        foreach (var signal in report.SignalSummary.Signals)
        {
            builder.AppendLine($"- {SupportDiagnosticExportFormatter.LocalizeSignal(signal, language)}");
        }

        return builder.ToString().TrimEnd();
    }
}
