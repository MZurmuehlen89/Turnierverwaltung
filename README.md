# Fussballturnierverwaltung

Private Anwendung zur Verwaltung, Auslosung, Simulation und Auswertung von Fußballturnieren. Verfügbar als WinUI-3-Desktop-Anwendung (WindowsAppSDK).

## Ziel

Version 1 ermöglicht:
- Stammdatenverwaltung (Mannschaften, Länder, Verbände)
- Frei konfigurierbare Turniere mit beliebigen Rundenstrukturen
- Wahrscheinlichkeitsbasierte Spielsimulation mit reproduzierbarem Seed
- Flexible Auslosungs-Engine mit Lostöpfen, Setzlisten und Regelprüfung
- Konfigurierbare Regelwerke (Punkte, Ranking, Entscheidungsverfahren)
- Turnierbaum-Darstellung
- Import und Export (CSV, JSON)
- WinUI-3-Desktop-Oberfläche mit MVVM-Navigation

Die Fachlogik ist vollständig von der UI getrennt: die Oberfläche teilt sich mit den übrigen Schichten dieselbe Application- und Infrastructure-Schicht.

## Technologie

| Bereich | Technologie |
|---|---|
| Sprache | C# / .NET 10 |
| Datenbank | SQL Server Developer Edition |
| ORM | Entity Framework Core 10 (Code First) |
| Desktop-Oberfläche | WinUI 3 / WindowsAppSDK + CommunityToolkit.Mvvm |
| Tests | xUnit + FluentAssertions |
| Logging | Microsoft.Extensions.Logging |
| Architektur | Clean Architecture |

## Projektstruktur

```
Fussballturnierverwaltung.slnx

src/
  Fussballturnierverwaltung.Domain          # Entitäten, Value Objects, Enums
  Fussballturnierverwaltung.Application     # Services, Use Cases, Logik, Generatoren
  Fussballturnierverwaltung.Infrastructure  # EF Core, Repositories, Migrationen, Seed
  Fussballturnierverwaltung.WinUi           # WinUI 3 GUI, MVVM ViewModels, Startup (Desktop)

tests/
  Fussballturnierverwaltung.Tests           # xUnit Unit- und Integrationstests
```

## Abhängigkeiten (Clean Architecture)

```
Domain  <──  Application  <──  Infrastructure  <──  WinUi
```

Domain hat keine externen Abhängigkeiten. WinUi ist die einzige Kompositionswurzel.

## Erste Schritte

```bash
# Datenbank erstellen (SQL Server muss laufen)
dotnet ef database update --project src/Fussballturnierverwaltung.Infrastructure --startup-project src/Fussballturnierverwaltung.WinUi

# Anwendung starten
dotnet run --project src/Fussballturnierverwaltung.WinUi

# Tests ausführen
dotnet test
```

## Version 1 – Umfang

Implementiert in Version 1:
- Stammdaten (Mannschaften, Länder, Verbände, Kontinente)
- Turniervorlagen und -instanzen mit konfigurierbaren Runden
- Regelwerke (Punkte, Ranking, Entscheidungsverfahren inkl. Auswärtstorregel-Flag)
- Auslosungs-Engine (automatisch/manuell, Lostöpfe, Sackgassenerkennung)
- Spielsimulation (Schnell, Ausführlich, Manuell) mit Liveticker
- Turnierbaum-Darstellung
- Import/Export (CSV, JSON)
- WinUI-3-Desktop-Oberfläche mit MVVM, Scope-basierter Navigation

Nicht in Version 1 (Backlog für Version 2):
- Spieler/Kader, Form/Moral/Verletzungen, Turnierdruck, Massensimulationen

## Namenskonventionen

Deutsche Namen für alle Domänenklassen, Properties, Methoden und Enums.
Englische Begriffe nur wenn im deutschen IT- oder Fußballkontext etabliert (Rating, Repository, Service, DbContext, Seed, Import, Export, ViewModel, View).
