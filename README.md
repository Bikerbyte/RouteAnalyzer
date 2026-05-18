# Route Analyzer

輕量的 local-first 網路檢測與視覺化工具。

它的定位不是判斷「誰的網路壞了」，而是快速收集事實訊號：ping、packet loss、DNS、TCP、traceroute hops、本機網卡資訊，然後整理成可以交給 support / IT 的 snapshot。

## 給使用者

使用者不需要安裝 .NET，也不需要 `dotnet build`。

1. 從 GitHub Release 下載 `RouteAnalyzer.App-win-x64.zip`。
2. 解壓縮整個資料夾。
3. 雙擊 `Start-RouteAnalyzer.cmd` 或 `RouteAnalyzer.App.exe`。
4. 瀏覽器會自動開啟本機診斷頁。
5. 輸入目標，例如 `vpn.company.com`、`github.com`、`1.1.1.1`，按 Run diagnostic。

檢測結果會存在 app 同層的 `reports/app/<report-id>/`。

## App 會檢測什麼

- ping average / packet loss / jitter
- DNS lookup 是否成功
- TCP port 是否可連
- traceroute hop 明細
- hop timeout 與 latency step-up 標記
- 本機連線資訊，例如 adapter、gateway、DNS servers
- HTML / JSON / CSV / text report

## 不做什麼

Route Analyzer 不做 rule-based fault domain 判斷。

它不會宣稱問題一定是 Wi-Fi、ISP、transit 或目的端服務。這些結論很容易不準，也很難維護。工具只呈現可檢查、可轉交、可重跑的檢測訊號。

## 給維護者

本機產生 Windows portable app：

```powershell
./scripts/publish-app.ps1 -Runtime win-x64 -Configuration Release
```

輸出位置：

```text
artifacts/app/win-x64
```

GitHub Actions 也可以手動執行 `Publish portable app` workflow 產生下載 artifact。推 `v*` tag 時會把 zip 放到 GitHub Release。

## 開發

```powershell
dotnet build RouteAnalyzer.sln
dotnet test RouteAnalyzer.sln
```

本機跑 app：

```powershell
dotnet run --project RouteAnalyzer.App --urls http://localhost:5015
```

開啟：

```text
http://localhost:5015
```

## CLI

CLI 保留給進階或自動化情境。

使用 profile：

```powershell
RouteAnalyzer.Cli.exe --profile-file .\routeanalyzer.profile.json
```

快速測目標：

```powershell
RouteAnalyzer.Cli.exe --target github.com
```

產生 sample profile：

```powershell
RouteAnalyzer.Cli.exe --create-sample-profile
```

常用參數：

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
