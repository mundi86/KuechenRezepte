using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using KuechenRezepte.Helpers;

namespace KuechenRezepte.Services;

public class ChefkochImporter
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

    public ImportResult? Parse(string html)
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

                var recipe = MapImportResult(recipeElement);
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

    private static ImportResult? MapImportResult(JsonElement recipeElement)
    {
        var name = ReadFlexibleString(recipeElement, "name");
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var description = ReadFlexibleString(recipeElement, "alternativeHeadline")
            ?? ReadFlexibleString(recipeElement, "description");

        return new ImportResult
        {
            Name = name.Trim(),
            Description = description?.Trim(),
            Instructions = ParseInstructions(recipeElement),
            Portions = ParsePortions(ReadFlexibleString(recipeElement, "recipeYield")),
            TotalMinutes = ParseTotalMinutes(recipeElement),
            ImageUrl = ParseImageUrl(recipeElement),
            Ingredients = ParseIngredients(recipeElement)
        };
    }

    private static string? ParseImageUrl(JsonElement recipeElement)
    {
        if (!recipeElement.TryGetProperty("image", out var imageElement))
        {
            return null;
        }

        return ReadFlexibleValue(imageElement, "url", "contentUrl");
    }

    internal static int? ParsePortions(string? recipeYield)
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

    internal static int? ParseDurationToMinutes(string? value)
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

    internal static ZutatInput ParseIngredientLine(string raw)
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

    public sealed class ImportResult
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
