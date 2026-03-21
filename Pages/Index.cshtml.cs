using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using RouteAnalyzer.Models;
using RouteAnalyzer.Options;
using RouteAnalyzer.Services;
using System.Text;
using System.Text.Json;

namespace RouteAnalyzer.Pages;

public class IndexModel : PageModel
{
    private readonly NetworkRouteDiagnosticService _diagnosticService;
    private readonly RouteAnalyzerOptions _options;

    public IndexModel(
        NetworkRouteDiagnosticService diagnosticService,
        IOptions<RouteAnalyzerOptions> options)
    {
        _diagnosticService = diagnosticService;
        _options = options.Value;
    }

    [BindProperty]
    public string TargetHost { get; set; } = string.Empty;

    [BindProperty]
    public int PingCount { get; set; } = 4;

    public RouteDiagnosticReport? Report { get; private set; }

    public void OnGet()
    {
        TargetHost = _options.DefaultTarget;
        PingCount = _options.DefaultPingCount;
    }

    public async Task OnPostAsync()
    {
        PingCount = Math.Clamp(PingCount, 3, 10);

        if (string.IsNullOrWhiteSpace(TargetHost))
        {
            ModelState.AddModelError(string.Empty, "請輸入要測試的目標主機、網域或 IP。");
            return;
        }

        var normalizedTarget = NormalizeTarget(TargetHost);
        TargetHost = normalizedTarget;
        Report = await _diagnosticService.AnalyzeAsync(normalizedTarget, PingCount, HttpContext.RequestAborted);
    }

    public async Task<IActionResult> OnPostExportJsonAsync()
    {
        var report = await BuildReportOrNullAsync();
        if (report is null)
        {
            return Page();
        }

        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        var fileName = $"route-report-{SanitizeFileName(report.TargetHost)}.json";
        return File(Encoding.UTF8.GetBytes(json), "application/json", fileName);
    }

    public async Task<IActionResult> OnPostExportCsvAsync()
    {
        var report = await BuildReportOrNullAsync();
        if (report is null)
        {
            return Page();
        }

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

        var fileName = $"route-report-{SanitizeFileName(report.TargetHost)}.csv";
        return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", fileName);
    }

    public string GetHopClass(RouteHop hop)
    {
        if (hop.IsTimeout)
        {
            return "hop-card hop-timeout";
        }

        if (hop.SuspectedSpike)
        {
            return "hop-card hop-spike";
        }

        return "hop-card";
    }

    public string GetBarWidth(RouteHop hop)
    {
        var maxLatency = Report?.Hops
            .Where(static item => item.AverageLatencyMs.HasValue)
            .Select(static item => item.AverageLatencyMs!.Value)
            .DefaultIfEmpty(1)
            .Max() ?? 1;

        if (!hop.AverageLatencyMs.HasValue || maxLatency <= 0)
        {
            return "8%";
        }

        var ratio = (double)hop.AverageLatencyMs.Value / maxLatency;
        var width = Math.Clamp((int)Math.Round(ratio * 100), 8, 100);
        return $"{width}%";
    }

    public string GetGeoX(RouteHop hop)
    {
        var longitude = hop.GeoDetails?.Longitude;
        if (!longitude.HasValue)
        {
            return "8%";
        }

        var x = ((longitude.Value + 180d) / 360d) * 100d;
        return $"{Math.Clamp(x, 5d, 95d):0.##}%";
    }

    public string GetGeoY(RouteHop hop)
    {
        var latitude = hop.GeoDetails?.Latitude;
        if (!latitude.HasValue)
        {
            return $"{12 + Math.Min(hop.HopNumber * 10, 70)}%";
        }

        var normalized = (90d - latitude.Value) / 180d;
        var y = normalized * 100d;
        return $"{Math.Clamp(y, 10d, 82d):0.##}%";
    }

    private async Task<RouteDiagnosticReport?> BuildReportOrNullAsync()
    {
        PingCount = Math.Clamp(PingCount, 3, 10);

        if (string.IsNullOrWhiteSpace(TargetHost))
        {
            ModelState.AddModelError(string.Empty, "請輸入要測試的目標主機、網域或 IP。");
            return null;
        }

        var normalizedTarget = NormalizeTarget(TargetHost);
        TargetHost = normalizedTarget;
        Report = await _diagnosticService.AnalyzeAsync(normalizedTarget, PingCount, HttpContext.RequestAborted);
        return Report;
    }

    private static string NormalizeTarget(string rawValue)
    {
        var candidate = rawValue.Trim();

        if (Uri.TryCreate(candidate, UriKind.Absolute, out var uri))
        {
            return uri.Host;
        }

        return candidate;
    }

    private static string SanitizeFileName(string value)
    {
        return string.Join("-", value.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries)).Trim();
    }

    private static string Csv(string value)
    {
        var escaped = value.Replace("\"", "\"\"");
        return $"\"{escaped}\"";
    }
}
