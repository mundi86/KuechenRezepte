using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using KuechenRezepte.Data;
using KuechenRezepte.Pages.Rezepte;
using KuechenRezepte.Services;

namespace KuechenRezepte.Tests;

public class CreateModelImportTests
{
    [Fact]
    public async Task OnPostImportAsync_GaumenfreundinUrl_PopulatesImportedFields()
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

        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var httpClientFactory = CreateHttpClientFactory(html);
        var storage = CreateStorageMock();
        var imageService = new RezeptImageService(storage.Object, httpClientFactory.Object);
        var commandService = new RecipeCommandService(context, new IngredientService(context), imageService);
        var queryService = new RecipeQueryService(context);

        var model = new CreateModel(
            httpClientFactory.Object,
            new ChefkochImporter(),
            commandService,
            queryService)
        {
            ImportUrl = "https://www.gaumenfreundin.de/aioli-ohne-ei/"
        };

        var result = await model.OnPostImportAsync();

        Assert.IsType<PageResult>(result);
        Assert.True(model.ImportSucceeded);
        Assert.Equal("Import erfolgreich. Gaumenfreundin wurde erkannt und 6 Zutaten wurden übernommen.", model.ImportStatusMessage);
        Assert.Equal("Aioli ohne Ei einfach selber machen", model.Rezept.Name);
        Assert.Equal("Für meine wunderbar cremige Blitz-Aioli brauchst du nur eine Handvoll Zutaten und einen Pürierstab.", model.Rezept.Beschreibung);
        Assert.Equal(8, model.Rezept.Portionen);
        Assert.Equal(5, model.Rezept.Zubereitungszeit);
        Assert.Equal("https://www.gaumenfreundin.de/wp-content/uploads/2021/09/Aioli-ohne-Ei-einfaches-Rezept.jpg", model.Rezept.BildPfad);
        Assert.Equal(6, model.Zutaten.Count);
        Assert.Equal("Knoblauchzehen", model.Zutaten[0].Name);
        Assert.Equal("3", model.Zutaten[0].Menge);
        Assert.Equal("https://www.gaumenfreundin.de/aioli-ohne-ei/", model.ImportUrl);
        Assert.Contains("Knoblauch klein hacken.", model.Rezept.Zubereitung);
    }

    private static AppDbContext CreateContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        return new AppDbContext(options);
    }

    private static Mock<IHttpClientFactory> CreateHttpClientFactory(string html)
    {
        var handler = new StubHttpMessageHandler(html);
        var client = new HttpClient(handler);

        var mock = new Mock<IHttpClientFactory>();
        mock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(client);
        return mock;
    }

    private static Mock<IImageStorage> CreateStorageMock()
    {
        var mock = new Mock<IImageStorage>(MockBehavior.Loose);
        mock.Setup(s => s.ListFiles(It.IsAny<string>(), It.IsAny<string>())).Returns([]);
        mock.Setup(s => s.Exists(It.IsAny<string>())).Returns(false);
        return mock;
    }

    private sealed class StubHttpMessageHandler(string html) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(html)
            };

            response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/html");
            return Task.FromResult(response);
        }
    }
}
