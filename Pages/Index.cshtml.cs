using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using RouteAnalyzer.Models;
using RouteAnalyzer.Options;
using RouteAnalyzer.Services;

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
    [StringLength(255)]
    public string TargetHost { get; set; } = string.Empty;

    [BindProperty]
    [Range(RouteAnalyzerOptions.MinPingCount, RouteAnalyzerOptions.MaxPingCount)]
    public int PingCount { get; set; } = RouteAnalyzerOptions.MinPingCount + 1;

    public RouteDiagnosticReport? Report { get; private set; }

    public int TimeoutCount => Report?.Hops.Count(static hop => hop.IsTimeout) ?? 0;

    public int SpikeCount => Report?.Hops.Count(static hop => hop.SuspectedSpike) ?? 0;

    public int GeoResolvedCount => Report?.Hops.Count(static hop => !string.IsNullOrWhiteSpace(hop.GeoDetails?.Summary)) ?? 0;

    public int GeoLookupMissCount => Report?.Hops.Count(static hop =>
        !hop.IsTimeout
        && hop.ScopeLabel is "Public hop" or "Transit hop" or "Access / ISP edge"
        && string.IsNullOrWhiteSpace(hop.GeoDetails?.Summary)) ?? 0;

    public int PublicHopCount => Report?.Hops.Count(static hop =>
        !hop.IsTimeout
        && hop.ScopeLabel is "Public hop" or "Transit hop" or "Access / ISP edge" or "Destination") ?? 0;

    public int CoordinateHopCount => Report?.Hops.Count(static hop => hop.GeoDetails?.HasCoordinates == true) ?? 0;

    public int GeoCoveragePercent => PublicHopCount == 0
        ? 0
        : (int)Math.Round((double)GeoResolvedCount / PublicHopCount * 100);

    public string GeoCoverageDisplay => PublicHopCount == 0
        ? "n/a"
        : $"{GeoResolvedCount}/{PublicHopCount} ({GeoCoveragePercent}%)";

    public string JitterDisplay => Report?.PingSummary.JitterMs is int jitter
        ? $"{jitter} ms"
        : "-";

    public string PingRangeDisplay
    {
        get
        {
            if (Report?.PingSummary.MinimumRoundTripMs is int min
                && Report.PingSummary.MaximumRoundTripMs is int max)
            {
                return $"{min}-{max} ms";
            }

            return "-";
        }
    }

    public string GeneratedAtDisplay => Report is null
        ? "-"
        : Report.GeneratedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss 'UTC'zzz", CultureInfo.InvariantCulture);

    public void OnGet()
    {
        TargetHost = _options.DefaultTarget;
        PingCount = _options.DefaultPingCount;
    }

    public async Task OnPostAsync()
    {
        PingCount = Math.Clamp(PingCount, RouteAnalyzerOptions.MinPingCount, RouteAnalyzerOptions.MaxPingCount);

        if (!TryNormalizeTarget(TargetHost, out var normalizedTarget))
        {
            ModelState.AddModelError(string.Empty, "Enter a valid hostname, IP address, or URL.");
            return;
        }

        if (!ModelState.IsValid)
        {
            return;
        }

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

    public double GetGeoXValue(RouteHop hop)
    {
        var longitude = hop.GeoDetails?.Longitude;
        if (!longitude.HasValue)
        {
            return GetFallbackX(hop);
        }

        var x = ((longitude.Value + 180d) / 360d) * 100d;
        return Math.Clamp(x, 14d, 86d);
    }

    public string GetGeoX(RouteHop hop)
    {
        return $"{GetGeoXValue(hop):0.##}%";
    }

    public double GetGeoYValue(RouteHop hop)
    {
        var latitude = hop.GeoDetails?.Latitude;
        if (!latitude.HasValue)
        {
            return GetFallbackY(hop);
        }

        var normalized = (90d - latitude.Value) / 180d;
        var y = normalized * 100d;
        return Math.Clamp(y, 14d, 80d);
    }

    public string GetGeoY(RouteHop hop)
    {
        return $"{GetGeoYValue(hop):0.##}%";
    }

    public string GetMapNodeClass(RouteHop hop)
    {
        var classes = new List<string> { "map-node" };

        if (hop.SuspectedSpike)
        {
            classes.Add("map-node-spike");
        }

        if (hop.ScopeLabel is "LAN / Gateway" or "Private network")
        {
            classes.Add("map-node-private");
        }
        else if (hop.ScopeLabel == "Destination")
        {
            classes.Add("map-node-destination");
        }

        if (string.IsNullOrWhiteSpace(hop.GeoDetails?.Summary))
        {
            classes.Add("map-node-ungrounded");
        }

        return string.Join(" ", classes);
    }

    public string GetMapLineClass(RouteHop fromHop, RouteHop toHop)
    {
        if (toHop.SuspectedSpike)
        {
            return "map-line map-line-spike";
        }

        if (fromHop.ScopeLabel is "LAN / Gateway" or "Private network"
            && toHop.ScopeLabel is "LAN / Gateway" or "Private network")
        {
            return "map-line map-line-private";
        }

        return "map-line";
    }

    public string GetStatusClass()
    {
        return Report?.StatusLabel switch
        {
            "Critical" => "status-pill status-critical",
            "Investigate" => "status-pill status-investigate",
            "Observe" => "status-pill status-observe",
            "Unsupported" => "status-pill status-unsupported",
            _ => "status-pill status-stable"
        };
    }

    private double GetFallbackX(RouteHop hop)
    {
        var visibleHops = Report?.Hops.Where(static item => !item.IsTimeout).ToList() ?? [];
        if (visibleHops.Count <= 1)
        {
            return 18d;
        }

        var hopIndex = visibleHops.FindIndex(item => item.HopNumber == hop.HopNumber);
        if (hopIndex < 0)
        {
            return 18d;
        }

        var ratio = (double)hopIndex / Math.Max(visibleHops.Count - 1, 1);
        return 14d + (ratio * 72d);
    }

    private double GetFallbackY(RouteHop hop)
    {
        var visibleHops = Report?.Hops.Where(static item => !item.IsTimeout).ToList() ?? [];
        var hopIndex = visibleHops.FindIndex(item => item.HopNumber == hop.HopNumber);
        var stagger = hopIndex < 0 ? 0d : ((hopIndex % 3) - 1) * 6d;

        var baseline = hop.ScopeLabel switch
        {
            "LAN / Gateway" => 24d,
            "Private network" => 36d,
            "Access / ISP edge" => 48d,
            "Public hop" => 58d,
            "Transit hop" => 68d,
            "Destination" => 52d,
            _ => 60d
        };

        if (hop.SuspectedSpike)
        {
            baseline -= 10d;
        }

        return Math.Clamp(baseline + stagger, 18d, 82d);
    }

    private async Task<RouteDiagnosticReport?> BuildReportOrNullAsync()
    {
        PingCount = Math.Clamp(PingCount, RouteAnalyzerOptions.MinPingCount, RouteAnalyzerOptions.MaxPingCount);

        if (!TryNormalizeTarget(TargetHost, out var normalizedTarget))
        {
            ModelState.AddModelError(string.Empty, "Enter a valid hostname, IP address, or URL.");
            return null;
        }

        if (!ModelState.IsValid)
        {
            return null;
        }

        TargetHost = normalizedTarget;
        Report = await _diagnosticService.AnalyzeAsync(normalizedTarget, PingCount, HttpContext.RequestAborted);
        return Report;
    }

    private static bool TryNormalizeTarget(string rawValue, out string normalizedTarget)
    {
        normalizedTarget = string.Empty;
        var candidate = rawValue.Trim();

        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        if (Uri.TryCreate(candidate, UriKind.Absolute, out var uri))
        {
            candidate = uri.Host;
        }

        candidate = candidate.Trim().TrimEnd('.');

        if (candidate.Length == 0
            || candidate.Length > 255
            || candidate.Contains(' ')
            || candidate.Contains('/')
            || candidate.Contains('\\'))
        {
            return false;
        }

        if (IPAddress.TryParse(candidate, out _))
        {
            normalizedTarget = candidate;
            return true;
        }

        var hostNameType = Uri.CheckHostName(candidate);
        if (hostNameType is UriHostNameType.Dns or UriHostNameType.IPv4 or UriHostNameType.IPv6)
        {
            normalizedTarget = candidate;
            return true;
        }

        return false;
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
