using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using KuechenRezepte.Data;
using KuechenRezepte.Models;
using KuechenRezepte.Services;

namespace KuechenRezepte.Tests;

public class RecipeCommandServiceTests
{
    [Fact]
    public async Task CreateAsync_CreatesRecipeAndIngredients()
    {
        await using var conn = new SqliteConnection("Data Source=:memory:");
        await conn.OpenAsync();
        await using var context = CreateContext(conn);
        await context.Database.EnsureCreatedAsync();

        var commandService = CreateCommandService(context, CreateStorageMock().Object);

        var result = await commandService.CreateAsync(
            new Rezept { Name = "Pasta", Portionen = 2, Kategorie = Kategorie.Mittagessen },
            [
                new RecipeIngredientInput { Name = "Tomate", Menge = "2", Einheit = "Stk" },
                new RecipeIngredientInput { Name = "Nudeln", Menge = "500", Einheit = "g" }
            ],
            []);

        Assert.True(result.Success);
        Assert.NotNull(result.RecipeId);
        Assert.Equal(1, await context.Rezepte.CountAsync());
        Assert.Equal(2, await context.Zutaten.CountAsync());
        Assert.Equal(2, await context.RezeptZutaten.CountAsync());
    }

    [Fact]
    public async Task UpdateAsync_WithBilderLöschenAndNoNewImages_DeletesOldLocalImagesAfterSave()
    {
        await using var conn = new SqliteConnection("Data Source=:memory:");
        await conn.OpenAsync();
        await using var context = CreateContext(conn);
        await context.Database.EnsureCreatedAsync();

        var rezept = new Rezept { Name = "Alt", Portionen = 2, Kategorie = Kategorie.Mittagessen, BildPfad = "/uploads/rezept-1-old.jpg" };
        context.Rezepte.Add(rezept);
        await context.SaveChangesAsync();

        var storageMock = CreateStorageMock();
        storageMock.Setup(s => s.ListFiles("uploads", It.IsAny<string>()))
            .Returns(["uploads/rezept-1-old.jpg", "uploads/rezept-1-old2.jpg"]);
        storageMock.Setup(s => s.Exists(It.IsAny<string>())).Returns(true);

        var commandService = CreateCommandService(context, storageMock.Object);

        var result = await commandService.UpdateAsync(
            new Rezept { Id = rezept.Id, Name = "Neu", Portionen = 3, Kategorie = Kategorie.Abendessen },
            [new RecipeIngredientInput { Name = "Salz" }],
            [],
            bilderLöschen: true);

        Assert.True(result.Success);
        Assert.Equal("Neu", (await context.Rezepte.FindAsync(rezept.Id))!.Name);
        storageMock.Verify(s => s.Delete(It.Is<string>(p => p.Contains("rezept-1-old", StringComparison.OrdinalIgnoreCase))), Times.AtLeastOnce);
    }

    [Fact]
    public async Task DeleteAsync_WhenImageCleanupFails_StillDeletesRecipe()
    {
        await using var conn = new SqliteConnection("Data Source=:memory:");
        await conn.OpenAsync();
        await using var context = CreateContext(conn);
        await context.Database.EnsureCreatedAsync();

        var rezept = new Rezept { Name = "Zu Löschen", Portionen = 1, Kategorie = Kategorie.Snack };
        context.Rezepte.Add(rezept);
        await context.SaveChangesAsync();

        var storageMock = CreateStorageMock();
        storageMock.Setup(s => s.ListFiles("uploads", It.IsAny<string>()))
            .Throws(new IOException("cleanup failed"));

        var commandService = CreateCommandService(context, storageMock.Object);

        var deleted = await commandService.DeleteAsync(rezept.Id);

        Assert.True(deleted);
        Assert.Null(await context.Rezepte.FindAsync(rezept.Id));
    }

    private static AppDbContext CreateContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        return new AppDbContext(options);
    }

    private static RecipeCommandService CreateCommandService(AppDbContext context, IImageStorage storage)
    {
        var httpFactoryMock = new Mock<IHttpClientFactory>();
        var imageService = new RezeptImageService(storage, httpFactoryMock.Object);
        var ingredientService = new IngredientService(context);
        return new RecipeCommandService(context, ingredientService, imageService);
    }

    private static Mock<IImageStorage> CreateStorageMock()
    {
        var mock = new Mock<IImageStorage>(MockBehavior.Loose);
        mock.Setup(s => s.ListFiles(It.IsAny<string>(), It.IsAny<string>())).Returns([]);
        mock.Setup(s => s.Exists(It.IsAny<string>())).Returns(false);
        return mock;
    }
}
