# ADR 0004: MAUI-App mit eigener Core-Bibliothek und CommunityToolkit.Mvvm

- **Status:** akzeptiert (Zustimmung des Product Owners für die Abhängigkeit am 2026-11-05)
- **Kontext:** Die MAUI-App war nur die Projektvorlage. Sie soll dieselben Funktionen wie die Blazor-Oberfläche bieten. MAUI-Workloads und Simulatoren sind in der CI nicht verfügbar; Logik muss trotzdem automatisiert testbar sein.
- **Entscheidung:**
  1. Alle Logik (ViewModels, Sitzung, Validierung, Texte) liegt in `CleanArchitecture.Presentation.Maui.Core` (reines `net10.0`, keine MAUI-Abhängigkeit). Das MAUI-Projekt enthält nur Views, DI-Verdrahtung und plattformspezifische Adapter (`SecureStorage`, Teilen, Dialoge, Navigation).
  2. MVVM mit **CommunityToolkit.Mvvm** (Source-Generatoren für `ObservableProperty`/`RelayCommand`). Es ist das von Microsoft gepflegte Standardpaket, MIT-lizenziert, ohne eigene Laufzeit-Abhängigkeiten außer der BCL, und reduziert Boilerplate.
  3. Tokens liegen ausschließlich im sicheren Speicher des Betriebssystems, abstrahiert über `ISecureStore`.
  4. Texte über `.resx` (DE/EN) in der Core-Bibliothek, Zugriff über `ILocalizer`.
- **Alternativen:** eigenes `ObservableObject` (mehr eigener Code, keine Source-Generatoren); Logik im MAUI-Projekt (in der CI nicht testbar).
- **Folgen:** Core-Tests laufen in der bestehenden CI auf Linux. Views werden nur manuell im Simulator geprüft. `ChartGeometry` der Web-UI wird in Phase 2 entweder in eine gemeinsame Bibliothek gehoben oder mit Test kopiert.
