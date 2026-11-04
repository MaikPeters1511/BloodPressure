# Epic 8: Continuous Delivery (Container-Image)

> **Status:** proposed · **Priorität:** 4 · **Abhängigkeiten:** CI-Change (`ci-github-actions`) · **Agenten:** devops-agent, sec-agent
> Ablage: `UserStories/epics/epic-08-cd-deployment.openspec.md`

## 1. Proposal (Why & What)
- **Why:** Die CI prüft den Code, liefert aber kein auslieferbares Artefakt.
- **What Changes:** Bei Push auf `main` baut ein Workflow das Docker-Image der API und veröffentlicht es in der GitHub Container Registry (GHCR); ein Schwachstellenscan prüft das Image.
- **Impact:** `.github/workflows/cd.yml`, Dockerfile, Repository-Einstellungen (Packages). **Nicht** Teil: konkretes Hosting (Zielumgebung offen).

## 2. User Stories
- Als Betreiber möchte ich nach jedem Merge ein getaggtes Image, damit ich es ausrollen kann.
- Als Betreiber möchte ich Images mit bekannten schweren Lücken nicht veröffentlichen.

## 3. Requirements (Delta)

### ADDED Requirements

#### Requirement: Image bauen und veröffentlichen
Das System SHALL nach erfolgreichem CI-Lauf auf `main` das Image aus `CleanArchitecture.Presentation.API/Dockerfile` (Build-Kontext Repository-Wurzel) bauen und als `ghcr.io/<owner>/bloodpressure-api` mit den Tags `sha-<commit>` und `latest` veröffentlichen.

##### Scenario: Merge auf main
- **GIVEN** ein grüner CI-Lauf auf `main`
- **WHEN** der CD-Workflow läuft
- **THEN** existiert das Image mit dem Tag des Commits in GHCR

##### Scenario: CI rot
- **GIVEN** ein roter CI-Lauf
- **WHEN** der CD-Workflow ausgelöst würde
- **THEN** wird kein Image veröffentlicht

#### Requirement: Image-Scan
Das System SHALL das Image vor dem Veröffentlichen mit Trivy auf Schwachstellen der Schwere HIGH/CRITICAL prüfen und bei Fund (mit verfügbarem Fix) abbrechen.

##### Scenario: Kritische Lücke
- **GIVEN** ein Basis-Image mit behebbarer CRITICAL-Lücke
- **WHEN** der Scan läuft
- **THEN** bricht der Workflow ab und veröffentlicht nichts

#### Requirement: Minimale Rechte
Das System SHALL `packages: write` nur dem Veröffentlichungs-Job geben, alle anderen Jobs `contents: read`; Anmeldung an GHCR nur über `GITHUB_TOKEN`.

##### Scenario: Rechteprüfung
- **GIVEN** die Workflow-Datei
- **WHEN** sie geprüft wird
- **THEN** besitzt kein Job mehr Rechte als benötigt und es werden keine zusätzlichen Secrets verwendet

### MODIFIED Requirements
_Entfällt._ (Das Dockerfile nutzt bereits .NET 10; Anpassung nur falls der Build es verlangt.)

### REMOVED Requirements
_Entfällt._

## 4. Design
- `docker/build-push-action` mit Layer-Cache (`type=gha`); Basis-Image `mcr.microsoft.com/dotnet/aspnet:10.0` als Nicht-Root (`USER app` ist gesetzt).
- Trigger `workflow_run` der CI oder `needs`-Job im selben Workflow – Entscheidung: gleicher Workflow-Datei-Trigger `push` auf `main` mit eigenem Job nach Test, damit „CI grün“ garantiert ist.
- Konfiguration (Connection String, Cors) nur per Umgebungsvariablen zur Laufzeit.
- **Offene Frage:** Zielumgebung (VPS mit Docker Compose, Azure Container Apps, Kubernetes) – bestimmt Folge-Epic.

## 5. Tasks
- [ ] T8.1 (devops) `cd.yml` schreiben (Build, Scan, Push)
- [ ] T8.2 (sec) Rechte und Scan-Policy prüfen; Entscheidung zu Health-Endpunkten in Produktion
- [ ] T8.3 (qa) Image lokal starten (`docker run`) und `/health` prüfen
- [ ] T8.4 (doc) README: Image-Nutzung und benötigte Umgebungsvariablen

## 6. Acceptance Criteria (DoD)
- [ ] Alle Scenarios an echten Workflow-Läufen nachgewiesen.
- [ ] Container startet mit gesetzten Umgebungsvariablen und ist erreichbar.
- [ ] Health-Endpunkt: `MapDefaultEndpoints` in den ServiceDefaults mappt `/health` und `/alive` nur in Development. Für Container-Healthchecks in Produktion ist eine bewusste Entscheidung nötig (z. B. nur `/alive` freigeben, nicht öffentlich erreichbar); sie wird in T8.2 durch `sec-agent` getroffen.
