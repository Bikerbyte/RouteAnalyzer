using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RouteAnalyzer.Models;
using RouteAnalyzer.Options;
using RouteAnalyzer.Services;

Console.OutputEncoding = Encoding.UTF8;
var exitCode = await CliApplication.RunAsync(args);
return exitCode;

internal static class CliApplication
{
    public static async Task<int> RunAsync(string[] args)
    {
        // Parse first so the main execution flow can stay linear and easy to scan.
        var parseResult = CliArguments.Parse(args);
        if (!parseResult.IsValid)
        {
            if (!string.IsNullOrWhiteSpace(parseResult.ErrorMessage))
            {
                Console.Error.WriteLine(parseResult.ErrorMessage);
                Console.Error.WriteLine();
            }

            PrintHelp();
            return parseResult.ShowHelp ? 0 : 1;
        }

        try
        {
            // Func: create an editable sample profile for helpdesk rollout.
            if (parseResult.CreateSampleProfile)
            {
                var samplePath = parseResult.SampleProfilePath
                    ?? Path.Combine(Directory.GetCurrentDirectory(), DiagnosticProfileLoader.DefaultFileName);

                DiagnosticProfileLoader.WriteSampleProfile(samplePath, overwrite: parseResult.Force);
                Console.WriteLine($"Sample profile written to: {Path.GetFullPath(samplePath)}");
                return 0;
            }

            // Main diagnostic flow
            var options = new RouteAnalyzerOptions();
            var profileResolution = ResolveExecutionProfile(parseResult, options);

            using var httpClient = new HttpClient
            {
                BaseAddress = new Uri("https://ipwho.is/"),
                Timeout = TimeSpan.FromSeconds(2.5)
            };

            var geoService = new IpGeoLookupService(httpClient, NullLogger<IpGeoLookupService>.Instance);
            var routeDiagnosticService = new NetworkRouteDiagnosticService(
                geoService,
                NullLogger<NetworkRouteDiagnosticService>.Instance,
                Options.Create(options));
            var supportDiagnosticService = new SupportDiagnosticService(
                routeDiagnosticService,
                NullLogger<SupportDiagnosticService>.Instance,
                Options.Create(options));

            var report = await supportDiagnosticService.RunAsync(profileResolution.Profile, CancellationToken.None);

            if (!string.IsNullOrWhiteSpace(profileResolution.ProfilePath))
            {
                Console.WriteLine($"Profile : {profileResolution.Profile.ProfileName}");
                Console.WriteLine($"Source  : {profileResolution.ProfilePath}");
            }

            await EmitOutputsAsync(report, parseResult);
            return 0;
        }
        catch (DiagnosticProfileException ex)
        {
            Console.Error.WriteLine($"Profile error: {ex.Message}");
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Route analysis failed: {ex.Message}");
            return 1;
        }
    }

    private static async Task EmitOutputsAsync(SupportDiagnosticReport report, CliArguments arguments)
    {
        var summary = BuildConsoleSummary(report);
        Console.WriteLine(summary);

        // Console-only mode is for quick triage and skips file output completely.
        if (arguments.ConsoleOnly)
        {
            return;
        }

        // Bundle mode is the default support workflow:
        // keep the summary, HTML report, and raw artifacts together in one folder.
        if (!string.IsNullOrWhiteSpace(arguments.ReportDirectory) || string.Equals(arguments.Format, "bundle", StringComparison.OrdinalIgnoreCase))
        {
            var reportDirectory = arguments.ReportDirectory
                ?? arguments.OutputPath
                ?? Path.Combine(Directory.GetCurrentDirectory(), "reports", SupportDiagnosticExportFormatter.BuildDefaultDirectoryName(report));

            var bundle = SupportDiagnosticExportFormatter.WriteBundle(report, reportDirectory);
            Console.WriteLine();
            Console.WriteLine($"Report bundle saved to: {bundle.DirectoryPath}");
            Console.WriteLine($"Main report : {bundle.HtmlPath}");
            Console.WriteLine($"Summary     : {bundle.SummaryPath}");
            Console.WriteLine($"JSON        : {bundle.JsonPath}");
            Console.WriteLine($"Route CSV   : {bundle.RouteCsvPath}");

            if (!arguments.SuppressAutoOpen)
            {
                TryOpenPath(bundle.HtmlPath, "HTML report");
            }

            return;
        }

        var output = BuildSingleOutput(report, arguments.Format);
        if (!string.IsNullOrWhiteSpace(arguments.OutputPath))
        {
            var fullPath = Path.GetFullPath(arguments.OutputPath);
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(fullPath, output, Encoding.UTF8);
            Console.WriteLine();
            Console.WriteLine($"Report saved to: {fullPath}");

            if (!arguments.SuppressAutoOpen && string.Equals(arguments.Format, "html", StringComparison.OrdinalIgnoreCase))
            {
                TryOpenPath(fullPath, "HTML report");
            }

            return;
        }

        Console.WriteLine();
        Console.WriteLine(output);
    }

    private static string BuildSingleOutput(SupportDiagnosticReport report, string format)
    {
        return format.ToLowerInvariant() switch
        {
            "json" => SupportDiagnosticExportFormatter.ToJson(report),
            "html" => SupportDiagnosticExportFormatter.ToHtml(report),
            "csv" => RouteDiagnosticExportFormatter.ToCsv(report.PrimaryRoute),
            _ => SupportDiagnosticExportFormatter.ToText(report)
        };
    }

    private static string BuildConsoleSummary(SupportDiagnosticReport report)
    {
        var dnsPassed = report.DnsResults.Count(static result => result.Success);
        var tcpPassed = report.TcpResults.Count(static result => result.Success);
        var builder = new StringBuilder();

        builder.AppendLine("Route Analyzer");
        builder.AppendLine("--------------");
        builder.AppendLine($"Status      : {report.Assessment.OverallStatusLabel}");
        builder.AppendLine($"Possible    : {report.Assessment.FaultDomain}");
        builder.AppendLine($"Target      : {report.Profile.TargetHost}");
        builder.AppendLine($"Ping Avg    : {report.PrimaryRoute.PingSummary.AverageRoundTripMs?.ToString() ?? "-"} ms");
        builder.AppendLine($"Packet Loss : {report.PrimaryRoute.PingSummary.PacketLossPercent}%");
        builder.AppendLine($"DNS         : {(report.DnsResults.Count == 0 ? "n/a" : $"{dnsPassed}/{report.DnsResults.Count} pass")}");
        builder.AppendLine($"TCP         : {(report.TcpResults.Count == 0 ? "n/a" : $"{tcpPassed}/{report.TcpResults.Count} pass")}");
        builder.AppendLine();
        builder.AppendLine(report.Assessment.UserSummary);

        var highlightedSignals = report.Assessment.EvidenceHighlights.Take(2).ToArray();
        if (highlightedSignals.Length > 0)
        {
            builder.AppendLine();
            builder.AppendLine("Observations:");
            foreach (var signal in highlightedSignals)
            {
                builder.AppendLine($"- {signal}");
            }
        }

        if (report.Assessment.Recommendations.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("Next steps:");
            foreach (var recommendation in report.Assessment.Recommendations.Take(3))
            {
                builder.AppendLine($"- {recommendation}");
            }
        }

        return builder.ToString().TrimEnd();
    }

    private static ProfileResolution ResolveExecutionProfile(CliArguments arguments, RouteAnalyzerOptions options)
    {
        var explicitProfilePath = string.IsNullOrWhiteSpace(arguments.ProfilePath)
            ? null
            : Path.GetFullPath(arguments.ProfilePath);

        var autoProfilePath = string.IsNullOrWhiteSpace(arguments.TargetHost) && explicitProfilePath is null
            ? DiagnosticProfileLoader.TryFindDefaultProfilePath()
            : null;

        var resolvedProfilePath = explicitProfilePath ?? autoProfilePath;
        DiagnosticProfile profile;

        if (resolvedProfilePath is not null)
        {
            // Profile-driven mode keeps DNS/TCP checks consistent across support cases.
            profile = DiagnosticProfileLoader.Load(resolvedProfilePath);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(arguments.TargetHost))
            {
                throw new DiagnosticProfileException(
                    $"No target was provided and no default profile file named {DiagnosticProfileLoader.DefaultFileName} was found.");
            }

            if (!TargetHostParser.TryNormalize(arguments.TargetHost, out var normalizedTarget))
            {
                throw new DiagnosticProfileException("Target must be a valid hostname, IP address, or URL.");
            }

            // Fall back to ad hoc mode for quick one-off diagnostics.
            profile = BuildQuickDiagnosticProfile(arguments, options, normalizedTarget);
        }

        var overriddenTarget = profile.TargetHost;
        if (!string.IsNullOrWhiteSpace(arguments.TargetHost))
        {
            if (!TargetHostParser.TryNormalize(arguments.TargetHost, out overriddenTarget))
            {
                throw new DiagnosticProfileException("Target must be a valid hostname, IP address, or URL.");
            }
        }

        var mergedProfile = new DiagnosticProfile
        {
            ProfileName = profile.ProfileName,
            DestinationName = profile.DestinationName,
            Description = profile.Description,
            PreferredLanguage = arguments.Language ?? profile.PreferredLanguage,
            TargetHost = overriddenTarget,
            PingCount = arguments.PingCount ?? profile.PingCount,
            MaxHops = arguments.MaxHops ?? profile.MaxHops,
            IncludeGeoDetails = arguments.IncludeGeoDetails ?? profile.IncludeGeoDetails,
            DnsLookups = profile.DnsLookups,
            TcpEndpoints = profile.TcpEndpoints
        };

        return new ProfileResolution(DiagnosticProfileLoader.Normalize(mergedProfile), resolvedProfilePath);
    }

    private static DiagnosticProfile BuildQuickDiagnosticProfile(
        CliArguments arguments,
        RouteAnalyzerOptions options,
        string normalizedTarget)
    {
        return new DiagnosticProfile
        {
            ProfileName = "Quick Diagnostic",
            Description = "Ad hoc route diagnostic without a saved helpdesk profile.",
            PreferredLanguage = arguments.Language ?? ReportLanguage.English,
            TargetHost = normalizedTarget,
            PingCount = arguments.PingCount ?? options.DefaultPingCount,
            MaxHops = arguments.MaxHops ?? options.DefaultMaxHops,
            IncludeGeoDetails = arguments.IncludeGeoDetails ?? options.DefaultIncludeGeoDetails
        };
    }

    private static void PrintHelp()
    {
        Console.WriteLine("RouteAnalyzer CLI");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine($"  RouteAnalyzer.Cli --profile-file <path-to-{DiagnosticProfileLoader.DefaultFileName}>");
        Console.WriteLine("  RouteAnalyzer.Cli --target <host>");
        Console.WriteLine("  RouteAnalyzer.Cli --create-sample-profile [path]");
        Console.WriteLine();
        Console.WriteLine("Default behavior:");
        Console.WriteLine($"  If {DiagnosticProfileLoader.DefaultFileName} exists in the current directory or next to the EXE, running with no arguments uses it.");
        Console.WriteLine("  If no output is specified, a full report bundle is written to ./reports/<timestamp-target>/.");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --profile-file <path>         Load a helpdesk profile with DNS/TCP templates.");
        Console.WriteLine("  --target <value>              Override or supply the primary target host.");
        Console.WriteLine("  --ping-count <value>          Ping probes (3-10).");
        Console.WriteLine("  --max-hops <value>            Traceroute max hops (4-64).");
        Console.WriteLine("  --format <bundle|text|json|csv|html>");
        Console.WriteLine("                                bundle is the default and writes summary.txt, report.json, report.html, and route-hops.csv.");
        Console.WriteLine("  --output <path>               File path for single-format output, or bundle directory when format is bundle.");
        Console.WriteLine("  --report-dir <path>           Explicit directory for a full report bundle.");
        Console.WriteLine("  --console-only                Print the summary only and skip file output.");
        Console.WriteLine("  --language <en|zh-TW>         Set the default report language.");
        Console.WriteLine("  --create-sample-profile [path]");
        Console.WriteLine("                                Write an editable sample profile JSON.");
        Console.WriteLine("  --force                       Allow overwriting when creating a sample profile.");
        Console.WriteLine("  --no-geo                      Disable geo enrichment.");
        Console.WriteLine("  --no-open                     Do not auto-open the generated HTML report.");
        Console.WriteLine("  --help                        Show this help.");
    }

    private static void TryOpenPath(string path, string label)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });

            Console.WriteLine($"Opened {label}: {path}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Could not auto-open {label}: {ex.Message}");
        }
    }

    private sealed record ProfileResolution(DiagnosticProfile Profile, string? ProfilePath);
}

internal sealed class CliArguments
{
    public bool IsValid { get; private init; }

    public bool ShowHelp { get; private init; }

    public string? ErrorMessage { get; private init; }

    public string? ProfilePath { get; private init; }

    public string? TargetHost { get; private init; }

    public int? PingCount { get; private init; }

    public int? MaxHops { get; private init; }

    public bool? IncludeGeoDetails { get; private init; }

    public string Format { get; private init; } = "bundle";

    public string? OutputPath { get; private init; }

    public string? ReportDirectory { get; private init; }

    public bool ConsoleOnly { get; private init; }

    public string? Language { get; private init; }

    public bool CreateSampleProfile { get; private init; }

    public string? SampleProfilePath { get; private init; }

    public bool Force { get; private init; }

    public bool SuppressAutoOpen { get; private init; }

    public static CliArguments Parse(string[] args)
    {
        if (args.Length == 0)
        {
            return new CliArguments
            {
                IsValid = true
            };
        }

        string? profilePath = null;
        string? targetHost = null;
        int? pingCount = null;
        int? maxHops = null;
        bool? includeGeoDetails = null;
        var format = "bundle";
        string? outputPath = null;
        string? reportDirectory = null;
        string? language = null;
        string? sampleProfilePath = null;
        var createSampleProfile = false;
        var consoleOnly = false;
        var force = false;
        var suppressAutoOpen = false;

        // Keep parsing explicit and predictable for future tweaks.
        for (var index = 0; index < args.Length; index++)
        {
            var token = args[index];
            switch (token)
            {
                case "--help":
                case "-h":
                    return new CliArguments
                    {
                        IsValid = false,
                        ShowHelp = true
                    };

                case "--profile-file":
                    if (!TryReadNext(args, ref index, out profilePath))
                    {
                        return Invalid("Missing value after --profile-file.");
                    }
                    break;

                case "--target":
                    if (!TryReadNext(args, ref index, out targetHost))
                    {
                        return Invalid("Missing value after --target.");
                    }
                    break;

                case "--ping-count":
                    if (!TryReadNext(args, ref index, out var pingValue) || !int.TryParse(pingValue, out var parsedPing))
                    {
                        return Invalid("--ping-count expects an integer value.");
                    }

                    pingCount = parsedPing;
                    break;

                case "--max-hops":
                    if (!TryReadNext(args, ref index, out var maxHopValue) || !int.TryParse(maxHopValue, out var parsedMaxHops))
                    {
                        return Invalid("--max-hops expects an integer value.");
                    }

                    maxHops = parsedMaxHops;
                    break;

                case "--format":
                    if (!TryReadNext(args, ref index, out var formatValue))
                    {
                        return Invalid("Missing value after --format.");
                    }

                    format = formatValue.Trim().ToLowerInvariant();
                    if (format is not ("bundle" or "text" or "json" or "csv" or "html"))
                    {
                        return Invalid("--format must be one of: bundle, text, json, csv, html.");
                    }
                    break;

                case "--output":
                    if (!TryReadNext(args, ref index, out outputPath))
                    {
                        return Invalid("Missing value after --output.");
                    }
                    break;

                case "--report-dir":
                    if (!TryReadNext(args, ref index, out reportDirectory))
                    {
                        return Invalid("Missing value after --report-dir.");
                    }
                    break;

                case "--language":
                    if (!TryReadNext(args, ref index, out var languageValue))
                    {
                        return Invalid("Missing value after --language.");
                    }

                    language = ReportLanguage.Normalize(languageValue);
                    break;

                case "--console-only":
                    consoleOnly = true;
                    break;

                case "--create-sample-profile":
                    createSampleProfile = true;
                    if (TryPeekNext(args, index, out var samplePathToken) && !IsSwitch(samplePathToken))
                    {
                        sampleProfilePath = samplePathToken;
                        index++;
                    }
                    break;

                case "--force":
                    force = true;
                    break;

                case "--no-geo":
                    includeGeoDetails = false;
                    break;

                case "--no-open":
                    suppressAutoOpen = true;
                    break;

                default:
                    if (IsSwitch(token))
                    {
                        return Invalid($"Unknown argument: {token}");
                    }

                    targetHost ??= token;
                    break;
            }
        }

        if (consoleOnly && !string.IsNullOrWhiteSpace(outputPath))
        {
            return Invalid("--console-only cannot be combined with --output.");
        }

        if (createSampleProfile && (!string.IsNullOrWhiteSpace(profilePath) || !string.IsNullOrWhiteSpace(targetHost)))
        {
            return Invalid("--create-sample-profile should be run on its own.");
        }

        return new CliArguments
        {
            IsValid = true,
            ProfilePath = profilePath,
            TargetHost = targetHost,
            PingCount = pingCount,
            MaxHops = maxHops,
            IncludeGeoDetails = includeGeoDetails,
            Format = format,
            OutputPath = outputPath,
            ReportDirectory = reportDirectory,
            ConsoleOnly = consoleOnly,
            Language = language,
            CreateSampleProfile = createSampleProfile,
            SampleProfilePath = sampleProfilePath,
            Force = force,
            SuppressAutoOpen = suppressAutoOpen
        };
    }

    private static bool TryReadNext(string[] args, ref int index, out string value)
    {
        var nextIndex = index + 1;
        if (nextIndex >= args.Length || IsSwitch(args[nextIndex]))
        {
            value = string.Empty;
            return false;
        }

        value = args[nextIndex];
        index = nextIndex;
        return true;
    }

    private static bool TryPeekNext(string[] args, int index, out string value)
    {
        var nextIndex = index + 1;
        if (nextIndex >= args.Length)
        {
            value = string.Empty;
            return false;
        }

        value = args[nextIndex];
        return true;
    }

    private static bool IsSwitch(string token)
    {
        return token.StartsWith('-');
    }

    private static CliArguments Invalid(string message)
    {
        return new CliArguments
        {
            IsValid = false,
            ShowHelp = false,
            ErrorMessage = message
        };
    }
}
