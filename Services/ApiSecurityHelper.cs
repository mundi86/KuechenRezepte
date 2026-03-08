using System.Net;
using Microsoft.AspNetCore.Http;

namespace KuechenRezepte.Services;

public static class ApiSecurityHelper
{
    public static bool IsApiKeyAuthorized(IHeaderDictionary headers, string? expectedApiKey)
    {
        if (string.IsNullOrWhiteSpace(expectedApiKey))
        {
            return true;
        }

        if (!headers.TryGetValue("X-API-Key", out var provided))
        {
            return false;
        }

        return string.Equals(provided.ToString(), expectedApiKey, StringComparison.Ordinal);
    }

    public static string? ResolveClientIp(HttpRequest request)
    {
        if (request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor))
        {
            var first = forwardedFor.ToString()
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(first))
            {
                return first;
            }
        }

        return request.HttpContext.Connection.RemoteIpAddress?.ToString();
    }

    public static bool IsClientIpAllowed(HttpRequest request, IReadOnlyCollection<string>? allowedIps)
    {
        if (allowedIps == null || allowedIps.Count == 0)
        {
            return true;
        }

        var clientIp = ResolveClientIp(request);
        if (string.IsNullOrWhiteSpace(clientIp))
        {
            return false;
        }

        // Normalize IPv4-mapped IPv6 to plain IPv4 if possible.
        if (IPAddress.TryParse(clientIp, out var parsed) && parsed.IsIPv4MappedToIPv6)
        {
            clientIp = parsed.MapToIPv4().ToString();
        }

        return allowedIps.Any(ip => string.Equals(ip?.Trim(), clientIp, StringComparison.OrdinalIgnoreCase));
    }
}
