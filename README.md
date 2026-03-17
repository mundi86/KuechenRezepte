# 🍳 KüchenRezepte

Eine persönliche Rezeptverwaltung als **ASP.NET Core Razor Pages App** — Rezepte erfassen, Wochenplan befüllen, Einkaufsliste generieren und per Alexa abfragen.

![.NET](https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet) ![SQLite](https://img.shields.io/badge/SQLite-embedded-003B57?logo=sqlite) ![License](https://img.shields.io/badge/license-MIT-green)

---

## 📋 Inhaltsverzeichnis

- [Features](#-features)
- [Tech Stack](#-tech-stack)
- [Architektur](#-architektur)
- [Schnellstart](#-schnellstart)
- [Rezepte importieren](#-rezepte-importieren)
- [Wochenplan & Einkaufsliste](#-wochenplan--einkaufsliste)
- [Mealplan API](#-mealplan-api)
- [Alexa Integration](#-alexa-integration)
- [Security-Konfiguration](#-security-konfiguration)
- [Docker](#-docker)
- [Deployment](#-deployment)
- [Tests](#-tests)
- [Datenbank & Upgrade](#-datenbank--upgrade)

---

## ✨ Features

### Rezeptverwaltung
- 📝 Vollständiges Rezept-CRUD (Erstellen, Anzeigen, Bearbeiten, Löschen)
- 🧂 Zutatenverwaltung pro Rezept (Menge + Einheit)
- 🔍 Suche über Rezeptname, Beschreibung und Zutaten
- 🏷️ Kategoriefilter und Pagination
- 🎲 Zufallsrezept / Inspiration auf Knopfdruck
- 🖼️ Bild-Upload inkl. Galerie/Lightbox (`jpg`, `jpeg`, `png`, `webp`, max. 2 MB)

### Wochenplanung
- 📅 Wochenplan (7 Tage, Navigation, Zuordnung/Entfernung)
- 🛒 Einkaufsliste automatisch aus dem Wochenplan:
  - Aggregation nach Zutat + Einheit
  - Unterstützt Dezimalzahlen, Brüche (`1/2`, `1 1/2`) und Bereiche (`2-3`)
  - Copy-to-Clipboard, Druckansicht, Abhaken mit Wochen-Persistenz (`localStorage`)

### Import
- 🍴 **Website-Import** — URLs von `chefkoch.de` und `gaumenfreundin.de` automatisch parsen (JSON-LD + Bild)
- 📦 **JSON-Batch-Import** — viele Rezepte auf einmal importieren (Datei oder Paste, inkl. Dry-Run)

### Alexa & API
- 🔊 Alexa Skill Integration — „Was gibt es heute?" direkt per Sprachassistent
- 🌐 REST-API für externe Clients (`/api/mealplan/today`, `/api/mealplan/tomorrow`)

### UI & Accessibility
- 🌙 Dark/Light Theme Toggle
- 🎨 Modernes Aurora/Glass UI-Redesign (responsive)
- ♿ WCAG-orientierte Accessibility (Skip-Link, ARIA-Live, Fokus-Styles)
- 🌱 Seed-Daten beim ersten Start (5 Beispielrezepte)

---

## 🛠️ Tech Stack

| Bereich | Technologie |
|---|---|
| Framework | ASP.NET Core 9 Razor Pages |
| Datenbank | Entity Framework Core 9 + SQLite |
| Frontend | Bootstrap 5 |
| Tests | xUnit + Moq |
| Container | Docker (multi-stage, non-root) |

---

## 🏗️ Architektur

Die App folgt einem klaren **Service Layer Pattern** — Pages delegieren an Services, Services kapseln die Logik:

| Service | Aufgabe |
|---|---|
| `RecipeQueryService` | Lesen, Suche, Random, Listen |
| `RecipeCommandService` | Create/Update/Delete mit transaktionalem Write-Flow |
| `MealPlanService` | Wochenplan + Einkaufslisten-Aggregation |
| `IngredientService` | Zentrale Zutaten-Normalisierung (GetOrCreate) |
| `RezeptImageService` | Bild-Validierung + Pfadverwaltung |
| `RecipeJsonImportService` | JSON-Parsing, flexibles Feldmapping, Validierung |
| `LocalImageStorage` | Abstrahierter Bildspeicher (via `IImageStorage`) |
| `AlexaSkillService` | Intent-Handling für Alexa Requests |
| `ApiAuditLogger` | Strukturiertes Audit-Logging für alle API-Calls |

---

## 🚀 Schnellstart

**Voraussetzungen:** .NET SDK 9.x — kein separater Datenbankserver nötig (SQLite).

```bash
# 1. Repository klonen
git clone https://github.com/mundi/KuechenRezepte.git
cd KuechenRezepte

# 2. Dependencies laden
dotnet restore

# 3. Starten (mit Hot Reload)
dotnet watch run
```

Dann im Browser: `http://localhost:5000`

> **Datenbankpfad:** Standard ist `C:\docker\KuechenRezepte\KuechenRezepte.db`.
> Anpassbar in `appsettings.json` unter `ConnectionStrings:DefaultConnection`.
> Das Verzeichnis wird beim Start automatisch angelegt.

---

## 📥 Rezepte importieren

### 🍴 Website-Import

URL einer unterstützten Rezeptseite einfügen — aktuell `chefkoch.de` und `gaumenfreundin.de`. Die App liest automatisch Name, Zutaten, Zubereitung und Titelbild aus dem JSON-LD der Seite aus.

**Bildpriorität:**
1. Slider-Bild 3:2 (`ds-slider-image__image-wrap--3_2`)
2. Slider-Bild allgemein (`ds-slider-image__image-wrap`)
3. Fallback auf `img.ds-teaser-link__image`

> Die spezielle Bildpriorität betrifft `chefkoch.de`. Auf `gaumenfreundin.de` reicht in der Regel bereits das eingebettete Rezept-JSON-LD mit Bild-URL.

Wenn kein eigenes Bild hochgeladen wird, aber ein externer Link vorhanden ist, wird das Bild automatisch heruntergeladen und lokal unter `/uploads/...` gespeichert (max. 2 MB, nur `jpg/jpeg/png/webp`).

> Netzwerkfehler oder blockierte Requests führen nicht zum HTTP 500 — stattdessen erscheint eine saubere Fehlermeldung im UI.

---

### 📦 JSON-Batch-Import

Viele Rezepte auf einmal importieren — ideal für den **Word → ChatGPT → KüchenRezepte**-Workflow:

1. Word-Rezepte in ChatGPT einlesen
2. In einheitliches JSON-Format konvertieren lassen
3. JSON in der App hochladen oder einfügen
4. Optional: `Dry Run` aktivieren (prüft ohne zu speichern)
5. Echten Import ausführen

**Aufruf:** `Neues Rezept` → Seitenpanel-Link oder direkt `/Rezepte/ImportJson`

**Unterstützte Formate:**

```json
// Als Array
[{ "name": "...", "zutaten": [...] }, { ... }]

// Als Objekt mit Array
{ "rezepte": [...] }
{ "recipes": [...] }
```

**Unterstützte Felder** (deutsch/englisch):

| Feld | Aliase |
|---|---|
| Name | `name`, `titel`, `title` |
| Beschreibung | `beschreibung`, `description` |
| Zubereitung | `zubereitung`, `instructions`, `steps` |
| Kategorie | `kategorie`, `category` |
| Portionen | `portionen`, `servings` |
| Zubereitungszeit | `zubereitungszeit`, `prepMinutes`, `durationMinutes` |
| Bild | `bildPfad`, `bild`, `image`, `imageUrl` |

**Zutaten** als Objekte oder Strings:
```json
[{ "name": "Mehl", "menge": "200", "einheit": "g" }]
// oder
["200 g Mehl", "1 Prise Salz"]
```

**Kategorien:** `Fruehstueck`, `Mittagessen`, `Abendessen`, `Dessert`, `Snack`, `Getraenk`
(Synonyme wie `Frühstück`, `breakfast`, `drink` werden automatisch gemappt)

---

## 🛒 Wochenplan & Einkaufsliste

Über `/Wochenplan` lassen sich Rezepte den 7 Tagen der Woche zuweisen. Die Einkaufsliste unter `/Einkaufsliste` aggregiert alle Zutaten der Woche automatisch — gleiche Zutaten mit gleicher Einheit werden summiert. Brüche und Bereiche werden korrekt verarbeitet.

---

## 🌐 Mealplan API

REST-Endpunkte für externe Clients und Sprachassistenten:

```
GET /api/mealplan/today
GET /api/mealplan/tomorrow
GET /api/mealplan/day/{date}    # date: yyyy-MM-dd
```

Wenn `Security:Api:MealPlanApiKey` gesetzt ist, muss der Header mitgesendet werden:
```
X-API-Key: <dein-key>
```

**Beispiel-Response:**
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

`speechText` kann ein Alexa Skill direkt vorlesen. Wenn kein Rezept geplant ist, enthält das Feld eine passende „kein Eintrag"-Ansage.

---

## 🔊 Alexa Integration

### Endpoint

```
POST /api/alexa
```

**Unterstützte Intents:**

| Intent | Beispiel-Aussage |
|---|---|
| `TodayIntent` | „Was gibt es heute?" |
| `TomorrowIntent` | „Was essen wir morgen?" |
| `DayIntent` | „Was gibt es am Montag?" |
| `AMAZON.HelpIntent` | „Hilfe" |
| `AMAZON.FallbackIntent` | (Fallback) |

**Sicherheitschecks (konfigurierbar):**
- Skill-ID-Validierung
- Alexa-Signaturprüfung (Zertifikat + Signatur)
- Timestamp-Check (Standardfenster: 150 Sekunden)
- IP-Allowlist

### Alexa Developer Console Setup

1. Skill erstellen: `Custom` → `Provision your own`
2. Invocation Name: `küchen rezepte`
3. Intents anlegen: `TodayIntent`, `TomorrowIntent`, `DayIntent` (Slot `date`, Typ `AMAZON.DATE`)
4. Endpoint: `HTTPS` → `https://<deine-domain>/api/alexa`

> Für lokale Entwicklung: Tunnel mit HTTPS nötig (z. B. Cloudflare Tunnel, ngrok).

**Schnelltest ohne Alexa:**
```bash
curl -X POST https://<deine-domain>/api/alexa \
  -H "Content-Type: application/json" \
  -d '{"request":{"type":"IntentRequest","intent":{"name":"TodayIntent"}}}'
```

<details>
<summary>📋 JSON Interaction Model (Copy/Paste für Alexa Console)</summary>

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
</details>

---

## 🔒 Security-Konfiguration

### appsettings.json

```json
"Security": {
  "Api": {
    "MealPlanApiKey": ""
  },
  "Alexa": {
    "SkillId": "amzn1.ask.skill.xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
    "ValidateSignature": true,
    "ValidateTimestamp": true,
    "MaxRequestAgeSeconds": 150,
    "AllowedClientIps": []
  }
}
```

### Secrets per Umgebungsvariable (empfohlen)

Niemals Keys in `appsettings.json` committen — stattdessen Umgebungsvariablen setzen:

```bash
KUECHENREZEPTE_MEALPLAN_API_KEY=dein-langer-zufalls-key
KUECHENREZEPTE_ALEXA_SKILL_ID=amzn1.ask.skill.xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx
```

Siehe [.env.example](.env.example) als Vorlage.

**Hinweise:**
- `MealPlanApiKey` leer → API ohne Key zugänglich (nur für Dev)
- `SkillId` leer → Alexa-Endpoint prüft nur den Timestamp
- `ValidateSignature: true` ist in Produktion Pflicht

### Rate Limiting & Audit Logging

| Endpoint | Limit |
|---|---|
| `/api/mealplan/*` | 60 Requests/Minute |
| `/api/alexa` | 30 Requests/Minute |

Bei Überschreitung: HTTP `429`. Alle API-Zugriffe werden strukturiert geloggt (`audit_type=mealplan` / `audit_type=alexa`) inkl. Client-IP, Intent und Erfolg/Fehlergrund.

---

## 🐳 Docker

```bash
# Host-Verzeichnisse anlegen
mkdir -p /docker/KuechenRezepte/data \
         /docker/KuechenRezepte/uploads \
         /docker/KuechenRezepte/keys

# Rechte für den Container-User (UID 10001)
chown -R 10001:10001 /docker/KuechenRezepte/data \
                     /docker/KuechenRezepte/uploads \
                     /docker/KuechenRezepte/keys

# Stack starten
docker compose up -d --build
```

Dann im Browser: `http://localhost:6655`

Persistente Host-Pfade:
- `/docker/KuechenRezepte/data` → SQLite-Datenbank
- `/docker/KuechenRezepte/uploads` → hochgeladene/importierte Bilder
- `/docker/KuechenRezepte/keys` → ASP.NET Data Protection Keys

> Der Container läuft als non-root user `appuser` mit UID `10001`.

---

## 🚀 Deployment

### Lokal

Lokal läuft nur die **Test-/Entwicklungsinstanz**. Dafür kannst du entweder direkt mit .NET starten:

```bash
dotnet watch run
```

oder den Container lokal testweise bauen:

```bash
docker compose up --build
```

Für lokale Tests ist es in Ordnung, mit Dev-Settings oder einer separaten SQLite-Datei zu arbeiten. Die lokale Instanz ist **nicht** das Produktionssystem.

### Produktion

Die produktive Instanz läuft auf dem Remote-Host unter:

- Repo/Stack: `/docker/KuechenRezepte`
- Compose-Projekt: `docker compose`
- Exponierter Port: `6655`
- URL im LAN: `http://192.168.1.183:6655/`

Persistente Host-Pfade in Produktion:

- `/docker/KuechenRezepte/data` → SQLite-Datenbank
- `/docker/KuechenRezepte/uploads` → hochgeladene/importierte Bilder
- `/docker/KuechenRezepte/keys` → ASP.NET Data Protection Keys

### Erstes Setup auf dem Host

```bash
git clone <repo-url> /docker/KuechenRezepte
cd /docker/KuechenRezepte

mkdir -p /docker/KuechenRezepte/data \
         /docker/KuechenRezepte/uploads \
         /docker/KuechenRezepte/keys

chown -R 10001:10001 /docker/KuechenRezepte/data \
                     /docker/KuechenRezepte/uploads \
                     /docker/KuechenRezepte/keys

docker compose up -d --build
```

### Update-Workflow Produktion

```bash
cd /docker/KuechenRezepte
git pull
docker compose up -d --build
```

### Wichtige Betriebsdetails

- Die produktive SQLite-DB liegt auf dem Host unter `/docker/KuechenRezepte/data/KuechenRezepte.db`.
- Das Compose-Setup nutzt **keine Docker-Volumes**, sondern ausschließlich Host-Bind-Mounts.
- Der Container läuft als non-root mit UID `10001`; die Host-Verzeichnisse müssen diesem User schreibbar gehören.
- Die ASP.NET Data Protection Keys liegen bewusst persistent auf dem Host, damit Cookies/Tokens nach Container-Neustarts gültig bleiben.

---

## 🧪 Tests

```bash
dotnet build KuechenRezepte.sln -c Release
dotnet test KuechenRezepte.sln -c Release
```

Testabdeckung (xUnit + Moq):
- `ApiSecurityHelper` — IP-Auflösung, Key-Validierung
- `AlexaRequestSignatureValidator` — Zertifikat + Signaturprüfung
- `AlexaSkillService` — Intent-Routing
- `MealPlanService` — Tagesabfrage + Sprachtext
- `RecipeCommandService` — CRUD + transaktionale Flows
- `ChefkochImporter` — JSON-LD Parsing + Bildlogik
- `RecipeJsonImportService` — Feldmapping + Validierung
- `MigrationBootstrapper` — Legacy-DB-Upgrade

---

## 🗄️ Datenbank & Upgrade

Die App startet mit `Database.Migrate()` — Migrationen werden automatisch angewendet.

**Upgrade von älteren Versionen** (vor EF Core Migrations, d. h. `EnsureCreated`-Datenbanken):

```powershell
# 1. Backup erstellen
Copy-Item "C:\docker\KuechenRezepte\KuechenRezepte.db" `
          "C:\docker\KuechenRezepte\KuechenRezepte.db.bak-$(Get-Date -Format yyyyMMdd-HHmmss)"

# 2. Neue Version starten — Migration läuft automatisch
dotnet run
```

Der `MigrationBootstrapper` erkennt Bestands-Tabellen, initialisiert die Migrationshistorie und führt anschließend reguläre Migrationen durch.

---

## ✅ Smoke Test Checkliste

Nach dem Start kurz durchklicken:

- [ ] Startseite öffnen (`/`)
- [ ] Rezept erstellen + Bild hochladen
- [ ] Chefkoch-URL importieren, Bildvorschau prüfen, speichern
- [ ] JSON-Import: Dry Run → echter Import → Ergebnisreport prüfen
- [ ] Rezept bearbeiten + Bild aktualisieren/löschen
- [ ] Suche / Kategorie / Pagination testen
- [ ] Wochenplan befüllen (`/Wochenplan`)
- [ ] Einkaufsliste prüfen (`/Einkaufsliste`): Summen, Copy, Druck, Abhaken
- [ ] Zufallsrezept (`/Random`)
- [ ] Dark/Light Theme Toggle
- [ ] Rezept löschen

---

## 📄 Lizenz

[MIT](LICENSE) © 2026 mundi
