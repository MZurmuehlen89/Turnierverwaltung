# Fussballturnierverwaltung

Private Anwendung zur Verwaltung, Auslosung, Simulation und Auswertung von Fußballturnieren. Verfügbar als Konsolenanwendung (Spectre.Console) und als WPF-Desktop-GUI (Fluent.Ribbon).

## Ziel

Version 1 ermöglicht:
- Stammdatenverwaltung (Mannschaften, Länder, Verbände)
- Frei konfigurierbare Turniere mit beliebigen Rundenstrukturen
- Wahrscheinlichkeitsbasierte Spielsimulation mit reproduzierbarem Seed
- Flexible Auslosungs-Engine mit Lostöpfen, Setzlisten und Regelprüfung
- Konfigurierbare Regelwerke (Punkte, Ranking, Entscheidungsverfahren)
- Turnierbaum-Darstellung
- Import und Export (CSV, JSON)
- WPF-Desktop-Oberfläche mit Fluent.Ribbon-Navigation

Die Fachlogik ist vollständig von der UI getrennt: beide Oberflächen (Console, WPF) teilen sich dieselbe Application- und Infrastructure-Schicht.

## Technologie

| Bereich | Technologie |
|---|---|
| Sprache | C# / .NET 10 |
| Datenbank | SQL Server Developer Edition |
| ORM | Entity Framework Core 10 (Code First) |
| Konsolenoberfläche | Spectre.Console |
| Desktop-Oberfläche | WPF + Fluent.Ribbon + CommunityToolkit.Mvvm |
| Tests | xUnit + FluentAssertions |
| Logging | Microsoft.Extensions.Logging |
| Architektur | Clean Architecture |

## Projektstruktur

```
Fussballturnierverwaltung.sln

src/
  Fussballturnierverwaltung.Domain          # Entitäten, Value Objects, Enums
  Fussballturnierverwaltung.Application     # Services, Use Cases, Logik, Generatoren
  Fussballturnierverwaltung.Infrastructure  # EF Core, Repositories, Migrationen, Seed
  Fussballturnierverwaltung.Console         # Spectre.Console UI, Startup (Konsole)
  Fussballturnierverwaltung.Wpf             # WPF GUI, MVVM ViewModels, Startup (Desktop)

tests/
  Fussballturnierverwaltung.Tests           # xUnit Unit- und Integrationstests
```

## Abhängigkeiten (Clean Architecture)

```
Domain  <──  Application  <──  Infrastructure
                    ▲                 ▲
                    ├──── Console ────┤
                    └──── Wpf ────────┘
```

Domain hat keine externen Abhängigkeiten. Console und Wpf sind unabhängige Kompositionswurzeln.

## Erste Schritte

```bash
# Datenbank erstellen (SQL Server muss laufen)
dotnet ef database update --project src/Fussballturnierverwaltung.Infrastructure --startup-project src/Fussballturnierverwaltung.Console

# Konsolenanwendung starten
dotnet run --project src/Fussballturnierverwaltung.Console

# WPF-Anwendung starten
dotnet run --project src/Fussballturnierverwaltung.Wpf

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
- WPF-Desktop-Oberfläche mit Fluent.Ribbon, MVVM, Scope-basierter Navigation

Nicht in Version 1 (Backlog für Version 2):
- Spieler/Kader, Form/Moral/Verletzungen, Turnierdruck, Massensimulationen

## Namenskonventionen

Deutsche Namen für alle Domänenklassen, Properties, Methoden und Enums.
Englische Begriffe nur wenn im deutschen IT- oder Fußballkontext etabliert (Rating, Repository, Service, DbContext, Seed, Import, Export, ViewModel, View).
