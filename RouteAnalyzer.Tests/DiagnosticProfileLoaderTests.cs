using System.Text.Json;
using RouteAnalyzer.Models;
using RouteAnalyzer.Services;

namespace RouteAnalyzer.Tests;

public class DiagnosticProfileLoaderTests
{
    [Fact]
    public void Normalize_NormalizesTargetAndTemplateHosts()
    {
        var normalized = DiagnosticProfileLoader.Normalize(new DiagnosticProfile
        {
            ProfileName = "VPN",
            TargetHost = "https://vpn.example.com/login",
            PingCount = 4,
            MaxHops = 24,
            DnsLookups =
            [
                new DnsLookupDefinition
                {
                    Name = "Portal",
                    Hostname = "https://portal.example.com/home"
                }
            ],
            TcpEndpoints =
            [
                new TcpEndpointDefinition
                {
                    Name = "Gateway",
                    Host = "https://rdg.example.com",
                    Port = 443
                }
            ]
        });

        Assert.Equal("vpn.example.com", normalized.TargetHost);
        Assert.Equal("portal.example.com", normalized.DnsLookups[0].Hostname);
        Assert.Equal("rdg.example.com", normalized.TcpEndpoints[0].Host);
    }

    [Fact]
    public void WriteSampleProfile_CreatesValidJsonFile()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json");

        try
        {
            DiagnosticProfileLoader.WriteSampleProfile(tempFile);
            var json = File.ReadAllText(tempFile);
            var profile = JsonSerializer.Deserialize<DiagnosticProfile>(json, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            Assert.NotNull(profile);
            Assert.False(string.IsNullOrWhiteSpace(profile.TargetHost));
            Assert.NotEmpty(profile.DnsLookups);
            Assert.NotEmpty(profile.TcpEndpoints);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }
}
