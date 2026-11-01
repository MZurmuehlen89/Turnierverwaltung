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

    public static IReadOnlyList<Pyramide> Alle { get; } = [Deutschland()];

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
}
