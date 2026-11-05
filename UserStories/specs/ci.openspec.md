# Capability: Continuous Integration (GitHub Actions)

> Dauerhafte Capability-Spec. Quelle: archivierte Änderung `UserStories/archive/2026-11-05-ci-github-actions.openspec.md`.

## Requirements

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
