using RouteAnalyzer.Models;

namespace RouteAnalyzer.Services;

public static class SupportReportLocalizer
{
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
            return "此跳沒有回覆 ICMP。";
        }

        if (hop.LatencyDeltaMs is int delta && delta >= 25)
        {
            return $"比前一跳延遲增加了 {delta} ms。";
        }

        return "沒有明顯的延遲變化。";
    }

    public static string Text(string key, string? language)
    {
        var zh = ReportLanguage.IsTraditionalChinese(language);

        return key switch
        {
            "ReportTitle" => zh ? "Route Analyzer 連線檢測報告" : "Route Analyzer Capture Report",
            "RunDetails" => zh ? "執行資訊" : "Run Details",
            "Destination" => zh ? "目的端" : "Destination",
            "Machine" => zh ? "裝置名稱" : "Machine",
            "ConnectionType" => zh ? "連線類型" : "Connection type",
            "ActiveAdapter" => zh ? "主要網卡" : "Active adapter",
            "DefaultGateway" => zh ? "預設閘道" : "Default gateway",
            "DnsServers" => zh ? "DNS 伺服器" : "DNS servers",
            "Target" => zh ? "目標" : "Target",
            "Generated" => zh ? "產生時間" : "Generated",
            "ExecutionId" => zh ? "執行 ID" : "Execution ID",
            "Overview" => zh ? "檢測摘要" : "Capture overview",
            "Signals" => zh ? "檢測訊號" : "Captured signals",
            "Latency" => zh ? "延遲" : "Latency",
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
            "HopsParsed" => zh ? "已解析 hops" : "Hops parsed",
            "TimeoutHops" => zh ? "超時 hops" : "Timeout hops",
            "GeoProvider" => zh ? "Geo 資料來源" : "Geo provider",
            "DiagnosticMode" => zh ? "診斷模式" : "Diagnostic mode",
            "Command" => zh ? "命令" : "Command",
            "Runtime" => zh ? "執行環境" : "Runtime",
            "None" => zh ? "無" : "None",
            "Summary" => zh ? "摘要" : "Summary",
            "Loss" => zh ? "遺失" : "Loss",
            "CopyHandoff" => zh ? "複製檢測摘要" : "Copy capture summary",
            "Copied" => zh ? "已複製" : "Copied",
            _ => key
        };
    }
}
