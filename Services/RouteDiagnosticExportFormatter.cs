using System.Text;
using System.Text.Json;
using RouteAnalyzer.Models;

namespace RouteAnalyzer.Services;

public static class RouteDiagnosticExportFormatter
{
    public static string ToJson(RouteDiagnosticReport report)
    {
        return JsonSerializer.Serialize(report, new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }

    public static string ToCsv(RouteDiagnosticReport report)
    {
        var csv = new StringBuilder();
        csv.AppendLine("Hop,Address,Scope,AverageLatencyMs,LatencyDeltaMs,Location,ISP,ASN,Timezone,ReverseDns,Note");

        foreach (var hop in report.Hops)
        {
            csv.AppendLine(string.Join(",",
                Csv(hop.HopNumber.ToString()),
                Csv(hop.DisplayAddress),
                Csv(hop.ScopeLabel),
                Csv(hop.AverageLatencyMs?.ToString() ?? string.Empty),
                Csv(hop.LatencyDeltaMs?.ToString() ?? string.Empty),
                Csv(hop.GeoDetails?.Summary ?? string.Empty),
                Csv(hop.GeoDetails?.Isp ?? string.Empty),
                Csv(hop.GeoDetails?.Asn ?? string.Empty),
                Csv(hop.GeoDetails?.Timezone ?? string.Empty),
                Csv(hop.ReverseDns ?? string.Empty),
                Csv(hop.Note)));
        }

        return csv.ToString();
    }

    public static string ToText(RouteDiagnosticReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Execution : {report.ExecutionId}");
        builder.AppendLine($"Target    : {report.TargetHost}");
        builder.AppendLine($"Status    : {report.StatusLabel}");
        builder.AppendLine($"Summary   : {report.StatusSummary}");
        builder.AppendLine($"Ping Avg  : {report.PingSummary.AverageRoundTripMs?.ToString() ?? "-"} ms");
        builder.AppendLine($"Loss      : {report.PingSummary.PacketLossPercent}% ({report.PingSummary.Received}/{report.PingSummary.Sent})");
        builder.AppendLine($"Jitter    : {report.PingSummary.JitterMs?.ToString() ?? "-"} ms");
        builder.AppendLine($"Duration  : {report.DurationMs} ms");
        builder.AppendLine($"Runtime   : {report.RuntimeSummary}");
        builder.AppendLine();
        builder.AppendLine("Hops");
        builder.AppendLine("----");

        foreach (var hop in report.Hops)
        {
            var latency = hop.AverageLatencyMs?.ToString() ?? "*";
            var delta = hop.LatencyDeltaMs?.ToString() ?? "-";
            builder.AppendLine($"#{hop.HopNumber,2} {hop.DisplayAddress,-40} {latency,5} ms  d{delta,4}  {hop.ScopeLabel}");
        }

        if (!string.IsNullOrWhiteSpace(report.SuspectedIssue))
        {
            builder.AppendLine();
            builder.AppendLine($"Suspected issue: {report.SuspectedIssue}");
        }

        return builder.ToString();
    }

    public static string BuildReportFileName(string targetHost, string extension)
    {
        var safeTarget = SanitizeFileName(targetHost);
        return $"route-report-{safeTarget}.{extension.Trim().TrimStart('.')}";
    }

    private static string Csv(string value)
    {
        var escaped = value.Replace("\"", "\"\"");
        return $"\"{escaped}\"";
    }

    private static string SanitizeFileName(string value)
    {
        return string.Join("-", value.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries)).Trim();
    }
}
