using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using KuechenRezepte.Services;

namespace KuechenRezepte.Pages;

public class EinkaufslisteModel : PageModel
{
    private readonly MealPlanService _mealPlanService;

    public EinkaufslisteModel(MealPlanService mealPlanService)
    {
        _mealPlanService = mealPlanService;
    }

    [BindProperty(SupportsGet = true)]
    public DateOnly Datum { get; set; }

    public DateOnly Montag { get; private set; }
    public int KalenderWoche { get; private set; }
    public List<ShoppingListItem> Items { get; private set; } = new();

    public async Task OnGetAsync()
    {
        var result = await _mealPlanService.GetShoppingListAsync(Datum);
        Montag = result.Montag;
        KalenderWoche = result.KalenderWoche;
        Items = result.Items;
    }
}
