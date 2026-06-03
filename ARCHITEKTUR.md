# Architektur – Fussballturnierverwaltung

## Projektstruktur

```
src/
  Domain/          Entitäten, Value Objects, Enums, Domain-Interfaces
  Application/     Services, Use Cases, Validierung, Auslosung, Simulation, Ranking
  Infrastructure/  EF Core DbContext, Repositories, Migrationen, Seed, Import/Export
  Console/         Spectre.Console Menüs, Dialoge, Liveticker, Turnierbaum-Anzeige

tests/
  Tests/           Unit- und Integrationstests (xUnit + FluentAssertions)
```

## Abhängigkeitsregeln

```
Domain           keine externen Abhängigkeiten, kein EF Core, kein UI
Application      kennt Domain; definiert Repository-Interfaces (IRepository<T> etc.)
Infrastructure   implementiert Application-Interfaces; kennt EF Core, SQL Server
Console          Kompositionswurzel; registriert alle Abhängigkeiten (DI), startet die App
Tests            referenziert alle src-Projekte; verwendet EF Core InMemory für Integrationstests
```

## Domänenmodell

### Stammdaten

```
Kontinent (Enum)
Verband         1 ──< Mannschaft
Land            >── Kontinent, >── Verband
Mannschaft      >── Land, enthält OffensivRating, MittelfeldRating, DefensivRating, Heimstaerke, Auswaertsstaerke
                GesamtRating wird als berechnete Eigenschaft (kein DB-Feld) geliefert: (O+M+D)/3
```

### Regelwerke

```
Regelwerk       1 ──< PunkteRegel (2- oder 3-Punkte-System)
                1 ──< EntscheidungsVerfahren (geordnete Liste: Verlängerung, Elfmeter, …)
                0..1── RankingRegelSet

RankingRegelSet 1 ──< RankingRegelSetEintrag >── RankingRegel (Punkte, Tordifferenz, Direkter Vergleich, …)
```

### Turniere

```
TurnierVorlage  0..1──< TurnierInstanz
TurnierInstanz  1 ──< TurnierRunde
TurnierRunde    >── Regelwerk; enthält Rundenart, Reihenfolge, AuswaertstorRegelAktiv (Flag), KonfigurationJson
```

### Spiele

```
TurnierInstanz  1 ──< Spiel
TurnierRunde    1 ──< Spiel
Spiel           0..1── SpielErgebnis
                0..1── HinspielId (Selbstreferenz für Rückspiele)
SpielErgebnis   enthält Tore regulär, Verlängerung, Elfmeter; SiegerMannschaftId; EntscheidungArt
                0..* SpielEreignis (Tor, Großchance, Golden Goal, …)
```

### Auslosung

```
Auslosung       1 ──< Lostopf
                1 ──< AuslosungsRegel
                1 ──< AuslosungsErgebnis
Lostopf         1 ──< LostopfEintrag >── Mannschaft
```

### Turnierbaum

```
TurnierBaum     >── TurnierInstanz; WurzelKnoten
TurnierBaumKnoten  Selbstreferenz (ElternKnotenId, LinkerKindKnotenId, RechterKindKnotenId)
                   0..1── Spiel
```

### Simulation

```
SimulationsLauf >── TurnierInstanz; speichert ZufallsSeed für Reproduzierbarkeit
```

## Entitätsbeziehungen (vereinfacht)

```
Kontinent ──< Land ──< Mannschaft
                          │
Verband ──────────────────┘
                          │
TurnierInstanz ──< Spiel ─┤
TurnierRunde ─────────────┘
     │
Regelwerk ──< PunkteRegel
           ──< EntscheidungsVerfahren
           ──< RankingRegelSet ──< RankingRegelSetEintrag ──< RankingRegel

Auslosung ──< Lostopf ──< LostopfEintrag ──> Mannschaft
          ──< AuslosungsRegel
          ──< AuslosungsErgebnis
```

## Architekturentscheidungen

### AD-001: Auswärtstorregel als Flag, nicht als VerfahrenArt

**Entscheidung:** `AuswaertstorRegelAktiv` ist ein Boolean-Flag auf `TurnierRunde`, kein Eintrag in `VerfahrenArt`.

**Begründung:** Die Auswärtstorregel ist kein eigenständiges "Verfahren" wie Verlängerung oder Elfmeterschießen. Sie ist eine Tiebreaker-Regel, die automatisch greift, wenn zwei Mannschaften im Aggregat gleichstehen. Die Auflösungslogik prüft dieses Flag *vor* dem ersten Eintrag in `EntscheidungsVerfahren`. `EntscheidungArt.Auswaertstorregel` in `SpielErgebnis` bleibt als Ergebnisbezeichnung korrekt erhalten.

**Konsequenz:** Der Auflösungsservice in Application wertet beim Hin-/Rückspiel folgende Reihenfolge aus:
1. Aggregat prüfen → klarer Sieger? → fertig
2. `AuswaertstorRegelAktiv`? → Auswärtstore vergleichen → Sieger? → fertig
3. `EntscheidungsVerfahren` in Reihenfolge durchlaufen (Verlängerung → Elfmeter etc.)

### AD-002: GesamtRating als berechnete Eigenschaft

**Entscheidung:** `GesamtRating` wird nicht als Datenbankfeld gespeichert, sondern als `get`-only Property in der Entität berechnet.

**Begründung:** Redundanz vermeiden. Bei Änderung eines Teilratings würde ein gespeichertes GesamtRating inkonsistent. EF Core unterstützt berechnete Spalten (`HasComputedColumnSql`), aber eine reine C#-Property ist einfacher und ohne DB-Overhead korrekt.

### AD-003: KonfigurationJson für rundenspezifische Parameter

**Entscheidung:** `TurnierRunde` enthält ein optionales `KonfigurationJson`-Feld für rundenspezifische Parameter (z.B. Gruppenanzahl beim Schweizer System).

**Begründung:** Rundenarten wie `SchweizerSystem` oder `Liga` haben unterschiedliche Parameter, die nicht alle in das relationale Modell passen. JSON erlaubt Erweiterbarkeit ohne Schemaänderung. Typisierung erfolgt über spezifische Konfigurationsklassen in Application, die per JSON deserialisiert werden.

### AD-004: Auslosungs-Lösbarkeit vor Start prüfen

**Entscheidung:** Vor jeder Auslosung (automatisch und manuell) wird geprüft, ob mindestens eine vollständige gültige Auslosung existiert.

**Begründung:** Sackgassen bei manueller Auslosung sollen verhindert werden. Der `AuslosungsAnalyseService` in Application berechnet dies per Constraint-Propagation und gibt bei Sackgassengefahr eine Warnung aus.

### AD-005: Namespace-Konflikt Console

**Entscheidung:** Das Projekt heißt `Fussballturnierverwaltung.Console` (per Spec), aber der Root-Namespace ist `Fussballturnierverwaltung.Konsole`.

**Begründung:** Der Name `Console` kollidiert in C# mit `System.Console`. Durch den abweichenden Namespace wird der Konflikt vermieden, ohne die Ordner- und Projektdatei-Namenskonvention zu verletzen.

### AD-006: Microsoft.Extensions.Hosting als Kompositionswurzel

**Entscheidung:** Das Console-Projekt verwendet `Microsoft.Extensions.Hosting` für DI, Logging und Konfiguration.

**Begründung:** Standardisierter Aufbau, der spätere Migration zu Desktop (WPF/Avalonia) oder Web vereinfacht. Alle Services werden zentral in `Program.cs` registriert.

## Erweiterungspunkte für Version 2

| Feature | Vorbereitung in V1 |
|---|---|
| Desktop-Oberfläche | Fachlogik vollständig in Application, kein UI-Code in Domain/Application |
| Spieler & Kader | Domain-Modell erweiterbar; `Mannschaft` hat keine feste Spielerliste |
| Form / Moral / Momentum | `SimulationsModus` und Simulations-Engine sind austauschbar (Interface) |
| Verletzungen / Sperren | `SpielEreignis.EreignisArt` ist erweiterbar |
| Turnierdruck | `SimulationsLauf` kann Parameter-Objekte aufnehmen |
| Grafischer Turnierbaum | `TurnierBaum`-Modell ist UI-unabhängig; textuelle Darstellung ist separater Service |
| Massensimulationen | `ZufallsSeed` in `SimulationsLauf` ermöglicht reproduzierbare Serienläufe |
| Erweiterte Statistiken | Alle Ereignisse werden in `SpielEreignis` protokolliert |

## Wichtige Designentscheidungen – Begründungen

**Deutsche Namen:** Maximale Lesbarkeit im Fachkontext. Domänenexperten (Fußball-Interessierte) sollen Modelle direkt verstehen können, ohne Übersetzungsarbeit.

**Kein hartcodiertes Turnierschema:** Die Engine kennt keine feste Reihenfolge (Gruppe → Achtelfinale → ...). Jedes Turnier besteht aus konfigurierten `TurnierRunden` mit expliziter `Reihenfolge`-Eigenschaft. Das ermöglicht historisch korrekte Sonderformate.

**Regelwerke als Datenobjekte:** Punkte-, Ranking- und Entscheidungsregeln sind Datenbankentitäten, nicht hartcodierte Logik. Das ermöglicht historisch korrekte Regelwerke (z.B. 2-Punkte-Regel vor 1995, Golden Goal bei EM 1996).

**Reproduzierbare Simulation:** `ZufallsSeed` in `SimulationsLauf` garantiert, dass eine Simulation mit gleichen Eingaben immer gleiche Ergebnisse liefert – wichtig für Debugging und Nachvollziehbarkeit.
