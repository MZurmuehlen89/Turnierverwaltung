using System.Text.RegularExpressions;

namespace Fussballturnierverwaltung.GuiTests.Seiten;

/// <summary>Ein Spiel, wie es der Spielplan zeigt. <see cref="Ergebnis"/> ist der Endstand einschließlich
/// Verlängerung („2:1“, „-:-“ wenn ungespielt), <see cref="Zusatz"/> der Entscheidungs- bzw. Halbzeithinweis
/// („(1:1) n.V.“, „i.E. 4:3“, „(1:0)“). <see cref="Kennung"/> ist die erste Spalte: in K.-o.-Runden „HF 1“, mit
/// Hin- und Rückspiel „HF 1 H“ bzw. „HF 1 R“, sonst der Spieltag („ST 3“).</summary>
public sealed record SpielZeile(string Heim, string Ergebnis, string Zusatz, string Auswaerts, string Status, string Kennung = "")
{
    public bool IstGespielt => Spielstand.Tore(Ergebnis) is not null;

    public bool IstRueckspiel => Kennung.EndsWith(" R", StringComparison.Ordinal);

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
        if (heim == auswaerts && Elfmeter(spiel.Zusatz) is { } elfmeter)
            (heim, auswaerts) = elfmeter;
        return heim > auswaerts ? spiel.Heim : auswaerts > heim ? spiel.Auswaerts : null;
    }

    /// <summary>Ordnet jedem Rückspiel sein Hinspiel zu (dieselben Teams mit getauschtem Heimrecht).</summary>
    public static IReadOnlyList<(SpielZeile Hin, SpielZeile Rueck)> HinUndRueckspiele(IReadOnlyList<SpielZeile> spiele)
    {
        var rueckspiele = spiele.Where(s => s.IstRueckspiel).ToList();
        if (rueckspiele.Count * 2 != spiele.Count)
            throw new InvalidOperationException(
                $"{spiele.Count} Spiele, davon {rueckspiele.Count} Rückspiele – nicht jedes Spiel hat sein Gegenstück.");
        return [.. rueckspiele.Select(r =>
            (spiele.Single(h => !h.IstRueckspiel && h.Heim == r.Auswaerts && h.Auswaerts == r.Heim), r))];
    }

    /// <summary>Wer eine Paarung aus Hin- und Rückspiel gewinnt: zuerst das Gesamtergebnis (Tore der Verlängerung
    /// zählen mit), bei Gleichstand mit <paramref name="auswaertstorregel"/> die mehr auswärts erzielten Tore,
    /// sonst das Elfmeterschießen nach dem Rückspiel. Null, wenn nichts davon entscheidet.</summary>
    public static string? Weiterkommender(SpielZeile hin, SpielZeile rueck, bool auswaertstorregel)
    {
        if (rueck.Heim != hin.Auswaerts || rueck.Auswaerts != hin.Heim)
            throw new ArgumentException($"„{rueck.Heim} – {rueck.Auswaerts}“ ist nicht das Rückspiel zu „{hin.Heim} – {hin.Auswaerts}“.");
        if (Tore(hin.Ergebnis) is not var (hinHeim, hinGast) || Tore(rueck.Ergebnis) is not var (rueckHeim, rueckGast))
            return null;

        // Aus Sicht des Hinspiels: A hat zuerst Heimrecht, B im Rückspiel.
        var (a, b) = (hin.Heim, hin.Auswaerts);
        int toreA = hinHeim + rueckGast, toreB = hinGast + rueckHeim;
        if (toreA != toreB) return toreA > toreB ? a : b;
        if (auswaertstorregel && rueckGast != hinGast) return rueckGast > hinGast ? a : b;
        return Elfmeter(rueck.Zusatz) is var (elfmeterB, elfmeterA) && elfmeterA != elfmeterB
            ? (elfmeterA > elfmeterB ? a : b)
            : null;
    }

    /// <summary>„… i.E. 4:3“ → (4, 3) aus Sicht des Heimteams; null ohne Elfmeterschießen.</summary>
    public static (int Heim, int Auswaerts)? Elfmeter(string zusatz) =>
        ElfmeterMuster().Match(zusatz) is { Success: true } m
            ? (int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value))
            : null;

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
