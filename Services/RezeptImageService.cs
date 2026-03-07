using Microsoft.AspNetCore.Http;
using System.Net.Http.Headers;

namespace KuechenRezepte.Services;

public class RezeptImageService
{
    private static readonly string[] AllowedTypes = [".jpg", ".jpeg", ".png", ".webp"];
    private const long MaxImageBytes = 2 * 1024 * 1024;

    private readonly IImageStorage _storage;
    private readonly IHttpClientFactory _httpClientFactory;

    public RezeptImageService(IImageStorage storage, IHttpClientFactory httpClientFactory)
    {
        _storage = storage;
        _httpClientFactory = httpClientFactory;
    }

    public bool IsValidImage(IFormFile file, out string? error)
    {
        error = null;

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedTypes.Contains(ext))
        {
            error = "Nur jpg, png oder webp erlaubt";
            return false;
        }

        if (file.Length > MaxImageBytes)
        {
            error = "Max. 2MB pro Bild erlaubt";
            return false;
        }

        return true;
    }

    public async Task<List<string>> SaveImagesAsync(
        int rezeptId,
        IEnumerable<IFormFile> files,
        CancellationToken cancellationToken = default)
    {
        var result = new List<string>();

        foreach (var file in files.Where(f => f.Length > 0))
        {
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            var fileName = $"rezept-{rezeptId}-{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}{ext}";
            var relativePath = $"uploads/{fileName}";

            await using var stream = file.OpenReadStream();
            await _storage.SaveAsync(relativePath, stream, cancellationToken);

            result.Add($"/uploads/{fileName}");
        }

        return result;
    }

    public async Task<string?> DownloadAndSaveExternalImageAsync(
        int rezeptId,
        string imageUrl,
        CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri))
        {
            return null;
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var client = _httpClientFactory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.UserAgent.ParseAdd("KuechenRezepteImageImporter/1.0");
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (!response.IsSuccessStatusCode || response.Content == null)
        {
            return null;
        }

        if (response.Content.Headers.ContentLength.HasValue &&
            response.Content.Headers.ContentLength.Value > MaxImageBytes)
        {
            return null;
        }

        var extension = ResolveExtension(uri, response.Content.Headers.ContentType);
        if (extension == null)
        {
            return null;
        }

        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var limited = new MemoryStream();
        var buffer = new byte[81920];
        long totalBytes = 0;

        while (true)
        {
            var read = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
            if (read == 0)
            {
                break;
            }

            totalBytes += read;
            if (totalBytes > MaxImageBytes)
            {
                return null;
            }

            await limited.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        if (totalBytes == 0)
        {
            return null;
        }

        limited.Position = 0;
        var fileName = $"rezept-{rezeptId}-{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}{extension}";
        var relativePath = $"uploads/{fileName}";
        await _storage.SaveAsync(relativePath, limited, cancellationToken);

        return $"/uploads/{fileName}";
    }

    public List<string> GetImagePaths(int rezeptId, string? primaryPath)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(primaryPath))
        {
            var normalizedPrimary = IsExternalPath(primaryPath) ? primaryPath : NormalizePath(primaryPath);
            if (seen.Add(normalizedPrimary))
            {
                result.Add(normalizedPrimary);
            }
        }

        var pattern = $"rezept-{rezeptId}-*.*";
        var localImages = _storage.ListFiles("uploads", pattern)
            .Select(path => NormalizePath($"/{path}"));

        foreach (var path in localImages)
        {
            var normalized = NormalizePath(path);
            if (seen.Add(normalized))
            {
                result.Add(normalized);
            }
        }

        return result;
    }

    public void DeleteAllLocalImages(int rezeptId, string? primaryPath)
    {
        DeleteLocalImageIfPresent(primaryPath);

        var pattern = $"rezept-{rezeptId}-*.*";
        var files = _storage.ListFiles("uploads", pattern);
        foreach (var file in files)
        {
            _storage.Delete(file);
        }
    }

    public void DeleteLocalImageIfPresent(string? imagePath)
    {
        var localPath = ResolveLocalUploadPath(imagePath);
        if (localPath != null && _storage.Exists(localPath))
        {
            _storage.Delete(localPath);
        }
    }

    public void DeleteLocalImages(IEnumerable<string?> imagePaths)
    {
        foreach (var imagePath in imagePaths)
        {
            DeleteLocalImageIfPresent(imagePath);
        }
    }

    public bool IsExternalPath(string? imagePath)
    {
        return Uri.TryCreate(imagePath, UriKind.Absolute, out _);
    }

    private string? ResolveLocalUploadPath(string? imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
        {
            return null;
        }

        if (IsExternalPath(imagePath))
        {
            return null;
        }

        var normalized = NormalizePath(imagePath);
        if (!normalized.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return normalized.TrimStart('/');
    }

    private static string NormalizePath(string path)
    {
        var normalized = path.Replace('\\', '/').Trim();
        return normalized.StartsWith('/') ? normalized : $"/{normalized}";
    }

    private static string? ResolveExtension(Uri uri, MediaTypeHeaderValue? contentType)
    {
        var contentTypeMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["image/jpeg"] = ".jpg",
            ["image/jpg"] = ".jpg",
            ["image/png"] = ".png",
            ["image/webp"] = ".webp"
        };

        if (contentType?.MediaType != null &&
            contentTypeMap.TryGetValue(contentType.MediaType, out var byContentType))
        {
            return byContentType;
        }

        var ext = Path.GetExtension(uri.AbsolutePath).ToLowerInvariant();
        return AllowedTypes.Contains(ext) ? ext : null;
    }
}
