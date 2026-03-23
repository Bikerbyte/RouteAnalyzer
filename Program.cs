using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using RouteAnalyzer.Options;
using RouteAnalyzer.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddProblemDetails();
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
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
        OperatingSystem.IsWindows()
            ? HealthCheckResult.Healthy("Windows traceroute support is available.")
            : HealthCheckResult.Degraded("Detailed traceroute parsing currently targets Windows tracert output."));
builder.Services
    .AddOptions<RouteAnalyzerOptions>()
    .Bind(builder.Configuration.GetSection(RouteAnalyzerOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(
        options => options.DefaultPingCount is >= RouteAnalyzerOptions.MinPingCount and <= RouteAnalyzerOptions.MaxPingCount,
        $"RouteAnalyzer:DefaultPingCount must be between {RouteAnalyzerOptions.MinPingCount} and {RouteAnalyzerOptions.MaxPingCount}.")
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
app.MapRazorPages()
   .WithStaticAssets();

app.Run();
