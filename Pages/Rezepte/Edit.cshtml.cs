using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using KuechenRezepte.Models;
using KuechenRezepte.Services;

namespace KuechenRezepte.Pages.Rezepte;

public class EditModel : PageModel
{
    private readonly RecipeQueryService _recipeQueryService;
    private readonly RecipeCommandService _recipeCommandService;
    private readonly RezeptImageService _imageService;

    public EditModel(
        RecipeQueryService recipeQueryService,
        RecipeCommandService recipeCommandService,
        RezeptImageService imageService)
    {
        _recipeQueryService = recipeQueryService;
        _recipeCommandService = recipeCommandService;
        _imageService = imageService;
    }

    [BindProperty]
    public Rezept Rezept { get; set; } = null!;

    [BindProperty]
    public List<RecipeIngredientInput> Zutaten { get; set; } = new();

    public List<Zutat> BestehendeZutaten { get; set; } = new();

    public List<string> GalerieBilder { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var rezept = await _recipeQueryService.GetByIdWithIngredientsAsync(id);
        if (rezept == null)
        {
            return NotFound();
        }

        Rezept = rezept;
        Zutaten = Rezept.RezeptZutaten.Select(rz => new RecipeIngredientInput
        {
            Name = rz.Zutat?.Name ?? string.Empty,
            Menge = rz.Menge,
            Einheit = rz.Einheit
        }).ToList();

        if (Zutaten.Count == 0)
        {
            Zutaten.Add(new RecipeIngredientInput());
        }

        BestehendeZutaten = await _recipeQueryService.GetAllIngredientsAsync();
        GalerieBilder = _imageService.GetImagePaths(Rezept.Id, Rezept.BildPfad);

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(List<IFormFile>? bilder, bool bilderLöschen = false)
    {
        if (!ModelState.IsValid)
        {
            BestehendeZutaten = await _recipeQueryService.GetAllIngredientsAsync();
            GalerieBilder = _imageService.GetImagePaths(Rezept.Id, Rezept.BildPfad);
            return Page();
        }

        var result = await _recipeCommandService.UpdateAsync(
            Rezept,
            Zutaten,
            bilder ?? [],
            bilderLöschen);

        if (!result.Success)
        {
            ModelState.AddModelError("bilder", result.Error ?? "Speichern fehlgeschlagen.");
            BestehendeZutaten = await _recipeQueryService.GetAllIngredientsAsync();
            GalerieBilder = _imageService.GetImagePaths(Rezept.Id, Rezept.BildPfad);
            return Page();
        }

        TempData["SuccessMessage"] = "Rezept erfolgreich aktualisiert!";
        return RedirectToPage("/Rezepte/Details", new { id = Rezept.Id });
    }
}
