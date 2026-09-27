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
        IReadOnlyDictionary<string, int>? landLookup = null) =>
    [
        FifaWm48Teams(regelwerkLookup),
        FifaWm32Teams(regelwerkLookup),
        FifaWm24Teams1986Bis1994(regelwerkLookup),
        FifaWm24Teams1982(regelwerkLookup),
        FifaWm16Teams(regelwerkLookup),
        FifaKlubWm32Teams(regelwerkLookup),
        UefaEm24Teams(regelwerkLookup, verbandLookup),
        UefaEm16Teams(regelwerkLookup, verbandLookup),
        UefaEm8Teams(regelwerkLookup, verbandLookup),
        UefaNationsLeagueLigaA(regelwerkLookup, verbandLookup),
        CopaAmerica16Teams(regelwerkLookup),
        CopaAmerica12Teams(regelwerkLookup),
        AfrikaCup24Teams(regelwerkLookup, verbandLookup),
        AfcAsianCup24Teams(regelwerkLookup, verbandLookup),
        ConcacafGoldCup16Teams(regelwerkLookup, verbandLookup),
        UefaChampionsLeague32TeamsKlassisch(regelwerkLookup, verbandLookup),
        UefaChampionsLeague32TeamsModern(regelwerkLookup, verbandLookup),
        UefaChampionsLeague36TeamsModern(regelwerkLookup, verbandLookup),
        UefaEuropaLeague36Teams(regelwerkLookup, verbandLookup),
        UefaConferenceLeague36Teams(regelwerkLookup, verbandLookup),
        DfbPokal64Teams(regelwerkLookup, verbandLookup, landLookup),
        FaCup64Teams(regelwerkLookup, verbandLookup, landLookup),
        NationaleLiga(regelwerkLookup, verbandLookup, landLookup, "Bundesliga (18 Teams)",     "GER", 18, LIGA_TORDIFFERENZ),
        NationaleLiga(regelwerkLookup, verbandLookup, landLookup, "2. Bundesliga (18 Teams)",  "GER", 18, LIGA_TORDIFFERENZ),
        NationaleLiga(regelwerkLookup, verbandLookup, landLookup, "Premier League (20 Teams)", "ENG", 20, LIGA_TORDIFFERENZ),
        NationaleLiga(regelwerkLookup, verbandLookup, landLookup, "La Liga (20 Teams)",        "ESP", 20, LIGA_DIREKTER_VERGLEICH),
        NationaleLiga(regelwerkLookup, verbandLookup, landLookup, "Serie A (20 Teams)",        "ITA", 20, LIGA_DIREKTER_VERGLEICH),
        NationaleLiga(regelwerkLookup, verbandLookup, landLookup, "Ligue 1 (18 Teams)",        "FRA", 18, LIGA_TORDIFFERENZ),
        MiniTurnier8Teams(regelwerkLookup),
        MiniTurnier4Teams(regelwerkLookup),
        FreiesTurnier(),
    ];

    // Regelwerk-Namen für nationale Ligen (Issue #350) — müssen mit RegelwerkeSeedDaten übereinstimmen.
    public const string LIGA_TORDIFFERENZ = "Nationale Liga (Tordifferenz vor direktem Vergleich)";
    public const string LIGA_DIREKTER_VERGLEICH = "Nationale Liga (direkter Vergleich vor Tordifferenz)";

    // Issue #350: umbenannte Vorlagen (alter Name → neuer Name) — die Nachrüstung identifiziert Vorlagen über
    // den Namen, ohne Umbenennung der Bestands-Zeile würde sie unter neuem Namen ein Duplikat anlegen.
    public static IReadOnlyDictionary<string, string> UmbenennungenNachAltemNamen { get; } = new Dictionary<string, string>
    {
        ["FIFA WM (24 Teams, 1982–1994)"] = "FIFA WM (24 Teams, 1982)",
    };

    // Für Nachrüstung bestehender DBs: Name → Runden-Blueprint
    public static Dictionary<string, List<TurnierRunde>> RundenNachVorlagenName(IReadOnlyDictionary<string, int>? regelwerkLookup = null)
        => Erstellen(regelwerkLookup)
            .Where(v => v.Runden.Count > 0)
            .ToDictionary(v => v.Name, v => v.Runden.ToList());

    // Für Nachrüstung bestehender DBs: Name → Soll-Filterwerte (VerbandId, LandId, MannschaftsartFilter)
    public static Dictionary<string, (int? VerbandId, int? LandId, MannschaftsartFilter MannschaftsartFilter)> FilterNachVorlagenName(
        IReadOnlyDictionary<string, int>? verbandLookup = null, IReadOnlyDictionary<string, int>? landLookup = null)
        => Erstellen(verbandLookup: verbandLookup, landLookup: landLookup)
            .ToDictionary(v => v.Name, v => (v.VerbandId, v.LandId, v.MannschaftsartFilter));

    // ── Weltmeisterschaften ────────────────────────────────────────────────────

    // Issue #350: 12 Vierergruppen, Erste/Zweite + 8 beste Dritte → Sechzehntelfinale. Die reale feste
    // Baum-Zuordnung der Dritten per Kombinationstabelle (495 Fälle) ist noch nicht abbildbar (#372) —
    // das Sechzehntelfinale wird bis dahin ausgelost.
    private static TurnierInstanz FifaWm48Teams(IReadOnlyDictionary<string, int>? rw)
    {
        var v = new TurnierInstanz
        {
            Status = TurnierStatus.Vorlage,
            Name = "FIFA WM (48 Teams, ab 2026)",
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
        return v;
    }

    private static TurnierInstanz FifaWm32Teams(IReadOnlyDictionary<string, int>? rw)
    {
        var v = new TurnierInstanz
        {
            Status = TurnierStatus.Vorlage,
            Name = "FIFA WM (32 Teams, 1998–2022)",
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
        return v;
    }

    // Issue #350: die frühere Vorlage "FIFA WM (24 Teams, 1982–1994)" bildete nur das Format von 1982 ab
    // (Zwischenrunde in Dreiergruppen) — 1986–1994 lief mit Achtelfinale und 4 besten Dritten, daher getrennt.
    private static TurnierInstanz FifaWm24Teams1982(IReadOnlyDictionary<string, int>? rw)
    {
        var v = new TurnierInstanz
        {
            Status = TurnierStatus.Vorlage,
            Name = "FIFA WM (24 Teams, 1982)",
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
        return v;
    }

    private static TurnierInstanz FifaWm24Teams1986Bis1994(IReadOnlyDictionary<string, int>? rw)
    {
        var v = new TurnierInstanz
        {
            Status = TurnierStatus.Vorlage,
            Name = "FIFA WM (24 Teams, 1986–1994)",
            Beschreibung = "Gruppenphase (6×4, Erste/Zweite + 4 beste Dritte) → Achtelfinale → Viertelfinale → " +
                           "Halbfinale → Spiel um Platz 3 + Finale; 1994 bereits mit 3-Punkte-Regel (Regelwerk ggf. umstellen)",
            ZielMannschaftsanzahl = 24,
            Kategorie = TurnierKategorie.Weltmeisterschaft,
            Startdatum = new DateOnly(1986, 1, 1),
            Enddatum = new DateOnly(1994, 12, 31),
            MannschaftsartFilter = MannschaftsartFilter.Nationalmannschaft,
        };
        SechsGruppenMitBestenDrittenUndKo(v, RwId(rw, "FIFA Historisch (2-Punkte, vor 1994)"), mitSpielUmPlatzDrei: true);
        return v;
    }

    private static TurnierInstanz FifaWm16Teams(IReadOnlyDictionary<string, int>? rw)
    {
        var v = new TurnierInstanz
        {
            Status = TurnierStatus.Vorlage,
            Name = "FIFA WM (16 Teams, 1934–1978)",
            Beschreibung = "Direktes K.-o. ab 16 Teams",
            ZielMannschaftsanzahl = 16,
            Kategorie = TurnierKategorie.Weltmeisterschaft,
            Startdatum = new DateOnly(1934, 1, 1),
            Enddatum = new DateOnly(1978, 12, 31),
            MannschaftsartFilter = MannschaftsartFilter.Nationalmannschaft,
        };
        int? rId = RwId(rw, "Historisch: Wiederholungsspiel + Los");
        v.Runden.Add(Runde(1, "Achtelfinale",      Rundenart.KoRunde,          16,  8, regelwerkId: rId));
        v.Runden.Add(Runde(2, "Viertelfinale",     Rundenart.KoRunde,           8,  4, regelwerkId: rId));
        v.Runden.Add(Runde(3, "Halbfinale",        Rundenart.KoRunde,           4,  2, regelwerkId: rId));
        v.Runden.Add(Runde(4, "Spiel um Platz 3",  Rundenart.SpielUmPlatzDrei,  2,  1, regelwerkId: rId));
        v.Runden.Add(Runde(5, "Finale",            Rundenart.Finale,            2,  1, regelwerkId: rId));
        return v;
    }

    // ── Europameisterschaften ──────────────────────────────────────────────────

    private static TurnierInstanz UefaEm24Teams(IReadOnlyDictionary<string, int>? rw, IReadOnlyDictionary<string, int>? verband)
    {
        var v = new TurnierInstanz
        {
            Status = TurnierStatus.Vorlage,
            Name = "UEFA EM (24 Teams, 2016+)",
            Beschreibung = "Gruppenphase (6×4) → Achtelfinale → Viertelfinale → Halbfinale → Finale",
            ZielMannschaftsanzahl = 24,
            Kategorie = TurnierKategorie.Kontinentalmeisterschaft,
            Startdatum = new DateOnly(2016, 1, 1),
            MannschaftsartFilter = MannschaftsartFilter.Nationalmannschaft,
            VerbandId = VId(verband, "UEFA"),
        };
        // Issue #350: Erste/Zweite + 4 beste Dritte; die feste Baum-Zuordnung der Dritten folgt mit #372.
        SechsGruppenMitBestenDrittenUndKo(v, RwId(rw, "FIFA Standard Modern (3-Punkte)"), mitSpielUmPlatzDrei: false);
        return v;
    }

    private static TurnierInstanz UefaEm16Teams(IReadOnlyDictionary<string, int>? rw, IReadOnlyDictionary<string, int>? verband)
    {
        var v = new TurnierInstanz
        {
            Status = TurnierStatus.Vorlage,
            Name = "UEFA EM (16 Teams, 1996–2012)",
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
        return v;
    }

    private static TurnierInstanz UefaEm8Teams(IReadOnlyDictionary<string, int>? rw, IReadOnlyDictionary<string, int>? verband)
    {
        var v = new TurnierInstanz
        {
            Status = TurnierStatus.Vorlage,
            Name = "UEFA EM (8 Teams, 1960–1992)",
            Beschreibung = "Halbfinale → Finale + Spiel um Platz 3",
            ZielMannschaftsanzahl = 8,
            Kategorie = TurnierKategorie.Kontinentalmeisterschaft,
            Startdatum = new DateOnly(1960, 1, 1),
            Enddatum = new DateOnly(1992, 12, 31),
            MannschaftsartFilter = MannschaftsartFilter.Nationalmannschaft,
            VerbandId = VId(verband, "UEFA"),
        };
        int? rId = RwId(rw, "Historisch: Wiederholungsspiel + Los");
        v.Runden.Add(Runde(1, "Gruppenphase",     Rundenart.Gruppenphase,     8,  4, gruppen: 2, regelwerkId: rId));
        v.Runden.Add(Runde(2, "Halbfinale",       Rundenart.KoRunde,          4,  2, regelwerkId: rId));
        v.Runden.Add(Runde(3, "Spiel um Platz 3", Rundenart.SpielUmPlatzDrei, 2,  1, regelwerkId: rId));
        v.Runden.Add(Runde(4, "Finale",           Rundenart.Finale,           2,  1, regelwerkId: rId));
        return v;
    }

    // ── Weitere Nationalmannschaftsturniere (Issue #350) ───────────────────────

    // Liga A seit 2024/25: Gruppen mit Hin-/Rückspiel, Viertelfinale (Gruppensieger gegen Gruppenzweite,
    // Hin-/Rückspiel), Final Four als Einzelspiele. Ligen B–D mit Auf-/Abstieg sind turnierübergreifend (#177).
    private static TurnierInstanz UefaNationsLeagueLigaA(IReadOnlyDictionary<string, int>? rw, IReadOnlyDictionary<string, int>? verband)
    {
        var v = new TurnierInstanz
        {
            Status = TurnierStatus.Vorlage,
            Name = "UEFA Nations League Liga A (16 Teams, ab 2024/25)",
            Beschreibung = "Gruppenphase (4×4, Hin-/Rückspiel) → Viertelfinale (Gruppensieger gegen Gruppenzweite, " +
                           "Hin-/Rückspiel) → Final Four: Halbfinale → Spiel um Platz 3 + Finale",
            ZielMannschaftsanzahl = 16,
            Kategorie = TurnierKategorie.Kontinentalmeisterschaft,
            Startdatum = new DateOnly(2024, 1, 1),
            MannschaftsartFilter = MannschaftsartFilter.Nationalmannschaft,
            VerbandId = VId(verband, "UEFA"),
        };
        int? rId = RwId(rw, "UEFA Clubwettbewerb Standard");
        v.Runden.Add(Runde(1, "Gruppenphase",     Rundenart.Gruppenphase,     16, 8, gruppen: 4, mitRueckspiel: true, regelwerkId: rId));
        v.Runden.Add(Runde(2, "Viertelfinale",    Rundenart.KoRunde,           8, 4, mitRueckspiel: true, regelwerkId: rId,
                           auslosungsart: Auslosungsart.GruppensiegerGegenGruppenzweite));
        v.Runden.Add(Runde(3, "Halbfinale",       Rundenart.KoRunde,           4, 2, regelwerkId: rId));
        v.Runden.Add(Runde(4, "Spiel um Platz 3", Rundenart.SpielUmPlatzDrei,  2, 1, regelwerkId: rId));
        v.Runden.Add(Runde(5, "Finale",           Rundenart.Finale,            2, 1, regelwerkId: rId));
        return v;
    }

    // Copa América: CONMEBOL hat nur 10 Mitglieder, beide Formate brauchen Gastteilnehmer aus anderen
    // Verbänden — bis #373 daher bewusst ohne Verbandsfilter.
    private static TurnierInstanz CopaAmerica16Teams(IReadOnlyDictionary<string, int>? rw)
    {
        var v = new TurnierInstanz
        {
            Status = TurnierStatus.Vorlage,
            Name = "Copa América (16 Teams, 2016/2024)",
            Beschreibung = "Gruppenphase (4×4) → Viertelfinale → Halbfinale → Spiel um Platz 3 + Finale; " +
                           "10 CONMEBOL-Teams + 6 Gäste (CONCACAF), daher ohne Verbandsfilter",
            ZielMannschaftsanzahl = 16,
            Kategorie = TurnierKategorie.Kontinentalmeisterschaft,
            Startdatum = new DateOnly(2016, 1, 1),
            MannschaftsartFilter = MannschaftsartFilter.Nationalmannschaft,
        };
        int? rId = RwId(rw, "FIFA Standard Modern (3-Punkte)");
        v.Runden.Add(Runde(1, "Gruppenphase",     Rundenart.Gruppenphase,     16, 8, gruppen: 4, regelwerkId: rId));
        v.Runden.Add(Runde(2, "Viertelfinale",    Rundenart.KoRunde,           8, 4, regelwerkId: rId));
        v.Runden.Add(Runde(3, "Halbfinale",       Rundenart.KoRunde,           4, 2, regelwerkId: rId));
        v.Runden.Add(Runde(4, "Spiel um Platz 3", Rundenart.SpielUmPlatzDrei,  2, 1, regelwerkId: rId));
        v.Runden.Add(Runde(5, "Finale",           Rundenart.Finale,            2, 1, regelwerkId: rId));
        return v;
    }

    private static TurnierInstanz CopaAmerica12Teams(IReadOnlyDictionary<string, int>? rw)
    {
        var v = new TurnierInstanz
        {
            Status = TurnierStatus.Vorlage,
            Name = "Copa América (12 Teams, 1993–2019)",
            Beschreibung = "Gruppenphase (3×4, Erste/Zweite + 2 beste Dritte) → Viertelfinale → Halbfinale → " +
                           "Spiel um Platz 3 + Finale; 10 CONMEBOL-Teams + 2 Gäste, daher ohne Verbandsfilter",
            ZielMannschaftsanzahl = 12,
            Kategorie = TurnierKategorie.Kontinentalmeisterschaft,
            Startdatum = new DateOnly(1993, 1, 1),
            Enddatum = new DateOnly(2019, 12, 31),
            MannschaftsartFilter = MannschaftsartFilter.Nationalmannschaft,
        };
        int? rId = RwId(rw, "FIFA Standard Modern (3-Punkte)");
        var gruppenphase  = Runde(1, "Gruppenphase",  Rundenart.Gruppenphase, 12, 8, gruppen: 3, regelwerkId: rId);
        var viertelfinale = Runde(2, "Viertelfinale", Rundenart.KoRunde,       8, 4, regelwerkId: rId);
        GruppenUebergaenge(gruppenphase, viertelfinale, direktProGruppe: 2, besteDritte: 2);
        v.Runden.Add(gruppenphase);
        v.Runden.Add(viertelfinale);
        v.Runden.Add(Runde(3, "Halbfinale",       Rundenart.KoRunde,           4, 2, regelwerkId: rId));
        v.Runden.Add(Runde(4, "Spiel um Platz 3", Rundenart.SpielUmPlatzDrei,  2, 1, regelwerkId: rId));
        v.Runden.Add(Runde(5, "Finale",           Rundenart.Finale,            2, 1, regelwerkId: rId));
        return v;
    }

    private static TurnierInstanz AfrikaCup24Teams(IReadOnlyDictionary<string, int>? rw, IReadOnlyDictionary<string, int>? verband)
    {
        var v = new TurnierInstanz
        {
            Status = TurnierStatus.Vorlage,
            Name = "Afrika-Cup (24 Teams, ab 2019)",
            Beschreibung = "Gruppenphase (6×4, Erste/Zweite + 4 beste Dritte) → Achtelfinale → Viertelfinale → " +
                           "Halbfinale → Spiel um Platz 3 + Finale",
            ZielMannschaftsanzahl = 24,
            Kategorie = TurnierKategorie.Kontinentalmeisterschaft,
            Startdatum = new DateOnly(2019, 1, 1),
            MannschaftsartFilter = MannschaftsartFilter.Nationalmannschaft,
            VerbandId = VId(verband, "CAF"),
        };
        SechsGruppenMitBestenDrittenUndKo(v, RwId(rw, "FIFA Standard Modern (3-Punkte)"), mitSpielUmPlatzDrei: true);
        return v;
    }

    private static TurnierInstanz AfcAsianCup24Teams(IReadOnlyDictionary<string, int>? rw, IReadOnlyDictionary<string, int>? verband)
    {
        var v = new TurnierInstanz
        {
            Status = TurnierStatus.Vorlage,
            Name = "AFC Asian Cup (24 Teams, ab 2019)",
            Beschreibung = "Gruppenphase (6×4, Erste/Zweite + 4 beste Dritte) → Achtelfinale → Viertelfinale → " +
                           "Halbfinale → Finale (kein Spiel um Platz 3)",
            ZielMannschaftsanzahl = 24,
            Kategorie = TurnierKategorie.Kontinentalmeisterschaft,
            Startdatum = new DateOnly(2019, 1, 1),
            MannschaftsartFilter = MannschaftsartFilter.Nationalmannschaft,
            VerbandId = VId(verband, "AFC"),
        };
        SechsGruppenMitBestenDrittenUndKo(v, RwId(rw, "FIFA Standard Modern (3-Punkte)"), mitSpielUmPlatzDrei: false);
        return v;
    }

    // Gäste aus anderen Konföderationen (z.B. Katar 2021/2023) sind erst mit #373 abbildbar.
    private static TurnierInstanz ConcacafGoldCup16Teams(IReadOnlyDictionary<string, int>? rw, IReadOnlyDictionary<string, int>? verband)
    {
        var v = new TurnierInstanz
        {
            Status = TurnierStatus.Vorlage,
            Name = "CONCACAF Gold Cup (16 Teams, ab 2021)",
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
        return v;
    }

    // ── UEFA Clubwettbewerbe ───────────────────────────────────────────────────

    private static TurnierInstanz UefaChampionsLeague32TeamsKlassisch(IReadOnlyDictionary<string, int>? rw, IReadOnlyDictionary<string, int>? verband)
    {
        var v = new TurnierInstanz
        {
            Status = TurnierStatus.Vorlage,
            Name = "UEFA Champions League (32 Teams, mit Auswärtstorregel, 2003–2021)",
            Beschreibung = "Gruppenphase (8×4) → KO-Runden (Hin-/Rückspiel, Auswärtstorregel aktiv) → Finale (Einzelspiel)",
            ZielMannschaftsanzahl = 32,
            Kategorie = TurnierKategorie.Vereinspokal,
            Startdatum = new DateOnly(2003, 1, 1),
            Enddatum = new DateOnly(2021, 12, 31),
            MannschaftsartFilter = MannschaftsartFilter.Verein,
            VerbandId = VId(verband, "UEFA"),
        };
        int? rId = RwId(rw, "UEFA Clubwettbewerb Standard");
        v.Runden.Add(Runde(1, "Gruppenphase",  Rundenart.Gruppenphase, 32, 16, gruppen: 8, regelwerkId: rId));
        v.Runden.Add(Runde(2, "Achtelfinale",  Rundenart.KoRunde,      16,  8, auswaerts: true,  mitRueckspiel: true, regelwerkId: rId));
        v.Runden.Add(Runde(3, "Viertelfinale", Rundenart.KoRunde,       8,  4, auswaerts: true,  mitRueckspiel: true, regelwerkId: rId));
        v.Runden.Add(Runde(4, "Halbfinale",    Rundenart.KoRunde,       4,  2, auswaerts: true,  mitRueckspiel: true, regelwerkId: rId));
        v.Runden.Add(Runde(5, "Finale",        Rundenart.Finale,        2,  1, regelwerkId: rId));
        return v;
    }

    private static TurnierInstanz UefaChampionsLeague32TeamsModern(IReadOnlyDictionary<string, int>? rw, IReadOnlyDictionary<string, int>? verband)
    {
        var v = new TurnierInstanz
        {
            Status = TurnierStatus.Vorlage,
            Name = "UEFA Champions League (32 Teams, ohne Auswärtstorregel, 2021–2024)",
            Beschreibung = "Gruppenphase (8×4) → KO-Runden (Hin-/Rückspiel, keine Auswärtstorregel) → Finale (Einzelspiel)",
            ZielMannschaftsanzahl = 32,
            Kategorie = TurnierKategorie.Vereinspokal,
            Startdatum = new DateOnly(2021, 1, 1),
            Enddatum = new DateOnly(2024, 12, 31),
            MannschaftsartFilter = MannschaftsartFilter.Verein,
            VerbandId = VId(verband, "UEFA"),
        };
        int? rId = RwId(rw, "UEFA Clubwettbewerb Standard");
        v.Runden.Add(Runde(1, "Gruppenphase",  Rundenart.Gruppenphase, 32, 16, gruppen: 8, regelwerkId: rId));
        v.Runden.Add(Runde(2, "Achtelfinale",  Rundenart.KoRunde,      16,  8, mitRueckspiel: true, regelwerkId: rId));
        v.Runden.Add(Runde(3, "Viertelfinale", Rundenart.KoRunde,       8,  4, mitRueckspiel: true, regelwerkId: rId));
        v.Runden.Add(Runde(4, "Halbfinale",    Rundenart.KoRunde,       4,  2, mitRueckspiel: true, regelwerkId: rId));
        v.Runden.Add(Runde(5, "Finale",        Rundenart.Finale,        2,  1, regelwerkId: rId));
        return v;
    }

    // ── UEFA Clubwettbewerbe (aktuelles Format, 2024+) ─────────────────────────

    private static TurnierInstanz UefaChampionsLeague36TeamsModern(IReadOnlyDictionary<string, int>? rw, IReadOnlyDictionary<string, int>? verband) =>
        UefaClubwettbewerbModern(rw, verband, "UEFA Champions League (36 Teams, ab 2024/25)");

    private static TurnierInstanz UefaEuropaLeague36Teams(IReadOnlyDictionary<string, int>? rw, IReadOnlyDictionary<string, int>? verband) =>
        UefaClubwettbewerbModern(rw, verband, "UEFA Europa League (36 Teams, ab 2024/25)");

    private static TurnierInstanz UefaConferenceLeague36Teams(IReadOnlyDictionary<string, int>? rw, IReadOnlyDictionary<string, int>? verband) =>
        UefaClubwettbewerbModern(rw, verband, "UEFA Conference League (36 Teams, ab 2024/25)");

    // Gemeinsame Struktur für CL/EL/ECL seit 2024/25 (Issue #25-Nachlieferung, freigegeben durch #29):
    // Ligaphase (36 Teams, je 8 Gegner statt voller Punktrunde) → Platz 1–8 direkt ins Achtelfinale,
    // Platz 9–24 in die zweibeinige Playoff-Runde, deren 8 Sieger die restlichen Achtelfinal-Plätze
    // füllen. Das Achtelfinale hat damit zwei Quellrunden — genau der Fall, für den strukturierte
    // Übergangsregeln (statt linearer Eingangs-/Ausgangszahl) eingeführt wurden.
    private static TurnierInstanz UefaClubwettbewerbModern(IReadOnlyDictionary<string, int>? rw, IReadOnlyDictionary<string, int>? verband, string name)
    {
        var v = new TurnierInstanz
        {
            Status = TurnierStatus.Vorlage,
            Name = name,
            Beschreibung = "Ligaphase (36 Teams, je 8 Gegner) → Platz 1–8 direkt ins Achtelfinale, " +
                           "Platz 9–24 in die Playoff-Runde (Hin-/Rückspiel) → Achtelfinale → Viertelfinale → " +
                           "Halbfinale → Finale (Einzelspiel); keine Auswärtstorregel",
            ZielMannschaftsanzahl = 36,
            Kategorie = TurnierKategorie.Vereinspokal,
            Startdatum = new DateOnly(2024, 1, 1),
            MannschaftsartFilter = MannschaftsartFilter.Verein,
            VerbandId = VId(verband, "UEFA"),
        };
        int? rId = RwId(rw, "UEFA Clubwettbewerb Standard");

        // Vorgegebene Auslosungsarten (Issue #347): Ligaphase per Topf-Zuordnung (je 2 Gegner aus jedem Topf),
        // Playoff und Achtelfinale nach Töpfen (besser Platzierte gegen schlechter Platzierte bzw. Platz 1–8
        // gegen Playoff-Sieger), danach Zufall. Bestehende Datenbanken: Migration AuslosungsartVorgabeJeRunde.
        var liga          = Runde(1, "Ligaphase",       Rundenart.TopfLiga,        36, 24, regelwerkId: rId, auslosungsart: Auslosungsart.TopfZuordnung);
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

    // ── FIFA Klubwettbewerbe ───────────────────────────────────────────────────

    private static TurnierInstanz FifaKlubWm32Teams(IReadOnlyDictionary<string, int>? rw)
    {
        var v = new TurnierInstanz
        {
            Status = TurnierStatus.Vorlage,
            Name = "FIFA Klub-WM (32 Teams, ab 2025)",
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
        return v;
    }

    // ── Nationale Vereinspokale ────────────────────────────────────────────────

    private static TurnierInstanz DfbPokal64Teams(
        IReadOnlyDictionary<string, int>? rw, IReadOnlyDictionary<string, int>? verband, IReadOnlyDictionary<string, int>? land)
    {
        var v = new TurnierInstanz
        {
            Status = TurnierStatus.Vorlage,
            Name = "DFB-Pokal (64 Teams)",
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
        v.Runden.Add(Runde(1, "1. Runde",          Rundenart.KoRunde,          64, 32, regelwerkId: rId));
        v.Runden.Add(Runde(2, "2. Runde",          Rundenart.KoRunde,          32, 16, regelwerkId: rId));
        v.Runden.Add(Runde(3, "Achtelfinale",      Rundenart.KoRunde,          16,  8, regelwerkId: rId));
        v.Runden.Add(Runde(4, "Viertelfinale",     Rundenart.KoRunde,           8,  4, regelwerkId: rId));
        v.Runden.Add(Runde(5, "Halbfinale",        Rundenart.KoRunde,           4,  2, regelwerkId: rId));
        v.Runden.Add(Runde(6, "Finale",            Rundenart.Finale,            2,  1, regelwerkId: rId));
        return v;
    }

    // Ab der 3. Hauptrunde (Einstieg der Premier-League- und Championship-Klubs), seit 2024/25 ohne
    // Wiederholungsspiele — Verlängerung + Elfmeterschießen direkt im ersten Spiel.
    private static TurnierInstanz FaCup64Teams(
        IReadOnlyDictionary<string, int>? rw, IReadOnlyDictionary<string, int>? verband, IReadOnlyDictionary<string, int>? land)
    {
        var v = new TurnierInstanz
        {
            Status = TurnierStatus.Vorlage,
            Name = "FA Cup (64 Teams, ab 3. Hauptrunde)",
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
        return v;
    }

    // ── Nationale Ligen (Issue #350) ───────────────────────────────────────────

    // Doppelrunde jeder gegen jeden; Ausgang 1 = Meister. Relegation/Auf-/Abstieg sind turnierübergreifend
    // (#175/#177) und daher nicht Teil der Vorlage.
    private static TurnierInstanz NationaleLiga(
        IReadOnlyDictionary<string, int>? rw, IReadOnlyDictionary<string, int>? verband, IReadOnlyDictionary<string, int>? land,
        string name, string fifaCode, int teams, string regelwerkName)
    {
        var v = new TurnierInstanz
        {
            Status = TurnierStatus.Vorlage,
            Name = name,
            Beschreibung = $"Hin- und Rückrunde, jeder gegen jeden ({teams} Teams, {2 * (teams - 1)} Spieltage)",
            ZielMannschaftsanzahl = teams,
            Kategorie = TurnierKategorie.Liga,
            MannschaftsartFilter = MannschaftsartFilter.Verein,
            LandId = LId(land, fifaCode),
            VerbandId = VId(verband, "UEFA"),
        };
        v.Runden.Add(Runde(1, "Saison", Rundenart.Liga, teams, 1, mitRueckspiel: true, regelwerkId: RwId(rw, regelwerkName)));
        return v;
    }

    // ── Kleine Turniere ────────────────────────────────────────────────────────

    private static TurnierInstanz MiniTurnier8Teams(IReadOnlyDictionary<string, int>? rw)
    {
        var v = new TurnierInstanz
        {
            Status = TurnierStatus.Vorlage,
            Name = "Mini-Turnier (8 Teams)",
            Beschreibung = "2 Vierergruppen → Halbfinale → Finale + Spiel um Platz 3",
            ZielMannschaftsanzahl = 8,
            Kategorie = TurnierKategorie.KleinesTurnier,
        };
        int? rId = RwId(rw, "FIFA Standard Modern (3-Punkte)");
        v.Runden.Add(Runde(1, "Gruppenphase",     Rundenart.Gruppenphase,     8,  4, gruppen: 2, regelwerkId: rId));
        v.Runden.Add(Runde(2, "Halbfinale",       Rundenart.KoRunde,          4,  2, regelwerkId: rId));
        v.Runden.Add(Runde(3, "Spiel um Platz 3", Rundenart.SpielUmPlatzDrei, 2,  1, regelwerkId: rId));
        v.Runden.Add(Runde(4, "Finale",           Rundenart.Finale,           2,  1, regelwerkId: rId));
        return v;
    }

    private static TurnierInstanz MiniTurnier4Teams(IReadOnlyDictionary<string, int>? rw)
    {
        var v = new TurnierInstanz
        {
            Status = TurnierStatus.Vorlage,
            Name = "Mini-Turnier (4 Teams)",
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
        Name = "Freies Turnier (benutzerdefiniert)",
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
