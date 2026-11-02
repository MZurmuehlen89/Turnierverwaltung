namespace Fussballturnierverwaltung.Infrastructure.Seed.Daten;

/// <summary>Vereinsranglisten der Kontinentalverbände außerhalb Europas nach dem FIFA-Klub-WM-Ranking 2021–2024
/// („FIFA Club World Cup 2025 Confederations ranking“) — fester Stand im Seed (Issue #581), je Verband eine Liste. Nach
/// diesen Listen hat die FIFA die Plätze der Klub-WM 2025 vergeben, die nicht an Kontinentalsieger gingen. Eine
/// weltweite Klubrangliste gibt es nicht; die Punkte sind zwischen den Verbänden nicht vergleichbar.
///
/// Die FIFA-Seite inside.fifa.com/fifa-rankings/fifa-club-world-cup ist nicht mehr erreichbar; Quelle ist ihr
/// Archivstand vom 13.06.2025 (web.archive.org, Seitendaten <c>__NEXT_DATA__</c>), abgeglichen mit der englischen
/// Wikipedia („2025 FIFA Club World Cup qualification“). Die Listen sind vollständig. Club León fehlt in der CONCACAF-Liste,
/// weil die FIFA ihn 2025 wegen Mehrfachbesitz ausgeschlossen hat. Punkte = Gesamtpunkte, Rang = Position in der Liste
/// (fortlaufend, auch bei Gleichstand, wie im Ranglisten-Editor, #578). Schlüssel ist Kurzname + Land wie bei der
/// Vereins-Nachrüstung (<see cref="VereinsmannschaftenSeedDaten"/>).</summary>
internal static class KlubWmRankingSeedDaten
{
    public static readonly DateOnly DATUM = new(2025, 6, 13);
    public const string QUELLE_TEXT =
        "FIFA Club World Cup 2025 Confederations ranking (inside.fifa.com), Stand 2021–2024, Archivstand vom 13.06.2025";

    public static string Rangliste(string verband) => $"FIFA-Klub-WM-Ranking {verband} 2021–2024";

    public static readonly IReadOnlyList<VereinsranglisteSeed> Alle =
    [
        Seed("CONMEBOL", Conmebol()),
        Seed("CONCACAF", Concacaf()),
        Seed("AFC", Afc()),
        Seed("CAF", Caf()),
        Seed("OFC", Ofc()),
    ];

    private static VereinsranglisteSeed Seed(string verband, VereinsranglisteSeed.Eintrag[] eintraege) =>
        new(new RanglisteSeedStand(Rangliste(verband), DATUM, QUELLE_TEXT), verband, eintraege);

    private static VereinsranglisteSeed.Eintrag[] Conmebol() =>
    [
        /*  1 */ new("FLA", "BRA", 141), new("PAL", "BRA", 140), new("CAM", "BRA", 122), new("RIV", "ARG", 103), new("FLU", "BRA", 97), new("BOJ", "ARG", 71),
        /*  7 */ new("CAP", "BRA", 59), new("OLP", "PAR", 57), new("NAC", "URU", 57), new("SPF", "BRA", 53), new("SCI", "BRA", 52), new("VEL", "ARG", 49),
        /* 13 */ new("RAC", "ARG", 48), new("IDV", "ECU", 48), new("BSG", "ECU", 46), new("BOV", "BOL", 46), new("TSR", "BOL", 46), new("CCP", "PAR", 46),
        /* 19 */ new("PEN", "URU", 44), new("TAL", "ARG", 43), new("CCO", "CHI", 41), new("BFR", "BRA", 37), new("ARJ", "ARG", 37), new("LIB", "PAR", 37),
        /* 25 */ new("EDL", "ARG", 35), new("COR", "BRA", 30), new("NAL", "COL", 27), new("JFC", "COL", 26), new("TAC", "VEN", 26), new("CRI", "PER", 23),
        /* 31 */ new("DPE", "COL", 22), new("UCA", "CHI", 22), new("LDU", "ECU", 21), new("GPA", "BRA", 19), new("ALI", "PER", 18), new("ARE", "BOL", 18),
        /* 37 */ new("FOR", "BRA", 17), new("CLN", "ARG", 17), new("TOL", "COL", 17), new("SLO", "ARG", 15), new("EME", "ECU", 15), new("DYJ", "ARG", 15),
        /* 43 */ new("UNI", "PER", 15), new("LVM", "URU", 14), new("CRS", "VEN", 13), new("DIM", "COL", 13), new("HUC", "CHI", 11), new("DCA", "COL", 11),
        /* 49 */ new("ROC", "ARG", 10), new("PAE", "CHI", 10), new("PAT", "ARG", 9), new("SFC", "BRA", 9), new("MNG", "VEN", 8), new("NUB", "CHI", 8),
        /* 55 */ new("RBB", "BRA", 8), new("MEL", "PER", 7), new("AUC", "ECU", 7), new("ADC", "COL", 7), new("MIL", "COL", 6), new("ISF", "COL", 6),
        /* 61 */ new("DLG", "VEN", 6), new("RTS", "URU", 6), new("AMG", "BRA", 5), new("ULC", "CHI", 5), new("CSL", "CHI", 4), new("IPE", "BOL", 4),
        /* 67 */ new("MTP", "VEN", 3),
    ];

    private static VereinsranglisteSeed.Eintrag[] Caf() =>
    [
        /*  1 */ new("ALA", "EGY", 140), new("WAC", "MAR", 108), new("EST", "TUN", 100), new("MSU", "RSA", 98), new("CRB", "ALG", 63), new("PDL", "ANG", 58),
        /*  7 */ new("SIM", "TAN", 55), new("RAJ", "MAR", 45), new("HOM", "SDN", 35), new("TPM", "COD", 32), new("HOR", "GUI", 29), new("KCH", "RSA", 28),
        /* 13 */ new("ZAM", "EGY", 28), new("ESS", "ALG", 23), new("MER", "SDN", 20), new("ASE", "CIV", 19), new("JSK", "ALG", 17), new("VCL", "COD", 17),
        /* 19 */ new("YAN", "TAN", 16), new("MCA", "ALG", 16), new("ESH", "TUN", 16), new("JWG", "BOT", 11), new("AMZ", "RSA", 10), new("NOU", "MTN", 8),
        /* 25 */ new("PYR", "EGY", 8), new("MED", "GHA", 7), new("TEU", "SEN", 7), new("VIP", "UGA", 5), new("SAG", "ANG", 5), new("COT", "CMR", 3),
    ];

    private static VereinsranglisteSeed.Eintrag[] Afc() =>
    [
        /*  1 */ new("HIL", "KSA", 118), new("UHD", "KOR", 81), new("JHM", "KOR", 80), new("KWS", "JPN", 64), new("NSR", "KSA", 61), new("YFM", "JPN", 60),
        /*  7 */ new("POH", "KOR", 56), new("DUH", "QAT", 53), new("URW", "JPN", 49), new("BGP", "THA", 46), new("AIN", "UAE", 43), new("PRS", "IRN", 38),
        /* 13 */ new("DAE", "KOR", 38), new("JDT", "MAS", 38), new("SHA", "UAE", 37), new("KIT", "HKG", 34), new("SAD", "QAT", 34), new("NAS", "UZB", 33),
        /* 19 */ new("FOO", "IRN", 32), new("PKT", "UZB", 29), new("IHD", "KSA", 28), new("SDT", "CHN", 28), new("SHB", "KSA", 28), new("NGG", "JPN", 28),
        /* 25 */ new("IST", "TJK", 28), new("QWJ", "IRQ", 28), new("MCY", "AUS", 27), new("SEP", "IRN", 26), new("SBA", "UAE", 26), new("RYN", "QAT", 25),
        /* 31 */ new("WAH", "UAE", 23), new("VSK", "JPN", 21), new("BKU", "THA", 21), new("NAV", "UZB", 20), new("CEO", "JPN", 20), new("LCS", "SIN", 19),
        /* 37 */ new("WEH", "JOR", 19), new("VFK", "JPN", 17), new("ESG", "IRN", 17), new("TRC", "IRN", 16), new("FAY", "KSA", 15), new("FAI", "KSA", 15),
        /* 43 */ new("ICU", "KOR", 15), new("CRU", "THA", 15), new("AHA", "TKM", 14), new("AGM", "UZB", 13), new("MUC", "IND", 13), new("GBO", "JPN", 12),
        /* 49 */ new("ALS", "KSA", 12), new("JND", "KOR", 11), new("PRT", "THA", 11), new("ZHE", "CHN", 10), new("TAA", "KSA", 10), new("UCF", "PHI", 10),
        /* 55 */ new("BRU", "THA", 9), new("NSM", "IRN", 9), new("FAA", "JOR", 9), new("HAN", "VIE", 9), new("VTL", "VIE", 9), new("WUT", "CHN", 8),
        /* 61 */ new("HAG", "VIE", 8), new("GHR", "QAT", 8), new("JAZ", "UAE", 7), new("KAY", "PHI", 6), new("GZF", "CHN", 6), new("SHO", "IRQ", 6),
        /* 67 */ new("GOA", "IND", 6), new("SYD", "AUS", 5), new("RAT", "THA", 5), new("BJG", "CHN", 4), new("TAM", "SIN", 3),
    ];

    private static VereinsranglisteSeed.Eintrag[] Ofc() =>
    [
        /*  1 */ new("ACK", "NZL", 66), new("PIR", "TAH", 31), new("REW", "FIJ", 20), new("VNS", "TAH", 18), new("SUV", "FIJ", 17), new("IBB", "VAN", 17),
        /*  7 */ new("MAG", "NCL", 13), new("CCT", "SOL", 12), new("HIE", "NCL", 12), new("HEK", "PNG", 12), new("SLW", "SOL", 9), new("GLX", "VAN", 7),
        /* 13 */ new("TGS", "NCL", 6), new("LAE", "PNG", 4), new("VAI", "SAM", 3), new("LUP", "SAM", 3), new("NIK", "COK", 3),
    ];

    private static VereinsranglisteSeed.Eintrag[] Concacaf() =>
    [
        /*  1 */ new("MTY", "MEX", 52), new("AME", "MEX", 44), new("PHU", "USA", 41), new("CRZ", "MEX", 39), new("CMB", "USA", 37), new("PAC", "MEX", 34),
        /*  7 */ new("TIG", "MEX", 32), new("SEA", "USA", 28), new("LAF", "USA", 25), new("PUM", "MEX", 24), new("NYC", "USA", 19), new("NER", "USA", 19),
        /* 13 */ new("ATL", "USA", 13), new("ALJ", "CRC", 13), new("VIO", "HAI", 12), new("MOT", "HON", 12), new("COM", "GUA", 12), new("OLH", "HON", 12),
        /* 19 */ new("PTI", "USA", 11), new("MIA", "USA", 10), new("HER", "CRC", 10), new("ATS", "MEX", 10), new("MTL", "CAN", 10), new("TOF", "CAN", 10),
        /* 25 */ new("VWC", "CAN", 9), new("ORL", "USA", 9), new("SAP", "CRC", 7), new("CHV", "MEX", 6), new("ATX", "USA", 6), new("RES", "HON", 6),
        /* 31 */ new("CRA", "USA", 6), new("SLG", "MEX", 6), new("HOU", "USA", 4), new("NSH", "USA", 4), new("ROB", "SUR", 4), new("ALZ", "SLV", 4),
        /* 37 */ new("MRT", "HON", 4), new("ARC", "HAI", 4), new("CIN", "USA", 3), new("TAU", "PAN", 3), new("CAV", "HAI", 3), new("FOG", "CAN", 3),
        /* 43 */ new("GTY", "GUA", 3), new("SDG", "CRC", 3), new("ATP", "DOM", 3), new("RET", "NCA", 3),
    ];
}
