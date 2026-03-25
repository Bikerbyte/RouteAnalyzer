# Route Analyzer

Route Analyzer is a practical network diagnostics toolkit for infrastructure, SRE, DevOps, and cloud workflows.
It now ships as a full toolchain:

- `RouteAnalyzer` (Web UI + API)
- `RouteAnalyzer.Cli` (local command-line executable)
- `RouteAnalyzer.Core` (shared diagnostics engine)

The goal is simple: run diagnostics where the problem actually happens, then export and share incident-ready evidence.

## Architecture

- **RouteAnalyzer.Core**
  - Shared models/options/services used by both Web and CLI.
  - Runs ICMP ping + platform traceroute command (`tracert` on Windows, `traceroute` on Linux/macOS).
  - Produces a structured route report with status labels, hop metadata, and optional geo enrichment.
- **RouteAnalyzer (Web)**
  - Razor Pages dashboard for visual review.
  - Programmatic API under `/api/v1`.
  - Operational endpoint `/healthz`.
- **RouteAnalyzer.Cli**
  - Runs directly on the suspect machine.
  - Outputs `text`, `json`, or `csv`.

## Why This Is Useful In Real Incidents

If a user says "this specific laptop is slow", server-side diagnostics alone are not enough.
With this setup you can:

1. Run CLI on the affected machine for local path truth.
2. Export JSON/CSV as incident evidence.
3. Use the Web UI/API for visualization and integration.

## Web API

Base path: `/api/v1`

- `GET /api/v1/info`
- `GET /api/v1/diagnostics/sample-targets`
- `POST /api/v1/diagnostics/route`

Example request:

```json
{
  "targetHost": "1.1.1.1",
  "pingCount": 4,
  "maxHops": 24,
  "includeGeoDetails": true
}
```

## CLI Usage

```bash
dotnet run --project RouteAnalyzer.Cli -- --target 1.1.1.1 --ping-count 5 --max-hops 24
```

Output format examples:

```bash
# JSON to console
dotnet run --project RouteAnalyzer.Cli -- --target github.com --format json

# CSV file
dotnet run --project RouteAnalyzer.Cli -- --target cloudflare.com --format csv --output .\reports\route.csv

# No geo lookup
dotnet run --project RouteAnalyzer.Cli -- --target 8.8.8.8 --no-geo
```

### CLI Arguments

- `--target <value>` hostname, IP, or URL
- `--ping-count <3-10>`
- `--max-hops <4-64>`
- `--format <text|json|csv>`
- `--output <path>`
- `--no-geo`
- `--help`

## Publish EXE

PowerShell helper:

```powershell
./scripts/publish-cli.ps1 -Runtime win-x64
```

Artifacts output to `artifacts/cli/<runtime>`.

## Local Run (Web)

```bash
dotnet run --project RouteAnalyzer.csproj
```

Then open the local URL and use the Diagnostics page.

## Verification

```bash
dotnet build RouteAnalyzer.sln
dotnet test RouteAnalyzer.sln --no-build
```

## Operational Surface

- `GET /healthz` health JSON
- Response compression enabled
- Forwarded headers handling enabled
- Options validation on startup
- API rate limit on route diagnostics endpoint

## Configuration

`appsettings.json` -> `RouteAnalyzer` section:

- `DefaultTarget`
- `DefaultPingCount`
- `DefaultMaxHops`
- `DefaultIncludeGeoDetails`
- `PingTimeoutMs`
- `TracerouteProbeTimeoutMs`
- `TracerouteProcessTimeoutSeconds`

## Current Limitations

- Traceroute command availability depends on host environment (`tracert`/`traceroute` presence and permissions).
- Geo enrichment depends on upstream API availability.
- This tool is designed for point-in-time diagnosis, not continuous telemetry collection.
