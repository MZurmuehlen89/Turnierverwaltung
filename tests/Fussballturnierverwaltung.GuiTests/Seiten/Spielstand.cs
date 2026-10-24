using System.Text.RegularExpressions;

namespace Fussballturnierverwaltung.GuiTests.Seiten;

/// <summary>Ein Spiel, wie es der Spielplan zeigt. <see cref="Ergebnis"/> ist der Endstand einschließlich
/// Verlängerung („2:1“, „-:-“ wenn ungespielt), <see cref="Zusatz"/> der Entscheidungs- bzw. Halbzeithinweis
/// („(1:1) n.V.“, „i.E. 4:3“, „(1:0)“).</summary>
public sealed record SpielZeile(string Heim, string Ergebnis, string Zusatz, string Auswaerts, string Status)
{
    public bool IstGespielt => Spielstand.Tore(Ergebnis) is not null;

    /// <summary>Gewinner des Spiels, null bei Unentschieden oder ungespieltem Spiel.</summary>
    public string? Sieger => Spielstand.Sieger(this);

    /// <summary>Verlierer des Spiels, null bei Unentschieden oder ungespieltem Spiel.</summary>
    public string? Verlierer => Sieger is { } sieger ? (sieger == Heim ? Auswaerts : Heim) : null;
}

/// <summary>Liest Spielstände und Fortschritt aus den Anzeigetexten der App – zustandslos, damit es ohne laufende
/// App getestet werden kann.</summary>
public static partial class Spielstand
{
    /// <summary>„a:b“ → (a, b); null für „-:-“ oder Unlesbares.</summary>
    public static (int Heim, int Auswaerts)? Tore(string text) =>
        ToreMuster().Match(text.Trim()) is { Success: true } m
            ? (int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value))
            : null;

    /// <summary>Sieger nach Endstand; bei Gleichstand entscheidet ein Elfmeterschießen („i.E. x:y“).</summary>
    public static string? Sieger(SpielZeile spiel)
    {
        if (Tore(spiel.Ergebnis) is not var (heim, auswaerts)) return null;
        if (heim == auswaerts && ElfmeterMuster().Match(spiel.Zusatz) is { Success: true } elfmeter)
            (heim, auswaerts) = (int.Parse(elfmeter.Groups[1].Value), int.Parse(elfmeter.Groups[2].Value));
        return heim > auswaerts ? spiel.Heim : auswaerts > heim ? spiel.Auswaerts : null;
    }

    /// <summary>„Runde 2 von 4 · 12 / 16 Spiele gespielt“ → (12, 16).</summary>
    public static (int Gespielt, int Gesamt)? Fortschritt(string text) =>
        FortschrittMuster().Match(text) is { Success: true } m
            ? (int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value))
            : null;

    [GeneratedRegex(@"^(\d+):(\d+)$")]
    private static partial Regex ToreMuster();

    [GeneratedRegex(@"i\.E\.\s*(\d+):(\d+)")]
    private static partial Regex ElfmeterMuster();

    [GeneratedRegex(@"(\d+)\s*/\s*(\d+)\s+Spiele gespielt")]
    private static partial Regex FortschrittMuster();
}
