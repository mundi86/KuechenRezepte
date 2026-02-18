using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using KuechenRezepte.Data;
using KuechenRezepte.Helpers;
using KuechenRezepte.Models;
using KuechenRezepte.Services;

namespace KuechenRezepte.Pages.Rezepte;

public class CreateModel : PageModel
{
    private readonly AppDbContext _context;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly RezeptImageService _imageService;
    private readonly ChefkochImporter _importer;

    public CreateModel(
        AppDbContext context,
        IHttpClientFactory httpClientFactory,
        RezeptImageService imageService,
        ChefkochImporter importer)
    {
        _context = context;
        _httpClientFactory = httpClientFactory;
        _imageService = imageService;
        _importer = importer;
    }

    [BindProperty]
    public Rezept Rezept { get; set; } = new();

    [BindProperty]
    public List<ChefkochImporter.ZutatInput> Zutaten { get; set; } = new() { new() };

    [BindProperty]
    public string? ImportUrl { get; set; }

    public List<Zutat> BestehendeZutaten { get; set; } = new();

    public string? ImportStatusMessage { get; set; }

    public bool ImportSucceeded { get; set; }

    public IActionResult OnGet()
    {
        LoadBestehendeZutaten();
        return Page();
    }

    public async Task<IActionResult> OnPostImportAsync()
    {
        LoadBestehendeZutaten();

        if (string.IsNullOrWhiteSpace(ImportUrl))
        {
            ImportStatusMessage = "Bitte gib eine Rezept-URL ein.";
            ImportSucceeded = false;
            return Page();
        }

        if (!TryValidateChefkochUrl(ImportUrl, out var recipeUri))
        {
            ImportStatusMessage = "Bitte eine gueltige Chefkoch-URL verwenden.";
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

        Zutaten = imported.Ingredients.Count > 0 ? imported.Ingredients : [new()];

        ImportUrl = recipeUri!.ToString();
        ImportSucceeded = true;
        ImportStatusMessage = $"Import erfolgreich. {Zutaten.Count} Zutaten wurden uebernommen.";

        // ModelState has precedence in Razor fields; clear it so imported values are displayed.
        ModelState.Clear();

        return Page();
    }

    public Task<IActionResult> OnPostAsync(List<IFormFile>? bilder)
    {
        return SaveRecipeAsync(bilder);
    }

    public Task<IActionResult> OnPostSaveAsync(List<IFormFile>? bilder)
    {
        return SaveRecipeAsync(bilder);
    }

    private async Task<IActionResult> SaveRecipeAsync(List<IFormFile>? bilder)
    {
        if (!ModelState.IsValid)
        {
            LoadBestehendeZutaten();
            return Page();
        }

        var validImages = (bilder ?? []).Where(b => b.Length > 0).ToList();
        foreach (var image in validImages)
        {
            if (!_imageService.IsValidImage(image, out var error))
            {
                ModelState.AddModelError("bilder", error ?? "Ungueltiges Bild");
                LoadBestehendeZutaten();
                return Page();
            }
        }

        Rezept.Name = (StringHelper.TrimToLength(Rezept.Name, 200) ?? string.Empty).Trim();
        Rezept.Beschreibung = StringHelper.TrimToLength(Rezept.Beschreibung, 2000);
        Rezept.Zubereitung = Rezept.Zubereitung?.Trim();
        Rezept.BildPfad = StringHelper.TrimToLength(Rezept.BildPfad, 500);

        _context.Rezepte.Add(Rezept);
        await _context.SaveChangesAsync();

        if (validImages.Count > 0)
        {
            var savedPaths = await _imageService.SaveImagesAsync(Rezept.Id, validImages);
            if (savedPaths.Count > 0)
            {
                Rezept.BildPfad = savedPaths[0];
            }
        }

        foreach (var zutatInput in Zutaten.Where(z => !string.IsNullOrWhiteSpace(z.Name)))
        {
            var trimmedName = (StringHelper.TrimToLength(zutatInput.Name, 100) ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(trimmedName))
            {
                continue;
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
                Menge = StringHelper.TrimToLength(zutatInput.Menge?.Trim(), 50),
                Einheit = StringHelper.TrimToLength(zutatInput.Einheit?.Trim(), 50)
            };

            _context.RezeptZutaten.Add(rezeptZutat);
        }

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Rezept erfolgreich erstellt!";
        return RedirectToPage("/Index");
    }

    private void LoadBestehendeZutaten()
    {
        BestehendeZutaten = _context.Zutaten.OrderBy(z => z.Name).ToList();
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
