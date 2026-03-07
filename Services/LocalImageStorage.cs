namespace KuechenRezepte.Services;

public class LocalImageStorage : IImageStorage
{
    private readonly string _webRootPath;

    public LocalImageStorage(IWebHostEnvironment env)
    {
        _webRootPath = env.WebRootPath;
    }

    public async Task SaveAsync(string relativePath, Stream source, CancellationToken cancellationToken = default)
    {
        var fullPath = ResolvePath(relativePath);
        var dir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        await using var stream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await source.CopyToAsync(stream, cancellationToken);
    }

    public IReadOnlyList<string> ListFiles(string relativeDirectory, string searchPattern)
    {
        var fullPath = ResolvePath(relativeDirectory);
        if (!Directory.Exists(fullPath))
        {
            return [];
        }

        return Directory.GetFiles(fullPath, searchPattern)
            .Select(path => Path.GetRelativePath(_webRootPath, path).Replace('\\', '/'))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public bool Exists(string relativePath)
    {
        return File.Exists(ResolvePath(relativePath));
    }

    public void Delete(string relativePath)
    {
        var fullPath = ResolvePath(relativePath);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }
    }

    private string ResolvePath(string relativePath)
    {
        var clean = relativePath.Replace('\\', '/').Trim().TrimStart('/');
        var combined = Path.Combine(_webRootPath, clean.Replace('/', Path.DirectorySeparatorChar));
        var fullPath = Path.GetFullPath(combined);
        var webRoot = Path.GetFullPath(_webRootPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var webRootWithSeparator = webRoot + Path.DirectorySeparatorChar;

        if (!string.Equals(fullPath, webRoot, StringComparison.OrdinalIgnoreCase) &&
            !fullPath.StartsWith(webRootWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Invalid storage path.");
        }

        return fullPath;
    }
}
