using KuechenRezepte.Models;

namespace KuechenRezepte.Services;

public sealed class RecipeIngredientInput
{
    public string? Name { get; set; }
    public string? Menge { get; set; }
    public string? Einheit { get; set; }
}

public sealed class RecipeListQuery
{
    public string? SearchTerm { get; init; }
    public Kategorie? Kategorie { get; init; }
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 12;
}

public sealed class RecipeListResult
{
    public required List<Rezept> Rezepte { get; init; }
    public required int TotalCount { get; init; }
    public required int TotalPages { get; init; }
    public required int PageNumber { get; init; }
}

public sealed class MealPlanWeekResult
{
    public required DateOnly Montag { get; init; }
    public required int KalenderWoche { get; init; }
    public required List<Models.Mahlzeit> Mahlzeiten { get; init; }
    public required List<Rezept> AlleRezepte { get; init; }
}

public sealed class ShoppingListResult
{
    public required DateOnly Montag { get; init; }
    public required int KalenderWoche { get; init; }
    public required List<ShoppingListItem> Items { get; init; }
}

public sealed class ShoppingListItem
{
    public required string Name { get; init; }
    public string? Einheit { get; init; }
    public decimal? MengeSumme { get; init; }
    public decimal? MengeVonSumme { get; init; }
    public decimal? MengeBisSumme { get; init; }
    public required List<string> MengenHinweise { get; init; }
}
