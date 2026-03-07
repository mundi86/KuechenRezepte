using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using KuechenRezepte.Models;
using KuechenRezepte.Services;

namespace KuechenRezepte.Pages.Rezepte;

public class DeleteModel : PageModel
{
    private readonly RecipeQueryService _recipeQueryService;
    private readonly RecipeCommandService _recipeCommandService;

    public DeleteModel(RecipeQueryService recipeQueryService, RecipeCommandService recipeCommandService)
    {
        _recipeQueryService = recipeQueryService;
        _recipeCommandService = recipeCommandService;
    }

    [BindProperty]
    public Rezept? Rezept { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        Rezept = await _recipeQueryService.GetByIdWithIngredientsAsync(id);
        if (Rezept == null)
        {
            return NotFound();
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        var deleted = await _recipeCommandService.DeleteAsync(id);
        if (!deleted)
        {
            return NotFound();
        }

        TempData["SuccessMessage"] = "Rezept erfolgreich Gelöscht!";
        return RedirectToPage("/Index");
    }
}
