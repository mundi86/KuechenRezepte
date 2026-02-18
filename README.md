# KuechenRezepte

Eine ASP.NET Core Razor Pages App fuer das Verwalten von Rezepten.

## Funktionen

- Rezepte erstellen, anzeigen, bearbeiten und loeschen (CRUD)
- Zutaten pro Rezept verwalten (Menge + Einheit)
- Suche ueber Rezeptname, Beschreibung und Zutaten
- Filter nach Kategorie
- Zufallsrezept ("Inspiration")
- Rezept-Import ueber Chefkoch-URL (JSON-LD Parsing)
- Bild-Upload mit Galerie fuer Rezepte (`jpg`, `jpeg`, `png`, `webp`, max. 2 MB/Bild)

## Tech Stack

- .NET 9 (`net9.0`)
- ASP.NET Core Razor Pages
- Entity Framework Core 9
- SQL Server

## Voraussetzungen

- .NET SDK 9.x
- SQL Server (oder SQL Server LocalDB unter Windows)

## Lokales Setup

1. Repository oeffnen:

```powershell
cd KuechenRezepte
```

2. Abhaengigkeiten wiederherstellen:

```powershell
dotnet restore
```

3. Connection String setzen:

Standard in `appsettings.json`:

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=KuechenRezepte;Trusted_Connection=True;MultipleActiveResultSets=True;TrustServerCertificate=True;"
}
```

Falls du einen anderen SQL Server nutzt, passe `ConnectionStrings:DefaultConnection` entsprechend an.

4. App starten:

```powershell
dotnet run
```

Beim Start wird die Datenbank automatisch erzeugt (`EnsureCreated` in `Program.cs`).

## Lokales Testen (Smoke Test)

1. Startseite laden: Rezeptliste sichtbar.
2. Neues Rezept anlegen: Name + Zutaten + optional Bilder speichern.
3. Details pruefen: Zutaten und Bilder werden korrekt angezeigt.
4. Rezept bearbeiten: Daten und Bilder aktualisieren.
5. Suche/Filter testen: Trefferlisten verifizieren.
6. Zufallsrezept testen: `/Random` liefert ein vorhandenes Rezept.
7. Rezept loeschen: Datensatz und lokale Upload-Bilder werden entfernt.

## Build Check

```powershell
dotnet build -c Release
```

## Docker (optional)

Image bauen:

```powershell
docker build -t kuechenrezepte:latest .
```

Container starten (Port `6655`):

```powershell
docker run --rm -p 6655:6655 `
  -e "ConnectionStrings__DefaultConnection=Server=<sql-host>,1433;Database=KuechenRezepte;User Id=<user>;Password=<password>;Encrypt=False;TrustServerCertificate=True;" `
  kuechenrezepte:latest
```

Dann im Browser: `http://localhost:6655`
