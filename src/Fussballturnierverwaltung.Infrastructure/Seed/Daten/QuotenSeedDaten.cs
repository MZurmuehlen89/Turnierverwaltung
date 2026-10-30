using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;

namespace Fussballturnierverwaltung.Infrastructure.Seed.Daten;

/// <summary>Verbandsquoten und Landes-Kontingente der Turniervorlagen (#574), je nach der letzten Ausgabe bzw. Saison
/// der Epoche. Eine Quote zählt nur garantierte Direktplätze: der Gastgeber bei seinem Verband, Play-off-Plätze und
/// Plätze gemeinsamer Zonen mehrerer Verbände (Asien/Ozeanien bis 1982) bleiben frei. Europapokale nach der
/// Zulassungsliste der Saison ohne die Umverteilung für Titelträger; Titelträger- und Qualifikationsplätze bleiben frei,
/// Pokalsieger-Plätze kommen mit #583. Welche Vereine ein Kontingent trifft, ergibt die Rangliste des Landes in der App
/// („letzte Saison“ = ihr Stand, Platz 1 = Meister). Bestehende Datenbanken:
/// <see cref="SeedVorlagenAbgleich.FehlendeQuoten"/> je Kontingent.</summary>
public static class QuotenSeedDaten
{
    /// <summary>Kontingent eines Landes: <paramref name="Anzahl"/> Vereine der <paramref name="Spielklasse"/> ab
    /// <paramref name="AbPlatz"/> — wie die App gespeichert, also immer mit Startplatz.</summary>
    public sealed record LandesKontingent(string FifaCode, int Anzahl, int Spielklasse, int AbPlatz = 1);

    public sealed record Quoten(IReadOnlyList<(string Verband, int Anzahl)> Verbaende, IReadOnlyList<LandesKontingent> Laender);

    private static Quoten Verbaende(params (string Verband, int Anzahl)[] verbaende) => new(verbaende, []);

    private static Quoten Laender(params LandesKontingent[] laender) => new([], laender);

    // Erste Liga ab Platz 1 — die Direktplätze der Champions League.
    private static LandesKontingent Spitze(string fifaCode, int anzahl) => new(fifaCode, anzahl, 1);

    public static IReadOnlyDictionary<(string Name, string Variante), Quoten> JeVorlage { get; } =
        new Dictionary<(string, string), Quoten>
        {
            // ── FIFA WM ─────────────────────────────────────────────────────────
            // 2026: 46 Direktplätze, die drei Gastgeber bei der CONCACAF; 2 Plätze über das Play-off-Turnier.
            [("FIFA WM", "48 Teams · ab 2026")] = Verbaende(
                ("UEFA", 16), ("CAF", 9), ("AFC", 8), ("CONMEBOL", 6), ("CONCACAF", 6), ("OFC", 1)),
            // 2022: Gastgeber Katar bei der AFC; 2 Plätze über Play-offs (AFC–CONMEBOL, CONCACAF–OFC).
            [("FIFA WM", "32 Teams · 1998–2022")] = Verbaende(
                ("UEFA", 13), ("CAF", 5), ("AFC", 5), ("CONMEBOL", 4), ("CONCACAF", 3)),
            // 1994: Titelverteidiger Deutschland bei der UEFA, Gastgeber USA bei der CONCACAF; 1 Platz über das
            // Play-off CONCACAF–OFC–CONMEBOL.
            [("FIFA WM", "24 Teams · 1986–1994")] = Verbaende(
                ("UEFA", 13), ("CONMEBOL", 3), ("CAF", 3), ("AFC", 2), ("CONCACAF", 2)),
            // 1982: Gastgeber Spanien bei der UEFA, Titelverteidiger Argentinien bei der CONMEBOL; 2 Plätze der
            // gemeinsamen Zone Asien/Ozeanien.
            [("FIFA WM", "24 Teams · 1982")] = Verbaende(
                ("UEFA", 14), ("CONMEBOL", 4), ("CAF", 2), ("CONCACAF", 2)),
            // 1978: Titelverteidiger BR Deutschland bei der UEFA, Gastgeber Argentinien bei der CONMEBOL; 1 Platz über
            // das Play-off UEFA–CONMEBOL, 1 Platz der gemeinsamen Zone Asien/Ozeanien.
            [("FIFA WM", "16 Teams · 1974–1978")] = Verbaende(
                ("UEFA", 9), ("CONMEBOL", 3), ("CONCACAF", 1), ("CAF", 1)),
            // 1970: Titelverteidiger England bei der UEFA, Gastgeber Mexiko bei der CONCACAF; 1 Platz der gemeinsamen
            // Zone Asien/Ozeanien.
            [("FIFA WM", "16 Teams · 1958–1970")] = Verbaende(
                ("UEFA", 9), ("CONMEBOL", 3), ("CONCACAF", 2), ("CAF", 1)),
            // 1938: Gastgeber Frankreich und Titelverteidiger Italien in Europa; Österreich qualifiziert, aber
            // zurückgezogen (der Platz blieb leer). Verbände nach heutiger Zuordnung.
            [("FIFA WM", TurnierVorlagenSeedDaten.WM_16_TEAMS_1934_BIS_1938)] = Verbaende(
                ("UEFA", 13), ("CONMEBOL", 1), ("CONCACAF", 1), ("AFC", 1)),

            // ── FIFA Klub-WM ────────────────────────────────────────────────────
            // 2025: Gastgeber-Platz (Inter Miami) bei der CONCACAF.
            [("FIFA Klub-WM", "32 Teams · ab 2025")] = Verbaende(
                ("UEFA", 12), ("CONMEBOL", 6), ("CONCACAF", 5), ("AFC", 4), ("CAF", 4), ("OFC", 1)),

            // ── Copa América ────────────────────────────────────────────────────
            // 2024: alle zehn CONMEBOL-Mitglieder und sechs CONCACAF-Gäste.
            [("Copa América", "16 Teams · 2016/2024")] = Verbaende(("CONMEBOL", 10), ("CONCACAF", 6)),
            // 2019: zehn CONMEBOL-Mitglieder und die Gäste Japan und Katar.
            [("Copa América", "12 Teams · 1993–2019")] = Verbaende(("CONMEBOL", 10), ("AFC", 2)),

            // ── UEFA Champions League ───────────────────────────────────────────
            // 2020/21: Verbände 1–4 vier, 5–6 zwei, 7–10 ein Direktplatz (Verbandsranking 2019).
            [("UEFA Champions League", "32 Teams · mit Auswärtstorregel · 2003–2021")] = Laender(
                Spitze("ESP", 4), Spitze("ENG", 4), Spitze("ITA", 4), Spitze("GER", 4),
                Spitze("FRA", 2), Spitze("RUS", 2),
                Spitze("POR", 1), Spitze("BEL", 1), Spitze("UKR", 1), Spitze("TUR", 1)),
            // 2023/24: wie 2020/21; Russland gesperrt, sein Platz ging an den Meister des Verbands 11 (Serbien).
            [("UEFA Champions League", "32 Teams · ohne Auswärtstorregel · 2021–2024")] = Laender(
                Spitze("ENG", 4), Spitze("ESP", 4), Spitze("ITA", 4), Spitze("GER", 4),
                Spitze("FRA", 2), Spitze("POR", 2),
                Spitze("NED", 1), Spitze("AUT", 1), Spitze("SCO", 1), Spitze("SRB", 1)),
            // 2025/26: Verbände 1–4 vier, 5 drei, 6 zwei, 7–10 ein Direktplatz; die Plätze für die europäische
            // Leistung (EPS) gingen an England und Spanien (je der Fünfte).
            [("UEFA Champions League", "36 Teams · ab 2024/25")] = Laender(
                Spitze("ENG", 5), Spitze("ITA", 4), Spitze("ESP", 5), Spitze("GER", 4),
                Spitze("FRA", 3), Spitze("NED", 2),
                Spitze("POR", 1), Spitze("BEL", 1), Spitze("TUR", 1), Spitze("CZE", 1)),

            // ── UEFA Europa League ──────────────────────────────────────────────
            // 2025/26: der Fünfte der Verbände 1–5; in England und Spanien rückte der Sechste nach, weil der Fünfte
            // über die EPS in der Champions League spielte. Die Pokalsieger der Verbände 1–7 kommen mit #583.
            // Die Conference League hat keine Direktplätze in der Ligaphase.
            [("UEFA Europa League", "36 Teams · ab 2024/25")] = Laender(
                new LandesKontingent("ENG", 1, 1, 6), new LandesKontingent("ITA", 1, 1, 5), new LandesKontingent("ESP", 1, 1, 6), new LandesKontingent("GER", 1, 1, 5), new LandesKontingent("FRA", 1, 1, 5)),

            // ── Deutschland ─────────────────────────────────────────────────────
            [("Bundesliga", "18 Teams")] = Laender(new LandesKontingent("GER", 18, 1)),
            [("2. Bundesliga", "18 Teams")] = Laender(new LandesKontingent("GER", 18, 2)),
            // Die 24 Amateurplätze (Landespokalsieger) bleiben frei und füllen sich nach der Ligapyramide.
            [("DFB-Pokal", "64 Teams")] = Laender(new LandesKontingent("GER", 18, 1), new LandesKontingent("GER", 18, 2), new LandesKontingent("GER", 4, 3)),
        };

    /// <summary>Hängt die Quoten des Seeds an die Vorlage. Verbände oder Länder, die der Lookup nicht kennt (oder ein
    /// fehlender Lookup), fallen weg.</summary>
    public static void Setzen(
        TurnierInstanz vorlage, IReadOnlyDictionary<string, int>? verbandLookup, IReadOnlyDictionary<string, int>? landLookup)
    {
        if (!JeVorlage.TryGetValue((vorlage.Name, vorlage.Variante), out var quoten)) return;

        foreach (var (verband, anzahl) in quoten.Verbaende)
            if (verbandLookup is not null && verbandLookup.TryGetValue(verband, out var verbandId))
                vorlage.VerbandsQuoten.Add(new VerbandsQuote { VerbandId = verbandId, Anzahl = anzahl });

        foreach (var kontingent in quoten.Laender)
            if (landLookup is not null && landLookup.TryGetValue(kontingent.FifaCode, out var landId))
                vorlage.LandesQuoten.Add(new LandesQuote
                {
                    LandId = landId, Anzahl = kontingent.Anzahl, Spielklasse = kontingent.Spielklasse, AbPlatz = kontingent.AbPlatz,
                });
    }
}
