namespace RouteAnalyzer.Models;

public sealed class IpGeoDetails
{
    public string? Country { get; init; }

    public string? Region { get; init; }

    public string? City { get; init; }

    public string? Timezone { get; init; }

    public string? Asn { get; init; }

    public string? Organization { get; init; }

    public string? Isp { get; init; }

    public double? Latitude { get; init; }

    public double? Longitude { get; init; }

    public bool HasCoordinates => Latitude.HasValue && Longitude.HasValue;

    public string? Summary
    {
        get
        {
            var location = string.Join(" / ", new[] { Country, Region, City }.Where(static item => !string.IsNullOrWhiteSpace(item)));
            return string.IsNullOrWhiteSpace(location) ? null : location;
        }
    }
}
