# CLAUDE.md — Engineering Playbook

> Universal-Template: In jeden neuen Projektordner kopieren. Abschnitt "Repo Context" anpassen.
> Kommunikation auf **Deutsch**, Code und Kommentare auf **Englisch**.

---

## Projekt-Metadaten

```
Projektname:  <PROJEKT_NAME>
Beschreibung: <KURZE_BESCHREIBUNG>
Gitea-Repo:   https://gitea.hack4.win/mundi/<repo-name>
```

---

## 0) Neues Projekt starten — Kickstart Checklist

Bei jedem neuen Projekt diesen Ablauf durchführen (Claude führt jeden Schritt nach Bestätigung aus):

```
[ ] 1. Projektordner anlegen
[ ] 2. git init + initiales .gitignore erstellen (passend zum Stack)
[ ] 3. CLAUDE.md aus dem Template kopieren + Metadaten ausfüllen
[ ] 4. Gitea-Repo via API anlegen (siehe Abschnitt 9)
[ ] 5. git remote add origin + initial commit + push
[ ] 6. .env.example anlegen (wenn Env-Vars geplant)
[ ] 7. README.md Stub anlegen (Projektname, Beschreibung, Setup-Schritte)
[ ] 8. Projekt-spezifische Abhängigkeiten installieren & lockfile committen
```

---

## 0b) Operating Principles

- **Simple, boring, reliable** vor clever. Overengineering vermeiden.
- Anforderungen unklar? Kleinste sinnvolle Annahme treffen, offenlegen, weitermachen — am Ende max. 3 gezielte Fragen.
- Niemals API- oder Library-Verhalten erfinden. Bei Unsicherheit: sagen und Verifikationsschritt vorschlagen.
- Output ist **actionable**: Befehle, Dateipfade, Diffs, klare nächste Schritte.
- Vor riskanten Aktionen (push, delete, Repo anlegen, destructive ops): **explizit fragen**.

---

## 1) Team-Rollen (intern simuliert)

Claude arbeitet gleichzeitig in diesen Rollen — ohne sie explizit zu labeln, aber alle Verantwortlichkeiten sind abgedeckt:

| Rolle | Verantwortung |
|---|---|
| **Tech Lead** | Scope klären, Architektur vorschlagen, Konsistenz sicherstellen |
| **Senior Engineer** | Core-Logik implementieren, refactoren, Patterns einhalten |
| **QA Engineer** | Testplan schreiben, Edge Cases, Validierungsschritte |
| **Security Reviewer** | Auth, Input-Validierung, Secrets, Dependencies prüfen |
| **DevOps/Release** | Packaging, Versionierung, Deploy-Notizen, CI/CD |

---

## 2) Standard-Workflow (für jeden Task)

### Step A — Understand & Plan
- Ziel in 1–2 Sätzen restaten.
- Annahmen auflisten (falls nötig).
- Kurzen Implementierungsplan erstellen (3–8 Punkte).
- Todo-Liste sichtbar machen.

### Step B — Implement
- Änderungen in **kleinen, reviewbaren Chunks**.
- Klare Namen, konsistente Patterns, minimale Duplikation.
- Frontend und Backend getrennt denken.
- Kommentare nur wo Logik nicht selbsterklärend ist.

### Step C — Verify
- Befehle zum Ausführen liefern: Tests, Lint, Format, Build.
- Testfälle für:
  - Happy Path
  - Edge Cases
  - Failure Modes
  - Security-sensitive Inputs

### Step D — Review Checklist
Vor dem Commit intern prüfen:
- **Performance:** Ist die Lösung effizient genug, nicht verschwenderisch?
- **Security:** Validierung, Auth, Secrets, Dependency-Sicherheit.
- **Observability:** Sinnvolle Logs/Metriken wo nötig.
- **UX/DevX:** Klare Fehlermeldungen, gute Doku, stabile Interfaces.

### Output Frame (Antwortformat)
Jede Antwort mit Code-Änderungen folgt diesem Format:

1. **Goal** — Kurzbeschreibung des Ziels (1–2 Sätze)
2. **Assumptions** — Nur wenn nötig
3. **Plan** — 3–8 Schritte
4. **Changes** — betroffene Dateien
5. **Diffs** — Änderungen (Edit/Write Tools)
6. **Run** — Format / Lint / Test / Build Commands
7. **Verify Checklist** — schnelle Prüfung
8. **Open Questions** — max. 3

---

## 3) Definition of Done (DoD)

Ein Task gilt erst als **done** wenn:

- ✅ Anforderungen erfüllt (oder Annahmen klar dokumentiert).
- ✅ Code baut und Tests laufen lokal durch.
- ✅ Neues Verhalten durch Tests abgedeckt.
- ✅ Keine Secrets committed; Config von Code getrennt.
- ✅ Logging/Error-Handling ist sinnvoll.
- ✅ Dokumentation aktuell (README, Kommentare, ADR wenn nötig).
- ✅ `.gitignore` korrekt (node_modules, __pycache__, .env, dist, etc.).

### Stop-the-line Rules

Ein Task darf **nicht** als done markiert werden wenn:

- ❌ Tests fehlschlagen
- ❌ Linting-Fehler vorhanden sind
- ❌ Build fehlschlägt
- ❌ Root Cause eines Bugs nicht verstanden wurde
- ❌ Fehler nur unterdrückt statt behoben werden
- ❌ Debugging-Logs im Code verbleiben

---

## 4) Code-Qualitäts-Standards

### Allgemein
- Funktionen klein und single-purpose halten.
- Pure Functions bevorzugen wo möglich.
- Globalen State vermeiden, es sei denn begründet.
- Typed Interfaces nutzen wo die Sprache es unterstützt (TypeScript, Python type hints, etc.).

### Error Handling
- Fail fast bei Programmer-Errors.
- Hilfreiche Meldungen bei User-Errors.
- Exceptions nicht schlucken.
- Kontext in Errors einbauen (Operation, sanitized Inputs).

### Logging
- Key State-Transitions und Failures loggen.
- **Niemals** Secrets, Tokens, Passwörter, API Keys, PII loggen.

### Configuration
- Config via Env-Vars oder Config-Files, nicht hardcoded.
- `.env.example` immer mitliefern wenn Env-Vars genutzt werden.
- `.env` **niemals** committen.

---

## 5) Security Baseline

- Alle externen Inputs validieren (HTTP, CLI Args, File Contents).
- Parameterized Queries für SQL — kein String-Concatenation.
- Path-Traversal-Schutz bei File-Operationen.
- Least Privilege für Tokens/Keys.
- Dependencies pinnen wo sinnvoll; keine unvertrauenswürdigen Scripts.
- Secure Defaults bevorzugen.

### Secrets & Supply Chain

- **Niemals committen:** `.env`, `*.pem`, `*.pfx`, `id_rsa*`, Tokens, API Keys
- Secret-Scanning verwenden wenn verfügbar (z.B. Gitea Secret Detection, gitleaks)
- Nur vertrauenswürdige Package-Registries nutzen (nuget.org, npmjs.com, pypi.org)
- Versions-Pinning bei kritischen Dependencies

---

## 6) Debugging — Systematisches Vorgehen

### Grundprinzipien
- **Logs zuerst, Annahmen zuletzt.** Erst alle verfügbaren Informationen sammeln, dann hypothetisieren.
- **Nie mehrere Dinge gleichzeitig ändern.** Eine Variable nach der anderen — sonst weiss man nicht was geholfen hat.
- **Reproduzieren before fixen.** Wenn der Bug nicht reproduzierbar ist, ist er nicht verstanden.

### Debugging-Prozess

**Schritt 1 — Symptom verstehen**
- Exakte Fehlermeldung lesen (nicht überfliegen)
- Stack Trace von unten nach oben: wo beginnt *eigener* Code?
- Wann tritt der Fehler auf? Immer? Nur unter bestimmten Bedingungen?

**Schritt 2 — Scope eingrenzen**
- Kleinste reproduzierbare Einheit isolieren (minimales Beispiel)
- Externe Dependencies als Ursache ausschließen
- "Hat früher funktioniert?" → **Bisect-Ansatz**: letzten funktionierenden Git-Stand finden (`git bisect`)

**Schritt 3 — Diff-Denken**
- Was hat sich seit dem letzten funktionierenden Stand geändert?
  - Dependencies (lockfile diff)
  - Config / Env-Vars
  - Code-Änderungen (`git log --oneline`, `git diff`)
  - Externe Services (API, DB, OS)

**Schritt 4 — Hypothese & Test**
- Eine Hypothese aufstellen, gezielt testen
- Logging/Debugging-Output hinzufügen (nachher wieder entfernen)
- Nie raten und hoffen — wenn unklar: mehr Infos sammeln

**Schritt 5 — Fix & Regression-Test**
- Root Cause fixen, nicht Symptome unterdrücken
- Regression-Test hinzufügen der genau diesen Bug abdeckt
- Debugging-Logs entfernen vor dem Commit

### Häufige Fallen
- `console.log` / `print` vergessen zu entfernen → Review Checklist nutzen
- Fix funktioniert lokal, nicht in Prod → Env-Vars, Config, Secrets prüfen
- "Geht jetzt irgendwie" ohne zu verstehen warum → **Nicht akzeptieren**

---

## 6b) Testing-Strategie

Immer bereitstellen:
- **Unit Tests** für logic-heavy Module.
- **Integration Tests** wo Boundaries existieren (DB, API, Filesystem).
- Kurzen **Testplan** in der Antwort: was testen, wie ausführen, erwartete Outputs.

Bei Bugfixes: **immer einen Regression-Test hinzufügen**.

---

## 7) Git & PR-Standards

### Commit Messages (Conventional Commits)
Format: `type(scope): summary`

Types: `feat`, `fix`, `refactor`, `test`, `docs`, `chore`, `perf`, `build`, `ci`

Beispiele:
```
feat(api): add pagination to list endpoint
fix(auth): prevent token reuse in refresh flow
chore(deps): update dependencies to latest
```

### PR-Beschreibungstemplate
```
## What / Why
<Was wurde geändert und warum>

## How
<Technischer Ansatz>

## Testing
<Was wurde getestet und wie>

## Risks & Mitigations
<Risiken und Gegenmaßnahmen>

## Rollback
<Wie wird die Änderung rückgängig gemacht wenn nötig>
```

---

## 8) Architekturentscheidungen (ADR-lite)

Bei Entscheidungen die Struktur oder Dependencies betreffen, kurze Notiz:

```
## ADR: <Titel>
Context:   <Warum war eine Entscheidung nötig>
Decision:  <Was wurde entschieden>
Alternatives: <Was wurde verworfen>
Consequences: <Auswirkungen>
```

5–15 Zeilen reichen.

---

## 9) Gitea Integration

### Neues Repo anlegen (Claude führt nach Bestätigung aus)
```bash
GITEA_TOKEN=$(cat ~/.claude/gitea_token)
curl -X POST "https://gitea.hack4.win/api/v1/user/repos" \
  -H "Authorization: token $GITEA_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "<repo-name>",
    "description": "<beschreibung>",
    "private": false,
    "auto_init": false,
    "default_branch": "main"
  }'
```

### Lokales Repo zu Gitea pushen
```bash
git remote add origin https://gitea.hack4.win/mundi/<repo-name>.git
git branch -M main
git push -u origin main   # nur nach expliziter Bestätigung
```

### Commit-Workflow
```bash
git add <spezifische-files>    # nie blindes "git add -A" ohne Review
git commit -m "feat(scope): summary"
git push origin main           # nur nach expliziter Bestätigung
```

---

## 10) Output-Format

Bei Code-Änderungen immer:
- Liste der geänderten Dateien.
- Diffs oder vollständige Dateiinhalte wenn nötig.
- Ausführbefehle.
- Kurze Verifikations-Checkliste.

Bei Scripts:
- Safety Checks einbauen (`set -euo pipefail` für Bash).
- Keine destructiven Operationen ohne expliziten Request.
- Dry-Run-Modus als Default wo möglich.

---

## 11) Repo Context (pro Projekt ausfüllen)

### Stack
- Language(s):
- Frameworks:
- Build-Tooling:
- Package Manager:
- Runtime/Target OS:

### Commands
```bash
# Install:
# Format:
# Lint:
# Test:
# Build:
# Run (dev):
# Run (prod):
```

### Environments
- Dev:
- Staging:
- Prod:

### Quality Gates
- Coverage Target:
- Lint Rules:
- Security Scanning:

---

## 11b) Stack Presets (bekannte Stacks — Commands sofort verfügbar)

### C# / .NET
```bash
dotnet format                    # Format
dotnet build -c Release          # Build
dotnet test                      # Test
dotnet run                       # Run (dev)
dotnet publish -c Release        # Publish
```

### ASP.NET
```bash
dotnet watch run                 # Dev-Server mit Hot Reload
dotnet user-secrets set <key> <value>   # Secrets (nie in .env committen)
```

### C++
```bash
cmake -B build -DCMAKE_BUILD_TYPE=Release   # Configure
cmake --build build                          # Build
clang-format -i **/*.cpp **/*.h              # Format
clang-tidy **/*.cpp                          # Lint (optional)
ctest --test-dir build                       # Test
```

### PowerShell
```powershell
Invoke-ScriptAnalyzer -Path .    # Lint
Invoke-Pester                    # Test
```

---

## 12) Claude Code Tool-Verhalten

Klare Regeln wann welches Tool eingesetzt wird — für konsistentes Verhalten über alle Sessions:

| Situation | Tool | Wann |
|---|---|---|
| Nicht-trivialer Task | **EnterPlanMode** | >2 Dateien betroffen, neue Features, Architekturentscheidungen, unklare Anforderungen |
| Multi-Step Task | **TodoWrite** | Bei ≥3 Schritten — sofort zu Beginn erstellen, jeden Schritt nach Abschluss markieren |
| Codebase erkunden | **Explore-Agent** | Bei breiter Recherche über viele Dateien — nicht selbst mit Grep/Glob suchen |
| Parallele Recherche | **Subagents** | Mehrere unabhängige Recherche-Fragen gleichzeitig |
| Stabile Erkenntnisse | **Memory** | User-Präferenzen, Patterns, Projekt-Konventionen nach Gespräch festhalten |

### Konkrete Regeln

**Plan Mode:**
- Immer betreten bei: neuen Features, Refactoring, Multi-File-Änderungen, unklaren Anforderungen
- Nicht betreten bei: Typo-Fixes, Single-Line-Changes, reine Recherche-Tasks
- Im Plan Mode: nur lesen, nicht schreiben (außer Plan-File)

**Todo-Liste:**
- Erstellen: sofort wenn ≥3 Schritte erkennbar
- Aktualisieren: Task auf `in_progress` setzen *bevor* begonnen wird
- Abschließen: sofort nach Fertigstellung markieren, nicht batchweise
- Niemals einen Task als `completed` markieren wenn Tests fehlschlagen

**Vor dem Antworten prüfen:**
- Wurde die Datei gelesen bevor sie bearbeitet wird? (Read first, then Edit)
- Wird eine spezifische Datei gesucht? → Glob/Grep direkt, kein Agent
- Ist die Suche offen-ended? → Explore-Agent

---

## 13) Projektnotizen

<!-- Hier projektspezifische Infos: API-Endpunkte, Deploy-Prozess, bekannte Eigenheiten, etc. -->
