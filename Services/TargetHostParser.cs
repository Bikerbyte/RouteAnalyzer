using System.Net;

namespace RouteAnalyzer.Services;

public static class TargetHostParser
{
    public static bool TryNormalize(string rawValue, out string normalizedTarget)
    {
        normalizedTarget = string.Empty;
        var candidate = rawValue.Trim();

        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        if (Uri.TryCreate(candidate, UriKind.Absolute, out var uri))
        {
            candidate = uri.Host;
        }

        candidate = candidate.Trim().TrimEnd('.');

        if (candidate.Length == 0
            || candidate.Length > 255
            || candidate.Contains(' ')
            || candidate.Contains('/')
            || candidate.Contains('\\'))
        {
            return false;
        }

        if (IPAddress.TryParse(candidate, out _))
        {
            normalizedTarget = candidate;
            return true;
        }

        var hostNameType = Uri.CheckHostName(candidate);
        if (hostNameType is UriHostNameType.Dns or UriHostNameType.IPv4 or UriHostNameType.IPv6)
        {
            normalizedTarget = candidate;
            return true;
        }

        return false;
    }
}
