using Fussballturnierverwaltung.Application.Abstraktionen;
using Fussballturnierverwaltung.Application.Stammdaten.Eingaben;

namespace Fussballturnierverwaltung.Application.Validierung;

/// <summary>Feldbezug der Meldungen (#602) = Name der Eigenschaft in der Eingabe.</summary>
public class VerbandValidator
{
    public ValidationErgebnis Validieren(VerbandErstellenEingabe eingabe)
    {
        var fehler = new List<Feldfehler>();
        if (string.IsNullOrWhiteSpace(eingabe.Name))
            fehler.Add(new("Verbandsname darf nicht leer sein.", nameof(eingabe.Name)));
        else if (eingabe.Name.Trim().Length > 200)
            fehler.Add(new("Verbandsname darf maximal 200 Zeichen haben.", nameof(eingabe.Name)));
        KurznamePruefen(eingabe.Kurzname, fehler);
        BildValidierung.GroessePruefen(eingabe.Logo, nameof(eingabe.Logo), fehler);
        return ValidationErgebnis.Aus(fehler);
    }

    public ValidationErgebnis Validieren(VerbandAktualisierenEingabe eingabe)
    {
        var fehler = new List<Feldfehler>();
        if (string.IsNullOrWhiteSpace(eingabe.Name))
            fehler.Add(new("Verbandsname darf nicht leer sein.", nameof(eingabe.Name)));
        KurznamePruefen(eingabe.Kurzname, fehler);
        BildValidierung.GroessePruefen(eingabe.Logo, nameof(eingabe.Logo), fehler);
        return ValidationErgebnis.Aus(fehler);
    }

    private static void KurznamePruefen(string kurzname, List<Feldfehler> fehler)
    {
        if (string.IsNullOrWhiteSpace(kurzname))
            fehler.Add(new("Kurzname darf nicht leer sein.", nameof(VerbandErstellenEingabe.Kurzname)));
    }
}
