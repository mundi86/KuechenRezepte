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
    public void Parse_GaumenfreundinJsonLdRecipe_ReturnsImportResult()
    {
        const string html = """
            <html><body>
            <script type="application/ld+json" class="yoast-schema-graph">
            {
              "@context":"https://schema.org",
              "@graph":[
                { "@type":"Article", "headline":"Ignorieren" },
                {
                  "@type":"Recipe",
                  "name":"Milchreis, Omas einfaches Rezept",
                  "description":"Milchreis selber machen ist mit Omas einfachem Rezept kinderleicht.",
                  "image":["https://www.gaumenfreundin.de/wp-content/uploads/2023/01/Milchreis-Gaumenfreundin.jpg"],
                  "recipeYield":["4","4 Portionen"],
                  "cookTime":"PT30M",
                  "totalTime":"PT30M",
                  "recipeIngredient":["1 EL Butter","250 g Milchreis ((Rundkornreis))","1 L Milch"],
                  "recipeInstructions":[
                    { "@type":"HowToStep", "text":"Butter in einem Topf erhitzen." },
                    { "@type":"HowToStep", "text":"Milch und Zucker zugeben und aufkochen lassen." }
                  ]
                }
              ]
            }
            </script>
            </body></html>
            """;

        var importer = new ChefkochImporter();
        var result = importer.Parse(html);

        Assert.NotNull(result);
        Assert.Equal("Milchreis, Omas einfaches Rezept", result.Name);
        Assert.Equal("Milchreis selber machen ist mit Omas einfachem Rezept kinderleicht.", result.Description);
        Assert.Equal(4, result.Portions);
        Assert.Equal(30, result.TotalMinutes);
        Assert.Equal("https://www.gaumenfreundin.de/wp-content/uploads/2023/01/Milchreis-Gaumenfreundin.jpg", result.ImageUrl);
        Assert.Equal(3, result.Ingredients.Count);
        Assert.Contains("Butter in einem Topf erhitzen.", result.Instructions);
        Assert.Contains("Milch und Zucker zugeben und aufkochen lassen.", result.Instructions);
    }

    [Theory]
    [InlineData("https://www.chefkoch.de/rezepte/123/test.html", "Chefkoch")]
    [InlineData("https://chefkoch.de/rezepte/123/test.html", "Chefkoch")]
    [InlineData("https://www.gaumenfreundin.de/milchreis-rezept/", "Gaumenfreundin")]
    [InlineData("https://gaumenfreundin.de/milchreis-rezept/", "Gaumenfreundin")]
    public void TryValidateSupportedUrl_WithSupportedHosts_ReturnsTrue(string url, string expectedSource)
    {
        var valid = ChefkochImporter.TryValidateSupportedUrl(url, out var uri, out var sourceName);

        Assert.True(valid);
        Assert.NotNull(uri);
        Assert.Equal(expectedSource, sourceName);
    }

    [Theory]
    [InlineData("https://example.com/rezept")]
    [InlineData("ftp://www.chefkoch.de/rezept")]
    [InlineData("https://user:pass@www.gaumenfreundin.de/rezept")]
    [InlineData("not a url")]
    public void TryValidateSupportedUrl_WithUnsupportedOrUnsafeUrl_ReturnsFalse(string url)
    {
        var valid = ChefkochImporter.TryValidateSupportedUrl(url, out var uri, out var sourceName);

        Assert.False(valid);
        Assert.Null(uri);
        Assert.Null(sourceName);
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
    public void Parse_PrefersSliderWrapImageOverGenericTeaserImage()
    {
        const string html = """
            <html><body>
            <img class="ds-teaser-link__image" src="https://img.chefkoch-cdn.de/wrong-teaser.jpg" />
            <figure class="ds-slider-image__image-wrap ds-teaser-link__image-wrap ds-slider-image__image-wrap--3_2">
                <img class="ds-teaser-link__image" src="https://img.chefkoch-cdn.de/right-main.jpg" />
            </figure>
            <script type="application/ld+json">
            { "@type": "Recipe", "name": "Bildpriorität" }
            </script>
            </body></html>
            """;

        var importer = new ChefkochImporter();
        var result = importer.Parse(html);

        Assert.NotNull(result);
        Assert.Equal("https://img.chefkoch-cdn.de/right-main.jpg", result.ImageUrl);
    }

    [Fact]
    public void Parse_SliderWrapImageWithProtocolRelativeUrl_NormalizesToHttps()
    {
        const string html = """
            <html><body>
            <div class="ds-slider-image__image-wrap ds-teaser-link__image-wrap ds-slider-image__image-wrap--3_2">
                <img class="ds-teaser-link__image" src="//img.chefkoch-cdn.de/right-main-2.jpg" />
            </div>
            <script type="application/ld+json">
            { "@type": "Recipe", "name": "Bildnormalisierung" }
            </script>
            </body></html>
            """;

        var importer = new ChefkochImporter();
        var result = importer.Parse(html);

        Assert.NotNull(result);
        Assert.Equal("https://img.chefkoch-cdn.de/right-main-2.jpg", result.ImageUrl);
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

    [Fact]
    public void Parse_GaumenfreundinRecipeJsonLd_ReturnsImportResult()
    {
        const string html = """
            <html><body>
            <script type="application/ld+json">
            {
              "@context": "https://schema.org",
              "@graph": [
                { "@type": "WebSite", "name": "Gaumenfreundin" },
                { "@type": "WebPage", "name": "Aioli ohne Ei" },
                {
                  "@type": "Recipe",
                  "name": "Aioli ohne Ei einfach selber machen",
                  "description": "Für meine wunderbar cremige Blitz-Aioli brauchst du nur eine Handvoll Zutaten und einen Pürierstab.",
                  "image": ["https://www.gaumenfreundin.de/wp-content/uploads/2021/09/Aioli-ohne-Ei-einfaches-Rezept.jpg"],
                  "recipeYield": ["8", "8 Portionen"],
                  "cookTime": "PT5M",
                  "totalTime": "PT5M",
                  "recipeIngredient": [
                    "3 Knoblauchzehen",
                    "100 ml Milch ((Zimmertemperatur))",
                    "200 ml Rapsöl (oder Sonnenblumenöl)",
                    "Salz und Pfeffer",
                    "1 Spritzer Zitronensaft",
                    "1 TL Senf"
                  ],
                  "recipeInstructions": [
                    { "@type": "HowToStep", "text": "Knoblauch klein hacken." },
                    { "@type": "HowToStep", "text": "Alle Zutaten in ein schmales, hohes Gefäß geben." }
                  ]
                }
              ]
            }
            </script>
            </body></html>
            """;

        var importer = new ChefkochImporter();
        var result = importer.Parse(html);

        Assert.NotNull(result);
        Assert.Equal("Aioli ohne Ei einfach selber machen", result.Name);
        Assert.Equal(8, result.Portions);
        Assert.Equal(5, result.TotalMinutes);
        Assert.Equal(6, result.Ingredients.Count);
        Assert.Equal("https://www.gaumenfreundin.de/wp-content/uploads/2021/09/Aioli-ohne-Ei-einfaches-Rezept.jpg", result.ImageUrl);
        Assert.Contains("Knoblauch klein hacken.", result.Instructions);
    }
}
