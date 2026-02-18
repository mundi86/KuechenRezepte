using Microsoft.AspNetCore.Http;

namespace KuechenRezepte.Services;

public class RezeptImageService
{
    private static readonly string[] AllowedTypes = [".jpg", ".jpeg", ".png", ".webp"];
    private const long MaxImageBytes = 2 * 1024 * 1024;

    private readonly IWebHostEnvironment _env;

    public RezeptImageService(IWebHostEnvironment env)
    {
        _env = env;
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

    public async Task<List<string>> SaveImagesAsync(int rezeptId, IEnumerable<IFormFile> files)
    {
        var result = new List<string>();
        var uploadsPath = EnsureUploadsDirectory();

        foreach (var file in files.Where(f => f.Length > 0))
        {
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            var fileName = $"rezept-{rezeptId}-{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}{ext}";
            var filePath = Path.Combine(uploadsPath, fileName);

            await using var stream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(stream);

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

        var uploadsPath = EnsureUploadsDirectory();
        var pattern = $"rezept-{rezeptId}-*.*";
        var localImages = Directory.GetFiles(uploadsPath, pattern)
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => $"/uploads/{name}")
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase);

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

        var uploadsPath = EnsureUploadsDirectory();
        var pattern = $"rezept-{rezeptId}-*.*";

        foreach (var file in Directory.GetFiles(uploadsPath, pattern))
        {
            if (File.Exists(file))
            {
                File.Delete(file);
            }
        }
    }

    public void DeleteLocalImageIfPresent(string? imagePath)
    {
        var localPath = ResolveLocalUploadPath(imagePath);
        if (localPath != null && File.Exists(localPath))
        {
            File.Delete(localPath);
        }
    }

    public bool IsExternalPath(string? imagePath)
    {
        return Uri.TryCreate(imagePath, UriKind.Absolute, out _);
    }

    private string EnsureUploadsDirectory()
    {
        var uploadsPath = Path.Combine(_env.WebRootPath, "uploads");
        Directory.CreateDirectory(uploadsPath);
        return uploadsPath;
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

        try
        {
            var relativePath = normalized.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            var fullPath = Path.GetFullPath(Path.Combine(_env.WebRootPath, relativePath));
            var uploadsRoot = Path.GetFullPath(Path.Combine(_env.WebRootPath, "uploads"));

            return fullPath.StartsWith(uploadsRoot, StringComparison.OrdinalIgnoreCase) ? fullPath : null;
        }
        catch
        {
            return null;
        }
    }

    private static string NormalizePath(string path)
    {
        var normalized = path.Replace('\\', '/').Trim();
        return normalized.StartsWith('/') ? normalized : $"/{normalized}";
    }
}
