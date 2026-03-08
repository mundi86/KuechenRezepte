using System.Globalization;
using System.Text.Json;
using KuechenRezepte.Models;

namespace KuechenRezepte.Services;

public class RecipeJsonImportService
{
    public RecipeJsonParseResult Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return RecipeJsonParseResult.WithGlobalError("JSON ist leer.");
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            return RecipeJsonParseResult.WithGlobalError($"Ungültiges JSON: {ex.Message}");
        }

        using (document)
        {
            if (!TryGetRecipeArray(document.RootElement, out var recipeArray, out var rootError))
            {
                return RecipeJsonParseResult.WithGlobalError(rootError ?? "JSON-Struktur nicht unterstützt.");
            }

            var rows = new List<RecipeJsonParseRow>();
            var index = 0;
            foreach (var element in recipeArray.EnumerateArray())
            {
                index++;
                rows.Add(ParseRow(index, element));
            }

            if (rows.Count == 0)
            {
                return RecipeJsonParseResult.WithGlobalError("Keine Rezepte im JSON gefunden.");
            }

            return new RecipeJsonParseResult(rows, []);
        }
    }

    private static RecipeJsonParseRow ParseRow(int index, JsonElement element)
    {
        var errors = new List<string>();
        if (element.ValueKind != JsonValueKind.Object)
        {
            errors.Add("Eintrag ist kein JSON-Objekt.");
            return RecipeJsonParseRow.WithErrors(index, errors);
        }

        var name = ReadString(element, "name", "titel", "title");
        if (string.IsNullOrWhiteSpace(name))
        {
            errors.Add("`name` fehlt.");
        }

        var portions = ReadInt(element, "portionen", "servings");
        if (portions.HasValue && portions.Value <= 0)
        {
            errors.Add("`portionen` muss größer als 0 sein.");
        }

        var prepMinutes = ReadInt(element, "zubereitungszeit", "zubereitungszeitMinuten", "prepMinutes", "durationMinutes");
        if (prepMinutes.HasValue && prepMinutes.Value < 0)
        {
            errors.Add("`zubereitungszeit` darf nicht negativ sein.");
        }

        var categoryRaw = ReadString(element, "kategorie", "category");
        var category = ParseKategorie(categoryRaw, out var categoryError);
        if (categoryError != null)
        {
            errors.Add(categoryError);
        }

        var ingredients = ParseIngredients(element, errors);
        if (!ingredients.Any())
        {
            errors.Add("Mindestens eine Zutat ist erforderlich.");
        }

        if (errors.Count > 0)
        {
            return RecipeJsonParseRow.WithErrors(index, errors);
        }

        var rezept = new Rezept
        {
            Name = name!.Trim(),
            Beschreibung = ReadString(element, "beschreibung", "description", "kurzbeschreibung"),
            Zubereitung = ReadInstructions(element),
            Portionen = portions ?? 2,
            Zubereitungszeit = prepMinutes,
            Kategorie = category ?? Kategorie.Mittagessen,
            BildPfad = ReadString(element, "bildPfad", "bild", "image", "imageUrl")
        };

        return new RecipeJsonParseRow(index, rezept, ingredients, []);
    }

    private static bool TryGetRecipeArray(JsonElement root, out JsonElement array, out string? error)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            array = root;
            error = null;
            return true;
        }

        if (root.ValueKind == JsonValueKind.Object)
        {
            if (TryGetArrayProperty(root, out array, "rezepte", "recipes", "items", "data"))
            {
                error = null;
                return true;
            }
        }

        array = default;
        error = "Erwartet wird ein JSON-Array oder ein Objekt mit `rezepte`/`recipes`.";
        return false;
    }

    private static bool TryGetArrayProperty(JsonElement obj, out JsonElement array, params string[] names)
    {
        foreach (var property in obj.EnumerateObject())
        {
            if (!names.Any(n => string.Equals(property.Name, n, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (property.Value.ValueKind == JsonValueKind.Array)
            {
                array = property.Value;
                return true;
            }
        }

        array = default;
        return false;
    }

    private static List<RecipeIngredientInput> ParseIngredients(JsonElement recipeElement, List<string> errors)
    {
        if (!TryGetProperty(recipeElement, out var ingredientsElement, "zutaten", "ingredients", "recipeIngredient"))
        {
            return [];
        }

        if (ingredientsElement.ValueKind != JsonValueKind.Array)
        {
            errors.Add("`zutaten` muss ein Array sein.");
            return [];
        }

        var result = new List<RecipeIngredientInput>();
        foreach (var item in ingredientsElement.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                var parsed = ChefkochImporter.ParseIngredientLine(item.GetString() ?? string.Empty);
                if (!string.IsNullOrWhiteSpace(parsed.Name))
                {
                    result.Add(new RecipeIngredientInput
                    {
                        Name = parsed.Name,
                        Menge = parsed.Menge,
                        Einheit = parsed.Einheit
                    });
                }

                continue;
            }

            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var ingredientName = ReadString(item, "name", "zutat", "ingredient");
            if (string.IsNullOrWhiteSpace(ingredientName))
            {
                continue;
            }

            var amount = ReadString(item, "menge", "amount", "quantity", "qty");
            var unit = ReadString(item, "einheit", "unit");

            result.Add(new RecipeIngredientInput
            {
                Name = ingredientName.Trim(),
                Menge = string.IsNullOrWhiteSpace(amount) ? null : amount.Trim(),
                Einheit = string.IsNullOrWhiteSpace(unit) ? null : unit.Trim()
            });
        }

        return result;
    }

    private static string? ReadInstructions(JsonElement recipeElement)
    {
        if (!TryGetProperty(recipeElement, out var instructionElement, "zubereitung", "instructions", "steps"))
        {
            return null;
        }

        if (instructionElement.ValueKind == JsonValueKind.String)
        {
            return instructionElement.GetString();
        }

        if (instructionElement.ValueKind == JsonValueKind.Array)
        {
            var lines = instructionElement.EnumerateArray()
                .Where(v => v.ValueKind == JsonValueKind.String)
                .Select(v => v.GetString())
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Select(v => v!.Trim())
                .ToList();

            return lines.Count == 0 ? null : string.Join(Environment.NewLine + Environment.NewLine, lines);
        }

        return null;
    }

    private static Kategorie? ParseKategorie(string? value, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim().ToLowerInvariant();
        normalized = normalized
            .Replace("ä", "ae", StringComparison.Ordinal)
            .Replace("ö", "oe", StringComparison.Ordinal)
            .Replace("ü", "ue", StringComparison.Ordinal)
            .Replace("ß", "ss", StringComparison.Ordinal);

        var map = new Dictionary<string, Kategorie>(StringComparer.OrdinalIgnoreCase)
        {
            ["fruehstueck"] = Kategorie.Fruehstueck,
            ["fruhstuck"] = Kategorie.Fruehstueck,
            ["breakfast"] = Kategorie.Fruehstueck,
            ["morgenessen"] = Kategorie.Fruehstueck,
            ["mittagessen"] = Kategorie.Mittagessen,
            ["lunch"] = Kategorie.Mittagessen,
            ["abendessen"] = Kategorie.Abendessen,
            ["dinner"] = Kategorie.Abendessen,
            ["dessert"] = Kategorie.Dessert,
            ["nachspeise"] = Kategorie.Dessert,
            ["snack"] = Kategorie.Snack,
            ["getraenk"] = Kategorie.Getraenk,
            ["getraenke"] = Kategorie.Getraenk,
            ["drink"] = Kategorie.Getraenk,
            ["drinks"] = Kategorie.Getraenk
        };

        if (map.TryGetValue(normalized, out var mapped))
        {
            return mapped;
        }

        if (Enum.TryParse<Kategorie>(value, ignoreCase: true, out var enumParsed))
        {
            return enumParsed;
        }

        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numeric) &&
            Enum.IsDefined(typeof(Kategorie), numeric))
        {
            return (Kategorie)numeric;
        }

        error = $"Unbekannte Kategorie: `{value}`.";
        return null;
    }

    private static string? ReadString(JsonElement obj, params string[] names)
    {
        if (!TryGetProperty(obj, out var value, names))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.String)
        {
            return value.GetString();
        }

        if (value.ValueKind == JsonValueKind.Number)
        {
            return value.GetRawText();
        }

        return null;
    }

    private static int? ReadInt(JsonElement obj, params string[] names)
    {
        if (!TryGetProperty(obj, out var value, names))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
        {
            return number;
        }

        if (value.ValueKind == JsonValueKind.String &&
            int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static bool TryGetProperty(JsonElement obj, out JsonElement value, params string[] names)
    {
        foreach (var property in obj.EnumerateObject())
        {
            if (!names.Any(n => string.Equals(property.Name, n, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            value = property.Value;
            return true;
        }

        value = default;
        return false;
    }
}

public sealed class RecipeJsonParseResult
{
    public RecipeJsonParseResult(List<RecipeJsonParseRow> rows, List<string> globalErrors)
    {
        Rows = rows;
        GlobalErrors = globalErrors;
    }

    public List<RecipeJsonParseRow> Rows { get; }
    public List<string> GlobalErrors { get; }

    public static RecipeJsonParseResult WithGlobalError(string error)
    {
        return new RecipeJsonParseResult([], [error]);
    }
}

public sealed class RecipeJsonParseRow
{
    public RecipeJsonParseRow(int index, Rezept? recipe, List<RecipeIngredientInput> ingredients, List<string> errors)
    {
        Index = index;
        Recipe = recipe;
        Ingredients = ingredients;
        Errors = errors;
    }

    public int Index { get; }
    public Rezept? Recipe { get; }
    public List<RecipeIngredientInput> Ingredients { get; }
    public List<string> Errors { get; }
    public bool IsValid => Recipe != null && Errors.Count == 0;

    public static RecipeJsonParseRow WithErrors(int index, List<string> errors)
    {
        return new RecipeJsonParseRow(index, null, [], errors);
    }
}
