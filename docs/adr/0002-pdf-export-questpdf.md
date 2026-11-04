# ADR 0002: PDF-Export mit QuestPDF

- **Status:** akzeptiert (Lizenzprüfung durch den Betreiber offen)
- **Kontext:** Epic 5 verlangt einen PDF-Bericht für den Arzt (Tabelle, Kennzahlen, Haftungshinweis) auf Windows, macOS und Linux-Containern.
- **Entscheidung:** QuestPDF mit `LicenseType.Community`. Der Bericht entsteht im Speicher (höchstens 10 000 Messungen je Export).
- **Alternativen:** PDFsharp (MIT): großzügigere Lizenz, aber Layout von Hand und unter Linux ist ein Font-Resolver nötig. Reine Browser-/HTML-Konvertierung: zusätzliche Abhängigkeit (Chromium) im Container.
- **Konsequenzen:**
  - **Lizenz:** Die Community-Lizenz von QuestPDF gilt nur unter bestimmten Bedingungen (Stand der Entscheidung: u. a. Jahresumsatz unter 1 Mio. USD, Open Source oder nicht kommerziell). Der Betreiber muss prüfen, ob sie für den Einsatz passt; sonst PDFsharp oder eine kostenpflichtige Lizenz verwenden.
  - Die Anbindung ist hinter `IExportWriter` gekapselt, ein Wechsel der Bibliothek betrifft nur die Infrastruktur.
