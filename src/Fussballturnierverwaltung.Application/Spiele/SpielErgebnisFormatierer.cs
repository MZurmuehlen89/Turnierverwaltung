using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Spiele;

/// <summary>
/// Formatiert das Entscheidungsart-Kuerzel eines KO-Ergebnisses nach gaengiger
/// Sportportal-Konvention: Regulaerzeit-Stand nur in Klammern, wenn er vom
/// (bereits kumulierten) Endstand abweicht, gefolgt vom Entscheidungs-Kuerzel.
/// </summary>
public static class SpielErgebnisFormatierer
{
    /// <param name="ausHeimSicht"><c>false</c> stellt die Stände aus Sicht des Auswärtsteams dar (Mannschaftsseite).</param>
    public static string DetailText(SpielErgebnis ergebnis, bool ausHeimSicht = true)
    {
        var art = ergebnis.EntscheidungArt;
        if (art == EntscheidungArt.Auswaertstorregel) return EntscheidungKuerzel(art);

        bool istVerlaengerungsFall = art is
            EntscheidungArt.Verlaengerung or EntscheidungArt.GoldenGoal or
            EntscheidungArt.SilverGoal or EntscheidungArt.Elfmeterschiessen or
            EntscheidungArt.Losentscheid or EntscheidungArt.Muenzwurf;
        if (!istVerlaengerungsFall) return "";

        // Die Regulärzeit-Klammer nur, wenn der Endstand durch Verlängerungstore von ihr abweicht.
        bool verlaengerungstoreGefallen =
            (ergebnis.HeimToreVerlaengerung ?? 0) != 0 || (ergebnis.AuswaertsToreVerlaengerung ?? 0) != 0;
        string regulaerKlammer = verlaengerungstoreGefallen
            ? $"({Stand(ergebnis.HeimToreRegulaer, ergebnis.AuswaertsToreRegulaer, ausHeimSicht)}) "
            : "";
        string elfmeterStand = art == EntscheidungArt.Elfmeterschiessen
            ? " " + Stand(ergebnis.HeimElfmetertore, ergebnis.AuswaertsElfmetertore, ausHeimSicht)
            : "";

        return $"{regulaerKlammer}{LosMitVerlaengerung(ergebnis)}{elfmeterStand}";
    }

    /// <summary>#391: Beim Los nach einer Verlängerung nennt das Kürzel beides („n.V., Los“), sonst nur das
    /// Kürzel der Entscheidung.</summary>
    private static string LosMitVerlaengerung(SpielErgebnis ergebnis) =>
        ergebnis.EntscheidungArt is EntscheidungArt.Losentscheid or EntscheidungArt.Muenzwurf
        && ergebnis.HeimToreVerlaengerung is not null
            ? $"{EntscheidungKuerzel(EntscheidungArt.Verlaengerung)}, {EntscheidungKuerzel(ergebnis.EntscheidungArt)}"
            : EntscheidungKuerzel(ergebnis.EntscheidungArt);

    private static string Stand(int? heim, int? auswaerts, bool ausHeimSicht) =>
        ausHeimSicht ? $"{heim}:{auswaerts}" : $"{auswaerts}:{heim}";

    /// <summary>Reines Kürzel der Entscheidung ohne Spielstand, für die kompakte Karte der Turnierbaum-Gesamtübersicht
    /// (Issue #238). Leer bei Entscheidung nach Toren.</summary>
    public static string EntscheidungKuerzel(EntscheidungArt art) => art switch
    {
        EntscheidungArt.Verlaengerung     => "n.V.",
        EntscheidungArt.Elfmeterschiessen => "i.E.",
        EntscheidungArt.Auswaertstorregel => "A.T.",
        EntscheidungArt.GoldenGoal        => "G.G.",
        EntscheidungArt.SilverGoal        => "S.G.",
        EntscheidungArt.Losentscheid      => "Los",
        EntscheidungArt.Muenzwurf         => "Münzwurf",
        _                                 => ""
    };

    /// <summary>
    /// Kurzform der Entscheidung einer Hin-/Rückspiel-Paarung aus Sicht des Rückspiels (Issue #340) — anders
    /// als <see cref="DetailText"/> ohne Regulärzeit-Klammer, da die im Gesamtkontext nichts aussagt.
    /// Leer bei Entscheidung nach Toren.
    /// </summary>
    public static string PaarungsEntscheidung(SpielErgebnis rueckspiel) => rueckspiel.EntscheidungArt switch
    {
        EntscheidungArt.Auswaertstorregel => "A.T.",
        EntscheidungArt.Elfmeterschiessen => $"i.E. {rueckspiel.HeimElfmetertore}:{rueckspiel.AuswaertsElfmetertore}",
        EntscheidungArt.Verlaengerung     => "n.V.",
        EntscheidungArt.GoldenGoal        => "G.G.",
        EntscheidungArt.SilverGoal        => "S.G.",
        EntscheidungArt.Losentscheid or EntscheidungArt.Muenzwurf => LosMitVerlaengerung(rueckspiel),
        _                                 => ""
    };

    /// <summary>
    /// Gesamt-Zeile unter dem Rückspiel im Spielplan (Issue #340): Gesamtergebnis aus Sicht des
    /// Rückspiel-Heimteams plus ggf. Entscheidungs-Kürzel, z.B. "Gesamt 3:3 · i.E. 2:4".
    /// </summary>
    public static string RueckspielGesamtText(SpielErgebnis hinspiel, SpielErgebnis rueckspiel)
    {
        var (teamA, teamB) = HinRueckspielWertung.GesamtTore(hinspiel, rueckspiel);
        string entscheidung = PaarungsEntscheidung(rueckspiel);
        // Rückspiel-Heimteam = Team B (Hinspiel-Auswärtsteam).
        return entscheidung.Length == 0 ? $"Gesamt {teamB}:{teamA}" : $"Gesamt {teamB}:{teamA} · {entscheidung}";
    }

    /// <summary>
    /// Fußzeile einer Turnierbaum-Karte für eine Hin-/Rückspiel-Paarung (Issue #340): beide Einzelergebnisse
    /// jeweils aus Sicht ihres Heimteams, z.B. "Hin 1:0 · Rück 3:2" — die Entscheidung steht separat
    /// (<see cref="PaarungsEntscheidung"/>) zwischen den Gesamt-Scores. Ohne Rückspiel-Ergebnis nur der
    /// Hinweis, dass der Stand nach dem Hinspiel gezeigt wird.
    /// </summary>
    public static string PaarungsEinzelergebnisse(SpielErgebnis hinspiel, SpielErgebnis? rueckspiel) =>
        rueckspiel is null
            ? "nach Hinspiel"
            : $"Hin {hinspiel.HeimToreGesamt}:{hinspiel.AuswaertsToreGesamt} · " +
              $"Rück {rueckspiel.HeimToreGesamt}:{rueckspiel.AuswaertsToreGesamt}";

    /// <summary>
    /// Formatiert den Halbzeitstand in Klammern (z.B. "(1:0)") für die Ergebnisanzeige, analog zur
    /// gängigen Sportportal-Konvention "2:1 (1:0)" (Issue #104). Nur bei reinen Regulärzeit-Spielen —
    /// bei Verlängerung/Elfmeterschießen/Auswärtstorregel steht in derselben Anzeige-Position bereits
    /// das <see cref="DetailText"/>-Kürzel. Leer, wenn kein Halbzeitstand erfasst wurde oder er dem
    /// Regulärzeit-Endstand entspricht (sonst wäre die Klammer redundant).
    /// </summary>
    public static string HalbzeitKlammer(
        EntscheidungArt entscheidungArt,
        int heimToreRegulaer, int auswaertsToreRegulaer,
        int? halbzeitHeimTore, int? halbzeitAuswaertsTore)
    {
        if (entscheidungArt != EntscheidungArt.RegulaereSpielzeit) return "";
        if (halbzeitHeimTore is null || halbzeitAuswaertsTore is null) return "";
        if (halbzeitHeimTore == heimToreRegulaer && halbzeitAuswaertsTore == auswaertsToreRegulaer) return "";

        return $"({halbzeitHeimTore}:{halbzeitAuswaertsTore})";
    }
}
