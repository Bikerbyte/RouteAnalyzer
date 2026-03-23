using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using RouteAnalyzer.Models;

namespace RouteAnalyzer.Services;

public partial class NetworkRouteDiagnosticService
{
    private const string DiagnosticMode = "ICMP ping + Windows tracert (-d -w 900)";
    private const string GeoDataProvider = "ipwho.is";

    private readonly IpGeoLookupService _ipGeoLookupService;
    private readonly ILogger<NetworkRouteDiagnosticService> _logger;

    public NetworkRouteDiagnosticService(
        IpGeoLookupService ipGeoLookupService,
        ILogger<NetworkRouteDiagnosticService> logger)
    {
        _ipGeoLookupService = ipGeoLookupService;
        _logger = logger;
    }

    public async Task<RouteDiagnosticReport> AnalyzeAsync(string targetHost, int pingCount, CancellationToken cancellationToken)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();
        var executionId = Guid.NewGuid().ToString("n")[..12];

        using var scope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["ExecutionId"] = executionId,
            ["TargetHost"] = targetHost
        });

        _logger.LogInformation(
            "Starting route analysis for {TargetHost} with {PingCount} ping probes",
            targetHost,
            pingCount);

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            stopwatch.Stop();

            _logger.LogWarning("Route analysis is running on an unsupported platform");

            return new RouteDiagnosticReport
            {
                TargetHost = targetHost,
                ExecutionId = executionId,
                GeneratedAtUtc = startedAt,
                DurationMs = stopwatch.ElapsedMilliseconds,
                PingSummary = new PingSummary
                {
                    Sent = 0,
                    Received = 0,
                    PacketLossPercent = 100
                },
                Hops = [],
                Narrative = "This build currently targets Windows because it parses native tracert output.",
                StatusLabel = "Unsupported",
                StatusSummary = "Route analysis is available, but detailed tracert parsing currently expects Windows.",
                RuntimeSummary = BuildRuntimeSummary(),
                DiagnosticMode = DiagnosticMode,
                GeoDataProvider = GeoDataProvider,
                SuspectedIssue = "Unsupported platform",
                RawTracerouteLines = []
            };
        }

        var pingSummary = await RunPingAsync(targetHost, pingCount);
        var tracerouteLines = await RunCommandLinesAsync("tracert", ["-d", "-w", "900", targetHost], cancellationToken);
        var hops = await ParseTracerouteAsync(targetHost, tracerouteLines, cancellationToken);
        var suspectedIssue = FindSuspectedIssue(hops, pingSummary);
        var statusLabel = DetermineStatusLabel(hops, pingSummary);
        var statusSummary = BuildStatusSummary(statusLabel, hops, pingSummary, suspectedIssue);

        stopwatch.Stop();

        _logger.LogInformation(
            "Completed route analysis for {TargetHost} in {DurationMs} ms with {HopCount} hops, {PacketLossPercent}% packet loss, status {StatusLabel}",
            targetHost,
            stopwatch.ElapsedMilliseconds,
            hops.Count,
            pingSummary.PacketLossPercent,
            statusLabel);

        return new RouteDiagnosticReport
        {
            TargetHost = targetHost,
            ExecutionId = executionId,
            GeneratedAtUtc = startedAt,
            DurationMs = stopwatch.ElapsedMilliseconds,
            PingSummary = pingSummary,
            Hops = hops,
            SuspectedIssue = suspectedIssue,
            StatusLabel = statusLabel,
            StatusSummary = statusSummary,
            RuntimeSummary = BuildRuntimeSummary(),
            DiagnosticMode = DiagnosticMode,
            GeoDataProvider = GeoDataProvider,
            Narrative = BuildNarrative(pingSummary, hops, suspectedIssue),
            RawTracerouteLines = tracerouteLines
        };
    }

    private static async Task<PingSummary> RunPingAsync(string targetHost, int pingCount)
    {
        using var ping = new Ping();
        var replies = new List<long>();

        for (var i = 0; i < pingCount; i++)
        {
            try
            {
                var reply = await ping.SendPingAsync(targetHost, 1200);
                if (reply.Status == IPStatus.Success)
                {
                    replies.Add(reply.RoundtripTime);
                }
            }
            catch
            {
                // Keep the diagnostic flow forgiving. Failures count toward packet loss.
            }
        }

        var received = replies.Count;
        var lossPercent = pingCount == 0 ? 100 : (int)Math.Round((double)(pingCount - received) / pingCount * 100);
        var jitter = replies.Count > 1
            ? (int?)Math.Round(replies.Zip(replies.Skip(1), static (previous, current) => Math.Abs(current - previous)).Average())
            : null;

        return new PingSummary
        {
            Sent = pingCount,
            Received = received,
            PacketLossPercent = lossPercent,
            AverageRoundTripMs = replies.Count > 0 ? (int?)Math.Round(replies.Average()) : null,
            MinimumRoundTripMs = replies.Count > 0 ? (int?)replies.Min() : null,
            MaximumRoundTripMs = replies.Count > 0 ? (int?)replies.Max() : null,
            JitterMs = jitter
        };
    }

    private static async Task<IReadOnlyList<string>> RunCommandLinesAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        using Process process = new()
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        process.Start();

        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        var output = await outputTask;
        var error = await errorTask;

        return (output + Environment.NewLine + error)
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }

    private async Task<IReadOnlyList<RouteHop>> ParseTracerouteAsync(string targetHost, IReadOnlyList<string> lines, CancellationToken cancellationToken)
    {
        var hops = new List<RouteHop>();
        RouteHop? previous = null;

        foreach (var line in lines)
        {
            var match = HopRegex().Match(line);
            if (!match.Success)
            {
                continue;
            }

            var hopNumber = int.Parse(match.Groups["hop"].Value);
            var samples = new[]
            {
                match.Groups["s1"].Value,
                match.Groups["s2"].Value,
                match.Groups["s3"].Value
            };

            var parsedSamples = samples.Select(ParseLatency).ToList();
            var address = match.Groups["addr"].Value.Trim();
            var isTimeout = parsedSamples.All(static value => !value.HasValue);
            var averageLatency = parsedSamples.Any(static value => value.HasValue)
                ? (int?)Math.Round(parsedSamples.Where(static value => value.HasValue).Average(static value => value!.Value))
                : null;

            var latencyDelta = previous?.AverageLatencyMs is int previousAverage && averageLatency is int currentAverage
                ? (int?)(currentAverage - previousAverage)
                : null;

            var suspectedSpike = latencyDelta is int delta && delta >= 25;
            var reverseDns = await ResolveReverseDnsAsync(address, cancellationToken);
            var geoDetails = !isTimeout && IPAddress.TryParse(address, out var parsedIp) && !IsPrivateAddress(parsedIp)
                ? await _ipGeoLookupService.LookupAsync(address, cancellationToken)
                : null;
            var (scopeLabel, scopeDetail) = DescribeHop(targetHost, hopNumber, address, isTimeout, reverseDns);

            var note = isTimeout
                ? "This hop did not reply to ICMP. That alone does not prove a failure."
                : suspectedSpike
                    ? $"Latency increases by {latencyDelta} ms compared with the previous hop."
                    : "No obvious step-up is visible at this hop.";

            var hop = new RouteHop
            {
                HopNumber = hopNumber,
                DisplayAddress = string.IsNullOrWhiteSpace(address) ? "(unknown)" : address,
                Samples = samples,
                AverageLatencyMs = averageLatency,
                LatencyDeltaMs = latencyDelta,
                IsTimeout = isTimeout,
                SuspectedSpike = suspectedSpike,
                ScopeLabel = scopeLabel,
                ScopeDetail = scopeDetail,
                ReverseDns = reverseDns,
                GeoDetails = geoDetails,
                Note = note
            };

            hops.Add(hop);
            previous = hop;
        }

        return hops;
    }

    private static async Task<string?> ResolveReverseDnsAsync(string address, CancellationToken cancellationToken)
    {
        if (!IPAddress.TryParse(address, out var ipAddress))
        {
            return null;
        }

        try
        {
            var entry = await Dns.GetHostEntryAsync(ipAddress).WaitAsync(TimeSpan.FromMilliseconds(700), cancellationToken);
            return string.Equals(entry.HostName, address, StringComparison.OrdinalIgnoreCase) ? null : entry.HostName;
        }
        catch
        {
            return null;
        }
    }

    private static (string Label, string Detail) DescribeHop(string targetHost, int hopNumber, string address, bool isTimeout, string? reverseDns)
    {
        if (isTimeout)
        {
            return ("No reply", "This hop did not reply to ICMP probes.");
        }

        if (IPAddress.TryParse(address, out var ipAddress) && IsPrivateAddress(ipAddress))
        {
            return hopNumber == 1
                ? ("LAN / Gateway", "Usually the local router or first-hop gateway.")
                : ("Private network", "Still inside private address space, often LAN or access-side ISP equipment.");
        }

        if (!string.IsNullOrWhiteSpace(reverseDns))
        {
            return ("Public hop", $"PTR: {reverseDns}");
        }

        if (string.Equals(address, targetHost, StringComparison.OrdinalIgnoreCase))
        {
            return ("Destination", "This hop appears to be the destination host.");
        }

        return hopNumber <= 2
            ? ("Access / ISP edge", "Usually near the local network boundary or ISP access edge.")
            : ("Transit hop", "Intermediate public network node, often upstream or backbone transit.");
    }

    private static bool IsPrivateAddress(IPAddress ipAddress)
    {
        var bytes = ipAddress.GetAddressBytes();
        return bytes.Length == 4
               && (bytes[0], bytes[1]) switch
               {
                   (10, _) => true,
                   (172, >= 16 and <= 31) => true,
                   (192, 168) => true,
                   (100, >= 64 and <= 127) => true,
                   _ => false
               };
    }

    private static string? FindSuspectedIssue(IReadOnlyList<RouteHop> hops, PingSummary pingSummary)
    {
        if (hops.Count == 0)
        {
            return "Traceroute returned no parsable hops";
        }

        var spike = hops.FirstOrDefault(static hop => hop.SuspectedSpike);
        if (spike is not null)
        {
            return $"Latency increases noticeably from hop {spike.HopNumber}";
        }

        if (pingSummary.PacketLossPercent >= 25)
        {
            return "Packet loss is elevated across the full path";
        }

        if (hops.Any(static hop => hop.IsTimeout))
        {
            return "One or more hops timed out, but timeout-only signals are inconclusive";
        }

        return null;
    }

    private static string BuildNarrative(PingSummary pingSummary, IReadOnlyList<RouteHop> hops, string? suspectedIssue)
    {
        if (hops.Count == 0)
        {
            return "This run did not produce any parsable hops. The target may be filtering responses, or the tracert output format differed from what the parser expects.";
        }

        if (!string.IsNullOrWhiteSpace(suspectedIssue))
        {
            return $"Average ping is {pingSummary.AverageRoundTripMs?.ToString() ?? "-"} ms and the primary signal is: {suspectedIssue}. Compare with repeated runs across different times to confirm whether the behavior is stable.";
        }

        return $"Average ping is {pingSummary.AverageRoundTripMs?.ToString() ?? "-"} ms and the path does not show a clear latency step-up. If the workload still feels unstable, the issue may be bursty traffic behavior or target-side saturation.";
    }

    private static string DetermineStatusLabel(IReadOnlyList<RouteHop> hops, PingSummary pingSummary)
    {
        var spikeCount = hops.Count(static hop => hop.SuspectedSpike);
        var timeoutCount = hops.Count(static hop => hop.IsTimeout);

        if (hops.Count == 0)
        {
            return "Investigate";
        }

        if (pingSummary.PacketLossPercent >= 40)
        {
            return "Critical";
        }

        if (pingSummary.PacketLossPercent >= 15 || spikeCount >= 2)
        {
            return "Investigate";
        }

        if (pingSummary.PacketLossPercent > 0 || timeoutCount > 0 || spikeCount == 1)
        {
            return "Observe";
        }

        return "Stable";
    }

    private static string BuildStatusSummary(
        string statusLabel,
        IReadOnlyList<RouteHop> hops,
        PingSummary pingSummary,
        string? suspectedIssue)
    {
        return statusLabel switch
        {
            "Critical" => "Packet loss is high enough to suggest an end-to-end connectivity problem.",
            "Investigate" when !string.IsNullOrWhiteSpace(suspectedIssue) => suspectedIssue,
            "Investigate" => "The path needs a closer look because the signal is noisy or incomplete.",
            "Observe" when hops.Any(static hop => hop.IsTimeout) => "Some hops did not reply. Re-run before treating a timeout as the actual fault domain.",
            "Observe" when pingSummary.PacketLossPercent > 0 => "The path is mostly reachable, but light loss or a single latency jump is worth monitoring.",
            _ => "The current path looks consistent and no strong bottleneck signal stands out."
        };
    }

    private static string BuildRuntimeSummary()
    {
        return $"{RuntimeInformation.OSDescription.Trim()} | .NET {Environment.Version}";
    }

    private static int? ParseLatency(string value)
    {
        var digits = Regex.Match(value, @"\d+").Value;
        return int.TryParse(digits, out var result) ? result : null;
    }

    [GeneratedRegex(@"^\s*(?<hop>\d+)\s+(?<s1><\d+\s*ms|\d+\s*ms|\*)\s+(?<s2><\d+\s*ms|\d+\s*ms|\*)\s+(?<s3><\d+\s*ms|\d+\s*ms|\*)\s+(?<addr>\S+)\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex HopRegex();
}
