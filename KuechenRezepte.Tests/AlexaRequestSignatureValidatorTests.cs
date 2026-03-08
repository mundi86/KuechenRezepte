using KuechenRezepte.Services;

namespace KuechenRezepte.Tests;

public class AlexaRequestSignatureValidatorTests
{
    [Theory]
    [InlineData("https://s3.amazonaws.com/echo.api/echo-api-cert.pem", true)]
    [InlineData("https://s3.amazonaws.com/echo.api/abc/xyz.pem", true)]
    [InlineData("http://s3.amazonaws.com/echo.api/echo-api-cert.pem", false)]
    [InlineData("https://example.com/echo.api/echo-api-cert.pem", false)]
    [InlineData("https://s3.amazonaws.com/other/echo-api-cert.pem", false)]
    [InlineData("not-a-url", false)]
    public void IsValidCertChainUrl_ValidatesAccordingToAlexaRules(string url, bool expected)
    {
        var result = AlexaRequestSignatureValidator.IsValidCertChainUrl(url);
        Assert.Equal(expected, result);
    }
}
