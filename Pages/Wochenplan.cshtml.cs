using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using KuechenRezepte.Data;
using KuechenRezepte.Models;

namespace KuechenRezepte.Pages;

public class WochenplanModel : PageModel
{
    private readonly AppDbContext _context;

    public WochenplanModel(AppDbContext context)
    {
        _context = context;
    }

    [BindProperty(SupportsGet = true)]
    public DateOnly Datum { get; set; }

    public DateOnly Montag { get; private set; }

    public List<TagEintrag> Woche { get; private set; } = new();
    public List<Rezept> AlleRezepte { get; private set; } = new();

    public int KalenderWoche { get; private set; }

    public async Task OnGetAsync()
    {
        Montag = Datum == default ? AktuellerMontag() : MontaguDerWoche(Datum);
        await LadeWocheAsync();
    }

    public async Task<IActionResult> OnPostAssignAsync(DateOnly datum, int rezeptId)
    {
        var mahlzeit = await _context.Mahlzeiten.FirstOrDefaultAsync(m => m.Datum == datum);
        if (mahlzeit == null)
        {
            mahlzeit = new Mahlzeit { Datum = datum };
            _context.Mahlzeiten.Add(mahlzeit);
        }

        mahlzeit.RezeptId = rezeptId;
        await _context.SaveChangesAsync();

        var monday = MontaguDerWoche(datum);
        return RedirectToPage(new { datum = monday.ToString("yyyy-MM-dd") });
    }

    public async Task<IActionResult> OnPostRemoveAsync(DateOnly datum)
    {
        var mahlzeit = await _context.Mahlzeiten.FirstOrDefaultAsync(m => m.Datum == datum);
        if (mahlzeit != null)
        {
            _context.Mahlzeiten.Remove(mahlzeit);
            await _context.SaveChangesAsync();
        }

        var monday = MontaguDerWoche(datum);
        return RedirectToPage(new { datum = monday.ToString("yyyy-MM-dd") });
    }

    private async Task LadeWocheAsync()
    {
        var sonntag = Montag.AddDays(6);
        KalenderWoche = ISOWeek.GetWeekOfYear(Montag.ToDateTime(TimeOnly.MinValue));

        var mahlzeiten = await _context.Mahlzeiten
            .Include(m => m.Rezept)
            .Where(m => m.Datum >= Montag && m.Datum <= sonntag)
            .ToListAsync();

        AlleRezepte = await _context.Rezepte
            .OrderBy(r => r.Name)
            .ToListAsync();

        Woche = Enumerable.Range(0, 7)
            .Select(i =>
            {
                var tag = Montag.AddDays(i);
                var mahlzeit = mahlzeiten.FirstOrDefault(m => m.Datum == tag);
                return new TagEintrag(tag, mahlzeit);
            })
            .ToList();
    }

    private static DateOnly AktuellerMontag()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        return MontaguDerWoche(today);
    }

    public static DateOnly MontaguDerWoche(DateOnly datum)
    {
        var dayOfWeek = (int)datum.DayOfWeek;
        var daysFromMonday = dayOfWeek == 0 ? 6 : dayOfWeek - 1;
        return datum.AddDays(-daysFromMonday);
    }

    public record TagEintrag(DateOnly Datum, Mahlzeit? Mahlzeit);
}
