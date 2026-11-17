using Fussballturnierverwaltung.Application.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Infrastructure.Seed.Daten;

/// <summary>
/// Spielweise der Seed-Mannschaften (#667): Die Grundausrichtung kommt aus den Ratings
/// (<see cref="SpielweisenVorschlag"/>), die Flexibilität ist „normal“. Die Handliste überschreibt beides für Teams
/// mit bekannter Spielweise, wo die Ratings sie nicht treffen (Stand 2025/26, mit dem Nutzer abgestimmt 17.11.2026).
/// Schlüssel ist (Mannschaftsart, Kurzname, Land) – ein eigener Verein mit gleichem Kurznamen in einem anderen Land
/// bleibt unberührt (Befund aus dem KI-Review).
/// </summary>
public static class SpielweiseSeedDaten
{
    /// <param name="Grundausrichtung"><c>null</c>: der Rating-Vorschlag gilt, nur die Flexibilität weicht ab.</param>
    public sealed record Eintrag(
        Mannschaftsart Art, string Kurzname, string Land, Grundausrichtung? Grundausrichtung,
        Flexibilitaet Flexibilitaet = Flexibilitaet.Normal);

    private const Mannschaftsart VEREIN = Mannschaftsart.Verein;
    private const Mannschaftsart NATIONAL = Mannschaftsart.Nationalmannschaft;

    public static readonly IReadOnlyList<Eintrag> Handliste =
    [
        // ── Vereine ───────────────────────────────────────────────────────
        new(VEREIN, "BAR", "ESP", Grundausrichtung.SehrOffensiv, Flexibilitaet.Starr),   // FC Barcelona
        new(VEREIN, "FCB", "GER", Grundausrichtung.Offensiv),                            // Bayern München
        new(VEREIN, "MCI", "ENG", Grundausrichtung.Offensiv, Flexibilitaet.Starr),       // Manchester City
        new(VEREIN, "LIV", "ENG", Grundausrichtung.Offensiv),                            // FC Liverpool
        new(VEREIN, "B04", "GER", Grundausrichtung.Offensiv),                            // Bayer 04 Leverkusen
        new(VEREIN, "RMA", "ESP", Grundausrichtung.Offensiv, Flexibilitaet.Flexibel),    // Real Madrid
        new(VEREIN, "INT", "ITA", Grundausrichtung.Ausgeglichen, Flexibilitaet.Flexibel),// Inter Mailand
        new(VEREIN, "ATM", "ESP", null, Flexibilitaet.Flexibel),                         // Atlético Madrid
        new(VEREIN, "GET", "ESP", null, Flexibilitaet.Starr),                            // Getafe CF
        // ── Nationalmannschaften ──────────────────────────────────────────
        new(NATIONAL, "ESP", "ESP", Grundausrichtung.Offensiv),
        new(NATIONAL, "GER", "GER", Grundausrichtung.Offensiv),
        new(NATIONAL, "ARG", "ARG", Grundausrichtung.Offensiv, Flexibilitaet.Flexibel),
        new(NATIONAL, "POR", "POR", Grundausrichtung.Offensiv),
        new(NATIONAL, "FRA", "FRA", null, Flexibilitaet.Flexibel),
        new(NATIONAL, "ITA", "ITA", Grundausrichtung.Defensiv),
        new(NATIONAL, "URU", "URU", Grundausrichtung.Offensiv),
        new(NATIONAL, "SUI", "SUI", Grundausrichtung.Ausgeglichen),
        new(NATIONAL, "CRO", "CRO", Grundausrichtung.Ausgeglichen),
        new(NATIONAL, "JPN", "JPN", Grundausrichtung.Ausgeglichen),
        new(NATIONAL, "IRN", "IRN", Grundausrichtung.SehrDefensiv),
    ];

    private static readonly Dictionary<(Mannschaftsart, string, string), Eintrag> _nachSchluessel =
        Handliste.ToDictionary(e => (e.Art, e.Kurzname, e.Land));

    /// <summary>Setzt Grundausrichtung und Flexibilität einer neu angelegten Seed-Mannschaft.</summary>
    /// <param name="land">FIFA-Code ihres Landes.</param>
    public static Mannschaft MitSpielweise(this Mannschaft mannschaft, string land)
    {
        _nachSchluessel.TryGetValue((mannschaft.Mannschaftsart, mannschaft.Kurzname, land), out var eintrag);
        mannschaft.Grundausrichtung = eintrag?.Grundausrichtung
            ?? SpielweisenVorschlag.AusRatings(mannschaft.OffensivRating, mannschaft.DefensivRating);
        mannschaft.Flexibilitaet = eintrag?.Flexibilitaet ?? Flexibilitaet.Normal;
        return mannschaft;
    }
}
