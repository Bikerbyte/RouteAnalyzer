using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RouteAnalyzer.Models;
using RouteAnalyzer.Options;

namespace RouteAnalyzer.Services;

public sealed class SupportDiagnosticService
{
    private readonly ILogger<SupportDiagnosticService> _logger;
    private readonly NetworkRouteDiagnosticService _routeDiagnosticService;
    private readonly RouteAnalyzerOptions _options;

    public SupportDiagnosticService(
        NetworkRouteDiagnosticService routeDiagnosticService,
        ILogger<SupportDiagnosticService> logger,
        IOptions<RouteAnalyzerOptions> options)
    {
        _routeDiagnosticService = routeDiagnosticService;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<SupportDiagnosticReport> RunAsync(DiagnosticProfile profile, CancellationToken cancellationToken)
    {
        var executionId = Guid.NewGuid().ToString("n")[..12];
        var startedAt = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation("Starting support diagnostic for profile {ProfileName} targeting {TargetHost}", profile.ProfileName, profile.TargetHost);

        var routeTask = _routeDiagnosticService.AnalyzeAsync(new RouteAnalysisRequest
        {
            TargetHost = profile.TargetHost,
            PingCount = profile.PingCount,
            MaxHops = profile.MaxHops,
            IncludeGeoDetails = profile.IncludeGeoDetails
        }, cancellationToken);

        var dnsTask = RunDnsLookupsAsync(profile.DnsLookups, cancellationToken);
        var tcpTask = RunTcpChecksAsync(profile.TcpEndpoints, cancellationToken);

        await Task.WhenAll(routeTask, dnsTask, tcpTask);

        var routeReport = await routeTask;
        var dnsResults = await dnsTask;
        var tcpResults = await tcpTask;
        var assessment = DiagnosticAssessmentEngine.Assess(profile, routeReport, dnsResults, tcpResults);

        stopwatch.Stop();

        _logger.LogInformation(
            "Completed support diagnostic {ExecutionId} in {DurationMs} ms with status {StatusLabel}",
            executionId,
            stopwatch.ElapsedMilliseconds,
            assessment.OverallStatusLabel);

        return new SupportDiagnosticReport
        {
            ExecutionId = executionId,
            GeneratedAtUtc = startedAt,
            DurationMs = stopwatch.ElapsedMilliseconds,
            MachineName = Environment.MachineName,
            RuntimeSummary = $"{RuntimeInformation.OSDescription.Trim()} | .NET {Environment.Version}",
            Profile = profile,
            Assessment = assessment,
            PrimaryRoute = routeReport,
            DnsResults = dnsResults,
            TcpResults = tcpResults
        };
    }

    private Task<IReadOnlyList<DnsLookupResult>> RunDnsLookupsAsync(
        IReadOnlyList<DnsLookupDefinition> definitions,
        CancellationToken cancellationToken)
    {
        if (definitions.Count == 0)
        {
            return Task.FromResult<IReadOnlyList<DnsLookupResult>>([]);
        }

        return RunDnsLookupsCoreAsync(definitions, cancellationToken);
    }

    private async Task<IReadOnlyList<DnsLookupResult>> RunDnsLookupsCoreAsync(
        IReadOnlyList<DnsLookupDefinition> definitions,
        CancellationToken cancellationToken)
    {
        var results = await Task.WhenAll(definitions.Select(definition => RunDnsLookupAsync(definition, cancellationToken)));
        return results;
    }

    private async Task<DnsLookupResult> RunDnsLookupAsync(DnsLookupDefinition definition, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var addresses = await Dns.GetHostAddressesAsync(definition.Hostname)
                .WaitAsync(TimeSpan.FromMilliseconds(_options.DnsTimeoutMs), cancellationToken);

            stopwatch.Stop();

            return new DnsLookupResult
            {
                Name = definition.Name,
                Hostname = definition.Hostname,
                Success = addresses.Length > 0,
                DurationMs = stopwatch.ElapsedMilliseconds,
                Addresses = addresses.Select(static address => address.ToString()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                ErrorMessage = addresses.Length > 0 ? null : "No addresses returned."
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();

            return new DnsLookupResult
            {
                Name = definition.Name,
                Hostname = definition.Hostname,
                Success = false,
                DurationMs = stopwatch.ElapsedMilliseconds,
                Addresses = [],
                ErrorMessage = $"Timed out after {_options.DnsTimeoutMs} ms."
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            return new DnsLookupResult
            {
                Name = definition.Name,
                Hostname = definition.Hostname,
                Success = false,
                DurationMs = stopwatch.ElapsedMilliseconds,
                Addresses = [],
                ErrorMessage = ex.Message
            };
        }
    }

    private Task<IReadOnlyList<TcpEndpointResult>> RunTcpChecksAsync(
        IReadOnlyList<TcpEndpointDefinition> definitions,
        CancellationToken cancellationToken)
    {
        if (definitions.Count == 0)
        {
            return Task.FromResult<IReadOnlyList<TcpEndpointResult>>([]);
        }

        return RunTcpChecksCoreAsync(definitions, cancellationToken);
    }

    private async Task<IReadOnlyList<TcpEndpointResult>> RunTcpChecksCoreAsync(
        IReadOnlyList<TcpEndpointDefinition> definitions,
        CancellationToken cancellationToken)
    {
        var results = await Task.WhenAll(definitions.Select(definition => RunTcpCheckAsync(definition, cancellationToken)));
        return results;
    }

    private async Task<TcpEndpointResult> RunTcpCheckAsync(TcpEndpointDefinition definition, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var timeoutMs = definition.TimeoutMs is >= RouteAnalyzerOptions.MinTcpConnectTimeoutMs and <= RouteAnalyzerOptions.MaxTcpConnectTimeoutMs
            ? definition.TimeoutMs.Value
            : _options.TcpConnectTimeoutMs;

        try
        {
            using var client = new TcpClient();
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(timeoutMs);

            await client.ConnectAsync(definition.Host, definition.Port, timeoutCts.Token);

            stopwatch.Stop();

            return new TcpEndpointResult
            {
                Name = definition.Name,
                Host = definition.Host,
                Port = definition.Port,
                Success = true,
                DurationMs = stopwatch.ElapsedMilliseconds
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();

            return new TcpEndpointResult
            {
                Name = definition.Name,
                Host = definition.Host,
                Port = definition.Port,
                Success = false,
                DurationMs = stopwatch.ElapsedMilliseconds,
                ErrorMessage = $"Timed out after {timeoutMs} ms."
            };
        }
        catch (Exception ex) when (ex is SocketException or InvalidOperationException)
        {
            stopwatch.Stop();

            return new TcpEndpointResult
            {
                Name = definition.Name,
                Host = definition.Host,
                Port = definition.Port,
                Success = false,
                DurationMs = stopwatch.ElapsedMilliseconds,
                ErrorMessage = ex.Message
            };
        }
    }
}
