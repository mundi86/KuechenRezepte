using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using KuechenRezepte.Data;
using KuechenRezepte.Models;

namespace KuechenRezepte.Pages;

public class RandomModel : PageModel
{
    private readonly AppDbContext _context;

    public RandomModel(AppDbContext context)
    {
        _context = context;
    }

    public Rezept? Rezept { get; set; }

    public async Task OnGetAsync()
    {
        var count = await _context.Rezepte.CountAsync();
        
        if (count == 0)
        {
            Rezept = null;
            return;
        }

        var random = new Random();
        var skip = random.Next(count);
        
        Rezept = await _context.Rezepte
            .Include(r => r.RezeptZutaten)
            .ThenInclude(rz => rz.Zutat)
            .Skip(skip)
            .FirstOrDefaultAsync();
    }
}
