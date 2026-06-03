# Fussballturnierverwaltung

Private Konsolenanwendung zur Verwaltung, Auslosung, Simulation und Auswertung von Fußballturnieren.

## Ziel

Version 1 ermöglicht:
- Stammdatenverwaltung (Mannschaften, Länder, Verbände)
- Frei konfigurierbare Turniere mit beliebigen Rundenstrukturen
- Wahrscheinlichkeitsbasierte Spielsimulation mit reproduzierbarem Seed
- Flexible Auslosungs-Engine mit Lostöpfen, Setzlisten und Regelprüfung
- Konfigurierbare Regelwerke (Punkte, Ranking, Entscheidungsverfahren)
- Turnierbaum-Darstellung in der Konsole
- Import und Export (CSV, JSON)

Die Fachlogik ist vollständig von der Konsolen-UI getrennt, sodass in Version 2 eine Desktop-Oberfläche ergänzt werden kann.

## Technologie

| Bereich | Technologie |
|---|---|
| Sprache | C# / .NET 10 |
| Datenbank | SQL Server Developer Edition |
| ORM | Entity Framework Core 10 (Code First) |
| Konsolenoberfläche | Spectre.Console |
| Tests | xUnit + FluentAssertions |
| Logging | Microsoft.Extensions.Logging |
| Architektur | Clean Architecture |

## Projektstruktur

```
Fussballturnierverwaltung.sln

src/
  Fussballturnierverwaltung.Domain          # Entitäten, Value Objects, Interfaces
  Fussballturnierverwaltung.Application     # Services, Use Cases, Logik
  Fussballturnierverwaltung.Infrastructure  # EF Core, Repositories, Migrationen
  Fussballturnierverwaltung.Console         # Spectre.Console UI, Startup

tests/
  Fussballturnierverwaltung.Tests           # xUnit Unit- und Integrationstests
```

## Abhängigkeiten (Clean Architecture)

```
Domain  <──  Application  <──  Infrastructure
                    ▲                 ▲
                    └──── Console ────┘
```

Domain hat keine externen Abhängigkeiten. Console ist der Kompositionswurzel.

## Erste Schritte

```bash
# Datenbank erstellen (SQL Server muss laufen)
dotnet ef database update --project src/Fussballturnierverwaltung.Infrastructure --startup-project src/Fussballturnierverwaltung.Console

# Anwendung starten
dotnet run --project src/Fussballturnierverwaltung.Console

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

Nicht in Version 1 (Backlog für Version 2):
- Desktop-Oberfläche, Spieler/Kader, Form/Moral/Verletzungen, Turnierdruck

## Namenskonventionen

Deutsche Namen für alle Domänenklassen, Properties, Methoden und Enums.
Englische Begriffe nur wenn im deutschen IT- oder Fußballkontext etabliert (Rating, Repository, Service, DbContext, Seed, Import, Export).
