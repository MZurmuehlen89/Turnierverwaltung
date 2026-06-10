using Fussballturnierverwaltung.Application.Abstraktionen;
using Fussballturnierverwaltung.Application.Stammdaten.Eingaben;

namespace Fussballturnierverwaltung.Application.Validierung;

public class LandValidator
{
    public ValidationErgebnis Validieren(LandErstellenEingabe eingabe)
    {
        var fehler = new List<string>();
        NamePruefen(eingabe.Name, fehler);
        KurznamePruefen(eingabe.Kurzname, fehler);
        FifaCodePruefen(eingabe.FifaCode, fehler);
        if (eingabe.VerbandId <= 0)
            fehler.Add("Es muss ein Verband ausgewählt werden.");
        return Ergebnis(fehler);
    }

    public ValidationErgebnis Validieren(LandAktualisierenEingabe eingabe)
    {
        var fehler = new List<string>();
        NamePruefen(eingabe.Name, fehler);
        KurznamePruefen(eingabe.Kurzname, fehler);
        FifaCodePruefen(eingabe.FifaCode, fehler);
        if (eingabe.VerbandId <= 0)
            fehler.Add("Es muss ein Verband ausgewählt werden.");
        return Ergebnis(fehler);
    }

    private static void NamePruefen(string name, List<string> fehler)
    {
        if (string.IsNullOrWhiteSpace(name))
            fehler.Add("Ländername darf nicht leer sein.");
        else if (name.Trim().Length > 200)
            fehler.Add("Ländername darf maximal 200 Zeichen haben.");
    }

    private static void KurznamePruefen(string kurzname, List<string> fehler)
    {
        if (string.IsNullOrWhiteSpace(kurzname))
            fehler.Add("Kurzname darf nicht leer sein.");
    }

    private static void FifaCodePruefen(string fifaCode, List<string> fehler)
    {
        if (string.IsNullOrWhiteSpace(fifaCode))
            fehler.Add("FIFA-Code darf nicht leer sein.");
        else if (fifaCode.Trim().Length > 3)
            fehler.Add("FIFA-Code darf maximal 3 Zeichen haben.");
    }

    private static ValidationErgebnis Ergebnis(List<string> fehler) =>
        fehler.Count == 0 ? ValidationErgebnis.Ok() : ValidationErgebnis.MitFehlern([.. fehler]);
}
