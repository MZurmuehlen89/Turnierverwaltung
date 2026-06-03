using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Infrastructure.Seed.Daten;

public static class VereinsmannschaftenSeedDaten
{
    private record Eintrag(
        string Name, string Kurzname, string LandFifaCode,
        int Off, int Mid, int Def, int Heim, int Ausw);

    private static readonly Eintrag[] Eintraege =
    [
        // Deutschland – Bundesliga
        new("Bayern München",       "FCB", "GER", Off: 90, Mid: 88, Def: 86, Heim: 95, Ausw: 82),
        new("Borussia Dortmund",    "BVB", "GER", Off: 84, Mid: 82, Def: 78, Heim: 88, Ausw: 76),

        // Spanien – La Liga
        new("Real Madrid",          "RMA", "ESP", Off: 91, Mid: 87, Def: 85, Heim: 93, Ausw: 83),
        new("FC Barcelona",         "BAR", "ESP", Off: 88, Mid: 89, Def: 84, Heim: 92, Ausw: 80),
        new("Atletico Madrid",      "ATM", "ESP", Off: 82, Mid: 81, Def: 86, Heim: 86, Ausw: 76),

        // England – Premier League
        new("Manchester City",      "MCI", "ENG", Off: 91, Mid: 90, Def: 87, Heim: 90, Ausw: 85),
        new("FC Liverpool",         "LIV", "ENG", Off: 88, Mid: 86, Def: 84, Heim: 91, Ausw: 82),
        new("Arsenal FC",           "ARS", "ENG", Off: 84, Mid: 84, Def: 81, Heim: 87, Ausw: 77),
        new("Chelsea FC",           "CHE", "ENG", Off: 83, Mid: 83, Def: 81, Heim: 85, Ausw: 77),

        // Frankreich – Ligue 1
        new("Paris Saint-Germain",  "PSG", "FRA", Off: 89, Mid: 85, Def: 82, Heim: 90, Ausw: 79),

        // Italien – Serie A
        new("Inter Mailand",        "INT", "ITA", Off: 84, Mid: 83, Def: 84, Heim: 86, Ausw: 77),
        new("Juventus Turin",       "JUV", "ITA", Off: 83, Mid: 81, Def: 83, Heim: 87, Ausw: 76),

        // Südamerika
        new("Flamengo",             "FLA", "BRA", Off: 83, Mid: 80, Def: 78, Heim: 88, Ausw: 73),
        new("River Plate",          "RIV", "ARG", Off: 82, Mid: 80, Def: 79, Heim: 87, Ausw: 72),
    ];

    public static List<Mannschaft> Erstellen(IReadOnlyDictionary<string, int> laenderLookup) =>
        Eintraege
            .Where(e => laenderLookup.ContainsKey(e.LandFifaCode))
            .Select(e => new Mannschaft
            {
                Name             = e.Name,
                Kurzname         = e.Kurzname,
                Mannschaftsart   = Mannschaftsart.Verein,
                LandId           = laenderLookup[e.LandFifaCode],
                OffensivRating   = e.Off,
                MittelfeldRating = e.Mid,
                DefensivRating   = e.Def,
                Heimstaerke      = e.Heim,
                Auswaertsstaerke = e.Ausw
            })
            .ToList();
}
