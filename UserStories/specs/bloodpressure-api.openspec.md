# Capability: Typisierter Refit-Client der BloodPressure-API

> Dauerhafte Capability-Spec. Quelle: archivierte Änderung `UserStories/archive/2026-11-05-refit-bloodpressure-client.openspec.md`.

## Requirements

#### Requirement: Refit-Interface deckt alle BloodPressure-Endpunkte ab
Das System SHALL ein Interface `IBloodPressureApi` bereitstellen, das `GET /api/BloodPressure`, `GET /api/BloodPressure/{id}`, `POST`, `PUT /{id}` und `DELETE /{id}` mit `CancellationToken` abbildet und `BloodPressureDto` bzw. `BloodPressureRequest` verwendet.

##### Scenario: Alle Messungen abrufen
- **GIVEN** die API enthält eine gespeicherte Messung
- **WHEN** `GetAllAsync` aufgerufen wird
- **THEN** wird eine Liste mit dieser Messung als `BloodPressureDto` zurückgegeben

##### Scenario: Messung anlegen
- **GIVEN** eine gültige `BloodPressureRequest`
- **WHEN** `CreateAsync` aufgerufen wird
- **THEN** liefert die API Status 201 und der Client gibt das `BloodPressureDto` mit vergebener Id zurück

##### Scenario: Messung ändern und löschen
- **GIVEN** eine vorhandene Messung
- **WHEN** `UpdateAsync` bzw. `DeleteAsync` mit ihrer Id aufgerufen wird
- **THEN** antwortet die API mit 204 und der Client wirft keine Ausnahme

#### Requirement: Fehlerantworten sind für den Aufrufer auswertbar
Das System SHALL Fehlerantworten der API (Status 400 mit ProblemDetails, 404) als `ApiException` des Clients bereitstellen, sodass Status und ProblemDetails ohne Stacktrace-Informationen auswertbar sind.

##### Scenario: Ungültiger Wert
- **GIVEN** eine Request mit `Systolisch = 999`
- **WHEN** `CreateAsync` aufgerufen wird
- **THEN** wirft der Client eine `ApiException` mit Status 400 und dem ProblemDetails-Titel „Ungültige Eingabe“

##### Scenario: Unbekannte Id
- **GIVEN** keine Messung mit Id 99
- **WHEN** `GetByIdAsync(99)` aufgerufen wird
- **THEN** wirft der Client eine `ApiException` mit Status 404

#### Requirement: Client-Registrierung per Extension-Methode
Das System SHALL `AddBloodPressureApiClient(this IServiceCollection, Uri baseAddress)` bereitstellen, die `IBloodPressureApi` über `AddRefitClient` registriert. Die Basisadresse MUST aus der Konfiguration stammen (unter Aspire `https+http://api` per Service-Discovery) und DARF NICHT im Code hartcodiert sein. Der Client SHALL den Standard-Resilience-Handler verwenden.

##### Scenario: Auflösung per DI
- **GIVEN** ein ServiceProvider mit `AddBloodPressureApiClient`
- **WHEN** `IBloodPressureApi` aufgelöst wird
- **THEN** wird eine Instanz geliefert, deren `HttpClient.BaseAddress` der konfigurierten Adresse entspricht

#### Requirement: JSON-Vertrag bleibt stabil
Das System SHALL die JSON-Serialisierung (camelCase, `dateTime` als ISO-8601 UTC) unverändert lassen, sodass bestehende Konsumenten weiter funktionieren.

##### Scenario: Round-Trip
- **GIVEN** ein `BloodPressureDto` mit bekannten Werten
- **WHEN** es vom Client gelesen wird, nachdem die API es geliefert hat
- **THEN** sind alle Felder (`id`, `systolisch`, `diastolisch`, `dateTime`, `pulse`, `oxygen`) gleich den gespeicherten Werten
