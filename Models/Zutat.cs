using System.ComponentModel.DataAnnotations;

namespace KuechenRezepte.Models;

public class Zutat
{
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    public ICollection<RezeptZutat> RezeptZutaten { get; set; } = new List<RezeptZutat>();
}
