# Route Analyzer

Route Analyzer is a .NET 10 Razor Pages tool that turns a single `ping` + `tracert` run into an operator-friendly network path report.
It is designed as a portfolio project for infra, SRE, DevOps, and cloud-facing roles, so the app emphasizes incident-style summaries,
exportable evidence, runtime metadata, and a small but intentional operational surface.

## What This Project Demonstrates

- Network troubleshooting workflow instead of CRUD-only UI work.
- Server-side enrichment of traceroute hops with PTR, geo, ASN, and ISP context.
- Operator-oriented reporting with status levels such as `Stable`, `Observe`, `Investigate`, and `Critical`.
- Production-minded ASP.NET Core setup with config validation, response compression, forwarded headers, and `/healthz`.
- Clear separation between page model orchestration, diagnostic services, and presentation.

## Stack

- .NET 10
- ASP.NET Core Razor Pages
- Native `System.Net.NetworkInformation.Ping`
- Windows `tracert` parsing
- `ipwho.is` for public IP enrichment

## Run Locally

```bash
dotnet run
```

Then open the local app and try targets such as:

- `1.1.1.1`
- `8.8.8.8`
- `github.com`
- `cloudflare.com`

## Operational Endpoint

- `GET /healthz`: simple JSON health output for runtime checks.

## Current Limitations

- Detailed traceroute parsing currently targets Windows.
- Geo enrichment depends on the availability of the upstream public API.
- This project focuses on single-run diagnostics rather than continuous monitoring.

## Why It Fits Infra / SRE / Cloud Roles

This project is intentionally positioned as a lightweight diagnostics workbench rather than a generic web app.
It shows how I think about troubleshooting UX, safe host setup, execution metadata, and operational clarity in addition to UI polish.
