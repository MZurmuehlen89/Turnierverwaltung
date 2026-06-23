# Architektur – Fussballturnierverwaltung

## Projektstruktur

```
src/
  Domain/          Entitäten, Value Objects, Enums, Domain-Interfaces
  Application/     Services, Use Cases, Validierung, Auslosung, Simulation, Ranking, Spielplangeneratoren
  Infrastructure/  EF Core DbContext, Repositories, Migrationen, Seed, Import/Export
  Console/         Spectre.Console Menüs, Dialoge, Liveticker, Startup (Konsole)
  Wpf/             WPF-Views, MVVM-ViewModels, Navigation, Fluent.Ribbon, Startup (Desktop)

tests/
  Tests/           Unit- und Integrationstests (xUnit + FluentAssertions)
```

## Abhängigkeitsregeln

```
Domain           keine externen Abhängigkeiten, kein EF Core, kein UI
Application      kennt Domain; definiert Repository-Interfaces (IRepository<T> etc.)
Infrastructure   implementiert Application-Interfaces; kennt EF Core, SQL Server
Console          Kompositionswurzel (Konsole); registriert alle Abhängigkeiten (DI), startet die App
Wpf              Kompositionswurzel (Desktop); registriert dieselben Services wie Console plus ViewModels
Tests            referenziert alle src-Projekte; verwendet EF Core InMemory für Integrationstests
```

## Domänenmodell

### Stammdaten

```
Kontinent (Enum)
Verband         1 ──< Mannschaft
Land            >── Kontinent, >── Verband
Mannschaft      >── Land; enthält OffensivRating, MittelfeldRating, DefensivRating, Heimstaerke, Auswaertsstaerke
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
TurnierVorlage  0..1── VorlagenKategorie (Enum)
                1 ──< VorlagenRunden (Vorlagen-Rundenstruktur ohne Instanz-Daten)
                0..* ──< TurnierInstanz

TurnierInstanz  >── TurnierVorlage (optional)
                0..1── StandardRegelwerk (Regelwerk-Default für alle Runden)
                0..1── Verband (Filter für Mannschaftsauswahl)
                enthält Status (TurnierStatus), SaisonOderJahr, MannschaftsartFilter
                1 ──< TurnierRunde

TurnierRunde    >── Regelwerk (überschreibt StandardRegelwerk der Instanz)
                enthält Rundenart, Reihenfolge, EingangsMannschaftsanzahl, AusgangsMannschaftsanzahl
                MitRueckspiel (Bool), AuswaertstorRegelAktiv (Flag, nur aktiv wenn MitRueckspiel=true)
                KonfigurationJson (rundenspezifische Parameter, z.B. Gruppenanzahl)
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

**Entscheidung:** `AuswaertstorRegelAktiv` ist ein Boolean-Flag auf `TurnierRunde`, kein Eintrag in `VerfahrenArt`. Es ist nur relevant wenn `MitRueckspiel = true`.

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

**Entscheidung:** Beide Startup-Projekte (Console und Wpf) verwenden `Microsoft.Extensions.Hosting` für DI, Logging und Konfiguration.

**Begründung:** Standardisierter Aufbau, der einen gemeinsamen Service-Registrierungscode für beide UI-Schichten ermöglicht. Alle Services werden zentral in `Program.cs` (Console) bzw. `App.xaml.cs` (Wpf) registriert. `InfrastrukturErweiterungen.AddInfrastruktur()` kapselt die Infrastructure-Registrierung und wird von beiden aufgerufen.

### AD-007: NavigationService erstellt einen neuen DI-Scope pro Navigation (WPF)

**Entscheidung:** `NavigationService.NavigiereZu<TViewModel>()` disposed den vorherigen Scope und erstellt einen neuen `IServiceScope`. ViewModels werden als `Transient` registriert.

**Begründung:** Ein `DbContext` ist nicht threadsafe und darf nicht über mehrere Views geteilt werden. Durch den Scope-Wechsel bei jeder Navigation erhält jedes ViewModel seinen eigenen `DbContext`. Ohne dieses Muster würden parallele oder aufeinanderfolgende Datenbankoperationen verschiedener Views denselben Kontext teilen und zu Tracking-Konflikten führen.

**Konsequenz:** `NavigationService` und `MainViewModel` sind `Singleton`; alle ViewModels und Application-Services sind `Transient` (WPF) bzw. `Scoped` (Console). Der aktive Scope lebt genau so lange wie das aktive ViewModel.

### AD-008: Regelwerk auf Turnierinstanz- und Rundenebene (Hierarchisches Default)

**Entscheidung:** `TurnierInstanz.StandardRegelwerkId` definiert ein optionales Default-Regelwerk. Jede `TurnierRunde` hat ein eigenes verpflichtendes `RegelwerkId`, das das Instanz-Default überschreibt.

**Begründung:** Turniere können mehrere Runden mit unterschiedlichen Regelwerken haben (z.B. Gruppenphase mit 3-Punkte-Regel, KO-Runde mit Elfmeter statt Verlängerung). Das Instanz-Default vereinfacht die Ersterfassung, wenn alle Runden dasselbe Regelwerk nutzen. Die Runden-Ebene erlaubt rundenspezifische Abweichungen ohne Redundanz.

## Erweiterungspunkte für Version 2

| Feature | Vorbereitung in V1 |
|---|---|
| Spieler & Kader | Domain-Modell erweiterbar; `Mannschaft` hat keine feste Spielerliste |
| Form / Moral / Momentum | `SimulationsModus` und Simulations-Engine sind austauschbar (Interface) |
| Verletzungen / Sperren | `SpielEreignis.EreignisArt` ist erweiterbar |
| Turnierdruck | `SimulationsLauf` kann Parameter-Objekte aufnehmen |
| Massensimulationen | `ZufallsSeed` in `SimulationsLauf` ermöglicht reproduzierbare Serienläufe |
| Erweiterte Statistiken | Alle Ereignisse werden in `SpielEreignis` protokolliert |

## Wichtige Designentscheidungen – Begründungen

**Deutsche Namen:** Maximale Lesbarkeit im Fachkontext. Domänenexperten (Fußball-Interessierte) sollen Modelle direkt verstehen können, ohne Übersetzungsarbeit.

**Kein hartcodiertes Turnierschema:** Die Engine kennt keine feste Reihenfolge (Gruppe → Achtelfinale → ...). Jedes Turnier besteht aus konfigurierten `TurnierRunden` mit expliziter `Reihenfolge`-Eigenschaft. Das ermöglicht historisch korrekte Sonderformate.

**Regelwerke als Datenobjekte:** Punkte-, Ranking- und Entscheidungsregeln sind Datenbankentitäten, nicht hartcodierte Logik. Das ermöglicht historisch korrekte Regelwerke (z.B. 2-Punkte-Regel vor 1995, Golden Goal bei EM 1996).

**Reproduzierbare Simulation:** `ZufallsSeed` in `SimulationsLauf` garantiert, dass eine Simulation mit gleichen Eingaben immer gleiche Ergebnisse liefert – wichtig für Debugging und Nachvollziehbarkeit.
