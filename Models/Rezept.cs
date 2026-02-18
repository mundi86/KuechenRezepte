using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KuechenRezepte.Models;

public enum Kategorie
{
    Frühstück,
    Mittagessen,
    Abendessen,
    Dessert,
    Snack,
    Getränk
}

public class Rezept
{
    public int Id { get; set; }

    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Beschreibung { get; set; }

    public string? Zubereitung { get; set; }

    public int Portionen { get; set; } = 2;

    public int? Zubereitungszeit { get; set; }

    public Kategorie Kategorie { get; set; } = Kategorie.Mittagessen;

    [StringLength(500)]
    public string? BildPfad { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public ICollection<RezeptZutat> RezeptZutaten { get; set; } = new List<RezeptZutat>();
}
