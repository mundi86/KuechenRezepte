using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Http;

namespace KuechenRezepte.Services;

public class AlexaRequestSignatureValidator : IAlexaRequestSignatureValidator
{
    private readonly IHttpClientFactory _httpClientFactory;

    public AlexaRequestSignatureValidator(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<bool> ValidateAsync(HttpRequest request, string rawBody, CancellationToken cancellationToken = default)
    {
        if (!request.Headers.TryGetValue("Signature", out var signatureHeader) ||
            !request.Headers.TryGetValue("SignatureCertChainUrl", out var certChainHeader))
        {
            return false;
        }

        var certUrl = certChainHeader.ToString();
        if (!IsValidCertChainUrl(certUrl))
        {
            return false;
        }

        byte[] signatureBytes;
        try
        {
            signatureBytes = Convert.FromBase64String(signatureHeader.ToString());
        }
        catch
        {
            return false;
        }

        X509Certificate2 certificate;
        try
        {
            var client = _httpClientFactory.CreateClient();
            var certBytes = await client.GetByteArrayAsync(certUrl, cancellationToken);
            certificate = X509CertificateLoader.LoadCertificate(certBytes);
        }
        catch
        {
            return false;
        }

        using (certificate)
        {
            if (!IsCertificateTimeValid(certificate) || !HasAlexaSan(certificate))
            {
                return false;
            }

            if (!BuildChain(certificate))
            {
                return false;
            }

            return VerifySignature(certificate, rawBody, signatureBytes);
        }
    }

    internal static bool IsValidCertChainUrl(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.Equals(uri.Host, "s3.amazonaws.com", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (uri.Port != 443 && uri.Port != -1)
        {
            return false;
        }

        return uri.AbsolutePath.StartsWith("/echo.api/", StringComparison.Ordinal);
    }

    private static bool IsCertificateTimeValid(X509Certificate2 certificate)
    {
        var now = DateTimeOffset.UtcNow;
        return now >= certificate.NotBefore && now <= certificate.NotAfter;
    }

    private static bool HasAlexaSan(X509Certificate2 certificate)
    {
        foreach (var extension in certificate.Extensions)
        {
            if (extension.Oid?.Value != "2.5.29.17")
            {
                continue;
            }

            var sanText = extension.Format(multiLine: false);
            if (sanText.Contains("echo-api.amazon.com", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool BuildChain(X509Certificate2 certificate)
    {
        using var chain = new X509Chain();
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;
        return chain.Build(certificate);
    }

    private static bool VerifySignature(X509Certificate2 certificate, string rawBody, byte[] signature)
    {
        using var rsa = certificate.GetRSAPublicKey();
        if (rsa == null)
        {
            return false;
        }

        var data = System.Text.Encoding.UTF8.GetBytes(rawBody);
        return rsa.VerifyData(data, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1) ||
               rsa.VerifyData(data, signature, HashAlgorithmName.SHA1, RSASignaturePadding.Pkcs1);
    }
}
