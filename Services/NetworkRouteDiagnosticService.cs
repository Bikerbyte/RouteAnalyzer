using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using RouteAnalyzer.Models;

namespace RouteAnalyzer.Services;

public partial class NetworkRouteDiagnosticService
{
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
                Narrative = "目前只支援 Windows，因為這版是直接解析 tracert 輸出。",
                SuspectedIssue = "作業系統不支援",
                RawTracerouteLines = []
            };
        }

        var pingSummary = await RunPingAsync(targetHost, pingCount);
        var tracerouteLines = await RunCommandLinesAsync("tracert", $"-d -w 900 {targetHost}", cancellationToken);
        var hops = ParseTraceroute(tracerouteLines);
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
                // Keep the tool forgiving. Failures count toward packet loss.
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
        var lines = (output + Environment.NewLine + error)
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        return lines;
    }

    private static IReadOnlyList<RouteHop> ParseTraceroute(IReadOnlyList<string> lines)
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

            var parsedSamples = samples
                .Select(ParseLatency)
                .ToList();

            var address = match.Groups["addr"].Value.Trim();
            var isTimeout = parsedSamples.All(static value => !value.HasValue);
            var avg = parsedSamples.Where(static value => value.HasValue).Select(static value => value!.Value).DefaultIfEmpty().ToList();
            var averageLatency = parsedSamples.Any(static value => value.HasValue) ? (int?)Math.Round(avg.Average()) : null;

            var suspectedSpike = previous?.AverageLatencyMs is int previousAverage
                && averageLatency is int currentAverage
                && currentAverage - previousAverage >= 25;

            var note = isTimeout
                ? "這一跳沒有回應，可能被防火牆或路由設備忽略。"
                : suspectedSpike
                    ? $"從上一跳增加了 {averageLatency - previous!.AverageLatencyMs!.Value} ms，值得注意。"
                    : "看起來還算平穩。";

            var hop = new RouteHop
            {
                HopNumber = hopNumber,
                DisplayAddress = string.IsNullOrWhiteSpace(address) ? "(unknown)" : address,
                Samples = samples,
                AverageLatencyMs = averageLatency,
                IsTimeout = isTimeout,
                SuspectedSpike = suspectedSpike,
                Note = note
            };

            hops.Add(hop);
            previous = hop;
        }

        return hops;
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
            return "封包遺失偏高，可能是整體連線品質不穩";
        }

        if (hops.Any(static hop => hop.IsTimeout))
        {
            return "途中有 timeout，但不一定代表真的故障";
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
            return $"整體平均 ping 約 {pingSummary.AverageRoundTripMs?.ToString() ?? "-"} ms，{suspectedIssue}。建議先比對不同時段再看是否穩定重現。";
        }

        return $"平均 ping 約 {pingSummary.AverageRoundTripMs?.ToString() ?? "-"} ms，路徑沒有看到特別突兀的延遲跳升。若連線仍不穩，問題可能更偏即時流量波動或目標端。";
    }

    private static int? ParseLatency(string value)
    {
        var digits = Regex.Match(value, @"\d+").Value;
        return int.TryParse(digits, out var result) ? result : null;
    }

    [GeneratedRegex(@"^\s*(?<hop>\d+)\s+(?<s1><\d+\s*ms|\d+\s*ms|\*)\s+(?<s2><\d+\s*ms|\d+\s*ms|\*)\s+(?<s3><\d+\s*ms|\d+\s*ms|\*)\s+(?<addr>\S+)\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex HopRegex();
}
