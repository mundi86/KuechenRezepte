using Microsoft.AspNetCore.Http;
using KuechenRezepte.Services;

namespace KuechenRezepte.Tests;

public class ApiSecurityHelperTests
{
    [Fact]
    public void IsApiKeyAuthorized_NoExpectedKey_ReturnsTrue()
    {
        var headers = new HeaderDictionary();
        Assert.True(ApiSecurityHelper.IsApiKeyAuthorized(headers, null));
    }

    [Fact]
    public void IsApiKeyAuthorized_ExpectedKeyMissing_ReturnsFalse()
    {
        var headers = new HeaderDictionary();
        Assert.False(ApiSecurityHelper.IsApiKeyAuthorized(headers, "secret"));
    }

    [Fact]
    public void IsApiKeyAuthorized_ExpectedKeyMatches_ReturnsTrue()
    {
        var headers = new HeaderDictionary
        {
            ["X-API-Key"] = "secret"
        };

        Assert.True(ApiSecurityHelper.IsApiKeyAuthorized(headers, "secret"));
    }

    [Fact]
    public void ResolveClientIp_UsesForwardedForFirstEntry()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("10.0.0.10");
        context.Request.Headers["X-Forwarded-For"] = "203.0.113.7, 10.0.0.10";

        var ip = ApiSecurityHelper.ResolveClientIp(context.Request);
        Assert.Equal("203.0.113.7", ip);
    }

    [Fact]
    public void IsClientIpAllowed_EmptyAllowList_ReturnsTrue()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("203.0.113.7");
        Assert.True(ApiSecurityHelper.IsClientIpAllowed(context.Request, []));
    }

    [Fact]
    public void IsClientIpAllowed_NotInAllowList_ReturnsFalse()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("203.0.113.7");
        Assert.False(ApiSecurityHelper.IsClientIpAllowed(context.Request, ["203.0.113.8"]));
    }
}
