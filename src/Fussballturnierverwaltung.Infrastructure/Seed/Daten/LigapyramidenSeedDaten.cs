namespace Fussballturnierverwaltung.Infrastructure.Seed.Daten;

/// <summary>Ligapyramiden der Länder als feste Seed-Stände — je Land eine Rangliste mit einem Abschnitt je Spielklasse,
/// am Land eingetragen (#572). Der Platz eines Vereins ist seine Position in den Seed-Daten. Grundlage für „Heimrecht
/// für Unterklassige“ im DFB-Pokal (#393) und für die Landes-Kontingente der Vorlagen (#574, „Platz 1 = Meister“).</summary>
public static class LigapyramidenSeedDaten
{
    public const string RANGLISTE_DEUTSCHLAND = "Ligapyramide Deutschland 2025/26";
    public const string RANGLISTE_ENGLAND = "Ligapyramide England 2024/25";

    /// <summary>Ein Abschnitt einer Pyramide; <c>null</c> in <paramref name="Vereine"/> ist ein Platz, dessen Verein
    /// nicht im Seed steht — er bleibt eine Lücke, damit die übrigen ihren echten Platz behalten.</summary>
    public sealed record Abschnitt(string Name, int Spielklasse, string?[] Vereine);

    /// <summary><paramref name="Gastlaender"/>: Länder, deren Vereine in dieser Liga mitspielen (Kanadier in der MLS,
    /// Neuseeländer in der A-League, #582) — ihre Vereine stehen im Seed unter ihrem eigenen Land.</summary>
    public sealed record Pyramide(
        string Land, string Name, DateOnly Datum, string QuelleText, int? Profigrenze, Abschnitt[] Abschnitte,
        string[]? Gastlaender = null)
    {
        /// <summary>Länder, in denen die Vereine der Pyramide gesucht werden: das eigene zuerst, dann die Gastländer.</summary>
        public IEnumerable<string> VereinsLaender => [Land, .. Gastlaender ?? []];

        /// <summary>Der nationale Pokalsieger zum Stand der Pyramide (#583), <c>null</c> ohne Eintrag.</summary>
        public string? Pokalsieger => _pokalsiegerJeRangliste.GetValueOrDefault(Name);
    }

    // ── Pokalsieger (#583) ───────────────────────────────────────────────────

    // Der Pokalsieger der Saison, auf die sich die Pyramide bezieht (2024 bzw. 2024/25; Deutschland: der Titelverteidiger
    // in die Saison 2025/26). Er muss im Vereins-Seed stehen und darf aus jeder Spielklasse kommen. Ohne Eintrag:
    // Österreich (Wolfsberger AC fehlt im Seed), Mexiko (kein Pokal), Marokko (Quellen zum Throne Cup 2024/25 uneins).
    private static readonly Dictionary<string, string> _pokalsiegerJeRangliste = new()
    {
        [RANGLISTE_DEUTSCHLAND] = "VfB Stuttgart",
        [RANGLISTE_ENGLAND] = "Crystal Palace",
        ["Ligapyramide Spanien 2024/25"] = "FC Barcelona",
        ["Ligapyramide Italien 2024/25"] = "Bologna FC",
        ["Ligapyramide Frankreich 2024/25"] = "Paris Saint-Germain",
        ["Ligapyramide Niederlande 2024/25"] = "Go Ahead Eagles",
        ["Ligapyramide Portugal 2024/25"] = "Sporting CP",
        ["Ligapyramide Belgien 2024/25"] = "Club Brugge KV",
        ["Ligapyramide Türkei 2024/25"] = "Galatasaray SK",
        ["Ligapyramide Tschechien 2024/25"] = "Sigma Olmütz",
        ["Ligapyramide Schottland 2024/25"] = "Aberdeen FC",
        ["Ligapyramide Serbien 2024/25"] = "Roter Stern Belgrad",
        ["Ligapyramide Ukraine 2024/25"] = "Schachtar Donezk",
        ["Ligapyramide Russland 2024/25"] = "CSKA Moskau",
        ["Ligapyramide Brasilien 2024"] = "Flamengo",
        ["Ligapyramide Argentinien 2024"] = "Central Córdoba Santiago del Estero",
        ["Ligapyramide USA 2024"] = "Los Angeles FC",
        ["Ligapyramide Saudi-Arabien 2024/25"] = "Al-Ittihad",
        ["Ligapyramide Japan 2024"] = "Vissel Kobe",
        ["Ligapyramide Südkorea 2024"] = "Pohang Steelers",
        ["Ligapyramide Vereinigte Arabische Emirate 2024/25"] = "Shabab Al-Ahli",
        ["Ligapyramide Ägypten 2024/25"] = "Zamalek SC",
        ["Ligapyramide Tunesien 2024/25"] = "Espérance Tunis",
        ["Ligapyramide Südafrika 2024/25"] = "Kaizer Chiefs",
        ["Ligapyramide Neuseeland 2024"] = "Wellington Olympic",
        ["Ligapyramide Australien 2024/25"] = "Macarthur FC",
    };

    public static IReadOnlyList<Pyramide> Alle { get; } = [Deutschland(), England(), Spanien(), Italien(), Frankreich(), .. ErsteLigen2024_25(),
        .. LigenAusserhalbEuropas()];

    // ── Deutschland ─────────────────────────────────────────────────────────

    // Spielklassen der Saison 2025/26 aus den geseedeten deutschen Vereinen (#393); innerhalb einer Liga ist die
    // Reihenfolge ohne Bedeutung. Reserveteams fehlen wie im Vereins-Seed (#351). Bundesliga und 2. Bundesliga sind
    // Profiligen — unterhalb davon haben Vereine im DFB-Pokal Heimrecht.
    private static Pyramide Deutschland() => new(
        "GER", RANGLISTE_DEUTSCHLAND, new DateOnly(2025, 7, 1), "Spielklassen der Saison 2025/26", Profigrenze: 2,
        [
            new("Bundesliga", 1,
            [
                "Bayern München", "Bayer 04 Leverkusen", "Borussia Dortmund", "RB Leipzig", "Eintracht Frankfurt",
                "VfB Stuttgart", "TSG 1899 Hoffenheim", "SC Freiburg", "1. FC Union Berlin", "SV Werder Bremen",
                "VfL Wolfsburg", "Borussia Mönchengladbach", "1. FSV Mainz 05", "FC Augsburg", "FC St. Pauli",
                "1. FC Heidenheim", "Hamburger SV", "1. FC Köln"
            ]),
            new("2. Bundesliga", 2,
            [
                "VfL Bochum", "Holstein Kiel", "Schalke 04", "Hannover 96", "1. FC Nürnberg", "Fortuna Düsseldorf",
                "Karlsruher SC", "SV Darmstadt 98", "Hertha BSC Berlin", "SpVgg Greuther Fürth", "1. FC Kaiserslautern",
                "SC Paderborn 07", "1. FC Magdeburg", "SV Elversberg", "Preußen Münster", "Eintracht Braunschweig",
                "Arminia Bielefeld", "Dynamo Dresden"
            ]),
            new("3. Liga", 3,
            [
                "SSV Ulm 1846", "SV Wehen Wiesbaden", "Jahn Regensburg", "1. FC Saarbrücken", "FC Energie Cottbus",
                "FC Hansa Rostock", "TSV 1860 München", "FC Ingolstadt 04", "MSV Duisburg", "VfL Osnabrück",
                "Rot-Weiss Essen", "Alemannia Aachen", "SV Waldhof Mannheim", "SC Verl", "FC Erzgebirge Aue",
                "FC Viktoria Köln", "TSV Havelse", "1. FC Schweinfurt 05"
            ]),
            new("Regionalliga Nordost", 4, ["1. FC Lokomotive Leipzig", "FC Carl Zeiss Jena", "Hallescher FC", "Chemnitzer FC"]),
            new("Regionalliga Südwest", 4, ["Kickers Offenbach", "SV Sandhausen"]),
            new("Regionalliga Bayern", 4, ["SpVgg Unterhaching", "Würzburger Kickers"]),
            new("Regionalliga West", 4, ["Rot-Weiß Oberhausen", "Wuppertaler SV"]),
        ]);

    // ── Erste Ligen weiterer UEFA-Länder (#580) ──────────────────────────────

    // Abschlusstabellen 2024/25 — über sie wurden die Plätze der Europapokale 2025/26 vergeben. Ligen mit Meister- und
    // Abstiegsrunde nach ihrer Endplatzierung. Nur Vereine, die der Seed kennt (Nutzerentscheid #580).
    private const string QUELLE_2024_25 = "Abschlusstabellen 2024/25 (englische Wikipedia), abgerufen am 01.11.2026";
    private static readonly DateOnly _saisonende2024_25 = new(2025, 6, 1);

    private static IEnumerable<Pyramide> ErsteLigen2024_25()
    {
        yield return ErsteLiga("NED", "Niederlande", "Eredivisie", 18,
            (1, "PSV Eindhoven"), (2, "Ajax Amsterdam"), (3, "Feyenoord Rotterdam"), (4, "FC Utrecht"),
            (5, "AZ Alkmaar"), (6, "FC Twente Enschede"), (7, "Go Ahead Eagles"));
        yield return ErsteLiga("POR", "Portugal", "Primeira Liga", 18,
            (1, "Sporting CP"), (2, "SL Benfica"), (3, "FC Porto"), (4, "SC Braga"), (5, "CD Santa Clara"),
            (6, "Vitória SC"), (12, "FC Arouca"), (13, "Gil Vicente FC"));
        yield return ErsteLiga("BEL", "Belgien", "Jupiler Pro League", 16,
            (1, "Union Saint-Gilloise"), (2, "Club Brugge KV"), (3, "KRC Genk"), (4, "RSC Anderlecht"),
            (5, "Royal Antwerp FC"), (6, "KAA Gent"), (7, "Sporting Charleroi"), (14, "Cercle Brügge"));
        yield return ErsteLiga("TUR", "Türkei", "Süper Lig", 19,
            (1, "Galatasaray SK"), (2, "Fenerbahçe SK"), (3, "Samsunspor"), (4, "Beşiktaş JK"),
            (5, "İstanbul Başakşehir"), (7, "Trabzonspor"), (11, "Konyaspor"), (17, "Sivasspor"), (19, "Adana Demirspor"));
        yield return ErsteLiga("CZE", "Tschechien", "Chance Liga", 16,
            (1, "Slavia Prag"), (2, "Viktoria Pilsen"), (3, "Baník Ostrava"), (4, "Sparta Prag"), (5, "FK Jablonec"),
            (6, "Sigma Olmütz"), (8, "Bohemians Prag 1905"), (12, "FK Mladá Boleslav"), (13, "1. FC Slovácko"));
        yield return ErsteLiga("AUT", "Österreich", "Bundesliga", 12,
            (1, "SK Sturm Graz"), (2, "Red Bull Salzburg"), (3, "FK Austria Wien"), (5, "SK Rapid Wien"), (7, "LASK"));
        yield return ErsteLiga("SCO", "Schottland", "Premiership", 12,
            (1, "Celtic FC"), (2, "Rangers FC"), (3, "Hibernian FC"), (4, "Dundee United"), (5, "Aberdeen FC"),
            (7, "Heart of Midlothian"));
        yield return ErsteLiga("SRB", "Serbien", "Superliga", 16,
            (1, "Roter Stern Belgrad"), (2, "Partizan Belgrad"));
        yield return ErsteLiga("UKR", "Ukraine", "Premjer-Liha", 16,
            (1, "Dynamo Kyiv"), (3, "Schachtar Donezk"), (4, "Polissja Schytomyr"), (7, "Sorja Luhansk"),
            (13, "Vorskla Poltawa"));
        yield return ErsteLiga("RUS", "Russland", "Premjer-Liga", 16,
            (1, "FK Krasnodar"), (2, "Zenit St. Petersburg"), (3, "CSKA Moskau"), (4, "Spartak Moskau"),
            (6, "Lokomotive Moskau"));
    }

    private static Pyramide ErsteLiga(
        string land, string landName, string liga, int plaetze, params (int Platz, string Verein)[] vereine) =>
        Pyramide2024_25(land, $"Ligapyramide {landName} 2024/25", profigrenze: null,
            new Abschnitt(liga, 1, Tabelle(plaetze, vereine)));

    private static Pyramide Pyramide2024_25(string land, string name, int? profigrenze, params Abschnitt[] abschnitte) =>
        new(land, name, _saisonende2024_25, QUELLE_2024_25, profigrenze, abschnitte);

    private static string?[] Tabelle(int plaetze, params (int Platz, string Verein)[] vereine)
    {
        var tabelle = new string?[plaetze];
        foreach (var (platz, verein) in vereine)
            tabelle[platz - 1] = verein;
        return tabelle;
    }

    // ── England, Spanien, Italien, Frankreich (#575) ─────────────────────────

    // Abschlusstabellen 2024/25 der Profiligen, je Liga in Tabellenreihenfolge. Die zweiten Ligen von Spanien, Italien
    // und Frankreich kamen mit #575 in den Seed; in England fehlen vier Absteiger der League One und die League Two
    // außer ihren Aufsteigern — sie bleiben Lücken. Der FA Cup hat „Heimrecht nach Los“, die Profigrenze wirkt dort nicht.
    private static Pyramide England() => Pyramide2024_25("ENG", RANGLISTE_ENGLAND, profigrenze: 4,
        new("Premier League", 1,
        [
            "FC Liverpool", "Arsenal FC", "Manchester City", "Chelsea FC", "Newcastle United", "Aston Villa",
            "Nottingham Forest", "Brighton & Hove Albion", "AFC Bournemouth", "Brentford FC", "Fulham FC",
            "Crystal Palace", "Everton FC", "West Ham United", "Manchester United", "Wolverhampton Wanderers",
            "Tottenham Hotspur", "Leicester City", "Ipswich Town", "Southampton FC"
        ]),
        new("Championship", 2,
        [
            "Leeds United", "Burnley FC", "Sheffield United", "Sunderland AFC", "Coventry City", "Bristol City",
            "Blackburn Rovers", "Millwall FC", "West Bromwich Albion", "Middlesbrough FC", "Swansea City",
            "Sheffield Wednesday", "Norwich City", "Watford FC", "Queens Park Rangers", "Portsmouth FC",
            "Oxford United", "Stoke City", "Derby County", "Preston North End", "Hull City", "Luton Town",
            "Plymouth Argyle", "Cardiff City"
        ]),
        new("League One", 3,
        [
            "Birmingham City", "Wrexham AFC", "Stockport County", "Charlton Athletic", "Wycombe Wanderers",
            "Leyton Orient", "Reading FC", "Bolton Wanderers", "Blackpool FC", "Huddersfield Town", "Lincoln City",
            "Barnsley FC", "Rotherham United", "Stevenage FC", "Wigan Athletic", "Exeter City", "Mansfield Town",
            "Peterborough United", "Northampton Town", "Burton Albion", null, null, null, null
        ]),
        new("League Two", 4,
            Tabelle(24, (1, "Doncaster Rovers"), (2, "Port Vale"), (3, "Bradford City"), (5, "AFC Wimbledon"))));

    private static Pyramide Spanien() => Pyramide2024_25("ESP", "Ligapyramide Spanien 2024/25", profigrenze: 2,
        new("La Liga", 1,
        [
            "FC Barcelona", "Real Madrid", "Atlético Madrid", "Athletic Club Bilbao", "Villarreal CF", "Real Betis",
            "Celta Vigo", "Rayo Vallecano", "CA Osasuna", "RCD Mallorca", "Real Sociedad", "Valencia CF",
            "Getafe CF", "RCD Espanyol", "Deportivo Alavés", "Girona FC", "Sevilla FC", "CD Leganés",
            "UD Las Palmas", "Real Valladolid"
        ]),
        new("Segunda División", 2,
        [
            "Levante UD", "Elche CF", "Real Oviedo", "CD Mirandés", "Racing Santander", "UD Almería", "Granada CF",
            "SD Huesca", "SD Eibar", "Albacete Balompié", "Sporting Gijón", "Burgos CF", "Cádiz CF", "Córdoba CF",
            "Deportivo La Coruña", "Málaga CF", "CD Castellón", "Real Zaragoza", "CD Eldense", "CD Tenerife",
            "Racing Ferrol", "FC Cartagena"
        ]));

    private static Pyramide Italien() => Pyramide2024_25("ITA", "Ligapyramide Italien 2024/25", profigrenze: 2,
        new("Serie A", 1,
        [
            "SSC Napoli", "Inter Mailand", "Atalanta BC", "Juventus Turin", "AS Roma", "ACF Fiorentina", "SS Lazio",
            "AC Mailand", "Bologna FC", "Como 1907", "Torino FC", "Udinese Calcio", "Genoa CFC", "Hellas Verona",
            "Cagliari Calcio", "Parma Calcio 1913", "US Lecce", "Empoli FC", "Venezia FC", "AC Monza"
        ]),
        new("Serie B", 2,
        [
            "US Sassuolo", "Pisa SC", "Spezia Calcio", "US Cremonese", "SS Juve Stabia", "US Catanzaro", "Cesena FC",
            "Palermo FC", "SSC Bari", "FC Südtirol", "Modena FC", "Carrarese Calcio", "Mantova 1911", "AC Reggiana",
            "Frosinone Calcio", "US Salernitana", "UC Sampdoria", "Brescia Calcio", "AS Cittadella", "Cosenza Calcio"
        ]));

    private static Pyramide Frankreich() => Pyramide2024_25("FRA", "Ligapyramide Frankreich 2024/25", profigrenze: 2,
        new("Ligue 1", 1,
        [
            "Paris Saint-Germain", "Olympique de Marseille", "AS Monaco", "OGC Nice", "LOSC Lille",
            "Olympique Lyonnais", "RC Strasbourg", "RC Lens", "Stade Brestois 29", "Toulouse FC", "AJ Auxerre",
            "Stade Rennais", "FC Nantes", "Angers SCO", "Le Havre AC", "Stade de Reims", "AS Saint-Étienne",
            "Montpellier HSC"
        ]),
        new("Ligue 2", 2,
        [
            "FC Lorient", "Paris FC", "FC Metz", "USL Dunkerque", "EA Guingamp", "FC Annecy", "Stade Lavallois",
            "SC Bastia", "Grenoble Foot 38", "ES Troyes AC", "Amiens SC", "AC Ajaccio", "Pau FC", "Rodez AF",
            "Red Star FC", "Clermont Foot", "FC Martigues", "SM Caen"
        ]));

    // ── Oberste Ligen außerhalb Europas (#582) ───────────────────────────────

    // Die Länder der Klub-WM-Teilnehmer 2025 außerhalb Europas und Australien. Stand ist die Saison, über die die
    // Kontinentalplätze 2025/26 vergeben wurden: Kalenderjahr 2024 bzw. 2024/25. Mexiko: Gesamttabelle aus Apertura 2024
    // und Clausura 2025; MLS: Gesamttabelle (Supporters' Shield); Ägypten: Meisterrunde vor Abstiegsrunde; Neuseeland:
    // Meisterschaftsrunde ohne Play-offs. Reserveteams (Wellington Phoenix Reserves) bleiben Lücken wie in Deutschland.
    private const string QUELLE_AUSSEREUROPA =
        "Abschlusstabellen 2024 bzw. 2024/25 (englische Wikipedia), abgerufen am 03.11.2026";
    private static readonly DateOnly _saisonende2024 = new(2025, 1, 1);

    private static IEnumerable<Pyramide> LigenAusserhalbEuropas()
    {
        yield return Kalenderjahr2024("BRA", "Brasilien", "Série A",
            ["Botafogo", "SE Palmeiras", "Flamengo", "Fortaleza EC", "SC Internacional", "São Paulo FC",
             "SC Corinthians", "EC Bahia", "Cruzeiro EC", "Vasco da Gama", "EC Vitória", "Atlético Mineiro",
             "Fluminense FC", "Grêmio Porto Alegre", "EC Juventude", "RB Bragantino", "Athletico Paranaense",
             "Criciúma EC", "Atlético Goianiense", "Cuiabá EC"]);
        yield return Kalenderjahr2024("ARG", "Argentinien", "Liga Profesional",
            ["Vélez Sarsfield", "Talleres Córdoba", "Racing Club", "CA Huracán", "River Plate", "Boca Juniors",
             "CA Independiente", "Atlético Tucumán", "Unión de Santa Fe", "CA Platense", "Independiente Rivadavia",
             "Estudiantes de La Plata", "Instituto Córdoba", "CA Lanús", "Godoy Cruz", "CA Belgrano",
             "Deportivo Riestra", "CA Tigre", "Gimnasia y Esgrima La Plata", "Rosario Central", "Defensa y Justicia",
             "Central Córdoba Santiago del Estero", "Argentinos Juniors", "San Lorenzo", "Newell's Old Boys",
             "Sarmiento Junín", "CA Banfield", "Barracas Central"]);
        yield return Saison2024_25("MEX", "Mexiko", "Liga MX",
            ["Cruz Azul", "Deportivo Toluca", "Tigres UANL", "Club América", "CF Monterrey", "Pumas UNAM",
             "Club León", "Atlético San Luis", "Club Tijuana", "Chivas Guadalajara", "Club Necaxa", "CF Pachuca",
             "FC Juárez", "Atlas FC", "Querétaro FC", "Mazatlán FC", "Club Puebla", "Santos Laguna"]);
        yield return Kalenderjahr2024("USA", "USA", "Major League Soccer",
            ["Inter Miami CF", "Columbus Crew", "Los Angeles FC", "LA Galaxy", "FC Cincinnati", "Real Salt Lake",
             "Seattle Sounders FC", "Houston Dynamo", "Orlando City", "Minnesota United", "Charlotte FC",
             "Colorado Rapids", "New York City FC", "Vancouver Whitecaps", "Portland Timbers", "New York Red Bulls",
             "CF Montréal", "Austin FC", "FC Dallas", "Atlanta United", "D.C. United", "Toronto FC",
             "Philadelphia Union", "St. Louis City SC", "Nashville SC", "New England Revolution",
             "Sporting Kansas City", "Chicago Fire", "San Jose Earthquakes"], gastlaender: ["CAN"]);
        yield return Saison2024_25("KSA", "Saudi-Arabien", "Saudi Pro League",
            ["Al-Ittihad", "Al-Hilal", "Al-Nassr", "Al-Qadsiah", "Al-Ahli", "Al-Shabab", "Al-Ettifaq", "Al-Taawoun",
             "Al-Kholood", "Al-Fateh", "Al-Riyadh", "Al-Khaleej", "Al-Fayha", "Damac FC", "Al-Okhdood", "Al-Wehda",
             "Al-Orobah", "Al-Raed"]);
        yield return Kalenderjahr2024("JPN", "Japan", "J1 League",
            ["Vissel Kobe", "Sanfrecce Hiroshima", "FC Machida Zelvia", "Gamba Osaka", "Kashima Antlers",
             "Tokyo Verdy", "FC Tokyo", "Kawasaki Frontale", "Yokohama F. Marinos", "Cerezo Osaka", "Nagoya Grampus",
             "Avispa Fukuoka", "Urawa Red Diamonds", "Kyoto Sanga", "Shonan Bellmare", "Albirex Niigata",
             "Kashiwa Reysol", "Júbilo Iwata", "Consadole Sapporo", "Sagan Tosu"]);
        yield return Kalenderjahr2024("KOR", "Südkorea", "K League 1",
            ["Ulsan HD FC", "Gangwon FC", "Gimcheon Sangmu", "FC Seoul", "Suwon FC", "Pohang Steelers",
             "Jeju United", "Daejeon Hana Citizen", "Gwangju FC", "Jeonbuk Hyundai Motors", "Daegu FC",
             "Incheon United"]);
        yield return Saison2024_25("UAE", "Vereinigte Arabische Emirate", "UAE Pro League",
            ["Shabab Al-Ahli", "Sharjah FC", "Al-Wahda", "Al-Wasl", "Al-Ain", "Al-Nasr Dubai", "Al-Jazira",
             "Khor Fakkan", "Ittihad Kalba", "Ajman Club", "Al-Bataeh", "Baniyas SC", "Dibba Al-Hisn", "Al-Urooba"]);
        yield return Saison2024_25("EGY", "Ägypten", "Egyptian Premier League",
            ["Al Ahly SC", "Pyramids FC", "Zamalek SC", "Al Masry", "National Bank of Egypt", "Ceramica Cleopatra",
             "Pharco FC", "Petrojet", "Haras El Hodoud", "ZED FC", "El Gouna FC", "Tala'ea El Gaish", "ENPPI",
             "Modern Sport", "Al Ittihad Alexandria", "Smouha SC", "Ismaily SC", "Ghazl El Mahalla"]);
        yield return Saison2024_25("MAR", "Marokko", "Botola Pro",
            ["RS Berkane", "FAR Rabat", "Wydad AC", "FUS Rabat", "Raja CA Casablanca", "RCA Zemamra", "MAS Fès",
             "Olympic Safi", "Difaâ El Jadidi", "IR Tanger", "COD Meknès", "Union de Touarga", "Hassania Agadir",
             "JS Soualem", "MA Tétouan", "SCC Mohammédia"]);
        yield return Saison2024_25("TUN", "Tunesien", "Ligue Professionnelle 1",
            ["Espérance Tunis", "US Monastir", "Étoile du Sahel", "Club Africain", "ES Zarzis", "Stade Tunisien",
             "CS Sfaxien", "ES Métlaoui", "CA Bizertin", "AS Soliman", "US Ben Guerdane", "Olympique Béja",
             "AS Gabès", "JS El Omrane", "EGS Gafsa", "US Tataouine"]);
        yield return Saison2024_25("RSA", "Südafrika", "Premiership",
            ["Mamelodi Sundowns", "Orlando Pirates", "Stellenbosch FC", "Sekhukhune United", "TS Galaxy",
             "AmaZulu FC", "Polokwane City", "Richards Bay FC", "Kaizer Chiefs", "Marumo Gallants", "Chippa United",
             "Golden Arrows", "Magesi FC", "SuperSport United", "Cape Town City", "Royal AM"]);
        yield return Kalenderjahr2024("NZL", "Neuseeland", "National League",
            ["Birkenhead United", "Auckland City", "Western Springs", "Napier City Rovers", null,
             "Wellington Olympic", "Coastal Spirit", "Cashmere Technical", "Eastern Suburbs", "Western Suburbs"]);
        yield return Saison2024_25("AUS", "Australien", "A-League Men",
            ["Auckland FC", "Melbourne City FC", "Western United", "Western Sydney Wanderers", "Melbourne Victory",
             "Adelaide United", "Sydney FC", "Macarthur FC", "Newcastle Jets", "Central Coast Mariners",
             "Wellington Phoenix", "Brisbane Roar", "Perth Glory"], gastlaender: ["NZL"]);
    }

    private static Pyramide Kalenderjahr2024(
        string land, string landName, string liga, string?[] tabelle, string[]? gastlaender = null) =>
        new(land, $"Ligapyramide {landName} 2024", _saisonende2024, QUELLE_AUSSEREUROPA, null,
            [new(liga, 1, tabelle)], gastlaender);

    private static Pyramide Saison2024_25(
        string land, string landName, string liga, string?[] tabelle, string[]? gastlaender = null) =>
        new(land, $"Ligapyramide {landName} 2024/25", _saisonende2024_25, QUELLE_AUSSEREUROPA, null,
            [new(liga, 1, tabelle)], gastlaender);
}
