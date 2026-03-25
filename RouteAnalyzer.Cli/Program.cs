using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RouteAnalyzer.Models;
using RouteAnalyzer.Options;
using RouteAnalyzer.Services;

var exitCode = await CliApplication.RunAsync(args);
return exitCode;

internal static class CliApplication
{
    public static async Task<int> RunAsync(string[] args)
    {
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

        var options = new RouteAnalyzerOptions();
        var pingCount = parseResult.PingCount ?? options.DefaultPingCount;
        var maxHops = parseResult.MaxHops ?? options.DefaultMaxHops;
        var includeGeoDetails = parseResult.IncludeGeoDetails ?? options.DefaultIncludeGeoDetails;

        if (!TargetHostParser.TryNormalize(parseResult.TargetHost!, out var normalizedTarget))
        {
            Console.Error.WriteLine("Target must be a valid hostname, IP address, or URL.");
            return 1;
        }

        if (pingCount is < RouteAnalyzerOptions.MinPingCount or > RouteAnalyzerOptions.MaxPingCount)
        {
            Console.Error.WriteLine($"Ping count must be between {RouteAnalyzerOptions.MinPingCount} and {RouteAnalyzerOptions.MaxPingCount}.");
            return 1;
        }

        if (maxHops is < RouteAnalyzerOptions.MinMaxHops or > RouteAnalyzerOptions.MaxMaxHops)
        {
            Console.Error.WriteLine($"Max hops must be between {RouteAnalyzerOptions.MinMaxHops} and {RouteAnalyzerOptions.MaxMaxHops}.");
            return 1;
        }

        var analysisOptions = new RouteAnalyzerOptions
        {
            DefaultTarget = normalizedTarget,
            DefaultPingCount = pingCount,
            DefaultMaxHops = maxHops,
            DefaultIncludeGeoDetails = includeGeoDetails,
            PingTimeoutMs = options.PingTimeoutMs,
            TracerouteProbeTimeoutMs = options.TracerouteProbeTimeoutMs,
            TracerouteProcessTimeoutSeconds = options.TracerouteProcessTimeoutSeconds
        };

        try
        {
            using var httpClient = new HttpClient
            {
                BaseAddress = new Uri("https://ipwho.is/"),
                Timeout = TimeSpan.FromSeconds(2.5)
            };

            var geoService = new IpGeoLookupService(httpClient, NullLogger<IpGeoLookupService>.Instance);
            var diagnosticService = new NetworkRouteDiagnosticService(
                geoService,
                NullLogger<NetworkRouteDiagnosticService>.Instance,
                Options.Create(analysisOptions));

            var report = await diagnosticService.AnalyzeAsync(new RouteAnalysisRequest
            {
                TargetHost = normalizedTarget,
                PingCount = pingCount,
                MaxHops = maxHops,
                IncludeGeoDetails = includeGeoDetails
            }, CancellationToken.None);

            var output = BuildOutput(report, parseResult.Format);
            if (!string.IsNullOrWhiteSpace(parseResult.OutputPath))
            {
                var path = Path.GetFullPath(parseResult.OutputPath);
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                await File.WriteAllTextAsync(path, output, Encoding.UTF8);
                Console.WriteLine($"Report saved to: {path}");
            }
            else
            {
                Console.WriteLine(output);
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Route analysis failed: {ex.Message}");
            return 1;
        }
    }

    private static string BuildOutput(RouteDiagnosticReport report, string format)
    {
        return format.ToLowerInvariant() switch
        {
            "json" => RouteDiagnosticExportFormatter.ToJson(report),
            "csv" => RouteDiagnosticExportFormatter.ToCsv(report),
            _ => RouteDiagnosticExportFormatter.ToText(report)
        };
    }

    private static void PrintHelp()
    {
        Console.WriteLine("RouteAnalyzer CLI");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  RouteAnalyzer.Cli --target <host> [options]");
        Console.WriteLine("  RouteAnalyzer.Cli <host> [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --target <value>        Hostname, IP, or URL to analyze.");
        Console.WriteLine("  --ping-count <value>    Ping probes (3-10).");
        Console.WriteLine("  --max-hops <value>      Traceroute max hops (4-64).");
        Console.WriteLine("  --format <value>        text | json | csv. Default: text.");
        Console.WriteLine("  --output <path>         Write report to a file instead of stdout.");
        Console.WriteLine("  --no-geo                Disable geo enrichment.");
        Console.WriteLine("  --help                  Show this help.");
    }
}

internal sealed class CliArguments
{
    public bool IsValid { get; private init; }

    public bool ShowHelp { get; private init; }

    public string? ErrorMessage { get; private init; }

    public string? TargetHost { get; private init; }

    public int? PingCount { get; private init; }

    public int? MaxHops { get; private init; }

    public bool? IncludeGeoDetails { get; private init; }

    public string Format { get; private init; } = "text";

    public string? OutputPath { get; private init; }

    public static CliArguments Parse(string[] args)
    {
        if (args.Length == 0)
        {
            return new CliArguments
            {
                IsValid = false,
                ShowHelp = true
            };
        }

        string? targetHost = null;
        int? pingCount = null;
        int? maxHops = null;
        bool? includeGeoDetails = null;
        var format = "text";
        string? outputPath = null;

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
                    if (format is not ("text" or "json" or "csv"))
                    {
                        return Invalid("--format must be one of: text, json, csv.");
                    }
                    break;

                case "--output":
                    if (!TryReadNext(args, ref index, out outputPath))
                    {
                        return Invalid("Missing value after --output.");
                    }
                    break;

                case "--no-geo":
                    includeGeoDetails = false;
                    break;

                default:
                    if (token.StartsWith('-'))
                    {
                        return Invalid($"Unknown argument: {token}");
                    }

                    targetHost ??= token;
                    break;
            }
        }

        if (string.IsNullOrWhiteSpace(targetHost))
        {
            return Invalid("Target is required. Use --target <value> or pass it as the first positional argument.");
        }

        return new CliArguments
        {
            IsValid = true,
            TargetHost = targetHost,
            PingCount = pingCount,
            MaxHops = maxHops,
            IncludeGeoDetails = includeGeoDetails,
            Format = format,
            OutputPath = outputPath
        };
    }

    private static bool TryReadNext(string[] args, ref int index, out string value)
    {
        var nextIndex = index + 1;
        if (nextIndex >= args.Length)
        {
            value = string.Empty;
            return false;
        }

        value = args[nextIndex];
        index = nextIndex;
        return true;
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

