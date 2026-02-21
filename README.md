# KuechenRezepte

Eine ASP.NET Core Razor Pages App fuer das Verwalten von Rezepten.

## Funktionen

- Rezepte erstellen, anzeigen, bearbeiten und loeschen (CRUD)
- Zutaten pro Rezept verwalten (Menge + Einheit)
- Suche ueber Rezeptname, Beschreibung und Zutaten
- Filter nach Kategorie
- Zufallsrezept / Inspiration (komplette Karte klickbar)
- Wochenplan: ein Rezept pro Tag, 7-Tage-Ansicht mit Wochennavigation, Wochenend-Hervorhebung
- Rezept-Import ueber Chefkoch-URL (JSON-LD Parsing)
- Bild-Upload mit Galerie fuer Rezepte (`jpg`, `jpeg`, `png`, `webp`, max. 2 MB/Bild)
- Seed-Daten: 5 deutsche Startrezepte beim ersten Start
- Dark Mode (Toggle, persistiert per `localStorage`)

## Tech Stack

- .NET 9 (`net9.0`)
- ASP.NET Core Razor Pages
- Entity Framework Core 9 mit **SQLite**
- Bootstrap 5

## Voraussetzungen

- .NET SDK 9.x
- Kein Datenbankserver noetig — SQLite-Datei wird automatisch angelegt

## Datenbankpfad

Die Datenbank liegt unter:

```
C:\docker\KuechenRezepte\KuechenRezepte.db
```

Dieses Verzeichnis liegt **ausserhalb des Repos** und wird beim App-Start automatisch erstellt.
Ein `git pull` beruehrt die Daten nicht.

Der Pfad laesst sich in `appsettings.json` anpassen:

```json
"ConnectionStrings": {
  "DefaultConnection": "Data Source=C:\\docker\\KuechenRezepte\\KuechenRezepte.db"
}
```

## Lokales Setup

1. Repository klonen:

```bash
git clone <repo-url>
cd KuechenRezepte
```

2. Abhaengigkeiten wiederherstellen:

```bash
dotnet restore
```

3. App starten:

```bash
dotnet run
```

Beim ersten Start wird:
- das Datenbankverzeichnis (`C:\docker\KuechenRezepte\`) angelegt
- die SQLite-Datenbank mit allen Tabellen erzeugt (`EnsureCreated`)
- mit 5 Startrezepten befuellt (nur wenn die DB leer ist)

## Lokales Testen (Smoke Test)

1. Startseite laden: Rezeptliste sichtbar (5 Seed-Rezepte).
2. Neues Rezept anlegen: Name + Zutaten + optional Bilder speichern → landet auf Detailseite, Bild sofort sichtbar.
3. Rezept bearbeiten: Daten und Bilder aktualisieren → landet auf Detailseite.
4. Suche/Filter testen: Trefferlisten verifizieren.
5. Zufallsrezept testen: `/Random` — Karte komplett klickbar, Detail oeffnet sich.
6. Wochenplan testen: `/Wochenplan` — Rezept zuweisen, entfernen, Woche navigieren.
7. Dark Mode testen: Toggle in der Navbar — wechselt, bleibt nach Reload erhalten.
8. Rezept loeschen: Datensatz und lokale Upload-Bilder werden entfernt.

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

Container starten (Port `6655`, SQLite-Volume einbinden):

```bash
docker run --rm -p 6655:6655 \
  -v /docker/KuechenRezepte:/docker/KuechenRezepte \
  kuechenrezepte:latest
```

Dann im Browser: `http://localhost:6655`
