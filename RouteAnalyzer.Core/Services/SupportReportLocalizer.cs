using System.Text.RegularExpressions;
using RouteAnalyzer.Models;

namespace RouteAnalyzer.Services;

public static partial class SupportReportLocalizer
{
    public static LocalizedAssessmentView GetAssessmentView(SupportDiagnosticReport report, string? language)
    {
        if (!ReportLanguage.IsTraditionalChinese(language))
        {
            return new LocalizedAssessmentView(
                report.Assessment.OverallStatusLabel,
                report.Assessment.FaultDomain,
                report.Assessment.ConfidenceLabel,
                report.Assessment.UserSummary,
                report.Assessment.ItSummary,
                report.Assessment.EvidenceHighlights,
                report.Assessment.Recommendations);
        }

        var route = report.PrimaryRoute;
        var failedDns = report.DnsResults.Where(static result => !result.Success).ToArray();
        var failedTcp = report.TcpResults.Where(static result => !result.Success).ToArray();
        var firstSpike = route.Hops.FirstOrDefault(static hop => hop.SuspectedSpike);
        var lastHop = route.Hops.LastOrDefault();
        var routeHealthy = string.Equals(route.StatusLabel, "Stable", StringComparison.OrdinalIgnoreCase);
        var severeLoss = route.PingSummary.PacketLossPercent >= 40;
        var firstHopIssue = route.Hops.FirstOrDefault(static hop => hop.HopNumber == 1 && (hop.IsTimeout || hop.SuspectedSpike || (hop.AverageLatencyMs ?? 0) >= 20));
        var accessHopIssue = route.Hops.FirstOrDefault(static hop => hop.HopNumber <= 2 && (hop.SuspectedSpike || hop.IsTimeout));
        var finalHopIssue = lastHop is not null
            && (lastHop.IsTimeout || lastHop.SuspectedSpike || string.Equals(lastHop.ScopeLabel, "Destination", StringComparison.OrdinalIgnoreCase));

        var overallStatus = TranslateOverallStatus(report.Assessment.OverallStatusLabel, ReportLanguage.TraditionalChinese);
        var confidence = TranslateConfidence(report.Assessment.ConfidenceLabel, ReportLanguage.TraditionalChinese);

        return report.Assessment.ScenarioKey switch
        {
            DiagnosticAssessmentEngine.ScenarioLocalDnsOrInitialConnectivity => new LocalizedAssessmentView(
                overallStatus,
                "本機 DNS 或起始連線",
                confidence,
                "裝置無法解析必要主機名稱，因此連線在抵達公司服務之前就已經失敗。",
                "所有設定的 DNS 查詢都失敗，而且路由測試沒有收到 ICMP 回應。這通常代表本機解析器異常、網際網路未連線，或 VPN 尚未完成前置連線。",
                BuildEvidence(report, ReportLanguage.TraditionalChinese),
                [
                    "重新連上目前的網路，或切換到另一個網路後再試一次。",
                    "如果所有目的地都受影響，請重新啟動 Wi-Fi 或家用路由器。",
                    "如果此環境需要 VPN 才能解析內部名稱，請確認 VPN 用戶端已正確連線。"
                ]),
            DiagnosticAssessmentEngine.ScenarioCompanyEdgeServiceTcpFailure => new LocalizedAssessmentView(
                overallStatus,
                "公司邊界或目標服務",
                confidence,
                "到公司端的路徑看起來可達，但實際服務埠沒有接受連線。",
                "路由品質看起來健康，且 DNS 解析成功，但所有設定的 TCP 端點都失敗。這種型態更像是服務監聽、防火牆、VPN gateway，或公司邊界設備的問題，而不是使用者家中網路。",
                BuildEvidence(report, ReportLanguage.TraditionalChinese),
                [
                    "檢查公司 VPN gateway、reverse proxy、防火牆或目標服務的健康狀態。",
                    "和其他使用者或既有監控比對，確認相同服務埠是否也異常。",
                    "查看伺服器端日誌，確認在回報時間點是否有拒絕連線或逾時。"
                ]),
            DiagnosticAssessmentEngine.ScenarioLocalNetworkOrWifi => new LocalizedAssessmentView(
                overallStatus,
                "本地網路或 Wi-Fi",
                confidence,
                "問題看起來是在非常靠近這台裝置的位置開始，通常代表 Wi-Fi 品質、家用路由器，或本地網路有狀況。",
                firstHopIssue is not null
                    ? $"第一跳就出現異常，發生在第 {firstHopIssue.HopNumber} 跳（{GetHopScopeLabel(firstHopIssue, ReportLanguage.TraditionalChinese)}）。再加上封包遺失 {route.PingSummary.PacketLossPercent}% ，目前最優先懷疑使用者端 LAN 或 gateway。"
                    : $"目前封包遺失為 {route.PingSummary.PacketLossPercent}% ，而且沒有更明確的下游訊號，應先把本地接入網路視為首要懷疑點。",
                BuildEvidence(report, ReportLanguage.TraditionalChinese),
                [
                    "請使用者靠近路由器重試；如果可行，優先改用有線網路。",
                    "請使用者重新連接 Wi-Fi，或重新啟動家用路由器。",
                    "如果可以，改用手機熱點再跑一次，以切開家用網路因素。"
                ]),
            DiagnosticAssessmentEngine.ScenarioIspOrAccessNetwork => new LocalizedAssessmentView(
                overallStatus,
                "ISP 或接入網路",
                confidence,
                "路徑在前幾跳就開始不穩定，這通常比較像 ISP 側，而不是公司系統本身。",
                accessHopIssue is not null
                    ? $"異常訊號在第 {accessHopIssue.HopNumber} 跳（{GetHopScopeLabel(accessHopIssue, ReportLanguage.TraditionalChinese)}）就出現，問題較可能位在使用者 gateway 與 ISP 接入邊界之間。"
                    : $"延遲在第 {firstSpike?.HopNumber ?? 0} 跳開始明顯上升，位置夠前段，較像接入 ISP 問題，而不是目標服務本身。",
                BuildEvidence(report, ReportLanguage.TraditionalChinese),
                [
                    "改用另一個網路，例如手機熱點，再跑一次檢測以確認是不是 ISP 路徑問題。",
                    "如果同樣現象持續發生，可以請使用者帶著報告聯絡 ISP。",
                    "建議在不同時間再跑 1 到 2 次，確認問題是尖峰型還是持續型。"
                ]),
            DiagnosticAssessmentEngine.ScenarioInternetTransitPath => new LocalizedAssessmentView(
                overallStatus,
                "公網 transit 路徑",
                confidence,
                "公網中段路徑出現延遲增加，因此速度變慢有可能發生在 ISP 與目標端之間。",
                $"延遲在第 {firstSpike?.HopNumber ?? 0} 跳開始上升，位置落在接入邊界之後、目標之前，這比較像 transit 或上游路徑壅塞，而不是純本地問題。",
                BuildEvidence(report, ReportLanguage.TraditionalChinese),
                [
                    "稍晚重跑一次，確認公網路徑異常是否持續存在。",
                    "如果可以，和另一個網路或其他區域的使用者比對同一條路徑。",
                    "若業務影響高，建議蒐集多份報告後再向 ISP 或上游業者升級。"
                ]),
            DiagnosticAssessmentEngine.ScenarioCompanyNetworkOrDestinationService => new LocalizedAssessmentView(
                overallStatus,
                "公司網路或目標服務",
                confidence,
                "症狀出現在路徑後段，因此問題較可能靠近公司邊界或服務本身。",
                failedTcp.Length > 0
                    ? "一個或多個目標服務埠失敗，但路由已經走到後段 hop，建議優先調查公司邊界、VPN listener 或目標服務。"
                    : "整條路徑在前段都大致正常，異常集中在最後一段，因此公司邊界或目標主機是更可能的故障域。",
                BuildEvidence(report, ReportLanguage.TraditionalChinese),
                [
                    "檢查 VPN gateway、remote desktop gateway、reverse proxy 或目標服務健康狀態。",
                    "確認公司端防火牆規則與 listener 狀態。",
                    "把這份報告和同時間的伺服器監控與日誌一起比對。"
                ]),
            DiagnosticAssessmentEngine.ScenarioNoClearNetworkFaultDetected => new LocalizedAssessmentView(
                overallStatus,
                "未偵測到明確網路故障",
                confidence,
                "這次檢測中的路由、DNS 與服務埠檢查目前都看起來健康。",
                "這次執行沒有看到明顯的網路側問題。如果使用者仍然感覺緩慢，問題可能偏向應用程式本身、間歇性狀況，或端點負載。",
                BuildEvidence(report, ReportLanguage.TraditionalChinese),
                [
                    "如果問題再次出現，請在同一時間點再收一次報告。",
                    "檢查目標應用程式、VPN client 日誌，或端點效能指標。",
                    "把同一台裝置在不同網路下的結果做比對。"
                ]),
            _ => new LocalizedAssessmentView(
                overallStatus,
                severeLoss || finalHopIssue ? "需進一步確認" : "間歇性或資訊不足",
                confidence,
                "這次檢測有一些警訊，但還不足以直接指向單一故障域。",
                "目前證據比較混合，代表值得持續追查，但還不夠支持直接判定為家用網路、ISP、公網 transit 或公司邊界問題。",
                BuildEvidence(report, ReportLanguage.TraditionalChinese),
                [
                    "建議在問題實際發生時再收一次報告。",
                    "改用另一個網路再測一次，以切開本地與遠端因素。",
                    "把這份報告和應用程式或 VPN client 日誌一起看，再決定是否升級。"
                ])
        };
    }

    public static LocalizedRouteView GetRouteView(RouteDiagnosticReport route, string? language)
    {
        if (!ReportLanguage.IsTraditionalChinese(language))
        {
            return new LocalizedRouteView(
                route.StatusLabel,
                route.StatusSummary,
                route.Narrative,
                route.SuspectedIssue);
        }

        var localizedIssue = LocalizeSuspectedIssue(route);

        return new LocalizedRouteView(
            TranslateRouteStatus(route.StatusLabel, ReportLanguage.TraditionalChinese),
            BuildRouteStatusSummary(route, localizedIssue, ReportLanguage.TraditionalChinese),
            BuildRouteNarrative(route, localizedIssue, ReportLanguage.TraditionalChinese),
            localizedIssue);
    }

    public static string GetHopScopeLabel(RouteHop hop, string? language)
    {
        if (!ReportLanguage.IsTraditionalChinese(language))
        {
            return hop.ScopeLabel;
        }

        return hop.ScopeLabel switch
        {
            "No reply" => "未回應",
            "LAN / Gateway" => "本地網路 / Gateway",
            "Private network" => "私有網段",
            "Public hop" => "公網節點",
            "Destination" => "目標端",
            "Access / ISP edge" => "接入 / ISP 邊界",
            "Transit hop" => "Transit 節點",
            _ => hop.ScopeLabel
        };
    }

    public static string GetHopScopeDetail(RouteHop hop, string? language)
    {
        if (!ReportLanguage.IsTraditionalChinese(language))
        {
            return hop.ScopeDetail;
        }

        return hop.ScopeLabel switch
        {
            "No reply" => "此 hop 沒有回應 ICMP 探測。",
            "LAN / Gateway" => "通常代表本地路由器或第一跳 gateway。",
            "Private network" => "仍位於私有位址空間，常見於 LAN 或 ISP 接入側設備。",
            "Public hop" when !string.IsNullOrWhiteSpace(hop.ReverseDns) => $"PTR: {hop.ReverseDns}",
            "Public hop" => "公網中的中繼節點。",
            "Destination" => "這一跳看起來就是目標主機。",
            "Access / ISP edge" => "通常靠近本地網路邊界或 ISP 接入邊界。",
            "Transit hop" => "公網中的中間節點，常見於上游或骨幹 transit。",
            _ => hop.ScopeDetail
        };
    }

    public static string GetHopNote(RouteHop hop, string? language)
    {
        if (!ReportLanguage.IsTraditionalChinese(language))
        {
            return hop.Note;
        }

        if (hop.IsTimeout)
        {
            return "這一跳沒有回覆 ICMP，但單憑這點還不能直接視為故障。";
        }

        if (hop.SuspectedSpike && hop.LatencyDeltaMs is int delta)
        {
            return $"和前一跳相比，延遲增加了 {delta} ms。";
        }

        return "這一跳沒有看到明顯的延遲階梯。";
    }

    public static string TranslateOverallStatus(string status, string? language)
    {
        if (!ReportLanguage.IsTraditionalChinese(language))
        {
            return status;
        }

        return status switch
        {
            "Healthy" => "健康",
            "Warning" => "警告",
            "Action Needed" => "需要處理",
            _ => status
        };
    }

    public static string TranslateConfidence(string confidence, string? language)
    {
        if (!ReportLanguage.IsTraditionalChinese(language))
        {
            return confidence;
        }

        return confidence switch
        {
            "High" => "高",
            "Medium" => "中",
            "Low" => "低",
            _ => confidence
        };
    }

    public static string TranslateRouteStatus(string status, string? language)
    {
        if (!ReportLanguage.IsTraditionalChinese(language))
        {
            return status;
        }

        return status switch
        {
            "Stable" => "穩定",
            "Observe" => "觀察",
            "Investigate" => "需調查",
            "Critical" => "嚴重",
            _ => status
        };
    }

    public static string Text(string key, string? language)
    {
        var zh = ReportLanguage.IsTraditionalChinese(language);

        return key switch
        {
            "ReportTitle" => zh ? "Route Analyzer 支援報告" : "Route Analyzer Support Report",
            "RunDetails" => zh ? "執行資訊" : "Run Details",
            "UserSummary" => zh ? "使用者摘要" : "User Summary",
            "ItSummary" => zh ? "IT 摘要" : "IT Summary",
            "Company" => zh ? "公司" : "Company",
            "Machine" => zh ? "裝置名稱" : "Machine",
            "Target" => zh ? "目標" : "Target",
            "Generated" => zh ? "產生時間" : "Generated",
            "ExecutionId" => zh ? "執行 ID" : "Execution ID",
            "FaultDomain" => zh ? "故障域" : "Fault domain",
            "Confidence" => zh ? "信心" : "Confidence",
            "ChecksOverview" => zh ? "檢查概覽" : "Checks overview",
            "DetailHint" => zh ? "完整細節請參考 HTML 報告、JSON 或 route-hops.csv。" : "For full detail, use the HTML report, JSON, or route-hops.csv.",
            "AverageLatency" => zh ? "平均延遲" : "Average latency",
            "PacketLoss" => zh ? "封包遺失" : "Packet loss",
            "Jitter" => zh ? "抖動" : "Jitter",
            "DnsChecks" => zh ? "DNS 檢查" : "DNS checks",
            "TcpChecks" => zh ? "TCP 檢查" : "TCP checks",
            "Duration" => zh ? "耗時" : "Duration",
            "Status" => zh ? "狀態" : "Status",
            "Detail" => zh ? "詳細資訊" : "Detail",
            "Endpoint" => zh ? "端點" : "Endpoint",
            "Name" => zh ? "名稱" : "Name",
            "Hostname" => zh ? "主機名稱" : "Hostname",
            "RouteDetail" => zh ? "路由細節" : "Route Detail",
            "RouteSummary" => zh ? "路由摘要" : "Route Summary",
            "Narrative" => zh ? "敘述" : "Narrative",
            "Hops" => zh ? "跳點" : "Hops",
            "Hop" => zh ? "Hop" : "Hop",
            "Address" => zh ? "位址" : "Address",
            "Avg" => zh ? "平均" : "Avg",
            "Delta" => zh ? "差值" : "Delta",
            "Scope" => zh ? "範圍" : "Scope",
            "Samples" => zh ? "樣本" : "Samples",
            "PtrGeo" => zh ? "PTR / 地理" : "PTR / Geo",
            "Note" => zh ? "說明" : "Note",
            "RawTracerouteOutput" => zh ? "原始 Traceroute 輸出" : "Raw Traceroute Output",
            "NoDnsChecks" => zh ? "未設定 DNS 檢查。" : "No DNS checks were configured.",
            "NoTcpChecks" => zh ? "未設定 TCP 端點檢查。" : "No TCP endpoints were configured.",
            "NoParsableHops" => zh ? "這次沒有擷取到可解析的 hop。" : "No parsable hops were captured.",
            "Pass" => zh ? "通過" : "Pass",
            "Fail" => zh ? "失敗" : "Fail",
            "Language" => zh ? "語言" : "Language",
            "English" => zh ? "英文" : "English",
            "TraditionalChinese" => zh ? "繁中" : "Traditional Chinese",
            "PathHighlights" => zh ? "路徑亮點" : "Path Highlights",
            "HopsParsed" => zh ? "已解析 hops" : "Hops parsed",
            "TimeoutHops" => zh ? "超時 hops" : "Timeout hops",
            "FirstSpike" => zh ? "第一個尖峰" : "First spike",
            "GeoProvider" => zh ? "Geo 資料來源" : "Geo provider",
            "DiagnosticMode" => zh ? "診斷模式" : "Diagnostic mode",
            "Command" => zh ? "命令" : "Command",
            "Runtime" => zh ? "執行環境" : "Runtime",
            "None" => zh ? "無" : "None",
            "NextSteps" => zh ? "下一步建議" : "Next steps",
            "Evidence" => zh ? "判斷依據" : "Evidence",
            "Summary" => zh ? "摘要" : "Summary",
            "Loss" => zh ? "遺失" : "Loss",
            "ConsoleTitle" => zh ? "Route Analyzer" : "Route Analyzer",
            _ => key
        };
    }

    private static IReadOnlyList<string> BuildEvidence(SupportDiagnosticReport report, string? language)
    {
        if (!ReportLanguage.IsTraditionalChinese(language))
        {
            return report.Assessment.EvidenceHighlights;
        }

        var route = report.PrimaryRoute;
        var failedDns = report.DnsResults.Where(static result => !result.Success).ToArray();
        var failedTcp = report.TcpResults.Where(static result => !result.Success).ToArray();
        var firstSpike = route.Hops.FirstOrDefault(static hop => hop.SuspectedSpike);
        var evidence = new List<string>
        {
            $"Ping 成功率: {route.PingSummary.SuccessRatePercent}% ，平均延遲 {route.PingSummary.AverageRoundTripMs?.ToString() ?? "-"} ms。"
        };

        if (route.PingSummary.PacketLossPercent > 0)
        {
            evidence.Add($"偵測到封包遺失: {route.PingSummary.PacketLossPercent}% 。");
        }

        if (firstSpike is not null)
        {
            evidence.Add($"延遲階梯從第 {firstSpike.HopNumber} 跳開始（{GetHopScopeLabel(firstSpike, language)}）。");
        }

        if (route.Hops.Any(static hop => hop.IsTimeout))
        {
            evidence.Add("一個或多個 traceroute hop 未回應。");
        }

        if (report.DnsResults.Count > 0)
        {
            evidence.Add($"DNS 檢查通過: {report.DnsResults.Count - failedDns.Length}/{report.DnsResults.Count}。");
        }

        if (failedDns.Length > 0)
        {
            evidence.Add($"DNS 失敗項目: {string.Join("、", failedDns.Select(static result => result.Name))}。");
        }

        if (report.TcpResults.Count > 0)
        {
            evidence.Add($"TCP 檢查通過: {report.TcpResults.Count - failedTcp.Length}/{report.TcpResults.Count}。");
        }

        if (failedTcp.Length > 0)
        {
            evidence.Add($"TCP 失敗端點: {string.Join("、", failedTcp.Select(static result => $"{result.Name} ({result.Host}:{result.Port})"))}。");
        }

        return evidence;
    }

    private static string? LocalizeSuspectedIssue(RouteDiagnosticReport route)
    {
        if (string.IsNullOrWhiteSpace(route.SuspectedIssue))
        {
            return null;
        }

        if (route.SuspectedIssue.StartsWith("Unable to start traceroute command:", StringComparison.Ordinal))
        {
            return $"無法啟動 traceroute 指令: {route.SuspectedIssue["Unable to start traceroute command:".Length..].Trim()}";
        }

        if (route.SuspectedIssue.StartsWith("Traceroute command timed out", StringComparison.Ordinal))
        {
            return "traceroute 指令執行逾時。";
        }

        if (route.SuspectedIssue.Equals("Traceroute returned no parsable hops", StringComparison.Ordinal))
        {
            return "traceroute 沒有產生可解析的 hop。";
        }

        var match = LatencyIncreaseRegex().Match(route.SuspectedIssue);
        if (match.Success)
        {
            return $"延遲從第 {match.Groups["hop"].Value} 跳開始明顯上升";
        }

        if (route.SuspectedIssue.Equals("Packet loss is elevated across the full path", StringComparison.Ordinal))
        {
            return "整條路徑的封包遺失偏高";
        }

        if (route.SuspectedIssue.Equals("One or more hops timed out, but timeout-only signals are inconclusive", StringComparison.Ordinal))
        {
            return "有一個或多個 hop 超時，但僅靠 timeout 還不足以直接判定故障域";
        }

        return route.SuspectedIssue;
    }

    private static string BuildRouteStatusSummary(RouteDiagnosticReport route, string? localizedIssue, string? language)
    {
        if (!ReportLanguage.IsTraditionalChinese(language))
        {
            return route.StatusSummary;
        }

        return route.StatusLabel switch
        {
            "Critical" => "封包遺失已高到足以懷疑端到端連線問題。",
            "Investigate" when !string.IsNullOrWhiteSpace(localizedIssue) => localizedIssue,
            "Investigate" => "這條路徑的訊號雜訊偏高或資訊不完整，需要再深入檢查。",
            "Observe" when route.Hops.Any(static hop => hop.IsTimeout) => "有些 hop 沒有回應，建議先重跑一次，再判斷 timeout 是否真的是故障點。",
            "Observe" when route.PingSummary.PacketLossPercent > 0 => "路徑大致可達，但輕微封包遺失或單一延遲跳升值得持續觀察。",
            _ => "目前路徑看起來整體穩定，沒有看到明顯瓶頸訊號。"
        };
    }

    private static string BuildRouteNarrative(RouteDiagnosticReport route, string? localizedIssue, string? language)
    {
        if (!ReportLanguage.IsTraditionalChinese(language))
        {
            return route.Narrative;
        }

        if (!string.IsNullOrWhiteSpace(route.SuspectedIssue) && route.SuspectedIssue.StartsWith("Unable to start traceroute command:", StringComparison.Ordinal))
        {
            return $"Ping 已完成，但 traceroute 指令執行失敗: {route.SuspectedIssue["Unable to start traceroute command:".Length..].Trim()}。請確認本機有可用的 traceroute 工具後再試。";
        }

        if (route.Hops.Count == 0)
        {
            return "這次執行沒有產生可解析的 hop。可能是目標端過濾回應，或 traceroute 輸出格式與目前解析器不一致。";
        }

        if (!string.IsNullOrWhiteSpace(localizedIssue))
        {
            return $"目前平均 Ping 為 {route.PingSummary.AverageRoundTripMs?.ToString() ?? "-"} ms，主要訊號是: {localizedIssue}。建議在不同時間多跑幾次，確認這個型態是否穩定存在。";
        }

        return $"目前平均 Ping 為 {route.PingSummary.AverageRoundTripMs?.ToString() ?? "-"} ms，路徑沒有出現明顯延遲階梯。如果體感仍然不穩，可能偏向突發流量行為或目標端飽和。";
    }

    [GeneratedRegex(@"Latency increases noticeably from hop (?<hop>\d+)", RegexOptions.CultureInvariant)]
    private static partial Regex LatencyIncreaseRegex();
}

public readonly record struct LocalizedAssessmentView(
    string OverallStatusLabel,
    string FaultDomain,
    string ConfidenceLabel,
    string UserSummary,
    string ItSummary,
    IReadOnlyList<string> EvidenceHighlights,
    IReadOnlyList<string> Recommendations);

public readonly record struct LocalizedRouteView(
    string StatusLabel,
    string StatusSummary,
    string Narrative,
    string? SuspectedIssue);
