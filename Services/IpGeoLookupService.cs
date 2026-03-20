using System.Net.Http.Json;
using System.Text.Json.Serialization;
using RouteAnalyzer.Models;

namespace RouteAnalyzer.Services;

public sealed class IpGeoLookupService(HttpClient httpClient)
{
    public async Task<IpGeoDetails?> LookupAsync(string ipAddress, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await httpClient.GetAsync($"{ipAddress}?fields=country,region,city,timezone,connection", cancellationToken);
            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadFromJsonAsync<IpWhoPayload>(cancellationToken: cancellationToken);
            if (payload?.Success != true)
            {
                return null;
            }

            return new IpGeoDetails
            {
                Country = payload.Country,
                Region = payload.Region,
                City = payload.City,
                Timezone = payload.Timezone?.Id,
                Asn = payload.Connection?.Asn?.ToString(),
                Organization = payload.Connection?.Org,
                Isp = payload.Connection?.Isp
            };
        }
        catch
        {
            return null;
        }
    }

    private sealed class IpWhoPayload
    {
        [JsonPropertyName("success")]
        public bool Success { get; init; }

        [JsonPropertyName("country")]
        public string? Country { get; init; }

        [JsonPropertyName("region")]
        public string? Region { get; init; }

        [JsonPropertyName("city")]
        public string? City { get; init; }

        [JsonPropertyName("timezone")]
        public TimezonePayload? Timezone { get; init; }

        [JsonPropertyName("connection")]
        public ConnectionPayload? Connection { get; init; }
    }

    private sealed class TimezonePayload
    {
        [JsonPropertyName("id")]
        public string? Id { get; init; }
    }

    private sealed class ConnectionPayload
    {
        [JsonPropertyName("asn")]
        public int? Asn { get; init; }

        [JsonPropertyName("org")]
        public string? Org { get; init; }

        [JsonPropertyName("isp")]
        public string? Isp { get; init; }
    }
}
