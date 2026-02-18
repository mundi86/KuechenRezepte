using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using KuechenRezepte.Data;
using KuechenRezepte.Models;
using KuechenRezepte.Services;

namespace KuechenRezepte.Pages.Rezepte;

public class DetailsModel : PageModel
{
    private readonly AppDbContext _context;
    private readonly RezeptImageService _imageService;

    public DetailsModel(AppDbContext context, RezeptImageService imageService)
    {
        _context = context;
        _imageService = imageService;
    }

    public Rezept? Rezept { get; set; }
    public List<string> GalerieBilder { get; set; } = new();

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

        GalerieBilder = _imageService.GetImagePaths(Rezept.Id, Rezept.BildPfad);

        return Page();
    }
}
