using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Infrastructure.Seed.Daten;

/// <summary>Erzeugt von <c>tools/Kondition-EAFC/auswerten.py</c> (#669) – nicht von Hand ändern. Nur die
/// Teams, die vom Standard „normal“ abweichen.</summary>
public static class NervenstaerkeSeedListe
{
    public static readonly IReadOnlyList<NervenstaerkeSeedDaten.Eintrag> Handliste =
    [
        new(Mannschaftsart.Verein, "ARJ", "ARG", Nervenstaerke.Stark),   // Argentinos Juniors
        new(Mannschaftsart.Verein, "BOJ", "ARG", Nervenstaerke.Stark),   // Boca Juniors
        new(Mannschaftsart.Verein, "CAT", "ARG", Nervenstaerke.Schwach),   // CA Tigre
        new(Mannschaftsart.Verein, "EDL", "ARG", Nervenstaerke.Stark),   // Estudiantes de La Plata
        new(Mannschaftsart.Verein, "HUR", "ARG", Nervenstaerke.Schwach),   // CA Huracán
        new(Mannschaftsart.Verein, "ICO", "ARG", Nervenstaerke.Schwach),   // Instituto Córdoba
        new(Mannschaftsart.Verein, "LAN", "ARG", Nervenstaerke.Stark),   // CA Lanús
        new(Mannschaftsart.Verein, "PLA", "ARG", Nervenstaerke.Schwach),   // CA Platense
        new(Mannschaftsart.Verein, "RAC", "ARG", Nervenstaerke.Stark),   // Racing Club
        new(Mannschaftsart.Verein, "RIE", "ARG", Nervenstaerke.Schwach),   // Deportivo Riestra
        new(Mannschaftsart.Verein, "RIV", "ARG", Nervenstaerke.Stark),   // River Plate
        new(Mannschaftsart.Verein, "SAR", "ARG", Nervenstaerke.Schwach),   // Sarmiento Junín
        new(Mannschaftsart.Verein, "ADE", "AUS", Nervenstaerke.Schwach),   // Adelaide United
        new(Mannschaftsart.Verein, "BRI", "AUS", Nervenstaerke.Schwach),   // Brisbane Roar
        new(Mannschaftsart.Verein, "MAC", "AUS", Nervenstaerke.Schwach),   // Macarthur FC
        new(Mannschaftsart.Verein, "MCY", "AUS", Nervenstaerke.Schwach),   // Melbourne City FC
        new(Mannschaftsart.Verein, "MEV", "AUS", Nervenstaerke.Schwach),   // Melbourne Victory
        new(Mannschaftsart.Verein, "NCJ", "AUS", Nervenstaerke.Schwach),   // Newcastle Jets
        new(Mannschaftsart.Verein, "PGL", "AUS", Nervenstaerke.Schwach),   // Perth Glory
        new(Mannschaftsart.Verein, "SYD", "AUS", Nervenstaerke.Schwach),   // Sydney FC
        new(Mannschaftsart.Verein, "STU", "AUT", Nervenstaerke.Schwach),   // SK Sturm Graz
        new(Mannschaftsart.Verein, "QAR", "AZE", Nervenstaerke.Schwach),   // Qarabağ Ağdam
        new(Mannschaftsart.Verein, "CER", "BEL", Nervenstaerke.Schwach),   // Cercle Brügge
        new(Mannschaftsart.Verein, "CLB", "BEL", Nervenstaerke.Stark),   // Club Brugge KV
        new(Mannschaftsart.Verein, "MTL", "CAN", Nervenstaerke.Schwach),   // CF Montréal
        new(Mannschaftsart.Verein, "UCH", "CHI", Nervenstaerke.Stark),   // Universidad de Chile
        new(Mannschaftsart.Verein, "WUT", "CHN", Nervenstaerke.Schwach),   // Wuhan Three Towns
        new(Mannschaftsart.Verein, "OCA", "COL", Nervenstaerke.Schwach),   // Once Caldas
        new(Mannschaftsart.Verein, "DZG", "CRO", Nervenstaerke.Stark),   // Dinamo Zagreb
        new(Mannschaftsart.Verein, "SLP", "CZE", Nervenstaerke.Stark),   // Slavia Prag
        new(Mannschaftsart.Verein, "SPP", "CZE", Nervenstaerke.Stark),   // Sparta Prag
        new(Mannschaftsart.Verein, "MJY", "DEN", Nervenstaerke.Schwach),   // FC Midtjylland
        new(Mannschaftsart.Verein, "NSJ", "DEN", Nervenstaerke.Schwach),   // FC Nordsjælland
        new(Mannschaftsart.Verein, "SIL", "DEN", Nervenstaerke.Schwach),   // Silkeborg IF
        new(Mannschaftsart.Verein, "VIB", "DEN", Nervenstaerke.Schwach),   // Viborg FF
        new(Mannschaftsart.Verein, "ARS", "ENG", Nervenstaerke.Stark),   // Arsenal FC
        new(Mannschaftsart.Verein, "AVL", "ENG", Nervenstaerke.Stark),   // Aston Villa
        new(Mannschaftsart.Verein, "BHA", "ENG", Nervenstaerke.Stark),   // Brighton & Hove Albion
        new(Mannschaftsart.Verein, "BIR", "ENG", Nervenstaerke.Stark),   // Birmingham City
        new(Mannschaftsart.Verein, "BOU", "ENG", Nervenstaerke.Stark),   // AFC Bournemouth
        new(Mannschaftsart.Verein, "BRE", "ENG", Nervenstaerke.Stark),   // Brentford FC
        new(Mannschaftsart.Verein, "BUA", "ENG", Nervenstaerke.Schwach),   // Burton Albion
        new(Mannschaftsart.Verein, "CHE", "ENG", Nervenstaerke.Stark),   // Chelsea FC
        new(Mannschaftsart.Verein, "DON", "ENG", Nervenstaerke.Schwach),   // Doncaster Rovers
        new(Mannschaftsart.Verein, "EVE", "ENG", Nervenstaerke.Stark),   // Everton FC
        new(Mannschaftsart.Verein, "FUL", "ENG", Nervenstaerke.Stark),   // Fulham FC
        new(Mannschaftsart.Verein, "IPS", "ENG", Nervenstaerke.Stark),   // Ipswich Town
        new(Mannschaftsart.Verein, "LEI", "ENG", Nervenstaerke.Stark),   // Leicester City
        new(Mannschaftsart.Verein, "LEY", "ENG", Nervenstaerke.Schwach),   // Leyton Orient
        new(Mannschaftsart.Verein, "LIV", "ENG", Nervenstaerke.Stark),   // FC Liverpool
        new(Mannschaftsart.Verein, "MCI", "ENG", Nervenstaerke.Stark),   // Manchester City
        new(Mannschaftsart.Verein, "MFT", "ENG", Nervenstaerke.Schwach),   // Mansfield Town
        new(Mannschaftsart.Verein, "MNU", "ENG", Nervenstaerke.Stark),   // Manchester United
        new(Mannschaftsart.Verein, "NEW", "ENG", Nervenstaerke.Stark),   // Newcastle United
        new(Mannschaftsart.Verein, "NFO", "ENG", Nervenstaerke.Stark),   // Nottingham Forest
        new(Mannschaftsart.Verein, "PET", "ENG", Nervenstaerke.Schwach),   // Peterborough United
        new(Mannschaftsart.Verein, "PLY", "ENG", Nervenstaerke.Schwach),   // Plymouth Argyle
        new(Mannschaftsart.Verein, "PVA", "ENG", Nervenstaerke.Schwach),   // Port Vale
        new(Mannschaftsart.Verein, "REA", "ENG", Nervenstaerke.Schwach),   // Reading FC
        new(Mannschaftsart.Verein, "STE", "ENG", Nervenstaerke.Schwach),   // Stevenage FC
        new(Mannschaftsart.Verein, "SUN", "ENG", Nervenstaerke.Stark),   // Sunderland AFC
        new(Mannschaftsart.Verein, "TOT", "ENG", Nervenstaerke.Stark),   // Tottenham Hotspur
        new(Mannschaftsart.Verein, "WHU", "ENG", Nervenstaerke.Stark),   // West Ham United
        new(Mannschaftsart.Verein, "WIG", "ENG", Nervenstaerke.Schwach),   // Wigan Athletic
        new(Mannschaftsart.Verein, "WIM", "ENG", Nervenstaerke.Schwach),   // AFC Wimbledon
        new(Mannschaftsart.Verein, "WYC", "ENG", Nervenstaerke.Schwach),   // Wycombe Wanderers
        new(Mannschaftsart.Verein, "ATH", "ESP", Nervenstaerke.Stark),   // Athletic Club Bilbao
        new(Mannschaftsart.Verein, "ATM", "ESP", Nervenstaerke.Stark),   // Atlético Madrid
        new(Mannschaftsart.Verein, "BAR", "ESP", Nervenstaerke.Stark),   // FC Barcelona
        new(Mannschaftsart.Verein, "BET", "ESP", Nervenstaerke.Stark),   // Real Betis
        new(Mannschaftsart.Verein, "CCF", "ESP", Nervenstaerke.Schwach),   // Córdoba CF
        new(Mannschaftsart.Verein, "CEL", "ESP", Nervenstaerke.Stark),   // Celta Vigo
        new(Mannschaftsart.Verein, "EPN", "ESP", Nervenstaerke.Stark),   // RCD Espanyol
        new(Mannschaftsart.Verein, "GET", "ESP", Nervenstaerke.Stark),   // Getafe CF
        new(Mannschaftsart.Verein, "GIR", "ESP", Nervenstaerke.Stark),   // Girona FC
        new(Mannschaftsart.Verein, "LEV", "ESP", Nervenstaerke.Stark),   // Levante UD
        new(Mannschaftsart.Verein, "OVI", "ESP", Nervenstaerke.Stark),   // Real Oviedo
        new(Mannschaftsart.Verein, "RMA", "ESP", Nervenstaerke.Stark),   // Real Madrid
        new(Mannschaftsart.Verein, "RSO", "ESP", Nervenstaerke.Stark),   // Real Sociedad
        new(Mannschaftsart.Verein, "SEV", "ESP", Nervenstaerke.Stark),   // Sevilla FC
        new(Mannschaftsart.Verein, "VCF", "ESP", Nervenstaerke.Stark),   // Valencia CF
        new(Mannschaftsart.Verein, "VIL", "ESP", Nervenstaerke.Stark),   // Villarreal CF
        new(Mannschaftsart.Verein, "HJK", "FIN", Nervenstaerke.Schwach),   // HJK Helsinki
        new(Mannschaftsart.Verein, "ANN", "FRA", Nervenstaerke.Schwach),   // FC Annecy
        new(Mannschaftsart.Verein, "CLE", "FRA", Nervenstaerke.Schwach),   // Clermont Foot
        new(Mannschaftsart.Verein, "GRE", "FRA", Nervenstaerke.Schwach),   // Grenoble Foot 38
        new(Mannschaftsart.Verein, "LNS", "FRA", Nervenstaerke.Stark),   // RC Lens
        new(Mannschaftsart.Verein, "LOS", "FRA", Nervenstaerke.Stark),   // LOSC Lille
        new(Mannschaftsart.Verein, "MON", "FRA", Nervenstaerke.Stark),   // AS Monaco
        new(Mannschaftsart.Verein, "OL", "FRA", Nervenstaerke.Stark),   // Olympique Lyonnais
        new(Mannschaftsart.Verein, "OM", "FRA", Nervenstaerke.Stark),   // Olympique de Marseille
        new(Mannschaftsart.Verein, "PAU", "FRA", Nervenstaerke.Schwach),   // Pau FC
        new(Mannschaftsart.Verein, "PSG", "FRA", Nervenstaerke.Stark),   // Paris Saint-Germain
        new(Mannschaftsart.Verein, "REN", "FRA", Nervenstaerke.Stark),   // Stade Rennais
        new(Mannschaftsart.Verein, "ROD", "FRA", Nervenstaerke.Schwach),   // Rodez AF
        new(Mannschaftsart.Verein, "RSF", "FRA", Nervenstaerke.Schwach),   // Red Star FC
        new(Mannschaftsart.Verein, "TRY", "FRA", Nervenstaerke.Schwach),   // ES Troyes AC
        new(Mannschaftsart.Verein, "AAC", "GER", Nervenstaerke.Schwach),   // Alemannia Aachen
        new(Mannschaftsart.Verein, "B04", "GER", Nervenstaerke.Stark),   // Bayer 04 Leverkusen
        new(Mannschaftsart.Verein, "BVB", "GER", Nervenstaerke.Stark),   // Borussia Dortmund
        new(Mannschaftsart.Verein, "DSC", "GER", Nervenstaerke.Schwach),   // Arminia Bielefeld
        new(Mannschaftsart.Verein, "EBR", "GER", Nervenstaerke.Schwach),   // Eintracht Braunschweig
        new(Mannschaftsart.Verein, "FCB", "GER", Nervenstaerke.Stark),   // Bayern München
        new(Mannschaftsart.Verein, "FCE", "GER", Nervenstaerke.Schwach),   // FC Energie Cottbus
        new(Mannschaftsart.Verein, "FCI", "GER", Nervenstaerke.Schwach),   // FC Ingolstadt 04
        new(Mannschaftsart.Verein, "HAV", "GER", Nervenstaerke.Schwach),   // TSV Havelse
        new(Mannschaftsart.Verein, "HRO", "GER", Nervenstaerke.Schwach),   // FC Hansa Rostock
        new(Mannschaftsart.Verein, "JRG", "GER", Nervenstaerke.Schwach),   // Jahn Regensburg
        new(Mannschaftsart.Verein, "KAI", "GER", Nervenstaerke.Schwach),   // 1. FC Kaiserslautern
        new(Mannschaftsart.Verein, "KIE", "GER", Nervenstaerke.Schwach),   // Holstein Kiel
        new(Mannschaftsart.Verein, "MSV", "GER", Nervenstaerke.Schwach),   // MSV Duisburg
        new(Mannschaftsart.Verein, "OSN", "GER", Nervenstaerke.Schwach),   // VfL Osnabrück
        new(Mannschaftsart.Verein, "PRM", "GER", Nervenstaerke.Schwach),   // Preußen Münster
        new(Mannschaftsart.Verein, "RWE", "GER", Nervenstaerke.Schwach),   // Rot-Weiss Essen
        new(Mannschaftsart.Verein, "SGD", "GER", Nervenstaerke.Schwach),   // Dynamo Dresden
        new(Mannschaftsart.Verein, "SGE", "GER", Nervenstaerke.Stark),   // Eintracht Frankfurt
        new(Mannschaftsart.Verein, "SVE", "GER", Nervenstaerke.Schwach),   // SV Elversberg
        new(Mannschaftsart.Verein, "SVW", "GER", Nervenstaerke.Stark),   // SV Werder Bremen
        new(Mannschaftsart.Verein, "TSG", "GER", Nervenstaerke.Stark),   // TSG 1899 Hoffenheim
        new(Mannschaftsart.Verein, "VIK", "GER", Nervenstaerke.Schwach),   // FC Viktoria Köln
        new(Mannschaftsart.Verein, "VRL", "GER", Nervenstaerke.Schwach),   // SC Verl
        new(Mannschaftsart.Verein, "WAL", "GER", Nervenstaerke.Schwach),   // SV Waldhof Mannheim
        new(Mannschaftsart.Verein, "WIE", "GER", Nervenstaerke.Schwach),   // SV Wehen Wiesbaden
        new(Mannschaftsart.Verein, "WOB", "GER", Nervenstaerke.Stark),   // VfL Wolfsburg
        new(Mannschaftsart.Verein, "AEK", "GRE", Nervenstaerke.Stark),   // AEK Athen
        new(Mannschaftsart.Verein, "OLY", "GRE", Nervenstaerke.Stark),   // Olympiakos Piräus
        new(Mannschaftsart.Verein, "PAK", "GRE", Nervenstaerke.Stark),   // PAOK Thessaloniki
        new(Mannschaftsart.Verein, "PAO", "GRE", Nervenstaerke.Stark),   // Panathinaikos
        new(Mannschaftsart.Verein, "GOA", "IND", Nervenstaerke.Schwach),   // FC Goa
        new(Mannschaftsart.Verein, "MUC", "IND", Nervenstaerke.Schwach),   // Mumbai City FC
        new(Mannschaftsart.Verein, "SHR", "IRL", Nervenstaerke.Schwach),   // Shamrock Rovers
        new(Mannschaftsart.Verein, "BOL", "ITA", Nervenstaerke.Stark),   // Bologna FC
        new(Mannschaftsart.Verein, "CAG", "ITA", Nervenstaerke.Stark),   // Cagliari Calcio
        new(Mannschaftsart.Verein, "CES", "ITA", Nervenstaerke.Schwach),   // Cesena FC
        new(Mannschaftsart.Verein, "CMO", "ITA", Nervenstaerke.Stark),   // Como 1907
        new(Mannschaftsart.Verein, "CRE", "ITA", Nervenstaerke.Stark),   // US Cremonese
        new(Mannschaftsart.Verein, "EMP", "ITA", Nervenstaerke.Schwach),   // Empoli FC
        new(Mannschaftsart.Verein, "FIO", "ITA", Nervenstaerke.Stark),   // ACF Fiorentina
        new(Mannschaftsart.Verein, "FRO", "ITA", Nervenstaerke.Schwach),   // Frosinone Calcio
        new(Mannschaftsart.Verein, "GEN", "ITA", Nervenstaerke.Stark),   // Genoa CFC
        new(Mannschaftsart.Verein, "JUV", "ITA", Nervenstaerke.Stark),   // Juventus Turin
        new(Mannschaftsart.Verein, "LAZ", "ITA", Nervenstaerke.Stark),   // SS Lazio
        new(Mannschaftsart.Verein, "MNZ", "ITA", Nervenstaerke.Stark),   // AC Monza
        new(Mannschaftsart.Verein, "NAP", "ITA", Nervenstaerke.Stark),   // SSC Napoli
        new(Mannschaftsart.Verein, "PLM", "ITA", Nervenstaerke.Stark),   // Palermo FC
        new(Mannschaftsart.Verein, "ROM", "ITA", Nervenstaerke.Stark),   // AS Roma
        new(Mannschaftsart.Verein, "SUD", "ITA", Nervenstaerke.Schwach),   // FC Südtirol
        new(Mannschaftsart.Verein, "TOR", "ITA", Nervenstaerke.Stark),   // Torino FC
        new(Mannschaftsart.Verein, "DJN", "KOR", Nervenstaerke.Schwach),   // Daejeon Hana Citizen
        new(Mannschaftsart.Verein, "GIM", "KOR", Nervenstaerke.Schwach),   // Gimcheon Sangmu
        new(Mannschaftsart.Verein, "GWJ", "KOR", Nervenstaerke.Schwach),   // Gwangju FC
        new(Mannschaftsart.Verein, "GWN", "KOR", Nervenstaerke.Schwach),   // Gangwon FC
        new(Mannschaftsart.Verein, "JEJ", "KOR", Nervenstaerke.Schwach),   // Jeju United
        new(Mannschaftsart.Verein, "ALS", "KSA", Nervenstaerke.Stark),   // Al-Ahli
        new(Mannschaftsart.Verein, "ETT", "KSA", Nervenstaerke.Stark),   // Al-Ettifaq
        new(Mannschaftsart.Verein, "FAY", "KSA", Nervenstaerke.Schwach),   // Al-Fayha
        new(Mannschaftsart.Verein, "HIL", "KSA", Nervenstaerke.Stark),   // Al-Hilal
        new(Mannschaftsart.Verein, "IHD", "KSA", Nervenstaerke.Stark),   // Al-Ittihad
        new(Mannschaftsart.Verein, "KHO", "KSA", Nervenstaerke.Schwach),   // Al-Kholood
        new(Mannschaftsart.Verein, "NSR", "KSA", Nervenstaerke.Stark),   // Al-Nassr
        new(Mannschaftsart.Verein, "FRE", "NOR", Nervenstaerke.Schwach),   // Fredrikstad FK
        new(Mannschaftsart.Verein, "TRO", "NOR", Nervenstaerke.Schwach),   // Tromsø IL
        new(Mannschaftsart.Verein, "VAL", "NOR", Nervenstaerke.Schwach),   // Vålerenga IF
        new(Mannschaftsart.Verein, "AKL", "NZL", Nervenstaerke.Schwach),   // Auckland FC
        new(Mannschaftsart.Verein, "WEL", "NZL", Nervenstaerke.Schwach),   // Wellington Phoenix
        new(Mannschaftsart.Verein, "LIB", "PAR", Nervenstaerke.Schwach),   // Club Libertad
        new(Mannschaftsart.Verein, "ALI", "PER", Nervenstaerke.Stark),   // Alianza Lima
        new(Mannschaftsart.Verein, "FCP", "POR", Nervenstaerke.Stark),   // FC Porto
        new(Mannschaftsart.Verein, "GIL", "POR", Nervenstaerke.Schwach),   // Gil Vicente FC
        new(Mannschaftsart.Verein, "SCB", "POR", Nervenstaerke.Stark),   // SC Braga
        new(Mannschaftsart.Verein, "SLB", "POR", Nervenstaerke.Stark),   // SL Benfica
        new(Mannschaftsart.Verein, "SPS", "POR", Nervenstaerke.Stark),   // Sporting CP
        new(Mannschaftsart.Verein, "CFR", "ROU", Nervenstaerke.Schwach),   // CFR Cluj
        new(Mannschaftsart.Verein, "UCR", "ROU", Nervenstaerke.Schwach),   // Universitatea Craiova
        new(Mannschaftsart.Verein, "CLT", "SCO", Nervenstaerke.Stark),   // Celtic FC
        new(Mannschaftsart.Verein, "DUN", "SCO", Nervenstaerke.Schwach),   // Dundee United
        new(Mannschaftsart.Verein, "RAN", "SCO", Nervenstaerke.Stark),   // Rangers FC
        new(Mannschaftsart.Verein, "CCT", "SOL", Nervenstaerke.Schwach),   // Central Coast FC
        new(Mannschaftsart.Verein, "LAU", "SUI", Nervenstaerke.Schwach),   // FC Lausanne-Sport
        new(Mannschaftsart.Verein, "SER", "SUI", Nervenstaerke.Schwach),   // Servette FC
        new(Mannschaftsart.Verein, "ELF", "SWE", Nervenstaerke.Schwach),   // IF Elfsborg
        new(Mannschaftsart.Verein, "BJK", "TUR", Nervenstaerke.Stark),   // Beşiktaş JK
        new(Mannschaftsart.Verein, "BSK", "TUR", Nervenstaerke.Stark),   // İstanbul Başakşehir
        new(Mannschaftsart.Verein, "FEN", "TUR", Nervenstaerke.Stark),   // Fenerbahçe SK
        new(Mannschaftsart.Verein, "GAL", "TUR", Nervenstaerke.Stark),   // Galatasaray SK
        new(Mannschaftsart.Verein, "KON", "TUR", Nervenstaerke.Stark),   // Konyaspor
        new(Mannschaftsart.Verein, "TRA", "TUR", Nervenstaerke.Stark),   // Trabzonspor
        new(Mannschaftsart.Verein, "DYN", "UKR", Nervenstaerke.Stark),   // Dynamo Kyiv
        new(Mannschaftsart.Verein, "ATL", "USA", Nervenstaerke.Stark),   // Atlanta United
        new(Mannschaftsart.Verein, "CRA", "USA", Nervenstaerke.Schwach),   // Colorado Rapids
        new(Mannschaftsart.Verein, "DAL", "USA", Nervenstaerke.Schwach),   // FC Dallas
        new(Mannschaftsart.Verein, "DCU", "USA", Nervenstaerke.Schwach),   // D.C. United
        new(Mannschaftsart.Verein, "MIA", "USA", Nervenstaerke.Stark),   // Inter Miami CF
        new(Mannschaftsart.Verein, "NYR", "USA", Nervenstaerke.Schwach),   // New York Red Bulls
        new(Mannschaftsart.Verein, "PTI", "USA", Nervenstaerke.Stark),   // Portland Timbers
        new(Mannschaftsart.Verein, "CRS", "VEN", Nervenstaerke.Schwach),   // Caracas FC
        new(Mannschaftsart.Nationalmannschaft, "ALG", "ALG", Nervenstaerke.Stark),
        new(Mannschaftsart.Nationalmannschaft, "ARG", "ARG", Nervenstaerke.Stark),
        new(Mannschaftsart.Nationalmannschaft, "AZE", "AZE", Nervenstaerke.Schwach),
        new(Mannschaftsart.Nationalmannschaft, "BEL", "BEL", Nervenstaerke.Stark),
        new(Mannschaftsart.Nationalmannschaft, "BFA", "BFA", Nervenstaerke.Schwach),
        new(Mannschaftsart.Nationalmannschaft, "BOL", "BOL", Nervenstaerke.Schwach),
        new(Mannschaftsart.Nationalmannschaft, "BRA", "BRA", Nervenstaerke.Stark),
        new(Mannschaftsart.Nationalmannschaft, "BUL", "BUL", Nervenstaerke.Schwach),
        new(Mannschaftsart.Nationalmannschaft, "CGO", "CGO", Nervenstaerke.Schwach),
        new(Mannschaftsart.Nationalmannschaft, "CHI", "CHI", Nervenstaerke.Stark),
        new(Mannschaftsart.Nationalmannschaft, "CHN", "CHN", Nervenstaerke.Schwach),
        new(Mannschaftsart.Nationalmannschaft, "COL", "COL", Nervenstaerke.Stark),
        new(Mannschaftsart.Nationalmannschaft, "COM", "COM", Nervenstaerke.Schwach),
        new(Mannschaftsart.Nationalmannschaft, "CRC", "CRC", Nervenstaerke.Schwach),
        new(Mannschaftsart.Nationalmannschaft, "CRO", "CRO", Nervenstaerke.Stark),
        new(Mannschaftsart.Nationalmannschaft, "CUW", "CUW", Nervenstaerke.Schwach),
        new(Mannschaftsart.Nationalmannschaft, "CYP", "CYP", Nervenstaerke.Schwach),
        new(Mannschaftsart.Nationalmannschaft, "CZE", "CZE", Nervenstaerke.Stark),
        new(Mannschaftsart.Nationalmannschaft, "ECU", "ECU", Nervenstaerke.Schwach),
        new(Mannschaftsart.Nationalmannschaft, "ENG", "ENG", Nervenstaerke.Stark),
        new(Mannschaftsart.Nationalmannschaft, "FRA", "FRA", Nervenstaerke.Stark),
        new(Mannschaftsart.Nationalmannschaft, "GUI", "GUI", Nervenstaerke.Stark),
        new(Mannschaftsart.Nationalmannschaft, "HAI", "HAI", Nervenstaerke.Schwach),
        new(Mannschaftsart.Nationalmannschaft, "IDN", "IDN", Nervenstaerke.Schwach),
        new(Mannschaftsart.Nationalmannschaft, "IND", "IND", Nervenstaerke.Schwach),
        new(Mannschaftsart.Nationalmannschaft, "ITA", "ITA", Nervenstaerke.Stark),
        new(Mannschaftsart.Nationalmannschaft, "JPN", "JPN", Nervenstaerke.Stark),
        new(Mannschaftsart.Nationalmannschaft, "LUX", "LUX", Nervenstaerke.Schwach),
        new(Mannschaftsart.Nationalmannschaft, "MAR", "MAR", Nervenstaerke.Stark),
        new(Mannschaftsart.Nationalmannschaft, "MEX", "MEX", Nervenstaerke.Stark),
        new(Mannschaftsart.Nationalmannschaft, "NED", "NED", Nervenstaerke.Stark),
        new(Mannschaftsart.Nationalmannschaft, "NGA", "NGA", Nervenstaerke.Stark),
        new(Mannschaftsart.Nationalmannschaft, "NIR", "NIR", Nervenstaerke.Schwach),
        new(Mannschaftsart.Nationalmannschaft, "NOR", "NOR", Nervenstaerke.Stark),
        new(Mannschaftsart.Nationalmannschaft, "PAN", "PAN", Nervenstaerke.Schwach),
        new(Mannschaftsart.Nationalmannschaft, "PAR", "PAR", Nervenstaerke.Stark),
        new(Mannschaftsart.Nationalmannschaft, "POL", "POL", Nervenstaerke.Schwach),
        new(Mannschaftsart.Nationalmannschaft, "POR", "POR", Nervenstaerke.Stark),
        new(Mannschaftsart.Nationalmannschaft, "RSA", "RSA", Nervenstaerke.Schwach),
        new(Mannschaftsart.Nationalmannschaft, "RUS", "RUS", Nervenstaerke.Schwach),
        new(Mannschaftsart.Nationalmannschaft, "SCO", "SCO", Nervenstaerke.Stark),
        new(Mannschaftsart.Nationalmannschaft, "SLE", "SLE", Nervenstaerke.Schwach),
        new(Mannschaftsart.Nationalmannschaft, "SWE", "SWE", Nervenstaerke.Stark),
        new(Mannschaftsart.Nationalmannschaft, "TOG", "TOG", Nervenstaerke.Schwach),
        new(Mannschaftsart.Nationalmannschaft, "URU", "URU", Nervenstaerke.Stark),
        new(Mannschaftsart.Nationalmannschaft, "ZIM", "ZIM", Nervenstaerke.Schwach),
    ];
}
