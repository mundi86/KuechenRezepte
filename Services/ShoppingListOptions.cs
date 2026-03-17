namespace KuechenRezepte.Services;

public class ShoppingListOptions
{
    public List<string> ExcludedIngredientTokens { get; set; } = [];
    public Dictionary<string, string> IngredientAliases { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public static ShoppingListOptions CreateDefault()
    {
        return new ShoppingListOptions
        {
            ExcludedIngredientTokens =
            [
                "salz",
                "meersalz",
                "jodsalz",
                "pfeffer",
                "wasser",
                "mineralwasser",
                "sprudelwasser",
                "leitungswasser"
            ],
            IngredientAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["ei"] = "Eier",
                ["eier"] = "Eier"
            }
        };
    }
}
