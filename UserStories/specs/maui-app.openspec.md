# Capability: MAUI-App (Anbindung an die API)

> Dauerhafte Capability-Spec. Quelle: archivierte Änderung `UserStories/archive/2026-11-05-maui-app-api-anbindung.openspec.md`.

## Requirements

#### Requirement: Anmeldung und sichere Token-Ablage
Das System SHALL Anmeldung und Registrierung über `IAuthApi` anbieten, Tokens ausschließlich im plattformeigenen sicheren Speicher (`SecureStorage`: Keychain/Keystore) ablegen, abgelaufene Access-Token per Refresh-Token erneuern und bei fehlgeschlagener Erneuerung auf die Anmeldung zurückführen.

##### Scenario: Erfolgreiche Anmeldung
- **GIVEN** gültige Zugangsdaten
- **WHEN** der Nutzer „Anmelden“ wählt
- **THEN** werden Tokens sicher gespeichert und die Messwerte-Seite erscheint

##### Scenario: Falsche Zugangsdaten
- **GIVEN** falsches Passwort
- **WHEN** der Nutzer anmeldet
- **THEN** zeigt die App eine lokalisierte Fehlermeldung ohne technische Details und speichert nichts

##### Scenario: App-Neustart
- **GIVEN** gespeicherte Tokens
- **WHEN** die App startet
- **THEN** ist der Nutzer angemeldet (nach Bedarf mit Token-Erneuerung)

##### Scenario: Abmelden
- **GIVEN** angemeldeter Nutzer
- **WHEN** er sich abmeldet
- **THEN** sind alle gespeicherten Tokens gelöscht und die Anmeldeseite erscheint

#### Requirement: Messwerte
Das System SHALL Messungen paginiert (neueste zuerst) mit Kategorie als Text und Farbmarkierung anzeigen, das Anlegen mit clientseitiger und serverseitiger Validierung (Feldmeldungen aus `ValidationProblemDetails`) und das Löschen mit Bestätigung und `If-Match` (ETag) erlauben; bei 401 wird zur Anmeldung zurückgeleitet, bei 412/409 erscheint ein Konflikthinweis.

##### Scenario: Liste und Nachladen
- **GIVEN** mehr als eine Seite Messungen
- **WHEN** der Nutzer ans Listenende scrollt
- **THEN** wird die nächste Seite nachgeladen

##### Scenario: Validierungsfehler
- **GIVEN** Systolisch 999
- **WHEN** gespeichert wird
- **THEN** erscheint die Feldmeldung der API am Feld und der Fokus bzw. die Screenreader-Ansage geht auf den Fehler

##### Scenario: Löschen mit Konflikt
- **GIVEN** eine zwischenzeitlich geänderte Messung
- **WHEN** der Nutzer löscht
- **THEN** zeigt die App den Konflikthinweis und lädt die Liste neu

#### Requirement: Auswertung
Das System SHALL Kennzahlen und Verlauf (Durchschnitt Systolisch/Diastolisch/Puls) je Tag, Woche, Monat für einen Zeitraum anzeigen; Diagramm mit zusätzlicher Wertetabelle als Alternative für Screenreader.

##### Scenario: Intervall wechseln
- **GIVEN** die Auswertung
- **WHEN** der Nutzer „Woche“ wählt
- **THEN** wird `interval=week` angefragt und die Anzeige aktualisiert

##### Scenario: Keine Daten
- **GIVEN** keine Messung im Zeitraum
- **WHEN** die Seite lädt
- **THEN** erscheint ein erklärender Leerzustand

#### Requirement: Export
Das System SHALL CSV oder PDF für einen Zeitraum laden und über den Teilen-Dialog des Betriebssystems bzw. in einen vom Nutzer gewählten Speicherort ausgeben; bei 400 (zu viele Messungen) erscheint eine lokalisierte Meldung.

##### Scenario: PDF teilen
- **GIVEN** Format PDF
- **WHEN** der Nutzer „Exportieren“ wählt
- **THEN** wird die Datei temporär gespeichert und der Teilen-Dialog geöffnet; die Temp-Datei wird danach entfernt

#### Requirement: Erinnerungen und Hinweise
Das System SHALL Erinnerungen (Zeitzone, bis zu 5 Uhrzeiten) bearbeiten und speichern, Hinweise anzeigen, als gelesen markieren und einen Ungelesen-Zähler am Tab/Menü zeigen. Warnhinweise enthalten den Hinweis „keine ärztliche Beratung“.

##### Scenario: Zeitzone vorschlagen
- **GIVEN** keine gespeicherten Erinnerungen
- **WHEN** die Seite öffnet
- **THEN** ist die Zeitzone des Geräts (`TimeZoneInfo.Local.Id`) vorbelegt

##### Scenario: Hinweis als gelesen markieren
- **GIVEN** ein ungelesener Hinweis
- **WHEN** der Nutzer ihn markiert
- **THEN** wird die API aufgerufen, der Zähler sinkt und die Änderung wird per `SemanticScreenReader.Announce` angesagt

#### Requirement: Konfigurierbare API-Adresse
Das System SHALL die API-Basisadresse aus der Konfiguration beziehen (nie fest im Code), je Build-Konfiguration überschreibbar (Debug: lokale Adresse, Android-Emulator `10.0.2.2`), Klartext-HTTP nur in Debug-Builds erlauben.

##### Scenario: Release-Build
- **GIVEN** ein Release-Build
- **WHEN** die App eine `http://`-Adresse konfiguriert bekäme
- **THEN** wird der Start mit einer klaren Konfigurationsfehlermeldung abgebrochen statt Klartext zu nutzen

#### Requirement: i18n und Barrierefreiheit
Das System SHALL alle sichtbaren Texte über `.resx` (DE/EN, Gerätesprache) ausgeben, jedem Bedienelement `SemanticProperties` (Description/Hint, Überschriften-Ebenen) geben, Mindestzielgröße 44 × 44 pt einhalten, Farben nie als einzigen Informationsträger nutzen und Dynamic Type/Schriftvergrößerung ohne Abschneiden unterstützen.

##### Scenario: Englisches Gerät
- **GIVEN** Gerätesprache Englisch
- **WHEN** eine Seite erscheint
- **THEN** sind alle Texte englisch; ein Test prüft DE/EN-Schlüsselparität
