using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Infrastructure.Seed.Daten;

/// <summary>
/// Ratings basieren auf FIFA-Weltrangliste und historischer Stärke (Stand ca. 2024).
/// Skala: 90+ Weltklasse | 80–89 Top | 70–79 Gut | 60–69 Mittelfeld | 50–59 Unteres Mittelfeld | unter 50 Schwach
/// </summary>
public static class NationalmannschaftenSeedDaten
{
    private sealed record E(string Code, int Off, int Mid, int Def, int Heim, int Ausw);

    private static readonly E[] Eintraege =
    [
        // ── UEFA ────────────────────────────────────────────────────────
        new("GER", 83, 84, 82, 85, 80), new("FRA", 89, 88, 87, 86, 83),
        new("ENG", 85, 84, 83, 85, 79), new("ESP", 86, 90, 85, 87, 83),
        new("ITA", 82, 84, 86, 84, 79), new("NED", 82, 83, 80, 81, 76),
        new("POR", 87, 82, 80, 84, 77), new("BEL", 84, 83, 79, 82, 76),
        new("CRO", 80, 82, 80, 80, 75), new("SUI", 76, 79, 78, 78, 72),
        new("AUT", 75, 75, 73, 76, 70), new("DEN", 77, 79, 78, 78, 72),
        new("SWE", 75, 74, 73, 76, 70), new("NOR", 75, 73, 72, 76, 69),
        new("POL", 74, 73, 72, 75, 68), new("TUR", 76, 75, 73, 78, 68),
        new("HUN", 73, 73, 73, 75, 67), new("CZE", 73, 72, 72, 74, 67),
        new("SRB", 76, 74, 73, 77, 69), new("SCO", 74, 73, 73, 76, 69),
        new("UKR", 75, 73, 72, 76, 68), new("ROU", 72, 72, 71, 74, 66),
        new("GRE", 70, 70, 72, 73, 65), new("SVK", 71, 71, 71, 72, 65),
        new("SVN", 70, 72, 71, 72, 65), new("WAL", 72, 71, 71, 74, 67),
        new("NIR", 67, 66, 66, 70, 61), new("IRL", 70, 69, 69, 72, 64),
        new("FIN", 67, 66, 65, 69, 61), new("ISL", 68, 67, 67, 70, 63),
        new("BIH", 68, 67, 66, 70, 62), new("MKD", 68, 67, 66, 70, 62),
        new("ALB", 67, 66, 68, 70, 60), new("BUL", 66, 65, 65, 68, 60),
        new("MNE", 63, 62, 63, 66, 57), new("GEO", 68, 67, 66, 70, 62),
        new("ARM", 63, 62, 62, 65, 56), new("AZE", 60, 59, 59, 62, 54),
        new("BLR", 65, 64, 63, 67, 58), new("KVX", 62, 61, 60, 64, 55),
        new("LUX", 66, 65, 64, 68, 60), new("ISR", 65, 65, 64, 68, 58),
        new("RUS", 73, 72, 73, 76, 65), new("KAZ", 57, 56, 56, 60, 50),
        new("CYP", 62, 61, 61, 64, 55), new("EST", 58, 58, 57, 61, 52),
        new("LVA", 56, 55, 55, 58, 49), new("LTU", 55, 54, 54, 57, 48),
        new("MDA", 50, 49, 49, 53, 43), new("FRO", 57, 56, 57, 60, 50),
        new("MLT", 48, 47, 47, 52, 42), new("AND", 45, 44, 43, 50, 38),
        new("GIB", 43, 42, 41, 47, 36), new("LIE", 43, 42, 42, 47, 36),
        new("SMR", 38, 37, 37, 42, 33),

        // ── CONMEBOL ────────────────────────────────────────────────────
        new("BRA", 88, 86, 83, 91, 79), new("ARG", 91, 85, 84, 88, 80),
        new("URU", 79, 78, 82, 83, 73), new("COL", 80, 79, 75, 80, 72),
        new("CHI", 77, 76, 74, 78, 71), new("ECU", 74, 73, 72, 77, 68),
        new("PER", 73, 72, 71, 76, 66), new("PAR", 72, 71, 71, 75, 65),
        new("VEN", 66, 65, 63, 68, 59), new("BOL", 63, 62, 62, 70, 54),

        // ── CONCACAF ────────────────────────────────────────────────────
        new("USA", 74, 75, 74, 76, 69), new("MEX", 76, 76, 73, 82, 69),
        new("CAN", 73, 73, 72, 74, 68), new("CRC", 70, 70, 72, 74, 65),
        new("JAM", 70, 68, 67, 70, 63), new("TRI", 68, 67, 66, 70, 62),
        new("PAN", 68, 67, 68, 72, 63), new("HON", 66, 65, 65, 70, 60),
        new("SLV", 65, 64, 63, 68, 58), new("GUA", 64, 63, 62, 67, 57),
        new("NCA", 55, 54, 53, 58, 47), new("BLZ", 48, 47, 46, 52, 40),
        new("CUB", 52, 51, 51, 56, 45), new("HAI", 62, 61, 60, 65, 55),
        new("DOM", 54, 53, 52, 57, 47), new("SUR", 62, 61, 60, 65, 55),
        new("GUY", 54, 53, 52, 57, 47), new("PUR", 49, 48, 47, 52, 41),
        new("ATG", 49, 48, 47, 52, 41), new("BAH", 45, 44, 43, 49, 38),
        new("BRB", 50, 49, 48, 53, 43), new("BER", 52, 51, 50, 56, 44),
        new("CAY", 44, 43, 43, 48, 37), new("DMA", 44, 43, 43, 48, 37),
        new("GRN", 51, 50, 49, 54, 43), new("MSR", 42, 41, 40, 46, 35),
        new("SKN", 48, 47, 46, 51, 40), new("LCA", 48, 47, 46, 51, 40),
        new("VIN", 50, 49, 48, 53, 42), new("TCA", 41, 40, 40, 45, 35),
        new("VGB", 40, 39, 39, 44, 34), new("VIR", 40, 39, 38, 44, 34),
        // #579: in der FIFA-Weltrangliste, im Seed bis dahin gefehlt; Ratings nach den Rangnachbarn geschätzt.
        new("CUW", 63, 62, 62, 66, 57), new("ARU", 44, 43, 43, 48, 37),
        new("AIA", 39, 38, 38, 43, 33),

        // ── CAF ─────────────────────────────────────────────────────────
        new("SEN", 78, 77, 76, 79, 70), new("MAR", 77, 79, 80, 82, 69),
        new("NGA", 77, 74, 72, 79, 68), new("CMR", 74, 72, 70, 77, 66),
        new("GHA", 74, 73, 70, 76, 66), new("EGY", 73, 71, 71, 78, 63),
        new("CIV", 75, 73, 71, 77, 67), new("ALG", 74, 72, 72, 76, 65),
        new("TUN", 72, 71, 72, 75, 64), new("RSA", 70, 70, 69, 73, 63),
        new("MLI", 72, 70, 69, 74, 64), new("ANG", 68, 66, 65, 70, 61),
        new("BEN", 64, 63, 62, 67, 57), new("BOT", 59, 58, 57, 62, 52),
        new("BFA", 68, 67, 66, 70, 62), new("BDI", 51, 50, 49, 54, 43),
        new("CPV", 71, 70, 69, 73, 65), new("CTA", 46, 45, 44, 50, 39),
        new("CHA", 44, 43, 42, 48, 37), new("COM", 57, 56, 55, 60, 50),
        new("CGO", 61, 60, 59, 63, 54), new("COD", 68, 67, 66, 70, 62),
        new("DJI", 45, 44, 43, 49, 38), new("EQG", 59, 58, 57, 62, 53),
        new("ERI", 45, 44, 44, 49, 38), new("SWZ", 53, 52, 52, 56, 46),
        new("ETH", 59, 58, 57, 62, 52), new("GAB", 64, 63, 62, 67, 57),
        new("GAM", 69, 68, 67, 71, 63), new("GUI", 70, 69, 68, 72, 63),
        new("GNB", 62, 61, 60, 65, 55), new("KEN", 63, 62, 61, 66, 56),
        new("LES", 52, 51, 51, 55, 45), new("LBR", 57, 56, 55, 60, 50),
        new("LBA", 59, 58, 58, 63, 53), new("MDG", 58, 57, 56, 61, 51),
        new("MWI", 58, 57, 56, 61, 51), new("MTN", 63, 62, 61, 66, 56),
        new("MRI", 50, 49, 48, 53, 43), new("MOZ", 57, 56, 55, 60, 50),
        new("NAM", 59, 58, 57, 62, 52), new("NIG", 54, 53, 52, 57, 47),
        new("RWA", 63, 62, 61, 66, 56), new("STP", 43, 42, 41, 47, 36),
        new("SEY", 44, 43, 42, 48, 37), new("SLE", 61, 60, 59, 63, 54),
        new("SOM", 46, 45, 44, 50, 39), new("SSD", 47, 46, 45, 50, 39),
        new("SDN", 57, 56, 55, 60, 50), new("TAN", 58, 57, 56, 62, 51),
        new("TOG", 62, 61, 60, 64, 55), new("UGA", 64, 63, 62, 67, 57),
        new("ZAM", 65, 64, 63, 67, 58), new("ZIM", 63, 62, 61, 65, 57),

        // ── AFC ─────────────────────────────────────────────────────────
        new("JPN", 74, 76, 74, 75, 68), new("KOR", 73, 74, 73, 75, 67),
        new("KSA", 73, 72, 71, 78, 64), new("IRN", 72, 71, 72, 77, 64),
        new("AUS", 73, 72, 71, 74, 68), new("QAT", 70, 70, 68, 78, 60),
        new("CHN", 68, 67, 66, 72, 61), new("IRQ", 68, 67, 67, 72, 62),
        new("UAE", 68, 67, 65, 72, 62), new("UZB", 68, 68, 67, 72, 63),
        new("THA", 60, 59, 58, 63, 54), new("VIE", 60, 59, 58, 63, 54),
        new("IND", 57, 56, 55, 60, 50), new("JOR", 67, 66, 65, 70, 61),
        new("OMA", 66, 65, 64, 70, 59), new("BHR", 65, 64, 63, 68, 58),
        new("KUW", 57, 56, 55, 60, 50), new("MAS", 55, 54, 53, 58, 48),
        new("SYR", 64, 63, 62, 67, 58), new("PLE", 62, 61, 60, 65, 55),
        new("LBN", 58, 57, 56, 61, 52), new("PRK", 61, 60, 60, 64, 55),
        new("TJK", 61, 60, 59, 64, 54), new("KGZ", 63, 62, 61, 65, 57),
        new("TKM", 57, 56, 55, 60, 50), new("IDN", 55, 54, 53, 58, 48),
        new("PHI", 56, 55, 54, 59, 49), new("HKG", 55, 54, 53, 58, 48),
        new("MYA", 52, 51, 50, 56, 45), new("CAM", 48, 47, 46, 52, 41),
        new("SIN", 52, 51, 50, 55, 45), new("TPE", 52, 51, 50, 55, 45),
        new("MNG", 48, 47, 46, 52, 40), new("NEP", 50, 49, 48, 53, 42),
        new("SRI", 43, 42, 41, 47, 36), new("BAN", 46, 45, 44, 50, 38),
        new("PAK", 42, 41, 40, 46, 35), new("AFG", 44, 43, 42, 48, 37),
        new("LAO", 46, 45, 44, 50, 38), new("MDV", 50, 49, 47, 54, 42),
        new("BHU", 39, 38, 38, 43, 33), new("BRU", 42, 41, 40, 46, 35),
        new("GUM", 46, 45, 44, 50, 38), new("TLS", 43, 42, 41, 47, 36),
        new("MAC", 42, 41, 40, 46, 35), new("NMI", 37, 36, 36, 41, 31),
        new("YEM", 52, 51, 50, 56, 44),

        // ── OFC ─────────────────────────────────────────────────────────
        new("NZL", 66, 65, 64, 69, 59), new("FIJ", 51, 50, 49, 54, 44),
        new("PNG", 53, 52, 51, 57, 46), new("SOL", 53, 52, 51, 57, 46),
        new("TAH", 55, 54, 53, 58, 48), new("VAN", 48, 47, 46, 52, 41),
        new("NCL", 55, 54, 53, 58, 48), new("SAM", 46, 45, 44, 50, 39),
        new("TGA", 46, 45, 44, 49, 38), new("COK", 38, 37, 37, 42, 32),
        new("ASA", 36, 35, 35, 40, 30),
    ];

    public static List<Mannschaft> Erstellen(
        IReadOnlyList<Land> laender,
        IReadOnlyDictionary<string, int> laenderLookup)
    {
        var laenderByCode = laender.ToDictionary(l => l.FifaCode);

        return Eintraege
            .Where(e => laenderLookup.ContainsKey(e.Code))
            .Select(e =>
            {
                var land = laenderByCode[e.Code];
                return new Mannschaft
                {
                    Name             = land.Name,
                    Kurzname         = land.FifaCode,
                    Mannschaftsart   = Mannschaftsart.Nationalmannschaft,
                    LandId           = laenderLookup[e.Code],
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
