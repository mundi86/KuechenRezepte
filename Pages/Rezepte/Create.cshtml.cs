using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using KuechenRezepte.Helpers;
using KuechenRezepte.Models;
using KuechenRezepte.Services;

namespace KuechenRezepte.Pages.Rezepte;

public class CreateModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ChefkochImporter _importer;
    private readonly RecipeCommandService _recipeCommandService;
    private readonly RecipeQueryService _recipeQueryService;

    public CreateModel(
        IHttpClientFactory httpClientFactory,
        ChefkochImporter importer,
        RecipeCommandService recipeCommandService,
        RecipeQueryService recipeQueryService)
    {
        _httpClientFactory = httpClientFactory;
        _importer = importer;
        _recipeCommandService = recipeCommandService;
        _recipeQueryService = recipeQueryService;
    }

    [BindProperty]
    public Rezept Rezept { get; set; } = new();

    [BindProperty]
    public List<RecipeIngredientInput> Zutaten { get; set; } = new() { new() };

    [BindProperty]
    public string? ImportUrl { get; set; }

    public List<Zutat> BestehendeZutaten { get; set; } = new();

    public string? ImportStatusMessage { get; set; }

    public bool ImportSucceeded { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        await LoadBestehendeZutatenAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostImportAsync()
    {
        await LoadBestehendeZutatenAsync();

        if (string.IsNullOrWhiteSpace(ImportUrl))
        {
            ImportStatusMessage = "Bitte gib eine Rezept-URL ein.";
            ImportSucceeded = false;
            return Page();
        }

        if (!TryValidateChefkochUrl(ImportUrl, out var recipeUri))
        {
            ImportStatusMessage = "Bitte eine gültige Chefkoch-URL verwenden.";
            ImportSucceeded = false;
            return Page();
        }

        var html = await FetchHtmlAsync(recipeUri!);
        if (html == null)
        {
            ImportStatusMessage = "Import fehlgeschlagen. Die Seite konnte nicht geladen werden.";
            ImportSucceeded = false;
            return Page();
        }

        var imported = _importer.Parse(html);
        if (imported == null || string.IsNullOrWhiteSpace(imported.Name))
        {
            ImportStatusMessage = "Import fehlgeschlagen. Die Seite konnte nicht geparst werden.";
            ImportSucceeded = false;
            return Page();
        }

        Rezept.Name = StringHelper.TrimToLength(imported.Name, 200) ?? string.Empty;
        Rezept.Beschreibung = StringHelper.TrimToLength(imported.Description, 2000);
        Rezept.Zubereitung = imported.Instructions;

        if (imported.Portions.HasValue && imported.Portions.Value > 0)
        {
            Rezept.Portionen = imported.Portions.Value;
        }

        Rezept.Zubereitungszeit = imported.TotalMinutes;

        if (!string.IsNullOrWhiteSpace(imported.ImageUrl))
        {
            Rezept.BildPfad = StringHelper.TrimToLength(imported.ImageUrl, 500);
        }

        Zutaten = imported.Ingredients.Count > 0
            ? imported.Ingredients.Select(i => new RecipeIngredientInput
            {
                Name = i.Name,
                Menge = i.Menge,
                Einheit = i.Einheit
            }).ToList()
            : [new()];

        ImportUrl = recipeUri!.ToString();
        ImportSucceeded = true;
        ImportStatusMessage = $"Import erfolgreich. {Zutaten.Count} Zutaten wurden übernommen.";

        // ModelState has precedence in Razor fields; clear it so imported values are displayed.
        ModelState.Clear();

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(List<IFormFile>? bilder)
    {
        return await SaveRecipeAsync(bilder);
    }

    public async Task<IActionResult> OnPostSaveAsync(List<IFormFile>? bilder)
    {
        return await SaveRecipeAsync(bilder);
    }

    private async Task<IActionResult> SaveRecipeAsync(List<IFormFile>? bilder)
    {
        if (!ModelState.IsValid)
        {
            await LoadBestehendeZutatenAsync();
            return Page();
        }

        Rezept.Name = StringHelper.TrimToLength(Rezept.Name, 200) ?? string.Empty;
        Rezept.Beschreibung = StringHelper.TrimToLength(Rezept.Beschreibung, 2000);
        Rezept.BildPfad = StringHelper.TrimToLength(Rezept.BildPfad, 500);

        var result = await _recipeCommandService.CreateAsync(
            Rezept,
            Zutaten,
            bilder ?? []);

        if (!result.Success || result.RecipeId == null)
        {
            ModelState.AddModelError("bilder", result.Error ?? "Speichern fehlgeschlagen.");
            await LoadBestehendeZutatenAsync();
            return Page();
        }

        TempData["SuccessMessage"] = "Rezept erfolgreich erstellt!";
        return RedirectToPage("/Rezepte/Details", new { id = result.RecipeId.Value });
    }

    private async Task LoadBestehendeZutatenAsync()
    {
        BestehendeZutaten = await _recipeQueryService.GetAllIngredientsAsync();
    }

    private static bool TryValidateChefkochUrl(string url, out Uri? uri)
    {
        uri = null;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed))
        {
            return false;
        }

        if (!string.Equals(parsed.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(parsed.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(parsed.UserInfo))
        {
            return false;
        }

        var isChefkochHost =
            string.Equals(parsed.Host, "chefkoch.de", StringComparison.OrdinalIgnoreCase) ||
            parsed.Host.EndsWith(".chefkoch.de", StringComparison.OrdinalIgnoreCase);

        if (!isChefkochHost)
        {
            return false;
        }

        uri = parsed;
        return true;
    }

    private async Task<string?> FetchHtmlAsync(Uri recipeUri)
    {
        var client = _httpClientFactory.CreateClient();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var request = new HttpRequestMessage(HttpMethod.Get, recipeUri);
        request.Headers.UserAgent.ParseAdd("KuechenRezepteImporter/1.0");
        request.Headers.Accept.ParseAdd("text/html");

        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadAsStringAsync(cts.Token);
    }
}
