using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using RouteAnalyzer.Api;
using RouteAnalyzer.Models;
using RouteAnalyzer.Options;
using RouteAnalyzer.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddProblemDetails();
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("route-api", configure =>
    {
        configure.PermitLimit = 12;
        configure.Window = TimeSpan.FromMinutes(1);
        configure.QueueLimit = 0;
        configure.AutoReplenishment = true;
    });
});
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor |
        ForwardedHeaders.XForwardedProto |
        ForwardedHeaders.XForwardedHost;

    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});
builder.Services.AddHealthChecks()
    .AddCheck("route-analyzer-platform", () =>
        OperatingSystem.IsWindows() || OperatingSystem.IsLinux() || OperatingSystem.IsMacOS()
            ? HealthCheckResult.Healthy("Traceroute command integration is available for this platform family.")
            : HealthCheckResult.Degraded("Traceroute command integration is not configured for this platform family."));
builder.Services
    .AddOptions<RouteAnalyzerOptions>()
    .Bind(builder.Configuration.GetSection(RouteAnalyzerOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(
        options => TargetHostParser.TryNormalize(options.DefaultTarget, out _),
        "RouteAnalyzer:DefaultTarget must be a valid hostname, IP address, or URL.")
    .ValidateOnStart();
builder.Services.AddHttpClient<IpGeoLookupService>(client =>
{
    client.BaseAddress = new Uri("https://ipwho.is/");
    client.Timeout = TimeSpan.FromSeconds(2.5);
});
builder.Services.AddSingleton<NetworkRouteDiagnosticService>();

var app = builder.Build();

app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseResponseCompression();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthorization();

app.MapStaticAssets();
app.MapHealthChecks("/healthz", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json; charset=utf-8";

        var payload = new
        {
            status = report.Status.ToString(),
            generatedAtUtc = DateTimeOffset.UtcNow,
            checks = report.Entries.Select(static entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                description = entry.Value.Description
            })
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            WriteIndented = true
        }));
    }
});

var apiV1 = app.MapGroup("/api/v1")
    .WithTags("Route Analyzer API");

apiV1.MapGet("/info", () =>
{
    var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "dev";

    return TypedResults.Ok(new RouteAnalyzerApiInfoResponse
    {
        Name = "Route Analyzer",
        Version = version,
        Runtime = Environment.Version.ToString(),
        OperatingSystem = RuntimeInformation.OSDescription.Trim(),
        EnvironmentName = app.Environment.EnvironmentName,
        SupportsTraceroute = OperatingSystem.IsWindows() || OperatingSystem.IsLinux() || OperatingSystem.IsMacOS(),
        Endpoints =
        [
            "GET /healthz",
            "GET /api/v1/info",
            "GET /api/v1/diagnostics/sample-targets",
            "POST /api/v1/diagnostics/route"
        ]
    });
});

apiV1.MapGet("/diagnostics/sample-targets", () =>
{
    return TypedResults.Ok(new[]
    {
        "1.1.1.1",
        "8.8.8.8",
        "github.com",
        "cloudflare.com"
    });
});

apiV1.MapPost("/diagnostics/route", async (
        RouteAnalyzeApiRequest request,
        IOptions<RouteAnalyzerOptions> optionsAccessor,
        NetworkRouteDiagnosticService diagnosticService,
        CancellationToken cancellationToken) =>
    {
        var options = optionsAccessor.Value;
        var errors = new Dictionary<string, string[]>();

        if (!TargetHostParser.TryNormalize(request.TargetHost, out var normalizedTarget))
        {
            errors["targetHost"] = ["Provide a valid hostname, IP address, or URL."];
        }

        var pingCount = request.PingCount ?? options.DefaultPingCount;
        if (pingCount is < RouteAnalyzerOptions.MinPingCount or > RouteAnalyzerOptions.MaxPingCount)
        {
            errors["pingCount"] = [$"pingCount must be between {RouteAnalyzerOptions.MinPingCount} and {RouteAnalyzerOptions.MaxPingCount}."];
        }

        var maxHops = request.MaxHops ?? options.DefaultMaxHops;
        if (maxHops is < RouteAnalyzerOptions.MinMaxHops or > RouteAnalyzerOptions.MaxMaxHops)
        {
            errors["maxHops"] = [$"maxHops must be between {RouteAnalyzerOptions.MinMaxHops} and {RouteAnalyzerOptions.MaxMaxHops}."];
        }

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var report = await diagnosticService.AnalyzeAsync(new RouteAnalysisRequest
        {
            TargetHost = normalizedTarget,
            PingCount = pingCount,
            MaxHops = maxHops,
            IncludeGeoDetails = request.IncludeGeoDetails ?? options.DefaultIncludeGeoDetails
        }, cancellationToken);

        return Results.Ok(report);
    })
    .RequireRateLimiting("route-api");

app.MapRazorPages()
   .WithStaticAssets();

app.Run();

public partial class Program;
