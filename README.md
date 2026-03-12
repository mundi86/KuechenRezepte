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
- Chefkoch-Import (JSON-LD Parsing + Hauptbild-Übernahme)
- JSON-Batch-Import für viele Rezepte (Datei oder Paste, inkl. Dry-Run)
- API-Endpunkte für Tagesabfrage (`/api/mealplan/today`, `/api/mealplan/tomorrow`)
- Security-Hardening für APIs (Rate-Limits, Audit-Logs, Alexa-Signaturprüfung, IP-Allowlist)
- Accessibility-Verbesserungen (Skip-Link, Fokus-Styles, ARIA-Live, bessere Navigation-Semantik)
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
- `RecipeJsonImportService`: JSON-Parsing + flexibles Feldmapping + Validierung pro Datensatz
- `IImageStorage`/`LocalImageStorage`: abstrahierter Bildspeicher

## Chefkoch-Import (Details)

Beim Import aus `chefkoch.de` werden Felder aus JSON-LD gelesen und mit zusätzlicher Bildlogik ergänzt:

- Name, Beschreibung, Zubereitung, Portionen, Zeit und Zutaten werden aus JSON-LD gemappt.
- Für das Titelbild gilt folgende Priorität:
  1. Wrapper `ds-slider-image__image-wrap ds-teaser-link__image-wrap ds-slider-image__image-wrap--3_2`
  2. Wrapper `ds-slider-image__image-wrap ds-teaser-link__image-wrap`
  3. Fallback auf `img.ds-teaser-link__image`
- Protokoll-relative URLs (`//...`) werden auf `https://...` normalisiert.

### Bildspeicherung beim Speichern

- Wird beim manuellen Erstellen kein eigenes Bild hochgeladen, aber ein externer Bildlink (z. B. von Chefkoch) übergeben, lädt die App das Bild herunter und speichert es lokal unter `/uploads/...`.
- Validierung beim Download:
  - nur `http/https`
  - nur `jpg/jpeg/png/webp`
  - maximal 2 MB
- In `Create` wird `Rezept.BildPfad` als Hidden-Field mitgesendet, damit der importierte Bildpfad zwischen "Importieren" und "Rezept speichern" nicht verloren geht.

### Robustheit / Fehlerverhalten

- Netzwerkfehler, Timeouts oder blockierte Requests beim Chefkoch-Fetch führen nicht zu einem HTTP 500.
- Stattdessen erscheint im UI eine normale Import-Fehlermeldung ("Seite konnte nicht geladen werden").

## JSON-Import für viele Rezepte

Aufruf über `Neues Rezept` (Seitenpanel-Link) oder direkt `/Rezepte/ImportJson`.

### Typischer Word -> JSON Workflow

1. Word-Rezepte in ChatGPT einlesen lassen.
2. In ein einheitliches JSON-Format umwandeln lassen (Array von Rezeptobjekten).
3. JSON in der App hochladen/einfügen.
4. Optional zuerst `Nur prüfen (Dry Run)` aktivieren.
5. Danach echten Import ausführen.

### Unterstützte JSON-Formate

- Top-Level:
  - Array: `[{...}, {...}]`
  - Objekt mit Array: `{ "rezepte": [...] }` oder `{ "recipes": [...] }`
- Rezept-Felder (de/en Alias):
  - `name` / `titel` / `title`
  - `beschreibung` / `description`
  - `zubereitung` / `instructions` / `steps`
  - `kategorie` / `category`
  - `portionen` / `servings`
  - `zubereitungszeit` / `prepMinutes` / `durationMinutes`
  - `bildPfad` / `bild` / `image` / `imageUrl`
- Zutaten:
  - Array aus Objekten (`name`, `menge`, `einheit`) oder Strings (`"200 g Mehl"`).

### Kategorien

- Zielwerte: `Fruehstueck`, `Mittagessen`, `Abendessen`, `Dessert`, `Snack`, `Getraenk`
- Gängige Synonyme werden gemappt (z. B. `Frühstück`, `breakfast`, `drink`).
- Unbekannte Kategorien werden pro Datensatz als Fehler gemeldet.

### Validierung und Ergebnisreport

- Jeder Datensatz wird einzeln geprüft und verarbeitet.
- Pflichtfelder:
  - `name`
  - mindestens eine Zutat
- Der Import zeigt anschließend eine Ergebnisliste pro Zeile:
  - Index
  - Rezeptname
  - Status (`OK`/`Fehler`)
  - Nachricht (z. B. Import-ID oder Fehlertext)

## Accessibility (a11y)

Umgesetzt (WCAG-orientiert):

- Skip-Link am Seitenanfang (`Direkt zum Inhalt springen`)
- `main`-Landmarke mit Zielanker (`id="main-content"`)
- Hauptnavigation mit `aria-label`
- aktive Navigation mit `aria-current="page"`
- sichtbare, kontrastreiche `:focus-visible`-Styles
- Statusmeldungen mit `role="status"` und `aria-live="polite"`
- Ergebnis-Tabelle im JSON-Import mit `scope` für Header/Row-Header

## Mealplan API (Alexa-Basis)

Für Sprachassistenten oder andere Clients stehen Tages-Endpunkte bereit:

- `GET /api/mealplan/today`
- `GET /api/mealplan/tomorrow`
- `GET /api/mealplan/day/{date}` mit `date` im Format `yyyy-MM-dd`

Wenn `Security:Api:MealPlanApiKey` gesetzt ist, muss der Header mitgesendet werden:

- `X-API-Key: <dein-key>`

Beispiel:

```txt
GET /api/mealplan/day/2026-03-09
```

Beispiel-Response:

```json
{
  "datum": "2026-03-09",
  "wochentag": "Montag",
  "rezeptId": 12,
  "rezeptName": "Chili con Carne",
  "kategorie": "Abendessen",
  "zubereitungszeit": 35,
  "speechText": "Am Montag, den 09.03.2026, gibt es Chili con Carne. Die Zubereitung dauert etwa 35 Minuten."
}
```

Wenn kein Rezept geplant ist, liefert `speechText` eine passende "kein Eintrag"-Ansage.  
Damit kann ein Alexa Skill die Antwort direkt vorlesen.

## Alexa Webhook (Intent-Mapping)

Endpoint:

- `POST /api/alexa`

Sicherheitschecks:

- optionaler Skill-ID-Check via `Security:Alexa:SkillId`
- optionale Alexa-Signaturprüfung (`Signature`, `SignatureCertChainUrl`)
- Timestamp-Check via `request.timestamp` (standardmäßig aktiv)
- erlaubtes Zeitfenster über `Security:Alexa:MaxRequestAgeSeconds`
- optionale IP-Allowlist über `Security:Alexa:AllowedClientIps`

Unterstützte Request-Typen/Intents:

- `LaunchRequest`
- `IntentRequest`:
  - `TodayIntent` / `MealPlanTodayIntent`
  - `TomorrowIntent` / `MealPlanTomorrowIntent`
  - `DayIntent` / `MealPlanByDateIntent` (Slot `date` oder `datum` im Format `yyyy-MM-dd`)
  - `AMAZON.HelpIntent`
  - `AMAZON.FallbackIntent`

Beispiel-Request (`TodayIntent`):

```json
{
  "request": {
    "type": "IntentRequest",
    "intent": {
      "name": "TodayIntent"
    }
  }
}
```

Beispiel-Response:

```json
{
  "version": "1.0",
  "response": {
    "outputSpeech": {
      "type": "PlainText",
      "text": "Am Montag, den 09.03.2026, gibt es Chili con Carne. Die Zubereitung dauert etwa 35 Minuten."
    },
    "shouldEndSession": true
  }
}
```

## Alexa Developer Console Setup (Copy/Paste)

### 1. Skill erstellen

1. In der Alexa Developer Console `Create Skill` wählen.
2. Name z. B. `KuechenRezepte`.
3. Typ: `Custom`.
4. Hosting: `Provision your own` (Endpoint über deine ASP.NET App).

### 2. Invocation Name

Empfohlen:

- `küchen rezepte`

### 3. Intents anlegen

- `TodayIntent`
- `TomorrowIntent`
- `DayIntent` mit Slot:
  - Name: `date`
  - Type: `AMAZON.DATE`

### 4. Sample Utterances

`TodayIntent`:

- was gibt es heute
- was steht heute im wochenplan
- was essen wir heute

`TomorrowIntent`:

- was gibt es morgen
- was steht morgen im wochenplan
- was essen wir morgen

`DayIntent`:

- was gibt es am {date}
- was steht am {date} im wochenplan
- was essen wir am {date}

### 5. Endpoint konfigurieren

Im Skill unter `Endpoint`:

- `HTTPS`
- URL: `https://<deine-domain>/api/alexa`

Wichtig:

- Für lokale Entwicklung brauchst du einen Tunnel mit HTTPS (z. B. Cloudflare Tunnel, ngrok).
- Alexa akzeptiert nur öffentlich erreichbare HTTPS-Endpunkte.
- Für private Nutzung: Skill nicht veröffentlichen, nur im eigenen Konto aktivieren.

### 6. Schneller Endpoint-Test (ohne Alexa)

```bash
curl -X POST https://<deine-domain>/api/alexa \
  -H "Content-Type: application/json" \
  -d "{\"request\":{\"type\":\"IntentRequest\",\"intent\":{\"name\":\"TodayIntent\"}}}"
```

Wenn korrekt konfiguriert, kommt `version: 1.0` mit `response.outputSpeech.text` zurück.

## Security-Konfiguration

Beispiel in `appsettings.json`:

```json
"Security": {
  "Api": {
    "MealPlanApiKey": "dein-langer-zufalls-key"
  },
  "Alexa": {
    "SkillId": "amzn1.ask.skill.xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
    "ValidateTimestamp": true,
    "MaxRequestAgeSeconds": 150
  }
}
```

Hinweis:

- `MealPlanApiKey` leer: Mealplan-API ohne Header zugänglich.
- `SkillId` leer: Alexa-Endpoint prüft nur den Timestamp.
- `ValidateSignature` sollte in Produktion `true` sein.
- Empfohlen für privat: starken API-Key setzen und Skill nicht publizieren.

### Env Vars (empfohlen für Secrets)

Statt Keys direkt in `appsettings*.json`:

- `KUECHENREZEPTE_MEALPLAN_API_KEY`
- `KUECHENREZEPTE_ALEXA_SKILL_ID`

Diese Werte überschreiben die Konfiguration beim Start.

## Rate Limiting und Audit Logging

- Mealplan-API: Fixed Window 60 Requests/Minute
- Alexa-Endpoint: Fixed Window 30 Requests/Minute
- Bei Überschreitung: HTTP `429`
- Audit-Logs (structured):
  - `audit_type=mealplan ...`
  - `audit_type=alexa ...`
  - enthalten u. a. Endpoint/Intent, Client-IP, Erfolg/Fehlergrund

### 7. Optional als JSON-Interaction Model (Beispiel)

```json
{
  "interactionModel": {
    "languageModel": {
      "invocationName": "küchen rezepte",
      "intents": [
        {
          "name": "TodayIntent",
          "samples": ["was gibt es heute", "was steht heute im wochenplan", "was essen wir heute"]
        },
        {
          "name": "TomorrowIntent",
          "samples": ["was gibt es morgen", "was steht morgen im wochenplan", "was essen wir morgen"]
        },
        {
          "name": "DayIntent",
          "slots": [{ "name": "date", "type": "AMAZON.DATE" }],
          "samples": ["was gibt es am {date}", "was steht am {date} im wochenplan", "was essen wir am {date}"]
        },
        { "name": "AMAZON.HelpIntent", "samples": [] },
        { "name": "AMAZON.FallbackIntent", "samples": [] },
        { "name": "AMAZON.CancelIntent", "samples": [] },
        { "name": "AMAZON.StopIntent", "samples": [] }
      ]
    }
  }
}
```

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
git clone https://github.com/mundi/KuechenRezepte.git
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
3. Chefkoch-Import testen:
   - URL importieren
   - importierte Bildvorschau prüfen
   - speichern und prüfen, dass das Bild lokal unter `/uploads/...` liegt
4. JSON-Import testen:
   - Dry Run mit 2-3 Datensätzen
   - danach echter Import
   - Ergebnisreport und neue Rezepte auf `/` prüfen
5. Rezept bearbeiten + Bilder aktualisieren/löschen
6. Suche/Kategorie/Pagination prüfen
7. Wochenplan befüllen (`/Wochenplan`)
8. Einkaufsliste prüfen (`/Einkaufsliste`): Summen, Bereiche, Copy, Druck, Abhaken
9. Zufallsrezept (`/Random`)
10. Theme Toggle testen
11. Rezept löschen

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
