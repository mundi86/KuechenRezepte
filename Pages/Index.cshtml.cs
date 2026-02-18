using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using KuechenRezepte.Data;
using KuechenRezepte.Models;

namespace KuechenRezepte.Pages;

public class IndexModel : PageModel
{
    private readonly AppDbContext _context;
    private const int PageSize = 12;

    public IndexModel(AppDbContext context)
    {
        _context = context;
    }

    public IList<Rezept> Rezepte { get; set; } = new List<Rezept>();

    [BindProperty(SupportsGet = true)]
    public string? SearchTerm { get; set; }

    [BindProperty(SupportsGet = true)]
    public Kategorie? Kategorie { get; set; }

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    public int TotalPages { get; set; }
    public int TotalCount { get; set; }

    public List<string> ZutatenVorschlaege { get; set; } = new();

    public async Task OnGetAsync()
    {
        var query = _context.Rezepte
            .Include(r => r.RezeptZutaten)
            .ThenInclude(rz => rz.Zutat)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(SearchTerm))
        {
            var tokens = SearchTerm
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            foreach (var token in tokens)
            {
                var pattern = $"%{token}%";
                query = query.Where(r =>
                    EF.Functions.Like(r.Name, pattern) ||
                    (r.Beschreibung != null && EF.Functions.Like(r.Beschreibung, pattern)) ||
                    r.RezeptZutaten.Any(rz => rz.Zutat != null && EF.Functions.Like(rz.Zutat.Name, pattern)));
            }
        }

        if (Kategorie.HasValue)
        {
            query = query.Where(r => r.Kategorie == Kategorie.Value);
        }

        TotalCount = await query.CountAsync();
        TotalPages = (int)Math.Ceiling(TotalCount / (double)PageSize);
        PageNumber = Math.Clamp(PageNumber, 1, Math.Max(1, TotalPages));

        Rezepte = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((PageNumber - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();

        ZutatenVorschlaege = await _context.Zutaten
            .OrderBy(z => z.Name)
            .Select(z => z.Name)
            .Distinct()
            .Take(500)
            .ToListAsync();
    }
}
