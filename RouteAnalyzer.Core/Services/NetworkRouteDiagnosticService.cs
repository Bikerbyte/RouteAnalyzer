using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RouteAnalyzer.Models;
using RouteAnalyzer.Options;

namespace RouteAnalyzer.Services;

public partial class NetworkRouteDiagnosticService
{
    private const string GeoDataProvider = "ipwho.is";

    private readonly IpGeoLookupService _ipGeoLookupService;
    private readonly ILogger<NetworkRouteDiagnosticService> _logger;
    private readonly RouteAnalyzerOptions _options;

    public NetworkRouteDiagnosticService(
        IpGeoLookupService ipGeoLookupService,
        ILogger<NetworkRouteDiagnosticService> logger,
        IOptions<RouteAnalyzerOptions> options)
    {
        _ipGeoLookupService = ipGeoLookupService;
        _logger = logger;
        _options = options.Value;
    }

    public Task<RouteDiagnosticReport> AnalyzeAsync(string targetHost, int pingCount, CancellationToken cancellationToken)
    {
        var request = new RouteAnalysisRequest
        {
            TargetHost = targetHost,
            PingCount = pingCount,
            MaxHops = _options.DefaultMaxHops,
            IncludeGeoDetails = _options.DefaultIncludeGeoDetails
        };

        return AnalyzeAsync(request, cancellationToken);
    }

    public async Task<RouteDiagnosticReport> AnalyzeAsync(RouteAnalysisRequest request, CancellationToken cancellationToken)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();
        var executionId = Guid.NewGuid().ToString("n")[..12];

        var normalizedPingCount = Math.Clamp(request.PingCount, RouteAnalyzerOptions.MinPingCount, RouteAnalyzerOptions.MaxPingCount);
        var normalizedMaxHops = Math.Clamp(request.MaxHops, RouteAnalyzerOptions.MinMaxHops, RouteAnalyzerOptions.MaxMaxHops);

        using var scope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["ExecutionId"] = executionId,
            ["TargetHost"] = request.TargetHost,
            ["PingCount"] = normalizedPingCount,
            ["MaxHops"] = normalizedMaxHops
        });

        _logger.LogInformation(
            "Starting route analysis for {TargetHost} with {PingCount} ping probes and max {MaxHops} hops",
            request.TargetHost,
            normalizedPingCount,
            normalizedMaxHops);

        var tracerouteSpec = BuildTracerouteCommandSpec(request.TargetHost, normalizedMaxHops);
        if (tracerouteSpec is null)
        {
            stopwatch.Stop();

            _logger.LogWarning("Route analysis is running on an unsupported platform");

            return new RouteDiagnosticReport
            {
                TargetHost = request.TargetHost,
                MaxHops = normalizedMaxHops,
                GeoDetailsEnabled = request.IncludeGeoDetails,
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
                RuntimeSummary = BuildRuntimeSummary(),
                DiagnosticMode = "Unsupported platform",
                TracerouteCommand = "n/a",
                GeoDataProvider = GeoDataProvider,
                RawTracerouteLines = []
            };
        }

        var pingSummary = await RunPingAsync(request.TargetHost, normalizedPingCount, cancellationToken);
        var tracerouteResult = await RunTracerouteCommandAsync(tracerouteSpec, cancellationToken);
        var hops = await ParseTracerouteAsync(
            request.TargetHost,
            tracerouteResult.OutputFlavor,
            tracerouteResult.Lines,
            request.IncludeGeoDetails,
            cancellationToken);

        stopwatch.Stop();

        _logger.LogInformation(
            "Completed route analysis for {TargetHost} in {DurationMs} ms with {HopCount} hops and {PacketLossPercent}% packet loss",
            request.TargetHost,
            stopwatch.ElapsedMilliseconds,
            hops.Count,
            pingSummary.PacketLossPercent);

        return new RouteDiagnosticReport
        {
            TargetHost = request.TargetHost,
            MaxHops = normalizedMaxHops,
            GeoDetailsEnabled = request.IncludeGeoDetails,
            ExecutionId = executionId,
            GeneratedAtUtc = startedAt,
            DurationMs = stopwatch.ElapsedMilliseconds,
            PingSummary = pingSummary,
            Hops = hops,
            RuntimeSummary = BuildRuntimeSummary(),
            DiagnosticMode = tracerouteSpec.ModeLabel,
            TracerouteCommand = tracerouteResult.CommandDisplay,
            GeoDataProvider = GeoDataProvider,
            RawTracerouteLines = tracerouteResult.Lines
        };
    }

    private async Task<PingSummary> RunPingAsync(string targetHost, int pingCount, CancellationToken cancellationToken)
    {
        using var ping = new Ping();
        var replies = new List<long>();

        for (var i = 0; i < pingCount; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var reply = await ping.SendPingAsync(targetHost, _options.PingTimeoutMs);
                if (reply.Status == IPStatus.Success)
                {
                    replies.Add(reply.RoundtripTime);
                }
            }
            catch
            {
                // 這裡先不要直接中斷，失敗就算進 packet loss 繼續往下收。
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

    private async Task<TracerouteRunResult> RunTracerouteCommandAsync(TracerouteCommandSpec spec, CancellationToken cancellationToken)
    {
        using Process process = new()
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = spec.FileName,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        foreach (var argument in spec.Arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        var commandDisplay = BuildCommandDisplay(spec.FileName, spec.Arguments);

        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            var errorLines = new List<string>
            {
                $"Unable to start traceroute command: {commandDisplay}",
                ex.Message
            };

            return new TracerouteRunResult(commandDisplay, spec.ModeLabel, spec.OutputFlavor, errorLines, ex.Message);
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(_options.TracerouteProcessTimeoutSeconds));

        try
        {
            var outputTask = ReadAllBytesAsync(process.StandardOutput.BaseStream, cancellationToken);
            var errorTask = ReadAllBytesAsync(process.StandardError.BaseStream, cancellationToken);

            await process.WaitForExitAsync(timeoutCts.Token);

            var output = CommandOutputDecoder.Decode(await outputTask);
            var error = CommandOutputDecoder.Decode(await errorTask);

            var lines = (output + Environment.NewLine + error)
                .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

            if (lines.Count == 0)
            {
                lines.Add("Traceroute returned no output.");
            }

            return new TracerouteRunResult(commandDisplay, spec.ModeLabel, spec.OutputFlavor, lines, null);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            TryKillProcess(process);

            var timeoutLines = new List<string>
            {
                $"Traceroute command timed out after {_options.TracerouteProcessTimeoutSeconds} seconds: {commandDisplay}"
            };

            return new TracerouteRunResult(commandDisplay, spec.ModeLabel, spec.OutputFlavor, timeoutLines, "Traceroute command timed out");
        }
    }

    private async Task<IReadOnlyList<RouteHop>> ParseTracerouteAsync(
        string targetHost,
        TracerouteOutputFlavor outputFlavor,
        IReadOnlyList<string> lines,
        bool includeGeoDetails,
        CancellationToken cancellationToken)
    {
        var hops = new List<RouteHop>();
        RouteHop? previous = null;

        foreach (var line in lines)
        {
            if (!TryParseHopLine(line, outputFlavor, out var parsedHop))
            {
                continue;
            }

            var parsedSamples = parsedHop.Samples.Select(ParseLatency).ToList();
            var isTimeout = parsedHop.IsTimeout || parsedSamples.All(static value => !value.HasValue);
            var averageLatency = parsedSamples.Any(static value => value.HasValue)
                ? (int?)Math.Round(parsedSamples.Where(static value => value.HasValue).Average(static value => value!.Value))
                : null;

            var latencyDelta = previous?.AverageLatencyMs is int previousAverage && averageLatency is int currentAverage
                ? (int?)(currentAverage - previousAverage)
                : null;

            var suspectedSpike = latencyDelta is int delta && delta >= 25;
            var reverseDns = await ResolveReverseDnsAsync(parsedHop.Address, cancellationToken);
            var geoDetails = includeGeoDetails
                && !isTimeout
                && IPAddress.TryParse(parsedHop.Address, out var parsedIp)
                && !IsPrivateAddress(parsedIp)
                ? await _ipGeoLookupService.LookupAsync(parsedHop.Address, cancellationToken)
                : null;

            var (scopeLabel, scopeDetail) = DescribeHop(targetHost, parsedHop.HopNumber, parsedHop.Address, isTimeout, reverseDns);

            var note = isTimeout
                ? "This hop did not reply to ICMP."
                : suspectedSpike
                    ? $"Latency increases by {latencyDelta} ms compared with the previous hop."
                    : "No obvious step-up is visible at this hop.";

            var hop = new RouteHop
            {
                HopNumber = parsedHop.HopNumber,
                DisplayAddress = string.IsNullOrWhiteSpace(parsedHop.Address) ? "(unknown)" : parsedHop.Address,
                Samples = parsedHop.Samples,
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

    private static bool TryParseHopLine(string line, TracerouteOutputFlavor outputFlavor, out ParsedHop parsedHop)
    {
        return outputFlavor switch
        {
            TracerouteOutputFlavor.Windows => TryParseWindowsHop(line, out parsedHop),
            _ => TryParseUnixHop(line, out parsedHop)
        };
    }

    private static bool TryParseWindowsHop(string line, out ParsedHop parsedHop)
    {
        parsedHop = default!;
        var match = WindowsHopRegex().Match(line);
        if (!match.Success)
        {
            return false;
        }

        var hopNumber = int.Parse(match.Groups["hop"].Value, CultureInfo.InvariantCulture);
        var samples = new[]
        {
            match.Groups["s1"].Value,
            match.Groups["s2"].Value,
            match.Groups["s3"].Value
        };

        var tail = match.Groups["tail"].Value.Trim();
        var isTimeout = samples.All(static sample => sample == "*");

        var address = ExtractAddress(tail);
        if (string.IsNullOrWhiteSpace(address))
        {
            address = isTimeout ? "*" : "(unknown)";
        }

        parsedHop = new ParsedHop(hopNumber, address, samples, isTimeout);
        return true;
    }

    private static bool TryParseUnixHop(string line, out ParsedHop parsedHop)
    {
        parsedHop = default!;
        var match = UnixHopRegex().Match(line);
        if (!match.Success)
        {
            return false;
        }

        var hopNumber = int.Parse(match.Groups["hop"].Value, CultureInfo.InvariantCulture);
        var tail = match.Groups["tail"].Value.Trim();

        if (string.IsNullOrWhiteSpace(tail))
        {
            return false;
        }

        var tailParts = tail.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tailParts.Length == 0)
        {
            return false;
        }

        var address = tailParts[0];
        var sampleTokens = new List<string>();

        if (address == "*")
        {
            sampleTokens.Add("*");
        }

        foreach (Match latencyMatch in LatencySampleRegex().Matches(tail))
        {
            sampleTokens.Add(latencyMatch.Value.Trim());
            if (sampleTokens.Count == 3)
            {
                break;
            }
        }

        while (sampleTokens.Count < 3)
        {
            sampleTokens.Add("*");
        }

        var samples = sampleTokens.Take(3).ToArray();
        var isTimeout = samples.All(static sample => sample == "*");

        if (address != "*" && !IPAddress.TryParse(address, out _))
        {
            address = ExtractAddress(tail) ?? address;
        }

        parsedHop = new ParsedHop(hopNumber, address, samples, isTimeout);
        return true;
    }

    private static string? ExtractAddress(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return null;
        }

        var ipMatch = IpAddressRegex().Match(line);
        if (ipMatch.Success)
        {
            return ipMatch.Value;
        }

        return null;
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
        if (ipAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
        {
            var ipv6Bytes = ipAddress.GetAddressBytes();
            var isUniqueLocal = (ipv6Bytes[0] & 0xFE) == 0xFC;
            return ipAddress.IsIPv6LinkLocal || isUniqueLocal;
        }

        var ipv4Bytes = ipAddress.GetAddressBytes();
        return ipv4Bytes.Length == 4
               && (ipv4Bytes[0], ipv4Bytes[1]) switch
               {
                   (10, _) => true,
                   (172, >= 16 and <= 31) => true,
                   (192, 168) => true,
                   (100, >= 64 and <= 127) => true,
                   _ => false
               };
    }

    private static string BuildRuntimeSummary()
    {
        return $"{RuntimeInformation.OSDescription.Trim()} | .NET {Environment.Version}";
    }

    private static async Task<byte[]> ReadAllBytesAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);
        return buffer.ToArray();
    }

    private static int? ParseLatency(string value)
    {
        if (value == "*")
        {
            return null;
        }

        var digits = NumericRegex().Match(value).Value;
        if (string.IsNullOrWhiteSpace(digits))
        {
            return null;
        }

        return double.TryParse(digits, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? (int?)Math.Round(parsed)
            : null;
    }

    private TracerouteCommandSpec? BuildTracerouteCommandSpec(string targetHost, int maxHops)
    {
        var waitMs = _options.TracerouteProbeTimeoutMs.ToString(CultureInfo.InvariantCulture);
        var hopValue = maxHops.ToString(CultureInfo.InvariantCulture);

        if (OperatingSystem.IsWindows())
        {
            return new TracerouteCommandSpec(
                "tracert",
                ["-d", "-w", waitMs, "-h", hopValue, targetHost],
                "ICMP ping + Windows tracert",
                TracerouteOutputFlavor.Windows);
        }

        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            var waitSeconds = Math.Clamp((int)Math.Round(_options.TracerouteProbeTimeoutMs / 1000.0), 1, 5)
                .ToString(CultureInfo.InvariantCulture);

            return new TracerouteCommandSpec(
                "traceroute",
                ["-n", "-w", waitSeconds, "-m", hopValue, targetHost],
                "ICMP ping + traceroute (-n)",
                TracerouteOutputFlavor.Unix);
        }

        return null;
    }

    private static string BuildCommandDisplay(string fileName, IReadOnlyList<string> arguments)
    {
        return $"{fileName} {string.Join(' ', arguments)}";
    }

    private static void TryKillProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // 這種收尾失敗先忽略，不要反過來蓋掉原本的診斷結果。
        }
    }

    [GeneratedRegex(@"^\s*(?<hop>\d+)\s+(?<s1><\d+\s*ms|\d+\s*ms|\*)\s+(?<s2><\d+\s*ms|\d+\s*ms|\*)\s+(?<s3><\d+\s*ms|\d+\s*ms|\*)(?:\s+(?<tail>.+))?\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex WindowsHopRegex();

    [GeneratedRegex(@"^\s*(?<hop>\d+)\s+(?<tail>.+)$", RegexOptions.IgnoreCase)]
    private static partial Regex UnixHopRegex();

    [GeneratedRegex(@"((?:<)?\d+(?:\.\d+)?)\s*ms|\*", RegexOptions.IgnoreCase)]
    private static partial Regex LatencySampleRegex();

    [GeneratedRegex(@"\d+(?:\.\d+)?")]
    private static partial Regex NumericRegex();

    [GeneratedRegex(@"((?:\d{1,3}(?:\.\d{1,3}){3})|(?:[0-9a-fA-F:]{2,}))")]
    private static partial Regex IpAddressRegex();

    private sealed record ParsedHop(int HopNumber, string Address, IReadOnlyList<string> Samples, bool IsTimeout);

    private sealed record TracerouteCommandSpec(
        string FileName,
        IReadOnlyList<string> Arguments,
        string ModeLabel,
        TracerouteOutputFlavor OutputFlavor);

    private sealed record TracerouteRunResult(
        string CommandDisplay,
        string ModeLabel,
        TracerouteOutputFlavor OutputFlavor,
        IReadOnlyList<string> Lines,
        string? StartErrorMessage);

    private enum TracerouteOutputFlavor
    {
        Windows,
        Unix
    }
}

