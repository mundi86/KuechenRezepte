# KüchenRezepte

Eine ASP.NET Core Razor Pages App zum Verwalten, Planen und Entdecken von Rezepten.

## Highlights

- Vollständiges Rezept-CRUD (Erstellen, Anzeigen, Bearbeiten, Löschen)
- Zutatenverwaltung pro Rezept (Menge + Einheit)
- Suche über Rezeptname, Beschreibung und Zutaten
- Kategoriefilter und Pagination
- Zufallsrezept / Inspiration
- Wochenplan (7 Tage, Navigation, Zuordnung/Entfernung)
- Einkaufsliste aus Wochenplan:
  - Aggregation nach Zutat + Einheit
  - unterstützt Dezimalzahlen, Brüche (`1/2`, `1 1/2`) und Bereiche (`2-3`)
  - Copy-to-Clipboard, Druckansicht, Abhaken mit Wochen-Persistenz (`localStorage`)
- Chefkoch-Import (JSON-LD Parsing)
- Bild-Upload inkl. Galerie/Lightbox (`jpg`, `jpeg`, `png`, `webp`, max. 2 MB)
- Seed-Daten beim ersten Start (5 Rezepte)
- Dark/Light Theme Toggle
- Modernes Aurora/Glass UI-Redesign (responsive)
- Eigenes passendes SVG-Favicon

## Tech Stack

- .NET 9 (`net9.0`)
- ASP.NET Core Razor Pages
- Entity Framework Core 9 + SQLite
- Bootstrap 5
- xUnit + Moq

## Architektur (aktuell)

### Service Layer

- `RecipeQueryService`: Lesen/Suche/Random/Listen
- `RecipeCommandService`: Create/Update/Delete mit transaktionalen Write-Flows
- `MealPlanService`: Wochenplan + Einkaufsliste (Aggregation)
- `IngredientService`: zentrale Zutaten-Normalisierung/GetOrCreate
- `RezeptImageService`: Validierung + Pfadverwaltung
- `IImageStorage`/`LocalImageStorage`: abstrahierter Bildspeicher

### Datenbank & Migrations

- App startet mit `Database.Migrate()`
- Legacy-Bridge für alte `EnsureCreated`-Datenbanken:
  - `MigrationBootstrapper` initialisiert `__EFMigrationsHistory`, wenn Bestands-Tabellen existieren
- Initial-Migration liegt im Ordner `Migrations/`

## Voraussetzungen

- .NET SDK 9.x
- Kein separater DB-Server nötig (SQLite Datei)

## Datenbankpfad

Standard:

```txt
C:\docker\KuechenRezepte\KuechenRezepte.db
```

Anpassbar in `appsettings.json`:

```json
"ConnectionStrings": {
  "DefaultConnection": "Data Source=C:\\docker\\KuechenRezepte\\KuechenRezepte.db"
}
```

## Lokales Setup

1. Repository klonen

```bash
git clone <repo-url>
cd KuechenRezepte
```

2. Restore

```bash
dotnet restore
```

3. Starten

```bash
dotnet watch run
```

## Upgrade von älteren Versionen (EnsureCreated -> Migrations)

Wenn eine bestehende Datenbank aus einer alten App-Version stammt:

1. Backup erstellen

```powershell
Copy-Item "C:\docker\KuechenRezepte\KuechenRezepte.db" "C:\docker\KuechenRezepte\KuechenRezepte.db.bak-$(Get-Date -Format yyyyMMdd-HHmmss)"
```

2. Neue Version starten (`dotnet run` / `dotnet watch run` / Docker)

Die App setzt die Migrationshistorie automatisch und führt anschließend reguläre Migrationen aus.

## Smoke Test

1. Startseite öffnen (`/`)
2. Rezept erstellen + Bilder hochladen
3. Rezept bearbeiten + Bilder aktualisieren/löschen
4. Suche/Kategorie/Pagination prüfen
5. Wochenplan befüllen (`/Wochenplan`)
6. Einkaufsliste prüfen (`/Einkaufsliste`): Summen, Bereiche, Copy, Druck, Abhaken
7. Zufallsrezept (`/Random`)
8. Theme Toggle testen
9. Rezept löschen

## Build & Tests

```bash
dotnet build KuechenRezepte.sln -c Release
dotnet test KuechenRezepte.sln -c Release
```

## Docker

Image bauen:

```bash
docker build -t kuechenrezepte:latest .
```

Container starten:

```bash
docker run --rm -p 6655:6655 \
  -v /docker/KuechenRezepte:/docker/KuechenRezepte \
  kuechenrezepte:latest
```

Dann im Browser: `http://localhost:6655`
