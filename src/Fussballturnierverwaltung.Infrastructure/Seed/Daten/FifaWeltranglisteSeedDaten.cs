namespace Fussballturnierverwaltung.Infrastructure.Seed.Daten;

/// <summary>FIFA-Weltrangliste der Männer, Ausgabe vom 07.10.2026, vollständig mit allen 211 Mitgliedsverbänden
/// (Issue #579) — fester Stand im Seed, unabhängig von einer Import-Quelle. Abgerufen über die Schnittstelle, die
/// inside.fifa.com selbst nutzt (<c>api.fifa.com/api/v3/fifarankings/rankings/rankingsbyschedule</c>, Ausgabe
/// <c>FRS_Male_Football_20260720</c>). Der Rang ergibt sich aus der Position in der Liste (die Ausgabe hat keine
/// geteilten Plätze); Punkte auf ganze Zahlen gerundet.
///
/// Kürzel sind die <see cref="Domain.Entitaeten.Stammdaten.Land.FifaCode"/>s des Seeds. Vier weichen vom
/// FIFA-Kürzel ab und bleiben, weil der Länder-Seed über das Kürzel nachrüstet: Kosovo KVX (FIFA KOS), Libyen LBA
/// (LBY), Madagaskar MDG (MAD), Singapur SIN (SGP).</summary>
internal static class FifaWeltranglisteSeedDaten
{
    public const string RANGLISTE = "FIFA-Weltrangliste (Herren)";
    public static readonly DateOnly DATUM = new(2026, 10, 7);
    public const string QUELLE_TEXT = "FIFA/Coca-Cola-Weltrangliste der Männer vom 07.10.2026 (inside.fifa.com)";

    public sealed record Eintrag(string FifaCode, int Punkte);

    public static readonly Eintrag[] Eintraege =
    [
        /*   1 */ new("ESP", 2012), new("ARG", 1973), new("FRA", 1959), new("ENG", 1926), new("POR", 1808), new("BRA", 1807), new("MAR", 1805), new("BEL", 1779),
        /*   9 */ new("NED", 1779), new("MEX", 1738), new("COL", 1736), new("SUI", 1725), new("GER", 1718), new("CRO", 1715), new("ITA", 1713), new("USA", 1705),
        /*  17 */ new("JPN", 1685), new("SEN", 1649), new("NOR", 1638), new("URU", 1636), new("DEN", 1620), new("AUT", 1601), new("IRN", 1597), new("ECU", 1587),
        /*  25 */ new("EGY", 1586), new("AUS", 1581), new("CAN", 1575), new("ALG", 1575), new("CIV", 1569), new("NGA", 1568), new("TUR", 1564), new("KOR", 1562),
        /*  33 */ new("PAR", 1552), new("SWE", 1544), new("RUS", 1537), new("UKR", 1535), new("POL", 1515), new("HUN", 1511), new("WAL", 1510), new("COD", 1506),
        /*  41 */ new("GRE", 1499), new("CMR", 1485), new("SCO", 1484), new("SRB", 1482), new("SVK", 1477), new("PAN", 1477), new("MLI", 1474), new("RSA", 1469),
        /*  49 */ new("VEN", 1466), new("CHI", 1459), new("CZE", 1457), new("SVN", 1452), new("PER", 1451), new("CRC", 1445), new("KSA", 1443), new("IRL", 1439),
        /*  57 */ new("BIH", 1429), new("ROU", 1429), new("UZB", 1417), new("QAT", 1416), new("TUN", 1415), new("BFA", 1408), new("IRQ", 1401), new("NIR", 1384),
        /*  65 */ new("UAE", 1377), new("ALB", 1376), new("HON", 1375), new("CPV", 1374), new("JAM", 1362), new("GHA", 1361), new("MKD", 1351), new("JOR", 1351),
        /*  73 */ new("ISL", 1349), new("GEO", 1346), new("FIN", 1341), new("KVX", 1328), new("ISR", 1326), new("BOL", 1322), new("MNE", 1315), new("CUW", 1305),
        /*  81 */ new("OMA", 1303), new("GUI", 1297), new("ANG", 1282), new("HAI", 1281), new("GAB", 1280), new("SYR", 1280), new("UGA", 1278), new("NZL", 1277),
        /*  89 */ new("BEN", 1266), new("BUL", 1263), new("ZAM", 1261), new("PLE", 1254), new("THA", 1251), new("BLR", 1250), new("GUA", 1245), new("CHN", 1242),
        /*  97 */ new("BHR", 1240), new("MOZ", 1238), new("VIE", 1231), new("TJK", 1219), new("LUX", 1219), new("SLV", 1212), new("EQG", 1200), new("KGZ", 1200),
        /* 105 */ new("TRI", 1199), new("MDG", 1197), new("COM", 1192), new("ARM", 1183), new("GAM", 1183), new("MTN", 1176), new("NIG", 1173), new("LBN", 1170),
        /* 113 */ new("LBA", 1169), new("KEN", 1169), new("TAN", 1167), new("IDN", 1167), new("KAZ", 1166), new("SDN", 1157), new("RWA", 1156), new("TOG", 1153),
        /* 121 */ new("PRK", 1151), new("EST", 1147), new("NAM", 1146), new("GNB", 1144), new("CYP", 1142), new("FRO", 1140), new("AZE", 1139), new("SUR", 1138),
        /* 129 */ new("ZIM", 1128), new("SLE", 1123), new("MWI", 1122), new("CGO", 1102), new("MAS", 1102), new("KUW", 1099), new("DOM", 1097), new("PHI", 1096),
        /* 137 */ new("NCA", 1091), new("IND", 1086), new("TKM", 1079), new("BDI", 1077), new("BOT", 1074), new("GUY", 1071), new("LVA", 1069), new("YEM", 1068),
        /* 145 */ new("CTA", 1065), new("LBR", 1065), new("ETH", 1064), new("LTU", 1059), new("SIN", 1059), new("LES", 1053), new("SKN", 1041), new("SOL", 1036),
        /* 153 */ new("HKG", 1035), new("FIJ", 1033), new("PUR", 1033), new("NCL", 1026), new("TAH", 1016), new("MDA", 1016), new("MYA", 1008), new("MLT", 1007),
        /* 161 */ new("VAN", 1006), new("ATG", 993), new("CUB", 990), new("SWZ", 979), new("LCA", 976), new("VIN", 975), new("PNG", 969), new("AFG", 968),
        /* 169 */ new("GRN", 968), new("SSD", 960), new("BER", 951), new("MSR", 939), new("MDV", 936), new("BRB", 935), new("AND", 928), new("TPE", 924),
        /* 177 */ new("CAM", 921), new("NEP", 915), new("MRI", 906), new("BLZ", 900), new("CHA", 897), new("BAN", 893), new("ERI", 891), new("SRI", 891),
        /* 185 */ new("ARU", 890), new("DMA", 885), new("COK", 880), new("LAO", 880), new("SAM", 876), new("MNG", 874), new("ASA", 872), new("BHU", 871),
        /* 193 */ new("MAC", 858), new("STP", 855), new("DJI", 854), new("BRU", 850), new("PAK", 839), new("CAY", 836), new("TGA", 836), new("SOM", 832),
        /* 201 */ new("GIB", 829), new("TLS", 823), new("GUM", 820), new("TCA", 806), new("SEY", 796), new("VIR", 795), new("LIE", 790), new("BAH", 772),
        /* 209 */ new("VGB", 753), new("AIA", 737), new("SMR", 716),
    ];
}
