using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using KuechenRezepte.Models;
using KuechenRezepte.Services;

namespace KuechenRezepte.Pages.Rezepte;

public class DetailsModel : PageModel
{
    private readonly RecipeQueryService _recipeQueryService;
    private readonly RezeptImageService _imageService;

    public DetailsModel(RecipeQueryService recipeQueryService, RezeptImageService imageService)
    {
        _recipeQueryService = recipeQueryService;
        _imageService = imageService;
    }

    public Rezept? Rezept { get; set; }
    public List<string> GalerieBilder { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        Rezept = await _recipeQueryService.GetByIdWithIngredientsAsync(id);

        if (Rezept == null)
        {
            return NotFound();
        }

        GalerieBilder = _imageService.GetImagePaths(Rezept.Id, Rezept.BildPfad);

        return Page();
    }
}
