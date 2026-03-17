using KuechenRezepte.Models;
using KuechenRezepte.Services;

namespace KuechenRezepte.Tests;

public class MealPlanServiceTests
{
    [Fact]
    public void BuildDaySpeechText_WithRecipe_ContainsRecipeAndTime()
    {
        var date = new DateOnly(2026, 3, 9); // Montag
        var rezept = new Rezept { Name = "Chili con Carne", Zubereitungszeit = 35 };

        var text = MealPlanService.BuildDaySpeechText(date, rezept);

        Assert.Contains("Montag", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Chili con Carne", text, StringComparison.Ordinal);
        Assert.Contains("35 Minuten", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildDaySpeechText_WithoutRecipe_ContainsNoPlanMessage()
    {
        var date = new DateOnly(2026, 3, 10); // Dienstag

        var text = MealPlanService.BuildDaySpeechText(date, null);

        Assert.Contains("Dienstag", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("kein Rezept im Wochenplan", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AggregateShoppingItems_SumsNumericAmounts_AndGroupsByNameAndUnit()
    {
        var rezeptA = new Rezept
        {
            RezeptZutaten =
            [
                new RezeptZutat { Zutat = new Zutat { Name = "Tomaten" }, Menge = "500", Einheit = "g" },
                new RezeptZutat { Zutat = new Zutat { Name = "Nudeln" }, Menge = "250", Einheit = "g" }
            ]
        };
        var rezeptB = new Rezept
        {
            RezeptZutaten =
            [
                new RezeptZutat { Zutat = new Zutat { Name = "Tomaten" }, Menge = "0,5", Einheit = "kg" },
                new RezeptZutat { Zutat = new Zutat { Name = "Nudeln" }, Menge = "250", Einheit = "g" }
            ]
        };

        var mahlzeiten = new[]
        {
            new Mahlzeit { Rezept = rezeptA },
            new Mahlzeit { Rezept = rezeptB }
        };

        var items = MealPlanService.AggregateShoppingItems(mahlzeiten);

        var nudeln = Assert.Single(items, i => i.Name == "Nudeln" && i.Einheit == "g");
        Assert.Equal(500m, nudeln.MengeSumme);

        var tomatenG = Assert.Single(items, i => i.Name == "Tomaten" && i.Einheit == "g");
        Assert.Equal(500m, tomatenG.MengeSumme);

        var tomatenKg = Assert.Single(items, i => i.Name == "Tomaten" && i.Einheit == "kg");
        Assert.Equal(0.5m, tomatenKg.MengeSumme);
    }

    [Fact]
    public void AggregateShoppingItems_CollectsNonNumericHints()
    {
        var rezept = new Rezept
        {
            RezeptZutaten =
            [
                new RezeptZutat { Zutat = new Zutat { Name = "Knoblauch" }, Menge = "2 Zehen", Einheit = null },
                new RezeptZutat { Zutat = new Zutat { Name = "Knoblauch" }, Menge = "nach Geschmack", Einheit = null }
            ]
        };

        var items = MealPlanService.AggregateShoppingItems([new Mahlzeit { Rezept = rezept }]);

        var knoblauch = Assert.Single(items, i => i.Name == "Knoblauch");
        Assert.Null(knoblauch.MengeSumme);
        Assert.Equal(2, knoblauch.MengenHinweise.Count);
        Assert.Contains("2 Zehen", knoblauch.MengenHinweise);
        Assert.Contains("nach Geschmack", knoblauch.MengenHinweise);
    }

    [Fact]
    public void AggregateShoppingItems_ParsesFractions_AndMixedFractions()
    {
        var rezept = new Rezept
        {
            RezeptZutaten =
            [
                new RezeptZutat { Zutat = new Zutat { Name = "Mehl" }, Menge = "1/2", Einheit = "kg" },
                new RezeptZutat { Zutat = new Zutat { Name = "Mehl" }, Menge = "1 1/2", Einheit = "kg" }
            ]
        };

        var items = MealPlanService.AggregateShoppingItems([new Mahlzeit { Rezept = rezept }]);
        var mehl = Assert.Single(items, i => i.Name == "Mehl" && i.Einheit == "kg");

        Assert.Equal(2.0m, mehl.MengeSumme);
        Assert.Null(mehl.MengeVonSumme);
        Assert.Null(mehl.MengeBisSumme);
    }

    [Fact]
    public void AggregateShoppingItems_ParsesRanges_AndAddsExactAmountsIntoRange()
    {
        var rezept = new Rezept
        {
            RezeptZutaten =
            [
                new RezeptZutat { Zutat = new Zutat { Name = "Karotten" }, Menge = "2-3", Einheit = "Stk" },
                new RezeptZutat { Zutat = new Zutat { Name = "Karotten" }, Menge = "1", Einheit = "Stk" }
            ]
        };

        var items = MealPlanService.AggregateShoppingItems([new Mahlzeit { Rezept = rezept }]);
        var karotten = Assert.Single(items, i => i.Name == "Karotten" && i.Einheit == "Stk");

        Assert.Null(karotten.MengeSumme);
        Assert.Equal(3m, karotten.MengeVonSumme);
        Assert.Equal(4m, karotten.MengeBisSumme);
    }

    [Fact]
    public void AggregateShoppingItems_NormalizesOptionalPluralVariants()
    {
        var rezept = new Rezept
        {
            RezeptZutaten =
            [
                new RezeptZutat { Zutat = new Zutat { Name = "Ei(er)" }, Menge = "2", Einheit = "Stk" },
                new RezeptZutat { Zutat = new Zutat { Name = "Eier" }, Menge = "3", Einheit = "Stk" },
                new RezeptZutat { Zutat = new Zutat { Name = "Ei" }, Menge = "1", Einheit = "Stk" }
            ]
        };

        var items = MealPlanService.AggregateShoppingItems([new Mahlzeit { Rezept = rezept }]);
        var eier = Assert.Single(items, i => i.Name == "Eier" && i.Einheit == "Stk");

        Assert.Equal(6m, eier.MengeSumme);
    }

    [Fact]
    public void AggregateShoppingItems_ExcludesPantryStaples()
    {
        var rezept = new Rezept
        {
            RezeptZutaten =
            [
                new RezeptZutat { Zutat = new Zutat { Name = "grobes Salz" }, Menge = "1", Einheit = "Prise" },
                new RezeptZutat { Zutat = new Zutat { Name = "schwarzer Pfeffer" }, Menge = "nach Geschmack", Einheit = null },
                new RezeptZutat { Zutat = new Zutat { Name = "kaltes Wasser" }, Menge = "500", Einheit = "ml" },
                new RezeptZutat { Zutat = new Zutat { Name = "Tomaten" }, Menge = "3", Einheit = "Stk" }
            ]
        };

        var items = MealPlanService.AggregateShoppingItems([new Mahlzeit { Rezept = rezept }]);

        var tomaten = Assert.Single(items);
        Assert.Equal("Tomaten", tomaten.Name);
        Assert.Equal("Stk", tomaten.Einheit);
        Assert.Equal(3m, tomaten.MengeSumme);
    }

    [Fact]
    public void ShouldExcludeFromShoppingList_RecognizesSinglePantryWordsInsideDescriptions()
    {
        Assert.True(MealPlanService.ShouldExcludeFromShoppingList("Meersalz"));
        Assert.True(MealPlanService.ShouldExcludeFromShoppingList("Sprudelwasser"));
        Assert.False(MealPlanService.ShouldExcludeFromShoppingList("Salzstangen"));
    }

    [Fact]
    public void AggregateShoppingItems_UsesConfiguredAliasesAndExcludedTokens()
    {
        var options = new ShoppingListOptions
        {
            ExcludedIngredientTokens = ["essig"],
            IngredientAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["fruehlingszwiebel"] = "Frühlingszwiebeln",
                ["frühlingszwiebel"] = "Frühlingszwiebeln"
            }
        };

        var rezept = new Rezept
        {
            RezeptZutaten =
            [
                new RezeptZutat { Zutat = new Zutat { Name = "Frühlingszwiebel" }, Menge = "2", Einheit = "Stk" },
                new RezeptZutat { Zutat = new Zutat { Name = "Essig" }, Menge = "1", Einheit = "EL" }
            ]
        };

        var items = MealPlanService.AggregateShoppingItems([new Mahlzeit { Rezept = rezept }], options);

        var zwiebeln = Assert.Single(items);
        Assert.Equal("Frühlingszwiebeln", zwiebeln.Name);
        Assert.Equal(2m, zwiebeln.MengeSumme);
    }
}
