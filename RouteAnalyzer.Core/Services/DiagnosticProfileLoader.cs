using System.Text.Json;
using RouteAnalyzer.Models;
using RouteAnalyzer.Options;

namespace RouteAnalyzer.Services;

public static class DiagnosticProfileLoader
{
    public const string DefaultFileName = "routeanalyzer.profile.json";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static string? TryFindDefaultProfilePath(IEnumerable<string>? searchRoots = null)
    {
        var roots = searchRoots?.ToArray()
            ?? [Directory.GetCurrentDirectory(), AppContext.BaseDirectory];

        foreach (var root in roots.Where(static root => !string.IsNullOrWhiteSpace(root)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var candidate = Path.Combine(root, DefaultFileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    public static DiagnosticProfile Load(string path)
    {
        if (!File.Exists(path))
        {
            throw new DiagnosticProfileException($"Profile file was not found: {path}");
        }

        var json = File.ReadAllText(path);
        var profile = JsonSerializer.Deserialize<DiagnosticProfile>(json, SerializerOptions);

        if (profile is null)
        {
            throw new DiagnosticProfileException("Profile file is empty or could not be parsed.");
        }

        return Normalize(profile);
    }

    public static void WriteSampleProfile(string path, bool overwrite = false)
    {
        var fullPath = Path.GetFullPath(path);
        if (File.Exists(fullPath) && !overwrite)
        {
            throw new DiagnosticProfileException($"Profile file already exists: {fullPath}");
        }

        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(fullPath, JsonSerializer.Serialize(CreateSampleProfile(), SerializerOptions));
    }

    public static DiagnosticProfile CreateSampleProfile()
    {
        return new DiagnosticProfile
        {
            ProfileName = "Remote Support - VPN",
            CompanyName = "Contoso",
            Description = "One-click remote access checks for users who report slow VPN or remote desktop sessions.",
            PreferredLanguage = ReportLanguage.English,
            TargetHost = "vpn.example.com",
            PingCount = 4,
            MaxHops = 24,
            IncludeGeoDetails = true,
            DnsLookups =
            [
                new DnsLookupDefinition
                {
                    Name = "VPN gateway DNS",
                    Hostname = "vpn.example.com"
                },
                new DnsLookupDefinition
                {
                    Name = "Portal DNS",
                    Hostname = "portal.example.com"
                }
            ],
            TcpEndpoints =
            [
                new TcpEndpointDefinition
                {
                    Name = "VPN gateway HTTPS",
                    Host = "vpn.example.com",
                    Port = 443
                },
                new TcpEndpointDefinition
                {
                    Name = "Remote desktop gateway",
                    Host = "rdg.example.com",
                    Port = 443
                }
            ]
        };
    }

    public static DiagnosticProfile Normalize(DiagnosticProfile profile)
    {
        if (!TargetHostParser.TryNormalize(profile.TargetHost, out var normalizedTarget))
        {
            throw new DiagnosticProfileException("Profile targetHost must be a valid hostname, IP address, or URL.");
        }

        if (profile.PingCount is < RouteAnalyzerOptions.MinPingCount or > RouteAnalyzerOptions.MaxPingCount)
        {
            throw new DiagnosticProfileException($"Profile pingCount must be between {RouteAnalyzerOptions.MinPingCount} and {RouteAnalyzerOptions.MaxPingCount}.");
        }

        if (profile.MaxHops is < RouteAnalyzerOptions.MinMaxHops or > RouteAnalyzerOptions.MaxMaxHops)
        {
            throw new DiagnosticProfileException($"Profile maxHops must be between {RouteAnalyzerOptions.MinMaxHops} and {RouteAnalyzerOptions.MaxMaxHops}.");
        }

        var normalizedDnsLookups = profile.DnsLookups.Select(static lookup =>
        {
            if (!TargetHostParser.TryNormalize(lookup.Hostname, out var normalizedHostname))
            {
                throw new DiagnosticProfileException($"DNS lookup host is invalid: {lookup.Hostname}");
            }

            return new DnsLookupDefinition
            {
                Name = string.IsNullOrWhiteSpace(lookup.Name) ? "DNS lookup" : lookup.Name.Trim(),
                Hostname = normalizedHostname
            };
        }).ToArray();

        var normalizedTcpEndpoints = profile.TcpEndpoints.Select(static endpoint =>
        {
            if (!TargetHostParser.TryNormalize(endpoint.Host, out var normalizedHost))
            {
                throw new DiagnosticProfileException($"TCP endpoint host is invalid: {endpoint.Host}");
            }

            if (endpoint.Port is <= 0 or > 65535)
            {
                throw new DiagnosticProfileException($"TCP endpoint port must be between 1 and 65535: {endpoint.Port}");
            }

            return new TcpEndpointDefinition
            {
                Name = string.IsNullOrWhiteSpace(endpoint.Name) ? $"{normalizedHost}:{endpoint.Port}" : endpoint.Name.Trim(),
                Host = normalizedHost,
                Port = endpoint.Port,
                TimeoutMs = endpoint.TimeoutMs
            };
        }).ToArray();

        return new DiagnosticProfile
        {
            ProfileName = string.IsNullOrWhiteSpace(profile.ProfileName) ? "Remote Support" : profile.ProfileName.Trim(),
            CompanyName = profile.CompanyName?.Trim(),
            Description = profile.Description?.Trim(),
            PreferredLanguage = ReportLanguage.Normalize(profile.PreferredLanguage),
            TargetHost = normalizedTarget,
            PingCount = profile.PingCount,
            MaxHops = profile.MaxHops,
            IncludeGeoDetails = profile.IncludeGeoDetails,
            DnsLookups = normalizedDnsLookups,
            TcpEndpoints = normalizedTcpEndpoints
        };
    }
}

public sealed class DiagnosticProfileException(string message) : Exception(message);
