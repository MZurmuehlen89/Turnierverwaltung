# Architektur – Fussballturnierverwaltung

## Projektstruktur

```
src/
  Domain/          Entitäten, Value Objects, Enums, Domain-Interfaces
  Application/     Services, Use Cases, Validierung, Auslosung, Simulation, Ranking, Spielplangeneratoren
  Infrastructure/  EF Core DbContext, Repositories, Migrationen, Seed, Import/Export
  WinUi/           WinUI-3-Views, MVVM-ViewModels, Navigation, Ribbon, Startup (Desktop)

tests/
  Tests/                Unit-, Integrations- und Architekturtests (xUnit + FluentAssertions, NetArchTest)
  GuiTests/             GUI-Tests gegen die laufende App (FlaUI/UI Automation, eigene Test-DB)
  GuiTest.Framework/    Seitenobjekte, Warte- und Szenario-Bausteine für die GUI-Tests
  GuiTest.Framework.Tests/  Tests des GUI-Test-Frameworks
```

## Abhängigkeitsregeln

```
Domain           keine externen Abhängigkeiten, kein EF Core, kein UI
Application      kennt Domain; definiert Repository-Interfaces (IRepository<T> etc.)
Infrastructure   implementiert Application-Interfaces; kennt EF Core, SQL Server
WinUi            einzige Kompositionswurzel; registriert alle Abhängigkeiten (DI), startet die App
Tests            referenziert Domain, Application, Infrastructure; EF Core InMemory für Integrationstests
```

Die Architekturtests (`tests/Fussballturnierverwaltung.Tests/Architektur`) prüfen diese Richtung bei jedem Testlauf.

Bis Herbst 2026 gab es neben WinUI eine Konsolen- und eine WPF-Oberfläche sowie einen Avalonia-Versuch. WPF wurde
aufgegeben, weil sich ein sauberer Dunkelmodus mit den nativen WPF-Steuerelementen nicht umsetzen ließ; die Konsole
war die erste Oberfläche überhaupt.

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
TurnierInstanz  0..1── Kategorie (Enum)
                0..1── StandardRegelwerk (Regelwerk-Default für alle Runden)
                0..1── Verband (Filter für Mannschaftsauswahl)
                enthält Status (TurnierStatus, inkl. Vorlage), SaisonOderJahr, MannschaftsartFilter,
                ZielMannschaftsanzahl (optional, rein informativ)
                1 ──< TurnierRunde

TurnierRunde    >── Regelwerk (optional; überschreibt StandardRegelwerk der Instanz, Pflicht sobald
                Status != Vorlage)
                enthält Rundenart, Reihenfolge, EingangsMannschaftsanzahl, AusgangsMannschaftsanzahl
                MitRueckspiel (Bool), AuswaertstorRegelAktiv (Flag, nur aktiv wenn MitRueckspiel=true)
                PaarungFestVorgegeben (Bool, feste Paarungen statt Auslosung)
                KonfigurationJson (rundenspezifische Parameter, z.B. Gruppenanzahl)
```

Eine "Turniervorlage" ist keine eigene Entität mehr, sondern eine `TurnierInstanz` mit
`Status = TurnierStatus.Vorlage` (siehe AD-009). `TurnierKlonService.KlonenAsync` kopiert die
Struktur einer Instanz (Kopf-Felder, Runden, Übergangsregeln, Verbandsquoten) in eine neue Instanz —
sowohl für "Instanz aus Vorlage erstellen" als auch fürs Klonen eines echten Turniers.

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

### AD-005: Namespace-Konflikt Console (entfallen)

Galt für das frühere Konsolenprojekt (Root-Namespace `Fussballturnierverwaltung.Konsole` statt `Console`, um
`System.Console` nicht zu verdecken). Das Projekt ist entfernt; die Nummer bleibt belegt.

### AD-006: Microsoft.Extensions.Hosting als Kompositionswurzel

**Entscheidung:** Das Startup-Projekt (WinUi) verwendet `Microsoft.Extensions.Hosting` für DI, Logging (Serilog) und Konfiguration.

**Begründung:** Standardisierter Aufbau; als es noch mehrere Oberflächen gab, teilten sie sich so denselben Registrierungscode. Alle Services werden in `App.xaml.cs` registriert, `InfrastrukturErweiterungen.AddInfrastruktur()` kapselt die Infrastructure-Registrierung.

### AD-007: NavigationService erstellt einen neuen DI-Scope pro Navigation

**Entscheidung:** Der `NavigationService` disposed bei jeder Navigation den vorherigen Scope und erstellt einen neuen `IServiceScope`, aus dem das Ziel-ViewModel aufgelöst wird.

**Begründung:** Ein `DbContext` ist nicht threadsafe und darf nicht über mehrere Views geteilt werden. Durch den Scope-Wechsel bei jeder Navigation erhält jedes ViewModel seinen eigenen `DbContext`. Ohne dieses Muster würden parallele oder aufeinanderfolgende Datenbankoperationen verschiedener Views denselben Kontext teilen und zu Tracking-Konflikten führen.

**Konsequenz:** Application-Services sind `Scoped` (Ausnahme: `EinstellungenService` als `Singleton`, er hält keinen `DbContext`), ViewModels überwiegend `Transient`. Der aktive Scope lebt genau so lange wie die aktive Seite.

### AD-008: Regelwerk auf Turnierinstanz- und Rundenebene (Hierarchisches Default)

**Entscheidung:** `TurnierInstanz.StandardRegelwerkId` definiert ein optionales Default-Regelwerk. Jede `TurnierRunde` hat ein eigenes verpflichtendes `RegelwerkId`, das das Instanz-Default überschreibt.

**Begründung:** Turniere können mehrere Runden mit unterschiedlichen Regelwerken haben (z.B. Gruppenphase mit 3-Punkte-Regel, KO-Runde mit Elfmeter statt Verlängerung). Das Instanz-Default vereinfacht die Ersterfassung, wenn alle Runden dasselbe Regelwerk nutzen. Die Runden-Ebene erlaubt rundenspezifische Abweichungen ohne Redundanz.

### AD-009: Vorlage als Status statt eigener Entität

**Entscheidung:** Es gibt keine eigenständige `TurnierVorlage`-Entität mehr. Eine Vorlage ist eine
`TurnierInstanz` mit `TurnierStatus.Vorlage`, ohne Selbstreferenz zurück auf ihre Vorlage.

**Begründung:** `TurnierVorlage` und `TurnierInstanz` waren strukturell nahezu identisch (Runden,
Übergangsregeln, Verbandsquoten) und pflegten das doppelt. Eine Rückreferenz Instanz→Vorlage wurde
nie für Logik gebraucht, nur zur Nachverfolgung. "Instanz aus Vorlage erstellen" ist dadurch ein
reiner Kopiervorgang (`TurnierKlonService`), der auch fürs allgemeine Turnier-Klonen dient.

**Konsequenz:** Es gilt für Vorlagen und echte Turniere eine einzige, strenge Rundenvalidierung
(`TurnierValidator`) statt einer separaten, laxeren Vorlagen-Validierung. `Spiel.HeimPlatzhalterName`/
`AuswaertsPlatzhalterName` bilden feste Paarungen (`TurnierRunde.PaarungFestVorgegeben`) sowohl für
Vorlagen als auch für Instanzen ab; die frühere `VorlagenPaarung`-Entität entfällt ersatzlos.

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
