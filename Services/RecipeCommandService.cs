using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using KuechenRezepte.Data;
using KuechenRezepte.Helpers;
using KuechenRezepte.Models;

namespace KuechenRezepte.Services;

public class RecipeCommandService
{
    private readonly AppDbContext _context;
    private readonly IngredientService _ingredientService;
    private readonly RezeptImageService _imageService;

    public RecipeCommandService(
        AppDbContext context,
        IngredientService ingredientService,
        RezeptImageService imageService)
    {
        _context = context;
        _ingredientService = ingredientService;
        _imageService = imageService;
    }

    public async Task<(bool Success, string? Error, int? RecipeId)> CreateAsync(
        Rezept rezept,
        IEnumerable<RecipeIngredientInput> zutaten,
        IEnumerable<IFormFile> bilder,
        CancellationToken cancellationToken = default)
    {
        var imageList = (bilder ?? []).Where(b => b.Length > 0).ToList();
        foreach (var image in imageList)
        {
            if (!_imageService.IsValidImage(image, out var error))
            {
                return (false, error ?? "Ungültiges Bild", null);
            }
        }

        rezept.Name = (StringHelper.TrimToLength(rezept.Name, 200) ?? string.Empty).Trim();
        rezept.Beschreibung = StringHelper.TrimToLength(rezept.Beschreibung, 2000);
        rezept.Zubereitung = rezept.Zubereitung?.Trim();
        rezept.BildPfad = StringHelper.TrimToLength(rezept.BildPfad, 500);
        rezept.CreatedAt = DateTime.UtcNow;
        rezept.UpdatedAt = DateTime.UtcNow;

        var savedPaths = new List<string>();
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            _context.Rezepte.Add(rezept);
            await _context.SaveChangesAsync(cancellationToken);

            if (imageList.Count > 0)
            {
                savedPaths = await _imageService.SaveImagesAsync(rezept.Id, imageList, cancellationToken);
                if (savedPaths.Count > 0)
                {
                    rezept.BildPfad = savedPaths[0];
                }
            }

            await AttachIngredientsAsync(rezept.Id, zutaten, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return (true, null, rezept.Id);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            // Compensate file writes when DB transaction fails.
            _imageService.DeleteLocalImages(savedPaths);
            throw;
        }
    }

    public async Task<(bool Success, string? Error)> UpdateAsync(
        Rezept rezeptInput,
        IEnumerable<RecipeIngredientInput> zutaten,
        IEnumerable<IFormFile> bilder,
        bool bilderLöschen,
        CancellationToken cancellationToken = default)
    {
        var rezept = await _context.Rezepte.FirstOrDefaultAsync(r => r.Id == rezeptInput.Id, cancellationToken);
        if (rezept == null)
        {
            return (false, "Rezept nicht gefunden");
        }

        var imageList = (bilder ?? []).Where(b => b.Length > 0).ToList();
        foreach (var image in imageList)
        {
            if (!_imageService.IsValidImage(image, out var error))
            {
                return (false, error ?? "Ungültiges Bild");
            }
        }

        var existingLocalImages = bilderLöschen
            ? _imageService.GetImagePaths(rezept.Id, rezept.BildPfad)
                .Where(path => !_imageService.IsExternalPath(path))
                .ToList()
            : [];

        var newSavedPaths = new List<string>();
        var originalPrimaryPath = rezept.BildPfad;

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            if (imageList.Count > 0)
            {
                newSavedPaths = await _imageService.SaveImagesAsync(rezept.Id, imageList, cancellationToken);
                if (newSavedPaths.Count > 0 &&
                    (string.IsNullOrWhiteSpace(rezept.BildPfad) || _imageService.IsExternalPath(rezept.BildPfad) || bilderLöschen))
                {
                    rezept.BildPfad = newSavedPaths[0];
                }
            }

            if (bilderLöschen && newSavedPaths.Count == 0)
            {
                rezept.BildPfad = null;
            }

            rezept.Name = (StringHelper.TrimToLength(rezeptInput.Name, 200) ?? string.Empty).Trim();
            rezept.Kategorie = rezeptInput.Kategorie;
            rezept.Portionen = rezeptInput.Portionen;
            rezept.Zubereitungszeit = rezeptInput.Zubereitungszeit;
            rezept.Beschreibung = StringHelper.TrimToLength(rezeptInput.Beschreibung, 2000);
            rezept.Zubereitung = rezeptInput.Zubereitung?.Trim();
            rezept.UpdatedAt = DateTime.UtcNow;

            var existingIngredients = await _context.RezeptZutaten
                .Where(rz => rz.RezeptId == rezept.Id)
                .ToListAsync(cancellationToken);
            _context.RezeptZutaten.RemoveRange(existingIngredients);

            await AttachIngredientsAsync(rezept.Id, zutaten, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            _imageService.DeleteLocalImages(newSavedPaths);
            throw;
        }

        if (bilderLöschen)
        {
            var preserveSet = new HashSet<string>(newSavedPaths, StringComparer.OrdinalIgnoreCase);
            var toDelete = existingLocalImages.Where(path => !preserveSet.Contains(path)).ToList();
            _imageService.DeleteLocalImages(toDelete);

            if (!string.IsNullOrWhiteSpace(originalPrimaryPath) &&
                !_imageService.IsExternalPath(originalPrimaryPath) &&
                !preserveSet.Contains(originalPrimaryPath))
            {
                _imageService.DeleteLocalImageIfPresent(originalPrimaryPath);
            }
        }

        return (true, null);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var rezept = await _context.Rezepte.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (rezept == null)
        {
            return false;
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        _context.Rezepte.Remove(rezept);
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        try
        {
            _imageService.DeleteAllLocalImages(rezept.Id, rezept.BildPfad);
        }
        catch
        {
            // DB state is already committed; keep delete operation best-effort.
        }

        return true;
    }

    private async Task AttachIngredientsAsync(
        int rezeptId,
        IEnumerable<RecipeIngredientInput> zutaten,
        CancellationToken cancellationToken)
    {
        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var zutatInput in zutaten.Where(z => !string.IsNullOrWhiteSpace(z.Name)))
        {
            var normalizedName = IngredientService.NormalizeName(zutatInput.Name);
            if (normalizedName == null)
            {
                continue;
            }

            if (!seenNames.Add(normalizedName))
            {
                continue;
            }

            var zutat = await _ingredientService.GetOrCreateByNameAsync(normalizedName, cancellationToken);
            if (zutat == null)
            {
                continue;
            }

            _context.RezeptZutaten.Add(new RezeptZutat
            {
                RezeptId = rezeptId,
                Zutat = zutat,
                Menge = StringHelper.TrimToLength(zutatInput.Menge?.Trim(), 50),
                Einheit = StringHelper.TrimToLength(zutatInput.Einheit?.Trim(), 50)
            });
        }
    }
}
