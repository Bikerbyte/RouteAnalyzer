using RouteAnalyzer.Models;

namespace RouteAnalyzer.Services;

public static class DiagnosticAssessmentEngine
{
    // Scenario keys are kept stable so reports and tests can rely on them.
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
        // Build a small set of reusable signals first so the attribution rules below stay readable.
        var failedDns = dnsResults.Where(static result => !result.Success).ToArray();
        var failedTcp = tcpResults.Where(static result => !result.Success).ToArray();
        var firstSpike = route.Hops.FirstOrDefault(static hop => hop.SuspectedSpike);
        var lastHop = route.Hops.LastOrDefault();
        var routeHealthy = string.Equals(route.StatusLabel, "Stable", StringComparison.OrdinalIgnoreCase);
        var severeLoss = route.PingSummary.PacketLossPercent >= 40;
        var moderateLoss = route.PingSummary.PacketLossPercent >= 15;
        var firstHopIssue = route.Hops.FirstOrDefault(static hop => hop.HopNumber == 1 && (hop.IsTimeout || hop.SuspectedSpike || (hop.AverageLatencyMs ?? 0) >= 20));
        var accessHopIssue = route.Hops.FirstOrDefault(static hop => hop.HopNumber <= 2 && (hop.SuspectedSpike || hop.IsTimeout));
        var finalHopIssue = lastHop is not null
            && (lastHop.IsTimeout || lastHop.SuspectedSpike || string.Equals(lastHop.ScopeLabel, "Destination", StringComparison.OrdinalIgnoreCase));

        string overallStatus;
        string faultDomain;
        string confidence;
        string userSummary;
        string itSummary;
        string scenarioKey;
        List<string> recommendations;

        // Rule block: fail early when the device cannot even resolve or start the route.
        if (dnsResults.Count > 0 && failedDns.Length == dnsResults.Count && route.PingSummary.Received == 0)
        {
            scenarioKey = ScenarioLocalDnsOrInitialConnectivity;
            overallStatus = "Action Needed";
            faultDomain = "Local DNS or initial connectivity";
            confidence = "High";
            userSummary = "The device could not resolve the required hostnames, so the connection is failing before it reaches the company services.";
            itSummary = "All configured DNS lookups failed and the route test received no ICMP replies. This strongly suggests a local resolver issue, disconnected internet access, or VPN pre-connect problem.";
            recommendations =
            [
                "Reconnect the local network or switch to another network and retry.",
                "Restart Wi-Fi or the home router if the issue affects all destinations.",
                "If the device is on VPN-required DNS, confirm the VPN client is running correctly."
            ];
        }
        // Rule block: route is reachable, but the service edge itself is not accepting traffic.
        else if (tcpResults.Count > 0 && failedTcp.Length == tcpResults.Count && routeHealthy && failedDns.Length == 0)
        {
            scenarioKey = ScenarioCompanyEdgeServiceTcpFailure;
            overallStatus = "Action Needed";
            faultDomain = "Company edge or destination service";
            confidence = "High";
            userSummary = "The route to the company looks reachable, but the actual service ports are not accepting connections.";
            itSummary = "Route quality appears healthy while every configured TCP endpoint failed. That pattern points more to a service listener, firewall, VPN gateway, or company edge issue than a home network issue.";
            recommendations =
            [
                "Check the company VPN gateway, reverse proxy, firewall, or target service health.",
                "Compare with another user or monitoring source hitting the same service ports.",
                "Review server-side logs for refused or timed-out connections at the reported time."
            ];
        }
        // Rule block: the first clear degradation is close to the user device or gateway.
        else if (firstHopIssue is not null || severeLoss)
        {
            scenarioKey = ScenarioLocalNetworkOrWifi;
            overallStatus = "Action Needed";
            faultDomain = "Local network or Wi-Fi";
            confidence = firstHopIssue is not null ? "High" : "Medium";
            userSummary = "The issue appears to start very close to the device, which usually means Wi-Fi quality, the local router, or the home network.";
            itSummary = firstHopIssue is not null
                ? $"The first hop already shows degradation at hop {firstHopIssue.HopNumber} ({firstHopIssue.ScopeLabel}). Combined with packet loss {route.PingSummary.PacketLossPercent}%, this points to the user's LAN or gateway."
                : $"Packet loss is {route.PingSummary.PacketLossPercent}% with no stronger downstream signal. Treat the local access network as the first suspect.";
            recommendations =
            [
                "Ask the user to retry closer to the router or on wired ethernet if possible.",
                "Have the user restart the home router or reconnect Wi-Fi.",
                "If available, compare the same test from a mobile hotspot to isolate the home network."
            ];
        }
        // Rule block: the path goes bad early, but not right on the device.
        else if (accessHopIssue is not null || (firstSpike is not null && firstSpike.HopNumber <= 2))
        {
            scenarioKey = ScenarioIspOrAccessNetwork;
            overallStatus = "Warning";
            faultDomain = "ISP or access network";
            confidence = "Medium";
            userSummary = "The path becomes unstable very early, which usually points to the ISP side rather than the company systems.";
            itSummary = accessHopIssue is not null
                ? $"An abnormal signal appears by hop {accessHopIssue.HopNumber} ({accessHopIssue.ScopeLabel}). The issue likely sits between the user gateway and the ISP access edge."
                : $"Latency rises sharply by hop {firstSpike!.HopNumber}. That is early enough to suspect the access ISP rather than the destination service.";
            recommendations =
            [
                "Retry the diagnostic from a different network, such as a hotspot, to confirm the ISP path.",
                "If the same behavior persists over time, ask the user to contact the ISP with the report.",
                "Capture one or two repeat runs at different times to check whether the issue is bursty or sustained."
            ];
        }
        // Rule block: public transit looks suspicious before the destination segment.
        else if (firstSpike is not null && firstSpike.HopNumber < Math.Max(route.Hops.Count - 1, 3))
        {
            scenarioKey = ScenarioInternetTransitPath;
            overallStatus = moderateLoss || failedTcp.Length > 0 ? "Warning" : "Healthy";
            faultDomain = "Internet transit path";
            confidence = "Medium";
            userSummary = "The route shows a delay increase in the public network path, so the slowdown may be happening between the ISP and the destination side.";
            itSummary = $"Latency rises at hop {firstSpike.HopNumber}, after the access edge but before the destination. This looks more like transit or upstream path congestion than a purely local issue.";
            recommendations =
            [
                "Repeat the test later to confirm whether the public path issue is persistent.",
                "Compare the same route from another network or another user region if possible.",
                "If business impact is high, collect multiple reports and escalate with the ISP or upstream provider."
            ];
        }
        // Rule block: later hops or service probes point to the company side.
        else if (finalHopIssue || failedTcp.Length > 0)
        {
            scenarioKey = ScenarioCompanyNetworkOrDestinationService;
            overallStatus = failedTcp.Length > 0 ? "Action Needed" : "Warning";
            faultDomain = "Company network or destination service";
            confidence = failedTcp.Length > 0 ? "High" : "Medium";
            userSummary = "The later part of the path is where the symptoms appear, so the issue is more likely near the company edge or service itself.";
            itSummary = failedTcp.Length > 0
                ? $"One or more destination service ports failed even though the route reached the later hops. Investigate the company edge, VPN listener, or target service."
                : "The route remains mostly normal until the final segment, which suggests the company edge or destination host is the more likely fault domain.";
            recommendations =
            [
                "Check VPN gateway, remote desktop gateway, reverse proxy, or destination service health.",
                "Review firewall and listener status on the company side.",
                "Correlate with server-side monitoring and logs for the same time window."
            ];
        }
        // Rule block: all current checks look healthy, so avoid over-claiming a network fault.
        else if (routeHealthy && failedDns.Length == 0 && failedTcp.Length == 0)
        {
            scenarioKey = ScenarioNoClearNetworkFaultDetected;
            overallStatus = "Healthy";
            faultDomain = "No clear network fault detected";
            confidence = "Medium";
            userSummary = "The route, DNS lookups, and service port checks all look healthy right now.";
            itSummary = "No strong network-side issue stands out in this run. If the user still feels slowness, the problem may be application-specific, intermittent, or workload-related.";
            recommendations =
            [
                "If the issue continues, collect an additional report at the exact time of the slowdown.",
                "Check the target application, VPN client logs, or endpoint performance metrics.",
                "Compare this result with another run from the same device on a different network."
            ];
        }
        // Fallback: keep the message useful even when the evidence is mixed.
        else
        {
            scenarioKey = ScenarioIntermittentOrInconclusive;
            overallStatus = "Warning";
            faultDomain = "Intermittent or inconclusive";
            confidence = "Low";
            userSummary = "This run shows some warning signals, but they do not point to one clear fault domain yet.";
            itSummary = "The evidence is mixed. There are enough signals to keep investigating, but not enough to assign the fault confidently to home network, ISP, transit, or company edge.";
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
            ConfidenceLabel = confidence,
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
        // Keep evidence short and support-oriented:
        // enough to explain the result without turning the summary into raw telemetry.
        var evidence = new List<string>
        {
            $"Ping success rate: {route.PingSummary.SuccessRatePercent}% with average latency {(route.PingSummary.AverageRoundTripMs?.ToString() ?? "-")} ms."
        };

        if (route.PingSummary.PacketLossPercent > 0)
        {
            evidence.Add($"Packet loss detected: {route.PingSummary.PacketLossPercent}%.");
        }

        if (firstSpike is not null)
        {
            evidence.Add($"Latency step-up starts at hop {firstSpike.HopNumber} ({firstSpike.ScopeLabel}).");
        }

        if (route.Hops.Any(static hop => hop.IsTimeout))
        {
            evidence.Add("One or more traceroute hops timed out.");
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
            evidence.Add($"Failed TCP endpoints: {string.Join(", ", failedTcp.Select(static result => $"{result.Name} ({result.Host}:{result.Port})"))}.");
        }

        return evidence;
    }
}
