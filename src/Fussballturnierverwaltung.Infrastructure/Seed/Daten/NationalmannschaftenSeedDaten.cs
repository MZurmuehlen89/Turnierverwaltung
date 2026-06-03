using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Infrastructure.Seed.Daten;

public static class NationalmannschaftenSeedDaten
{
    private record Eintrag(string FifaCode, int Off, int Mid, int Def, int Heim, int Ausw);

    // Ratings basieren auf FIFA-Weltrangliste und historischer Stärke (Stand ca. 2024)
    // Skala: 90+ Weltklasse, 80-89 Topnation, 70-79 Gut, 60-69 Mittelfeld
    private static readonly Eintrag[] Eintraege =
    [
        // Europa – UEFA
        new("GER", Off: 83, Mid: 84, Def: 82, Heim: 85, Ausw: 80),
        new("FRA", Off: 89, Mid: 88, Def: 87, Heim: 86, Ausw: 83),
        new("ENG", Off: 85, Mid: 84, Def: 83, Heim: 85, Ausw: 79),
        new("ESP", Off: 86, Mid: 90, Def: 85, Heim: 87, Ausw: 83),
        new("ITA", Off: 82, Mid: 84, Def: 86, Heim: 84, Ausw: 79),
        new("NED", Off: 82, Mid: 83, Def: 80, Heim: 81, Ausw: 76),
        new("POR", Off: 87, Mid: 82, Def: 80, Heim: 84, Ausw: 77),
        new("BEL", Off: 84, Mid: 83, Def: 79, Heim: 82, Ausw: 76),
        new("CRO", Off: 80, Mid: 82, Def: 80, Heim: 80, Ausw: 75),
        new("SUI", Off: 76, Mid: 79, Def: 78, Heim: 78, Ausw: 72),
        new("AUT", Off: 75, Mid: 75, Def: 73, Heim: 76, Ausw: 70),
        new("DEN", Off: 77, Mid: 79, Def: 78, Heim: 78, Ausw: 72),
        new("SWE", Off: 75, Mid: 74, Def: 73, Heim: 76, Ausw: 70),
        new("NOR", Off: 75, Mid: 73, Def: 72, Heim: 76, Ausw: 69),
        new("POL", Off: 74, Mid: 73, Def: 72, Heim: 75, Ausw: 68),
        new("TUR", Off: 76, Mid: 75, Def: 73, Heim: 78, Ausw: 68),
        new("HUN", Off: 73, Mid: 73, Def: 73, Heim: 75, Ausw: 67),
        new("CZE", Off: 73, Mid: 72, Def: 72, Heim: 74, Ausw: 67),
        new("SRB", Off: 76, Mid: 74, Def: 73, Heim: 77, Ausw: 69),
        new("SCO", Off: 74, Mid: 73, Def: 73, Heim: 76, Ausw: 69),
        new("UKR", Off: 75, Mid: 73, Def: 72, Heim: 76, Ausw: 68),
        new("ROU", Off: 72, Mid: 72, Def: 71, Heim: 74, Ausw: 66),
        new("GRE", Off: 70, Mid: 70, Def: 72, Heim: 73, Ausw: 65),
        new("SVK", Off: 71, Mid: 71, Def: 71, Heim: 72, Ausw: 65),
        new("SVN", Off: 70, Mid: 72, Def: 71, Heim: 72, Ausw: 65),

        // Südamerika – CONMEBOL
        new("BRA", Off: 88, Mid: 86, Def: 83, Heim: 91, Ausw: 79),
        new("ARG", Off: 91, Mid: 85, Def: 84, Heim: 88, Ausw: 80),
        new("URU", Off: 79, Mid: 78, Def: 82, Heim: 83, Ausw: 73),
        new("COL", Off: 80, Mid: 79, Def: 75, Heim: 80, Ausw: 72),
        new("CHI", Off: 77, Mid: 76, Def: 74, Heim: 78, Ausw: 71),
        new("ECU", Off: 74, Mid: 73, Def: 72, Heim: 77, Ausw: 68),
        new("PER", Off: 73, Mid: 72, Def: 71, Heim: 76, Ausw: 66),
        new("PAR", Off: 72, Mid: 71, Def: 71, Heim: 75, Ausw: 65),
        new("VEN", Off: 66, Mid: 65, Def: 63, Heim: 68, Ausw: 59),
        new("BOL", Off: 63, Mid: 62, Def: 62, Heim: 70, Ausw: 54), // Höhenbonus La Paz

        // Nord- und Mittelamerika – CONCACAF
        new("USA", Off: 74, Mid: 75, Def: 74, Heim: 76, Ausw: 69),
        new("MEX", Off: 76, Mid: 76, Def: 73, Heim: 82, Ausw: 69),
        new("CAN", Off: 73, Mid: 73, Def: 72, Heim: 74, Ausw: 68),
        new("CRC", Off: 70, Mid: 70, Def: 72, Heim: 74, Ausw: 65),
        new("JAM", Off: 70, Mid: 68, Def: 67, Heim: 70, Ausw: 63),

        // Afrika – CAF
        new("SEN", Off: 78, Mid: 77, Def: 76, Heim: 79, Ausw: 70),
        new("MAR", Off: 77, Mid: 79, Def: 80, Heim: 82, Ausw: 69),
        new("NGA", Off: 77, Mid: 74, Def: 72, Heim: 79, Ausw: 68),
        new("CMR", Off: 74, Mid: 72, Def: 70, Heim: 77, Ausw: 66),
        new("GHA", Off: 74, Mid: 73, Def: 70, Heim: 76, Ausw: 66),
        new("EGY", Off: 73, Mid: 71, Def: 71, Heim: 78, Ausw: 63),
        new("CIV", Off: 75, Mid: 73, Def: 71, Heim: 77, Ausw: 67),
        new("ALG", Off: 74, Mid: 72, Def: 72, Heim: 76, Ausw: 65),
        new("TUN", Off: 72, Mid: 71, Def: 72, Heim: 75, Ausw: 64),
        new("RSA", Off: 70, Mid: 70, Def: 69, Heim: 73, Ausw: 63),
        new("MLI", Off: 72, Mid: 70, Def: 69, Heim: 74, Ausw: 64),

        // Asien – AFC
        new("JPN", Off: 74, Mid: 76, Def: 74, Heim: 75, Ausw: 68),
        new("KOR", Off: 73, Mid: 74, Def: 73, Heim: 75, Ausw: 67),
        new("KSA", Off: 73, Mid: 72, Def: 71, Heim: 78, Ausw: 64),
        new("IRN", Off: 72, Mid: 71, Def: 72, Heim: 77, Ausw: 64),
        new("AUS", Off: 73, Mid: 72, Def: 71, Heim: 74, Ausw: 68),
        new("QAT", Off: 70, Mid: 70, Def: 68, Heim: 78, Ausw: 60),
        new("CHN", Off: 68, Mid: 67, Def: 66, Heim: 72, Ausw: 61),

        // Ozeanien – OFC
        new("NZL", Off: 66, Mid: 65, Def: 64, Heim: 69, Ausw: 59),
    ];

    public static List<Mannschaft> Erstellen(
        IReadOnlyList<Land> laender,
        IReadOnlyDictionary<string, int> laenderLookup)
    {
        var laenderByCode = laender.ToDictionary(l => l.FifaCode);

        return Eintraege
            .Where(e => laenderLookup.ContainsKey(e.FifaCode))
            .Select(e =>
            {
                var land = laenderByCode[e.FifaCode];
                return new Mannschaft
                {
                    Name             = land.Name,
                    Kurzname         = land.FifaCode,
                    Mannschaftsart   = Mannschaftsart.Nationalmannschaft,
                    LandId           = laenderLookup[e.FifaCode],
                    OffensivRating   = e.Off,
                    MittelfeldRating = e.Mid,
                    DefensivRating   = e.Def,
                    Heimstaerke      = e.Heim,
                    Auswaertsstaerke = e.Ausw
                };
            })
            .ToList();
    }
}
