using System.ComponentModel.DataAnnotations;

namespace KuechenRezepte.Models;

public class RezeptZutat
{
    public int Id { get; set; }

    public int RezeptId { get; set; }
    public Rezept? Rezept { get; set; }

    public int ZutatId { get; set; }
    public Zutat? Zutat { get; set; }

    [StringLength(50)]
    public string? Menge { get; set; }

    [StringLength(50)]
    public string? Einheit { get; set; }
}
