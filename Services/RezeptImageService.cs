using Microsoft.AspNetCore.Http;

namespace KuechenRezepte.Services;

public class RezeptImageService
{
    private static readonly string[] AllowedTypes = [".jpg", ".jpeg", ".png", ".webp"];
    private const long MaxImageBytes = 2 * 1024 * 1024;

    private readonly IImageStorage _storage;

    public RezeptImageService(IImageStorage storage)
    {
        _storage = storage;
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
}
