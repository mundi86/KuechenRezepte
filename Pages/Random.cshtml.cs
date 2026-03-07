using Microsoft.AspNetCore.Mvc.RazorPages;
using KuechenRezepte.Models;
using KuechenRezepte.Services;

namespace KuechenRezepte.Pages;

public class RandomModel : PageModel
{
    private readonly RecipeQueryService _recipeQueryService;

    public RandomModel(RecipeQueryService recipeQueryService)
    {
        _recipeQueryService = recipeQueryService;
    }

    public Rezept? Rezept { get; set; }

    public async Task OnGetAsync()
    {
        Rezept = await _recipeQueryService.GetRandomAsync();
    }
}
