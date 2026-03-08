using Microsoft.AspNetCore.Http;

namespace KuechenRezepte.Services;

public interface IAlexaRequestSignatureValidator
{
    Task<bool> ValidateAsync(HttpRequest request, string rawBody, CancellationToken cancellationToken = default);
}
