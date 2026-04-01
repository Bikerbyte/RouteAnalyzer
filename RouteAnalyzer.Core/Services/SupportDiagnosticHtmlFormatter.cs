using System.Net;
using System.Text;
using System.Text.Json;
using RouteAnalyzer.Models;

namespace RouteAnalyzer.Services;

internal static class SupportDiagnosticHtmlFormatter
{
    private const int MaxSummaryEvidenceItems = 4;
    private const int MaxSummaryRecommendationItems = 4;
    private const int MaxSummarySignalItems = 4;

    public static string Render(SupportDiagnosticReport report)
    {
        var defaultLanguage = ReportLanguage.Normalize(report.Profile.PreferredLanguage);
        var htmlLanguageClass = ReportLanguage.IsTraditionalChinese(defaultLanguage) ? "lang-zh" : "lang-en";
        var assessmentEn = SupportReportLocalizer.GetAssessmentView(report, ReportLanguage.English);
        var assessmentZh = SupportReportLocalizer.GetAssessmentView(report, ReportLanguage.TraditionalChinese);
        var routeEn = SupportReportLocalizer.GetRouteView(report.PrimaryRoute, ReportLanguage.English);
        var routeZh = SupportReportLocalizer.GetRouteView(report.PrimaryRoute, ReportLanguage.TraditionalChinese);
        var observationsEn = TakeSummaryItems(assessmentEn.EvidenceHighlights, MaxSummaryEvidenceItems);
        var observationsZh = TakeSummaryItems(assessmentZh.EvidenceHighlights, MaxSummaryEvidenceItems);
        var nextStepsEn = TakeSummaryItems(assessmentEn.Recommendations, MaxSummaryRecommendationItems);
        var nextStepsZh = TakeSummaryItems(assessmentZh.Recommendations, MaxSummaryRecommendationItems);
        var highlightedSignalsEn = TakeSummaryItems(BuildHighlightedSignals(report, ReportLanguage.English), MaxSummarySignalItems);
        var highlightedSignalsZh = TakeSummaryItems(BuildHighlightedSignals(report, ReportLanguage.TraditionalChinese), MaxSummarySignalItems);
        var suspiciousRowsEn = BuildSuspiciousHopRows(report, ReportLanguage.English);
        var suspiciousRowsZh = BuildSuspiciousHopRows(report, ReportLanguage.TraditionalChinese);
        var statusClass = GetStatusClass(report.Assessment.OverallStatusLabel);
        var dnsPassed = report.DnsResults.Count(static result => result.Success);
        var tcpPassed = report.TcpResults.Count(static result => result.Success);
        var latencyValue = report.PrimaryRoute.PingSummary.AverageRoundTripMs?.ToString() ?? "-";
        var packetLossValue = report.PrimaryRoute.PingSummary.PacketLossPercent + "%";
        var dnsValue = report.DnsResults.Count == 0 ? "n/a" : $"{dnsPassed} / {report.DnsResults.Count}";
        var tcpValue = report.TcpResults.Count == 0 ? "n/a" : $"{tcpPassed} / {report.TcpResults.Count}";
        var generatedDisplay = report.GeneratedAtUtc.ToString("yyyy-MM-dd HH:mm:ss 'UTC'");
        var nextActionEn = BuildRecommendedNextAction(assessmentEn, ReportLanguage.English);
        var nextActionZh = BuildRecommendedNextAction(assessmentZh, ReportLanguage.TraditionalChinese);
        var suggestedEscalationEn = BuildSuggestedEscalation(report, ReportLanguage.English);
        var suggestedEscalationZh = BuildSuggestedEscalation(report, ReportLanguage.TraditionalChinese);
        var networkPostureEn = BuildNetworkPosture(report, ReportLanguage.English);
        var networkPostureZh = BuildNetworkPosture(report, ReportLanguage.TraditionalChinese);
        var copyPayload = JsonSerializer.Serialize(new Dictionary<string, Dictionary<string, string>>
        {
            ["itSummary"] = new()
            {
                ["en"] = BuildItSummaryCopy(report, assessmentEn, routeEn, nextActionEn, ReportLanguage.English),
                ["zh-TW"] = BuildItSummaryCopy(report, assessmentZh, routeZh, nextActionZh, ReportLanguage.TraditionalChinese)
            },
            ["incidentNote"] = new()
            {
                ["en"] = BuildIncidentNoteCopy(report, assessmentEn, routeEn, nextActionEn, ReportLanguage.English),
                ["zh-TW"] = BuildIncidentNoteCopy(report, assessmentZh, routeZh, nextActionZh, ReportLanguage.TraditionalChinese)
            }
        });
        var initialCopyItSummaryLabel = ReportLanguage.IsTraditionalChinese(defaultLanguage)
            ? SupportReportLocalizer.Text("CopyItSummary", ReportLanguage.TraditionalChinese)
            : SupportReportLocalizer.Text("CopyItSummary", ReportLanguage.English);
        var initialCopyIncidentNoteLabel = ReportLanguage.IsTraditionalChinese(defaultLanguage)
            ? SupportReportLocalizer.Text("CopyIncidentNote", ReportLanguage.TraditionalChinese)
            : SupportReportLocalizer.Text("CopyIncidentNote", ReportLanguage.English);

        return $$"""
<!DOCTYPE html>
<html lang="en" class="{{htmlLanguageClass}}" data-report-language="{{Encode(defaultLanguage)}}">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>{{Encode(report.Profile.ProfileName)}} - Route Analyzer Report</title>
  <style>
    :root { color-scheme: light; --bg: #f6f7f9; --panel: #ffffff; --line: #e5e7eb; --ink: #111827; --muted: #6b7280; --healthy: #15803d; --healthy-bg: #ecfdf3; --warning: #b45309; --warning-bg: #fffbeb; --action: #b91c1c; --action-bg: #fef2f2; --info: #1d4ed8; --info-bg: #eff6ff; font-family: 'Segoe UI', Tahoma, sans-serif; }
    * { box-sizing: border-box; }
    body { margin: 0; background: var(--bg); color: var(--ink); }
    .page { max-width: 960px; margin: 0 auto; padding: 24px 16px 48px; }
    .header { display: flex; justify-content: space-between; align-items: flex-start; gap: 16px; margin-bottom: 20px; flex-wrap: wrap; }
    .eyebrow { font-size: 12px; text-transform: uppercase; letter-spacing: .12em; color: var(--muted); margin-bottom: 8px; }
    h1, h2, h3, p { margin: 0; }
    h1 { font-size: 28px; line-height: 1.15; }
    .meta { display: grid; grid-template-columns: repeat(3, auto); gap: 8px; color: var(--muted); font-size: 13px; margin-top: 12px; }
    .section { background: var(--panel); border: 1px solid var(--line); border-radius: 12px; padding: 16px; margin-top: 12px; }
    .top-grid { display: grid; grid-template-columns: 1.35fr .95fr; gap: 12px; }
    .status-row { display: flex; align-items: center; gap: 10px; flex-wrap: wrap; margin-bottom: 12px; }
    .badge { display: inline-block; padding: 6px 10px; border-radius: 999px; font-size: 13px; font-weight: 700; border: 1px solid transparent; }
    .status-healthy { color: var(--healthy); background: var(--healthy-bg); border-color: #bbf7d0; }
    .status-warning { color: var(--warning); background: var(--warning-bg); border-color: #fcd34d; }
    .status-action-needed { color: var(--action); background: var(--action-bg); border-color: #fecaca; }
    .subtle { color: var(--muted); font-size: 14px; line-height: 1.6; }
    .callout { padding: 12px 14px; border-radius: 10px; border: 1px solid #bfdbfe; background: var(--info-bg); margin-top: 14px; }
    .callout strong { display: block; margin-bottom: 6px; font-size: 14px; color: var(--info); }
    .mini-grid { display: grid; grid-template-columns: repeat(2, 1fr); gap: 10px; margin-top: 12px; }
    .mini { border: 1px solid var(--line); border-radius: 10px; padding: 12px; background: #fcfcfd; }
    .mini .label { font-size: 12px; text-transform: uppercase; letter-spacing: .08em; color: var(--muted); margin-bottom: 6px; display: block; }
    .mini .value { font-size: 24px; font-weight: 800; line-height: 1.1; display: block; }
    .mini .note { margin-top: 6px; color: var(--muted); font-size: 13px; line-height: 1.5; }
    .list { display: grid; gap: 10px; margin-top: 10px; }
    .row { display: flex; justify-content: space-between; gap: 12px; padding-bottom: 10px; border-bottom: 1px solid var(--line); font-size: 14px; }
    .row:last-child { border-bottom: none; padding-bottom: 0; }
    .row span:first-child { color: var(--muted); }
    .two-col { display: grid; grid-template-columns: 1fr 1fr; gap: 12px; }
    ul { margin: 10px 0 0; padding-left: 18px; line-height: 1.7; }
    .alert { background: var(--warning-bg); border-color: #fcd34d; }
    .alert h2 { font-size: 16px; }
    table { width: 100%; border-collapse: collapse; margin-top: 10px; font-size: 14px; }
    th, td { text-align: left; padding: 10px 8px; border-bottom: 1px solid var(--line); vertical-align: top; }
    th { font-size: 12px; text-transform: uppercase; letter-spacing: .08em; color: var(--muted); }
    .chart { margin-top: 10px; height: 140px; border: 1px solid var(--line); border-radius: 10px; background: linear-gradient(to top, rgba(17,24,39,.04) 1px, transparent 1px) 0 0/100% 35px, linear-gradient(to right, rgba(17,24,39,.04) 1px, transparent 1px) 0 0/48px 100%; position: relative; overflow: hidden; }
    .chart svg { position: absolute; inset: 0; }
    details { margin-top: 12px; background: var(--panel); border: 1px solid var(--line); border-radius: 12px; overflow: hidden; }
    summary { cursor: pointer; padding: 14px 16px; font-weight: 700; list-style: none; }
    summary::-webkit-details-marker { display: none; }
    .detail-body { padding: 0 16px 16px; }
    .button-row { display: flex; gap: 8px; flex-wrap: wrap; margin-top: 12px; }
    .btn { border: 1px solid var(--line); background: #fff; border-radius: 10px; padding: 9px 12px; font-size: 14px; color: var(--ink); cursor: pointer; }
    code { font-family: Consolas, 'Courier New', monospace; font-size: 12px; }
    pre { white-space: pre-wrap; word-break: break-word; color: var(--muted); font-size: 13px; line-height: 1.6; margin: 0; }
    .lang-switch { display: inline-flex; align-items: center; gap: 8px; }
    .lang-btn { border: 1px solid var(--line); border-radius: 999px; padding: 8px 12px; cursor: pointer; background: #fff; color: var(--muted); font-weight: 700; }
    .lang-btn[aria-pressed='true'] { color: var(--ink); border-color: #cbd5e1; }
    [data-lang='en'], [data-lang='zh-TW'] { display: inline; }
    html.lang-en [data-lang='zh-TW'] { display: none !important; }
    html.lang-zh [data-lang='en'] { display: none !important; }
    @media (max-width: 760px) { .top-grid, .two-col, .mini-grid, .meta { grid-template-columns: 1fr; } }
  </style>
</head>
<body>
  <div class="page">
    <div class="header">
      <div>
        <div class="eyebrow">{{Bilingual("RouteAnalyzer / Minimal triage report", "RouteAnalyzer / 極簡 triage 報告")}}</div>
        <h1>{{Encode(report.Profile.ProfileName)}}</h1>
      </div>
      <div>
        <div class="lang-switch" role="group" aria-label="Language switch">
          <button type="button" class="lang-btn" data-switch-language="en">{{Encode(SupportReportLocalizer.Text("English", ReportLanguage.English))}}</button>
          <button type="button" class="lang-btn" data-switch-language="zh-TW">{{Encode(SupportReportLocalizer.Text("TraditionalChinese", ReportLanguage.TraditionalChinese))}}</button>
        </div>
        <div class="meta">
          <div>{{BilingualLabelValue(SupportReportLocalizer.Text("ExecutionId", ReportLanguage.English), report.ExecutionId, SupportReportLocalizer.Text("ExecutionId", ReportLanguage.TraditionalChinese), report.ExecutionId, codeValue: true)}}</div>
          <div>{{BilingualLabelValue(SupportReportLocalizer.Text("Target", ReportLanguage.English), report.Profile.TargetHost, SupportReportLocalizer.Text("Target", ReportLanguage.TraditionalChinese), report.Profile.TargetHost)}}</div>
          <div>{{BilingualLabelValue(SupportReportLocalizer.Text("Machine", ReportLanguage.English), report.MachineName, SupportReportLocalizer.Text("Machine", ReportLanguage.TraditionalChinese), report.MachineName)}}</div>
        </div>
      </div>
    </div>
    <div class="top-grid">
      <section class="section">
        <div class="status-row">
          <span class="badge {{statusClass}}">{{Bilingual(assessmentEn.OverallStatusLabel, assessmentZh.OverallStatusLabel)}}</span>
          <span class="subtle">{{Bilingual($"{SupportReportLocalizer.Text("PossibleFaultDomain", ReportLanguage.English)}: {assessmentEn.FaultDomain}", $"{SupportReportLocalizer.Text("PossibleFaultDomain", ReportLanguage.TraditionalChinese)}: {assessmentZh.FaultDomain}")}}</span>
        </div>
        <h2 style="font-size:24px; line-height:1.25;">{{Bilingual(assessmentEn.UserSummary, assessmentZh.UserSummary)}}</h2>
        <p class="subtle" style="margin-top:10px;">{{Bilingual(assessmentEn.ItSummary, assessmentZh.ItSummary)}}</p>
        <div class="callout">
          <strong>{{Bilingual(SupportReportLocalizer.Text("RecommendedNextAction", ReportLanguage.English), SupportReportLocalizer.Text("RecommendedNextAction", ReportLanguage.TraditionalChinese))}}</strong>
          <span>{{Bilingual(nextActionEn, nextActionZh)}}</span>
        </div>
        <div class="mini-grid">
          {{RenderMetricCard(SupportReportLocalizer.Text("Latency", ReportLanguage.English), SupportReportLocalizer.Text("Latency", ReportLanguage.TraditionalChinese), latencyValue + " ms", BuildMetricNote(report, "latency", ReportLanguage.English), BuildMetricNote(report, "latency", ReportLanguage.TraditionalChinese))}}
          {{RenderMetricCard(SupportReportLocalizer.Text("PacketLoss", ReportLanguage.English), SupportReportLocalizer.Text("PacketLoss", ReportLanguage.TraditionalChinese), packetLossValue, BuildMetricNote(report, "loss", ReportLanguage.English), BuildMetricNote(report, "loss", ReportLanguage.TraditionalChinese))}}
          {{RenderMetricCard(SupportReportLocalizer.Text("DnsChecks", ReportLanguage.English), SupportReportLocalizer.Text("DnsChecks", ReportLanguage.TraditionalChinese), dnsValue, BuildMetricNote(report, "dns", ReportLanguage.English), BuildMetricNote(report, "dns", ReportLanguage.TraditionalChinese))}}
          {{RenderMetricCard(SupportReportLocalizer.Text("TcpChecks", ReportLanguage.English), SupportReportLocalizer.Text("TcpChecks", ReportLanguage.TraditionalChinese), tcpValue, BuildMetricNote(report, "tcp", ReportLanguage.English), BuildMetricNote(report, "tcp", ReportLanguage.TraditionalChinese))}}
        </div>
      </section>
      <aside class="section">
        <h2 style="font-size:16px;">{{Bilingual(SupportReportLocalizer.Text("RunDetails", ReportLanguage.English), SupportReportLocalizer.Text("RunDetails", ReportLanguage.TraditionalChinese))}}</h2>
        <div class="list">
          <div class="row"><span>{{Bilingual(SupportReportLocalizer.Text("Company", ReportLanguage.English), SupportReportLocalizer.Text("Company", ReportLanguage.TraditionalChinese))}}</span><span>{{Bilingual(report.Profile.CompanyName ?? "-", report.Profile.CompanyName ?? "-")}}</span></div>
          <div class="row"><span>{{Bilingual(SupportReportLocalizer.Text("Generated", ReportLanguage.English), SupportReportLocalizer.Text("Generated", ReportLanguage.TraditionalChinese))}}</span><span>{{Bilingual(generatedDisplay, generatedDisplay)}}</span></div>
          <div class="row"><span>{{Bilingual(SupportReportLocalizer.Text("SuggestedEscalation", ReportLanguage.English), SupportReportLocalizer.Text("SuggestedEscalation", ReportLanguage.TraditionalChinese))}}</span><span>{{Bilingual(suggestedEscalationEn, suggestedEscalationZh)}}</span></div>
          <div class="row"><span>{{Bilingual(SupportReportLocalizer.Text("NetworkPosture", ReportLanguage.English), SupportReportLocalizer.Text("NetworkPosture", ReportLanguage.TraditionalChinese))}}</span><span>{{Bilingual(networkPostureEn, networkPostureZh)}}</span></div>
        </div>
        <div class="button-row">
          <button type="button" class="btn" data-copy-key="itSummary" data-label-en="{{Encode(SupportReportLocalizer.Text("CopyItSummary", ReportLanguage.English))}}" data-label-zh="{{Encode(SupportReportLocalizer.Text("CopyItSummary", ReportLanguage.TraditionalChinese))}}">{{Encode(initialCopyItSummaryLabel)}}</button>
          <button type="button" class="btn" data-copy-key="incidentNote" data-label-en="{{Encode(SupportReportLocalizer.Text("CopyIncidentNote", ReportLanguage.English))}}" data-label-zh="{{Encode(SupportReportLocalizer.Text("CopyIncidentNote", ReportLanguage.TraditionalChinese))}}">{{Encode(initialCopyIncidentNoteLabel)}}</button>
        </div>
      </aside>
    </div>
    <section class="section alert">
      <h2>{{Bilingual(SupportReportLocalizer.Text("SuspiciousSignals", ReportLanguage.English), SupportReportLocalizer.Text("SuspiciousSignals", ReportLanguage.TraditionalChinese))}}</h2>
      {{RenderHighlightedSignals(highlightedSignalsEn, highlightedSignalsZh)}}
    </section>
    <div class="two-col">
      <section class="section">
        <h2 style="font-size:16px;">{{Bilingual(SupportReportLocalizer.Text("Observations", ReportLanguage.English), SupportReportLocalizer.Text("Observations", ReportLanguage.TraditionalChinese))}}</h2>
        {{RenderBilingualList(observationsEn, observationsZh)}}
      </section>
      <section class="section">
        <h2 style="font-size:16px;">{{Bilingual(SupportReportLocalizer.Text("NextSteps", ReportLanguage.English), SupportReportLocalizer.Text("NextSteps", ReportLanguage.TraditionalChinese))}}</h2>
        {{RenderBilingualList(nextStepsEn, nextStepsZh)}}
      </section>
    </div>
    <section class="section">
      <h2 style="font-size:16px;">{{Bilingual(SupportReportLocalizer.Text("RouteSummary", ReportLanguage.English), SupportReportLocalizer.Text("RouteSummary", ReportLanguage.TraditionalChinese))}}</h2>
      <p class="subtle" style="margin-top:8px;">{{Bilingual(routeEn.StatusSummary, routeZh.StatusSummary)}}</p>
      <p class="subtle" style="margin-top:8px;">{{Bilingual(routeEn.Narrative, routeZh.Narrative)}}</p>
      <div class="chart">{{BuildLatencyTrendSvg(report)}}</div>
      {{RenderSuspiciousHopTable(suspiciousRowsEn, suspiciousRowsZh)}}
    </section>
    <details{{(IsRouteDetailWorthOpening(report) ? " open" : string.Empty)}}>
      <summary>{{Bilingual(SupportReportLocalizer.Text("FullRouteDetail", ReportLanguage.English), SupportReportLocalizer.Text("FullRouteDetail", ReportLanguage.TraditionalChinese))}}</summary>
      <div class="detail-body">{{RenderFullRouteTable(report)}}</div>
    </details>
    <details>
      <summary>{{Bilingual(SupportReportLocalizer.Text("DnsChecks", ReportLanguage.English), SupportReportLocalizer.Text("DnsChecks", ReportLanguage.TraditionalChinese))}}</summary>
      <div class="detail-body">{{RenderDnsTable(report)}}</div>
    </details>
    <details>
      <summary>{{Bilingual(SupportReportLocalizer.Text("TcpChecks", ReportLanguage.English), SupportReportLocalizer.Text("TcpChecks", ReportLanguage.TraditionalChinese))}}</summary>
      <div class="detail-body">{{RenderTcpTable(report)}}</div>
    </details>
    <details>
      <summary>{{Bilingual(SupportReportLocalizer.Text("RawTracerouteOutput", ReportLanguage.English), SupportReportLocalizer.Text("RawTracerouteOutput", ReportLanguage.TraditionalChinese))}}</summary>
      <div class="detail-body"><pre>{{Encode(string.Join(Environment.NewLine, report.PrimaryRoute.RawTracerouteLines))}}</pre></div>
    </details>
  </div>
  <script type="application/json" id="copy-payload">{{EscapeForScriptTag(copyPayload)}}</script>
  <script>
    (() => {
      const root = document.documentElement;
      const languageButtons = document.querySelectorAll('[data-switch-language]');
      const copyButtons = document.querySelectorAll('[data-copy-key]');
      const copyPayload = JSON.parse(document.getElementById('copy-payload').textContent || '{}');
      const currentLanguage = () => root.classList.contains('lang-zh') ? 'zh-TW' : 'en';
      const setCopyLabels = () => {
        const language = currentLanguage();
        copyButtons.forEach((button) => {
          button.textContent = language === 'zh-TW' ? button.dataset.labelZh : button.dataset.labelEn;
        });
      };
      const applyLanguage = (language) => {
        root.classList.toggle('lang-en', language === 'en');
        root.classList.toggle('lang-zh', language === 'zh-TW');
        languageButtons.forEach((button) => button.setAttribute('aria-pressed', button.dataset.switchLanguage === language ? 'true' : 'false'));
        setCopyLabels();
      };
      languageButtons.forEach((button) => button.addEventListener('click', () => applyLanguage(button.dataset.switchLanguage)));
      copyButtons.forEach((button) => button.addEventListener('click', async () => {
        const language = currentLanguage();
        const payload = copyPayload?.[button.dataset.copyKey]?.[language];
        if (!payload) {
          return;
        }

        await navigator.clipboard.writeText(payload);
        const originalLabel = language === 'zh-TW' ? button.dataset.labelZh : button.dataset.labelEn;
        button.textContent = language === 'zh-TW' ? '{{Encode(SupportReportLocalizer.Text("Copied", ReportLanguage.TraditionalChinese))}}' : '{{Encode(SupportReportLocalizer.Text("Copied", ReportLanguage.English))}}';
        window.setTimeout(() => { button.textContent = originalLabel; }, 1200);
      }));
      applyLanguage(root.dataset.reportLanguage === 'zh-TW' ? 'zh-TW' : 'en');
    })();
  </script>
</body>
</html>
""";
    }

    private static IReadOnlyList<string> TakeSummaryItems(IReadOnlyList<string> items, int maxCount)
    {
        return items
            .Where(static item => !string.IsNullOrWhiteSpace(item))
            .Take(maxCount)
            .ToArray();
    }

    private static string RenderMetricCard(string englishLabel, string chineseLabel, string value, string englishNote, string chineseNote)
    {
        return $$"""
<div class="mini">
  <span class="label">{{Bilingual(englishLabel, chineseLabel)}}</span>
  <span class="value">{{Encode(value)}}</span>
  <div class="note">{{Bilingual(englishNote, chineseNote)}}</div>
</div>
""";
    }

    private static string RenderHighlightedSignals(IReadOnlyList<string> englishItems, IReadOnlyList<string> chineseItems)
    {
        if (englishItems.Count == 0 && chineseItems.Count == 0)
        {
            return $$"""<p class="subtle" style="margin-top:8px;">{{Bilingual(SupportReportLocalizer.Text("NoSuspiciousSignals", ReportLanguage.English), SupportReportLocalizer.Text("NoSuspiciousSignals", ReportLanguage.TraditionalChinese))}}</p>""";
        }

        return RenderBilingualList(englishItems, chineseItems);
    }

    private static string RenderBilingualList(IReadOnlyList<string> englishItems, IReadOnlyList<string> chineseItems)
    {
        var builder = new StringBuilder();
        builder.AppendLine("<ul>");
        for (var index = 0; index < Math.Max(englishItems.Count, chineseItems.Count); index++)
        {
            var english = index < englishItems.Count ? englishItems[index] : string.Empty;
            var chinese = index < chineseItems.Count ? chineseItems[index] : string.Empty;
            builder.AppendLine($"  <li>{Bilingual(english, chinese)}</li>");
        }

        builder.AppendLine("</ul>");
        return builder.ToString();
    }

    private static string RenderSuspiciousHopTable(IReadOnlyList<RouteSignalRow> englishRows, IReadOnlyList<RouteSignalRow> chineseRows)
    {
        var builder = new StringBuilder();
        builder.AppendLine("<table>");
        builder.AppendLine("  <thead>");
        builder.AppendLine($"    <tr><th>{Bilingual(SupportReportLocalizer.Text("Hop", ReportLanguage.English), SupportReportLocalizer.Text("Hop", ReportLanguage.TraditionalChinese))}</th><th>{Bilingual(SupportReportLocalizer.Text("Signal", ReportLanguage.English), SupportReportLocalizer.Text("Signal", ReportLanguage.TraditionalChinese))}</th><th>{Bilingual(SupportReportLocalizer.Text("Interpretation", ReportLanguage.English), SupportReportLocalizer.Text("Interpretation", ReportLanguage.TraditionalChinese))}</th></tr>");
        builder.AppendLine("  </thead>");
        builder.AppendLine("  <tbody>");

        if (englishRows.Count == 0 && chineseRows.Count == 0)
        {
            builder.AppendLine($"    <tr><td colspan=\"3\">{Bilingual(SupportReportLocalizer.Text("NoSuspiciousHops", ReportLanguage.English), SupportReportLocalizer.Text("NoSuspiciousHops", ReportLanguage.TraditionalChinese))}</td></tr>");
        }
        else
        {
            for (var index = 0; index < Math.Max(englishRows.Count, chineseRows.Count); index++)
            {
                var english = index < englishRows.Count ? englishRows[index] : RouteSignalRow.Empty;
                var chinese = index < chineseRows.Count ? chineseRows[index] : RouteSignalRow.Empty;
                builder.AppendLine($"    <tr><td>{Bilingual(english.HopLabel, chinese.HopLabel)}</td><td>{Bilingual(english.Signal, chinese.Signal)}</td><td>{Bilingual(english.Interpretation, chinese.Interpretation)}</td></tr>");
            }
        }

        builder.AppendLine("  </tbody>");
        builder.AppendLine("</table>");
        return builder.ToString();
    }

    private static string RenderFullRouteTable(SupportDiagnosticReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"<table><thead><tr><th>{Bilingual(SupportReportLocalizer.Text("Hop", ReportLanguage.English), SupportReportLocalizer.Text("Hop", ReportLanguage.TraditionalChinese))}</th><th>{Bilingual(SupportReportLocalizer.Text("Address", ReportLanguage.English), SupportReportLocalizer.Text("Address", ReportLanguage.TraditionalChinese))}</th><th>{Bilingual(SupportReportLocalizer.Text("Avg", ReportLanguage.English), SupportReportLocalizer.Text("Avg", ReportLanguage.TraditionalChinese))}</th><th>{Bilingual(SupportReportLocalizer.Text("Delta", ReportLanguage.English), SupportReportLocalizer.Text("Delta", ReportLanguage.TraditionalChinese))}</th><th>{Bilingual(SupportReportLocalizer.Text("Scope", ReportLanguage.English), SupportReportLocalizer.Text("Scope", ReportLanguage.TraditionalChinese))}</th><th>{Bilingual(SupportReportLocalizer.Text("Note", ReportLanguage.English), SupportReportLocalizer.Text("Note", ReportLanguage.TraditionalChinese))}</th></tr></thead><tbody>");

        if (report.PrimaryRoute.Hops.Count == 0)
        {
            builder.AppendLine($"<tr><td colspan=\"6\">{Bilingual(SupportReportLocalizer.Text("NoParsableHops", ReportLanguage.English), SupportReportLocalizer.Text("NoParsableHops", ReportLanguage.TraditionalChinese))}</td></tr>");
        }
        else
        {
            foreach (var hop in report.PrimaryRoute.Hops)
            {
                builder.AppendLine($"<tr><td>{hop.HopNumber}</td><td>{Encode(hop.DisplayAddress)}</td><td>{Encode(hop.AverageLatencyMs?.ToString() ?? "*")} ms</td><td>{Encode(hop.LatencyDeltaMs?.ToString() ?? "-")} ms</td><td>{Bilingual(hop.ScopeLabel, SupportReportLocalizer.GetHopScopeLabel(hop, ReportLanguage.TraditionalChinese))}</td><td>{Bilingual(hop.Note, SupportReportLocalizer.GetHopNote(hop, ReportLanguage.TraditionalChinese))}</td></tr>");
            }
        }

        builder.AppendLine("</tbody></table>");
        return builder.ToString();
    }

    private static string RenderDnsTable(SupportDiagnosticReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"<table><thead><tr><th>{Bilingual(SupportReportLocalizer.Text("Name", ReportLanguage.English), SupportReportLocalizer.Text("Name", ReportLanguage.TraditionalChinese))}</th><th>{Bilingual(SupportReportLocalizer.Text("Hostname", ReportLanguage.English), SupportReportLocalizer.Text("Hostname", ReportLanguage.TraditionalChinese))}</th><th>{Bilingual(SupportReportLocalizer.Text("Status", ReportLanguage.English), SupportReportLocalizer.Text("Status", ReportLanguage.TraditionalChinese))}</th><th>{Bilingual(SupportReportLocalizer.Text("Duration", ReportLanguage.English), SupportReportLocalizer.Text("Duration", ReportLanguage.TraditionalChinese))}</th><th>{Bilingual(SupportReportLocalizer.Text("Detail", ReportLanguage.English), SupportReportLocalizer.Text("Detail", ReportLanguage.TraditionalChinese))}</th></tr></thead><tbody>");

        if (report.DnsResults.Count == 0)
        {
            builder.AppendLine($"<tr><td colspan=\"5\">{Bilingual(SupportReportLocalizer.Text("NoDnsChecks", ReportLanguage.English), SupportReportLocalizer.Text("NoDnsChecks", ReportLanguage.TraditionalChinese))}</td></tr>");
        }
        else
        {
            foreach (var result in report.DnsResults)
            {
                var detail = result.Success ? string.Join(", ", result.Addresses) : result.ErrorMessage ?? "Lookup failed.";
                var englishStatus = result.Success ? SupportReportLocalizer.Text("Pass", ReportLanguage.English) : SupportReportLocalizer.Text("Fail", ReportLanguage.English);
                var chineseStatus = result.Success ? SupportReportLocalizer.Text("Pass", ReportLanguage.TraditionalChinese) : SupportReportLocalizer.Text("Fail", ReportLanguage.TraditionalChinese);
                builder.AppendLine($"<tr><td>{Encode(result.Name)}</td><td>{Encode(result.Hostname)}</td><td>{Bilingual(englishStatus, chineseStatus)}</td><td>{result.DurationMs} ms</td><td>{Encode(detail)}</td></tr>");
            }
        }

        builder.AppendLine("</tbody></table>");
        return builder.ToString();
    }

    private static string RenderTcpTable(SupportDiagnosticReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"<table><thead><tr><th>{Bilingual(SupportReportLocalizer.Text("Name", ReportLanguage.English), SupportReportLocalizer.Text("Name", ReportLanguage.TraditionalChinese))}</th><th>{Bilingual(SupportReportLocalizer.Text("Endpoint", ReportLanguage.English), SupportReportLocalizer.Text("Endpoint", ReportLanguage.TraditionalChinese))}</th><th>{Bilingual(SupportReportLocalizer.Text("Status", ReportLanguage.English), SupportReportLocalizer.Text("Status", ReportLanguage.TraditionalChinese))}</th><th>{Bilingual(SupportReportLocalizer.Text("Duration", ReportLanguage.English), SupportReportLocalizer.Text("Duration", ReportLanguage.TraditionalChinese))}</th><th>{Bilingual(SupportReportLocalizer.Text("Detail", ReportLanguage.English), SupportReportLocalizer.Text("Detail", ReportLanguage.TraditionalChinese))}</th></tr></thead><tbody>");

        if (report.TcpResults.Count == 0)
        {
            builder.AppendLine($"<tr><td colspan=\"5\">{Bilingual(SupportReportLocalizer.Text("NoTcpChecks", ReportLanguage.English), SupportReportLocalizer.Text("NoTcpChecks", ReportLanguage.TraditionalChinese))}</td></tr>");
        }
        else
        {
            foreach (var result in report.TcpResults)
            {
                var endpointLabel = $"{result.Host}:{result.Port}";
                var englishStatus = result.Success ? SupportReportLocalizer.Text("Pass", ReportLanguage.English) : SupportReportLocalizer.Text("Fail", ReportLanguage.English);
                var chineseStatus = result.Success ? SupportReportLocalizer.Text("Pass", ReportLanguage.TraditionalChinese) : SupportReportLocalizer.Text("Fail", ReportLanguage.TraditionalChinese);
                var englishDetail = result.Success ? "Connection established." : result.ErrorMessage ?? "Connection failed.";
                var chineseDetail = result.Success ? "連線已建立。" : result.ErrorMessage ?? "連線失敗。";
                builder.AppendLine($"<tr><td>{Encode(result.Name)}</td><td>{Encode(endpointLabel)}</td><td>{Bilingual(englishStatus, chineseStatus)}</td><td>{result.DurationMs} ms</td><td>{Bilingual(englishDetail, chineseDetail)}</td></tr>");
            }
        }

        builder.AppendLine("</tbody></table>");
        return builder.ToString();
    }

    private static IReadOnlyList<string> BuildHighlightedSignals(SupportDiagnosticReport report, string language)
    {
        var signals = new List<string>();
        var route = report.PrimaryRoute;
        var firstSpike = route.Hops.FirstOrDefault(static hop => hop.SuspectedSpike);
        var noteworthyHop = firstSpike ?? GetMostNoteworthyLatencyStep(route);
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

        if (noteworthyHop is not null)
        {
            var scopeLabel = zh
                ? SupportReportLocalizer.GetHopScopeLabel(noteworthyHop, ReportLanguage.TraditionalChinese)
                : noteworthyHop.ScopeLabel;
            var delta = noteworthyHop.LatencyDeltaMs ?? 0;

            signals.Add(noteworthyHop.SuspectedSpike
                ? (zh
                    ? $"延遲階梯從第 {noteworthyHop.HopNumber} 跳開始，位置接近 {scopeLabel}。"
                    : $"Latency begins stepping up around hop {noteworthyHop.HopNumber} near {scopeLabel}.")
                : (zh
                    ? $"第 {noteworthyHop.HopNumber} 跳可看到約 +{delta} ms 的延遲增加，但後續 hop 仍需要一起判讀。"
                    : $"A smaller latency step-up of about +{delta} ms is visible around hop {noteworthyHop.HopNumber}, but downstream hops still need to be read together."));
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

        if (signals.Count == 0)
        {
            signals.Add(zh
                ? "沒有看到高風險異常，但這次 capture 仍可作為後續比對的基準。"
                : "No high-risk anomaly was confirmed, but this capture is still useful as a baseline for comparison.");
        }

        return signals;
    }

    private static IReadOnlyList<RouteSignalRow> BuildSuspiciousHopRows(SupportDiagnosticReport report, string language)
    {
        var zh = ReportLanguage.IsTraditionalChinese(language);
        var rows = new List<RouteSignalRow>();
        var suspiciousHops = report.PrimaryRoute.Hops
            .Where(static hop => hop.SuspectedSpike || hop.IsTimeout)
            .Take(6);

        foreach (var hop in suspiciousHops)
        {
            var downstreamStillResponds = report.PrimaryRoute.Hops.Any(candidate =>
                candidate.HopNumber > hop.HopNumber &&
                !candidate.IsTimeout &&
                candidate.AverageLatencyMs.HasValue);

            string signal;
            string interpretation;

            if (hop.SuspectedSpike && hop.LatencyDeltaMs is int delta)
            {
                signal = zh ? $"+{delta} ms 延遲階梯" : $"+{delta} ms step-up";
                interpretation = downstreamStillResponds
                    ? zh ? "值得注意，但單靠這個階梯還不足以直接判定瓶頸。" : "Worth noting, but not enough alone to prove a bottleneck."
                    : zh ? "建議和後續 hop 的狀態一起判讀。" : "Review together with downstream hops.";
            }
            else
            {
                signal = zh ? "中間 hop timeout" : "Intermediate timeout";
                interpretation = downstreamStillResponds
                    ? zh ? "較像 ICMP 回應限制，不一定代表轉送失敗。" : "Likely ICMP reply limiting rather than forwarding failure."
                    : zh ? "需要和後續 hop 的結果一起判讀。" : "Interpret together with downstream evidence.";
            }

            rows.Add(new RouteSignalRow(hop.HopNumber.ToString(), signal, interpretation));
        }

        if (rows.Count == 0)
        {
            var noteworthyHop = GetMostNoteworthyLatencyStep(report.PrimaryRoute);
            if (noteworthyHop is not null && noteworthyHop.LatencyDeltaMs is int delta)
            {
                rows.Add(new RouteSignalRow(
                    noteworthyHop.HopNumber.ToString(),
                    zh ? $"+{delta} ms 較小延遲變化" : $"+{delta} ms smaller step-up",
                    zh
                        ? "值得記錄，但目前還不足以單獨視為故障 hop。"
                        : "Worth recording, but not strong enough on its own to call a fault hop."));
            }
        }

        return rows;
    }

    private static string BuildLatencyTrendSvg(SupportDiagnosticReport report)
    {
        var points = report.PrimaryRoute.Hops
            .Where(static hop => hop.AverageLatencyMs.HasValue)
            .Select(static hop => new
            {
                hop.HopNumber,
                Latency = hop.AverageLatencyMs!.Value,
                hop.SuspectedSpike,
                hop.IsTimeout
            })
            .ToArray();

        if (points.Length == 0)
        {
            return string.Empty;
        }

        const double width = 800;
        const double height = 140;
        const double leftPadding = 24;
        const double rightPadding = 24;
        const double topPadding = 18;
        const double bottomPadding = 12;
        var maxLatency = Math.Max(points.Max(static point => point.Latency), 20);
        var availableWidth = width - leftPadding - rightPadding;
        var availableHeight = height - topPadding - bottomPadding;

        var coordinates = points.Select((point, index) =>
        {
            var x = points.Length == 1
                ? width / 2
                : leftPadding + availableWidth * index / (points.Length - 1d);
            var y = height - bottomPadding - (point.Latency / (double)maxLatency * availableHeight);
            return new
            {
                point.HopNumber,
                point.SuspectedSpike,
                point.IsTimeout,
                X = x,
                Y = y
            };
        }).ToArray();

        var polyline = string.Join(" ", coordinates.Select(point => $"{point.X:0.##},{point.Y:0.##}"));
        var circles = string.Join(
            Environment.NewLine,
            coordinates
                .Where(static point => point.SuspectedSpike || point.IsTimeout)
                .Select(point => $"<circle cx=\"{point.X:0.##}\" cy=\"{point.Y:0.##}\" r=\"5\" fill=\"#d97706\" />"));

        return $$"""
<svg viewBox="0 0 800 140" preserveAspectRatio="none" aria-hidden="true">
  <polyline fill="none" stroke="#2563eb" stroke-width="3" points="{{polyline}}" />
  {{circles}}
</svg>
""";
    }

    private static string BuildRecommendedNextAction(LocalizedAssessmentView assessment, string language)
    {
        var nextAction = assessment.Recommendations.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(nextAction))
        {
            return nextAction;
        }

        return ReportLanguage.IsTraditionalChinese(language)
            ? "建議在問題發生時再收一次報告。"
            : "Collect another capture while the issue is actively happening.";
    }

    private static string BuildSuggestedEscalation(SupportDiagnosticReport report, string language)
    {
        var zh = ReportLanguage.IsTraditionalChinese(language);

        return report.Assessment.ScenarioKey switch
        {
            DiagnosticAssessmentEngine.ScenarioLocalDnsOrInitialConnectivity => zh ? "本地網路 / DNS / VPN 前置連線" : "Local network / DNS / VPN pre-connect",
            DiagnosticAssessmentEngine.ScenarioLocalNetworkOrWifi => zh ? "本地網路 / Wi-Fi" : "Local network / Wi-Fi",
            DiagnosticAssessmentEngine.ScenarioIspOrAccessNetwork => zh ? "ISP / 接入網路" : "ISP / access network",
            DiagnosticAssessmentEngine.ScenarioInternetTransitPath => zh ? "ISP / 上游路徑" : "ISP / upstream path",
            DiagnosticAssessmentEngine.ScenarioCompanyEdgeServiceTcpFailure or DiagnosticAssessmentEngine.ScenarioCompanyNetworkOrDestinationService => zh ? "公司邊界 / 目標服務" : "Company edge / destination service",
            DiagnosticAssessmentEngine.ScenarioNoClearNetworkFaultDetected => zh ? "應用程式 / VPN / 端點" : "App / VPN / endpoint",
            _ => zh ? "先補收更多證據" : "Collect another capture first"
        };
    }

    private static string BuildNetworkPosture(SupportDiagnosticReport report, string language)
    {
        var zh = ReportLanguage.IsTraditionalChinese(language);

        return report.Assessment.OverallStatusLabel switch
        {
            "Healthy" => zh ? "可作為健康基準" : "Healthy baseline candidate",
            "Action Needed" => zh ? "可帶著目前證據升級" : "Escalate with current evidence",
            _ => zh ? "需要再補一次 capture" : "Needs follow-up capture"
        };
    }

    private static string BuildMetricNote(SupportDiagnosticReport report, string metric, string language)
    {
        var zh = ReportLanguage.IsTraditionalChinese(language);
        var route = report.PrimaryRoute;
        var dnsPassed = report.DnsResults.Count(static result => result.Success);
        var tcpPassed = report.TcpResults.Count(static result => result.Success);

        return metric switch
        {
            "latency" => route.PingSummary.AverageRoundTripMs is null
                ? (zh ? "這次沒有可用的平均延遲。" : "No end-to-end latency average was captured in this run.")
                : report.PrimaryRoute.PingSummary.PacketLossPercent == 0
                    ? (zh ? "端對端延遲目前看起來穩定。" : "End-to-end latency looks stable in this run.")
                    : (zh ? "請和下方 route summary 一起判讀。" : "Review together with the route summary below."),
            "loss" => route.PingSummary.PacketLossPercent == 0
                ? (zh ? "沒有看到端對端封包遺失。" : "No end-to-end packet loss was observed.")
                : (zh ? "這次有觀察到封包遺失。" : "Packet loss was observed in this run."),
            "dns" => report.DnsResults.Count == 0
                ? (zh ? "這次沒有設定 DNS 檢查。" : "No DNS checks were configured.")
                : dnsPassed == report.DnsResults.Count
                    ? (zh ? "目標名稱解析成功。" : "Target resolution succeeded.")
                    : (zh ? "有一個以上的 DNS 查詢失敗。" : "One or more DNS lookups failed."),
            "tcp" => report.TcpResults.Count == 0
                ? (zh ? "這次沒有設定 TCP 檢查。" : "No TCP checks were configured.")
                : tcpPassed == report.TcpResults.Count
                    ? (zh ? "目標服務埠可達。" : "Configured service ports were reachable.")
                    : (zh ? "有一個以上的 TCP 端點失敗。" : "One or more TCP endpoints failed."),
            _ => string.Empty
        };
    }

    private static string BuildItSummaryCopy(SupportDiagnosticReport report, LocalizedAssessmentView assessment, LocalizedRouteView route, string nextAction, string language)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"{SupportReportLocalizer.Text("Status", language)}: {assessment.OverallStatusLabel}");
        builder.AppendLine($"{SupportReportLocalizer.Text("PossibleFaultDomain", language)}: {assessment.FaultDomain}");
        builder.AppendLine($"{SupportReportLocalizer.Text("OverallFinding", language)}: {assessment.UserSummary}");
        builder.AppendLine($"{SupportReportLocalizer.Text("Interpretation", language)}: {assessment.ItSummary}");
        builder.AppendLine($"{SupportReportLocalizer.Text("RouteSummary", language)}: {route.StatusSummary}");
        builder.AppendLine($"{SupportReportLocalizer.Text("RecommendedNextAction", language)}: {nextAction}");
        builder.AppendLine($"{SupportReportLocalizer.Text("ExecutionId", language)}: {report.ExecutionId}");
        return builder.ToString().TrimEnd();
    }

    private static string BuildIncidentNoteCopy(SupportDiagnosticReport report, LocalizedAssessmentView assessment, LocalizedRouteView route, string nextAction, string language)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"{SupportReportLocalizer.Text("Target", language)}: {report.Profile.TargetHost}");
        builder.AppendLine($"{SupportReportLocalizer.Text("Machine", language)}: {report.MachineName}");
        builder.AppendLine($"{SupportReportLocalizer.Text("Status", language)}: {assessment.OverallStatusLabel}");
        builder.AppendLine($"{SupportReportLocalizer.Text("PossibleFaultDomain", language)}: {assessment.FaultDomain}");
        builder.AppendLine($"{SupportReportLocalizer.Text("Observations", language)}: {assessment.EvidenceHighlights.FirstOrDefault() ?? route.StatusSummary}");
        builder.AppendLine($"{SupportReportLocalizer.Text("RecommendedNextAction", language)}: {nextAction}");
        return builder.ToString().TrimEnd();
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

    private static string EscapeForScriptTag(string value)
    {
        return value.Replace("</script>", "<\\/script>", StringComparison.OrdinalIgnoreCase);
    }

    private static RouteHop? GetMostNoteworthyLatencyStep(RouteDiagnosticReport route)
    {
        return route.Hops
            .Where(static hop => !hop.SuspectedSpike && !hop.IsTimeout && (hop.LatencyDeltaMs ?? 0) >= 10)
            .OrderByDescending(static hop => hop.LatencyDeltaMs ?? 0)
            .FirstOrDefault();
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

    private static bool IsRouteDetailWorthOpening(SupportDiagnosticReport report)
    {
        return !string.Equals(report.Assessment.OverallStatusLabel, "Healthy", StringComparison.OrdinalIgnoreCase)
            || report.PrimaryRoute.PingSummary.PacketLossPercent > 0
            || report.PrimaryRoute.Hops.Any(static hop => hop.SuspectedSpike || hop.IsTimeout);
    }

    private readonly record struct RouteSignalRow(string HopLabel, string Signal, string Interpretation)
    {
        public static RouteSignalRow Empty => new(string.Empty, string.Empty, string.Empty);
    }
}
