using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using KuechenRezepte.Models;
using KuechenRezepte.Services;

namespace KuechenRezepte.Pages;

public class WochenplanModel : PageModel
{
    private readonly MealPlanService _mealPlanService;

    public WochenplanModel(MealPlanService mealPlanService)
    {
        _mealPlanService = mealPlanService;
    }

    [BindProperty(SupportsGet = true)]
    public DateOnly Datum { get; set; }

    public DateOnly Montag { get; private set; }

    public List<TagEintrag> Woche { get; private set; } = new();
    public List<Rezept> AlleRezepte { get; private set; } = new();

    public int KalenderWoche { get; private set; }

    public async Task OnGetAsync()
    {
        var result = await _mealPlanService.GetWeekAsync(Datum);
        Montag = result.Montag;
        KalenderWoche = result.KalenderWoche;
        AlleRezepte = result.AlleRezepte;
        Woche = Enumerable.Range(0, 7)
            .Select(i =>
            {
                var tag = Montag.AddDays(i);
                var mahlzeit = result.Mahlzeiten.FirstOrDefault(m => m.Datum == tag);
                return new TagEintrag(tag, mahlzeit);
            })
            .ToList();
    }

    public async Task<IActionResult> OnPostAssignAsync(DateOnly datum, int rezeptId)
    {
        await _mealPlanService.AssignAsync(datum, rezeptId);
        var monday = MealPlanService.MontagDerWoche(datum);
        return RedirectToPage(new { datum = monday.ToString("yyyy-MM-dd") });
    }

    public async Task<IActionResult> OnPostRemoveAsync(DateOnly datum)
    {
        await _mealPlanService.RemoveAsync(datum);
        var monday = MealPlanService.MontagDerWoche(datum);
        return RedirectToPage(new { datum = monday.ToString("yyyy-MM-dd") });
    }

    public record TagEintrag(DateOnly Datum, Mahlzeit? Mahlzeit);
}
