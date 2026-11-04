# ADR 0003: Erinnerungen als BackgroundService, Warnungen transaktional

- **Status:** akzeptiert
- **Kontext:** Epic 6 verlangt Warnungen bei kritischen Werten und tägliche Messerinnerungen, ohne dass Hinweise verloren gehen.
- **Entscheidung:**
  - **Kritische Werte:** Die Benachrichtigung wird in derselben Datenbanktransaktion wie die Messung gespeichert (ein `SaveChanges`). Es gibt kein asynchrones Domain Event und damit keinen Zeitraum, in dem eine Messung ohne ihren Hinweis existiert.
  - **Erinnerungen:** Ein `BackgroundService` (`ReminderWorker`) prüft jede Minute; die Logik liegt im testbaren `ReminderProcessor`. Zeit kommt über `TimeProvider`, Zeitzonen über `TimeZoneInfo` (IANA-Kennungen). Pro Erinnerung wird das zuletzt verarbeitete lokale Datum gespeichert (`LastFiredOn`), damit sie höchstens einmal am Tag auslösen.
  - **Verspätung:** Wurde die Uhrzeit um mehr als 30 Minuten verpasst (z. B. Ausfall), wird die Erinnerung für den Tag ohne Hinweis abgeschlossen.
  - **Unterdrückung:** Wer in den 2 Stunden vor der Uhrzeit bereits gemessen hat, bekommt keinen Hinweis. Eine Messung am Morgen unterdrückt also nicht die Erinnerung am Abend.
- **Alternativen:** Wolverine-Outbox mit PostgreSQL-Persistenz und geplanten Nachrichten: mächtiger, aber zusätzliche Tabellen, Einrichtung und Betriebsaufwand, für den aktuellen Umfang nicht nötig. Ein externer Job-Scheduler (Hangfire/Quartz): zusätzliche Abhängigkeit.
- **Konsequenzen:**
  - Läuft die API in mehreren Instanzen, können Erinnerungen doppelt entstehen (kein verteiltes Locking). Dann ist ein Lock oder die Outbox nötig.
  - Der Scheduler lädt alle Erinnerungen (höchstens 5 je Nutzer); für sehr viele Nutzer ist eine Abfrage nach nächster Fälligkeit nötig.
  - Push-Zustellung (FCM/APNs) ist eine Folge-Change; der Kanal ist über die gespeicherten Benachrichtigungen bereits vorbereitet.
