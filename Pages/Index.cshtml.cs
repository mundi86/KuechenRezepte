using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using KuechenRezepte.Models;
using KuechenRezepte.Services;

namespace KuechenRezepte.Pages;

public class IndexModel : PageModel
{
    private readonly RecipeQueryService _recipeQueryService;
    private const int PageSize = 12;

    public IndexModel(RecipeQueryService recipeQueryService)
    {
        _recipeQueryService = recipeQueryService;
    }

    public IList<Rezept> Rezepte { get; set; } = new List<Rezept>();

    [BindProperty(SupportsGet = true)]
    public string? SearchTerm { get; set; }

    [BindProperty(SupportsGet = true)]
    public Kategorie? Kategorie { get; set; }

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    public int TotalPages { get; set; }
    public int TotalCount { get; set; }

    public List<string> ZutatenVorschlaege { get; set; } = new();

    public async Task OnGetAsync()
    {
        var result = await _recipeQueryService.SearchAsync(new RecipeListQuery
        {
            SearchTerm = SearchTerm,
            Kategorie = Kategorie,
            PageNumber = PageNumber,
            PageSize = PageSize
        });

        Rezepte = result.Rezepte;
        TotalCount = result.TotalCount;
        TotalPages = result.TotalPages;
        PageNumber = result.PageNumber;

        ZutatenVorschlaege = await _recipeQueryService.GetIngredientSuggestionsAsync();
    }
}
