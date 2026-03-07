using KuechenRezepte.Services;

namespace KuechenRezepte.Tests;

public class ChefkochImporterTests
{
    // --- ParseIngredientLine ---

    [Fact]
    public void ParseIngredientLine_WithQuantityUnitAndName_ReturnsAllParts()
    {
        var result = ChefkochImporter.ParseIngredientLine("250 g Mehl");
        Assert.Equal("Mehl", result.Name);
        Assert.Equal("250", result.Menge);
        Assert.Equal("g", result.Einheit);
    }

    [Fact]
    public void ParseIngredientLine_WithQuantityAndName_NoUnit()
    {
        var result = ChefkochImporter.ParseIngredientLine("2 Eier");
        Assert.Equal("Eier", result.Name);
        Assert.Equal("2", result.Menge);
        Assert.Null(result.Einheit);
    }

    [Fact]
    public void ParseIngredientLine_WithKnownUnit_SetsEinheit()
    {
        var result = ChefkochImporter.ParseIngredientLine("1 TL Salz");
        Assert.Equal("Salz", result.Name);
        Assert.Equal("1", result.Menge);
        Assert.Equal("TL", result.Einheit);
    }

    [Fact]
    public void ParseIngredientLine_NoNumbers_ReturnsRawAsName()
    {
        var result = ChefkochImporter.ParseIngredientLine("Salz und Pfeffer");
        Assert.Equal("Salz und Pfeffer", result.Name);
        Assert.Null(result.Menge);
        Assert.Null(result.Einheit);
    }

    [Fact]
    public void ParseIngredientLine_FractionQuantity_ParsesCorrectly()
    {
        var result = ChefkochImporter.ParseIngredientLine("1/2 Zitrone");
        Assert.Equal("Zitrone", result.Name);
        Assert.Equal("1/2", result.Menge);
        Assert.Null(result.Einheit);
    }

    [Fact]
    public void ParseIngredientLine_ExtraWhitespace_Normalizes()
    {
        var result = ChefkochImporter.ParseIngredientLine("  500   ml   Milch  ");
        Assert.Equal("Milch", result.Name);
        Assert.Equal("500", result.Menge);
        Assert.Equal("ml", result.Einheit);
    }

    [Fact]
    public void ParseIngredientLine_UnknownUnit_IncludedInName()
    {
        // "Stange" is not in KnownUnits, so it should be prepended to the name.
        var result = ChefkochImporter.ParseIngredientLine("2 Stange Lauch");
        Assert.Contains("Stange", result.Name);
        Assert.Null(result.Einheit);
    }

    // --- ParseDurationToMinutes ---

    [Fact]
    public void ParseDurationToMinutes_Null_ReturnsNull()
    {
        var result = ChefkochImporter.ParseDurationToMinutes(null);
        Assert.Null(result);
    }

    [Fact]
    public void ParseDurationToMinutes_MinutesOnly_ReturnsMinutes()
    {
        var result = ChefkochImporter.ParseDurationToMinutes("PT30M");
        Assert.Equal(30, result);
    }

    [Fact]
    public void ParseDurationToMinutes_HoursAndMinutes_ReturnsTotalMinutes()
    {
        var result = ChefkochImporter.ParseDurationToMinutes("PT1H30M");
        Assert.Equal(90, result);
    }

    [Fact]
    public void ParseDurationToMinutes_InvalidInput_ReturnsNull()
    {
        var result = ChefkochImporter.ParseDurationToMinutes("invalid");
        Assert.Null(result);
    }

    [Fact]
    public void ParseDurationToMinutes_EmptyString_ReturnsNull()
    {
        var result = ChefkochImporter.ParseDurationToMinutes("");
        Assert.Null(result);
    }

    // --- ParsePortions ---

    [Fact]
    public void ParsePortions_Null_ReturnsNull()
    {
        var result = ChefkochImporter.ParsePortions(null);
        Assert.Null(result);
    }

    [Fact]
    public void ParsePortions_NumberWithText_ExtractsNumber()
    {
        var result = ChefkochImporter.ParsePortions("4 Portionen");
        Assert.Equal(4, result);
    }

    [Fact]
    public void ParsePortions_PlainNumber_ReturnsParsedValue()
    {
        var result = ChefkochImporter.ParsePortions("12");
        Assert.Equal(12, result);
    }

    [Fact]
    public void ParsePortions_NoNumber_ReturnsNull()
    {
        var result = ChefkochImporter.ParsePortions("keine Angabe");
        Assert.Null(result);
    }

    // --- Parse (full HTML) ---

    [Fact]
    public void Parse_ValidJsonLdRecipe_ReturnsImportResult()
    {
        const string html = """
            <html><body>
            <img class="ds-teaser-link__image" src="https://img.chefkoch-cdn.de/teaser.jpg" />
            <script type="application/ld+json">
            {
              "@type": "Recipe",
              "name": "Testrezept",
              "description": "Eine Beschreibung",
              "image": "https://img.chefkoch-cdn.de/jsonld.jpg",
              "recipeYield": "4 Portionen",
              "totalTime": "PT45M",
              "recipeIngredient": ["200 g Mehl", "2 Eier"]
            }
            </script>
            </body></html>
            """;

        var importer = new ChefkochImporter();
        var result = importer.Parse(html);

        Assert.NotNull(result);
        Assert.Equal("Testrezept", result.Name);
        Assert.Equal("Eine Beschreibung", result.Description);
        Assert.Equal(4, result.Portions);
        Assert.Equal(45, result.TotalMinutes);
        Assert.Equal(2, result.Ingredients.Count);
        Assert.Equal("https://img.chefkoch-cdn.de/teaser.jpg", result.ImageUrl);
    }

    [Fact]
    public void Parse_TeaserImageWithProtocolRelativeUrl_NormalizesToHttps()
    {
        const string html = """
            <html><body>
            <img class="foo ds-teaser-link__image bar" src="//img.chefkoch-cdn.de/teaser2.jpg" />
            <script type="application/ld+json">
            { "@type": "Recipe", "name": "Bildtest" }
            </script>
            </body></html>
            """;

        var importer = new ChefkochImporter();
        var result = importer.Parse(html);

        Assert.NotNull(result);
        Assert.Equal("https://img.chefkoch-cdn.de/teaser2.jpg", result.ImageUrl);
    }

    [Fact]
    public void Parse_NoRecipeType_ReturnsNull()
    {
        const string html = """
            <html><body>
            <script type="application/ld+json">
            { "@type": "Article", "name": "Kein Rezept" }
            </script>
            </body></html>
            """;

        var importer = new ChefkochImporter();
        var result = importer.Parse(html);

        Assert.Null(result);
    }

    [Fact]
    public void Parse_EmptyHtml_ReturnsNull()
    {
        var importer = new ChefkochImporter();
        var result = importer.Parse("<html></html>");
        Assert.Null(result);
    }
}
