using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using KuechenRezepte.Data;
using KuechenRezepte.Models;
using KuechenRezepte.Services;

namespace KuechenRezepte.Pages.Rezepte;

public class CreateModel : PageModel
{
    private static readonly Regex JsonLdRegex = new(
        @"<script[^>]*type=[\""']application/ld\+json[\""'][^>]*>(?<json>.*?)</script>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex IngredientRegex = new(
        @"^\s*(?<qty>\d+(?:[.,]\d+)?(?:\s*[/-]\s*\d+(?:[.,]\d+)?)?)\s*(?<unit>[A-Za-z.]+)?\s+(?<name>.+)$",
        RegexOptions.Compiled);

    private static readonly HashSet<string> KnownUnits = new(StringComparer.OrdinalIgnoreCase)
    {
        "g", "kg", "mg", "ml", "cl", "l", "tl", "el", "pck", "pkt", "dose", "dosen",
        "st", "stk", "stueck", "zehe", "zehen", "bund", "prise", "tasse", "tassen", "becher",
        "scheibe", "scheiben", "blatt", "blaetter", "messerspitze", "glas"
    };

    private readonly AppDbContext _context;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly RezeptImageService _imageService;

    public CreateModel(AppDbContext context, IHttpClientFactory httpClientFactory, RezeptImageService imageService)
    {
        _context = context;
        _httpClientFactory = httpClientFactory;
        _imageService = imageService;
    }

    [BindProperty]
    public Rezept Rezept { get; set; } = new();

    [BindProperty]
    public List<ZutatInput> Zutaten { get; set; } = new() { new ZutatInput() };

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

        var imported = await ImportFromChefkochAsync(recipeUri!);
        if (imported == null || string.IsNullOrWhiteSpace(imported.Name))
        {
            ImportStatusMessage = "Import fehlgeschlagen. Die Seite konnte nicht gelesen oder geparst werden.";
            ImportSucceeded = false;
            return Page();
        }

        Rezept.Name = TrimToLength(imported.Name, 200) ?? string.Empty;
        Rezept.Beschreibung = TrimToLength(imported.Description, 2000);
        Rezept.Zubereitung = imported.Instructions;

        if (imported.Portions.HasValue && imported.Portions.Value > 0)
        {
            Rezept.Portionen = imported.Portions.Value;
        }

        Rezept.Zubereitungszeit = imported.TotalMinutes;

        if (!string.IsNullOrWhiteSpace(imported.ImageUrl))
        {
            Rezept.BildPfad = TrimToLength(imported.ImageUrl, 500);
        }

        Zutaten = imported.Ingredients.Count > 0 ? imported.Ingredients : new List<ZutatInput> { new() };

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

        Rezept.Name = (TrimToLength(Rezept.Name, 200) ?? string.Empty).Trim();
        Rezept.Beschreibung = TrimToLength(Rezept.Beschreibung, 2000);
        Rezept.Zubereitung = Rezept.Zubereitung?.Trim();
        Rezept.BildPfad = TrimToLength(Rezept.BildPfad, 500);

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
            var trimmedName = (TrimToLength(zutatInput.Name, 100) ?? string.Empty).Trim();
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
                Menge = TrimToLength(zutatInput.Menge?.Trim(), 50),
                Einheit = TrimToLength(zutatInput.Einheit?.Trim(), 50)
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

    private async Task<ImportedRecipe?> ImportFromChefkochAsync(Uri recipeUri)
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

        var html = await response.Content.ReadAsStringAsync(cts.Token);
        return ParseImportedRecipeFromHtml(html);
    }

    private static ImportedRecipe? ParseImportedRecipeFromHtml(string html)
    {
        var scriptMatches = JsonLdRegex.Matches(html);
        foreach (Match match in scriptMatches)
        {
            var rawJson = WebUtility.HtmlDecode(match.Groups["json"].Value).Trim();
            if (string.IsNullOrWhiteSpace(rawJson))
            {
                continue;
            }

            try
            {
                using var document = JsonDocument.Parse(rawJson);
                if (!TryFindRecipeElement(document.RootElement, out var recipeElement))
                {
                    continue;
                }

                var recipe = MapImportedRecipe(recipeElement);
                if (recipe != null)
                {
                    return recipe;
                }
            }
            catch (JsonException)
            {
                // Ignore invalid script blocks and try the next JSON-LD block.
            }
        }

        return null;
    }

    private static bool TryFindRecipeElement(JsonElement element, out JsonElement recipeElement)
    {
        if (IsRecipeElement(element))
        {
            recipeElement = element;
            return true;
        }

        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    if (TryFindRecipeElement(property.Value, out recipeElement))
                    {
                        return true;
                    }
                }
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    if (TryFindRecipeElement(item, out recipeElement))
                    {
                        return true;
                    }
                }
                break;
        }

        recipeElement = default;
        return false;
    }

    private static bool IsRecipeElement(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (!element.TryGetProperty("@type", out var typeValue))
        {
            return false;
        }

        return ContainsRecipeType(typeValue);
    }

    private static bool ContainsRecipeType(JsonElement typeValue)
    {
        return typeValue.ValueKind switch
        {
            JsonValueKind.String => string.Equals(typeValue.GetString(), "Recipe", StringComparison.OrdinalIgnoreCase),
            JsonValueKind.Array => typeValue.EnumerateArray().Any(v =>
                v.ValueKind == JsonValueKind.String &&
                string.Equals(v.GetString(), "Recipe", StringComparison.OrdinalIgnoreCase)),
            _ => false
        };
    }

    private static ImportedRecipe? MapImportedRecipe(JsonElement recipeElement)
    {
        var name = ReadFlexibleString(recipeElement, "name");
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var description = ReadFlexibleString(recipeElement, "alternativeHeadline")
            ?? ReadFlexibleString(recipeElement, "description");

        var imported = new ImportedRecipe
        {
            Name = name.Trim(),
            Description = description?.Trim(),
            Instructions = ParseInstructions(recipeElement),
            Portions = ParsePortions(ReadFlexibleString(recipeElement, "recipeYield")),
            TotalMinutes = ParseTotalMinutes(recipeElement),
            ImageUrl = ParseImageUrl(recipeElement),
            Ingredients = ParseIngredients(recipeElement)
        };

        return imported;
    }

    private static string? ParseImageUrl(JsonElement recipeElement)
    {
        if (!recipeElement.TryGetProperty("image", out var imageElement))
        {
            return null;
        }

        return ReadFlexibleValue(imageElement, "url", "contentUrl");
    }

    private static int? ParsePortions(string? recipeYield)
    {
        if (string.IsNullOrWhiteSpace(recipeYield))
        {
            return null;
        }

        var match = Regex.Match(recipeYield, @"\d+");
        if (!match.Success)
        {
            return null;
        }

        return int.TryParse(match.Value, out var portions) ? portions : null;
    }

    private static int? ParseTotalMinutes(JsonElement recipeElement)
    {
        var total = ParseDurationToMinutes(ReadFlexibleString(recipeElement, "totalTime"));
        if (total.HasValue)
        {
            return total;
        }

        var prep = ParseDurationToMinutes(ReadFlexibleString(recipeElement, "prepTime"));
        var cook = ParseDurationToMinutes(ReadFlexibleString(recipeElement, "cookTime"));

        if (!prep.HasValue && !cook.HasValue)
        {
            return null;
        }

        return (prep ?? 0) + (cook ?? 0);
    }

    private static int? ParseDurationToMinutes(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        try
        {
            var timeSpan = XmlConvert.ToTimeSpan(value.Trim());
            return (int)Math.Round(timeSpan.TotalMinutes);
        }
        catch
        {
            return null;
        }
    }

    private static string? ParseInstructions(JsonElement recipeElement)
    {
        if (!recipeElement.TryGetProperty("recipeInstructions", out var instructionsElement))
        {
            return null;
        }

        var lines = new List<string>();
        CollectInstructionTexts(instructionsElement, lines);

        var uniqueLines = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var raw in lines)
        {
            var cleaned = Regex.Replace(raw, @"\s+", " ").Trim();
            if (string.IsNullOrWhiteSpace(cleaned))
            {
                continue;
            }

            if (seen.Add(cleaned))
            {
                uniqueLines.Add(cleaned);
            }
        }

        return uniqueLines.Count == 0
            ? null
            : string.Join(Environment.NewLine + Environment.NewLine, uniqueLines);
    }

    private static void CollectInstructionTexts(JsonElement element, List<string> lines)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                var value = element.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    lines.Add(value);
                }
                break;

            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    CollectInstructionTexts(item, lines);
                }
                break;

            case JsonValueKind.Object:
                if (element.TryGetProperty("text", out var textElement))
                {
                    CollectInstructionTexts(textElement, lines);
                }

                if (element.TryGetProperty("itemListElement", out var listElement))
                {
                    CollectInstructionTexts(listElement, lines);
                }
                break;
        }
    }

    private static List<ZutatInput> ParseIngredients(JsonElement recipeElement)
    {
        var result = new List<ZutatInput>();

        if (!recipeElement.TryGetProperty("recipeIngredient", out var ingredientsElement) ||
            ingredientsElement.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        foreach (var ingredientElement in ingredientsElement.EnumerateArray())
        {
            var raw = ReadFlexibleValue(ingredientElement, "text", "name");
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            result.Add(ParseIngredientLine(raw));
        }

        return result;
    }

    private static ZutatInput ParseIngredientLine(string raw)
    {
        var cleaned = Regex.Replace(raw, @"\s+", " ").Trim();
        var match = IngredientRegex.Match(cleaned);

        if (!match.Success)
        {
            return new ZutatInput { Name = cleaned };
        }

        var qty = match.Groups["qty"].Value.Trim();
        var unit = match.Groups["unit"].Value.Trim();
        var name = match.Groups["name"].Value.Trim().TrimStart(',', '-', ' ');

        if (string.IsNullOrWhiteSpace(name))
        {
            return new ZutatInput { Name = cleaned };
        }

        if (!string.IsNullOrWhiteSpace(unit))
        {
            var normalizedUnit = NormalizeUnit(unit);
            if (!KnownUnits.Contains(normalizedUnit))
            {
                name = $"{unit} {name}";
                unit = string.Empty;
            }
        }

        return new ZutatInput
        {
            Name = name,
            Menge = string.IsNullOrWhiteSpace(qty) ? null : qty,
            Einheit = string.IsNullOrWhiteSpace(unit) ? null : unit
        };
    }

    private static string NormalizeUnit(string value)
    {
        return value.Trim().Trim('.').ToLowerInvariant();
    }

    private static string? ReadFlexibleString(JsonElement source, string propertyName)
    {
        if (!source.TryGetProperty(propertyName, out var propertyValue))
        {
            return null;
        }

        return ReadFlexibleValue(propertyValue, "text", "name", "url", "contentUrl");
    }

    private static string? ReadFlexibleValue(JsonElement value, params string[] nestedPropertyCandidates)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.String:
                var text = value.GetString();
                return string.IsNullOrWhiteSpace(text) ? null : text;

            case JsonValueKind.Array:
                foreach (var item in value.EnumerateArray())
                {
                    var fromArray = ReadFlexibleValue(item, nestedPropertyCandidates);
                    if (!string.IsNullOrWhiteSpace(fromArray))
                    {
                        return fromArray;
                    }
                }
                break;

            case JsonValueKind.Object:
                foreach (var candidate in nestedPropertyCandidates)
                {
                    if (value.TryGetProperty(candidate, out var nestedValue))
                    {
                        var fromCandidate = ReadFlexibleValue(nestedValue, nestedPropertyCandidates);
                        if (!string.IsNullOrWhiteSpace(fromCandidate))
                        {
                            return fromCandidate;
                        }
                    }
                }
                break;
        }

        return null;
    }

    private static string? TrimToLength(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private sealed class ImportedRecipe
    {
        public string Name { get; init; } = string.Empty;
        public string? Description { get; init; }
        public string? Instructions { get; init; }
        public int? Portions { get; init; }
        public int? TotalMinutes { get; init; }
        public string? ImageUrl { get; init; }
        public List<ZutatInput> Ingredients { get; init; } = new();
    }

    public class ZutatInput
    {
        public string? Name { get; set; }
        public string? Menge { get; set; }
        public string? Einheit { get; set; }
    }
}
