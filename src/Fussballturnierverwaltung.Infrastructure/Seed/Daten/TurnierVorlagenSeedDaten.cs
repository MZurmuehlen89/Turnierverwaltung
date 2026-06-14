using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Infrastructure.Seed.Daten;

public static class TurnierVorlagenSeedDaten
{
    public static List<TurnierVorlage> Erstellen(IReadOnlyDictionary<string, int>? regelwerkLookup = null) =>
    [
        FifaWm32Teams(regelwerkLookup),
        FifaWm24Teams(regelwerkLookup),
        FifaWm16Teams(regelwerkLookup),
        UefaEm24Teams(regelwerkLookup),
        UefaEm16Teams(regelwerkLookup),
        UefaEm8Teams(regelwerkLookup),
        MiniTurnier8Teams(regelwerkLookup),
        MiniTurnier4Teams(regelwerkLookup),
        FreiesTurnier(),
    ];

    // Für Nachrüstung bestehender DBs: Name → Runden-Blueprint
    public static Dictionary<string, List<VorlagenRunde>> RundenNachVorlagenName(IReadOnlyDictionary<string, int>? regelwerkLookup = null)
        => Erstellen(regelwerkLookup)
            .Where(v => v.VorlagenRunden.Count > 0)
            .ToDictionary(v => v.Name, v => v.VorlagenRunden.ToList());

    // ── Weltmeisterschaften ────────────────────────────────────────────────────

    private static TurnierVorlage FifaWm32Teams(IReadOnlyDictionary<string, int>? rw)
    {
        var v = new TurnierVorlage
        {
            Name = "FIFA WM (32 Teams, 1998–2022)",
            Beschreibung = "Gruppenphase (8×4) → Achtelfinale → Viertelfinale → Halbfinale → Spiel um Platz 3 + Finale",
            ErforderlicheMannschaftsanzahl = 32,
            VorlagenKategorie = VorlagenKategorie.Weltmeisterschaft,
            IstHistorisch = false,
            GueltigVon = new DateOnly(1998, 1, 1),
        };
        int? rId = RwId(rw, "FIFA Standard Modern (3-Punkte)");
        v.VorlagenRunden.Add(Runde(1, "Gruppenphase",      Rundenart.Gruppenphase,    32, 16, regelwerkId: rId));
        v.VorlagenRunden.Add(Runde(2, "Achtelfinale",      Rundenart.KoRunde,          16,  8, regelwerkId: rId));
        v.VorlagenRunden.Add(Runde(3, "Viertelfinale",     Rundenart.KoRunde,           8,  4, regelwerkId: rId));
        v.VorlagenRunden.Add(Runde(4, "Halbfinale",        Rundenart.KoRunde,           4,  2, regelwerkId: rId));
        v.VorlagenRunden.Add(Runde(5, "Spiel um Platz 3",  Rundenart.SpielUmPlatzDrei,  2,  1, regelwerkId: rId));
        v.VorlagenRunden.Add(Runde(6, "Finale",            Rundenart.Finale,            2,  1, regelwerkId: rId));
        return v;
    }

    private static TurnierVorlage FifaWm24Teams(IReadOnlyDictionary<string, int>? rw)
    {
        var v = new TurnierVorlage
        {
            Name = "FIFA WM (24 Teams, 1982–1994)",
            Beschreibung = "6 Dreiergruppen + Achtelfinale-Runde → K.-o.",
            ErforderlicheMannschaftsanzahl = 24,
            VorlagenKategorie = VorlagenKategorie.Weltmeisterschaft,
            IstHistorisch = true,
            GueltigVon = new DateOnly(1982, 1, 1),
            GueltigBis = new DateOnly(1994, 12, 31),
        };
        int? rId = RwId(rw, "FIFA Historisch (2-Punkte, vor 1994)");
        v.VorlagenRunden.Add(Runde(1, "Gruppenphase",      Rundenart.Gruppenphase,    24, 12, regelwerkId: rId));
        v.VorlagenRunden.Add(Runde(2, "Zwischenrunde",     Rundenart.Gruppenphase,    12,  4, regelwerkId: rId));
        v.VorlagenRunden.Add(Runde(3, "Halbfinale",        Rundenart.KoRunde,          4,  2, regelwerkId: rId));
        v.VorlagenRunden.Add(Runde(4, "Spiel um Platz 3",  Rundenart.SpielUmPlatzDrei, 2,  1, regelwerkId: rId));
        v.VorlagenRunden.Add(Runde(5, "Finale",            Rundenart.Finale,           2,  1, regelwerkId: rId));
        return v;
    }

    private static TurnierVorlage FifaWm16Teams(IReadOnlyDictionary<string, int>? rw)
    {
        var v = new TurnierVorlage
        {
            Name = "FIFA WM (16 Teams, 1934–1978)",
            Beschreibung = "Direktes K.-o. ab 16 Teams",
            ErforderlicheMannschaftsanzahl = 16,
            VorlagenKategorie = VorlagenKategorie.Weltmeisterschaft,
            IstHistorisch = true,
            GueltigVon = new DateOnly(1934, 1, 1),
            GueltigBis = new DateOnly(1978, 12, 31),
        };
        int? rId = RwId(rw, "Historisch: Wiederholungsspiel + Los");
        v.VorlagenRunden.Add(Runde(1, "Achtelfinale",      Rundenart.KoRunde,          16,  8, regelwerkId: rId));
        v.VorlagenRunden.Add(Runde(2, "Viertelfinale",     Rundenart.KoRunde,           8,  4, regelwerkId: rId));
        v.VorlagenRunden.Add(Runde(3, "Halbfinale",        Rundenart.KoRunde,           4,  2, regelwerkId: rId));
        v.VorlagenRunden.Add(Runde(4, "Spiel um Platz 3",  Rundenart.SpielUmPlatzDrei,  2,  1, regelwerkId: rId));
        v.VorlagenRunden.Add(Runde(5, "Finale",            Rundenart.Finale,            2,  1, regelwerkId: rId));
        return v;
    }

    // ── Europameisterschaften ──────────────────────────────────────────────────

    private static TurnierVorlage UefaEm24Teams(IReadOnlyDictionary<string, int>? rw)
    {
        var v = new TurnierVorlage
        {
            Name = "UEFA EM (24 Teams, 2016+)",
            Beschreibung = "Gruppenphase (6×4) → Achtelfinale → Viertelfinale → Halbfinale → Finale",
            ErforderlicheMannschaftsanzahl = 24,
            VorlagenKategorie = VorlagenKategorie.Kontinentalmeisterschaft,
            IstHistorisch = false,
            GueltigVon = new DateOnly(2016, 1, 1),
        };
        int? rId = RwId(rw, "FIFA Standard Modern (3-Punkte)");
        v.VorlagenRunden.Add(Runde(1, "Gruppenphase",  Rundenart.Gruppenphase, 24, 16, regelwerkId: rId));
        v.VorlagenRunden.Add(Runde(2, "Achtelfinale",  Rundenart.KoRunde,      16,  8, regelwerkId: rId));
        v.VorlagenRunden.Add(Runde(3, "Viertelfinale", Rundenart.KoRunde,       8,  4, regelwerkId: rId));
        v.VorlagenRunden.Add(Runde(4, "Halbfinale",    Rundenart.KoRunde,       4,  2, regelwerkId: rId));
        v.VorlagenRunden.Add(Runde(5, "Finale",        Rundenart.Finale,        2,  1, regelwerkId: rId));
        return v;
    }

    private static TurnierVorlage UefaEm16Teams(IReadOnlyDictionary<string, int>? rw)
    {
        var v = new TurnierVorlage
        {
            Name = "UEFA EM (16 Teams, 1996–2012)",
            Beschreibung = "Gruppenphase (4×4) → Viertelfinale → Halbfinale → Finale",
            ErforderlicheMannschaftsanzahl = 16,
            VorlagenKategorie = VorlagenKategorie.Kontinentalmeisterschaft,
            IstHistorisch = true,
            GueltigVon = new DateOnly(1996, 1, 1),
            GueltigBis = new DateOnly(2012, 12, 31),
        };
        int? rId = RwId(rw, "UEFA EM 1996 (Golden Goal)");
        v.VorlagenRunden.Add(Runde(1, "Gruppenphase",  Rundenart.Gruppenphase, 16,  8, regelwerkId: rId));
        v.VorlagenRunden.Add(Runde(2, "Viertelfinale", Rundenart.KoRunde,       8,  4, regelwerkId: rId));
        v.VorlagenRunden.Add(Runde(3, "Halbfinale",    Rundenart.KoRunde,       4,  2, regelwerkId: rId));
        v.VorlagenRunden.Add(Runde(4, "Finale",        Rundenart.Finale,        2,  1, regelwerkId: rId));
        return v;
    }

    private static TurnierVorlage UefaEm8Teams(IReadOnlyDictionary<string, int>? rw)
    {
        var v = new TurnierVorlage
        {
            Name = "UEFA EM (8 Teams, 1960–1992)",
            Beschreibung = "Halbfinale → Finale + Spiel um Platz 3",
            ErforderlicheMannschaftsanzahl = 8,
            VorlagenKategorie = VorlagenKategorie.Kontinentalmeisterschaft,
            IstHistorisch = true,
            GueltigVon = new DateOnly(1960, 1, 1),
            GueltigBis = new DateOnly(1992, 12, 31),
        };
        int? rId = RwId(rw, "Historisch: Wiederholungsspiel + Los");
        v.VorlagenRunden.Add(Runde(1, "Gruppenphase",     Rundenart.Gruppenphase,     8,  4, regelwerkId: rId));
        v.VorlagenRunden.Add(Runde(2, "Halbfinale",       Rundenart.KoRunde,          4,  2, regelwerkId: rId));
        v.VorlagenRunden.Add(Runde(3, "Spiel um Platz 3", Rundenart.SpielUmPlatzDrei, 2,  1, regelwerkId: rId));
        v.VorlagenRunden.Add(Runde(4, "Finale",           Rundenart.Finale,           2,  1, regelwerkId: rId));
        return v;
    }

    // ── Kleine Turniere ────────────────────────────────────────────────────────

    private static TurnierVorlage MiniTurnier8Teams(IReadOnlyDictionary<string, int>? rw)
    {
        var v = new TurnierVorlage
        {
            Name = "Mini-Turnier (8 Teams)",
            Beschreibung = "2 Vierergruppen → Halbfinale → Finale + Spiel um Platz 3",
            ErforderlicheMannschaftsanzahl = 8,
            VorlagenKategorie = VorlagenKategorie.KleinesTurnier,
            IstHistorisch = false,
        };
        int? rId = RwId(rw, "FIFA Standard Modern (3-Punkte)");
        v.VorlagenRunden.Add(Runde(1, "Gruppenphase",     Rundenart.Gruppenphase,     8,  4, regelwerkId: rId));
        v.VorlagenRunden.Add(Runde(2, "Halbfinale",       Rundenart.KoRunde,          4,  2, regelwerkId: rId));
        v.VorlagenRunden.Add(Runde(3, "Spiel um Platz 3", Rundenart.SpielUmPlatzDrei, 2,  1, regelwerkId: rId));
        v.VorlagenRunden.Add(Runde(4, "Finale",           Rundenart.Finale,           2,  1, regelwerkId: rId));
        return v;
    }

    private static TurnierVorlage MiniTurnier4Teams(IReadOnlyDictionary<string, int>? rw)
    {
        var v = new TurnierVorlage
        {
            Name = "Mini-Turnier (4 Teams)",
            Beschreibung = "Liga-Runde oder direktes Halbfinale + Finale",
            ErforderlicheMannschaftsanzahl = 4,
            VorlagenKategorie = VorlagenKategorie.KleinesTurnier,
            IstHistorisch = false,
        };
        int? rId = RwId(rw, "FIFA Standard Modern (3-Punkte)");
        v.VorlagenRunden.Add(Runde(1, "Halbfinale",       Rundenart.KoRunde,          4,  2, regelwerkId: rId));
        v.VorlagenRunden.Add(Runde(2, "Spiel um Platz 3", Rundenart.SpielUmPlatzDrei, 2,  1, regelwerkId: rId));
        v.VorlagenRunden.Add(Runde(3, "Finale",           Rundenart.Finale,           2,  1, regelwerkId: rId));
        return v;
    }

    private static TurnierVorlage FreiesTurnier() => new()
    {
        Name = "Freies Turnier (benutzerdefiniert)",
        Beschreibung = "Frei konfigurierbare Rundenstruktur",
        ErforderlicheMannschaftsanzahl = 0,
        VorlagenKategorie = VorlagenKategorie.Benutzerdefiniert,
        IstHistorisch = false,
    };

    private static int? RwId(IReadOnlyDictionary<string, int>? lookup, string name)
        => lookup is not null && lookup.TryGetValue(name, out var id) ? id : null;

    private static VorlagenRunde Runde(
        int reihenfolge, string name, Rundenart art,
        int eingang, int ausgang, bool auswaerts = false,
        int? regelwerkId = null) => new()
    {
        Reihenfolge               = reihenfolge,
        Name                      = name,
        Rundenart                 = art,
        EingangsMannschaftsanzahl = eingang,
        AusgangsMannschaftsanzahl = ausgang,
        AuswaertstorRegelAktiv    = auswaerts,
        RegelwerkId               = regelwerkId,
    };
}
