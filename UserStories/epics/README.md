# Backlog: Epics für das Backend

Jede Epic ist eine OpenSpec-Change-Spec (Status `proposed`) mit User Stories, SHALL-Requirements, Scenarios und einer Task-Liste. Tasks sind mit dem zuständigen Agenten gekennzeichnet (`T<Epic>.<Nr>`). Ablauf je Epic: propose → apply → archive (siehe `po-agent`).

| Epic | Titel | Priorität | Abhängig von | Datei |
|---|---|---|---|---|
| 1 | Authentifizierung und Benutzerbezug | 1 | – | [epic-01](epic-01-auth-benutzerbezug.openspec.md) |
| 2 | Abfrage mit Paging, Filter, Sortierung | 2 | (1) | [epic-02](epic-02-abfrage-paging-filter.openspec.md) |
| 3 | Validierung und Fehlerantworten | 2 | – | [epic-03](epic-03-validierung-fehler.openspec.md) |
| 7 | Qualität und Robustheit (Postgres-Tests u. a.) | 2 | – | [epic-07](epic-07-qualitaet-betrieb.openspec.md) |
| 4 | Klassifikation und Auswertung | 3 | 1, 2 | [epic-04](epic-04-klassifikation-auswertung.openspec.md) |
| 5 | Export für den Arzt | 4 | 1, 2, 4 | [epic-05](epic-05-export.openspec.md) |
| 8 | Continuous Delivery (Container-Image) | 4 | CI-Change | [epic-08](epic-08-cd-deployment.openspec.md) |
| 6 | Erinnerungen und Warnungen | 5 | 1, 4 | [epic-06](epic-06-erinnerungen-warnungen.openspec.md) |
| 10 | Geräteimport (Apple Health, Health Connect, Samsung Health) | 5 | 1, 3 | [epic-10](epic-10-geraeteimport.openspec.md) |
| 9 | KI-Zusammenfassung | 6 | 1, 4 | [epic-09](epic-09-ki-zusammenfassung.openspec.md) |

## Empfohlene Reihenfolge
1. **Epic 1** (Grundlage und Datenschutz), parallel **Epic 3** und der Postgres-Teil von **Epic 7** (unabhängig).
2. **Epic 2**, dann **Epic 4**.
3. **Epic 10** (Spike T10.1 kann sofort starten, weil Store-Freigaben lange dauern) sowie **Epic 5**, **Epic 8**.
4. **Epic 6**, zuletzt **Epic 9**.

## Übergreifende Hinweise
- **Breaking Changes** sind in den Epics mit **BREAKING** markiert (Epic 1 Auth, Epic 2 Listenformat, Epic 10 optionale Felder). Versionierung aus Epic 7 sollte vorher stehen, wenn Frontends bereits ausgeliefert sind.
- **Migrationen:** Epic 1, 2, 6, 9, 10 ändern das Schema; immer neue Migration, bestehende nie editieren.
- **Frontend (Blazor/MAUI)** ist in diesen Specs nur dort enthalten, wo es nötig ist (Epic 10). Weitere UI-Anpassungen sind eigene Specs für den `frontend-agent`.
- **Medizinische Inhalte** (Epic 4, 6, 9) sind keine Diagnose; Schwellenwerte vor der Umsetzung durch eine Fachquelle bestätigen.
