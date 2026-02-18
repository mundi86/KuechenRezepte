using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using KuechenRezepte.Data;
using KuechenRezepte.Models;
using KuechenRezepte.Services;

namespace KuechenRezepte.Pages.Rezepte;

public class DeleteModel : PageModel
{
    private readonly AppDbContext _context;
    private readonly RezeptImageService _imageService;

    public DeleteModel(AppDbContext context, RezeptImageService imageService)
    {
        _context = context;
        _imageService = imageService;
    }

    [BindProperty]
    public Rezept? Rezept { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        Rezept = await _context.Rezepte
            .Include(r => r.RezeptZutaten)
            .ThenInclude(rz => rz.Zutat)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (Rezept == null)
        {
            return NotFound();
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        var rezept = await _context.Rezepte.FindAsync(id);
        if (rezept == null)
        {
            return NotFound();
        }

        _imageService.DeleteAllLocalImages(rezept.Id, rezept.BildPfad);

        var rezeptZutaten = await _context.RezeptZutaten
            .Where(rz => rz.RezeptId == id)
            .ToListAsync();
        _context.RezeptZutaten.RemoveRange(rezeptZutaten);

        _context.Rezepte.Remove(rezept);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Rezept erfolgreich geloescht!";
        return RedirectToPage("/Index");
    }
}
