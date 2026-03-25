using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using RouteAnalyzer.Models;

namespace RouteAnalyzer.Tests;

public class ApiIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ApiIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetInfo_ReturnsApiMetadata()
    {
        var response = await _client.GetAsync("/api/v1/info");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<RouteAnalyzerApiInfoTestView>();
        Assert.NotNull(payload);
        Assert.Equal("Route Analyzer", payload.Name);
        Assert.Contains("POST /api/v1/diagnostics/route", payload.Endpoints);
    }

    [Fact]
    public async Task PostDiagnosticsRoute_ReturnsReport()
    {
        var request = new
        {
            targetHost = "127.0.0.1",
            pingCount = 3,
            maxHops = 6,
            includeGeoDetails = false
        };

        var response = await _client.PostAsJsonAsync("/api/v1/diagnostics/route", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var report = await response.Content.ReadFromJsonAsync<RouteDiagnosticReport>();
        Assert.NotNull(report);
        Assert.Equal("127.0.0.1", report.TargetHost);
        Assert.Equal(3, report.PingSummary.Sent);
        Assert.Equal(6, report.MaxHops);
        Assert.False(string.IsNullOrWhiteSpace(report.StatusLabel));
    }

    private sealed class RouteAnalyzerApiInfoTestView
    {
        public required string Name { get; init; }

        public required string[] Endpoints { get; init; }
    }
}
