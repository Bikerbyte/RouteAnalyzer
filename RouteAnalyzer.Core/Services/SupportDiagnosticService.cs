using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
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

        // 主路徑、DNS、TCP 併行跑，整體時間才壓得在 helpdesk 能接受的範圍。
        var routeTask = _routeDiagnosticService.AnalyzeAsync(BuildRouteRequest(profile), cancellationToken);

        var dnsTask = RunDnsLookupsAsync(profile.DnsLookups, cancellationToken);
        var tcpTask = RunTcpChecksAsync(profile.TcpEndpoints, cancellationToken);

        await Task.WhenAll(routeTask, dnsTask, tcpTask);

        var routeReport = await routeTask;
        var dnsResults = await dnsTask;
        var tcpResults = await tcpTask;
        var assessment = DiagnosticAssessmentEngine.Assess(profile, routeReport, dnsResults, tcpResults);

        stopwatch.Stop();
        var networkContext = BuildNetworkContext();

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
            NetworkContext = networkContext,
            Profile = profile,
            Assessment = assessment,
            PrimaryRoute = routeReport,
            DnsResults = dnsResults,
            TcpResults = tcpResults
        };
    }

    private static RouteAnalysisRequest BuildRouteRequest(DiagnosticProfile profile)
    {
        return new RouteAnalysisRequest
        {
            TargetHost = profile.TargetHost,
            PingCount = profile.PingCount,
            MaxHops = profile.MaxHops,
            IncludeGeoDetails = profile.IncludeGeoDetails
        };
    }

    private static NetworkContextSnapshot BuildNetworkContext()
    {
        try
        {
            var activeInterfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(static networkInterface =>
                    networkInterface.OperationalStatus == OperationalStatus.Up &&
                    networkInterface.NetworkInterfaceType is not NetworkInterfaceType.Loopback &&
                    networkInterface.NetworkInterfaceType is not NetworkInterfaceType.Tunnel)
                .Select(networkInterface => new
                {
                    NetworkInterface = networkInterface,
                    Properties = networkInterface.GetIPProperties(),
                    Gateways = networkInterface.GetIPProperties().GatewayAddresses
                        .Select(static gateway => gateway.Address)
                        .Where(static address => address is not null && !address.Equals(IPAddress.Any) && !address.Equals(IPAddress.IPv6Any))
                        .Select(static address => address!.ToString())
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToArray(),
                    DnsServers = networkInterface.GetIPProperties().DnsAddresses
                        .Where(static address => address is not null)
                        .Select(static address => address.ToString())
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToArray()
                })
                .ToArray();

            var primaryInterface = activeInterfaces
                .OrderByDescending(candidate => candidate.Gateways.Length > 0)
                .ThenByDescending(candidate => candidate.NetworkInterface.Speed)
                .FirstOrDefault();

            if (primaryInterface is null)
            {
                return CreateFallbackNetworkContext();
            }

            return new NetworkContextSnapshot
            {
                ConnectionType = DescribeConnectionType(primaryInterface.NetworkInterface.NetworkInterfaceType),
                ActiveAdapterName = string.IsNullOrWhiteSpace(primaryInterface.NetworkInterface.Name) ? "-" : primaryInterface.NetworkInterface.Name,
                DefaultGateway = primaryInterface.Gateways.FirstOrDefault() ?? "-",
                DnsServers = primaryInterface.DnsServers.Length > 0 ? primaryInterface.DnsServers : ["-"]
            };
        }
        catch
        {
            return CreateFallbackNetworkContext();
        }
    }

    private static NetworkContextSnapshot CreateFallbackNetworkContext()
    {
        return new NetworkContextSnapshot
        {
            ConnectionType = "Unknown",
            ActiveAdapterName = "-",
            DefaultGateway = "-",
            DnsServers = ["-"]
        };
    }

    private static string DescribeConnectionType(NetworkInterfaceType interfaceType)
    {
        return interfaceType switch
        {
            NetworkInterfaceType.Wireless80211 => "Wi-Fi",
            NetworkInterfaceType.Ethernet or
            NetworkInterfaceType.Ethernet3Megabit or
            NetworkInterfaceType.FastEthernetFx or
            NetworkInterfaceType.FastEthernetT or
            NetworkInterfaceType.GigabitEthernet => "Ethernet",
            NetworkInterfaceType.Ppp => "PPP",
            NetworkInterfaceType.Wwanpp or
            NetworkInterfaceType.Wwanpp2 => "Cellular",
            _ => "Other"
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
            _logger.LogWarning(ex, "DNS lookup failed for {LookupName} ({Hostname})", definition.Name, definition.Hostname);
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
            _logger.LogWarning(ex, "TCP check failed for {EndpointName} ({Host}:{Port})", definition.Name, definition.Host, definition.Port);
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
