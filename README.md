# Route Analyzer

Route Analyzer is a client-side network diagnostics EXE for helpdesk and remote support workflows.
It is designed for the situation where a user says "remote access is slow" and IT needs evidence that separates local Wi-Fi, ISP, transit, DNS, and destination-side service problems.

## What It Does

- Runs on the affected machine, not just on the server side
- Supports a reusable helpdesk profile with fixed DNS and TCP checks
- Runs:
  - ICMP ping
  - traceroute / tracert
  - DNS lookups
  - TCP connectivity checks
- Produces:
  - a user-friendly summary
  - an IT-focused summary
  - a full report bundle with `summary.txt`, `report.json`, `report.html`, and `route-hops.csv`
  - bilingual report output with English / Traditional Chinese switching in HTML
- Adds automatic fault-domain hints such as:
  - local network or Wi-Fi
  - ISP / access network
  - public transit path
  - destination edge or destination service
  - DNS or initial connectivity

## Solution Shape

- `RouteAnalyzer.Core`
  - Diagnostics engine, profile loader, attribution logic, and report exporters
- `RouteAnalyzer.Cli`
  - EXE entry point for support staff or end users
- `RouteAnalyzer.Tests`
  - Unit tests for parsing, attribution, and report generation

## Profile-Driven Mode

The intended helpdesk workflow is profile-driven.
A profile defines the main target plus any DNS or TCP checks that should always run for your environment.

Example profile file: `routeanalyzer.profile.example.json`

The CLI also knows how to generate one:

```powershell
dotnet run --project RouteAnalyzer.Cli -- --create-sample-profile
```

If a file named `routeanalyzer.profile.json` exists in the current directory or next to the EXE, running the EXE without arguments will use it automatically.

## Quick Start

Run with a helpdesk profile:

```powershell
dotnet run --project RouteAnalyzer.Cli -- --profile-file .\routeanalyzer.profile.json
```

Run an ad hoc quick diagnostic:

```powershell
dotnet run --project RouteAnalyzer.Cli -- --target vpn.example.com
```

Generate only console output:

```powershell
dotnet run --project RouteAnalyzer.Cli -- --target 127.0.0.1 --console-only --format text
```

Write the full report bundle to a specific directory:

```powershell
dotnet run --project RouteAnalyzer.Cli -- --profile-file .\routeanalyzer.profile.json --report-dir .\reports\case-001
```

## Output Bundle

By default, the CLI writes a bundle under `.\reports\<timestamp-target>\`.
After the bundle is written, the CLI will also try to open `report.html` automatically for quick review.

Bundle contents:

- `summary.txt`
- `report.json`
- `report.html`
- `route-hops.csv`

The HTML report contains:

- traffic-light style overall status
- English / Traditional Chinese toggle
- user summary
- IT summary
- DNS check table
- TCP check table
- route hop table
- raw traceroute output

## CLI Options

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

## Publish EXE

```powershell
./scripts/publish-cli.ps1 -Runtime win-x64 -Configuration Release
```

Artifacts are written to `artifacts/cli/<runtime>`.

## Verification

```powershell
dotnet build RouteAnalyzer.sln
dotnet test RouteAnalyzer.sln
```

## Current Limitations

- `tracert` / `traceroute` must exist on the host
- Geo enrichment depends on `ipwho.is`
- The tool is still point-in-time diagnostics, not continuous monitoring
