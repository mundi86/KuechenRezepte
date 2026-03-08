using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using KuechenRezepte.Services;

namespace KuechenRezepte.Pages.Rezepte;

public class ImportJsonModel : PageModel
{
    private readonly RecipeJsonImportService _jsonImportService;
    private readonly RecipeCommandService _recipeCommandService;

    public ImportJsonModel(
        RecipeJsonImportService jsonImportService,
        RecipeCommandService recipeCommandService)
    {
        _jsonImportService = jsonImportService;
        _recipeCommandService = recipeCommandService;
    }

    [BindProperty]
    public string? JsonInput { get; set; }

    [BindProperty]
    public IFormFile? JsonDatei { get; set; }

    [BindProperty]
    public bool NurPruefen { get; set; }

    public string? StatusMessage { get; set; }
    public bool ImportSucceeded { get; set; }
    public JsonImportRunSummary? Summary { get; set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var sourceJson = await ResolveSourceJsonAsync();
        if (string.IsNullOrWhiteSpace(sourceJson))
        {
            StatusMessage = "Bitte JSON einfügen oder eine JSON-Datei hochladen.";
            ImportSucceeded = false;
            return Page();
        }

        var parseResult = _jsonImportService.Parse(sourceJson);
        if (parseResult.GlobalErrors.Count > 0)
        {
            StatusMessage = string.Join(" ", parseResult.GlobalErrors);
            ImportSucceeded = false;
            return Page();
        }

        var summary = new JsonImportRunSummary
        {
            Total = parseResult.Rows.Count
        };

        foreach (var row in parseResult.Rows)
        {
            if (!row.IsValid || row.Recipe == null)
            {
                summary.Failed++;
                summary.Results.Add(new JsonImportRowResult
                {
                    Index = row.Index,
                    Name = row.Recipe?.Name,
                    Success = false,
                    Message = string.Join(" ", row.Errors)
                });
                continue;
            }

            if (NurPruefen)
            {
                summary.Valid++;
                summary.Results.Add(new JsonImportRowResult
                {
                    Index = row.Index,
                    Name = row.Recipe.Name,
                    Success = true,
                    Message = "Validierung OK (nicht importiert)."
                });
                continue;
            }

            var createResult = await _recipeCommandService.CreateAsync(
                row.Recipe,
                row.Ingredients,
                []);

            if (createResult.Success)
            {
                summary.Imported++;
                summary.Results.Add(new JsonImportRowResult
                {
                    Index = row.Index,
                    Name = row.Recipe.Name,
                    Success = true,
                    Message = $"Importiert (ID {createResult.RecipeId})."
                });
            }
            else
            {
                summary.Failed++;
                summary.Results.Add(new JsonImportRowResult
                {
                    Index = row.Index,
                    Name = row.Recipe.Name,
                    Success = false,
                    Message = createResult.Error ?? "Import fehlgeschlagen."
                });
            }
        }

        Summary = summary;
        ImportSucceeded = summary.Failed == 0;
        StatusMessage = NurPruefen
            ? $"Validierung beendet: {summary.Valid} gültig, {summary.Failed} fehlerhaft."
            : $"Import beendet: {summary.Imported} importiert, {summary.Failed} fehlgeschlagen.";

        return Page();
    }

    private async Task<string?> ResolveSourceJsonAsync()
    {
        if (JsonDatei is { Length: > 0 })
        {
            await using var stream = JsonDatei.OpenReadStream();
            using var reader = new StreamReader(stream);
            return await reader.ReadToEndAsync();
        }

        return JsonInput;
    }
}

public sealed class JsonImportRunSummary
{
    public int Total { get; set; }
    public int Imported { get; set; }
    public int Valid { get; set; }
    public int Failed { get; set; }
    public List<JsonImportRowResult> Results { get; } = new();
}

public sealed class JsonImportRowResult
{
    public int Index { get; set; }
    public string? Name { get; set; }
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}
