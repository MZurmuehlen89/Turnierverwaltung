using Fussballturnierverwaltung.Application.Abstraktionen;
using Fussballturnierverwaltung.Application.Stammdaten.Eingaben;

namespace Fussballturnierverwaltung.Application.Validierung;

public class VerbandValidator
{
    public ValidationErgebnis Validieren(VerbandErstellenEingabe eingabe)
    {
        var fehler = new List<string>();
        if (string.IsNullOrWhiteSpace(eingabe.Name))
            fehler.Add("Verbandsname darf nicht leer sein.");
        else if (eingabe.Name.Trim().Length > 200)
            fehler.Add("Verbandsname darf maximal 200 Zeichen haben.");
        if (string.IsNullOrWhiteSpace(eingabe.Kurzname))
            fehler.Add("Kurzname darf nicht leer sein.");
        BildValidierung.GroessePruefen(eingabe.Logo, fehler);
        return Ergebnis(fehler);
    }

    public ValidationErgebnis Validieren(VerbandAktualisierenEingabe eingabe)
    {
        var fehler = new List<string>();
        if (string.IsNullOrWhiteSpace(eingabe.Name))
            fehler.Add("Verbandsname darf nicht leer sein.");
        if (string.IsNullOrWhiteSpace(eingabe.Kurzname))
            fehler.Add("Kurzname darf nicht leer sein.");
        BildValidierung.GroessePruefen(eingabe.Logo, fehler);
        return Ergebnis(fehler);
    }

    private static ValidationErgebnis Ergebnis(List<string> fehler) =>
        fehler.Count == 0 ? ValidationErgebnis.Ok() : ValidationErgebnis.MitFehlern([.. fehler]);
}
