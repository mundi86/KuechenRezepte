namespace KuechenRezepte.Models;

public class Mahlzeit
{
    public int Id { get; set; }
    public DateOnly Datum { get; set; }
    public int? RezeptId { get; set; }
    public Rezept? Rezept { get; set; }
}
