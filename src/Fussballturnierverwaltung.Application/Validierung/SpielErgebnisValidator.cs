using Fussballturnierverwaltung.Application.Abstraktionen;
using Fussballturnierverwaltung.Application.Spiele.Eingaben;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Validierung;

public class SpielErgebnisValidator
{
    public ValidationErgebnis Validieren(SpielErgebnisEingabe eingabe)
    {
        var fehler = new List<string>();

        RegulaereTorePruefen(eingabe, fehler);
        HalbzeitstandPruefen(eingabe, fehler);
        VerlaengerungPruefen(eingabe, fehler);
        ElfmeterschiessenPruefen(eingabe, fehler);
        SiegerBeteiligtPruefen(eingabe, fehler);
        KoHinspielPruefen(eingabe, fehler);
        SiegerZumTorverhaeltnisPruefen(eingabe, fehler);

        return fehler.Count == 0 ? ValidationErgebnis.Ok() : ValidationErgebnis.MitFehlern([.. fehler]);
    }

    private static void RegulaereTorePruefen(SpielErgebnisEingabe eingabe, List<string> fehler)
    {
        if (eingabe.HeimToreRegulaer < 0 || eingabe.AuswaertsToreRegulaer < 0)
            fehler.Add("Tore der regulären Spielzeit dürfen nicht negativ sein.");
    }

    private static void HalbzeitstandPruefen(SpielErgebnisEingabe eingabe, List<string> fehler)
    {
        // Halbzeitstand ist optional (Issue #104), aber wenn angegeben nur für beide Seiten zusammen
        // und muss zum Regulärzeit-Endstand passen (kann bis zur Halbzeit nicht mehr Tore haben als
        // am Ende der regulären Spielzeit).
        if (eingabe.HalbzeitHeimTore.HasValue != eingabe.HalbzeitAuswaertsTore.HasValue)
            fehler.Add("Der Halbzeitstand muss für beide Mannschaften zusammen angegeben werden.");
        else if (eingabe.HalbzeitHeimTore.HasValue && eingabe.HalbzeitAuswaertsTore.HasValue)
        {
            if (eingabe.HalbzeitHeimTore < 0 || eingabe.HalbzeitAuswaertsTore < 0)
                fehler.Add("Der Halbzeitstand darf nicht negativ sein.");
            else if (eingabe.HalbzeitHeimTore > eingabe.HeimToreRegulaer ||
                     eingabe.HalbzeitAuswaertsTore > eingabe.AuswaertsToreRegulaer)
                fehler.Add("Der Halbzeitstand darf den Stand der regulären Spielzeit nicht überschreiten.");
        }
    }

    private static void VerlaengerungPruefen(SpielErgebnisEingabe eingabe, List<string> fehler)
    {
        // Elfmeterschiessen kann direkt nach Regulärzeit folgen → keine Verlängerungstore erforderlich
        var brauchtVerlaengerung = eingabe.EntscheidungArt is
            EntscheidungArt.Verlaengerung or EntscheidungArt.GoldenGoal or EntscheidungArt.SilverGoal;

        if (brauchtVerlaengerung)
        {
            if (!eingabe.HeimToreVerlaengerung.HasValue || !eingabe.AuswaertsToreVerlaengerung.HasValue)
                fehler.Add("Bei Verlängerung müssen Verlängerungstore für beide Mannschaften angegeben werden.");
            else if (eingabe.HeimToreVerlaengerung < 0 || eingabe.AuswaertsToreVerlaengerung < 0)
                fehler.Add("Verlängerungstore dürfen nicht negativ sein.");
        }
    }

    private static void ElfmeterschiessenPruefen(SpielErgebnisEingabe eingabe, List<string> fehler)
    {
        if (eingabe.EntscheidungArt == EntscheidungArt.Elfmeterschiessen)
        {
            if (!eingabe.HeimElfmetertore.HasValue || !eingabe.AuswaertsElfmetertore.HasValue)
                fehler.Add("Bei Elfmeterschießen müssen die Elfmetertore für beide Mannschaften angegeben werden.");
            else
            {
                if (eingabe.HeimElfmetertore < 0 || eingabe.AuswaertsElfmetertore < 0)
                    fehler.Add("Elfmetertore dürfen nicht negativ sein.");

                if (eingabe.HeimElfmetertore == eingabe.AuswaertsElfmetertore)
                    fehler.Add("Beim Elfmeterschießen muss es einen klaren Sieger geben – Gleichstand ist nicht möglich.");
            }
        }
    }

    private static void SiegerBeteiligtPruefen(SpielErgebnisEingabe eingabe, List<string> fehler)
    {
        // SiegerMannschaft muss eine der beiden beteiligten Mannschaften sein
        if (eingabe.SiegerMannschaftId.HasValue &&
            eingabe.SiegerMannschaftId != eingabe.HeimMannschaftId &&
            eingabe.SiegerMannschaftId != eingabe.AuswaertsMannschaftId)
            fehler.Add("Der Sieger muss eine der beiden beteiligten Mannschaften sein.");
    }

    private static void KoHinspielPruefen(SpielErgebnisEingabe eingabe, List<string> fehler)
    {
        // Issue #346: ein K.-o.-Hinspiel entscheidet für sich allein nichts — Verlängerung/Elfmeterschießen
        // gibt es erst im Rückspiel, abhängig vom Gesamtergebnis beider Spiele.
        if (eingabe.IstKoHinspiel && eingabe.EntscheidungArt != EntscheidungArt.RegulaereSpielzeit)
            fehler.Add("Im Hinspiel gibt es keine Verlängerung und kein Elfmeterschießen – die Entscheidung fällt erst im Rückspiel.");
    }

    private static void SiegerZumTorverhaeltnisPruefen(SpielErgebnisEingabe eingabe, List<string> fehler)
    {
        // Bei regulärer Spielzeit: Sieger aus Torverhältnis ableiten und prüfen — nicht bei einem
        // K.-o.-Rückspiel (IstGesamtwertung), dort kann der Sieger aus dem Gesamtergebnis beider Spiele
        // auch der Verlierer bzw. ein Remis-Partner des einzelnen Rückspiels sein (Issue #346).
        if (eingabe.EntscheidungArt == EntscheidungArt.RegulaereSpielzeit && !eingabe.IstGesamtwertung)
        {
            if (eingabe.HeimToreRegulaer == eingabe.AuswaertsToreRegulaer && eingabe.SiegerMannschaftId.HasValue)
                fehler.Add("Bei Unentschieden in regulärer Spielzeit kann es keinen Sieger geben.");

            if (eingabe.HeimToreRegulaer > eingabe.AuswaertsToreRegulaer &&
                eingabe.SiegerMannschaftId.HasValue &&
                eingabe.SiegerMannschaftId != eingabe.HeimMannschaftId)
                fehler.Add("Der Sieger entspricht nicht dem Torverhältnis der regulären Spielzeit.");

            if (eingabe.AuswaertsToreRegulaer > eingabe.HeimToreRegulaer &&
                eingabe.SiegerMannschaftId.HasValue &&
                eingabe.SiegerMannschaftId != eingabe.AuswaertsMannschaftId)
                fehler.Add("Der Sieger entspricht nicht dem Torverhältnis der regulären Spielzeit.");
        }
    }
}
