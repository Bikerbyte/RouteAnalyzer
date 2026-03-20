using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using RouteAnalyzer.Models;

namespace RouteAnalyzer.Services;

public partial class NetworkRouteDiagnosticService
{
    private readonly IpGeoLookupService _ipGeoLookupService;

    public NetworkRouteDiagnosticService(IpGeoLookupService ipGeoLookupService)
    {
        _ipGeoLookupService = ipGeoLookupService;
    }

    public async Task<RouteDiagnosticReport> AnalyzeAsync(string targetHost, int pingCount, CancellationToken cancellationToken)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return new RouteDiagnosticReport
            {
                TargetHost = targetHost,
                PingSummary = new PingSummary
                {
                    Sent = 0,
                    Received = 0,
                    PacketLossPercent = 100
                },
                Hops = [],
                Narrative = "目前只支援 Windows，因為這版直接解析 tracert 輸出。",
                SuspectedIssue = "作業系統不支援",
                RawTracerouteLines = []
            };
        }

        var pingSummary = await RunPingAsync(targetHost, pingCount);
        var tracerouteLines = await RunCommandLinesAsync("tracert", $"-d -w 900 {targetHost}", cancellationToken);
        var hops = await ParseTracerouteAsync(targetHost, tracerouteLines, cancellationToken);
        var suspectedIssue = FindSuspectedIssue(hops, pingSummary);

        return new RouteDiagnosticReport
        {
            TargetHost = targetHost,
            PingSummary = pingSummary,
            Hops = hops,
            SuspectedIssue = suspectedIssue,
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

        return new PingSummary
        {
            Sent = pingCount,
            Received = received,
            PacketLossPercent = lossPercent,
            AverageRoundTripMs = replies.Count > 0 ? (int?)Math.Round(replies.Average()) : null,
            MinimumRoundTripMs = replies.Count > 0 ? (int?)replies.Min() : null,
            MaximumRoundTripMs = replies.Count > 0 ? (int?)replies.Max() : null
        };
    }

    private static async Task<IReadOnlyList<string>> RunCommandLinesAsync(string fileName, string arguments, CancellationToken cancellationToken)
    {
        using Process process = new()
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

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
                ? "該 hop 沒有回應 ICMP，未必代表真正故障。"
                : suspectedSpike
                    ? $"相較上一跳增加 {latencyDelta} ms，值得注意。"
                    : "目前沒有看到異常跳升。";

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
            return ("No reply", "該 hop 沒有回覆 ICMP。");
        }

        if (IPAddress.TryParse(address, out var ipAddress) && IsPrivateAddress(ipAddress))
        {
            return hopNumber == 1
                ? ("LAN / Gateway", "通常是本機路由器或第一層閘道。")
                : ("Private network", "仍在私有網段內，可能是內網或 ISP 前段設備。");
        }

        if (!string.IsNullOrWhiteSpace(reverseDns))
        {
            return ("Public hop", $"PTR: {reverseDns}");
        }

        if (string.Equals(address, targetHost, StringComparison.OrdinalIgnoreCase))
        {
            return ("Destination", "這一跳看起來就是目標主機。");
        }

        return hopNumber <= 2
            ? ("Access / ISP edge", "通常靠近本地網路或 ISP 接入邊界。")
            : ("Transit hop", "中途公網節點，可能是骨幹或上游網路。");
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
        var spike = hops.FirstOrDefault(static hop => hop.SuspectedSpike);
        if (spike is not null)
        {
            return $"第 {spike.HopNumber} 跳開始延遲明顯升高";
        }

        if (pingSummary.PacketLossPercent >= 25)
        {
            return "封包遺失偏高，整體連線品質不穩";
        }

        if (hops.Any(static hop => hop.IsTimeout))
        {
            return "途中有 timeout，但未必就是故障點";
        }

        return null;
    }

    private static string BuildNarrative(PingSummary pingSummary, IReadOnlyList<RouteHop> hops, string? suspectedIssue)
    {
        if (hops.Count == 0)
        {
            return "這次 tracert 沒有抓到可解析的 hop，可能是目標拒絕回應，或指令輸出格式和預期不同。";
        }

        if (!string.IsNullOrWhiteSpace(suspectedIssue))
        {
            return $"平均 ping 約 {pingSummary.AverageRoundTripMs?.ToString() ?? "-"} ms，{suspectedIssue}。建議比對不同時段結果，確認是否穩定重現。";
        }

        return $"平均 ping 約 {pingSummary.AverageRoundTripMs?.ToString() ?? "-"} ms，路徑沒有看到明顯的延遲跳升。若連線仍不穩，問題可能更偏即時流量波動或目標端。";
    }

    private static int? ParseLatency(string value)
    {
        var digits = Regex.Match(value, @"\d+").Value;
        return int.TryParse(digits, out var result) ? result : null;
    }

    [GeneratedRegex(@"^\s*(?<hop>\d+)\s+(?<s1><\d+\s*ms|\d+\s*ms|\*)\s+(?<s2><\d+\s*ms|\d+\s*ms|\*)\s+(?<s3><\d+\s*ms|\d+\s*ms|\*)\s+(?<addr>\S+)\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex HopRegex();
}
