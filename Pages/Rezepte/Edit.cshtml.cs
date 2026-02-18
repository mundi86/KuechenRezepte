using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using KuechenRezepte.Data;
using KuechenRezepte.Models;
using KuechenRezepte.Services;

namespace KuechenRezepte.Pages.Rezepte;

public class EditModel : PageModel
{
    private readonly AppDbContext _context;
    private readonly RezeptImageService _imageService;

    public EditModel(AppDbContext context, RezeptImageService imageService)
    {
        _context = context;
        _imageService = imageService;
    }

    [BindProperty]
    public Rezept Rezept { get; set; } = null!;

    [BindProperty]
    public List<ZutatInput> Zutaten { get; set; } = new();

    public List<Zutat> BestehendeZutaten { get; set; } = new();

    public List<string> GalerieBilder { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var rezept = await _context.Rezepte
            .Include(r => r.RezeptZutaten)
            .ThenInclude(rz => rz.Zutat)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (rezept == null)
        {
            return NotFound();
        }

        Rezept = rezept;
        Zutaten = Rezept.RezeptZutaten.Select(rz => new ZutatInput
        {
            Id = rz.Id,
            Name = rz.Zutat?.Name ?? string.Empty,
            Menge = rz.Menge,
            Einheit = rz.Einheit
        }).ToList();

        if (Zutaten.Count == 0)
        {
            Zutaten.Add(new ZutatInput());
        }

        BestehendeZutaten = _context.Zutaten.OrderBy(z => z.Name).ToList();
        GalerieBilder = _imageService.GetImagePaths(Rezept.Id, Rezept.BildPfad);

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(List<IFormFile>? bilder, bool bilderLoeschen = false)
    {
        var existingRezept = await _context.Rezepte.FindAsync(Rezept.Id);
        if (existingRezept == null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            BestehendeZutaten = _context.Zutaten.OrderBy(z => z.Name).ToList();
            GalerieBilder = _imageService.GetImagePaths(existingRezept.Id, existingRezept.BildPfad);
            return Page();
        }

        var newImages = (bilder ?? []).Where(b => b.Length > 0).ToList();
        foreach (var image in newImages)
        {
            if (!_imageService.IsValidImage(image, out var error))
            {
                ModelState.AddModelError("bilder", error ?? "Ungueltiges Bild");
                BestehendeZutaten = _context.Zutaten.OrderBy(z => z.Name).ToList();
                GalerieBilder = _imageService.GetImagePaths(existingRezept.Id, existingRezept.BildPfad);
                return Page();
            }
        }

        if (bilderLoeschen)
        {
            _imageService.DeleteAllLocalImages(existingRezept.Id, existingRezept.BildPfad);
            existingRezept.BildPfad = null;
        }

        if (newImages.Count > 0)
        {
            var savedPaths = await _imageService.SaveImagesAsync(existingRezept.Id, newImages);
            if (savedPaths.Count > 0 &&
                (string.IsNullOrWhiteSpace(existingRezept.BildPfad) || _imageService.IsExternalPath(existingRezept.BildPfad) || bilderLoeschen))
            {
                existingRezept.BildPfad = savedPaths[0];
            }
        }

        existingRezept.Name = (Rezept.Name ?? string.Empty).Trim();
        existingRezept.Kategorie = Rezept.Kategorie;
        existingRezept.Portionen = Rezept.Portionen;
        existingRezept.Zubereitungszeit = Rezept.Zubereitungszeit;
        existingRezept.Beschreibung = Rezept.Beschreibung?.Trim();
        existingRezept.Zubereitung = Rezept.Zubereitung?.Trim();

        var existingRz = await _context.RezeptZutaten
            .Where(rz => rz.RezeptId == Rezept.Id)
            .ToListAsync();
        _context.RezeptZutaten.RemoveRange(existingRz);

        foreach (var zutatInput in Zutaten.Where(z => !string.IsNullOrWhiteSpace(z.Name)))
        {
            var trimmedName = zutatInput.Name!.Trim();
            if (trimmedName.Length > 100)
            {
                trimmedName = trimmedName[..100];
            }

            var zutat = _context.Zutaten.FirstOrDefault(z => z.Name.ToLower() == trimmedName.ToLower());
            if (zutat == null)
            {
                zutat = new Zutat { Name = trimmedName };
                _context.Zutaten.Add(zutat);
                await _context.SaveChangesAsync();
            }

            var rezeptZutat = new RezeptZutat
            {
                RezeptId = Rezept.Id,
                ZutatId = zutat.Id,
                Menge = Truncate(zutatInput.Menge, 50),
                Einheit = Truncate(zutatInput.Einheit, 50)
            };

            _context.RezeptZutaten.Add(rezeptZutat);
        }

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Rezept erfolgreich aktualisiert!";
        return RedirectToPage("/Index");
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    public class ZutatInput
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Menge { get; set; }
        public string? Einheit { get; set; }
    }
}
