namespace KuechenRezepte.Services;

public interface IImageStorage
{
    Task SaveAsync(string relativePath, Stream source, CancellationToken cancellationToken = default);
    IReadOnlyList<string> ListFiles(string relativeDirectory, string searchPattern);
    bool Exists(string relativePath);
    void Delete(string relativePath);
}
