using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Infrastructure.Seed.Daten;

public static class VereinsmannschaftenSeedDaten
{
    private sealed record E(string Name, string Kurz, string Land, int Off, int Mid, int Def, int Heim, int Ausw);

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

        // ── Segunda División (Abschluss 2024/25, Issue #575) ───────────────
        // Rating G linear nach Tabellenplatz 2024/25, vom Ersten mit 67 bis zum Letzten mit 59;
        // O = G+1, M = G, D = G−1, Heim = G+3, Auswärts = G−5.
        new("Levante UD",              "LEV", "ESP", 68, 67, 66, 70, 62),
        new("Elche CF",                "ELC", "ESP", 68, 67, 66, 70, 62),
        new("Real Oviedo",             "OVI", "ESP", 67, 66, 65, 69, 61),
        new("CD Mirandés",             "MIR", "ESP", 67, 66, 65, 69, 61),
        new("Racing Santander",        "RSN", "ESP", 66, 65, 64, 68, 60),
        new("UD Almería",              "ALM", "ESP", 66, 65, 64, 68, 60),
        new("Granada CF",              "GRA", "ESP", 66, 65, 64, 68, 60),
        new("SD Huesca",               "HUE", "ESP", 65, 64, 63, 67, 59),
        new("SD Eibar",                "EIB", "ESP", 65, 64, 63, 67, 59),
        new("Albacete Balompié",       "ALB", "ESP", 65, 64, 63, 67, 59),
        new("Sporting Gijón",          "SPG", "ESP", 64, 63, 62, 66, 58),
        new("Burgos CF",               "BRG", "ESP", 64, 63, 62, 66, 58),
        new("Cádiz CF",                "CAD", "ESP", 63, 62, 61, 65, 57),
        new("Córdoba CF",              "CCF", "ESP", 63, 62, 61, 65, 57),
        new("Deportivo La Coruña",     "DEP", "ESP", 63, 62, 61, 65, 57),
        new("Málaga CF",               "MAL", "ESP", 62, 61, 60, 64, 56),
        new("CD Castellón",            "CAS", "ESP", 62, 61, 60, 64, 56),
        new("Real Zaragoza",           "ZAR", "ESP", 62, 61, 60, 64, 56),
        new("CD Eldense",              "ELD", "ESP", 61, 60, 59, 63, 55),
        new("CD Tenerife",             "TEN", "ESP", 61, 60, 59, 63, 55),
        new("Racing Ferrol",           "RFE", "ESP", 60, 59, 58, 62, 54),
        new("FC Cartagena",            "CRT", "ESP", 60, 59, 58, 62, 54),

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

        // ── Ligue 2 (Abschluss 2024/25, Issue #575) ────────────────────────
        // Rating G linear nach Tabellenplatz 2024/25, vom Ersten mit 67 bis zum Letzten mit 59;
        // O = G+1, M = G, D = G−1, Heim = G+3, Auswärts = G−5.
        new("FC Lorient",              "LOR", "FRA", 68, 67, 66, 70, 62),
        new("Paris FC",                "PFC", "FRA", 68, 67, 66, 70, 62),
        new("FC Metz",                 "MTZ", "FRA", 67, 66, 65, 69, 61),
        new("USL Dunkerque",           "USD", "FRA", 67, 66, 65, 69, 61),
        new("EA Guingamp",             "EAG", "FRA", 66, 65, 64, 68, 60),
        new("FC Annecy",               "ANN", "FRA", 66, 65, 64, 68, 60),
        new("Stade Lavallois",         "LAV", "FRA", 65, 64, 63, 67, 59),
        new("SC Bastia",               "BST", "FRA", 65, 64, 63, 67, 59),
        new("Grenoble Foot 38",        "GRE", "FRA", 64, 63, 62, 66, 58),
        new("ES Troyes AC",            "TRY", "FRA", 64, 63, 62, 66, 58),
        new("Amiens SC",               "AMI", "FRA", 63, 62, 61, 65, 57),
        new("AC Ajaccio",              "ACA", "FRA", 63, 62, 61, 65, 57),
        new("Pau FC",                  "PAU", "FRA", 62, 61, 60, 64, 56),
        new("Rodez AF",                "ROD", "FRA", 62, 61, 60, 64, 56),
        new("Red Star FC",             "RSF", "FRA", 61, 60, 59, 63, 55),
        new("Clermont Foot",           "CLE", "FRA", 61, 60, 59, 63, 55),
        new("FC Martigues",            "MTG", "FRA", 60, 59, 58, 62, 54),
        new("SM Caen",                 "CAE", "FRA", 60, 59, 58, 62, 54),

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

        // ── Serie B (Abschluss 2024/25, Issue #575) ────────────────────────
        // Rating G linear nach Tabellenplatz 2024/25, vom Ersten mit 67 bis zum Letzten mit 59;
        // O = G+1, M = G, D = G−1, Heim = G+3, Auswärts = G−5.
        new("US Sassuolo",             "SAS", "ITA", 68, 67, 66, 70, 62),
        new("Pisa SC",                 "PIS", "ITA", 68, 67, 66, 70, 62),
        new("Spezia Calcio",           "SPE", "ITA", 67, 66, 65, 69, 61),
        new("US Cremonese",            "CRE", "ITA", 67, 66, 65, 69, 61),
        new("SS Juve Stabia",          "JST", "ITA", 66, 65, 64, 68, 60),
        new("US Catanzaro",            "CTZ", "ITA", 66, 65, 64, 68, 60),
        new("Cesena FC",               "CES", "ITA", 65, 64, 63, 67, 59),
        new("Palermo FC",              "PLM", "ITA", 65, 64, 63, 67, 59),
        new("SSC Bari",                "BAI", "ITA", 65, 64, 63, 67, 59),
        new("FC Südtirol",             "SUD", "ITA", 64, 63, 62, 66, 58),
        new("Modena FC",               "MOD", "ITA", 64, 63, 62, 66, 58),
        new("Carrarese Calcio",        "CRR", "ITA", 63, 62, 61, 65, 57),
        new("Mantova 1911",            "MAN", "ITA", 63, 62, 61, 65, 57),
        new("AC Reggiana",             "RGA", "ITA", 63, 62, 61, 65, 57),
        new("Frosinone Calcio",        "FRO", "ITA", 62, 61, 60, 64, 56),
        new("US Salernitana",          "SAL", "ITA", 62, 61, 60, 64, 56),
        new("UC Sampdoria",            "SMP", "ITA", 61, 60, 59, 63, 55),
        new("Brescia Calcio",          "BRS", "ITA", 61, 60, 59, 63, 55),
        new("AS Cittadella",           "CIT", "ITA", 60, 59, 58, 62, 54),
        new("Cosenza Calcio",          "COS", "ITA", 60, 59, 58, 62, 54),

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

        // ── Weitere UEFA-Vereine: Top 200 des Klub-Koeffizienten (Issue #381) ─────────
        // Alle Vereine der Kassies-Rangliste trank2026 bis Platz 200 (samt Gleichstand), die vorher fehlten, dazu
        // Dundee United und Polissja Schytomyr. Rating G = 55,2 + 5,8·ln(Koeffizient), geeicht an den vorhandenen
        // Vereinen; Abschlag 4·ln(8,4/Landesanteil) für Ligen mit kleinem Landesanteil (sonst stünde Gibraltars
        // Meister über Aberdeen). Vereine, deren Koeffizient nur der Landesanteil ist, rechnen mit dem geometrischen
        // Mittel aus eigenen Punkten und Landesanteil. O = G+1, M = G, D = G−1, Heim = G+3, Auswärts = G−5.
        // Armenien
        new("FC Noah",                 "NOA", "ARM", 66, 65, 64, 68, 60),
        new("Pjunik Jerewan",          "PYU", "ARM", 65, 64, 63, 67, 59),

        // Österreich
        new("Red Bull Salzburg",       "RBS", "AUT", 77, 76, 75, 79, 71),
        new("SK Rapid Wien",           "RAP", "AUT", 75, 74, 73, 77, 69),
        new("SK Sturm Graz",           "STU", "AUT", 75, 74, 73, 77, 69),
        new("LASK",                    "LASK","AUT", 73, 72, 71, 75, 67),
        new("FK Austria Wien",         "AWI", "AUT", 68, 67, 66, 70, 62),

        // Aserbaidschan
        new("Qarabağ Ağdam",           "QAR", "AZE", 76, 75, 74, 78, 70),

        // Belgien
        new("Union Saint-Gilloise",    "USG", "BEL", 79, 78, 77, 81, 73),
        new("Cercle Brügge",           "CER", "BEL", 71, 70, 69, 73, 65),
        new("Sporting Charleroi",      "CHL", "BEL", 65, 64, 63, 67, 59),

        // Bosnien-Herzegowina
        new("Zrinjski Mostar",         "ZRI", "BIH", 67, 66, 65, 69, 61),
        new("Borac Banja Luka",        "BOR", "BIH", 67, 66, 65, 69, 61),

        // Bulgarien
        new("Ludogorez Rasgrad",       "LUD", "BUL", 73, 72, 71, 75, 67),

        // Kroatien
        new("Dinamo Zagreb",           "DZG", "CRO", 77, 76, 75, 79, 71),
        new("HNK Rijeka",              "RIJ", "CRO", 72, 71, 70, 74, 66),
        new("Hajduk Split",            "HAJ", "CRO", 68, 67, 66, 70, 62),

        // Zypern
        new("Pafos FC",                "PAF", "CYP", 74, 73, 72, 76, 68),
        new("Omonia Nikosia",          "OMO", "CYP", 73, 72, 71, 75, 67),
        new("AEK Larnaka",             "AEL", "CYP", 73, 72, 71, 75, 67),
        new("APOEL Nikosia",           "APO", "CYP", 71, 70, 69, 73, 65),

        // Tschechien
        new("Viktoria Pilsen",         "VPL", "CZE", 79, 78, 77, 81, 73),
        new("Slavia Prag",             "SLP", "CZE", 78, 77, 76, 80, 72),
        new("Sparta Prag",             "SPP", "CZE", 77, 76, 75, 79, 71),
        new("Sigma Olmütz",            "SIG", "CZE", 68, 67, 66, 70, 62),
        new("Baník Ostrava",           "BAN", "CZE", 67, 66, 65, 69, 61),
        new("FK Mladá Boleslav",       "MBO", "CZE", 67, 66, 65, 69, 61),
        new("Bohemians Prag 1905",     "BOH", "CZE", 64, 63, 62, 66, 58),
        new("1. FC Slovácko",          "SLC", "CZE", 68, 67, 66, 70, 62),
        new("FK Jablonec",             "JAB", "CZE", 67, 66, 65, 69, 61),

        // Dänemark
        new("FC Kopenhagen",           "FCK", "DEN", 79, 78, 77, 81, 73),
        new("FC Midtjylland",          "MJY", "DEN", 79, 78, 77, 81, 73),
        new("Brøndby IF",              "BRO", "DEN", 69, 68, 67, 71, 63),
        new("Silkeborg IF",            "SIL", "DEN", 68, 67, 66, 70, 62),
        new("FC Nordsjælland",         "NSJ", "DEN", 68, 67, 66, 70, 62),
        new("AGF Aarhus",              "AGF", "DEN", 66, 65, 64, 68, 60),
        new("Viborg FF",               "VIB", "DEN", 65, 64, 63, 67, 59),
        new("Randers FC",              "RFC", "DEN", 68, 67, 66, 70, 62),

        // Estland
        new("FC Flora Tallinn",        "FLO", "EST", 63, 62, 61, 65, 57),

        // Finnland
        new("KuPS Kuopio",             "KUP", "FIN", 67, 66, 65, 69, 61),
        new("HJK Helsinki",            "HJK", "FIN", 67, 66, 65, 69, 61),

        // Färöer
        new("KÍ Klaksvík",             "KIK", "FRO", 64, 63, 62, 66, 58),

        // Gibraltar
        new("Lincoln Red Imps",        "LRI", "GIB", 64, 63, 62, 66, 58),

        // Ungarn
        new("Ferencváros Budapest",    "FTC", "HUN", 77, 76, 75, 79, 71),

        // Irland
        new("Shamrock Rovers",         "SHR", "IRL", 70, 69, 68, 72, 64),

        // Island
        new("Breiðablik",              "BRK", "ISL", 67, 66, 65, 69, 61),
        new("Víkingur Reykjavík",      "VKR", "ISL", 67, 66, 65, 69, 61),

        // Israel
        new("Maccabi Tel Aviv",        "MTA", "ISR", 75, 74, 73, 77, 69),
        new("Maccabi Haifa",           "MHA", "ISR", 71, 70, 69, 73, 65),
        new("Hapoel Be’er Scheva",     "HBS", "ISR", 70, 69, 68, 72, 64),

        // Kasachstan
        new("FK Astana",               "AST", "KAZ", 66, 65, 64, 68, 60),
        new("Kairat Almaty",           "KRT", "KAZ", 66, 65, 64, 68, 60),

        // Kosovo
        new("FC Drita",                "DRI", "KVX", 67, 66, 65, 69, 61),
        new("FC Ballkani",             "BLK", "KVX", 65, 64, 63, 67, 59),

        // Liechtenstein
        new("FC Vaduz",                "VAD", "LIE", 62, 61, 60, 64, 56),

        // Litauen
        new("Žalgiris Vilnius",        "ZAL", "LTU", 64, 63, 62, 66, 58),

        // Lettland
        new("RFS Riga",                "RFS", "LVA", 66, 65, 64, 68, 60),
        new("Riga FC",                 "RIG", "LVA", 65, 64, 63, 67, 59),

        // Moldau
        new("Sheriff Tiraspol",        "SHE", "MDA", 69, 68, 67, 71, 63),
        new("Petrocub Hîncești",       "PTC", "MDA", 65, 64, 63, 67, 59),

        // Nordmazedonien
        new("Shkëndija Tetovo",        "SHK", "MKD", 63, 62, 61, 65, 57),

        // Malta
        new("Ħamrun Spartans",         "HSP", "MLT", 62, 61, 60, 64, 56),

        // Niederlande
        new("Go Ahead Eagles",         "GAE", "NED", 69, 68, 67, 71, 63),
        new("FC Twente Enschede",      "TWE", "NED", 71, 70, 69, 73, 65),
        new("Vitesse Arnheim",         "VTS", "NED", 70, 69, 68, 72, 64),

        // Nordirland
        new("Linfield FC",             "LNF", "NIR", 62, 61, 60, 64, 56),
        new("Larne FC",                "LAR", "NIR", 62, 61, 60, 64, 56),

        // Norwegen
        new("FK Bodø/Glimt",           "BOD", "NOR", 80, 79, 78, 82, 74),
        new("Molde FK",                "MOL", "NOR", 74, 73, 72, 76, 68),
        new("SK Brann",                "BRN", "NOR", 71, 70, 69, 73, 65),
        new("Rosenborg BK",            "ROS", "NOR", 68, 67, 66, 70, 62),
        new("Fredrikstad FK",          "FRE", "NOR", 65, 64, 63, 67, 59),
        new("Viking FK",               "VKG", "NOR", 67, 66, 65, 69, 61),
        new("Tromsø IL",               "TRO", "NOR", 64, 63, 62, 66, 58),
        new("Lillestrøm SK",           "LIL", "NOR", 64, 63, 62, 66, 58),
        new("Vålerenga IF",            "VAL", "NOR", 63, 62, 61, 65, 57),

        // Polen
        new("Legia Warschau",          "LGW", "POL", 76, 75, 74, 78, 70),
        new("Lech Posen",              "LPO", "POL", 75, 74, 73, 77, 69),
        new("Raków Częstochowa",       "RAK", "POL", 74, 73, 72, 76, 68),
        new("Jagiellonia Białystok",   "JAG", "POL", 74, 73, 72, 76, 68),
        new("Wisła Krakau",            "WIS", "POL", 65, 64, 63, 67, 59),
        new("Śląsk Breslau",           "SLA", "POL", 67, 66, 65, 69, 61),
        new("Pogoń Stettin",           "POG", "POL", 67, 66, 65, 69, 61),
        new("Lechia Danzig",           "LGD", "POL", 64, 63, 62, 66, 58),

        // Portugal
        new("CD Santa Clara",          "SCL", "POR", 69, 68, 67, 71, 63),
        new("FC Arouca",               "ARO", "POR", 66, 65, 64, 68, 60),
        new("Gil Vicente FC",          "GIL", "POR", 67, 66, 65, 69, 61),
        new("FC Paços de Ferreira",    "PFE", "POR", 67, 66, 65, 69, 61),

        // Rumänien
        new("FCSB",                    "FCSB","ROU", 73, 72, 71, 75, 67),
        new("CFR Cluj",                "CFR", "ROU", 71, 70, 69, 73, 65),
        new("Universitatea Craiova",   "UCR", "ROU", 68, 67, 66, 70, 62),

        // Schottland
        new("Dundee United",           "DUN", "SCO", 65, 64, 63, 67, 59),

        // Serbien
        new("Roter Stern Belgrad",     "CZV", "SRB", 77, 76, 75, 79, 71),
        new("Partizan Belgrad",        "PAR", "SRB", 72, 71, 70, 74, 66),

        // Schweiz
        new("FC Basel",                "BAS", "SUI", 76, 75, 74, 78, 70),
        new("BSC Young Boys",          "YBO", "SUI", 75, 74, 73, 77, 69),
        new("FC Lugano",               "LUG", "SUI", 73, 72, 71, 75, 67),
        new("Servette FC",             "SER", "SUI", 70, 69, 68, 72, 64),
        new("FC Lausanne-Sport",       "LAU", "SUI", 69, 68, 67, 71, 63),

        // Slowakei
        new("Slovan Bratislava",       "SVB", "SVK", 74, 73, 72, 76, 68),
        new("Spartak Trnava",          "TRN", "SVK", 67, 66, 65, 69, 61),

        // Slowenien
        new("NK Celje",                "CLJ", "SVN", 72, 71, 70, 74, 66),
        new("Olimpija Ljubljana",      "OLI", "SVN", 71, 70, 69, 73, 65),
        new("NK Maribor",              "MAR", "SVN", 67, 66, 65, 69, 61),

        // Schweden
        new("Djurgårdens IF",          "DIF", "SWE", 75, 74, 73, 77, 69),
        new("Malmö FF",                "MFF", "SWE", 71, 70, 69, 73, 65),
        new("IF Elfsborg",             "ELF", "SWE", 69, 68, 67, 71, 63),
        new("BK Häcken",               "HAC", "SWE", 68, 67, 66, 70, 62),

        // Türkei
        new("Sivasspor",               "SIV", "TUR", 71, 70, 69, 73, 65),
        new("Samsunspor",              "SAM", "TUR", 70, 69, 68, 72, 64),
        new("Adana Demirspor",         "ADS", "TUR", 66, 65, 64, 68, 60),
        new("Konyaspor",               "KON", "TUR", 65, 64, 63, 67, 59),

        // Ukraine
        new("Sorja Luhansk",           "ZOR", "UKR", 69, 68, 67, 71, 63),
        new("Polissja Schytomyr",      "PZH", "UKR", 63, 62, 61, 65, 57),

        // Wales
        new("The New Saints",          "TNS", "WAL", 61, 60, 59, 63, 55),

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

        // ── Weitere Vereine außerhalb Europas: FIFA-Klub-WM-Ranking 2021–2024 (Issue #581) ──────────
        // Alle Vereine der fünf Verbandsranglisten (KlubWmRankingSeedDaten), die vorher fehlten. Die Punkte sind
        // zwischen den Verbänden nicht vergleichbar, und die Ligastärke des Landes wiegt mehr als die Punkte
        // (saudische Vereine stehen über koreanischen mit mehr Punkten). Deshalb G = Landeswert + 3·ln(Punkte):
        // Landeswert = Mittel aus G − 3·ln(Punkte) der vorhandenen Vereine des Landes; Länder ohne vorhandenen
        // Verein stufenweise unter einem Vergleichsland (CONMEBOL: Kolumbien −0/−3; CAF: Marokko −1/−3/−5;
        // AFC: Japan −2/−4/−7; CONCACAF: USA −1/−5/−8; OFC: Neuseeland mit Auckland City = 62 als Anker, −3/−6).
        // O = G+1, M = G, D = G−1, Heim = G+3, Auswärts = G−5.
        // Südamerika (CONMEBOL)
        // · Argentinien
        new("Vélez Sarsfield",          "VEL", "ARG", 78, 77, 76, 80, 72),
        new("Talleres Córdoba",         "TAL", "ARG", 77, 76, 75, 79, 71),
        new("Argentinos Juniors",       "ARJ", "ARG", 77, 76, 75, 79, 71),
        new("Estudiantes de La Plata",  "EDL", "ARG", 77, 76, 75, 79, 71),
        new("CA Colón",                 "CLN", "ARG", 74, 73, 72, 76, 68),
        new("Defensa y Justicia",       "DYJ", "ARG", 74, 73, 72, 76, 68),
        new("Rosario Central",          "ROC", "ARG", 73, 72, 71, 75, 67),
        new("CA Patronato",             "PAT", "ARG", 72, 71, 70, 74, 66),
        // · Bolivien
        new("Club Bolívar",             "BOV", "BOL", 71, 70, 69, 73, 65),
        new("The Strongest",            "TSR", "BOL", 71, 70, 69, 73, 65),
        new("Always Ready",             "ARE", "BOL", 68, 67, 66, 70, 62),
        new("Independiente Petrolero",  "IPE", "BOL", 63, 62, 61, 65, 57),
        // · Brasilien
        new("Atlético Mineiro",         "CAM", "BRA", 80, 79, 78, 82, 74),
        new("Athletico Paranaense",     "CAP", "BRA", 78, 77, 76, 80, 72),
        new("SC Internacional",         "SCI", "BRA", 77, 76, 75, 79, 71),
        new("Botafogo",                 "BFR", "BRA", 76, 75, 74, 78, 70),
        new("Grêmio Porto Alegre",      "GPA", "BRA", 74, 73, 72, 76, 68),
        new("Fortaleza EC",             "FOR", "BRA", 74, 73, 72, 76, 68),
        new("FC Santos",                "SFC", "BRA", 72, 71, 70, 74, 66),
        new("RB Bragantino",            "RBB", "BRA", 72, 71, 70, 74, 66),
        new("América Mineiro",          "AMG", "BRA", 70, 69, 68, 72, 64),
        // · Chile
        new("CD Palestino",             "PAE", "CHI", 66, 65, 64, 68, 60),
        new("Ñublense",                 "NUB", "CHI", 65, 64, 63, 67, 59),
        new("Unión La Calera",          "ULC", "CHI", 64, 63, 62, 66, 58),
        new("CD Cobresal",              "CSL", "CHI", 63, 62, 61, 65, 57),
        // · Ecuador
        new("Independiente del Valle",  "IDV", "ECU", 74, 73, 72, 76, 68),
        new("Barcelona SC",             "BSG", "ECU", 74, 73, 72, 76, 68),
        new("LDU Quito",                "LDU", "ECU", 71, 70, 69, 73, 65),
        new("CS Emelec",                "EME", "ECU", 70, 69, 68, 72, 64),
        new("SD Aucas",                 "AUC", "ECU", 68, 67, 66, 70, 62),
        // · Kolumbien
        new("Deportivo Pereira",        "DPE", "COL", 71, 70, 69, 73, 65),
        new("Deportes Tolima",          "TOL", "COL", 71, 70, 69, 73, 65),
        new("Independiente Medellín",   "DIM", "COL", 70, 69, 68, 72, 64),
        new("América de Cali",          "ADC", "COL", 68, 67, 66, 70, 62),
        new("Independiente Santa Fe",   "ISF", "COL", 68, 67, 66, 70, 62),
        // · Paraguay
        new("Olimpia Asunción",         "OLP", "PAR", 74, 73, 72, 76, 68),
        new("Cerro Porteño",            "CCP", "PAR", 74, 73, 72, 76, 68),
        new("Club Libertad",            "LIB", "PAR", 73, 72, 71, 75, 67),
        // · Peru
        new("Sporting Cristal",         "CRI", "PER", 69, 68, 67, 71, 63),
        new("Alianza Lima",             "ALI", "PER", 68, 67, 66, 70, 62),
        new("Universitario de Deportes","UNI", "PER", 67, 66, 65, 69, 61),
        new("FBC Melgar",               "MEL", "PER", 65, 64, 63, 67, 59),
        // · Uruguay
        new("Nacional Montevideo",      "NAC", "URU", 74, 73, 72, 76, 68),
        new("CA Peñarol",               "PEN", "URU", 74, 73, 72, 76, 68),
        new("Liverpool Montevideo",     "LVM", "URU", 70, 69, 68, 72, 64),
        new("CA Rentistas",             "RTS", "URU", 68, 67, 66, 70, 62),
        // · Venezuela
        new("Deportivo Táchira",        "TAC", "VEN", 69, 68, 67, 71, 63),
        new("Caracas FC",               "CRS", "VEN", 67, 66, 65, 69, 61),
        new("Monagas SC",               "MNG", "VEN", 65, 64, 63, 67, 59),
        new("Deportivo La Guaira",      "DLG", "VEN", 65, 64, 63, 67, 59),
        new("Metropolitanos FC",        "MTP", "VEN", 62, 61, 60, 64, 56),

        // Nord- und Mittelamerika (CONCACAF)
        // · Costa Rica
        new("LD Alajuelense",           "ALJ", "CRC", 66, 65, 64, 68, 60),
        new("CS Herediano",             "HER", "CRC", 65, 64, 63, 67, 59),
        new("Deportivo Saprissa",       "SAP", "CRC", 64, 63, 62, 66, 58),
        new("Santos de Guápiles",       "SDG", "CRC", 62, 61, 60, 64, 56),
        // · Dominikanische Republik
        new("Atlético Pantoja",         "ATP", "DOM", 59, 58, 57, 61, 53),
        // · El Salvador
        new("Alianza FC",               "ALZ", "SLV", 60, 59, 58, 62, 54),
        // · Guatemala
        new("CSD Comunicaciones",       "COM", "GUA", 66, 65, 64, 68, 60),
        new("CSD Guastatoya",           "GTY", "GUA", 62, 61, 60, 64, 56),
        // · Haiti
        new("Violette AC",              "VIO", "HAI", 63, 62, 61, 65, 57),
        new("AS Arcahaie",              "ARC", "HAI", 60, 59, 58, 62, 54),
        new("AS Cavaly",                "CAV", "HAI", 59, 58, 57, 61, 53),
        // · Honduras
        new("CD Motagua",               "MOT", "HON", 66, 65, 64, 68, 60),
        new("CD Olimpia",               "OLH", "HON", 66, 65, 64, 68, 60),
        new("Real España",              "RES", "HON", 64, 63, 62, 66, 58),
        new("CD Marathón",              "MRT", "HON", 63, 62, 61, 65, 57),
        // · Kanada
        new("CF Montréal",              "MTL", "CAN", 69, 68, 67, 71, 63),
        new("Toronto FC",               "TOF", "CAN", 69, 68, 67, 71, 63),
        new("Vancouver Whitecaps",      "VWC", "CAN", 69, 68, 67, 71, 63),
        new("Forge FC",                 "FOG", "CAN", 66, 65, 64, 68, 60),
        // · Mexiko
        new("CF Pachuca",               "PAC", "MEX", 76, 75, 74, 78, 70),
        new("Pumas UNAM",               "PUM", "MEX", 75, 74, 73, 77, 69),
        new("Atlas FC",                 "ATS", "MEX", 72, 71, 70, 74, 66),
        new("Santos Laguna",            "SLG", "MEX", 71, 70, 69, 73, 65),
        // · Nicaragua
        new("Real Estelí",              "RET", "NCA", 59, 58, 57, 61, 53),
        // · Panama
        new("Tauro FC",                 "TAU", "PAN", 62, 61, 60, 64, 56),
        // · Suriname
        new("SV Robinhood",             "ROB", "SUR", 60, 59, 58, 62, 54),
        // · USA
        new("Philadelphia Union",       "PHU", "USA", 74, 73, 72, 76, 68),
        new("Columbus Crew",            "CMB", "USA", 74, 73, 72, 76, 68),
        new("Los Angeles FC",           "LAF", "USA", 73, 72, 71, 75, 67),
        new("Atlanta United",           "ATL", "USA", 71, 70, 69, 73, 65),
        new("Portland Timbers",         "PTI", "USA", 71, 70, 69, 73, 65),
        new("Orlando City",             "ORL", "USA", 70, 69, 68, 72, 64),
        new("Austin FC",                "ATX", "USA", 69, 68, 67, 71, 63),
        new("Colorado Rapids",          "CRA", "USA", 69, 68, 67, 71, 63),
        new("Houston Dynamo",           "HOU", "USA", 68, 67, 66, 70, 62),
        new("Nashville SC",             "NSH", "USA", 68, 67, 66, 70, 62),
        new("FC Cincinnati",            "CIN", "USA", 67, 66, 65, 69, 61),

        // Asien (AFC)
        // · China
        new("Zhejiang FC",              "ZHE", "CHN", 67, 66, 65, 69, 61),
        // · Hongkong
        new("Kitchee SC",               "KIT", "HKG", 65, 64, 63, 67, 59),
        // · Indien
        new("Mumbai City FC",           "MUC", "IND", 62, 61, 60, 64, 56),
        new("FC Goa",                   "GOA", "IND", 59, 58, 57, 61, 53),
        // · Irak
        new("Al-Quwa Al-Jawiya",        "QWJ", "IRQ", 67, 66, 65, 69, 61),
        new("Al-Shorta",                "SHO", "IRQ", 62, 61, 60, 64, 56),
        // · Iran
        new("Persepolis Teheran",       "PRS", "IRN", 70, 69, 68, 72, 64),
        new("Foolad Khuzestan",         "FOO", "IRN", 69, 68, 67, 71, 63),
        new("Sepahan Isfahan",          "SEP", "IRN", 69, 68, 67, 71, 63),
        new("Esteghlal Teheran",        "ESG", "IRN", 68, 67, 66, 70, 62),
        new("Tractor Täbris",           "TRC", "IRN", 67, 66, 65, 69, 61),
        new("Nassaji Mazandaran",       "NSM", "IRN", 66, 65, 64, 68, 60),
        // · Japan
        new("Yokohama F. Marinos",      "YFM", "JPN", 73, 72, 71, 75, 67),
        new("Nagoya Grampus",           "NGG", "JPN", 71, 70, 69, 73, 65),
        new("Cerezo Osaka",             "CEO", "JPN", 70, 69, 68, 72, 64),
        new("Ventforet Kofu",           "VFK", "JPN", 70, 69, 68, 72, 64),
        // · Jordanien
        new("Al-Wehdat",                "WEH", "JOR", 63, 62, 61, 65, 57),
        new("Al-Faisaly Amman",         "FAA", "JOR", 61, 60, 59, 63, 55),
        // · Katar
        new("Al-Duhail",                "DUH", "QAT", 71, 70, 69, 73, 65),
        new("Al-Sadd",                  "SAD", "QAT", 70, 69, 68, 72, 64),
        new("Al-Rayyan",                "RYN", "QAT", 69, 68, 67, 71, 63),
        new("Al-Gharafa",               "GHR", "QAT", 65, 64, 63, 67, 59),
        // · Malaysia
        new("Johor Darul Ta'zim",       "JDT", "MAS", 68, 67, 66, 70, 62),
        // · Philippinen
        new("United City FC",           "UCF", "PHI", 61, 60, 59, 63, 55),
        new("Kaya FC",                  "KAY", "PHI", 59, 58, 57, 61, 53),
        // · Saudi-Arabien
        new("Al-Fayha",                 "FAY", "KSA", 74, 73, 72, 76, 68),
        new("Al-Faisaly",               "FAI", "KSA", 74, 73, 72, 76, 68),
        new("Al-Taawoun",               "TAA", "KSA", 73, 72, 71, 75, 67),
        // · Singapur
        new("Lion City Sailors",        "LCS", "SIN", 63, 62, 61, 65, 57),
        new("Tampines Rovers",          "TAM", "SIN", 57, 56, 55, 59, 51),
        // · Südkorea
        new("Pohang Steelers",          "POH", "KOR", 69, 68, 67, 71, 63),
        new("Daegu FC",                 "DAE", "KOR", 68, 67, 66, 70, 62),
        new("Incheon United",           "ICU", "KOR", 65, 64, 63, 67, 59),
        new("Jeonnam Dragons",          "JND", "KOR", 65, 64, 63, 67, 59),
        // · Tadschikistan
        new("Istiklol Duschanbe",       "IST", "TJK", 64, 63, 62, 66, 58),
        // · Thailand
        new("BG Pathum United",         "BGP", "THA", 69, 68, 67, 71, 63),
        new("Bangkok United",           "BKU", "THA", 66, 65, 64, 68, 60),
        new("Chiangrai United",         "CRU", "THA", 65, 64, 63, 67, 59),
        new("Port FC",                  "PRT", "THA", 64, 63, 62, 66, 58),
        new("Buriram United",           "BRU", "THA", 64, 63, 62, 66, 58),
        new("Ratchaburi FC",            "RAT", "THA", 62, 61, 60, 64, 56),
        // · Turkmenistan
        new("Ahal FK",                  "AHA", "TKM", 62, 61, 60, 64, 56),
        // · Usbekistan
        new("Nasaf Qarshi",             "NAS", "UZB", 68, 67, 66, 70, 62),
        new("Pakhtakor Taschkent",      "PKT", "UZB", 67, 66, 65, 69, 61),
        new("Navbahor Namangan",        "NAV", "UZB", 66, 65, 64, 68, 60),
        new("AGMK Olmaliq",             "AGM", "UZB", 65, 64, 63, 67, 59),
        // · Vereinigte Arabische Emirate
        new("Al-Ain",                   "AIN", "UAE", 70, 69, 68, 72, 64),
        new("Sharjah FC",               "SHA", "UAE", 70, 69, 68, 72, 64),
        new("Shabab Al-Ahli",           "SBA", "UAE", 69, 68, 67, 71, 63),
        new("Al-Wahda",                 "WAH", "UAE", 68, 67, 66, 70, 62),
        new("Al-Jazira",                "JAZ", "UAE", 65, 64, 63, 67, 59),
        // · Vietnam
        new("Hanoi FC",                 "HAN", "VIE", 61, 60, 59, 63, 55),
        new("Viettel FC",               "VTL", "VIE", 61, 60, 59, 63, 55),
        new("Hoàng Anh Gia Lai",        "HAG", "VIE", 60, 59, 58, 62, 54),

        // Afrika (CAF)
        // · Algerien
        new("CR Belouizdad",            "CRB", "ALG", 70, 69, 68, 72, 64),
        new("ES Sétif",                 "ESS", "ALG", 67, 66, 65, 69, 61),
        new("JS Kabylie",               "JSK", "ALG", 66, 65, 64, 68, 60),
        new("MC Algier",                "MCA", "ALG", 66, 65, 64, 68, 60),
        // · Angola
        new("Petro de Luanda",          "PDL", "ANG", 67, 66, 65, 69, 61),
        new("Sagrada Esperança",        "SAG", "ANG", 60, 59, 58, 62, 54),
        // · Botswana
        new("Jwaneng Galaxy",           "JWG", "BOT", 60, 59, 58, 62, 54),
        // · DR Kongo
        new("TP Mazembe",               "TPM", "COD", 66, 65, 64, 68, 60),
        new("AS Vita Club",             "VCL", "COD", 64, 63, 62, 66, 58),
        // · Elfenbeinküste
        new("ASEC Mimosas",             "ASE", "CIV", 62, 61, 60, 64, 56),
        // · Ghana
        new("Medeama SC",               "MED", "GHA", 59, 58, 57, 61, 53),
        // · Guinea
        new("Horoya AC",                "HOR", "GUI", 63, 62, 61, 65, 57),
        // · Kamerun
        new("Coton Sport",              "COT", "CMR", 57, 56, 55, 59, 51),
        // · Mauretanien
        new("FC Nouadhibou",            "NOU", "MTN", 60, 59, 58, 62, 54),
        // · Senegal
        new("Teungueth FC",             "TEU", "SEN", 59, 58, 57, 61, 53),
        // · Sudan
        new("Al-Hilal Omdurman",        "HOM", "SDN", 66, 65, 64, 68, 60),
        new("Al-Merrikh",               "MER", "SDN", 64, 63, 62, 66, 58),
        // · Südafrika
        new("Mamelodi Sundowns",        "MSU", "RSA", 71, 70, 69, 73, 65),
        new("Kaizer Chiefs",            "KCH", "RSA", 67, 66, 65, 69, 61),
        new("AmaZulu FC",               "AMZ", "RSA", 64, 63, 62, 66, 58),
        // · Tansania
        new("Simba SC",                 "SIM", "TAN", 67, 66, 65, 69, 61),
        new("Young Africans",           "YAN", "TAN", 64, 63, 62, 66, 58),
        // · Tunesien
        new("Espérance Tunis",          "EST", "TUN", 71, 70, 69, 73, 65),
        new("Étoile du Sahel",          "ESH", "TUN", 66, 65, 64, 68, 60),
        // · Uganda
        new("Vipers SC",                "VIP", "UGA", 58, 57, 56, 60, 52),

        // Ozeanien (OFC)
        // · Cookinseln
        new("Nikao Sokattak",           "NIK", "COK", 48, 47, 46, 50, 42),
        // · Fidschi
        new("Rewa FC",                  "REW", "FIJ", 56, 55, 54, 58, 50),
        new("Suva FC",                  "SUV", "FIJ", 56, 55, 54, 58, 50),
        // · Neukaledonien
        new("AS Magenta",               "MAG", "NCL", 55, 54, 53, 57, 49),
        new("Hienghène Sport",          "HIE", "NCL", 55, 54, 53, 57, 49),
        new("Tiga Sport",               "TGS", "NCL", 53, 52, 51, 55, 47),
        // · Neuseeland
        new("Auckland City",            "ACK", "NZL", 63, 62, 61, 65, 57),
        // · Papua-Neuguinea
        new("Hekari United",            "HEK", "PNG", 55, 54, 53, 57, 49),
        new("Lae City FC",              "LAE", "PNG", 52, 51, 50, 54, 46),
        // · Salomonen
        new("Central Coast FC",         "CCT", "SOL", 55, 54, 53, 57, 49),
        new("Solomon Warriors",         "SLW", "SOL", 54, 53, 52, 56, 48),
        // · Samoa
        new("Vaivase-Tai",              "VAI", "SAM", 48, 47, 46, 50, 42),
        new("Lupe ole Soaga",           "LUP", "SAM", 48, 47, 46, 50, 42),
        // · Tahiti
        new("AS Pirae",                 "PIR", "TAH", 58, 57, 56, 60, 52),
        new("AS Vénus",                 "VNS", "TAH", 56, 55, 54, 58, 50),
        // · Vanuatu
        new("Ifira Black Bird",         "IBB", "VAN", 56, 55, 54, 58, 50),
        new("Galaxy FC",                "GLX", "VAN", 53, 52, 51, 55, 47),

        // ── Oberste Ligen außerhalb Europas (Issue #582) ──────────────────────────────────────────────
        // Vereine der Abschlusstabellen (LigapyramidenSeedDaten), die vorher fehlten; Reserveteams bleiben weg.
        // G = Ligawert − 8·(Platz − 1)/(Ligagröße − 1): Spanne von 8 wie bei den Zweitligisten aus #575; Ligawert =
        // Mittel aus G + 8·(Platz − 1)/(Ligagröße − 1) der vorhandenen Vereine dieser Liga (Ausgleich über alle Ligen ergab
        // nur 4, weil deren Ratings aus mehrjährigen Ranglisten stammen). O = G+1, M = G, D = G−1, Heim = G+3, Auswärts = G−5.
        // · Brasilien – Série A 2024
        new("EC Bahia",                           "BAH", "BRA", 77, 76, 75, 79, 71),
        new("Cruzeiro EC",                        "CRZE","BRA", 77, 76, 75, 79, 71),
        new("Vasco da Gama",                      "VAS", "BRA", 76, 75, 74, 78, 70),
        new("EC Vitória",                         "ECV", "BRA", 76, 75, 74, 78, 70),
        new("EC Juventude",                       "ECJ", "BRA", 74, 73, 72, 76, 68),
        new("Criciúma EC",                        "CRC", "BRA", 73, 72, 71, 75, 67),
        new("Atlético Goianiense",                "ACG", "BRA", 72, 71, 70, 74, 66),
        new("Cuiabá EC",                          "CUI", "BRA", 72, 71, 70, 74, 66),
        // · Argentinien – Liga Profesional 2024
        new("CA Huracán",                         "HUR", "ARG", 79, 78, 77, 81, 73),
        new("Atlético Tucumán",                   "ATU", "ARG", 77, 76, 75, 79, 71),
        new("Unión de Santa Fe",                  "USF", "ARG", 77, 76, 75, 79, 71),
        new("CA Platense",                        "PLA", "ARG", 77, 76, 75, 79, 71),
        new("Independiente Rivadavia",            "IRI", "ARG", 76, 75, 74, 78, 70),
        new("Instituto Córdoba",                  "ICO", "ARG", 76, 75, 74, 78, 70),
        new("CA Lanús",                           "LAN", "ARG", 76, 75, 74, 78, 70),
        new("Godoy Cruz",                         "GOD", "ARG", 75, 74, 73, 77, 69),
        new("CA Belgrano",                        "BEL", "ARG", 75, 74, 73, 77, 69),
        new("Deportivo Riestra",                  "RIE", "ARG", 75, 74, 73, 77, 69),
        new("CA Tigre",                           "CAT", "ARG", 74, 73, 72, 76, 68),
        new("Gimnasia y Esgrima La Plata",        "GLP", "ARG", 74, 73, 72, 76, 68),
        new("Central Córdoba Santiago del Estero","CCS", "ARG", 73, 72, 71, 75, 67),
        new("Newell's Old Boys",                  "NOB", "ARG", 72, 71, 70, 74, 66),
        new("Sarmiento Junín",                    "SAR", "ARG", 72, 71, 70, 74, 66),
        new("CA Banfield",                        "CAB", "ARG", 72, 71, 70, 74, 66),
        new("Barracas Central",                   "BCE", "ARG", 71, 70, 69, 73, 65),
        // · Mexiko – Liga MX 2024/25
        new("Deportivo Toluca",                   "TLC", "MEX", 77, 76, 75, 79, 71),
        new("Club León",                          "LEO", "MEX", 75, 74, 73, 77, 69),
        new("Atlético San Luis",                  "ASL", "MEX", 75, 74, 73, 77, 69),
        new("Club Tijuana",                       "TIJ", "MEX", 74, 73, 72, 76, 68),
        new("Club Necaxa",                        "NEC", "MEX", 73, 72, 71, 75, 67),
        new("FC Juárez",                          "JUA", "MEX", 72, 71, 70, 74, 66),
        new("Querétaro FC",                       "QRO", "MEX", 71, 70, 69, 73, 65),
        new("Mazatlán FC",                        "MAZ", "MEX", 71, 70, 69, 73, 65),
        new("Club Puebla",                        "PUE", "MEX", 70, 69, 68, 72, 64),
        // · USA – Major League Soccer 2024
        new("Real Salt Lake",                     "RSL", "USA", 73, 72, 71, 75, 67),
        new("Minnesota United",                   "MIN", "USA", 71, 70, 69, 73, 65),
        new("Charlotte FC",                       "CLTF","USA", 71, 70, 69, 73, 65),
        new("New York Red Bulls",                 "NYR", "USA", 70, 69, 68, 72, 64),
        new("FC Dallas",                          "DAL", "USA", 69, 68, 67, 71, 63),
        new("D.C. United",                        "DCU", "USA", 68, 67, 66, 70, 62),
        new("St. Louis City SC",                  "STL", "USA", 67, 66, 65, 69, 61),
        new("Sporting Kansas City",               "SKC", "USA", 67, 66, 65, 69, 61),
        new("Chicago Fire",                       "CHI", "USA", 66, 65, 64, 68, 60),
        new("San Jose Earthquakes",               "SJE", "USA", 66, 65, 64, 68, 60),
        // · Saudi-Arabien – Saudi Pro League 2024/25
        new("Al-Qadsiah",                         "QAD", "KSA", 77, 76, 75, 79, 71),
        new("Al-Ettifaq",                         "ETT", "KSA", 75, 74, 73, 77, 69),
        new("Al-Kholood",                         "KHO", "KSA", 74, 73, 72, 76, 68),
        new("Al-Fateh",                           "FAT", "KSA", 74, 73, 72, 76, 68),
        new("Al-Riyadh",                          "RIY", "KSA", 73, 72, 71, 75, 67),
        new("Al-Khaleej",                         "KHA", "KSA", 73, 72, 71, 75, 67),
        new("Damac FC",                           "DAM", "KSA", 72, 71, 70, 74, 66),
        new("Al-Okhdood",                         "OKH", "KSA", 72, 71, 70, 74, 66),
        new("Al-Wehda",                           "WHD", "KSA", 71, 70, 69, 73, 65),
        new("Al-Orobah",                          "ORO", "KSA", 71, 70, 69, 73, 65),
        new("Al-Raed",                            "RAE", "KSA", 70, 69, 68, 72, 64),
        // · Japan – J1 League 2024
        new("FC Machida Zelvia",                  "MCH", "JPN", 73, 72, 71, 75, 67),
        new("Kashima Antlers",                    "KAS", "JPN", 72, 71, 70, 74, 66),
        new("Tokyo Verdy",                        "VER", "JPN", 72, 71, 70, 74, 66),
        new("FC Tokyo",                           "TOK", "JPN", 71, 70, 69, 73, 65),
        new("Avispa Fukuoka",                     "AVI", "JPN", 69, 68, 67, 71, 63),
        new("Kyoto Sanga",                        "KYO", "JPN", 68, 67, 66, 70, 62),
        new("Shonan Bellmare",                    "SBE", "JPN", 68, 67, 66, 70, 62),
        new("Albirex Niigata",                    "ANI", "JPN", 67, 66, 65, 69, 61),
        new("Kashiwa Reysol",                     "KSW", "JPN", 67, 66, 65, 69, 61),
        new("Júbilo Iwata",                       "JUB", "JPN", 67, 66, 65, 69, 61),
        new("Consadole Sapporo",                  "HCS", "JPN", 66, 65, 64, 68, 60),
        new("Sagan Tosu",                         "SGT", "JPN", 66, 65, 64, 68, 60),
        // · Südkorea – K League 1 2024
        new("Gimcheon Sangmu",                    "GIM", "KOR", 71, 70, 69, 73, 65),
        new("Suwon FC",                           "SUW", "KOR", 70, 69, 68, 72, 64),
        new("Jeju United",                        "JEJ", "KOR", 68, 67, 66, 70, 62),
        new("Daejeon Hana Citizen",               "DJN", "KOR", 67, 66, 65, 69, 61),
        new("Gwangju FC",                         "GWJ", "KOR", 67, 66, 65, 69, 61),
        // · Vereinigte Arabische Emirate – UAE Pro League 2024/25
        new("Al-Wasl",                            "WAS", "UAE", 68, 67, 66, 70, 62),
        new("Al-Nasr Dubai",                      "NAD", "UAE", 67, 66, 65, 69, 61),
        new("Khor Fakkan",                        "KHF", "UAE", 66, 65, 64, 68, 60),
        new("Ittihad Kalba",                      "KAL", "UAE", 65, 64, 63, 67, 59),
        new("Ajman Club",                         "AJM", "UAE", 64, 63, 62, 66, 58),
        new("Al-Bataeh",                          "BAT", "UAE", 64, 63, 62, 66, 58),
        new("Baniyas SC",                         "BNY", "UAE", 63, 62, 61, 65, 57),
        new("Dibba Al-Hisn",                      "DIB", "UAE", 63, 62, 61, 65, 57),
        new("Al-Urooba",                          "URO", "UAE", 62, 61, 60, 64, 56),
        // · Ägypten – Egyptian Premier League 2024/25
        new("Al Masry",                           "MSR", "EGY", 68, 67, 66, 70, 62),
        new("National Bank of Egypt",             "NBE", "EGY", 67, 66, 65, 69, 61),
        new("Pharco FC",                          "PHA", "EGY", 66, 65, 64, 68, 60),
        new("Petrojet",                           "PJT", "EGY", 66, 65, 64, 68, 60),
        new("Haras El Hodoud",                    "HAR", "EGY", 65, 64, 63, 67, 59),
        new("ZED FC",                             "ZED", "EGY", 65, 64, 63, 67, 59),
        new("Tala'ea El Gaish",                   "TEG", "EGY", 64, 63, 62, 66, 58),
        new("ENPPI",                              "ENP", "EGY", 64, 63, 62, 66, 58),
        new("Modern Sport",                       "MOS", "EGY", 63, 62, 61, 65, 57),
        new("Al Ittihad Alexandria",              "ITA", "EGY", 63, 62, 61, 65, 57),
        new("Smouha SC",                          "SMO", "EGY", 62, 61, 60, 64, 56),
        new("Ismaily SC",                         "ISM", "EGY", 62, 61, 60, 64, 56),
        new("Ghazl El Mahalla",                   "GHA", "EGY", 61, 60, 59, 63, 55),
        // · Marokko – Botola Pro 2024/25
        new("FUS Rabat",                          "FUS", "MAR", 67, 66, 65, 69, 61),
        new("RCA Zemamra",                        "ZEM", "MAR", 66, 65, 64, 68, 60),
        new("MAS Fès",                            "MAS", "MAR", 66, 65, 64, 68, 60),
        new("Olympic Safi",                       "SAF", "MAR", 65, 64, 63, 67, 59),
        new("Difaâ El Jadidi",                    "DHJ", "MAR", 65, 64, 63, 67, 59),
        new("IR Tanger",                          "IRT", "MAR", 64, 63, 62, 66, 58),
        new("COD Meknès",                         "COD", "MAR", 63, 62, 61, 65, 57),
        new("Union de Touarga",                   "UTS", "MAR", 63, 62, 61, 65, 57),
        new("Hassania Agadir",                    "HUS", "MAR", 62, 61, 60, 64, 56),
        new("JS Soualem",                         "JSS", "MAR", 62, 61, 60, 64, 56),
        new("MA Tétouan",                         "MAT", "MAR", 61, 60, 59, 63, 55),
        new("SCC Mohammédia",                     "SCC", "MAR", 61, 60, 59, 63, 55),
        // · Tunesien – Ligue 1 2024/25
        new("US Monastir",                        "USM", "TUN", 69, 68, 67, 71, 63),
        new("Club Africain",                      "CAF", "TUN", 67, 66, 65, 69, 61),
        new("ES Zarzis",                          "ESZ", "TUN", 67, 66, 65, 69, 61),
        new("Stade Tunisien",                     "STT", "TUN", 66, 65, 64, 68, 60),
        new("CS Sfaxien",                         "CSS", "TUN", 66, 65, 64, 68, 60),
        new("ES Métlaoui",                        "ESM", "TUN", 65, 64, 63, 67, 59),
        new("CA Bizertin",                        "CAZ", "TUN", 65, 64, 63, 67, 59),
        new("AS Soliman",                         "ASO", "TUN", 64, 63, 62, 66, 58),
        new("US Ben Guerdane",                    "USBG","TUN", 64, 63, 62, 66, 58),
        new("Olympique Béja",                     "OBE", "TUN", 63, 62, 61, 65, 57),
        new("AS Gabès",                           "ASG", "TUN", 63, 62, 61, 65, 57),
        new("JS El Omrane",                       "JSO", "TUN", 62, 61, 60, 64, 56),
        new("EGS Gafsa",                          "EGS", "TUN", 62, 61, 60, 64, 56),
        new("US Tataouine",                       "UST", "TUN", 61, 60, 59, 63, 55),
        // · Südafrika – Premiership 2024/25
        new("Orlando Pirates",                    "ORP", "RSA", 69, 68, 67, 71, 63),
        new("Stellenbosch FC",                    "STB", "RSA", 69, 68, 67, 71, 63),
        new("Sekhukhune United",                  "SEK", "RSA", 68, 67, 66, 70, 62),
        new("TS Galaxy",                          "TSGA","RSA", 68, 67, 66, 70, 62),
        new("Polokwane City",                     "POL", "RSA", 66, 65, 64, 68, 60),
        new("Richards Bay FC",                    "RIC", "RSA", 66, 65, 64, 68, 60),
        new("Marumo Gallants",                    "MGA", "RSA", 65, 64, 63, 67, 59),
        new("Chippa United",                      "CHU", "RSA", 64, 63, 62, 66, 58),
        new("Golden Arrows",                      "GAR", "RSA", 64, 63, 62, 66, 58),
        new("Magesi FC",                          "MGS", "RSA", 63, 62, 61, 65, 57),
        new("SuperSport United",                  "SSU", "RSA", 63, 62, 61, 65, 57),
        new("Cape Town City",                     "CTC", "RSA", 62, 61, 60, 64, 56),
        new("Royal AM",                           "RAM", "RSA", 62, 61, 60, 64, 56),
        // · Neuseeland – National League 2024
        new("Birkenhead United",                  "BHU", "NZL", 64, 63, 62, 66, 58),
        new("Western Springs",                    "WSP", "NZL", 62, 61, 60, 64, 56),
        new("Napier City Rovers",                 "NCR", "NZL", 61, 60, 59, 63, 55),
        new("Wellington Olympic",                 "WLO", "NZL", 59, 58, 57, 61, 53),
        new("Coastal Spirit",                     "COA", "NZL", 59, 58, 57, 61, 53),
        new("Cashmere Technical",                 "CTE", "NZL", 58, 57, 56, 60, 52),
        new("Eastern Suburbs",                    "ESU", "NZL", 57, 56, 55, 59, 51),
        new("Western Suburbs",                    "WSU", "NZL", 56, 55, 54, 58, 50),
        // · Australien – A-League Men 2024/25 (Auckland FC und Wellington Phoenix aus Neuseeland)
        new("Auckland FC",                        "AKL", "NZL", 70, 69, 68, 72, 64),
        new("Western United",                     "WUN", "AUS", 68, 67, 66, 70, 62),
        new("Adelaide United",                    "ADE", "AUS", 66, 65, 64, 68, 60),
        new("Macarthur FC",                       "MAC", "AUS", 65, 64, 63, 67, 59),
        new("Newcastle Jets",                     "NCJ", "AUS", 64, 63, 62, 66, 58),
        new("Central Coast Mariners",             "CCM", "AUS", 64, 63, 62, 66, 58),
        new("Wellington Phoenix",                 "WEL", "NZL", 63, 62, 61, 65, 57),
        new("Brisbane Roar",                      "BRI", "AUS", 62, 61, 60, 64, 56),
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
