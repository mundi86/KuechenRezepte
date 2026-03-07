using Microsoft.EntityFrameworkCore;
using KuechenRezepte.Data;
using KuechenRezepte.Models;

namespace KuechenRezepte.Services;

public class RecipeQueryService
{
    private readonly AppDbContext _context;

    public RecipeQueryService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<RecipeListResult> SearchAsync(RecipeListQuery query, CancellationToken cancellationToken = default)
    {
        var recipes = _context.Rezepte
            .Include(r => r.RezeptZutaten)
            .ThenInclude(rz => rz.Zutat)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            var tokens = query.SearchTerm
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            foreach (var token in tokens)
            {
                var pattern = $"%{token}%";
                recipes = recipes.Where(r =>
                    EF.Functions.Like(r.Name, pattern) ||
                    (r.Beschreibung != null && EF.Functions.Like(r.Beschreibung, pattern)) ||
                    r.RezeptZutaten.Any(rz => rz.Zutat != null && EF.Functions.Like(rz.Zutat.Name, pattern)));
            }
        }

        if (query.Kategorie.HasValue)
        {
            recipes = recipes.Where(r => r.Kategorie == query.Kategorie.Value);
        }

        var totalCount = await recipes.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(totalCount / (double)query.PageSize);
        var pageNumber = Math.Clamp(query.PageNumber, 1, Math.Max(1, totalPages));

        var pageItems = await recipes
            .OrderByDescending(r => r.CreatedAt)
            .Skip((pageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return new RecipeListResult
        {
            Rezepte = pageItems,
            TotalCount = totalCount,
            TotalPages = totalPages,
            PageNumber = pageNumber
        };
    }

    public Task<List<string>> GetIngredientSuggestionsAsync(int take = 500, CancellationToken cancellationToken = default)
    {
        return _context.Zutaten
            .OrderBy(z => z.Name)
            .Select(z => z.Name)
            .Distinct()
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public Task<List<Zutat>> GetAllIngredientsAsync(CancellationToken cancellationToken = default)
    {
        return _context.Zutaten
            .OrderBy(z => z.Name)
            .ToListAsync(cancellationToken);
    }

    public Task<Rezept?> GetByIdWithIngredientsAsync(int id, CancellationToken cancellationToken = default)
    {
        return _context.Rezepte
            .Include(r => r.RezeptZutaten)
            .ThenInclude(rz => rz.Zutat)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<Rezept?> GetRandomAsync(CancellationToken cancellationToken = default)
    {
        var count = await _context.Rezepte.CountAsync(cancellationToken);
        if (count == 0)
        {
            return null;
        }

        var skip = Random.Shared.Next(count);
        return await _context.Rezepte
            .Include(r => r.RezeptZutaten)
            .ThenInclude(rz => rz.Zutat)
            .OrderBy(r => r.Id)
            .Skip(skip)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<List<Rezept>> GetAllOrderedByNameAsync(CancellationToken cancellationToken = default)
    {
        return _context.Rezepte
            .OrderBy(r => r.Name)
            .ToListAsync(cancellationToken);
    }
}
