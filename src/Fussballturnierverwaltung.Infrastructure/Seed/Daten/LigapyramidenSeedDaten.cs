namespace Fussballturnierverwaltung.Infrastructure.Seed.Daten;

/// <summary>Ligapyramiden der Länder als feste Seed-Stände — je Land eine Rangliste mit einem Abschnitt je Spielklasse,
/// am Land eingetragen (#572). Der Platz eines Vereins ist seine Position in den Seed-Daten. Grundlage für „Heimrecht
/// für Unterklassige“ im DFB-Pokal (#393) und für die Landes-Kontingente der Vorlagen (#574, „Platz 1 = Meister“).</summary>
public static class LigapyramidenSeedDaten
{
    public const string RANGLISTE_DEUTSCHLAND = "Ligapyramide Deutschland 2025/26";

    /// <summary>Ein Abschnitt einer Pyramide; <c>null</c> in <paramref name="Vereine"/> ist ein Platz, dessen Verein
    /// nicht im Seed steht — er bleibt eine Lücke, damit die übrigen ihren echten Platz behalten.</summary>
    public sealed record Abschnitt(string Name, int Spielklasse, string?[] Vereine);

    public sealed record Pyramide(
        string Land, string Name, DateOnly Datum, string QuelleText, int? Profigrenze, Abschnitt[] Abschnitte);

    public static IReadOnlyList<Pyramide> Alle { get; } = [Deutschland(), .. ErsteLigen2024_25()];

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
        string land, string landName, string liga, int plaetze, params (int Platz, string Verein)[] vereine)
    {
        var tabelle = new string?[plaetze];
        foreach (var (platz, verein) in vereine)
            tabelle[platz - 1] = verein;
        return new Pyramide(land, $"Ligapyramide {landName} 2024/25", _saisonende2024_25, QUELLE_2024_25, Profigrenze: null,
            [new Abschnitt(liga, 1, tabelle)]);
    }
}
