using Microsoft.EntityFrameworkCore;
using KuechenRezepte.Models;

namespace KuechenRezepte.Data;

public static class RecipeSeeder
{
    private sealed record ZutatDef(string Name, string? Menge, string? Einheit);

    private sealed record RezeptDef(
        string Name,
        string Beschreibung,
        string Zubereitung,
        int Portionen,
        int Minuten,
        Kategorie Kategorie,
        ZutatDef[] Zutaten);

    public static async Task SeedAsync(AppDbContext context)
    {
        if (await context.Rezepte.AnyAsync()) return;

        foreach (var def in GetRecipes())
        {
            var rezept = new Rezept
            {
                Name = def.Name,
                Beschreibung = def.Beschreibung,
                Zubereitung = def.Zubereitung,
                Portionen = def.Portionen,
                Zubereitungszeit = def.Minuten,
                Kategorie = def.Kategorie,
            };

            context.Rezepte.Add(rezept);
            await context.SaveChangesAsync();

            foreach (var z in def.Zutaten)
            {
                var zutat = await context.Zutaten
                    .FirstOrDefaultAsync(x => x.Name.ToLower() == z.Name.ToLower());

                if (zutat == null)
                {
                    zutat = new Zutat { Name = z.Name };
                    context.Zutaten.Add(zutat);
                    await context.SaveChangesAsync();
                }

                context.RezeptZutaten.Add(new RezeptZutat
                {
                    RezeptId = rezept.Id,
                    ZutatId = zutat.Id,
                    Menge = z.Menge,
                    Einheit = z.Einheit,
                });
            }

            await context.SaveChangesAsync();
        }
    }

    private static IEnumerable<RezeptDef> GetRecipes() =>
    [
        new(
            Name: "Spaghetti Carbonara",
            Beschreibung: "Klassische römische Pasta mit cremiger Ei-Käse-Sauce und knusprigem Pancetta. Kein Schlagobers — nur Eier, Käse und Pasta-Kochwasser machen die Sauce samtig.",
            Zubereitung: "1. Spaghetti in reichlich gesalzenem Wasser al dente kochen.\n2. Pancetta in einer großen Pfanne ohne Fett knusprig anbraten, Knoblauch kurz mitbraten.\n3. Eier mit geriebenem Parmesan und viel frisch gemahlenem schwarzen Pfeffer verquirlen.\n4. Herd ausschalten. Abgetropfte Spaghetti in die Pfanne geben, 2–3 EL Kochwasser zugeben.\n5. Ei-Käse-Mischung unterrühren, dabei schnell und gleichmäßig rühren, bis eine cremige Sauce entsteht.\n6. Sofort servieren, mit extra Parmesan und Pfeffer.",
            Portionen: 4,
            Minuten: 25,
            Kategorie: Kategorie.Mittagessen,
            Zutaten:
            [
                new("Spaghetti", "400", "g"),
                new("Pancetta oder Speck", "150", "g"),
                new("Eier", "4", null),
                new("Parmesan", "80", "g"),
                new("Knoblauch", "2", "Zehen"),
                new("Schwarzer Pfeffer", null, null),
                new("Salz", null, null),
            ]
        ),
        new(
            Name: "Österreichisches Rindergulasch",
            Beschreibung: "Herzhaftes Gulasch mit zart geschmortem Rindfleisch und tiefer Paprikasauce. Klassiker der Wiener Küche — am besten mit Semmelknödeln oder Brot.",
            Zubereitung: "1. Rindfleisch in 3–4 cm große Würfel schneiden.\n2. Zwiebeln würfeln und in Schmalz bei mittlerer Hitze 15 Minuten langsam goldbraun anbraten — sie werden zur Basis der Sauce.\n3. Hitze erhöhen, Fleisch zugeben und von allen Seiten kräftig anbraten.\n4. Hitze reduzieren, Paprikapulver und Tomatenmark einrühren, 1 Minute mitrösten.\n5. Mit Rinderbrühe ablöschen, Knoblauch und Kümmel zugeben, aufkochen.\n6. Deckel auflegen, bei niedriger Hitze 90–100 Minuten schmoren, bis das Fleisch zart ist.\n7. Mit Salz abschmecken. Mit Semmelknödeln oder Weißbrot servieren.",
            Portionen: 4,
            Minuten: 120,
            Kategorie: Kategorie.Mittagessen,
            Zutaten:
            [
                new("Rindfleisch (Schulter)", "800", "g"),
                new("Zwiebeln", "400", "g"),
                new("Paprikapulver edelsüß", "3", "EL"),
                new("Tomatenmark", "2", "EL"),
                new("Rinderbrühe", "400", "ml"),
                new("Schmalz oder Öl", "2", "EL"),
                new("Knoblauch", "3", "Zehen"),
                new("Kümmel", "1", "TL"),
                new("Salz", null, null),
            ]
        ),
        new(
            Name: "Käsespätzle",
            Beschreibung: "Schwäbischer Klassiker mit hausgemachten Spätzle, würzigem Bergkäse und goldbraunen Röstzwiebeln. Wohlfühlessen pur — einfach und unwiderstehlich.",
            Zubereitung: "1. Mehl, Eier, Milch und Salz zu einem zähflüssigen Teig verühren, bis er Blasen wirft — ca. 5 Minuten kräftig rühren.\n2. Reichlich Salzwasser zum Kochen bringen. Teig portionsweise durch eine Spätzlepresse oder Spätzlehobel ins Wasser geben.\n3. Spätzle kurz kochen, bis sie an die Oberfläche steigen (ca. 2 Minuten), mit einer Schöpfkelle abschöpfen.\n4. Zwiebeln in Butter bei niedriger Hitze 20–25 Minuten sehr langsam goldbraun braten (Röstzwiebeln).\n5. Ofen auf 200°C vorheizen. Spätzle lagenweise abwechselnd mit geriebenem Käse in eine gebutterte Auflaufform schichten.\n6. Käse auf der letzten Lage verteilen, 10 Minuten überbacken. Röstzwiebeln obenauf, sofort servieren.",
            Portionen: 4,
            Minuten: 50,
            Kategorie: Kategorie.Mittagessen,
            Zutaten:
            [
                new("Mehl", "300", "g"),
                new("Eier", "3", null),
                new("Milch", "150", "ml"),
                new("Salz", "1", "TL"),
                new("Emmentaler oder Bergkäse", "200", "g"),
                new("Zwiebeln", "2", null),
                new("Butter", "60", "g"),
            ]
        ),
        new(
            Name: "Apfelkuchen mit Butterstreuseln",
            Beschreibung: "Saftiger Apfelkuchen mit knusprigen Butterstreuseln — ein Klassiker der deutschen Kaffeetafel. Duftet herrlich nach Zimt und macht immer Eindruck.",
            Zubereitung: "1. Ofen auf 175°C Ober-/Unterhitze vorheizen. Springform (26 cm) fetten und leicht bemehlen.\n2. Weiche Butter mit Zucker und Vanilleextrakt cremig aufschlagen. Eier einzeln unterrühren.\n3. Mehl mit Backpulver und einer Prise Salz mischen, abwechselnd mit Milch unterheben.\n4. Teig in die Springform füllen und glatt streichen.\n5. Äpfel schälen, vierteln, in Scheiben schneiden. Mit Zimt und Zitronensaft vermengen und gleichmäßig auf dem Teig verteilen.\n6. Für die Streusel: Mehl, kalte Butterwürfel, Zucker und Zimt mit den Fingern zu krümeligen Streuseln verkneten. Über die Äpfel streuen.\n7. Im heißen Ofen 45–50 Minuten backen, bis goldbraun. Etwas abkühlen lassen, mit Puderzucker bestäuben.",
            Portionen: 12,
            Minuten: 70,
            Kategorie: Kategorie.Dessert,
            Zutaten:
            [
                new("Mehl", "350", "g"),
                new("Butter", "170", "g"),
                new("Zucker", "160", "g"),
                new("Eier", "2", null),
                new("Vanilleextrakt", "1", "TL"),
                new("Backpulver", "1", "TL"),
                new("Milch", "50", "ml"),
                new("Äpfel", "3", null),
                new("Zimt", "2", "TL"),
                new("Zitronensaft", "1", "EL"),
                new("Puderzucker", null, null),
            ]
        ),
        new(
            Name: "Cremige Tomatensuppe mit Basilikum",
            Beschreibung: "Samtige Tomatensuppe aus reifen Tomaten, frischem Basilikum und einem Hauch Sahne. Schnell gemacht, tief im Geschmack — perfekt mit knusprigem Baguette.",
            Zubereitung: "1. Zwiebel würfeln und Knoblauch fein hacken. Beides in Olivenöl bei mittlerer Hitze 5 Minuten anschwitzen, nicht bräunen.\n2. Tomaten grob würfeln und zugeben. 10 Minuten bei mittlerer Hitze köcheln lassen.\n3. Gemüsebrühe zugießen und weitere 5 Minuten mitkochen.\n4. Suppe mit einem Stabmixer fein pürieren. Wer eine besonders glatte Suppe möchte, durch ein feines Sieb streichen.\n5. Sahne einrühren, mit Salz und einer Prise Zucker abschmecken. Frisch gehacktes Basilikum unterrühren.\n6. In Tellern anrichten, mit einem Schuss Sahne, Basilikumblättern und schwarzem Pfeffer garnieren.",
            Portionen: 4,
            Minuten: 35,
            Kategorie: Kategorie.Mittagessen,
            Zutaten:
            [
                new("Reife Tomaten", "1000", "g"),
                new("Zwiebel", "1", null),
                new("Knoblauch", "2", "Zehen"),
                new("Frisches Basilikum", "1", "Bund"),
                new("Olivenöl", "3", "EL"),
                new("Gemüsebrühe", "300", "ml"),
                new("Sahne", "100", "ml"),
                new("Salz", null, null),
                new("Zucker", "1", "Prise"),
            ]
        ),
    ];
}
