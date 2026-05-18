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
        var language = ReportLanguage.Normalize(report.Profile.PreferredLanguage);
        var builder = new StringBuilder();
        builder.AppendLine(SupportReportLocalizer.Text("ReportTitle", language));
        builder.AppendLine("============================");
        builder.AppendLine($"{SupportReportLocalizer.Text("ExecutionId", language),-14}: {report.ExecutionId}");
        builder.AppendLine($"{SupportReportLocalizer.Text("Generated", language),-14}: {report.GeneratedAtUtc:yyyy-MM-dd HH:mm:ss 'UTC'}");
        builder.AppendLine($"{SupportReportLocalizer.Text("Machine", language),-14}: {report.MachineName}");
        builder.AppendLine($"{SupportReportLocalizer.Text("Target", language),-14}: {report.Profile.TargetHost}");
        builder.AppendLine($"{SupportReportLocalizer.Text("Duration", language),-14}: {report.DurationMs} ms");
        builder.AppendLine();
        builder.AppendLine(SupportReportLocalizer.Text("Overview", language));
        builder.AppendLine("--------");
        builder.AppendLine(LocalizeOverview(report.SignalSummary.Overview, language));
        builder.AppendLine();
        builder.AppendLine(SupportReportLocalizer.Text("Signals", language));
        builder.AppendLine("-------");

        foreach (var signal in report.SignalSummary.Signals)
        {
            builder.AppendLine($"- {LocalizeSignal(signal, language)}");
        }

        builder.AppendLine();
        builder.AppendLine(SupportReportLocalizer.Text("RouteSummary", language));
        builder.AppendLine("-------------");
        builder.AppendLine($"{SupportReportLocalizer.Text("AverageLatency", language),-14}: {report.PrimaryRoute.PingSummary.AverageRoundTripMs?.ToString() ?? "-"} ms");
        builder.AppendLine($"{SupportReportLocalizer.Text("Loss", language),-14}: {report.PrimaryRoute.PingSummary.PacketLossPercent}%");
        builder.AppendLine($"{SupportReportLocalizer.Text("Jitter", language),-14}: {report.PrimaryRoute.PingSummary.JitterMs?.ToString() ?? "-"} ms");
        builder.AppendLine($"{SupportReportLocalizer.Text("HopsParsed", language),-14}: {report.PrimaryRoute.Hops.Count}");

        return builder.ToString();
    }

    public static string ToHtml(SupportDiagnosticReport report)
    {
        return SupportDiagnosticHtmlFormatter.Render(report);
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

    public static string LocalizeOverview(string overview, string? language)
    {
        if (!ReportLanguage.IsTraditionalChinese(language))
        {
            return overview;
        }

        return overview
            .Replace("Captured ping, traceroute,", "已收集 ping、traceroute、", StringComparison.Ordinal)
            .Replace("、 DNS", "、DNS", StringComparison.Ordinal)
            .Replace(", TCP", "、TCP", StringComparison.Ordinal)
            .Replace(". Average", "。Average", StringComparison.Ordinal)
            .Replace("Average latency:", "平均延遲:", StringComparison.Ordinal)
            .Replace("packet loss:", "封包遺失:", StringComparison.Ordinal)
            .Replace("hops parsed:", "已解析 hops:", StringComparison.Ordinal)
            .Replace("ms; ", "ms；", StringComparison.Ordinal)
            .Replace("%; ", "%；", StringComparison.Ordinal);
    }

    public static string LocalizeSignal(string signal, string? language)
    {
        if (!ReportLanguage.IsTraditionalChinese(language))
        {
            return signal;
        }

        return signal
            .Replace("Ping replies:", "Ping 回覆:", StringComparison.Ordinal)
            .Replace("average latency:", "平均延遲:", StringComparison.Ordinal)
            .Replace("packet loss:", "封包遺失:", StringComparison.Ordinal)
            .Replace("Traceroute hops parsed:", "Traceroute 已解析 hops:", StringComparison.Ordinal)
            .Replace("Traceroute produced no parsable hops.", "Traceroute 沒有產生可解析的 hop。", StringComparison.Ordinal)
            .Replace("Traceroute timeout hops:", "Traceroute timeout hops:", StringComparison.Ordinal)
            .Replace("Latency step-ups observed:", "觀察到延遲階梯:", StringComparison.Ordinal)
            .Replace("DNS checks passed:", "DNS 檢查通過:", StringComparison.Ordinal)
            .Replace("DNS failures:", "DNS 失敗:", StringComparison.Ordinal)
            .Replace("TCP checks passed:", "TCP 檢查通過:", StringComparison.Ordinal)
            .Replace("TCP failures:", "TCP 失敗:", StringComparison.Ordinal);
    }

    internal static string Encode(string value)
    {
        return WebUtility.HtmlEncode(value);
    }

    private static string SanitizeFileName(string value)
    {
        return string.Join("-", value.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries)).Trim('-');
    }
}
