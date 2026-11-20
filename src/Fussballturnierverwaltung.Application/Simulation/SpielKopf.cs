using Fussballturnierverwaltung.Application.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Simulation;

/// <summary>Issue #341: Spielkopf nach kicker.de-Vorbild — Kontextzeile (Turnier/Runde bzw. Hinspiel/
/// Rückspiel), große Wappen mit Name und Untertitel, Ergebnis-Box mit Entscheidungsart und
/// Zwischenständen. Gemeinsam genutzt von Spielbericht-Karte und Hinspiel-/Rückspiel-Karten
/// (SpielKopfControl).</summary>
public sealed record SpielKopf(
    string? Kontext,
    string HeimName, byte[]? HeimWappen, string? HeimUntertitel, string HeimKuerzel,
    string AuswaertsName, byte[]? AuswaertsWappen, string? AuswaertsUntertitel, string AuswaertsKuerzel,
    string Score, string? Entscheidung, string? Zwischenstaende)
{
    public bool HatKontext             => Kontext is not null;
    public bool HatHeimUntertitel      => HeimUntertitel is not null;
    public bool HatAuswaertsUntertitel => AuswaertsUntertitel is not null;
    public bool HatEntscheidung        => Entscheidung is not null;
    public bool HatZwischenstaende     => Zwischenstaende is not null;
}


/// <summary>Baut den Spielkopf (#302, #341; #509: aus SimulationsProtokollHelfer verschoben).</summary>
public static class SpielKopfBau
{
    // ── Spielkopf (Issue #341) ────────────────────────────────────────────────

    /// <summary>Baut den Spielkopf aus einem Ergebnis; <paramref name="untertitel"/> liefert Verband und Liga für die
    /// Untertitel (#384).</summary>
    public static SpielKopf SpielKopfErstellen(
        string? kontext,
        Mannschaft? heim, string heimName,
        Mannschaft? auswaerts, string auswaertsName,
        SimulationsErgebnis e,
        UntertitelQuellen untertitel)
    {
        // Wie bei kicker: nach Elfmeterschießen steht der Elfmeterstand groß in der Box, das Ergebnis
        // nach Verlängerung wandert in die Zwischenstände.
        bool imElfmeterschiessen = e is { EntscheidungArt: EntscheidungArt.Elfmeterschiessen, HeimElfmetertore: not null, AuswaertsElfmetertore: not null };
        string score = imElfmeterschiessen
            ? $"{e.HeimElfmetertore}:{e.AuswaertsElfmetertore}"
            : $"{e.HeimToreRegulaer + (e.HeimToreVerlaengerung ?? 0)}:{e.AuswaertsToreRegulaer + (e.AuswaertsToreVerlaengerung ?? 0)}";

        string? entscheidung = e.EntscheidungArt switch
        {
            EntscheidungArt.Elfmeterschiessen => "im Elfmeterschießen",
            EntscheidungArt.Verlaengerung     => "nach Verlängerung",
            EntscheidungArt.GoldenGoal        => "nach Golden Goal",
            EntscheidungArt.SilverGoal        => "nach Silver Goal",
            EntscheidungArt.Auswaertstorregel => "nach Auswärtstorregel",
            EntscheidungArt.Losentscheid      => "per Losentscheid",
            EntscheidungArt.Muenzwurf         => "per Münzwurf",
            EntscheidungArt.Wiederholungsspiel => "nach Wiederholungsspiel",
            _                                 => null
        };

        // Issue #383: bei Los/Münze zeigt der (ausgeglichene) Spielstand keinen Sieger — daher ausschreiben.
        if (e.LosGewinnerHeim is bool losFuerHeim)
            entscheidung += $" für {(losFuerHeim ? heimName : auswaertsName)}";

        return new SpielKopf(
            kontext,
            heimName, MannschaftsIcon.Fuer(heim), SpielKopfUntertitel.Fuer(heim, auswaerts, untertitel), Kuerzel(heim, heimName),
            auswaertsName, MannschaftsIcon.Fuer(auswaerts), SpielKopfUntertitel.Fuer(auswaerts, heim, untertitel), Kuerzel(auswaerts, auswaertsName),
            score, entscheidung, ZwischenstandText(e));
    }

    /// <summary>Gesamtkopf einer Hin-/Rückspiel-Paarung aus Sicht des Hinspiel-Heimteams — Summenergebnis
    /// in der Box, <paramref name="zusatz"/> ("Ergebnis nach Hinspiel", "nach 180 Minuten", …) darunter;
    /// ohne Zwischenstands-Streifen, die Einzelergebnisse stehen in den Karten darüber.</summary>
    public static SpielKopf GesamtKopfErstellen(
        Mannschaft heim, Mannschaft auswaerts, string score, string zusatz,
        UntertitelQuellen untertitel) =>
        new("Gesamt",
            heim.Name, MannschaftsIcon.Fuer(heim), SpielKopfUntertitel.Fuer(heim, auswaerts, untertitel), Kuerzel(heim, heim.Name),
            auswaerts.Name, MannschaftsIcon.Fuer(auswaerts), SpielKopfUntertitel.Fuer(auswaerts, heim, untertitel), Kuerzel(auswaerts, auswaerts.Name),
            score, zusatz, null);

    // Zwischenstände ("HZ 1:0 · 90' 1:1 · n.V. 2:1"). Der Halbzeitstand fehlt bei manuell eingegebenen
    // Ergebnissen — ohne jeden Zwischenstand liefert die Methode null.
    private static string? ZwischenstandText(SimulationsErgebnis e)
    {
        var teile = new List<string>();
        if (e is { HalbzeitHeimTore: int hzHeim, HalbzeitAuswaertsTore: int hzGast })
            teile.Add($"HZ {hzHeim}:{hzGast}");
        bool hatVerlaengerung = e is { HeimToreVerlaengerung: not null, AuswaertsToreVerlaengerung: not null };
        if (hatVerlaengerung || e.EntscheidungArt == EntscheidungArt.Elfmeterschiessen)
            teile.Add($"90' {e.HeimToreRegulaer}:{e.AuswaertsToreRegulaer}");
        if (hatVerlaengerung)
            teile.Add($"n.V. {e.HeimToreRegulaer + e.HeimToreVerlaengerung}:{e.AuswaertsToreRegulaer + e.AuswaertsToreVerlaengerung}");
        if (e.Wiederholung is { } wdh)
            teile.Add($"Wdh. {wdh.HeimGesamt}:{wdh.AuswaertsGesamt}{(wdh.HeimToreVerlaengerung is null ? "" : " n.V.")}");
        return teile.Count > 0 ? string.Join(" · ", teile) : null;
    }

    // Issue #341: Team-Kürzel am Zeitstrahl — Kurzname, sonst die ersten drei Buchstaben des Namens
    // (auch für Platzhalter-Teams ohne Mannschaft, z. B. "Sieger Gruppe A").
    public static string Kuerzel(Mannschaft? mannschaft, string ersatzName)
    {
        if (!string.IsNullOrWhiteSpace(mannschaft?.Kurzname)) return mannschaft.Kurzname;
        string name = mannschaft?.Name ?? ersatzName;
        return name[..Math.Min(3, name.Length)].ToUpperInvariant();
    }

}
