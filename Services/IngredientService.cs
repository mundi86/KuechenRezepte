using Microsoft.EntityFrameworkCore;
using KuechenRezepte.Data;
using KuechenRezepte.Helpers;
using KuechenRezepte.Models;

namespace KuechenRezepte.Services;

public class IngredientService
{
    private readonly AppDbContext _context;

    public IngredientService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Zutat?> GetOrCreateByNameAsync(string? rawName, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeName(rawName);
        if (normalized == null)
        {
            return null;
        }

        var existing = await _context.Zutaten
            .FirstOrDefaultAsync(
                z => EF.Functions.Collate(z.Name, "NOCASE") == normalized,
                cancellationToken);

        if (existing != null)
        {
            return existing;
        }

        var created = new Zutat { Name = normalized };
        _context.Zutaten.Add(created);
        return created;
    }

    public static string? NormalizeName(string? rawName)
    {
        return StringHelper.TrimToLength(rawName, 100);
    }
}
