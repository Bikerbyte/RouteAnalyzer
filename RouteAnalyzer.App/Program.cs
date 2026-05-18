using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.FileProviders;
using RouteAnalyzer.App.Diagnostics;
using RouteAnalyzer.Options;
using RouteAnalyzer.Services;

var defaultUrl = ResolveDefaultUrl(args);
var builder = WebApplication.CreateBuilder(args);
if (defaultUrl is not null)
{
    builder.WebHost.UseUrls(defaultUrl);
}

builder.Services.AddRazorPages();
builder.Services.AddOptions<RouteAnalyzerOptions>()
    .Bind(builder.Configuration.GetSection("RouteAnalyzer"))
    .ValidateDataAnnotations();

builder.Services.AddHttpClient<IpGeoLookupService>(client =>
{
    client.BaseAddress = new Uri("https://ipwho.is/");
    client.Timeout = TimeSpan.FromSeconds(2.5);
});
builder.Services.AddTransient<NetworkRouteDiagnosticService>();
builder.Services.AddTransient<SupportDiagnosticService>();
builder.Services.AddTransient<AppDiagnosticRunner>();

var app = builder.Build();
var reportRoot = AppDiagnosticRunner.GetReportRoot(app.Environment);
Directory.CreateDirectory(reportRoot);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(reportRoot),
    RequestPath = "/reports/app"
});

app.UseRouting();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

var diagnostics = app.MapGroup("/api/diagnostics");
diagnostics.MapPost("/run", async (
    DiagnosticRunRequest request,
    AppDiagnosticRunner runner,
    CancellationToken cancellationToken) =>
{
    try
    {
        var result = await runner.RunAsync(request, cancellationToken);
        return Results.Ok(result);
    }
    catch (DiagnosticProfileException ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
});

OpenBrowserWhenReady(app, args);

app.Run();

static string? ResolveDefaultUrl(string[] args)
{
    if (HasConfiguredUrl(args))
    {
        return null;
    }

    var port = TryReservePreferredPort(5015, out var preferredPort)
        ? preferredPort
        : ReserveEphemeralPort();

    return $"http://127.0.0.1:{port}";
}

static bool HasConfiguredUrl(string[] args)
{
    if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ASPNETCORE_URLS"))
        || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DOTNET_URLS")))
    {
        return true;
    }

    return args.Any(static arg =>
        string.Equals(arg, "--urls", StringComparison.OrdinalIgnoreCase)
        || arg.StartsWith("--urls=", StringComparison.OrdinalIgnoreCase));
}

static bool TryReservePreferredPort(int port, out int reservedPort)
{
    reservedPort = port;

    try
    {
        using var listener = new TcpListener(IPAddress.Loopback, port);
        listener.Start();
        return true;
    }
    catch
    {
        return false;
    }
}

static int ReserveEphemeralPort()
{
    using var listener = new TcpListener(IPAddress.Loopback, 0);
    listener.Start();
    return ((IPEndPoint)listener.LocalEndpoint).Port;
}

static void OpenBrowserWhenReady(WebApplication app, string[] args)
{
    if (args.Any(static arg => string.Equals(arg, "--no-open", StringComparison.OrdinalIgnoreCase)))
    {
        return;
    }

    app.Lifetime.ApplicationStarted.Register(() =>
    {
        var addresses = app.Services
            .GetRequiredService<IServer>()
            .Features
            .Get<IServerAddressesFeature>()
            ?.Addresses;
        var launchUrl = addresses?.FirstOrDefault(static address => address.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            ?? addresses?.FirstOrDefault()
            ?? "http://127.0.0.1:5015";

        TryOpenBrowser(launchUrl);
    });
}

static void TryOpenBrowser(string url)
{
    try
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });
    }
    catch
    {
        Console.WriteLine($"Route Analyzer is ready: {url}");
    }
}
