using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Infrastructure.Seed.Daten;

/// <summary>Erzeugt von <c>tools/Kondition-EAFC/auswerten.py</c> (#677) – nicht von Hand ändern. Nur die
/// Teams, die vom Standard „normal“ abweichen.</summary>
public static class KonditionSeedListe
{
    public static readonly IReadOnlyList<KonditionSeedDaten.Eintrag> Handliste =
    [
        new(Mannschaftsart.Verein, "BOJ", "ARG", Kondition.Stark),   // Boca Juniors
        new(Mannschaftsart.Verein, "CAB", "ARG", Kondition.Schwach),   // CA Banfield
        new(Mannschaftsart.Verein, "EDL", "ARG", Kondition.Stark),   // Estudiantes de La Plata
        new(Mannschaftsart.Verein, "HUR", "ARG", Kondition.Schwach),   // CA Huracán
        new(Mannschaftsart.Verein, "ICO", "ARG", Kondition.Schwach),   // Instituto Córdoba
        new(Mannschaftsart.Verein, "INB", "ARG", Kondition.Stark),   // CA Independiente
        new(Mannschaftsart.Verein, "IRI", "ARG", Kondition.Schwach),   // Independiente Rivadavia
        new(Mannschaftsart.Verein, "NOB", "ARG", Kondition.Schwach),   // Newell's Old Boys
        new(Mannschaftsart.Verein, "PLA", "ARG", Kondition.Schwach),   // CA Platense
        new(Mannschaftsart.Verein, "RIE", "ARG", Kondition.Schwach),   // Deportivo Riestra
        new(Mannschaftsart.Verein, "SAR", "ARG", Kondition.Schwach),   // Sarmiento Junín
        new(Mannschaftsart.Verein, "SLO", "ARG", Kondition.Schwach),   // San Lorenzo
        new(Mannschaftsart.Verein, "TAL", "ARG", Kondition.Schwach),   // Talleres Córdoba
        new(Mannschaftsart.Verein, "BRI", "AUS", Kondition.Schwach),   // Brisbane Roar
        new(Mannschaftsart.Verein, "MAC", "AUS", Kondition.Schwach),   // Macarthur FC
        new(Mannschaftsart.Verein, "MEV", "AUS", Kondition.Schwach),   // Melbourne Victory
        new(Mannschaftsart.Verein, "NCJ", "AUS", Kondition.Stark),   // Newcastle Jets
        new(Mannschaftsart.Verein, "LASK", "AUT", Kondition.Stark),   // LASK
        new(Mannschaftsart.Verein, "STU", "AUT", Kondition.Stark),   // SK Sturm Graz
        new(Mannschaftsart.Verein, "QAR", "AZE", Kondition.Schwach),   // Qarabağ Ağdam
        new(Mannschaftsart.Verein, "ANT", "BEL", Kondition.Stark),   // Royal Antwerp FC
        new(Mannschaftsart.Verein, "CER", "BEL", Kondition.Schwach),   // Cercle Brügge
        new(Mannschaftsart.Verein, "CLB", "BEL", Kondition.Stark),   // Club Brugge KV
        new(Mannschaftsart.Verein, "GNK", "BEL", Kondition.Stark),   // KRC Genk
        new(Mannschaftsart.Verein, "BOV", "BOL", Kondition.Schwach),   // Club Bolívar
        new(Mannschaftsart.Verein, "MTL", "CAN", Kondition.Schwach),   // CF Montréal
        new(Mannschaftsart.Verein, "PAE", "CHI", Kondition.Schwach),   // CD Palestino
        new(Mannschaftsart.Verein, "UCH", "CHI", Kondition.Schwach),   // Universidad de Chile
        new(Mannschaftsart.Verein, "SDT", "CHN", Kondition.Stark),   // Shandong Taishan
        new(Mannschaftsart.Verein, "ADC", "COL", Kondition.Schwach),   // América de Cali
        new(Mannschaftsart.Verein, "NAL", "COL", Kondition.Schwach),   // Atlético Nacional
        new(Mannschaftsart.Verein, "OCA", "COL", Kondition.Schwach),   // Once Caldas
        new(Mannschaftsart.Verein, "DZG", "CRO", Kondition.Stark),   // Dinamo Zagreb
        new(Mannschaftsart.Verein, "SLP", "CZE", Kondition.Stark),   // Slavia Prag
        new(Mannschaftsart.Verein, "VPL", "CZE", Kondition.Schwach),   // Viktoria Pilsen
        new(Mannschaftsart.Verein, "FCK", "DEN", Kondition.Stark),   // FC Kopenhagen
        new(Mannschaftsart.Verein, "MJY", "DEN", Kondition.Schwach),   // FC Midtjylland
        new(Mannschaftsart.Verein, "VIB", "DEN", Kondition.Schwach),   // Viborg FF
        new(Mannschaftsart.Verein, "BSG", "ECU", Kondition.Schwach),   // Barcelona SC
        new(Mannschaftsart.Verein, "LDU", "ECU", Kondition.Schwach),   // LDU Quito
        new(Mannschaftsart.Verein, "ARS", "ENG", Kondition.Stark),   // Arsenal FC
        new(Mannschaftsart.Verein, "AVL", "ENG", Kondition.Stark),   // Aston Villa
        new(Mannschaftsart.Verein, "BIR", "ENG", Kondition.Stark),   // Birmingham City
        new(Mannschaftsart.Verein, "BNS", "ENG", Kondition.Stark),   // Barnsley FC
        new(Mannschaftsart.Verein, "BOU", "ENG", Kondition.Stark),   // AFC Bournemouth
        new(Mannschaftsart.Verein, "BRA", "ENG", Kondition.Stark),   // Bradford City
        new(Mannschaftsart.Verein, "BRC", "ENG", Kondition.Stark),   // Bristol City
        new(Mannschaftsart.Verein, "BUA", "ENG", Kondition.Schwach),   // Burton Albion
        new(Mannschaftsart.Verein, "BUR", "ENG", Kondition.Stark),   // Burnley FC
        new(Mannschaftsart.Verein, "CAR", "ENG", Kondition.Schwach),   // Cardiff City
        new(Mannschaftsart.Verein, "CHE", "ENG", Kondition.Stark),   // Chelsea FC
        new(Mannschaftsart.Verein, "COV", "ENG", Kondition.Stark),   // Coventry City
        new(Mannschaftsart.Verein, "EXE", "ENG", Kondition.Schwach),   // Exeter City
        new(Mannschaftsart.Verein, "FUL", "ENG", Kondition.Stark),   // Fulham FC
        new(Mannschaftsart.Verein, "HUD", "ENG", Kondition.Stark),   // Huddersfield Town
        new(Mannschaftsart.Verein, "IPS", "ENG", Kondition.Stark),   // Ipswich Town
        new(Mannschaftsart.Verein, "LEE", "ENG", Kondition.Stark),   // Leeds United
        new(Mannschaftsart.Verein, "LEY", "ENG", Kondition.Stark),   // Leyton Orient
        new(Mannschaftsart.Verein, "LIV", "ENG", Kondition.Stark),   // FC Liverpool
        new(Mannschaftsart.Verein, "MCI", "ENG", Kondition.Stark),   // Manchester City
        new(Mannschaftsart.Verein, "MID", "ENG", Kondition.Stark),   // Middlesbrough FC
        new(Mannschaftsart.Verein, "NEW", "ENG", Kondition.Stark),   // Newcastle United
        new(Mannschaftsart.Verein, "NOR", "ENG", Kondition.Stark),   // Norwich City
        new(Mannschaftsart.Verein, "PET", "ENG", Kondition.Schwach),   // Peterborough United
        new(Mannschaftsart.Verein, "PNE", "ENG", Kondition.Stark),   // Preston North End
        new(Mannschaftsart.Verein, "ROT", "ENG", Kondition.Schwach),   // Rotherham United
        new(Mannschaftsart.Verein, "SHW", "ENG", Kondition.Schwach),   // Sheffield Wednesday
        new(Mannschaftsart.Verein, "SOU", "ENG", Kondition.Stark),   // Southampton FC
        new(Mannschaftsart.Verein, "STK", "ENG", Kondition.Schwach),   // Stoke City
        new(Mannschaftsart.Verein, "TOT", "ENG", Kondition.Stark),   // Tottenham Hotspur
        new(Mannschaftsart.Verein, "WHU", "ENG", Kondition.Schwach),   // West Ham United
        new(Mannschaftsart.Verein, "ALB", "ESP", Kondition.Schwach),   // Albacete Balompié
        new(Mannschaftsart.Verein, "ALM", "ESP", Kondition.Schwach),   // UD Almería
        new(Mannschaftsart.Verein, "ALV", "ESP", Kondition.Schwach),   // Deportivo Alavés
        new(Mannschaftsart.Verein, "ATH", "ESP", Kondition.Stark),   // Athletic Club Bilbao
        new(Mannschaftsart.Verein, "ATM", "ESP", Kondition.Stark),   // Atlético Madrid
        new(Mannschaftsart.Verein, "BET", "ESP", Kondition.Schwach),   // Real Betis
        new(Mannschaftsart.Verein, "CCF", "ESP", Kondition.Schwach),   // Córdoba CF
        new(Mannschaftsart.Verein, "EIB", "ESP", Kondition.Schwach),   // SD Eibar
        new(Mannschaftsart.Verein, "GIR", "ESP", Kondition.Schwach),   // Girona FC
        new(Mannschaftsart.Verein, "LEG", "ESP", Kondition.Schwach),   // CD Leganés
        new(Mannschaftsart.Verein, "LEV", "ESP", Kondition.Stark),   // Levante UD
        new(Mannschaftsart.Verein, "MAL", "ESP", Kondition.Schwach),   // Málaga CF
        new(Mannschaftsart.Verein, "OVI", "ESP", Kondition.Schwach),   // Real Oviedo
        new(Mannschaftsart.Verein, "RAY", "ESP", Kondition.Stark),   // Rayo Vallecano
        new(Mannschaftsart.Verein, "RMA", "ESP", Kondition.Stark),   // Real Madrid
        new(Mannschaftsart.Verein, "VIL", "ESP", Kondition.Stark),   // Villarreal CF
        new(Mannschaftsart.Verein, "VLD", "ESP", Kondition.Schwach),   // Real Valladolid
        new(Mannschaftsart.Verein, "HJK", "FIN", Kondition.Schwach),   // HJK Helsinki
        new(Mannschaftsart.Verein, "ASS", "FRA", Kondition.Stark),   // AS Saint-Étienne
        new(Mannschaftsart.Verein, "CLE", "FRA", Kondition.Schwach),   // Clermont Foot
        new(Mannschaftsart.Verein, "EAG", "FRA", Kondition.Stark),   // EA Guingamp
        new(Mannschaftsart.Verein, "GRE", "FRA", Kondition.Schwach),   // Grenoble Foot 38
        new(Mannschaftsart.Verein, "LNS", "FRA", Kondition.Stark),   // RC Lens
        new(Mannschaftsart.Verein, "LOR", "FRA", Kondition.Stark),   // FC Lorient
        new(Mannschaftsart.Verein, "MHC", "FRA", Kondition.Schwach),   // Montpellier HSC
        new(Mannschaftsart.Verein, "NAN", "FRA", Kondition.Schwach),   // FC Nantes
        new(Mannschaftsart.Verein, "OGN", "FRA", Kondition.Stark),   // OGC Nice
        new(Mannschaftsart.Verein, "PAU", "FRA", Kondition.Schwach),   // Pau FC
        new(Mannschaftsart.Verein, "PSG", "FRA", Kondition.Stark),   // Paris Saint-Germain
        new(Mannschaftsart.Verein, "RMS", "FRA", Kondition.Stark),   // Stade de Reims
        new(Mannschaftsart.Verein, "ROD", "FRA", Kondition.Schwach),   // Rodez AF
        new(Mannschaftsart.Verein, "TRY", "FRA", Kondition.Schwach),   // ES Troyes AC
        new(Mannschaftsart.Verein, "EBR", "GER", Kondition.Schwach),   // Eintracht Braunschweig
        new(Mannschaftsart.Verein, "F95", "GER", Kondition.Schwach),   // Fortuna Düsseldorf
        new(Mannschaftsart.Verein, "FCB", "GER", Kondition.Stark),   // Bayern München
        new(Mannschaftsart.Verein, "FCI", "GER", Kondition.Schwach),   // FC Ingolstadt 04
        new(Mannschaftsart.Verein, "H96", "GER", Kondition.Stark),   // Hannover 96
        new(Mannschaftsart.Verein, "HSV", "GER", Kondition.Schwach),   // Hamburger SV
        new(Mannschaftsart.Verein, "JRG", "GER", Kondition.Schwach),   // Jahn Regensburg
        new(Mannschaftsart.Verein, "KAI", "GER", Kondition.Stark),   // 1. FC Kaiserslautern
        new(Mannschaftsart.Verein, "M05", "GER", Kondition.Stark),   // 1. FSV Mainz 05
        new(Mannschaftsart.Verein, "MSV", "GER", Kondition.Schwach),   // MSV Duisburg
        new(Mannschaftsart.Verein, "PRM", "GER", Kondition.Schwach),   // Preußen Münster
        new(Mannschaftsart.Verein, "RWE", "GER", Kondition.Schwach),   // Rot-Weiss Essen
        new(Mannschaftsart.Verein, "SCF", "GER", Kondition.Stark),   // SC Freiburg
        new(Mannschaftsart.Verein, "SCP", "GER", Kondition.Stark),   // SC Paderborn 07
        new(Mannschaftsart.Verein, "SGE", "GER", Kondition.Stark),   // Eintracht Frankfurt
        new(Mannschaftsart.Verein, "STPA", "GER", Kondition.Stark),   // FC St. Pauli
        new(Mannschaftsart.Verein, "SVW", "GER", Kondition.Stark),   // SV Werder Bremen
        new(Mannschaftsart.Verein, "VFB", "GER", Kondition.Stark),   // VfB Stuttgart
        new(Mannschaftsart.Verein, "VIK", "GER", Kondition.Schwach),   // FC Viktoria Köln
        new(Mannschaftsart.Verein, "WAL", "GER", Kondition.Schwach),   // SV Waldhof Mannheim
        new(Mannschaftsart.Verein, "WOB", "GER", Kondition.Stark),   // VfL Wolfsburg
        new(Mannschaftsart.Verein, "AEK", "GRE", Kondition.Schwach),   // AEK Athen
        new(Mannschaftsart.Verein, "OLY", "GRE", Kondition.Stark),   // Olympiakos Piräus
        new(Mannschaftsart.Verein, "PAK", "GRE", Kondition.Stark),   // PAOK Thessaloniki
        new(Mannschaftsart.Verein, "PAO", "GRE", Kondition.Schwach),   // Panathinaikos
        new(Mannschaftsart.Verein, "OLH", "HON", Kondition.Schwach),   // CD Olimpia
        new(Mannschaftsart.Verein, "GOA", "IND", Kondition.Schwach),   // FC Goa
        new(Mannschaftsart.Verein, "MUC", "IND", Kondition.Schwach),   // Mumbai City FC
        new(Mannschaftsart.Verein, "SHR", "IRL", Kondition.Schwach),   // Shamrock Rovers
        new(Mannschaftsart.Verein, "CRR", "ITA", Kondition.Schwach),   // Carrarese Calcio
        new(Mannschaftsart.Verein, "EMP", "ITA", Kondition.Schwach),   // Empoli FC
        new(Mannschaftsart.Verein, "FRO", "ITA", Kondition.Schwach),   // Frosinone Calcio
        new(Mannschaftsart.Verein, "GEN", "ITA", Kondition.Stark),   // Genoa CFC
        new(Mannschaftsart.Verein, "JUV", "ITA", Kondition.Stark),   // Juventus Turin
        new(Mannschaftsart.Verein, "LAZ", "ITA", Kondition.Stark),   // SS Lazio
        new(Mannschaftsart.Verein, "LEC", "ITA", Kondition.Schwach),   // US Lecce
        new(Mannschaftsart.Verein, "MOD", "ITA", Kondition.Schwach),   // Modena FC
        new(Mannschaftsart.Verein, "NAP", "ITA", Kondition.Stark),   // SSC Napoli
        new(Mannschaftsart.Verein, "PIS", "ITA", Kondition.Schwach),   // Pisa SC
        new(Mannschaftsart.Verein, "PLM", "ITA", Kondition.Stark),   // Palermo FC
        new(Mannschaftsart.Verein, "ROM", "ITA", Kondition.Stark),   // AS Roma
        new(Mannschaftsart.Verein, "SUD", "ITA", Kondition.Schwach),   // FC Südtirol
        new(Mannschaftsart.Verein, "TOR", "ITA", Kondition.Schwach),   // Torino FC
        new(Mannschaftsart.Verein, "DJN", "KOR", Kondition.Stark),   // Daejeon Hana Citizen
        new(Mannschaftsart.Verein, "FCS", "KOR", Kondition.Stark),   // FC Seoul
        new(Mannschaftsart.Verein, "GIM", "KOR", Kondition.Stark),   // Gimcheon Sangmu
        new(Mannschaftsart.Verein, "GWN", "KOR", Kondition.Stark),   // Gangwon FC
        new(Mannschaftsart.Verein, "JEJ", "KOR", Kondition.Stark),   // Jeju United
        new(Mannschaftsart.Verein, "ALS", "KSA", Kondition.Stark),   // Al-Ahli
        new(Mannschaftsart.Verein, "FAY", "KSA", Kondition.Schwach),   // Al-Fayha
        new(Mannschaftsart.Verein, "HIL", "KSA", Kondition.Stark),   // Al-Hilal
        new(Mannschaftsart.Verein, "KHO", "KSA", Kondition.Schwach),   // Al-Kholood
        new(Mannschaftsart.Verein, "QAD", "KSA", Kondition.Stark),   // Al-Qadsiah
        new(Mannschaftsart.Verein, "SHB", "KSA", Kondition.Schwach),   // Al-Shabab
        new(Mannschaftsart.Verein, "BOD", "NOR", Kondition.Stark),   // FK Bodø/Glimt
        new(Mannschaftsart.Verein, "BRN", "NOR", Kondition.Stark),   // SK Brann
        new(Mannschaftsart.Verein, "FRE", "NOR", Kondition.Stark),   // Fredrikstad FK
        new(Mannschaftsart.Verein, "MOL", "NOR", Kondition.Stark),   // Molde FK
        new(Mannschaftsart.Verein, "ROS", "NOR", Kondition.Stark),   // Rosenborg BK
        new(Mannschaftsart.Verein, "VKG", "NOR", Kondition.Stark),   // Viking FK
        new(Mannschaftsart.Verein, "AKL", "NZL", Kondition.Stark),   // Auckland FC
        new(Mannschaftsart.Verein, "CCP", "PAR", Kondition.Schwach),   // Cerro Porteño
        new(Mannschaftsart.Verein, "LIB", "PAR", Kondition.Schwach),   // Club Libertad
        new(Mannschaftsart.Verein, "ALI", "PER", Kondition.Schwach),   // Alianza Lima
        new(Mannschaftsart.Verein, "LGW", "POL", Kondition.Stark),   // Legia Warschau
        new(Mannschaftsart.Verein, "LPO", "POL", Kondition.Stark),   // Lech Posen
        new(Mannschaftsart.Verein, "SCB", "POR", Kondition.Stark),   // SC Braga
        new(Mannschaftsart.Verein, "SCL", "POR", Kondition.Schwach),   // CD Santa Clara
        new(Mannschaftsart.Verein, "SLB", "POR", Kondition.Stark),   // SL Benfica
        new(Mannschaftsart.Verein, "SPS", "POR", Kondition.Stark),   // Sporting CP
        new(Mannschaftsart.Verein, "CFR", "ROU", Kondition.Schwach),   // CFR Cluj
        new(Mannschaftsart.Verein, "FCSB", "ROU", Kondition.Stark),   // FCSB
        new(Mannschaftsart.Verein, "CLT", "SCO", Kondition.Stark),   // Celtic FC
        new(Mannschaftsart.Verein, "HRT", "SCO", Kondition.Stark),   // Heart of Midlothian
        new(Mannschaftsart.Verein, "CCT", "SOL", Kondition.Schwach),   // Central Coast FC
        new(Mannschaftsart.Verein, "LUG", "SUI", Kondition.Schwach),   // FC Lugano
        new(Mannschaftsart.Verein, "DIF", "SWE", Kondition.Schwach),   // Djurgårdens IF
        new(Mannschaftsart.Verein, "BSK", "TUR", Kondition.Stark),   // İstanbul Başakşehir
        new(Mannschaftsart.Verein, "GAL", "TUR", Kondition.Stark),   // Galatasaray SK
        new(Mannschaftsart.Verein, "KON", "TUR", Kondition.Schwach),   // Konyaspor
        new(Mannschaftsart.Verein, "SCH", "UKR", Kondition.Schwach),   // Schachtar Donezk
        new(Mannschaftsart.Verein, "PEN", "URU", Kondition.Schwach),   // CA Peñarol
        new(Mannschaftsart.Verein, "ATL", "USA", Kondition.Stark),   // Atlanta United
        new(Mannschaftsart.Verein, "CIN", "USA", Kondition.Stark),   // FC Cincinnati
        new(Mannschaftsart.Verein, "CMB", "USA", Kondition.Stark),   // Columbus Crew
        new(Mannschaftsart.Verein, "DAL", "USA", Kondition.Schwach),   // FC Dallas
        new(Mannschaftsart.Verein, "DCU", "USA", Kondition.Schwach),   // D.C. United
        new(Mannschaftsart.Verein, "NER", "USA", Kondition.Stark),   // New England Revolution
        new(Mannschaftsart.Verein, "NYC", "USA", Kondition.Schwach),   // New York City FC
        new(Mannschaftsart.Verein, "NYR", "USA", Kondition.Schwach),   // New York Red Bulls
        new(Mannschaftsart.Verein, "RSL", "USA", Kondition.Schwach),   // Real Salt Lake
        new(Mannschaftsart.Verein, "SEA", "USA", Kondition.Stark),   // Seattle Sounders FC
        new(Mannschaftsart.Verein, "CRS", "VEN", Kondition.Schwach),   // Caracas FC
        new(Mannschaftsart.Nationalmannschaft, "ALG", "ALG", Kondition.Stark),
        new(Mannschaftsart.Nationalmannschaft, "AUS", "AUS", Kondition.Stark),
        new(Mannschaftsart.Nationalmannschaft, "AUT", "AUT", Kondition.Stark),
        new(Mannschaftsart.Nationalmannschaft, "AZE", "AZE", Kondition.Schwach),
        new(Mannschaftsart.Nationalmannschaft, "BEL", "BEL", Kondition.Schwach),
        new(Mannschaftsart.Nationalmannschaft, "BOL", "BOL", Kondition.Schwach),
        new(Mannschaftsart.Nationalmannschaft, "CHI", "CHI", Kondition.Schwach),
        new(Mannschaftsart.Nationalmannschaft, "CIV", "CIV", Kondition.Stark),
        new(Mannschaftsart.Nationalmannschaft, "CMR", "CMR", Kondition.Stark),
        new(Mannschaftsart.Nationalmannschaft, "COL", "COL", Kondition.Stark),
        new(Mannschaftsart.Nationalmannschaft, "CRC", "CRC", Kondition.Schwach),
        new(Mannschaftsart.Nationalmannschaft, "CUW", "CUW", Kondition.Schwach),
        new(Mannschaftsart.Nationalmannschaft, "CZE", "CZE", Kondition.Stark),
        new(Mannschaftsart.Nationalmannschaft, "ENG", "ENG", Kondition.Stark),
        new(Mannschaftsart.Nationalmannschaft, "FRA", "FRA", Kondition.Stark),
        new(Mannschaftsart.Nationalmannschaft, "GAM", "GAM", Kondition.Stark),
        new(Mannschaftsart.Nationalmannschaft, "HAI", "HAI", Kondition.Schwach),
        new(Mannschaftsart.Nationalmannschaft, "IDN", "IDN", Kondition.Schwach),
        new(Mannschaftsart.Nationalmannschaft, "IND", "IND", Kondition.Schwach),
        new(Mannschaftsart.Nationalmannschaft, "ISL", "ISL", Kondition.Stark),
        new(Mannschaftsart.Nationalmannschaft, "JAM", "JAM", Kondition.Schwach),
        new(Mannschaftsart.Nationalmannschaft, "JPN", "JPN", Kondition.Stark),
        new(Mannschaftsart.Nationalmannschaft, "KOR", "KOR", Kondition.Schwach),
        new(Mannschaftsart.Nationalmannschaft, "KSA", "KSA", Kondition.Stark),
        new(Mannschaftsart.Nationalmannschaft, "LUX", "LUX", Kondition.Schwach),
        new(Mannschaftsart.Nationalmannschaft, "MEX", "MEX", Kondition.Stark),
        new(Mannschaftsart.Nationalmannschaft, "MKD", "MKD", Kondition.Schwach),
        new(Mannschaftsart.Nationalmannschaft, "MLI", "MLI", Kondition.Stark),
        new(Mannschaftsart.Nationalmannschaft, "MNE", "MNE", Kondition.Schwach),
        new(Mannschaftsart.Nationalmannschaft, "NED", "NED", Kondition.Stark),
        new(Mannschaftsart.Nationalmannschaft, "NGA", "NGA", Kondition.Stark),
        new(Mannschaftsart.Nationalmannschaft, "NOR", "NOR", Kondition.Stark),
        new(Mannschaftsart.Nationalmannschaft, "PAN", "PAN", Kondition.Schwach),
        new(Mannschaftsart.Nationalmannschaft, "PER", "PER", Kondition.Schwach),
        new(Mannschaftsart.Nationalmannschaft, "POL", "POL", Kondition.Schwach),
        new(Mannschaftsart.Nationalmannschaft, "POR", "POR", Kondition.Stark),
        new(Mannschaftsart.Nationalmannschaft, "RSA", "RSA", Kondition.Schwach),
        new(Mannschaftsart.Nationalmannschaft, "RUS", "RUS", Kondition.Schwach),
        new(Mannschaftsart.Nationalmannschaft, "SCO", "SCO", Kondition.Stark),
        new(Mannschaftsart.Nationalmannschaft, "SRB", "SRB", Kondition.Schwach),
        new(Mannschaftsart.Nationalmannschaft, "SUR", "SUR", Kondition.Schwach),
        new(Mannschaftsart.Nationalmannschaft, "SWE", "SWE", Kondition.Stark),
        new(Mannschaftsart.Nationalmannschaft, "TUN", "TUN", Kondition.Stark),
        new(Mannschaftsart.Nationalmannschaft, "USA", "USA", Kondition.Stark),
        new(Mannschaftsart.Nationalmannschaft, "VEN", "VEN", Kondition.Schwach),
        new(Mannschaftsart.Nationalmannschaft, "ZIM", "ZIM", Kondition.Schwach),
    ];
}
