<!DOCTYPE html>
<html lang="en" class="lang-en" data-report-language="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>Remote Support - Network - Route Analyzer Report</title>
  <style>
    :root { color-scheme: light; --bg: #f6f7f9; --panel: #ffffff; --line: #e5e7eb; --ink: #111827; --muted: #6b7280; --healthy: #15803d; --healthy-bg: #ecfdf3; --warning: #b45309; --warning-bg: #fffbeb; --action: #b91c1c; --action-bg: #fef2f2; --info: #1d4ed8; --info-bg: #eff6ff; font-family: 'Segoe UI', Tahoma, sans-serif; }
    * { box-sizing: border-box; }
    body { margin: 0; background: var(--bg); color: var(--ink); }
    .page { max-width: 960px; margin: 0 auto; padding: 24px 16px 48px; }
    .header { display: flex; justify-content: space-between; align-items: flex-start; gap: 16px; margin-bottom: 20px; flex-wrap: wrap; }
    .eyebrow { font-size: 12px; text-transform: uppercase; letter-spacing: .12em; color: var(--muted); margin-bottom: 8px; }
    h1, h2, h3, p { margin: 0; }
    h1 { font-size: 28px; line-height: 1.15; }
    .meta { display: grid; grid-template-columns: repeat(3, auto); gap: 8px; color: var(--muted); font-size: 13px; margin-top: 12px; }
    .section { background: var(--panel); border: 1px solid var(--line); border-radius: 12px; padding: 16px; margin-top: 12px; }
    .top-grid { display: grid; grid-template-columns: 1.35fr .95fr; gap: 12px; }
    .status-row { display: flex; align-items: center; gap: 10px; flex-wrap: wrap; margin-bottom: 12px; }
    .badge { display: inline-block; padding: 6px 10px; border-radius: 999px; font-size: 13px; font-weight: 700; border: 1px solid transparent; }
    .status-healthy { color: var(--healthy); background: var(--healthy-bg); border-color: #bbf7d0; }
    .status-warning { color: var(--warning); background: var(--warning-bg); border-color: #fcd34d; }
    .status-action-needed { color: var(--action); background: var(--action-bg); border-color: #fecaca; }
    .subtle { color: var(--muted); font-size: 14px; line-height: 1.6; }
    .callout { padding: 12px 14px; border-radius: 10px; border: 1px solid #bfdbfe; background: var(--info-bg); margin-top: 14px; }
    .callout strong { display: block; margin-bottom: 6px; font-size: 14px; color: var(--info); }
    .mini-grid { display: grid; grid-template-columns: repeat(2, 1fr); gap: 10px; margin-top: 12px; }
    .mini { border: 1px solid var(--line); border-radius: 10px; padding: 12px; background: #fcfcfd; }
    .mini .label { font-size: 12px; text-transform: uppercase; letter-spacing: .08em; color: var(--muted); margin-bottom: 6px; display: block; }
    .mini .value { font-size: 24px; font-weight: 800; line-height: 1.1; display: block; }
    .mini .note { margin-top: 6px; color: var(--muted); font-size: 13px; line-height: 1.5; }
    .list { display: grid; gap: 10px; margin-top: 10px; }
    .row { display: flex; justify-content: space-between; gap: 12px; padding-bottom: 10px; border-bottom: 1px solid var(--line); font-size: 14px; }
    .row:last-child { border-bottom: none; padding-bottom: 0; }
    .row span:first-child { color: var(--muted); }
    .two-col { display: grid; grid-template-columns: 1fr 1fr; gap: 12px; }
    ul { margin: 10px 0 0; padding-left: 18px; line-height: 1.7; }
    .alert { background: var(--warning-bg); border-color: #fcd34d; }
    .alert h2 { font-size: 16px; }
    table { width: 100%; border-collapse: collapse; margin-top: 10px; font-size: 14px; }
    th, td { text-align: left; padding: 10px 8px; border-bottom: 1px solid var(--line); vertical-align: top; }
    th { font-size: 12px; text-transform: uppercase; letter-spacing: .08em; color: var(--muted); }
    .chart { margin-top: 10px; height: 160px; border: 1px solid var(--line); border-radius: 10px; background: linear-gradient(to top, rgba(17,24,39,.04) 1px, transparent 1px) 0 0/100% 35px, linear-gradient(to right, rgba(17,24,39,.04) 1px, transparent 1px) 0 0/48px 100%; position: relative; overflow: hidden; }
    .chart svg { position: absolute; inset: 0; }
    .chart-axis { display: flex; justify-content: space-between; gap: 12px; padding: 8px 4px 0 48px; color: var(--muted); font-size: 12px; }
    details { margin-top: 12px; background: var(--panel); border: 1px solid var(--line); border-radius: 12px; overflow: hidden; }
    summary { cursor: pointer; padding: 14px 16px; font-weight: 700; list-style: none; }
    summary::-webkit-details-marker { display: none; }
    .detail-body { padding: 0 16px 16px; }
    .button-row { display: flex; gap: 8px; flex-wrap: wrap; margin-top: 12px; }
    .btn { border: 1px solid var(--line); background: #fff; border-radius: 10px; padding: 9px 12px; font-size: 14px; color: var(--ink); cursor: pointer; }
    code { font-family: Consolas, 'Courier New', monospace; font-size: 12px; }
    pre { white-space: pre-wrap; word-break: break-word; color: var(--muted); font-size: 13px; line-height: 1.6; margin: 0; }
    .lang-switch { display: inline-flex; align-items: center; gap: 8px; }
    .lang-btn { border: 1px solid var(--line); border-radius: 999px; padding: 8px 12px; cursor: pointer; background: #fff; color: var(--muted); font-weight: 700; }
    .lang-btn[aria-pressed='true'] { color: var(--ink); border-color: #cbd5e1; }
    [data-lang='en'], [data-lang='zh-TW'] { display: inline; }
    html.lang-en [data-lang='zh-TW'] { display: none !important; }
    html.lang-zh [data-lang='en'] { display: none !important; }
    @media (max-width: 760px) { .top-grid, .two-col, .mini-grid, .meta { grid-template-columns: 1fr; } }
  </style>
</head>
<body>
  <div class="page">
    <div class="header">
      <div>
        <div class="eyebrow"><span data-lang="en">RouteAnalyzer / Minimal triage report</span><span data-lang="zh-TW">RouteAnalyzer / 分析結果</span></div>
        <h1>Remote Support - Network</h1>
      </div>
      <div>
        <div class="lang-switch" role="group" aria-label="Language switch">
          <button type="button" class="lang-btn" data-switch-language="en">English</button>
          <button type="button" class="lang-btn" data-switch-language="zh-TW">繁中</button>
        </div>
        <div class="meta">
          <div><span data-lang="en"><strong>Execution ID:</strong> <code>6833248ffe81</code></span><span data-lang="zh-TW"><strong>執行 ID:</strong> <code>6833248ffe81</code></span></div>
          <div><span data-lang="en"><strong>Target:</strong> chatgpt.com</span><span data-lang="zh-TW"><strong>目標:</strong> chatgpt.com</span></div>
          <div><span data-lang="en"><strong>Machine:</strong> MR-27526</span><span data-lang="zh-TW"><strong>裝置名稱:</strong> MR-27526</span></div>
        </div>
      </div>
    </div>
    <div class="top-grid">
      <section class="section">
        <div class="status-row">
          <span class="badge status-action-needed"><span data-lang="en">Action Needed</span><span data-lang="zh-TW">需要處理</span></span>
          <span class="subtle"><span data-lang="en">Possible fault domain: Local network or Wi-Fi</span><span data-lang="zh-TW">可能故障區段: 本地網路或 Wi-Fi</span></span>
        </div>
        <h2 style="font-size:24px; line-height:1.25;"><span data-lang="en">This run suggests the issue starts very close to the device, which is often consistent with Wi-Fi quality, the router&#39;s proxy and firewall settings.</span><span data-lang="zh-TW">結果顯示在非常靠近這台裝置的節點便出現問題，常見於 Wi-Fi 品質、路由器的 Proxy 和防火牆設定。</span></h2>
        <p class="subtle" style="margin-top:10px;"><span data-lang="en">Packet loss is 100% with no stronger downstream signal. The local access network is still the first place to verify.</span><span data-lang="zh-TW">目前封包遺失為 100% ，而且沒有更明確的下游訊號，建議先從本地接入網路開始確認。</span></p>
        <div class="callout">
          <strong><span data-lang="en">Recommended next action</span><span data-lang="zh-TW">建議下一步</span></strong>
          <span><span data-lang="en">Try to use wired ethernet if possible, and check proxy or firewall settings.</span><span data-lang="zh-TW">請優先改用有線網路，並檢查路由器的 Proxy 和防火牆設定。</span></span>
        </div>
        <div class="mini-grid">
          <div class="mini">
  <span class="label"><span data-lang="en">Latency</span><span data-lang="zh-TW">延遲</span></span>
  <span class="value">- ms</span>
  <div class="note"><span data-lang="en">No end-to-end latency average was captured in this run.</span><span data-lang="zh-TW">這次沒有可用的平均延遲。</span></div>
</div>
          <div class="mini">
  <span class="label"><span data-lang="en">Packet loss</span><span data-lang="zh-TW">封包遺失</span></span>
  <span class="value">100%</span>
  <div class="note"><span data-lang="en">Packet loss was observed in this run.</span><span data-lang="zh-TW">這次有觀察到封包遺失。</span></div>
</div>
          <div class="mini">
  <span class="label"><span data-lang="en">DNS checks</span><span data-lang="zh-TW">DNS 檢查</span></span>
  <span class="value">1 / 1</span>
  <div class="note"><span data-lang="en">Target resolution succeeded.</span><span data-lang="zh-TW">目標名稱解析成功。</span></div>
</div>
          <div class="mini">
  <span class="label"><span data-lang="en">TCP checks</span><span data-lang="zh-TW">TCP 檢查</span></span>
  <span class="value">1 / 1</span>
  <div class="note"><span data-lang="en">Configured service ports were reachable.</span><span data-lang="zh-TW">目標服務埠可達。</span></div>
</div>
        </div>
      </section>
      <aside class="section">
        <h2 style="font-size:16px;"><span data-lang="en">Run Details</span><span data-lang="zh-TW">執行資訊</span></h2>
        <div class="list">
          <div class="row"><span><span data-lang="en">Destination</span><span data-lang="zh-TW">目的端</span></span><span><span data-lang="en">OpenAI</span><span data-lang="zh-TW">OpenAI</span></span></div>
          <div class="row"><span><span data-lang="en">Generated</span><span data-lang="zh-TW">產生時間</span></span><span><span data-lang="en">2026-04-02 08:50:03 UTC</span><span data-lang="zh-TW">2026-04-02 08:50:03 UTC</span></span></div>
          <div class="row"><span><span data-lang="en">Connection type</span><span data-lang="zh-TW">連線類型</span></span><span><span data-lang="en">Ethernet</span><span data-lang="zh-TW">Ethernet</span></span></div>
          <div class="row"><span><span data-lang="en">Active adapter</span><span data-lang="zh-TW">主要網卡</span></span><span><span data-lang="en">乙太網路</span><span data-lang="zh-TW">乙太網路</span></span></div>
          <div class="row"><span><span data-lang="en">Default gateway</span><span data-lang="zh-TW">預設閘道</span></span><span><span data-lang="en">172.17.68.254</span><span data-lang="zh-TW">172.17.68.254</span></span></div>
          <div class="row"><span><span data-lang="en">DNS servers</span><span data-lang="zh-TW">DNS 伺服器</span></span><span><span data-lang="en">172.17.22.88, 172.18.12.1</span><span data-lang="zh-TW">172.17.22.88, 172.18.12.1</span></span></div>
        </div>
        <div class="button-row">
          <button type="button" class="btn" data-copy-key="itSummary" data-label-en="Copy IT summary" data-label-zh="複製 IT 摘要">Copy IT summary</button>
          <button type="button" class="btn" data-copy-key="incidentNote" data-label-en="Copy incident note" data-label-zh="複製事件摘要">Copy incident note</button>
        </div>
      </aside>
    </div>
    <section class="section alert">
      <h2><span data-lang="en">Highlighted anomalies</span><span data-lang="zh-TW">值得注意的訊號</span></h2>
      <ul>
  <li><span data-lang="en">Packet loss was observed at 100%.</span><span data-lang="zh-TW">封包遺失為 100%。</span></li>
  <li><span data-lang="en">Traceroute timeouts were observed at hop 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24.</span><span data-lang="zh-TW">這次 traceroute 在 hop 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24 出現 timeout。</span></li>
</ul>

    </section>
    <div class="two-col">
      <section class="section">
        <h2 style="font-size:16px;"><span data-lang="en">Observations</span><span data-lang="zh-TW">觀察到的訊號</span></h2>
        <ul>
  <li><span data-lang="en">Ping success rate observed: 0% with average latency - ms.</span><span data-lang="zh-TW">Ping 成功率: 0% ，平均延遲 - ms。</span></li>
  <li><span data-lang="en">Packet loss observed: 100%.</span><span data-lang="zh-TW">偵測到封包遺失: 100% 。</span></li>
  <li><span data-lang="en">One or more traceroute hops timed out during this run.</span><span data-lang="zh-TW">一個或多個 traceroute hop 未回應。</span></li>
  <li><span data-lang="en">DNS checks passed: 1/1.</span><span data-lang="zh-TW">DNS 檢查通過: 1/1。</span></li>
</ul>

      </section>
      <section class="section">
        <h2 style="font-size:16px;"><span data-lang="en">Next steps</span><span data-lang="zh-TW">下一步建議</span></h2>
        <ul>
  <li><span data-lang="en">Try to use wired ethernet if possible, and check proxy or firewall settings.</span><span data-lang="zh-TW">請優先改用有線網路，並檢查路由器的 Proxy 和防火牆設定。</span></li>
  <li><span data-lang="en">Have the user restart the home router or reconnect Wi-Fi.</span><span data-lang="zh-TW">請使用者重新連接 Wi-Fi，或重新啟動家用路由器。</span></li>
  <li><span data-lang="en">If available, compare the same test from a mobile hotspot to isolate the home network.</span><span data-lang="zh-TW">如果可以，改用手機熱點再跑一次，以切開家用網路因素。</span></li>
</ul>

      </section>
    </div>
    <section class="section">
      <h2 style="font-size:16px;"><span data-lang="en">Route Summary</span><span data-lang="zh-TW">路由摘要</span></h2>
      <p class="subtle" style="margin-top:8px;"><span data-lang="en">Packet loss is high enough to suggest an end-to-end connectivity problem.</span><span data-lang="zh-TW">封包遺失已高到足以懷疑端到端連線問題。</span></p>
      <p class="subtle" style="margin-top:8px;"><span data-lang="en">Average ping is - ms and the primary signal is: Packet loss is elevated across the full path. Compare with repeated runs across different times to confirm whether the behavior is stable.</span><span data-lang="zh-TW">目前平均 Ping 為 - ms，主要訊號是: 整條路徑的封包遺失偏高。建議在不同時間多跑幾次，確認這個型態是否穩定存在。</span></p>
      <div class="chart"><svg viewBox="0 0 800 160" preserveAspectRatio="none" aria-hidden="true">
  <line x1="48" y1="132" x2="780" y2="132" stroke="rgba(107,114,128,.12)" stroke-width="1" />
<text x="40" y="136" text-anchor="end" font-size="11" fill="#6b7280">0 ms</text>
<line x1="48" y1="74" x2="780" y2="74" stroke="rgba(107,114,128,.12)" stroke-width="1" />
<text x="40" y="78" text-anchor="end" font-size="11" fill="#6b7280">10 ms</text>
<line x1="48" y1="16" x2="780" y2="16" stroke="rgba(107,114,128,.12)" stroke-width="1" />
<text x="40" y="20" text-anchor="end" font-size="11" fill="#6b7280">20 ms</text>

  <line x1="400" y1="132" x2="400" y2="137" stroke="rgba(107,114,128,.35)" stroke-width="1" />

  <line x1="48" y1="132" x2="780" y2="132" stroke="rgba(107,114,128,.35)" stroke-width="1" />
  <line x1="48" y1="16" x2="48" y2="132" stroke="rgba(107,114,128,.35)" stroke-width="1" />
  <polyline fill="none" stroke="#2563eb" stroke-width="3" points="400,126.2" />
  
</svg></div>
      <div class="chart-axis"><span>hop 1</span></div>
      <table>
  <thead>
    <tr><th><span data-lang="en">Hop</span><span data-lang="zh-TW">Hop</span></th><th><span data-lang="en">Signal</span><span data-lang="zh-TW">訊號</span></th><th><span data-lang="en">Interpretation</span><span data-lang="zh-TW">判讀</span></th></tr>
  </thead>
  <tbody>
    <tr><td><span data-lang="en">2</span><span data-lang="zh-TW">2</span></td><td><span data-lang="en">Intermediate timeout</span><span data-lang="zh-TW">中間 hop timeout</span></td><td><span data-lang="en">Interpret together with downstream evidence.</span><span data-lang="zh-TW">需要和後續 hop 的結果一起判讀。</span></td></tr>
    <tr><td><span data-lang="en">3</span><span data-lang="zh-TW">3</span></td><td><span data-lang="en">Intermediate timeout</span><span data-lang="zh-TW">中間 hop timeout</span></td><td><span data-lang="en">Interpret together with downstream evidence.</span><span data-lang="zh-TW">需要和後續 hop 的結果一起判讀。</span></td></tr>
    <tr><td><span data-lang="en">4</span><span data-lang="zh-TW">4</span></td><td><span data-lang="en">Intermediate timeout</span><span data-lang="zh-TW">中間 hop timeout</span></td><td><span data-lang="en">Interpret together with downstream evidence.</span><span data-lang="zh-TW">需要和後續 hop 的結果一起判讀。</span></td></tr>
    <tr><td><span data-lang="en">5</span><span data-lang="zh-TW">5</span></td><td><span data-lang="en">Intermediate timeout</span><span data-lang="zh-TW">中間 hop timeout</span></td><td><span data-lang="en">Interpret together with downstream evidence.</span><span data-lang="zh-TW">需要和後續 hop 的結果一起判讀。</span></td></tr>
    <tr><td><span data-lang="en">6</span><span data-lang="zh-TW">6</span></td><td><span data-lang="en">Intermediate timeout</span><span data-lang="zh-TW">中間 hop timeout</span></td><td><span data-lang="en">Interpret together with downstream evidence.</span><span data-lang="zh-TW">需要和後續 hop 的結果一起判讀。</span></td></tr>
    <tr><td><span data-lang="en">7</span><span data-lang="zh-TW">7</span></td><td><span data-lang="en">Intermediate timeout</span><span data-lang="zh-TW">中間 hop timeout</span></td><td><span data-lang="en">Interpret together with downstream evidence.</span><span data-lang="zh-TW">需要和後續 hop 的結果一起判讀。</span></td></tr>
  </tbody>
</table>

    </section>
    <details open>
      <summary><span data-lang="en">Full route detail</span><span data-lang="zh-TW">完整路由細節</span></summary>
      <div class="detail-body"><table><thead><tr><th><span data-lang="en">Hop</span><span data-lang="zh-TW">Hop</span></th><th><span data-lang="en">Address</span><span data-lang="zh-TW">位址</span></th><th><span data-lang="en">Avg</span><span data-lang="zh-TW">平均</span></th><th><span data-lang="en">Delta</span><span data-lang="zh-TW">差值</span></th><th><span data-lang="en">Scope</span><span data-lang="zh-TW">範圍</span></th><th><span data-lang="en">Note</span><span data-lang="zh-TW">說明</span></th></tr></thead><tbody>
<tr><td>1</td><td>172.17.68.253</td><td>1 ms</td><td>- ms</td><td><span data-lang="en">LAN / Gateway</span><span data-lang="zh-TW">本地網路 / Gateway</span></td><td><span data-lang="en">No obvious step-up is visible at this hop.</span><span data-lang="zh-TW">沒有明顯的延遲。</span></td></tr>
<tr><td>2</td><td>*</td><td>* ms</td><td>- ms</td><td><span data-lang="en">No reply</span><span data-lang="zh-TW">未回應</span></td><td><span data-lang="en">This hop did not reply to ICMP.</span><span data-lang="zh-TW">此跳沒有回覆 ICMP。</span></td></tr>
<tr><td>3</td><td>*</td><td>* ms</td><td>- ms</td><td><span data-lang="en">No reply</span><span data-lang="zh-TW">未回應</span></td><td><span data-lang="en">This hop did not reply to ICMP.</span><span data-lang="zh-TW">此跳沒有回覆 ICMP。</span></td></tr>
<tr><td>4</td><td>*</td><td>* ms</td><td>- ms</td><td><span data-lang="en">No reply</span><span data-lang="zh-TW">未回應</span></td><td><span data-lang="en">This hop did not reply to ICMP.</span><span data-lang="zh-TW">此跳沒有回覆 ICMP。</span></td></tr>
<tr><td>5</td><td>*</td><td>* ms</td><td>- ms</td><td><span data-lang="en">No reply</span><span data-lang="zh-TW">未回應</span></td><td><span data-lang="en">This hop did not reply to ICMP.</span><span data-lang="zh-TW">此跳沒有回覆 ICMP。</span></td></tr>
<tr><td>6</td><td>*</td><td>* ms</td><td>- ms</td><td><span data-lang="en">No reply</span><span data-lang="zh-TW">未回應</span></td><td><span data-lang="en">This hop did not reply to ICMP.</span><span data-lang="zh-TW">此跳沒有回覆 ICMP。</span></td></tr>
<tr><td>7</td><td>*</td><td>* ms</td><td>- ms</td><td><span data-lang="en">No reply</span><span data-lang="zh-TW">未回應</span></td><td><span data-lang="en">This hop did not reply to ICMP.</span><span data-lang="zh-TW">此跳沒有回覆 ICMP。</span></td></tr>
<tr><td>8</td><td>*</td><td>* ms</td><td>- ms</td><td><span data-lang="en">No reply</span><span data-lang="zh-TW">未回應</span></td><td><span data-lang="en">This hop did not reply to ICMP.</span><span data-lang="zh-TW">此跳沒有回覆 ICMP。</span></td></tr>
<tr><td>9</td><td>*</td><td>* ms</td><td>- ms</td><td><span data-lang="en">No reply</span><span data-lang="zh-TW">未回應</span></td><td><span data-lang="en">This hop did not reply to ICMP.</span><span data-lang="zh-TW">此跳沒有回覆 ICMP。</span></td></tr>
<tr><td>10</td><td>*</td><td>* ms</td><td>- ms</td><td><span data-lang="en">No reply</span><span data-lang="zh-TW">未回應</span></td><td><span data-lang="en">This hop did not reply to ICMP.</span><span data-lang="zh-TW">此跳沒有回覆 ICMP。</span></td></tr>
<tr><td>11</td><td>*</td><td>* ms</td><td>- ms</td><td><span data-lang="en">No reply</span><span data-lang="zh-TW">未回應</span></td><td><span data-lang="en">This hop did not reply to ICMP.</span><span data-lang="zh-TW">此跳沒有回覆 ICMP。</span></td></tr>
<tr><td>12</td><td>*</td><td>* ms</td><td>- ms</td><td><span data-lang="en">No reply</span><span data-lang="zh-TW">未回應</span></td><td><span data-lang="en">This hop did not reply to ICMP.</span><span data-lang="zh-TW">此跳沒有回覆 ICMP。</span></td></tr>
<tr><td>13</td><td>*</td><td>* ms</td><td>- ms</td><td><span data-lang="en">No reply</span><span data-lang="zh-TW">未回應</span></td><td><span data-lang="en">This hop did not reply to ICMP.</span><span data-lang="zh-TW">此跳沒有回覆 ICMP。</span></td></tr>
<tr><td>14</td><td>*</td><td>* ms</td><td>- ms</td><td><span data-lang="en">No reply</span><span data-lang="zh-TW">未回應</span></td><td><span data-lang="en">This hop did not reply to ICMP.</span><span data-lang="zh-TW">此跳沒有回覆 ICMP。</span></td></tr>
<tr><td>15</td><td>*</td><td>* ms</td><td>- ms</td><td><span data-lang="en">No reply</span><span data-lang="zh-TW">未回應</span></td><td><span data-lang="en">This hop did not reply to ICMP.</span><span data-lang="zh-TW">此跳沒有回覆 ICMP。</span></td></tr>
<tr><td>16</td><td>*</td><td>* ms</td><td>- ms</td><td><span data-lang="en">No reply</span><span data-lang="zh-TW">未回應</span></td><td><span data-lang="en">This hop did not reply to ICMP.</span><span data-lang="zh-TW">此跳沒有回覆 ICMP。</span></td></tr>
<tr><td>17</td><td>*</td><td>* ms</td><td>- ms</td><td><span data-lang="en">No reply</span><span data-lang="zh-TW">未回應</span></td><td><span data-lang="en">This hop did not reply to ICMP.</span><span data-lang="zh-TW">此跳沒有回覆 ICMP。</span></td></tr>
<tr><td>18</td><td>*</td><td>* ms</td><td>- ms</td><td><span data-lang="en">No reply</span><span data-lang="zh-TW">未回應</span></td><td><span data-lang="en">This hop did not reply to ICMP.</span><span data-lang="zh-TW">此跳沒有回覆 ICMP。</span></td></tr>
<tr><td>19</td><td>*</td><td>* ms</td><td>- ms</td><td><span data-lang="en">No reply</span><span data-lang="zh-TW">未回應</span></td><td><span data-lang="en">This hop did not reply to ICMP.</span><span data-lang="zh-TW">此跳沒有回覆 ICMP。</span></td></tr>
<tr><td>20</td><td>*</td><td>* ms</td><td>- ms</td><td><span data-lang="en">No reply</span><span data-lang="zh-TW">未回應</span></td><td><span data-lang="en">This hop did not reply to ICMP.</span><span data-lang="zh-TW">此跳沒有回覆 ICMP。</span></td></tr>
<tr><td>21</td><td>*</td><td>* ms</td><td>- ms</td><td><span data-lang="en">No reply</span><span data-lang="zh-TW">未回應</span></td><td><span data-lang="en">This hop did not reply to ICMP.</span><span data-lang="zh-TW">此跳沒有回覆 ICMP。</span></td></tr>
<tr><td>22</td><td>*</td><td>* ms</td><td>- ms</td><td><span data-lang="en">No reply</span><span data-lang="zh-TW">未回應</span></td><td><span data-lang="en">This hop did not reply to ICMP.</span><span data-lang="zh-TW">此跳沒有回覆 ICMP。</span></td></tr>
<tr><td>23</td><td>*</td><td>* ms</td><td>- ms</td><td><span data-lang="en">No reply</span><span data-lang="zh-TW">未回應</span></td><td><span data-lang="en">This hop did not reply to ICMP.</span><span data-lang="zh-TW">此跳沒有回覆 ICMP。</span></td></tr>
<tr><td>24</td><td>*</td><td>* ms</td><td>- ms</td><td><span data-lang="en">No reply</span><span data-lang="zh-TW">未回應</span></td><td><span data-lang="en">This hop did not reply to ICMP.</span><span data-lang="zh-TW">此跳沒有回覆 ICMP。</span></td></tr>
</tbody></table>
</div>
    </details>
    <details>
      <summary><span data-lang="en">DNS checks</span><span data-lang="zh-TW">DNS 檢查</span></summary>
      <div class="detail-body"><table><thead><tr><th><span data-lang="en">Name</span><span data-lang="zh-TW">名稱</span></th><th><span data-lang="en">Hostname</span><span data-lang="zh-TW">主機名稱</span></th><th><span data-lang="en">Status</span><span data-lang="zh-TW">狀態</span></th><th><span data-lang="en">Duration</span><span data-lang="zh-TW">耗時</span></th><th><span data-lang="en">Detail</span><span data-lang="zh-TW">詳細資訊</span></th></tr></thead><tbody>
<tr><td>OpenAI</td><td>chatgpt.com</td><td><span data-lang="en">Pass</span><span data-lang="zh-TW">通過</span></td><td>13 ms</td><td>104.18.32.47, 172.64.155.209</td></tr>
</tbody></table>
</div>
    </details>
    <details>
      <summary><span data-lang="en">TCP checks</span><span data-lang="zh-TW">TCP 檢查</span></summary>
      <div class="detail-body"><table><thead><tr><th><span data-lang="en">Name</span><span data-lang="zh-TW">名稱</span></th><th><span data-lang="en">Endpoint</span><span data-lang="zh-TW">端點</span></th><th><span data-lang="en">Status</span><span data-lang="zh-TW">狀態</span></th><th><span data-lang="en">Duration</span><span data-lang="zh-TW">耗時</span></th><th><span data-lang="en">Detail</span><span data-lang="zh-TW">詳細資訊</span></th></tr></thead><tbody>
<tr><td>OpenAI</td><td>chatgpt.com:443</td><td><span data-lang="en">Pass</span><span data-lang="zh-TW">通過</span></td><td>15 ms</td><td><span data-lang="en">Connection established.</span><span data-lang="zh-TW">連線已建立。</span></td></tr>
</tbody></table>
</div>
    </details>
    <details>
      <summary><span data-lang="en">Raw Traceroute Output</span><span data-lang="zh-TW">原始 Traceroute 輸出</span></summary>
      <div class="detail-body"><pre>在上限 24 個躍點上
追蹤 chatgpt.com [104.18.32.47] 的路由:
1     1 ms    &lt;1 ms    &lt;1 ms  172.17.68.253
2     *        *        *     要求等候逾時。
3     *        *        *     要求等候逾時。
4     *        *        *     要求等候逾時。
5     *        *        *     要求等候逾時。
6     *        *        *     要求等候逾時。
7     *        *        *     要求等候逾時。
8     *        *        *     要求等候逾時。
9     *        *        *     要求等候逾時。
10     *        *        *     要求等候逾時。
11     *        *        *     要求等候逾時。
12     *        *        *     要求等候逾時。
13     *        *        *     要求等候逾時。
14     *        *        *     要求等候逾時。
15     *        *        *     要求等候逾時。
16     *        *        *     要求等候逾時。
17     *        *        *     要求等候逾時。
18     *        *        *     要求等候逾時。
19     *        *        *     要求等候逾時。
20     *        *        *     要求等候逾時。
21     *        *        *     要求等候逾時。
22     *        *        *     要求等候逾時。
23     *        *        *     要求等候逾時。
24     *        *        *     要求等候逾時。
追蹤完成。</pre></div>
    </details>
  </div>
  <script type="application/json" id="copy-payload">{"itSummary":{"en":"Status: Action Needed\r\nPossible fault domain: Local network or Wi-Fi\r\nOverall finding: This run suggests the issue starts very close to the device, which is often consistent with Wi-Fi quality, the router\u0027s proxy and firewall settings.\r\nInterpretation: Packet loss is 100% with no stronger downstream signal. The local access network is still the first place to verify.\r\nRoute Summary: Packet loss is high enough to suggest an end-to-end connectivity problem.\r\nRecommended next action: Try to use wired ethernet if possible, and check proxy or firewall settings.\r\nExecution ID: 6833248ffe81","zh-TW":"\u72C0\u614B: \u9700\u8981\u8655\u7406\r\n\u53EF\u80FD\u6545\u969C\u5340\u6BB5: \u672C\u5730\u7DB2\u8DEF\u6216 Wi-Fi\r\n\u7E3D\u9AD4\u89C0\u5BDF: \u7D50\u679C\u986F\u793A\u5728\u975E\u5E38\u9760\u8FD1\u9019\u53F0\u88DD\u7F6E\u7684\u7BC0\u9EDE\u4FBF\u51FA\u73FE\u554F\u984C\uFF0C\u5E38\u898B\u65BC Wi-Fi \u54C1\u8CEA\u3001\u8DEF\u7531\u5668\u7684 Proxy \u548C\u9632\u706B\u7246\u8A2D\u5B9A\u3002\r\n\u5224\u8B80: \u76EE\u524D\u5C01\u5305\u907A\u5931\u70BA 100% \uFF0C\u800C\u4E14\u6C92\u6709\u66F4\u660E\u78BA\u7684\u4E0B\u6E38\u8A0A\u865F\uFF0C\u5EFA\u8B70\u5148\u5F9E\u672C\u5730\u63A5\u5165\u7DB2\u8DEF\u958B\u59CB\u78BA\u8A8D\u3002\r\n\u8DEF\u7531\u6458\u8981: \u5C01\u5305\u907A\u5931\u5DF2\u9AD8\u5230\u8DB3\u4EE5\u61F7\u7591\u7AEF\u5230\u7AEF\u9023\u7DDA\u554F\u984C\u3002\r\n\u5EFA\u8B70\u4E0B\u4E00\u6B65: \u8ACB\u512A\u5148\u6539\u7528\u6709\u7DDA\u7DB2\u8DEF\uFF0C\u4E26\u6AA2\u67E5\u8DEF\u7531\u5668\u7684 Proxy \u548C\u9632\u706B\u7246\u8A2D\u5B9A\u3002\r\n\u57F7\u884C ID: 6833248ffe81"},"incidentNote":{"en":"Target: chatgpt.com\r\nMachine: MR-27526\r\nStatus: Action Needed\r\nPossible fault domain: Local network or Wi-Fi\r\nObservations: Ping success rate observed: 0% with average latency - ms.\r\nRecommended next action: Try to use wired ethernet if possible, and check proxy or firewall settings.","zh-TW":"\u76EE\u6A19: chatgpt.com\r\n\u88DD\u7F6E\u540D\u7A31: MR-27526\r\n\u72C0\u614B: \u9700\u8981\u8655\u7406\r\n\u53EF\u80FD\u6545\u969C\u5340\u6BB5: \u672C\u5730\u7DB2\u8DEF\u6216 Wi-Fi\r\n\u89C0\u5BDF\u5230\u7684\u8A0A\u865F: Ping \u6210\u529F\u7387: 0% \uFF0C\u5E73\u5747\u5EF6\u9072 - ms\u3002\r\n\u5EFA\u8B70\u4E0B\u4E00\u6B65: \u8ACB\u512A\u5148\u6539\u7528\u6709\u7DDA\u7DB2\u8DEF\uFF0C\u4E26\u6AA2\u67E5\u8DEF\u7531\u5668\u7684 Proxy \u548C\u9632\u706B\u7246\u8A2D\u5B9A\u3002"}}</script>
  <script>
    (() => {
      const root = document.documentElement;
      const languageButtons = document.querySelectorAll('[data-switch-language]');
      const copyButtons = document.querySelectorAll('[data-copy-key]');
      const copyPayload = JSON.parse(document.getElementById('copy-payload').textContent || '{}');
      const currentLanguage = () => root.classList.contains('lang-zh') ? 'zh-TW' : 'en';
      const setCopyLabels = () => {
        const language = currentLanguage();
        copyButtons.forEach((button) => {
          button.textContent = language === 'zh-TW' ? button.dataset.labelZh : button.dataset.labelEn;
        });
      };
      const applyLanguage = (language) => {
        root.classList.toggle('lang-en', language === 'en');
        root.classList.toggle('lang-zh', language === 'zh-TW');
        languageButtons.forEach((button) => button.setAttribute('aria-pressed', button.dataset.switchLanguage === language ? 'true' : 'false'));
        setCopyLabels();
      };
      languageButtons.forEach((button) => button.addEventListener('click', () => applyLanguage(button.dataset.switchLanguage)));
      copyButtons.forEach((button) => button.addEventListener('click', async () => {
        const language = currentLanguage();
        const payload = copyPayload?.[button.dataset.copyKey]?.[language];
        if (!payload) {
          return;
        }

        await navigator.clipboard.writeText(payload);
        const originalLabel = language === 'zh-TW' ? button.dataset.labelZh : button.dataset.labelEn;
        button.textContent = language === 'zh-TW' ? '已複製' : 'Copied';
        window.setTimeout(() => { button.textContent = originalLabel; }, 1200);
      }));
      applyLanguage(root.dataset.reportLanguage === 'zh-TW' ? 'zh-TW' : 'en');
    })();
  </script>
</body>
</html>
