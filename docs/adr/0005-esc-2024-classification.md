# ADR 0005: Blutdruck-Einordnung nach ESC 2024 statt ESC/ESH 2018

- **Status:** akzeptiert (Auftrag des Product Owners am 2026-11-05)
- **Kontext:** Bisher wurden Messungen nach ESC/ESH 2018 in sechs Kategorien eingeordnet (Optimal, Normal, Hochnormal, Grad 1–3). Die ESC-Leitlinien 2024 ersetzen das durch drei Kategorien und führen die Kategorie „erhöhter Blutdruck“ ein.
- **Entscheidung:**
  1. Kategorien: `NonElevated` (< 120 und < 70 mmHg), `Elevated` (120–139 und/oder 70–89), `Hypertension` (≥ 140 und/oder ≥ 90). Es gilt die höhere Kategorie aus Systolisch und Diastolisch.
  2. Der Hinweis bei kritischen Werten hängt nicht mehr an einer Kategorie, sondern an `IsSevere` (≥ 180 systolisch oder ≥ 110 diastolisch). Das entspricht dem bisherigen Auslöser („Grad 3“), die Hinweise bleiben also gleich häufig.
  3. Die isolierte systolische Hypertonie bleibt ein Zusatzmerkmal.
  4. Bestehende Hinweise (Kategorie `Grade1`–`Grade3`, in der Praxis nur `Grade3`) werden per Datenmigration `UpdateCategoriesToEsc2024` auf `Hypertension` und den neuen Text umgestellt.
- **Folgen:**
  - **Breaking Change der API-Werte:** Das Feld `category` (und die Spalte `Category` im CSV-Export) liefert jetzt `NonElevated`/`Elevated`/`Hypertension` statt `Optimal`…`Grade3`. Externe Clients müssen angepasst werden; die Pfade (`/api/v1`) bleiben unverändert. Web-UI und MAUI-App sind angepasst.
  - Viele Messwerte, die bisher „Optimal“ oder „Normal“ waren, sind jetzt „Erhöht“ (diastolisch ab 70, systolisch ab 120). Das ist beabsichtigt, kann aber Nutzer überraschen; die Texte nennen die Einordnung ausdrücklich keine Diagnose.
- **Quellenlage (geprüft am 2026-11-05 gegen den Leitlinientext, Eur Heart J 2024;45:3912, doi 10.1093/eurheartj/ehae178, Abschnitt 6.1 und 5.3.2/7.2):**
  - **Bestätigt:** Nicht erhöht = systolisch < 120 **und** diastolisch < 70 mmHg; erhöht = systolisch 120–139 **oder** diastolisch 70–89 mmHg; Bluthochdruck = bestätigt systolisch ≥ 140 **oder** diastolisch ≥ 90 mmHg. Die Grenzen im Code (`BloodPressureClassifier`) stimmen damit überein.
  - **Hinweisgrenze 180/110:** Die Leitlinie nennt „BP of ≥ 180/110 mmHg“ als Anlass, einen hypertensiven Notfall abzuklären. `IsSevere` (≥ 180 systolisch oder ≥ 110 diastolisch) entspricht dieser Grenze. Die Grenze 180/120, die in Fachartikeln (ACC/AHA-Sprache, „hypertensive Krise“) vorkommt, ist nicht die der ESC-Leitlinie.
  - **Nicht ausdrücklich geregelt:** Eine Regel „es gilt die höhere Kategorie“ steht im gelesenen Text nicht; sie folgt aus dem „oder“ der Definitionen (erreicht ein Wert die Grenze, gilt die Kategorie). Die isolierte systolische Hypertonie (Abschnitt 9.4.1) wurde nicht gelesen; die Definition systolisch ≥ 140 bei diastolisch < 90 ist die übliche und bleibt ein reines Zusatzmerkmal.
  - **Einschränkungen:** Die Leitlinie gilt für standardisierte Praxismessungen; Messungen zu Hause können abweichen (Praxiswerte liegen im Mittel 5–10 mmHg höher). Die App ordnet Einzelwerte ein und stellt keine Diagnose; die Diagnose braucht bestätigte, wiederholte Messungen. Gelesen wurde die Online-Fassung des Leitlinientexts über Suchtreffer und Auszüge, nicht das gedruckte PDF mit Abbildung 6.
  - Die Leitlinie 2024 ist eine ESC-Leitlinie; die Bezeichnung „ESC/ESH 2024“ im Auftrag wurde daher als „ESC 2024“ umgesetzt.
- **Alternativen:** ESH-2023-Leitlinie (behält die Grade 1–3); beide Schemata parallel anbieten (mehr Aufwand, unklarer Nutzen).
