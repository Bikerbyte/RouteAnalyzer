# Route Analyzer

一個 local-first 網路路徑診斷工具。
當 user 回報「連線很慢」「VPN 很卡」「網站連不上」時，可以直接開本機診斷台，輸入目標後取得可轉交給 support / IT 的判讀、路徑圖、下一步建議與完整報告。

目前專案分成兩個入口：

- `RouteAnalyzer.App`：主要使用入口，提供本機 web UI 與互動式診斷流程。
- `RouteAnalyzer.Cli`：進階 / 自動化入口，保留 profile-driven 與 headless report bundle 流程。

## Demo
- 成功範例
<img width="736" height="514" alt="Screenshot 2026-04-02 193121" src="https://github.com/user-attachments/assets/8fcffdff-eafc-4abe-bd26-5b8e80d75932" />
<img width="638" height="576" alt="Screenshot 2026-04-02 193246" src="https://github.com/user-attachments/assets/1f3811b2-5905-4f77-b977-3dcbc0902bf0" />

<br/><br/>
- 異常範例
<img width="1417" height="878" alt="image" src="https://github.com/user-attachments/assets/52d3dcec-0be0-4d95-a5c4-9fe43c8279c0" />


輸出 summary：

- ping / packet loss / jitter
- DNS / TCP / route 訊號
- 可展開的完整 traceroute 與明細

## One-click App

產生可交付給 user 的 portable app：

```powershell
./scripts/publish-app.ps1 -Runtime win-x64 -Configuration Release
```

輸出位置：

```text
artifacts/app/win-x64
```

把整個資料夾交給 user，user 只要雙擊 `Start-RouteAnalyzer.cmd` 或 `RouteAnalyzer.App.exe`。App 會自己啟動 localhost 並開瀏覽器。

本機開發啟動：

```powershell
dotnet run --project RouteAnalyzer.App --urls http://localhost:5015
```

開啟：

```text
http://localhost:5015
```

診斷完成後，app 會在 `reports/app/<report-id>/` 產生完整 bundle，並可從畫面直接打開 `report.html`。

## CLI Guide

若當前目錄或 EXE 同層有 `routeanalyzer.profile.json`，直接執行便會直接使用該 profile：

```powershell
RouteAnalyzer.Cli.exe
```

用指定 profile 執行：

```powershell
dotnet run --project RouteAnalyzer.Cli -- --profile-file .\routeanalyzer.profile.json
```

測試指定 URL：

```powershell
dotnet run --project RouteAnalyzer.Cli -- --target vpn.example.com
```

Console only，不自動開報表：

```powershell
dotnet run --project RouteAnalyzer.Cli -- --target vpn.example.com --console-only --no-open
```

產生 sample profile：

```powershell
dotnet run --project RouteAnalyzer.Cli -- --create-sample-profile
```

## Profile 設定檔

這個工具目前為 profile-driven。

需將固定要檢查的目標、DNS lookup、TCP port 都寫進 profile，之後便可連同 profile 與執行檔一同提供給 user 運行。

範例檔案：[`routeanalyzer.profile.example.json`](/e:/Biker/Code/RouteAnalyzer/routeanalyzer.profile.example.json)

目前 profile 會用到這幾個核心欄位：

- `profileName`
- `destinationName`
- `targetHost`
- `dnsLookups`
- `tcpEndpoints`

## 產出結果

每次執行預設會產生一個報告資料夾，並自動開啟 `report.html`。

內容包含：

- `summary.txt`
  - 短摘要
- `report.json`
  - 後續分析或程式處理使用
- `report.html`
  - 直觀閱讀使用
- `route-hops.csv`
  - network hop 明細


## CLI 參數

- `--profile-file <path>`
- `--target <value>`
- `--ping-count <3-10>`
- `--max-hops <4-64>`
- `--format <bundle|text|json|csv|html>`
- `--output <path>`
- `--report-dir <path>`
- `--console-only`
- `--language <en|zh-TW>`
- `--create-sample-profile [path]`
- `--force`
- `--no-geo`
- `--no-open`
- `--help`

## Build / Publish

本機驗證：

```powershell
dotnet build RouteAnalyzer.sln
dotnet test RouteAnalyzer.sln
```

輸出 Windows EXE：

```powershell
./scripts/publish-cli.ps1 -Runtime win-x64 -Configuration Release
```

production output: `artifacts/cli/<runtime>`。
