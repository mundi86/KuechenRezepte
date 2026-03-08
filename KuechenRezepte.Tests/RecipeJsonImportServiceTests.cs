using KuechenRezepte.Models;
using KuechenRezepte.Services;

namespace KuechenRezepte.Tests;

public class RecipeJsonImportServiceTests
{
    private readonly RecipeJsonImportService _service = new();

    [Fact]
    public void Parse_ArrayPayload_MapsRecipeAndIngredients()
    {
        const string json = """
            [
              {
                "name": "Tomatensuppe",
                "beschreibung": "Schnell gemacht",
                "zubereitung": ["Tomaten kochen", "Puerieren"],
                "kategorie": "Abendessen",
                "portionen": 3,
                "zubereitungszeit": 20,
                "bildPfad": "https://example.com/suppe.jpg",
                "zutaten": [
                  { "name": "Tomaten", "menge": "800", "einheit": "g" },
                  "1 EL Olivenoel"
                ]
              }
            ]
            """;

        var result = _service.Parse(json);

        Assert.Empty(result.GlobalErrors);
        Assert.Single(result.Rows);
        var row = result.Rows[0];
        Assert.True(row.IsValid);
        Assert.NotNull(row.Recipe);
        Assert.Equal("Tomatensuppe", row.Recipe!.Name);
        Assert.Equal(Kategorie.Abendessen, row.Recipe.Kategorie);
        Assert.Equal(3, row.Recipe.Portionen);
        Assert.Equal(20, row.Recipe.Zubereitungszeit);
        Assert.Equal(2, row.Ingredients.Count);
    }

    [Fact]
    public void Parse_ObjectWithRezepteProperty_IsSupported()
    {
        const string json = """
            {
              "rezepte": [
                {
                  "title": "Pfannkuchen",
                  "category": "Dessert",
                  "ingredients": [
                    { "ingredient": "Mehl", "amount": "200", "unit": "g" }
                  ]
                }
              ]
            }
            """;

        var result = _service.Parse(json);

        Assert.Empty(result.GlobalErrors);
        Assert.Single(result.Rows);
        Assert.True(result.Rows[0].IsValid);
        Assert.Equal(Kategorie.Dessert, result.Rows[0].Recipe!.Kategorie);
    }

    [Fact]
    public void Parse_UnknownCategory_ReturnsRowError()
    {
        const string json = """
            [
              {
                "name": "X",
                "kategorie": "Unbekannt",
                "zutaten": [{ "name": "Test" }]
              }
            ]
            """;

        var result = _service.Parse(json);

        Assert.Empty(result.GlobalErrors);
        Assert.Single(result.Rows);
        Assert.False(result.Rows[0].IsValid);
        Assert.Contains(result.Rows[0].Errors, e => e.Contains("Unbekannte Kategorie", StringComparison.OrdinalIgnoreCase));
    }
}
