using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Infrastructure.Seed.Daten;

public static class VereinsmannschaftenSeedDaten
{
    private record E(string Name, string Kurz, string Land, int Off, int Mid, int Def, int Heim, int Ausw);

    private static readonly E[] Eintraege =
    [
        // ── Bundesliga (Saison 2025/26) ────────────────────────────────────
        new("Bayern München",          "FCB", "GER", 90, 88, 86, 95, 82),
        new("Bayer 04 Leverkusen",     "B04", "GER", 87, 86, 84, 88, 80),
        new("Borussia Dortmund",       "BVB", "GER", 84, 82, 78, 88, 76),
        new("RB Leipzig",              "RBL", "GER", 83, 82, 80, 84, 76),
        new("Eintracht Frankfurt",     "SGE", "GER", 79, 78, 77, 82, 73),
        new("VfB Stuttgart",           "VFB", "GER", 78, 77, 76, 80, 72),
        new("TSG 1899 Hoffenheim",     "TSG", "GER", 75, 75, 73, 77, 69),
        new("SC Freiburg",             "SCF", "GER", 74, 74, 74, 78, 69),
        new("1. FC Union Berlin",      "UNB", "GER", 73, 73, 73, 79, 68),
        new("SV Werder Bremen",        "SVW", "GER", 73, 73, 72, 76, 67),
        new("VfL Wolfsburg",           "WOB", "GER", 74, 73, 72, 76, 68),
        new("Borussia Mönchengladbach","BMG", "GER", 73, 73, 71, 76, 67),
        new("1. FSV Mainz 05",         "M05", "GER", 72, 72, 71, 75, 67),
        new("FC Augsburg",             "FCA", "GER", 70, 70, 70, 73, 65),
        new("FC St. Pauli",            "STPA","GER", 68, 68, 67, 73, 62),
        new("1. FC Heidenheim",        "HEI", "GER", 67, 67, 66, 70, 61),
        new("Hamburger SV",            "HSV", "GER", 73, 72, 71, 77, 67),
        new("1. FC Köln",              "KOE", "GER", 70, 69, 68, 75, 63),

        // ── 2. Bundesliga (Saison 2025/26) ─────────────────────────────────
        new("VfL Bochum",              "BOC", "GER", 68, 68, 68, 71, 62),
        new("Holstein Kiel",           "KIE", "GER", 67, 67, 67, 70, 61),
        new("Schalke 04",              "S04", "GER", 71, 70, 70, 76, 64),
        new("Hannover 96",             "H96", "GER", 69, 69, 68, 72, 63),
        new("1. FC Nürnberg",          "FCN", "GER", 68, 68, 67, 71, 62),
        new("Fortuna Düsseldorf",      "F95", "GER", 68, 67, 67, 70, 62),
        new("Karlsruher SC",           "KSC", "GER", 67, 66, 66, 69, 60),
        new("SV Darmstadt 98",         "D98", "GER", 66, 66, 65, 68, 60),
        new("Hertha BSC Berlin",       "BSC", "GER", 69, 68, 67, 71, 62),
        new("SpVgg Greuther Fürth",    "SPV", "GER", 66, 65, 65, 68, 60),
        new("1. FC Kaiserslautern",    "KAI", "GER", 67, 66, 66, 70, 60),
        new("SC Paderborn 07",         "SCP", "GER", 65, 65, 64, 68, 59),
        new("1. FC Magdeburg",         "FCM", "GER", 65, 65, 64, 68, 59),
        new("SV Elversberg",           "SVE", "GER", 65, 64, 63, 67, 58),
        new("Preußen Münster",         "PRM", "GER", 63, 63, 62, 66, 56),
        new("Eintracht Braunschweig",  "EBR", "GER", 63, 62, 62, 65, 56),
        new("Arminia Bielefeld",       "DSC", "GER", 65, 64, 64, 69, 58),
        new("Dynamo Dresden",          "SGD", "GER", 64, 64, 63, 69, 57),

        // ── 3. Liga (Saison 2025/26) ───────────────────────────────────────
        // VfB Stuttgart II / TSG Hoffenheim II bewusst weggelassen: Reserveteams sind im DFB-Pokal
        // nicht zugelassen, würden aber über den Land-/Vereinsfilter der Pokal-Vorlage mit ausgelost (#351).
        new("SSV Ulm 1846",            "ULM", "GER", 64, 63, 63, 67, 57),
        new("SV Wehen Wiesbaden",      "WIE", "GER", 62, 62, 61, 64, 55),
        new("Jahn Regensburg",         "JRG", "GER", 62, 61, 61, 64, 55),
        new("1. FC Saarbrücken",       "SAA", "GER", 62, 61, 61, 66, 56),
        new("FC Energie Cottbus",      "FCE", "GER", 62, 61, 60, 66, 55),
        new("FC Hansa Rostock",        "HRO", "GER", 62, 61, 61, 67, 55),
        new("TSV 1860 München",        "M60", "GER", 61, 61, 60, 66, 55),
        new("FC Ingolstadt 04",        "FCI", "GER", 61, 60, 60, 64, 55),
        new("MSV Duisburg",            "MSV", "GER", 61, 60, 59, 66, 55),
        new("VfL Osnabrück",           "OSN", "GER", 61, 60, 60, 65, 55),
        new("Rot-Weiss Essen",         "RWE", "GER", 61, 60, 60, 66, 54),
        new("Alemannia Aachen",        "AAC", "GER", 60, 60, 60, 65, 54),
        new("SV Waldhof Mannheim",     "WAL", "GER", 60, 59, 59, 64, 54),
        new("SC Verl",                 "VRL", "GER", 60, 60, 59, 63, 54),
        new("FC Erzgebirge Aue",       "AUE", "GER", 60, 59, 59, 64, 53),
        new("FC Viktoria Köln",        "VIK", "GER", 59, 59, 58, 62, 53),
        new("TSV Havelse",             "HAV", "GER", 56, 56, 56, 60, 51),
        new("1. FC Schweinfurt 05",    "SWF", "GER", 56, 56, 55, 60, 50),

        // ── Regionalliga (DFB-Pokal-Teilnehmer über Landespokale) ──────────
        new("Kickers Offenbach",       "OFC", "GER", 56, 55, 55, 60, 50),
        new("SV Sandhausen",           "SVS", "GER", 55, 55, 54, 59, 50),
        new("1. FC Lokomotive Leipzig", "LLE", "GER", 55, 55, 54, 60, 49),
        new("FC Carl Zeiss Jena",      "FCC", "GER", 55, 55, 54, 59, 49),
        new("Hallescher FC",           "HFC", "GER", 54, 54, 54, 59, 49),
        new("SpVgg Unterhaching",      "SPU", "GER", 54, 54, 53, 58, 49),
        new("Rot-Weiß Oberhausen",     "RWO", "GER", 54, 53, 53, 58, 48),
        new("Chemnitzer FC",           "CFC", "GER", 53, 53, 53, 58, 48),
        new("Würzburger Kickers",      "FWK", "GER", 53, 53, 52, 57, 48),
        new("Wuppertaler SV",          "WSV", "GER", 53, 52, 52, 57, 47),

        // ── La Liga ───────────────────────────────────────────────────────
        new("Real Madrid",             "RMA", "ESP", 91, 87, 85, 93, 83),
        new("FC Barcelona",            "BAR", "ESP", 88, 89, 84, 92, 80),
        new("Atlético Madrid",         "ATM", "ESP", 82, 81, 86, 86, 76),
        new("Athletic Club Bilbao",    "ATH", "ESP", 77, 76, 76, 82, 71),
        new("Real Sociedad",           "RSO", "ESP", 76, 77, 74, 79, 70),
        new("Villarreal CF",           "VIL", "ESP", 78, 76, 75, 80, 72),
        new("Real Betis",              "BET", "ESP", 75, 75, 73, 79, 69),
        new("Sevilla FC",              "SEV", "ESP", 77, 76, 76, 80, 71),
        new("Valencia CF",             "VCF", "ESP", 74, 73, 73, 78, 68),
        new("Girona FC",               "GIR", "ESP", 75, 74, 73, 76, 69),
        new("CA Osasuna",              "OSA", "ESP", 71, 71, 72, 74, 65),
        new("Getafe CF",               "GET", "ESP", 69, 69, 71, 72, 63),
        new("Celta Vigo",              "CEL", "ESP", 72, 71, 70, 74, 66),
        new("Rayo Vallecano",          "RAY", "ESP", 68, 68, 68, 72, 62),
        new("RCD Espanyol",            "EPN", "ESP", 69, 68, 68, 71, 63),
        new("UD Las Palmas",           "LPA", "ESP", 68, 67, 67, 70, 62),
        new("RCD Mallorca",            "MLL", "ESP", 67, 67, 68, 71, 61),
        new("Deportivo Alavés",        "ALV", "ESP", 66, 66, 66, 69, 60),
        new("Real Valladolid",         "VLD", "ESP", 64, 64, 64, 67, 58),
        new("CD Leganés",              "LEG", "ESP", 64, 63, 64, 67, 57),

        // ── Premier League (Saison 2025/26) ────────────────────────────────
        new("Manchester City",         "MCI", "ENG", 91, 90, 87, 90, 85),
        new("FC Liverpool",            "LIV", "ENG", 88, 86, 84, 91, 82),
        new("Arsenal FC",              "ARS", "ENG", 84, 84, 81, 87, 77),
        new("Chelsea FC",              "CHE", "ENG", 83, 83, 81, 85, 77),
        new("Tottenham Hotspur",       "TOT", "ENG", 82, 81, 78, 82, 75),
        new("Manchester United",       "MNU", "ENG", 81, 80, 78, 83, 73),
        new("Aston Villa",             "AVL", "ENG", 80, 79, 77, 81, 73),
        new("Newcastle United",        "NEW", "ENG", 79, 78, 77, 80, 72),
        new("Brighton & Hove Albion",  "BHA", "ENG", 78, 80, 76, 78, 72),
        new("West Ham United",         "WHU", "ENG", 77, 76, 74, 79, 70),
        new("Brentford FC",            "BRE", "ENG", 76, 75, 74, 78, 70),
        new("Fulham FC",               "FUL", "ENG", 76, 75, 74, 78, 70),
        new("Crystal Palace",          "CRY", "ENG", 74, 73, 73, 76, 68),
        new("Wolverhampton Wanderers", "WOL", "ENG", 73, 72, 73, 75, 67),
        new("Nottingham Forest",       "NFO", "ENG", 73, 72, 73, 75, 67),
        new("Everton FC",              "EVE", "ENG", 72, 71, 71, 74, 66),
        new("AFC Bournemouth",         "BOU", "ENG", 71, 70, 70, 73, 65),
        new("Leeds United",            "LEE", "ENG", 72, 71, 70, 76, 65),
        new("Sunderland AFC",          "SUN", "ENG", 69, 68, 68, 74, 62),
        new("Burnley FC",              "BUR", "ENG", 69, 68, 69, 72, 63),

        // ── Championship (Saison 2025/26) ──────────────────────────────────
        new("Leicester City",          "LEI", "ENG", 71, 70, 70, 73, 65),
        new("Southampton FC",          "SOU", "ENG", 68, 67, 67, 70, 62),
        new("Ipswich Town",            "IPS", "ENG", 66, 65, 65, 68, 60),
        new("Sheffield United",        "SHU", "ENG", 68, 67, 67, 71, 62),
        new("Middlesbrough FC",        "MID", "ENG", 67, 67, 66, 70, 61),
        new("Coventry City",           "COV", "ENG", 67, 66, 66, 70, 61),
        new("Birmingham City",         "BIR", "ENG", 66, 65, 64, 70, 60),
        new("West Bromwich Albion",    "WBA", "ENG", 66, 66, 65, 69, 60),
        new("Norwich City",            "NOR", "ENG", 66, 65, 64, 69, 60),
        new("Bristol City",            "BRC", "ENG", 66, 65, 65, 69, 60),
        new("Blackburn Rovers",        "BLB", "ENG", 65, 65, 64, 68, 59),
        new("Watford FC",              "WAT", "ENG", 65, 64, 63, 68, 59),
        new("Wrexham AFC",             "WRX", "ENG", 65, 64, 63, 69, 59),
        new("Millwall FC",             "MLW", "ENG", 64, 64, 65, 69, 58),
        new("Stoke City",              "STK", "ENG", 64, 63, 63, 67, 58),
        new("Hull City",               "HUL", "ENG", 64, 63, 62, 67, 58),
        new("Swansea City",            "SWA", "ENG", 64, 65, 63, 67, 58),
        new("Preston North End",       "PNE", "ENG", 63, 63, 64, 67, 57),
        new("Queens Park Rangers",     "QPR", "ENG", 63, 63, 62, 67, 57),
        new("Derby County",            "DER", "ENG", 63, 63, 62, 67, 57),
        new("Portsmouth FC",           "POR", "ENG", 63, 62, 62, 68, 56),
        new("Sheffield Wednesday",     "SHW", "ENG", 63, 62, 62, 67, 57),
        new("Charlton Athletic",       "CHA", "ENG", 63, 62, 62, 66, 57),
        new("Oxford United",           "OXF", "ENG", 62, 62, 62, 66, 56),

        // ── League One (Saison 2025/26) ────────────────────────────────────
        new("Luton Town",              "LUT", "ENG", 62, 61, 61, 66, 56),
        new("Cardiff City",            "CAR", "ENG", 61, 60, 60, 65, 55),
        new("Plymouth Argyle",         "PLY", "ENG", 60, 59, 59, 64, 54),
        new("Huddersfield Town",       "HUD", "ENG", 60, 59, 58, 63, 54),
        new("Stockport County",        "STO", "ENG", 59, 59, 58, 63, 54),
        new("Bolton Wanderers",        "BLT", "ENG", 59, 58, 58, 63, 53),
        new("Wycombe Wanderers",       "WYC", "ENG", 59, 58, 58, 62, 53),
        new("Leyton Orient",           "LEY", "ENG", 58, 58, 57, 61, 53),
        new("Reading FC",              "REA", "ENG", 58, 58, 57, 62, 53),
        new("Doncaster Rovers",        "DON", "ENG", 57, 57, 56, 61, 52),
        new("Bradford City",           "BRA", "ENG", 57, 56, 56, 62, 51),
        new("Blackpool FC",            "BPL", "ENG", 57, 57, 56, 61, 52),
        new("Lincoln City",            "LIN", "ENG", 57, 57, 57, 61, 52),
        new("Barnsley FC",             "BNS", "ENG", 57, 56, 56, 61, 51),
        new("Rotherham United",        "ROT", "ENG", 57, 56, 56, 61, 51),
        new("Peterborough United",     "PET", "ENG", 57, 57, 55, 61, 51),
        new("Port Vale",               "PVA", "ENG", 56, 56, 56, 60, 51),
        new("AFC Wimbledon",           "WIM", "ENG", 56, 56, 55, 60, 51),
        new("Wigan Athletic",          "WIG", "ENG", 56, 56, 56, 60, 51),
        new("Stevenage FC",            "STE", "ENG", 55, 55, 56, 59, 50),
        new("Mansfield Town",          "MFT", "ENG", 55, 55, 55, 59, 50),
        new("Exeter City",             "EXE", "ENG", 55, 55, 55, 59, 50),
        new("Burton Albion",           "BUA", "ENG", 54, 54, 54, 58, 49),
        new("Northampton Town",        "NTH", "ENG", 54, 54, 54, 58, 49),

        // ── Ligue 1 ───────────────────────────────────────────────────────
        new("Paris Saint-Germain",     "PSG", "FRA", 92, 88, 85, 91, 84),
        new("Olympique de Marseille",  "OM",  "FRA", 79, 77, 75, 83, 70),
        new("Olympique Lyonnais",      "OL",  "FRA", 78, 78, 75, 80, 71),
        new("AS Monaco",               "MON", "FRA", 79, 77, 76, 80, 72),
        new("LOSC Lille",              "LOS", "FRA", 77, 76, 75, 79, 70),
        new("OGC Nice",                "OGN", "FRA", 75, 74, 73, 77, 68),
        new("RC Strasbourg",           "STR", "FRA", 72, 72, 71, 75, 66),
        new("Stade de Reims",          "RMS", "FRA", 72, 71, 71, 74, 66),
        new("RC Lens",                 "LNS", "FRA", 73, 72, 72, 76, 67),
        new("Stade Rennais",           "REN", "FRA", 73, 72, 71, 75, 67),
        new("Montpellier HSC",         "MHC", "FRA", 69, 69, 68, 72, 63),
        new("Toulouse FC",             "TFC", "FRA", 70, 69, 68, 72, 64),
        new("Le Havre AC",             "LHV", "FRA", 67, 67, 66, 70, 61),
        new("Stade Brestois 29",       "SBR", "FRA", 68, 67, 67, 70, 62),
        new("FC Nantes",               "NAN", "FRA", 68, 67, 67, 71, 62),
        new("AJ Auxerre",              "AJA", "FRA", 66, 65, 65, 68, 60),
        new("Angers SCO",              "ANG", "FRA", 65, 65, 64, 68, 59),
        new("AS Saint-Étienne",        "ASS", "FRA", 64, 63, 63, 69, 57),

        // ── Serie A ───────────────────────────────────────────────────────
        new("Inter Mailand",           "INT", "ITA", 84, 83, 84, 86, 77),
        new("Juventus Turin",          "JUV", "ITA", 83, 81, 83, 87, 76),
        new("AC Mailand",              "ACM", "ITA", 84, 82, 82, 86, 77),
        new("SSC Napoli",              "NAP", "ITA", 83, 81, 81, 85, 76),
        new("AS Roma",                 "ROM", "ITA", 81, 79, 78, 83, 74),
        new("SS Lazio",                "LAZ", "ITA", 79, 77, 76, 80, 72),
        new("Atalanta BC",             "ATA", "ITA", 82, 81, 80, 82, 75),
        new("ACF Fiorentina",          "FIO", "ITA", 77, 76, 75, 79, 71),
        new("Bologna FC",              "BOL", "ITA", 75, 75, 74, 77, 69),
        new("Torino FC",               "TOR", "ITA", 72, 71, 71, 74, 66),
        new("Udinese Calcio",          "UDI", "ITA", 69, 68, 68, 72, 63),
        new("Cagliari Calcio",         "CAG", "ITA", 68, 67, 67, 71, 62),
        new("Genoa CFC",               "GEN", "ITA", 68, 67, 67, 71, 62),
        new("Venezia FC",              "VFC", "ITA", 65, 64, 64, 68, 59),
        new("Como 1907",               "CMO", "ITA", 66, 65, 64, 68, 60),
        new("AC Monza",                "MNZ", "ITA", 67, 66, 65, 69, 61),
        new("Hellas Verona",           "HVR", "ITA", 67, 66, 65, 70, 61),
        new("Empoli FC",               "EMP", "ITA", 65, 65, 64, 68, 59),
        new("Parma Calcio 1913",       "PCA", "ITA", 66, 65, 64, 69, 60),
        new("US Lecce",                "LEC", "ITA", 65, 64, 64, 68, 59),

        // ── Niederlande (Eredivisie Top 5) ────────────────────────────────
        new("Ajax Amsterdam",          "AJX", "NED", 83, 82, 79, 86, 76),
        new("PSV Eindhoven",           "PSV", "NED", 82, 81, 79, 84, 75),
        new("Feyenoord Rotterdam",     "FEY", "NED", 80, 80, 78, 83, 73),
        new("AZ Alkmaar",              "AZA", "NED", 75, 74, 73, 77, 69),
        new("FC Utrecht",              "UTR", "NED", 72, 71, 70, 74, 66),

        // ── Portugal (Primeira Liga Top 5) ────────────────────────────────
        new("SL Benfica",              "SLB", "POR", 83, 81, 80, 85, 76),
        new("FC Porto",                "FCP", "POR", 82, 81, 80, 85, 75),
        new("Sporting CP",             "SPS", "POR", 81, 80, 79, 83, 74),
        new("SC Braga",                "SCB", "POR", 76, 75, 73, 78, 70),
        new("Vitória SC",              "VIT", "POR", 70, 69, 68, 72, 64),

        // ── Belgien (Jupiler Pro League Top 5) ───────────────────────────
        new("Club Brugge KV",          "CLB", "BEL", 78, 77, 75, 80, 72),
        new("RSC Anderlecht",          "RSA", "BEL", 76, 75, 73, 78, 70),
        new("KRC Genk",                "GNK", "BEL", 74, 73, 72, 76, 68),
        new("KAA Gent",                "GNT", "BEL", 73, 72, 71, 75, 67),
        new("Royal Antwerp FC",        "ANT", "BEL", 74, 73, 72, 76, 68),

        // ── Schottland (Premiership Top 5) ───────────────────────────────
        new("Celtic FC",               "CLT", "SCO", 79, 77, 76, 82, 72),
        new("Rangers FC",              "RAN", "SCO", 78, 77, 75, 81, 71),
        new("Aberdeen FC",             "ABE", "SCO", 69, 68, 68, 72, 63),
        new("Heart of Midlothian",     "HRT", "SCO", 68, 67, 67, 71, 62),
        new("Hibernian FC",            "HIB", "SCO", 68, 67, 67, 71, 62),

        // ── Türkei (Süper Lig Top 5) ──────────────────────────────────────
        new("Galatasaray SK",          "GAL", "TUR", 79, 78, 77, 84, 72),
        new("Fenerbahçe SK",           "FEN", "TUR", 79, 77, 76, 83, 71),
        new("Beşiktaş JK",             "BJK", "TUR", 76, 75, 73, 80, 68),
        new("Trabzonspor",             "TRA", "TUR", 73, 72, 71, 77, 65),
        new("İstanbul Başakşehir",     "BSK", "TUR", 71, 70, 70, 73, 64),

        // ── Russland (Premier Liga Top 5) ────────────────────────────────
        new("Zenit St. Petersburg",    "ZEN", "RUS", 77, 76, 75, 80, 70),
        new("CSKA Moskau",             "CSK", "RUS", 75, 74, 73, 78, 68),
        new("Spartak Moskau",          "SPK", "RUS", 74, 73, 72, 77, 67),
        new("Lokomotive Moskau",       "LOK", "RUS", 72, 71, 70, 75, 65),
        new("FK Krasnodar",            "KRD", "RUS", 71, 70, 69, 73, 64),

        // ── Ukraine (Premier Liga Top 5) ──────────────────────────────────
        new("Dynamo Kyiv",             "DYN", "UKR", 74, 73, 73, 77, 68),
        new("Schachtar Donezk",        "SCH", "UKR", 76, 75, 73, 79, 70),
        new("FC Dnipro-1",             "DNP", "UKR", 68, 67, 67, 70, 62),
        new("Metalist Charkiw",        "MET", "UKR", 66, 65, 65, 68, 60),
        new("Vorskla Poltawa",         "VOR", "UKR", 65, 64, 64, 67, 59),

        // ── Griechenland (Super League Top 5) ────────────────────────────
        new("Olympiakos Piräus",       "OLY", "GRE", 72, 71, 72, 76, 66),
        new("Panathinaikos",           "PAO", "GRE", 70, 69, 69, 73, 64),
        new("AEK Athen",               "AEK", "GRE", 70, 69, 69, 73, 64),
        new("PAOK Thessaloniki",       "PAK", "GRE", 70, 69, 69, 73, 64),
        new("Aris Thessaloniki",       "ARI", "GRE", 66, 65, 65, 69, 60),

        // ── Brasilien (Série A Top 5) ─────────────────────────────────────
        new("Flamengo",                "FLA", "BRA", 83, 80, 78, 88, 73),
        new("SE Palmeiras",            "PAL", "BRA", 83, 81, 80, 88, 74),
        new("São Paulo FC",            "SPF", "BRA", 77, 76, 75, 80, 70),
        new("SC Corinthians",          "COR", "BRA", 76, 75, 74, 81, 69),
        new("Fluminense FC",           "FLU", "BRA", 76, 75, 74, 79, 69),

        // ── Argentinien (Primera División Top 5) ─────────────────────────
        new("River Plate",             "RIV", "ARG", 82, 80, 79, 87, 72),
        new("Boca Juniors",            "BOJ", "ARG", 82, 80, 79, 87, 73),
        new("Racing Club",             "RAC", "ARG", 76, 75, 74, 79, 69),
        new("CA Independiente",        "INB", "ARG", 73, 72, 72, 76, 67),
        new("San Lorenzo",             "SLO", "ARG", 72, 71, 71, 75, 66),

        // ── Kolumbien (Liga BetPlay Top 5) ────────────────────────────────
        new("Atlético Nacional",       "NAL", "COL", 73, 72, 71, 76, 66),
        new("Millonarios FC",          "MIL", "COL", 71, 70, 69, 74, 64),
        new("Junior FC",               "JFC", "COL", 69, 68, 67, 73, 62),
        new("Deportivo Cali",          "DCA", "COL", 68, 67, 66, 71, 61),
        new("Once Caldas",             "OCA", "COL", 65, 64, 63, 68, 58),

        // ── Chile (Primera División Top 5) ────────────────────────────────
        new("Colo-Colo",               "CCO", "CHI", 72, 71, 70, 77, 64),
        new("Universidad de Chile",    "UCH", "CHI", 70, 69, 68, 74, 63),
        new("Universidad Católica",    "UCA", "CHI", 69, 68, 68, 73, 62),
        new("Audax Italiano",          "AUI", "CHI", 65, 64, 63, 68, 58),
        new("Huachipato",              "HUC", "CHI", 63, 62, 62, 66, 57),

        // ── USA (MLS Top 5) ───────────────────────────────────────────────
        new("LA Galaxy",               "LAG", "USA", 73, 72, 70, 74, 67),
        new("Inter Miami CF",          "MIA", "USA", 75, 73, 71, 75, 69),
        new("New England Revolution",  "NER", "USA", 70, 70, 69, 72, 65),
        new("Seattle Sounders FC",     "SEA", "USA", 71, 71, 70, 74, 65),
        new("New York City FC",        "NYC", "USA", 70, 70, 69, 72, 65),

        // ── Mexiko (Liga MX Top 5) ────────────────────────────────────────
        new("Club América",            "AME", "MEX", 79, 78, 76, 83, 72),
        new("Chivas Guadalajara",      "CHV", "MEX", 75, 74, 73, 81, 68),
        new("Cruz Azul",               "CRZ", "MEX", 74, 73, 72, 78, 67),
        new("Tigres UANL",             "TIG", "MEX", 75, 74, 73, 79, 69),
        new("CF Monterrey",            "MTY", "MEX", 75, 73, 73, 79, 68),

        // ── Saudi-Arabien (Saudi Pro League Top 5) ────────────────────────
        new("Al-Hilal",                "HIL", "KSA", 82, 80, 79, 85, 74),
        new("Al-Nassr",                "NSR", "KSA", 80, 78, 77, 82, 72),
        new("Al-Ittihad",              "IHD", "KSA", 78, 77, 76, 80, 71),
        new("Al-Ahli",                 "ALS", "KSA", 75, 74, 73, 77, 68),
        new("Al-Shabab",               "SHB", "KSA", 72, 71, 70, 74, 65),

        // ── Japan (J-League Top 5) ────────────────────────────────────────
        new("Kawasaki Frontale",       "KWS", "JPN", 72, 72, 71, 73, 66),
        new("Gamba Osaka",             "GBO", "JPN", 70, 70, 69, 72, 65),
        new("Urawa Red Diamonds",      "URW", "JPN", 70, 69, 69, 72, 64),
        new("Vissel Kobe",             "VSK", "JPN", 71, 70, 69, 72, 65),
        new("Sanfrecce Hiroshima",     "SFH", "JPN", 69, 69, 68, 71, 63),

        // ── Südkorea (K-League Top 5) ─────────────────────────────────────
        new("Jeonbuk Hyundai Motors",  "JHM", "KOR", 70, 69, 69, 72, 64),
        new("Ulsan HD FC",             "UHD", "KOR", 71, 70, 70, 73, 65),
        new("FC Seoul",                "FCS", "KOR", 69, 68, 68, 71, 63),
        new("Suwon Samsung Bluewings", "SSB", "KOR", 67, 66, 66, 69, 61),
        new("Gangwon FC",              "GWN", "KOR", 66, 65, 65, 68, 60),

        // ── Australien (A-League Top 5) ───────────────────────────────────
        new("Melbourne City FC",       "MCY", "AUS", 68, 67, 66, 70, 63),
        new("Sydney FC",               "SYD", "AUS", 67, 66, 65, 69, 62),
        new("Melbourne Victory",       "MEV", "AUS", 67, 66, 65, 69, 62),
        new("Western Sydney Wanderers","WSW", "AUS", 65, 64, 63, 67, 60),
        new("Perth Glory",             "PGL", "AUS", 64, 63, 62, 66, 58),

        // ── China (Super League Top 5) ────────────────────────────────────
        new("Shandong Taishan",        "SDT", "CHN", 68, 67, 67, 72, 62),
        new("Shanghai Port",           "SHP", "CHN", 67, 66, 66, 70, 60),
        new("Beijing Guoan",           "BJG", "CHN", 67, 66, 65, 71, 60),
        new("Guangzhou FC",            "GZF", "CHN", 65, 64, 63, 68, 58),
        new("Wuhan Three Towns",       "WUT", "CHN", 65, 64, 64, 69, 58),

        // ── Ägypten (Premier League Top 5) ───────────────────────────────
        new("Al Ahly SC",              "ALA", "EGY", 73, 72, 71, 77, 66),
        new("Zamalek SC",              "ZAM", "EGY", 71, 70, 70, 75, 64),
        new("Pyramids FC",             "PYR", "EGY", 68, 67, 66, 70, 62),
        new("El Gouna FC",             "ELG", "EGY", 63, 62, 61, 65, 57),
        new("Ceramica Cleopatra",      "CLC", "EGY", 62, 61, 60, 64, 56),

        // ── Marokko (Botola Pro Top 5) ────────────────────────────────────
        new("Wydad AC",                "WAC", "MAR", 71, 70, 70, 75, 64),
        new("Raja CA Casablanca",      "RAJ", "MAR", 71, 70, 70, 75, 64),
        new("FAR Rabat",               "FAR", "MAR", 65, 64, 64, 68, 59),
        new("RS Berkane",              "RSB", "MAR", 63, 62, 62, 66, 57),
        new("ASFAR Rabat",             "ASF", "MAR", 62, 61, 61, 65, 56),

        // ── Nigeria (NPFL Top 5) ──────────────────────────────────────────
        new("Enyimba FC",              "ENY", "NGA", 62, 61, 60, 65, 56),
        new("Rangers International",   "RNG", "NGA", 61, 60, 59, 64, 55),
        new("Kano Pillars",            "KNP", "NGA", 60, 59, 58, 63, 54),
        new("Lobi Stars",              "LOB", "NGA", 59, 58, 57, 62, 53),
        new("Plateau United",          "PLT", "NGA", 58, 57, 57, 61, 52),
    ];

    public static List<Mannschaft> Erstellen(IReadOnlyDictionary<string, int> laenderLookup) =>
        Eintraege
            .Where(e => laenderLookup.ContainsKey(e.Land))
            .Select(e => new Mannschaft
            {
                Name             = e.Name,
                Kurzname         = e.Kurz,
                Mannschaftsart   = Mannschaftsart.Verein,
                LandId           = laenderLookup[e.Land],
                OffensivRating   = e.Off,
                MittelfeldRating = e.Mid,
                DefensivRating   = e.Def,
                Heimstaerke      = e.Heim,
                Auswaertsstaerke = e.Ausw
            })
            .ToList();
}
