using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Infrastructure.Seed.Daten;

/// <summary>Vordefinierte Turniervorlagen (Issue #294: seit der Vorlage/Instanz-Vereinheitlichung sind
/// das ganz normale <see cref="TurnierInstanz"/>-Objekte mit <see cref="TurnierStatus.Vorlage"/>, kein
/// eigener Typ mehr).</summary>
public static class TurnierVorlagenSeedDaten
{
    public static List<TurnierInstanz> Erstellen(
        IReadOnlyDictionary<string, int>? regelwerkLookup = null,
        IReadOnlyDictionary<string, int>? verbandLookup = null,
        IReadOnlyDictionary<string, int>? landLookup = null,
        IReadOnlyDictionary<string, int>? ranglisteLookup = null)
    {
        List<TurnierInstanz> vorlagen =
        [
            FifaWm48Teams(regelwerkLookup),
            FifaWm32Teams(regelwerkLookup),
            FifaWm24Teams1986Bis1994(regelwerkLookup),
            FifaWm24Teams1982(regelwerkLookup),
            FifaWm16Teams1974Bis1978(regelwerkLookup),
            FifaWm16Teams1958Bis1970(regelwerkLookup),
            FifaWm16Teams1934Bis1938(regelwerkLookup),
            FifaKlubWm32Teams(regelwerkLookup),
            UefaEm24Teams(regelwerkLookup, verbandLookup),
            UefaEm16Teams(regelwerkLookup, verbandLookup),
            UefaEm8Teams1984Bis1992(regelwerkLookup, verbandLookup),
            UefaEm8Teams1980(regelwerkLookup, verbandLookup),
            UefaEm4Teams(regelwerkLookup, verbandLookup),
            UefaNationsLeagueLigaA(regelwerkLookup, verbandLookup),
            CopaAmerica16Teams(regelwerkLookup),
            CopaAmerica12Teams(regelwerkLookup),
            AfrikaCup24Teams(regelwerkLookup, verbandLookup),
            AfcAsianCup24Teams(regelwerkLookup, verbandLookup),
            ConcacafGoldCup16Teams(regelwerkLookup, verbandLookup),
            UefaChampionsLeague32TeamsKlassisch(regelwerkLookup, verbandLookup, ranglisteLookup),
            UefaChampionsLeague32TeamsModern(regelwerkLookup, verbandLookup, ranglisteLookup),
            UefaChampionsLeague36TeamsModern(regelwerkLookup, verbandLookup, ranglisteLookup),
            UefaEuropaLeague36Teams(regelwerkLookup, verbandLookup, ranglisteLookup),
            UefaConferenceLeague36Teams(regelwerkLookup, verbandLookup, ranglisteLookup),
            DfbPokal64Teams(regelwerkLookup, verbandLookup, landLookup, ranglisteLookup),
            FaCup64Teams(regelwerkLookup, verbandLookup, landLookup, ranglisteLookup),
            NationaleLiga(regelwerkLookup, verbandLookup, landLookup, "Bundesliga",     "18 Teams",  "GER", 18, LIGA_TORDIFFERENZ,       BUNDESLIGA_BEREICHE),
            NationaleLiga(regelwerkLookup, verbandLookup, landLookup, "2. Bundesliga",  "18 Teams",  "GER", 18, LIGA_TORDIFFERENZ,       ZWEITE_BUNDESLIGA_BEREICHE),
            NationaleLiga(regelwerkLookup, verbandLookup, landLookup, "Premier League", "20 Teams",  "ENG", 20, LIGA_TORDIFFERENZ,       ZWANZIGER_LIGA_BEREICHE),
            NationaleLiga(regelwerkLookup, verbandLookup, landLookup, "La Liga",        "20 Teams",  "ESP", 20, LIGA_DIREKTER_VERGLEICH, ZWANZIGER_LIGA_BEREICHE),
            NationaleLiga(regelwerkLookup, verbandLookup, landLookup, "Serie A",        "20 Teams",  "ITA", 20, LIGA_DIREKTER_VERGLEICH, ZWANZIGER_LIGA_BEREICHE),
            NationaleLiga(regelwerkLookup, verbandLookup, landLookup, "Ligue 1",        "18 Teams",  "FRA", 18, LIGA_TORDIFFERENZ,       LIGUE_1_BEREICHE),
            MiniTurnier8Teams(regelwerkLookup),
            MiniTurnier4Teams(regelwerkLookup),
            FreiesTurnier(),
        ];
        foreach (var vorlage in vorlagen)
        {
            NeutraleSpielorteSetzen(vorlage);
            UeberraschungsfaktorSetzen(vorlage);
            AuslosungsregelnSetzen(vorlage);
            GruppenpositionSetzen(vorlage);
            GruppenkoepfeSetzen(vorlage);
            AuslosungsartSetzen(vorlage);
            FifaWeltranglisteSetzen(vorlage, ranglisteLookup);
            QuotenSeedDaten.Setzen(vorlage, verbandLookup, landLookup);
        }
        return vorlagen;
    }

    // ── Vorgegebene Auslosungsregeln (Issue #356) ──────────────────────────────

    // Regeln laut realem Reglement, beim Anlegen einer Auslosung bereits aktiv. Bestehende Datenbanken: Migration
    // AuslosungsregelnVorgabeJeRunde — dort dieselbe Zuordnung als SQL, bei Änderungen hier mitziehen.
    private static void AuslosungsregelnSetzen(TurnierInstanz vorlage)
    {
        foreach (var runde in vorlage.Runden)
            runde.VorgegebeneAuslosungsregeln = [.. VorgegebeneAuslosungsregeln(vorlage, runde)];
    }

    private static AuslosungsRegelArt[] VorgegebeneAuslosungsregeln(TurnierInstanz vorlage, TurnierRunde runde)
    {
        bool gruppenphase = runde.Rundenart == Rundenart.Gruppenphase && runde.Reihenfolge == 1;
        switch (vorlage.Name)
        {
            // FIFA-WM: je Gruppe höchstens ein Team je Konföderation, UEFA höchstens zwei — als allgemeine Regel
            // erst seit 1994 (davor wurden nur die Südamerikaner getrennt), daher nicht in den älteren Vorlagen;
            // die Vorlage 1986–1994 bleibt ohne, weil die Regel nur für eines ihrer drei Turniere galt.
            case "FIFA WM" when gruppenphase && vorlage.ZielMannschaftsanzahl >= 32:
                return [AuslosungsRegelArt.Konfoederationsverteilung];

            // Klub-WM 2025: wie die WM, zusätzlich keine zwei Klubs desselben Landes in einer Gruppe.
            case "FIFA Klub-WM" when gruppenphase:
                return [AuslosungsRegelArt.Konfoederationsverteilung, AuslosungsRegelArt.KeinGleichesLand];

            case "UEFA Champions League" when vorlage.ZielMannschaftsanzahl == 32:
                // Gruppenphase ohne Klubs desselben Landes; Achtelfinale Gruppensieger gegen Gruppenzweite (nie
                // aus der eigenen Gruppe — bei dieser Auslosungsart immer aktiv), nie aus demselben Land, der
                // Gruppensieger hat im Rückspiel Heimrecht. Ab dem Viertelfinale offene Auslosung.
                if (gruppenphase) return [AuslosungsRegelArt.KeinGleichesLand];
                if (runde.Name == "Achtelfinale")
                    return [AuslosungsRegelArt.KeinGleichesLand, AuslosungsRegelArt.HeimrechtNachSetzliste];
                return [];

            // Ligaphase ab 2024/25: kein Gegner aus dem eigenen Land, höchstens zwei aus demselben Land (hängt an
            // „Kein gleiches Land“, #347). Playoff und Achtelfinale: der in der Liga besser Platzierte hat im
            // Rückspiel Heimrecht.
            case "UEFA Champions League" or "UEFA Europa League" or "UEFA Conference League":
                if (runde.Rundenart == Rundenart.TopfLiga) return [AuslosungsRegelArt.KeinGleichesLand];
                if (runde.Name is "Playoff-Runde" or "Achtelfinale") return [AuslosungsRegelArt.HeimrechtNachSetzliste];
                return [];

            // Gruppensieger hat im Rückspiel Heimrecht.
            case "UEFA Nations League Liga A" when runde.Name == "Viertelfinale":
                return [AuslosungsRegelArt.HeimrechtNachSetzliste];

            // DFB-Pokal (#393): Vereine unterhalb der 2. Bundesliga haben gegen Erst- und Zweitligisten Heimrecht, bis
            // einschließlich Halbfinale; sonst der zuerst Gezogene (= Los). Die Spielklasse kommt aus der
            // Ligapyramide als Standard-Rangliste der Runde. Finale an neutralem Ort. In der 1. Runde spielt dagegen
            // der ganze Amateurtopf zu Hause, auch seine Zweitligisten (#470).
            case "DFB-Pokal" when runde.Reihenfolge == 1:
                return [AuslosungsRegelArt.HeimrechtFuerUnterenTopf];
            case "DFB-Pokal" when !runde.NeutralerSpielort:
                return [AuslosungsRegelArt.HeimrechtFuerUnterklassige];

            // FA Cup: der zuerst Gezogene spielt zu Hause, auch gegen Unterklassige — außer an neutralem Ort
            // (Halbfinale und Finale in Wembley).
            case "FA Cup" when !runde.NeutralerSpielort:
                return [AuslosungsRegelArt.HeimrechtNachLos];

            default:
                return [];
        }
    }

    // ── Position in der Gruppe (Issue #475) ────────────────────────────────────

    // Nach der neuesten Ausgabe der Vorlage. Fehlt sie hier, gilt „Nach Topf“ — belegt bei Copa América 2024 und
    // Afrika-Cup 2025, nicht belegbar bei WM 1934–1994, EM mit 8 Teams und Asian Cup. Bestehende Datenbanken:
    // Migration Gruppenposition — dort dieselbe Zuordnung als SQL, bei Änderungen hier mitziehen.
    public static IReadOnlyDictionary<(string Name, string Variante), Gruppenposition> GruppenpositionJeVorlage { get; } =
        new Dictionary<(string, string), Gruppenposition>
        {
            // Topf 1 auf Position 1, die übrigen per Positionskugel (WM 2022, EM 2012, Copa 2019, Gold Cup 2025,
            // Klub-WM 2025). Die WM 2026 legt die Positionen 2–4 nach festem Schema je Gruppe fest — angenähert.
            [("FIFA WM", "48 Teams · ab 2026")]           = Gruppenposition.Topf1Fest,
            [("FIFA WM", "32 Teams · 1998–2022")]         = Gruppenposition.Topf1Fest,
            [("UEFA EM", "16 Teams · 1996–2012")]         = Gruppenposition.Topf1Fest,
            [("Copa América", "12 Teams · 1993–2019")]    = Gruppenposition.Topf1Fest,
            [("CONCACAF Gold Cup", "16 Teams · ab 2021")] = Gruppenposition.Topf1Fest,
            [("FIFA Klub-WM", "32 Teams · ab 2025")]      = Gruppenposition.Topf1Fest,
            // Alle Positionen gelost (EM 2024; Gastgeber über die Setzliste fest auf A1). CL und Nations League:
            // der Spielplan entsteht erst nach der Auslosung, ohne Bindung an den Topf.
            [("UEFA EM", "24 Teams · 2016+")]                                          = Gruppenposition.AlleGelost,
            [("UEFA Champions League", "32 Teams · mit Auswärtstorregel · 2003–2021")] = Gruppenposition.AlleGelost,
            [("UEFA Champions League", "32 Teams · ohne Auswärtstorregel · 2021–2024")] = Gruppenposition.AlleGelost,
            [("UEFA Nations League Liga A", "16 Teams · ab 2024/25")]                  = Gruppenposition.AlleGelost,
        };

    private static void GruppenpositionSetzen(TurnierInstanz vorlage)
    {
        if (!GruppenpositionJeVorlage.TryGetValue((vorlage.Name, vorlage.Variante), out var position)) return;
        foreach (var runde in vorlage.Runden.Where(r => r.Rundenart == Rundenart.Gruppenphase && r.Reihenfolge == 1))
            runde.VorgegebeneGruppenposition = position;
    }

    // ── Feste Gruppenköpfe (Issue #487) ────────────────────────────────────────

    // Gruppen (A = 1) der Gastgeber und des Titelverteidigers in der Gruppenphase, nach der neuesten Ausgabe der
    // Vorlage. Ist der Titelverteidiger zugleich Gastgeber, zählt die Gastgeber-Gruppe. Bestehende Datenbanken:
    // Migration Gruppenkoepfe — dort dieselbe Zuordnung als SQL, bei Änderungen hier mitziehen.
    public static IReadOnlyDictionary<(string Name, string Variante), (int[] Gastgeber, int? Titelverteidiger)> GruppenkoepfeJeVorlage { get; } =
        new Dictionary<(string, string), (int[], int?)>
        {
            [("FIFA WM", "48 Teams · ab 2026")]           = ([1, 2, 4], null), // Mexiko A1, Kanada B1, USA D1
            [("FIFA WM", "32 Teams · 1998–2022")]         = ([1], null),       // Katar A1
            [("FIFA WM", "24 Teams · 1986–1994")]         = ([1], 3),          // USA A1, Deutschland C1
            [("FIFA WM", "24 Teams · 1982")]              = ([5], 3),          // Spanien Gruppe 5, Argentinien Gruppe 3
            [("FIFA WM", "16 Teams · 1974–1978")]         = ([1], 2),          // Argentinien Gruppe 1, Deutschland Gruppe 2
            [("FIFA WM", "16 Teams · 1958–1970")]         = ([1], 3),          // Mexiko Gruppe 1, England Gruppe 3
            [("UEFA EM", "24 Teams · 2016+")]             = ([1], null),       // Deutschland A1
            [("UEFA EM", "16 Teams · 1996–2012")]         = ([1, 4], null),    // Polen A1, Ukraine D1
            [("UEFA EM", EM_8_TEAMS_1984_BIS_1992)]       = ([1], 2),          // Schweden Gruppe 1, Niederlande Gruppe 2
            [("UEFA EM", "8 Teams · 1980")]               = ([2], 1),          // Italien Gruppe 2, ČSSR Gruppe 1
            [("Copa América", "16 Teams · 2016/2024")]    = ([3], 1),          // USA C1, Argentinien A1
            [("Copa América", "12 Teams · 1993–2019")]    = ([1], null),       // Brasilien A1
            [("CONCACAF Gold Cup", "16 Teams · ab 2021")] = ([], 1),           // Mexiko A1
            [("Afrika-Cup", "24 Teams · ab 2019")]        = ([1], null),       // Marokko A1
            [("AFC Asian Cup", "24 Teams · ab 2019")]     = ([1], null),       // Katar A1
        };

    private static void GruppenkoepfeSetzen(TurnierInstanz vorlage)
    {
        if (!GruppenkoepfeJeVorlage.TryGetValue((vorlage.Name, vorlage.Variante), out var koepfe)) return;
        foreach (var runde in vorlage.Runden.Where(r => r.Rundenart == Rundenart.Gruppenphase && r.Reihenfolge == 1))
        {
            runde.GastgeberGruppen       = [.. koepfe.Gastgeber];
            runde.TitelverteidigerGruppe = koepfe.Titelverteidiger;
        }
    }

    // ── Auslosungsart der Gruppenphase (Issue #471) ────────────────────────────

    // Reale Gruppenphasen werden aus Lostöpfen gezogen. Als Regel statt Liste, damit neue Vorlagen die Vorgabe ohne
    // eigenen Eintrag bekommen. Ausnahmen: das Mini-Turnier (kein reales Vorbild; das GUI-Szenario aus #522 lost mit
    // der bisherigen Art) und spätere Gruppenphasen wie die Zwischenrunde der WM 1982, die fest besetzt wurde (#466).
    // Bestehende Datenbanken: Migration AuslosungsartGruppenphaseNachToepfen — dort dieselbe Regel als SQL, bei
    // Änderungen hier mitziehen.
    private const string MINI_TURNIER = "Mini-Turnier";

    private static void AuslosungsartSetzen(TurnierInstanz vorlage)
    {
        if (vorlage.Name == MINI_TURNIER) return;
        foreach (var runde in vorlage.Runden.Where(r => r.Rundenart == Rundenart.Gruppenphase && r.Reihenfolge == 1))
            runde.VorgegebeneAuslosungsart ??= Auslosungsart.NachToepfen;
    }

    // ── Standard-Rangliste der Nationalmannschaften (Issue #579) ───────────────

    // Gruppenphasen von Nationalmannschafts-Wettbewerben bilden ihre Lostöpfe nach der FIFA-Weltrangliste. Als Regel
    // statt Liste wie die Auslosungsart. Bestehende Datenbanken: SeedVorlagenAbgleich.StandardRanglisteErgaenzen
    // (nur Runden ohne Standard-Rangliste).
    private static void FifaWeltranglisteSetzen(TurnierInstanz vorlage, IReadOnlyDictionary<string, int>? ranglisteLookup)
    {
        if (vorlage.MannschaftsartFilter != MannschaftsartFilter.Nationalmannschaft) return;
        int? fifa = RlId(ranglisteLookup, FifaWeltranglisteSeedDaten.RANGLISTE);
        foreach (var runde in vorlage.Runden.Where(r => r.Rundenart == Rundenart.Gruppenphase && r.Reihenfolge == 1))
            runde.StandardRanglisteId ??= fifa;
    }

    // ── Neutraler Spielort (Issue #352) ────────────────────────────────────────

    // Endrunden-Turniere finden komplett in einem Gastgeberland statt — Heimvorteil hat dort nur ein Gastgeber.
    private static readonly HashSet<string> _komplettNeutral =
        ["FIFA WM", "FIFA Klub-WM", "UEFA EM", "Copa América", "Afrika-Cup", "AFC Asian Cup", "CONCACAF Gold Cup"];

    // Europapokal-/Pokalfinale an vorab festgelegtem Ort; FA-Cup-Halbfinale seit 2008 ebenfalls in Wembley.
    private static readonly HashSet<string> _neutralesFinale =
        ["UEFA Champions League", "UEFA Europa League", "UEFA Conference League", "DFB-Pokal", "FA Cup"];

    private static void NeutraleSpielorteSetzen(TurnierInstanz vorlage)
    {
        foreach (var runde in vorlage.Runden)
            runde.NeutralerSpielort = IstNeutralerSpielort(vorlage.Name, runde);
    }

    private static bool IstNeutralerSpielort(string vorlagenName, TurnierRunde runde) => vorlagenName switch
    {
        _ when _komplettNeutral.Contains(vorlagenName) => true,
        // Final Four: Halbfinale, Spiel um Platz 3 und Finale beim Gastgeber der Endrunde
        "UEFA Nations League Liga A" => runde.Rundenart is Rundenart.Finale or Rundenart.SpielUmPlatzDrei
                                        || runde.Name == "Halbfinale",
        "FA Cup" => runde.Rundenart == Rundenart.Finale || runde.Name == "Halbfinale",
        _ when _neutralesFinale.Contains(vorlagenName) => runde.Rundenart == Rundenart.Finale,
        _ => false
    };

    // ── Überraschungsfaktor (Issue #360) ───────────────────────────────────────

    // Issue #360: „Pokal hat eigene Gesetze“ — in nationalen Pokalen gewinnen unterklassige Teams öfter, als die
    // Ratings hergeben. 20 % per Monte-Carlo gewählt: ~3,6 statt ~2,9 ausgeschiedene Bundesligisten in DFB-Pokal-
    // Runde 1 (real 1–4). Europapokale bewusst ohne Faktor — dort treffen meist ähnlich starke Teams aufeinander.
    public const int POKAL_UEBERRASCHUNGSFAKTOR = 20;

    private static readonly HashSet<string> _nationalePokale = ["DFB-Pokal", "FA Cup"];

    private static void UeberraschungsfaktorSetzen(TurnierInstanz vorlage)
    {
        if (!_nationalePokale.Contains(vorlage.Name)) return;
        foreach (var runde in vorlage.Runden)
            runde.Ueberraschungsfaktor = POKAL_UEBERRASCHUNGSFAKTOR;
    }

    // Regelwerk-Namen für nationale Ligen (Issue #350) — müssen mit RegelwerkeSeedDaten übereinstimmen.
    public const string LIGA_TORDIFFERENZ = "Nationale Liga (Tordifferenz vor direktem Vergleich)";
    public const string LIGA_DIREKTER_VERGLEICH = "Nationale Liga (direkter Vergleich vor Tordifferenz)";

    // Regelwerk-Namen der UEFA-Clubwettbewerbe (Issue #353): CL/EL/ECL ab 2024/25 nutzen das Ligaphasen-Regelwerk,
    // die Formate mit Gruppenphase das Standard-Regelwerk.
    public const string UEFA_CLUB_STANDARD = "UEFA Clubwettbewerb Standard";
    public const string UEFA_LIGAPHASE = "UEFA Clubwettbewerb Ligaphase (ab 2024/25)";

    // Regelwerk-Namen der Copa América (Issue #374): Viertelfinale, Halbfinale und Spiel um Platz 3 gehen bei
    // Unentschieden direkt ins Elfmeterschießen, nur das Finale behält das FIFA-Regelwerk mit Verlängerung.
    public const string FIFA_STANDARD_MODERN = "FIFA Standard Modern (3-Punkte)";
    public const string COPA_DIREKTES_ELFMETERSCHIESSEN = "CONMEBOL Copa América (direktes Elfmeterschießen)";

    // Regelwerk-Namen der frühen Europameisterschaften (Issue #375): bis 1992 zwei Punkte je Sieg, Verlängerung +
    // Elfmeterschießen; das Spiel um Platz 3 von 1980 ging ohne Verlängerung direkt ins Elfmeterschießen. Die
    // frühere Sammelvorlage stand auf dem Regelwerk mit Wiederholungsspiel + Los.
    public const string FIFA_HISTORISCH_2_PUNKTE = "FIFA Historisch (2-Punkte, vor 1994)";
    public const string HISTORISCH_WIEDERHOLUNGSSPIEL_LOS = "Historisch: Wiederholungsspiel + Los";
    public const string EM_1980_DIREKTES_ELFMETERSCHIESSEN = "UEFA EM 1980 (direktes Elfmeterschießen)";

    // Regelwerk-Namen der frühen Weltmeisterschaften (Issue #465): 1958–1970 entschied im K.-o. nach der
    // Verlängerung das Los, nur das Finale (und 1934/1938 jede K.-o.-Runde) wäre wiederholt worden.
    public const string WM_VERLAENGERUNG_LOS = "FIFA WM 1958–1970 (Verlängerung + Los)";
    public const string HISTORISCH_VERLAENGERUNG_WIEDERHOLUNGSSPIEL_LOS = "Historisch: Verlängerung + Wiederholungsspiel + Los";

    // Seed-Beschreibungen vor #374 (Copa América), #375 (EM mit 8 Teams), #465 (WM mit 16 Teams) und #469 (Conference
    // League) — die Nachrüstung zieht die Beschreibung nur mit, solange sie noch diesem Stand entspricht.
    public static IReadOnlyDictionary<(string Name, string Variante), string> AlteBeschreibungen { get; } =
        new Dictionary<(string Name, string Variante), string>
    {
        [("Copa América", "16 Teams · 2016/2024")] =
            "Gruppenphase (4×4) → Viertelfinale → Halbfinale → Spiel um Platz 3 + Finale; " +
            "10 CONMEBOL-Teams + 6 Gäste (CONCACAF), daher ohne Verbandsfilter",
        [("Copa América", "12 Teams · 1993–2019")] =
            "Gruppenphase (3×4, Erste/Zweite + 2 beste Dritte) → Viertelfinale → Halbfinale → " +
            "Spiel um Platz 3 + Finale; 10 CONMEBOL-Teams + 2 Gäste, daher ohne Verbandsfilter",
        [("UEFA EM", EM_8_TEAMS_1984_BIS_1992)] = "Halbfinale → Finale + Spiel um Platz 3",
        [("FIFA WM", WM_16_TEAMS_1934_BIS_1938)] = "Direktes K.-o. ab 16 Teams",
        [("UEFA Conference League", "36 Teams · ab 2024/25")] = UefaLigaphaseBeschreibung("je 8 Gegner"),
    };

    // Issue #350/#367: umbenannte Vorlagen (alter Name → neuer Name + Variante) — die Nachrüstung identifiziert
    // Vorlagen über Name + Variante, ohne Umbenennung der Bestands-Zeile würde sie ein Duplikat anlegen.
    // #367 hat den Klammerzusatz aus dem Namen in das Feld Variante verschoben.
    public static IReadOnlyDictionary<string, (string Name, string Variante)> UmbenennungenNachAltemNamen { get; } =
        new Dictionary<string, (string Name, string Variante)>
    {
        ["FIFA WM (24 Teams, 1982–1994)"]                                       = ("FIFA WM", "24 Teams · 1982"),
        ["FIFA WM (48 Teams, ab 2026)"]                                         = ("FIFA WM", "48 Teams · ab 2026"),
        ["FIFA WM (32 Teams, 1998–2022)"]                                       = ("FIFA WM", "32 Teams · 1998–2022"),
        ["FIFA WM (24 Teams, 1982)"]                                            = ("FIFA WM", "24 Teams · 1982"),
        ["FIFA WM (24 Teams, 1986–1994)"]                                       = ("FIFA WM", "24 Teams · 1986–1994"),
        ["FIFA WM (16 Teams, 1934–1978)"]                                       = ("FIFA WM", WM_16_TEAMS_1934_BIS_1938),
        ["UEFA EM (24 Teams, 2016+)"]                                           = ("UEFA EM", "24 Teams · 2016+"),
        ["UEFA EM (16 Teams, 1996–2012)"]                                       = ("UEFA EM", "16 Teams · 1996–2012"),
        ["UEFA EM (8 Teams, 1960–1992)"]                                        = ("UEFA EM", EM_8_TEAMS_1984_BIS_1992),
        ["UEFA Nations League Liga A (16 Teams, ab 2024/25)"]                   = ("UEFA Nations League Liga A", "16 Teams · ab 2024/25"),
        ["Copa América (16 Teams, 2016/2024)"]                                  = ("Copa América", "16 Teams · 2016/2024"),
        ["Copa América (12 Teams, 1993–2019)"]                                  = ("Copa América", "12 Teams · 1993–2019"),
        ["Afrika-Cup (24 Teams, ab 2019)"]                                      = ("Afrika-Cup", "24 Teams · ab 2019"),
        ["AFC Asian Cup (24 Teams, ab 2019)"]                                   = ("AFC Asian Cup", "24 Teams · ab 2019"),
        ["CONCACAF Gold Cup (16 Teams, ab 2021)"]                               = ("CONCACAF Gold Cup", "16 Teams · ab 2021"),
        ["UEFA Champions League (32 Teams, mit Auswärtstorregel, 2003–2021)"]   = ("UEFA Champions League", "32 Teams · mit Auswärtstorregel · 2003–2021"),
        ["UEFA Champions League (32 Teams, ohne Auswärtstorregel, 2021–2024)"]  = ("UEFA Champions League", "32 Teams · ohne Auswärtstorregel · 2021–2024"),
        ["FIFA Klub-WM (32 Teams, ab 2025)"]                                    = ("FIFA Klub-WM", "32 Teams · ab 2025"),
        ["DFB-Pokal (64 Teams)"]                                                = ("DFB-Pokal", "64 Teams"),
        ["FA Cup (64 Teams, ab 3. Hauptrunde)"]                                 = ("FA Cup", "64 Teams · ab 3. Hauptrunde"),
        ["Mini-Turnier (8 Teams)"]                                              = ("Mini-Turnier", "8 Teams"),
        ["Mini-Turnier (4 Teams)"]                                              = ("Mini-Turnier", "4 Teams"),
        ["Freies Turnier (benutzerdefiniert)"]                                  = ("Freies Turnier", "benutzerdefiniert"),
        ["Bundesliga (18 Teams)"]                                               = ("Bundesliga", "18 Teams"),
        ["2. Bundesliga (18 Teams)"]                                            = ("2. Bundesliga", "18 Teams"),
        ["Premier League (20 Teams)"]                                           = ("Premier League", "20 Teams"),
        ["La Liga (20 Teams)"]                                                  = ("La Liga", "20 Teams"),
        ["Serie A (20 Teams)"]                                                  = ("Serie A", "20 Teams"),
        ["Ligue 1 (18 Teams)"]                                                  = ("Ligue 1", "18 Teams"),
        ["UEFA Champions League (36 Teams, ab 2024/25)"]                        = ("UEFA Champions League", "36 Teams · ab 2024/25"),
        ["UEFA Europa League (36 Teams, ab 2024/25)"]                           = ("UEFA Europa League", "36 Teams · ab 2024/25"),
        ["UEFA Conference League (36 Teams, ab 2024/25)"]                       = ("UEFA Conference League", "36 Teams · ab 2024/25"),
    };

    // Issue #375: die frühere Vorlage "UEFA EM · 8 Teams · 1960–1992" fasste drei verschiedene Formate zusammen.
    // Die Bestands-Zeile wird zur Vorlage 1984–1992 (ihrem Aufbau am nächsten, siehe
    // TurnierVorlagenNachruestung.Em8SpielUmPlatzDreiEntfernenAsync), die Formate 1960–1976 und 1980 kommen neu hinzu.
    public const string EM_8_TEAMS_1984_BIS_1992 = "8 Teams · 1984–1992";

    // Issue #465: die frühere Vorlage "FIFA WM · 16 Teams · 1934–1978" bildete nur das reine K.-o. von 1934 und 1938
    // ab. Die Bestands-Zeile wird zur Vorlage 1934–1938, die Formate 1958–1970 und 1974–1978 kommen neu hinzu.
    public const string WM_16_TEAMS_1934_BIS_1938 = "16 Teams · 1934–1938";

    // Vorlagen, deren Variante sich seit #367 geändert hat (Name + alte Variante → neue Variante).
    public static IReadOnlyDictionary<(string Name, string Variante), string> VariantenUmbenennungen { get; } =
        new Dictionary<(string Name, string Variante), string>
    {
        [("UEFA EM", "8 Teams · 1960–1992")] = EM_8_TEAMS_1984_BIS_1992,
        [("FIFA WM", "16 Teams · 1934–1978")] = WM_16_TEAMS_1934_BIS_1938,
    };

    // ── Weltmeisterschaften ────────────────────────────────────────────────────

    // Issue #350: 12 Vierergruppen, Erste/Zweite + 8 beste Dritte → Sechzehntelfinale. Issue #372: fester Baum
    // wie WM 2026 (Spiele 73–88 in Baumreihenfolge), Dritte per Kombinationstabelle aus Annex C (495 Fälle).
    private static TurnierInstanz FifaWm48Teams(IReadOnlyDictionary<string, int>? rw)
    {
        var v = new TurnierInstanz
        {
            Status = TurnierStatus.Vorlage,
            Name = "FIFA WM",
            Variante = "48 Teams · ab 2026",
            Beschreibung = "Gruppenphase (12×4, Erste/Zweite + 8 beste Dritte) → Sechzehntelfinale → Achtelfinale → " +
                           "Viertelfinale → Halbfinale → Spiel um Platz 3 + Finale",
            ZielMannschaftsanzahl = 48,
            Kategorie = TurnierKategorie.Weltmeisterschaft,
            Startdatum = new DateOnly(2026, 1, 1),
            MannschaftsartFilter = MannschaftsartFilter.Nationalmannschaft,
        };
        int? rId = RwId(rw, "FIFA Standard Modern (3-Punkte)");
        var gruppenphase      = Runde(1, "Gruppenphase",      Rundenart.Gruppenphase, 48, 32, gruppen: 12, regelwerkId: rId);
        var sechzehntelfinale = Runde(2, "Sechzehntelfinale", Rundenart.KoRunde,      32, 16, regelwerkId: rId);
        GruppenUebergaenge(gruppenphase, sechzehntelfinale, direktProGruppe: 2, besteDritte: 8);
        v.Runden.Add(gruppenphase);
        v.Runden.Add(sechzehntelfinale);
        v.Runden.Add(Runde(3, "Achtelfinale",      Rundenart.KoRunde,          16,  8, regelwerkId: rId));
        v.Runden.Add(Runde(4, "Viertelfinale",     Rundenart.KoRunde,           8,  4, regelwerkId: rId));
        v.Runden.Add(Runde(5, "Halbfinale",        Rundenart.KoRunde,           4,  2, regelwerkId: rId));
        v.Runden.Add(Runde(6, "Spiel um Platz 3",  Rundenart.SpielUmPlatzDrei,  2,  1, regelwerkId: rId));
        v.Runden.Add(Runde(7, "Finale",            Rundenart.Finale,            2,  1, regelwerkId: rId));
        FesterKoBaum(v, KombinationstabellenSeedDaten.FifaWm2026,
            "1E-3ABCDF", "1I-3CDFGH", "2A-2B", "1F-2C", "2K-2L", "1H-2J", "1D-3BEFIJ", "1G-3AEHIJ",
            "1C-2F", "2E-2I", "1A-3CEFHI", "1L-3EHIJK", "1J-2H", "2D-2G", "1B-3EFGIJ", "1K-3DEIJL");
        return v;
    }

    private static TurnierInstanz FifaWm32Teams(IReadOnlyDictionary<string, int>? rw)
    {
        var v = new TurnierInstanz
        {
            Status = TurnierStatus.Vorlage,
            Name = "FIFA WM",
            Variante = "32 Teams · 1998–2022",
            Beschreibung = "Gruppenphase (8×4) → Achtelfinale → Viertelfinale → Halbfinale → Spiel um Platz 3 + Finale",
            ZielMannschaftsanzahl = 32,
            Kategorie = TurnierKategorie.Weltmeisterschaft,
            Startdatum = new DateOnly(1998, 1, 1),
            MannschaftsartFilter = MannschaftsartFilter.Nationalmannschaft,
        };
        int? rId = RwId(rw, "FIFA Standard Modern (3-Punkte)");
        v.Runden.Add(Runde(1, "Gruppenphase",      Rundenart.Gruppenphase,    32, 16, gruppen: 8, regelwerkId: rId));
        v.Runden.Add(Runde(2, "Achtelfinale",      Rundenart.KoRunde,          16,  8, regelwerkId: rId));
        v.Runden.Add(Runde(3, "Viertelfinale",     Rundenart.KoRunde,           8,  4, regelwerkId: rId));
        v.Runden.Add(Runde(4, "Halbfinale",        Rundenart.KoRunde,           4,  2, regelwerkId: rId));
        v.Runden.Add(Runde(5, "Spiel um Platz 3",  Rundenart.SpielUmPlatzDrei,  2,  1, regelwerkId: rId));
        v.Runden.Add(Runde(6, "Finale",            Rundenart.Finale,            2,  1, regelwerkId: rId));
        GruppenUebergaenge(v, direktProGruppe: 2);
        FesterKoBaum(v, tabelle: null, ACHT_GRUPPEN_BAUM);
        return v;
    }

    // Issue #350: die frühere Vorlage "FIFA WM (24 Teams, 1982–1994)" bildete nur das Format von 1982 ab
    // (Zwischenrunde in Dreiergruppen) — 1986–1994 lief mit Achtelfinale und 4 besten Dritten, daher getrennt.
    private static TurnierInstanz FifaWm24Teams1982(IReadOnlyDictionary<string, int>? rw)
    {
        var v = new TurnierInstanz
        {
            Status = TurnierStatus.Vorlage,
            Name = "FIFA WM",
            Variante = "24 Teams · 1982",
            Beschreibung = "Gruppenphase (6×4) → Zwischenrunde (4 Dreiergruppen, nur Gruppensieger weiter) → " +
                           "Halbfinale → Spiel um Platz 3 + Finale",
            ZielMannschaftsanzahl = 24,
            Kategorie = TurnierKategorie.Weltmeisterschaft,
            Startdatum = new DateOnly(1982, 1, 1),
            Enddatum = new DateOnly(1982, 12, 31),
            MannschaftsartFilter = MannschaftsartFilter.Nationalmannschaft,
        };
        int? rId = RwId(rw, "FIFA Historisch (2-Punkte, vor 1994)");
        v.Runden.Add(Runde(1, "Gruppenphase",      Rundenart.Gruppenphase,    24, 12, gruppen: 6, regelwerkId: rId));
        v.Runden.Add(Runde(2, "Zwischenrunde",     Rundenart.Gruppenphase,    12,  4, gruppen: 4, regelwerkId: rId));
        v.Runden.Add(Runde(3, "Halbfinale",        Rundenart.KoRunde,          4,  2, regelwerkId: rId));
        v.Runden.Add(Runde(4, "Spiel um Platz 3",  Rundenart.SpielUmPlatzDrei, 2,  1, regelwerkId: rId));
        v.Runden.Add(Runde(5, "Finale",            Rundenart.Finale,           2,  1, regelwerkId: rId));
        GruppenUebergaenge(v, direktProGruppe: 2);
        GruppenUebergaenge(v, direktProGruppe: 1, quellReihenfolge: 2);
        // Issue #466: Zwischenrunde und Halbfinale wurden nicht ausgelost (Gruppe 1–6 der Vorrunde = A–F). Die
        // Platzhalter des Halbfinales beziehen sich auf die nächstliegende Gruppenphase davor, die Zwischenrunde.
        FesteGruppen(v, v.Runden.Single(r => r.Reihenfolge == 2),
            ["1A", "1C", "2F"], ["1B", "1D", "2E"], ["2A", "2C", "1F"], ["2B", "2D", "1E"]);
        FesterKoBaum(v, tabelle: null, "1A-1C", "1B-1D");
        return v;
    }

    private static TurnierInstanz FifaWm24Teams1986Bis1994(IReadOnlyDictionary<string, int>? rw)
    {
        var v = new TurnierInstanz
        {
            Status = TurnierStatus.Vorlage,
            Name = "FIFA WM",
            Variante = "24 Teams · 1986–1994",
            Beschreibung = "Gruppenphase (6×4, Erste/Zweite + 4 beste Dritte) → Achtelfinale → Viertelfinale → " +
                           "Halbfinale → Spiel um Platz 3 + Finale; 1994 bereits mit 3-Punkte-Regel (Regelwerk ggf. umstellen)",
            ZielMannschaftsanzahl = 24,
            Kategorie = TurnierKategorie.Weltmeisterschaft,
            Startdatum = new DateOnly(1986, 1, 1),
            Enddatum = new DateOnly(1994, 12, 31),
            MannschaftsartFilter = MannschaftsartFilter.Nationalmannschaft,
        };
        SechsGruppenMitBestenDrittenUndKo(v, RwId(rw, "FIFA Historisch (2-Punkte, vor 1994)"), mitSpielUmPlatzDrei: true);
        // Issue #372: fester Baum wie WM 1994, Dritten-Zuordnung laut Reglement (identisch mit EURO 2016).
        FesterKoBaum(v, KombinationstabellenSeedDaten.SechsGruppenSchema2016,
            "1A-3CDE", "2B-2F", "1F-2E", "1B-3ACD", "1E-2D", "1C-3ABF", "1D-3BEF", "2A-2C");
        return v;
    }

    // Issue #465: nur 1934 und 1938 wurde die WM ab 16 Teams im reinen K.-o. gespielt (1938 mit 15 Teams nach einem
    // Rückzug). Unentschieden: Verlängerung, dann Wiederholungsspiel.
    private static TurnierInstanz FifaWm16Teams1934Bis1938(IReadOnlyDictionary<string, int>? rw)
    {
        var v = new TurnierInstanz
        {
            Status = TurnierStatus.Vorlage,
            Name = "FIFA WM",
            Variante = WM_16_TEAMS_1934_BIS_1938,
            Beschreibung = "Direktes K.-o. ab Achtelfinale → Viertelfinale → Halbfinale → Spiel um Platz 3 + Finale; " +
                           "bei Unentschieden Verlängerung, dann Wiederholungsspiel",
            ZielMannschaftsanzahl = 16,
            Kategorie = TurnierKategorie.Weltmeisterschaft,
            Startdatum = new DateOnly(1934, 1, 1),
            Enddatum = new DateOnly(1938, 12, 31),
            MannschaftsartFilter = MannschaftsartFilter.Nationalmannschaft,
        };
        int? rId = RwId(rw, HISTORISCH_VERLAENGERUNG_WIEDERHOLUNGSSPIEL_LOS);
        v.Runden.Add(Runde(1, "Achtelfinale",      Rundenart.KoRunde,          16,  8, regelwerkId: rId));
        v.Runden.Add(Runde(2, "Viertelfinale",     Rundenart.KoRunde,           8,  4, regelwerkId: rId));
        v.Runden.Add(Runde(3, "Halbfinale",        Rundenart.KoRunde,           4,  2, regelwerkId: rId));
        v.Runden.Add(Runde(4, "Spiel um Platz 3",  Rundenart.SpielUmPlatzDrei,  2,  1, regelwerkId: rId));
        v.Runden.Add(Runde(5, "Finale",            Rundenart.Finale,            2,  1, regelwerkId: rId));
        return v;
    }

    // Issue #465: 1958–1970 fester Baum (belegt für 1966, 1958 passt zu den Ergebnissen): Viertelfinale über Kreuz, im
    // Halbfinale treffen die Sieger aus 1A–2B und 1C–2D aufeinander. Im K.-o. nach der Verlängerung das Los, nur das
    // Finale wäre wiederholt worden. Nicht abgebildet: 1954 mit eigenem Format (je Team nur zwei Gruppenspiele, #546)
    // und das Entscheidungsspiel bei Punktgleichheit um Platz 2 von 1958 (#484).
    private static TurnierInstanz FifaWm16Teams1958Bis1970(IReadOnlyDictionary<string, int>? rw)
    {
        var v = new TurnierInstanz
        {
            Status = TurnierStatus.Vorlage,
            Name = "FIFA WM",
            Variante = "16 Teams · 1958–1970",
            Beschreibung = "Gruppenphase (4×4) → Viertelfinale (1A–2B, 1C–2D, 1B–2A, 1D–2C) → Halbfinale → " +
                           "Spiel um Platz 3 + Finale; 2 Punkte je Sieg; im K.-o. nach der Verlängerung das Los, " +
                           "im Finale ein Wiederholungsspiel",
            ZielMannschaftsanzahl = 16,
            Kategorie = TurnierKategorie.Weltmeisterschaft,
            Startdatum = new DateOnly(1958, 1, 1),
            Enddatum = new DateOnly(1970, 12, 31),
            MannschaftsartFilter = MannschaftsartFilter.Nationalmannschaft,
        };
        int? rId = RwId(rw, WM_VERLAENGERUNG_LOS);
        v.Runden.Add(Runde(1, "Gruppenphase",      Rundenart.Gruppenphase,     16,  8, gruppen: 4, regelwerkId: rId));
        v.Runden.Add(Runde(2, "Viertelfinale",     Rundenart.KoRunde,           8,  4, regelwerkId: rId));
        v.Runden.Add(Runde(3, "Halbfinale",        Rundenart.KoRunde,           4,  2, regelwerkId: rId));
        v.Runden.Add(Runde(4, "Spiel um Platz 3",  Rundenart.SpielUmPlatzDrei,  2,  1, regelwerkId: rId));
        v.Runden.Add(Runde(5, "Finale",            Rundenart.Finale,            2,  1,
                           regelwerkId: RwId(rw, HISTORISCH_VERLAENGERUNG_WIEDERHOLUNGSSPIEL_LOS)));
        GruppenUebergaenge(v, direktProGruppe: 2);
        FesterKoBaum(v, tabelle: null, "1A-2B", "1C-2D", "1B-2A", "1D-2C");
        return v;
    }

    // Issue #465: 1974 und 1978 bildeten die Gruppenersten und -zweiten zwei fest besetzte Zwischenrunden-Gruppen;
    // deren Sieger spielten das Finale, die Zweiten um Platz 3 (kein Halbfinale). Die Platzhalter von Finale und Spiel
    // um Platz 3 beziehen sich auf die Zwischenrunde (nächstliegende Gruppenphase davor, #466).
    private static TurnierInstanz FifaWm16Teams1974Bis1978(IReadOnlyDictionary<string, int>? rw)
    {
        var v = new TurnierInstanz
        {
            Status = TurnierStatus.Vorlage,
            Name = "FIFA WM",
            Variante = "16 Teams · 1974–1978",
            Beschreibung = "Gruppenphase (4×4) → Zwischenrunde (2 Vierergruppen: 1A/2B/1C/2D und 1B/2A/1D/2C) → " +
                           "Gruppensieger im Finale, Gruppenzweite im Spiel um Platz 3 (kein Halbfinale); 2 Punkte je Sieg",
            ZielMannschaftsanzahl = 16,
            Kategorie = TurnierKategorie.Weltmeisterschaft,
            Startdatum = new DateOnly(1974, 1, 1),
            Enddatum = new DateOnly(1978, 12, 31),
            MannschaftsartFilter = MannschaftsartFilter.Nationalmannschaft,
        };
        int? rId = RwId(rw, FIFA_HISTORISCH_2_PUNKTE);
        var gruppenphase  = Runde(1, "Gruppenphase",     Rundenart.Gruppenphase,     16, 8, gruppen: 4, regelwerkId: rId);
        var zwischenrunde = Runde(2, "Zwischenrunde",    Rundenart.Gruppenphase,      8, 4, gruppen: 2, regelwerkId: rId);
        var platzDrei     = Runde(3, "Spiel um Platz 3", Rundenart.SpielUmPlatzDrei,  2, 1, regelwerkId: rId);
        var finale        = Runde(4, "Finale",           Rundenart.Finale,            2, 1, regelwerkId: rId);
        GruppenUebergaenge(gruppenphase, zwischenrunde, direktProGruppe: 2);
        for (int gruppe = 1; gruppe <= 2; gruppe++)
        {
            Uebergang(zwischenrunde, finale,    gruppe, platzierungVon: 1, platzierungBis: 1);
            Uebergang(zwischenrunde, platzDrei, gruppe, platzierungVon: 2, platzierungBis: 2);
        }
        v.Runden.Add(gruppenphase);
        v.Runden.Add(zwischenrunde);
        v.Runden.Add(platzDrei);
        v.Runden.Add(finale);
        FesteGruppen(v, zwischenrunde, ["1A", "2B", "1C", "2D"], ["1B", "2A", "1D", "2C"]);
        FestePaarungen(v, platzDrei, [(GruppenPlatz("2A"), GruppenPlatz("2B"))]);
        FestePaarungen(v, finale,    [(GruppenPlatz("1A"), GruppenPlatz("1B"))]);
        return v;
    }

    // ── Europameisterschaften ──────────────────────────────────────────────────

    private static TurnierInstanz UefaEm24Teams(IReadOnlyDictionary<string, int>? rw, IReadOnlyDictionary<string, int>? verband)
    {
        var v = new TurnierInstanz
        {
            Status = TurnierStatus.Vorlage,
            Name = "UEFA EM",
            Variante = "24 Teams · 2016+",
            Beschreibung = "Gruppenphase (6×4) → Achtelfinale → Viertelfinale → Halbfinale → Finale",
            ZielMannschaftsanzahl = 24,
            Kategorie = TurnierKategorie.Kontinentalmeisterschaft,
            Startdatum = new DateOnly(2016, 1, 1),
            MannschaftsartFilter = MannschaftsartFilter.Nationalmannschaft,
            VerbandId = VId(verband, "UEFA"),
        };
        // Issue #350: Erste/Zweite + 4 beste Dritte. Issue #372: fester Baum und Dritten-Zuordnung wie EURO 2020/2024
        // (die EURO 2016 hatte noch den Baum der Afrika-Cup-/Asian-Cup-Vorlagen).
        SechsGruppenMitBestenDrittenUndKo(v, RwId(rw, "FIFA Standard Modern (3-Punkte)"), mitSpielUmPlatzDrei: false);
        FesterKoBaum(v, KombinationstabellenSeedDaten.SechsGruppenSchema2020,
            "1B-3ADEF", "1A-2C", "1F-3ABC", "2D-2E", "1E-3ABCD", "1D-2F", "1C-3DEF", "2A-2B");
        return v;
    }

    private static TurnierInstanz UefaEm16Teams(IReadOnlyDictionary<string, int>? rw, IReadOnlyDictionary<string, int>? verband)
    {
        var v = new TurnierInstanz
        {
            Status = TurnierStatus.Vorlage,
            Name = "UEFA EM",
            Variante = "16 Teams · 1996–2012",
            Beschreibung = "Gruppenphase (4×4) → Viertelfinale → Halbfinale → Finale",
            ZielMannschaftsanzahl = 16,
            Kategorie = TurnierKategorie.Kontinentalmeisterschaft,
            Startdatum = new DateOnly(1996, 1, 1),
            Enddatum = new DateOnly(2012, 12, 31),
            MannschaftsartFilter = MannschaftsartFilter.Nationalmannschaft,
            VerbandId = VId(verband, "UEFA"),
        };
        int? rId = RwId(rw, "UEFA EM 1996 (Golden Goal)");
        v.Runden.Add(Runde(1, "Gruppenphase",  Rundenart.Gruppenphase, 16,  8, gruppen: 4, regelwerkId: rId));
        v.Runden.Add(Runde(2, "Viertelfinale", Rundenart.KoRunde,       8,  4, regelwerkId: rId));
        v.Runden.Add(Runde(3, "Halbfinale",    Rundenart.KoRunde,       4,  2, regelwerkId: rId));
        v.Runden.Add(Runde(4, "Finale",        Rundenart.Finale,        2,  1, regelwerkId: rId));
        GruppenUebergaenge(v, direktProGruppe: 2);
        // Issue #372: fester Baum 1996–2012 (VF 1A–2B/1C–2D bilden HF 1, 1B–2A/1D–2C HF 2).
        FesterKoBaum(v, tabelle: null, "1A-2B", "1C-2D", "1B-2A", "1D-2C");
        return v;
    }

    // Issue #375: Format der Endrunden 1984, 1988 und 1992 — Halbfinale über Kreuz, kein Spiel um Platz 3 mehr.
    private static TurnierInstanz UefaEm8Teams1984Bis1992(IReadOnlyDictionary<string, int>? rw, IReadOnlyDictionary<string, int>? verband)
    {
        var v = new TurnierInstanz
        {
            Status = TurnierStatus.Vorlage,
            Name = "UEFA EM",
            Variante = EM_8_TEAMS_1984_BIS_1992,
            Beschreibung = "Gruppenphase (2×4) → Halbfinale (1A–2B, 1B–2A) → Finale (kein Spiel um Platz 3); " +
                           "2 Punkte je Sieg",
            ZielMannschaftsanzahl = 8,
            Kategorie = TurnierKategorie.Kontinentalmeisterschaft,
            Startdatum = new DateOnly(1984, 1, 1),
            Enddatum = new DateOnly(1992, 12, 31),
            MannschaftsartFilter = MannschaftsartFilter.Nationalmannschaft,
            VerbandId = VId(verband, "UEFA"),
        };
        int? rId = RwId(rw, FIFA_HISTORISCH_2_PUNKTE);
        v.Runden.Add(Runde(1, "Gruppenphase", Rundenart.Gruppenphase, 8, 4, gruppen: 2, regelwerkId: rId));
        v.Runden.Add(Runde(2, "Halbfinale",   Rundenart.KoRunde,      4, 2, regelwerkId: rId));
        v.Runden.Add(Runde(3, "Finale",       Rundenart.Finale,       2, 1, regelwerkId: rId));
        GruppenUebergaenge(v, direktProGruppe: 2);
        // Issue #372: Halbfinale über Kreuz wie 1984–1992.
        FesterKoBaum(v, tabelle: null, "1A-2B", "1B-2A");
        return v;
    }

    // Issue #375: die Endrunde 1980 hatte kein Halbfinale — die Gruppensieger spielten das Finale, die
    // Gruppenzweiten das Spiel um Platz 3 (ohne Verlängerung direkt ins Elfmeterschießen).
    private static TurnierInstanz UefaEm8Teams1980(IReadOnlyDictionary<string, int>? rw, IReadOnlyDictionary<string, int>? verband)
    {
        var v = new TurnierInstanz
        {
            Status = TurnierStatus.Vorlage,
            Name = "UEFA EM",
            Variante = "8 Teams · 1980",
            Beschreibung = "Gruppenphase (2×4) → Gruppensieger im Finale, Gruppenzweite im Spiel um Platz 3 " +
                           "(kein Halbfinale); 2 Punkte je Sieg; Spiel um Platz 3 bei Unentschieden direkt Elfmeterschießen",
            ZielMannschaftsanzahl = 8,
            Kategorie = TurnierKategorie.Kontinentalmeisterschaft,
            Startdatum = new DateOnly(1980, 1, 1),
            Enddatum = new DateOnly(1980, 12, 31),
            MannschaftsartFilter = MannschaftsartFilter.Nationalmannschaft,
            VerbandId = VId(verband, "UEFA"),
        };
        int? rId = RwId(rw, FIFA_HISTORISCH_2_PUNKTE);
        var gruppenphase = Runde(1, "Gruppenphase",     Rundenart.Gruppenphase,     8, 4, gruppen: 2, regelwerkId: rId);
        var platzDrei    = Runde(2, "Spiel um Platz 3", Rundenart.SpielUmPlatzDrei, 2, 1, regelwerkId: RwId(rw, EM_1980_DIREKTES_ELFMETERSCHIESSEN));
        var finale       = Runde(3, "Finale",           Rundenart.Finale,           2, 1, regelwerkId: rId);
        for (int gruppe = 1; gruppe <= 2; gruppe++)
        {
            Uebergang(gruppenphase, finale,    gruppe, platzierungVon: 1, platzierungBis: 1);
            Uebergang(gruppenphase, platzDrei, gruppe, platzierungVon: 2, platzierungBis: 2);
        }
        v.Runden.Add(gruppenphase);
        v.Runden.Add(platzDrei);
        v.Runden.Add(finale);
        FestePaarungen(v, platzDrei, [(GruppenPlatz("2A"), GruppenPlatz("2B"))]);
        FestePaarungen(v, finale,    [(GruppenPlatz("1A"), GruppenPlatz("1B"))]);
        return v;
    }

    // Issue #375: Endrunden 1960–1976 mit vier Teams, die Halbfinals wurden ausgelost. Die Qualifikation davor
    // (K.-o.-Runden mit Hin-/Rückspiel, ab 1968 mit Gruppen) ist nicht Teil der Vorlage.
    private static TurnierInstanz UefaEm4Teams(IReadOnlyDictionary<string, int>? rw, IReadOnlyDictionary<string, int>? verband)
    {
        var v = new TurnierInstanz
        {
            Status = TurnierStatus.Vorlage,
            Name = "UEFA EM",
            Variante = "4 Teams · 1960–1976",
            Beschreibung = "Endrunde: Halbfinale → Spiel um Platz 3 + Finale; Verlängerung + Elfmeterschießen (Stand 1976) – " +
                           "bis 1972 entschied nach der Verlängerung im Halbfinale der Münzwurf, im Finale ein " +
                           "Wiederholungsspiel (Regelwerk ggf. umstellen)",
            ZielMannschaftsanzahl = 4,
            Kategorie = TurnierKategorie.Kontinentalmeisterschaft,
            Startdatum = new DateOnly(1960, 1, 1),
            Enddatum = new DateOnly(1976, 12, 31),
            MannschaftsartFilter = MannschaftsartFilter.Nationalmannschaft,
            VerbandId = VId(verband, "UEFA"),
        };
        int? rId = RwId(rw, FIFA_HISTORISCH_2_PUNKTE);
        v.Runden.Add(Runde(1, "Halbfinale",       Rundenart.KoRunde,          4, 2, regelwerkId: rId));
        v.Runden.Add(Runde(2, "Spiel um Platz 3", Rundenart.SpielUmPlatzDrei, 2, 1, regelwerkId: rId));
        v.Runden.Add(Runde(3, "Finale",           Rundenart.Finale,           2, 1, regelwerkId: rId));
        return v;
    }

    // ── Weitere Nationalmannschaftsturniere (Issue #350) ───────────────────────

    // Liga A seit 2024/25: Gruppen mit Hin-/Rückspiel, Viertelfinale (Gruppensieger gegen Gruppenzweite,
    // Hin-/Rückspiel), Final Four als Einzelspiele. Ligen B–D mit Auf-/Abstieg sind turnierübergreifend (#177);
    // Gruppendritte (Relegation gegen Liga B) und -vierte (Abstieg) stehen als Tabellenbereiche in der Gruppenphase
    // (Issue #420).
    private static TurnierInstanz UefaNationsLeagueLigaA(IReadOnlyDictionary<string, int>? rw, IReadOnlyDictionary<string, int>? verband)
    {
        var v = new TurnierInstanz
        {
            Status = TurnierStatus.Vorlage,
            Name = "UEFA Nations League Liga A",
            Variante = "16 Teams · ab 2024/25",
            Beschreibung = "Gruppenphase (4×4, Hin-/Rückspiel) → Viertelfinale (Gruppensieger gegen Gruppenzweite, " +
                           "Hin-/Rückspiel) → Final Four: Halbfinale → Spiel um Platz 3 + Finale",
            ZielMannschaftsanzahl = 16,
            Kategorie = TurnierKategorie.Kontinentalmeisterschaft,
            Startdatum = new DateOnly(2024, 1, 1),
            MannschaftsartFilter = MannschaftsartFilter.Nationalmannschaft,
            VerbandId = VId(verband, "UEFA"),
        };
        int? rId = RwId(rw, "UEFA Clubwettbewerb Standard");
        var gruppenphase = Runde(1, "Gruppenphase", Rundenart.Gruppenphase, 16, 8, gruppen: 4, mitRueckspiel: true, regelwerkId: rId);
        TabellenBereicheSetzen(gruppenphase,
        [
            (3, 3, TabellenBereichArt.Relegation, "Relegation gegen Liga B"),
            (4, 4, TabellenBereichArt.Abstieg, "Abstieg in Liga B"),
        ]);
        v.Runden.Add(gruppenphase);
        v.Runden.Add(Runde(2, "Viertelfinale",    Rundenart.KoRunde,           8, 4, mitRueckspiel: true, regelwerkId: rId,
                           auslosungsart: Auslosungsart.GruppensiegerGegenGruppenzweite));
        v.Runden.Add(Runde(3, "Halbfinale",       Rundenart.KoRunde,           4, 2, regelwerkId: rId));
        v.Runden.Add(Runde(4, "Spiel um Platz 3", Rundenart.SpielUmPlatzDrei,  2, 1, regelwerkId: rId));
        v.Runden.Add(Runde(5, "Finale",           Rundenart.Finale,            2, 1, regelwerkId: rId));
        GruppenUebergaenge(v, direktProGruppe: 2);
        return v;
    }

    // Copa América: CONMEBOL hat nur 10 Mitglieder, beide Formate brauchen Gastteilnehmer aus anderen
    // Verbänden — bis #373 daher bewusst ohne Verbandsfilter.
    private static TurnierInstanz CopaAmerica16Teams(IReadOnlyDictionary<string, int>? rw)
    {
        var v = new TurnierInstanz
        {
            Status = TurnierStatus.Vorlage,
            Name = "Copa América",
            Variante = "16 Teams · 2016/2024",
            Beschreibung = "Gruppenphase (4×4) → Viertelfinale → Halbfinale → Spiel um Platz 3 + Finale; " +
                           "10 CONMEBOL-Teams + 6 Gäste (CONCACAF), daher ohne Verbandsfilter; " +
                           "bei Unentschieden im K.-o. direkt Elfmeterschießen, Verlängerung nur im Finale",
            ZielMannschaftsanzahl = 16,
            Kategorie = TurnierKategorie.Kontinentalmeisterschaft,
            Startdatum = new DateOnly(2016, 1, 1),
            MannschaftsartFilter = MannschaftsartFilter.Nationalmannschaft,
        };
        // Issue #374: 2016 und 2024 ohne Verlängerung in Viertelfinale, Halbfinale und Spiel um Platz 3.
        int? rId = RwId(rw, FIFA_STANDARD_MODERN);
        int? elfId = RwId(rw, COPA_DIREKTES_ELFMETERSCHIESSEN);
        v.Runden.Add(Runde(1, "Gruppenphase",     Rundenart.Gruppenphase,     16, 8, gruppen: 4, regelwerkId: rId));
        v.Runden.Add(Runde(2, "Viertelfinale",    Rundenart.KoRunde,           8, 4, regelwerkId: elfId));
        v.Runden.Add(Runde(3, "Halbfinale",       Rundenart.KoRunde,           4, 2, regelwerkId: elfId));
        v.Runden.Add(Runde(4, "Spiel um Platz 3", Rundenart.SpielUmPlatzDrei,  2, 1, regelwerkId: elfId));
        v.Runden.Add(Runde(5, "Finale",           Rundenart.Finale,            2, 1, regelwerkId: rId));
        GruppenUebergaenge(v, direktProGruppe: 2);
        // Issue #372: fester Baum wie Copa América 2024.
        FesterKoBaum(v, tabelle: null, "1A-2B", "1B-2A", "1C-2D", "1D-2C");
        return v;
    }

    private static TurnierInstanz CopaAmerica12Teams(IReadOnlyDictionary<string, int>? rw)
    {
        var v = new TurnierInstanz
        {
            Status = TurnierStatus.Vorlage,
            Name = "Copa América",
            Variante = "12 Teams · 1993–2019",
            Beschreibung = "Gruppenphase (3×4, Erste/Zweite + 2 beste Dritte) → Viertelfinale → Halbfinale → " +
                           "Spiel um Platz 3 + Finale; 10 CONMEBOL-Teams + 2 Gäste, daher ohne Verbandsfilter; " +
                           "bei Unentschieden im K.-o. direkt Elfmeterschießen, Verlängerung nur im Finale (Stand 2015) – " +
                           "bis 2007 auch das Finale ohne Verlängerung, 2011 alle K.-o.-Runden mit, 2019 nur das " +
                           "Viertelfinale ohne (Regelwerk je Runde ggf. umstellen)",
            ZielMannschaftsanzahl = 12,
            Kategorie = TurnierKategorie.Kontinentalmeisterschaft,
            Startdatum = new DateOnly(1993, 1, 1),
            Enddatum = new DateOnly(2019, 12, 31),
            MannschaftsartFilter = MannschaftsartFilter.Nationalmannschaft,
        };
        // Issue #374: das Reglement wechselte je Ausgabe (siehe Beschreibung) — die Vorlage bildet den Stand von
        // 2015 ab, der auch für die 16er-Vorlage gilt.
        int? rId = RwId(rw, FIFA_STANDARD_MODERN);
        int? elfId = RwId(rw, COPA_DIREKTES_ELFMETERSCHIESSEN);
        var gruppenphase  = Runde(1, "Gruppenphase",  Rundenart.Gruppenphase, 12, 8, gruppen: 3, regelwerkId: rId);
        var viertelfinale = Runde(2, "Viertelfinale", Rundenart.KoRunde,       8, 4, regelwerkId: elfId);
        GruppenUebergaenge(gruppenphase, viertelfinale, direktProGruppe: 2, besteDritte: 2);
        v.Runden.Add(gruppenphase);
        v.Runden.Add(viertelfinale);
        v.Runden.Add(Runde(3, "Halbfinale",       Rundenart.KoRunde,           4, 2, regelwerkId: elfId));
        v.Runden.Add(Runde(4, "Spiel um Platz 3", Rundenart.SpielUmPlatzDrei,  2, 1, regelwerkId: elfId));
        v.Runden.Add(Runde(5, "Finale",           Rundenart.Finale,            2, 1, regelwerkId: rId));
        // Issue #372: fester Baum und Dritten-Zuordnung wie Copa América 2019.
        FesterKoBaum(v, KombinationstabellenSeedDaten.CopaAmerica12, "1A-3BC", "2A-2B", "1B-2C", "1C-3AB");
        return v;
    }

    private static TurnierInstanz AfrikaCup24Teams(IReadOnlyDictionary<string, int>? rw, IReadOnlyDictionary<string, int>? verband)
    {
        var v = new TurnierInstanz
        {
            Status = TurnierStatus.Vorlage,
            Name = "Afrika-Cup",
            Variante = "24 Teams · ab 2019",
            Beschreibung = "Gruppenphase (6×4, Erste/Zweite + 4 beste Dritte) → Achtelfinale → Viertelfinale → " +
                           "Halbfinale → Spiel um Platz 3 + Finale",
            ZielMannschaftsanzahl = 24,
            Kategorie = TurnierKategorie.Kontinentalmeisterschaft,
            Startdatum = new DateOnly(2019, 1, 1),
            MannschaftsartFilter = MannschaftsartFilter.Nationalmannschaft,
            VerbandId = VId(verband, "CAF"),
        };
        SechsGruppenMitBestenDrittenUndKo(v, RwId(rw, "FIFA Standard Modern (3-Punkte)"), mitSpielUmPlatzDrei: true);
        // Issue #372: fester Baum wie Afrika-Cup 2023, Dritten-Zuordnung laut Reglement (identisch mit EURO 2016).
        FesterKoBaum(v, KombinationstabellenSeedDaten.SechsGruppenSchema2016, SECHS_GRUPPEN_BAUM_2016);
        return v;
    }

    private static TurnierInstanz AfcAsianCup24Teams(IReadOnlyDictionary<string, int>? rw, IReadOnlyDictionary<string, int>? verband)
    {
        var v = new TurnierInstanz
        {
            Status = TurnierStatus.Vorlage,
            Name = "AFC Asian Cup",
            Variante = "24 Teams · ab 2019",
            Beschreibung = "Gruppenphase (6×4, Erste/Zweite + 4 beste Dritte) → Achtelfinale → Viertelfinale → " +
                           "Halbfinale → Finale (kein Spiel um Platz 3)",
            ZielMannschaftsanzahl = 24,
            Kategorie = TurnierKategorie.Kontinentalmeisterschaft,
            Startdatum = new DateOnly(2019, 1, 1),
            MannschaftsartFilter = MannschaftsartFilter.Nationalmannschaft,
            VerbandId = VId(verband, "AFC"),
        };
        SechsGruppenMitBestenDrittenUndKo(v, RwId(rw, "FIFA Standard Modern (3-Punkte)"), mitSpielUmPlatzDrei: false);
        // Issue #372: fester Baum wie Asian Cup 2023, Dritten-Zuordnung laut Reglement (identisch mit EURO 2016).
        FesterKoBaum(v, KombinationstabellenSeedDaten.SechsGruppenSchema2016, SECHS_GRUPPEN_BAUM_2016);
        return v;
    }

    // Gäste aus anderen Konföderationen (z.B. Katar 2021/2023) sind erst mit #373 abbildbar.
    private static TurnierInstanz ConcacafGoldCup16Teams(IReadOnlyDictionary<string, int>? rw, IReadOnlyDictionary<string, int>? verband)
    {
        var v = new TurnierInstanz
        {
            Status = TurnierStatus.Vorlage,
            Name = "CONCACAF Gold Cup",
            Variante = "16 Teams · ab 2021",
            Beschreibung = "Gruppenphase (4×4) → Viertelfinale → Halbfinale → Finale (kein Spiel um Platz 3)",
            ZielMannschaftsanzahl = 16,
            Kategorie = TurnierKategorie.Kontinentalmeisterschaft,
            Startdatum = new DateOnly(2021, 1, 1),
            MannschaftsartFilter = MannschaftsartFilter.Nationalmannschaft,
            VerbandId = VId(verband, "CONCACAF"),
        };
        int? rId = RwId(rw, "FIFA Standard Modern (3-Punkte)");
        v.Runden.Add(Runde(1, "Gruppenphase",  Rundenart.Gruppenphase, 16, 8, gruppen: 4, regelwerkId: rId));
        v.Runden.Add(Runde(2, "Viertelfinale", Rundenart.KoRunde,       8, 4, regelwerkId: rId));
        v.Runden.Add(Runde(3, "Halbfinale",    Rundenart.KoRunde,       4, 2, regelwerkId: rId));
        v.Runden.Add(Runde(4, "Finale",        Rundenart.Finale,        2, 1, regelwerkId: rId));
        GruppenUebergaenge(v, direktProGruppe: 2);
        // Issue #372: fester Baum wie Gold Cup 2025.
        FesterKoBaum(v, tabelle: null, "1D-2A", "1B-2C", "1A-2D", "1C-2B");
        return v;
    }

    // ── UEFA Clubwettbewerbe ───────────────────────────────────────────────────

    private static TurnierInstanz UefaChampionsLeague32TeamsKlassisch(
        IReadOnlyDictionary<string, int>? rw, IReadOnlyDictionary<string, int>? verband, IReadOnlyDictionary<string, int>? rangliste)
    {
        var v = new TurnierInstanz
        {
            Status = TurnierStatus.Vorlage,
            Name = "UEFA Champions League",
            Variante = "32 Teams · mit Auswärtstorregel · 2003–2021",
            Beschreibung = "Gruppenphase (8×4) → KO-Runden (Hin-/Rückspiel, Auswärtstorregel aktiv) → Finale (Einzelspiel)",
            ZielMannschaftsanzahl = 32,
            Kategorie = TurnierKategorie.Vereinspokal,
            Startdatum = new DateOnly(2003, 1, 1),
            Enddatum = new DateOnly(2021, 12, 31),
            MannschaftsartFilter = MannschaftsartFilter.Verein,
            VerbandId = VId(verband, "UEFA"),
        };
        int? rId = RwId(rw, "UEFA Clubwettbewerb Standard");
        var gruppenphase = Runde(1, "Gruppenphase", Rundenart.Gruppenphase, 32, 16, gruppen: 8, regelwerkId: rId);
        // Töpfe nach Klub-Koeffizient (Issue #471), wie die Ligaphase der 36er-Vorlagen.
        gruppenphase.StandardRanglisteId = RlId(rangliste, KlubKoeffizientSeedDaten.RANGLISTE);
        v.Runden.Add(gruppenphase);
        v.Runden.Add(Runde(2, "Achtelfinale",  Rundenart.KoRunde,      16,  8, auswaerts: true,  mitRueckspiel: true, regelwerkId: rId,
                           auslosungsart: Auslosungsart.GruppensiegerGegenGruppenzweite));
        v.Runden.Add(Runde(3, "Viertelfinale", Rundenart.KoRunde,       8,  4, auswaerts: true,  mitRueckspiel: true, regelwerkId: rId));
        v.Runden.Add(Runde(4, "Halbfinale",    Rundenart.KoRunde,       4,  2, auswaerts: true,  mitRueckspiel: true, regelwerkId: rId));
        v.Runden.Add(Runde(5, "Finale",        Rundenart.Finale,        2,  1, regelwerkId: rId));
        GruppenUebergaenge(v, direktProGruppe: 2);
        return v;
    }

    private static TurnierInstanz UefaChampionsLeague32TeamsModern(
        IReadOnlyDictionary<string, int>? rw, IReadOnlyDictionary<string, int>? verband, IReadOnlyDictionary<string, int>? rangliste)
    {
        var v = new TurnierInstanz
        {
            Status = TurnierStatus.Vorlage,
            Name = "UEFA Champions League",
            Variante = "32 Teams · ohne Auswärtstorregel · 2021–2024",
            Beschreibung = "Gruppenphase (8×4) → KO-Runden (Hin-/Rückspiel, keine Auswärtstorregel) → Finale (Einzelspiel)",
            ZielMannschaftsanzahl = 32,
            Kategorie = TurnierKategorie.Vereinspokal,
            Startdatum = new DateOnly(2021, 1, 1),
            Enddatum = new DateOnly(2024, 12, 31),
            MannschaftsartFilter = MannschaftsartFilter.Verein,
            VerbandId = VId(verband, "UEFA"),
        };
        int? rId = RwId(rw, "UEFA Clubwettbewerb Standard");
        var gruppenphase = Runde(1, "Gruppenphase", Rundenart.Gruppenphase, 32, 16, gruppen: 8, regelwerkId: rId);
        // Töpfe nach Klub-Koeffizient (Issue #471), wie die Ligaphase der 36er-Vorlagen.
        gruppenphase.StandardRanglisteId = RlId(rangliste, KlubKoeffizientSeedDaten.RANGLISTE);
        v.Runden.Add(gruppenphase);
        v.Runden.Add(Runde(2, "Achtelfinale",  Rundenart.KoRunde,      16,  8, mitRueckspiel: true, regelwerkId: rId,
                           auslosungsart: Auslosungsart.GruppensiegerGegenGruppenzweite));
        v.Runden.Add(Runde(3, "Viertelfinale", Rundenart.KoRunde,       8,  4, mitRueckspiel: true, regelwerkId: rId));
        v.Runden.Add(Runde(4, "Halbfinale",    Rundenart.KoRunde,       4,  2, mitRueckspiel: true, regelwerkId: rId));
        v.Runden.Add(Runde(5, "Finale",        Rundenart.Finale,        2,  1, regelwerkId: rId));
        GruppenUebergaenge(v, direktProGruppe: 2);
        return v;
    }

    // ── UEFA Clubwettbewerbe (aktuelles Format, 2024+) ─────────────────────────

    // Titelverteidiger in Topf 1 nur in der Champions League (UCL-Reglement ab 2024/25); in EL/ECL besteht Topf 1
    // allein aus den neun koeffizientenstärksten Klubs (Issue #349).
    private static TurnierInstanz UefaChampionsLeague36TeamsModern(
        IReadOnlyDictionary<string, int>? rw, IReadOnlyDictionary<string, int>? verband, IReadOnlyDictionary<string, int>? rangliste) =>
        UefaClubwettbewerbModern(rw, verband, rangliste, "UEFA Champions League", "36 Teams · ab 2024/25",
            titelverteidigerInTopf1: true, gegnerJeTopf: 2);

    private static TurnierInstanz UefaEuropaLeague36Teams(
        IReadOnlyDictionary<string, int>? rw, IReadOnlyDictionary<string, int>? verband, IReadOnlyDictionary<string, int>? rangliste) =>
        UefaClubwettbewerbModern(rw, verband, rangliste, "UEFA Europa League", "36 Teams · ab 2024/25",
            titelverteidigerInTopf1: false, gegnerJeTopf: 2);

    // Conference League: 6 Gegner aus 6 Töpfen à 6, die Töpfe 1/2, 3/4, 5/6 bilden Paare mit je einem Heim- und
    // einem Auswärtsgegner (Issue #469). Bestehende Datenbanken: TurnierVorlagenNachruestung.GegnerJeTopfUebernehmen.
    private static TurnierInstanz UefaConferenceLeague36Teams(
        IReadOnlyDictionary<string, int>? rw, IReadOnlyDictionary<string, int>? verband, IReadOnlyDictionary<string, int>? rangliste) =>
        UefaClubwettbewerbModern(rw, verband, rangliste, "UEFA Conference League", "36 Teams · ab 2024/25",
            titelverteidigerInTopf1: false, gegnerJeTopf: 1);

    // Gemeinsame Struktur für CL/EL/ECL seit 2024/25 (Issue #25-Nachlieferung, freigegeben durch #29):
    // Ligaphase (36 Teams, je 8 Gegner statt voller Punktrunde) → Platz 1–8 direkt ins Achtelfinale,
    // Platz 9–24 in die zweibeinige Playoff-Runde, deren 8 Sieger die restlichen Achtelfinal-Plätze
    // füllen. Das Achtelfinale hat damit zwei Quellrunden — genau der Fall, für den strukturierte
    // Übergangsregeln (statt linearer Eingangs-/Ausgangszahl) eingeführt wurden.
    private static TurnierInstanz UefaClubwettbewerbModern(
        IReadOnlyDictionary<string, int>? rw, IReadOnlyDictionary<string, int>? verband, IReadOnlyDictionary<string, int>? rangliste,
        string name, string variante, bool titelverteidigerInTopf1, int gegnerJeTopf)
    {
        var v = new TurnierInstanz
        {
            Status = TurnierStatus.Vorlage,
            Name = name,
            Variante = variante,
            Beschreibung = UefaLigaphaseBeschreibung(gegnerJeTopf == 1 ? "je 6 Gegner aus 6 Töpfen" : "je 8 Gegner"),
            ZielMannschaftsanzahl = 36,
            Kategorie = TurnierKategorie.Vereinspokal,
            Startdatum = new DateOnly(2024, 1, 1),
            MannschaftsartFilter = MannschaftsartFilter.Verein,
            VerbandId = VId(verband, "UEFA"),
        };
        // Rangfolge der Ligaphase ohne direkten Vergleich (Issue #353). Bestehende Datenbanken:
        // TurnierVorlagenNachruestung.LigaphaseRegelwerkUmstellen.
        int? rId = RwId(rw, UEFA_LIGAPHASE);

        // Vorgegebene Auslosungsarten (Issue #347): Ligaphase per Topf-Zuordnung (je 2 Gegner aus jedem Topf, ECL je 1),
        // Playoff und Achtelfinale nach Töpfen (besser Platzierte gegen schlechter Platzierte bzw. Platz 1–8
        // gegen Playoff-Sieger), danach Zufall. Bestehende Datenbanken: Migration AuslosungsartVorgabeJeRunde.
        var liga          = Runde(1, "Ligaphase",       Rundenart.TopfLiga,        36, 24, regelwerkId: rId, auslosungsart: Auslosungsart.TopfZuordnung);
        // Töpfe der Ligaphase nach Klub-Koeffizient (Issue #349): seit #381 die Rangliste des Seed-Stands, gefunden über
        // ihren Namen.
        liga.StandardRanglisteId = RlId(rangliste, KlubKoeffizientSeedDaten.RANGLISTE);
        liga.TitelverteidigerInTopf1    = titelverteidigerInTopf1;
        liga.GegnerJeTopf               = gegnerJeTopf;
        var playoff       = Runde(2, "Playoff-Runde",   Rundenart.KoRunde,         16,  8, mitRueckspiel: true, regelwerkId: rId, auslosungsart: Auslosungsart.NachToepfen);
        var achtelfinale  = Runde(3, "Achtelfinale",    Rundenart.KoRunde,         16,  8, mitRueckspiel: true, regelwerkId: rId, auslosungsart: Auslosungsart.NachToepfen);
        var viertelfinale = Runde(4, "Viertelfinale",   Rundenart.KoRunde,          8,  4, mitRueckspiel: true, regelwerkId: rId, auslosungsart: Auslosungsart.Zufall);
        var halbfinale    = Runde(5, "Halbfinale",      Rundenart.KoRunde,          4,  2, mitRueckspiel: true, regelwerkId: rId, auslosungsart: Auslosungsart.Zufall);
        var finale        = Runde(6, "Finale",          Rundenart.Finale,           2,  1, regelwerkId: rId, auslosungsart: Auslosungsart.Zufall);

        Uebergang(liga, achtelfinale, gruppeIndex: null, platzierungVon: 1, platzierungBis: 8);
        Uebergang(liga, playoff,      gruppeIndex: null, platzierungVon: 9, platzierungBis: 24);
        for (int spiel = 1; spiel <= 8; spiel++)
            Uebergang(playoff, achtelfinale, gruppeIndex: spiel, platzierungVon: 1, platzierungBis: 1);

        v.Runden.Add(liga);
        v.Runden.Add(playoff);
        v.Runden.Add(achtelfinale);
        v.Runden.Add(viertelfinale);
        v.Runden.Add(halbfinale);
        v.Runden.Add(finale);
        return v;
    }

    private static string UefaLigaphaseBeschreibung(string gegner) =>
        $"Ligaphase (36 Teams, {gegner}) → Platz 1–8 direkt ins Achtelfinale, " +
        "Platz 9–24 in die Playoff-Runde (Hin-/Rückspiel) → Achtelfinale → Viertelfinale → " +
        "Halbfinale → Finale (Einzelspiel); keine Auswärtstorregel";

    // ── FIFA Klubwettbewerbe ───────────────────────────────────────────────────

    private static TurnierInstanz FifaKlubWm32Teams(IReadOnlyDictionary<string, int>? rw)
    {
        var v = new TurnierInstanz
        {
            Status = TurnierStatus.Vorlage,
            Name = "FIFA Klub-WM",
            Variante = "32 Teams · ab 2025",
            Beschreibung = "Gruppenphase (8×4) → Achtelfinale → Viertelfinale → Halbfinale → Finale; alle KO-Spiele als Einzelspiel",
            ZielMannschaftsanzahl = 32,
            Kategorie = TurnierKategorie.Weltmeisterschaft,
            Startdatum = new DateOnly(2025, 1, 1),
            MannschaftsartFilter = MannschaftsartFilter.Verein,
        };
        int? rId = RwId(rw, "FIFA Standard Modern (3-Punkte)");
        v.Runden.Add(Runde(1, "Gruppenphase",     Rundenart.Gruppenphase,    32, 16, gruppen: 8, regelwerkId: rId));
        v.Runden.Add(Runde(2, "Achtelfinale",     Rundenart.KoRunde,         16,  8, regelwerkId: rId));
        v.Runden.Add(Runde(3, "Viertelfinale",    Rundenart.KoRunde,          8,  4, regelwerkId: rId));
        v.Runden.Add(Runde(4, "Halbfinale",       Rundenart.KoRunde,          4,  2, regelwerkId: rId));
        v.Runden.Add(Runde(5, "Finale",           Rundenart.Finale,           2,  1, regelwerkId: rId));
        GruppenUebergaenge(v, direktProGruppe: 2);
        // Issue #372: fester Baum wie Klub-WM 2025 (gleiche Struktur wie die WM mit 32 Teams).
        FesterKoBaum(v, tabelle: null, ACHT_GRUPPEN_BAUM);
        return v;
    }

    // ── Nationale Vereinspokale ────────────────────────────────────────────────

    private static TurnierInstanz DfbPokal64Teams(
        IReadOnlyDictionary<string, int>? rw, IReadOnlyDictionary<string, int>? verband, IReadOnlyDictionary<string, int>? land,
        IReadOnlyDictionary<string, int>? rangliste)
    {
        var v = new TurnierInstanz
        {
            Status = TurnierStatus.Vorlage,
            Name = "DFB-Pokal",
            Variante = "64 Teams",
            Beschreibung = "Reines K.-o.-Format ab 64 Teams; alle Runden als Einzelspiel (kein Rückspiel)",
            ZielMannschaftsanzahl = 64,
            Kategorie = TurnierKategorie.Vereinspokal,
            MannschaftsartFilter = MannschaftsartFilter.Verein,
            // Landesfilter ist die schärfere Bedingung; VerbandId wird zusätzlich (redundant) gesetzt,
            // damit der Filter im Turnierkopf vollständig statt lückenhaft aussieht (Land impliziert immer
            // genau einen Kontinentalverband, siehe Land.VerbandId).
            LandId = LId(land, "GER"),
            VerbandId = VId(verband, "UEFA"),
        };
        int? rId = RwId(rw, "FIFA Standard Modern (3-Punkte)");
        // 1. und 2. Runde mit Profi- und Amateurtopf (#470): in der 1. Runde gleich groß nach Ligapyramide (die besten 32
        // gegen den Rest), in der 2. Runde nach Spielklasse, solange Amateure übrig sind. Ab dem Achtelfinale ein Topf.
        v.Runden.Add(Runde(1, "1. Runde",          Rundenart.KoRunde,          64, 32, regelwerkId: rId, auslosungsart: Auslosungsart.NachToepfen));
        v.Runden.Add(Runde(2, "2. Runde",          Rundenart.KoRunde,          32, 16, regelwerkId: rId, auslosungsart: Auslosungsart.NachToepfen));
        v.Runden.Add(Runde(3, "Achtelfinale",      Rundenart.KoRunde,          16,  8, regelwerkId: rId));
        v.Runden.Add(Runde(4, "Viertelfinale",     Rundenart.KoRunde,           8,  4, regelwerkId: rId));
        v.Runden.Add(Runde(5, "Halbfinale",        Rundenart.KoRunde,           4,  2, regelwerkId: rId));
        v.Runden.Add(Runde(6, "Finale",            Rundenart.Finale,            2,  1, regelwerkId: rId));

        // Spielklassen für „Heimrecht für Unterklassige“ (#393) bis einschließlich Halbfinale; das Finale ist neutral.
        int? pyramide = RlId(rangliste, LigapyramidenSeedDaten.RANGLISTE_DEUTSCHLAND);
        foreach (var runde in v.Runden.Where(r => r.Rundenart == Rundenart.KoRunde))
            runde.StandardRanglisteId = pyramide;
        return v;
    }

    // Ab der 3. Hauptrunde (Einstieg der Premier-League- und Championship-Klubs), seit 2024/25 ohne
    // Wiederholungsspiele — Verlängerung + Elfmeterschießen direkt im ersten Spiel.
    private static TurnierInstanz FaCup64Teams(
        IReadOnlyDictionary<string, int>? rw, IReadOnlyDictionary<string, int>? verband, IReadOnlyDictionary<string, int>? land,
        IReadOnlyDictionary<string, int>? rangliste)
    {
        var v = new TurnierInstanz
        {
            Status = TurnierStatus.Vorlage,
            Name = "FA Cup",
            Variante = "64 Teams · ab 3. Hauptrunde",
            Beschreibung = "Reines K.-o.-Format ab der 3. Hauptrunde; alle Runden als Einzelspiel, " +
                           "seit 2024/25 ohne Wiederholungsspiele",
            ZielMannschaftsanzahl = 64,
            Kategorie = TurnierKategorie.Vereinspokal,
            Startdatum = new DateOnly(2024, 1, 1),
            MannschaftsartFilter = MannschaftsartFilter.Verein,
            LandId = LId(land, "ENG"),
            VerbandId = VId(verband, "UEFA"),
        };
        int? rId = RwId(rw, "FIFA Standard Modern (3-Punkte)");
        v.Runden.Add(Runde(1, "3. Runde",          Rundenart.KoRunde,          64, 32, regelwerkId: rId));
        v.Runden.Add(Runde(2, "4. Runde",          Rundenart.KoRunde,          32, 16, regelwerkId: rId));
        v.Runden.Add(Runde(3, "5. Runde",          Rundenart.KoRunde,          16,  8, regelwerkId: rId));
        v.Runden.Add(Runde(4, "Viertelfinale",     Rundenart.KoRunde,           8,  4, regelwerkId: rId));
        v.Runden.Add(Runde(5, "Halbfinale",        Rundenart.KoRunde,           4,  2, regelwerkId: rId));
        v.Runden.Add(Runde(6, "Finale",            Rundenart.Finale,            2,  1, regelwerkId: rId));

        // Die Plätze außerhalb der Kontingente füllen sich nach der Ligapyramide (#575), wie im DFB-Pokal.
        int? pyramide = RlId(rangliste, LigapyramidenSeedDaten.RANGLISTE_ENGLAND);
        foreach (var runde in v.Runden.Where(r => r.Rundenart == Rundenart.KoRunde))
            runde.StandardRanglisteId = pyramide;
        return v;
    }

    // ── Nationale Ligen (Issue #350) ───────────────────────────────────────────

    // Tabellenbereiche (Issue #420) nach dem Reglement 2025/26. Europapokal-Plätze laut UEFA-Zugangsliste
    // 2024–27 für die Verbandsplätze 1–4 (ENG, ITA, ESP, GER) bzw. 5 (FRA); Zusatzplätze über die Europapokal-
    // Leistung (EPS) und Pokalsieger wechseln je Saison und bleiben außen vor.
    private static readonly (int Von, int Bis, TabellenBereichArt Art, string Bezeichnung)[] BUNDESLIGA_BEREICHE =
    [
        (1, 4, TabellenBereichArt.Weiterkommen, "Champions League"),
        (5, 5, TabellenBereichArt.Weiterkommen, "Europa League"),
        (6, 6, TabellenBereichArt.Weiterkommen, "Conference League"),
        (16, 16, TabellenBereichArt.Relegation, "Relegation"),
        (17, 18, TabellenBereichArt.Abstieg, "Abstieg"),
    ];

    private static readonly (int Von, int Bis, TabellenBereichArt Art, string Bezeichnung)[] ZWEITE_BUNDESLIGA_BEREICHE =
    [
        (1, 2, TabellenBereichArt.Weiterkommen, "Aufstieg"),
        (3, 3, TabellenBereichArt.Relegation, "Relegation (Aufstieg)"),
        (16, 16, TabellenBereichArt.Relegation, "Relegation (Abstieg)"),
        (17, 18, TabellenBereichArt.Abstieg, "Abstieg"),
    ];

    private static readonly (int Von, int Bis, TabellenBereichArt Art, string Bezeichnung)[] ZWANZIGER_LIGA_BEREICHE =
    [
        (1, 4, TabellenBereichArt.Weiterkommen, "Champions League"),
        (5, 5, TabellenBereichArt.Weiterkommen, "Europa League"),
        (6, 6, TabellenBereichArt.Weiterkommen, "Conference League"),
        (18, 20, TabellenBereichArt.Abstieg, "Abstieg"),
    ];

    private static readonly (int Von, int Bis, TabellenBereichArt Art, string Bezeichnung)[] LIGUE_1_BEREICHE =
    [
        (1, 3, TabellenBereichArt.Weiterkommen, "Champions League"),
        (4, 4, TabellenBereichArt.Weiterkommen, "Champions-League-Qualifikation"),
        (5, 5, TabellenBereichArt.Weiterkommen, "Europa League"),
        (6, 6, TabellenBereichArt.Weiterkommen, "Conference League"),
        (16, 16, TabellenBereichArt.Relegation, "Relegation"),
        (17, 18, TabellenBereichArt.Abstieg, "Abstieg"),
    ];

    // Doppelrunde jeder gegen jeden; Ausgang 1 = Meister. Europapokal-Plätze, Relegation und Auf-/Abstieg führen
    // in andere Turniere bzw. die Folgesaison (#177/#379) und stehen deshalb als Tabellenbereiche in der Runde.
    private static TurnierInstanz NationaleLiga(
        IReadOnlyDictionary<string, int>? rw, IReadOnlyDictionary<string, int>? verband, IReadOnlyDictionary<string, int>? land,
        string name, string variante, string fifaCode, int teams, string regelwerkName,
        (int Von, int Bis, TabellenBereichArt Art, string Bezeichnung)[] bereiche)
    {
        var v = new TurnierInstanz
        {
            Status = TurnierStatus.Vorlage,
            Name = name,
            Variante = variante,
            Beschreibung = $"Hin- und Rückrunde, jeder gegen jeden ({teams} Teams, {2 * (teams - 1)} Spieltage)",
            ZielMannschaftsanzahl = teams,
            Kategorie = TurnierKategorie.Liga,
            MannschaftsartFilter = MannschaftsartFilter.Verein,
            LandId = LId(land, fifaCode),
            VerbandId = VId(verband, "UEFA"),
        };
        var saison = Runde(1, "Saison", Rundenart.Liga, teams, 1, mitRueckspiel: true, regelwerkId: RwId(rw, regelwerkName));
        TabellenBereicheSetzen(saison, bereiche);
        v.Runden.Add(saison);
        return v;
    }

    private static void TabellenBereicheSetzen(
        TurnierRunde runde, (int Von, int Bis, TabellenBereichArt Art, string Bezeichnung)[] bereiche)
    {
        foreach (var (von, bis, art, bezeichnung) in bereiche)
            runde.TabellenBereiche.Add(new TabellenBereich { PlatzVon = von, PlatzBis = bis, Art = art, Bezeichnung = bezeichnung });
    }

    // ── Kleine Turniere ────────────────────────────────────────────────────────

    private static TurnierInstanz MiniTurnier8Teams(IReadOnlyDictionary<string, int>? rw)
    {
        var v = new TurnierInstanz
        {
            Status = TurnierStatus.Vorlage,
            Name = MINI_TURNIER,
            Variante = "8 Teams",
            Beschreibung = "2 Vierergruppen → Halbfinale → Finale + Spiel um Platz 3",
            ZielMannschaftsanzahl = 8,
            Kategorie = TurnierKategorie.KleinesTurnier,
        };
        int? rId = RwId(rw, "FIFA Standard Modern (3-Punkte)");
        v.Runden.Add(Runde(1, "Gruppenphase",     Rundenart.Gruppenphase,     8,  4, gruppen: 2, regelwerkId: rId));
        v.Runden.Add(Runde(2, "Halbfinale",       Rundenart.KoRunde,          4,  2, regelwerkId: rId));
        v.Runden.Add(Runde(3, "Spiel um Platz 3", Rundenart.SpielUmPlatzDrei, 2,  1, regelwerkId: rId));
        v.Runden.Add(Runde(4, "Finale",           Rundenart.Finale,           2,  1, regelwerkId: rId));
        GruppenUebergaenge(v, direktProGruppe: 2);
        return v;
    }

    private static TurnierInstanz MiniTurnier4Teams(IReadOnlyDictionary<string, int>? rw)
    {
        var v = new TurnierInstanz
        {
            Status = TurnierStatus.Vorlage,
            Name = MINI_TURNIER,
            Variante = "4 Teams",
            Beschreibung = "Liga-Runde oder direktes Halbfinale + Finale",
            ZielMannschaftsanzahl = 4,
            Kategorie = TurnierKategorie.KleinesTurnier,
        };
        int? rId = RwId(rw, "FIFA Standard Modern (3-Punkte)");
        v.Runden.Add(Runde(1, "Halbfinale",       Rundenart.KoRunde,          4,  2, regelwerkId: rId));
        v.Runden.Add(Runde(2, "Spiel um Platz 3", Rundenart.SpielUmPlatzDrei, 2,  1, regelwerkId: rId));
        v.Runden.Add(Runde(3, "Finale",           Rundenart.Finale,           2,  1, regelwerkId: rId));
        return v;
    }

    private static TurnierInstanz FreiesTurnier() => new()
    {
        Status = TurnierStatus.Vorlage,
        Name = "Freies Turnier",
        Variante = "benutzerdefiniert",
        Beschreibung = "Frei konfigurierbare Rundenstruktur",
        ZielMannschaftsanzahl = 0,
        Kategorie = TurnierKategorie.Benutzerdefiniert,
    };

    private static int? RwId(IReadOnlyDictionary<string, int>? lookup, string name)
        => lookup is not null && lookup.TryGetValue(name, out var id) ? id : null;

    // Verband-Lookup nach Kurzname (z.B. "UEFA"), Land-Lookup nach FifaCode (z.B. "GER") — analog RwId.
    private static int? VId(IReadOnlyDictionary<string, int>? lookup, string kurzname)
        => lookup is not null && lookup.TryGetValue(kurzname, out var id) ? id : null;

    private static int? LId(IReadOnlyDictionary<string, int>? lookup, string fifaCode)
        => lookup is not null && lookup.TryGetValue(fifaCode, out var id) ? id : null;

    // Rangliste nach Name, etwa die geseedete Ligapyramide (Issue #393).
    private static int? RlId(IReadOnlyDictionary<string, int>? lookup, string name)
        => lookup is not null && lookup.TryGetValue(name, out var id) ? id : null;

    private static TurnierRunde Runde(
        int reihenfolge, string name, Rundenart art,
        int eingang, int ausgang, bool auswaerts = false,
        bool mitRueckspiel = false, int? regelwerkId = null, Auslosungsart? auslosungsart = null, int? gruppen = null) => new()
    {
        Reihenfolge               = reihenfolge,
        Name                      = name,
        Rundenart                 = art,
        EingangsMannschaftsanzahl = eingang,
        AusgangsMannschaftsanzahl = ausgang,
        AuswaertstorRegelAktiv    = auswaerts,
        MitRueckspiel             = mitRueckspiel,
        GruppenAnzahl             = gruppen,
        RegelwerkId               = regelwerkId,
        VorgegebeneAuslosungsart  = auslosungsart,
    };

    // Verdrahtet eine Uebergangsregel per Objektreferenz, da Quelle/Ziel vor dem Speichern noch
    // keine reale Id haben — EF Core löst die Fremdschlüssel beim SaveChanges über den Objektgraphen auf.
    // Die Zielrunde wird dabei als "Eingang über Übergangsregeln" markiert (wie es der Runden-Editor beim
    // Speichern tut), sonst prüft TurnierValidator sie linear gegen die Vorrunde (Issue #350).
    private static void Uebergang(
        TurnierRunde quelle, TurnierRunde ziel, int? gruppeIndex, int platzierungVon, int platzierungBis, int? anzahlBeste = null)
    {
        ziel.EingangUeberUebergangsregeln = true;
        quelle.Uebergangsregeln.Add(new Uebergangsregel
        {
            QuellRunde     = quelle,
            ZielRunde      = ziel,
            GruppeIndex    = gruppeIndex,
            PlatzierungVon = platzierungVon,
            PlatzierungBis = platzierungBis,
            AnzahlBeste    = anzahlBeste,
        });
    }

    // Gruppenphase → Zielrunde: Platz 1..direktProGruppe jeder Gruppe (die Auflösung eines Gruppenrangs braucht
    // zwingend einen Gruppenindex, daher eine Regel je Gruppe) plus optional die besten Nächstplatzierten über
    // alle Gruppen hinweg (Uebergangsregel.AnzahlBeste, #184).
    private static void GruppenUebergaenge(TurnierRunde gruppenphase, TurnierRunde ziel, int direktProGruppe, int besteDritte = 0)
    {
        for (int gruppe = 1; gruppe <= gruppenphase.GruppenAnzahl!.Value; gruppe++)
            Uebergang(gruppenphase, ziel, gruppe, 1, direktProGruppe);
        if (besteDritte > 0)
            Uebergang(gruppenphase, ziel, gruppeIndex: null, direktProGruppe + 1, direktProGruppe + 1, anzahlBeste: besteDritte);
    }

    // Kurzform für Vorlagen, deren Runden direkt an v.Runden angehängt werden (Issue #358): die Gruppenphase an
    // quellReihenfolge speist die unmittelbar folgende Runde. Jede Gruppenphase bekommt Regeln, weil die
    // Tabellenzonen im Turnierbaum allein aus ihnen abgeleitet werden.
    private static void GruppenUebergaenge(TurnierInstanz v, int direktProGruppe, int quellReihenfolge = 1) =>
        GruppenUebergaenge(
            v.Runden.Single(r => r.Reihenfolge == quellReihenfolge),
            v.Runden.Single(r => r.Reihenfolge == quellReihenfolge + 1),
            direktProGruppe);

    // ── Feste K.-o.-Bäume (Issue #372) ───────────────────────────────────────

    // WM 32 (1998–2022) und Klub-WM 2025: Achtelfinale in Baumreihenfolge (Spiele 49, 50, 53, 54, 51, 52, 55, 56).
    private static readonly string[] ACHT_GRUPPEN_BAUM =
        ["1A-2B", "1C-2D", "1E-2F", "1G-2H", "1B-2A", "1D-2C", "1F-2E", "1H-2G"];

    // Afrika-Cup/Asian Cup ab 2019 (Baum wie EURO 2016): Achtelfinale in Baumreihenfolge.
    private static readonly string[] SECHS_GRUPPEN_BAUM_2016 =
        ["2A-2C", "1D-3BEF", "1B-3ACD", "1F-2E", "1E-2D", "1C-3ABF", "2B-2F", "1A-3CDE"];

    /// <summary>Hinterlegt den realen festen K.-o.-Baum als Vorbelegung: die erste K.-o.-Runde bekommt die
    /// übergebenen Paarungen in Baumreihenfolge (Kurznotation "1B-3ADEF" = "1. Gruppe B" gegen "3. Gruppe A/D/E/F"),
    /// jede weitere K.-o.-Runde "Sieger &lt;Vorrunde&gt; 1 – Sieger &lt;Vorrunde&gt; 2" usw., Spiel um Platz 3 und Finale
    /// die Verlierer bzw. Sieger des Halbfinals. Muss nach dem Anlegen aller Runden aufgerufen werden.</summary>
    private static void FesterKoBaum(TurnierInstanz v, Kombinationstabelle? tabelle, params string[] ersteRunde)
    {
        var koRunden = v.Runden.Where(r => r.Rundenart != Rundenart.Gruppenphase).OrderBy(r => r.Reihenfolge).ToList();
        var erste = koRunden.First(r => r.Rundenart == Rundenart.KoRunde);
        erste.KombinationstabelleJson = tabelle?.ZuJson();
        FestePaarungen(v, erste, ersteRunde.Select(p => p.Split('-')).Select(t => (GruppenPlatz(t[0]), GruppenPlatz(t[1]))));

        var vorrunde = erste;
        foreach (var runde in koRunden.Where(r => r.Reihenfolge > erste.Reihenfolge))
        {
            string prefix = runde.Rundenart == Rundenart.SpielUmPlatzDrei ? "Verlierer" : "Sieger";
            int spiele = vorrunde.EingangsMannschaftsanzahl / 2;
            FestePaarungen(v, runde, Enumerable.Range(0, spiele / 2)
                .Select(i => ($"{prefix} {vorrunde.Name} {2 * i + 1}", $"{prefix} {vorrunde.Name} {2 * i + 2}")));
            if (runde.Rundenart == Rundenart.KoRunde) vorrunde = runde;
        }
    }

    // "1B" → "1. Gruppe B", "3ADEF" → "3. Gruppe A/D/E/F" (Format aus PlatzhalterHilfsmittel bzw. Kombinationstabelle).
    private static string GruppenPlatz(string kurz) => $"{kurz[0]}. Gruppe {string.Join('/', kurz[1..].ToCharArray())}";

    private static void FestePaarungen(TurnierInstanz v, TurnierRunde runde, IEnumerable<(string Heim, string Auswaerts)> paarungen)
    {
        var auslosung = FesteVorbelegung(v, runde);
        int nr = 1;
        foreach (var (heim, auswaerts) in paarungen)
            auslosung.Ergebnisse.Add(new AuslosungsErgebnis
                { PaarungsNummer = nr++, HeimPlatzhalterName = heim, AuswaertsPlatzhalterName = auswaerts });
    }

    /// <summary>Fest besetzte Gruppenphase (Issue #466): je Gruppe die Plätze in Kurznotation ("1A" = "1. Gruppe A").
    /// Gruppen-Auslosungen speichern einen Eintrag je Team mit PaarungsNummer = Gruppe (siehe GruppenSpielplan).</summary>
    private static void FesteGruppen(TurnierInstanz v, TurnierRunde runde, params string[][] gruppen)
    {
        var auslosung = FesteVorbelegung(v, runde);
        for (int gruppe = 0; gruppe < gruppen.Length; gruppe++)
            foreach (var platz in gruppen[gruppe])
                auslosung.Ergebnisse.Add(new AuslosungsErgebnis
                    { PaarungsNummer = gruppe + 1, HeimPlatzhalterName = GruppenPlatz(platz) });
    }

    private static Auslosung FesteVorbelegung(TurnierInstanz v, TurnierRunde runde)
    {
        runde.PaarungFestVorgegeben = true;
        var auslosung = new Auslosung
        {
            TurnierInstanz = v,
            TurnierRunde   = runde,
            Name           = runde.Name,
            Auslosungsart  = Auslosungsart.Manuell,
            Status         = AuslosungsStatus.Abgeschlossen,
        };
        v.Auslosungen.Add(auslosung);
        return auslosung;
    }

    // Gemeinsamer Aufbau der 24er-Turniere (EM 24, WM 1986–1994, Afrika-Cup, Asian Cup): 6 Vierergruppen,
    // Erste/Zweite + 4 beste Dritte → Achtelfinale → Viertelfinale → Halbfinale → [Spiel um Platz 3] → Finale.
    private static void SechsGruppenMitBestenDrittenUndKo(TurnierInstanz v, int? rId, bool mitSpielUmPlatzDrei)
    {
        var gruppenphase = Runde(1, "Gruppenphase", Rundenart.Gruppenphase, 24, 16, gruppen: 6, regelwerkId: rId);
        var achtelfinale = Runde(2, "Achtelfinale", Rundenart.KoRunde,      16,  8, regelwerkId: rId);
        GruppenUebergaenge(gruppenphase, achtelfinale, direktProGruppe: 2, besteDritte: 4);
        v.Runden.Add(gruppenphase);
        v.Runden.Add(achtelfinale);
        v.Runden.Add(Runde(3, "Viertelfinale", Rundenart.KoRunde, 8, 4, regelwerkId: rId));
        v.Runden.Add(Runde(4, "Halbfinale",    Rundenart.KoRunde, 4, 2, regelwerkId: rId));
        if (mitSpielUmPlatzDrei)
            v.Runden.Add(Runde(5, "Spiel um Platz 3", Rundenart.SpielUmPlatzDrei, 2, 1, regelwerkId: rId));
        v.Runden.Add(Runde(mitSpielUmPlatzDrei ? 6 : 5, "Finale", Rundenart.Finale, 2, 1, regelwerkId: rId));
    }
}
