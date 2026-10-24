using Fussballturnierverwaltung.Application.Abstraktionen;
using Fussballturnierverwaltung.Application.Spiele.Eingaben;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Validierung;

public class SpielErgebnisValidator
{
    public ValidationErgebnis Validieren(SpielErgebnisEingabe eingabe)
    {
        string[] fehler =
        [
            .. RegulaereTorePruefen(eingabe),
            .. HalbzeitstandPruefen(eingabe),
            .. VerlaengerungPruefen(eingabe),
            .. ElfmeterschiessenPruefen(eingabe),
            .. SiegerBeteiligtPruefen(eingabe),
            .. KoHinspielPruefen(eingabe),
            .. SiegerZumTorverhaeltnisPruefen(eingabe),
        ];

        return fehler.Length == 0 ? ValidationErgebnis.Ok() : ValidationErgebnis.MitFehlern(fehler);
    }

    private static IEnumerable<string> RegulaereTorePruefen(SpielErgebnisEingabe eingabe)
    {
        if (eingabe.HeimToreRegulaer < 0 || eingabe.AuswaertsToreRegulaer < 0)
            yield return "Tore der regulären Spielzeit dürfen nicht negativ sein.";
    }

    /// <summary>Der Halbzeitstand ist optional (Issue #104), wenn angegeben aber nur für beide Seiten zusammen und
    /// höchstens so hoch wie der Stand nach der regulären Spielzeit.</summary>
    private static IEnumerable<string> HalbzeitstandPruefen(SpielErgebnisEingabe eingabe)
    {
        if (eingabe.HalbzeitHeimTore.HasValue != eingabe.HalbzeitAuswaertsTore.HasValue)
        {
            yield return "Der Halbzeitstand muss für beide Mannschaften zusammen angegeben werden.";
            yield break;
        }

        if (eingabe.HalbzeitHeimTore is not { } heim || eingabe.HalbzeitAuswaertsTore is not { } auswaerts)
            yield break;

        if (heim < 0 || auswaerts < 0)
            yield return "Der Halbzeitstand darf nicht negativ sein.";
        else if (heim > eingabe.HeimToreRegulaer || auswaerts > eingabe.AuswaertsToreRegulaer)
            yield return "Der Halbzeitstand darf den Stand der regulären Spielzeit nicht überschreiten.";
    }

    /// <summary>Verlängerungstore nur bei Verlängerung/Golden/Silver Goal — ein Elfmeterschießen kann direkt auf
    /// die reguläre Spielzeit folgen.</summary>
    private static IEnumerable<string> VerlaengerungPruefen(SpielErgebnisEingabe eingabe)
    {
        if (eingabe.EntscheidungArt is not
            (EntscheidungArt.Verlaengerung or EntscheidungArt.GoldenGoal or EntscheidungArt.SilverGoal))
            yield break;

        if (eingabe.HeimToreVerlaengerung is not { } heim || eingabe.AuswaertsToreVerlaengerung is not { } auswaerts)
            yield return "Bei Verlängerung müssen Verlängerungstore für beide Mannschaften angegeben werden.";
        else if (heim < 0 || auswaerts < 0)
            yield return "Verlängerungstore dürfen nicht negativ sein.";
    }

    private static IEnumerable<string> ElfmeterschiessenPruefen(SpielErgebnisEingabe eingabe)
    {
        if (eingabe.EntscheidungArt != EntscheidungArt.Elfmeterschiessen)
            yield break;

        if (eingabe.HeimElfmetertore is not { } heim || eingabe.AuswaertsElfmetertore is not { } auswaerts)
        {
            yield return "Bei Elfmeterschießen müssen die Elfmetertore für beide Mannschaften angegeben werden.";
            yield break;
        }

        if (heim < 0 || auswaerts < 0)
            yield return "Elfmetertore dürfen nicht negativ sein.";

        if (heim == auswaerts)
            yield return "Beim Elfmeterschießen muss es einen klaren Sieger geben – Gleichstand ist nicht möglich.";
    }

    private static IEnumerable<string> SiegerBeteiligtPruefen(SpielErgebnisEingabe eingabe)
    {
        if (eingabe.SiegerMannschaftId is { } sieger &&
            sieger != eingabe.HeimMannschaftId && sieger != eingabe.AuswaertsMannschaftId)
            yield return "Der Sieger muss eine der beiden beteiligten Mannschaften sein.";
    }

    /// <summary>Issue #346: ein K.-o.-Hinspiel entscheidet für sich allein nichts — Verlängerung/Elfmeterschießen
    /// gibt es erst im Rückspiel, abhängig vom Gesamtergebnis beider Spiele.</summary>
    private static IEnumerable<string> KoHinspielPruefen(SpielErgebnisEingabe eingabe)
    {
        if (eingabe.IstKoHinspiel && eingabe.EntscheidungArt != EntscheidungArt.RegulaereSpielzeit)
            yield return "Im Hinspiel gibt es keine Verlängerung und kein Elfmeterschießen – die Entscheidung fällt erst im Rückspiel.";
    }

    /// <summary>Bei regulärer Spielzeit muss ein angegebener Sieger zum Torverhältnis passen — nicht bei einem
    /// K.-o.-Rückspiel (IstGesamtwertung), dort kann der Sieger aus dem Gesamtergebnis beider Spiele auch der
    /// Verlierer bzw. ein Remis-Partner des einzelnen Rückspiels sein (Issue #346).</summary>
    private static IEnumerable<string> SiegerZumTorverhaeltnisPruefen(SpielErgebnisEingabe eingabe)
    {
        if (eingabe.EntscheidungArt != EntscheidungArt.RegulaereSpielzeit || eingabe.IstGesamtwertung ||
            eingabe.SiegerMannschaftId is not { } sieger)
            yield break;

        if (SiegerNachToren(eingabe) is not { } erwarteterSieger)
            yield return "Bei Unentschieden in regulärer Spielzeit kann es keinen Sieger geben.";
        else if (sieger != erwarteterSieger)
            yield return "Der Sieger entspricht nicht dem Torverhältnis der regulären Spielzeit.";
    }

    /// <summary>Sieger nach den Toren der regulären Spielzeit, <c>null</c> bei Unentschieden.</summary>
    private static int? SiegerNachToren(SpielErgebnisEingabe eingabe) =>
        eingabe.HeimToreRegulaer.CompareTo(eingabe.AuswaertsToreRegulaer) switch
        {
            > 0 => eingabe.HeimMannschaftId,
            < 0 => eingabe.AuswaertsMannschaftId,
            _   => null
        };
}
