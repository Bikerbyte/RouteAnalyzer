using RouteAnalyzer.Models;

namespace RouteAnalyzer.Services;

public static class DiagnosticAssessmentEngine
{
    private const string TimeoutOnlyInconclusiveIssue = "One or more hops timed out, but timeout-only signals are inconclusive";

    // 這些 scenario key 先維持穩定，報表和測試都會直接吃。
    public const string ScenarioLocalDnsOrInitialConnectivity = "local-dns-or-initial-connectivity";
    public const string ScenarioCompanyEdgeServiceTcpFailure = "company-edge-service-tcp-failure";
    public const string ScenarioLocalNetworkOrWifi = "local-network-or-wifi";
    public const string ScenarioIspOrAccessNetwork = "isp-or-access-network";
    public const string ScenarioInternetTransitPath = "internet-transit-path";
    public const string ScenarioCompanyNetworkOrDestinationService = "company-network-or-destination-service";
    public const string ScenarioNoClearNetworkFaultDetected = "no-clear-network-fault-detected";
    public const string ScenarioIntermittentOrInconclusive = "intermittent-or-inconclusive";

    public static DiagnosticAssessment Assess(
        DiagnosticProfile profile,
        RouteDiagnosticReport route,
        IReadOnlyList<DnsLookupResult> dnsResults,
        IReadOnlyList<TcpEndpointResult> tcpResults)
    {
        // 先把後面會重複用到的訊號整理出來，下面的判斷才不會越看越亂。
        var failedDns = dnsResults.Where(static result => !result.Success).ToArray();
        var failedTcp = tcpResults.Where(static result => !result.Success).ToArray();
        var firstSpike = route.Hops.FirstOrDefault(static hop => hop.SuspectedSpike);
        var lastHop = route.Hops.LastOrDefault();
        var routeHealthy = string.Equals(route.StatusLabel, "Stable", StringComparison.OrdinalIgnoreCase);
        var severeLoss = route.PingSummary.PacketLossPercent >= 40;
        var moderateLoss = route.PingSummary.PacketLossPercent >= 15;
        var timeoutOnlyObservation =
            string.Equals(route.SuspectedIssue, TimeoutOnlyInconclusiveIssue, StringComparison.Ordinal)
            && route.PingSummary.PacketLossPercent == 0;
        var firstHopIssue = route.Hops.FirstOrDefault(static hop => hop.HopNumber == 1 && (hop.IsTimeout || hop.SuspectedSpike || (hop.AverageLatencyMs ?? 0) >= 20));
        var accessHopIssue = route.Hops.FirstOrDefault(static hop => hop.HopNumber <= 2 && (hop.SuspectedSpike || hop.IsTimeout));
        var finalHopIssue = lastHop is not null
            && (lastHop.IsTimeout || lastHop.SuspectedSpike || string.Equals(lastHop.ScopeLabel, "Destination", StringComparison.OrdinalIgnoreCase));

        string overallStatus;
        string faultDomain;
        string userSummary;
        string itSummary;
        string scenarioKey;
        List<string> recommendations;

        // 連解析都失敗、路也起不來時，先往本機 DNS 或前段連線看。
        if (dnsResults.Count > 0 && failedDns.Length == dnsResults.Count && route.PingSummary.Received == 0)
        {
            scenarioKey = ScenarioLocalDnsOrInitialConnectivity;
            overallStatus = "Action Needed";
            faultDomain = "Local DNS or initial connectivity";
            userSummary = "This run is most consistent with a local DNS or early connectivity issue before traffic reaches the destination side.";
            itSummary = "All configured DNS lookups failed and the route test received no ICMP replies. This is more consistent with a local resolver issue, disconnected internet access, or a VPN pre-connect problem than with a destination-side service issue.";
            recommendations =
            [
                "Reconnect the local network or switch to another network and retry.",
                "Restart Wi-Fi or the home router if the issue affects all destinations.",
                "If the device is on VPN-required DNS, confirm the VPN client is running correctly."
            ];
        }
        // 路徑大致可達，但服務埠全失敗時，先懷疑目的端邊界或服務本身。
        else if (tcpResults.Count > 0 && failedTcp.Length == tcpResults.Count && routeHealthy && failedDns.Length == 0)
        {
            scenarioKey = ScenarioCompanyEdgeServiceTcpFailure;
            overallStatus = "Action Needed";
            faultDomain = "Destination edge or destination service";
            userSummary = "This run suggests the route is reachable, but the destination service ports are still not accepting connections.";
            itSummary = "Route quality appears healthy while every configured TCP endpoint failed. That pattern is more consistent with a service listener, firewall, VPN gateway, or destination-edge issue than with a home network issue.";
            recommendations =
            [
                "Check the destination VPN gateway, reverse proxy, firewall, or target service health.",
                "Compare with another user or monitoring source hitting the same service ports.",
                "Review server-side logs for refused or timed-out connections at the reported time."
            ];
        }
        // 只有 timeout 雜訊、其他檢查都正常時，不要過度放大成網路故障。
        else if (timeoutOnlyObservation && failedDns.Length == 0 && failedTcp.Length == 0)
        {
            scenarioKey = ScenarioNoClearNetworkFaultDetected;
            overallStatus = "Healthy";
            faultDomain = "No clear network fault detected";
            userSummary = "The route, DNS lookups, and service port checks look mostly healthy in this run. A traceroute hop timed out, but that alone does not point to a network fault.";
            itSummary = "One or more traceroute hops timed out, but the remaining path, DNS, TCP checks, and end-to-end ping do not show a stronger failure signal. Treat this as a low-signal observation unless repeat runs show the same pattern.";
            recommendations =
            [
                "If the issue continues, collect another report while the slowdown is actively happening.",
                "Compare with a repeat run or another network before treating the timeout as a fault hop.",
                "If the user still feels slowness, check application-side or endpoint-side evidence next."
            ];
        }
        // 明顯異常一開始就貼近使用者端時，先看本地網路或 gateway。
        else if (firstHopIssue is not null || severeLoss)
        {
            scenarioKey = ScenarioLocalNetworkOrWifi;
            overallStatus = "Action Needed";
            faultDomain = "Local network or Wi-Fi";
            userSummary = "This run suggests the issue starts very close to the device, which is often consistent with Wi-Fi quality, the router's proxy and firewall settings.";
            itSummary = firstHopIssue is not null
                ? $"The first hop already shows degradation at hop {firstHopIssue.HopNumber} ({firstHopIssue.ScopeLabel}). Combined with packet loss {route.PingSummary.PacketLossPercent}%, this is more consistent with the user's LAN or gateway."
                : $"Packet loss is {route.PingSummary.PacketLossPercent}% with no stronger downstream signal. The local access network is still the first place to verify.";
            recommendations =
            [
                "Try to use wired ethernet if possible, and check proxy or firewall settings.",
                "Have the user restart the home router or reconnect Wi-Fi.",
                "If available, compare the same test from a mobile hotspot to isolate the home network."
            ];
        }
        // 問題很前段就出現，但不是卡在第一跳時，優先往 ISP / access network 看。
        else if (accessHopIssue is not null || (firstSpike is not null && firstSpike.HopNumber <= 2))
        {
            scenarioKey = ScenarioIspOrAccessNetwork;
            overallStatus = "Warning";
            faultDomain = "ISP or access network";
            userSummary = "This run suggests the path becomes unstable very early, which is often more consistent with the ISP side than with the destination systems.";
            itSummary = accessHopIssue is not null
                ? $"An abnormal signal appears by hop {accessHopIssue.HopNumber} ({accessHopIssue.ScopeLabel}). The issue likely sits between the user gateway and the ISP access edge."
                : $"Latency rises sharply by hop {firstSpike!.HopNumber}. That is early enough to suspect the access ISP before the destination service.";
            recommendations =
            [
                "Retry the diagnostic from a different network, such as a hotspot, to confirm the ISP path.",
                "If the same behavior persists over time, ask the user to contact the ISP with the report.",
                "Capture one or two repeat runs at different times to check whether the issue is bursty or sustained."
            ];
        }
        // 延遲是在公網中段拉上來時，比較像 transit 路徑問題。
        else if (firstSpike is not null && firstSpike.HopNumber < Math.Max(route.Hops.Count - 1, 3))
        {
            scenarioKey = ScenarioInternetTransitPath;
            overallStatus = moderateLoss || failedTcp.Length > 0 ? "Warning" : "Healthy";
            faultDomain = "Internet transit path";
            userSummary = "This run shows a delay increase in the public network path, so the slowdown may be happening between the ISP and the destination side.";
            itSummary = $"Latency rises at hop {firstSpike.HopNumber}, after the access edge but before the destination. This is more consistent with transit or upstream path congestion than with a purely local issue.";
            recommendations =
            [
                "Repeat the test later to confirm whether the public path issue is persistent.",
                "Compare the same route from another network or another user region if possible.",
                "If business impact is high, collect multiple reports and escalate with the ISP or upstream provider."
            ];
        }
        // 後段 hop 或服務探測才開始出現異常時，先查目的端那一側。
        else if (finalHopIssue || failedTcp.Length > 0)
        {
            scenarioKey = ScenarioCompanyNetworkOrDestinationService;
            overallStatus = failedTcp.Length > 0 ? "Action Needed" : "Warning";
            faultDomain = "Destination network or destination service";
            userSummary = "This run suggests the later part of the path is where the symptoms appear, so the issue may be near the destination edge or service itself.";
            itSummary = failedTcp.Length > 0
                ? $"One or more destination service ports failed even though the route reached the later hops. Investigate the destination edge, VPN listener, or target service first."
                : "The route remains mostly normal until the final segment, which makes the destination edge or destination host the more likely fault domain in this run.";
            recommendations =
            [
                "Check VPN gateway, remote desktop gateway, reverse proxy, or destination service health.",
                "Review firewall and listener status on the destination side.",
                "Correlate with server-side monitoring and logs for the same time window."
            ];
        }
        // 目前檢查都健康時，先不要硬判成網路側故障。
        else if (routeHealthy && failedDns.Length == 0 && failedTcp.Length == 0)
        {
            scenarioKey = ScenarioNoClearNetworkFaultDetected;
            overallStatus = "Healthy";
            faultDomain = "No clear network fault detected";
            userSummary = "The route, DNS lookups, and service port checks all look healthy right now.";
            itSummary = "No strong network-side issue stands out in this run. If the user still feels slowness, the problem may be application-specific, intermittent, or workload-related.";
            recommendations =
            [
                "If the issue continues, collect an additional report at the exact time of the slowdown.",
                "Check the target application, VPN client logs, or endpoint performance metrics.",
                "Compare this result with another run from the same device on a different network."
            ];
        }
        // 訊號混在一起時，至少保留一個還有判讀價值的結論。
        else
        {
            scenarioKey = ScenarioIntermittentOrInconclusive;
            overallStatus = "Warning";
            faultDomain = "Intermittent or inconclusive";
            userSummary = "This run shows some warning signals, but they do not point to one clear fault domain yet.";
            itSummary = "The evidence is mixed. There are enough signals to keep investigating, but not enough to assign the fault confidently to home network, ISP, transit, or the destination edge.";
            recommendations =
            [
                "Collect a second run while the issue is actively happening.",
                "Compare with the same test on another network to split local versus remote factors.",
                "Pair this report with application-side logs or VPN client logs before escalating."
            ];
        }

        var evidence = BuildEvidence(route, failedDns, failedTcp, firstSpike, dnsResults, tcpResults);

        return new DiagnosticAssessment
        {
            ScenarioKey = scenarioKey,
            OverallStatusLabel = overallStatus,
            FaultDomain = faultDomain,
            UserSummary = userSummary,
            ItSummary = itSummary,
            EvidenceHighlights = evidence,
            Recommendations = recommendations
        };
    }

    private static IReadOnlyList<string> BuildEvidence(
        RouteDiagnosticReport route,
        IReadOnlyList<DnsLookupResult> failedDns,
        IReadOnlyList<TcpEndpointResult> failedTcp,
        RouteHop? firstSpike,
        IReadOnlyList<DnsLookupResult> dnsResults,
        IReadOnlyList<TcpEndpointResult> tcpResults)
    {
        // evidence 先維持精簡、偏 support 看的語氣。
        // 要能交代判斷依據，但不要把 summary 寫成原始遙測 dump。
        var evidence = new List<string>
        {
            $"Ping success rate observed: {route.PingSummary.SuccessRatePercent}% with average latency {(route.PingSummary.AverageRoundTripMs?.ToString() ?? "-")} ms."
        };

        if (route.PingSummary.PacketLossPercent > 0)
        {
            evidence.Add($"Packet loss observed: {route.PingSummary.PacketLossPercent}%.");
        }
        else
        {
            evidence.Add("No end-to-end packet loss was observed.");
        }

        if (firstSpike is not null)
        {
            evidence.Add($"Latency step-up begins around hop {firstSpike.HopNumber} ({firstSpike.ScopeLabel}).");
        }

        if (route.Hops.Any(static hop => hop.IsTimeout))
        {
            evidence.Add("One or more traceroute hops timed out during this run.");
        }

        if (dnsResults.Count > 0)
        {
            evidence.Add($"DNS checks passed: {dnsResults.Count - failedDns.Count}/{dnsResults.Count}.");
        }

        if (failedDns.Count > 0)
        {
            evidence.Add($"Failed DNS lookups: {string.Join(", ", failedDns.Select(static result => result.Name))}.");
        }

        if (tcpResults.Count > 0)
        {
            evidence.Add($"TCP checks passed: {tcpResults.Count - failedTcp.Count}/{tcpResults.Count}.");
        }

        if (failedTcp.Count > 0)
        {
            evidence.Add($"TCP endpoint failures observed: {string.Join(", ", failedTcp.Select(static result => $"{result.Name} ({result.Host}:{result.Port})"))}.");
        }

        return evidence;
    }
}
