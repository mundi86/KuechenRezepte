using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using KuechenRezepte.Data;
using KuechenRezepte.Models;

namespace KuechenRezepte.Services;

public class MealPlanService : IMealPlanDayService
{
    private readonly AppDbContext _context;
    private readonly RecipeQueryService _recipeQueryService;
    private readonly ShoppingListOptions _shoppingListOptions;

    public MealPlanService(
        AppDbContext context,
        RecipeQueryService recipeQueryService,
        IOptions<ShoppingListOptions>? shoppingListOptions = null)
    {
        _context = context;
        _recipeQueryService = recipeQueryService;
        _shoppingListOptions = shoppingListOptions?.Value ?? ShoppingListOptions.CreateDefault();
    }

    public async Task<MealPlanWeekResult> GetWeekAsync(DateOnly? datum, CancellationToken cancellationToken = default)
    {
        var montag = datum == null || datum.Value == default
            ? AktuellerMontag()
            : MontagDerWoche(datum.Value);

        var sonntag = montag.AddDays(6);
        var mahlzeiten = await _context.Mahlzeiten
            .Include(m => m.Rezept)
            .Where(m => m.Datum >= montag && m.Datum <= sonntag)
            .ToListAsync(cancellationToken);

        return new MealPlanWeekResult
        {
            Montag = montag,
            KalenderWoche = ISOWeek.GetWeekOfYear(montag.ToDateTime(TimeOnly.MinValue)),
            Mahlzeiten = mahlzeiten,
            AlleRezepte = await _recipeQueryService.GetAllOrderedByNameAsync(cancellationToken)
        };
    }

    public async Task<MealPlanDayResult> GetDayAsync(DateOnly datum, CancellationToken cancellationToken = default)
    {
        var mahlzeit = await _context.Mahlzeiten
            .Include(m => m.Rezept)
            .FirstOrDefaultAsync(m => m.Datum == datum, cancellationToken);

        var rezept = mahlzeit?.Rezept;
        var wochentag = CultureInfo.GetCultureInfo("de-DE").DateTimeFormat.GetDayName(datum.DayOfWeek);
        var speech = BuildDaySpeechText(datum, rezept);

        return new MealPlanDayResult
        {
            Datum = datum,
            Wochentag = wochentag,
            RezeptId = rezept?.Id,
            RezeptName = rezept?.Name,
            Kategorie = rezept?.Kategorie,
            Zubereitungszeit = rezept?.Zubereitungszeit,
            SpeechText = speech
        };
    }

    public async Task<ShoppingListResult> GetShoppingListAsync(DateOnly? datum, CancellationToken cancellationToken = default)
    {
        var montag = datum == null || datum.Value == default
            ? AktuellerMontag()
            : MontagDerWoche(datum.Value);
        var sonntag = montag.AddDays(6);

        var mahlzeiten = await _context.Mahlzeiten
            .Include(m => m.Rezept)
            .ThenInclude(r => r!.RezeptZutaten)
            .ThenInclude(rz => rz.Zutat)
            .Where(m => m.Datum >= montag && m.Datum <= sonntag)
            .ToListAsync(cancellationToken);

        return new ShoppingListResult
        {
            Montag = montag,
            KalenderWoche = ISOWeek.GetWeekOfYear(montag.ToDateTime(TimeOnly.MinValue)),
            Items = AggregateShoppingItems(mahlzeiten, _shoppingListOptions)
        };
    }

    public async Task AssignAsync(DateOnly datum, int rezeptId, CancellationToken cancellationToken = default)
    {
        var mahlzeit = await _context.Mahlzeiten.FirstOrDefaultAsync(m => m.Datum == datum, cancellationToken);
        if (mahlzeit == null)
        {
            mahlzeit = new Mahlzeit { Datum = datum };
            _context.Mahlzeiten.Add(mahlzeit);
        }

        mahlzeit.RezeptId = rezeptId;
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveAsync(DateOnly datum, CancellationToken cancellationToken = default)
    {
        var mahlzeit = await _context.Mahlzeiten.FirstOrDefaultAsync(m => m.Datum == datum, cancellationToken);
        if (mahlzeit == null)
        {
            return;
        }

        _context.Mahlzeiten.Remove(mahlzeit);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public static DateOnly MontagDerWoche(DateOnly datum)
    {
        var dayOfWeek = (int)datum.DayOfWeek;
        var daysFromMonday = dayOfWeek == 0 ? 6 : dayOfWeek - 1;
        return datum.AddDays(-daysFromMonday);
    }

    private static DateOnly AktuellerMontag()
    {
        return MontagDerWoche(DateOnly.FromDateTime(DateTime.Today));
    }

    internal static string BuildDaySpeechText(DateOnly datum, Rezept? rezept)
    {
        var weekday = CultureInfo.GetCultureInfo("de-DE").DateTimeFormat.GetDayName(datum.DayOfWeek);
        var dateText = datum.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
        if (rezept == null)
        {
            return $"Am {weekday}, den {dateText}, ist kein Rezept im Wochenplan eingetragen.";
        }

        var prepText = rezept.Zubereitungszeit.HasValue
            ? $" Die Zubereitung dauert etwa {rezept.Zubereitungszeit.Value} Minuten."
            : string.Empty;

        return $"Am {weekday}, den {dateText}, gibt es {rezept.Name}.{prepText}";
    }

    internal static List<ShoppingListItem> AggregateShoppingItems(IEnumerable<Mahlzeit> mahlzeiten, ShoppingListOptions? options = null)
    {
        options ??= ShoppingListOptions.CreateDefault();
        var entries = new Dictionary<string, ShoppingAccumulator>(StringComparer.OrdinalIgnoreCase);

        foreach (var mahlzeit in mahlzeiten)
        {
            var rezept = mahlzeit.Rezept;
            if (rezept == null)
            {
                continue;
            }

            foreach (var rz in rezept.RezeptZutaten)
            {
                var ingredientName = NormalizeShoppingListIngredientName(rz.Zutat?.Name, options);
                if (string.IsNullOrWhiteSpace(ingredientName))
                {
                    continue;
                }

                if (ShouldExcludeFromShoppingList(ingredientName, options))
                {
                    continue;
                }

                var unit = string.IsNullOrWhiteSpace(rz.Einheit) ? null : rz.Einheit.Trim();
                var key = $"{ingredientName}|{unit ?? string.Empty}";

                if (!entries.TryGetValue(key, out var bucket))
                {
                    bucket = new ShoppingAccumulator(ingredientName, unit);
                    entries[key] = bucket;
                }

                if (TryParseAmount(rz.Menge, out var parseResult))
                {
                    if (parseResult.Kind == ParsedAmountKind.Exact && parseResult.ExactAmount.HasValue)
                    {
                        bucket.ExactSum += parseResult.ExactAmount.Value;
                        bucket.HasExactAmount = true;
                    }
                    else if (parseResult.Kind == ParsedAmountKind.Range &&
                             parseResult.RangeMin.HasValue &&
                             parseResult.RangeMax.HasValue)
                    {
                        bucket.RangeMinSum += parseResult.RangeMin.Value;
                        bucket.RangeMaxSum += parseResult.RangeMax.Value;
                        bucket.HasRangeAmount = true;
                    }
                }
                else if (!string.IsNullOrWhiteSpace(rz.Menge))
                {
                    if (!bucket.Hints.Contains(rz.Menge.Trim(), StringComparer.OrdinalIgnoreCase))
                    {
                        bucket.Hints.Add(rz.Menge.Trim());
                    }
                }
            }
        }

        return entries.Values
            .OrderBy(v => v.Name, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(v => v.Unit ?? string.Empty, StringComparer.CurrentCultureIgnoreCase)
            .Select(v => new ShoppingListItem
            {
                Name = v.Name,
                Einheit = v.Unit,
                MengeSumme = v.HasExactAmount && !v.HasRangeAmount ? v.ExactSum : null,
                MengeVonSumme = v.HasRangeAmount ? v.RangeMinSum + (v.HasExactAmount ? v.ExactSum : 0m) : null,
                MengeBisSumme = v.HasRangeAmount ? v.RangeMaxSum + (v.HasExactAmount ? v.ExactSum : 0m) : null,
                MengenHinweise = v.Hints
            })
            .ToList();
    }

    internal static string? NormalizeShoppingListIngredientName(string? rawName, ShoppingListOptions? options = null)
    {
        options ??= ShoppingListOptions.CreateDefault();

        if (string.IsNullOrWhiteSpace(rawName))
        {
            return null;
        }

        var normalized = Regex.Replace(rawName.Trim(), @"\s+", " ");
        normalized = Regex.Replace(normalized, @"\s*\([^)]*\)", string.Empty);

        normalized = Regex.Replace(normalized, @"\s+", " ").Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        var key = NormalizeShoppingListKey(normalized);
        return options.IngredientAliases.TryGetValue(key, out var alias)
            ? alias
            : normalized;
    }

    internal static bool ShouldExcludeFromShoppingList(string? ingredientName, ShoppingListOptions? options = null)
    {
        options ??= ShoppingListOptions.CreateDefault();
        var pantryStaples = new HashSet<string>(options.ExcludedIngredientTokens, StringComparer.OrdinalIgnoreCase);
        var normalized = NormalizeShoppingListIngredientName(ingredientName, options);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        return NormalizeShoppingListKey(normalized)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(token => pantryStaples.Contains(token));
    }

    private static string NormalizeShoppingListKey(string value)
    {
        var normalized = Regex.Replace(value.ToLowerInvariant(), @"[^\p{L}\p{Nd}]+", " ");
        return Regex.Replace(normalized, @"\s+", " ").Trim();
    }

    private static bool TryParseAmount(string? raw, out ParsedAmount parseResult)
    {
        parseResult = default;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var normalized = Regex.Replace(raw.Trim(), @"\s+", " ");
        var rangeMatch = Regex.Match(normalized, @"^(?<min>[^-–]+)\s*[-–]\s*(?<max>.+)$");
        if (rangeMatch.Success &&
            TryParseSingleAmount(rangeMatch.Groups["min"].Value, out var min) &&
            TryParseSingleAmount(rangeMatch.Groups["max"].Value, out var max))
        {
            var orderedMin = Math.Min(min, max);
            var orderedMax = Math.Max(min, max);
            parseResult = new ParsedAmount
            {
                Kind = ParsedAmountKind.Range,
                RangeMin = orderedMin,
                RangeMax = orderedMax
            };
            return true;
        }

        if (TryParseSingleAmount(normalized, out var exact))
        {
            parseResult = new ParsedAmount
            {
                Kind = ParsedAmountKind.Exact,
                ExactAmount = exact
            };
            return true;
        }

        return false;
    }

    private static bool TryParseSingleAmount(string raw, out decimal amount)
    {
        amount = 0;
        var normalized = raw.Trim().Replace(',', '.');

        if (decimal.TryParse(
            normalized,
            NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture,
            out amount))
        {
            return true;
        }

        var mixedMatch = Regex.Match(normalized, @"^(?<whole>\d+)\s+(?<num>\d+)\s*/\s*(?<den>\d+)$");
        if (mixedMatch.Success &&
            decimal.TryParse(mixedMatch.Groups["whole"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var whole) &&
            decimal.TryParse(mixedMatch.Groups["num"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var num) &&
            decimal.TryParse(mixedMatch.Groups["den"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var den) &&
            den != 0)
        {
            amount = whole + (num / den);
            return true;
        }

        var fractionMatch = Regex.Match(normalized, @"^(?<num>\d+)\s*/\s*(?<den>\d+)$");
        if (fractionMatch.Success &&
            decimal.TryParse(fractionMatch.Groups["num"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var fNum) &&
            decimal.TryParse(fractionMatch.Groups["den"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var fDen) &&
            fDen != 0)
        {
            amount = fNum / fDen;
            return true;
        }

        return false;
    }

    private sealed class ShoppingAccumulator
    {
        public ShoppingAccumulator(string name, string? unit)
        {
            Name = name;
            Unit = unit;
        }

        public string Name { get; }
        public string? Unit { get; }
        public decimal ExactSum { get; set; }
        public bool HasExactAmount { get; set; }
        public decimal RangeMinSum { get; set; }
        public decimal RangeMaxSum { get; set; }
        public bool HasRangeAmount { get; set; }
        public List<string> Hints { get; } = new();
    }

    private enum ParsedAmountKind
    {
        Exact,
        Range
    }

    private struct ParsedAmount
    {
        public ParsedAmountKind Kind { get; init; }
        public decimal? ExactAmount { get; init; }
        public decimal? RangeMin { get; init; }
        public decimal? RangeMax { get; init; }
    }
}
