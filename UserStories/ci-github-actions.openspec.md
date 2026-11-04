# Change: CI-Pipeline mit GitHub Actions (Build & Tests)

> **Status:** in-progress
> Ablage: `UserStories/ci-github-actions.openspec.md` (nach Abschluss: `UserStories/archive/YYYY-MM-DD-ci-github-actions.openspec.md`)

## 1. Proposal (Why & What)
- **Why:** Seit dem Entfernen von `.github/workflows` läuft keine automatische Prüfung mehr. `CLAUDE.md` verlangt aber, dass jeder PR passing Tests (CI/CD) hat. Ohne Pipeline kann ein defekter Stand unbemerkt in `main` gelangen.
- **What Changes:**
  - Neuer Workflow `.github/workflows/ci.yml`, der bei jedem Pull Request gegen `main` und bei jedem Push auf `main` das Backend baut und die TUnit-Tests ausführt.
  - Neue Solution-Filterdatei `CleanArchitecture.Backend.slnf`, die nur die Backend-Projekte und das Testprojekt enthält.
  - Testergebnisse werden als Artefakt hochgeladen.
  - NuGet-Schwachstellenprüfung als Teil der Pipeline.
- **Impact:** DevOps/CI, Repository-Struktur. Kein Eingriff in Anwendungscode. **Nicht** Teil dieser Change: Build von Blazor-UI und MAUI (benötigen Workloads, UI liegt noch auf .NET 8), Deployment/CD, Docker-Image-Build, Code-Review-Automatisierung.

## 2. User Stories
- Als Entwickler möchte ich, dass jeder PR automatisch gebaut und getestet wird, damit fehlerhafter Code nicht in `main` gelangt.
- Als Reviewer möchte ich im PR sehen, ob Build und Tests bestanden haben, damit ich nicht lokal nachprüfen muss.
- Als Betreiber möchte ich bei bekannten Schwachstellen in NuGet-Paketen gewarnt werden, damit Sicherheitslücken nicht unbemerkt bleiben.

## 3. Requirements (Delta)

### ADDED Requirements

#### Requirement: Pipeline läuft bei PR und Push auf main
Das System SHALL den Workflow `ci.yml` bei jedem `pull_request` gegen `main` und bei jedem `push` auf `main` starten. Parallele Läufe desselben Branches SHALL abgebrochen werden, wenn ein neuerer Lauf startet.

##### Scenario: Pull Request geöffnet
- **GIVEN** ein Feature-Branch mit Änderungen
- **WHEN** ein Pull Request gegen `main` geöffnet oder aktualisiert wird
- **THEN** startet der Workflow `CI` und erscheint als Check am PR

##### Scenario: Neuer Push während eines Laufs
- **GIVEN** ein laufender CI-Lauf für einen Branch
- **WHEN** ein weiterer Commit auf denselben Branch gepusht wird
- **THEN** wird der ältere Lauf abgebrochen und nur der neue ausgeführt

#### Requirement: Build und Tests des Backends
Das System SHALL mit .NET 10 die Backend-Projekte (Domain, Application, Contracts, Infrastructure, API, ServiceDefaults, AppHost) restoren und im Release-Modus bauen und danach alle Tests aus `Tests/` ausführen. Der Lauf SHALL fehlschlagen, wenn der Build Fehler hat oder ein Test fehlschlägt.

##### Scenario: Alles grün
- **GIVEN** ein Stand ohne Build- und Testfehler
- **WHEN** die Pipeline läuft
- **THEN** endet der Job mit Erfolg

##### Scenario: Fehlschlagender Test
- **GIVEN** ein Commit, der einen Test bricht
- **WHEN** die Pipeline läuft
- **THEN** endet der Job mit Fehler und der PR zeigt den Check als fehlgeschlagen

##### Scenario: Kein Workload erforderlich
- **GIVEN** ein frischer Runner ohne MAUI-/Blazor-Workloads
- **WHEN** die Pipeline läuft
- **THEN** werden nur die Projekte der Solution-Filterdatei gebaut und der Lauf scheitert nicht an fehlenden Workloads

#### Requirement: Testergebnisse sind abrufbar
Das System SHALL die Testergebnisse (TRX/HTML-Report aus `TestResults/`) als Workflow-Artefakt hochladen, auch wenn Tests fehlschlagen.

##### Scenario: Test schlägt fehl
- **GIVEN** ein fehlschlagender Test
- **WHEN** der Lauf endet
- **THEN** ist das Artefakt `test-results` am Lauf herunterladbar

#### Requirement: Schwachstellenprüfung für NuGet-Pakete
Das System SHALL `dotnet list package --vulnerable --include-transitive` für die Backend-Projekte ausführen und den Lauf fehlschlagen lassen, wenn Pakete mit Schweregrad **High** oder **Critical** gefunden werden. Schwachstellen mit Schweregrad Moderate SHALL nur ausgegeben werden.

##### Scenario: Paket mit hoher Schwachstelle
- **GIVEN** eine direkte oder transitive Abhängigkeit mit High-Schwachstelle
- **WHEN** die Pipeline läuft
- **THEN** schlägt der Schritt fehl und nennt Paket und Advisory

##### Scenario: Nur moderate Schwachstelle
- **GIVEN** ausschließlich Pakete mit Moderate-Schwachstelle (aktuell `MessagePack` im AppHost)
- **WHEN** die Pipeline läuft
- **THEN** wird sie ausgegeben, der Lauf bleibt grün

#### Requirement: Sichere Workflow-Konfiguration
Das System SHALL dem Workflow nur die Berechtigung `contents: read` geben, ohne Secrets auszukommen und Actions auf feste Hauptversionen pinnen.

##### Scenario: Fork-PR
- **GIVEN** ein PR aus einem Fork
- **WHEN** der Workflow läuft
- **THEN** hat er keinen Schreibzugriff und benötigt keine Secrets

### MODIFIED Requirements
_Entfällt._

### REMOVED Requirements
_Entfällt._ (Die früheren Workflows wurden bewusst gelöscht und werden nicht wiederhergestellt.)

## 4. UI/UX Requirements
Entfällt.

## 5. API & Backend Requirements
- **Authentifizierung/Autorisierung:** nicht betroffen.
- **Endpoints/Modelle:** keine Änderung.
- **Repository-Dateien:**
  - `.github/workflows/ci.yml`
  - `CleanArchitecture.Backend.slnf` (enthält Domain, Application, Contracts, Infrastructure, Presentation.API, ServiceDefaults, AppHost, Tests/CleanArchitecture.Tests)

## 6. KI/AI Requirements
Entfällt.

## 7. Design (Technischer Ansatz)
- **Runner:** `ubuntu-latest`, `actions/checkout`, `actions/setup-dotnet` mit `dotnet-version: 10.0.x` und NuGet-Cache über `cache: true` (Lockfile-Alternative: `packages.lock.json` ist nicht vorhanden, daher Cache-Key über `**/*.csproj`).
- **Schritte:** Restore → Build (`--configuration Release --no-restore`) → Test (`dotnet test` auf das Testprojekt; Runner `Microsoft.Testing.Platform` ist über `global.json` gesetzt) → Schwachstellenprüfung → Artefakt-Upload (`if: always()`).
- **Solution-Filter statt Gesamt-Solution:** `CleanArchitecture.sln` enthält Blazor-WASM (net8) und MAUI; deren Build braucht Workloads und ist nicht Ziel dieser Change.
- **Integrationstests:** laufen ohne Datenbank (Fake-Repository, `WebApplicationFactory`), daher keine Postgres-Service-Container nötig.
- **Schwachstellen-Gate:** `dotnet list package --vulnerable` liefert immer Exitcode 0; das Gate wertet die Ausgabe nach Schweregrad aus (Skriptschritt).
- **Branch-Schutz:** Damit der Check einen Merge wirklich blockiert, muss in den GitHub-Repo-Einstellungen eine Branch-Protection-Regel mit dem erforderlichen Check `CI` für `main` angelegt werden. Das ist eine Admin-Aktion des Repo-Eigentümers und nicht Teil des Codes.
- **Alternativen:** Pipeline über .NET Aspire/`dotnet run` (zu schwer), Gesamt-Solution mit Workload-Installation (langsam, UI noch .NET 8). Kein ADR nötig.
- **Risiken:** Die Schwachstellenprüfung kann PRs durch neu veröffentlichte Advisories ohne Codeänderung brechen; die Ausnahme per Pin auf eine gepatchte Version ist der vorgesehene Weg.

## 8. Tasks (Implementierungs-Checkliste)
- [x] 1. Workflow-Datei als Testfall prüfen: lokale Befehlsfolge (Restore, Build Release, Test, Vulnerable-Check) läuft auf dem Backend-Filter grün durch (Red/Green der Befehlskette)
- [x] 2. `CleanArchitecture.Backend.slnf` anlegen
- [x] 3. `.github/workflows/ci.yml` schreiben (devops-agent)
- [ ] 4. Pipeline am PR verifizieren: grüner Lauf und absichtlich roter Lauf (qa-agent)
- [ ] 5. Branch-Protection mit erforderlichem Check `CI` einrichten (Repo-Eigentümer)
- [ ] 6. README um CI-Hinweis ergänzen (doc-agent)

## 9. Acceptance Criteria (DoD)
- [ ] Jedes Scenario aus Abschnitt 3 ist durch einen Workflow-Lauf nachgewiesen (grün, rot, abgebrochen, Artefakt vorhanden).
- [ ] Der Workflow läuft ohne Secrets und mit `contents: read`.
- [ ] Lokal und in der Pipeline ergibt `dotnet test` dasselbe Ergebnis (15 Tests grün).
- [ ] Der Check `CI` ist am PR sichtbar.
- [ ] Spec ist archiviert (Status `archived`, Requirements nach `UserStories/specs/ci.openspec.md` übernommen).
