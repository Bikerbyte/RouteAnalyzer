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
    public string TargetHost { get; set; } = string.Empty;

    [BindProperty]
    public int PingCount { get; set; } = 4;

    public RouteDiagnosticReport? Report { get; private set; }

    public async Task OnGetAsync()
    {
        TargetHost = _options.DefaultTarget;
        PingCount = _options.DefaultPingCount;
        Report = await _diagnosticService.AnalyzeAsync(TargetHost, PingCount, HttpContext.RequestAborted);
    }

    public async Task OnPostAsync()
    {
        PingCount = Math.Clamp(PingCount, 3, 10);

        if (string.IsNullOrWhiteSpace(TargetHost))
        {
            ModelState.AddModelError(string.Empty, "請輸入要測試的目標主機、網域或 IP。");
            return;
        }

        Report = await _diagnosticService.AnalyzeAsync(TargetHost.Trim(), PingCount, HttpContext.RequestAborted);
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
}
