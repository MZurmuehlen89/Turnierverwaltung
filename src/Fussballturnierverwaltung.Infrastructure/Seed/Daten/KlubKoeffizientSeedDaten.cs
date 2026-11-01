namespace Fussballturnierverwaltung.Infrastructure.Seed.Daten;

/// <summary>UEFA-Klub-Koeffizient nach Bert Kassies, „UEFA 5-year Club Ranking 2026“ (Fünfjahreswertung bis
/// einschließlich 2025/26, gilt für die Auslosungen 2026/27), abgerufen am 01.11.2026 — fester Stand im Seed (Issue #381),
/// unabhängig von der Import-Quelle <see cref="RanglistenQuellenSeedDaten.UEFA_KLUB_KOEFFIZIENT"/>.
///
/// Enthalten sind alle Vereine der Kassies-Liste, die der Seed kennt (<see cref="VereinsmannschaftenSeedDaten"/>), in der
/// Reihenfolge der Liste. Punkte sind der maßgebliche Koeffizient, also der höhere Wert aus eigenen Punkten und
/// Landesanteil, auf ganze Zahlen gerundet. Der Rang ergibt sich aus der Position in dieser Liste — fortlaufend, auch bei
/// Gleichstand, wie im Ranglisten-Editor (#578). Schlüssel ist Kurzname + Land wie bei der Vereins-Nachrüstung.</summary>
internal static class KlubKoeffizientSeedDaten
{
    public const string RANGLISTE = RanglistenQuellenSeedDaten.UEFA_KLUB_KOEFFIZIENT;
    public static readonly DateOnly DATUM = new(2026, 11, 1);
    public const string QUELLE_TEXT = "UEFA 5-year Club Ranking 2026 (Bert Kassies, kassiesa.net), abgerufen am 01.11.2026";

    public sealed record Eintrag(string Kurzname, string Land, int Punkte);

    public static readonly Eintrag[] Eintraege =
    [
        /*   1 */ new("FCB", "GER", 148), new("RMA", "ESP", 144), new("PSG", "FRA", 132), new("LIV", "ENG", 130), new("INT", "ITA", 127), new("MCI", "ENG", 126),
        /*   7 */ new("ARS", "ENG", 119), new("BAR", "ESP", 113), new("B04", "GER", 105), new("ATM", "ESP", 105), new("BVB", "GER", 101), new("CHE", "ENG", 99),
        /*  13 */ new("ROM", "ITA", 98), new("SLB", "POR", 90), new("SPS", "POR", 84), new("ATA", "ITA", 84), new("AVL", "ENG", 83), new("SGE", "GER", 83),
        /*  19 */ new("TOT", "ENG", 82), new("FCP", "POR", 81), new("MNU", "ENG", 76), new("FIO", "ITA", 76), new("CLB", "BEL", 75), new("BET", "ESP", 74),
        /*  25 */ new("JUV", "ITA", 72), new("PSV", "NED", 71), new("FEY", "NED", 71), new("WHU", "ENG", 69), new("LOS", "FRA", 69), new("ACM", "ITA", 66),
        /*  31 */ new("OL", "FRA", 66), new("BOD", "NOR", 64), new("SCB", "POR", 64), new("NAP", "ITA", 63), new("AZA", "NED", 63), new("OLY", "GRE", 62),
        /*  37 */ new("RBL", "GER", 61), new("RAN", "SCO", 59), new("VIL", "ESP", 59), new("LAZ", "ITA", 59), new("AJX", "NED", 58), new("FEN", "TUR", 58),
        /*  43 */ new("RSO", "ESP", 57), new("SCF", "GER", 56), new("SCH", "UKR", 56), new("MON", "FRA", 56), new("FCK", "DEN", 54), new("OM", "FRA", 54),
        /*  49 */ new("GAL", "TUR", 54), new("FTC", "HUN", 51), new("VPL", "CZE", 50), new("MJY", "DEN", 48), new("PAK", "GRE", 48), new("USG", "BEL", 48),
        /*  55 */ new("CZV", "SRB", 46), new("DZG", "CRO", 46), new("RBS", "AUT", 45), new("CLT", "SCO", 44), new("SLP", "CZE", 44), new("QAR", "AZE", 43),
        /*  61 */ new("GNT", "BEL", 39), new("SEV", "ESP", 39), new("ATH", "ESP", 39), new("SPP", "CZE", 38), new("SVB", "SVK", 36), new("REN", "FRA", 35),
        /*  67 */ new("BAS", "SUI", 34), new("LGW", "POL", 32), new("MTA", "ISR", 32), new("DIF", "SWE", 32), new("BOL", "ITA", 31), new("RSA", "BEL", 31),
        /*  73 */ new("NEW", "ENG", 30), new("RAP", "AUT", 30), new("YBO", "SUI", 30), new("PAO", "GRE", 29), new("GNK", "BEL", 29), new("LUD", "BUL", 29),
        /*  79 */ new("STU", "AUT", 28), new("VFB", "GER", 28), new("LPO", "POL", 27), new("FCSB", "ROU", 26), new("PAF", "CYP", 24), new("AEK", "GRE", 24),
        /*  85 */ new("NFO", "ENG", 24), new("CRY", "ENG", 24), new("BHA", "ENG", 24), new("LEI", "ENG", 24), new("CLJ", "SVN", 23), new("UNB", "GER", 23),
        /*  91 */ new("RAK", "POL", 22), new("RAY", "ESP", 22), new("JAG", "POL", 22), new("PAR", "SRB", 22), new("MOL", "NOR", 22), new("STR", "FRA", 22),
        /*  97 */ new("OMO", "CYP", 21), new("LUG", "SUI", 21), new("LASK", "AUT", 21), new("AEL", "CYP", 20), new("OGN", "FRA", 20), new("SHE", "MDA", 20),
        /* 103 */ new("BSK", "TUR", 20), new("CEL", "ESP", 19), new("GIR", "ESP", 19), new("OSA", "ESP", 19), new("SHR", "IRL", 19), new("VIT", "POR", 19),
        /* 109 */ new("RIJ", "CRO", 19), new("M05", "GER", 19), new("HEI", "GER", 19), new("TSG", "GER", 19), new("KOE", "GER", 19), new("WOB", "GER", 19),
        /* 115 */ new("OLI", "SVN", 18), new("DYN", "UKR", 18), new("CFR", "ROU", 18), new("MHA", "ISR", 18), new("SBR", "FRA", 17), new("LNS", "FRA", 17),
        /* 121 */ new("TFC", "FRA", 17), new("NAN", "FRA", 17), new("BJK", "TUR", 16), new("MFF", "SWE", 15), new("APO", "CYP", 15), new("SCL", "POR", 15),
        /* 127 */ new("ARO", "POR", 15), new("GIL", "POR", 15), new("PFE", "POR", 15), new("KUP", "FIN", 14), new("HJK", "FIN", 14), new("HBS", "ISR", 14),
        /* 133 */ new("DRI", "KVX", 14), new("GAE", "NED", 14), new("UTR", "NED", 14), new("TWE", "NED", 14), new("VTS", "NED", 14), new("LRI", "GIB", 14),
        /* 139 */ new("ZRI", "BIH", 13), new("BOR", "BIH", 13), new("CER", "BEL", 13), new("RFS", "LVA", 12), new("SER", "SUI", 12), new("AST", "KAZ", 12),
        /* 145 */ new("ANT", "BEL", 12), new("SIV", "TUR", 12), new("CHL", "BEL", 12), new("BRN", "NOR", 12), new("BRK", "ISL", 12), new("ZAL", "LTU", 12),
        /* 151 */ new("DNP", "UKR", 12), new("ZOR", "UKR", 12), new("SPK", "RUS", 12), new("VKR", "ISL", 12), new("HRT", "SCO", 12), new("SAM", "TUR", 11),
        /* 157 */ new("KRT", "KAZ", 11), new("ELF", "SWE", 11), new("TRA", "TUR", 11), new("SHK", "MKD", 11), new("NOA", "ARM", 11), new("UCR", "ROU", 10),
        /* 163 */ new("RIG", "LVA", 10), new("KIK", "FRO", 10), new("TRN", "SVK", 10), new("ADS", "TUR", 10), new("KON", "TUR", 10), new("LAU", "SUI", 10),
        /* 169 */ new("HAC", "SWE", 10), new("ABE", "SCO", 10), new("BLK", "KVX", 10), new("HAJ", "CRO", 10), new("PYU", "ARM", 10), new("MAR", "SVN", 10),
        /* 175 */ new("FLO", "EST", 10), new("SIG", "CZE", 10), new("BAN", "CZE", 10), new("MBO", "CZE", 10), new("BOH", "CZE", 10), new("SLC", "CZE", 10),
        /* 181 */ new("JAB", "CZE", 10), new("ARI", "GRE", 10), new("BRO", "DEN", 10), new("LNF", "NIR", 10), new("AWI", "AUT", 10), new("WIS", "POL", 9),
        /* 187 */ new("SLA", "POL", 9), new("POG", "POL", 9), new("LGD", "POL", 9), new("LAR", "NIR", 9), new("PTC", "MDA", 9), new("TNS", "WAL", 9),
        /* 193 */ new("HSP", "MLT", 8), new("VAD", "LIE", 8), new("SIL", "DEN", 8), new("NSJ", "DEN", 8), new("AGF", "DEN", 8), new("VIB", "DEN", 8),
        /* 199 */ new("RFC", "DEN", 8), new("ROS", "NOR", 8), new("FRE", "NOR", 8), new("VKG", "NOR", 8), new("TRO", "NOR", 8), new("LIL", "NOR", 8),
        /* 205 */ new("VAL", "NOR", 8), new("ZEN", "RUS", 8), new("HIB", "SCO", 7), new("DUN", "SCO", 6), new("PZH", "UKR", 5), new("VOR", "UKR", 5),
        /* 211 */ new("LOK", "RUS", 5),
    ];
}
